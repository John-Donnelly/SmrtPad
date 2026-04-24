using SmrtPad.AI.Benchmarks.Reporting;

namespace SmrtPad.AI.Benchmarks.Tests;

public sealed class ModelPromptPolicyTests
{
    [Fact]
    public void SupportsThinkingMode_ReturnsTrue_ForReasoningCapableAliases()
    {
        Assert.True(ModelPromptPolicy.SupportsThinkingMode("qwen3-1.7b"));
        Assert.True(ModelPromptPolicy.SupportsThinkingMode("phi-4-mini"));
        Assert.True(ModelPromptPolicy.SupportsThinkingMode("deepseek-r1-7b"));
    }

    [Fact]
    public void SupportsThinkingMode_ReturnsFalse_ForNonReasoningAliases()
    {
        Assert.False(ModelPromptPolicy.SupportsThinkingMode("gemma-4-e2b"));
        Assert.False(ModelPromptPolicy.SupportsThinkingMode("llama-3.2-1b"));
        Assert.False(ModelPromptPolicy.SupportsThinkingMode("qwen2.5-0.5b"));
    }

    [Fact]
    public void BuildSystemPrompt_UsesModelSpecificDirective_ForGemma4()
    {
        var prompt = ModelPromptPolicy.BuildSystemPrompt("gemma-4-e2b", "gemma4", ModelReasoningMode.NoThinking);

        Assert.Contains("For Gemma 4 E2B", prompt);
        Assert.Contains("Non-thinking mode is enabled", prompt);
        Assert.DoesNotContain("Thinking mode is enabled", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_UsesThinkingDirective_ForReasoningModel()
    {
        var prompt = ModelPromptPolicy.BuildSystemPrompt("phi-4-mini-reasoning", "phi", ModelReasoningMode.Thinking);

        Assert.Contains("For Phi-4 Mini Reasoning", prompt);
        Assert.Contains("Thinking mode is enabled", prompt);
        Assert.Contains("<think>", prompt);
    }

    [Fact]
    public void ApplyPromptControls_UsesQwenModePrefix()
    {
        var thinkPrompt = ModelPromptPolicy.ApplyPromptControls("hello", "qwen3-1.7b", "qwen3", ModelReasoningMode.Thinking);
        var noThinkPrompt = ModelPromptPolicy.ApplyPromptControls("hello", "qwen3-1.7b", "qwen3", ModelReasoningMode.NoThinking);

        Assert.StartsWith("/think\n", thinkPrompt, StringComparison.Ordinal);
        Assert.StartsWith("/no_think\n", noThinkPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void NormalizeMode_ForUnsupportedModel_ForcesNoThinking()
    {
        var mode = ModelPromptPolicy.NormalizeMode("gemma-3-4b", "gemma3", ModelReasoningMode.Thinking);

        Assert.Equal(ModelReasoningMode.NoThinking, mode);
    }

    [Fact]
    public void BenchmarkReport_IncludesReasoningModeColumn()
    {
        var benchmarkCase = BenchmarkPromptCatalog.All.First();
        var eval = new EvaluationScore(40, 20, 20, 20, null, null);
        var thinkResult = new BenchmarkResult(
            benchmarkCase,
            "raw",
            "insert",
            null,
            1000,
            eval,
            "phi-4-mini",
            "GPU",
            DateTimeOffset.UtcNow,
            10,
            20,
            0.01,
            100,
            "Think");
        var noThinkResult = thinkResult with { ReasoningTag = "NoThink" };
        var run = new BenchmarkRun("run-1", "phi-4-mini", "GPU", DateTimeOffset.UtcNow, [thinkResult, noThinkResult], "Think");

        var markdown = BenchmarkReportGenerator.GenerateMarkdownReport(run);

        Assert.Contains("| Mode | `Think` |", markdown);
        Assert.Contains("| `phi-4-mini` | GPU | Think |", markdown);
        Assert.Contains("| `phi-4-mini` | GPU | NoThink |", markdown);
    }

    [Fact]
    public void DashboardData_UsesReasoningTaggedModelLabels()
    {
        var benchmarkCase = BenchmarkPromptCatalog.All.First();
        var eval = new EvaluationScore(40, 20, 20, 20, null, null);
        var thinkResult = new BenchmarkResult(
            benchmarkCase,
            "raw",
            "insert",
            null,
            1000,
            eval,
            "qwen3-1.7b",
            "GPU",
            DateTimeOffset.UtcNow,
            10,
            20,
            0.01,
            100,
            "Think");
        var noThinkResult = thinkResult with { ReasoningTag = "NoThink" };
        var run = new BenchmarkRun(
            "run-2",
            "qwen3-1.7b",
            "GPU",
            DateTimeOffset.UtcNow,
            [thinkResult, noThinkResult],
            "Think");
        var dir = Path.Combine(Path.GetTempPath(), "SmrtPad-ModeDash-" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            BenchmarkDashboardGenerator.Generate(run, 2, dir);
            var dataPath = Directory.GetFiles(dir, "*-data.js").Single();
            var data = File.ReadAllText(dataPath);

            Assert.Contains("qwen3-1.7b [Think]", data);
            Assert.Contains("qwen3-1.7b [NoThink]", data);
            Assert.Contains("modelReasoningTags", data);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void BenchmarkRun_Combine_PreservesDistinctReasoningTags()
    {
        var benchmarkCase = BenchmarkPromptCatalog.All.First();
        var eval = new EvaluationScore(40, 20, 20, 20, null, null);
        var noThinkRun = new BenchmarkRun(
            "run-a",
            "phi-4-mini",
            "GPU",
            DateTimeOffset.UtcNow,
            [new BenchmarkResult(benchmarkCase, "raw", "insert", null, 1000, eval, "phi-4-mini", "GPU", DateTimeOffset.UtcNow, 10, 20, 0.01, 100, "NoThink")],
            "NoThink");
        var thinkRun = new BenchmarkRun(
            "run-b",
            "phi-4-mini",
            "GPU",
            DateTimeOffset.UtcNow,
            [new BenchmarkResult(benchmarkCase, "raw", "insert", null, 1000, eval, "phi-4-mini", "GPU", DateTimeOffset.UtcNow, 10, 20, 0.01, 100, "Think")],
            "Think");

        var combined = BenchmarkRun.Combine("combined", [noThinkRun, thinkRun]);

        Assert.Contains("NoThink", combined.ReasoningTag);
        Assert.Contains("Think", combined.ReasoningTag);
        Assert.Equal(2, combined.Results.Count);
    }
}
