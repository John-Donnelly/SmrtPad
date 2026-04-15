using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;

namespace SmrtPad.AI;

/// <summary>
/// Adapts LLamaSharp (llama.cpp) for GGUF model inference on CPU and CUDA GPU.
/// Supports all GGUF-format models including Gemma 4, which is not supported by ORT GenAI.
/// </summary>
internal sealed class ConcreteLlamaSharpModelAdapter : ILanguageModelAdapter
{
    // Runs once per process before any LLamaWeights.LoadFromFile call.
    //
    // Strategy: when CUDA is available, point NativeLibraryConfig directly at the
    // cuda12/llama.dll via WithLibrary (full path).  This bypasses the selecting
    // policy entirely and tells the OS loader exactly which binary to open.
    // We also call AddDllDirectory so Windows resolves ggml-cuda.dll (which llama.dll
    // depends on) from the same subfolder without needing it on PATH.
    //
    // Fallback: when CUDA is absent, WithAutoFallback(true) lets LLamaSharp pick the
    // best CPU variant (avx512 → avx2 → avx → noavx).
    static ConcreteLlamaSharpModelAdapter()
    {
        var assemblyDir = Path.GetDirectoryName(
            typeof(ConcreteLlamaSharpModelAdapter).Assembly.Location) ?? string.Empty;

        if (IsCudaAvailable())
        {
            var cuda12Dir = Path.Combine(assemblyDir, "runtimes", "win-x64", "native", "cuda12");
            var llamaCuda = Path.Combine(cuda12Dir, "llama.dll");

            if (File.Exists(llamaCuda))
            {
                // NativeLibrary.Load with an absolute path uses LOAD_WITH_ALTERED_SEARCH_PATH
                // which ignores SetDllDirectory.  Pre-load every dependency in dependency order
                // so Windows finds them in the loader cache before ggml.dll's static imports
                // are resolved.  This sidesteps all DLL search path issues.
                foreach (var dep in new[]
                {
                    "cudart64_12.dll", "cublas64_12.dll", "cublasLt64_12.dll",  // CUDA runtime
                    "ggml-base.dll", "ggml-cpu.dll",                              // GGML base
                    "ggml-cuda.dll",                                               // CUDA backend
                    "ggml.dll",                                                    // GGML dispatcher
                })
                {
                    var depPath = Path.Combine(cuda12Dir, dep);
                    if (File.Exists(depPath))
                        System.Runtime.InteropServices.NativeLibrary.Load(depPath);
                }

                NativeLibraryConfig.All
                    .WithLibrary(llamaCuda, null)
                    .WithAutoFallback(false);
                return;
            }
        }

        // CPU path: let LLamaSharp pick the best AVX variant automatically.
        var cpuDir = Path.Combine(assemblyDir, "runtimes", "win-x64", "native");
        NativeLibraryConfig.All
            .WithCuda(false)
            .WithSearchDirectory(cpuDir)
            .WithAutoFallback(true);
    }

    private readonly LLamaWeights _weights;
    private readonly ModelParams _modelParams;
    private readonly int _maxContextTokens;
    private readonly string _chatTemplateFamily;

    private ConcreteLlamaSharpModelAdapter(
        LLamaWeights weights,
        ModelParams modelParams,
        int maxContextTokens,
        string chatTemplateFamily)
    {
        _weights = weights;
        _modelParams = modelParams;
        _maxContextTokens = maxContextTokens;
        _chatTemplateFamily = chatTemplateFamily;
    }

    /// <summary>
    /// Loads a GGUF model from <paramref name="ggufPath"/> using llama.cpp.
    /// GPU layer count is auto-detected from available VRAM; falls back to CPU-only when no CUDA driver is present.
    /// </summary>
    public static async Task<ConcreteLlamaSharpModelAdapter> CreateAsync(
        string ggufPath,
        int maxContextTokens,
        Action<string>? onProgress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ggufPath);
        if (!File.Exists(ggufPath))
            throw new FileNotFoundException($"GGUF file not found: {ggufPath}", ggufPath);

        ct.ThrowIfCancellationRequested();
        onProgress?.Invoke("AI_STAGE_LOADING");

        int gpuLayers = DetermineGpuLayerCount(ggufPath);

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

        var weights = await Task.Run(
            () => LLamaWeights.LoadFromFile(modelParams), ct).ConfigureAwait(false);

        ct.ThrowIfCancellationRequested();

        var family = DetectChatTemplateFamily(ggufPath);
        return new ConcreteLlamaSharpModelAdapter(weights, modelParams, maxContextTokens, family);
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

    private string ApplyChatTemplate(string prompt) => _chatTemplateFamily switch
    {
        "qwen"     => $"<|im_start|>user\n{prompt}<|im_end|>\n<|im_start|>assistant\n",
        "deepseek" => $"<|User|>{prompt}<|Assistant|>",
        "llama"    => $"<|begin_of_text|><|start_header_id|>user<|end_header_id|>\n\n{prompt}<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\n",
        "gemma3"   => $"<start_of_turn>user\n{prompt}<end_of_turn>\n<start_of_turn>model\n",
        "gemma4"   => $"<start_of_turn>user\n{prompt}<end_of_turn>\n<start_of_turn>model\n",
        _          => $"<|user|>\n{prompt}<|end|>\n<|assistant|>\n",   // Phi default
    };

    private static IReadOnlyList<string> GetAntiPrompts(string family) => family switch
    {
        "qwen"     => ["<|im_end|>", "<|endoftext|>"],
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
                    if (t.Contains("qwen", StringComparison.OrdinalIgnoreCase)) return "qwen";
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
        if (name.Contains("qwen")) return "qwen";
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
    private static int DetermineGpuLayerCount(string ggufPath)
    {
        if (!IsCudaAvailable())
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
