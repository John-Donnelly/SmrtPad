using System.Runtime.CompilerServices;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;

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

        EnsurePackageIdentity();

        var readyState = LanguageModel.GetReadyState();
        switch (readyState)
        {
            case AIFeatureReadyState.Ready:
                break;

            case AIFeatureReadyState.NotReady:
                var ensureResult = await LanguageModel.EnsureReadyAsync();
                ct.ThrowIfCancellationRequested();

                if (ensureResult.ExtendedError is not null)
                {
                    throw new InvalidOperationException(
                        $"Phi Silica model preparation failed: {ensureResult.ExtendedError.Message}",
                        ensureResult.ExtendedError);
                }

                if (ensureResult.Status != AIFeatureReadyResultState.Success)
                {
                    throw new InvalidOperationException(
                        $"Phi Silica model could not be prepared: {ensureResult.Status}");
                }

                break;

            default:
                throw new InvalidOperationException(GetUnsupportedReadyStateMessage(readyState));
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
        Exception? operationException = null;
        operation.Progress = (_, partialResult) =>
        {
            if (!string.IsNullOrEmpty(partialResult))
                tokenChannel.Writer.TryWrite(partialResult);
        };

        // Register cancellation
        using var cancellationRegistration = ct.Register(() =>
        {
            operation.Cancel();
            tokenChannel.Writer.TryComplete();
        });

        // Await the full result and close the channel
        var completionTask = Task.Run(async () =>
        {
            try
            {
                await operation;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                operationException = ex;
            }
            finally
            {
                tokenChannel.Writer.TryComplete();
            }
        }, CancellationToken.None);

        await foreach (var token in tokenChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return token;
        }

        await completionTask.ConfigureAwait(false);
        if (operationException is not null)
            throw new InvalidOperationException("Phi Silica text generation failed.", operationException);
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

    private static void EnsurePackageIdentity()
    {
        try
        {
            _ = Package.Current.Id.FullName;
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                "Phi Silica requires the app to be running with registered package identity.",
                ex);
        }
        catch (COMException ex) when (ex.HResult == unchecked((int)0x80070490))
        {
            throw new InvalidOperationException(
                "Phi Silica could not resolve the app's package registration. Re-run the packaged deployment before using AI.",
                ex);
        }
    }

    private static string GetUnsupportedReadyStateMessage(AIFeatureReadyState readyState)
    {
        return string.Equals(readyState.ToString(), "NotSupportedOnCurrentSystem", StringComparison.Ordinal)
            ? "Phi Silica is not supported on this device. A Copilot+ PC with the required Windows AI components is required."
            : $"Phi Silica is unavailable. Reported readiness state: {readyState}.";
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
