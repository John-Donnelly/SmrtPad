namespace SmrtPad.AI.Benchmarks;

/// <summary>Score breakdown from rule-based evaluation.</summary>
public sealed record EvaluationScore(
    int TagCompliancePts,
    int NoPreamblePts,
    int NoClosingRemarksPts,
    int ContentCompletenessPts,
    int? LlmQualityScore,
    string? LlmQualityReason)
{
    /// <summary>Total rule-based score (0-100).</summary>
    public int RuleScore => TagCompliancePts + NoPreamblePts + NoClosingRemarksPts + ContentCompletenessPts;
}
