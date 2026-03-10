namespace SmrtPad.AI;

/// <summary>Describes the AI execution backend selected by hardware probing.</summary>
public enum AIExecutionTarget
{
    /// <summary>On-device NPU via Phi Silica (Copilot+ PCs).</summary>
    PhiSilicaNpu,

    /// <summary>Local GPU via Foundry Local SDK.</summary>
    FoundryLocalGpu,

    /// <summary>Local CPU via Foundry Local SDK (fallback).</summary>
    FoundryLocalCpu,
}

/// <summary>Abstracts hardware capability queries for testability.</summary>
public interface IExecutionProviderCatalogAdapter
{
    /// <summary>Returns <see langword="true"/> when an NPU with Phi Silica support is available.</summary>
    Task<bool> IsNpuAvailableAsync(CancellationToken ct);

    /// <summary>Returns <see langword="true"/> when a compatible GPU is available.</summary>
    Task<bool> IsGpuAvailableAsync(CancellationToken ct);
}

/// <summary>
/// Probes local hardware to determine the best AI execution target.
/// Priority: NPU → GPU → CPU.
/// </summary>
public sealed class HardwareProbeService
{
    private readonly IExecutionProviderCatalogAdapter _catalog;

    public HardwareProbeService(IExecutionProviderCatalogAdapter catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _catalog = catalog;
    }

    /// <summary>
    /// Detects the best available execution target.
    /// Returns <see cref="AIExecutionTarget.PhiSilicaNpu"/> if NPU is available,
    /// <see cref="AIExecutionTarget.FoundryLocalGpu"/> if GPU is available,
    /// otherwise <see cref="AIExecutionTarget.FoundryLocalCpu"/>.
    /// </summary>
    public async Task<AIExecutionTarget> DetectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Probe NPU
        try
        {
            if (await _catalog.IsNpuAvailableAsync(ct).ConfigureAwait(false))
            {
                return AIExecutionTarget.PhiSilicaNpu;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // NPU probe failed — fall through to GPU
        }

        ct.ThrowIfCancellationRequested();

        // Probe GPU
        try
        {
            if (await _catalog.IsGpuAvailableAsync(ct).ConfigureAwait(false))
            {
                return AIExecutionTarget.FoundryLocalGpu;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // GPU probe failed — fall through to CPU
        }

        return AIExecutionTarget.FoundryLocalCpu;
    }
}
