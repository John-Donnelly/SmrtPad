using SmrtPad.AI;

namespace SmrtPad.AI.Tests;

public class PromptTemplatesTests
{
    // --- Summarize ---

    [Fact]
    public void Summarize_ContainsInputText()
    {
        var result = PromptTemplates.Summarize("Hello world");
        Assert.Contains("Hello world", result);
    }

    [Fact]
    public void Summarize_EmptyText_ReturnsValidPrompt()
    {
        var result = PromptTemplates.Summarize("");
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void Summarize_WhitespaceOnlyText_ReturnsValidPrompt()
    {
        var result = PromptTemplates.Summarize("   ");
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void Summarize_VeryLongText_ContainsEntireText()
    {
        var longText = new string('x', 10_000);
        var result = PromptTemplates.Summarize(longText);
        Assert.Contains(longText, result);
    }

    [Fact]
    public void Summarize_TextWithSpecialChars_ContainsRawText()
    {
        const string text = "Price: $100 & tax <5%> \"quoted\"";
        var result = PromptTemplates.Summarize(text);
        Assert.Contains(text, result);
    }

    [Fact]
    public void Summarize_TextWithCurlyBraces_DoesNotThrow()
    {
        var result = PromptTemplates.Summarize("{braces} and {{double}}");
        Assert.Contains("{braces}", result);
    }

    [Fact]
    public void Summarize_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PromptTemplates.Summarize(null!));
    }

    // --- ToneProfessional ---

    [Fact]
    public void ToneProfessional_ContainsInputText()
    {
        var result = PromptTemplates.ToneProfessional("Hey there");
        Assert.Contains("Hey there", result);
    }

    [Fact]
    public void ToneProfessional_EmptyText_ReturnsValidPrompt()
    {
        var result = PromptTemplates.ToneProfessional("");
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void ToneProfessional_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PromptTemplates.ToneProfessional(null!));
    }

    // --- ToneCasual ---

    [Fact]
    public void ToneCasual_ContainsInputText()
    {
        var result = PromptTemplates.ToneCasual("Formal message");
        Assert.Contains("Formal message", result);
    }

    [Fact]
    public void ToneCasual_EmptyText_ReturnsValidPrompt()
    {
        var result = PromptTemplates.ToneCasual("");
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void ToneCasual_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PromptTemplates.ToneCasual(null!));
    }

    // --- Rewrite ---

    [Fact]
    public void Rewrite_ContainsInputText()
    {
        var result = PromptTemplates.Rewrite("Messy sentence");
        Assert.Contains("Messy sentence", result);
    }

    [Fact]
    public void Rewrite_EmptyText_ReturnsValidPrompt()
    {
        var result = PromptTemplates.Rewrite("");
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void Rewrite_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PromptTemplates.Rewrite(null!));
    }

    // --- SemanticQuery ---

    [Fact]
    public void SemanticQuery_TrimsLeadingWhitespace()
    {
        var result = PromptTemplates.SemanticQuery("   query");
        Assert.Equal("query", result);
    }

    [Fact]
    public void SemanticQuery_TrimsTrailingWhitespace()
    {
        var result = PromptTemplates.SemanticQuery("query   ");
        Assert.Equal("query", result);
    }

    [Fact]
    public void SemanticQuery_AlreadyTrimmed_ReturnsSameValue()
    {
        var result = PromptTemplates.SemanticQuery("query");
        Assert.Equal("query", result);
    }

    [Fact]
    public void SemanticQuery_EmptyString_ReturnsEmptyString()
    {
        var result = PromptTemplates.SemanticQuery("");
        Assert.Equal("", result);
    }

    [Fact]
    public void SemanticQuery_WhitespaceOnly_ReturnsEmptyString()
    {
        var result = PromptTemplates.SemanticQuery("   ");
        Assert.Equal("", result);
    }

    // --- OcrFallback ---

    [Fact]
    public void OcrFallback_ContainsRawText()
    {
        var result = PromptTemplates.OcrFallback("s0me OCR t3xt");
        Assert.Contains("s0me OCR t3xt", result);
    }

    [Fact]
    public void OcrFallback_EmptyText_ReturnsValidPrompt()
    {
        var result = PromptTemplates.OcrFallback("");
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void OcrFallback_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PromptTemplates.OcrFallback(null!));
    }

    // --- Cross-method ---

    [Fact]
    public void ToneProfessional_AndToneCasual_UseDistinctPrompts()
    {
        const string text = "Same input";
        var professional = PromptTemplates.ToneProfessional(text);
        var casual = PromptTemplates.ToneCasual(text);
        Assert.NotEqual(professional, casual);
    }
}
