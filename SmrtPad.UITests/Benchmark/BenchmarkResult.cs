namespace SmrtPad.UITests.Benchmark;

/// <summary>
/// Captures the raw result of a single benchmark prompt execution.
/// </summary>
/// <param name="PromptId">Correlates back to <see cref="BenchmarkPrompt.Id"/>.</param>
/// <param name="ModelAlias">Foundry Local alias or "phi-silica" for NPU.</param>
/// <param name="ExecutionTarget">GPU, CPU, or NPU.</param>
/// <param name="SkillKey">Skill key that was active.</param>
/// <param name="InputText">Text that was sent.</param>
/// <param name="OutputText">Full response text captured from the sidebar.</param>
/// <param name="ElapsedSeconds">Wall-clock time from send to completion.</param>
/// <param name="EstimatedInputTokens">Rough token count of the input.</param>
/// <param name="EstimatedOutputTokens">Rough token count of the output.</param>
/// <param name="TokensPerSecond">Parsed from the HardwareBadge tooltip, or computed.</param>
/// <param name="Succeeded">Whether the interaction completed without error.</param>
/// <param name="ErrorMessage">Error details if <paramref name="Succeeded"/> is false.</param>
/// <param name="HardwareBadgeTooltip">Raw tooltip text from the HardwareBadge element.</param>
public sealed record BenchmarkResult(
    string PromptId,
    string ModelAlias,
    string ExecutionTarget,
    string SkillKey,
    string InputText,
    string OutputText,
    double ElapsedSeconds,
    int EstimatedInputTokens,
    int EstimatedOutputTokens,
    double TokensPerSecond,
    bool Succeeded,
    string? ErrorMessage = null,
    string? HardwareBadgeTooltip = null,
    string ReasoningTag = "NoThink")
{
    public string ModelDisplayLabel => $"{ModelAlias} [{ReasoningTag}]";
}
