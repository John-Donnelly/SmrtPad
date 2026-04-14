using System.Runtime.CompilerServices;
using SmrtPad.AI;

namespace SmrtPad.AI.Tests.Skills;

internal sealed class SkillDispatcherTestContext : IAsyncDisposable
{
    private readonly string[] _tokens;
    private readonly Mock<IExecutionProviderCatalogAdapter> _catalog = new();

    public SkillDispatcherTestContext(params string[] tokens)
    {
        _tokens = tokens;
        _catalog.Setup(c => c.ProbePhiSilicaAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unsupported));
        _catalog.Setup(c => c.ProbeOnnxRuntimeGpuAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIBackendCapability("ORT GenAI GPU", AIBackendAvailabilityStatus.Unavailable));

        Model = new Mock<ILanguageModelAdapter>();
        Model.Setup(m => m.StreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string prompt, CancellationToken ct) => StreamTokensAsync(prompt, ct, _tokens));
        Model.Setup(m => m.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f]);
        Model.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);

        Dispatcher = new AIDispatcher(
            new HardwareProbeService(_catalog.Object),
            (_, _, _, _) => Task.FromResult(Model.Object));
    }

    public AIDispatcher Dispatcher { get; }

    public Mock<ILanguageModelAdapter> Model { get; }

    public string? LastPrompt { get; private set; }

    public CancellationToken CapturedToken { get; private set; }

    public int StreamCallCount { get; private set; }

    public void UseThrowingStream(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        Model.Setup(m => m.StreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string prompt, CancellationToken ct) => ThrowingStreamAsync(prompt, ct, exception));
    }

    public async ValueTask DisposeAsync()
    {
        await Dispatcher.DisposeAsync();
    }

    private async IAsyncEnumerable<string> StreamTokensAsync(
        string prompt,
        CancellationToken token,
        IReadOnlyList<string> tokens,
        [EnumeratorCancellation] CancellationToken enumerationToken = default)
    {
        LastPrompt = prompt;
        CapturedToken = token;
        StreamCallCount++;

        foreach (var item in tokens)
        {
            token.ThrowIfCancellationRequested();
            enumerationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return item;
        }
    }

    private async IAsyncEnumerable<string> ThrowingStreamAsync(
        string prompt,
        CancellationToken token,
        Exception exception,
        [EnumeratorCancellation] CancellationToken enumerationToken = default)
    {
        LastPrompt = prompt;
        CapturedToken = token;
        StreamCallCount++;

        token.ThrowIfCancellationRequested();
        enumerationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        throw exception;
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
}
