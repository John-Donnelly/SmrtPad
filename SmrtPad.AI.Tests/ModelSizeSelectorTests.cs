using SmrtPad.AI;

namespace SmrtPad.AI.Tests;

public class ModelSizeSelectorTests
{
    // ── IsAliasEligible ─────────────────────────────────────────────────────

    [Fact]
    public void IsAliasEligible_BudgetExceedsHeadroomThreshold_ReturnsTrue()
    {
        // 2500 MB model × 1.25 = 3125 MB required; budget 4000 MB → eligible
        Assert.True(ModelSizeSelector.IsAliasEligible(2_500, 4_000, 1.25));
    }

    [Fact]
    public void IsAliasEligible_BudgetBelowHeadroomThreshold_ReturnsFalse()
    {
        // 2500 MB model × 1.25 = 3125 MB required; budget 3000 MB → not eligible
        Assert.False(ModelSizeSelector.IsAliasEligible(2_500, 3_000, 1.25));
    }

    [Fact]
    public void IsAliasEligible_ZeroBudget_ReturnsFalse()
    {
        Assert.False(ModelSizeSelector.IsAliasEligible(2_500, 0, 1.25));
    }

    // ── PickContextTokens ───────────────────────────────────────────────────

    [Fact]
    public void PickContextTokens_HeadroomRatioAtMinimum_ReturnsBaseTokens()
    {
        // budget = footprint × headroom → ratio == headroomFactor → scale = 1.0 → base tokens
        int tokens = ModelSizeSelector.PickContextTokens(2_500, 3_125, 1.25);

        Assert.Equal(2048, tokens);
    }

    [Fact]
    public void PickContextTokens_DoubleHeadroom_CapsAtTwiceBase()
    {
        // budget = footprint × headroom × 2 → scale = 2.0 → 2 × 2048 = 4096
        int tokens = ModelSizeSelector.PickContextTokens(2_500, 6_250, 1.25);

        Assert.Equal(4096, tokens);
    }

    [Fact]
    public void PickContextTokens_ExcessiveBudget_ClampsToMaxContextTokens()
    {
        // scale would exceed 2.0 but clamp keeps it at max
        int tokens = ModelSizeSelector.PickContextTokens(500, 100_000, 1.25);

        Assert.Equal(16384, tokens);
    }

    // ── SelectBestAliasAsync ────────────────────────────────────────────────

    [Fact]
    public async Task SelectBestAliasAsync_ZeroVramAndZeroRam_ReturnsFallbackAlias()
    {
        var capability = new AIBackendCapability(
            "Foundry Local GPU",
            AIBackendAvailabilityStatus.Unavailable,
            GpuVramMb: 0,
            AvailableSystemRamMb: 0);

        var (alias, _) = await ModelSizeSelector.SelectBestAliasAsync(capability);

        Assert.Equal(ModelSizeSelector.FallbackAlias, alias);
    }

    [Fact]
    public async Task SelectBestAliasAsync_HighVram_ReturnsLargestEligibleAlias()
    {
        // 8 GB VRAM → phi-4-mini (5000 MB × 1.25 = 6250 MB required) fits
        var capability = new AIBackendCapability(
            "Foundry Local GPU",
            AIBackendAvailabilityStatus.Available,
            GpuVramMb: 8_000,
            AvailableSystemRamMb: 16_000);

        var (alias, _) = await ModelSizeSelector.SelectBestAliasAsync(capability);

        Assert.Equal("phi-4-mini-reasoning", alias);
    }

    [Fact]
    public async Task SelectBestAliasAsync_LowVram_SkipsLargeModels()
    {
        // 2 GB VRAM → phi-4-mini (6250 MB) and phi-3.5-mini (3125 MB) don't fit;
        // phi-3-mini (2000 × 1.25 = 2500 MB) fits in 2048 MB? No.
        // qwen2.5-1.5b (1200 × 1.25 = 1500 MB) fits in 2048 MB? Yes.
        var capability = new AIBackendCapability(
            "Foundry Local GPU",
            AIBackendAvailabilityStatus.Available,
            GpuVramMb: 2_048,
            AvailableSystemRamMb: 16_000);

        var (alias, _) = await ModelSizeSelector.SelectBestAliasAsync(capability);

        Assert.Equal("qwen2.5-1.5b", alias);
    }

    [Fact]
    public async Task SelectBestAliasAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        var capability = new AIBackendCapability(
            "Foundry Local GPU",
            AIBackendAvailabilityStatus.Available,
            GpuVramMb: 8_000,
            AvailableSystemRamMb: 16_000);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => ModelSizeSelector.SelectBestAliasAsync(capability, cts.Token));
    }
}
