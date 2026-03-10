using System.Runtime.CompilerServices;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Microsoft.AI.Foundry.Local;

namespace SmrtPad.AI;

/// <summary>
/// Adapts Foundry Local SDK for GPU/CPU inference via its native OpenAI-compatible streaming API.
/// </summary>
internal sealed class ConcreteFoundryModelAdapter : ILanguageModelAdapter
{
    private readonly Model _model;
    private readonly OpenAIChatClient _chatClient;

    private ConcreteFoundryModelAdapter(Model model, OpenAIChatClient chatClient)
    {
        _model = model;
        _chatClient = chatClient;
    }

    /// <summary>Creates and initializes a Foundry Local adapter for the given <paramref name="target"/>.</summary>
    public static async Task<ConcreteFoundryModelAdapter> CreateAsync(
        AIExecutionTarget target, CancellationToken ct = default)
    {
        string alias = target switch
        {
            AIExecutionTarget.FoundryLocalGpu => "phi-3.5-mini-instruct",
            AIExecutionTarget.FoundryLocalCpu => "phi-3.5-mini-instruct",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target,
                "ConcreteFoundryModelAdapter only supports GPU and CPU targets.")
        };

        var config = new Configuration
        {
            AppName = "SmrtPad",
            LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Warning,
        };

        ct.ThrowIfCancellationRequested();

        await FoundryLocalManager.CreateAsync(config, logger: null!).ConfigureAwait(false);
        var manager = FoundryLocalManager.Instance;

        var catalog = await manager.GetCatalogAsync().ConfigureAwait(false);
        var model = await catalog.GetModelAsync(alias).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Model '{alias}' not found in local catalog.");

        // Select CPU variant explicitly when target is CPU
        if (target == AIExecutionTarget.FoundryLocalCpu)
        {
            var cpuVariant = model.Variants.FirstOrDefault(v =>
                v.Info.Runtime?.DeviceType == DeviceType.CPU);

            if (cpuVariant is not null)
                model.SelectVariant(cpuVariant);
        }

        ct.ThrowIfCancellationRequested();

        await model.DownloadAsync().ConfigureAwait(false);
        await model.LoadAsync().ConfigureAwait(false);

        var chatClient = await model.GetChatClientAsync().ConfigureAwait(false);

        return new ConcreteFoundryModelAdapter(model, chatClient);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct)
    {
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
        // Foundry Local chat models do not natively support embeddings.
        // Return an empty array; callers should use a dedicated embedding model.
        return Task.FromResult(Array.Empty<float>());
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try { await _model.UnloadAsync().ConfigureAwait(false); }
        catch { /* best-effort cleanup */ }
    }
}
