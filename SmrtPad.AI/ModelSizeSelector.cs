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

    // GPU headroom: model file must fit in VRAM with 1.25x margin (20% headroom).
    private const double GpuHeadroomFactor = 1.25;
    // CPU headroom: model file must fit in system RAM with 1.5x margin (33% headroom).
    private const double CpuHeadroomFactor = 1.50;

    /// <summary>
    /// Ordered from largest (most capable) to smallest (most compatible).
    /// Alias names match Foundry Local catalog identifiers.
    /// Approximate VRAM/RAM footprint in MB per alias.
    /// </summary>
    private static readonly (string Alias, long FootprintMb)[] PreferredAliases =
    [
        ("phi-4-mini-reasoning", 5_000),
        ("phi-3.5-mini",         2_500),
        ("phi-3-mini",       2_000),
        ("qwen2.5-1.5b",     1_200),
        ("qwen2.5-0.5b",       600),
    ];

    /// <summary>
    /// Selects the best alias and max context tokens given the probed GPU capability.
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

        foreach (var (alias, footprintMb) in PreferredAliases)
        {
            if (IsAliasEligible(footprintMb, budgetMb, headroom))
            {
                int ctx = PickContextTokens(footprintMb, budgetMb, headroom);
                return Task.FromResult((alias, ctx));
            }
        }

        return Task.FromResult((FallbackAlias, BaseContextTokens));
    }

    /// <summary>Returns true when the hardware budget can accommodate the model with the required headroom.</summary>
    internal static bool IsAliasEligible(long footprintMb, long budgetMb, double headroomFactor)
        => budgetMb > 0 && budgetMb >= (long)(footprintMb * headroomFactor);

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
}
