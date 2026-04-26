namespace SmrtPad.AI;

/// <summary>
/// Describes a single GGUF model file hosted on HuggingFace Hub.
/// </summary>
/// <param name="Repo">HuggingFace repository ID (e.g. <c>HauhauCS/Gemma-4-E2B-Uncensored-HauhauCS-Aggressive</c>).</param>
/// <param name="Filename">Filename of the GGUF file inside the repo root.</param>
/// <param name="GpuMb">Approximate file size in MB when offloading to GPU.</param>
/// <param name="ChatFamily">Chat template family key (gemma4, gemma3, llama, qwen, phi, deepseek).</param>
internal record GgufModelEntry(string Repo, string Filename, long GpuMb, string ChatFamily);

/// <summary>
/// Registry of GGUF models available for the llama.cpp runner.
/// Includes all 12 benchmark models and the two Gemma 4 variants that are not supported
/// by ORT GenAI (Gemma4ForConditionalGeneration architecture).
/// All quantizations are Q4_K_M (4-bit, good balance of quality and speed).
/// </summary>
internal static class GgufModelCatalog
{
    /// <summary>Alias for the production single model — Gemma 4 E2B (HauhauCS Q4_K_P uncensored).</summary>
    internal const string Gemma4E2BAlias = "gemma-4-e2b";

    /// <summary>Context window (tokens) for Gemma 4 E2B — balances quality and VRAM headroom.</summary>
    internal const int Gemma4E2BContextTokens = 8192;

    private static readonly IReadOnlyDictionary<string, GgufModelEntry> Entries =
        new Dictionary<string, GgufModelEntry>(StringComparer.OrdinalIgnoreCase)
        {
            // ── Gemma 4 E2B — production single model (HauhauCS Q4_K_P, ~3.45 GB) ──
            ["gemma-4-e2b"] = new(
                Repo:       "HauhauCS/Gemma-4-E2B-Uncensored-HauhauCS-Aggressive",
                Filename:   "Gemma-4-E2B-Uncensored-HauhauCS-Aggressive-Q4_K_P.gguf",
                GpuMb:      3_533,
                ChatFamily: "gemma4"),

            // E4B Q3_K_S chosen: fits in 8 GB VRAM (4,274 MB vs 8,188 MB available).
            // Q4_K_M (5,155 MB) also fits; Q3_K_S used to leave more headroom for KV cache.
            ["gemma-4-e4b"] = new(
                Repo:       "bartowski/google_gemma-4-e4b-it-GGUF",
                Filename:   "google_gemma-4-E4B-it-Q3_K_S.gguf",
                GpuMb:      4_274,
                ChatFamily: "gemma4"),

            // ── Phi ────────────────────────────────────────────────────────────────────────────
            ["phi-4-mini"] = new(
                Repo:       "unsloth/Phi-4-Mini-Instruct-GGUF",
                Filename:   "Phi-4-mini-instruct-Q4_K_M.gguf",
                GpuMb:      2_376,
                ChatFamily: "phi"),

            ["phi-4-mini-reasoning"] = new(
                Repo:       "lmstudio-community/Phi-4-Mini-Reasoning-GGUF",
                Filename:   "Phi-4-mini-reasoning-Q4_K_M.gguf",
                GpuMb:      2_376,
                ChatFamily: "phi"),

            ["phi-3.5-mini"] = new(
                Repo:       "bartowski/Phi-3.5-mini-instruct-GGUF",
                Filename:   "Phi-3.5-mini-instruct-Q4_K_M.gguf",
                GpuMb:      2_282,
                ChatFamily: "phi"),

            // ── Gemma 3 ────────────────────────────────────────────────────────────────────────
            ["gemma-3-4b"] = new(
                Repo:       "unsloth/gemma-3-4b-it-GGUF",
                Filename:   "gemma-3-4b-it-Q4_K_M.gguf",
                GpuMb:      2_375,
                ChatFamily: "gemma3"),

            ["gemma-3-1b"] = new(
                Repo:       "unsloth/gemma-3-1b-it-GGUF",
                Filename:   "gemma-3-1b-it-Q4_K_M.gguf",
                GpuMb:      769,
                ChatFamily: "gemma3"),

            // ── Llama 3.2 ──────────────────────────────────────────────────────────────────────
            ["llama-3.2-3b"] = new(
                Repo:       "bartowski/Llama-3.2-3B-Instruct-GGUF",
                Filename:   "Llama-3.2-3B-Instruct-Q4_K_M.gguf",
                GpuMb:      1_926,
                ChatFamily: "llama"),

            ["llama-3.2-1b"] = new(
                Repo:       "bartowski/Llama-3.2-1B-Instruct-GGUF",
                Filename:   "Llama-3.2-1B-Instruct-Q4_K_M.gguf",
                GpuMb:      770,
                ChatFamily: "llama"),

            // ── Qwen ───────────────────────────────────────────────────────────────────────────
            ["qwen3-1.7b"] = new(
                Repo:       "unsloth/Qwen3-1.7B-GGUF",
                Filename:   "Qwen3-1.7B-Q4_K_M.gguf",
                GpuMb:      1_056,
                ChatFamily: "qwen"),

            ["qwen3-0.6b"] = new(
                Repo:       "unsloth/Qwen3-0.6B-GGUF",
                Filename:   "Qwen3-0.6B-Q4_K_M.gguf",
                GpuMb:      378,
                ChatFamily: "qwen"),

            ["qwen2.5-0.5b"] = new(
                Repo:       "bartowski/Qwen2.5-0.5B-Instruct-GGUF",
                Filename:   "Qwen2.5-0.5B-Instruct-Q4_K_M.gguf",
                GpuMb:      379,
                ChatFamily: "qwen"),
        };

    /// <summary>Returns the GGUF catalog entry for <paramref name="alias"/>, or <c>null</c> if not registered.</summary>
    internal static GgufModelEntry? Get(string alias) =>
        Entries.TryGetValue(alias, out var e) ? e : null;

    /// <summary>Returns all registered GGUF aliases in order of descending GPU size.</summary>
    internal static IReadOnlyList<string> AllAliases =>
        Entries.OrderByDescending(kv => kv.Value.GpuMb)
               .Select(kv => kv.Key)
               .ToArray();

    /// <summary>
    /// Returns the local path where the GGUF file for <paramref name="alias"/> is (or will be) cached.
    /// Layout: <c>%LOCALAPPDATA%\SmrtPad\gguf\{alias}\{filename}</c>.
    /// </summary>
    internal static string GetLocalGgufPath(string alias)
    {
        ArgumentNullException.ThrowIfNull(alias);
        var entry = Get(alias)
            ?? throw new InvalidOperationException($"No GGUF entry for alias '{alias}'.");
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmrtPad", "gguf", alias, entry.Filename);
    }
}
