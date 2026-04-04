using System.Diagnostics;
using SmrtPad.AI.Benchmarks.Evaluation;
using SmrtPad.AI.Benchmarks.Reporting;

namespace SmrtPad.AI.Benchmarks;

/// <summary>
/// Runs benchmark cases against a real or mocked <see cref="AIDispatcher"/>,
/// captures output via <see cref="InlineTagParser"/>, evaluates with rules + LLM grading,
/// and produces a <see cref="BenchmarkRun"/>.
/// If a single case throws, the error is captured and the run continues with the next case.
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
    /// Individual case failures are recorded with zero scores; the run always continues.
    /// When <paramref name="dashboardOutputDir"/> is set the live dashboard HTML is generated
    /// (and opened in the default browser) before the first case and updated after every case.
    /// </summary>
    public async Task<BenchmarkRun> RunAsync(
        IReadOnlyList<BenchmarkCase> cases,
        Action<string>? onProgress = null,
        string? dashboardOutputDir = null,
        Action<BenchmarkResult>? onResultAdded = null,
        CancellationToken ct = default)
    {
        var runId = $"bench-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        var startedAt = DateTimeOffset.UtcNow;
        var results = new List<BenchmarkResult>(cases.Count);

        // Live dashboard: create an initial empty dashboard and open it in the browser.
        string? dashboardPath = null;
        if (dashboardOutputDir is not null)
        {
            var emptyRun = new BenchmarkRun(runId, _modelAlias, _backendTarget, startedAt, results);
            dashboardPath = BenchmarkDashboardGenerator.Generate(emptyRun, cases.Count, dashboardOutputDir);
            try { Process.Start(new ProcessStartInfo(dashboardPath) { UseShellExecute = true }); } catch { }
        }

        for (int i = 0; i < cases.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var benchmarkCase = cases[i];
            onProgress?.Invoke($"[{i + 1}/{cases.Count}] {benchmarkCase.Description}");

            BenchmarkResult result;
            try
            {
                result = await RunSingleCaseAsync(benchmarkCase, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw; // cancellation should propagate
            }
            catch (Exception ex)
            {
                // Record failure but keep running the remaining cases.
                var failEval = new EvaluationScore(0, 0, 0, 0, null, $"Unhandled error: {ex.Message}");
                result = new BenchmarkResult(benchmarkCase, string.Empty, null, null,
                    0, failEval, _modelAlias, _backendTarget, DateTimeOffset.UtcNow, 0, 0, 0);
            }

            results.Add(result);
            onResultAdded?.Invoke(result);

            // Persist live dashboard after every case.
            if (dashboardOutputDir is not null)
            {
                var partialRun = new BenchmarkRun(runId, _modelAlias, _backendTarget, startedAt, results);
                BenchmarkDashboardGenerator.Generate(partialRun, cases.Count, dashboardOutputDir);
            }
        }

        var finalRun = new BenchmarkRun(runId, _modelAlias, _backendTarget, startedAt, results);

        // Write final reports (Markdown + JSON) alongside the dashboard.
        if (dashboardOutputDir is not null)
        {
            BenchmarkReportGenerator.WriteReports(finalRun, dashboardOutputDir);
            BenchmarkDashboardGenerator.Generate(finalRun, cases.Count, dashboardOutputDir);
        }

        return finalRun;
    }

    private async Task<BenchmarkResult> RunSingleCaseAsync(BenchmarkCase benchmarkCase, CancellationToken ct)
    {
        var parser = new InlineTagParser();
        var sw = Stopwatch.StartNew();
        var tcs = new TaskCompletionSource();
        Exception? streamError = null;
        long ttftMs = 0;
        bool firstTokenSeen = false;

        await _dispatcher.StreamResponseAsync(
            benchmarkCase.SkillKey,
            benchmarkCase.InputText,
            token =>
            {
                if (!firstTokenSeen) { ttftMs = sw.ElapsedMilliseconds; firstTokenSeen = true; }
                parser.Feed(token);
            },
            () => tcs.TrySetResult(),
            ex => { streamError = ex; tcs.TrySetResult(); },
            ct).ConfigureAwait(false);

        await tcs.Task.ConfigureAwait(false);
        sw.Stop();

        var rawOutput = parser.GetRawOutput();
        var insertContent = parser.GetInsertContent();
        var thinkContent = parser.GetThinkContent();

        int estInputTokens = EstimateTokens(benchmarkCase.InputText);
        int estOutputTokens = EstimateTokens(rawOutput);

        // Cost estimation
        double gpuWatts = double.TryParse(Environment.GetEnvironmentVariable("BENCHMARK_GPU_WATTS"), out var gw) ? gw : 115;
        double electricityRate = double.TryParse(Environment.GetEnvironmentVariable("BENCHMARK_ELECTRICITY_RATE"), out var er) ? er : 0.2015; // £0.2015/kWh (20.15p unit rate)

        double elapsedHours = sw.Elapsed.TotalHours;
        double electricityCost = (gpuWatts * elapsedHours / 1000.0) * electricityRate;

        if (streamError is not null)
        {
            var failEval = new EvaluationScore(0, 0, 0, 0, null, $"Stream error: {streamError.Message}");
            return new BenchmarkResult(benchmarkCase, rawOutput, insertContent, thinkContent,
                sw.ElapsedMilliseconds, failEval, _modelAlias, _backendTarget, DateTimeOffset.UtcNow,
                estInputTokens, estOutputTokens, electricityCost, ttftMs);
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
            sw.ElapsedMilliseconds, finalEval, _modelAlias, _backendTarget, DateTimeOffset.UtcNow,
            estInputTokens, estOutputTokens, electricityCost, ttftMs);
    }

    /// <summary>Estimates token count from text using word-count × 1.3 heuristic.</summary>
    internal static int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        int words = text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
        return (int)(words * 1.3);
    }
}
