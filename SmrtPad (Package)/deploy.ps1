<#
.SYNOPSIS
    Builds SmrtPad and deploys it locally (loose registration) or to a remote UI-test machine (signed .msix via WinRM).

.DESCRIPTION
    Remote mode: builds a signed Release .msix, copies it to the remote machine via WinRM,
    trusts the signing certificate, and installs with Add-AppxPackage -Path (works in any session).
    Local mode: registers the Debug loose-layout directly.
#>

param(
    [string]$RemoteHost,
    [string]$RemoteUser,
    [string]$RemotePassword,
    [string]$RemoteShareRoot
)

$ErrorActionPreference = 'Stop'
$configuration = if ([string]::IsNullOrWhiteSpace($RemoteHost)) { 'Debug' } else { 'Release' }

function New-OptionalCredential {
    param(
        [string]$UserName,
        [string]$Password
    )

    if ([string]::IsNullOrWhiteSpace($UserName) -or [string]::IsNullOrWhiteSpace($Password)) {
        return $null
    }

    $securePassword = ConvertTo-SecureString $Password -AsPlainText -Force
    return [pscredential]::new($UserName, $securePassword)
}

function Get-AppUserModelId {
    param(
        [string]$PackageName
    )

    $package = Get-AppxPackage -Name $PackageName | Sort-Object Version -Descending | Select-Object -First 1
    if ($null -eq $package) {
        throw "Package '$PackageName' is not registered."
    }

    return "$($package.PackageFamilyName)!App"
}

# Ensures a self-signed PFX exists for the given publisher subject and returns its path.
# The cert is stored in the script directory and reused across runs.
function Get-OrCreateSigningCertificate {
    param(
        [string]$Subject,
        [string]$OutputDir
    )

    $pfxPath = Join-Path $OutputDir 'SmrtPadTestSign.pfx'
    $cerPath = Join-Path $OutputDir 'SmrtPadTestSign.cer'

    if (-not (Test-Path $pfxPath)) {
        Write-Host "  Generating self-signed certificate for '$Subject'..." -ForegroundColor DarkGray
        $cert = New-SelfSignedCertificate `
            -Subject $Subject `
            -Type CodeSigningCert `
            -CertStoreLocation 'Cert:\CurrentUser\My' `
            -HashAlgorithm SHA256 `
            -NotAfter (Get-Date).AddYears(5)

        $pfxPassword = [System.Security.SecureString]::new()
        Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pfxPassword | Out-Null
        Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT | Out-Null
        Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force -ErrorAction SilentlyContinue
        Write-Host "  Certificate created: $pfxPath" -ForegroundColor DarkGray
    }

    return [pscustomobject]@{ PfxPath = $pfxPath; CerPath = $cerPath }
}

# Installs the signing cert into TrustedPeople and Root stores on the remote machine.
function Install-RemoteTrustCertificate {
    param(
        [System.Management.Automation.Runspaces.PSSession]$Session,
        [string]$CerPath
    )

    $cerBytes = [System.IO.File]::ReadAllBytes($CerPath)

    Invoke-Command -Session $Session -ScriptBlock {
        param([byte[]]$CertBytes)

        $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($CertBytes)

        $stores = @('TrustedPeople', 'Root')
        foreach ($storeName in $stores) {
            $store = [System.Security.Cryptography.X509Certificates.X509Store]::new(
                $storeName,
                [System.Security.Cryptography.X509Certificates.StoreLocation]::LocalMachine)
            $store.Open([System.Security.Cryptography.X509Certificates.OpenFlags]::ReadWrite)
            if (-not ($store.Certificates | Where-Object { $_.Thumbprint -eq $cert.Thumbprint })) {
                $store.Add($cert)
                Write-Host "  Trusted cert in LocalMachine\$storeName ($($cert.Thumbprint))" -ForegroundColor DarkGray
            }
            $store.Close()
        }
    } -ArgumentList (,$cerBytes)
}

# Installs the .msix and its x64 dependencies on the remote machine via a scheduled task
# running under the interactive user's token.  Add-AppxPackage fails with 0x80070005 when
# called directly from a WinRM session because WinRM tokens are non-interactive; the
# scheduled-task approach mirrors what MarkUp Markdown Editor's UI-test harness uses.
function Install-RemoteMsix {
    param(
        [System.Management.Automation.Runspaces.PSSession]$Session,
        [string]$RemoteRootPath,
        [string]$MsixFileName,
        [string]$RemoteUserName
    )

    Invoke-Command -Session $Session -ScriptBlock {
        param([string]$RootPath, [string]$MsixFile, [string]$UserName)

        $ErrorActionPreference = 'Stop'

        $msixPath = Join-Path $RootPath $MsixFile

        $depPaths = @()
        $depRoot = Join-Path $RootPath 'Dependencies'
        if (Test-Path $depRoot) {
            $depPaths = @(Get-ChildItem $depRoot -Recurse -Include *.appx, *.msix |
                Select-Object -ExpandProperty FullName)
        }

        # Paths for the helper script and result sentinel files.
        $scriptPath   = Join-Path $RootPath 'Install-SmrtPad.ps1'
        $exitCodePath = Join-Path $RootPath 'install.exitcode.txt'
        $logPath      = Join-Path $RootPath 'install.log'

        # Build the install script that the scheduled task will execute.
        $scriptLines = @()
        $scriptLines += '$ErrorActionPreference = ''Stop'''
        $scriptLines += 'try {'

        # Trust the signing cert inside the elevated task for belt-and-suspenders.
        $cerPath = Join-Path $RootPath 'SmrtPadTestSign.cer'
        if (Test-Path $cerPath) {
            $scriptLines += "    certutil.exe -addstore TrustedPeople '$cerPath' 2>&1 | Out-Null"
            $scriptLines += "    certutil.exe -addstore Root '$cerPath' 2>&1 | Out-Null"
        }

        $depArgs = ''
        if ($depPaths.Count -gt 0) {
            $depList = ($depPaths | ForEach-Object { "'$_'" }) -join ','
            $depArgs = " -DependencyPath @($depList)"
        }

        $scriptLines += "    Add-AppxPackage -Path '$msixPath'$depArgs -ForceUpdateFromAnyVersion -ForceApplicationShutdown -ErrorAction Stop"
        $scriptLines += "    Set-Content -LiteralPath '$exitCodePath' -Value '0' -Encoding UTF8 -Force"
        $scriptLines += '} catch {'
        $scriptLines += "    `$_ | Out-String | Set-Content -LiteralPath '$logPath' -Encoding UTF8 -Force"
        $scriptLines += "    Set-Content -LiteralPath '$exitCodePath' -Value '1' -Encoding UTF8 -Force"
        $scriptLines += '}'

        $scriptLines | Set-Content -LiteralPath $scriptPath -Encoding UTF8 -Force
        Remove-Item -LiteralPath $exitCodePath -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $logPath      -Force -ErrorAction SilentlyContinue

        # Register and run a scheduled task under the interactive user's token so that
        # Add-AppxPackage receives the desktop session context it requires.
        $taskName      = 'SmrtPadUiTestsInstall'
        $taskArg       = '-NoLogo -NoProfile -ExecutionPolicy Bypass -File "' + $scriptPath + '"'
        $taskAction    = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $taskArg
        $taskPrincipal = New-ScheduledTaskPrincipal -UserId $UserName -LogonType Interactive -RunLevel Highest
        Register-ScheduledTask -TaskName $taskName -Action $taskAction -Principal $taskPrincipal -Force | Out-Null

        try {
            Start-ScheduledTask -TaskName $taskName
            $timeout = (Get-Date).AddMinutes(5)
            while (-not (Test-Path -LiteralPath $exitCodePath) -and (Get-Date) -lt $timeout) {
                Start-Sleep -Seconds 3
            }
        } finally {
            Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
        }

        if (-not (Test-Path -LiteralPath $exitCodePath)) {
            throw 'Timed out waiting for SmrtPad package install scheduled task to complete.'
        }

        $installExitCode = (Get-Content -LiteralPath $exitCodePath -Raw).Trim()
        if ($installExitCode -ne '0') {
            $installLog = if (Test-Path -LiteralPath $logPath) { (Get-Content -LiteralPath $logPath -Raw).Trim() } else { '' }
            $errMsg = if (-not [string]::IsNullOrWhiteSpace($installLog)) { $installLog } else { "Package install failed with exit code $installExitCode." }
            throw $errMsg
        }

        $package = Get-AppxPackage -Name 'JohnDonnelly.SmrtPad' |
            Sort-Object Version -Descending | Select-Object -First 1
        if ($null -eq $package) {
            throw "Package 'JohnDonnelly.SmrtPad' was not found after installation."
        }

        Write-Output "AUMID=$($package.PackageFamilyName)!App"
    } -ArgumentList $RemoteRootPath, $MsixFileName, $RemoteUserName
}

# ── Script body ──────────────────────────────────────────────────────────────

$signtool = 'A:\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe'
$msBuild  = 'A:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe'
$root     = Split-Path -Parent $PSScriptRoot
$wap      = Join-Path $PSScriptRoot 'SmrtPad (Package).wapproj'

# ── Local (Debug) paths ──────────────────────────────────────────────────────
$appxRoot = Join-Path $PSScriptRoot "bin\x64\$configuration\AppX"
$appx     = Join-Path $appxRoot 'SmrtPad'
$manifest = Join-Path $appxRoot 'AppxManifest.xml'
$tfm      = Join-Path $root "SmrtPad\bin\x64\$configuration\net10.0-windows10.0.26100.0\win-x64"
$publish  = Join-Path $root 'SmrtPad\bin\win-x64\publish'

# Stop any running SmrtPad instance — required before touching the AppX layout.
$running = Get-Process -Name 'SmrtPad' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host '==> Stopping running SmrtPad process...' -ForegroundColor Yellow
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 1000
}

# ── Build ─────────────────────────────────────────────────────────────────────
Write-Host "==> Building WAP ($configuration|x64)..." -ForegroundColor Cyan

$msBuildArgs = @(
    $wap
    '/t:Build'
    "/p:Configuration=$configuration"
    '/p:Platform=x64'
    '/p:AppxPackageSigningEnabled=false'
    '/p:PublishReadyToRun=false'
    '/p:PublishTrimmed=false'
    '/m'
    '/v:minimal'
)
& $msBuild @msBuildArgs
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }

# Manifest may live one level up from AppX\ for Release builds.
if (-not (Test-Path $manifest)) {
    $manifest = Join-Path $PSScriptRoot "bin\x64\$configuration\AppxManifest.xml"
}

# Sync DLLs / XBFs into the AppX layout.
Write-Host '==> Syncing build output into AppX layout...' -ForegroundColor Cyan
Copy-Item -Path "$tfm\*" -Destination $appx -Recurse -Force
Get-ChildItem -Path $tfm -Filter '*.xbf' -Recurse |
    ForEach-Object { Copy-Item -Path $_.FullName -Destination $appxRoot -Force }

if (Test-Path $publish) {
    Copy-Item -Path "$publish\*" -Destination $appx -Recurse -Force
}
else {
    Write-Warning "Publish dir not found at '$publish' - skipping runtime dependency sync."
}

# ── Local deployment (no remote host) ────────────────────────────────────────
if ([string]::IsNullOrWhiteSpace($RemoteHost)) {
    Write-Host '==> Registering loose package locally...' -ForegroundColor Cyan
    Add-AppxPackage -Register $manifest -ForceUpdateFromAnyVersion
    $aumid = Get-AppUserModelId -PackageName 'JohnDonnelly.SmrtPad'
    Write-Output "AUMID=$aumid"
    Write-Host '==> Deployment complete.' -ForegroundColor Green
    return
}

# ── Remote deployment: sign and install .msix ────────────────────────────────

# Locate the AppPackages output folder produced by the WAP build.
$packageOutputDirectory = Get-ChildItem (Join-Path $PSScriptRoot 'AppPackages') -Directory |
    Where-Object { $_.Name -like 'SmrtPad (Package)_*_x64_Test' -and $_.Name -notlike '*Debug*' } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $packageOutputDirectory) {
    throw "Could not locate AppPackages output for Release|x64."
}

$msixPath = Get-ChildItem $packageOutputDirectory.FullName -Filter '*.msix' |
    Where-Object { $_.Name -notlike '*.appxsym' } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1 -ExpandProperty FullName

if ($null -eq $msixPath) {
    throw "No .msix found in '$($packageOutputDirectory.FullName)'."
}

# x64 dependency packages to copy alongside the .msix.
$dependencyFiles = Get-ChildItem $packageOutputDirectory.FullName -Recurse -Include *.appx, *.msix |
    Where-Object { $_.FullName -match '\\Dependencies\\x64\\' }

# Ensure a self-signed cert exists and sign the .msix.
Write-Host '==> Ensuring signing certificate...' -ForegroundColor Cyan
$certDir  = Join-Path $PSScriptRoot 'TestCerts'
New-Item -Path $certDir -ItemType Directory -Force | Out-Null
$certInfo = Get-OrCreateSigningCertificate -Subject 'CN=John_' -OutputDir $certDir

Write-Host '==> Signing .msix...' -ForegroundColor Cyan
& $signtool sign /fd SHA256 /a /f $certInfo.PfxPath $msixPath
if ($LASTEXITCODE -ne 0) { throw "signtool sign failed with exit code $LASTEXITCODE." }

# ── Copy files to remote and install ─────────────────────────────────────────
$credential  = New-OptionalCredential -UserName $RemoteUser -Password $RemotePassword
$sessionArgs = @{ ComputerName = $RemoteHost }
if ($null -ne $credential) { $sessionArgs.Credential = $credential }

$session = New-PSSession @sessionArgs
try {
    # Prepare a clean staging directory on the remote machine.
    $remoteRoot = Invoke-Command -Session $session -ScriptBlock {
        $stagingRoot = Join-Path $env:LOCALAPPDATA 'SmrtPadUITestAppX'
        if (Test-Path $stagingRoot) { Remove-Item -Path $stagingRoot -Recurse -Force }
        New-Item -Path $stagingRoot -ItemType Directory -Force | Out-Null

        $runningRemote = Get-Process -Name 'SmrtPad' -ErrorAction SilentlyContinue
        if ($runningRemote) { $runningRemote | Stop-Process -Force; Start-Sleep -Milliseconds 1000 }

        return $stagingRoot
    }

    Write-Host "==> Copying .msix + dependencies to $RemoteHost..." -ForegroundColor Cyan
    $remoteMsixName = [System.IO.Path]::GetFileName($msixPath)
    Copy-Item -Path $msixPath         -Destination (Join-Path $remoteRoot $remoteMsixName) -ToSession $session -Force
    Copy-Item -Path $certInfo.CerPath -Destination (Join-Path $remoteRoot 'SmrtPadTestSign.cer') -ToSession $session -Force

    if ($dependencyFiles.Count -gt 0) {
        $remoteDepsRoot = Join-Path $remoteRoot 'Dependencies'
        Invoke-Command -Session $session -ScriptBlock {
            param($D); New-Item -Path $D -ItemType Directory -Force | Out-Null
        } -ArgumentList $remoteDepsRoot

        foreach ($dep in $dependencyFiles) {
            Copy-Item -Path $dep.FullName -Destination (Join-Path $remoteDepsRoot $dep.Name) -ToSession $session -Force
        }
    }

    Write-Host '==> Trusting signing certificate on remote machine...' -ForegroundColor Cyan
    Install-RemoteTrustCertificate -Session $session -CerPath $certInfo.CerPath

    Write-Host '==> Installing .msix on remote machine...' -ForegroundColor Cyan
    $aumidLine = Install-RemoteMsix -Session $session -RemoteRootPath $remoteRoot -MsixFileName $remoteMsixName -RemoteUserName $RemoteUser

    $aumid = ($aumidLine | Where-Object { $_ -like 'AUMID=*' } | Select-Object -First 1)
    if ([string]::IsNullOrWhiteSpace($aumid)) {
        throw "Install-RemoteMsix did not return an AUMID line. Output: $aumidLine"
    }

    Write-Output "REMOTE_ROOT=$remoteRoot"
    Write-Output $aumid
    Write-Host '==> Remote deployment complete.' -ForegroundColor Green
}
finally {
    if ($null -ne $session) { Remove-PSSession -Session $session }
}
