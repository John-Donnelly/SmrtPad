namespace SmrtPad.AI;

/// <summary>HuggingFace Hub source information for an ONNX GenAI model variant.</summary>
/// <param name="Repo">HuggingFace repository ID (e.g. <c>microsoft/Phi-4-mini-instruct-onnx</c>).</param>
/// <param name="GpuSubdir">Subdirectory inside the repo containing the CUDA/GPU variant files, or <c>null</c> if unavailable.</param>
/// <param name="CpuSubdir">Subdirectory inside the repo containing the CPU variant files, or <c>null</c> if unavailable.</param>
internal record HuggingFaceModelInfo(string Repo, string? GpuSubdir, string? CpuSubdir);

/// <summary>
/// Selects the best local model alias and an appropriate context-token limit
/// based on the hardware capabilities reported by <see cref="AIBackendCapability"/>.
/// </summary>
internal static class ModelSizeSelector
{
    internal const string FallbackAlias = "phi-4-mini";
    private const int MinContextTokens = 512;
    private const int MaxContextTokens = 16384;
    private const int BaseContextTokens = 2048;

    /// <summary>
    /// Sentinel for <see cref="PreferredAliases"/> <c>GpuMb</c>: model has no GPU ONNX variant.
    /// </summary>
    internal const long CpuOnly = -1;

    /// <summary>
    /// Sentinel for <see cref="PreferredAliases"/> <c>CpuMb</c>: model has no CPU ONNX variant.
    /// </summary>
    internal const long GpuOnly = -1;

    // A model is eligible when its footprint × (1/0.9) ≤ budget,
    // i.e. the model occupies at most 90% of available memory, leaving ≥10% overhead free.
    private const double HeadroomFactor = 1.0 / 0.9;

    /// <summary>
    /// Ordered from largest (most capable) to smallest (most compatible).
    /// <c>GpuMb</c> = CUDA int-4 variant file size in MB (or <see cref="CpuOnly"/> when no GPU variant exists).
    /// <c>CpuMb</c> = CPU int-4 variant file size in MB (or <see cref="GpuOnly"/> when no CPU variant exists).
    /// Sizes measured from published HuggingFace ONNX repos.
    /// </summary>
    private static readonly (string Alias, long GpuMb, long CpuMb)[] PreferredAliases =
    [
        //                              alias                  GPU       CPU
        ("phi-4",                     8_570,  10_403),
        ("deepseek-r1-7b",            5_406,   6_584),
        ("qwen2.5-7b",                4_843,   6_307),
        ("qwen2.5-coder-7b",          4_843,   6_307),
        // Benchmark models — measured from downloaded ORT GenAI int-4 ONNX files
        ("phi-4-mini",                3_276,   4_702),
        ("phi-4-mini-reasoning",      3_276,   4_702),
        ("gemma-3-4b",                2_608, CpuOnly),   // GPU only: MiCkSoftware flat ONNX GenAI repo
        ("llama-3.2-3b",              2_516,   3_491),
        ("phi-3.5-mini",              2_214,   2_590),
        ("phi-3-mini-128k",           2_181,   2_600),
        ("phi-3-mini-4k",             2_181,   2_590),
        ("qwen3-1.7b",                1_542, CpuOnly),   // GPU only: colli-ai flat ONNX GenAI repo
        ("llama-3.2-1b",              1_189, CpuOnly),   // GPU only: no CPU int-4 ONNX variant downloaded
        ("deepseek-r1-1.5b",          1_464, GpuOnly),   // GPU only: no CPU int-4 ONNX variant published
        ("qwen2.5-coder-1.5b",        1_280,   1_822),
        ("qwen2.5-1.5b",              1_280,   1_822),
        ("gemma-3-1b",                  699, CpuOnly),   // GPU only: MiCkSoftware flat ONNX GenAI repo
        ("qwen3-0.6b",              CpuOnly,     387),   // CPU only: no GPU ONNX variant downloaded
        ("qwen2.5-coder-0.5b",          528,     822),
        ("qwen2.5-0.5b",                544,     822),
        ("ernie-4.5-0.3b",              320,     490),
    ];

    /// <summary>
    /// HuggingFace Hub source information keyed by alias.
    /// Aliases without an entry have no auto-download support; models must be placed manually
    /// in the local cache directory returned by <see cref="ModelDownloadService.GetLocalModelDirectory"/>.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, HuggingFaceModelInfo> HuggingFaceInfos =
        new Dictionary<string, HuggingFaceModelInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["phi-4"]               = new("microsoft/phi-4-onnx",
                                         GpuSubdir: "cuda-int4-rtn-block-32",
                                         CpuSubdir: "cpu-int4-rtn-block-32-acc-level-4"),
            ["phi-4-mini"]          = new("microsoft/Phi-4-mini-instruct-onnx",
                                         GpuSubdir: "cuda-int4-rtn-block-32",
                                         CpuSubdir: "cpu-int4-rtn-block-32-acc-level-4"),
            ["phi-4-mini-reasoning"]= new("microsoft/Phi-4-mini-reasoning-onnx",
                                         GpuSubdir: "cuda-int4-rtn-block-32",
                                         CpuSubdir: "cpu-int4-rtn-block-32-acc-level-4"),
            ["phi-3.5-mini"]        = new("microsoft/Phi-3.5-mini-instruct-onnx",
                                         GpuSubdir: "cuda/cuda-int4-rtn-block-32",
                                         CpuSubdir: "cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4"),
            ["phi-3-mini-128k"]     = new("microsoft/Phi-3-mini-128k-instruct-onnx",
                                         GpuSubdir: "cuda/cuda-int4-rtn-block-32",
                                         CpuSubdir: "cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4"),
            ["phi-3-mini-4k"]       = new("microsoft/Phi-3-mini-4k-instruct-onnx",
                                         GpuSubdir: "cuda/cuda-int4-rtn-block-32",
                                         CpuSubdir: "cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4"),
            // Benchmark models sourced from HuggingFace in ORT GenAI int-4 format
            ["llama-3.2-3b"]        = new("onnx-community/Llama-3.2-3B-Instruct-GENAI-ONNX",
                                         GpuSubdir: "cuda/cuda-int4-rtn-block-32",
                                         CpuSubdir: "cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4"),
            ["llama-3.2-1b"]        = new("onnx-community/Llama-3.2-1B-Instruct-GENAI-ONNX",
                                         GpuSubdir: "cuda/cuda-int4-rtn-block-32",
                                         CpuSubdir: "cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4"),
            // Flat repos (no subdirectory; files are at root of the HuggingFace repo)
            ["gemma-3-4b"]          = new("MiCkSoftware/gemma-3-4b-it-abliterated-onnx-genai-int4-rtn-block-32-acc-level-4-20260213-233056",
                                         GpuSubdir: null,
                                         CpuSubdir: null),
            ["gemma-3-1b"]          = new("MiCkSoftware/gemma-3-1b-it-abliterated-onnx-genai-int4-rtn-block-32-acc-level-4-20260213-230409",
                                         GpuSubdir: null,
                                         CpuSubdir: null),
            ["qwen3-1.7b"]          = new("colli-ai/Qwen-1.7B-ONNX-genai-cuda-int4",
                                         GpuSubdir: null,
                                         CpuSubdir: null),
            ["qwen3-0.6b"]          = new("xiaoyao9184/Qwen3-0.6B-onnx-genai",
                                         GpuSubdir: "cuda/cuda-int4-rtn-block-32",
                                         CpuSubdir: "cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4"),
        };

    /// <summary>
    /// Returns the HuggingFace Hub source information for <paramref name="alias"/>,
    /// or <c>null</c> when no auto-download source is configured.
    /// </summary>
    internal static HuggingFaceModelInfo? GetHuggingFaceInfo(string alias) =>
        HuggingFaceInfos.TryGetValue(alias, out var info) ? info : null;

    /// <summary>
    /// Selects the best alias and max context tokens for the given hardware capability.
    /// Uses GPU VRAM when available; otherwise uses system RAM and CPU footprints.
    /// Falls back to <see cref="FallbackAlias"/> if no alias fits the hardware budget.
    /// </summary>
    public static Task<(string Alias, int MaxContextTokens)> SelectBestAliasAsync(
        AIBackendCapability capability,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(capability);

        bool isGpu = capability.GpuVramMb > 0;
        long budgetMb = isGpu ? capability.GpuVramMb : capability.AvailableSystemRamMb;

        foreach (var (alias, gpuMb, cpuMb) in PreferredAliases)
        {
            if (isGpu && gpuMb == CpuOnly) continue;   // no GPU execution provider
            if (!isGpu && cpuMb == GpuOnly) continue;  // no CPU execution provider
            long footprintMb = isGpu ? gpuMb : cpuMb;
            if (IsAliasEligible(footprintMb, budgetMb))
            {
                int ctx = PickContextTokens(footprintMb, budgetMb);
                return Task.FromResult((alias, ctx));
            }
        }

        return Task.FromResult((FallbackAlias, BaseContextTokens));
    }

    /// <summary>
    /// Returns aliases ordered best-first that fit within the hardware budget.
    /// Uses GPU VRAM when available; otherwise uses system RAM and CPU footprints.
    /// Returns all known aliases when no hardware data is available (pre-init).
    /// </summary>
    internal static IReadOnlyList<string> GetEligibleAliases(AIBackendCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        bool isGpu = capability.GpuVramMb > 0;
        long budgetMb = isGpu ? capability.GpuVramMb : capability.AvailableSystemRamMb;

        // If no hardware data yet, return all aliases so the user can still pick
        if (budgetMb <= 0)
            return PreferredAliases.Select(static p => p.Alias).ToArray();

        return PreferredAliases
            .Where(p => !(isGpu && p.GpuMb == CpuOnly)
                     && !(!isGpu && p.CpuMb == GpuOnly)
                     && IsAliasEligible(isGpu ? p.GpuMb : p.CpuMb, budgetMb))
            .Select(static p => p.Alias)
            .ToArray();
    }

    /// <summary>
    /// Returns <c>true</c> when the model footprint fits within the hardware budget
    /// with at least 10% overhead remaining (<c>footprint × 1.10 ≤ budget</c>).
    /// </summary>
    internal static bool IsAliasEligible(long footprintMb, long budgetMb)
        => budgetMb > 0 && (long)(footprintMb * HeadroomFactor) <= budgetMb;

    /// <summary>
    /// Looks up a known alias by name and returns its GPU and CPU footprints in MB.
    /// Returns <c>false</c> if the alias is not in <see cref="PreferredAliases"/>.
    /// </summary>
    internal static bool TryGetAlias(string alias, out long gpuMb, out long cpuMb)
    {
        foreach (var (a, g, c) in PreferredAliases)
        {
            if (string.Equals(a, alias, StringComparison.OrdinalIgnoreCase))
            {
                gpuMb = g;
                cpuMb = c;
                return true;
            }
        }
        gpuMb = 0;
        cpuMb = 0;
        return false;
    }

    /// <summary>
    /// Returns the context token count for a user-forced alias.
    /// Uses <see cref="MaxContextTokens"/> because the user explicitly chose the model
    /// and we should not silently truncate their input.
    /// </summary>
    internal static int PickContextTokens(long footprintMb) => MaxContextTokens;

    /// <summary>
    /// Scales context tokens proportionally to available headroom above the minimum required budget.
    /// More headroom → longer context, clamped to [<see cref="MinContextTokens"/>, <see cref="MaxContextTokens"/>].
    /// </summary>
    internal static int PickContextTokens(long footprintMb, long budgetMb)
    {
        if (footprintMb <= 0 || budgetMb <= 0)
            return BaseContextTokens;

        // scale = 1.0 when budget == footprint * HeadroomFactor (minimum eligible budget)
        double actualRatio = (double)budgetMb / footprintMb;
        double scale = actualRatio / HeadroomFactor;
        int tokens = (int)(BaseContextTokens * scale);
        return Math.Clamp(tokens, MinContextTokens, MaxContextTokens);
    }

    /// <summary>
    /// Returns the alias of the best model that fits the given hardware capability,
    /// or <see cref="FallbackAlias"/> when no model fits.
    /// </summary>
    internal static string GetBestAliasForCapability(AIBackendCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        bool isGpu = capability.GpuVramMb > 0;
        long budgetMb = isGpu ? capability.GpuVramMb : capability.AvailableSystemRamMb;

        foreach (var (alias, gpuMb, cpuMb) in PreferredAliases)
        {
            if (isGpu && gpuMb == CpuOnly) continue;   // no GPU execution provider
            if (!isGpu && cpuMb == GpuOnly) continue;  // no CPU execution provider
            long footprintMb = isGpu ? gpuMb : cpuMb;
            if (IsAliasEligible(footprintMb, budgetMb))
                return alias;
        }

        return FallbackAlias;
    }
}

