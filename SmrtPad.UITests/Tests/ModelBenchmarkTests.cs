using System;
using System.IO;
using SmrtPad.UITests.Benchmark;
using SmrtPad.UITests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SmrtPad.UITests.Tests;

/// <summary>
/// AI model benchmark test suite. Runs all prompts against all selected models
/// (filtered by <c>BENCHMARK_MODEL_FILTER</c> env var), collects metrics, scores
/// results, estimates power costs, and generates reports (Markdown + HTML + JSON).
///
/// <para>Prerequisites:</para>
/// <list type="bullet">
///   <item>Run <c>Scripts/start-benchmark.ps1</c> to start WinAppDriver + Appium locally</item>
///   <item>SmrtPad must be deployed (AppX installed) on this machine</item>
///   <item>Set <c>BENCHMARK_MODEL_FILTER</c> to a comma-separated list of aliases to test a subset (optional)</item>
/// </list>
/// </summary>
[Collection("Benchmark")]
public sealed class ModelBenchmarkTests
{
    private readonly BenchmarkAppFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ModelBenchmarkTests(BenchmarkAppFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Runs the full AI model benchmark suite: all models × all prompts.
    /// Generates Markdown report, HTML dashboard, JSON data, and qualitative
    /// assessment prompt. Results are written to the <c>BenchmarkResults/</c>
    /// directory in the solution root.
    /// </summary>
    [SkippableFact]
    public void RunFullBenchmark()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.InitializationFailure ?? "Benchmark fixture not available");

        _fixture.RequireSession();

        var sidebar = new SidebarAutomationHelper(_fixture, msg => _output.WriteLine(msg));
        var scorer = new RuleBasedScorer();
        var costEstimator = new CostEstimator();

        _output.WriteLine($"Hardware profile: {costEstimator.GetHardwareProfile()}");

        var models = ModelBenchmarkRunner.GetModelsToRun();
        var prompts = BenchmarkPromptSet.GetAll();

        _output.WriteLine($"Models to benchmark: {string.Join(", ", models)}");
        _output.WriteLine($"Prompts: {prompts.Count}");
        _output.WriteLine($"Total runs: {models.Count * prompts.Count}");
        _output.WriteLine("");

        var runner = new ModelBenchmarkRunner(
            sidebar,
            scorer,
            costEstimator,
            log: msg => _output.WriteLine(msg));

        var report = runner.RunAll(prompts);

        // Determine output directory
        var outputDir = GetOutputDirectory();
        _output.WriteLine($"Output directory: {outputDir}");

        // Generate all report formats
        var paths = BenchmarkReportGenerator.GenerateAll(report, outputDir);
        _output.WriteLine($"Markdown report: {paths.MarkdownPath}");
        _output.WriteLine($"HTML dashboard:  {paths.HtmlPath}");
        _output.WriteLine($"JSON results:    {paths.JsonPath}");

        // Generate qualitative assessment prompt
        var assessmentPath = QualitativeAssessmentPrompt.Generate(report, outputDir);
        _output.WriteLine($"Assessment prompt: {assessmentPath}");

        // Summary assertions
        Assert.NotEmpty(report.Results);
        _output.WriteLine("");
        _output.WriteLine("═══════════════════════════════════════════");
        _output.WriteLine("           BENCHMARK COMPLETE");
        _output.WriteLine("═══════════════════════════════════════════");
        _output.WriteLine($"Models tested:     {report.ModelsRun.Count}");
        _output.WriteLine($"Total runs:        {report.Results.Count}");
        _output.WriteLine($"Successful:        {report.Results.Count(r => r.Succeeded)}");
        _output.WriteLine($"Failed:            {report.Results.Count(r => !r.Succeeded)}");
        _output.WriteLine($"Total time:        {report.TotalElapsed:hh\\:mm\\:ss}");
        _output.WriteLine($"Model errors:      {report.ModelErrors.Count}");
        _output.WriteLine("═══════════════════════════════════════════");
    }

    /// <summary>
    /// Quick smoke test: runs a single freeform prompt against the current model
    /// to verify the benchmark infrastructure works end-to-end.
    /// </summary>
    [SkippableFact]
    public void SmokeTest_SinglePrompt()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.InitializationFailure ?? "Benchmark fixture not available");

        _fixture.RequireSession();

        var sidebar = new SidebarAutomationHelper(_fixture, msg => _output.WriteLine(msg));
        var scorer = new RuleBasedScorer();
        var costEstimator = new CostEstimator();

        var prompt = new BenchmarkPrompt(
            "smoke-01", "freeform",
            "What is 2 + 2?",
            "Smoke test arithmetic question",
            ExpectedMinTokens: 1,
            ExpectedMaxTokens: 50);

        // Use whatever model is currently loaded
        var result = sidebar.ExecutePrompt(prompt, "current", "GPU");

        _output.WriteLine($"Succeeded: {result.Succeeded}");
        _output.WriteLine($"Output: '{result.OutputText}'");
        _output.WriteLine($"Error: '{result.ErrorMessage}'");
        _output.WriteLine($"Elapsed: {result.ElapsedSeconds:F1}s");
        _output.WriteLine($"TPS: {result.TokensPerSecond:F1}");

        if (result.Succeeded)
        {
            var score = scorer.Score(prompt, result);
            _output.WriteLine($"Score: {score.OverallScore:F3}");
            _output.WriteLine($"Notes: {score.Notes}");

            var cost = costEstimator.Estimate(result);
            _output.WriteLine($"Cost: ${cost.EstimatedCostUsd:F8}");
        }

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(string.IsNullOrWhiteSpace(result.OutputText));
    }

    /// <summary>
    /// Resolves the output directory for benchmark results.
    /// Uses the solution root's <c>BenchmarkResults/</c> directory.
    /// </summary>
    private static string GetOutputDirectory()
    {
        // Walk up from the test assembly to find the solution root
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "SmrtPad.sln")))
                return Path.Combine(dir, "BenchmarkResults");
            dir = Path.GetDirectoryName(dir);
        }

        // Fallback to a temp directory
        var fallback = Path.Combine(Path.GetTempPath(), "SmrtPad-BenchmarkResults");
        Directory.CreateDirectory(fallback);
        return fallback;
    }
}
