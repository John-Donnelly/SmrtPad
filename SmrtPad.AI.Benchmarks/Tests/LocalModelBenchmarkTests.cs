using System.Diagnostics;
using SmrtPad.AI.Benchmarks.Reporting;

namespace SmrtPad.AI.Benchmarks.Tests;

/// <summary>
/// Benchmarks all locally available llama.cpp GGUF models found under the configured GGUF roots.
/// Run with: dotnet test --filter "Category=LocalModelBenchmark"
/// </summary>
[Collection("LiveBenchmarks")]
public sealed class LocalModelBenchmarkTests
{
    /// <summary>
    /// Root directories containing GGUF model subdirectories.
    /// Each immediate subdirectory of these roots is expected to hold one <c>.gguf</c> file.
    /// </summary>
    private static readonly string[] GgufSearchRoots =
    [
        @"B:\Models\benchmark-models-gguf",
    ];

    private const int MaxContextTokens = 4096;

    private const string BenchmarkModeEnv = "SMRTPAD_BENCHMARK_MODE";
    private const string LlamaBackendDirEnv = "SMRTPAD_LLAMA_BACKEND_DIR";
    private static readonly string[] KnownLlamaBackendDirs =
    [
        @"B:\Tools\llama-gemma4-backend",
    ];

    private enum BenchmarkMode
    {
        Gpu,
        Cpu,
    }

    [Fact(Timeout = 43_200_000)] // 12-hour ceiling
    [Trait("Category", "LocalModelBenchmark")]
    public async Task LocalModelBenchmark_AllDiscoveredModels_WithLiveDashboard()
    {
        var mode = ResolveBenchmarkMode();
        string? backendOverride = ResolveLlamaBackendDirectoryOverride();

        var outputDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "BenchmarkResults"));
        Directory.CreateDirectory(outputDir);

        var discoveredModels = DiscoverLocalModels();
        var benchmarkRuns = CreateBenchmarkRuns(discoveredModels, mode);

        if (benchmarkRuns.Count == 0)
        {
            Assert.Fail(
                $"No GGUF models found. Searched: {string.Join(", ", GgufSearchRoots)}. " +
                "Expected subdirectories each containing one *.gguf file.");
            return;
        }

        if (discoveredModels.Any(m => m.Name.Contains("gemma-4", StringComparison.OrdinalIgnoreCase))
            && !SelectedBackendSupportsArchitecture(backendOverride, "gemma4"))
        {
            Assert.Fail(
                $"Gemma 4 models were discovered but the selected llama backend does not support architecture 'gemma4'. " +
                $"Set {LlamaBackendDirEnv} to a Gemma4-capable backend directory or place one at a known path.");
            return;
        }

        Console.WriteLine($"=== LOCAL MODEL BENCHMARK ===");
        Console.WriteLine($"  Mode: {mode}");
        Console.WriteLine($"  Backend override ({LlamaBackendDirEnv}): {backendOverride ?? "<none>"}");
        Console.WriteLine($"  Discovered {discoveredModels.Count} model(s):");
        foreach (var (name, path, _) in discoveredModels)
            Console.WriteLine($"    {name,-40}  {path}");
        Console.WriteLine($"  Planned runs: {benchmarkRuns.Count}");
        foreach (var run in benchmarkRuns)
            Console.WriteLine($"    {run.DisplayName,-55} [{run.BackendLabel}] ({run.ReasoningTag})");
        Console.WriteLine();

        var cases = BenchmarkPromptCatalog.All;
        int totalEvals = benchmarkRuns.Count * cases.Count;

        var combinedRunId = $"bench-{DateTime.UtcNow:yyyyMMdd-HHmmss}-local-models";
        var startedAt = DateTimeOffset.UtcNow;
        var allResults = new List<BenchmarkResult>(totalEvals);
        string? dashPath = null;
        string currentStatus = string.Empty;
        var responseLogPath = Path.Combine(outputDir, combinedRunId + "-responses.jsonl");

        foreach (var benchmarkRun in benchmarkRuns)
        {
            Console.WriteLine($"\n=== {benchmarkRun.DisplayName} ===");
            Console.WriteLine($"    Path: {benchmarkRun.ModelPath}");
            Console.WriteLine($"    Backend: {benchmarkRun.BackendLabel}");
            Console.WriteLine($"    Reasoning: {benchmarkRun.ReasoningTag}");

            AIDispatcher? dispatcher = null;
            try
            {
                dispatcher = AIDispatcherFactory.CreateFromLocalPath(
                    benchmarkRun.ModelPath,
                    MaxContextTokens,
                    benchmarkRun.ForceCpu,
                    backendOverride);
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
                var runner = new BenchmarkRunner(
                    dispatcher,
                    benchmarkRun.ModelName,
                    benchmarkRun.BackendLabel,
                    enableLlmGrading: false,
                    benchmarkRun.ReasoningTag);

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
                        var snap = new BenchmarkRun(combinedRunId, benchmarkRun.ModelName, benchmarkRun.BackendLabel, startedAt, allResults, benchmarkRun.ReasoningTag);
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
        var finalRun = new BenchmarkRun(combinedRunId, "local-models", mode == BenchmarkMode.Cpu ? "LlamaCpp CPU" : "LlamaCpp GPU+CPU", startedAt, allResults, "NoThink/Think");
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

        Assert.NotEmpty(finalRun.Results);
    }

    private sealed record LocalBenchmarkRun(
        string ModelName,
        string ModelPath,
        string BackendLabel,
        bool ForceCpu,
        string ReasoningTag,
        string DisplayName);

    /// <summary>
    /// Discovers all GGUF models from the configured roots.
    /// Each immediate subdirectory of a GGUF root is expected to hold one <c>.gguf</c> file.
    /// All models run fully on GPU via llama.cpp (<c>GpuLayerCount=999</c>).
    /// Returns <c>(friendly name, absolute path, backendTarget)</c> ordered Gemma-4 first, then alphabetically.
    /// </summary>
    private static List<(string Name, string Path, bool SupportsThinking)> DiscoverLocalModels()
    {
        var models = new List<(string Name, string Path, bool SupportsThinking)>();

        // GGUF files — each immediate subdirectory of a GGUF root holds one .gguf file.
        // All GGUF models run fully on GPU (GpuLayerCount=999).
        foreach (var root in GgufSearchRoots)
        {
            if (!Directory.Exists(root))
                continue;

            foreach (var subdir in Directory.EnumerateDirectories(root))
            {
                var gguf = Directory.EnumerateFiles(subdir, "*.gguf").FirstOrDefault();
                if (gguf is not null)
                {
                    var modelName = System.IO.Path.GetFileName(subdir) + " [GGUF]";
                    models.Add((modelName, gguf, SupportsThinkingMode(gguf, modelName)));
                }
            }
        }

        models.Sort((a, b) =>
        {
            int rankA = a.Name.Contains("gemma-4", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            int rankB = b.Name.Contains("gemma-4", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            int rankCmp = rankA.CompareTo(rankB);
            return rankCmp != 0 ? rankCmp : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return models;
    }

    private static List<LocalBenchmarkRun> CreateBenchmarkRuns(
        IReadOnlyList<(string Name, string Path, bool SupportsThinking)> discoveredModels,
        BenchmarkMode mode)
    {
        var runs = new List<LocalBenchmarkRun>();

        void AddRuns(bool forceCpu, string backendLabel)
        {
            foreach (var (name, path, supportsThinking) in discoveredModels)
            {
                runs.Add(new LocalBenchmarkRun(
                    name,
                    path,
                    backendLabel,
                    forceCpu,
                    "NoThink",
                    $"{name} - {backendLabel} - NoThink"));

                if (supportsThinking)
                {
                    runs.Add(new LocalBenchmarkRun(
                        name,
                        path,
                        backendLabel,
                        forceCpu,
                        "Think",
                        $"{name} - {backendLabel} - Think"));
                }
            }
        }

        if (mode == BenchmarkMode.Gpu)
        {
            AddRuns(forceCpu: false, backendLabel: "LlamaCpp GPU");
            AddRuns(forceCpu: true, backendLabel: "LlamaCpp CPU");
        }
        else
        {
            AddRuns(forceCpu: true, backendLabel: "LlamaCpp CPU");
        }

        return runs;
    }

    private static bool SupportsThinkingMode(string modelPath, string modelName)
    {
        var alias = ModelPromptPolicy.DetectAliasFromPath(modelPath);
        if (ModelPromptPolicy.SupportsThinkingMode(alias))
            return true;

        if (modelName.Contains("reasoning", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static BenchmarkMode ResolveBenchmarkMode()
    {
        var raw = Environment.GetEnvironmentVariable(BenchmarkModeEnv);
        if (string.IsNullOrWhiteSpace(raw))
            return BenchmarkMode.Gpu;

        return raw.Trim().ToUpperInvariant() switch
        {
            "GPU" => BenchmarkMode.Gpu,
            "CPU" => BenchmarkMode.Cpu,
            _ => throw new InvalidOperationException(
                $"Invalid {BenchmarkModeEnv} value '{raw}'. Expected 'GPU' or 'CPU'."),
        };
    }

    private static string? ResolveLlamaBackendDirectoryOverride()
    {
        var configured = Environment.GetEnvironmentVariable(LlamaBackendDirEnv);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        return KnownLlamaBackendDirs.FirstOrDefault(IsValidLlamaBackendDirectory);
    }

    private static bool SelectedBackendSupportsArchitecture(string? backendDirectory, string architecture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(architecture);

        string? llamaDllPath = backendDirectory is not null
            ? Path.Combine(backendDirectory, "llama.dll")
            : Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "cuda12", "llama.dll");

        if (!File.Exists(llamaDllPath))
            return false;

        try
        {
            var text = System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(llamaDllPath));
            return text.Contains(architecture, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidLlamaBackendDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return File.Exists(Path.Combine(path, "llama.dll"))
            && File.Exists(Path.Combine(path, "ggml.dll"));
    }

    /// <summary>
    /// Targeted benchmark: runs only gemma-4-e2b on GPU via the llama.cpp GGUF path.
    /// Run with: dotnet test --filter "Category=Gemma4E2bBenchmark"
    /// </summary>
    [Fact(Timeout = 7_200_000)] // 2-hour ceiling
    [Trait("Category", "Gemma4E2bBenchmark")]
    public async Task TargetedBenchmarkRun_Gemma4E2b_WithLiveDashboard()
    {
        const string alias = "gemma-4-e2b";
        var modelPath = GgufModelCatalog.GetLocalGgufPath(alias);

        if (!File.Exists(modelPath))
            Assert.Fail($"Model file not found: {modelPath}. Download it before running this benchmark.");

        string? backendOverride = ResolveLlamaBackendDirectoryOverride();

        if (!SelectedBackendSupportsArchitecture(backendOverride, "gemma4"))
            Assert.Fail(
                $"The selected llama backend does not support architecture 'gemma4'. " +
                $"Set {LlamaBackendDirEnv} to a Gemma4-capable backend directory or place one at a known path.");

        var outputDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "BenchmarkResults"));
        Directory.CreateDirectory(outputDir);

        var cases = BenchmarkPromptCatalog.All;
        var combinedRunId = $"bench-{DateTime.UtcNow:yyyyMMdd-HHmmss}-gemma4-e2b";
        var startedAt = DateTimeOffset.UtcNow;
        var allResults = new List<BenchmarkResult>(cases.Count);
        string? dashPath = null;
        string currentStatus = string.Empty;
        var responseLogPath = Path.Combine(outputDir, combinedRunId + "-responses.jsonl");
        const string backendLabel = "LlamaCpp GPU";
        const string modelName = "gemma-4-e2b [GGUF]";
        const string reasoningTag = "NoThink";
        int totalEvals = cases.Count;

        Console.WriteLine($"=== TARGETED BENCHMARK: {alias} ===");
        Console.WriteLine($"  Path    : {modelPath}");
        Console.WriteLine($"  Backend : {backendLabel}");
        Console.WriteLine($"  Cases   : {cases.Count}");
        Console.WriteLine();

        AIDispatcher? dispatcher = null;
        try
        {
            dispatcher = AIDispatcherFactory.CreateFromLocalPath(
                modelPath,
                MaxContextTokens,
                forceCpuForGguf: false,
                backendOverride);
            await dispatcher.InitializeAsync(msg => Console.WriteLine($"  [init] {msg}"));
        }
        catch (Exception ex)
        {
            if (dispatcher is not null) await dispatcher.DisposeAsync();
            Assert.Fail($"Dispatcher init failed: {ex.Message}");
            return;
        }

        await using (dispatcher)
        {
            var runner = new BenchmarkRunner(
                dispatcher,
                modelName,
                backendLabel,
                enableLlmGrading: false,
                reasoningTag);

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
                    var snap = new BenchmarkRun(combinedRunId, modelName, backendLabel, startedAt, allResults, reasoningTag);
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

        var finalRun = new BenchmarkRun(combinedRunId, modelName, backendLabel, startedAt, allResults, reasoningTag);
        BenchmarkReportGenerator.WriteReports(finalRun, outputDir);
        BenchmarkDashboardGenerator.Generate(finalRun, totalEvals, outputDir);

        int passed = finalRun.Results.Count(r => r.Evaluation.RuleScore >= BenchmarkReportGenerator.PassThreshold);
        double avg = finalRun.Results.Count > 0 ? finalRun.Results.Average(r => r.Evaluation.RuleScore) : 0;

        Console.WriteLine();
        Console.WriteLine($"=== GEMMA 4 E2B BENCHMARK COMPLETE ===");
        Console.WriteLine($"  Results : {finalRun.Results.Count}/{totalEvals}");
        Console.WriteLine($"  Passed  : {passed}");
        Console.WriteLine($"  Avg     : {avg:F1}/100");
        Console.WriteLine($"  Reports : {outputDir}");

        Assert.NotEmpty(finalRun.Results);
    }
}

