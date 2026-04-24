using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;
using SmrtPad.AI.Benchmarks.Reporting;

namespace SmrtPad.AI.Benchmarks;

[CPUUsageDiagnoser]
[MemoryDiagnoser]
public class BenchmarkDashboardGeneratorBenchmarks
{
    private BenchmarkRun _run = null!;
    private string _outputDir = null!;
    [GlobalSetup]
    public void GlobalSetup()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "SmrtPad-BenchmarkDashboardGenerator", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDir);
        _run = CreateRun();
        BenchmarkDashboardGenerator.Generate(_run, _run.Results.Count, _outputDir, currentStatus: "[warmup] BenchmarkDashboardGenerator.Generate");
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }

    [Benchmark]
    public string Generate()
    {
        return BenchmarkDashboardGenerator.Generate(_run, _run.Results.Count, _outputDir, currentStatus: "[32/32] reporting benchmark");
    }

    private static BenchmarkRun CreateRun()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var cases = Enumerable.Range(1, 32)
            .Select(i => new BenchmarkCase(
                $"case-{i:00}",
                i % 2 == 0 ? "summarize" : "rewrite",
                $"Synthetic input for benchmark case {i}.",
                i % 3 == 0 ? "Business" : null,
                ["alpha", "beta"],
                i % 4 != 0,
                $"Synthetic benchmark case {i}",
                (i % 3) switch
                {
                    0 => BenchmarkCategory.DocumentComposition,
                    1 => BenchmarkCategory.EditSkill,
                    _ => BenchmarkCategory.TagCompliance,
                }))
            .ToList();
        var results = new List<BenchmarkResult>(cases.Count);
        for (int i = 0; i < cases.Count; i++)
        {
            var benchmarkCase = cases[i];
            var eval = new EvaluationScore(40, i % 5 == 0 ? 10 : 20, i % 7 == 0 ? 10 : 20, 20, 7 + (i % 3), "Synthetic benchmark result");
            var latencyMs = 900 + (i * 37);
            var ttftMs = 120 + (i * 3);
            var outputTokens = 140 + (i * 5);
            var electricityCost = 0.00012 + (i * 0.00001);
            var reasoningTag = i % 2 == 0 ? "NoThink" : "Think";
            results.Add(new BenchmarkResult(benchmarkCase, $"raw output {i}", benchmarkCase.ExpectsInsertTag ? $"insert content {i}" : null, reasoningTag == "Think" ? $"reasoning {i}" : null, latencyMs, eval, i % 3 == 0 ? "phi-3.5-mini" : "qwen3-0.6b", i % 4 == 0 ? "OnnxRuntimeGpu" : "OnnxRuntimeCpu", startedAt.AddSeconds(i * 12), 80 + (i * 2), outputTokens, electricityCost, ttftMs, reasoningTag));
        }

        return new BenchmarkRun("bench-reporting-clean", "multi-model", "CPU+GPU", startedAt, results);
    }
}