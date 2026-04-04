using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SmrtPad.UITests.Infrastructure;

namespace SmrtPad.UITests.Benchmark;

/// <summary>
/// Drives the AI benchmark suite via Appium: iterates every available model
/// against all 38 catalog prompts, records extended metrics (insert-tag compliance,
/// keyword coverage, contamination), and emits Markdown and JSON reports.
/// </summary>
public sealed partial class AiModelBenchmarkRunner
{
    private readonly IBenchmarkFixture _fixture;
    private readonly SidebarAutomationHelper _sidebar;
    private readonly RuleBasedScorer _scorer;
    private readonly Action<string>? _log;

    // Contamination patterns — mirror those in PromptTemplates / ResponseCleaner
    [GeneratedRegex(
        @"^(here'?s|here is|certainly[!,]?|sure[!,]?|of course[!,]?|absolutely[!,]?|i'?d be happy|i can help|below is|the following is)",
        RegexOptions.IgnoreCase)]
    private static partial Regex PreamblePattern();

    [GeneratedRegex(
        @"(i hope (this|that)|let me know|feel free to|if you (need|have)|don'?t hesitate|is there anything else|hope this helps)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ClosingRemarkPattern();

    private const string OutputDirectory = "BenchmarkResults";

    public AiModelBenchmarkRunner(IBenchmarkFixture fixture, Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
        _sidebar = new SidebarAutomationHelper(fixture, log);
        _scorer = new RuleBasedScorer();
        _log = log;
    }

    private void Log(string message) => _log?.Invoke($"[AIRunner] {message}");

    private const string PhiSilicaAlias = "phi-silica";
    private const string PhiSilicaTarget = "NPU";
    private const string GpuTarget = "GPU";

    /// <summary>
    /// Runs all catalog prompts against every available model and returns a full report.
    /// </summary>
    public AiBenchmarkRunReport RunAll() => RunAll(AiBenchmarkCatalog.GetAll());

    /// <summary>
    /// Runs the provided set of prompts against every available model.
    /// </summary>
    public AiBenchmarkRunReport RunAll(IReadOnlyList<AiBenchmarkPrompt> prompts)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var modelSummaries = new List<AiModelSummary>();

        var models = ModelBenchmarkRunner.GetModelsToRun();
        Log($"RunAll: {models.Count} model(s) × {prompts.Count} prompt(s) = {models.Count * prompts.Count} total interactions");

        foreach (var modelAlias in models)
        {
            var isPhiSilica = modelAlias.Equals(PhiSilicaAlias, StringComparison.OrdinalIgnoreCase);
            var executionTarget = isPhiSilica ? PhiSilicaTarget : GpuTarget;

            Log($"--- Starting model '{modelAlias}' on {executionTarget} ---");
            var summary = RunModel(modelAlias, executionTarget, isPhiSilica, prompts);
            modelSummaries.Add(summary);
            Log($"--- Model '{modelAlias}' complete: {summary.Succeeded}/{summary.TotalPrompts} succeeded, " +
                $"insert compliance={summary.InsertComplianceRate:P0}, avg score={summary.AvgBaseScore:F3} ---");
        }

        var report = new AiBenchmarkRunReport(startedAt, DateTimeOffset.UtcNow, modelSummaries);
        WriteReports(report);
        return report;
    }

    private AiModelSummary RunModel(
        string modelAlias, string executionTarget, bool isPhiSilica,
        IReadOnlyList<AiBenchmarkPrompt> prompts)
    {
        bool switchOk;
        if (isPhiSilica)
        {
            switchOk = _sidebar.SwitchExecutionTarget("⚡ NPU (Phi Silica)");
        }
        else
        {
            if (!_sidebar.SwitchExecutionTarget("🖥️ GPU"))
                Log($"RunModel: GPU target switch failed — proceeding with current target");
            switchOk = _sidebar.SwitchModel(modelAlias);
        }

        if (!switchOk)
        {
            Log($"RunModel: switch to '{modelAlias}' failed — skipping");
            return EmptySummary(modelAlias, executionTarget, prompts.Count);
        }

        var results = new List<AiModelBenchmarkResult>();

        foreach (var aiPrompt in prompts)
        {
            Log($"  Running [{aiPrompt.Prompt.Id}] skill={aiPrompt.Prompt.SkillKey}...");

            BenchmarkResult baseResult;
            bool hasInsert = false;
            string? insertContent = null;

            try
            {
                baseResult = _sidebar.ExecutePrompt(aiPrompt.Prompt, modelAlias, executionTarget);
            }
            catch (Exception ex)
            {
                Log($"  [{aiPrompt.Prompt.Id}] EXCEPTION in ExecutePrompt: {ex.Message}");
                baseResult = new BenchmarkResult(
                    PromptId: aiPrompt.Prompt.Id,
                    ModelAlias: modelAlias,
                    ExecutionTarget: executionTarget,
                    SkillKey: aiPrompt.Prompt.SkillKey,
                    InputText: aiPrompt.Prompt.InputText,
                    OutputText: string.Empty,
                    ElapsedSeconds: 0,
                    EstimatedInputTokens: 0,
                    EstimatedOutputTokens: 0,
                    TokensPerSecond: 0,
                    Succeeded: false,
                    ErrorMessage: ex.Message);
            }

            // Check for Insert button BEFORE starting the next session clears the history.
            // Also attempt to read the insert content for keyword scoring.
            // These calls have their own internal error handling and do not affect baseResult.
            if (aiPrompt.ExpectsInsertTag)
            {
                hasInsert = _sidebar.HasInsertButton();
                if (hasInsert)
                    insertContent = _sidebar.TryGetInsertText();
            }

            var score = _scorer.Score(aiPrompt.Prompt, baseResult);

            // When the model correctly wrapped all output in <insert> tags the visible bubble
            // text is empty (or a single UI artefact word).  In that case patch the result
            // with real content so scoring, token counts, and keyword search are accurate.
            // Timing and TPS are preserved from the original baseResult.
            var effectiveResult = insertContent is { Length: > 10 }
                && baseResult.EstimatedOutputTokens <= 5
                ? baseResult with
                  {
                      OutputText = insertContent,
                      EstimatedOutputTokens = insertContent.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                      Succeeded = true,
                  }
                : baseResult;

            if (effectiveResult != baseResult)
                score = _scorer.Score(aiPrompt.Prompt, effectiveResult);

            // Keyword search against the effective output text (which may be the insert content).
            var searchText = effectiveResult.OutputText + " " + insertContent;
            var keywordsFound = FindKeywords(searchText, aiPrompt.ExpectedKeywords);
            var keywordScore = aiPrompt.ExpectedKeywords.Length == 0
                ? 1.0
                : (double)keywordsFound.Length / aiPrompt.ExpectedKeywords.Length;
            var insertCompliant = aiPrompt.ExpectsInsertTag == hasInsert;

            results.Add(new AiModelBenchmarkResult(
                Base: effectiveResult,
                ExpectsInsertTag: aiPrompt.ExpectsInsertTag,
                HasInsertButton: hasInsert,
                InsertContent: insertContent,
                ExpectedKeywords: aiPrompt.ExpectedKeywords,
                KeywordsFound: keywordsFound,
                KeywordScore: keywordScore,
                InsertCompliant: insertCompliant,
                BaseScore: score));

            Log($"  [{aiPrompt.Prompt.Id}] done: succeeded={effectiveResult.Succeeded}, " +
                $"insertCompliant={insertCompliant}, insertTextLen={insertContent?.Length ?? 0}, " +
                $"keywords={keywordsFound.Length}/{aiPrompt.ExpectedKeywords.Length}, " +
                $"score={score.OverallScore:F3}, latency={effectiveResult.ElapsedSeconds:F1}s");
        }

        return BuildSummary(modelAlias, executionTarget, results);
    }

    private static string[] FindKeywords(string output, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(output) || keywords.Length == 0)
            return [];

        return keywords
            .Where(kw => output.Contains(kw, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static AiModelSummary BuildSummary(
        string modelAlias, string executionTarget, List<AiModelBenchmarkResult> results)
    {
        var succeeded = results.Count(r => r.Base.Succeeded);
        var insertCompliantCount = results.Count(r => r.InsertCompliant);
        var insertRate = results.Count > 0 ? (double)insertCompliantCount / results.Count : 0.0;
        var avgKeyword = results.Count > 0 ? results.Average(r => r.KeywordScore) : 0.0;
        var avgLatency = succeeded > 0 ? results.Where(r => r.Base.Succeeded).Average(r => r.Base.ElapsedSeconds) : 0.0;
        var avgTps = succeeded > 0 ? results.Where(r => r.Base.Succeeded).Average(r => r.Base.TokensPerSecond) : 0.0;
        var avgScore = results.Count > 0 ? results.Average(r => r.BaseScore.OverallScore) : 0.0;

        return new AiModelSummary(
            ModelAlias: modelAlias,
            ExecutionTarget: executionTarget,
            TotalPrompts: results.Count,
            Succeeded: succeeded,
            InsertCompliantCount: insertCompliantCount,
            InsertComplianceRate: insertRate,
            AvgKeywordScore: avgKeyword,
            AvgLatencySeconds: avgLatency,
            AvgTokensPerSecond: avgTps,
            AvgBaseScore: avgScore,
            Results: results);
    }

    private static AiModelSummary EmptySummary(string modelAlias, string executionTarget, int total) =>
        new(modelAlias, executionTarget, total, 0, 0, 0.0, 0.0, 0.0, 0.0, 0.0, []);

    // ── Report generation ────────────────────────────────────────────────────

    private void WriteReports(AiBenchmarkRunReport report)
    {
        try
        {
            Directory.CreateDirectory(OutputDirectory);
            var ts = report.StartedAt.ToString("yyyyMMdd-HHmmss");

            var mdPath = Path.Combine(OutputDirectory, $"ai-benchmark-{ts}.md");
            File.WriteAllText(mdPath, GenerateMarkdown(report), Encoding.UTF8);
            Log($"Report written: {mdPath}");

            var jsonPath = Path.Combine(OutputDirectory, $"ai-benchmark-{ts}.json");
            File.WriteAllText(jsonPath, GenerateJson(report), Encoding.UTF8);
            Log($"Report written: {jsonPath}");
        }
        catch (Exception ex)
        {
            Log($"WriteReports FAILED: {ex.Message}");
        }
    }

    private static string GenerateMarkdown(AiBenchmarkRunReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# AI Benchmark Report");
        sb.AppendLine();
        sb.AppendLine($"**Run started:** {report.StartedAt:u}  ");
        sb.AppendLine($"**Run completed:** {report.CompletedAt:u}  ");
        sb.AppendLine($"**Duration:** {(report.CompletedAt - report.StartedAt).TotalMinutes:F1} minutes");
        sb.AppendLine();

        // ── Model summary table ──
        sb.AppendLine("## Model Summary");
        sb.AppendLine();
        sb.AppendLine("| Model | Target | Prompts | Succeeded | Insert Compliance | Avg Keywords | Avg Score | Avg Latency | Avg TPS |");
        sb.AppendLine("|-------|--------|---------|-----------|-------------------|--------------|-----------|-------------|---------|");

        foreach (var m in report.ModelSummaries)
        {
            sb.AppendLine(
                $"| {m.ModelAlias} | {m.ExecutionTarget} | {m.TotalPrompts} " +
                $"| {m.Succeeded}/{m.TotalPrompts} " +
                $"| {m.InsertComplianceRate:P0} ({m.InsertCompliantCount}/{m.TotalPrompts}) " +
                $"| {m.AvgKeywordScore:P0} " +
                $"| {m.AvgBaseScore:F3} " +
                $"| {m.AvgLatencySeconds:F1}s " +
                $"| {m.AvgTokensPerSecond:F1} |");
        }

        sb.AppendLine();

        // ── Per-model detail sections ──
        foreach (var m in report.ModelSummaries)
        {
            sb.AppendLine($"## {m.ModelAlias} ({m.ExecutionTarget})");
            sb.AppendLine();
            sb.AppendLine("| Prompt ID | Skill | Succeeded | Insert ✓ | Keywords | Score | Latency | TPS | Notes |");
            sb.AppendLine("|-----------|-------|-----------|----------|----------|-------|---------|-----|-------|");

            foreach (var r in m.Results)
            {
                var insertMark = r.InsertCompliant ? "✓" : $"✗ (expected={r.ExpectsInsertTag}, got={r.HasInsertButton})";
                var kwDisplay = $"{r.KeywordsFound.Length}/{r.ExpectedKeywords.Length}";
                var notes = r.Base.ErrorMessage is not null
                    ? EscapeMd(r.Base.ErrorMessage[..Math.Min(60, r.Base.ErrorMessage.Length)])
                    : r.BaseScore.Notes is { Length: > 0 }
                        ? EscapeMd(r.BaseScore.Notes[..Math.Min(80, r.BaseScore.Notes.Length)])
                        : string.Empty;

                sb.AppendLine(
                    $"| {r.Base.PromptId} " +
                    $"| {r.Base.SkillKey} " +
                    $"| {(r.Base.Succeeded ? "✓" : "✗")} " +
                    $"| {insertMark} " +
                    $"| {kwDisplay} " +
                    $"| {r.BaseScore.OverallScore:F3} " +
                    $"| {r.Base.ElapsedSeconds:F1}s " +
                    $"| {r.Base.TokensPerSecond:F1} " +
                    $"| {notes} |");
            }

            sb.AppendLine();

            // Insert compliance breakdown
            var insertExpected = m.Results.Count(r => r.ExpectsInsertTag);
            var insertProduced = m.Results.Count(r => r.ExpectsInsertTag && r.HasInsertButton);
            var chatFalsePositives = m.Results.Count(r => !r.ExpectsInsertTag && r.HasInsertButton);

            sb.AppendLine($"**Insert tag stats:** {insertProduced}/{insertExpected} document prompts produced insert button " +
                $"({chatFalsePositives} false-positive(s) on conversational prompts)");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string GenerateJson(AiBenchmarkRunReport report)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        var payload = new
        {
            startedAt = report.StartedAt,
            completedAt = report.CompletedAt,
            durationMinutes = (report.CompletedAt - report.StartedAt).TotalMinutes,
            models = report.ModelSummaries.Select(m => new
            {
                model = m.ModelAlias,
                target = m.ExecutionTarget,
                totalPrompts = m.TotalPrompts,
                succeeded = m.Succeeded,
                insertCompliance = m.InsertComplianceRate,
                avgKeywordScore = m.AvgKeywordScore,
                avgScore = m.AvgBaseScore,
                avgLatencySeconds = m.AvgLatencySeconds,
                avgTokensPerSecond = m.AvgTokensPerSecond,
                results = m.Results.Select(r => new
                {
                    promptId = r.Base.PromptId,
                    skill = r.Base.SkillKey,
                    succeeded = r.Base.Succeeded,
                    expectsInsert = r.ExpectsInsertTag,
                    hasInsertButton = r.HasInsertButton,
                    insertCompliant = r.InsertCompliant,
                    insertContentLength = r.InsertContent?.Length ?? 0,
                    insertContentPreview = r.InsertContent is { Length: > 0 }
                        ? r.InsertContent[..Math.Min(200, r.InsertContent.Length)]
                        : null,
                    keywordsExpected = r.ExpectedKeywords,
                    keywordsFound = r.KeywordsFound,
                    keywordScore = r.KeywordScore,
                    overallScore = r.BaseScore.OverallScore,
                    elapsedSeconds = r.Base.ElapsedSeconds,
                    tokensPerSecond = r.Base.TokensPerSecond,
                    estimatedOutputTokens = r.Base.EstimatedOutputTokens,
                    error = r.Base.ErrorMessage,
                    scoreNotes = r.BaseScore.Notes,
                    hardwareBadge = r.Base.HardwareBadgeTooltip,
                }),
            }),
        };

        return JsonSerializer.Serialize(payload, options);
    }

    private static string EscapeMd(string s) =>
        s.Replace("|", "\\|").Replace("\n", " ").Replace("\r", string.Empty);
}
