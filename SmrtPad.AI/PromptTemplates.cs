namespace SmrtPad.AI;

/// <summary>Builds prompt strings for common AI operations.</summary>
public static class PromptTemplates
{
    /// <summary>Builds a summarization prompt that wraps the given <paramref name="text"/>.</summary>
    public static string Summarize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"Summarize the following text concisely:\n\n{text}";
    }

    /// <summary>Builds a prompt that rewrites <paramref name="text"/> in a professional tone.</summary>
    public static string ToneProfessional(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"Rewrite the following text in a professional tone:\n\n{text}";
    }

    /// <summary>Builds a prompt that rewrites <paramref name="text"/> in a casual tone.</summary>
    public static string ToneCasual(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"Rewrite the following text in a casual, friendly tone:\n\n{text}";
    }

    /// <summary>Builds a prompt that rewrites <paramref name="text"/> for clarity.</summary>
    public static string Rewrite(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"Rewrite the following text to improve clarity and readability:\n\n{text}";
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
        return $"The following text was extracted via OCR and may contain errors. Please correct any mistakes and return clean text:\n\n{rawOcrText}";
    }
}
