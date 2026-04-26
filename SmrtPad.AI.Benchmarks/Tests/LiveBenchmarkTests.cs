using System.Diagnostics;
using System.Text.RegularExpressions;
using SmrtPad.AI.Benchmarks.Reporting;

namespace SmrtPad.AI.Benchmarks.Tests;

/// <summary>
/// Full end-to-end benchmark run that iterates ALL GGUF catalog models on both GPU and CPU
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

    /// <summary>Enumerate GGUF aliases available on disk together with their local path and target.</summary>
    private static IReadOnlyList<(string Alias, string Target)> GetGgufTargets(bool gpuOnly = false)
    {
        var targets = new List<(string Alias, string Target)>();
        foreach (var alias in GgufModelCatalog.AllAliases)
        {
            var localPath = GgufModelCatalog.GetLocalGgufPath(alias);
            if (!File.Exists(localPath))
                continue;

            targets.Add((alias, "GgufGpu"));
            if (!gpuOnly)
                targets.Add((alias, "GgufCpu"));
        }
        return targets;
    }

    [Fact(Timeout = 43_200_000)] // 12-hour ceiling for multi-model run
    [Trait("Category", "LiveBenchmark")]
    public async Task FullBenchmarkRun_AllModels_CpuAndGpu_WithLiveDashboard()
    {
        var outputDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "BenchmarkResults"));
        Directory.CreateDirectory(outputDir);

        await _probeDispatcher.InitializeAsync(msg => Console.WriteLine($"[probe] {msg}"));

        var targets = GetGgufTargets(gpuOnly: false);

        if (targets.Count == 0)
        {
            Assert.Fail("No eligible GGUF models found on disk. Ensure model files are downloaded.");
            return;
        }

        var cases = BenchmarkPromptCatalog.All;
        int currentGrandTotal = targets.Count * cases.Count;

        Console.WriteLine($"GGUF models on disk : {targets.Count / 2}");
        Console.WriteLine($"Total evaluations   : {targets.Count} targets \u00d7 {cases.Count} cases = {currentGrandTotal}");

        var combinedRunId = $"bench-{DateTime.UtcNow:yyyyMMdd-HHmmss}-multimodel";
        var startedAt = DateTimeOffset.UtcNow;
        var allResults = new List<BenchmarkResult>(currentGrandTotal);
        string? dashPath = null;
        string currentStatus = string.Empty;
        var responseLogPath = Path.Combine(outputDir, combinedRunId + "-responses.jsonl");

        foreach (var (alias, target) in targets)
        {
            Console.WriteLine($"\n=== {alias} ({target}) ===");

            var localPath = GgufModelCatalog.GetLocalGgufPath(alias);
            bool forceCpu = target == "GgufCpu";

            await using var dispatcher = AIDispatcherFactory.CreateFromLocalPath(
                localPath,
                maxContextTokens: GgufModelCatalog.Gemma4E2BContextTokens,
                forceCpuForGguf: forceCpu);

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

            var runner = new BenchmarkRunner(dispatcher, alias, target, enableLlmGrading: false);

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
                    var snap = new BenchmarkRun(combinedRunId, alias, target, startedAt, allResults);
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

        var targets = GetGgufTargets(gpuOnly: true);

        if (targets.Count == 0)
        {
            Assert.Fail("No GGUF models found on disk. Ensure model files are downloaded.");
            return;
        }

        var cases = BenchmarkPromptCatalog.All;
        int totalEvals = targets.Count * cases.Count;

        Console.WriteLine($"GGUF models on disk : {targets.Count}");
        Console.WriteLine($"Total evaluations   : {targets.Count} models \u00d7 {cases.Count} cases = {totalEvals}");

        var combinedRunId = $"bench-{DateTime.UtcNow:yyyyMMdd-HHmmss}-gpu-only";
        var startedAt = DateTimeOffset.UtcNow;
        var allResults = new List<BenchmarkResult>(totalEvals);
        string? dashPath = null;
        string currentStatus = string.Empty;
        var responseLogPath = Path.Combine(outputDir, combinedRunId + "-responses.jsonl");

        foreach (var (alias, target) in targets)
        {
            Console.WriteLine($"\n=== {alias} ({target}) ===");

            var localPath = GgufModelCatalog.GetLocalGgufPath(alias);

            await using var dispatcher = AIDispatcherFactory.CreateFromLocalPath(
                localPath,
                maxContextTokens: GgufModelCatalog.Gemma4E2BContextTokens,
                forceCpuForGguf: false);

            try
            {
                await dispatcher.InitializeAsync(msg => Console.WriteLine($"  [init] {msg}"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  \u26a0\ufe0f  Skipped \u2014 init failed: {ex.Message}");
                totalEvals -= cases.Count;
                continue;
            }

            var runner = new BenchmarkRunner(dispatcher, alias, target, enableLlmGrading: false);

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
                    var snap = new BenchmarkRun(combinedRunId, alias, target, startedAt, allResults);
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

    /// <summary>
    /// Targeted benchmark: runs gemma-4-e2b on both GPU and CPU paths.
    /// Run with: dotnet test --filter "Category=TargetedBenchmark"
    /// </summary>
    [Fact(Timeout = 7_200_000)] // 2-hour ceiling
    [Trait("Category", "TargetedBenchmark")]
    public async Task TargetedBenchmarkRun_Gemma4E2B_WithLiveDashboard()
    {
        var outputDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "BenchmarkResults"));
        Directory.CreateDirectory(outputDir);

        await _probeDispatcher.InitializeAsync(msg => Console.WriteLine($"[probe] {msg}"));

        const string alias = GgufModelCatalog.Gemma4E2BAlias;
        var localPath = GgufModelCatalog.GetLocalGgufPath(alias);

        if (!File.Exists(localPath))
        {
            Assert.Fail($"Gemma 4 E2B GGUF not found at: {localPath}. Run ModelDownloadService first.");
            return;
        }

        var targets = new[] { (alias, "GgufGpu"), (alias, "GgufCpu") };

        var cases = BenchmarkPromptCatalog.All;
        int totalEvals = targets.Length * cases.Count;

        Console.WriteLine($"Model: {alias}");
        Console.WriteLine($"Total evaluations: {targets.Length} targets \u00d7 {cases.Count} cases = {totalEvals}");

        var combinedRunId = $"bench-{DateTime.UtcNow:yyyyMMdd-HHmmss}-gemma4-e2b";
        var startedAt = DateTimeOffset.UtcNow;
        var allResults = new List<BenchmarkResult>(totalEvals);
        string? dashPath = null;
        string currentStatus = string.Empty;
        var responseLogPath = Path.Combine(outputDir, combinedRunId + "-responses.jsonl");

        foreach (var (a, target) in targets)
        {
            Console.WriteLine($"\n=== {a} ({target}) ===");

            bool forceCpu = target == "GgufCpu";
            await using var dispatcher = AIDispatcherFactory.CreateFromLocalPath(
                localPath,
                maxContextTokens: GgufModelCatalog.Gemma4E2BContextTokens,
                forceCpuForGguf: forceCpu);

            try
            {
                await dispatcher.InitializeAsync(msg => Console.WriteLine($"  [init] {msg}"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  \u26a0\ufe0f  Skipped \u2014 init failed: {ex.Message}");
                totalEvals -= cases.Count;
                continue;
            }

            var runner = new BenchmarkRunner(dispatcher, a, target, enableLlmGrading: false);

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
                    var snap = new BenchmarkRun(combinedRunId, a, target, startedAt, allResults);
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

        var finalRun = new BenchmarkRun(combinedRunId, "targeted", "CPU+GPU", startedAt, allResults);
        BenchmarkReportGenerator.WriteReports(finalRun, outputDir);
        BenchmarkDashboardGenerator.Generate(finalRun, totalEvals, outputDir);

        int passed = finalRun.Results.Count(r => r.Evaluation.RuleScore >= BenchmarkReportGenerator.PassThreshold);
        double avg  = finalRun.Results.Count > 0 ? finalRun.Results.Average(r => r.Evaluation.RuleScore) : 0;

        Console.WriteLine();
        Console.WriteLine($"=== TARGETED BENCHMARK COMPLETE ===");
        Console.WriteLine($"  Models tested : {targets.Length}");
        Console.WriteLine($"  Results       : {finalRun.Results.Count}/{totalEvals}");
        Console.WriteLine($"  Passed        : {passed}");
        Console.WriteLine($"  Avg score     : {avg:F1}/100");
        Console.WriteLine($"  Reports       : {outputDir}");

        Assert.True(finalRun.Results.Count > 0, "Expected at least one result from the targeted benchmark run.");
    }
}
