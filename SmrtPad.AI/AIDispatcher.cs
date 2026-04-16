using SmrtPad.AI.Skills;

namespace SmrtPad.AI;

/// <summary>Abstraction for streaming text generation and embedding from a language model.</summary>
public interface ILanguageModelAdapter : IAsyncDisposable
{
    /// <summary>Streams generated tokens for the given <paramref name="prompt"/>.</summary>
    IAsyncEnumerable<string> StreamAsync(string prompt, CancellationToken ct);

    /// <summary>Generates an embedding vector for the given <paramref name="text"/>.</summary>
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct);
}

/// <summary>
/// Orchestrates hardware detection and language model initialization,
/// then dispatches streaming inference and embedding requests.
/// </summary>
public sealed class AIDispatcher : IAsyncDisposable
{
    private readonly HardwareProbeService _hardwareProbe;
    private readonly Func<AIExecutionTarget, HardwareProbeResult, Action<string>?, CancellationToken, Task<ILanguageModelAdapter>> _modelFactory;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private ILanguageModelAdapter? _model;
    private SemanticSearchService? _semanticSearchService;

    /// <summary>The execution target selected after initialization.</summary>
    public AIExecutionTarget ExecutionTarget { get; private set; }

    /// <summary>The latest hardware probe result captured by the dispatcher.</summary>
    public HardwareProbeResult ProbeResult { get; private set; } = HardwareProbeResult.Uninitialized;

    /// <summary>Whether <see cref="InitializeAsync"/> has completed successfully.</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// User-selected model alias override. When set, overrides automatic hardware-based model selection.
    /// </summary>
    public string? PreferredAlias { get; private set; }

    /// <summary>
    /// The alias of the model that was actually loaded during the last successful initialization.
    /// </summary>
    public string? ActiveModelAlias { get; private set; }

    /// <summary>
    /// User-selected execution target override. When set, overrides automatic hardware-based target selection.
    /// Accepted values: <c>"PhiSilicaNpu"</c>, <c>"OnnxRuntimeGpu"</c>, <c>"OnnxRuntimeCpu"</c>.
    /// </summary>
    public string? PreferredExecutionTarget { get; private set; }

    /// <summary>
    /// User-selected reasoning mode override for models that support both thinking and non-thinking behavior.
    /// </summary>
    public ModelReasoningMode PreferredReasoningMode { get; private set; } = ModelReasoningMode.Default;

    public void SetPreferredExecutionTarget(string? target)
    {
        PreferredExecutionTarget = target;
    }

    public void SetPreferredReasoningMode(ModelReasoningMode mode)
    {
        PreferredReasoningMode = mode;
    }

    public AIDispatcher(
        HardwareProbeService hardwareProbe,
        Func<AIExecutionTarget, HardwareProbeResult, Action<string>?, CancellationToken, Task<ILanguageModelAdapter>> modelFactory)
    {
        ArgumentNullException.ThrowIfNull(hardwareProbe);
        ArgumentNullException.ThrowIfNull(modelFactory);
        _hardwareProbe = hardwareProbe;
        _modelFactory = modelFactory;
    }

    /// <summary>
    /// Detects hardware and creates the language model adapter. Idempotent — second calls are no-ops.
    /// </summary>
    public Task InitializeAsync(CancellationToken ct = default) =>
        InitializeCoreAsync(onProgress: null, ct);

    /// <summary>
    /// Detects hardware and loads the model, emitting descriptive progress messages via <paramref name="onProgress"/>.
    /// </summary>
    public Task InitializeAsync(Action<string> onProgress, CancellationToken ct = default) =>
        InitializeCoreAsync(onProgress, ct);

    private async Task InitializeCoreAsync(Action<string>? onProgress, CancellationToken ct)
    {
        if (IsInitialized)
            return;

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsInitialized)
                return;

            ct.ThrowIfCancellationRequested();
            onProgress?.Invoke("AI_STAGE_PROBING");
            ProbeResult = await _hardwareProbe.DetectAsync(ct).ConfigureAwait(false);

            // Apply user execution target override if set and valid
            ExecutionTarget = PreferredExecutionTarget switch
            {
                "PhiSilicaNpu" when ProbeResult.PhiSilica.IsUsable => AIExecutionTarget.PhiSilicaNpu,
                "OnnxRuntimeGpu" when ProbeResult.Gpu.IsUsable => AIExecutionTarget.OnnxRuntimeGpu,
                "OnnxRuntimeCpu" => AIExecutionTarget.OnnxRuntimeCpu,
                _ => ProbeResult.SelectedTarget,
            };

            onProgress?.Invoke("AI_STAGE_SELECTING");
            _model = await _modelFactory(ExecutionTarget, ProbeResult, onProgress, ct).ConfigureAwait(false);

            // Capture the alias that was actually loaded for display purposes
            ActiveModelAlias = ExecutionTarget == AIExecutionTarget.PhiSilicaNpu
                ? "Phi Silica"
                : PreferredAlias ?? ModelSizeSelector.GetBestAliasForCapability(
                    ExecutionTarget == AIExecutionTarget.OnnxRuntimeCpu
                        ? ProbeResult.Gpu with { GpuVramMb = 0 }
                        : ProbeResult.Gpu);

            IsInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Streams model responses token-by-token. Auto-initializes if not yet done.
    /// </summary>
    public async Task StreamResponseAsync(
        string skillKey,
        string prompt,
        Action<string> onToken,
        Action onComplete,
        Action<Exception>? onError = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skillKey);
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(onToken);
        ArgumentNullException.ThrowIfNull(onComplete);

        var builtPrompt = skillKey switch
        {
            "summarize"    => PromptTemplates.Summarize(prompt),
            "tone-professional" => PromptTemplates.ToneProfessional(prompt),
            "tone-casual"  => PromptTemplates.ToneCasual(prompt),
            "rewrite"      => PromptTemplates.Rewrite(prompt),
            "grammar"      => PromptTemplates.GrammarFix(prompt),
            "shorten"      => PromptTemplates.Shorten(prompt),
            "autocomplete" => PromptTemplates.AutoComplete(prompt),
            "semantic"     => PromptTemplates.SemanticQuery(prompt),
            "ocr"          => PromptTemplates.OcrFallback(prompt),
            "freeform"     => PromptTemplates.FreeformChat(prompt),
            "grade"        => prompt, // GradeResponse builds the full prompt externally
            _              => prompt,
        };

        try
        {
            if (!IsInitialized)
                await InitializeAsync(ct).ConfigureAwait(false);

            await foreach (var token in _model!.StreamAsync(builtPrompt, ct).ConfigureAwait(false))
            {
                onToken(token);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation is expected; fall through to onComplete
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
        }

        onComplete();
    }

    /// <summary>Generates an embedding vector. Auto-initializes if not yet done.</summary>
    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!IsInitialized)
            await InitializeAsync(ct).ConfigureAwait(false);

        return await _model!.GenerateEmbeddingAsync(text, ct).ConfigureAwait(false);
    }

    /// <summary>Indexes a document for semantic search. Auto-initializes if not yet done.</summary>
    public async Task IndexDocumentAsync(int tabId, string documentText, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(documentText);

        if (!IsInitialized)
            await InitializeAsync(ct).ConfigureAwait(false);

        _semanticSearchService ??= new SemanticSearchService(this);
        await _semanticSearchService.IndexDocumentAsync(tabId, documentText, ct).ConfigureAwait(false);
    }

    /// <summary>Queries the semantic index. Auto-initializes if not yet done.</summary>
    public async Task<IReadOnlyList<SearchResult>> QuerySemanticAsync(string queryText, int topK = 5, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(queryText);

        if (!IsInitialized)
            await InitializeAsync(ct).ConfigureAwait(false);

        _semanticSearchService ??= new SemanticSearchService(this);
        return await _semanticSearchService.QueryAsync(queryText, topK, ct).ConfigureAwait(false);
    }

    /// <summary>Removes semantic-search index entries for the given tab.</summary>
    public void RemoveIndexedTab(int tabId)
    {
        _semanticSearchService?.RemoveTab(tabId);
    }

    /// <summary>
    /// Stores the user's preferred model alias for use on the next <see cref="InitializeAsync"/> call.
    /// Pass <c>null</c> to revert to automatic hardware-based selection.
    /// </summary>
    public void SetPreferredModelAlias(string? alias)
    {
        PreferredAlias = alias;
    }

    /// <summary>
    /// Returns the model aliases that fit within the detected hardware budget, ordered best-first.
    /// Returns all known aliases when called before initialization.
    /// </summary>
    public IReadOnlyList<string> GetEligibleModelAliases() =>
        ModelSizeSelector.GetEligibleAliases(ProbeResult.Gpu);

    /// <summary>
    /// Returns model aliases that fit within the CPU RAM budget (GPU VRAM ignored), ordered best-first.
    /// Used to enumerate models for CPU-path benchmarking.
    /// </summary>
    public IReadOnlyList<string> GetEligibleCpuModelAliases() =>
        ModelSizeSelector.GetEligibleAliases(ProbeResult.Gpu with { GpuVramMb = 0 });

    /// <summary>
    /// Disposes the current model and resets initialization state so the dispatcher can be
    /// re-initialized (e.g. after the user selects a different model alias).
    /// Unlike <see cref="DisposeAsync"/>, the lock and hardware probe are kept alive.
    /// </summary>
    public async Task ResetAsync()
    {
        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_semanticSearchService is not null)
            {
                await _semanticSearchService.DisposeAsync().ConfigureAwait(false);
                _semanticSearchService = null;
            }

            if (_model is not null)
            {
                await _model.DisposeAsync().ConfigureAwait(false);
                _model = null;
            }

            IsInitialized = false;
            ActiveModelAlias = null;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_semanticSearchService is not null)
        {
            await _semanticSearchService.DisposeAsync().ConfigureAwait(false);
            _semanticSearchService = null;
        }

        if (_model is not null)
        {
            await _model.DisposeAsync().ConfigureAwait(false);
            _model = null;
        }
        IsInitialized = false;
        ProbeResult = HardwareProbeResult.Uninitialized;
        _initLock.Dispose();
    }
}
