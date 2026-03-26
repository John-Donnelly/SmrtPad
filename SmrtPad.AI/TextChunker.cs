using System.Text.RegularExpressions;

namespace SmrtPad.AI;

public static partial class TextChunker
{
    /// <summary>
    /// Truncates <paramref name="text"/> to at most <paramref name="maxTokens"/> estimated tokens,
    /// using the same 4-chars-per-token approximation as <see cref="ChunkByParagraph"/>.
    /// </summary>
    public static string TruncateToTokens(string text, int maxTokens)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (maxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens));

        var maxChars = maxTokens * 4;
        return text.Length <= maxChars ? text : text[..maxChars];
    }

    public static IReadOnlyList<string> ChunkByParagraph(string text, int maxTokens = 512)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (maxTokens <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTokens));

        if (string.IsNullOrWhiteSpace(text))
            return [];

        var chunks = new List<string>();
        foreach (var paragraph in ParagraphSeparatorRegex().Split(text))
        {
            if (string.IsNullOrWhiteSpace(paragraph))
                continue;

            var normalizedParagraph = paragraph.Trim();
            if (EstimateTokens(normalizedParagraph) <= maxTokens)
            {
                chunks.Add(normalizedParagraph);
                continue;
            }

            AddParagraphChunks(normalizedParagraph, maxTokens, chunks);
        }

        return chunks;
    }

    private static void AddParagraphChunks(string paragraph, int maxTokens, ICollection<string> chunks)
    {
        var sentences = SentenceSeparatorRegex().Split(paragraph)
            .Where(static sentence => !string.IsNullOrWhiteSpace(sentence))
            .Select(static sentence => sentence.Trim())
            .ToArray();

        if (sentences.Length <= 1)
        {
            chunks.Add(paragraph);
            return;
        }

        var builder = new List<string>();
        foreach (var sentence in sentences)
        {
            if (EstimateTokens(sentence) > maxTokens)
            {
                FlushChunk(builder, chunks);
                chunks.Add(sentence);
                continue;
            }

            builder.Add(sentence);
            var candidate = string.Join(" ", builder);
            if (EstimateTokens(candidate) <= maxTokens)
                continue;

            builder.RemoveAt(builder.Count - 1);
            FlushChunk(builder, chunks);
            builder.Add(sentence);
        }

        FlushChunk(builder, chunks);
    }

    private static void FlushChunk(List<string> builder, ICollection<string> chunks)
    {
        if (builder.Count == 0)
            return;

        chunks.Add(string.Join(" ", builder));
        builder.Clear();
    }

    private static int EstimateTokens(string text) => Math.Max(1, (text.Length + 3) / 4);

    [GeneratedRegex(@"\r\n\r\n|\n\n", RegexOptions.Compiled)]
    private static partial Regex ParagraphSeparatorRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+", RegexOptions.Compiled)]
    private static partial Regex SentenceSeparatorRegex();
}
