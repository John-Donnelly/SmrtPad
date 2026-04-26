using System.Runtime.CompilerServices;
using SmrtPad.AI.Benchmarks.Reporting;

namespace SmrtPad.AI.Benchmarks.Tests;

/// <summary>
/// Shared test context that creates an <see cref="AIDispatcher"/> with a mocked
/// <see cref="ILanguageModelAdapter"/> for deterministic benchmark testing.
/// </summary>
internal sealed class MockedBenchmarkContext : IAsyncDisposable
{
    private readonly Dictionary<string, Func<string, string[]>> _skillResponses = new();

    public AIDispatcher Dispatcher { get; }
    public Mock<ILanguageModelAdapter> Model { get; }

    public MockedBenchmarkContext()
    {
        var catalog = new Mock<IExecutionProviderCatalogAdapter>();
        catalog.Setup(c => c.ProbePhiSilicaAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unsupported));
        catalog.Setup(c => c.ProbeOnnxRuntimeGpuAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIBackendCapability("ORT GenAI GPU", AIBackendAvailabilityStatus.Unavailable));

        Model = new Mock<ILanguageModelAdapter>();
        Model.Setup(m => m.StreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string prompt, CancellationToken ct) => StreamResponse(prompt, ct));
        Model.Setup(m => m.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([0.1f]);
        Model.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);

        Dispatcher = new AIDispatcher(
            new HardwareProbeService(catalog.Object),
            (_, _, _) => Task.FromResult(Model.Object));
    }

    /// <summary>
    /// Configures a response for a given skill key. The factory receives the built prompt
    /// and returns tokens to stream.
    /// </summary>
    public void SetResponse(string skillKey, Func<string, string[]> tokenFactory)
    {
        _skillResponses[skillKey] = tokenFactory;
    }

    /// <summary>
    /// Sets a fixed response for any prompt containing the given substring.
    /// Useful for simple mocking scenarios.
    /// </summary>
    public void SetDefaultResponse(params string[] tokens)
    {
        Model.Setup(m => m.StreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken ct) => ToAsyncEnumerable(tokens, ct));
    }

    public async ValueTask DisposeAsync()
    {
        await Dispatcher.DisposeAsync();
    }

    private async IAsyncEnumerable<string> StreamResponse(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // Determine the skill key from the prompt content
        string[] tokens;
        var matchedFactory = _skillResponses.FirstOrDefault(kv => prompt.Contains(kv.Key, StringComparison.OrdinalIgnoreCase));
        if (matchedFactory.Value is not null)
        {
            tokens = matchedFactory.Value(prompt);
        }
        else
        {
            // Default: wrap response in insert tags for skills, plain text for others
            tokens = ["<insert>", "Default mock response.", "</insert>"];
        }

        foreach (var token in tokens)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return token;
        }
    }

    private static async IAsyncEnumerable<string> ToAsyncEnumerable(
        string[] items,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return item;
        }
    }
}
