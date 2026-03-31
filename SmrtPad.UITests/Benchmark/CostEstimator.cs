using System;
using System.Globalization;

namespace SmrtPad.UITests.Benchmark;

/// <summary>
/// Estimates the electricity cost of a single inference request based on hardware
/// power draw and wall-clock inference time. Calibrated to local hardware via
/// environment variables with sensible defaults for the development workstation:
/// <list type="bullet">
///   <item>BENCHMARK_GPU_WATTS — GPU power draw (default: 115W for RTX 4060)</item>
///   <item>BENCHMARK_CPU_WATTS — CPU power draw (default: 105W for Ryzen 7 5800X)</item>
///   <item>BENCHMARK_NPU_WATTS — NPU power draw (default: 15W for integrated NPU)</item>
///   <item>BENCHMARK_ELECTRICITY_RATE — USD per kWh (default: 0.12)</item>
/// </list>
/// </summary>
public sealed class CostEstimator
{
    private readonly double _gpuWatts;
    private readonly double _cpuWatts;
    private readonly double _npuWatts;
    private readonly double _electricityRate;

    public CostEstimator()
    {
        _gpuWatts = ReadEnvDouble("BENCHMARK_GPU_WATTS", 115.0);
        _cpuWatts = ReadEnvDouble("BENCHMARK_CPU_WATTS", 105.0);
        _npuWatts = ReadEnvDouble("BENCHMARK_NPU_WATTS", 15.0);
        _electricityRate = ReadEnvDouble("BENCHMARK_ELECTRICITY_RATE", 0.12);
    }

    /// <summary>
    /// Estimates the power cost for a single benchmark result.
    /// </summary>
    public PowerCostEstimate Estimate(BenchmarkResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var watts = result.ExecutionTarget.ToUpperInvariant() switch
        {
            "GPU" => _gpuWatts,
            "CPU" => _cpuWatts,
            "NPU" => _npuWatts,
            _ => _gpuWatts, // default to GPU
        };

        // Energy = Power × Time
        // Convert elapsed seconds to hours for kWh calculation
        var elapsedHours = result.ElapsedSeconds / 3600.0;
        var energyWh = watts * elapsedHours;
        var energyKwh = energyWh / 1000.0;
        var costUsd = energyKwh * _electricityRate;

        return new PowerCostEstimate(
            PromptId: result.PromptId,
            ModelAlias: result.ModelAlias,
            ExecutionTarget: result.ExecutionTarget,
            WattsUsed: watts,
            EnergyWhPerRequest: Math.Round(energyWh, 6),
            EstimatedCostUsd: Math.Round(costUsd, 8),
            ElectricityRatePerKwh: _electricityRate);
    }

    /// <summary>
    /// Returns a summary of the configured hardware power profile.
    /// </summary>
    public string GetHardwareProfile() =>
        $"GPU: {_gpuWatts}W | CPU: {_cpuWatts}W | NPU: {_npuWatts}W | Rate: ${_electricityRate}/kWh";

    private static double ReadEnvDouble(string name, double defaultValue)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return !string.IsNullOrWhiteSpace(raw)
            && double.TryParse(raw, CultureInfo.InvariantCulture, out var value)
                ? value
                : defaultValue;
    }
}
