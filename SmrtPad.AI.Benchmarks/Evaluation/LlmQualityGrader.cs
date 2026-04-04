using System.Text.RegularExpressions;

namespace SmrtPad.AI.Benchmarks.Evaluation;

/// <summary>
/// Uses a second LLM call via the "grade" skill key to score a benchmark response 0–10.
/// Parses the <c>&lt;grade&gt;{json}&lt;/grade&gt;</c> response format.
/// </summary>
internal static partial class LlmQualityGrader
{
    [GeneratedRegex(@"<grade>\s*(\{.*?\})\s*</grade>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex GradeTagRegex();

    /// <summary>
    /// Grades a benchmark result using the AI dispatcher's "grade" skill.
    /// Returns (null, null) if the model fails to return a parseable grade.
    /// </summary>
    public static async Task<(int? Score, string? Reason)> GradeAsync(
        BenchmarkCase benchmarkCase,
        string responseText,
        AIDispatcher dispatcher,
        CancellationToken ct)
    {
        var gradePrompt = PromptTemplates.GradeResponse(benchmarkCase.InputText, responseText);
        var responseBuilder = new StringBuilder();
        var tcs = new TaskCompletionSource();

        await dispatcher.StreamResponseAsync(
            "grade",
            gradePrompt,
            token => responseBuilder.Append(token),
            () => tcs.TrySetResult(),
            ex => tcs.TrySetException(ex),
            ct).ConfigureAwait(false);

        // Wait for the complete callback
        try { await tcs.Task.ConfigureAwait(false); }
        catch { /* non-fatal */ }

        var fullResponse = responseBuilder.ToString();
        var match = GradeTagRegex().Match(fullResponse);
        if (!match.Success)
            return (null, null);

        try
        {
            using var doc = JsonDocument.Parse(match.Groups[1].Value);
            var root = doc.RootElement;
            int? score = root.TryGetProperty("score", out var scoreProp) ? scoreProp.GetInt32() : null;
            string? reason = root.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() : null;
            return (score, reason);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }
}
