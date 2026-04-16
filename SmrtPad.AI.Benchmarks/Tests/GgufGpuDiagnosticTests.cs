using System.Diagnostics;
using LLama;
using LLama.Common;
using LLama.Native;
using SmrtPad.AI;

namespace SmrtPad.AI.Benchmarks.Tests;

/// <summary>
/// Quick diagnostic: loads one small GGUF, prints the backend that actually loaded
/// (CPU vs CUDA), GPU layer count, and raw tokens-per-second.
/// Run with: dotnet test --filter "Category=GgufDiag"
/// </summary>
public sealed class GgufGpuDiagnosticTests
{
    // Smallest model on disk — fast to load, easy to reason about.
    private const string GgufPath1B = @"B:\Models\benchmark-models-gguf\llama-3.2-1b\Llama-3.2-1B-Instruct-Q4_K_M.gguf";
    private const string GgufPath3B = @"B:\Models\benchmark-models-gguf\llama-3.2-3b\Llama-3.2-3B-Instruct-Q4_K_M.gguf";
    private const string Prompt = "<|begin_of_text|><|start_header_id|>user<|end_header_id|>\n\nCount from 1 to 20.<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\n";

    [Fact(Timeout = 120_000)]
    [Trait("Category", "GgufDiag")]
    public async Task GgufDiag_PrintsBackendAndTps()
    {
        Skip.IfNot(File.Exists(GgufPath1B), $"Model not found: {GgufPath1B}");
        await RunDiag(GgufPath1B);
    }

    [Fact(Timeout = 120_000)]
    [Trait("Category", "GgufDiag")]
    public async Task GgufDiag_3B_PrintsBackendAndTps()
    {
        Skip.IfNot(File.Exists(GgufPath3B), $"Model not found: {GgufPath3B}");
        await RunDiag(GgufPath3B);
    }

    private async Task RunDiag(string ggufPath)
    {
        // ── 1. Configure backend ─────────────────────────────────────────────────────────
        // Delegate to the single source of truth — ConcreteLlamaSharpModelAdapter —
        // which handles the correct CUDA DLL pre-load order and NativeLibraryConfig setup.
        // Pass Console.WriteLine so we get full diagnostic output including any pre-load failures.
        ConcreteLlamaSharpModelAdapter.ConfigureNativeLibrary(msg => Console.WriteLine($"[diag] {msg}"));

        var cuda12Dir = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "cuda12");
        bool hasCuda = IsCudaAvailable();

        Console.WriteLine($"[diag] CUDA driver present : {hasCuda}");
        Console.WriteLine($"[diag] cuda12 dir exists   : {Directory.Exists(cuda12Dir)}");
        Console.WriteLine($"[diag] ggml-cuda.dll exists: {File.Exists(Path.Combine(cuda12Dir, "ggml-cuda.dll"))}");
        Console.WriteLine($"[diag] llama.dll exists    : {File.Exists(Path.Combine(cuda12Dir, "llama.dll"))}");
        Console.WriteLine($"[diag] NativeLib locked    : {NativeLibraryConfig.LLama.LibraryHasLoaded}");

        // ── 2. Load model ────────────────────────────────────────────────────────────────
        long modelMb  = new FileInfo(ggufPath).Length / (1024 * 1024);
        int  gpuLayers = hasCuda ? 999 : 0;

        Console.WriteLine($"[diag] Model               : {Path.GetFileName(ggufPath)}");
        Console.WriteLine($"[diag] Model size          : {modelMb} MB");
        Console.WriteLine($"[diag] Requested GPU layers: {gpuLayers}");

        var modelParams = new ModelParams(ggufPath)
        {
            ContextSize    = 512,
            GpuLayerCount  = gpuLayers,
            MainGpu        = 0,
            FlashAttention = gpuLayers > 0,
            BatchSize      = 512,
            UBatchSize     = 512,
        };

        var swLoad = Stopwatch.StartNew();
        var weights = await Task.Run(() => LLamaWeights.LoadFromFile(modelParams));
        swLoad.Stop();
        Console.WriteLine($"[diag] Model loaded in     : {swLoad.Elapsed.TotalSeconds:F1}s");
        Console.WriteLine($"[diag] NativeLib loaded    : {NativeLibraryConfig.LLama.LibraryHasLoaded}");

        // ── 3. Infer and measure TPS ────────────────────────────────────────────────────
        using (weights)
        {
            var executor = new StatelessExecutor(weights, modelParams);

            var inferParams = new InferenceParams
            {
                MaxTokens        = 100,
                AntiPrompts      = ["<|eot_id|>"],
                SamplingPipeline = new LLama.Sampling.DefaultSamplingPipeline(),
            };

            int    tokenCount = 0;
            string fullText   = string.Empty;
            var    swInfer    = Stopwatch.StartNew();

            await foreach (var tok in executor.InferAsync(Prompt, inferParams))
            {
                tokenCount++;
                fullText += tok;
            }

            swInfer.Stop();
            double tps = tokenCount / swInfer.Elapsed.TotalSeconds;

            Console.WriteLine($"[diag] Tokens generated    : {tokenCount}");
            Console.WriteLine($"[diag] Inference time      : {swInfer.Elapsed.TotalSeconds:F2}s");
            Console.WriteLine($"[diag] Tokens/sec          : {tps:F1}");
            Console.WriteLine($"[diag] Response            : {fullText.Trim()}");
            Console.WriteLine($"[diag] Backend assessment  : {(tps > 30 ? "GPU ✓" : $"CPU or slow GPU (expected >30 TPS, got {tps:F1})")}");

            // GPU should deliver >>30 TPS on a 1B Q4 model (RTX 4060 sees ~190 TPS).
            Assert.True(tps > 30, $"TPS too low ({tps:F1}) — expected >30. CUDA backend may not have loaded.");
        }
    }

    private static bool IsCudaAvailable()
    {
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return File.Exists(Path.Combine(system32, "nvcuda.dll"));
    }
}
