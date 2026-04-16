using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Channels;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;

namespace SmrtPad.AI;

/// <summary>
/// Adapts LLamaSharp (llama.cpp) for GGUF model inference on CPU and CUDA GPU.
/// </summary>
internal sealed class ConcreteLlamaSharpModelAdapter : ILanguageModelAdapter
{
    // Runs once per process before any LLamaWeights.LoadFromFile call.
    //
    // Strategy: pre-load the CUDA runtime DLLs into the Windows loader cache in strict
    // dependency order so that when LLamaSharp later loads llama.dll (cuda12 build) the
    // OS can resolve all transitive imports without needing them on PATH or in System32.
    //
    // Dependency order (each entry depends only on those above it):
    //   cudart64_12   — standalone CUDA runtime
    //   cublasLt64_12 — standalone cuBLAS-Lt runtime
    //   cublas64_12   — depends on cublasLt64_12
    //   ggml-base     — standalone GGML base
    //   ggml-cpu      — depends on ggml-base
    //   ggml-cuda     — depends on cudart, cublas, cublasLt, ggml-base, ggml-cpu
    //   ggml          — depends on ggml-base, ggml-cpu, ggml-cuda
    //
    // After pre-loading, tell LLamaSharp to use the CUDA 12 backend via WithCuda(true).
    // LLamaSharp resolves the relative path runtimes/win-x64/native/cuda12/llama.dll
    // from AppDomain.CurrentDomain.BaseDirectory — the pre-loaded DLLs are already in
    // the loader cache so Windows finds them when it processes llama.dll's import table.
    //
    // Fallback: when CUDA is absent, WithAutoFallback(true) lets LLamaSharp pick the
    // best CPU variant (avx512 → avx2 → avx → noavx).
    static ConcreteLlamaSharpModelAdapter()
    {
    }

    internal static void ConfigureNativeLibrary(
        Action<string>? log = null,
        bool forceCpu = false,
        string? backendDirectoryOverride = null)
    {
        // Guard: NativeLibraryConfig is one-shot — calling it after the library is
        // already loaded throws.  Return early so tests that share a process don't crash.
        if (NativeLibraryConfig.LLama.LibraryHasLoaded)
        {
            log?.Invoke("NativeLibraryConfig already locked — skipping configuration.");
            return;
        }

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var configuredOverride = Environment.GetEnvironmentVariable("SMRTPAD_LLAMA_BACKEND_DIR");
        var effectiveOverride = string.IsNullOrWhiteSpace(backendDirectoryOverride) ? configuredOverride : backendDirectoryOverride;
        var cuda12Dir = ResolveCudaBackendDirectory(baseDir, effectiveOverride);
        var defaultCuda12Dir = Path.Combine(baseDir, "runtimes", "win-x64", "native", "cuda12");

        log?.Invoke($"BaseDirectory: {baseDir}");
        log?.Invoke($"Backend override: {effectiveOverride ?? "<none>"}");
        log?.Invoke($"Force CPU mode: {forceCpu}");
        log?.Invoke($"cuda12Dir exists: {Directory.Exists(cuda12Dir)}");
        log?.Invoke($"CUDA driver available: {IsCudaAvailable()}");

        var llamaCudaDll = Path.Combine(cuda12Dir, "llama.dll");
        log?.Invoke($"llama.dll exists: {File.Exists(llamaCudaDll)}");

        if (!forceCpu && IsCudaAvailable() && File.Exists(llamaCudaDll))
        {
            // Pre-load all dependencies in correct order BEFORE LLamaSharp opens llama.dll,
            // so Windows resolves its import table from the loader cache (not PATH/System32).
            PreloadCudaDependencies(cuda12Dir, defaultCuda12Dir, log);

            // WithLibrary(absolutePath) bypasses DefaultNativeLibrarySelectingPolicy entirely —
            // it uses NativeLibraryFromPath which returns this exact DLL path regardless of
            // CUDA version probes or Vulkan detection.  This is the proven-working approach
            // (confirmed 192 TPS on RTX 4060).  WithCuda(true) was NOT used because
            // LLamaSharp's SystemInfo.CudaMajorVersion probe can return -1 in xUnit hosts,
            // causing UseCuda:False and falling through to Vulkan.
            NativeLibraryConfig.All
                .WithLibrary(llamaCudaDll, null)
                .WithAutoFallback(false)
                .WithLogCallback((level, message) => log?.Invoke($"[llama.cpp/{level}] {message?.TrimEnd()}"));
        }
        else
        {
            log?.Invoke("CPU path selected (forceCpu or CUDA unavailable or backend missing).");
            // CPU path: let LLamaSharp pick the best AVX variant automatically.
            NativeLibraryConfig.All
                .WithCuda(false)
                .WithVulkan(false)
                .WithAutoFallback(true);
        }
    }

    private static string ResolveCudaBackendDirectory(string baseDir, string? backendDirectoryOverride)
    {
        if (!string.IsNullOrWhiteSpace(backendDirectoryOverride))
            return backendDirectoryOverride;

        return Path.Combine(baseDir, "runtimes", "win-x64", "native", "cuda12");
    }

    /// <summary>
    /// Pre-loads CUDA runtime DLLs and GGML support DLLs into the Windows loader cache
    /// in strict dependency order. Must be called before LLamaSharp loads llama.dll.
    /// </summary>
    /// <param name="cuda12Dir">Absolute path to the cuda12 native directory.</param>
    /// <param name="log">Optional callback to receive load status messages.</param>
    private static void PreloadCudaDependencies(string cuda12Dir, string defaultCuda12Dir, Action<string>? log = null)
    {
        // Order matters: each DLL must be in the cache before any DLL that imports it.
        // Newer modular llama.cpp builds keep runtime/plugin DLLs separate, so only preload
        // the core chain here and let ggml_backend_load_all_from_path load backend plugins.
        foreach (var (dll, allowDefaultFallback) in new (string Dll, bool AllowDefaultFallback)[]
        {
            ("libomp140.x86_64.dll", false), // OpenMP runtime used by some newer ggml builds
            ("cudart64_12.dll", true),       // CUDA runtime — no CUDA deps
            ("cublasLt64_12.dll", true),     // cuBLAS-Lt    — no CUDA deps
            ("cublas64_12.dll", true),       // cuBLAS        — imports cublasLt64_12
            ("ggml-base.dll", false),        // GGML base     — no CUDA deps
            ("ggml.dll", false),             // GGML dispatch core
        })
        {
            var fullPath = Path.Combine(cuda12Dir, dll);
            if (!File.Exists(fullPath) && allowDefaultFallback)
                fullPath = Path.Combine(defaultCuda12Dir, dll);

            if (File.Exists(fullPath))
            {
                try
                {
                    System.Runtime.InteropServices.NativeLibrary.Load(fullPath);
                    log?.Invoke($"Pre-loaded: {dll} ({fullPath})");
                }
                catch (Exception ex)
                {
                    log?.Invoke($"FAILED to pre-load {dll}: {ex.Message}");
                    throw;
                }
            }
            else
            {
                log?.Invoke($"Missing dep (skipped): {dll}");
            }
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nuint GgmlBackendLoadAllFromPathDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string backendDirectory);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GgmlBackendLoadAllDelegate();

    private static void EnsureGgmlBackendsLoaded(string backendDirectory, Action<string>? log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backendDirectory);

        var ggmlPath = Path.Combine(backendDirectory, "ggml.dll");
        if (!File.Exists(ggmlPath))
            throw new FileNotFoundException($"ggml.dll not found in backend directory: {backendDirectory}", ggmlPath);

        nint handle = NativeLibrary.Load(ggmlPath);

        if (NativeLibrary.TryGetExport(handle, "ggml_backend_load_all_from_path", out var loadFromPathExport))
        {
            var loadFromPath = Marshal.GetDelegateForFunctionPointer<GgmlBackendLoadAllFromPathDelegate>(loadFromPathExport);
            nuint loaded = loadFromPath(backendDirectory);
            log?.Invoke($"ggml backends loaded from override dir: {loaded}");
            return;
        }

        if (NativeLibrary.TryGetExport(handle, "ggml_backend_load_all", out var loadAllExport))
        {
            var loadAll = Marshal.GetDelegateForFunctionPointer<GgmlBackendLoadAllDelegate>(loadAllExport);
            loadAll();
            log?.Invoke("ggml backends loaded via ggml_backend_load_all().");
            return;
        }

        throw new InvalidOperationException("ggml backend loading API was not found in ggml.dll.");
    }

    private readonly LLamaWeights _weights;
    private readonly ModelParams _modelParams;
    private readonly int _maxContextTokens;
    private readonly string _modelAlias;
    private readonly string _chatTemplateFamily;
    private readonly ModelReasoningMode _reasoningMode;

    private ConcreteLlamaSharpModelAdapter(
        LLamaWeights weights,
        ModelParams modelParams,
        int maxContextTokens,
        string chatTemplateFamily,
        string modelAlias,
        ModelReasoningMode reasoningMode)
    {
        _weights = weights;
        _modelParams = modelParams;
        _maxContextTokens = maxContextTokens;
        _chatTemplateFamily = chatTemplateFamily;
        _modelAlias = modelAlias;
        _reasoningMode = reasoningMode;
    }

    /// <summary>
    /// Loads a GGUF model from <paramref name="ggufPath"/> using llama.cpp.
    /// GPU layer count is auto-detected from available VRAM; falls back to CPU-only when no CUDA driver is present.
    /// </summary>
    public static async Task<ConcreteLlamaSharpModelAdapter> CreateAsync(
        string ggufPath,
        int maxContextTokens,
        Action<string>? onProgress = null,
        bool forceCpu = false,
        string? backendDirectoryOverride = null,
        ModelReasoningMode reasoningMode = ModelReasoningMode.Default,
        string? modelAlias = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ggufPath);
        if (!File.Exists(ggufPath))
            throw new FileNotFoundException($"GGUF file not found: {ggufPath}", ggufPath);

        // Re-run native config with optional progress logging so benchmark failures surface
        // the backend selection and native loader details in test output.
        ConfigureNativeLibrary(onProgress, forceCpu, backendDirectoryOverride);

        if (!string.IsNullOrWhiteSpace(backendDirectoryOverride))
            EnsureGgmlBackendsLoaded(backendDirectoryOverride, onProgress);

        ct.ThrowIfCancellationRequested();
        onProgress?.Invoke("AI_STAGE_LOADING");

        var family = DetectChatTemplateFamily(ggufPath);
        if (string.Equals(family, "gemma4", StringComparison.OrdinalIgnoreCase)
            && !BackendSupportsArchitecture("gemma4", backendDirectoryOverride))
        {
            throw new NotSupportedException(
                "The currently loaded llama.cpp backend does not support Gemma 4 (missing architecture 'gemma4'). " +
                "Update/replace llama.dll with a Gemma 4-capable backend, then rerun benchmarks.");
        }

        int gpuLayers = DetermineGpuLayerCount(ggufPath, forceCpu);

        var modelParams = new ModelParams(ggufPath)
        {
            ContextSize    = (uint)maxContextTokens,
            GpuLayerCount  = gpuLayers,
            MainGpu        = 0,
            // Flash attention halves KV-cache memory bandwidth and roughly doubles TPS on GPU.
            FlashAttention = gpuLayers > 0,
            // Larger physical batch = more parallelism per GPU kernel launch.
            // 512 is a safe default for models up to 7B on 8 GiB VRAM.
            BatchSize      = 512,
            UBatchSize     = 512,
        };

        LLamaWeights weights;
        try
        {
            weights = await Task.Run(
                () => LLamaWeights.LoadFromFile(modelParams), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Failed to load model '{ggufPath}' (gpuLayers={gpuLayers}, context={maxContextTokens}, flashAttention={modelParams.FlashAttention}, batch={modelParams.BatchSize}).",
                ex);
        }

        ct.ThrowIfCancellationRequested();

        var resolvedAlias = string.IsNullOrWhiteSpace(modelAlias)
            ? ModelPromptPolicy.DetectAliasFromPath(ggufPath)
            : modelAlias;

        return new ConcreteLlamaSharpModelAdapter(weights, modelParams, maxContextTokens, family, resolvedAlias, reasoningMode);
    }

    private static bool BackendSupportsArchitecture(string architecture, string? backendDirectoryOverride)
    {
        var llamaDllPath = ResolveLlamaDllPath(backendDirectoryOverride);
        if (llamaDllPath is null || !File.Exists(llamaDllPath))
            return false;

        try
        {
            var text = System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(llamaDllPath));
            return text.Contains(architecture, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolveLlamaDllPath(string? backendDirectoryOverride)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        if (!string.IsNullOrWhiteSpace(backendDirectoryOverride))
        {
            var overridden = Path.Combine(backendDirectoryOverride, "llama.dll");
            return File.Exists(overridden) ? overridden : null;
        }

        var candidates = new[]
        {
            Path.Combine(baseDir, "runtimes", "win-x64", "native", "cuda12", "llama.dll"),
            Path.Combine(baseDir, "runtimes", "win-x64", "native", "avx2", "llama.dll"),
            Path.Combine(baseDir, "runtimes", "win-x64", "native", "avx512", "llama.dll"),
            Path.Combine(baseDir, "runtimes", "win-x64", "native", "avx", "llama.dll"),
            Path.Combine(baseDir, "runtimes", "win-x64", "native", "noavx", "llama.dll"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var formattedPrompt = ApplyChatTemplate(
            TextChunker.TruncateToTokens(prompt, _maxContextTokens));

        var channel = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

        Exception? generationException = null;

        var generationTask = Task.Run(() =>
        {
            try
            {
                using var context = _weights.CreateContext(_modelParams);
                var executor = new StatelessExecutor(_weights, _modelParams);

                var inferParams = new InferenceParams
                {
                    MaxTokens = _maxContextTokens,
                    AntiPrompts = GetAntiPrompts(_chatTemplateFamily),
                    SamplingPipeline = new DefaultSamplingPipeline(),
                };

                // InferAsync returns IAsyncEnumerable — bridge to our channel
                var tokenStream = executor.InferAsync(formattedPrompt, inferParams, CancellationToken.None);

                // We need to consume the async enumerable on this thread-pool thread.
                // Use GetAwaiter pattern for sync consumption of IAsyncEnumerable.
                ConsumeTokenStream(tokenStream, channel.Writer, ct);
            }
            catch (Exception ex)
            {
                generationException = ex;
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, CancellationToken.None);

        await foreach (var token in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return token;
        }

        await generationTask.ConfigureAwait(false);

        if (generationException is not null)
            throw new InvalidOperationException("LLamaSharp text generation failed.", generationException);
    }

    /// <inheritdoc/>
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(text);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Array.Empty<float>());
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _weights.Dispose();
        return ValueTask.CompletedTask;
    }

    // ── Chat template formatting ──────────────────────────────────────────────

    private string ApplyChatTemplate(string prompt)
    {
        var systemPrompt = ModelPromptPolicy.BuildSystemPrompt(_modelAlias, _chatTemplateFamily, _reasoningMode);
        var userPrompt = ModelPromptPolicy.ApplyPromptControls(prompt, _modelAlias, _chatTemplateFamily, _reasoningMode);

        return _chatTemplateFamily switch
        {
            "qwen3"   => $"<|im_start|>system\n{systemPrompt}<|im_end|>\n<|im_start|>user\n{userPrompt}<|im_end|>\n<|im_start|>assistant\n",
            "qwen25"  => $"<|im_start|>system\n{systemPrompt}<|im_end|>\n<|im_start|>user\n{userPrompt}<|im_end|>\n<|im_start|>assistant\n",
            "deepseek" => $"<|User|>{systemPrompt}\n\n{userPrompt}<|Assistant|>",
            "llama"    => $"<|begin_of_text|><|start_header_id|>system<|end_header_id|>\n\n{systemPrompt}<|eot_id|><|start_header_id|>user<|end_header_id|>\n\n{userPrompt}<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\n",
            "gemma3"   => $"<start_of_turn>system\n{systemPrompt}<end_of_turn>\n<start_of_turn>user\n{userPrompt}<end_of_turn>\n<start_of_turn>model\n",
            "gemma4"   => $"<start_of_turn>system\n{systemPrompt}<end_of_turn>\n<start_of_turn>user\n{userPrompt}<end_of_turn>\n<start_of_turn>model\n",
            _           => $"<|system|>\n{systemPrompt}<|end|>\n<|user|>\n{userPrompt}<|end|>\n<|assistant|>\n",
        };
    }

    private static IReadOnlyList<string> GetAntiPrompts(string family) => family switch
    {
        "qwen3"    => ["<|im_end|>", "<|endoftext|>"],
        "qwen25"   => ["<|im_end|>", "<|endoftext|>"],
        "llama"    => ["<|eot_id|>"],
        "gemma3"   => ["<end_of_turn>"],
        "gemma4"   => ["<end_of_turn>"],
        "deepseek" => ["<|User|>"],
        _          => ["<|end|>", "<|endoftext|>"],  // Phi
    };

    /// <summary>
    /// Reads the GGUF metadata or filename to determine chat template family.
    /// Falls back to inspecting the filename for well-known model names.
    /// </summary>
    private static string DetectChatTemplateFamily(string ggufPath)
    {
        try
        {
            // First: try a sibling genai_config.json if the GGUF is in a model dir
            var dir = Path.GetDirectoryName(ggufPath) ?? string.Empty;
            var configPath = Path.Combine(dir, "genai_config.json");
            if (File.Exists(configPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                if (doc.RootElement.TryGetProperty("model", out var modelProp) &&
                    modelProp.TryGetProperty("type", out var typeProp))
                {
                    var t = typeProp.GetString() ?? string.Empty;
                    if (t.Contains("qwen3", StringComparison.OrdinalIgnoreCase)) return "qwen3";
                    if (t.Contains("qwen2.5", StringComparison.OrdinalIgnoreCase) || t.Contains("qwen25", StringComparison.OrdinalIgnoreCase)) return "qwen25";
                    if (t.Contains("qwen", StringComparison.OrdinalIgnoreCase)) return "qwen25";
                    if (t.Contains("deepseek", StringComparison.OrdinalIgnoreCase)) return "deepseek";
                    if (t.Equals("llama", StringComparison.OrdinalIgnoreCase)) return "llama";
                    if (t.Contains("gemma", StringComparison.OrdinalIgnoreCase)) return "gemma3";
                }
            }
        }
        catch { }

        // Second: infer from the filename
        var name = Path.GetFileNameWithoutExtension(ggufPath)
                       .ToLowerInvariant();

        if (name.Contains("gemma-4") || name.Contains("gemma4")) return "gemma4";
        if (name.Contains("gemma-3") || name.Contains("gemma3") || name.Contains("gemma")) return "gemma3";
        if (name.Contains("llama")) return "llama";
        if (name.Contains("qwen3")) return "qwen3";
        if (name.Contains("qwen2.5") || name.Contains("qwen25")) return "qwen25";
        if (name.Contains("qwen")) return "qwen25";
        if (name.Contains("deepseek")) return "deepseek";
        if (name.Contains("mistral")) return "llama";  // Mistral uses Llama-style tokens

        return "phi";  // phi / unknown fallback
    }

    // ── GPU layer selection ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the number of transformer layers to offload to GPU.
    /// Offloads all layers (999) when CUDA is available and the model fits in VRAM;
    /// uses partial offload when the model is too large; falls back to 0 (CPU) otherwise.
    /// </summary>
    private static int DetermineGpuLayerCount(string ggufPath, bool forceCpu)
    {
        if (forceCpu || !IsCudaAvailable())
            return 0;

        // RTX 4060 has 8 188 MiB VRAM.  Reserve 1 GiB for KV-cache and activations;
        // the rest is available for model weights.
        const long GpuBudgetMb = 8188 - 1024;
        long modelMb = new FileInfo(ggufPath).Length / (1024 * 1024);

        if (modelMb <= GpuBudgetMb)
            return 999;  // all layers on GPU

        // Partial offload: scale layer count proportionally.
        // Use 40 as a conservative upper bound (covers 1B-7B models).
        double ratio = (double)GpuBudgetMb / modelMb;
        return Math.Max(1, (int)(40 * ratio));
    }

    /// <summary>Returns <c>true</c> when an NVIDIA CUDA driver is present on this machine.</summary>
    private static bool IsCudaAvailable()
    {
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return File.Exists(Path.Combine(system32, "nvcuda.dll"));
    }

    // ── Async token stream bridging ───────────────────────────────────────────

    /// <summary>
    /// Synchronously drains <paramref name="tokenStream"/> on the current thread,
    /// writing each token to <paramref name="writer"/> until completion or cancellation.
    /// LLamaSharp's <c>InferAsync</c> must be awaited; we do so with a dedicated async loop.
    /// </summary>
    private static void ConsumeTokenStream(
        IAsyncEnumerable<string> tokenStream,
        ChannelWriter<string> writer,
        CancellationToken ct)
    {
        // Run the async enumeration synchronously on the thread-pool thread via GetAwaiter/Wait.
        ConsumeAsync(tokenStream, writer, ct).GetAwaiter().GetResult();

        static async Task ConsumeAsync(
            IAsyncEnumerable<string> stream,
            ChannelWriter<string> w,
            CancellationToken cancellationToken)
        {
            await foreach (var token in stream.WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                if (!string.IsNullOrEmpty(token))
                    w.TryWrite(token);
            }
        }
    }
}
