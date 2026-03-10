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

    /// <summary>Detects hardware and loads the model. Idempotent.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>Streams generated tokens for the given <paramref name="prompt"/>.</summary>
    Task StreamResponseAsync(
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
}
