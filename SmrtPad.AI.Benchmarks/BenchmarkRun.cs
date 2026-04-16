namespace SmrtPad.AI.Benchmarks;

/// <summary>A complete benchmark run containing results for all cases.</summary>
public sealed record BenchmarkRun(
    string RunId,
    string ModelAlias,
    string BackendTarget,
    DateTimeOffset StartedAt,
    IReadOnlyList<BenchmarkResult> Results,
    string ReasoningTag = "NoThink")
{
    /// <summary>
    /// Combines results from multiple runs into a single aggregated run for multi-model dashboards.
    /// Each individual result retains its own <see cref="BenchmarkResult.ModelAlias"/> and
    /// <see cref="BenchmarkResult.BackendTarget"/> for per-row filtering.
    /// </summary>
    public static BenchmarkRun Combine(string combinedRunId, IEnumerable<BenchmarkRun> runs)
    {
        var list = runs.ToList();
        var allResults = list.SelectMany(r => r.Results).ToList();
        var earliest = list.Count > 0 ? list.Min(r => r.StartedAt) : DateTimeOffset.UtcNow;
        var models = string.Join(", ", list.Select(r => r.ModelAlias).Distinct());
        var backends = string.Join("+", list.Select(r => r.BackendTarget).Distinct());
        var reasoning = string.Join(", ", list.Select(r => r.ReasoningTag).Distinct());
        return new BenchmarkRun(combinedRunId, models, backends, earliest, allResults, reasoning);
    }
}
