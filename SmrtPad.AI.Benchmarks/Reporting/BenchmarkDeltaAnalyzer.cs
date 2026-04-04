namespace SmrtPad.AI.Benchmarks.Reporting;

/// <summary>Score delta for a single benchmark case between two runs.</summary>
public sealed record CaseDelta(
    string CaseId,
    string Description,
    int BaselineRuleScore,
    int CurrentRuleScore,
    int RuleScoreChange,
    int? BaselineLlmScore,
    int? CurrentLlmScore,
    bool NewlyPassing,
    bool NewlyFailing);

/// <summary>Comparison report between a baseline and current benchmark run.</summary>
public sealed record DeltaReport(
    string BaselineRunId,
    string CurrentRunId,
    IReadOnlyList<CaseDelta> Deltas,
    double BaselineAvgRule,
    double CurrentAvgRule);

/// <summary>
/// Compares two <see cref="BenchmarkRun"/> instances to show score changes per case.
/// </summary>
public static class BenchmarkDeltaAnalyzer
{
    public static DeltaReport CompareRuns(BenchmarkRun baseline, BenchmarkRun current)
    {
        var baselineLookup = baseline.Results.ToDictionary(r => r.Case.Id);
        var deltas = new List<CaseDelta>();

        foreach (var cur in current.Results)
        {
            if (!baselineLookup.TryGetValue(cur.Case.Id, out var baseResult))
                continue;

            int baseRule = baseResult.Evaluation.RuleScore;
            int curRule = cur.Evaluation.RuleScore;
            bool newlyPassing = baseRule < 70 && curRule >= 70;
            bool newlyFailing = baseRule >= 70 && curRule < 70;

            deltas.Add(new CaseDelta(
                cur.Case.Id,
                cur.Case.Description,
                baseRule,
                curRule,
                curRule - baseRule,
                baseResult.Evaluation.LlmQualityScore,
                cur.Evaluation.LlmQualityScore,
                newlyPassing,
                newlyFailing));
        }

        double baseAvg = baseline.Results.Average(r => r.Evaluation.RuleScore);
        double curAvg = current.Results.Average(r => r.Evaluation.RuleScore);

        return new DeltaReport(baseline.RunId, current.RunId, deltas, baseAvg, curAvg);
    }

    /// <summary>Generates a Markdown delta comparison report.</summary>
    public static string GenerateDeltaMarkdown(DeltaReport delta)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Benchmark Delta Report");
        sb.AppendLine();
        sb.AppendLine($"| Property | Value |");
        sb.AppendLine($"|----------|-------|");
        sb.AppendLine($"| Baseline Run | `{delta.BaselineRunId}` |");
        sb.AppendLine($"| Current Run | `{delta.CurrentRunId}` |");
        sb.AppendLine($"| Baseline Avg | {delta.BaselineAvgRule:F1} |");
        sb.AppendLine($"| Current Avg | {delta.CurrentAvgRule:F1} |");
        sb.AppendLine($"| Avg Change | {delta.CurrentAvgRule - delta.BaselineAvgRule:+0.0;-0.0;0.0} |");
        sb.AppendLine();

        var newlyPassing = delta.Deltas.Where(d => d.NewlyPassing).ToList();
        var newlyFailing = delta.Deltas.Where(d => d.NewlyFailing).ToList();

        if (newlyPassing.Count > 0)
        {
            sb.AppendLine("## ✅ Newly Passing");
            foreach (var d in newlyPassing)
                sb.AppendLine($"- **{d.CaseId}**: {d.Description} ({d.BaselineRuleScore} → {d.CurrentRuleScore})");
            sb.AppendLine();
        }

        if (newlyFailing.Count > 0)
        {
            sb.AppendLine("## ❌ Newly Failing");
            foreach (var d in newlyFailing)
                sb.AppendLine($"- **{d.CaseId}**: {d.Description} ({d.BaselineRuleScore} → {d.CurrentRuleScore})");
            sb.AppendLine();
        }

        sb.AppendLine("## All Cases");
        sb.AppendLine();
        sb.AppendLine("| Case | Baseline | Current | Change | LLM Base | LLM Cur |");
        sb.AppendLine("|------|--------:|---------:|-------:|---------:|--------:|");
        foreach (var d in delta.Deltas)
        {
            var change = d.RuleScoreChange > 0 ? $"+{d.RuleScoreChange}" : d.RuleScoreChange.ToString();
            var llmBase = d.BaselineLlmScore?.ToString() ?? "N/A";
            var llmCur = d.CurrentLlmScore?.ToString() ?? "N/A";
            sb.AppendLine($"| {d.CaseId} | {d.BaselineRuleScore} | {d.CurrentRuleScore} | {change} | {llmBase} | {llmCur} |");
        }

        return sb.ToString();
    }
}
