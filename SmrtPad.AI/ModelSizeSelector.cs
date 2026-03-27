namespace SmrtPad.AI;

/// <summary>
/// Selects the best local model alias and an appropriate context-token limit
/// based on the hardware capabilities reported by <see cref="AIBackendCapability"/>.
/// </summary>
internal static class ModelSizeSelector
{
    internal const string FallbackAlias = "phi-3.5-mini";
    private const int MinContextTokens = 512;
    private const int MaxContextTokens = 16384;
    private const int BaseContextTokens = 2048;

    // Show a model if the hardware budget covers at least 90% of its footprint (~10% leeway).
    private const double GpuHeadroomFactor = 0.90;
    private const double CpuHeadroomFactor = 0.90;

    /// <summary>
    /// Ordered from largest (most capable) to smallest (most compatible).
    /// Alias names match Foundry Local catalog identifiers.
    /// <c>GpuMb</c> = CUDA execution provider variant <c>fileSizeMb</c>.
    /// <c>CpuMb</c> = CPU execution provider variant <c>fileSizeMb</c>.
    /// Sizes taken from <c>foundry.modelinfo.json</c>.
    /// Models without a GPU variant (e.g. qwen3-0.6b) are excluded to avoid
    /// download failures when the GPU path is selected.
    /// </summary>
    private static readonly (string Alias, long GpuMb, long CpuMb)[] PreferredAliases =
    [
        //                         alias                  GPU       CPU
        ("deepseek-r1-14b",     10_065,  11_786),
        ("gpt-oss-20b",          9_882,  12_552),
        ("qwen2.5-14b",          9_000,  11_325),
        ("qwen2.5-coder-14b",    9_000,  11_325),
        ("phi-4",                8_570,  10_403),
        ("deepseek-r1-7b",       5_406,   6_584),
        ("qwen2.5-7b",           4_843,   6_307),
        ("qwen2.5-coder-7b",     4_843,   6_307),
        ("mistral-7b-v0.2",      4_075,   4_167),
        ("phi-4-mini",           3_686,   4_915),
        ("phi-4-mini-reasoning", 3_225,   4_628),
        ("phi-3.5-mini",         2_181,   2_590),
        ("phi-3-mini-128k",      2_181,   2_600),
        ("phi-3-mini-4k",        2_181,   2_590),
        ("qwen2.5-coder-1.5b",   1_280,   1_822),
        ("qwen2.5-1.5b",         1_280,   1_822),
        ("qwen2.5-coder-0.5b",     528,     822),
        ("qwen2.5-0.5b",           528,     822),
    ];

    /// <summary>
    /// Selects the best alias and max context tokens for the given hardware capability.
    /// Uses GPU VRAM when available; otherwise uses system RAM and CPU footprints.
    /// Falls back to <see cref="FallbackAlias"/> if no alias fits the hardware budget.
    /// </summary>
    public static Task<(string Alias, int MaxContextTokens)> SelectBestAliasAsync(
        AIBackendCapability capability,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(capability);

        bool isGpu = capability.GpuVramMb > 0;
        long budgetMb = isGpu ? capability.GpuVramMb : capability.AvailableSystemRamMb;
        double headroom = isGpu ? GpuHeadroomFactor : CpuHeadroomFactor;

        foreach (var (alias, gpuMb, cpuMb) in PreferredAliases)
        {
            long footprintMb = isGpu ? gpuMb : cpuMb;
            if (IsAliasEligible(footprintMb, budgetMb, headroom))
            {
                int ctx = PickContextTokens(footprintMb, budgetMb, headroom);
                return Task.FromResult((alias, ctx));
            }
        }

        return Task.FromResult((FallbackAlias, BaseContextTokens));
    }

    /// <summary>
    /// Returns aliases ordered best-first that fit within the hardware budget.
    /// Uses GPU VRAM when available; otherwise uses system RAM and CPU footprints.
    /// Returns all known aliases when no hardware data is available (pre-init).
    /// </summary>
    internal static IReadOnlyList<string> GetEligibleAliases(AIBackendCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        bool isGpu = capability.GpuVramMb > 0;
        long budgetMb = isGpu ? capability.GpuVramMb : capability.AvailableSystemRamMb;
        double headroom = isGpu ? GpuHeadroomFactor : CpuHeadroomFactor;

        // If no hardware data yet, return all aliases so the user can still pick
        if (budgetMb <= 0)
            return PreferredAliases.Select(static p => p.Alias).ToArray();

        return PreferredAliases
            .Where(p => IsAliasEligible(isGpu ? p.GpuMb : p.CpuMb, budgetMb, headroom))
            .Select(static p => p.Alias)
            .ToArray();
    }

    /// <summary>Returns true when the hardware budget can accommodate the model with the required headroom.</summary>
    internal static bool IsAliasEligible(long footprintMb, long budgetMb, double headroomFactor)
        => budgetMb > 0 && budgetMb >= (long)(footprintMb * headroomFactor);

    /// <summary>
    /// Looks up a known alias by name and returns its GPU and CPU footprints in MB.
    /// Returns <c>false</c> if the alias is not in <see cref="PreferredAliases"/>.
    /// </summary>
    internal static bool TryGetAlias(string alias, out long gpuMb, out long cpuMb)
    {
        foreach (var (a, g, c) in PreferredAliases)
        {
            if (string.Equals(a, alias, StringComparison.OrdinalIgnoreCase))
            {
                gpuMb = g;
                cpuMb = c;
                return true;
            }
        }
        gpuMb = 0;
        cpuMb = 0;
        return false;
    }

    /// <summary>Returns the context token count for a user-forced alias (uses base context budget as a safe default).</summary>
    internal static int PickContextTokens(long footprintMb) => BaseContextTokens;

    /// <summary>
    /// Scales context tokens proportionally to available headroom.
    /// More headroom → longer context, clamped to [<see cref="MinContextTokens"/>, <see cref="MaxContextTokens"/>].
    /// </summary>
    internal static int PickContextTokens(long footprintMb, long budgetMb, double headroomFactor)
    {
        if (footprintMb <= 0 || budgetMb <= 0)
            return BaseContextTokens;

        double actualRatio = (double)budgetMb / footprintMb;
        double scale = actualRatio / headroomFactor;
        int tokens = (int)(BaseContextTokens * scale);
        return Math.Clamp(tokens, MinContextTokens, MaxContextTokens);
    }

    /// <summary>
    /// Returns the alias of the best model that fits the given hardware capability,
    /// or <see cref="FallbackAlias"/> when no model fits.
    /// </summary>
    internal static string GetBestAliasForCapability(AIBackendCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        bool isGpu = capability.GpuVramMb > 0;
        long budgetMb = isGpu ? capability.GpuVramMb : capability.AvailableSystemRamMb;
        double headroom = isGpu ? GpuHeadroomFactor : CpuHeadroomFactor;

        foreach (var (alias, gpuMb, cpuMb) in PreferredAliases)
        {
            long footprintMb = isGpu ? gpuMb : cpuMb;
            if (IsAliasEligible(footprintMb, budgetMb, headroom))
                return alias;
        }

        return FallbackAlias;
    }
}

