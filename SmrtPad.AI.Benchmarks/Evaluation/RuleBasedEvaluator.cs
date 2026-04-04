namespace SmrtPad.AI.Benchmarks.Evaluation;

/// <summary>
/// Evaluates a benchmark result against rule-based criteria.
/// Total score: 0–100 (TagCompliance 40, NoPreamble 20, NoClosingRemarks 20, ContentCompleteness 20).
/// Additional deductions (code fences, hedging) are applied within the NoPreamble and NoClosingRemarks buckets.
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
        // Also penalises code fences (markdown leak) within this bucket.
        int preamblePts = 20;
        if (ContaminationDetector.HasPreamble(textToCheck))
            preamblePts = 0;
        else if (ContaminationDetector.HasCodeFence(textToCheck))
            preamblePts = 10; // half credit — content is present but formatted wrong

        // No Closing Remarks (20 pts)
        // Also penalises hedging/filler language within this bucket.
        int closingPts = 20;
        if (ContaminationDetector.HasClosingRemark(textToCheck))
            closingPts = 0;
        else if (ContaminationDetector.HasHedging(textToCheck))
            closingPts = 10; // half credit — answer is self-contained but hedgy

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
            contentPts = (int)(20.0 * found / benchmarkCase.ExpectedKeywords.Length);
        }

        return new EvaluationScore(tagPts, preamblePts, closingPts, contentPts, null, null);
    }
}
