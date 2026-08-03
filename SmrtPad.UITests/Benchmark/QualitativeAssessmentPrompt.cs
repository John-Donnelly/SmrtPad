using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace SmrtPad.UITests.Benchmark;

/// <summary>
/// Generates a Markdown prompt with sampled benchmark results that can be
/// pasted into any chat-capable model for qualitative "LLM-as-judge"
/// assessment of model output quality.
/// </summary>
public static class QualitativeAssessmentPrompt
{
    /// <summary>Maximum results to include per model to keep the prompt concise.</summary>
    private const int SamplesPerModel = 5;

    /// <summary>
    /// Generates the qualitative assessment prompt and saves it to a file.
    /// </summary>
    public static string Generate(BenchmarkRunReport report, string outputDir)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(outputDir);

        var prompt = BuildPrompt(report);

        Directory.CreateDirectory(outputDir);
        var path = Path.Combine(outputDir,
            $"qualitative-assessment-prompt-{report.Timestamp:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(path, prompt);

        return path;
    }

    /// <summary>
    /// Builds the assessment prompt as a Markdown string.
    /// </summary>
    public static string BuildPrompt(BenchmarkRunReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Qualitative Assessment Request");
        sb.AppendLine();
        sb.AppendLine("You are evaluating AI model outputs from SmrtPad's Smart Sidebar benchmarks.");
        sb.AppendLine("For each sample below, assess the output quality on these dimensions:");
        sb.AppendLine();
        sb.AppendLine("1. **Relevance** (0-10): Does the output address the prompt correctly?");
        sb.AppendLine("2. **Coherence** (0-10): Is the text well-structured and logical?");
        sb.AppendLine("3. **Accuracy** (0-10): Are facts, grammar fixes, or tone shifts correct?");
        sb.AppendLine("4. **Fluency** (0-10): Is the language natural and readable?");
        sb.AppendLine("5. **Helpfulness** (0-10): Would this output be useful to the end user?");
        sb.AppendLine();
        sb.AppendLine("After assessing each sample, provide:");
        sb.AppendLine("- A summary table with average scores per model");
        sb.AppendLine("- Top 3 models by overall quality");
        sb.AppendLine("- Notable strengths/weaknesses per model");
        sb.AppendLine("- Any concerning patterns (hallucinations, refusals, irrelevant content)");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var model in report.ModelsRun)
        {
            var results = report.Results
                .Where(r => r.ModelAlias == model && r.Succeeded)
                .ToList();

            if (results.Count == 0)
            {
                sb.AppendLine($"## {model}");
                sb.AppendLine();
                sb.AppendLine("*All prompts failed for this model.*");
                sb.AppendLine();
                continue;
            }

            // Sample diverse skill keys
            var sampled = SampleDiverse(results, report.PromptsRun, SamplesPerModel);

            sb.AppendLine($"## {model}");
            sb.AppendLine();

            var scores = report.Scores.Where(s => s.ModelAlias == model).ToList();
            var avgScore = scores.Count > 0 ? scores.Average(s => s.OverallScore) : 0;
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"*Rule-based avg score: {avgScore:F3} | Successful: {results.Count}/{report.PromptsRun.Count}*"));
            sb.AppendLine();

            foreach (var result in sampled)
            {
                var prompt = report.PromptsRun.FirstOrDefault(p => p.Id == result.PromptId);
                var score = report.Scores.FirstOrDefault(s => s.PromptId == result.PromptId && s.ModelAlias == model);

                sb.AppendLine($"### [{result.PromptId}] {prompt?.Description ?? result.SkillKey}");
                sb.AppendLine();
                sb.AppendLine($"**Skill:** `{result.SkillKey}` | **Score:** {score?.OverallScore.ToString("F3", CultureInfo.InvariantCulture) ?? "N/A"} | **TPS:** {result.TokensPerSecond.ToString("F1", CultureInfo.InvariantCulture)}");
                sb.AppendLine();
                sb.AppendLine("**Input:**");
                sb.AppendLine("```");
                sb.AppendLine(Truncate(result.InputText, 500));
                sb.AppendLine("```");
                sb.AppendLine();
                sb.AppendLine("**Output:**");
                sb.AppendLine("```");
                sb.AppendLine(Truncate(result.OutputText, 1000));
                sb.AppendLine("```");
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Samples results to ensure diverse skill coverage.
    /// </summary>
    private static List<BenchmarkResult> SampleDiverse(
        List<BenchmarkResult> results,
        IReadOnlyList<BenchmarkPrompt> prompts,
        int maxSamples)
    {
        // Group by skill key and take one from each, then fill remaining slots
        var bySkill = results
            .GroupBy(r => r.SkillKey)
            .OrderBy(g => g.Key)
            .ToList();

        var sampled = new List<BenchmarkResult>();

        // First pass: one from each skill
        foreach (var group in bySkill)
        {
            if (sampled.Count >= maxSamples) break;
            sampled.Add(group.First());
        }

        // Second pass: fill remaining with interesting results (lowest scores)
        if (sampled.Count < maxSamples)
        {
            var remaining = results
                .Except(sampled)
                .OrderBy(r => r.TokensPerSecond) // slowest first (interesting)
                .Take(maxSamples - sampled.Count);
            sampled.AddRange(remaining);
        }

        return sampled;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return string.Concat(text.AsSpan(0, maxLength), "\n[... truncated]");
    }
}
