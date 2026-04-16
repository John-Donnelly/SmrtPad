namespace SmrtPad.AI;

/// <summary>Builds prompt strings for common AI operations.</summary>
public static class PromptTemplates
{
    /// <summary>Builds a summarization prompt that wraps the given <paramref name="text"/>.</summary>
    public static string Summarize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"""
            Summarise the following text into one concise paragraph that keeps the key facts, names, numbers, and conclusions.
            Preserve the original meaning and important specifics, but remove repetition and background detail.
            Do not add a heading, bullets, commentary, markdown, code fences, or <think> tags.
            Output exactly one <insert>...</insert> block and nothing else.

            {text}
            """;
    }

    /// <summary>Builds a prompt that rewrites <paramref name="text"/> in a professional tone.</summary>
    public static string ToneProfessional(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"""
            Rewrite the following text in a professional, formal tone.
            Preserve the original meaning exactly, keep it plain text, and do not add commentary, signatures, subject lines, or <think> tags unless they already belong in the source.
            Output exactly one <insert>...</insert> block and nothing else.

            {text}
            """;
    }

    /// <summary>Builds a prompt that rewrites <paramref name="text"/> in a casual tone.</summary>
    public static string ToneCasual(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"""
            Rewrite the following text in a casual, friendly, conversational tone.
            Preserve the original meaning exactly, keep it plain text, and do not add commentary, emojis, sign-offs, or <think> tags unless they already belong in the source.
            Output exactly one <insert>...</insert> block and nothing else.

            {text}
            """;
    }

    /// <summary>Builds a prompt that rewrites <paramref name="text"/> for clarity.</summary>
    public static string Rewrite(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"""
            Rewrite the following text for clarity and readability.
            Simplify awkward wording, remove ambiguity, preserve the original meaning, and keep the result in plain text.
            Do not add commentary, examples, headings, or <think> tags.
            Output exactly one <insert>...</insert> block and nothing else.

            {text}
            """;
    }

    /// <summary>Builds a prompt that corrects grammar, spelling, and punctuation without changing meaning.</summary>
    public static string GrammarFix(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"""
            Correct grammar, punctuation, and spelling errors in the following text.
            Do not change the meaning, intended tone, point of view, or format, and do not add commentary or <think> tags.
            Output exactly one <insert>...</insert> block and nothing else.

            {text}
            """;
    }

    /// <summary>Builds a prompt that shortens <paramref name="text"/> while preserving meaning.</summary>
    public static string Shorten(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"""
            Shorten the following text by removing redundancy, filler, and unnecessary detail.
            Preserve the key information and meaning, keep the result in plain text, and do not add commentary, headings, or <think> tags.
            Output exactly one <insert>...</insert> block and nothing else.

            {text}
            """;
    }

    /// <summary>Builds a prompt that completes the current sentence from the existing context.</summary>
    public static string AutoComplete(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"""
            Continue the text below naturally, matching the existing style and tone.
            Write only the continuation, do not repeat the input, stay concise, and do not add commentary, scene-setting, or <think> tags.
            Output exactly one <insert>...</insert> block and nothing else.

            {text}
            """;
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
        return $"""
            The following text came from OCR and may contain recognition errors, missing spaces, or garbled characters.
            Correct it into clean, readable plain text while preserving the intended wording and structure. Do not add commentary, labels, or <think> tags.
            Output exactly one <insert>...</insert> block and nothing else.

            {rawOcrText}
            """;
    }

    /// <summary>Passes the user message through as a free-form chat turn.</summary>
    public static string FreeformChat(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return $"""
            Decide whether the user wants ready-to-paste writing or a direct answer.

            If the user wants drafted or transformed document content:
            - produce the finished writing only
            - use plain text unless markdown is explicitly requested
            - use [Placeholder] only for missing details that are not provided
            - obey exact requested structure such as counts, headings, sections, word length, rhyme scheme, or required fields
            - do not add commentary, preambles, sign-offs, subject lines, signatures, markdown fences, or <think> tags unless the requested document type clearly needs them
            - output exactly one <insert>...</insert> block and nothing else

            If the user is asking for advice, explanation, or comparison:
            - answer directly in plain text
            - use 1 to 3 concise sentences, or a short list only when the request explicitly asks for a list
            - do not use <insert> or <think> tags
            - do not add preambles, sign-offs, or commentary about what you are doing

            User request:
            {message}
            """;
    }

    /// <summary>Builds a prompt for the LLM quality grader to score a response on a 0–10 scale.</summary>
    public static string GradeResponse(string request, string response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        return $$"""
            You are a writing quality evaluator. Given a user's request and an AI-generated response, grade the response on a scale of 0 to 10. Consider: accuracy, completeness, style appropriateness, and absence of unnecessary boilerplate or filler. Return ONLY a JSON object wrapped in <grade> tags with no other text.

            Example output:
            <grade>{"score": 7, "reason": "Good structure but missing a closing paragraph."}</grade>

            User request:
            {{request}}

            AI response:
            {{response}}
            """;
    }
}
