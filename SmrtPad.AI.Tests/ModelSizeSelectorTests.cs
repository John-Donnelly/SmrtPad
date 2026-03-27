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
        // 2500 MB × 1.10 = 2750 MB; budget exactly 2750 MB → eligible (boundary)
        Assert.True(ModelSizeSelector.IsAliasEligible(2_500, 2_750));
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
        // budget = 2500 × 1.10 = 2750 → scale = 1.0 → base tokens
        int tokens = ModelSizeSelector.PickContextTokens(2_500, 2_750);

        Assert.Equal(2048, tokens);
    }

    [Fact]
    public void PickContextTokens_DoubleMinimumBudget_ReturnsTwiceBaseTokens()
    {
        // budget = 2500 × 2.20 = 5500 → scale = 2.0 → 2 × 2048 = 4096
        int tokens = ModelSizeSelector.PickContextTokens(2_500, 5_500);

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
        //   deepseek-r1-14b: 10065 × 1.10 = 11071.5 > 8192 ✗
        //   gpt-oss-20b:      9882 × 1.10 = 10870.2 > 8192 ✗
        //   qwen2.5-14b:      9000 × 1.10 =  9900.0 > 8192 ✗
        //   qwen2.5-coder-14b:9000 × 1.10 =  9900.0 > 8192 ✗
        //   phi-4:            8570 × 1.10 =  9427.0 > 8192 ✗
        //   deepseek-r1-7b:   5406 × 1.10 =  5946.6 ≤ 8192 ✓ → best fit
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
        //   deepseek-r1-14b: 10065 × 1.10 = 11071.5 ≤ 12288 ✓ → best fit
        var capability = new AIBackendCapability(
            "Foundry Local GPU",
            AIBackendAvailabilityStatus.Available,
            GpuVramMb: 12_288,
            AvailableSystemRamMb: 32_000);

        var (alias, _) = await ModelSizeSelector.SelectBestAliasAsync(capability);

        Assert.Equal("deepseek-r1-14b", alias);
    }

    [Fact]
    public async Task SelectBestAliasAsync_2GbVram_SkipsLargeModels()
    {
        // With 2 GB VRAM (2048 MB):
        //   phi-3.5-mini:       2181 × 1.10 = 2399.1 > 2048 ✗
        //   qwen2.5-coder-1.5b: 1280 × 1.10 = 1408.0 ≤ 2048 ✓ → best fit
        var capability = new AIBackendCapability(
            "Foundry Local GPU",
            AIBackendAvailabilityStatus.Available,
            GpuVramMb: 2_048,
            AvailableSystemRamMb: 16_000);

        var (alias, _) = await ModelSizeSelector.SelectBestAliasAsync(capability);

        Assert.Equal("qwen2.5-coder-1.5b", alias);
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
