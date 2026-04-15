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
    public Task<AIBackendCapability> ProbeOnnxRuntimeGpuAsync(CancellationToken ct)
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
                    "ORT GenAI GPU",
                    AIBackendAvailabilityStatus.Available,
                    DiagnosticCode: "READY",
                    GpuVramMb: vramMb,
                    AvailableSystemRamMb: ramMb));
            }
        }
        catch (COMException) { }
        catch (InvalidOperationException) { }

        // Second: NVIDIA CUDA — nvcuda.dll in System32 is present whenever the CUDA driver
        // is installed. ORT GenAI uses its own CUDA execution provider and does not rely on
        // the Windows AI catalog, so the catalog returning no ready providers does not mean
        // CUDA is unavailable.
        if (HasCudaDriver())
        {
            // WMI fallback for VRAM when DXGI returned nothing (can happen on some NVIDIA configs)
            if (vramMb == 0)
                vramMb = HardwareProbeService.QueryWmiVramMb();

            return Task.FromResult(new AIBackendCapability(
                "ORT GenAI GPU",
                AIBackendAvailabilityStatus.Available,
                DiagnosticCode: "CUDA_DRIVER",
                DiagnosticMessage: "NVIDIA CUDA driver detected.",
                GpuVramMb: vramMb,
                AvailableSystemRamMb: ramMb));
        }

        return Task.FromResult(new AIBackendCapability(
            "ORT GenAI GPU",
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

        AIDispatcher? dispatcher = null;
        dispatcher = new AIDispatcher(probe, async (target, probeResult, onProgress, ct) =>
        {
            if (target == AIExecutionTarget.PhiSilicaNpu)
                return await CreatePhiSilicaModelAdapterAsync(ct).ConfigureAwait(false);

            return await CreateOrtGenAiModelAdapterAsync(target, probeResult.Gpu, dispatcher!.PreferredAlias, onProgress, ct).ConfigureAwait(false);
        });
        return dispatcher;
    }

    /// <summary>
    /// Creates an <see cref="AIDispatcher"/> that loads a model directly from
    /// <paramref name="modelPath"/> bypassing the alias/download pipeline.
    /// <list type="bullet">
    ///   <item>If <paramref name="modelPath"/> is a <c>.gguf</c> file, the llama.cpp (LLamaSharp) runner is used.</item>
    ///   <item>If <paramref name="modelPath"/> is a directory containing <c>genai_config.json</c>, the ORT GenAI runner is used.</item>
    /// </list>
    /// Intended for benchmarking local model directories and GGUF files.
    /// </summary>
    /// <param name="modelPath">
    /// Absolute path to either a <c>.gguf</c> file or a directory containing <c>genai_config.json</c> + <c>model.onnx</c>.
    /// </param>
    /// <param name="maxContextTokens">Maximum context window size in tokens.</param>
    public static AIDispatcher CreateFromLocalPath(string modelPath, int maxContextTokens = 4096)
    {
        ArgumentNullException.ThrowIfNull(modelPath);

        bool isGguf = modelPath.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase)
                   && File.Exists(modelPath);

        if (!isGguf && !File.Exists(Path.Combine(modelPath, "genai_config.json")))
            throw new ArgumentException(
                $"Path must be a .gguf file or a directory containing genai_config.json: {modelPath}",
                nameof(modelPath));

        var stubCatalog = new StubCatalogAdapter();
        var probe = new HardwareProbeService(stubCatalog);

        if (isGguf)
        {
            return new AIDispatcher(probe, async (_, _, onProgress, ct) =>
                await ConcreteLlamaSharpModelAdapter
                    .CreateAsync(modelPath, maxContextTokens, onProgress, ct)
                    .ConfigureAwait(false));
        }

        return new AIDispatcher(probe, async (_, _, onProgress, ct) =>
            await ConcreteOrtGenAiModelAdapter
                .CreateAsync(modelPath, maxContextTokens, onProgress, ct)
                .ConfigureAwait(false));
    }

    private static async Task<ILanguageModelAdapter> CreatePhiSilicaModelAdapterAsync(CancellationToken ct)
    {
        return await ConcretePhiSilicaModelAdapter.CreateAsync(ct).ConfigureAwait(false);
    }

    private static async Task<ILanguageModelAdapter> CreateOrtGenAiModelAdapterAsync(
        AIExecutionTarget target,
        AIBackendCapability gpuCapability,
        string? preferredAlias,
        Action<string>? onProgress,
        CancellationToken ct)
    {
        bool isGpu = target == AIExecutionTarget.OnnxRuntimeGpu;

        // Build the ordered list of aliases to try.
        IReadOnlyList<string> aliases;
        if (preferredAlias is not null && ModelSizeSelector.TryGetAlias(preferredAlias, out _, out _))
        {
            aliases = [preferredAlias];
        }
        else
        {
            // When the CPU target is forced, evaluate model sizes against system RAM, not VRAM.
            var capabilityForSelection = isGpu ? gpuCapability : gpuCapability with { GpuVramMb = 0 };
            aliases = ModelSizeSelector.GetEligibleAliases(capabilityForSelection);
            if (aliases.Count == 0)
                aliases = [ModelSizeSelector.FallbackAlias];
        }

        // Try GPU first; if loading fails for every alias fall through to CPU.
        if (isGpu)
        {
            var gpuAdapter = await TryLoadAliasesAsync(aliases, isGpu: true, onProgress, ct)
                .ConfigureAwait(false);
            if (gpuAdapter is not null)
                return gpuAdapter;

            // GPU variants unavailable — fall back to CPU.
        }

        var cpuAdapter = await TryLoadAliasesAsync(aliases, isGpu: false, onProgress, ct)
            .ConfigureAwait(false);
        if (cpuAdapter is not null)
            return cpuAdapter;

        throw new InvalidOperationException(
            "No eligible ORT GenAI model could be loaded for the current hardware configuration. " +
            "Ensure model files are present in the SmrtPad local model cache or have a registered HuggingFace source.");
    }

    private static async Task<ILanguageModelAdapter?> TryLoadAliasesAsync(
        IReadOnlyList<string> aliases,
        bool isGpu,
        Action<string>? onProgress,
        CancellationToken ct)
    {
        foreach (var alias in aliases)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (!ModelSizeSelector.TryGetAlias(alias, out var gpuMb, out var cpuMb))
                    continue;

                // Skip if this execution path has no variant for the alias.
                long footprintMb = isGpu ? gpuMb : cpuMb;
                if (footprintMb == ModelSizeSelector.CpuOnly || footprintMb == ModelSizeSelector.GpuOnly)
                    continue;

                int maxContextTokens = ModelSizeSelector.PickContextTokens(footprintMb);

                var modelDir = await ModelDownloadService
                    .EnsureModelAsync(alias, isGpu, onProgress, ct)
                    .ConfigureAwait(false);

                return await ConcreteOrtGenAiModelAdapter
                    .CreateAsync(modelDir, maxContextTokens, onProgress, ct)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // This alias/variant failed — try the next one.
            }
        }

        return null;
    }

    /// <summary>
    /// Minimal <see cref="IExecutionProviderCatalogAdapter"/> that reports no NPU and no GPU.
    /// Used by <see cref="CreateFromLocalPath"/> where the model factory loads directly from
    /// a known path and hardware probing is not needed.
    /// </summary>
    private sealed class StubCatalogAdapter : IExecutionProviderCatalogAdapter
    {
        public Task<AIBackendCapability> ProbePhiSilicaAsync(CancellationToken ct) =>
            Task.FromResult(new AIBackendCapability("Phi Silica", AIBackendAvailabilityStatus.Unsupported));

        public Task<AIBackendCapability> ProbeOnnxRuntimeGpuAsync(CancellationToken ct) =>
            Task.FromResult(new AIBackendCapability("ORT GenAI GPU", AIBackendAvailabilityStatus.Unavailable));
    }
}
