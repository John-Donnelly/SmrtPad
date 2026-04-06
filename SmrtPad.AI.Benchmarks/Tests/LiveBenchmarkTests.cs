using System.Diagnostics;
using System.Text.RegularExpressions;
using SmrtPad.AI.Benchmarks.Reporting;

namespace SmrtPad.AI.Benchmarks.Tests;

/// <summary>
/// Full end-to-end benchmark run that iterates ALL hardware-eligible models on both GPU and CPU
/// paths, accumulating results into a single combined live dashboard with CPU/GPU filter support.
/// Run with: dotnet test --filter "Category=LiveBenchmark"
/// GPU-only: dotnet test --filter "Category=GpuBenchmark"
/// </summary>
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
    [Trait("Category", "LiveBenchmark")]
    public async Task FullBenchmarkRun_AllModels_CpuAndGpu_WithLiveDashboard()
    {
        var outputDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "BenchmarkResults"));
        Directory.CreateDirectory(outputDir);

        // Probe hardware once to determine available budgets
        await _probeDispatcher.InitializeAsync(msg => Console.WriteLine($"[probe] {msg}"));

        var gpuAliases = _probeDispatcher.GetEligibleModelAliases();       // VRAM-eligible
        var cpuAliases = _probeDispatcher.GetEligibleCpuModelAliases();    // RAM-eligible

        // Filter to models ≤10B params (parse from alias, e.g. "deepseek-r1-7b" → 7)
        static double ParseBillionParams(string alias)
        {
            var m = Regex.Match(alias, @"(\d+(?:\.\d+)?)b", RegexOptions.IgnoreCase);
            if (m.Success) return double.Parse(m.Groups[1].Value);
            if (alias.StartsWith("phi-4-mini", StringComparison.OrdinalIgnoreCase)) return 3.8;
            if (alias.StartsWith("phi-3.5-mini", StringComparison.OrdinalIgnoreCase)) return 3.8;
            if (alias.StartsWith("phi-3-mini", StringComparison.OrdinalIgnoreCase)) return 3.8;
            if (alias.StartsWith("phi-4", StringComparison.OrdinalIgnoreCase)) return 14;
            if (alias.StartsWith("qwen3", StringComparison.OrdinalIgnoreCase)) return 0.6;
            return 0; // unknown — include by default
        }
        bool IsWithinParamLimit(string alias) => ParseBillionParams(alias) is 0 or <= 10;

        // Run GPU-eligible models on GPU AND CPU-eligible models on CPU (full cross-platform coverage).
        // Models that are eligible on both platforms are benchmarked on both.
        var targets = new List<(string Alias, string Target)>();
        foreach (var a in gpuAliases.Where(IsWithinParamLimit))  targets.Add((a, "FoundryLocalGpu"));
        foreach (var a in cpuAliases.Where(IsWithinParamLimit))  targets.Add((a, "FoundryLocalCpu"));

        // NPU proxy tier: run phi-3.5-mini on GPU to simulate what NPU would produce
        // (phi-silica / phi-3.5-mini is the NPU target model on qualifying hardware)
        foreach (var a in new[] { "phi-3.5-mini" })
        {
            if (gpuAliases.Any(g => g.Equals(a, StringComparison.OrdinalIgnoreCase)))
                targets.Add((a, "NpuProxy (GPU)"));
        }

        if (targets.Count == 0)
        {
            Assert.Fail("No eligible models found. Ensure Foundry Local has models downloaded.");
            return;
        }

        var cases = BenchmarkPromptCatalog.All;
        int currentGrandTotal = targets.Count * cases.Count;

        Console.WriteLine($"GPU-eligible models : {gpuAliases.Count}");
        Console.WriteLine($"CPU-eligible models : {cpuAliases.Count}");
        Console.WriteLine($"Total evaluations   : {targets.Count} models \u00d7 {cases.Count} cases = {currentGrandTotal}");

        // Shared combined-dashboard state
        var combinedRunId = $"bench-{DateTime.UtcNow:yyyyMMdd-HHmmss}-multimodel";
        var startedAt = DateTimeOffset.UtcNow;
        var allResults = new List<BenchmarkResult>(currentGrandTotal);
        string? dashPath = null;
        string currentStatus = string.Empty;
        var responseLogPath = Path.Combine(outputDir, combinedRunId + "-responses.jsonl");

        foreach (var (alias, target) in targets)
        {
            Console.WriteLine($"\n=== {alias} ({target}) ===");

            await using var dispatcher = new AIDispatcherFactory().Create();
            dispatcher.SetPreferredModelAlias(alias);
            // NpuProxy runs on GPU; all other targets map directly
            var execTarget = target == "NpuProxy (GPU)" ? "FoundryLocalGpu" : target;
            dispatcher.SetPreferredExecutionTarget(execTarget);

            try
            {
                await dispatcher.InitializeAsync(msg => Console.WriteLine($"  [init] {msg}"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  \u26a0\ufe0f  Skipped \u2014 init failed: {ex.Message}");
                currentGrandTotal -= cases.Count;
                continue;
            }

            var activeAlias  = dispatcher.ActiveModelAlias ?? alias;
            // Use the display target (NpuProxy label preserved) for dashboard tagging
            var activeTarget = target;

            // LLM grading disabled for multi-model run speed; re-enable for single-model deep analysis
            var runner = new BenchmarkRunner(dispatcher, activeAlias, activeTarget, enableLlmGrading: false);

            await runner.RunAsync(
                cases,
                onProgress: msg =>
                {
                    currentStatus = msg;
                    Console.WriteLine($"  {msg}");
                },
                onResultAdded: result =>
                {
                    allResults.Add(result);
                    var snap = new BenchmarkRun(combinedRunId, activeAlias, activeTarget, startedAt, allResults);
                    var path = BenchmarkDashboardGenerator.Generate(snap, currentGrandTotal, outputDir, currentStatus: currentStatus);
                    if (dashPath is null)
                    {
                        dashPath = path;
                        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { /* browser optional */ }
                    }
                },
                responseLogPath: responseLogPath);
        }

        // Write final Markdown + JSON reports from combined results
        var finalRun = new BenchmarkRun(combinedRunId, "multi-model", "CPU+GPU", startedAt, allResults);
        BenchmarkReportGenerator.WriteReports(finalRun, outputDir);
        BenchmarkDashboardGenerator.Generate(finalRun, currentGrandTotal, outputDir);

        int passed = finalRun.Results.Count(r => r.Evaluation.RuleScore >= BenchmarkReportGenerator.PassThreshold);
        double avg  = finalRun.Results.Count > 0 ? finalRun.Results.Average(r => r.Evaluation.RuleScore) : 0;

        Console.WriteLine();
        Console.WriteLine($"=== BENCHMARK COMPLETE ===");
        Console.WriteLine($"  Models tested : {targets.Count}");
        Console.WriteLine($"  Results       : {finalRun.Results.Count}/{currentGrandTotal}");
        Console.WriteLine($"  Passed        : {passed}");
        Console.WriteLine($"  Avg score     : {avg:F1}/100");
        Console.WriteLine($"  Reports       : {outputDir}");

        Assert.True(finalRun.Results.Count > 0, "Expected at least one result from the benchmark run.");
    }

    /// <summary>
    /// GPU-only benchmark: skips all CPU targets so slow CPU inference cannot block the run.
    /// Each case is subject to a 5-minute per-case timeout as a safety net.
    /// Run with: dotnet test --filter "Category=GpuBenchmark"
    /// </summary>
    [Fact(Timeout = 21_600_000)] // 6-hour ceiling
    [Trait("Category", "GpuBenchmark")]
    public async Task GpuOnlyBenchmarkRun_WithLiveDashboard()
    {
        var outputDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "BenchmarkResults"));
        Directory.CreateDirectory(outputDir);

        await _probeDispatcher.InitializeAsync(msg => Console.WriteLine($"[probe] {msg}"));

        var gpuAliases = _probeDispatcher.GetEligibleModelAliases();

        static double ParseBillionParams(string alias)
        {
            var m = Regex.Match(alias, @"(\d+(?:\.\d+)?)b", RegexOptions.IgnoreCase);
            if (m.Success) return double.Parse(m.Groups[1].Value);
            if (alias.StartsWith("phi-4-mini", StringComparison.OrdinalIgnoreCase)) return 3.8;
            if (alias.StartsWith("phi-3.5-mini", StringComparison.OrdinalIgnoreCase)) return 3.8;
            if (alias.StartsWith("phi-3-mini", StringComparison.OrdinalIgnoreCase)) return 3.8;
            if (alias.StartsWith("phi-4", StringComparison.OrdinalIgnoreCase)) return 14;
            if (alias.StartsWith("qwen3", StringComparison.OrdinalIgnoreCase)) return 0.6;
            return 0;
        }
        bool IsWithinParamLimit(string alias) => ParseBillionParams(alias) is 0 or <= 10;

        var targets = new List<(string Alias, string Target)>();
        foreach (var a in gpuAliases.Where(IsWithinParamLimit))
            targets.Add((a, "FoundryLocalGpu"));

        // NPU proxy tier
        foreach (var a in new[] { "phi-3.5-mini" })
        {
            if (gpuAliases.Any(g => g.Equals(a, StringComparison.OrdinalIgnoreCase)))
                targets.Add((a, "NpuProxy (GPU)"));
        }

        if (targets.Count == 0)
        {
            Assert.Fail("No GPU-eligible models found. Ensure Foundry Local has models downloaded.");
            return;
        }

        var cases = BenchmarkPromptCatalog.All;
        int totalEvals = targets.Count * cases.Count;

        Console.WriteLine($"GPU-eligible models : {gpuAliases.Count}");
        Console.WriteLine($"Total evaluations   : {targets.Count} models × {cases.Count} cases = {totalEvals}");

        var combinedRunId = $"bench-{DateTime.UtcNow:yyyyMMdd-HHmmss}-gpu-only";
        var startedAt = DateTimeOffset.UtcNow;
        var allResults = new List<BenchmarkResult>(totalEvals);
        string? dashPath = null;
        string currentStatus = string.Empty;
        var responseLogPath = Path.Combine(outputDir, combinedRunId + "-responses.jsonl");

        foreach (var (alias, target) in targets)
        {
            Console.WriteLine($"\n=== {alias} ({target}) ===");

            await using var dispatcher = new AIDispatcherFactory().Create();
            dispatcher.SetPreferredModelAlias(alias);
            var execTarget = target == "NpuProxy (GPU)" ? "FoundryLocalGpu" : target;
            dispatcher.SetPreferredExecutionTarget(execTarget);

            try
            {
                await dispatcher.InitializeAsync(msg => Console.WriteLine($"  [init] {msg}"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️  Skipped — init failed: {ex.Message}");
                totalEvals -= cases.Count;
                continue;
            }

            var activeAlias  = dispatcher.ActiveModelAlias ?? alias;
            var activeTarget = target;

            var runner = new BenchmarkRunner(dispatcher, activeAlias, activeTarget, enableLlmGrading: false);

            await runner.RunAsync(
                cases,
                onProgress: msg =>
                {
                    currentStatus = msg;
                    Console.WriteLine($"  {msg}");
                },
                onResultAdded: result =>
                {
                    allResults.Add(result);
                    var snap = new BenchmarkRun(combinedRunId, activeAlias, activeTarget, startedAt, allResults);
                    var path = BenchmarkDashboardGenerator.Generate(snap, totalEvals, outputDir, currentStatus: currentStatus);
                    if (dashPath is null)
                    {
                        dashPath = path;
                        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { /* browser optional */ }
                    }
                },
                responseLogPath: responseLogPath,
                perCaseTimeout: TimeSpan.FromSeconds(300));
        }

        var finalRun = new BenchmarkRun(combinedRunId, "multi-model", "GPU", startedAt, allResults);
        BenchmarkReportGenerator.WriteReports(finalRun, outputDir);
        BenchmarkDashboardGenerator.Generate(finalRun, totalEvals, outputDir);

        int passed = finalRun.Results.Count(r => r.Evaluation.RuleScore >= BenchmarkReportGenerator.PassThreshold);
        double avg  = finalRun.Results.Count > 0 ? finalRun.Results.Average(r => r.Evaluation.RuleScore) : 0;

        Console.WriteLine();
        Console.WriteLine($"=== GPU BENCHMARK COMPLETE ===");
        Console.WriteLine($"  Models tested : {targets.Count}");
        Console.WriteLine($"  Results       : {finalRun.Results.Count}/{totalEvals}");
        Console.WriteLine($"  Passed        : {passed}");
        Console.WriteLine($"  Avg score     : {avg:F1}/100");
        Console.WriteLine($"  Reports       : {outputDir}");

        Assert.True(finalRun.Results.Count > 0, "Expected at least one result from the GPU benchmark run.");
    }
}

