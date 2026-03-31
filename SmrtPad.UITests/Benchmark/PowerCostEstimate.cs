namespace SmrtPad.UITests.Benchmark;

/// <summary>
/// Estimated power cost for a single benchmark result, calibrated to the local hardware.
/// </summary>
/// <param name="PromptId">Correlates back to <see cref="BenchmarkPrompt.Id"/>.</param>
/// <param name="ModelAlias">Model that produced the result.</param>
/// <param name="ExecutionTarget">GPU, CPU, or NPU.</param>
/// <param name="WattsUsed">Estimated power draw in watts during inference.</param>
/// <param name="EnergyWhPerRequest">Energy consumed in watt-hours for this single request.</param>
/// <param name="EstimatedCostUsd">Estimated electricity cost in USD.</param>
/// <param name="ElectricityRatePerKwh">Rate used for cost calculation ($/kWh).</param>
public sealed record PowerCostEstimate(
    string PromptId,
    string ModelAlias,
    string ExecutionTarget,
    double WattsUsed,
    double EnergyWhPerRequest,
    double EstimatedCostUsd,
    double ElectricityRatePerKwh);
