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
    private readonly Func<AIExecutionTarget, Task<ILanguageModelAdapter>> _modelFactory;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private ILanguageModelAdapter? _model;

    /// <summary>The execution target selected after initialization.</summary>
    public AIExecutionTarget ExecutionTarget { get; private set; }

    /// <summary>Whether <see cref="InitializeAsync"/> has completed successfully.</summary>
    public bool IsInitialized { get; private set; }

    public AIDispatcher(
        HardwareProbeService hardwareProbe,
        Func<AIExecutionTarget, Task<ILanguageModelAdapter>> modelFactory)
    {
        ArgumentNullException.ThrowIfNull(hardwareProbe);
        ArgumentNullException.ThrowIfNull(modelFactory);
        _hardwareProbe = hardwareProbe;
        _modelFactory = modelFactory;
    }

    /// <summary>
    /// Detects hardware and creates the language model adapter. Idempotent — second calls are no-ops.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (IsInitialized)
            return;

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsInitialized)
                return;

            ct.ThrowIfCancellationRequested();
            ExecutionTarget = await _hardwareProbe.DetectAsync(ct).ConfigureAwait(false);
            _model = await _modelFactory(ExecutionTarget).ConfigureAwait(false);
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
        string prompt,
        Action<string> onToken,
        Action onComplete,
        Action<Exception>? onError = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(onToken);
        ArgumentNullException.ThrowIfNull(onComplete);

        try
        {
            if (!IsInitialized)
                await InitializeAsync(ct).ConfigureAwait(false);

            await foreach (var token in _model!.StreamAsync(prompt, ct).ConfigureAwait(false))
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

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_model is not null)
        {
            await _model.DisposeAsync().ConfigureAwait(false);
            _model = null;
        }
        IsInitialized = false;
        _initLock.Dispose();
    }
}
