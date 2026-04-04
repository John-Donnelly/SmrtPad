using System.Diagnostics;
using System.Text.RegularExpressions;
using SmrtPad.AI.Benchmarks.Reporting;

namespace SmrtPad.AI.Benchmarks.Tests;

/// <summary>
/// Full end-to-end benchmark run that iterates ALL hardware-eligible models on both GPU and CPU
/// paths, accumulating results into a single combined live dashboard with CPU/GPU filter support.
/// Run with: dotnet test --filter "Category=LiveBenchmark"
/// </summary>
[Trait("Category", "LiveBenchmark")]
[Collection("LiveBenchmarks")]
public sealed class LiveBenchmarkTests : IAsyncDisposable
{
    private readonly AIDispatcher _probeDispatcher;

    public LiveBenchmarkTests()
    {
        _probeDispatcher = new AIDispatcherFactory().Create();
    }

    public async ValueTask DisposeAsync()
    {
        await _probeDispatcher.DisposeAsync();
    }

    [Fact(Timeout = 43_200_000)] // 12-hour ceiling for multi-model run
    public async Task FullBenchmarkRun_AllModels_CpuAndGpu_WithLiveDashboard()
    {
        var outputDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "BenchmarkResults"));
        Directory.CreateDirectory(outputDir);

        // Probe hardware once to determine available budgets
        await _probeDispatcher.InitializeAsync(msg => Console.WriteLine($"[probe] {msg}"));

        var gpuAliases = _probeDispatcher.GetEligibleModelAliases();       // VRAM-eligible
        var cpuAliases = _probeDispatcher.GetEligibleCpuModelAliases();    // RAM-eligible

        // Run GPU-eligible models on GPU; CPU-exclusive models (fit RAM but not VRAM) on CPU
        var gpuSet = new HashSet<string>(gpuAliases, StringComparer.OrdinalIgnoreCase);
        var cpuExclusive = cpuAliases.Where(a => !gpuSet.Contains(a)).ToList();

        // Filter to models ≤10B params (parse from alias, e.g. "deepseek-r1-7b" → 7)
        static double ParseBillionParams(string alias)
        {
            // Match patterns like "-7b", "-1.5b", "-14b", "-0.5b"
            var m = Regex.Match(alias, @"(\d+(?:\.\d+)?)b", RegexOptions.IgnoreCase);
            if (m.Success) return double.Parse(m.Groups[1].Value);
            // Known models without explicit param count in alias
            if (alias.StartsWith("phi-4-mini", StringComparison.OrdinalIgnoreCase)) return 3.8;
            if (alias.StartsWith("phi-3.5-mini", StringComparison.OrdinalIgnoreCase)) return 3.8;
            if (alias.StartsWith("phi-3-mini", StringComparison.OrdinalIgnoreCase)) return 3.8;
            if (alias.StartsWith("phi-4", StringComparison.OrdinalIgnoreCase)) return 14;
            return 0; // unknown — include by default
        }

        bool IsWithinParamLimit(string alias) => ParseBillionParams(alias) is 0 or <= 10;

        var targets = new List<(string Alias, string Target)>();
        foreach (var a in gpuAliases.Where(IsWithinParamLimit))   targets.Add((a, "FoundryLocalGpu"));
        foreach (var a in cpuExclusive.Where(IsWithinParamLimit)) targets.Add((a, "FoundryLocalCpu"));

        if (targets.Count == 0)
        {
            Assert.Fail("No eligible models found. Ensure Foundry Local has models downloaded.");
            return;
        }

        var cases = BenchmarkPromptCatalog.All;
        int grandTotal = targets.Count * cases.Count;

        Console.WriteLine($"GPU-eligible models : {gpuAliases.Count}");
        Console.WriteLine($"CPU-exclusive models: {cpuExclusive.Count}");
        Console.WriteLine($"Total evaluations   : {targets.Count} models \u00d7 {cases.Count} cases = {grandTotal}");

        // Shared combined-dashboard state
        var combinedRunId = $"bench-{DateTime.UtcNow:yyyyMMdd-HHmmss}-multimodel";
        var startedAt = DateTimeOffset.UtcNow;
        var allResults = new List<BenchmarkResult>(grandTotal);
        string? dashPath = null;

        foreach (var (alias, target) in targets)
        {
            Console.WriteLine($"\n=== {alias} ({target}) ===");

            await using var dispatcher = new AIDispatcherFactory().Create();
            dispatcher.SetPreferredModelAlias(alias);
            dispatcher.SetPreferredExecutionTarget(target);

            try
            {
                await dispatcher.InitializeAsync(msg => Console.WriteLine($"  [init] {msg}"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  \u26a0\ufe0f  Skipped \u2014 init failed: {ex.Message}");
                continue;
            }

            var activeAlias  = dispatcher.ActiveModelAlias ?? alias;
            var activeTarget = dispatcher.ExecutionTarget.ToString();

            // LLM grading disabled for multi-model run speed; re-enable for single-model deep analysis
            var runner = new BenchmarkRunner(dispatcher, activeAlias, activeTarget, enableLlmGrading: false);

            await runner.RunAsync(
                cases,
                onProgress: msg => Console.WriteLine($"  {msg}"),
                onResultAdded: result =>
                {
                    allResults.Add(result);
                    var snap = new BenchmarkRun(combinedRunId, activeAlias, activeTarget, startedAt, allResults);
                    var path = BenchmarkDashboardGenerator.Generate(snap, grandTotal, outputDir);
                    if (dashPath is null)
                    {
                        dashPath = path;
                        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { /* browser optional */ }
                    }
                });
        }

        // Write final Markdown + JSON reports from combined results
        var finalRun = new BenchmarkRun(combinedRunId, "multi-model", "CPU+GPU", startedAt, allResults);
        BenchmarkReportGenerator.WriteReports(finalRun, outputDir);
        BenchmarkDashboardGenerator.Generate(finalRun, grandTotal, outputDir);

        int passed = finalRun.Results.Count(r => r.Evaluation.RuleScore >= BenchmarkReportGenerator.PassThreshold);
        double avg  = finalRun.Results.Count > 0 ? finalRun.Results.Average(r => r.Evaluation.RuleScore) : 0;

        Console.WriteLine();
        Console.WriteLine($"=== BENCHMARK COMPLETE ===");
        Console.WriteLine($"  Models tested : {targets.Count}");
        Console.WriteLine($"  Results       : {finalRun.Results.Count}/{grandTotal}");
        Console.WriteLine($"  Passed        : {passed}");
        Console.WriteLine($"  Avg score     : {avg:F1}/100");
        Console.WriteLine($"  Reports       : {outputDir}");

        Assert.True(finalRun.Results.Count > 0, "Expected at least one result from the benchmark run.");
    }
}

