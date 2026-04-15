using System.Diagnostics;
using SmrtPad.AI.Benchmarks.Reporting;

namespace SmrtPad.AI.Benchmarks.Tests;

/// <summary>
/// Benchmarks all ONNX GenAI models discovered on local drives (B:\Models, etc.).
/// Each subdirectory containing <c>genai_config.json</c> + <c>model.onnx</c> is treated
/// as a loadable model and benchmarked via the ORT GenAI adapter with a live dashboard.
/// Run with: dotnet test --filter "Category=LocalModelBenchmark"
/// </summary>
[Collection("LiveBenchmarks")]
public sealed class LocalModelBenchmarkTests
{
    /// <summary>
    /// Root directories to scan recursively for ONNX GenAI model folders.
    /// A valid model folder must contain both <c>genai_config.json</c> and <c>model.onnx</c>.
    /// </summary>
    private static readonly string[] ModelSearchRoots =
    [
        @"B:\Models\benchmark-models",
    ];

    private const int MaxContextTokens = 4096;

    [Fact(Timeout = 43_200_000)] // 12-hour ceiling
    [Trait("Category", "LocalModelBenchmark")]
    public async Task LocalModelBenchmark_AllDiscoveredModels_WithLiveDashboard()
    {
        var outputDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "BenchmarkResults"));
        Directory.CreateDirectory(outputDir);

        var discoveredModels = DiscoverLocalModels();

        if (discoveredModels.Count == 0)
        {
            Assert.Fail(
                $"No ONNX GenAI models found. Searched: {string.Join(", ", ModelSearchRoots)}. " +
                "Each model directory must contain genai_config.json and model.onnx.");
            return;
        }

        Console.WriteLine($"=== LOCAL MODEL BENCHMARK ===");
        Console.WriteLine($"  Discovered {discoveredModels.Count} model(s):");
        foreach (var (name, path) in discoveredModels)
            Console.WriteLine($"    {name,-35} {path}");
        Console.WriteLine();

        var cases = BenchmarkPromptCatalog.All;
        int totalEvals = discoveredModels.Count * cases.Count;

        var combinedRunId = $"bench-{DateTime.UtcNow:yyyyMMdd-HHmmss}-local-models";
        var startedAt = DateTimeOffset.UtcNow;
        var allResults = new List<BenchmarkResult>(totalEvals);
        string? dashPath = null;
        string currentStatus = string.Empty;
        var responseLogPath = Path.Combine(outputDir, combinedRunId + "-responses.jsonl");

        foreach (var (modelName, modelDir) in discoveredModels)
        {
            Console.WriteLine($"\n=== {modelName} ===");
            Console.WriteLine($"    Path: {modelDir}");

            AIDispatcher? dispatcher = null;
            try
            {
                dispatcher = AIDispatcherFactory.CreateFromLocalPath(modelDir, MaxContextTokens);
                await dispatcher.InitializeAsync(msg => Console.WriteLine($"  [init] {msg}"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️  Skipped — load failed: {ex.Message}");
                if (ex.InnerException is not null)
                    Console.WriteLine($"       Inner: {ex.InnerException.Message}");
                totalEvals -= cases.Count;

                if (dispatcher is not null)
                    await dispatcher.DisposeAsync();

                continue;
            }

            await using (dispatcher)
            {
                var runner = new BenchmarkRunner(dispatcher, modelName, "LocalDirect", enableLlmGrading: false);

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
                        var snap = new BenchmarkRun(combinedRunId, modelName, "LocalDirect", startedAt, allResults);
                        var path = BenchmarkDashboardGenerator.Generate(snap, totalEvals, outputDir, currentStatus: currentStatus);
                        if (dashPath is null)
                        {
                            dashPath = path;
                            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
                        }
                    },
                    responseLogPath: responseLogPath,
                    perCaseTimeout: TimeSpan.FromSeconds(300));
            }
        }

        // Final reports
        var finalRun = new BenchmarkRun(combinedRunId, "local-models", "LocalDirect", startedAt, allResults);
        BenchmarkReportGenerator.WriteReports(finalRun, outputDir);
        BenchmarkDashboardGenerator.Generate(finalRun, totalEvals, outputDir);

        int passed = finalRun.Results.Count(r => r.Evaluation.RuleScore >= BenchmarkReportGenerator.PassThreshold);
        double avg = finalRun.Results.Count > 0 ? finalRun.Results.Average(r => r.Evaluation.RuleScore) : 0;

        Console.WriteLine();
        Console.WriteLine($"=== LOCAL MODEL BENCHMARK COMPLETE ===");
        Console.WriteLine($"  Models tested : {discoveredModels.Count}");
        Console.WriteLine($"  Results       : {finalRun.Results.Count}/{totalEvals}");
        Console.WriteLine($"  Passed        : {passed}");
        Console.WriteLine($"  Avg score     : {avg:F1}/100");
        Console.WriteLine($"  Reports       : {outputDir}");

        Assert.True(finalRun.Results.Count > 0, "Expected at least one result from the local model benchmark run.");
    }

    /// <summary>
    /// Recursively scans <see cref="ModelSearchRoots"/> for directories containing
    /// both <c>genai_config.json</c> and <c>model.onnx</c>.
    /// Returns a list of (friendly name, absolute directory path) tuples ordered by name.
    /// </summary>
    private static List<(string Name, string Path)> DiscoverLocalModels()
    {
        var models = new List<(string Name, string Path)>();

        foreach (var root in ModelSearchRoots)
        {
            if (!Directory.Exists(root))
                continue;

            // Check if the root itself is a model directory
            if (IsOnnxGenAiModelDirectory(root))
            {
                models.Add((DeriveFriendlyName(root), root));
                continue;
            }

            // Recurse into subdirectories
            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                if (IsOnnxGenAiModelDirectory(dir))
                    models.Add((DeriveFriendlyName(dir), dir));
            }
        }

        models.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return models;
    }

    private static bool IsOnnxGenAiModelDirectory(string dir) =>
        File.Exists(System.IO.Path.Combine(dir, "genai_config.json")) &&
        File.Exists(System.IO.Path.Combine(dir, "model.onnx"));

    /// <summary>
    /// Derives a human-readable model name from the directory path.
    /// Uses the leaf directory name, or parent+leaf for deeply nested models
    /// (e.g. "Microsoft/qwen2.5-7b-instruct-cuda-gpu-4/v4" → "qwen2.5-7b-instruct-cuda-gpu-4").
    /// </summary>
    private static string DeriveFriendlyName(string modelDir)
    {
        var leaf = System.IO.Path.GetFileName(modelDir);

        // If the leaf is a version tag like "v4" or "v2", use the parent directory name
        if (leaf is not null && leaf.StartsWith('v') && leaf.Length <= 3 && leaf.Skip(1).All(char.IsDigit))
        {
            var parent = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(modelDir));
            return parent ?? leaf;
        }

        return leaf ?? modelDir;
    }
}
