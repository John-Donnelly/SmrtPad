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

    // --- GrammarFix ---

    [Fact]
    public void GrammarFix_ContainsInputText()
    {
        var result = PromptTemplates.GrammarFix("teh sentence");
        Assert.Contains("teh sentence", result);
    }

    [Fact]
    public void GrammarFix_EmptyText_ReturnsValidPrompt()
    {
        var result = PromptTemplates.GrammarFix(string.Empty);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void GrammarFix_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PromptTemplates.GrammarFix(null!));
    }

    // --- Shorten ---

    [Fact]
    public void Shorten_ContainsInputText()
    {
        var result = PromptTemplates.Shorten("A much longer sentence than needed.");
        Assert.Contains("A much longer sentence than needed.", result);
    }

    [Fact]
    public void Shorten_EmptyText_ReturnsValidPrompt()
    {
        var result = PromptTemplates.Shorten(string.Empty);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void Shorten_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PromptTemplates.Shorten(null!));
    }

    // --- AutoComplete ---

    [Fact]
    public void AutoComplete_ContainsInputText()
    {
        var result = PromptTemplates.AutoComplete("Hello there");
        Assert.Contains("Hello there", result);
    }

    [Fact]
    public void AutoComplete_EmptyText_ReturnsValidPrompt()
    {
        var result = PromptTemplates.AutoComplete(string.Empty);
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void AutoComplete_NullText_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PromptTemplates.AutoComplete(null!));
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

    [Fact]
    public void GrammarFix_AndShorten_UseDistinctPrompts()
    {
        const string text = "Same input";
        var grammarFix = PromptTemplates.GrammarFix(text);
        var shorten = PromptTemplates.Shorten(text);
        Assert.NotEqual(grammarFix, shorten);
    }

    // --- FreeformChat ---

    [Fact]
    public void FreeformChat_ContainsUserMessage()
    {
        var result = PromptTemplates.FreeformChat("write a cover letter");
        Assert.Contains("write a cover letter", result);
    }

    [Fact]
    public void FreeformChat_EmptyMessage_ReturnsValidPrompt()
    {
        var result = PromptTemplates.FreeformChat("");
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void FreeformChat_NullMessage_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PromptTemplates.FreeformChat(null!));
    }

    [Fact]
    public void FreeformChat_IdentifiesAsWritingAssistant()
    {
        var result = PromptTemplates.FreeformChat("anything");
        Assert.Contains("writing assistant", result);
    }

    [Fact]
    public void FreeformChat_InstructsDocumentOutputOnly()
    {
        var result = PromptTemplates.FreeformChat("anything");
        Assert.Contains("output ONLY the finished document", result);
    }

    [Fact]
    public void FreeformChat_InstructsDocumentDraftBehaviour()
    {
        var result = PromptTemplates.FreeformChat("anything");
        Assert.Contains("write, draft, compose, or create", result);
    }

    [Fact]
    public void FreeformChat_InstructsConversationalBehaviour()
    {
        var result = PromptTemplates.FreeformChat("anything");
        Assert.Contains("one or two plain sentences", result);
    }

    [Fact]
    public void FreeformChat_MessageWithSpecialChars_ContainsRawMessage()
    {
        const string message = "Write an email: subject \"Q1 Report\" & attach <file>";
        var result = PromptTemplates.FreeformChat(message);
        Assert.Contains(message, result);
    }
}
