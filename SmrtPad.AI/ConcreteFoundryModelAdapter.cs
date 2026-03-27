using System.Runtime.CompilerServices;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging.Abstractions;

namespace SmrtPad.AI;

/// <summary>
/// Adapts Foundry Local SDK for GPU/CPU inference via its native OpenAI-compatible streaming API.
/// </summary>
internal sealed class ConcreteFoundryModelAdapter : ILanguageModelAdapter
{
    private static readonly SemaphoreSlim ManagerInitializationLock = new(1, 1);
    private static bool s_managerInitialized;

    private readonly Model _model;
    private readonly OpenAIChatClient _chatClient;
    private readonly int _maxContextTokens;

    private ConcreteFoundryModelAdapter(Model model, OpenAIChatClient chatClient, int maxContextTokens)
    {
        _model = model;
        _chatClient = chatClient;
        _maxContextTokens = maxContextTokens;
    }

    /// <summary>Creates and initializes a Foundry Local adapter for the given <paramref name="target"/>.</summary>
    public static async Task<ConcreteFoundryModelAdapter> CreateAsync(
        AIExecutionTarget target,
        string alias,
        int maxContextTokens,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        onProgress?.Invoke("AI_STAGE_SERVICE");
        var manager = await EnsureManagerAsync(ct).ConfigureAwait(false);
        var model = await GetModelAsync(manager, target, alias, ct).ConfigureAwait(false);

        bool alreadyCached = await model.IsCachedAsync().WaitAsync(ct).ConfigureAwait(false);
        if (alreadyCached)
        {
            onProgress?.Invoke("AI_STAGE_CACHED");
        }
        else
        {
            // Get expected file size from the selected variant for progress reporting
            long expectedBytes = GetExpectedBytes(model);
            string? cachePath = await TryGetCachePathAsync(model, ct).ConfigureAwait(false);

            // Start polling file size in the background while DownloadAsync runs
            using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var pollTask = PollDownloadProgressAsync(cachePath, expectedBytes, alias, onProgress, pollCts.Token);

            try
            {
                onProgress?.Invoke($"AI_STAGE_DOWNLOADING\t{alias}\t{expectedBytes / (1024 * 1024)}");
                await model.DownloadAsync().WaitAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                await pollCts.CancelAsync().ConfigureAwait(false);
                try { await pollTask.ConfigureAwait(false); } catch (OperationCanceledException) { }
            }
        }

        ct.ThrowIfCancellationRequested();
        onProgress?.Invoke("AI_STAGE_LOADING");
        await model.LoadAsync().WaitAsync(ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        var chatClient = await model.GetChatClientAsync().WaitAsync(ct).ConfigureAwait(false);

        return new ConcreteFoundryModelAdapter(model, chatClient, maxContextTokens);
    }

    private static long GetExpectedBytes(Model model)
    {
        // Use the first variant for size estimation (SelectPreferredVariant runs before this)
        var info = model.Variants.FirstOrDefault()?.Info;
        if (info is not null)
        {
            try
            {
                // FileSizeInBytes is the authoritative field when present
                var prop = info.GetType().GetProperty("FileSizeInBytes")
                    ?? info.GetType().GetProperty("FileSizeMb");
                if (prop?.GetValue(info) is long bytes && bytes > 0)
                    return bytes;
                if (prop?.GetValue(info) is int mb && mb > 0)
                    return (long)mb * 1024 * 1024;
            }
            catch { }
        }
        return 0;
    }

    private static async Task<string?> TryGetCachePathAsync(Model model, CancellationToken ct)
    {
        try
        {
            return await model.GetPathAsync().WaitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static async Task PollDownloadProgressAsync(
        string? cachePath,
        long expectedBytes,
        string alias,
        Action<string>? onProgress,
        CancellationToken ct)
    {
        if (onProgress is null || expectedBytes <= 0 || string.IsNullOrEmpty(cachePath))
        {
            // Still poll periodically with an indeterminate message
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(2000, ct).ConfigureAwait(false);
                onProgress?.Invoke($"AI_STAGE_DOWNLOADING\t{alias}\t0");
            }
            return;
        }

        var dir = Path.GetDirectoryName(cachePath);
        int lastPct = -1;
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(800, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) break;
            try
            {
                long downloaded = 0;
                if (Directory.Exists(dir))
                {
                    foreach (var f in Directory.GetFiles(dir!, "*", SearchOption.AllDirectories))
                    {
                        try { downloaded += new FileInfo(f).Length; } catch { }
                    }
                }

                int pct = expectedBytes > 0 ? (int)Math.Min(99, downloaded * 100 / expectedBytes) : 0;
                if (pct != lastPct)
                {
                    lastPct = pct;
                    onProgress?.Invoke($"AI_STAGE_DOWNLOADING\t{alias}\t{expectedBytes / (1024 * 1024)}\t{pct}");
                }
            }
            catch { }
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        prompt = TextChunker.TruncateToTokens(prompt, _maxContextTokens);

        var messages = new List<ChatMessage>
        {
            new() { Role = "user", Content = prompt }
        };

        var stream = _chatClient.CompleteChatStreamingAsync(messages, ct);

        await foreach (var chunk in stream.ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            var text = chunk.Choices?.FirstOrDefault()?.Message?.Content;
            if (!string.IsNullOrEmpty(text))
                yield return text;
        }
    }

    /// <inheritdoc/>
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(text);
        ct.ThrowIfCancellationRequested();

        // Foundry Local chat models do not natively support embeddings.
        // Return an empty array; callers should use a dedicated embedding model.
        return Task.FromResult(Array.Empty<float>());
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        return new ValueTask(_model.UnloadAsync());
    }

    private static async Task<FoundryLocalManager> EnsureManagerAsync(CancellationToken ct)
    {
        if (s_managerInitialized)
            return FoundryLocalManager.Instance;

        await ManagerInitializationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!s_managerInitialized)
            {
                await FoundryLocalManager.CreateAsync(CreateConfiguration(), NullLogger.Instance)
                    .WaitAsync(ct)
                    .ConfigureAwait(false);
                s_managerInitialized = true;
            }

            return FoundryLocalManager.Instance;
        }
        finally
        {
            ManagerInitializationLock.Release();
        }
    }

    private static Configuration CreateConfiguration()
    {
        return new Configuration
        {
            AppName = "SmrtPad",
            LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Warning,
        };
    }

    private static async Task<Model> GetModelAsync(FoundryLocalManager manager, AIExecutionTarget target, string alias, CancellationToken ct)
    {
        var catalog = await manager.GetCatalogAsync().WaitAsync(ct).ConfigureAwait(false);
        var model = await catalog.GetModelAsync(alias).WaitAsync(ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Model '{alias}' was not found in the local Foundry catalog.");

        SelectPreferredVariant(model, target);
        ct.ThrowIfCancellationRequested();
        return model;
    }

    private static void SelectPreferredVariant(Model model, AIExecutionTarget target)
    {
        ArgumentNullException.ThrowIfNull(model);

        var selectedVariant = target switch
        {
            AIExecutionTarget.FoundryLocalCpu => model.Variants.FirstOrDefault(v => v.Info.Runtime?.DeviceType == DeviceType.CPU),
            AIExecutionTarget.FoundryLocalGpu => model.Variants.FirstOrDefault(v => v.Info.Runtime?.DeviceType == DeviceType.GPU),
            _ => null,
        };

        if (selectedVariant is not null)
            model.SelectVariant(selectedVariant);
    }
}
