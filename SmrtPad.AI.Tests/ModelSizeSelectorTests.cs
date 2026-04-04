using SmrtPad.AI;

namespace SmrtPad.AI.Tests;

public class ModelSizeSelectorTests
{
    // ── IsAliasEligible ─────────────────────────────────────────────────────
    // Rule: footprint × 1.10 ≤ budget  (model must fit with ≥10% overhead free)

    [Fact]
    public void IsAliasEligible_FootprintPlusTenPercentFitsInBudget_ReturnsTrue()
    {
        // 2500 MB × 1.10 = 2750 MB required; budget 4000 MB → eligible
        Assert.True(ModelSizeSelector.IsAliasEligible(2_500, 4_000));
    }

    [Fact]
    public void IsAliasEligible_FootprintPlusTenPercentExceedsBudget_ReturnsFalse()
    {
        // 2500 MB × 1.10 = 2750 MB required; budget 2600 MB → not eligible
        Assert.False(ModelSizeSelector.IsAliasEligible(2_500, 2_600));
    }

    [Fact]
    public void IsAliasEligible_FootprintExactlyFitsWithTenPercentOverhead_ReturnsTrue()
    {
        // HeadroomFactor = 1/0.9 = 1.111…
        // 2700 × (1/0.9) = 3000.0 exactly; (long)(3000) = 3000 ≤ 3000 → eligible (boundary)
        Assert.True(ModelSizeSelector.IsAliasEligible(2_700, 3_000));
    }

    [Fact]
    public void IsAliasEligible_ZeroBudget_ReturnsFalse()
    {
        Assert.False(ModelSizeSelector.IsAliasEligible(2_500, 0));
    }

    // ── PickContextTokens ───────────────────────────────────────────────────
    // scale = 1.0 when budget == footprint × 1.10 (minimum eligible budget)

    [Fact]
    public void PickContextTokens_AtMinimumEligibleBudget_ReturnsBaseTokens()
    {
        // HeadroomFactor = 1/0.9 = 1.111…; minimum budget for footprint 2700 = 2700/0.9 = 3000
        // scale = (3000/2700) ÷ (1/0.9) = (10/9) ÷ (10/9) = 1.0 → base tokens
        int tokens = ModelSizeSelector.PickContextTokens(2_700, 3_000);

        Assert.Equal(2048, tokens);
    }

    [Fact]
    public void PickContextTokens_DoubleMinimumBudget_ReturnsTwiceBaseTokens()
    {
        // minimum budget for footprint 2700 = 3000; double = 6000
        // scale = (6000/2700) ÷ (1/0.9) = (20/9) ÷ (10/9) = 2.0 → 2 × 2048 = 4096
        int tokens = ModelSizeSelector.PickContextTokens(2_700, 6_000);

        Assert.Equal(4096, tokens);
    }

    [Fact]
    public void PickContextTokens_ExcessiveBudget_ClampsToMaxContextTokens()
    {
        int tokens = ModelSizeSelector.PickContextTokens(500, 100_000);

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
    public async Task SelectBestAliasAsync_8GbVram_ReturnsDeepSeekR1_7b()
    {
        // With 8 GB VRAM (8192 MB):
        //   phi-4:          8570 × 1.10 = 9427.0 > 8192 ✗
        //   deepseek-r1-7b: 5406 × 1.10 = 5946.6 ≤ 8192 ✓ → best fit
        var capability = new AIBackendCapability(
            "Foundry Local GPU",
            AIBackendAvailabilityStatus.Available,
            GpuVramMb: 8_192,
            AvailableSystemRamMb: 16_000);

        var (alias, _) = await ModelSizeSelector.SelectBestAliasAsync(capability);

        Assert.Equal("deepseek-r1-7b", alias);
    }

    [Fact]
    public async Task SelectBestAliasAsync_12GbVram_ReturnsPhi4()
    {
        // With 12 GB VRAM (12288 MB):
        //   phi-4: 8570 × 1.10 = 9427.0 ≤ 12288 ✓ → best fit
        var capability = new AIBackendCapability(
            "Foundry Local GPU",
            AIBackendAvailabilityStatus.Available,
            GpuVramMb: 12_288,
            AvailableSystemRamMb: 32_000);

        var (alias, _) = await ModelSizeSelector.SelectBestAliasAsync(capability);

        Assert.Equal("phi-4", alias);
    }

    [Fact]
    public async Task SelectBestAliasAsync_2GbVram_SkipsLargeModels()
    {
        // With 2 GB VRAM (2048 MB); HeadroomFactor = 1/0.9 = 1.111:
        //   phi-3.5-mini:       2181 × 1.111 = 2423.4 > 2048 ✗
        //   phi-3-mini-128k:    2181 × 1.111 = 2423.4 > 2048 ✗
        //   phi-3-mini-4k:      2181 × 1.111 = 2423.4 > 2048 ✗
        //   deepseek-r1-1.5b:   1464 × 1.111 = 1626.5 ≤ 2048 ✓ → best fit
        var capability = new AIBackendCapability(
            "Foundry Local GPU",
            AIBackendAvailabilityStatus.Available,
            GpuVramMb: 2_048,
            AvailableSystemRamMb: 16_000);

        var (alias, _) = await ModelSizeSelector.SelectBestAliasAsync(capability);

        Assert.Equal("deepseek-r1-1.5b", alias);
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
