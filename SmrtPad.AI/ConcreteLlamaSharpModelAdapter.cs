using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace SmrtPad.AI;

/// <summary>
/// Adapts LLamaSharp (llama.cpp) for GGUF model inference on CPU and CUDA GPU.
/// Supports all GGUF-format models including Gemma 4, which is not supported by ORT GenAI.
/// </summary>
internal sealed class ConcreteLlamaSharpModelAdapter : ILanguageModelAdapter
{
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
            ContextSize = (uint)maxContextTokens,
            GpuLayerCount = gpuLayers,
            MainGpu = 0,
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
    /// Uses all layers (999) when a CUDA driver is present; 0 for pure CPU.
    /// </summary>
    private static int DetermineGpuLayerCount(string ggufPath)
    {
        // Check for NVIDIA CUDA driver (same heuristic as ConcreteOrtGenAiModelAdapter)
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        bool hasCuda = File.Exists(Path.Combine(system32, "nvcuda.dll"));
        if (!hasCuda)
            return 0;

        // Estimate model file size to decide if we can fit it fully on GPU.
        // RTX 4060 has 8188 MiB usable; leave 512 MiB headroom for activations/KV cache.
        const long GpuBudgetMb = 8188 - 512;
        long modelMb = new FileInfo(ggufPath).Length / (1024 * 1024);

        // If the model fits comfortably, offload everything (999 = all layers)
        if (modelMb <= GpuBudgetMb)
            return 999;

        // Partial offload: estimate layers proportional to budget
        // Typical: 32 layers for 1B-4B, 40 for 7B. Use a conservative 40.
        const int EstimatedLayers = 40;
        double ratio = (double)GpuBudgetMb / modelMb;
        return Math.Max(1, (int)(EstimatedLayers * ratio));
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
