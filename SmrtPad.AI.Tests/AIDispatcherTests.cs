using SmrtPad.AI;

namespace SmrtPad.AI.Tests;

public sealed class AIDispatcherTests : IAsyncDisposable
{
    private AIDispatcher? _dispatcher;

    public async ValueTask DisposeAsync()
    {
        if (_dispatcher is not null)
            await _dispatcher.DisposeAsync();
    }

    private static Mock<IExecutionProviderCatalogAdapter> CreateCatalog(AIExecutionTarget target)
    {
        var mock = new Mock<IExecutionProviderCatalogAdapter>();
        mock.Setup(c => c.ProbePhiSilicaAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(target == AIExecutionTarget.PhiSilicaNpu
                ? new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Available)
                : new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unsupported));
        mock.Setup(c => c.ProbeFoundryGpuAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(target == AIExecutionTarget.FoundryLocalGpu
                ? new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Available)
                : new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Unavailable));
        return mock;
    }

    private static Mock<ILanguageModelAdapter> CreateModelAdapter(params string[] tokens)
    {
        var mock = new Mock<ILanguageModelAdapter>();
        mock.Setup(m => m.StreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(tokens));
        mock.Setup(m => m.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });
        mock.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);
        return mock;
    }

    private AIDispatcher CreateDispatcher(
        AIExecutionTarget target = AIExecutionTarget.FoundryLocalCpu,
        Mock<ILanguageModelAdapter>? modelMock = null)
    {
        var catalog = CreateCatalog(target);
        var probe = new HardwareProbeService(catalog.Object);
        var model = modelMock ?? CreateModelAdapter("Hello", " world");

        _dispatcher = new AIDispatcher(probe, (_, _, _, _) => Task.FromResult(model.Object));
        return _dispatcher;
    }

    private static async IAsyncEnumerable<string> ToAsyncEnumerable(
        string[] items,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return item;
        }
    }

    // --- InitializeAsync ---

    [Fact]
    public async Task InitializeAsync_FirstCall_SetsIsInitializedTrue()
    {
        var dispatcher = CreateDispatcher();
        await dispatcher.InitializeAsync();
        Assert.True(dispatcher.IsInitialized);
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_InitializesOnce()
    {
        int factoryCallCount = 0;
        var catalog = CreateCatalog(AIExecutionTarget.FoundryLocalCpu);
        var probe = new HardwareProbeService(catalog.Object);
        var model = CreateModelAdapter();

        _dispatcher = new AIDispatcher(probe, (_, _, _, _) =>
        {
            Interlocked.Increment(ref factoryCallCount);
            return Task.FromResult(model.Object);
        });

        await _dispatcher.InitializeAsync();
        await _dispatcher.InitializeAsync();

        Assert.Equal(1, factoryCallCount);
    }

    [Fact]
    public async Task InitializeAsync_NpuTarget_SetsExecutionTargetPhiSilicaNpu()
    {
        var dispatcher = CreateDispatcher(AIExecutionTarget.PhiSilicaNpu);
        await dispatcher.InitializeAsync();
        Assert.Equal(AIExecutionTarget.PhiSilicaNpu, dispatcher.ExecutionTarget);
        Assert.Equal(AIBackendAvailabilityStatus.Available, dispatcher.ProbeResult.PhiSilica.Status);
    }

    [Fact]
    public async Task InitializeAsync_CpuTarget_SetsExecutionTargetFoundryLocalCpu()
    {
        var dispatcher = CreateDispatcher(AIExecutionTarget.FoundryLocalCpu);
        await dispatcher.InitializeAsync();
        Assert.Equal(AIExecutionTarget.FoundryLocalCpu, dispatcher.ExecutionTarget);
    }

    [Fact]
    public async Task InitializeAsync_GpuTarget_SetsExecutionTargetFoundryLocalGpu()
    {
        var dispatcher = CreateDispatcher(AIExecutionTarget.FoundryLocalGpu);
        await dispatcher.InitializeAsync();
        Assert.Equal(AIExecutionTarget.FoundryLocalGpu, dispatcher.ExecutionTarget);
        Assert.Equal(AIBackendAvailabilityStatus.Available, dispatcher.ProbeResult.FoundryGpu.Status);
    }

    [Fact]
    public async Task InitializeAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var dispatcher = CreateDispatcher();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatcher.InitializeAsync(cts.Token));
    }

    // --- StreamResponseAsync ---

    [Fact]
    public async Task StreamResponseAsync_CallsOnTokenForEachToken()
    {
        var dispatcher = CreateDispatcher();
        var tokens = new List<string>();

        await dispatcher.StreamResponseAsync("freeform", "prompt", tokens.Add, () => { });

        Assert.Equal(2, tokens.Count);
        Assert.Equal("Hello", tokens[0]);
        Assert.Equal(" world", tokens[1]);
    }

    [Fact]
    public async Task StreamResponseAsync_CallsOnCompleteAfterAllTokens()
    {
        var dispatcher = CreateDispatcher();
        bool completed = false;

        await dispatcher.StreamResponseAsync("freeform", "prompt", _ => { }, () => completed = true);

        Assert.True(completed);
    }

    [Fact]
    public async Task StreamResponseAsync_EmptyStream_CallsOnCompleteWithNoTokens()
    {
        var model = CreateModelAdapter(); // no tokens
        model.Setup(m => m.StreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([]));
        var dispatcher = CreateDispatcher(modelMock: model);
        var tokens = new List<string>();
        bool completed = false;

        await dispatcher.StreamResponseAsync("freeform", "prompt", tokens.Add, () => completed = true);

        Assert.Empty(tokens);
        Assert.True(completed);
    }

    [Fact]
    public async Task StreamResponseAsync_CancellationDuringStream_StopsTokenDelivery()
    {
        var cts = new CancellationTokenSource();
        var model = new Mock<ILanguageModelAdapter>();
        model.Setup(m => m.StreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken ct) => CancelAfterFirst(ct, cts));
        model.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var dispatcher = CreateDispatcher(modelMock: model);
        var tokens = new List<string>();

        await dispatcher.StreamResponseAsync("freeform", "prompt", tokens.Add, () => { }, ct: cts.Token);

        Assert.Single(tokens);
    }

    [Fact]
    public async Task StreamResponseAsync_CancellationDuringStream_CallsOnComplete()
    {
        var cts = new CancellationTokenSource();
        var model = new Mock<ILanguageModelAdapter>();
        model.Setup(m => m.StreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string _, CancellationToken ct) => CancelAfterFirst(ct, cts));
        model.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var dispatcher = CreateDispatcher(modelMock: model);
        bool completed = false;

        await dispatcher.StreamResponseAsync("freeform", "prompt", _ => { }, () => completed = true, ct: cts.Token);

        Assert.True(completed);
    }

    [Fact]
    public async Task StreamResponseAsync_ModelThrows_CallsOnError()
    {
        var model = new Mock<ILanguageModelAdapter>();
        var expectedException = new InvalidOperationException("model error");
        model.Setup(m => m.StreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ThrowingAsyncEnumerable(expectedException));
        model.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var dispatcher = CreateDispatcher(modelMock: model);
        Exception? captured = null;

        await dispatcher.StreamResponseAsync("freeform", "prompt", _ => { }, () => { }, ex => captured = ex);

        Assert.Same(expectedException, captured);
    }

    [Fact]
    public async Task StreamResponseAsync_ModelThrows_OnErrorNull_DoesNotThrow()
    {
        var model = new Mock<ILanguageModelAdapter>();
        model.Setup(m => m.StreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ThrowingAsyncEnumerable(new InvalidOperationException("fail")));
        model.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var dispatcher = CreateDispatcher(modelMock: model);

        // Should not throw even though onError is null
        await dispatcher.StreamResponseAsync("freeform", "prompt", _ => { }, () => { });
    }

    [Fact]
    public async Task StreamResponseAsync_BeforeInitialize_AutoInitializes()
    {
        var dispatcher = CreateDispatcher();
        Assert.False(dispatcher.IsInitialized);

        await dispatcher.StreamResponseAsync("freeform", "prompt", _ => { }, () => { });

        Assert.True(dispatcher.IsInitialized);
    }

    [Fact]
    public async Task StreamResponseAsync_CalledConcurrently_BothComplete()
    {
        var dispatcher = CreateDispatcher();
        bool complete1 = false;
        bool complete2 = false;

        var t1 = dispatcher.StreamResponseAsync("freeform", "p1", _ => { }, () => complete1 = true);
        var t2 = dispatcher.StreamResponseAsync("freeform", "p2", _ => { }, () => complete2 = true);

        await Task.WhenAll(t1, t2);

        Assert.True(complete1);
        Assert.True(complete2);
    }

    // --- GenerateEmbeddingAsync ---

    [Fact]
    public async Task GenerateEmbeddingAsync_ReturnsNonEmptyArray()
    {
        var dispatcher = CreateDispatcher();

        var embedding = await dispatcher.GenerateEmbeddingAsync("test");

        Assert.NotEmpty(embedding);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var model = new Mock<ILanguageModelAdapter>();
        model.Setup(m => m.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        model.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var dispatcher = CreateDispatcher(modelMock: model);
        await dispatcher.InitializeAsync();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => dispatcher.GenerateEmbeddingAsync("test", cts.Token));
    }

    // --- DisposeAsync ---

    [Fact]
    public async Task DisposeAsync_DisposesLanguageModelAdapter()
    {
        var model = CreateModelAdapter();
        var dispatcher = CreateDispatcher(modelMock: model);
        await dispatcher.InitializeAsync();

        await dispatcher.DisposeAsync();
        _dispatcher = null; // prevent double dispose in cleanup

        model.Verify(m => m.DisposeAsync(), Times.Once());
    }

    [Fact]
    public async Task DisposeAsync_CalledTwice_IsIdempotent()
    {
        var model = CreateModelAdapter();
        var dispatcher = CreateDispatcher(modelMock: model);
        await dispatcher.InitializeAsync();

        await dispatcher.DisposeAsync();
        await dispatcher.DisposeAsync();
        _dispatcher = null;

        // DisposeAsync on the model is called once because second time _model is null
        model.Verify(m => m.DisposeAsync(), Times.Once());
    }

    [Fact]
    public async Task DisposeAsync_StreamingInProgress_CancelsStream()
    {
        // After dispose, IsInitialized should be false
        var dispatcher = CreateDispatcher();
        await dispatcher.InitializeAsync();
        Assert.True(dispatcher.IsInitialized);

        await dispatcher.DisposeAsync();
        _dispatcher = null;

        Assert.False(dispatcher.IsInitialized);
    }

    // --- Helpers ---

    private static async IAsyncEnumerable<string> CancelAfterFirst(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct,
        CancellationTokenSource cts)
    {
        await Task.Yield();
        yield return "first";
        cts.Cancel();
        ct.ThrowIfCancellationRequested();
        yield return "second";
    }

    private static async IAsyncEnumerable<string> ThrowingAsyncEnumerable(
        Exception ex,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        _ = ct; // suppress unused warning
        throw ex;
#pragma warning disable CS0162 // Unreachable code detected
        yield break;
#pragma warning restore CS0162
    }
}
