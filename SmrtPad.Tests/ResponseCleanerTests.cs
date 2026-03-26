using SmrtPad.Helpers;

namespace SmrtPad.Tests;

public class ResponseCleanerTests
{
    // --- Code fences ---

    [Fact]
    public void Clean_RemovesTripleBacktickFence()
    {
        var input = "```\nDear John,\n\nThank you.\n```";
        Assert.Equal("Dear John,\n\nThank you.", ResponseCleaner.Clean(input));
    }

    [Fact]
    public void Clean_RemovesTripleBacktickFenceWithLanguageTag()
    {
        var input = "```plaintext\nHello world\n```";
        Assert.Equal("Hello world", ResponseCleaner.Clean(input));
    }

    [Fact]
    public void Clean_RemovesTripleSingleQuoteFence()
    {
        var input = "'''\nHello world\n'''";
        Assert.Equal("Hello world", ResponseCleaner.Clean(input));
    }

    // --- Preamble lines ---

    [Fact]
    public void Clean_RemovesPreambleEndingWithColon()
    {
        var input = "Sure, here is your letter:\n\nDear Sir,\n\nYours faithfully,\n[Name]";
        Assert.Equal("Dear Sir,\n\nYours faithfully,\n[Name]", ResponseCleaner.Clean(input));
    }

    [Fact]
    public void Clean_RemovesMultiplePreambleLines()
    {
        var input = "Of course:\nHere is the draft:\n\nDear Sir,\n\nSincerely,\n[Name]";
        Assert.Equal("Dear Sir,\n\nSincerely,\n[Name]", ResponseCleaner.Clean(input));
    }

    [Fact]
    public void Clean_DoesNotRemoveBodyLineEndingWithColon()
    {
        // A colon-ending line mid-document (after content has started) must be preserved
        var input = "Dear Sir,\n\nRegarding your complaint:\n\nWe have reviewed it.";
        var result = ResponseCleaner.Clean(input);
        Assert.Contains("Regarding your complaint:", result);
    }

    // --- Closing remarks ---

    [Fact]
    public void Clean_RemovesIfYouNeedFurtherAssistanceLine()
    {
        var input = "Dear Sir,\n\nSincerely,\n[Name]\n\nIf you need further assistance, please contact us.";
        Assert.DoesNotContain("If you need further assistance", ResponseCleaner.Clean(input));
    }

    [Fact]
    public void Clean_RemovesLetMeKnowLine()
    {
        var input = "Yours faithfully,\n[Name]\n\nLet me know if you need any changes.";
        Assert.DoesNotContain("Let me know", ResponseCleaner.Clean(input));
    }

    [Fact]
    public void Clean_RemovesPleaseLetMeLine()
    {
        var input = "Best regards,\n[Name]\n\nPlease let me know if this resolves your issue.";
        Assert.DoesNotContain("Please let me know", ResponseCleaner.Clean(input));
    }

    [Fact]
    public void Clean_RemovesFeelFreeToLine()
    {
        var input = "Regards,\n[Name]\n\nFeel free to contact me at any time.";
        Assert.DoesNotContain("Feel free to contact", ResponseCleaner.Clean(input));
    }

    // --- Reasoning leaks ---

    [Fact]
    public void Clean_RemovesOkayLeadLine()
    {
        var input = "Okay, let's see. The user wants a letter.\n\nDear Sir,\n\nSincerely,\n[Name]";
        var result = ResponseCleaner.Clean(input);
        Assert.DoesNotContain("Okay,", result);
        Assert.Contains("Dear Sir,", result);
    }

    [Fact]
    public void Clean_RemovesAlrightLeadLine()
    {
        var input = "Alright, here we go.\n\nDear Madam,\n\nYours,\n[Name]";
        var result = ResponseCleaner.Clean(input);
        Assert.DoesNotContain("Alright,", result);
        Assert.Contains("Dear Madam,", result);
    }

    // --- Clean content is preserved ---

    [Fact]
    public void Clean_PreservesFullLetterContent()
    {
        const string letter = "Dear [Recipient],\n\nI am writing to follow up on my complaint.\n\nYours faithfully,\n[Your Name]";
        Assert.Equal(letter, ResponseCleaner.Clean(letter));
    }

    [Fact]
    public void Clean_PreservesShortConversationalReply()
    {
        const string reply = "Hello! How can I help you today?";
        Assert.Equal(reply, ResponseCleaner.Clean(reply));
    }

    // --- Edge cases ---

    [Fact]
    public void Clean_EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ResponseCleaner.Clean(string.Empty));
    }

    [Fact]
    public void Clean_WhitespaceOnly_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, ResponseCleaner.Clean("   \n\n  "));
    }

    [Fact]
    public void Clean_TrimsTrailingBlankLines()
    {
        var input = "Dear Sir,\n\nSincerely,\n[Name]\n\n\n";
        Assert.Equal("Dear Sir,\n\nSincerely,\n[Name]", ResponseCleaner.Clean(input));
    }

    [Fact]
    public void Clean_NoPreambleOrFence_ReturnsUnchanged()
    {
        const string text = "Here is a straightforward paragraph of text that needs no cleaning.";
        Assert.Equal(text, ResponseCleaner.Clean(text));
    }

    [Fact]
    public void Clean_FenceAndPreambleAndClosing_AllRemoved()
    {
        var input = "Sure, here is your letter:\n```\nDear Sir,\n\nSincerely,\n[Name]\n```\n\nLet me know if you need changes.";
        Assert.Equal("Dear Sir,\n\nSincerely,\n[Name]", ResponseCleaner.Clean(input));
    }
}
