using System;
using System.Collections.Generic;

namespace SmrtPad.UITests.Benchmark;

/// <summary>
/// Extended result from the AI benchmark suite that augments the base
/// <see cref="BenchmarkResult"/> with insert-tag detection and keyword analysis.
/// </summary>
/// <param name="Base">The raw Appium execution result.</param>
/// <param name="ExpectsInsertTag">Whether the prompt required <c>&lt;insert&gt;</c> tags.</param>
/// <param name="HasInsertButton">Whether the "Insert" button was visible in the sidebar after the response.</param>
/// <param name="InsertContent">The text from the Insert button's HelpText UIA attribute, or <c>null</c> if not available.</param>
/// <param name="ExpectedKeywords">Keywords from the catalog that should appear in the response.</param>
/// <param name="KeywordsFound">Subset of <paramref name="ExpectedKeywords"/> that were found (case-insensitive).</param>
/// <param name="KeywordScore">Fraction of expected keywords found (0.0–1.0).</param>
/// <param name="InsertCompliant">
/// <c>true</c> when insert-tag expectation matches observed presence of the Insert button.
/// </param>
/// <param name="BaseScore">The rule-based <see cref="BenchmarkScore"/> from the standard scorer.</param>
public sealed record AiModelBenchmarkResult(
    BenchmarkResult Base,
    bool ExpectsInsertTag,
    bool HasInsertButton,
    string? InsertContent,
    string[] ExpectedKeywords,
    string[] KeywordsFound,
    double KeywordScore,
    bool InsertCompliant,
    BenchmarkScore BaseScore);

/// <summary>
/// Per-model summary statistics across all prompts.
/// </summary>
public sealed record AiModelSummary(
    string ModelAlias,
    string ExecutionTarget,
    string ReasoningTag,
    int TotalPrompts,
    int Succeeded,
    int InsertCompliantCount,
    double InsertComplianceRate,
    double AvgKeywordScore,
    double AvgLatencySeconds,
    double AvgTokensPerSecond,
    double AvgBaseScore,
    IReadOnlyList<AiModelBenchmarkResult> Results);

/// <summary>
/// Full run output from the AI benchmark suite.
/// </summary>
public sealed record AiBenchmarkRunReport(
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    IReadOnlyList<AiModelSummary> ModelSummaries);
