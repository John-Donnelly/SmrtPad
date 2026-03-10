using SmrtPad.AI;

namespace SmrtPad.AI.Tests;

public sealed class TextChunkerTests
{
    [Fact]
    public void ChunkByParagraph_EmptyString_ReturnsEmptyList()
    {
        var chunks = TextChunker.ChunkByParagraph(string.Empty);

        Assert.Empty(chunks);
    }

    [Fact]
    public void ChunkByParagraph_WhitespaceOnly_ReturnsEmptyList()
    {
        var chunks = TextChunker.ChunkByParagraph("   \r\n\r\n  ");

        Assert.Empty(chunks);
    }

    [Fact]
    public void ChunkByParagraph_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TextChunker.ChunkByParagraph(null!));
    }

    [Fact]
    public void ChunkByParagraph_MaxTokensZero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextChunker.ChunkByParagraph("text", 0));
    }

    [Fact]
    public void ChunkByParagraph_MaxTokensNegative_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TextChunker.ChunkByParagraph("text", -1));
    }

    [Fact]
    public void ChunkByParagraph_SingleShortParagraph_ReturnsSingleChunk()
    {
        var chunks = TextChunker.ChunkByParagraph("Short paragraph.");

        Assert.Single(chunks);
    }

    [Fact]
    public void ChunkByParagraph_SingleShortParagraph_ChunkEqualsInput()
    {
        var chunks = TextChunker.ChunkByParagraph("Short paragraph.");

        Assert.Equal("Short paragraph.", chunks[0]);
    }

    [Fact]
    public void ChunkByParagraph_TwoParagraphs_ReturnsTwoChunks()
    {
        var chunks = TextChunker.ChunkByParagraph("First paragraph.\n\nSecond paragraph.");

        Assert.Equal(2, chunks.Count);
    }

    [Fact]
    public void ChunkByParagraph_ThreeParagraphs_ReturnsThreeChunks()
    {
        var chunks = TextChunker.ChunkByParagraph("One.\n\nTwo.\n\nThree.");

        Assert.Equal(3, chunks.Count);
    }

    [Fact]
    public void ChunkByParagraph_ParagraphExceedsMaxTokens_SplitsAtSentenceBoundary()
    {
        const string text = "Sentence one is somewhat long. Sentence two is also fairly long. Sentence three stays here.";

        var chunks = TextChunker.ChunkByParagraph(text, 8);

        Assert.Equal(3, chunks.Count);
    }

    [Fact]
    public void ChunkByParagraph_VeryShortMaxTokens_SplitsAggressively()
    {
        const string text = "One. Two. Three. Four.";

        var chunks = TextChunker.ChunkByParagraph(text, 1);

        Assert.Equal(4, chunks.Count);
    }

    [Fact]
    public void ChunkByParagraph_NoSentenceBoundary_ReturnsSingleOversizedChunk()
    {
        var oversizedWord = new string('a', 80);

        var chunks = TextChunker.ChunkByParagraph(oversizedWord, 4);

        Assert.Single(chunks);
    }

    [Fact]
    public void ChunkByParagraph_ParagraphWithOnlyWhitespace_IsDiscarded()
    {
        var chunks = TextChunker.ChunkByParagraph("First.\n\n   \n\nSecond.");

        Assert.Equal(2, chunks.Count);
    }

    [Fact]
    public void ChunkByParagraph_WindowsLineEndings_SplitsCorrectly()
    {
        var chunks = TextChunker.ChunkByParagraph("First.\r\n\r\nSecond.");

        Assert.Equal(2, chunks.Count);
    }

    [Fact]
    public void ChunkByParagraph_UnixLineEndings_SplitsCorrectly()
    {
        var chunks = TextChunker.ChunkByParagraph("First.\n\nSecond.");

        Assert.Equal(2, chunks.Count);
    }

    [Fact]
    public void ChunkByParagraph_AllChunks_NonEmpty()
    {
        var chunks = TextChunker.ChunkByParagraph("One.\n\nTwo.\n\nThree.");

        Assert.DoesNotContain(chunks, static chunk => string.IsNullOrWhiteSpace(chunk));
    }

    [Fact]
    public void ChunkByParagraph_MaxTokens_NoChunkExceedsLimit()
    {
        const string text = "A short sentence. Another short sentence. Final short sentence.";

        var chunks = TextChunker.ChunkByParagraph(text, 8);

        Assert.DoesNotContain(chunks, static chunk => chunk.Length > 32);
    }

    [Fact]
    public void ChunkByParagraph_UnicodeText_SplitsCorrectly()
    {
        var chunks = TextChunker.ChunkByParagraph("こんにちは。\n\nПривет.");

        Assert.Equal(2, chunks.Count);
    }
}
