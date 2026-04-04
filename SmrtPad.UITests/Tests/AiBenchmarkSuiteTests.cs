using System;
using System.IO;
using System.Linq;
using SmrtPad.UITests.Benchmark;
using SmrtPad.UITests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SmrtPad.UITests.Tests;

/// <summary>
/// Appium-driven AI benchmark suite that exercises every installed model against
/// all 38 catalog prompts (document composition, edit skills, tag compliance).
/// Records standard metrics plus insert-tag compliance and keyword coverage.
///
/// <para>Prerequisites:</para>
/// <list type="bullet">
///   <item>Run <c>Scripts/start-benchmark.ps1</c> to start WinAppDriver + Appium locally</item>
///   <item>SmrtPad must be deployed (AppX installed) on this machine</item>
///   <item>Set <c>BENCHMARK_MODEL_FILTER</c> to a comma-separated list of aliases to run a subset (optional)</item>
///   <item>Set <c>BENCHMARK_PROMPT_LIMIT</c> to an integer to cap prompts per model (optional)</item>
/// </list>
/// </summary>
[Collection("Benchmark")]
public sealed class AiBenchmarkSuiteTests
{
    private readonly BenchmarkAppFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AiBenchmarkSuiteTests(BenchmarkAppFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Runs the full AI benchmark suite: all models × all 38 catalog prompts.
    /// Validates insert-tag compliance and keyword coverage for each interaction.
    /// Generates Markdown and JSON reports in <c>BenchmarkResults/</c>.
    /// </summary>
    [SkippableFact]
    public void RunAiBenchmarkSuite()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.InitializationFailure ?? "Benchmark fixture not available");

        _fixture.RequireSession();

        var models = ModelBenchmarkRunner.GetModelsToRun();
        var promptLimit = ModelBenchmarkRunner.GetPromptLimit();
        var allPrompts = AiBenchmarkCatalog.GetAll();
        var prompts = promptLimit.HasValue
            ? (System.Collections.Generic.IReadOnlyList<AiBenchmarkPrompt>)allPrompts.Take(promptLimit.Value).ToList()
            : allPrompts;

        _output.WriteLine($"Models: {string.Join(", ", models)}");
        _output.WriteLine($"Prompts: {prompts.Count} (catalog: {allPrompts.Count})");
        _output.WriteLine($"Total interactions: {models.Count * prompts.Count}");
        _output.WriteLine(string.Empty);

        var runner = new AiModelBenchmarkRunner(_fixture, msg => _output.WriteLine(msg));
        var report = runner.RunAll(prompts);

        // Surface summary to test output
        _output.WriteLine(string.Empty);
        _output.WriteLine("=== AI Benchmark Results ===");
        foreach (var model in report.ModelSummaries)
        {
            _output.WriteLine(
                $"  {model.ModelAlias} [{model.ExecutionTarget}]: " +
                $"{model.Succeeded}/{model.TotalPrompts} succeeded | " +
                $"insert compliance {model.InsertComplianceRate:P0} ({model.InsertCompliantCount}/{model.TotalPrompts}) | " +
                $"keywords {model.AvgKeywordScore:P0} | " +
                $"avg score {model.AvgBaseScore:F3} | " +
                $"avg {model.AvgLatencySeconds:F1}s @ {model.AvgTokensPerSecond:F1} tps");
        }

        // Assert overall success rate is non-zero for at least one model
        // (guards against silent total failure without failing on slow models)
        var anySucceeded = report.ModelSummaries.Any(m => m.Succeeded > 0);
        Assert.True(anySucceeded, "No model produced any successful response. Check Appium and app state.");
    }
}
