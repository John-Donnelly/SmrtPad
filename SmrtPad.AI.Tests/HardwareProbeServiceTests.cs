using SmrtPad.AI;

namespace SmrtPad.AI.Tests;

public class HardwareProbeServiceTests
{
    private static Mock<IExecutionProviderCatalogAdapter> CreateCatalog(
        AIBackendCapability? phiSilica = null,
        AIBackendCapability? gpu = null,
        Exception? phiSilicaException = null,
        Exception? gpuException = null)
    {
        var mock = new Mock<IExecutionProviderCatalogAdapter>();

        if (phiSilicaException is not null)
            mock.Setup(c => c.ProbePhiSilicaAsync(It.IsAny<CancellationToken>())).ThrowsAsync(phiSilicaException);
        else
            mock.Setup(c => c.ProbePhiSilicaAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(phiSilica ?? new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unsupported));

        if (gpuException is not null)
            mock.Setup(c => c.ProbeOnnxRuntimeGpuAsync(It.IsAny<CancellationToken>())).ThrowsAsync(gpuException);
        else
            mock.Setup(c => c.ProbeOnnxRuntimeGpuAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(gpu ?? new AIBackendCapability("ORT GenAI GPU", AIBackendAvailabilityStatus.Unavailable));

        return mock;
    }

    [Fact]
    public async Task DetectAsync_NpuAvailable_ReturnsPhiSilicaNpu()
    {
        var catalog = CreateCatalog(
            phiSilica: new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Available),
            gpu: new AIBackendCapability("ORT GenAI GPU", AIBackendAvailabilityStatus.Available));
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.PhiSilicaNpu, result.SelectedTarget);
        Assert.Equal(AIBackendAvailabilityStatus.Available, result.PhiSilica.Status);
    }

    [Fact]
    public async Task DetectAsync_NpuUnavailable_GpuAvailable_ReturnsOnnxRuntimeGpu()
    {
        var catalog = CreateCatalog(
            phiSilica: new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unsupported),
            gpu: new AIBackendCapability("ORT GenAI GPU", AIBackendAvailabilityStatus.Available));
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.OnnxRuntimeGpu, result.SelectedTarget);
    }

    [Fact]
    public async Task DetectAsync_NpuUnavailable_GpuUnavailable_ReturnsOnnxRuntimeCpu()
    {
        var catalog = CreateCatalog(
            phiSilica: new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unsupported),
            gpu: new AIBackendCapability("ORT GenAI GPU", AIBackendAvailabilityStatus.Unavailable));
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.OnnxRuntimeCpu, result.SelectedTarget);
    }

    [Fact]
    public async Task DetectAsync_PhiSilicaInstallRequired_ReturnsPhiSilicaNpu()
    {
        var catalog = CreateCatalog(
            phiSilica: new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.InstallRequired),
            gpu: new AIBackendCapability("ORT GenAI GPU", AIBackendAvailabilityStatus.Available));
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
            gpu: new AIBackendCapability("ORT GenAI GPU", AIBackendAvailabilityStatus.Available));
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.OnnxRuntimeGpu, result.SelectedTarget);
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
            gpuException: new InvalidOperationException("GPU error"));
        var service = new HardwareProbeService(catalog.Object);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DetectAsync());
        Assert.Equal("GPU error", exception.Message);
    }

    [Fact]
    public async Task DetectAsync_CanceledBeforeNpuProbe_ThrowsOperationCanceledException()
    {
        var catalog = CreateCatalog(
            phiSilica: new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Available),
            gpu: new AIBackendCapability("ORT GenAI GPU", AIBackendAvailabilityStatus.Available));
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
        catalog.Setup(c => c.ProbeOnnxRuntimeGpuAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AIBackendCapability("ORT GenAI GPU", AIBackendAvailabilityStatus.Available));

        var service = new HardwareProbeService(catalog.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.DetectAsync(cts.Token));
    }

    [Fact]
    public async Task DetectAsync_CalledTwice_ReturnsSameResult()
    {
        var catalog = CreateCatalog(
            phiSilica: new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unsupported),
            gpu: new AIBackendCapability("ORT GenAI GPU", AIBackendAvailabilityStatus.Available));
        var service = new HardwareProbeService(catalog.Object);

        var result1 = await service.DetectAsync();
        var result2 = await service.DetectAsync();

        Assert.Equal(result1.SelectedTarget, result2.SelectedTarget);
        Assert.Equal(result1.Gpu.Status, result2.Gpu.Status);
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
