namespace SmrtPad.UITests.Benchmark;

/// <summary>
/// A single prompt to be sent to the Smart Sidebar during benchmarking.
/// </summary>
/// <param name="Id">Unique identifier for correlation (e.g., "summarize-01").</param>
/// <param name="SkillKey">Skill key as used in the sidebar dropdown (e.g., "summarize", "freeform").</param>
/// <param name="InputText">The text to seed into the editor (or chat input for freeform).</param>
/// <param name="Description">Human-readable label for reports.</param>
/// <param name="ExpectedMinTokens">Minimum expected output tokens (for scoring).</param>
/// <param name="ExpectedMaxTokens">Maximum expected output tokens (for scoring).</param>
/// <param name="MustContainTags">Optional substrings the output should contain (case-insensitive).</param>
/// <param name="MustNotContainTags">Optional substrings the output must NOT contain (case-insensitive).</param>
public sealed record BenchmarkPrompt(
    string Id,
    string SkillKey,
    string InputText,
    string Description,
    int ExpectedMinTokens = 10,
    int ExpectedMaxTokens = 2000,
    string[]? MustContainTags = null,
    string[]? MustNotContainTags = null);
