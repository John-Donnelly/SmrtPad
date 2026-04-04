namespace SmrtPad.AI.Benchmarks.Reporting;

/// <summary>
/// Generates Markdown and JSON benchmark reports from a <see cref="BenchmarkRun"/>.
/// </summary>
public static class BenchmarkReportGenerator
{
    /// <summary>Pass threshold — cases scoring at or above this value are considered passing.</summary>
    internal const int PassThreshold = 80;

    /// <summary>Generates a human-readable Markdown report.</summary>
    public static string GenerateMarkdownReport(BenchmarkRun run)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# AI Benchmark Report");
        sb.AppendLine();
        sb.AppendLine($"| Property | Value |");
        sb.AppendLine($"|----------|-------|");
        sb.AppendLine($"| Run ID | `{run.RunId}` |");
        sb.AppendLine($"| Model | `{run.ModelAlias}` |");
        sb.AppendLine($"| Backend | `{run.BackendTarget}` |");
        sb.AppendLine($"| Started | {run.StartedAt:yyyy-MM-dd HH:mm:ss UTC} |");
        sb.AppendLine($"| Total Cases | {run.Results.Count} |");
        sb.AppendLine();

        if (run.Results.Count == 0)
        {
            sb.AppendLine("**No benchmark results were produced.** All cases may have failed to execute.");
            return sb.ToString();
        }

        // Summary table by category
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"| Category | Tests | Pass (≥{PassThreshold}) | Fail | Avg Rule | Avg LLM |");
        sb.AppendLine("|----------|------:|----------:|-----:|---------:|--------:|");

        foreach (var category in Enum.GetValues<BenchmarkCategory>())
        {
            var group = run.Results.Where(r => r.Case.Category == category).ToList();
            if (group.Count == 0) continue;

            int pass = group.Count(r => r.Evaluation.RuleScore >= PassThreshold);
            int fail = group.Count - pass;
            double avgRule = group.Average(r => r.Evaluation.RuleScore);
            var llmScores = group.Where(r => r.Evaluation.LlmQualityScore.HasValue).ToList();
            string avgLlm = llmScores.Count > 0
                ? $"{llmScores.Average(r => r.Evaluation.LlmQualityScore!.Value):F1}"
                : "N/A";

            sb.AppendLine($"| {category} | {group.Count} | {pass} | {fail} | {avgRule:F1} | {avgLlm} |");
        }

        sb.AppendLine();

        // Multi-model comparison (shown when results span two or more distinct model+backend combinations)
        var modelGroups = run.Results
            .GroupBy(r => new { r.ModelAlias, r.BackendTarget })
            .OrderByDescending(g => g.Average(r => r.Evaluation.RuleScore))
            .ToList();
        if (modelGroups.Count > 1)
        {
            sb.AppendLine("## Model Comparison");
            sb.AppendLine();
            sb.AppendLine($"| Model | Backend | Tests | Pass Rate | Avg Score | Avg Tok/s | Total £ Cost |");
            sb.AppendLine("|-------|---------|------:|----------:|----------:|----------:|-------------:|");
            foreach (var g in modelGroups)
            {
                int pass = g.Count(r => r.Evaluation.RuleScore >= PassThreshold);
                double passRate = g.Count() > 0 ? 100.0 * pass / g.Count() : 0;
                double avgScore = g.Average(r => r.Evaluation.RuleScore);
                double avgTok = g.Average(r => r.TokensPerSecond);
                double cost = g.Sum(r => r.TotalCostUsd);
                sb.AppendLine($"| `{g.Key.ModelAlias}` | {g.Key.BackendTarget} | {g.Count()} | {passRate:F0}% | {avgScore:F1} | {avgTok:F1} | £{cost:F4} |");
            }
            sb.AppendLine();
        }

        // Overall stats
        double overallAvg = run.Results.Average(r => r.Evaluation.RuleScore);
        int overallPass = run.Results.Count(r => r.Evaluation.RuleScore >= PassThreshold);
        sb.AppendLine($"**Overall: {overallPass}/{run.Results.Count} passing, avg rule score {overallAvg:F1}/100**");
        sb.AppendLine();

        // Per-case details
        sb.AppendLine("## Detailed Results");
        sb.AppendLine();

        foreach (var result in run.Results)
        {
            var status = result.Evaluation.RuleScore >= PassThreshold ? "✅" : "❌";
            sb.AppendLine($"### {status} {result.Case.Id}: {result.Case.Description}");
            sb.AppendLine();
            sb.AppendLine($"| Metric | Score |");
            sb.AppendLine($"|--------|------:|");
            sb.AppendLine($"| Tag Compliance | {result.Evaluation.TagCompliancePts}/40 |");
            sb.AppendLine($"| No Preamble | {result.Evaluation.NoPreamblePts}/20 |");
            sb.AppendLine($"| No Closing Remarks | {result.Evaluation.NoClosingRemarksPts}/20 |");
            sb.AppendLine($"| Content Completeness | {result.Evaluation.ContentCompletenessPts}/20 |");
            sb.AppendLine($"| **Rule Total** | **{result.Evaluation.RuleScore}/100** |");
            if (result.Evaluation.LlmQualityScore.HasValue)
                sb.AppendLine($"| LLM Grade | {result.Evaluation.LlmQualityScore}/10 |");
            else
                sb.AppendLine($"| LLM Grade | N/A |");
            sb.AppendLine($"| Latency | {result.LatencyMs}ms |");
            sb.AppendLine($"| TTFT | {result.TimeToFirstTokenMs}ms |");
            sb.AppendLine($"| Generation Time | {result.GenerationMs}ms |");
            sb.AppendLine($"| Throughput | {result.TokensPerSecond:F1} tok/s |");
            sb.AppendLine($"| Tokens (in/out) | {result.EstimatedInputTokens}/{result.EstimatedOutputTokens} |");
            sb.AppendLine($"| £/Token | £{result.TokenCostUsd:F8} |");
            sb.AppendLine($"| Electricity Cost | £{result.ElectricityCostUsd:F6} |");
            sb.AppendLine($"| Total Cost | £{result.TotalCostUsd:F6} |");
            sb.AppendLine();

            if (result.Evaluation.LlmQualityReason is not null)
            {
                sb.AppendLine($"**LLM Reason:** {result.Evaluation.LlmQualityReason}");
                sb.AppendLine();
            }

            sb.AppendLine("<details>");
            sb.AppendLine($"<summary>Input (click to expand)</summary>");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine(Truncate(result.Case.InputText, 500));
            sb.AppendLine("```");
            sb.AppendLine("</details>");
            sb.AppendLine();

            if (result.InsertContent is not null)
            {
                sb.AppendLine("<details>");
                sb.AppendLine("<summary>Insert content (click to expand)</summary>");
                sb.AppendLine();
                sb.AppendLine("> " + result.InsertContent.Replace("\n", "\n> "));
                sb.AppendLine();
                sb.AppendLine("</details>");
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>Generates a machine-readable JSON report.</summary>
    public static string GenerateJsonReport(BenchmarkRun run)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        return JsonSerializer.Serialize(run, options);
    }

    /// <summary>Writes both Markdown and JSON reports to the specified directory.</summary>
    public static void WriteReports(BenchmarkRun run, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var mdPath = Path.Combine(outputDir, $"{run.RunId}-report.md");
        var jsonPath = Path.Combine(outputDir, $"{run.RunId}-report.json");
        File.WriteAllText(mdPath, GenerateMarkdownReport(run));
        File.WriteAllText(jsonPath, GenerateJsonReport(run));
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "…";
}
