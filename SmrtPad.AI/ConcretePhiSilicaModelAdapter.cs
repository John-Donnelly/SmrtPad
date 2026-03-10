using System.Runtime.CompilerServices;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;

namespace SmrtPad.AI;

/// <summary>
/// Adapts the Windows AI Phi Silica LanguageModel (NPU path) for use by <see cref="AIDispatcher"/>.
/// Requires a Copilot+ PC with NPU support.
/// </summary>
internal sealed class ConcretePhiSilicaModelAdapter : ILanguageModelAdapter
{
    private LanguageModel? _languageModel;

    private ConcretePhiSilicaModelAdapter(LanguageModel languageModel)
    {
        _languageModel = languageModel;
    }

    /// <summary>Creates and initializes the Phi Silica language model on the NPU.</summary>
    public static async Task<ConcretePhiSilicaModelAdapter> CreateAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (LanguageModel.GetReadyState() == AIFeatureReadyState.NotReady)
        {
            var ensureResult = await LanguageModel.EnsureReadyAsync();
            if (ensureResult.Status != AIFeatureReadyResultState.Success)
            {
                throw new InvalidOperationException(
                    $"Phi Silica model could not be prepared: {ensureResult.Status}");
            }
        }

        ct.ThrowIfCancellationRequested();

        var model = await LanguageModel.CreateAsync();
        return new ConcretePhiSilicaModelAdapter(model);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (_languageModel is null)
            throw new ObjectDisposedException(nameof(ConcretePhiSilicaModelAdapter));

        // LanguageModel.GenerateResponseAsync returns IAsyncOperationWithProgress<LanguageModelResponseResult, string>.
        // The Progress callback delivers partial/streaming tokens.
        var tokenChannel = System.Threading.Channels.Channel.CreateUnbounded<string>();

        var operation = _languageModel.GenerateResponseAsync(prompt);
        operation.Progress = (_, partialResult) =>
        {
            if (!string.IsNullOrEmpty(partialResult))
                tokenChannel.Writer.TryWrite(partialResult);
        };

        // Register cancellation
        ct.Register(() =>
        {
            operation.Cancel();
            tokenChannel.Writer.TryComplete();
        });

        // Await the full result and close the channel
        _ = Task.Run(async () =>
        {
            try
            {
                await operation;
            }
            catch { /* cancelled or failed — channel will be completed */ }
            finally
            {
                tokenChannel.Writer.TryComplete();
            }
        }, CancellationToken.None);

        await foreach (var token in tokenChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return token;
        }
    }

    /// <inheritdoc/>
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        if (_languageModel is null)
            throw new ObjectDisposedException(nameof(ConcretePhiSilicaModelAdapter));

        ct.ThrowIfCancellationRequested();

        // Use GenerateEmbeddingVectors when available on WinAppSDK 1.8.1+
        // For now return empty — full embedding support depends on device capabilities
        return Task.FromResult(Array.Empty<float>());
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_languageModel is not null)
        {
            _languageModel.Dispose();
            _languageModel = null;
        }
        return ValueTask.CompletedTask;
    }
}
