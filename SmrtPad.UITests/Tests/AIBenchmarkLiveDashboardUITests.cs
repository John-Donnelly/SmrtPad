using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SmrtPad.AI.Benchmarks;
using SmrtPad.AI.Benchmarks.Evaluation;
using SmrtPad.AI.Benchmarks.Reporting;
using SmrtPad.UITests.Benchmark;
using SmrtPad.UITests.Infrastructure;
using Xunit;
using Xunit.Abstractions;
using AIBenchmarkResult = SmrtPad.AI.Benchmarks.BenchmarkResult;
using UIBenchmarkResult = SmrtPad.UITests.Benchmark.BenchmarkResult;

namespace SmrtPad.UITests.Tests;

/// <summary>
/// Appium-driven benchmark suite that runs all 73 <see cref="BenchmarkPromptCatalog"/> cases
/// against the currently-active model in SmrtPad, evaluates each response with
/// <see cref="RuleBasedEvaluator"/>, and updates the live dashboard HTML after every case.
///
/// <para>Every response is streamed to the test output (CLI-visible), so you can watch
/// results in real-time via <c>dotnet test --logger console</c> or the VS Test Explorer output pane.</para>
///
/// <para>Prerequisites:</para>
/// <list type="bullet">
///   <item>Appium server running at 127.0.0.1:4723 (WinAppDriver 1.2.1)</item>
///   <item>SmrtPad installed as AppX package on this machine</item>
///   <item>Set <c>BENCHMARK_MODEL_FILTER</c> to switch to a specific model alias (optional)</item>
/// </list>
/// </summary>
[Collection("Benchmark")]
public sealed class AIBenchmarkLiveDashboardUITests
{
    private readonly BenchmarkAppFixture _fixture;
    private readonly ITestOutputHelper _output;

    // Output directory mirrors what LiveBenchmarkTests uses
    private static readonly string OutputDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "BenchmarkResults");

    public AIBenchmarkLiveDashboardUITests(BenchmarkAppFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Runs all 73 benchmark cases against SmrtPad's current active model via Appium.
    /// Streams every response to the test output and updates the live dashboard after each case.
    /// </summary>
    [SkippableFact]
    [Trait("Category", "UIBenchmark")]
    public void RunAllCasesWithLiveDashboard()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.InitializationFailure ?? "Benchmark fixture not available");
        _fixture.RequireSession();

        var modelAlias = Environment.GetEnvironmentVariable("BENCHMARK_MODEL_FILTER")
                         ?? "active-model";
        var executionTarget = Environment.GetEnvironmentVariable("BENCHMARK_EXECUTION_TARGET")
                              ?? "GPU";

        var cases = BenchmarkPromptCatalog.All;
        var promptLimit = int.TryParse(Environment.GetEnvironmentVariable("BENCHMARK_PROMPT_LIMIT"), out var lim)
            ? (int?)lim : null;
        var caseList = (promptLimit.HasValue ? cases.Take(promptLimit.Value) : cases).ToList();

        Directory.CreateDirectory(OutputDir);

        var runStart = DateTimeOffset.UtcNow;
        var allResults = new List<AIBenchmarkResult>();
        var sidebar = new SidebarAutomationHelper(_fixture, msg => _output.WriteLine(msg));

        // Emit the initial empty-shell dashboard so the browser tab can be opened early
        var runId = $"ui-{runStart:yyyyMMdd-HHmmss}";
        BenchmarkDashboardGenerator.Generate(
            new BenchmarkRun(runId, modelAlias, executionTarget, runStart, allResults),
            caseList.Count, OutputDir);

        // Helper to rebuild snapshot and regenerate dashboard
        void UpdateDashboard()
        {
            BenchmarkDashboardGenerator.Generate(
                new BenchmarkRun(runId, modelAlias, executionTarget, runStart, allResults),
                caseList.Count, OutputDir);
        }

        _output.WriteLine($"╔══════════════════════════════════════════════════╗");
        _output.WriteLine($"  AI Benchmark Live Dashboard — UITests              ");
        _output.WriteLine($"  Model: {modelAlias} | Backend: {executionTarget}   ");
        _output.WriteLine($"  Cases: {caseList.Count}                             ");
        _output.WriteLine($"  Output: {OutputDir}                                 ");
        _output.WriteLine($"╚══════════════════════════════════════════════════╝");
        _output.WriteLine(string.Empty);

        int caseNum = 0;
        foreach (var benchCase in caseList)
        {
            caseNum++;
            _output.WriteLine($"[{caseNum}/{caseList.Count}] {benchCase.Id} ({benchCase.SkillKey})");
            _output.WriteLine($"  Input: {Truncate(benchCase.InputText, 120)}");

            // Map BenchmarkCase → BenchmarkPrompt for the sidebar helper
            var prompt = new BenchmarkPrompt(
                Id: benchCase.Id,
                SkillKey: benchCase.SkillKey,
                InputText: benchCase.InputText,
                Description: benchCase.Description);

            var sw = Stopwatch.StartNew();
            UIBenchmarkResult uiResult;
            try
            {
                uiResult = sidebar.ExecutePrompt(prompt, modelAlias, executionTarget);
            }
            catch (Exception ex)
            {
                _output.WriteLine($"  ERROR: {ex.Message}");
                // Record failure and continue
                var failEval = new EvaluationScore(0, 0, 0, 0, null, $"Appium error: {ex.Message}");
                allResults.Add(new AIBenchmarkResult(
                    Case: benchCase,
                    RawOutput: string.Empty,
                    InsertContent: null,
                    ThinkContent: null,
                    LatencyMs: sw.ElapsedMilliseconds,
                    Evaluation: failEval,
                    ModelAlias: modelAlias,
                    BackendTarget: executionTarget,
                    RunTimestamp: DateTimeOffset.UtcNow));
                UpdateDashboard();
                continue;
            }
            sw.Stop();

            // Stream the captured response to test output
            var rawOutput = uiResult.OutputText ?? string.Empty;
            _output.WriteLine($"  Response ({rawOutput.Length} chars, {sw.ElapsedMilliseconds}ms):");
            _output.WriteLine($"  ┌─ {Truncate(rawOutput, 300)}");
            _output.WriteLine(string.Empty);

            // Extract inline tags from the UI response using InlineTagParser
            var parser = new InlineTagParser();
            parser.Feed(rawOutput);
            var insertContent = parser.GetInsertContent();
            var thinkContent = parser.GetThinkContent();

            if (insertContent is not null)
                _output.WriteLine($"  <insert> content: {Truncate(insertContent, 150)}");
            if (thinkContent is not null)
                _output.WriteLine($"  <think>  content: {Truncate(thinkContent, 100)}");

            // Evaluate with the same rule-based evaluator used by AI.Benchmarks
            var eval = RuleBasedEvaluator.Evaluate(benchCase, rawOutput, insertContent, thinkContent);

            // Cost: use the same heuristics as BenchmarkRunner
            int estIn = BenchmarkRunner.EstimateTokens(benchCase.InputText);
            int estOut = BenchmarkRunner.EstimateTokens(rawOutput);
            double elapsedHours = sw.Elapsed.TotalHours;
            double gpuWatts = double.TryParse(
                Environment.GetEnvironmentVariable("BENCHMARK_GPU_WATTS"), out var gw) ? gw : 115;
            double elecRate = double.TryParse(
                Environment.GetEnvironmentVariable("BENCHMARK_ELECTRICITY_RATE"), out var er) ? er : 0.2015;
            double tokenRate = double.TryParse(
                Environment.GetEnvironmentVariable("BENCHMARK_TOKEN_RATE_PER_1K"), out var tr) ? tr : 0.01;
            double elecCost = (gpuWatts * elapsedHours / 1000.0) * elecRate;
            double tokenCost = ((estIn + estOut) / 1000.0) * tokenRate;

            var result = new AIBenchmarkResult(
                Case: benchCase,
                RawOutput: rawOutput,
                InsertContent: insertContent,
                ThinkContent: thinkContent,
                LatencyMs: sw.ElapsedMilliseconds,
                Evaluation: eval,
                ModelAlias: modelAlias,
                BackendTarget: executionTarget,
                RunTimestamp: DateTimeOffset.UtcNow,
                EstimatedInputTokens: estIn,
                EstimatedOutputTokens: estOut,
                TokenCostUsd: tokenCost,
                ElectricityCostUsd: elecCost);

            allResults.Add(result);

            var icon = eval.RuleScore >= 80 ? "✅" : eval.RuleScore >= 60 ? "⚠️" : "❌";
            _output.WriteLine($"  {icon} Score: {eval.RuleScore}/100  " +
                $"(tag={eval.TagCompliancePts} preamble={eval.NoPreamblePts} " +
                $"closing={eval.NoClosingRemarksPts} content={eval.ContentCompletenessPts})");
            _output.WriteLine($"  Tok/s: {result.TokensPerSecond:F1}  " +
                $"Cost: £{result.TotalCostUsd:F6}  Tokens: {result.TotalTokens}");
            _output.WriteLine(string.Empty);

            // Update the live dashboard after each case
            UpdateDashboard();
        }

        // Final summary
        int totalPassed = allResults.Count(r => r.Evaluation.RuleScore >= 80);
        double avgScore = allResults.Count > 0
            ? allResults.Average(r => r.Evaluation.RuleScore) : 0;

        _output.WriteLine("══════════════════════════════════════════════════");
        _output.WriteLine($"FINAL RESULTS: {totalPassed}/{allResults.Count} passed (≥80) | " +
            $"Avg score: {avgScore:F1}/100");
        _output.WriteLine($"Dashboard: {OutputDir}");
        _output.WriteLine("══════════════════════════════════════════════════");

        // Non-fatal assertion: warn if average score is below 50
        Assert.True(avgScore >= 50 || allResults.Count == 0,
            $"Average score {avgScore:F1} is below 50 across {allResults.Count} cases. " +
            "Check model, prompts, and sidebar connectivity.");
    }

    private static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..maxLen] + "…";
}
