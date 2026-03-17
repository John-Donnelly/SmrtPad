using Microsoft.Windows.AI;
using Microsoft.Windows.AI.MachineLearning;

namespace SmrtPad.AI;

/// <summary>
/// Production adapter that wraps the Windows App SDK
/// <see cref="ExecutionProviderCatalog"/> and <see cref="Microsoft.Windows.AI.Text.LanguageModel"/>
/// for hardware probing.
/// </summary>
internal sealed class ConcreteExecutionProviderCatalogAdapter : IExecutionProviderCatalogAdapter
{
    /// <inheritdoc/>
    public Task<bool> IsNpuAvailableAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var readyState = Microsoft.Windows.AI.Text.LanguageModel.GetReadyState();
            return Task.FromResult(readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady);
        }
        catch (Exception ex) when (ex.HResult == unchecked((int)0x80070490))
        {
            // 0x80070490 = ERROR_NOT_FOUND: Microsoft.Windows.Workloads.dll cannot resolve
            // the app's package installation path because the package is not registered in
            // the Windows package store.  This is a deployment gap, not an NPU absence.
            // Fix: stop the app, run  SmrtPad (Package)\deploy.ps1  to register the loose
            // MSIX, then restart the debug session.
            return Task.FromResult(false);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public Task<bool> IsGpuAvailableAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var catalog = ExecutionProviderCatalog.GetDefault();
            var providers = catalog.FindAllProviders();
            return Task.FromResult(providers.Any(p => p.ReadyState == ExecutionProviderReadyState.Ready));
        }
        catch
        {
            return Task.FromResult(false);
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

    private static async Task<ILanguageModelAdapter> CreateModelAdapterAsync(AIExecutionTarget target)
    {
        return target switch
        {
            AIExecutionTarget.PhiSilicaNpu => await ConcretePhiSilicaModelAdapter.CreateAsync().ConfigureAwait(false),
            AIExecutionTarget.FoundryLocalGpu or AIExecutionTarget.FoundryLocalCpu =>
                await ConcreteFoundryModelAdapter.CreateAsync(target).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported AI execution target.")
        };
    }
}
