using System.Text;
using System.Text.RegularExpressions;
using SmrtPad.AI;

namespace SmrtPad.AI.Skills;

/// <summary>Represents a semantic-search match for a document chunk.</summary>
public sealed record SearchResult(int TabId, string ChunkText, float Score);

/// <summary>Builds and queries a local hybrid semantic-search index.</summary>
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

    internal static IReadOnlyList<string> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return TokenRegex.Matches(text)
            .Select(static match => match.Value.ToLowerInvariant())
            .ToArray();
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
            indexedChunks.Add(CreateIndexedChunk(tabId, chunk));
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
        var queryVector = BuildTokenFrequencyVector(Tokenize(queryText));
        if (queryVector.Count == 0)
            return [];

        _lock.EnterReadLock();
        try
        {
            return _entries
                .Select(entry => new SearchResult(entry.TabId, entry.ChunkText, Score(entry, queryVector)))
                .Where(static result => result.Score > 0)
                .OrderByDescending(static result => result.Score)
                .Take(topK)
                .ToArray();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private static IndexedChunk CreateIndexedChunk(int tabId, string chunk)
    {
        var tokens = Tokenize(chunk);
        return new IndexedChunk(tabId, chunk, BuildTokenFrequencyVector(tokens));
    }

    private static Dictionary<string, int> BuildTokenFrequencyVector(IReadOnlyList<string> tokens)
    {
        var vector = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (vector.TryGetValue(token, out var current))
                vector[token] = current + 1;
            else
                vector[token] = 1;
        }

        return vector;
    }

    private static float Score(IndexedChunk entry, IReadOnlyDictionary<string, int> queryVector)
    {
        if (entry.TokenFrequency.Count == 0 || queryVector.Count == 0)
            return 0;

        double dot = 0;
        double entryMagnitude = 0;
        double queryMagnitude = 0;
        var lexicalHits = 0;

        foreach (var (token, queryFrequency) in queryVector)
        {
            queryMagnitude += queryFrequency * queryFrequency;
            if (entry.TokenFrequency.TryGetValue(token, out var entryFrequency))
            {
                lexicalHits++;
                dot += queryFrequency * entryFrequency;
            }
        }

        foreach (var entryFrequency in entry.TokenFrequency.Values)
            entryMagnitude += entryFrequency * entryFrequency;

        if (dot == 0 || entryMagnitude == 0 || queryMagnitude == 0)
            return 0;

        var cosine = dot / (Math.Sqrt(entryMagnitude) * Math.Sqrt(queryMagnitude));
        var lexicalBoost = lexicalHits / (double)queryVector.Count;
        var coverageBoost = Math.Min(1d, queryVector.Count / Math.Max(1d, entry.TokenFrequency.Count));
        return (float)((cosine * 0.75d) + (lexicalBoost * 0.2d) + (coverageBoost * 0.05d));
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
                writer.Write(entry.TokenFrequency.Count);
                foreach (var (token, frequency) in entry.TokenFrequency)
                {
                    writer.Write(token);
                    writer.Write(frequency);
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
                    var tokenCount = reader.ReadInt32();
                    var tokenFrequency = new Dictionary<string, int>(tokenCount, StringComparer.Ordinal);
                    for (var j = 0; j < tokenCount; j++)
                    {
                        tokenFrequency[reader.ReadString()] = reader.ReadInt32();
                    }

                    entries.Add(new IndexedChunk(tabId, chunkText, tokenFrequency));
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
        public IndexedChunk(int tabId, string chunkText, Dictionary<string, int> tokenFrequency)
        {
            ArgumentNullException.ThrowIfNull(chunkText);
            ArgumentNullException.ThrowIfNull(tokenFrequency);
            TabId = tabId;
            ChunkText = chunkText;
            TokenFrequency = tokenFrequency;
        }

        public int TabId { get; }

        public string ChunkText { get; }

        public Dictionary<string, int> TokenFrequency { get; }

        public IndexedChunk Clone() => new(TabId, ChunkText, new Dictionary<string, int>(TokenFrequency, StringComparer.Ordinal));
    }

    private static readonly Regex TokenRegex = new("[\\p{L}\\p{Nd}']+", RegexOptions.Compiled);
}
