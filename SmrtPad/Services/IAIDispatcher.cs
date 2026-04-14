namespace SmrtPad.Services;

/// <summary>
/// Abstraction for AI inference consumed by the main app.
/// The concrete implementation lives in <c>SmrtPad.AI</c> and is loaded
/// at runtime via <see cref="System.Runtime.Loader.AssemblyLoadContext"/>.
/// </summary>
public interface IAIDispatcher
{
    /// <summary>Whether the dispatcher has been initialized.</summary>
    bool IsInitialized { get; }

    /// <summary>User-friendly label for the active execution path (e.g. "NPU", "GPU", "CPU").</summary>
    string ExecutionTargetDisplayName { get; }

    /// <summary>The latest backend availability snapshot reported by the dispatcher.</summary>
    AIDispatcherAvailability Availability { get; }

    /// <summary>Detects hardware and loads the model. Idempotent.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Detects hardware and loads the model, reporting progress messages via <paramref name="onProgress"/>.
    /// The callback is invoked on whatever thread the dispatcher uses internally; marshal to UI as needed.
    /// </summary>
    Task InitializeAsync(Action<string> onProgress, CancellationToken ct = default);

    /// <summary>Streams generated tokens for the given <paramref name="prompt"/> using the specified <paramref name="skillKey"/>.</summary>
    Task StreamResponseAsync(
        string skillKey,
        string prompt,
        Action<string> onToken,
        Action onComplete,
        Action<Exception>? onError = null,
        CancellationToken ct = default);

    /// <summary>Generates an embedding vector for the given <paramref name="text"/>.</summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);

    /// <summary>Indexes a document for semantic search.</summary>
    Task IndexDocumentAsync(int tabId, string documentText, CancellationToken ct = default);

    /// <summary>Queries the semantic index for the highest-scoring chunks.</summary>
    Task<IReadOnlyList<SemanticSearchResult>> QuerySemanticAsync(string queryText, int topK = 5, CancellationToken ct = default);

    /// <summary>Removes indexed semantic-search entries for the given tab.</summary>
    void RemoveIndexedTab(int tabId);

    /// <summary>
    /// Sets the preferred model alias for the next initialization.
    /// Pass <c>null</c> to revert to automatic hardware-based selection.
    /// Has no effect once the dispatcher is initialized; call after reset.
    /// </summary>
    void SetPreferredModelAlias(string? alias);

    /// <summary>The currently preferred model alias, or <c>null</c> if using automatic selection.</summary>
    string? PreferredModelAlias { get; }

    /// <summary>
    /// The alias of the model that was actually loaded during the last successful initialization,
    /// or <c>null</c> if the dispatcher has not yet been initialized.
    /// </summary>
    string? ActiveModelAlias { get; }

    /// <summary>
    /// Returns the model aliases that fit within the detected hardware budget, ordered best-first.
    /// Returns all known aliases when called before initialization.
    /// </summary>
    IReadOnlyList<string> GetEligibleModelAliases();

    /// <summary>
    /// Returns model aliases that fit within the CPU RAM budget (GPU VRAM ignored), ordered best-first.
    /// Use this to populate the model selector when the CPU execution target is selected.
    /// </summary>
    IReadOnlyList<string> GetEligibleCpuModelAliases();

    /// <summary>
    /// Sets the preferred execution target for the next initialization.
    /// Pass <c>null</c> to revert to automatic hardware-based selection.
    /// Accepted values: <c>"PhiSilicaNpu"</c>, <c>"OnnxRuntimeGpu"</c>, <c>"OnnxRuntimeCpu"</c>.
    /// </summary>
    void SetPreferredExecutionTarget(string? target);

    /// <summary>The preferred execution target key, or <c>null</c> if using automatic selection.</summary>
    string? PreferredExecutionTarget { get; }

    /// <summary>
    /// Disposes the current model and resets the initialized state so the dispatcher can be
    /// re-initialized with a different model alias or execution target.
    /// </summary>
    Task ResetAsync();
}
