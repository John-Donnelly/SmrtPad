namespace SmrtPad.AI;

/// <summary>Builds prompt strings for common AI operations.</summary>
public static class PromptTemplates
{
    /// <summary>Builds a summarization prompt that wraps the given <paramref name="text"/>.</summary>
    public static string Summarize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"""
            You are a writing assistant in a text editor. Summarise the following text into one concise paragraph that captures all key points. Do not add a label or heading.

            If you need to reason through the task, put all reasoning inside <think> and </think> tags before the result.
            Then output ONLY the summary inside <insert> and </insert> tags. No text outside these tags. Do not add sign-off lines such as "I hope this helps" or "Let me know if you need anything".

            {text}
            """;
    }

    /// <summary>Builds a prompt that rewrites <paramref name="text"/> in a professional tone.</summary>
    public static string ToneProfessional(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"""
            You are a writing assistant in a text editor. Rewrite the following text in a professional, formal tone. Preserve the original meaning exactly.

            If you need to reason through the task, put all reasoning inside <think> and </think> tags before the result.
            Then output ONLY the rewritten text inside <insert> and </insert> tags. No text outside these tags. Do not add sign-off lines such as "I hope this helps" or "Let me know if you need anything".

            {text}
            """;
    }

    /// <summary>Builds a prompt that rewrites <paramref name="text"/> in a casual tone.</summary>
    public static string ToneCasual(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"""
            You are a writing assistant in a text editor. Rewrite the following text in a casual, friendly, conversational tone. Preserve the original meaning exactly.

            If you need to reason through the task, put all reasoning inside <think> and </think> tags before the result.
            Then output ONLY the rewritten text inside <insert> and </insert> tags. No text outside these tags. Do not add sign-off lines such as "I hope this helps" or "Let me know if you need anything".

            {text}
            """;
    }

    /// <summary>Builds a prompt that rewrites <paramref name="text"/> for clarity.</summary>
    public static string Rewrite(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"""
            You are a writing assistant in a text editor. Rewrite the following text to improve clarity and readability. Simplify complex sentences, remove ambiguity, and improve flow. Preserve the original meaning.

            If you need to reason through the task, put all reasoning inside <think> and </think> tags before the result.
            Then output ONLY the rewritten text inside <insert> and </insert> tags. No text outside these tags. Do not add sign-off lines such as "I hope this helps" or "Let me know if you need anything".

            {text}
            """;
    }

    /// <summary>Builds a prompt that corrects grammar, spelling, and punctuation without changing meaning.</summary>
    public static string GrammarFix(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"""
            You are a proofreading assistant in a text editor. Correct all grammar, punctuation, and spelling errors in the following text. Do not change the meaning, tone, or style.

            If you need to reason through the task, put all reasoning inside <think> and </think> tags before the result.
            Then output ONLY the corrected text inside <insert> and </insert> tags. No text outside these tags. Do not add sign-off lines such as "I hope this helps" or "Let me know if you need anything".

            {text}
            """;
    }

    /// <summary>Builds a prompt that shortens <paramref name="text"/> while preserving meaning.</summary>
    public static string Shorten(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"""
            You are a writing assistant in a text editor. Shorten the following text by removing redundancy, filler words, and unnecessary detail. Preserve all key information and meaning.

            If you need to reason through the task, put all reasoning inside <think> and </think> tags before the result.
            Then output ONLY the shortened text inside <insert> and </insert> tags. No text outside these tags. Do not add sign-off lines such as "I hope this helps" or "Let me know if you need anything".

            {text}
            """;
    }

    /// <summary>Builds a prompt that completes the current sentence from the existing context.</summary>
    public static string AutoComplete(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return $"""
            You are a writing assistant in a text editor. Continue the text below naturally, matching the existing style and tone. Write only the continuation — do not repeat any of the input text. Stay concise; do not start a new paragraph unless the context clearly calls for one.

            Output ONLY the continuation inside <insert> and </insert> tags. No text outside the tags.

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
            You are a proofreading assistant in a text editor. The following text was extracted via OCR and may contain recognition errors, missing spaces, or garbled characters. Correct any mistakes and return clean, readable text.

            If you need to reason through the task, put all reasoning inside <think> and </think> tags before the result.
            Then output ONLY the clean text inside <insert> and </insert> tags. No text outside these tags. Do not add sign-off lines such as "I hope this helps" or "Let me know if you need anything".

            {rawOcrText}
            """;
    }

    /// <summary>Passes the user message through as a free-form chat turn.</summary>
    public static string FreeformChat(string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return $"""
            You are a writing assistant embedded in a text editor.

            DOCUMENT REQUEST — write, draft, compose, or create a document (letter, email, report, essay, story, press release, memo, resume, bio, announcement, agenda, minutes, or any other document):
            - If you need to plan, put reasoning inside <think> and </think> tags first.
            - output ONLY the finished document inside <insert> and </insert> tags. Use [Placeholder] for unknown personal details. No text outside the tags.

            QUESTION OR CONVERSATION — a question, request for tips, grammar explanation, or any conversational message:
            - Reply in plain text only, 1 to 3 sentences. Do NOT use <insert> or <think> tags.
            - No preamble ("Sure!", "Of course!"). No sign-off lines ("I hope this helps", "Let me know if you need anything").

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
