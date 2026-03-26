using Microsoft.AI.Foundry.Local;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.MachineLearning;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;

namespace SmrtPad.AI;

/// <summary>
/// Production adapter that wraps the Windows App SDK
/// <see cref="ExecutionProviderCatalog"/> and <see cref="Microsoft.Windows.AI.Text.LanguageModel"/>
/// for hardware probing.
/// </summary>
internal sealed class ConcreteExecutionProviderCatalogAdapter : IExecutionProviderCatalogAdapter
{
    /// <inheritdoc/>
    public Task<AIBackendCapability> ProbePhiSilicaAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (!HasPackageIdentity())
        {
            return Task.FromResult(new AIBackendCapability(
                "Phi Silica",
                AIBackendAvailabilityStatus.RequiresPackageIdentity,
                DiagnosticCode: "PACKAGE_IDENTITY_REQUIRED",
                DiagnosticMessage: "Phi Silica requires the app to be running with registered package identity."));
        }

        try
        {
            var readyState = Microsoft.Windows.AI.Text.LanguageModel.GetReadyState();

            if (readyState == AIFeatureReadyState.Ready)
            {
                return Task.FromResult(new AIBackendCapability(
                    "Phi Silica",
                    AIBackendAvailabilityStatus.Available,
                    DiagnosticCode: readyState.ToString()));
            }

            if (readyState == AIFeatureReadyState.NotReady)
            {
                return Task.FromResult(new AIBackendCapability(
                    "Phi Silica",
                    AIBackendAvailabilityStatus.InstallRequired,
                    DiagnosticCode: readyState.ToString(),
                    DiagnosticMessage: "Phi Silica is supported but still needs model preparation."));
            }

            var availability = string.Equals(readyState.ToString(), "NotSupportedOnCurrentSystem", StringComparison.Ordinal)
                ? AIBackendAvailabilityStatus.Unsupported
                : AIBackendAvailabilityStatus.Unavailable;

            return Task.FromResult(new AIBackendCapability(
                "Phi Silica",
                availability,
                DiagnosticCode: readyState.ToString(),
                DiagnosticMessage: $"Phi Silica reported readiness state '{readyState}'."));
        }
        catch (COMException ex) when (ex.HResult == unchecked((int)0x80070490))
        {
            return Task.FromResult(new AIBackendCapability(
                "Phi Silica",
                AIBackendAvailabilityStatus.RequiresPackageIdentity,
                DiagnosticCode: $"0x{ex.HResult:X8}",
                DiagnosticMessage: "Phi Silica could not resolve the package registration required by Microsoft.Windows.Workloads."));
        }
        catch (COMException ex)
        {
            return Task.FromResult(new AIBackendCapability(
                "Phi Silica",
                AIBackendAvailabilityStatus.Error,
                DiagnosticCode: $"0x{ex.HResult:X8}",
                DiagnosticMessage: ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(new AIBackendCapability(
                "Phi Silica",
                AIBackendAvailabilityStatus.Error,
                DiagnosticCode: ex.GetType().Name,
                DiagnosticMessage: ex.Message));
        }
    }

    /// <inheritdoc/>
    public Task<AIBackendCapability> ProbeFoundryGpuAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        long vramMb = HardwareProbeService.QueryDxgiVramMb();
        long ramMb = HardwareProbeService.QueryAvailableRamMb();

        // First: Windows AI ExecutionProviderCatalog (DirectML-backed providers on some systems).
        try
        {
            var catalog = ExecutionProviderCatalog.GetDefault();
            var providers = catalog.FindAllProviders();
            if (providers.Any(p => p.ReadyState == ExecutionProviderReadyState.Ready))
            {
                return Task.FromResult(new AIBackendCapability(
                    "Foundry Local GPU",
                    AIBackendAvailabilityStatus.Available,
                    DiagnosticCode: "READY",
                    GpuVramMb: vramMb,
                    AvailableSystemRamMb: ramMb));
            }
        }
        catch (COMException) { }
        catch (InvalidOperationException) { }

        // Second: NVIDIA CUDA — nvcuda.dll in System32 is present whenever the CUDA driver
        // is installed. Foundry Local downloads its own CUDA execution provider and does not
        // rely on the Windows AI catalog, so the catalog returning no ready providers does not
        // mean CUDA is unavailable.
        if (HasCudaDriver())
        {
            // WMI fallback for VRAM when DXGI returned nothing (can happen on some NVIDIA configs)
            if (vramMb == 0)
                vramMb = HardwareProbeService.QueryWmiVramMb();

            return Task.FromResult(new AIBackendCapability(
                "Foundry Local GPU",
                AIBackendAvailabilityStatus.Available,
                DiagnosticCode: "CUDA_DRIVER",
                DiagnosticMessage: "NVIDIA CUDA driver detected.",
                GpuVramMb: vramMb,
                AvailableSystemRamMb: ramMb));
        }

        return Task.FromResult(new AIBackendCapability(
            "Foundry Local GPU",
            AIBackendAvailabilityStatus.Unavailable,
            DiagnosticCode: "NO_GPU",
            DiagnosticMessage: "No GPU found via Windows AI catalog or CUDA driver check.",
            GpuVramMb: vramMb,
            AvailableSystemRamMb: ramMb));
    }

    private static bool HasCudaDriver()
    {
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return File.Exists(Path.Combine(system32, "nvcuda.dll"));
    }

    private static bool HasPackageIdentity()
    {
        try
        {
            _ = Package.Current.Id.FullName;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

/// <summary>
/// Factory that creates a fully wired <see cref="AIDispatcher"/> with production adapters.
/// Loaded at runtime via <c>AssemblyLoadContext</c> and activated by reflection.
/// </summary>
public sealed class AIDispatcherFactory
{
    /// <summary>Creates a ready-to-use <see cref="AIDispatcher"/> with production hardware probing and model adapters.</summary>
    public AIDispatcher Create()
    {
        var catalog = new ConcreteExecutionProviderCatalogAdapter();
        var probe = new HardwareProbeService(catalog);

        return new AIDispatcher(probe, async (target, probeResult, ct) =>
        {
            if (target == AIExecutionTarget.PhiSilicaNpu)
                return await CreatePhiSilicaModelAdapterAsync(ct).ConfigureAwait(false);

            return await CreateFoundryModelAdapterAsync(target, probeResult.FoundryGpu, ct).ConfigureAwait(false);
        });
    }

    private static async Task<ILanguageModelAdapter> CreatePhiSilicaModelAdapterAsync(CancellationToken ct)
    {
        return await ConcretePhiSilicaModelAdapter.CreateAsync(ct).ConfigureAwait(false);
    }

    private static async Task<ILanguageModelAdapter> CreateFoundryModelAdapterAsync(
        AIExecutionTarget target,
        AIBackendCapability gpuCapability,
        CancellationToken ct)
    {
        var (alias, maxContextTokens) = await ModelSizeSelector
            .SelectBestAliasAsync(gpuCapability, ct)
            .ConfigureAwait(false);

        if (target == AIExecutionTarget.FoundryLocalGpu)
        {
            try
            {
                return await ConcreteFoundryModelAdapter
                    .CreateAsync(AIExecutionTarget.FoundryLocalGpu, alias, maxContextTokens, ct)
                    .ConfigureAwait(false);
            }
            catch (FoundryLocalException) { }     // GPU model failed to load
            catch (InvalidOperationException) { } // No GPU variant in catalog
        }

        return await ConcreteFoundryModelAdapter
            .CreateAsync(AIExecutionTarget.FoundryLocalCpu, alias, maxContextTokens, ct)
            .ConfigureAwait(false);
    }
}
