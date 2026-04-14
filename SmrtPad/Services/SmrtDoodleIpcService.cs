using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmrtPad.Services;

/// <summary>
/// Manages IPC between SmrtPad and SmrtDoodle for the Create A Drawing workflow.
///
/// IPC contract (file-based, accessible from both sandboxed MSIX processes via %TEMP%):
///   1. SmrtPad writes <c>%TEMP%\SmrtSuite\pending-request.json</c> containing the
///      path where SmrtDoodle should save the exported PNG.
///   2. SmrtPad launches SmrtDoodle via its registered executable path.
///   3. SmrtDoodle reads the request on startup and, when the user closes the window,
///      saves the drawing as a PNG to <c>OutputPath</c>.
///   4. SmrtPad detects the file and inserts it into the active document; if the file
///      is absent after SmrtDoodle exits the user cancelled without saving.
/// </summary>
internal sealed class SmrtDoodleIpcService
{
    private static readonly string s_ipcDirectory =
        Path.Combine(Path.GetTempPath(), "SmrtSuite");

    private static readonly string s_requestFilePath =
        Path.Combine(s_ipcDirectory, "pending-request.json");

    private static readonly string s_windowsAppsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft", "WindowsApps");

    /// <summary>
    /// Finds the SmrtDoodle executable path, or <c>null</c> if SmrtDoodle is not installed.
    /// </summary>
    public static string? FindExecutable()
    {
        var candidate = Path.Combine(s_windowsAppsPath, "SmrtDoodle.exe");
        if (File.Exists(candidate)) return candidate;

        foreach (var dir in Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? [])
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            candidate = Path.Combine(dir.Trim(), "SmrtDoodle.exe");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    /// <summary>
    /// Writes the IPC handshake, launches SmrtDoodle, and waits for it to close.
    /// </summary>
    /// <returns>
    /// The absolute path of the PNG written by SmrtDoodle, or <c>null</c> when the
    /// user closed SmrtDoodle without saving a drawing.
    /// </returns>
    /// <exception cref="InvalidOperationException">SmrtDoodle could not be found or started.</exception>
    public async Task<string?> LaunchAndAwaitAsync(CancellationToken ct = default)
    {
        string? exe = FindExecutable()
            ?? throw new InvalidOperationException("SmrtDoodle executable not found.");

        Directory.CreateDirectory(s_ipcDirectory);
        CleanupStaleFiles();

        // Each session gets a unique output path so concurrent launches don't collide.
        string outputPath = Path.Combine(s_ipcDirectory, $"{Guid.NewGuid():N}.png");

        var request = new SmrtDoodleIpcRequest(outputPath, Version: 1);
        string json = JsonSerializer.Serialize(
            request, SmrtDoodleIpcRequestContext.Default.SmrtDoodleIpcRequest);
        await File.WriteAllTextAsync(s_requestFilePath, json, ct);

        var psi = new ProcessStartInfo(exe) { UseShellExecute = true };
        using var launchProcess = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start SmrtDoodle.");

        // MSIX apps are launched via a stub executable in WindowsApps\ that activates
        // the packaged host and exits immediately.  In that case, poll for the real
        // SmrtDoodle process; for a direct (non-MSIX) executable we can await it.
        bool isMsixStub = exe.StartsWith(s_windowsAppsPath, StringComparison.OrdinalIgnoreCase);
        if (isMsixStub)
            await PollForCompletionAsync(outputPath, ct);
        else
            await launchProcess.WaitForExitAsync(ct);

        TryDelete(s_requestFilePath);
        return File.Exists(outputPath) ? outputPath : null;
    }

    // Polls until SmrtDoodle writes the output file or the SmrtDoodle process is gone.
    private static async Task PollForCompletionAsync(string outputPath, CancellationToken ct)
    {
        // Allow time for the MSIX host process to activate before we start watching it.
        await Task.Delay(TimeSpan.FromSeconds(2), ct);

        while (!ct.IsCancellationRequested)
        {
            if (File.Exists(outputPath))
                return;

            if (Process.GetProcessesByName("SmrtDoodle").Length == 0)
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(500), ct);
        }
    }

    private static void CleanupStaleFiles()
    {
        TryDelete(s_requestFilePath);
        try
        {
            foreach (var file in Directory.GetFiles(s_ipcDirectory, "*.png"))
            {
                if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddHours(-1))
                    TryDelete(file);
            }
        }
        catch { /* best-effort */ }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch { /* best-effort */ }
    }
}

/// <param name="OutputPath">Absolute path where SmrtDoodle should save the exported PNG.</param>
/// <param name="Version">Protocol version — currently always 1.</param>
internal sealed record SmrtDoodleIpcRequest(string OutputPath, int Version);

[System.Text.Json.Serialization.JsonSerializable(typeof(SmrtDoodleIpcRequest))]
internal sealed partial class SmrtDoodleIpcRequestContext
    : System.Text.Json.Serialization.JsonSerializerContext { }
