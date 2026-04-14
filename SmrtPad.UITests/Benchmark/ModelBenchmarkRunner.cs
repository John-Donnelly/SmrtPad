using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using OpenQA.Selenium;

namespace SmrtPad.UITests.Benchmark;

/// <summary>
/// Orchestrates a full benchmark run: iterates over models (optionally filtered),
/// runs all prompts against each model, collects results, scores them, estimates
/// costs, and generates reports.
/// </summary>
public sealed class ModelBenchmarkRunner
{
    /// <summary>All 18 Foundry Local model aliases in descending size order, plus Phi Silica.</summary>
    private static readonly string[] AllFoundryAliases =
    [
        "deepseek-r1-14b",
        "phi-4",
        "mistral-nemo",
        "phi-4-mini",
        "llama-3.2-3b",
        "deepseek-r1-7b",
        "phi-3.5-mini",
        "mistral-7b-v0.3",
        "qwen2.5-7b",
        "qwen2.5-14b",
        "gemma-2-2b",
        "gemma-2-9b",
        "llama-3.2-1b",
        "qwen2.5-3b",
        "phi-3-mini",
        "deepseek-r1-1.5b",
        "qwen2.5-1.5b",
        "qwen2.5-0.5b",
        "ernie-4.5-0.3b",
    ];

    private const string PhiSilicaAlias = "phi-silica";
    private const string PhiSilicaTarget = "NPU";

    private readonly SidebarAutomationHelper _sidebar;
    private readonly RuleBasedScorer _scorer;
    private readonly CostEstimator _costEstimator;
    private readonly IReadOnlyList<string>? _explicitModels;
    private readonly Action<string> _log;

    /// <summary>
    /// Initializes a new benchmark runner.
    /// </summary>
    /// <param name="sidebar">Automation helper for sidebar UI interactions.</param>
    /// <param name="scorer">Rule-based quality scorer.</param>
    /// <param name="costEstimator">Power cost estimator.</param>
    /// <param name="log">Logging callback for progress messages.</param>
    public ModelBenchmarkRunner(
        SidebarAutomationHelper sidebar,
        RuleBasedScorer scorer,
        CostEstimator costEstimator,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(sidebar);
        ArgumentNullException.ThrowIfNull(scorer);
        ArgumentNullException.ThrowIfNull(costEstimator);
        ArgumentNullException.ThrowIfNull(log);

        _sidebar = sidebar;
        _scorer = scorer;
        _costEstimator = costEstimator;
        _log = log;
    }

    /// <summary>
    /// Initializes a new benchmark runner with an explicit model list.
    /// Used by remote benchmarks where models are pre-filtered by hardware capability.
    /// </summary>
    /// <param name="sidebar">Automation helper for sidebar UI interactions.</param>
    /// <param name="scorer">Rule-based quality scorer.</param>
    /// <param name="costEstimator">Power cost estimator.</param>
    /// <param name="explicitModels">Pre-filtered list of model aliases to benchmark.</param>
    /// <param name="log">Logging callback for progress messages.</param>
    public ModelBenchmarkRunner(
        SidebarAutomationHelper sidebar,
        RuleBasedScorer scorer,
        CostEstimator costEstimator,
        IReadOnlyList<string> explicitModels,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(sidebar);
        ArgumentNullException.ThrowIfNull(scorer);
        ArgumentNullException.ThrowIfNull(costEstimator);
        ArgumentNullException.ThrowIfNull(explicitModels);
        ArgumentNullException.ThrowIfNull(log);

        _sidebar = sidebar;
        _scorer = scorer;
        _costEstimator = costEstimator;
        _explicitModels = explicitModels;
        _log = log;
    }

    /// <summary>
    /// Returns the list of models to benchmark, filtered by BENCHMARK_MODEL_FILTER if set.
    /// </summary>
    public static IReadOnlyList<string> GetModelsToRun()
    {
        var filter = Environment.GetEnvironmentVariable("BENCHMARK_MODEL_FILTER");
        var allModels = new List<string>(AllFoundryAliases) { PhiSilicaAlias };

        if (string.IsNullOrWhiteSpace(filter))
            return allModels;

        var filters = filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return allModels
            .Where(m => filters.Any(f => m.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Returns the prompt limit from <c>BENCHMARK_PROMPT_LIMIT</c>, or <c>null</c> for no limit.
    /// </summary>
    public static int? GetPromptLimit()
    {
        var raw = Environment.GetEnvironmentVariable("BENCHMARK_PROMPT_LIMIT");
        if (int.TryParse(raw, out var limit) && limit > 0)
            return limit;
        return null;
    }

    /// <summary>
    /// Runs the full benchmark suite against all selected models and prompts.
    /// </summary>
    public BenchmarkRunReport RunAll(IReadOnlyList<BenchmarkPrompt> prompts)
    {
        ArgumentNullException.ThrowIfNull(prompts);

        var promptLimit = GetPromptLimit();
        var activePrompts = promptLimit.HasValue
            ? (IReadOnlyList<BenchmarkPrompt>)prompts.Take(promptLimit.Value).ToList()
            : prompts;

        var models = _explicitModels ?? GetModelsToRun();
        _log($"Benchmark starting: {models.Count} models × {activePrompts.Count} prompts = {models.Count * activePrompts.Count} runs"
            + (promptLimit.HasValue ? $" (BENCHMARK_PROMPT_LIMIT={promptLimit})" : string.Empty));

        var allResults = new List<BenchmarkResult>();
        var allScores = new List<BenchmarkScore>();
        var allCosts = new List<PowerCostEstimate>();
        var modelErrors = new Dictionary<string, string>();
        var overallStopwatch = Stopwatch.StartNew();

        foreach (var model in models)
        {
            _log($"── Model: {model} ──");

            var isPhiSilica = model.Equals(PhiSilicaAlias, StringComparison.OrdinalIgnoreCase);
            var executionTarget = isPhiSilica ? PhiSilicaTarget : "GPU";

            try
            {
                // Switch to the appropriate model
                bool switchOk;
                if (isPhiSilica)
                {
                    switchOk = _sidebar.SwitchExecutionTarget("⚡ NPU (Phi Silica)");
                }
                else
                {
                    // GPU switch failure is non-fatal — log a warning and proceed
                    if (!_sidebar.SwitchExecutionTarget("🖥️ GPU"))
                        _log($"  ⚠ GPU target switch failed — proceeding with current target");
                    Thread.Sleep(500);
                    switchOk = _sidebar.SwitchModel(model);
                }

                if (!switchOk)
                {
                    var reason = $"Failed to switch to model {model}";
                    _log($"  ⚠ {reason} — skipping");
                    modelErrors[model] = reason;

                    // Record failure for all prompts
                    foreach (var prompt in activePrompts)
                    {
                        allResults.Add(new BenchmarkResult(
                            prompt.Id, model, executionTarget, prompt.SkillKey,
                            prompt.InputText, string.Empty, 0, 0, 0, 0,
                            Succeeded: false, ErrorMessage: reason));
                    }
                    continue;
                }

                _log($"  Model ready. Running {activePrompts.Count} prompts...");

                foreach (var prompt in activePrompts)
                {
                    _log($"  [{prompt.Id}] {prompt.Description}");

                    var result = _sidebar.ExecutePrompt(prompt, model, executionTarget);
                    allResults.Add(result);

                    if (result.Succeeded)
                    {
                        var score = _scorer.Score(prompt, result);
                        allScores.Add(score);

                        var cost = _costEstimator.Estimate(result);
                        allCosts.Add(cost);

                        _log($"    ✓ {result.EstimatedOutputTokens} tokens, {result.TokensPerSecond:F1} tps, score={score.OverallScore:F2}, ${cost.EstimatedCostUsd:F6}");
                    }
                    else
                    {
                        _log($"    ✗ Error: {result.ErrorMessage}");
                    }

                    // Brief pause between prompts to let the UI settle
                    Thread.Sleep(1000);
                }

                _log($"  Model {model} complete.");
            }
            catch (WebDriverException ex)
            {
                // App crashed or session lost — record remaining prompts and try to recover
                var errorMsg = $"Session lost during {model}: {ex.Message[..Math.Min(120, ex.Message.Length)]}";
                _log($"  ✗ {errorMsg}");
                modelErrors[model] = errorMsg;

                // Fill in failed results for any prompts not yet recorded
                var recorded = allResults.Count(r => r.ModelAlias == model);
                foreach (var prompt in activePrompts.Skip(recorded))
                {
                    allResults.Add(new BenchmarkResult(
                        prompt.Id, model, executionTarget, prompt.SkillKey,
                        prompt.InputText, string.Empty, 0, 0, 0, 0,
                        Succeeded: false, ErrorMessage: errorMsg));
                }

                // Attempt session restart so the next model can run
                _log($"  Attempting session restart...");
                if (_sidebar.TryRestartApp())
                    _log($"  Session restarted successfully — continuing to next model");
                else
                    _log($"  Session restart failed — remaining models will fail their switch check");
            }
        }

        overallStopwatch.Stop();
        _log($"Benchmark complete in {overallStopwatch.Elapsed:hh\\:mm\\:ss}");

        return new BenchmarkRunReport(
            Timestamp: DateTime.UtcNow,
            TotalElapsed: overallStopwatch.Elapsed,
            ModelsRun: models,
            PromptsRun: prompts,
            Results: allResults,
            Scores: allScores,
            Costs: allCosts,
            ModelErrors: modelErrors);
    }

    /// <summary>
    /// Saves the raw results to a JSON file for later analysis.
    /// </summary>
    public static string SaveResultsJson(BenchmarkRunReport report, string outputDir)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(outputDir);

        Directory.CreateDirectory(outputDir);
        var path = Path.Combine(outputDir, $"benchmark-results-{report.Timestamp:yyyyMMdd-HHmmss}.json");
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
        File.WriteAllText(path, json);
        return path;
    }
}

/// <summary>
/// Aggregated output of a full benchmark run.
/// </summary>
public sealed record BenchmarkRunReport(
    DateTime Timestamp,
    TimeSpan TotalElapsed,
    IReadOnlyList<string> ModelsRun,
    IReadOnlyList<BenchmarkPrompt> PromptsRun,
    IReadOnlyList<BenchmarkResult> Results,
    IReadOnlyList<BenchmarkScore> Scores,
    IReadOnlyList<PowerCostEstimate> Costs,
    IReadOnlyDictionary<string, string> ModelErrors);
