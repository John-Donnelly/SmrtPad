using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace SmrtPad.AI;

/// <summary>
/// Adapts Microsoft.ML.OnnxRuntimeGenAI for in-process GPU/CPU inference.
/// No background service process is required — the model runs directly in the calling process.
/// </summary>
internal sealed class ConcreteOrtGenAiModelAdapter : ILanguageModelAdapter
{
    private readonly Model _model;
    private readonly Tokenizer _tokenizer;
    private readonly int _maxContextTokens;
    private readonly string _modelAlias;
    private readonly string _chatTemplateFamily;
    private readonly ModelReasoningMode _reasoningMode;

    private ConcreteOrtGenAiModelAdapter(
        Model model,
        Tokenizer tokenizer,
        int maxContextTokens,
        string chatTemplateFamily,
        string modelAlias,
        ModelReasoningMode reasoningMode)
    {
        _model = model;
        _tokenizer = tokenizer;
        _maxContextTokens = maxContextTokens;
        _chatTemplateFamily = chatTemplateFamily;
        _modelAlias = modelAlias;
        _reasoningMode = reasoningMode;
    }

    /// <summary>
    /// Loads the ONNX GenAI model from <paramref name="modelDirectory"/> (must contain
    /// <c>genai_config.json</c> and <c>model.onnx</c> / <c>model.onnx.data</c>).
    /// </summary>
    public static async Task<ConcreteOrtGenAiModelAdapter> CreateAsync(
        string modelDirectory,
        int maxContextTokens,
        Action<string>? onProgress = null,
        ModelReasoningMode reasoningMode = ModelReasoningMode.Default,
        string? modelAlias = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(modelDirectory);
        ct.ThrowIfCancellationRequested();

        onProgress?.Invoke("AI_STAGE_LOADING");

        EnsureCudaRuntimeLibrariesLoaded();

        var model = await Task.Run(() => new Model(modelDirectory), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        var tokenizer = await Task.Run(() => new Tokenizer(model), ct).ConfigureAwait(false);
        var family = DetectChatTemplateFamily(modelDirectory);
        var resolvedAlias = string.IsNullOrWhiteSpace(modelAlias)
            ? ModelPromptPolicy.DetectAliasFromPath(modelDirectory)
            : modelAlias;

        return new ConcreteOrtGenAiModelAdapter(model, tokenizer, maxContextTokens, family, resolvedAlias, reasoningMode);
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

        // ORT GenAI generation is synchronous (C++ bindings); run on a thread-pool thread
        // and pipe tokens into the channel so the caller can await them asynchronously.
        var generationTask = Task.Run(() =>
        {
            try
            {
                var sequences = _tokenizer.Encode(formattedPrompt);
                using var generatorParams = new GeneratorParams(_model);
                generatorParams.SetSearchOption("max_length", _maxContextTokens);

                using var generator = new Generator(_model, generatorParams);
                generator.AppendTokenSequences(sequences);
                using var tokenizerStream = _tokenizer.CreateStream();

                while (!generator.IsDone())
                {
                    if (ct.IsCancellationRequested)
                        break;

                    generator.GenerateNextToken();

                    var seq = generator.GetSequence(0);
                    if (seq.Length > 0)
                    {
                        var text = tokenizerStream.Decode(seq[^1]);
                        if (!string.IsNullOrEmpty(text))
                            channel.Writer.TryWrite(text);
                    }
                }
            }
            catch (Exception ex)
            {
                generationException = ex;
            }
            finally
            {
                channel.Writer.TryComplete();
            }
        }, CancellationToken.None); // cancellation checked inside the loop above

        await foreach (var token in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return token;
        }

        await generationTask.ConfigureAwait(false);

        if (generationException is not null)
            throw new InvalidOperationException("ORT GenAI text generation failed.", generationException);
    }

    /// <inheritdoc/>
    public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(text);
        ct.ThrowIfCancellationRequested();

        // ORT GenAI chat models do not natively support embeddings.
        return Task.FromResult(Array.Empty<float>());
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _tokenizer.Dispose();
        _model.Dispose();
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
            _           => $"<|system|>\n{systemPrompt}<|end|>\n<|user|>\n{userPrompt}<|end|>\n<|assistant|>\n",
        };
    }

    /// <summary>
    /// Reads <c>genai_config.json</c> from <paramref name="modelDirectory"/> to determine
    /// which chat-template family to use when formatting prompts.
    /// Falls back to the Phi family when the file is absent or unrecognised.
    /// </summary>
    private static string DetectChatTemplateFamily(string modelDirectory)
    {
        try
        {
            var configPath = Path.Combine(modelDirectory, "genai_config.json");
            if (!File.Exists(configPath))
                return InferFamilyFromPath(modelDirectory);

            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));

            // Check the model type field that ORT GenAI places in genai_config.json
            if (doc.RootElement.TryGetProperty("model", out var modelProp) &&
                modelProp.TryGetProperty("type", out var typeProp))
            {
                var type = typeProp.GetString() ?? string.Empty;
                    if (type.Contains("qwen3", StringComparison.OrdinalIgnoreCase))
                        return "qwen3";
                    if (type.Contains("qwen2.5", StringComparison.OrdinalIgnoreCase) || type.Contains("qwen25", StringComparison.OrdinalIgnoreCase))
                        return "qwen25";
                    if (type.Contains("qwen", StringComparison.OrdinalIgnoreCase))
                        return InferQwenFamilyFromPath(modelDirectory);
                    if (type.Contains("deepseek", StringComparison.OrdinalIgnoreCase))
                        return "deepseek";
                    if (type.Equals("llama", StringComparison.OrdinalIgnoreCase))
                        return "llama";
                    if (type.Contains("gemma", StringComparison.OrdinalIgnoreCase))
                        return "gemma3";
            }

            // Also try checking the EOS tokens for Qwen-style markers
            if (doc.RootElement.TryGetProperty("search", out var searchProp) &&
                searchProp.TryGetProperty("eos_token_id", out var eosEl))
            {
                var eosString = eosEl.ToString();
                if (eosString.Contains("151645", StringComparison.Ordinal)) // Qwen2 eos id
                    return InferQwenFamilyFromPath(modelDirectory);
            }
        }
        catch
        {
            // Configuration unreadable; fall back to phi format
        }

        return InferFamilyFromPath(modelDirectory);
    }

    private static string InferFamilyFromPath(string modelDirectory)
    {
        var path = modelDirectory.ToLowerInvariant();
        if (path.Contains("qwen3")) return "qwen3";
        if (path.Contains("qwen2.5") || path.Contains("qwen25")) return "qwen25";
        if (path.Contains("qwen")) return "qwen25";
        if (path.Contains("deepseek")) return "deepseek";
        if (path.Contains("llama")) return "llama";
        if (path.Contains("gemma")) return "gemma3";
        return "phi";
    }

    private static string InferQwenFamilyFromPath(string modelDirectory)
    {
        var path = modelDirectory.ToLowerInvariant();
        return path.Contains("qwen3") ? "qwen3" : "qwen25";
    }

    // ── CUDA runtime library resolution ───────────────────────────────────────

    private static int s_cudaRuntimeLoaded; // 0 = not tried, 1 = done

    /// <summary>
    /// CUDA toolkit runtime DLLs that <c>onnxruntime_providers_cuda.dll</c> depends on.
    /// </summary>
    private static readonly string[] CudaRuntimeDlls =
    [
        "cudart64_12.dll",
        "cublas64_12.dll",
        "cublasLt64_12.dll",
        "cufft64_11.dll",
        "cusparse64_12.dll",
        "cudnn64_9.dll",
        "cudnn_ops64_9.dll",
        "cudnn_graph64_9.dll",
    ];

    /// <summary>
    /// Ensures CUDA toolkit runtime DLLs (cuBLAS, cuDNN, etc.) are co-located with
    /// <c>onnxruntime_providers_cuda.dll</c> so that the native loader's
    /// <c>LOAD_WITH_ALTERED_SEARCH_PATH</c> finds them.
    /// <para/>
    /// ORT loads its provider DLL via <c>LoadLibraryEx</c> with altered search path,
    /// which resolves dependent DLLs relative to the provider DLL's directory — not
    /// the process module table or <c>PATH</c>.  The only reliable way to satisfy
    /// these dependencies is to place (or symlink) the CUDA runtime DLLs into that
    /// same directory.
    /// </summary>
    private static void EnsureCudaRuntimeLibrariesLoaded()
    {
        if (Interlocked.CompareExchange(ref s_cudaRuntimeLoaded, 1, 0) != 0)
            return;

        // Locate the directory that contains onnxruntime_providers_cuda.dll.
        // NuGet places it in runtimes/win-x64/native/ relative to the assembly output.
        var providerDir = FindProviderDirectory();
        if (providerDir is null)
            return;

        // Find a source directory that has the CUDA toolkit runtime DLLs.
        var cudaSourceDir = FindCudaRuntimeDirectory();
        if (cudaSourceDir is null)
            return;

        // Place each missing CUDA DLL into the provider directory.
        foreach (var dll in CudaRuntimeDlls)
        {
            var target = Path.Combine(providerDir, dll);
            if (File.Exists(target))
                continue;

            var source = Path.Combine(cudaSourceDir, dll);
            if (!File.Exists(source))
                continue;

            try
            {
                File.CreateSymbolicLink(target, source);
            }
            catch
            {
                // Symlinks may require elevated privileges; fall back to copy.
                try { File.Copy(source, target, overwrite: false); } catch { }
            }
        }
    }

    /// <summary>
    /// Locates the directory containing <c>onnxruntime_providers_cuda.dll</c> by
    /// searching outward from the assembly's base directory.
    /// </summary>
    private static string? FindProviderDirectory()
    {
        var baseDir = AppContext.BaseDirectory;

        // Flat layout: providers DLL next to the assembly.
        if (File.Exists(Path.Combine(baseDir, "onnxruntime_providers_cuda.dll")))
            return baseDir;

        // NuGet runtimes layout: runtimes/win-x64/native/
        var runtimesPath = Path.Combine(baseDir, "runtimes", "win-x64", "native");
        if (File.Exists(Path.Combine(runtimesPath, "onnxruntime_providers_cuda.dll")))
            return runtimesPath;

        return null;
    }

    /// <summary>
    /// Finds a directory that contains the CUDA 12 runtime DLLs.
    /// Searches the SmrtPad EP cache first, then standard CUDA Toolkit paths.
    /// </summary>
    private static string? FindCudaRuntimeDirectory()
    {
        ReadOnlySpan<string> candidates =
        [
            // SmrtPad's own cached CUDA EP libraries (%USERPROFILE%\.SmrtPad\ep\cuda-ep)
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".SmrtPad", "ep", "cuda-ep"),

            // Standard CUDA Toolkit v12 install paths
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "NVIDIA GPU Computing Toolkit", "CUDA", "v12.6", "bin"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "NVIDIA GPU Computing Toolkit", "CUDA", "v12.5", "bin"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "NVIDIA GPU Computing Toolkit", "CUDA", "v12.4", "bin"),
        ];

        foreach (var dir in candidates)
        {
            if (Directory.Exists(dir) &&
                File.Exists(Path.Combine(dir, "cublasLt64_12.dll")))
            {
                return dir;
            }
        }

        return null;
    }
}
