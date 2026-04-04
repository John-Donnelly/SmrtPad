using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmrtPad.UITests.Infrastructure;

/// <summary>
/// Filters the benchmark model list to only those that are eligible to run on the
/// remote system's hardware, mirroring the application's <c>ModelSizeSelector</c>
/// behaviour. Uses the same headroom factor (10%) and ordered preference list.
/// </summary>
internal static class RemoteModelFilter
{
    /// <summary>
    /// Headroom factor: a model is eligible when <c>footprint × 1.10 ≤ budget</c>,
    /// i.e. the model occupies at most ~91% of available memory, leaving ≥10% overhead.
    /// Must match <c>ModelSizeSelector.HeadroomFactor</c>.
    /// </summary>
    private const double HeadroomFactor = 1.10;

    /// <summary>
    /// All known Foundry Local model aliases with their GPU and CPU footprints in MB.
    /// Kept in sync with <c>ModelSizeSelector.PreferredAliases</c> in SmrtPad.AI.
    /// Ordered from largest (most capable) to smallest (most compatible).
    /// </summary>
    private static readonly (string Alias, long GpuMb, long CpuMb)[] KnownModels =
    [
        ("deepseek-r1-14b",     10_065,  11_786),
        ("gpt-oss-20b",          9_882,  12_552),
        ("qwen2.5-14b",          9_000,  11_325),
        ("qwen2.5-coder-14b",    9_000,  11_325),
        ("phi-4",                8_570,  10_403),
        ("deepseek-r1-7b",       5_406,   6_584),
        ("qwen2.5-7b",           4_843,   6_307),
        ("qwen2.5-coder-7b",     4_843,   6_307),
        ("mistral-7b-v0.2",      4_075,   4_167),
        ("phi-4-mini",           3_686,   4_915),
        ("phi-4-mini-reasoning", 3_225,   4_628),
        ("phi-3.5-mini",         2_181,   2_590),
        ("phi-3-mini-128k",      2_181,   2_600),
        ("phi-3-mini-4k",        2_181,   2_590),
        ("qwen2.5-coder-1.5b",   1_280,   1_822),
        ("qwen2.5-1.5b",         1_280,   1_822),
        ("qwen2.5-coder-0.5b",     528,     822),
        ("qwen2.5-0.5b",           528,     822),
    ];

    /// <summary>
    /// Returns the model aliases that are eligible to run on the given hardware,
    /// ordered from largest/most-capable to smallest/most-compatible.
    /// </summary>
    public static IReadOnlyList<string> GetEligibleModels(RemoteHardwareInfo hardware)
    {
        ArgumentNullException.ThrowIfNull(hardware);

        bool isGpu = hardware.HasGpu;
        long budgetMb = isGpu ? hardware.GpuVramMb : hardware.SystemRamMb;

        if (budgetMb <= 0)
            return KnownModels.Select(static m => m.Alias).ToList();

        return KnownModels
            .Where(m => IsEligible(isGpu ? m.GpuMb : m.CpuMb, budgetMb))
            .Select(static m => m.Alias)
            .ToList();
    }

    /// <summary>
    /// Returns the best (largest) model alias that fits within the hardware budget.
    /// </summary>
    public static string GetBestAlias(RemoteHardwareInfo hardware)
    {
        var eligible = GetEligibleModels(hardware);
        return eligible.Count > 0 ? eligible[0] : "phi-4-mini";
    }

    private static bool IsEligible(long footprintMb, long budgetMb)
        => budgetMb > 0 && (long)(footprintMb * HeadroomFactor) <= budgetMb;
}

/// <summary>
/// Pre-downloads AI models on the remote test machine via Foundry Local CLI before
/// the benchmark suite starts, so that download time is not included in benchmark
/// measurements and model switching doesn't timeout during tests.
/// </summary>
internal static class RemoteModelPreloader
{
    /// <summary>Maximum time to wait for a single model download (minutes).</summary>
    private const int SingleModelTimeoutMinutes = 30;

    /// <summary>
    /// Pre-downloads all eligible models on the remote machine.
    /// Uses Foundry Local's <c>foundry model pull</c> CLI command via PowerShell remoting.
    /// </summary>
    /// <param name="remoteHost">Hostname or IP of the remote machine.</param>
    /// <param name="models">Model aliases to pre-download.</param>
    /// <param name="isGpu">True if the remote system uses GPU inference.</param>
    /// <param name="remoteUser">Optional WinRM username.</param>
    /// <param name="remotePassword">Optional WinRM password.</param>
    /// <param name="log">Optional logging callback.</param>
    /// <returns>Dictionary of model alias → success/failure.</returns>
    public static Dictionary<string, bool> PreloadModels(
        string remoteHost,
        IReadOnlyList<string> models,
        bool isGpu,
        string? remoteUser = null,
        string? remotePassword = null,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(remoteHost);
        ArgumentNullException.ThrowIfNull(models);

        var results = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        if (models.Count == 0)
        {
            log?.Invoke("[ModelPreloader] No models to preload.");
            return results;
        }

        log?.Invoke($"[ModelPreloader] Pre-downloading {models.Count} model(s) on {remoteHost}...");

        // Build a PowerShell script that checks each model and downloads if needed.
        // Foundry Local registers models by pulling them; the SDK handles caching.
        // We use 'foundry model pull <alias>' which downloads the model weights.
        var executionProvider = isGpu ? "cuda" : "cpu";

        foreach (var model in models)
        {
            log?.Invoke($"[ModelPreloader] Checking/downloading '{model}' ({executionProvider})...");

            // Build the PowerShell script as a plain (non-interpolated) string to
            // avoid conflicts between C# interpolation and PowerShell's $-syntax.
            // Model alias is injected via simple string.Replace.
            var script = """
                $ErrorActionPreference = 'Continue'
                try {
                    $foundry = Get-Command foundry -ErrorAction SilentlyContinue
                    if (-not $foundry) {
                        # Foundry is a per-user MSIX; check all user profiles since the
                        # interactive user may differ from the WinRM session user.
                        $profileRoots = @($env:LOCALAPPDATA)
                        $usersDir = Split-Path $env:USERPROFILE -Parent
                        if (Test-Path $usersDir) {
                            Get-ChildItem $usersDir -Directory -ErrorAction SilentlyContinue |
                                ForEach-Object { $profileRoots += (Join-Path $_.FullName 'AppData\Local') }
                        }
                        foreach ($root in ($profileRoots | Select-Object -Unique)) {
                            $checks = @(
                                (Join-Path $root 'Microsoft\FoundryLocal\foundry.exe'),
                                (Join-Path $root 'Microsoft\WindowsApps\foundry.exe')
                            )
                            foreach ($c in $checks) {
                                if (Test-Path $c) { $foundry = Get-Item $c; break }
                            }
                            if ($foundry) { break }
                        }
                    }
                    if (-not $foundry) {
                        $prog = Join-Path $env:ProgramFiles 'Microsoft\FoundryLocal\foundry.exe'
                        if (Test-Path $prog) { $foundry = Get-Item $prog }
                    }
                    if (-not $foundry) {
                        Write-Output 'RESULT=SKIP_NO_CLI'
                        return
                    }

                    $foundryPath = if ($foundry -is [System.Management.Automation.ApplicationInfo]) { $foundry.Source } else { $foundry.FullName }

                    $proc = Start-Process -FilePath $foundryPath -ArgumentList 'model pull __MODEL_ALIAS__' -Wait -PassThru -NoNewWindow
                    if ($proc.ExitCode -eq 0) {
                        Write-Output 'RESULT=OK'
                    } else {
                        Write-Output 'RESULT=FAIL'
                    }
                } catch {
                    Write-Output "RESULT=ERROR:$($_.Exception.Message)"
                }
                """.Replace("__MODEL_ALIAS__", model);

            var output = RunRemoteScript(remoteHost, remoteUser, remotePassword, script, log);
            var resultLine = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(l => l.Trim().StartsWith("RESULT=", StringComparison.OrdinalIgnoreCase));

            if (resultLine is not null && resultLine.Contains("RESULT=OK", StringComparison.OrdinalIgnoreCase))
            {
                log?.Invoke($"[ModelPreloader] '{model}' ready ✓");
                results[model] = true;
            }
            else if (resultLine is not null && resultLine.Contains("RESULT=SKIP_NO_CLI", StringComparison.OrdinalIgnoreCase))
            {
                log?.Invoke($"[ModelPreloader] Foundry CLI not found on remote — skipping preload (models will download on first use)");
                results[model] = true; // Not fatal; the app downloads on demand
            }
            else
            {
                var reason = resultLine ?? output;
                log?.Invoke($"[ModelPreloader] '{model}' preload issue: {reason[..Math.Min(200, reason.Length)]}");
                results[model] = false;
            }
        }

        var successCount = results.Count(r => r.Value);
        log?.Invoke($"[ModelPreloader] Pre-download complete: {successCount}/{models.Count} models ready.");

        return results;
    }

    private static string RunRemoteScript(
        string remoteHost,
        string? remoteUser,
        string? remotePassword,
        string scriptBlock,
        Action<string>? log)
    {
        var credentialSetup = "";
        if (!string.IsNullOrWhiteSpace(remoteUser) && !string.IsNullOrWhiteSpace(remotePassword))
        {
            var escapedPass = remotePassword.Replace("'", "''");
            credentialSetup = $"$secPass = ConvertTo-SecureString '{escapedPass}' -AsPlainText -Force; " +
                              $"$cred = [pscredential]::new('{remoteUser}', $secPass); ";
        }

        var credArg = string.IsNullOrWhiteSpace(credentialSetup) ? "" : " -Credential $cred";
        var command = $"{credentialSetup}Invoke-Command -ComputerName '{remoteHost}'{credArg} -ScriptBlock {{ {scriptBlock} }}";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

        var psi = new ProcessStartInfo("powershell.exe",
            $"-NoProfile -NonInteractive -EncodedCommand {encoded}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            log?.Invoke("[ModelPreloader] Failed to start powershell.exe");
            return string.Empty;
        }

        // Read stdout and stderr concurrently to prevent deadlock when both buffers fill
        var stdoutTask = Task.Run(() => process.StandardOutput.ReadToEnd());
        var stderrTask = Task.Run(() => process.StandardError.ReadToEnd());

        int timeoutMs = SingleModelTimeoutMinutes * 60 * 1000;
        var exited = process.WaitForExit(timeoutMs);
        if (!exited)
        {
            log?.Invoke($"[ModelPreloader] Model pull timed out after {SingleModelTimeoutMinutes} min; killing process.");
            try { process.Kill(entireProcessTree: true); } catch { }
        }

        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();

        if (exited && process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
        {
            log?.Invoke($"[ModelPreloader] Remote script stderr: {stderr[..Math.Min(300, stderr.Length)]}");
        }

        return stdout;
    }
}
