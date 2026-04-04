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
$requestedConfiguration = $env:UITEST_DEPLOY_CONFIGURATION
if (-not [string]::IsNullOrWhiteSpace($requestedConfiguration)) {
    if ($requestedConfiguration -ne 'Debug' -and $requestedConfiguration -ne 'Release') {
        throw "UITEST_DEPLOY_CONFIGURATION must be 'Debug' or 'Release' when set. Current value: '$requestedConfiguration'."
    }
    $configuration = $requestedConfiguration
}
else {
    $configuration = if ([string]::IsNullOrWhiteSpace($RemoteHost)) { 'Debug' } else { 'Release' }
}

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

function Get-PackagePublisherFromManifest {
    param(
        [string]$ManifestPath
    )

    if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
        throw 'Manifest path is required.'
    }

    if (-not (Test-Path $ManifestPath)) {
        throw "Manifest not found at '$ManifestPath'."
    }

    [xml]$manifestXml = Get-Content -LiteralPath $ManifestPath -Raw
    $publisher = $manifestXml.Package.Identity.Publisher
    if ([string]::IsNullOrWhiteSpace($publisher)) {
        throw "Could not read Package/Identity/@Publisher from '$ManifestPath'."
    }

    return $publisher
}

function Get-FoundryCommandPath {
    $foundry = Get-Command foundry -ErrorAction SilentlyContinue
    if ($null -ne $foundry) {
        return $foundry.Source
    }

    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Microsoft\FoundryLocal\foundry.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft\FoundryLocal\foundry.exe')
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

function Get-FoundryCandidatePackageIds {
    $ids = @()
    if (-not [string]::IsNullOrWhiteSpace($env:SMRTPAD_FOUNDRY_WINGET_ID)) {
        $ids += $env:SMRTPAD_FOUNDRY_WINGET_ID.Split(',', [System.StringSplitOptions]::RemoveEmptyEntries) |
            ForEach-Object { $_.Trim() }
    }

    $ids += @('Microsoft.FoundryLocal', 'Microsoft.Foundry')
    return $ids | Select-Object -Unique
}

function Get-FoundryInstallerUrl {
    if (-not [string]::IsNullOrWhiteSpace($env:SMRTPAD_FOUNDRY_INSTALLER_URL)) {
        return $env:SMRTPAD_FOUNDRY_INSTALLER_URL
    }

    # Resolve the latest x64 MSIX from the GitHub releases API.
    # The aka.ms/foundry-local-installer redirect points to the releases page, not a file.
    try {
        $apiUrl  = 'https://api.github.com/repos/microsoft/Foundry-Local/releases'
        $headers = @{ 'User-Agent' = 'SmrtPad-Deploy' }
        $releases = Invoke-RestMethod -Uri $apiUrl -Headers $headers -UseBasicParsing -ErrorAction Stop
        $latest   = $releases | Sort-Object { [version]($_.tag_name -replace '^v','') } -Descending | Select-Object -First 1
        $asset    = $latest.assets | Where-Object { $_.name -like 'FoundryLocal-x64-*.msix' } | Select-Object -First 1
        if ($null -ne $asset) {
            return $asset.browser_download_url
        }
    }
    catch {
        Write-Host "  Warning: could not resolve Foundry release from GitHub API: $($_.Exception.Message)" -ForegroundColor Yellow
    }

    # Fallback: direct link to the releases page so the error message is actionable.
    return 'https://github.com/microsoft/Foundry-Local/releases'
}

function Get-FoundryInstallerLocalPath {
    $path = $env:SMRTPAD_FOUNDRY_INSTALLER_PATH
    if ([string]::IsNullOrWhiteSpace($path)) {
        return $null
    }

    if (Test-Path $path) {
        return (Resolve-Path $path).Path
    }

    return $null
}

function Try-InstallFoundryFromDirectInstaller {
    param(
        [scriptblock]$GetFoundryPath
    )

    $tempDir = Join-Path $env:TEMP 'SmrtPadFoundryInstall'
    New-Item -Path $tempDir -ItemType Directory -Force | Out-Null
    $targetPath = Join-Path $tempDir 'foundry-installer.bin'

    $localInstallerPath = Get-FoundryInstallerLocalPath
    if (-not [string]::IsNullOrWhiteSpace($localInstallerPath)) {
        Copy-Item -Path $localInstallerPath -Destination $targetPath -Force
    }
    else {
        $installerUrl = Get-FoundryInstallerUrl
        if ([string]::IsNullOrWhiteSpace($installerUrl)) {
            return $false
        }

        try {
            Invoke-WebRequest -Uri $installerUrl -OutFile $targetPath -UseBasicParsing
        }
        catch {
            return $false
        }
    }

    $ext = [System.IO.Path]::GetExtension($targetPath)
    if ($null -eq $ext) {
        $ext = [string]::Empty
    }
    $ext = $ext.ToLowerInvariant()
    if ([string]::IsNullOrWhiteSpace($ext) -and (Get-Item $targetPath).Name -match '\.([A-Za-z0-9]+)$') {
        $ext = '.' + $matches[1].ToLowerInvariant()
    }

    $finalPath = $targetPath
    if ($ext -in @('.exe', '.msi', '.msix', '.msixbundle', '.appx', '.appxbundle')) {
        $finalPath = Join-Path $tempDir ("foundry-installer$ext")
        Move-Item -Path $targetPath -Destination $finalPath -Force
    }

    try {
        switch -Regex ($ext)
        {
            '^\.msix$|^\.msixbundle$|^\.appx$|^\.appxbundle$' {
                Add-AppxPackage -Path $finalPath -ForceUpdateFromAnyVersion -ForceApplicationShutdown -ErrorAction Stop
                break
            }
            '^\.msi$' {
                $proc = Start-Process -FilePath 'msiexec.exe' -ArgumentList "/i `"$finalPath`" /qn /norestart" -Wait -PassThru -NoNewWindow
                if ($proc.ExitCode -ne 0) { return $false }
                break
            }
            default {
                $proc = Start-Process -FilePath $finalPath -ArgumentList '/quiet /norestart' -Wait -PassThru -NoNewWindow
                if ($proc.ExitCode -ne 0) {
                    $proc = Start-Process -FilePath $finalPath -ArgumentList '/S' -Wait -PassThru -NoNewWindow
                    if ($proc.ExitCode -ne 0) { return $false }
                }
                break
            }
        }
    }
    catch {
        return $false
    }

    $installed = & $GetFoundryPath
    return -not [string]::IsNullOrWhiteSpace($installed)
}

function Ensure-FoundryInstalledLocal {
    $existing = Get-FoundryCommandPath
    if (-not [string]::IsNullOrWhiteSpace($existing)) {
        Write-Host "==> Foundry already installed: $existing" -ForegroundColor DarkGray
        return
    }

    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if ($null -ne $winget) {
        $candidateIds = Get-FoundryCandidatePackageIds
        foreach ($source in @('winget', 'msstore')) {
            foreach ($id in $candidateIds) {
                Write-Host "==> Installing Foundry via winget id '$id' (source: $source)..." -ForegroundColor Cyan
                & $winget.Source install --id $id --source $source --accept-package-agreements --accept-source-agreements --silent --exact --disable-interactivity
                if ($LASTEXITCODE -eq 0) {
                    $installed = Get-FoundryCommandPath
                    if (-not [string]::IsNullOrWhiteSpace($installed)) {
                        Write-Host "==> Foundry installed: $installed" -ForegroundColor Green
                        return
                    }
                }
            }
        }
    }

    Write-Host '==> Attempting direct installer fallback for Foundry...' -ForegroundColor Yellow
    if (Try-InstallFoundryFromDirectInstaller -GetFoundryPath { Get-FoundryCommandPath }) {
        $installed = Get-FoundryCommandPath
        Write-Host "==> Foundry installed: $installed" -ForegroundColor Green
        return
    }

    throw 'Foundry Local is not installed and automatic installation failed. Install Foundry Local, then rerun deployment.'
}

function Ensure-FoundryInstalledRemote {
    param(
        [System.Management.Automation.Runspaces.PSSession]$Session,
        [string]$RemoteHost,
        [string]$RemoteInstallerPath,
        [string]$RemoteUserName
    )

    $candidateIds = Get-FoundryCandidatePackageIds
    $idsCsv = ($candidateIds -join ';')

    # Resolve the active interactive user on the remote before entering the script block,
    # since functions defined locally aren't available inside Invoke-Command.
    $activeInteractiveUser = Invoke-Command -Session $Session -ScriptBlock {
        try {
            $cs = Get-WmiObject -Class Win32_ComputerSystem -ErrorAction SilentlyContinue
            if ($cs -and -not [string]::IsNullOrWhiteSpace($cs.UserName)) {
                return $cs.UserName -replace '^.*\\', ''
            }
        } catch {}
        return $null
    }
    $taskPrincipalUser = if (-not [string]::IsNullOrWhiteSpace($activeInteractiveUser)) { $activeInteractiveUser } else { $RemoteUserName }

    $result = Invoke-Command -Session $Session -ScriptBlock {
        param([string]$CandidateIdsCsv, [string]$PreferredInstallerPath, [string]$RemoteUser, [string]$TaskUser)

        function Get-RemoteFoundryPath {
            $foundry = Get-Command foundry -ErrorAction SilentlyContinue
            if ($null -ne $foundry) {
                return $foundry.Source
            }

            # Check the current session's profile first, then all user profiles.
            # Foundry is a per-user MSIX; if installed under a different account (e.g.
            # John_ when WinRM runs as John-Dev), it won't be in $env:LOCALAPPDATA.
            $profileRoots = @($env:LOCALAPPDATA)
            $usersRoot = Split-Path $env:USERPROFILE -Parent
            if (Test-Path $usersRoot) {
                Get-ChildItem $usersRoot -Directory -ErrorAction SilentlyContinue |
                    ForEach-Object { $profileRoots += (Join-Path $_.FullName 'AppData\Local') }
            }

            foreach ($root in ($profileRoots | Select-Object -Unique)) {
                $candidates = @(
                    (Join-Path $root 'Microsoft\FoundryLocal\foundry.exe'),
                    (Join-Path $root 'Microsoft\WindowsApps\foundry.exe')
                )
                foreach ($c in $candidates) {
                    if (Test-Path $c) { return $c }
                }
            }

            if (Test-Path (Join-Path $env:ProgramFiles 'Microsoft\FoundryLocal\foundry.exe')) {
                return (Join-Path $env:ProgramFiles 'Microsoft\FoundryLocal\foundry.exe')
            }

            return $null
        }

        $existing = Get-RemoteFoundryPath
        if (-not [string]::IsNullOrWhiteSpace($existing)) {
            return "FOUND:$existing"
        }

        function Try-InstallFromPath {
            param([string]$InstallerPath, [string]$RemoteUserName, [string]$TaskUser)

            if ([string]::IsNullOrWhiteSpace($InstallerPath) -or -not (Test-Path $InstallerPath)) {
                return $null
            }

            $ext = [System.IO.Path]::GetExtension($InstallerPath)
            if ($null -eq $ext) { $ext = [string]::Empty }
            $ext = $ext.ToLowerInvariant()

            try {
                switch -Regex ($ext)
                {
                    '^\.msix$|^\.msixbundle$|^\.appx$|^\.appxbundle$' {
                        # Add-AppxPackage requires an interactive token, which WinRM sessions
                        # don't provide.  Use a scheduled task running under the interactive
                        # user's logon session — the same pattern used by Install-RemoteMsix.
                        $tempDir      = Join-Path $env:TEMP 'SmrtPadFoundryInstall'
                        $scriptPath   = Join-Path $tempDir 'Install-Foundry.ps1'
                        $exitCodePath = Join-Path $tempDir 'foundry-install.exitcode.txt'
                        $logPath      = Join-Path $tempDir 'foundry-install.log.txt'
                        New-Item -Path $tempDir -ItemType Directory -Force | Out-Null

                        $scriptContent  = '$ErrorActionPreference = ''Stop''' + "`n"
                        $scriptContent += 'try {' + "`n"
                        $scriptContent += "    Add-AppxPackage -Path '$InstallerPath' -ForceUpdateFromAnyVersion -ForceApplicationShutdown -ErrorAction Stop`n"
                        $scriptContent += "    Set-Content -LiteralPath '$exitCodePath' -Value '0' -Encoding UTF8 -Force`n"
                        $scriptContent += '} catch {' + "`n"
                        $scriptContent += "    `$_ | Out-String | Set-Content -LiteralPath '$logPath' -Encoding UTF8 -Force`n"
                        $scriptContent += "    Set-Content -LiteralPath '$exitCodePath' -Value '1' -Encoding UTF8 -Force`n"
                        $scriptContent += '}'
                        Set-Content -LiteralPath $scriptPath -Value $scriptContent -Encoding UTF8 -Force
                        Remove-Item -LiteralPath $exitCodePath -Force -ErrorAction SilentlyContinue

                        $taskName      = 'SmrtPadFoundryInstall'
                        $taskArg       = '-NoLogo -NoProfile -ExecutionPolicy Bypass -File "' + $scriptPath + '"'
                        $taskAction    = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $taskArg
                        # The task must run under the user with an active desktop session.
                        # $TaskUser was resolved via WMI before entering this script block.
                        $taskPrincipal = New-ScheduledTaskPrincipal -UserId $TaskUser -LogonType Interactive -RunLevel Highest
                        Register-ScheduledTask -TaskName $taskName -Action $taskAction -Principal $taskPrincipal -Force | Out-Null

                        try {
                            Start-ScheduledTask -TaskName $taskName
                            $deadline = (Get-Date).AddMinutes(5)
                            # Wait for the exit-code sentinel file (same pattern as Install-RemoteMsix).
                            while (-not (Test-Path -LiteralPath $exitCodePath) -and (Get-Date) -lt $deadline) {
                                Start-Sleep -Seconds 3
                            }
                        }
                        finally {
                            Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction SilentlyContinue
                        }

                        if (-not (Test-Path -LiteralPath $exitCodePath)) {
                            throw 'Timed out waiting for Foundry installer scheduled task to complete.'
                        }
                        $exitCode = Get-Content -LiteralPath $exitCodePath -ErrorAction SilentlyContinue
                        if ($exitCode -ne '0') {
                            $log = Get-Content -LiteralPath $logPath -Raw -ErrorAction SilentlyContinue
                            throw "Scheduled-task Foundry MSIX install failed (exit $exitCode): $log"
                        }
                        break
                    }
                    '^\.msi$' {
                        $proc = Start-Process -FilePath 'msiexec.exe' -ArgumentList "/i `"$InstallerPath`" /qn /norestart" -Wait -PassThru -NoNewWindow
                        if ($proc.ExitCode -ne 0) { return $null }
                        break
                    }
                    default {
                        $proc = Start-Process -FilePath $InstallerPath -ArgumentList '/quiet /norestart' -Wait -PassThru -NoNewWindow
                        if ($proc.ExitCode -ne 0) {
                            $proc = Start-Process -FilePath $InstallerPath -ArgumentList '/S' -Wait -PassThru -NoNewWindow
                            if ($proc.ExitCode -ne 0) { return $null }
                        }
                        break
                    }
                }
            }
            catch {
                return $null
            }

            $installed = Get-RemoteFoundryPath
            if (-not [string]::IsNullOrWhiteSpace($installed)) {
                return "INSTALLED:$installed"
            }

            return $null
        }

        $preferredInstallResult = Try-InstallFromPath -InstallerPath $PreferredInstallerPath -RemoteUserName $RemoteUser -TaskUser $TaskUser
        if (-not [string]::IsNullOrWhiteSpace($preferredInstallResult)) {
            return $preferredInstallResult
        }

        $winget = Get-Command winget -ErrorAction SilentlyContinue
        if ($null -eq $winget) {
            return 'MISSING:winget_not_found'
        }

        $ids = $CandidateIdsCsv.Split(';', [System.StringSplitOptions]::RemoveEmptyEntries)
        foreach ($id in $ids) {
            # Only use the 'winget' source — 'msstore' requires interactive geographic-region
            # consent that cannot be provided in a WinRM session.
            & $winget.Source install --id $id --source winget --accept-package-agreements --accept-source-agreements --silent --exact --disable-interactivity
            if ($LASTEXITCODE -eq 0) {
                $installed = Get-RemoteFoundryPath
                if (-not [string]::IsNullOrWhiteSpace($installed)) {
                    return "INSTALLED:$installed"
                }
            }
        }

        function Get-InstallerUrl {
            if (-not [string]::IsNullOrWhiteSpace($env:SMRTPAD_FOUNDRY_INSTALLER_URL)) {
                return $env:SMRTPAD_FOUNDRY_INSTALLER_URL
            }
            # Resolve the real x64 MSIX URL from the GitHub releases API.
            try {
                $releases = Invoke-RestMethod -Uri 'https://api.github.com/repos/microsoft/Foundry-Local/releases' `
                    -Headers @{ 'User-Agent' = 'SmrtPad-Deploy' } -UseBasicParsing -ErrorAction Stop
                $latest = $releases | Sort-Object { [version]($_.tag_name -replace '^v','') } -Descending | Select-Object -First 1
                $asset  = $latest.assets | Where-Object { $_.name -like 'FoundryLocal-x64-*.msix' } | Select-Object -First 1
                if ($null -ne $asset) { return $asset.browser_download_url }
            } catch {}
            return $null
        }

        try {
            $installerUrl = Get-InstallerUrl
            $tempDir = Join-Path $env:TEMP 'SmrtPadFoundryInstall'
            New-Item -Path $tempDir -ItemType Directory -Force | Out-Null
            $installerPath = Join-Path $tempDir 'foundry-installer.bin'
            Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing

            $ext = [System.IO.Path]::GetExtension($installerPath)
            if ($null -eq $ext) {
                $ext = [string]::Empty
            }
            $ext = $ext.ToLowerInvariant()
            $finalPath = $installerPath
            if ($ext -in @('.exe', '.msi', '.msix', '.msixbundle', '.appx', '.appxbundle')) {
                $finalPath = Join-Path $tempDir ("foundry-installer$ext")
                Move-Item -Path $installerPath -Destination $finalPath -Force
            }

            $downloadInstallResult = Try-InstallFromPath -InstallerPath $finalPath -RemoteUserName $RemoteUser -TaskUser $TaskUser
            if (-not [string]::IsNullOrWhiteSpace($downloadInstallResult)) {
                return $downloadInstallResult
            }
        }
        catch {
            # ignore and return final missing state below
        }

        $after = Get-RemoteFoundryPath
        if (-not [string]::IsNullOrWhiteSpace($after)) {
            return "FOUND:$after"
        }

        return 'MISSING:install_failed'
    } -ArgumentList $idsCsv, $RemoteInstallerPath, $RemoteUserName, $taskPrincipalUser

    if ($result -like 'FOUND:*' -or $result -like 'INSTALLED:*') {
        Write-Host "==> Foundry available on ${RemoteHost}: $($result.Split(':', 2)[1])" -ForegroundColor Green
        return
    }

    throw "Foundry Local is not available on $RemoteHost and automatic installation failed ($result). Install Foundry Local on the remote machine, then rerun deployment."
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

# Returns the username of the currently active interactive (console/RDP) session on the
# local machine.  Used to identify the correct task principal for scheduled-task MSIX
# installs, which require a real interactive token — not the WinRM network logon token.
function Get-ActiveInteractiveUser {
    try {
        # Win32_ComputerSystem.UserName returns the interactively logged-on user.
        $cs = Get-WmiObject -Class Win32_ComputerSystem -ErrorAction SilentlyContinue
        if ($cs -and -not [string]::IsNullOrWhiteSpace($cs.UserName)) {
            # Strip the DOMAIN\ prefix so we get just the username.
            return $cs.UserName -replace '^.*\\', ''
        }
    } catch {}
    return $null
}

# Ensures a self-signed PFX exists for the given publisher subject and returns its path.
# The cert is stored in the script directory and reused across runs.
# The PFX is protected with a fixed test password so signtool can consume it.
function Get-OrCreateSigningCertificate {
    param(
        [string]$Subject,
        [string]$OutputDir
    )

    $pfxPath = Join-Path $OutputDir 'SmrtPadTestSign.pfx'
    $cerPath = Join-Path $OutputDir 'SmrtPadTestSign.cer'
    # Fixed password used for the test-signing PFX — not a security secret.
    $pfxPassword = 'SmrtPadTestSign'

    # Regenerate if either file is missing.
    if (-not (Test-Path $pfxPath) -or -not (Test-Path $cerPath)) {
        Write-Host "  Generating self-signed certificate for '$Subject'..." -ForegroundColor DarkGray
        $cert = New-SelfSignedCertificate `
            -Subject $Subject `
            -Type CodeSigningCert `
            -CertStoreLocation 'Cert:\CurrentUser\My' `
            -HashAlgorithm SHA256 `
            -NotAfter (Get-Date).AddYears(5)

        $securePassword = ConvertTo-SecureString $pfxPassword -AsPlainText -Force
        Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword | Out-Null
        Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT | Out-Null
        Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force -ErrorAction SilentlyContinue
        Write-Host "  Certificate created: $pfxPath" -ForegroundColor DarkGray
    }

    return [pscustomobject]@{ PfxPath = $pfxPath; CerPath = $cerPath; Password = $pfxPassword }
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

    # Resolve the active interactive user before entering Invoke-Command, since
    # local functions (Get-ActiveInteractiveUser) are not available in remote sessions.
    $activeInteractiveUser = Invoke-Command -Session $Session -ScriptBlock {
        try {
            $cs = Get-WmiObject -Class Win32_ComputerSystem -ErrorAction SilentlyContinue
            if ($cs -and -not [string]::IsNullOrWhiteSpace($cs.UserName)) {
                return $cs.UserName -replace '^.*\\', ''
            }
        } catch {}
        return $null
    }
    $taskUser = if (-not [string]::IsNullOrWhiteSpace($activeInteractiveUser)) { $activeInteractiveUser } else { $RemoteUserName }

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

        # Try direct install in the WinRM session first. On some machines this works
        # reliably and is faster than scheduled-task indirection.
        try {
            Get-AppxPackage -Name 'JohnDonnelly.SmrtPad' -AllUsers -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue
            Get-AppxPackage -Name 'JohnDonnelly.SmrtPad' -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue

            if ($depPaths.Count -gt 0) {
                Add-AppxPackage -Path $msixPath -DependencyPath $depPaths -ForceUpdateFromAnyVersion -ForceApplicationShutdown -ErrorAction Stop
            }
            else {
                Add-AppxPackage -Path $msixPath -ForceUpdateFromAnyVersion -ForceApplicationShutdown -ErrorAction Stop
            }

            $packageDirect = Get-AppxPackage -Name 'JohnDonnelly.SmrtPad' |
                Sort-Object Version -Descending | Select-Object -First 1
            if ($null -eq $packageDirect) {
                throw "Package 'JohnDonnelly.SmrtPad' was not found after direct installation."
            }

            Write-Output "AUMID=$($packageDirect.PackageFamilyName)!App"
            return
        }
        catch {
            Write-Host "  Direct Add-AppxPackage failed in WinRM session; falling back to scheduled task. Reason: $($_.Exception.Message)" -ForegroundColor DarkGray
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

        # Remove ALL existing versions of the package before installing the new one.
        # Add-AppxPackage fails with 0x80073CFB when any installed version has the same
        # identity but different content (e.g. Debug vs Release, or a rebuild without
        # a version bump).  Using -AllUsers with elevation removes it for every user.
        $scriptLines += "    Get-AppxPackage -Name 'JohnDonnelly.SmrtPad' -AllUsers -ErrorAction SilentlyContinue | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue"
        $scriptLines += "    Get-AppxPackage -Name 'JohnDonnelly.SmrtPad' -ErrorAction SilentlyContinue | Remove-AppxPackage -ErrorAction SilentlyContinue"

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
        # $UserName is the active interactive user resolved via WMI before this block.
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
    } -ArgumentList $RemoteRootPath, $MsixFileName, $taskUser
}

# ── Tool path resolution ─────────────────────────────────────────────────────

# Resolves signtool.exe by checking the Windows Kits registry, then PATH, then the
# developer-machine hardcoded fallback.  Throws if the tool cannot be found anywhere.
function Get-SignToolPath {
    # 1. Registry: Windows SDK installed roots
    try {
        $kitRoot = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots' `
            -ErrorAction SilentlyContinue).'KitsRoot10'
        if ($kitRoot) {
            $found = Get-ChildItem (Join-Path $kitRoot 'bin') -Recurse -Filter 'signtool.exe' `
                -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -match '\\x64\\' } |
                Sort-Object FullName -Descending |
                Select-Object -First 1 -ExpandProperty FullName
            if ($found) { return $found }
        }
    } catch {}

    # 2. PATH
    $inPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($inPath) { return $inPath.Source }

    # 3. Developer-machine fallback
    $fallback = 'A:\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe'
    if (Test-Path $fallback) { return $fallback }

    throw 'signtool.exe not found. Install the Windows 10 SDK or add it to PATH.'
}

# Resolves MSBuild.exe by using vswhere (preferred), then the developer-machine fallback.
function Get-MSBuildPath {
    # 1. vswhere: works on any machine with VS installed
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $vswhere)) {
        $vswhere = Join-Path $env:ProgramFiles 'Microsoft Visual Studio\Installer\vswhere.exe'
    }
    if (Test-Path $vswhere) {
        $vsPath = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -property installationPath 2>$null
        if ($vsPath) {
            $candidates = @(
                (Join-Path $vsPath 'MSBuild\Current\Bin\amd64\MSBuild.exe'),
                (Join-Path $vsPath 'MSBuild\Current\Bin\MSBuild.exe')
            )
            foreach ($c in $candidates) {
                if (Test-Path $c) { return $c }
            }
        }
    }

    # 2. Developer-machine fallback
    $fallback = 'A:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe'
    if (Test-Path $fallback) { return $fallback }

    throw 'MSBuild.exe not found. Install Visual Studio with the MSBuild component.'
}

# ── Script body ──────────────────────────────────────────────────────────────

$signtool = Get-SignToolPath
$msBuild  = Get-MSBuildPath
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
    $ensureFoundry = $env:SMRTPAD_ENSURE_FOUNDRY
    $ensureFoundryEnabled = [string]::IsNullOrWhiteSpace($ensureFoundry) -or $ensureFoundry -match '^(1|true|yes)$'
    if ($ensureFoundryEnabled) {
        Ensure-FoundryInstalledLocal
    }

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
    Where-Object { $_.Name -like 'SmrtPad (Package)_*_x64_Test' } |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if ($null -eq $packageOutputDirectory) {
    throw "Could not locate AppPackages output folder matching 'SmrtPad (Package)_*_x64_Test'."
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
$publisherSubject = Get-PackagePublisherFromManifest -ManifestPath $manifest
Write-Host "  Using package publisher subject: $publisherSubject" -ForegroundColor DarkGray
$certInfo = Get-OrCreateSigningCertificate -Subject $publisherSubject -OutputDir $certDir

Write-Host '==> Signing .msix...' -ForegroundColor Cyan
# /f specifies the PFX file; /p its password; /fd the digest algorithm.
# /a is intentionally omitted — it selects from the cert store and conflicts with /f.
& $signtool sign /fd SHA256 /f $certInfo.PfxPath /p $certInfo.Password $msixPath
if ($LASTEXITCODE -ne 0) { throw "signtool sign failed with exit code $LASTEXITCODE." }

# ── Copy files to remote and install ─────────────────────────────────────────
$credential  = New-OptionalCredential -UserName $RemoteUser -Password $RemotePassword
$sessionArgs = @{ ComputerName = $RemoteHost }
if ($null -ne $credential) { $sessionArgs.Credential = $credential }

$session = New-PSSession @sessionArgs
try {
    $ensureFoundry = $env:SMRTPAD_ENSURE_FOUNDRY
    $ensureFoundryEnabled = [string]::IsNullOrWhiteSpace($ensureFoundry) -or $ensureFoundry -match '^(1|true|yes)$'
    # Prepare a clean staging directory on the remote machine.
    $remoteRoot = Invoke-Command -Session $session -ScriptBlock {
        $stagingRoot = Join-Path $env:LOCALAPPDATA 'SmrtPadUITestAppX'
        if (Test-Path $stagingRoot) { Remove-Item -Path $stagingRoot -Recurse -Force }
        New-Item -Path $stagingRoot -ItemType Directory -Force | Out-Null

        $runningRemote = Get-Process -Name 'SmrtPad' -ErrorAction SilentlyContinue
        if ($runningRemote) { $runningRemote | Stop-Process -Force; Start-Sleep -Milliseconds 1000 }

        return $stagingRoot
    }

    $remoteFoundryInstallerPath = ''
    if ($ensureFoundryEnabled) {
        # Quick remote check: skip the expensive download if Foundry is already installed.
        $remoteFoundryCheck = Invoke-Command -Session $session -ScriptBlock {
            $f = Get-Command foundry -ErrorAction SilentlyContinue
            if ($f) { return $f.Source }
            $usersRoot = Split-Path $env:USERPROFILE -Parent
            $roots = @($env:LOCALAPPDATA)
            if (Test-Path $usersRoot) {
                Get-ChildItem $usersRoot -Directory -ErrorAction SilentlyContinue |
                    ForEach-Object { $roots += (Join-Path $_.FullName 'AppData\Local') }
            }
            foreach ($r in ($roots | Select-Object -Unique)) {
                foreach ($c in @(
                    (Join-Path $r 'Microsoft\FoundryLocal\foundry.exe'),
                    (Join-Path $r 'Microsoft\WindowsApps\foundry.exe')
                )) { if (Test-Path $c) { return $c } }
            }
            $prog = Join-Path $env:ProgramFiles 'Microsoft\FoundryLocal\foundry.exe'
            if (Test-Path $prog) { return $prog }
            return $null
        }

        if (-not [string]::IsNullOrWhiteSpace($remoteFoundryCheck)) {
            Write-Host "==> Foundry already installed on remote: $remoteFoundryCheck (skipping download)" -ForegroundColor Green
        }
        else {
            $localInstallerPath = Get-FoundryInstallerLocalPath
            if ([string]::IsNullOrWhiteSpace($localInstallerPath)) {
                # No pre-staged installer: download the Foundry MSIX locally so we can
                # copy it to the remote over the PSSession (avoids GitHub API calls inside
                # the WinRM session, which may be blocked or lack interactive consent).
                $foundryInstallerUrl = Get-FoundryInstallerUrl
                if (-not [string]::IsNullOrWhiteSpace($foundryInstallerUrl) -and $foundryInstallerUrl -notlike '*/releases') {
                    $localTempDir = Join-Path $env:TEMP 'SmrtPadFoundryInstall'
                    New-Item -Path $localTempDir -ItemType Directory -Force | Out-Null
                    $ext = [System.IO.Path]::GetExtension($foundryInstallerUrl)
                    if ([string]::IsNullOrWhiteSpace($ext)) { $ext = '.msix' }
                    $downloadedPath = Join-Path $localTempDir "foundry-installer$ext"
                    try {
                        Write-Host "==> Downloading Foundry installer locally: $foundryInstallerUrl" -ForegroundColor DarkGray
                        Invoke-WebRequest -Uri $foundryInstallerUrl -OutFile $downloadedPath -UseBasicParsing -ErrorAction Stop
                        $localInstallerPath = $downloadedPath
                        Write-Host "  Downloaded to: $localInstallerPath" -ForegroundColor DarkGray
                    }
                    catch {
                        Write-Host "  Warning: could not download Foundry installer: $($_.Exception.Message)" -ForegroundColor Yellow
                    }
                }
            }

            if (-not [string]::IsNullOrWhiteSpace($localInstallerPath)) {
                $ext = [System.IO.Path]::GetExtension($localInstallerPath)
                if ([string]::IsNullOrWhiteSpace($ext)) { $ext = '.bin' }
                $remoteFoundryInstallerPath = Join-Path $remoteRoot ("foundry-installer$ext")
                Copy-Item -Path $localInstallerPath -Destination $remoteFoundryInstallerPath -ToSession $session -Force
                Write-Host "==> Copied Foundry installer to remote: $remoteFoundryInstallerPath" -ForegroundColor DarkGray
            }

            Write-Host '==> Ensuring Foundry Local is installed on remote machine...' -ForegroundColor Cyan
            Ensure-FoundryInstalledRemote -Session $session -RemoteHost $RemoteHost -RemoteInstallerPath $remoteFoundryInstallerPath -RemoteUserName $RemoteUser
        }
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
