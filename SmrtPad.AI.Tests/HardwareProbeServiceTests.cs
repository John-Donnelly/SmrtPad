using SmrtPad.AI;

namespace SmrtPad.AI.Tests;

public class HardwareProbeServiceTests
{
    private static Mock<IExecutionProviderCatalogAdapter> CreateCatalog(
        bool? npuAvailable = null,
        bool? gpuAvailable = null,
        Exception? npuException = null,
        Exception? gpuException = null)
    {
        var mock = new Mock<IExecutionProviderCatalogAdapter>();

        if (npuException is not null)
            mock.Setup(c => c.IsNpuAvailableAsync(It.IsAny<CancellationToken>())).ThrowsAsync(npuException);
        else
            mock.Setup(c => c.IsNpuAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(npuAvailable ?? false);

        if (gpuException is not null)
            mock.Setup(c => c.IsGpuAvailableAsync(It.IsAny<CancellationToken>())).ThrowsAsync(gpuException);
        else
            mock.Setup(c => c.IsGpuAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(gpuAvailable ?? false);

        return mock;
    }

    [Fact]
    public async Task DetectAsync_NpuAvailable_ReturnsPhiSilicaNpu()
    {
        var catalog = CreateCatalog(npuAvailable: true, gpuAvailable: true);
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.PhiSilicaNpu, result);
    }

    [Fact]
    public async Task DetectAsync_NpuUnavailable_GpuAvailable_ReturnsFoundryLocalGpu()
    {
        var catalog = CreateCatalog(npuAvailable: false, gpuAvailable: true);
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.FoundryLocalGpu, result);
    }

    [Fact]
    public async Task DetectAsync_NpuUnavailable_GpuUnavailable_ReturnsFoundryLocalCpu()
    {
        var catalog = CreateCatalog(npuAvailable: false, gpuAvailable: false);
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.FoundryLocalCpu, result);
    }

    [Fact]
    public async Task DetectAsync_NpuProbeThrows_FallsBackToGpuProbe()
    {
        var catalog = CreateCatalog(npuException: new InvalidOperationException("NPU error"), gpuAvailable: true);
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.FoundryLocalGpu, result);
    }

    [Fact]
    public async Task DetectAsync_NpuProbeThrows_GpuUnavailable_ReturnsFoundryLocalCpu()
    {
        var catalog = CreateCatalog(npuException: new InvalidOperationException("NPU error"), gpuAvailable: false);
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.FoundryLocalCpu, result);
    }

    [Fact]
    public async Task DetectAsync_NpuProbeThrows_GpuProbeThrows_ReturnsFoundryLocalCpu()
    {
        var catalog = CreateCatalog(
            npuException: new InvalidOperationException("NPU error"),
            gpuException: new InvalidOperationException("GPU error"));
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.FoundryLocalCpu, result);
    }

    [Fact]
    public async Task DetectAsync_GpuProbeThrows_ReturnsFoundryLocalCpu()
    {
        var catalog = CreateCatalog(npuAvailable: false, gpuException: new InvalidOperationException("GPU error"));
        var service = new HardwareProbeService(catalog.Object);

        var result = await service.DetectAsync();

        Assert.Equal(AIExecutionTarget.FoundryLocalCpu, result);
    }

    [Fact]
    public async Task DetectAsync_CanceledBeforeNpuProbe_ThrowsOperationCanceledException()
    {
        var catalog = CreateCatalog(npuAvailable: true, gpuAvailable: true);
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
        catalog.Setup(c => c.IsNpuAvailableAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                cts.Cancel(); // Cancel after NPU probe returns false
                return false;
            });
        catalog.Setup(c => c.IsGpuAvailableAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var service = new HardwareProbeService(catalog.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(() => service.DetectAsync(cts.Token));
    }

    [Fact]
    public async Task DetectAsync_CalledTwice_ReturnsSameResult()
    {
        var catalog = CreateCatalog(npuAvailable: false, gpuAvailable: true);
        var service = new HardwareProbeService(catalog.Object);

        var result1 = await service.DetectAsync();
        var result2 = await service.DetectAsync();

        Assert.Equal(result1, result2);
    }
}
