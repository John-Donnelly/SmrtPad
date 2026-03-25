using System.Runtime.Loader;

namespace SmrtPad.Services;

/// <summary>
/// Custom <see cref="AssemblyLoadContext"/> for loading the <c>SmrtPad.AI</c> plugin assembly.
/// Falls back to the default context for shared framework assemblies.
/// </summary>
internal sealed class AIAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string _pluginDirectory;

    public AIAssemblyLoadContext(string pluginPath)
        : base(name: "SmrtPad.AI", isCollectible: false)
    {
        _pluginDirectory = Path.GetDirectoryName(pluginPath)!;
    }

    protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName assemblyName)
    {
        // Try to resolve from the plugin directory first; fall back to default context otherwise.
        var candidate = Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll");
        if (File.Exists(candidate))
            return LoadFromAssemblyPath(candidate);

        return null; // fall through to Default context
    }
}

/// <summary>
/// Adapts a <c>SmrtPad.AI.AIDispatcher</c> loaded via <see cref="AIAssemblyLoadContext"/>
/// to the <see cref="IAIDispatcher"/> contract using <see langword="dynamic"/> dispatch.
/// </summary>
internal sealed class AIDispatcherProxy : IAIDispatcher, IAsyncDisposable
{
    private readonly dynamic _dispatcher;

    public AIDispatcherProxy(dynamic dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <inheritdoc/>
    public bool IsInitialized => (bool)_dispatcher.IsInitialized;

    /// <inheritdoc/>
    public AIDispatcherAvailability Availability => MapAvailability(_dispatcher.ProbeResult);

    /// <inheritdoc/>
    public string ExecutionTargetDisplayName
    {
        get
        {
            // AIExecutionTarget is an enum in SmrtPad.AI; map to a display string.
            object target = _dispatcher.ExecutionTarget;
            return target.ToString() switch
            {
                "PhiSilicaNpu" => "⚡ NPU",
                "FoundryLocalGpu" => "🖥️ GPU",
                "FoundryLocalCpu" => "🐢 CPU",
                _ => target.ToString()!,
            };
        }
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken ct = default) =>
        (Task)_dispatcher.InitializeAsync(ct);

    /// <inheritdoc/>
    public Task StreamResponseAsync(
        string prompt,
        Action<string> onToken,
        Action onComplete,
        Action<Exception>? onError = null,
        CancellationToken ct = default) =>
        (Task)_dispatcher.StreamResponseAsync(prompt, onToken, onComplete, onError, ct);

    /// <inheritdoc/>
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default) =>
        (Task<float[]>)_dispatcher.GenerateEmbeddingAsync(text, ct);

    /// <inheritdoc/>
    public Task IndexDocumentAsync(int tabId, string documentText, CancellationToken ct = default) =>
        (Task)_dispatcher.IndexDocumentAsync(tabId, documentText, ct);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SemanticSearchResult>> QuerySemanticAsync(string queryText, int topK = 5, CancellationToken ct = default)
    {
        dynamic results = await _dispatcher.QuerySemanticAsync(queryText, topK, ct);
        var mappedResults = new List<SemanticSearchResult>();
        foreach (var result in results)
        {
            mappedResults.Add(new SemanticSearchResult((int)result.TabId, (string)result.ChunkText, (float)result.Score));
        }

        return mappedResults;
    }

    /// <inheritdoc/>
    public void RemoveIndexedTab(int tabId) =>
        _dispatcher.RemoveIndexedTab(tabId);

    /// <summary>Disposes the underlying AI dispatcher.</summary>
    public async ValueTask DisposeAsync()
    {
        await ((IAsyncDisposable)_dispatcher).DisposeAsync().ConfigureAwait(false);
    }

    private static AIDispatcherAvailability MapAvailability(dynamic probeResult)
    {
        if (probeResult is null)
            return AIDispatcherAvailability.Uninitialized;

        return new AIDispatcherAvailability(
            SelectedTarget: probeResult.SelectedTarget.ToString(),
            PhiSilica: MapBackendAvailability(probeResult.PhiSilica),
            FoundryGpu: MapBackendAvailability(probeResult.FoundryGpu));
    }

    private static AIBackendAvailability MapBackendAvailability(dynamic capability)
    {
        if (capability is null)
        {
            return new AIBackendAvailability(
                BackendName: string.Empty,
                Status: AIBackendAvailabilityStatus.Unknown,
                DiagnosticCode: null,
                DiagnosticMessage: null);
        }

        return new AIBackendAvailability(
            BackendName: (string)capability.BackendName,
            Status: Enum.TryParse<AIBackendAvailabilityStatus>((string)capability.Status.ToString(), out var status)
                ? status
                : AIBackendAvailabilityStatus.Unknown,
            DiagnosticCode: capability.DiagnosticCode,
            DiagnosticMessage: capability.DiagnosticMessage);
    }
}
