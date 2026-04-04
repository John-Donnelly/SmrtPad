namespace SmrtPad.AI.Benchmarks;

/// <summary>Result of running a single benchmark case.</summary>
public sealed record BenchmarkResult(
    BenchmarkCase Case,
    string RawOutput,
    string? InsertContent,
    string? ThinkContent,
    long LatencyMs,
    EvaluationScore Evaluation,
    string ModelAlias,
    string BackendTarget,
    DateTimeOffset RunTimestamp);
