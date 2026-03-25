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

/// <summary>Describes the availability state for an AI backend.</summary>
public enum AIBackendAvailabilityStatus
{
    /// <summary>The backend has not yet been evaluated.</summary>
    Unknown,

    /// <summary>The backend is ready to initialize.</summary>
    Available,

    /// <summary>The backend is supported but still requires model preparation.</summary>
    InstallRequired,

    /// <summary>The backend requires package identity or registration before it can be used.</summary>
    RequiresPackageIdentity,

    /// <summary>The backend is not supported on the current system.</summary>
    Unsupported,

    /// <summary>The backend was evaluated but no compatible device was found.</summary>
    Unavailable,

    /// <summary>The backend probe failed unexpectedly.</summary>
    Error,
}

/// <summary>Captures the capability result for a single AI backend probe.</summary>
public sealed record AIBackendCapability(
    string BackendName,
    AIBackendAvailabilityStatus Status,
    string? DiagnosticCode = null,
    string? DiagnosticMessage = null)
{
    /// <summary>Whether the backend can still be selected for initialization.</summary>
    public bool IsUsable =>
        Status is AIBackendAvailabilityStatus.Available or AIBackendAvailabilityStatus.InstallRequired;
}

/// <summary>Captures the outcome of selecting the best available AI execution target.</summary>
public sealed record HardwareProbeResult(
    AIExecutionTarget SelectedTarget,
    AIBackendCapability PhiSilica,
    AIBackendCapability FoundryGpu)
{
    /// <summary>Default probe state before any detection has run.</summary>
    public static HardwareProbeResult Uninitialized { get; } = new(
        AIExecutionTarget.FoundryLocalCpu,
        new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unknown),
        new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Unknown));
}

/// <summary>Abstracts hardware capability queries for testability.</summary>
public interface IExecutionProviderCatalogAdapter
{
    /// <summary>Returns the capability result for the Phi Silica NPU path.</summary>
    Task<AIBackendCapability> ProbePhiSilicaAsync(CancellationToken ct);

    /// <summary>Returns the capability result for the Foundry Local GPU path.</summary>
    Task<AIBackendCapability> ProbeFoundryGpuAsync(CancellationToken ct);
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
    /// Detects the best available execution target and captures backend diagnostics.
    /// </summary>
    public async Task<HardwareProbeResult> DetectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var phiSilica = await _catalog.ProbePhiSilicaAsync(ct).ConfigureAwait(false);
        if (phiSilica.IsUsable)
        {
            return new HardwareProbeResult(
                AIExecutionTarget.PhiSilicaNpu,
                phiSilica,
                new AIBackendCapability("Foundry Local GPU", AIBackendAvailabilityStatus.Unknown));
        }

        ct.ThrowIfCancellationRequested();

        var foundryGpu = await _catalog.ProbeFoundryGpuAsync(ct).ConfigureAwait(false);
        if (foundryGpu.IsUsable)
        {
            return new HardwareProbeResult(AIExecutionTarget.FoundryLocalGpu, phiSilica, foundryGpu);
        }

        return new HardwareProbeResult(AIExecutionTarget.FoundryLocalCpu, phiSilica, foundryGpu);
    }
}
