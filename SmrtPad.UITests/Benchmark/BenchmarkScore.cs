namespace SmrtPad.UITests.Benchmark;

/// <summary>
/// Rule-based quality score for a single benchmark result.
/// </summary>
/// <param name="PromptId">Correlates back to <see cref="BenchmarkPrompt.Id"/>.</param>
/// <param name="ModelAlias">Model that produced the result.</param>
/// <param name="OverallScore">Aggregate score from 0.0 (worst) to 1.0 (best).</param>
/// <param name="LengthScore">Did the output length fall within expected bounds?</param>
/// <param name="TagComplianceScore">Did the output contain/avoid required tags?</param>
/// <param name="FormatScore">Were expected formatting patterns present (e.g., bullet lists, headings)?</param>
/// <param name="LatencyScore">Normalized latency rating (higher = faster).</param>
/// <param name="Notes">Human-readable notes about scoring deductions.</param>
public sealed record BenchmarkScore(
    string PromptId,
    string ModelAlias,
    double OverallScore,
    double LengthScore,
    double TagComplianceScore,
    double FormatScore,
    double LatencyScore,
    string Notes);
