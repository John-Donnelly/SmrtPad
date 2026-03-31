using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SmrtPad.UITests.Benchmark;

/// <summary>
/// Scores a benchmark result against the prompt's expected constraints using
/// deterministic rule-based checks. Produces a <see cref="BenchmarkScore"/>
/// with component scores from 0.0 (worst) to 1.0 (best).
/// </summary>
public sealed partial class RuleBasedScorer
{
    /// <summary>
    /// Reference latency in seconds for "good" performance (tokens/sec-based normalization).
    /// Results faster than this get a perfect latency score; slower results scale linearly.
    /// </summary>
    private const double ReferenceLatencySeconds = 10.0;

    /// <summary>Weight of each component in the overall score.</summary>
    private const double WeightLength = 0.30;
    private const double WeightTagCompliance = 0.30;
    private const double WeightFormat = 0.15;
    private const double WeightLatency = 0.25;

    /// <summary>
    /// Scores a single benchmark result against its prompt expectations.
    /// </summary>
    public BenchmarkScore Score(BenchmarkPrompt prompt, BenchmarkResult result)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(result);

        var notes = new List<string>();

        var lengthScore = ScoreLength(prompt, result, notes);
        var tagScore = ScoreTagCompliance(prompt, result, notes);
        var formatScore = ScoreFormat(prompt, result, notes);
        var latencyScore = ScoreLatency(result, notes);

        var overall = (lengthScore * WeightLength)
            + (tagScore * WeightTagCompliance)
            + (formatScore * WeightFormat)
            + (latencyScore * WeightLatency);

        return new BenchmarkScore(
            PromptId: prompt.Id,
            ModelAlias: result.ModelAlias,
            OverallScore: Math.Round(overall, 4),
            LengthScore: Math.Round(lengthScore, 4),
            TagComplianceScore: Math.Round(tagScore, 4),
            FormatScore: Math.Round(formatScore, 4),
            LatencyScore: Math.Round(latencyScore, 4),
            Notes: string.Join("; ", notes));
    }

    // ── Length scoring ────────────────────────────────────────────────────────

    private static double ScoreLength(BenchmarkPrompt prompt, BenchmarkResult result, List<string> notes)
    {
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.OutputText))
        {
            notes.Add("Empty output");
            return 0.0;
        }

        var tokens = result.EstimatedOutputTokens;

        if (tokens >= prompt.ExpectedMinTokens && tokens <= prompt.ExpectedMaxTokens)
            return 1.0;

        if (tokens < prompt.ExpectedMinTokens)
        {
            // Linearly scale: 0 tokens = 0, min tokens = 1
            var ratio = prompt.ExpectedMinTokens > 0
                ? (double)tokens / prompt.ExpectedMinTokens
                : 0.0;
            notes.Add($"Output too short ({tokens} < {prompt.ExpectedMinTokens} min)");
            return Math.Max(0, ratio);
        }

        // tokens > ExpectedMaxTokens — penalize proportionally
        var overflowRatio = prompt.ExpectedMaxTokens > 0
            ? (double)prompt.ExpectedMaxTokens / tokens
            : 0.0;
        notes.Add($"Output too long ({tokens} > {prompt.ExpectedMaxTokens} max)");
        return Math.Max(0, overflowRatio);
    }

    // ── Tag compliance scoring ───────────────────────────────────────────────

    private static double ScoreTagCompliance(BenchmarkPrompt prompt, BenchmarkResult result, List<string> notes)
    {
        if (!result.Succeeded)
            return 0.0;

        var output = result.OutputText;
        var checks = 0;
        var passes = 0;

        if (prompt.MustContainTags is { Length: > 0 })
        {
            foreach (var tag in prompt.MustContainTags)
            {
                checks++;
                if (output.Contains(tag, StringComparison.OrdinalIgnoreCase))
                {
                    passes++;
                }
                else
                {
                    notes.Add($"Missing required tag: '{tag}'");
                }
            }
        }

        if (prompt.MustNotContainTags is { Length: > 0 })
        {
            foreach (var tag in prompt.MustNotContainTags)
            {
                checks++;
                if (!output.Contains(tag, StringComparison.OrdinalIgnoreCase))
                {
                    passes++;
                }
                else
                {
                    notes.Add($"Contains forbidden tag: '{tag}'");
                }
            }
        }

        // If no tag constraints, perfect score
        return checks == 0 ? 1.0 : (double)passes / checks;
    }

    // ── Format scoring ───────────────────────────────────────────────────────

    private static double ScoreFormat(BenchmarkPrompt prompt, BenchmarkResult result, List<string> notes)
    {
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.OutputText))
            return 0.0;

        var output = result.OutputText;
        var score = 1.0;

        // Skill-specific format checks
        switch (prompt.SkillKey)
        {
            case "summarize":
                // Summarize should be shorter than input
                if (output.Length >= result.InputText.Length)
                {
                    score -= 0.3;
                    notes.Add("Summary not shorter than input");
                }
                break;

            case "shorten":
                // Shortened text should be notably shorter
                if (output.Length >= result.InputText.Length * 0.9)
                {
                    score -= 0.4;
                    notes.Add("Shortened text not significantly shorter");
                }
                break;

            case "grammar":
                // Grammar output should preserve approximate meaning/length
                var lengthRatio = (double)output.Length / result.InputText.Length;
                if (lengthRatio < 0.5 || lengthRatio > 2.0)
                {
                    score -= 0.3;
                    notes.Add($"Grammar fix changed length significantly ({lengthRatio:F2}x)");
                }
                break;

            case "tone-professional":
                // Professional tone should avoid very casual language
                if (HasCasualLanguage().IsMatch(output))
                {
                    score -= 0.2;
                    notes.Add("Professional tone contains casual language");
                }
                break;

            case "tone-casual":
                // Casual tone should avoid overly formal patterns
                if (HasFormalLanguage().IsMatch(output))
                {
                    score -= 0.1;
                    notes.Add("Casual tone contains formal language");
                }
                break;

            case "freeform":
                // Check for numbered list when expected
                if (prompt.MustContainTags is { Length: > 0 } &&
                    prompt.MustContainTags.Any(t => int.TryParse(t, out _)))
                {
                    if (!HasNumberedListPattern().IsMatch(output))
                    {
                        score -= 0.2;
                        notes.Add("Expected numbered list format not detected");
                    }
                }
                break;
        }

        // General: check for obvious error messages in output
        if (output.Contains("error", StringComparison.OrdinalIgnoreCase) &&
            output.Contains("exception", StringComparison.OrdinalIgnoreCase))
        {
            score -= 0.5;
            notes.Add("Output appears to contain error text");
        }

        return Math.Max(0, score);
    }

    // ── Latency scoring ──────────────────────────────────────────────────────

    private static double ScoreLatency(BenchmarkResult result, List<string> notes)
    {
        if (!result.Succeeded || result.ElapsedSeconds <= 0)
            return 0.0;

        // Normalize: reference time gets 1.0, linear decay
        var score = Math.Min(1.0, ReferenceLatencySeconds / result.ElapsedSeconds);

        if (result.ElapsedSeconds > ReferenceLatencySeconds * 3)
            notes.Add($"Slow response ({result.ElapsedSeconds:F1}s)");

        return score;
    }

    // ── Regex patterns ───────────────────────────────────────────────────────

    [GeneratedRegex(@"\b(lol|gonna|wanna|gotta|yo|dude|hey)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HasCasualLanguage();

    [GeneratedRegex(@"\b(hereby|whereas|aforementioned|pursuant|hereinafter)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HasFormalLanguage();

    [GeneratedRegex(@"^\s*\d+[\.\)]\s", RegexOptions.Multiline)]
    private static partial Regex HasNumberedListPattern();
}
