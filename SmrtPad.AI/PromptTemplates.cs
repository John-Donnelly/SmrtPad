namespace SmrtPad.AI;

/// <summary>Builds prompt strings for common AI operations.</summary>
public static class PromptTemplates
{
    /// <summary>Builds a summarization prompt that wraps the given <paramref name="text"/>.</summary>
    public static string Summarize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"You are a summarization assistant. Summarize the following text into one concise paragraph that captures the key points. Return only the summary, with no preamble or labels.\n\n{text}";
    }

    /// <summary>Builds a prompt that rewrites <paramref name="text"/> in a professional tone.</summary>
    public static string ToneProfessional(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"You are a writing assistant. Rewrite the following text in a professional, formal tone. Preserve the original meaning exactly. Return only the rewritten text, with no preamble or labels.\n\n{text}";
    }

    /// <summary>Builds a prompt that rewrites <paramref name="text"/> in a casual tone.</summary>
    public static string ToneCasual(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"You are a writing assistant. Rewrite the following text in a casual, friendly, conversational tone. Preserve the original meaning exactly. Return only the rewritten text, with no preamble or labels.\n\n{text}";
    }

    /// <summary>Builds a prompt that rewrites <paramref name="text"/> for clarity.</summary>
    public static string Rewrite(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"You are a writing assistant. Rewrite the following text to improve clarity and readability. Simplify complex sentences, remove ambiguity, and improve flow while preserving the original meaning. Return only the rewritten text, with no preamble or labels.\n\n{text}";
    }

    /// <summary>Builds a prompt that corrects grammar, spelling, and punctuation without changing meaning.</summary>
    public static string GrammarFix(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"You are a proofreading assistant. Correct all grammar, punctuation, and spelling errors in the following text. Do not change the meaning, tone, or style. Return only the corrected text, with no preamble or labels.\n\n{text}";
    }

    /// <summary>Builds a prompt that shortens <paramref name="text"/> while preserving meaning.</summary>
    public static string Shorten(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"You are a writing assistant. Shorten the following text by removing redundancy, filler words, and unnecessary detail while preserving all key information and meaning. Return only the shortened text, with no preamble or labels.\n\n{text}";
    }

    /// <summary>Builds a prompt that completes the current sentence from the existing context.</summary>
    public static string AutoComplete(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"You are a writing assistant. Continue the text below naturally, matching the existing style and tone. Write only the continuation — do not repeat any of the input text. Keep it concise and do not start a new paragraph unless the context clearly calls for one.\n\n{text}";
    }

    /// <summary>Returns the trimmed <paramref name="query"/> for semantic search.</summary>
    public static string SemanticQuery(string query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.Trim();
    }

    /// <summary>Builds a prompt to clean up raw OCR output.</summary>
    public static string OcrFallback(string rawOcrText)
    {
        ArgumentNullException.ThrowIfNull(rawOcrText);
        return $"You are a proofreading assistant. The following text was extracted via OCR and may contain recognition errors, missing spaces, or garbled characters. Correct any mistakes and return clean, readable text. Return only the corrected text, with no preamble or labels.\n\n{rawOcrText}";
    }

    /// <summary>Passes the user message through as a free-form chat turn.</summary>
    public static string FreeformChat(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return $"""
            You are a writing assistant embedded in a text editor. Your response is inserted directly into the user's document, so output only the content itself — plain text, no wrappers, no commentary.

            If the user asks you to write or draft a document (letter, email, report, essay, etc.): output only the finished document text. Use placeholders like [Your Name] where values are unknown.
            If the user asks a writing question or says something conversational: reply in one or two plain sentences only.

            User: {message}
            """;
    }
}
