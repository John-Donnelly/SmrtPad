using System.Text;
using SmrtPad.AI;

namespace SmrtPad.AI.Skills;

/// <summary>Represents a semantic-search match for a document chunk.</summary>
public sealed record SearchResult(int TabId, string ChunkText, float Score);

/// <summary>Builds and queries a semantic-search index backed by AI embeddings.</summary>
public sealed class SemanticSearchService : IAsyncDisposable
{
    private readonly AIDispatcher _dispatcher;
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly List<IndexedChunk> _entries = [];
    private bool _disposed;

    public SemanticSearchService(AIDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    /// <summary>Calculates the cosine similarity between two embedding vectors.</summary>
    public static float CosineSimilarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        if (left.Length != right.Length)
            throw new ArgumentException("Embedding vectors must have the same length.");

        if (left.IsEmpty)
            return 0;

        double dot = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;

        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftMagnitude += left[i] * left[i];
            rightMagnitude += right[i] * right[i];
        }

        if (leftMagnitude == 0 || rightMagnitude == 0)
            return 0;

        return (float)(dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude)));
    }

    /// <summary>Indexes the given document text for a tab, replacing any prior entries for the same tab.</summary>
    public async Task IndexDocumentAsync(int tabId, string documentText, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(documentText);
        ct.ThrowIfCancellationRequested();

        var chunks = TextChunker.ChunkByParagraph(documentText);
        var indexedChunks = new List<IndexedChunk>(chunks.Count);

        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();
            var embedding = await _dispatcher.GenerateEmbeddingAsync(chunk, ct).ConfigureAwait(false);
            indexedChunks.Add(new IndexedChunk(tabId, chunk, embedding));
        }

        _lock.EnterWriteLock();
        try
        {
            _entries.RemoveAll(entry => entry.TabId == tabId);
            _entries.AddRange(indexedChunks);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>Queries the semantic index and returns the highest-scoring document chunks.</summary>
    public async Task<IReadOnlyList<SearchResult>> QueryAsync(string queryText, int topK = 5, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(queryText);
        if (topK <= 0)
            throw new ArgumentOutOfRangeException(nameof(topK));
        if (string.IsNullOrWhiteSpace(queryText))
            return [];

        ct.ThrowIfCancellationRequested();
        var queryEmbedding = await _dispatcher.GenerateEmbeddingAsync(queryText, ct).ConfigureAwait(false);

        _lock.EnterReadLock();
        try
        {
            return _entries
                .Select(entry => new SearchResult(entry.TabId, entry.ChunkText, CosineSimilarity(entry.Embedding, queryEmbedding)))
                .OrderByDescending(static result => result.Score)
                .Take(topK)
                .ToArray();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>Removes all indexed chunks for the given tab.</summary>
    public void RemoveTab(int tabId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _lock.EnterWriteLock();
        try
        {
            _entries.RemoveAll(entry => entry.TabId == tabId);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>Saves the current semantic index to disk.</summary>
    public async Task SaveIndexAsync(string filePath, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("A file path is required.", nameof(filePath));

        IndexedChunk[] snapshot;
        _lock.EnterReadLock();
        try
        {
            snapshot = _entries.Select(static entry => entry.Clone()).ToArray();
        }
        finally
        {
            _lock.ExitReadLock();
        }

        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using var stream = File.Create(filePath);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);
            writer.Write(snapshot.Length);
            foreach (var entry in snapshot)
            {
                ct.ThrowIfCancellationRequested();
                writer.Write(entry.TabId);
                writer.Write(entry.ChunkText);
                writer.Write(entry.Embedding.Length);
                foreach (var value in entry.Embedding)
                {
                    writer.Write(value);
                }
            }
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Loads a semantic index from disk, replacing the current in-memory entries.</summary>
    public async Task LoadIndexAsync(string filePath, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("A file path is required.", nameof(filePath));
        if (!File.Exists(filePath))
            return;

        var loadedEntries = await Task.Run(() =>
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                using var stream = File.OpenRead(filePath);
                using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);
                var count = reader.ReadInt32();
                var entries = new List<IndexedChunk>(count);
                for (var i = 0; i < count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var tabId = reader.ReadInt32();
                    var chunkText = reader.ReadString();
                    var embeddingLength = reader.ReadInt32();
                    var embedding = new float[embeddingLength];
                    for (var j = 0; j < embeddingLength; j++)
                    {
                        embedding[j] = reader.ReadSingle();
                    }

                    entries.Add(new IndexedChunk(tabId, chunkText, embedding));
                }

                return entries;
            }
            catch (EndOfStreamException ex)
            {
                throw new InvalidDataException("The semantic index file is corrupted.", ex);
            }
        }, ct).ConfigureAwait(false);

        _lock.EnterWriteLock();
        try
        {
            _entries.Clear();
            _entries.AddRange(loadedEntries);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _lock.Dispose();
        _disposed = true;
        return ValueTask.CompletedTask;
    }

    private sealed class IndexedChunk
    {
        public IndexedChunk(int tabId, string chunkText, float[] embedding)
        {
            ArgumentNullException.ThrowIfNull(chunkText);
            ArgumentNullException.ThrowIfNull(embedding);
            TabId = tabId;
            ChunkText = chunkText;
            Embedding = embedding;
        }

        public int TabId { get; }

        public string ChunkText { get; }

        public float[] Embedding { get; }

        public IndexedChunk Clone() => new(TabId, ChunkText, [.. Embedding]);
    }
}
