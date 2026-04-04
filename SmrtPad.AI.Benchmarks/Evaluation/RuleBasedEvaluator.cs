namespace SmrtPad.AI.Benchmarks.Evaluation;

/// <summary>
/// Evaluates a benchmark result against rule-based criteria.
/// Total score: 0–100 (TagCompliance 40, NoPreamble 20, NoClosingRemarks 20, ContentCompleteness 20).
/// </summary>
internal static class RuleBasedEvaluator
{
    public static EvaluationScore Evaluate(BenchmarkCase benchmarkCase, string rawOutput, string? insertContent, string? thinkContent)
    {
        var textToCheck = insertContent ?? rawOutput;

        // Tag Compliance (40 pts)
        int tagPts;
        if (benchmarkCase.ExpectsInsertTag)
            tagPts = insertContent is not null ? 40 : 0;
        else
            tagPts = insertContent is null ? 40 : 0;

        // No Preamble (20 pts) — check the relevant output segment
        int preamblePts = ContaminationDetector.HasPreamble(textToCheck) ? 0 : 20;

        // No Closing Remarks (20 pts)
        int closingPts = ContaminationDetector.HasClosingRemark(textToCheck) ? 0 : 20;

        // Content Completeness (20 pts) — all expected keywords present
        int contentPts = 20;
        if (benchmarkCase.ExpectedKeywords.Length > 0)
        {
            int found = 0;
            foreach (var keyword in benchmarkCase.ExpectedKeywords)
            {
                if (textToCheck.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    found++;
            }
            contentPts = benchmarkCase.ExpectedKeywords.Length > 0
                ? (int)(20.0 * found / benchmarkCase.ExpectedKeywords.Length)
                : 20;
        }

        return new EvaluationScore(tagPts, preamblePts, closingPts, contentPts, null, null);
    }
}
