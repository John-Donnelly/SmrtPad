namespace SmrtPad.AI.Benchmarks;

/// <summary>A complete benchmark run containing results for all cases.</summary>
public sealed record BenchmarkRun(
    string RunId,
    string ModelAlias,
    string BackendTarget,
    DateTimeOffset StartedAt,
    IReadOnlyList<BenchmarkResult> Results);
