using System.Net.Http;
using System.Text.Json;

namespace SmrtPad.AI;

/// <summary>
/// Downloads ONNX GenAI model files from HuggingFace Hub into the local SmrtPad model cache.
/// The cache root is <c>%LOCALAPPDATA%\SmrtPad\models\</c>.
/// </summary>
internal static class ModelDownloadService
{
    private const string HuggingFaceBase = "https://huggingface.co";

    // Shared client; 2-hour timeout to accommodate large model files.
    private static readonly HttpClient s_http = new()
    {
        Timeout = TimeSpan.FromHours(2),
    };

    /// <summary>
    /// Returns the local directory where the given model variant is (or will be) cached.
    /// </summary>
    public static string GetLocalModelDirectory(string alias, bool isGpu)
    {
        ArgumentNullException.ThrowIfNull(alias);
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmrtPad", "models", alias, isGpu ? "gpu" : "cpu");
    }

    /// <summary>
    /// Ensures the model files for <paramref name="alias"/> are present locally.
    /// Downloads from HuggingFace Hub if absent. Reports progress via <paramref name="onProgress"/>.
    /// Returns the local directory path ready for use by <c>OrtGenAi.Model</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no HuggingFace source is configured for the given alias/variant.
    /// </exception>
    public static async Task<string> EnsureModelAsync(
        string alias,
        bool isGpu,
        Action<string>? onProgress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(alias);

        var info = ModelSizeSelector.GetHuggingFaceInfo(alias)
            ?? throw new InvalidOperationException(
                $"No HuggingFace source is configured for model '{alias}'. " +
                "Place the model files manually in the SmrtPad models directory.");

        var subdir = isGpu ? info.GpuSubdir : info.CpuSubdir;
        if (subdir is null)
            throw new InvalidOperationException(
                $"Model '{alias}' has no {(isGpu ? "GPU" : "CPU")} variant registered.");

        var localDir = GetLocalModelDirectory(alias, isGpu);

        // A complete download is signalled by the presence of genai_config.json.
        if (File.Exists(Path.Combine(localDir, "genai_config.json")))
        {
            onProgress?.Invoke("AI_STAGE_CACHED");
            return localDir;
        }

        Directory.CreateDirectory(localDir);

        var files = await ListFilesAsync(info.Repo, subdir, ct).ConfigureAwait(false);
        long totalBytes = files.Sum(f => f.Size);
        long downloadedBytes = 0;

        onProgress?.Invoke($"AI_STAGE_DOWNLOADING\t{alias}\t{totalBytes / (1024 * 1024)}");

        foreach (var (filePath, fileSize) in files)
        {
            ct.ThrowIfCancellationRequested();

            var localFilePath = Path.Combine(localDir, Path.GetFileName(filePath));

            // Skip files already fully downloaded.
            if (File.Exists(localFilePath) && new FileInfo(localFilePath).Length == fileSize)
            {
                downloadedBytes += fileSize;
                continue;
            }

            var url = $"{HuggingFaceBase}/{info.Repo}/resolve/main/{filePath}";
            await DownloadFileAsync(url, localFilePath, bytesWritten =>
            {
                downloadedBytes += bytesWritten;
                int pct = totalBytes > 0
                    ? (int)Math.Min(99, downloadedBytes * 100 / totalBytes)
                    : 0;
                onProgress?.Invoke(
                    $"AI_STAGE_DOWNLOADING\t{alias}\t{totalBytes / (1024 * 1024)}\t{pct}");
            }, ct).ConfigureAwait(false);
        }

        return localDir;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static async Task<IReadOnlyList<(string Path, long Size)>> ListFilesAsync(
        string repo, string subdir, CancellationToken ct)
    {
        var url = $"{HuggingFaceBase}/api/models/{repo}/tree/main/{Uri.EscapeDataString(subdir)}";
        var json = await s_http.GetStringAsync(url, ct).ConfigureAwait(false);

        var results = new List<(string, long)>();
        using var doc = JsonDocument.Parse(json);

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var typeEl) ||
                typeEl.GetString() != "file")
                continue;

            if (!item.TryGetProperty("path", out var pathEl))
                continue;

            long size = item.TryGetProperty("size", out var sizeEl) ? sizeEl.GetInt64() : 0;
            results.Add((pathEl.GetString()!, size));
        }

        return results;
    }

    private static async Task DownloadFileAsync(
        string url,
        string localPath,
        Action<long> onBytesWritten,
        CancellationToken ct)
    {
        var tempPath = localPath + ".tmp";
        try
        {
            using var response = await s_http
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var remote = await response.Content
                .ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var local = new FileStream(
                tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81_920, useAsync: true);

            var buffer = new byte[81_920];
            int read;
            while ((read = await remote.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await local.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                onBytesWritten(read);
            }

            await local.FlushAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // Clean up partial download before propagating.
            try { File.Delete(tempPath); } catch { }
            throw;
        }

        File.Move(tempPath, localPath, overwrite: true);
    }
}
