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
        await using var context = SemanticSearchServiceTestContext.Create(
            ("Alpha section.", [1f, 0f]),
            ("Beta section.", [0f, 1f]),
            ("alpha", [1f, 0f]));

        await context.Service.IndexDocumentAsync(1, "Alpha section.\n\nBeta section.");
        var results = await context.Service.QueryAsync("alpha");

        Assert.Equal("Alpha section.", results[0].ChunkText);
    }

    [Fact]
    public async Task IndexDocument_ThenQuery_TopKLimitsResults()
    {
        await using var context = SemanticSearchServiceTestContext.Create(
            ("One.", [1f, 0f]),
            ("Two.", [0.9f, 0.1f]),
            ("Three.", [0.8f, 0.2f]),
            ("query", [1f, 0f]));

        await context.Service.IndexDocumentAsync(1, "One.\n\nTwo.\n\nThree.");
        var results = await context.Service.QueryAsync("query", 2);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task IndexDocument_ThenQuery_ScoresDescending()
    {
        await using var context = SemanticSearchServiceTestContext.Create(
            ("Exact.", [1f, 0f]),
            ("Close.", [0.6f, 0.4f]),
            ("Far.", [0f, 1f]),
            ("query", [1f, 0f]));

        await context.Service.IndexDocumentAsync(1, "Exact.\n\nClose.\n\nFar.");
        var results = await context.Service.QueryAsync("query", 3);

        Assert.True(results[0].Score >= results[1].Score && results[1].Score >= results[2].Score);
    }

    [Fact]
    public async Task IndexDocument_SameTabId_ReplacesExistingEntries()
    {
        await using var context = SemanticSearchServiceTestContext.Create(
            ("Old chunk.", [0f, 1f]),
            ("New chunk.", [1f, 0f]),
            ("query", [1f, 0f]));

        await context.Service.IndexDocumentAsync(1, "Old chunk.");
        await context.Service.IndexDocumentAsync(1, "New chunk.");
        var results = await context.Service.QueryAsync("query", 5);

        Assert.Equal("New chunk.", results[0].ChunkText);
    }

    [Fact]
    public async Task IndexDocument_MultipleTabIds_BothReturned()
    {
        await using var context = SemanticSearchServiceTestContext.Create(
            ("Alpha chunk.", [1f, 0f]),
            ("Beta chunk.", [0.9f, 0.1f]),
            ("query", [1f, 0f]));

        await context.Service.IndexDocumentAsync(1, "Alpha chunk.");
        await context.Service.IndexDocumentAsync(2, "Beta chunk.");
        var results = await context.Service.QueryAsync("query", 2);

        Assert.Equal(2, results.Select(static result => result.TabId).Distinct().Count());
    }

    [Fact]
    public async Task IndexDocument_EmptyText_IndexesNoChunks()
    {
        await using var context = SemanticSearchServiceTestContext.Create(("query", [1f, 0f]));

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
        await using var context = SemanticSearchServiceTestContext.Create(("Chunk.", [1f, 0f]));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => context.Service.IndexDocumentAsync(1, "Chunk.", cts.Token));
    }

    [Fact]
    public async Task Query_EmptyIndex_ReturnsEmptyList()
    {
        await using var context = SemanticSearchServiceTestContext.Create(("query", [1f, 0f]));

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
        await using var context = SemanticSearchServiceTestContext.Create(
            ("One.", [1f, 0f]),
            ("Two.", [0f, 1f]),
            ("query", [1f, 0f]));

        await context.Service.IndexDocumentAsync(1, "One.\n\nTwo.");
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
        await using var context = SemanticSearchServiceTestContext.Create(
            ("Chunk.", [1f, 0f]),
            ("query", [1f, 0f]));

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
        await using var context = SemanticSearchServiceTestContext.Create(
            ("Chunk.", [1f, 0f]),
            ("query", [1f, 0f]));
        await using var reloadedContext = SemanticSearchServiceTestContext.Create(
            ("Chunk.", [1f, 0f]),
            ("query", [1f, 0f]));

        await context.Service.IndexDocumentAsync(1, "Chunk.");
        await context.Service.SaveIndexAsync(filePath);
        await reloadedContext.Service.LoadIndexAsync(filePath);
        var results = await reloadedContext.Service.QueryAsync("query");

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
        await using var context = SemanticSearchServiceTestContext.Create(("query", [1f, 0f]));

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
        await using var context = SemanticSearchServiceTestContext.Create(
            ("Chunk 1.", [1f, 0f]),
            ("Chunk 2.", [0.9f, 0.1f]),
            ("Chunk 3.", [0.8f, 0.2f]),
            ("Chunk 4.", [0.7f, 0.3f]),
            ("query", [1f, 0f]));

        await Task.WhenAll(
            context.Service.IndexDocumentAsync(1, "Chunk 1."),
            context.Service.IndexDocumentAsync(2, "Chunk 2."),
            context.Service.IndexDocumentAsync(3, "Chunk 3."),
            context.Service.IndexDocumentAsync(4, "Chunk 4."));

        var results = await context.Service.QueryAsync("query", 4);

        Assert.Equal(4, results.Count);
    }

    private sealed class SemanticSearchServiceTestContext : IAsyncDisposable
    {
        private readonly Dictionary<string, float[]> _embeddings;
        private readonly Mock<ILanguageModelAdapter> _model = new();

        private SemanticSearchServiceTestContext(params (string Text, float[] Embedding)[] embeddings)
        {
            _embeddings = embeddings.ToDictionary(static entry => entry.Text, static entry => entry.Embedding, StringComparer.Ordinal);

            var catalog = new Mock<IExecutionProviderCatalogAdapter>();
            catalog.Setup(c => c.IsNpuAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
            catalog.Setup(c => c.IsGpuAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);

            _model.Setup(m => m.StreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(EmptyStream());
            _model.Setup(m => m.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((string text, CancellationToken ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return Task.FromResult(_embeddings.TryGetValue(text, out var embedding)
                        ? embedding
                        : CreateFallbackEmbedding(text));
                });
            _model.Setup(m => m.DisposeAsync()).Returns(ValueTask.CompletedTask);

            Dispatcher = new AIDispatcher(new HardwareProbeService(catalog.Object), _ => Task.FromResult(_model.Object));
            Service = new SemanticSearchService(Dispatcher);
        }

        public AIDispatcher Dispatcher { get; }

        public SemanticSearchService Service { get; }

        public static SemanticSearchServiceTestContext Create(params (string Text, float[] Embedding)[] embeddings) => new(embeddings);

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

        private static float[] CreateFallbackEmbedding(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return [0f, 0f];

            return [text.Length, text.Count(char.IsLetter)];
        }
    }
}
