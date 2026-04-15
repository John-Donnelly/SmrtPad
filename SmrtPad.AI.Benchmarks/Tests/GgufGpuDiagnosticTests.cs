using System.Diagnostics;
using System.Runtime.InteropServices;
using LLama;
using LLama.Common;
using LLama.Native;

namespace SmrtPad.AI.Benchmarks.Tests;

/// <summary>
/// Quick diagnostic: loads one small GGUF, prints the backend that actually loaded
/// (CPU vs CUDA), GPU layer count, and raw tokens-per-second.
/// Run with: dotnet test --filter "Category=GgufDiag"
/// </summary>
public sealed class GgufGpuDiagnosticTests
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool SetDllDirectory(string lpPathName);
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
        var assemblyDir = AppContext.BaseDirectory;
        var cuda12Dir   = Path.Combine(assemblyDir, "runtimes", "win-x64", "native", "cuda12");
        var llamaCuda   = Path.Combine(cuda12Dir, "llama.dll");
        bool hasCuda    = IsCudaAvailable();

        Console.WriteLine($"[diag] CUDA driver present : {hasCuda}");
        Console.WriteLine($"[diag] cuda12 dir exists   : {Directory.Exists(cuda12Dir)}  ({cuda12Dir})");
        Console.WriteLine($"[diag] ggml-cuda.dll exists: {File.Exists(Path.Combine(cuda12Dir, "ggml-cuda.dll"))}");
        Console.WriteLine($"[diag] llama.dll exists    : {File.Exists(llamaCuda)}");
        Console.WriteLine($"[diag] NativeLib locked    : {NativeLibraryConfig.LLama.LibraryHasLoaded}");

        // NativeLibraryConfig is one-shot. The static ctor in ConcreteLlamaSharpModelAdapter
        // already configured it if that type was touched. If not locked yet, configure here.
        if (!NativeLibraryConfig.LLama.LibraryHasLoaded)
        {
            if (hasCuda && File.Exists(llamaCuda))
            {
                // Pre-load all dependencies in order so the Windows loader cache has them
                // before ggml.dll's static import of ggml-cuda.dll is resolved.
                foreach (var dep in new[] { "cudart64_12.dll", "cublas64_12.dll", "cublasLt64_12.dll", "ggml-base.dll", "ggml-cpu.dll", "ggml-cuda.dll", "ggml.dll" })
                {
                    var depPath = Path.Combine(cuda12Dir, dep);
                    if (File.Exists(depPath))
                    {
                        try { System.Runtime.InteropServices.NativeLibrary.Load(depPath); Console.WriteLine($"[diag] Pre-loaded: {dep}"); }
                        catch (Exception ex) { Console.WriteLine($"[diag] FAILED to pre-load {dep}: {ex.Message}"); }
                    }
                    else Console.WriteLine($"[diag] Missing dep: {dep}");
                }

                NativeLibraryConfig.All
                    .WithLibrary(llamaCuda, null)
                    .WithAutoFallback(false);
                Console.WriteLine($"[diag] Config applied      : WithLibrary(cuda12/llama.dll) + pre-loaded deps");
            }
            else
            {
                NativeLibraryConfig.All.WithCuda(false).WithAutoFallback(true);
                Console.WriteLine($"[diag] Config applied      : CPU fallback");
            }
        }
        else
        {
            Console.WriteLine($"[diag] Config applied      : already locked (static ctor fired first)");
        }

        // ── 2. Load model ────────────────────────────────────────────────────────────────
        long modelMb = new FileInfo(ggufPath).Length / (1024 * 1024);
        int  gpuLayers = hasCuda ? 999 : 0;

        Console.WriteLine($"[diag] Model                : {Path.GetFileName(ggufPath)}");
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

        // Print the metadata to see what GGML backend was actually chosen
        // LLamaSharp exposes this via NativeLibraryConfig.Instance description
        try
        {
            var desc = NativeLibraryConfig.LLama.LibraryHasLoaded;
            Console.WriteLine($"[diag] NativeLib loaded    : {desc}");
        }
        catch { }

        // ── 3. Infer and measure TPS ────────────────────────────────────────────────────
        using (weights)
        {
            var context  = weights.CreateContext(modelParams);
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

            // GPU should be giving >>50 TPS on a 1B Q4 model.
            // Assert loosely — even bad CPU gets ~5 TPS, CUDA should be >>30.
            Console.WriteLine($"[diag] Backend assessment  : {(tps > 30 ? "GPU ✓" : $"CPU or slow GPU (expected >30 TPS, got {tps:F1})")}");

            context.Dispose();
        }
    }

    private static bool IsCudaAvailable()
    {
        var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return File.Exists(Path.Combine(system32, "nvcuda.dll"));
    }
}
