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
    DateTimeOffset RunTimestamp,
    int EstimatedInputTokens = 0,
    int EstimatedOutputTokens = 0,
    double ElectricityCostUsd = 0,
    long TimeToFirstTokenMs = 0,
    string ReasoningTag = "NoThink")
{
    /// <summary>Sum of input + output token estimates.</summary>
    public int TotalTokens => EstimatedInputTokens + EstimatedOutputTokens;

    /// <summary>Electricity cost per token (£/token), derived from electricity cost ÷ total tokens.</summary>
    public double TokenCostUsd => TotalTokens > 0 ? ElectricityCostUsd / TotalTokens : 0;

    /// <summary>Total cost — equals electricity cost for local inference.</summary>
    public double TotalCostUsd => ElectricityCostUsd;

    /// <summary>Generation time excluding time-to-first-token.</summary>
    public long GenerationMs => Math.Max(0, LatencyMs - TimeToFirstTokenMs);

    /// <summary>Output tokens per second based on generation time (excludes TTFT).</summary>
    public double TokensPerSecond => GenerationMs > 0 ? EstimatedOutputTokens / (GenerationMs / 1000.0) : 0;

    /// <summary>Display label that distinguishes the same model across different reasoning modes.</summary>
    public string ModelDisplayLabel => $"{ModelAlias} [{ReasoningTag}]";
}
