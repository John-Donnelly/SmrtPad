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

        try
        {
            var catalog = ExecutionProviderCatalog.GetDefault();
            var providers = catalog.FindAllProviders();
            return Task.FromResult(providers.Any(p => p.ReadyState == ExecutionProviderReadyState.Ready)
                ? new AIBackendCapability(
                    "Foundry Local GPU",
                    AIBackendAvailabilityStatus.Available,
                    DiagnosticCode: "READY")
                : new AIBackendCapability(
                    "Foundry Local GPU",
                    AIBackendAvailabilityStatus.Unavailable,
                    DiagnosticCode: "NO_READY_PROVIDER",
                    DiagnosticMessage: "No ready GPU execution provider was reported by Windows AI."));
        }
        catch (COMException ex)
        {
            return Task.FromResult(new AIBackendCapability(
                "Foundry Local GPU",
                AIBackendAvailabilityStatus.Error,
                DiagnosticCode: $"0x{ex.HResult:X8}",
                DiagnosticMessage: ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(new AIBackendCapability(
                "Foundry Local GPU",
                AIBackendAvailabilityStatus.Error,
                DiagnosticCode: ex.GetType().Name,
                DiagnosticMessage: ex.Message));
        }
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

        return new AIDispatcher(probe, CreateModelAdapterAsync);
    }

    private static Task<ILanguageModelAdapter> CreateModelAdapterAsync(AIExecutionTarget target, CancellationToken ct)
    {
        return target switch
        {
            AIExecutionTarget.PhiSilicaNpu => CreatePhiSilicaModelAdapterAsync(ct),
            AIExecutionTarget.FoundryLocalGpu or AIExecutionTarget.FoundryLocalCpu =>
                CreateFoundryModelAdapterAsync(target, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported AI execution target.")
        };
    }

    private static async Task<ILanguageModelAdapter> CreatePhiSilicaModelAdapterAsync(CancellationToken ct)
    {
        return await ConcretePhiSilicaModelAdapter.CreateAsync(ct).ConfigureAwait(false);
    }

    private static async Task<ILanguageModelAdapter> CreateFoundryModelAdapterAsync(AIExecutionTarget target, CancellationToken ct)
    {
        return await ConcreteFoundryModelAdapter.CreateAsync(target, ct).ConfigureAwait(false);
    }
}
