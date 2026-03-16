<#
.SYNOPSIS
    Builds SmrtPad (Debug|x64) and deploys it as a loose MSIX package for UI testing.

.DESCRIPTION
    MSBuild populates two output directories that must BOTH be synced to the F5-deploy
    AppX layout folder before registering the package:

      1. SmrtPad\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\
         Contains: SmrtPad.dll (code), *.xbf (compiled XAML), App.xbf, Views\*, Controls\*
         These are the hot build outputs that change every build.

      2. SmrtPad\bin\win-x64\publish\
         Contains: all runtime dependencies (NuGet packages, AppSDK DLLs, etc.)
         These change only when dependencies change.

    Without syncing BOTH sources the AppX layout folder runs stale XBF (XAML binary)
    or stale DLL, causing test failures.

    Steps performed:
      1. Build the WAP project (Debug|x64).
      2. Sync XBF + DLL from the TFM build output → AppX\SmrtPad\
      3. Sync runtime dependencies from publish\ → AppX\SmrtPad\
      4. Add-AppxPackage -Register AppxManifest.xml  (loose / F5-style deployment).
#>

$ErrorActionPreference = 'Stop'

$root    = Split-Path -Parent $PSScriptRoot
$msBuild = "A:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\amd64\MSBuild.exe"
$wap     = Join-Path $PSScriptRoot "SmrtPad (Package).wapproj"
$tfm     = Join-Path $root "SmrtPad\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\"
$publish = Join-Path $root "SmrtPad\bin\win-x64\publish\"
$appx    = Join-Path $PSScriptRoot "bin\x64\Debug\AppX\SmrtPad\"
$manifest= Join-Path $PSScriptRoot "bin\x64\Debug\AppX\AppxManifest.xml"

Write-Host "==> Building WAP (Debug|x64)..." -ForegroundColor Cyan
& $msBuild $wap /t:Build /p:Configuration=Debug /p:Platform=x64 `
           /p:AppxPackageSigningEnabled=false /m /v:minimal
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE." }

Write-Host "==> Syncing TFM build output (DLL + XBF) to AppX layout folder..." -ForegroundColor Cyan
Copy-Item -Path "$tfm*" -Destination $appx -Recurse -Force

# XBF files must also be present at the AppX package root (alongside AppxManifest.xml)
# because WinUI 3 resolves XAML binaries relative to the package root, not the exe subfolder.
$appxRoot = Join-Path $PSScriptRoot "bin\x64\Debug\AppX\"
Get-ChildItem -Path "$tfm" -Filter "*.xbf" | ForEach-Object {
    Copy-Item -Path $_.FullName -Destination $appxRoot -Force
}

Write-Host "==> Syncing publish output (runtime dependencies) to AppX layout folder..." -ForegroundColor Cyan
Copy-Item -Path "$publish*" -Destination $appx -Recurse -Force

Write-Host "==> Registering loose package..." -ForegroundColor Cyan
Add-AppxPackage -Register $manifest -ForceUpdateFromAnyVersion

Write-Host "==> Deployment complete." -ForegroundColor Green
