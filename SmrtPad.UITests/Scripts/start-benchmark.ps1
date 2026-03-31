<#
.SYNOPSIS
    Starts WinAppDriver and Appium locally for the SmrtPad AI model benchmark suite.

.DESCRIPTION
    1. Starts WinAppDriver if not already running.
    2. Starts Appium 2.x at http://127.0.0.1:4723 if not already running.
    3. Discovers the local SmrtPad AUMID from the deployed AppX package.
    4. Optionally pre-downloads models listed in BENCHMARK_MODEL_FILTER.
    5. Prints a summary of all benchmark-related environment variables.

.NOTES
    Prerequisites:
      - Node.js / npm with Appium installed globally (npm install -g appium)
      - appium-windows-driver installed (appium driver install windows)
      - WinAppDriver 1.2.1 at "C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe"
      - SmrtPad WAP project built and deployed (Debug|x64)
#>

$ErrorActionPreference = 'Stop'

# ── Configuration ────────────────────────────────────────────────────────────
$AppiumAddress  = '127.0.0.1'
$AppiumPort     = 4723
$AppiumUrl      = "http://${AppiumAddress}:${AppiumPort}"
$StatusEndpoint = "${AppiumUrl}/status"
$WinAppDriverPath = 'C:\Program Files (x86)\Windows Application Driver\WinAppDriver.exe'

# Locate Appium executable — check PATH first, then common npm global locations
$appiumCmd = Get-Command appium -ErrorAction SilentlyContinue
if (-not $appiumCmd) {
    $npmGlobal = Join-Path $env:APPDATA 'npm'
    $candidates = @(
        (Join-Path $npmGlobal 'appium.cmd'),
        (Join-Path $npmGlobal 'appium.ps1'),
        (Join-Path $npmGlobal 'appium')
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { $appiumCmd = Get-Item $c; break }
    }
}

if (-not $appiumCmd) {
    Write-Error "Appium not found on PATH or in npm global directory. Run: npm install -g appium"
    exit 1
}

$AppiumExe = if ($appiumCmd -is [System.Management.Automation.ApplicationInfo]) {
    $appiumCmd.Source
} else {
    $appiumCmd.FullName
}

Write-Host "Using Appium at: $AppiumExe" -ForegroundColor Cyan

# ── 1. Start WinAppDriver ───────────────────────────────────────────────────
$wadProcess = Get-Process WinAppDriver -ErrorAction SilentlyContinue
if ($wadProcess) {
    Write-Host "[OK] WinAppDriver already running (PID $($wadProcess.Id))." -ForegroundColor Green
} else {
    if (-not (Test-Path $WinAppDriverPath)) {
        Write-Error "WinAppDriver not found at '$WinAppDriverPath'. Install from https://github.com/microsoft/WinAppDriver/releases"
        exit 1
    }
    Write-Host "Starting WinAppDriver..." -ForegroundColor Yellow
    Start-Process -FilePath $WinAppDriverPath -WindowStyle Minimized
    Start-Sleep -Seconds 2
    Write-Host "[OK] WinAppDriver started." -ForegroundColor Green
}

# ── 2. Start Appium ─────────────────────────────────────────────────────────
function Test-AppiumReady {
    try {
        $response = Invoke-WebRequest -Uri $StatusEndpoint -TimeoutSec 3 -ErrorAction SilentlyContinue
        return $response.StatusCode -eq 200
    } catch {
        return $false
    }
}

if (Test-AppiumReady) {
    Write-Host "[OK] Appium already running at $AppiumUrl." -ForegroundColor Green
} else {
    Write-Host "Starting Appium at $AppiumUrl..." -ForegroundColor Yellow
    Start-Process -FilePath $AppiumExe -ArgumentList "--address $AppiumAddress --port $AppiumPort --relaxed-security" -WindowStyle Minimized

    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        if (Test-AppiumReady) {
            Write-Host "[OK] Appium started and ready." -ForegroundColor Green
            break
        }
        Start-Sleep -Seconds 1
    }

    if (-not (Test-AppiumReady)) {
        Write-Error "Appium did not become ready within 30 seconds at $AppiumUrl."
        exit 1
    }
}

# ── 3. Discover local AUMID ─────────────────────────────────────────────────
$pkg = Get-AppxPackage -Name '*SmrtPad*' -ErrorAction SilentlyContinue
if (-not $pkg) {
    Write-Error "SmrtPad AppX package not found. Build and deploy the WAP project (SmrtPad (Package)) in Debug|x64 first."
    exit 1
}

$aumid = "$($pkg.PackageFamilyName)!App"
Write-Host ""
Write-Host "Local AUMID: $aumid" -ForegroundColor Cyan

# ── 4. Hardware profile ─────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== Hardware Profile ===" -ForegroundColor Magenta
try {
    $gpuInfo = nvidia-smi --query-gpu=gpu_name,power.limit,memory.total --format=csv,noheader 2>$null
    Write-Host "GPU: $gpuInfo"
} catch {
    Write-Host "GPU: nvidia-smi not available"
}
Write-Host "CPU: AMD Ryzen 7 5800X (105W TDP)"
Write-Host ""

# ── 5. Environment variable summary ─────────────────────────────────────────
Write-Host "=== Benchmark Environment Variables ===" -ForegroundColor Magenta
$env:SMRTPAD_APPIUM_SERVER = $AppiumUrl
Write-Host "  SMRTPAD_APPIUM_SERVER   = $env:SMRTPAD_APPIUM_SERVER"
Write-Host "  BENCHMARK_MODEL_FILTER  = $(if ($env:BENCHMARK_MODEL_FILTER) { $env:BENCHMARK_MODEL_FILTER } else { '(all eligible models)' })"
Write-Host "  BENCHMARK_GPU_WATTS     = $(if ($env:BENCHMARK_GPU_WATTS) { $env:BENCHMARK_GPU_WATTS } else { '115.0 (RTX 4060 default)' })"
Write-Host "  BENCHMARK_CPU_WATTS     = $(if ($env:BENCHMARK_CPU_WATTS) { $env:BENCHMARK_CPU_WATTS } else { '105.0 (Ryzen 5800X default)' })"
Write-Host "  BENCHMARK_NPU_WATTS     = $(if ($env:BENCHMARK_NPU_WATTS) { $env:BENCHMARK_NPU_WATTS } else { '15.0 (default)' })"
Write-Host "  BENCHMARK_ELECTRICITY_RATE = $(if ($env:BENCHMARK_ELECTRICITY_RATE) { $env:BENCHMARK_ELECTRICITY_RATE } else { '0.30 (USD/kWh default)' })"
Write-Host ""

# ── 6. Summary ──────────────────────────────────────────────────────────────
Write-Host "=== Ready ===" -ForegroundColor Green
Write-Host "Run the benchmark with:"
Write-Host '  dotnet test SmrtPad.UITests/SmrtPad.UITests.csproj -c Debug -p:Platform=x64 --filter "FullyQualifiedName~ModelBenchmarkTests.RunFullBenchmark"'
Write-Host ""
Write-Host "To test a subset of models:"
Write-Host '  $env:BENCHMARK_MODEL_FILTER="phi-4-mini,phi-3.5-mini"'
Write-Host ""
