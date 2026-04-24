namespace SmrtPad.AI;

/// <summary>
/// Controls model-specific system prompting and optional thinking-mode behavior.
/// </summary>
public enum ModelReasoningMode
{
    Default = 0,
    NoThinking = 1,
    Thinking = 2,
}

/// <summary>
/// Builds model-tuned system prompts and reasoning-mode controls.
/// </summary>
public static class ModelPromptPolicy
{
    /// <summary>Returns <c>true</c> when the model supports both thinking and non-thinking benchmark variants.</summary>
    public static bool SupportsThinkingMode(string? modelAlias, string? family = null)
    {
        var alias = NormalizeAlias(modelAlias);

        if (alias.Contains("qwen3", StringComparison.OrdinalIgnoreCase))
            return true;

        if (alias.StartsWith("phi-", StringComparison.OrdinalIgnoreCase))
            return true;

        if (alias.Contains("deepseek-r1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(family, "deepseek", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>Returns the short dashboard tag for a reasoning mode.</summary>
    public static string GetModeTag(ModelReasoningMode mode) => mode switch
    {
        ModelReasoningMode.Thinking => "Think",
        _ => "NoThink",
    };

    internal static string DetectAliasFromPath(string modelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);

        var normalizedPath = NormalizeAlias(modelPath);

        foreach (var alias in ModelSizeSelector.AllAliases.Concat(GgufModelCatalog.AllAliases).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (normalizedPath.Contains(NormalizeAlias(alias), StringComparison.OrdinalIgnoreCase))
                return alias;
        }

        return Path.GetFileNameWithoutExtension(modelPath);
    }

    internal static string BuildSystemPrompt(string? modelAlias, string family, ModelReasoningMode requestedMode)
    {
        var alias = NormalizeAlias(modelAlias);
        var effectiveMode = NormalizeMode(modelAlias, family, requestedMode);
        var modeDirective = effectiveMode switch
        {
            ModelReasoningMode.Thinking =>
                "Thinking mode is enabled for this turn. Use a single <think>...</think> block only if hidden scratch work is genuinely necessary. Keep all reasoning inside that block. The final answer must appear outside <think> and must not mention the reasoning process.",
            _ =>
                "Non-thinking mode is enabled for this turn. Plan silently. Do not output reasoning, analysis, chain-of-thought, planning text, self-talk, or <think> tags.",
        };

        var modelDirective = GetModelDirective(alias, family);

        return $"""
            You are SmrtPad, an in-editor writing assistant.
            The user wants ready-to-use writing or a direct, useful answer — not commentary about what you are doing.
            Obey the requested tone, audience, format, and length exactly.
            Preserve all supplied facts, names, dates, numbers, and constraints unless the user explicitly asks you to change them.
            Satisfy exact structure requests such as item counts, section names, word limits, stanza counts, rhyme schemes, and JSON shapes.
            Start directly with the requested content. Do not begin with preambles such as "Alright", "Okay", "Sure", "Here is", or "I can help with that".
            Prefer clean final prose over explanation.
            Do not add commentary, scene-setting, apologies, coaching language, or extra wrap-up text.
            Do not add headings, labels, subject lines, signatures, bullets, or markdown unless the user asks for them or the document type clearly requires them.
            Do not invent missing facts, names, dates, addresses, metrics, citations, or contact details. Use [Placeholder] only when a necessary detail is missing.
            Use plain text unless the user explicitly asks for markdown.
            Never emit code fences.
            {modeDirective}
            If the user prompt asks for <insert> tags, return exactly one <insert>...</insert> block containing the final answer and nothing else outside it except an optional <think> block when thinking mode is enabled.
            Never put <think> inside <insert>.
            If the user prompt asks for a direct answer, reply directly without <insert> tags.
            {modelDirective}
            """;
    }

    internal static string ApplyPromptControls(string prompt, string? modelAlias, string family, ModelReasoningMode requestedMode)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var mode = NormalizeMode(modelAlias, family, requestedMode);
        return family switch
        {
            "qwen3" when mode == ModelReasoningMode.Thinking => "/think\n" + prompt,
            "qwen3" => "/no_think\n" + prompt,
            _ => prompt,
        };
    }

    internal static ModelReasoningMode NormalizeMode(string? modelAlias, string family, ModelReasoningMode requestedMode)
    {
        var effective = requestedMode == ModelReasoningMode.Default
            ? ModelReasoningMode.NoThinking
            : requestedMode;

        return SupportsThinkingMode(modelAlias, family)
            ? effective
            : ModelReasoningMode.NoThinking;
    }

    private static string GetModelDirective(string alias, string family)
    {
        if (alias.StartsWith("gemma-4-e4b", StringComparison.OrdinalIgnoreCase))
        {
            return "For Gemma 4 E4B: be decisive, highly literal, and structured. Deliver polished business-quality writing with strong formatting discipline and no filler.";
        }

        if (alias.StartsWith("gemma-4-e2b", StringComparison.OrdinalIgnoreCase))
        {
            return "Gemma 4 E2B: never open with a line ending in a colon. Never close with \"let me know\", \"feel free to\", \"I hope this\", or \"don't hesitate\". Never use \"perhaps\", \"maybe\", or \"I would suggest\". Start and end with the answer itself.";
        }

        if (alias.StartsWith("gemma-3-4b", StringComparison.OrdinalIgnoreCase))
        {
            return "For Gemma 3 4B: write clear, natural prose with strong instruction-following. Do not echo the prompt or restate the task.";
        }

        if (alias.StartsWith("gemma-3-1b", StringComparison.OrdinalIgnoreCase))
        {
            return "For Gemma 3 1B: keep outputs short, concrete, and low-variance. Use the simplest wording that fully satisfies the request.";
        }

        if (alias.StartsWith("llama-3.2-3b", StringComparison.OrdinalIgnoreCase))
        {
            return "For Llama 3.2 3B: write natural, human-sounding prose with a steady voice. Avoid repetitive phrasing and avoid over-explaining.";
        }

        if (alias.StartsWith("llama-3.2-1b", StringComparison.OrdinalIgnoreCase))
        {
            return "For Llama 3.2 1B: stay concise and stable. Favor one strong draft over extra elaboration.";
        }

        if (alias.StartsWith("phi-4-mini-reasoning", StringComparison.OrdinalIgnoreCase))
        {
            return "For Phi-4 Mini Reasoning: reason carefully when allowed, but make the final answer crisp, polished, and fully usable. Never let analysis leak into the final answer.";
        }

        if (alias.StartsWith("phi-4-mini", StringComparison.OrdinalIgnoreCase))
        {
            return "For Phi-4 Mini: prioritize precise edits, factual retention, and polished professional wording. Keep outputs efficient and high-signal.";
        }

        if (alias.StartsWith("phi-3.5-mini", StringComparison.OrdinalIgnoreCase))
        {
            return "For Phi-3.5 Mini: produce compact, accurate revisions that preserve meaning exactly. Avoid decorative wording and keep the result practical.";
        }

        if (alias.StartsWith("qwen3-1.7b", StringComparison.OrdinalIgnoreCase))
        {
            return "For Qwen3 1.7B: follow output contracts literally, especially tags and requested format. Keep the answer deterministic, direct, and free of extra commentary.";
        }

        if (alias.StartsWith("qwen3-0.6b", StringComparison.OrdinalIgnoreCase))
        {
            return "For Qwen3 0.6B: use minimal, exact wording and simple structure. Do not add any extra explanation beyond the requested output.";
        }

        if (alias.StartsWith("qwen2.5-0.5b", StringComparison.OrdinalIgnoreCase))
        {
            return "For Qwen2.5 0.5B: keep responses short, literal, and format-safe. Prefer straightforward phrasing and avoid optional embellishments.";
        }

        if (alias.StartsWith("qwen2.5-7b", StringComparison.OrdinalIgnoreCase))
        {
            return "For Qwen2.5 7B: follow the requested structure exactly, keep formatting clean, and avoid extra explanation or duplicated tags. Prefer a single polished final answer over elaboration.";
        }

        if (alias.Contains("qwen2.5-coder-7b", StringComparison.OrdinalIgnoreCase))
        {
            return "For Qwen2.5 Coder 7B: obey output contracts literally, keep generated text format-safe, and avoid code fences, HTML wrappers, or explanatory lead-ins unless explicitly requested.";
        }

        if (alias.StartsWith("deepseek-r1", StringComparison.OrdinalIgnoreCase))
        {
            return "For DeepSeek-R1: suppress conversational self-talk and never narrate your plan. Skip lead-ins like 'Alright' or 'Let me think'. Keep any reasoning fully contained, and make the final answer terse, contract-faithful, and free of extra commentary.";
        }

        return family switch
        {
            "gemma4" => "For Gemma 4 models: be explicit, format-safe, and concise.",
            "gemma3" => "For Gemma 3 models: be clear, literal, and low-noise.",
            "llama" => "For Llama models: write natural prose without echoing the instruction.",
            "qwen3" => "For Qwen3 models: follow the output contract exactly and avoid extra words.",
            "qwen25" => "For Qwen2.5 models: keep the result short, literal, and stable.",
            "phi" => "For Phi models: focus on precise, high-quality editing and polished final wording.",
            "deepseek" => "For DeepSeek models: keep reasoning separated from the final answer.",
            _ => "Be accurate, concise, and directly useful.",
        };
    }

    private static string NormalizeAlias(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Replace('_', '-')
            .Replace(' ', '-')
            .ToLowerInvariant();
    }
}