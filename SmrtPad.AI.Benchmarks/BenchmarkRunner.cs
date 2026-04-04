using System.Diagnostics;
using SmrtPad.AI.Benchmarks.Evaluation;

namespace SmrtPad.AI.Benchmarks;

/// <summary>
/// Runs benchmark cases against a real or mocked <see cref="AIDispatcher"/>,
/// captures output via <see cref="InlineTagParser"/>, evaluates with rules + LLM grading,
/// and produces a <see cref="BenchmarkRun"/>.
/// </summary>
public sealed class BenchmarkRunner
{
    private readonly AIDispatcher _dispatcher;
    private readonly string _modelAlias;
    private readonly string _backendTarget;
    private readonly bool _enableLlmGrading;

    /// <summary>
    /// Creates a runner using an externally-provided dispatcher (for testability).
    /// </summary>
    public BenchmarkRunner(AIDispatcher dispatcher, string modelAlias, string backendTarget, bool enableLlmGrading = true)
    {
        _dispatcher = dispatcher;
        _modelAlias = modelAlias;
        _backendTarget = backendTarget;
        _enableLlmGrading = enableLlmGrading;
    }

    /// <summary>
    /// Runs all provided benchmark cases sequentially and returns a complete <see cref="BenchmarkRun"/>.
    /// </summary>
    public async Task<BenchmarkRun> RunAsync(
        IReadOnlyList<BenchmarkCase> cases,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        var runId = $"bench-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        var startedAt = DateTimeOffset.UtcNow;
        var results = new List<BenchmarkResult>(cases.Count);

        for (int i = 0; i < cases.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var benchmarkCase = cases[i];
            onProgress?.Invoke($"[{i + 1}/{cases.Count}] {benchmarkCase.Description}");
            var result = await RunSingleCaseAsync(benchmarkCase, ct).ConfigureAwait(false);
            results.Add(result);
        }

        return new BenchmarkRun(runId, _modelAlias, _backendTarget, startedAt, results);
    }

    private async Task<BenchmarkResult> RunSingleCaseAsync(BenchmarkCase benchmarkCase, CancellationToken ct)
    {
        var parser = new InlineTagParser();
        var sw = Stopwatch.StartNew();
        var tcs = new TaskCompletionSource();
        Exception? streamError = null;

        await _dispatcher.StreamResponseAsync(
            benchmarkCase.SkillKey,
            benchmarkCase.InputText,
            token => parser.Feed(token),
            () => tcs.TrySetResult(),
            ex => { streamError = ex; tcs.TrySetResult(); },
            ct).ConfigureAwait(false);

        await tcs.Task.ConfigureAwait(false);
        sw.Stop();

        var rawOutput = parser.GetRawOutput();
        var insertContent = parser.GetInsertContent();
        var thinkContent = parser.GetThinkContent();

        if (streamError is not null)
        {
            // Record failure with zero scores
            var failEval = new EvaluationScore(0, 0, 0, 0, null, $"Stream error: {streamError.Message}");
            return new BenchmarkResult(benchmarkCase, rawOutput, insertContent, thinkContent,
                sw.ElapsedMilliseconds, failEval, _modelAlias, _backendTarget, DateTimeOffset.UtcNow);
        }

        // Rule-based evaluation
        var ruleEval = RuleBasedEvaluator.Evaluate(benchmarkCase, rawOutput, insertContent, thinkContent);

        // LLM quality grading (optional)
        int? llmScore = null;
        string? llmReason = null;
        if (_enableLlmGrading)
        {
            var responseText = insertContent ?? parser.GetAnswerText();
            if (!string.IsNullOrWhiteSpace(responseText))
            {
                try
                {
                    (llmScore, llmReason) = await LlmQualityGrader.GradeAsync(
                        benchmarkCase, responseText, _dispatcher, ct).ConfigureAwait(false);
                }
                catch
                {
                    // LLM grading is non-fatal
                }
            }
        }

        var finalEval = ruleEval with
        {
            LlmQualityScore = llmScore,
            LlmQualityReason = llmReason,
        };

        return new BenchmarkResult(benchmarkCase, rawOutput, insertContent, thinkContent,
            sw.ElapsedMilliseconds, finalEval, _modelAlias, _backendTarget, DateTimeOffset.UtcNow);
    }
}
