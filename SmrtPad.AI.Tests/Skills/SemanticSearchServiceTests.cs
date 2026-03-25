using SmrtPad.AI;
using SmrtPad.AI.Skills;

namespace SmrtPad.AI.Tests.Skills;

public sealed class SemanticSearchServiceTests
{
    [Fact]
    public void CosineSimilarity_IdenticalVectors_ReturnsOne()
    {
        var score = SemanticSearchService.CosineSimilarity([1f, 0f], [1f, 0f]);

        Assert.Equal(1f, score, 3);
    }

    [Fact]
    public void CosineSimilarity_OppositeVectors_ReturnsNegativeOne()
    {
        var score = SemanticSearchService.CosineSimilarity([1f, 0f], [-1f, 0f]);

        Assert.Equal(-1f, score, 3);
    }

    [Fact]
    public void CosineSimilarity_OrthogonalVectors_ReturnsZero()
    {
        var score = SemanticSearchService.CosineSimilarity([1f, 0f], [0f, 1f]);

        Assert.Equal(0f, score, 3);
    }

    [Fact]
    public void CosineSimilarity_ZeroVector_ReturnsZero()
    {
        var score = SemanticSearchService.CosineSimilarity([0f, 0f], [1f, 1f]);

        Assert.Equal(0f, score, 3);
    }

    [Fact]
    public void CosineSimilarity_SingleElementVectors_CorrectResult()
    {
        var score = SemanticSearchService.CosineSimilarity([2f], [4f]);

        Assert.Equal(1f, score, 3);
    }

    [Fact]
    public async Task IndexDocument_ThenQuery_ReturnsMatchingChunk()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        await context.Service.IndexDocumentAsync(1, "Alpha section.\n\nBeta section.");
        var results = await context.Service.QueryAsync("alpha");

        Assert.Equal("Alpha section.", results[0].ChunkText);
    }

    [Fact]
    public async Task IndexDocument_ThenQuery_TopKLimitsResults()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        await context.Service.IndexDocumentAsync(1, "Query alpha.\n\nQuery beta.\n\nQuery gamma.");
        var results = await context.Service.QueryAsync("query", 2);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task IndexDocument_ThenQuery_ScoresDescending()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        await context.Service.IndexDocumentAsync(1, "query exact match.\n\nquery close.\n\nfar away.");
        var results = await context.Service.QueryAsync("query exact", 3);

        Assert.Equal(2, results.Count);
        Assert.Equal("query exact match.", results[0].ChunkText);
        Assert.Equal("query close.", results[1].ChunkText);
    }

    [Fact]
    public async Task IndexDocument_SameTabId_ReplacesExistingEntries()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        await context.Service.IndexDocumentAsync(1, "Old chunk.");
        await context.Service.IndexDocumentAsync(1, "New chunk.");
        var results = await context.Service.QueryAsync("new", 5);

        Assert.Equal("New chunk.", results[0].ChunkText);
    }

    [Fact]
    public async Task IndexDocument_MultipleTabIds_BothReturned()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        await context.Service.IndexDocumentAsync(1, "Alpha chunk.");
        await context.Service.IndexDocumentAsync(2, "Beta chunk.");
        var results = await context.Service.QueryAsync("chunk", 2);

        Assert.Equal(2, results.Select(static result => result.TabId).Distinct().Count());
    }

    [Fact]
    public async Task IndexDocument_EmptyText_IndexesNoChunks()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        await context.Service.IndexDocumentAsync(1, string.Empty);
        var results = await context.Service.QueryAsync("query");

        Assert.Empty(results);
    }

    [Fact]
    public async Task IndexDocument_NullText_ThrowsArgumentNullException()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        await Assert.ThrowsAsync<ArgumentNullException>(() => context.Service.IndexDocumentAsync(1, null!));
    }

    [Fact]
    public async Task IndexDocument_CancellationRequested_ThrowsOperationCanceledException()
    {
        await using var context = SemanticSearchServiceTestContext.Create();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => context.Service.IndexDocumentAsync(1, "Chunk.", cts.Token));
    }

    [Fact]
    public async Task Query_EmptyIndex_ReturnsEmptyList()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        var results = await context.Service.QueryAsync("query");

        Assert.Empty(results);
    }

    [Fact]
    public async Task Query_NullQuery_ThrowsArgumentNullException()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        await Assert.ThrowsAsync<ArgumentNullException>(() => context.Service.QueryAsync(null!));
    }

    [Fact]
    public async Task Query_TopKGreaterThanResults_ReturnsAllResults()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        await context.Service.IndexDocumentAsync(1, "One query.\n\nTwo query.");
        var results = await context.Service.QueryAsync("query", 10);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Query_TopKZero_ThrowsArgumentOutOfRangeException()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.Service.QueryAsync("query", 0));
    }

    [Fact]
    public async Task Query_TopKNegative_ThrowsArgumentOutOfRangeException()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.Service.QueryAsync("query", -1));
    }

    [Fact]
    public async Task RemoveTab_ExistingTab_RemovesChunks()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        await context.Service.IndexDocumentAsync(1, "Chunk.");
        context.Service.RemoveTab(1);
        var results = await context.Service.QueryAsync("query");

        Assert.Empty(results);
    }

    [Fact]
    public async Task RemoveTab_NonExistentTab_DoesNotThrow()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        context.Service.RemoveTab(999);
        var results = await context.Service.QueryAsync(" ");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SaveIndex_ThenLoadIndex_PreservesEntries()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"semantic-index-{Guid.NewGuid():N}.bin");
        await using var context = SemanticSearchServiceTestContext.Create();
        await using var reloadedContext = SemanticSearchServiceTestContext.Create();

        await context.Service.IndexDocumentAsync(1, "Chunk.");
        await context.Service.SaveIndexAsync(filePath);
        await reloadedContext.Service.LoadIndexAsync(filePath);
        var results = await reloadedContext.Service.QueryAsync("chunk");

        Assert.Single(results);
    }

    [Fact]
    public async Task SaveIndex_EmptyIndex_WritesValidFile()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"semantic-index-empty-{Guid.NewGuid():N}.bin");
        await using var context = SemanticSearchServiceTestContext.Create();

        await context.Service.SaveIndexAsync(filePath);

        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task LoadIndex_NonExistentFile_IndexRemainsEmpty()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"semantic-index-missing-{Guid.NewGuid():N}.bin");
        await using var context = SemanticSearchServiceTestContext.Create();

        await context.Service.LoadIndexAsync(filePath);
        var results = await context.Service.QueryAsync("query");

        Assert.Empty(results);
    }

    [Fact]
    public async Task LoadIndex_CorruptedFile_ThrowsOrReturnsEmpty()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"semantic-index-corrupt-{Guid.NewGuid():N}.bin");
        await File.WriteAllTextAsync(filePath, "corrupt");
        await using var context = SemanticSearchServiceTestContext.Create();

        await Assert.ThrowsAsync<InvalidDataException>(() => context.Service.LoadIndexAsync(filePath));
    }

    [Fact]
    public async Task ConcurrentIndexDocument_ThreadSafe()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        await Task.WhenAll(
            context.Service.IndexDocumentAsync(1, "Query chunk 1."),
            context.Service.IndexDocumentAsync(2, "Query chunk 2."),
            context.Service.IndexDocumentAsync(3, "Query chunk 3."),
            context.Service.IndexDocumentAsync(4, "Query chunk 4."));

        var results = await context.Service.QueryAsync("query", 4);

        Assert.Equal(4, results.Count);
    }

    [Fact]
    public async Task Query_NormalizesWordsAndNumbers()
    {
        await using var context = SemanticSearchServiceTestContext.Create();

        await context.Service.IndexDocumentAsync(1, "Alpha-2 beta's GAMMA");
        var results = await context.Service.QueryAsync("gamma beta's 2");

        Assert.Single(results);
        Assert.Equal("Alpha-2 beta's GAMMA", results[0].ChunkText);
    }

    private sealed class SemanticSearchServiceTestContext : IAsyncDisposable
    {
        private readonly Mock<ILanguageModelAdapter> _model = new();

        private SemanticSearchServiceTestContext()
        {
            var catalog = new Mock<IExecutionProviderCatalogAdapter>();
            catalog.Setup(c => c.ProbePhiSilicaAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unsupported));
            catalog.Setup(c => c.ProbeFoundryGpuAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Unavailable));

            _model.Setup(m => m.StreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(EmptyStream());
            _model.Setup(m => m.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Array.Empty<float>());
            _model.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);

            Dispatcher = new AIDispatcher(new HardwareProbeService(catalog.Object), (_, _) => Task.FromResult(_model.Object));
            Service = new SemanticSearchService(Dispatcher);
        }

        public AIDispatcher Dispatcher { get; }

        public SemanticSearchService Service { get; }

        public static SemanticSearchServiceTestContext Create() => new();

        public async ValueTask DisposeAsync()
        {
            await Service.DisposeAsync();
            await Dispatcher.DisposeAsync();
        }

        private static async IAsyncEnumerable<string> EmptyStream()
        {
            await Task.Yield();
            yield break;
        }
    }
}
