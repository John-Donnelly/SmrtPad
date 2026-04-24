# Changelog

All notable changes to SmrtPad are documented in this file.

## [Unreleased]

### Added
- **`SkillContext` ambient carrier** — new `internal static class SkillContext` using `AsyncLocal<string?>` threads the active skill key from `AIDispatcher.StreamResponseAsync` through to `ConcreteLlamaSharpModelAdapter.BuildSamplingPipeline` without changing the `ILanguageModelAdapter` interface
- **Skill-aware sampling pipeline** — `BuildSamplingPipeline` now reads `SkillContext.Current` and applies precision settings (Temperature 0.3, TopP 0.85, Seed 42) for `autocomplete` and `ocr` skills to reduce stochastic variance in tag wrapping and content fidelity; all other skills use LLamaSharp library defaults
- **`TargetedBenchmarkRun_Gemma4E2b_WithLiveDashboard` benchmark test** — single-model focused benchmark for Gemma 4 E2B GGUF with live dashboard, per-case JSONL response logging, and a 300-second per-case timeout; opens the dashboard in the browser automatically on first result
- **`ModelPromptPolicyTests`** — xUnit unit tests covering `SupportsThinkingMode`, `GetModelDirective`, `NormalizeMode`, and `BuildSystemPrompt` for all model families
- **`BenchmarkSuite1` project** — Benchmark.NET project for `BenchmarkDashboardGeneratorBenchmarks` with CPU and memory diagnosers

### Changed
- **`PromptTemplates.ToneProfessional`** — added explicit guard against closing phrases (`"let me know"`, `"feel free to"`, `"I hope this helps"`, `"don't hesitate"`) that trigger the `ClosingRemarkLine` evaluator regex
- **`PromptTemplates.ToneCasual`** — same closing-phrase guard as `ToneProfessional`
- **`PromptTemplates.AutoComplete`** — prompt now explicitly forbids ellipsis (`"..."`) as a placeholder and requires the continuation text in full
- **`PromptTemplates.OcrFallback`** — prompt now explicitly forbids ellipsis (`"..."`) as a placeholder and requires the corrected text in full
- **`ConcreteLlamaSharpModelAdapter.ConsumeTokenStream`** — tokens are now scrubbed of chat-template turn markers (`<end_of_turn>`, `<start_of_turn>`, `</start_of_turn>`, `<|eot_id|>`, `<|im_end|>`, `<|end|>`) before being written to the channel, preventing occasional leakage into scored output
- **`ConcreteLlamaSharpModelAdapter.DetectChatTemplateFamily`** — doc comment corrected (was missing leading `///`)


- **`IAIDispatcher.SetPreferredReasoningMode(string)` / `PreferredReasoningMode`** — new interface members so the proxy tier can round-trip the mode key as a plain string across the assembly boundary
- **`AIDispatcherProxy.SetPreferredReasoningMode()` / `PreferredReasoningMode`** — proxy implementation that parses and forwards the mode string to the inner dynamic dispatcher
- **`SidebarAutomationHelper.SwitchReasoningMode()`** — UI-test helper that opens the options flyout, navigates to the *ReasoningModeSubMenu*, and clicks the target mode item; waits for dispatcher reload to complete
- **`ModelSizeSelector.AllAliases`** — exposes all registered alias names for enumeration without a hardware-budget filter
- **Thinking/no-thinking benchmark matrix in `LocalModelBenchmarkTests`** — each discovered GGUF model now runs in both `NoThink` and `Think` modes where supported; GPU runs execute first (both modes), then CPU runs (both modes); models that do not support thinking receive one `NoThink` run per target
- **`SMRTPAD_BENCHMARK_MODE` env var** — set to `CPU` to execute CPU-only runs; default (`GPU`) runs GPU first then CPU
- **`SMRTPAD_LLAMA_BACKEND_DIR` env var** — override path to a custom llama.cpp backend directory; needed for architectures (e.g., Gemma 4) not supported by the default CUDA 12 build
- **`LocalBenchmarkRun` record** — internal value type capturing model name, path, backend label, CPU flag, reasoning mode, reasoning tag, and display name for each planned benchmark slot
- **Reasoning tag (`ReasoningTag`) on `BenchmarkResult` / `BenchmarkRun` / `BenchmarkRunner`** — every result and run now carries a `NoThink` or `Think` tag; the benchmark runner constructor accepts a reasoning tag parameter
- **`BenchmarkRun.Combine()`** — merges multiple `BenchmarkRun` instances into one; combined `ReasoningTag` reflects the set of distinct tags across constituent runs
- **`modelReasoningTags` dictionary in the JS data sidecar** — maps each model key to its reasoning tag for client-side rendering
- **Mode column in Markdown reports** — `BenchmarkReportGenerator` now emits a `| Mode |` row in the run header and a `| Mode |` column in the multi-model comparison table; model groups are keyed by `(ModelAlias, BackendTarget, ReasoningTag)` so Think and NoThink runs are compared as separate rows
- **Reasoning tag in dashboard result table** — each row shows `[Think]` / `[NoThink]` alongside the model alias
- **`AiModelBenchmarkResult.ReasoningTag`** and surface in UI-test runner section headers and JSON summary output
- **`SmrtSidebarPro` UI tests** — new `SmrtSidebarProUITests` covering sidebar availability and reasoning mode switching

### Changed
- **`AIDispatcherFactory.CreateFromLocalPath()`** — local-path dispatchers (GGUF and ORT) now honour `dispatcher.PreferredReasoningMode` when constructing the adapter; previously hardcoded to `ModelReasoningMode.Default`
- **`ConcreteLlamaSharpModelAdapter.ConfigureNativeLibrary()`** — CUDA DLL pre-load list revised: dependency order is now `libomp140 → cudart64_12 → cublasLt64_12 → cublas64_12 → ggml-base → ggml`; each failed load logs diagnostics and throws instead of silently continuing; missing optional DLLs log `Missing dep (skipped)`
- **`EnsureGgmlBackendsLoaded()`** extracted from the CUDA path into a standalone method; discovers `ggml_backend_load_all_from_path` before falling back to `ggml_backend_load_all`
- **`LocalModelBenchmarkTests`** refactored — ORT GenAI search roots removed; test is now GGUF-only; discovery returns `(Name, Path, SupportsThinking)` tuples; run planning delegated to `CreateBenchmarkRuns()`
- **Prompt templates** — all skill templates rewritten to be model-neutral: no persona preamble, explicit `<think>` tag suppression, and a consistent `output exactly one <insert>…</insert> block and nothing else` contract; `FreeformChat` template restructured with clearer document-vs-question branching
- **Solution file (`SmrtPad.slnx`)** — `BenchmarkSuite1` project added; `SmrtPad.Tests` and `SmrtPad.UITests` platform entries simplified to `<Platform Project="x64" />`
- **`Directory.Packages.props`** — `BenchmarkDotNet 0.15.2` and `Microsoft.VisualStudio.DiagnosticsHub.BenchmarkDotNetDiagnosers 18.3.36812.1` added as test-tier packages
- **Copilot instructions** — benchmark policy documented: prefer LLamaSharp runner for live runs; run GPU (both modes) before CPU (both modes) for thinking-capable models

### Fixed
- **`GgufGpuDiagnosticTests`** trimmed of stale ORT/GenAI diagnostic paths that are no longer applicable

---

## [0.8.0] - 2026-04-08

### Added
- **ORT GenAI in-process inference** — replaced Foundry Local with direct `Microsoft.ML.OnnxRuntimeGenAI.Cuda` 0.12.2; the AI engine now runs ONNX GenAI models in-process with CUDA GPU acceleration and CPU fallback
- **`ConcreteOrtGenAiModelAdapter`** — new `ILanguageModelAdapter` implementation for ORT GenAI; supports streaming, automatic chat-template detection (phi/qwen/deepseek families), and CUDA runtime library resolution
- **`ModelDownloadService`** — downloads ONNX GenAI model files from HuggingFace Hub with progress reporting; integrates with the Smart Sidebar download progress UI
- **`AIDispatcherFactory.CreateFromLocalPath()`** — public factory method for direct local-model loading; bypasses the alias/download pipeline for benchmarking and development
- **CUDA GPU support** — CUDA execution provider enabled via `Microsoft.ML.OnnxRuntimeGenAI.Cuda`; runtime libraries are symlinked/copied from `%USERPROFILE%\.SmrtPad\ep\cuda-ep\` into the ORT provider directory at startup
- **Local model benchmark test** (`LocalModelBenchmarkTests`) — scans local directories for ONNX GenAI models and benchmarks them with live dashboard output; uses `CreateFromLocalPath` public API
- **SmrtDoodle IPC service** (`SmrtDoodleIpcService`) — named-pipe IPC integration; launches SmrtDoodle, awaits exit, and retrieves the drawing result for document insertion
- **3 new SmrtDoodle localization strings** across all 9 locale `.resw` files

### Changed
- **Target framework** migrated from .NET 10 preview to .NET 8 across all 7 projects, MSIX packaging, CI workflow, and deploy scripts
- **AI engine naming** — all `Foundry`/`FoundryLocal`/`FoundryGpu` references renamed to `OnnxRuntime`/`OnnxRuntimeGpu`/`Gpu` throughout the codebase (public API, tests, UI layer, comments)
- **`HardwareProbeService.ProbeFoundryGpuAsync()`** renamed to `ProbeOnnxRuntimeGpuAsync()`
- **`AIDispatcher`** execution-path enum values renamed: `FoundryLocalGpu` → `OnnxRuntimeGpu`, `FoundryLocalCpu` → `OnnxRuntimeCpu`
- **`SmartSidebar`** — removed `IsLikelyFoundryMissingMessage` and `StatusCodeFoundryMissing` (no longer applicable with in-process inference)
- **`ModelSizeSelector`** — added `HuggingFaceModelInfo` record and HuggingFace source registry for model download resolution
- **CI workflow** (`.github/workflows/ci.yml`) — switched from .NET 10 preview SDK to .NET 8 stable
- **Deploy script** — updated published output path from `net10.0-windows*` to `net8.0-windows*`
- **`MainWindow.PaintDrawing_Click`** — rewritten to use `SmrtDoodleIpcService` named-pipe IPC instead of direct process launch

### Removed
- **`ConcreteFoundryModelAdapter`** — deleted; replaced by `ConcreteOrtGenAiModelAdapter`
- **`Microsoft.AI.Foundry.Local`** NuGet package dependency — removed from `Directory.Packages.props`

### Added
- **`CpuOnly` / `GpuOnly` sentinel constants** in `ModelSizeSelector` — `-1L` sentinels mark models that have no GPU or CPU execution provider in Foundry Local; all three selector methods (`SelectBestAliasAsync`, `GetEligibleAliases`, `GetBestAliasForCapability`) skip entries whose provider flag matches the requested path
- **`qwen3-0.6b`** added to model catalog as CPU-only (594 MB RAM, no GPU execution provider in Foundry)
- **`GetEligibleCpuModelAliases()`** on `IAIDispatcher` / `AIDispatcherProxy` — returns aliases eligible for CPU execution (VRAM budget zeroed); used to populate the model submenu when the CPU execution target is active
- **NPU proxy tier** in `LiveBenchmarkTests` — `phi-3.5-mini` queued as `NpuProxy (GPU)` to approximate phi-silica performance on hardware without an NPU
- **JSONL response log** — `BenchmarkRunner.RunAsync()` accepts an optional `responseLogPath`; when set, a JSONL record is appended after every case containing runId, model, backend, caseId, skillKey, input, rawOutput, insert/think content, scores, latency, and tokens-per-second
- **`#status-line`** element on the live benchmark dashboard — shows the current CLI progress string above the progress bar; cleared with a ✅ completion message when the run finishes
- **`modelBackends` / `modelHardwareTags`** dictionaries in the JS data sidecar — map each model key to its execution tier (GPU / CPU / NPU) for client-side filtering
- **NPU filter chip** on the benchmark dashboard — sits alongside the existing GPU / CPU chips; `backendMatch()` dispatches by tier: NPU (`npu`), GPU (`gpu` without `npu`), CPU (fallback)
- **Hardware tag prefix** (`[GPU]`, `[CPU]`, `[NPU]`) on every model label in the result table and all Chart.js axis labels / legend entries
- **Persistent model-checkbox state** — `buildModelCbs()` saves and restores per-model check state in `localStorage` keyed by container; selections survive page reloads

### Fixed
- **`grandTotal` accuracy during benchmark runs** — `currentGrandTotal` is now mutable; decremented by `cases.Count` when a model fails to initialise, keeping the progress fraction correct (previously showed e.g. 949/1022 when models were skipped)
- **CPU benchmark coverage** — `LiveBenchmarkTests` previously only ran CPU-exclusive models on the CPU path (~1 095 evaluations); now all CPU-eligible models run on CPU AND all GPU-eligible models run on GPU independently (~1 971 evaluations)
- **7 missing localization keys** in all 8 satellite `.resw` files (`ar-SA`, `de-DE`, `es-ES`, `fr-FR`, `ja-JP`, `ru-RU`, `zh-Hans`, `ur-PK`): `SmartSidebarAIDispatcherUnavailableTitle`, `SmartSidebarAIDispatcherUnavailableContent`, `SmartSidebarFoundryMissingContent`, `SmartSidebarAIDispatcherUnavailableSetup`, `SmartSidebarAIDispatcherUnavailableDismiss`, `SmartSidebarPrerequisiteFoundryMissingStatus`, `SmartSidebarPrerequisiteDispatcherInitFailedStatus`

### Changed
- **Model catalog narrowed to ≤10B parameter models** — removed `mistral-7b-v0.2`, `deepseek-r1-14b`, `qwen2.5-14b`, `qwen2.5-coder-14b`, `gpt-oss-20b` (all exceed 10B or are not available in Foundry Local with a verified execution provider)
- **`deepseek-r1-1.5b`** updated to GPU-only with corrected 1 464 MB footprint (trtrtx provider; no CPU execution provider exists in Foundry Local)
- **`SmartSidebar.PopulateModelMenu()`** is now target-aware: CPU target uses `GetEligibleCpuModelAliases()`, NPU target hides the model submenu (phi-silica auto-selected), GPU / automatic uses `GetEligibleModelAliases()` as before
- **Dashboard removes "Avg LLM Grade" KPI tile** and the LLM column from the per-result table; LLM scoring infrastructure remains in the runner
- **Dashboard `getFilteredModels()`** filters the model key list by the active hardware tier before chart or checkbox builds, so GPU / CPU / NPU views are fully independent
- **`currentStatus` passed to `BenchmarkDashboardGenerator.Generate()`** — live CLI progress line updates the dashboard status element on each refresh poll

---

## [0.7.0] - 2026-04-04

### Added
- **Smart Sidebar full-response streaming** — insert-only responses (Summarize, Tone, Rewrite, Grammar Fix, Shorten, Autocomplete) now stream into the chat bubble in real time; the complete output is visible in the conversation history before the user clicks Insert
- **Insert button** (`InsertBubbleButton`) on assistant bubbles — shown only when a response contains insertable content (`InsertText` non-empty); a hidden `InsertContent` UIA element exposes the insert text to accessibility and benchmark tooling without occupying visual space
- **`BubbleText` computed property** on `SidebarChatEntry` — falls back to `InsertText` when `Text` is empty or whitespace, ensuring a response is always visible; XAML bubble now binds to `BubbleText` instead of `Text`
- **`PromptTemplates.GradeResponse(request, response)`** — LLM quality-evaluator prompt that returns `{"score": N, "reason": "..."}` JSON wrapped in `<grade>` tags; used by the AI benchmark LLM scorer
- **`AIDispatcher` "grade" skill key** — passes the fully-formed grader prompt through to the model unchanged
- **FreeformChat `<insert>` tag instructions** — document-writing requests (letter, email, report, essay, story, press release, etc.) now produce output wrapped in `<insert>` tags so the result can be inserted into the editor; conversational questions reply in plain text without tags
- **`SmrtPad.AI.Benchmarks` project** — standalone benchmark runner with rule-based and LLM-driven quality scoring; `BenchmarkPromptCatalog` covers document composition (15 prompts), edit skills (22 prompts), and tag-compliance (3 prompts); `EvaluationScore` per-result breakdown; JSON reports written to `Reports/`
- **AI model benchmark suite** (`AiModelBenchmarkRunner`, `AiBenchmarkCatalog`, `AiBenchmarkSuiteTests`) — end-to-end UI benchmark that drives the live app via Appium/WinAppDriver; measures per-prompt latency, tokens per second, insert compliance, and keyword score; writes JSON reports to `BenchmarkResults/`; supports `BENCHMARK_MODEL_FILTER`, `BENCHMARK_PROMPT_LIMIT`, and `SMRTPAD_APPIUM_SERVER` environment variables
- **VS Code launch/task configuration** (`.vscode/`) — `C#: SmrtPad` attach-mode debug configuration activates the app via AUMID then attaches vsdbg; `build` task compiles x64 Debug; `launch-smrtpad` task stops any running instance, activates via `shell:AppsFolder`, and waits for the process to appear
- **`GetEligibleCpuModelAliases()`** on `AIDispatcher` — returns model aliases that fit within the CPU RAM budget (VRAM budget zeroed); used to enumerate models for CPU-path benchmarking separate from the GPU-eligible set
- **`deepseek-r1-1.5b` model entry** in `ModelSizeSelector` — adds the DeepSeek-R1 1.5B thinking model (GPU 1 028 MB / CPU 1 450 MB footprint) to the ordered alias table
- **`StripResidualTags()` helper** in `SmartSidebar` — removes residual `<insert>`, `</insert>`, `<think>`, and `</think>` tag text that can leak through the streaming parser due to token-boundary edge cases; applied as a safety net to both `trimmedAnswer` and `insertContent` in the stream-completion callback
- **Expanded `BenchmarkPromptCatalog`** — catalog grows from ~40 to **73 cases** across five tones/types:
  - Document Composition: formal letters (×7 inc. apology, termination, legal disclaimer, board resolution), business reports (×4), technical docs (×3 inc. bug report, ADR), casual/personal (×4), creative (×4 inc. poem, taglines, dialogue), professional (×1 resume)
  - Edit Skills: minimum 5 cases per skill — Summarize ×5, Rewrite ×5, Grammar Fix ×5, Tone Professional ×5, Tone Casual ×5, Shorten ×5, Autocomplete ×5, OCR Fallback ×3
  - Tag Compliance: 7 chat Q&A cases (writing tips, grammar rules, word choice, style guide + the original 3)
- **`BenchmarkResult` performance fields** — `EstimatedInputTokens`, `EstimatedOutputTokens`, `ElectricityCostUsd`, `TimeToFirstTokenMs` added as optional record parameters; computed properties `TotalTokens`, `TokenCostUsd`, `TotalCostUsd`, `GenerationMs`, `TokensPerSecond` derived from them
- **`BenchmarkRun.Combine()`** — static factory that merges results from multiple per-model runs into a single aggregated run for multi-model dashboards; each result retains its own `ModelAlias`/`BackendTarget` for per-row filtering
- **`BenchmarkRunner` resilience** — individual case exceptions are caught, recorded with zero scores, and the run continues; `OperationCanceledException` still propagates; dashboard writes are guarded inside the try-chain
- **`BenchmarkRunner` live dashboard integration** — `RunAsync` accepts `dashboardOutputDir` and `onResultAdded` callbacks; opens the dashboard HTML in the default browser at run start and writes the sidecar JSON after every case; final Markdown/JSON reports written via `BenchmarkReportGenerator.WriteReports` on completion
- **`BenchmarkRunner` performance instrumentation** — time-to-first-token (TTFT) measured per case; token counts estimated via `EstimateTokens` (word count × 1.3); electricity cost estimated from `BENCHMARK_GPU_WATTS` (default 115 W) and `BENCHMARK_ELECTRICITY_RATE` (default £0.2015/kWh) env vars
- **`BenchmarkDashboardGenerator`** (`Reporting/BenchmarkDashboardGenerator.cs`) — generates a live benchmark dashboard: static HTML shell (written once) + a `.js` data sidecar (updated after every case); uses a JSONP-style polling approach (5 s interval) that works on `file://` origins where `fetch()` is blocked by CORS; shows progress ring, pass rate, avg score, throughput, elapsed time, and per-case result rows with category and GPU/CPU filter chips
- **`PassThreshold` constant** on `BenchmarkReportGenerator` — centrally-defined pass threshold (80); used by `BenchmarkDeltaAnalyzer`, `BenchmarkReportGenerator`, and `BenchmarkSuiteTests` so the threshold is a single source of truth
- **Model comparison table** in Markdown report — when results span two or more distinct model+backend combinations, `GenerateMarkdownReport` emits a `## Model Comparison` section with pass rate, avg score, avg tok/s, and total electricity cost per model
- **Detailed per-result metrics** in Markdown report — per-case stat table now includes TTFT, generation time, throughput (tok/s), input/output token counts, per-token cost, electricity cost, and total cost columns
- **`ContaminationDetector.HasCodeFence()` / `HasHedging()`** — two new static detectors; `HasCodeFence` flags markdown code fence lines (``` or ~~~); `HasHedging` flags filler phrases like "perhaps", "it's worth noting", "as you may know"
- **Partial scoring in `RuleBasedEvaluator`** — code-fence violations score 10/20 in the NoPreamble bucket (half credit: content present but formatted wrong); hedging violations score 10/20 in the NoClosingRemarks bucket
- **`LiveBenchmarkTests`** (`Tests/LiveBenchmarkTests.cs`) — full end-to-end benchmark that iterates all hardware-eligible models (≤10B params) on both GPU and CPU paths, accumulates results into a single combined live dashboard with GPU/CPU filter support; 12-hour ceiling; run via `dotnet test --filter "Category=LiveBenchmark"`
- **`AIBenchmarkLiveDashboardUITests`** (`SmrtPad.UITests/Tests/`) — Appium-driven benchmark suite that submits all 73 catalog cases via the live app, evaluates each response with `RuleBasedEvaluator`, and streams live output to the test output pane; updates the live dashboard HTML after every case
- **`InternalsVisibleTo("SmrtPad.UITests")`** added to `SmrtPad.AI.Benchmarks` assembly so the UITests project can access internal benchmark types
- **Benchmark result artifacts** (`BenchmarkResults/`) — HTML dashboards and JS data sidecars from multi-model benchmark runs committed to repo for historical reference

### Fixed
- **Implicit `<think>` blocks** — phi-4-mini and similar reasoning models emit their chain-of-thought without an opening `<think>` tag, closing only with `</think>`; the stream parser now retroactively moves all accumulated answer content to `thinkBuilder` on `</think>`, keeping the chat bubble clean during reasoning
- **Streaming thread safety** — `answerBuilder`, `thinkBuilder`, and `insertBuilder` were read inside `DispatcherQueue.TryEnqueue` closures while being mutated on the model thread; replaced with snapshots captured on the model thread before enqueuing
- **`BubbleText` whitespace fallback** — `IsNullOrEmpty` → `IsNullOrWhiteSpace`; model-generated newlines between `</think>` and `<insert>` tags no longer prevent the fallback to `InsertText`
- **Benchmark WinAppDriver/Appium port conflict** — `start-benchmark.ps1` now starts WinAppDriver on port 4727 (was default 4723); Appium health check distinguishes Appium 2.x (`{"value":{"ready":true}}`) from WinAppDriver (`{"status":0,...}`) responses, preventing false-positive "already running" detection that caused Appium to never start
- **Benchmark session creation** (`BenchmarkAppFixture`) — primary session path uses the published exe with `launchViaAppId: false` (COM activation → hwnd attach) to avoid W3C `appium:` capability prefix rejection by WinAppDriver 1.2.x; AUMID launch is used as fallback
- **`HeadroomFactor` arithmetic** in `ModelSizeSelector` — was `1.10` (10% overhead), now `1.0 / 0.9` (correctly reserves ≥10% of the budget; 90% occupancy ceiling)
- **Residual streaming buffer not flushed** in `SmartSidebar.onComplete` — any content remaining in `rawBuffer` at stream end is now appended to `answerBuilder` before the final tag-stripping and display logic runs, preventing partial responses from being silently discarded
- **`BenchmarkDeltaAnalyzer` crash on empty result sets** — `Average()` on an empty sequence threw `InvalidOperationException`; guarded with `Count > 0` checks before averaging
- **`BenchmarkSuiteTests` default score threshold** raised from 60 to 80 to match `PassThreshold`
- **`RuleBasedEvaluator` keyword scoring edge case** — removed the `ExpectedKeywords.Length > 0` guard that returned 20/20 when no keywords were listed; the formula now always evaluates proportionally (zero keywords → zero content points)

### Changed
- **`FinalizeStreamingEntry`** trims whitespace-only answer text (`.Trim()`) for non-freeform skills before storing in `entry.Text`
- **`UpdateStreamingEntryWithThinking`** accepts optional `insertText` parameter to update `entry.InsertText` live during streaming
- **`SetField<T>`** on `SidebarChatEntry` now returns `bool` to allow chained `PropertyChanged` notifications for `BubbleText`
- **`HardwareBadge`** automation help text updated on each inference metrics update so benchmark tooling can read tokens-per-second via UIA without relying on tooltip visibility
- **`BenchmarkReportGenerator` pass threshold** moved from hard-coded `70` to `PassThreshold = 80` constant; all pass/fail formatting, category tables, overall stats, and per-result status icons updated
- **`BenchmarkSuiteTests` default threshold** changed from `60` to `80` (aligns with `PassThreshold`)
- **`BenchmarkRunner.RunAsync` signature** extended with `dashboardOutputDir`, `onResultAdded` parameters (both optional, non-breaking)
- **`SmrtPad.UITests` target framework** bumped from `net10.0-windows10.0.19041.0` to `net8.0-windows10.0.26100.0`
- **`SmrtPad.UITests` project references** — added reference to `SmrtPad.AI.Benchmarks` to enable sharing of `BenchmarkPromptCatalog`, `RuleBasedEvaluator`, and `BenchmarkDashboardGenerator` with the UITest benchmark suite
- **`BenchmarkPromptCatalog` object initializer style** — `new BenchmarkCase(…)` → `new(…)` throughout for readability; prompt descriptions tightened
- **Tag-compliance cases** — `ExpectedKeywords` changed from empty arrays to meaningful hint words (e.g. `["name","title"]`, `["page","paragraph"]`), allowing the keyword completeness score to validate chat answers meaningfully

---

## [0.6.0] - 2026-03-27

### Added
- **Live AI initialization progress**
- **Download progress polling** â€” `ConcreteFoundryModelAdapter` polls the model cache directory every 800 ms during `DownloadAsync` (which has no `IProgress<T>` overload) and emits `AI_STAGE_DOWNLOADING\t{alias}\t{mb}\t{pct}` tokens; `IsCachedAsync` is checked first and the download stage is skipped entirely when the model is already cached; `GetExpectedBytes` reads variant size via reflection with a silent fallback to indeterminate mode
- **Status bar mirroring** â€” `SmartSidebar.ReportStatus` (`Action<string>?` property) is wired in `MainWindow.ToggleSmrtSidebarAsync` to `ViewModel.UpdateStatus`; every progress stage update also updates the app status bar; bar is cleared when AI reaches ready state
- **Model selector UI** â€” `OptionsButton` flyout with `ModelSubMenu` (`MenuFlyoutSubItem`) listing all hardware-eligible models as `RadioMenuFlyoutItem`; active model shown with checkmark; selecting an alias calls `SetPreferredModelAlias` + `ResetAsync` + re-initializes
- **Execution target selector** â€” `ExecutionTargetSubMenu` (`MenuFlyoutSubItem`) in the same flyout with NPU / GPU / CPU options; selecting a target calls `SetPreferredExecutionTarget` + `ResetAsync` + re-initializes; menu auto-hides unavailable targets
- **`ModelSizeSelector.TryGetAlias`** â€” looks up a named alias and returns its GPU and CPU footprints in MB; used by `CreateFoundryModelAdapterAsync` to honour the user's model choice with the correct footprint for the active execution target
- **`ModelSizeSelector.GetBestAliasForCapability`** â€” returns the alias of the best-fitting model for a given `AIBackendCapability` without running `SelectBestAliasAsync`; used by `AIDispatcher.ActiveModelAlias`
- **`ModelSizeSelector.GetEligibleAliases`** â€” returns all model aliases that fit the hardware budget, ordered best-first; used to populate the model selector menu
- **`ModelSizeSelector.PickContextTokens(long footprintMb)`** overload â€” returns `BaseContextTokens` for a user-forced alias (no hardware headroom calculation needed)
- **`IAIDispatcher.ActiveModelAlias`** â€” alias of the model actually loaded during the last initialization, or `null` pre-init
- **`IAIDispatcher.SetPreferredModelAlias` / `PreferredModelAlias`** â€” user-chosen model alias; `null` = automatic hardware selection
- **`IAIDispatcher.SetPreferredExecutionTarget` / `PreferredExecutionTarget`** â€” user-chosen execution target key (`"PhiSilicaNpu"`, `"FoundryLocalGpu"`, `"FoundryLocalCpu"`); `null` = automatic
- **`IAIDispatcher.GetEligibleModelAliases`** â€” returns hardware-eligible aliases ordered best-first
- **`IAIDispatcher.ResetAsync`** â€” disposes the current model and resets initialized state so the dispatcher can be re-initialized with a different alias or target
- **i18n stage strings** â€” `SmartSidebarStageProbing`, `SmartSidebarStageSelecting`, `SmartSidebarStageService`, `SmartSidebarStageCached`, `SmartSidebarStageDownloading`, `SmartSidebarStageDownloadingPct`, `SmartSidebarStageLoading` added to all 9 locale `.resw` files
- **i18n model/target selector strings** â€” `SmartSidebarModelSelector`, `SmartSidebarExecutionTarget`, `SmartSidebarOptions`, `SmartSidebarExecutionTargetSeparator` added to all 9 locale files

### Changed
- **`AIDispatcher` factory delegate** â€” signature updated from `Func<AIExecutionTarget, HardwareProbeResult, CancellationToken, Task<ILanguageModelAdapter>>` to `Func<AIExecutionTarget, HardwareProbeResult, Action<string>?, CancellationToken, Task<ILanguageModelAdapter>>`; `onProgress` threaded through `AIDispatcherFactory` â†’ `CreateFoundryModelAdapterAsync` â†’ `ConcreteFoundryModelAdapter.CreateAsync`
- **`ConcreteFoundryModelAdapter.CreateAsync`** â€” accepts `Action<string>? onProgress`; checks `IsCachedAsync` before calling `DownloadAsync`; starts `PollDownloadProgressAsync` as a background `Task` cancelled immediately after download; emits `AI_STAGE_SERVICE`, `AI_STAGE_CACHED`, `AI_STAGE_LOADING` tokens
- **`SmartSidebar.InitializeDispatcherAsync`** â€” passes a `DispatcherQueue.TryEnqueue`-wrapped progress callback to `_dispatcher.InitializeAsync`; init timeout raised from 60 s to 120 s to accommodate large model downloads
- **`SmartSidebar.GetModelName`** â€” now prefers `_dispatcher.ActiveModelAlias`, then `PreferredModelAlias`, then falls back to the execution target display name; removes hard-coded `phi-3.5-mini-instruct` fallback
- **`AIBackendAvailability` mapping in `AIDispatcherProxy`** â€” `GpuVramMb` and `AvailableSystemRamMb` fields are now correctly mapped from the dynamic capability object
- **`ModelSizeSelector.SelectBestAliasAsync`** â€” updated to use `(Alias, GpuMb, CpuMb)` tuples; `footprintMb` resolved based on `isGpu` flag so CPU targets use CPU footprints, preventing over-sized download attempts

### Fixed
- **CPU model download failure** â€” `CreateFoundryModelAdapterAsync` now passes CPU footprint (not GPU footprint) to `ModelSizeSelector` when the execution target is `FoundryLocalCpu`; previously the GPU footprint was used for both targets, causing the service to request a variant too large for the system
- **`SmrtPad.AI.csproj` native DLL resolution** â€” added `<RuntimeIdentifier>` based on `$(Platform)` and `<AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>` so `Microsoft.AI.Foundry.Local.Core.dll` and other native DLLs are placed flat in the build output directory where `AIAssemblyLoadContext` and the WAP copy target can find them
- **WAP project native DLL tracking** â€” added `UpToDateCheckInput`/`UpToDateCheckOutput` entries for `Microsoft.AI.Foundry.Local.Core.dll` and a `CopySmrtPadAiNativeRuntimeOutputs` copy step for the `runtimes/{rid}/native/` layout, ensuring VS incremental build re-deploys the native DLL when it changes

---

## [0.5.0] - 2026-03-26

### Added
- **ResponseCleaner**
- **SidebarChatEntry** (`SmrtPad/Controls/SidebarChatEntry.cs`) â€” INPC chat-message model carrying role, text, streaming flag, thinking text, thinking-phase flag, and thinking-label; backed by `SetField` to minimise PropertyChanged noise
- **SidebarChatTemplateSelector** (`SmrtPad/Controls/SidebarChatTemplateSelector.cs`) â€” `DataTemplateSelector` that routes `SidebarChatRole.User` entries to a user-bubble template and all other entries to an assistant-bubble template
- **NonEmptyStringToVisibilityConverter** (`SmrtPad/Converters/NonEmptyStringToVisibilityConverter.cs`) â€” one-way converter; `Visibility.Visible` when string is non-empty, `Visibility.Collapsed` otherwise; used in sidebar thinking-panel binding
- **DXGI VRAM enumeration** in `HardwareProbeService` â€” `QueryDxgiVramMb()` uses `CreateDXGIFactory1` P/Invoke and raw vtable dispatch (`IDXGIFactory1::EnumAdapters1`, `IDXGIAdapter1::GetDesc1`) to read `DedicatedVideoMemory` for every adapter; skips Microsoft Basic Render Driver (software adapter)
- **WMI VRAM fallback** â€” `QueryWmiVramMb()` falls back to `Win32_VideoController.AdapterRAM` via `System.Management` when DXGI returns zero (some NVIDIA configurations)
- **Available system RAM query** â€” `QueryAvailableRamMb()` uses `GlobalMemoryStatusEx` P/Invoke with a GC `GetGCMemoryInfo` fallback; result stored in `AIBackendCapability` and used by `ModelSizeSelector`
- **Freeform chat prompt** â€” `PromptTemplates.FreeformChat(message)` routes open-ended user questions directly to the model with an explicit writing-assistant persona and clean-output instructions
- **Skill-key routing in AIDispatcher** â€” `StreamResponseAsync` now accepts a `skillKey` string and builds the final prompt internally via `PromptTemplates`; callers no longer format prompts themselves
- **New i18n strings** across all 9 locales â€” `SmartSidebarSummarize`, `SmartSidebarToneRewrite`, `SmartSidebarNewSession`, `SmartSidebarApplySkill`, `SmartSidebarSkillPlaceholder`, `SmartSidebarThinkingLabel`, `SmartSidebarThinkingDoneLabel`
- `InternalsVisibleTo SmrtPad.AI.Tests` added to `SmrtPad.AI.csproj` so `ModelSizeSelector` internal methods are reachable from tests

### Changed
- **SmartSidebar redesigned** â€” replaced seven independent per-skill accordion sections with a unified chat-bubble UI; skills are selected via a `ComboBox` dropdown (`SkillDropdown`) and dispatched through a single `ApplySkillButton_Click`; chat history rendered in a `ListView` of `SidebarChatEntry` items using `SidebarChatTemplateSelector`; thinking/reasoning panel shown as a collapsible `Expander` during model inference
- **Tone toggle** is now hidden until the `tone-professional` skill is selected, eliminating clutter for all other skills
- **AIDispatcherFactory** model-factory delegate signature changed to `Func<AIExecutionTarget, HardwareProbeResult, CancellationToken, Task<ILanguageModelAdapter>>`; `HardwareProbeResult` carries the probed `AIBackendCapability` through to `CreateFoundryModelAdapterAsync` which feeds it to `ModelSizeSelector`
- **ConcreteFoundryModelAdapter** â€” `MaxContextTokens` constant removed; context window is now an instance field set at construction from the hardware-selected value; `ResolveModelAlias` removed; `alias` and `maxContextTokens` are passed into `CreateAsync` from `ModelSizeSelector`
- **Prompt templates hardened** â€” all six skill prompts (`Rewrite`, `GrammarFix`, `Shorten`, `AutoComplete`, `OcrFallback`, and the existing `Summarize`/`Tone` prompts) now open with an explicit "You are a writing assistant" persona and instruct the model to return only the result with no preamble or labels, reducing unwanted boilerplate in output
- **`AllowUnsafeBlocks` enabled** in `SmrtPad.AI.csproj` to support DXGI vtable P/Invoke
- `System.Management` v9.0.7 added to `Directory.Packages.props` and referenced in `SmrtPad.AI.csproj`
- `SmrtPad.slnx` platform mappings updated â€” `SmrtPad.AI` and `SmrtPad.AI.Tests` explicit overrides removed (inherit solution defaults); `SmrtPad.Tests` and `SmrtPad.UITests` gain explicit ARM64/x86 `Build=false` entries
- **Package publisher** changed from `CN=John_` to certificate thumbprint GUID for code-signing

### Fixed
- **File backstage navigation hover** â€” removed `_suppressSelectionEvent` workaround that set `Nav.SelectedItem` on hover, which caused subsequent clicks to not fire `SelectionChanged`; backstage pane now previewed on `PointerEntered` without touching the selection

### Refactored
- `IAIDispatcher` and `AIDispatcherProxy` updated to match the new `skillKey` parameter on `StreamResponseAsync`

---

## [0.4.0] - 2026-03-11

### Added
- **App icon**
- **SmrtDoodle install check** â€” `PaintDrawing_Click` calls `IsSmrtDoodleInstalled()` before launching; checks `%LOCALAPPDATA%\Microsoft\WindowsApps\SmrtDoodle.exe` (Store/MSIX install) and every directory on `PATH`; if not found shows a `ContentDialog` with a **Get from Store** primary button that opens `ms-windows-store://search/?query=SmrtDoodle`; removed the crash-prone built-in fallback drawing dialog (`ShowBuiltInDrawingDialogAsync`)
- `SmrtDoodleGetFromStore` resource string added to all 9 locale files

### Changed
- **SmrtDoodle ribbon button** â€” redesigned to match all other ribbon buttons: `StackPanel` with a 22 px `Image` icon above a `TextBlock "SmrtDoodle"` label; button width reduced 72 â†’ 52, padding corrected to 0; tooltip updated to `"SmrtDoodle - Create A Drawing"`
- `SmrtDoodleNotFoundMessage` resource updated to reference the Microsoft Store
- **SmrtDoodle assets** â€” `Assets/SmrtDoodle.png` and `Assets/SmrtDoodle-LM.png` replaced with new clean icons (no baked-in text)
- **Theme-aware title bar** â€” caption button foreground/hover/press colours set via `AppWindowTitleBar` to match the app's current light/dark/system theme; updated on theme toggle
- **SmrtDoodle button ThemeResource** â€” `App.xaml` ThemeDictionaries serve `SmrtDoodle-LM.png` in Light and `SmrtDoodle.png` in Dark/HighContrast themes via `BitmapImage x:Key="SmrtDoodleSource"`
- Macro menu items now all carry visible `Text` labels alongside their icons

### Fixed
- **File backstage background** â€” changed from semi-transparent `LayerFillColorDefaultBrush` (â‰ˆ4% opaque in dark mode with Mica) to fully opaque `SolidBackgroundFillColorBaseBrush` so the backstage properly covers the tab strip and document editor when open
- Font family `ComboBox` now shows the document's current font on window load; re-entrancy guarded with `_suppressFontComboChange` to prevent unwanted font changes during programmatic sync
- Replaced `RadioButton` with `ToggleButton` for paragraph alignment buttons to fix WinUI 3 runtime crash (`E_INVALIDARG` / `E_UNEXPECTED`) caused by applying `DefaultToggleButtonStyle` to `RadioButton`
- Alignment buttons now enforce mutual exclusivity via code-behind helper
- Paragraph alignment button icons are now horizontally and vertically centred
- Font size dropdown width doubled (56 â†’ 112 px) so selected values are fully visible
- Color swatch buttons now render as filled circles â€” replaced `Border`+`Rectangle` (no explicit size) with `Ellipse` (20Ã—20) for font color and highlight color grids
- Line spacing no longer produces massive gaps â€” removed erroneous `Ã— 12` multiplier; `LineSpacingRule.Single`/`OneAndHalf`/`Double` used for standard values
- Exit no longer throws `winrt::hresult_error` â€” removed duplicate `Application.Current.Exit()` from backstage, replaced with `Window.Close()`

### Performance
- `DocxExportHelper` RTF hex parsing â€” `body.Substring(i+1, 2)` replaced with `body.AsSpan(i+1, 2)` to avoid a heap allocation per character

### Refactored
- `NewDocument_Fires_MultiplePropertyChangedEvents` test converted to object-initializer style for `EditorViewModel`

---

## [0.3.0] - 2025-06-25

### Added
- Comprehensive unit test suite
- `ColorHelper` utility class extracted for hex color parsing
- `ParseHexColor` supports both 6-digit (`#RRGGBB`) and 8-digit (`#AARRGGBB`) formats

## [0.2.0] - 2025-06-25

### Added
- **Clipboard group** with large Paste button and stacked Cut/Copy (WordPad pattern)
- **Color swatch grids** for font color and text highlight color (UltraPad-style)
- **List type dropdown** with None, Bullet, Number, Lowercase/Uppercase Letter, Lowercase/Uppercase Roman
- **Line spacing dropdown** with 1.0, 1.15, 1.5, 2.0 options
- **Zoom level display** in status bar
- ViewModel properties: `IsWordWrap`, `ZoomLevel`, `ListType`, `LineSpacing`
- ViewModel commands: `ZoomIn`/`ZoomOut`, `SetListType`, `SetLineSpacing`, `ToggleWordWrap`

### Changed
- Redesigned ribbon to match Microsoft WordPad layout using UltraPad as reference
- Replaced plain `Button` controls with `ToggleButton` for B/I/U/S/Sub/Sup formatting
- Converted alignment buttons to `RadioButton` group for mutual exclusivity
- Redesigned Insert group with tall icon+label buttons (Picture, Paint, Object, Date/Time)
- Redesigned Editing group with proper Find/Replace flyout dialogs
- Updated all icon references from Segoe MDL2 Assets to Segoe Fluent Icons

## [0.1.0] - 2025-06-24

### Added
- Initial WordPad-style ribbon with Font, Paragraph, Insert, and Editing groups
- Rich text editing with RTF and TXT support
- File backstage view (New, Open, Save, Save As, Print, Options, Exit)
- Quick-access toolbar (Save, New, Undo, Redo)
- Mica backdrop, Edit/View menus
- MVVM architecture with `EditorViewModel` and CommunityToolkit.Mvvm
- Basic unit tests for ViewModel
