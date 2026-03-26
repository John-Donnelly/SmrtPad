using SmrtPad.AI;

namespace SmrtPad.AI.Tests;

public class HardwareProbeServiceTests
{
    private static Mock<IExecutionProviderCatalogAdapter> CreateCatalog(
        AIBackendCapability? phiSilica = null,
        AIBackendCapability? foundryGpu = null,
        Exception? phiSilicaException = null,
        Exception? foundryGpuException = null)
    {
        var mock = new Mock<IExecutionProviderCatalogAdapter>();

        if (phiSilicaException is not null)
            mock.Setup(c => c.ProbePhiSilicaAsync(It.IsAny<CancellationToken>())).ThrowsAsync(phiSilicaException);
        else
            mock.Setup(c => c.ProbePhiSilicaAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(phiSilica ?? new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unsupported));

        if (foundryGpuException is not null)
            mock.Setup(c => c.ProbeFoundryGpuAsync(It.IsAny<CancellationToken>())).ThrowsAsync(foundryGpuException);
        else
            mock.Setup(c => c.ProbeFoundryGpuAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(foundryGpu ?? new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Unavailable));

        return mock;
    }

    [Fact]
    public async Task DetectAsync_NpuAvailable_ReturnsPhiSilicaNpu()
    {
        var catalog = CreateCatalog(
            phiSilica: new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Available),
            foundryGpu: new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Available));
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.PhiSilicaNpu, result.SelectedTarget);
        Assert.Equal(AIBackendAvailabilityStatus.Available, result.PhiSilica.Status);
    }

    [Fact]
    public async Task DetectAsync_NpuUnavailable_GpuAvailable_ReturnsFoundryLocalGpu()
    {
        var catalog = CreateCatalog(
            phiSilica: new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unsupported),
            foundryGpu: new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Available));
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.FoundryLocalGpu, result.SelectedTarget);
    }

    [Fact]
    public async Task DetectAsync_NpuUnavailable_GpuUnavailable_ReturnsFoundryLocalCpu()
    {
        var catalog = CreateCatalog(
            phiSilica: new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unsupported),
            foundryGpu: new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Unavailable));
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.FoundryLocalCpu, result.SelectedTarget);
    }

    [Fact]
    public async Task DetectAsync_PhiSilicaInstallRequired_ReturnsPhiSilicaNpu()
    {
        var catalog = CreateCatalog(
            phiSilica: new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.InstallRequired),
            foundryGpu: new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Available));
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.PhiSilicaNpu, result.SelectedTarget);
    }

    [Fact]
    public async Task DetectAsync_PhiSilicaRequiresPackageIdentity_FallsBackToGpuProbe()
    {
        var catalog = CreateCatalog(
            phiSilica: new AIBackendCapability(
                "Phi Silica",
                AIBackendAvailabilityStatus.RequiresPackageIdentity,
                DiagnosticCode: "PACKAGE_IDENTITY_REQUIRED"),
            foundryGpu: new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Available));
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.FoundryLocalGpu, result.SelectedTarget);
        Assert.Equal(AIBackendAvailabilityStatus.RequiresPackageIdentity, result.PhiSilica.Status);
    }

    [Fact]
    public async Task DetectAsync_PhiSilicaProbeThrows_BubblesException()
    {
        var catalog = CreateCatalog(phiSilicaException: new InvalidOperationException("NPU error"));
        var service = new HardwareProbeService(catalog.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DetectAsync());
        Assert.Equal("NPU error", exception.Message);
    }

    [Fact]
    public async Task DetectAsync_GpuProbeThrows_BubblesException()
    {
        var catalog = CreateCatalog(
            phiSilica: new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unsupported),
            foundryGpuException: new InvalidOperationException("GPU error"));
        var service = new HardwareProbeService(catalog.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DetectAsync());
        Assert.Equal("GPU error", exception.Message);
    }

    [Fact]
    public async Task DetectAsync_CanceledBeforeNpuProbe_ThrowsOperationCanceledException()
    {
        var catalog = CreateCatalog(
            phiSilica: new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Available),
            foundryGpu: new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Available));
        var service = new HardwareProbeService(catalog.Object);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.DetectAsync(cts.Token));
    }

    [Fact]
    public async Task DetectAsync_CanceledAfterNpuProbe_ThrowsOperationCanceledException()
    {
        var cts = new CancellationTokenSource();
        var catalog = new Mock<IExecutionProviderCatalogAdapter>();
        catalog.Setup(c => c.ProbePhiSilicaAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                cts.Cancel();
                return new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unsupported);
            });
        catalog.Setup(c => c.ProbeFoundryGpuAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Available));

        var service = new HardwareProbeService(catalog.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.DetectAsync(cts.Token));
    }

    [Fact]
    public async Task DetectAsync_CalledTwice_ReturnsSameResult()
    {
        var catalog = CreateCatalog(
            phiSilica: new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unsupported),
            foundryGpu: new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Available));
        var service = new HardwareProbeService(catalog.Object);

        var result1 = await service.DetectAsync();
        var result2 = await service.DetectAsync();

        Assert.Equal(result1.SelectedTarget, result2.SelectedTarget);
        Assert.Equal(result1.FoundryGpu.Status, result2.FoundryGpu.Status);
    }

    [Fact]
    public void QueryDxgiVramMb_ReturnsNonNegativeValue()
    {
        var vram = HardwareProbeService.QueryDxgiVramMb();

        Assert.True(vram >= 0);
    }

    [Fact]
    public void QueryAvailableRamMb_ReturnsPositiveValue()
    {
        var ram = HardwareProbeService.QueryAvailableRamMb();

        Assert.True(ram > 0);
    }
}
