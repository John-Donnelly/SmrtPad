using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace SmrtPad.UITests.Infrastructure;

/// <summary>
/// Probes the remote test machine's hardware capabilities via PowerShell remoting.
/// Used to filter AI models to only those the remote system can actually run,
/// mirroring the application's <c>ModelSizeSelector</c> behaviour on launch.
/// </summary>
internal static class RemoteHardwareProbe
{
    /// <summary>
    /// Queries the remote machine for GPU VRAM and total system RAM.
    /// Tries <c>nvidia-smi</c> first (NVIDIA GPUs), then falls back to
    /// <c>Win32_VideoController</c> for Intel/AMD integrated or discrete GPUs.
    /// Returns structured hardware info that can be fed into <see cref="RemoteModelFilter"/>.
    /// </summary>
    /// <param name="remoteHost">Hostname or IP of the remote machine.</param>
    /// <param name="remoteUser">Optional WinRM username.</param>
    /// <param name="remotePassword">Optional WinRM password.</param>
    /// <param name="log">Optional logging callback.</param>
    public static RemoteHardwareInfo Probe(
        string remoteHost,
        string? remoteUser = null,
        string? remotePassword = null,
        Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(remoteHost);

        log?.Invoke($"[HardwareProbe] Querying hardware on {remoteHost}...");

        // Build the remote script that gathers GPU VRAM and system RAM
        var script = """
            $gpu = 0
            $gpuName = ''
            try {
                $nvsmi = nvidia-smi --query-gpu=memory.total,gpu_name --format=csv,noheader,nounits 2>$null
                if ($LASTEXITCODE -eq 0 -and $nvsmi) {
                    $parts = $nvsmi.Split(',')
                    $gpu = [int]$parts[0].Trim()
                    if ($parts.Length -gt 1) { $gpuName = $parts[1].Trim() }
                }
            } catch {}

            if ($gpu -eq 0) {
                try {
                    $vc = Get-CimInstance Win32_VideoController |
                          Sort-Object AdapterRAM -Descending |
                          Select-Object -First 1
                    if ($vc -and $vc.AdapterRAM -gt 0) {
                        $gpu = [math]::Round($vc.AdapterRAM / 1MB)
                        $gpuName = $vc.Name
                    }
                } catch {}
            }

            $ram = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1MB)
            $cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name

            Write-Output "GPU_VRAM_MB=$gpu"
            Write-Output "GPU_NAME=$gpuName"
            Write-Output "SYSTEM_RAM_MB=$ram"
            Write-Output "CPU_NAME=$cpu"
            """;

        var output = RunRemotePowerShell(remoteHost, remoteUser, remotePassword, script, log);

        long gpuVramMb = 0;
        long systemRamMb = 0;
        string gpuName = "";
        string cpuName = "";

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("GPU_VRAM_MB=", StringComparison.OrdinalIgnoreCase))
            {
                long.TryParse(trimmed["GPU_VRAM_MB=".Length..], CultureInfo.InvariantCulture, out gpuVramMb);
            }
            else if (trimmed.StartsWith("GPU_NAME=", StringComparison.OrdinalIgnoreCase))
            {
                gpuName = trimmed["GPU_NAME=".Length..].Trim();
            }
            else if (trimmed.StartsWith("SYSTEM_RAM_MB=", StringComparison.OrdinalIgnoreCase))
            {
                long.TryParse(trimmed["SYSTEM_RAM_MB=".Length..], CultureInfo.InvariantCulture, out systemRamMb);
            }
            else if (trimmed.StartsWith("CPU_NAME=", StringComparison.OrdinalIgnoreCase))
            {
                cpuName = trimmed["CPU_NAME=".Length..].Trim();
            }
        }

        var info = new RemoteHardwareInfo(gpuVramMb, systemRamMb, gpuName, cpuName);

        log?.Invoke($"[HardwareProbe] GPU: {info.GpuName} ({info.GpuVramMb} MB VRAM)");
        log?.Invoke($"[HardwareProbe] CPU: {info.CpuName}");
        log?.Invoke($"[HardwareProbe] RAM: {info.SystemRamMb} MB");

        return info;
    }

    /// <summary>
    /// Executes a PowerShell script on the remote machine via <c>Invoke-Command</c>.
    /// </summary>
    private static string RunRemotePowerShell(
        string remoteHost,
        string? remoteUser,
        string? remotePassword,
        string scriptBlock,
        Action<string>? log)
    {
        // Build credential portion if provided
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
            log?.Invoke("[HardwareProbe] Failed to start powershell.exe");
            return string.Empty;
        }

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        var exited = process.WaitForExit(30_000);
        if (!exited)
        {
            log?.Invoke("[HardwareProbe] Remote probe timed out after 30 s; killing process.");
            try { process.Kill(entireProcessTree: true); } catch { }
        }
        else if (process.ExitCode != 0)
        {
            log?.Invoke($"[HardwareProbe] Remote probe failed (exit {process.ExitCode}): {stderr}");
        }

        return stdout;
    }
}

/// <summary>
/// Hardware capabilities of the remote test machine.
/// </summary>
/// <param name="GpuVramMb">Total GPU VRAM in megabytes (0 if no GPU detected).</param>
/// <param name="SystemRamMb">Total system RAM in megabytes.</param>
/// <param name="GpuName">GPU model name (e.g. "NVIDIA GeForce RTX 4060" or "Intel(R) UHD Graphics 605").</param>
/// <param name="CpuName">CPU model name.</param>
public sealed record RemoteHardwareInfo(
    long GpuVramMb,
    long SystemRamMb,
    string GpuName,
    string CpuName)
{
    /// <summary>Returns <c>true</c> when the remote machine has a usable GPU (NVIDIA, Intel, or AMD).</summary>
    public bool HasGpu => GpuVramMb > 0;
}
