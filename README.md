# SmrtPad

[![CI](https://github.com/John-Donnelly/SmrtPad/actions/workflows/ci.yml/badge.svg)](https://github.com/John-Donnelly/SmrtPad/actions/workflows/ci.yml)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?logo=windows&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![WinUI 3](https://img.shields.io/badge/WinUI-3-0078D4)
[![License](https://img.shields.io/badge/license-PolyForm%20Noncommercial%201.0.0-blue.svg)](LICENSE)
[![Release](https://img.shields.io/github/v/release/John-Donnelly/SmrtPad?label=download&logo=github)](https://github.com/John-Donnelly/SmrtPad/releases/latest)

A modern WordPad-inspired rich text editor built with WinUI 3 and .NET 8, featuring a Microsoft WordPad-style ribbon interface, tabbed documents, macro recording, a full suite of export options, and an on-device AI writing assistant powered by ONNX Runtime GenAI (CUDA/CPU) / Windows AI APIs.

## Download

Grab the latest portable build from [Releases](https://github.com/John-Donnelly/SmrtPad/releases/latest) — unzip and run `SmrtPad.exe`. Self-contained, so no .NET runtime is needed.

> Builds are currently unsigned; Windows SmartScreen will warn on first run.
> Choose **More info** → **Run anyway** if you trust the source.

## Features

### Ribbon Toolbar
- **Clipboard** — Large Paste button with stacked Cut/Copy and Paste Special (plain text)
- **Font** — Font family picker, size selector, grow/shrink buttons, Bold/Italic/Underline/Strikethrough/Subscript/Superscript toggles, font color and text highlight color swatch grids
- **Paragraph** — Indent increase/decrease, list type dropdown (None, Bullet, Numbers, Lowercase/Uppercase Letters, Lowercase/Uppercase Roman numerals), line spacing selector (1.0, 1.15, 1.5, 2.0), alignment toggle buttons (Left, Center, Right, Justify)
- **Insert** — Picture, SmrtDoodle drawing, Insert Object (images), Date/Time — all with large icon + label buttons
- **Editing** — Find and Replace with flyout dialogs, Select All

### Editor
- Rich text editing via WinUI 3 `RichEditBox` (RTF and TXT open/save)
- **Tabbed documents** — multiple documents open simultaneously; per-tab file, modified, encoding and zoom state
- Undo/Redo, Word wrap toggle, spell check toggle (persisted in settings)
- **True visual zoom** via `ScaleTransform` — Ctrl+Plus/Minus and Ctrl+Scroll; per-tab zoom level
- Horizontal and vertical rulers (inches and centimetres)
- Page view mode (US Letter with 1-inch margins)
- Focus mode (hides ribbon and status bar)
- Drag-and-drop open (RTF, TXT, DOCX, HTML, ODT) and inline image insert

### File Operations
- New, Open (RTF, TXT, DOCX, HTML, ODT), Save, Save As, Print
- **Export to PDF** — multi-page PDF 1.4 (Helvetica, A4, 72 pt margins)
- **Export to DOCX** — valid OOXML `.docx` via `ZipArchive` + `XDocument`
- **Save to OneDrive** — saves via standard file picker to the user's OneDrive folder; guarded with availability check
- Auto-save to recovery folder for unnamed documents; saves in-place for named documents
- Recent files list (MRU, max 10) in the backstage

### File Backstage
- WordPad-style backstage for New, Templates, Open, Save, Save As, Print, Export PDF, Export DOCX, OneDrive, Options, and Exit
- **Document Templates** — 5 built-in templates (Blank, Letter, Meeting Notes, To-Do List, Report)
- Fully opaque overlay that covers the tab strip and editor when open

### SmrtDoodle Integration
- **SmrtDoodle** ribbon button launches the SmrtDoodle companion drawing app via the `smrtdoodle://` protocol; the current selection (if any) is sent as the initial canvas image over a per-session named pipe
- When SmrtDoodle closes, the rendered drawing is returned to SmrtPad via the same named pipe and the user is prompted to **Replace selection** or **Insert as new image**
- Pre-launch installation check via `Launcher.QueryUriSupportAsync`; if SmrtDoodle is not installed a dialog offers a **Get from Store** button that opens the Microsoft Store search for SmrtDoodle
- Shared bridge contract and frame serialization live in the `SmrtAI.Core` library, reused by both apps

### Smart Sidebar (on-device AI)
- Collapsible AI panel that operates entirely on-device using Windows AI APIs (Phi Silica NPU) or ONNX Runtime GenAI (GPU/CPU)
- **Chat-bubble UI** — user and assistant messages rendered as distinct bubbles in a scrollable history; responses stream token-by-token into the chat bubble in real time so the full output is always visible in the conversation
- **Insert button** — appears on assistant bubbles that contain insertable content (rewrites, summaries, drafts); clicking it inserts the AI-generated text directly into the active document at the cursor position
- **Thinking/reasoning display** — reasoning tokens emitted between `<think>…</think>` tags (including implicit pre-`</think>` reasoning from phi-4-mini) shown in a collapsible expander labelled "Thinking…"; collapses automatically once reasoning is complete so the response is front and centre
- **Skill dropdown** — one unified `ComboBox` selects the active skill (Summarize, Professional tone, Rewrite for clarity, Grammar fix, Shorten, Complete at cursor); single **Apply** button dispatches to the correct prompt template
- **Tone toggle** shown only when the tone skill is active (professional / casual)
- **Freeform chat** — open-ended writing questions answered in plain text; document-drafting requests (letter, email, report, essay, story, press release, etc.) produce a full draft inserted via the Insert button
- **Fixed model: Gemma 4 E2B** — always uses the HauhauCS Aggressive Q4_K_P GGUF (~3.5 GB); downloaded automatically from HuggingFace Hub on first use; context window set to 8 192 tokens
- **Live initialization progress** — sidebar status text and app status bar both update in real time as the AI initializes: detecting hardware → selecting model → starting service → downloading `{model}` (`{n} MB`) at `{n}%` → loading into memory; download percentage is computed by polling the model cache directory every 800 ms
- **ResponseCleaner** strips preamble lines, code-fence delimiters, closing remarks, and reasoning-leak fragments from model output before text is shown or inserted
- Prompt templates hardened with explicit, model-neutral output instructions (no persona dependency; consistent `<insert>…</insert>` contract across all skills)
- New-session button clears chat history

### Macros
- Macro recording and playback — record a sequence of typing and formatting actions, then replay them; persisted in settings

### Multi-Window
- Open multiple editor windows (`Ctrl+Shift+N` / Window menu); each window is fully independent

### UI & Theming
- **Mica backdrop** for a modern Windows 11 appearance
- Light / Dark / System theme toggle (persisted in settings); theme-aware title bar caption button colours
- Segoe Fluent Icons throughout the ribbon
- **App icon** (`SmrtPad.ico`) shown in the taskbar, title bar, and Alt-Tab thumbnail
- Status bar — document status, word count, character count, selection length, line/column, encoding, and zoom level
- Localization in **9 languages** — English, German, Spanish, French, Japanese, Simplified Chinese, Arabic, Russian, Urdu

### Options
- Font, size, word wrap, save format, theme, auto-save interval, language, ruler units, and spell check — all persisted via `SettingsService` (JSON at `%LOCALAPPDATA%\SmrtPad\settings.json`)

## Requirements

- Windows 10 version 1809 (build 17763) or later
- .NET 8 SDK
- Windows App SDK 1.8+
- (Optional) [SmrtDoodle](https://www.microsoft.com/store/apps) for in-document drawing
- (Optional) A CUDA-capable GPU for on-device AI; CPU fallback is supported. The AI engine uses LLamaSharp / llama.cpp with Gemma 4 E2B GGUF (downloaded automatically from HuggingFace Hub)

## Building

1. Clone the repository:
   ```
   git clone https://github.com/John-Donnelly/SmrtPad.git
   ```
2. Open `SmrtPad.slnx` in Visual Studio 2022 or later.
3. Set the platform to **x64** (or ARM64).
4. Build and run the **SmrtPad (Package)** project for a fully packaged experience, or the **SmrtPad** project for unpackaged debug.

## Running Tests

```
dotnet test SmrtPad.Tests\SmrtPad.Tests.csproj -c Debug -p:Platform=x64
dotnet test SmrtPad.AI.Tests\SmrtPad.AI.Tests.csproj -c Debug -p:Platform=x64
```

### AI benchmark UI tests

SmrtPad includes a local Appium-based benchmark suite for the Smart Sidebar AI models. The benchmark exercises all supported sidebar skills across a curated prompt set, records latency and throughput, applies rule-based scoring, estimates electricity cost, and generates report artifacts for qualitative review.

For models that support reasoning (Qwen3, Phi-4 Mini Reasoning, DeepSeek-R1), each model is benchmarked in both **no-thinking** and **thinking** modes. GPU runs execute first (both modes), followed by CPU runs (both modes where applicable).

#### Prerequisites

- Build and deploy the packaged **SmrtPad (Package)** app so the local AUMID is registered.
- Install [Appium](https://appium.io/) via npm.
- Install the Appium Windows driver:

  ```powershell
  appium driver install --source=npm appium-windows-driver
  ```

- Install WinAppDriver 1.2.1.
- Start the local benchmark infrastructure:

  ```powershell
  powershell -ExecutionPolicy Bypass -File .\SmrtPad.UITests\Scripts\start-benchmark.ps1
  ```

#### Benchmark tests

- Smoke validation:

  ```powershell
  dotnet test .\SmrtPad.UITests\SmrtPad.UITests.csproj -c Debug -p:Platform=x64 --filter "FullyQualifiedName=SmrtPad.UITests.Tests.ModelBenchmarkTests.SmokeTest_SinglePrompt"
  ```

- Full benchmark:

  ```powershell
  dotnet test .\SmrtPad.UITests\SmrtPad.UITests.csproj -c Debug -p:Platform=x64 --filter "FullyQualifiedName=SmrtPad.UITests.Tests.ModelBenchmarkTests.RunFullBenchmark"
  ```

#### Environment variables

- `BENCHMARK_MODEL_FILTER` — comma-separated model alias filter for subset runs, for example `phi-4-mini` or `phi-4-mini,qwen2.5-1.5b`
- `BENCHMARK_GPU_WATTS` — GPU power draw override, default `115`
- `BENCHMARK_CPU_WATTS` — CPU power draw override, default `105`
- `BENCHMARK_NPU_WATTS` — NPU power draw override, default `15`
- `BENCHMARK_ELECTRICITY_RATE` — electricity cost in USD per kWh, default `0.12`
- `SMRTPAD_BENCHMARK_MODE` — set to `CPU` to skip GPU runs and only execute CPU runs; omit (or set to `GPU`) for the default GPU-first-then-CPU matrix
- `SMRTPAD_LLAMA_BACKEND_DIR` — absolute path to a custom llama.cpp backend directory (must contain `llama.dll` + `ggml.dll`); used when the default CUDA 12 build does not support a required model architecture (e.g., Gemma 4)

#### Generated artifacts

Each benchmark run produces:

- `benchmark-report-*.md` — Markdown summary report (includes **Mode** column distinguishing Think / NoThink runs)
- `benchmark-dashboard-*.html` — static Chart.js live dashboard (per-model series split by reasoning tag)
- `benchmark-results-*.json` — raw machine-readable results
- `bench-*-responses.jsonl` — per-case response log (JSONL)
- `qualitative-assessment-prompt-*.md` — prompt for chat-based qualitative review

Artifacts are written to `BenchmarkResults\` at the solution root when discoverable. If the solution root cannot be resolved from the test host, the suite falls back to `%TEMP%\SmrtPad-BenchmarkResults\`.

The test suite covers:
- ViewModel default values and all property-change notifications
- All formatting toggle commands (Bold, Italic, Underline, Strikethrough, Subscript, Superscript)
- Alignment, list type, and line spacing for all defined values
- Zoom in/out with min/max clamping; word wrap; spell check settings
- `NewDocument` full state reset
- `ColorHelper` hex parsing (6-digit, 8-digit, error cases)
- PDF generation (page count, header content, byte-array structure)
- DOCX generation (ZIP structure, `word/document.xml` content, paragraph mapping)
- OneDrive availability detection
- Document import (DOCX, ODT text extraction)
- Macro recording and playback
- Settings persistence, concurrency, and recent-files MRU
- Localization — all 9 locales, all resource keys present and non-empty
- **AI engine** — `ModelSizeSelector` alias/budget logic, `HardwareProbeService` VRAM/RAM probing, `PromptTemplates` output, `AIDispatcher` skill routing, `ResponseCleaner` output stripping
- **AI benchmark suite** — Smart Sidebar Appium automation, per-model prompt execution, rule-based scoring, electricity cost estimation, Markdown/HTML/JSON report generation, and qualitative-assessment prompt generation

## Project Structure

```
SmrtPad/
├── SmrtAI.Core/             # Shared AI abstractions and IPC contract (net8.0 library)
│   ├── IAIDispatcher.cs     # AI dispatcher interface (shared with SmrtPad.AI)
│   ├── AIDispatcherAvailability.cs  # Backend availability DTOs
│   ├── SemanticSearchModels.cs      # Semantic search records
│   ├── SmrtDoodleIpcContract.cs     # Protocol URI builder and pipe-name helpers
│   └── SmrtDoodleFrame.cs           # Length-prefixed JSON frame serializer
├── SmrtPad/
│   ├── Assets/              # App icon (SmrtPad.ico/.png), SmrtDoodle icons
│   ├── Controls/            # SmartSidebar, SidebarChatEntry, SidebarChatTemplateSelector
│   ├── Converters/          # NonEmptyStringToVisibilityConverter
│   ├── Helpers/             # ColorHelper, DocxExportHelper, DocumentImportHelper,
│   │                        # DocumentTemplates, MacroHelper, OneDriveHelper,
│   │                        # ParagraphStyleHelper, PdfHelper, ResourceHelper,
│   │                        # ResponseCleaner, RtfHelper, RulerHelper
│   ├── Models/              # DocumentTemplate
│   ├── Services/            # AIDispatcherProxy, DialogService, FileService,
│   │                        # SettingsService, SmrtDoodleIpcService
│   │                        # (+ IDialogService, IFileService, ISettingsService)
│   ├── Strings/             # 9 locale .resw files (en-US, de-DE, es-ES, fr-FR,
│   │                        # ja-JP, zh-Hans, ar-SA, ru-RU, ur-PK)
│   ├── ViewModels/          # EditorViewModel
│   ├── Views/               # FileBackstageView
│   ├── MainWindow.xaml      # Main window with ribbon UI
│   ├── MainWindow.xaml.cs   # Code-behind — editor logic, ribbon handlers
│   ├── App.xaml             # Application resources and ThemeDictionaries
│   └── App.xaml.cs          # Entry point, DI container, multi-window factory
├── SmrtPad.AI/
│   ├── Skills/              # AIRewriteSkill, AutoCompleteSkill, GrammarFixSkill,
│   │                        # ShortenSkill, SummarizerSkill, ToneShifterSkill
│   ├── AIDispatcher.cs      # Core streaming dispatcher with skill-key routing;
│   │                        # exposes PreferredReasoningMode / SetPreferredReasoningMode
│   ├── AIDispatcherFactory.cs # DI factory — hardware probing, model selection;
│   │                        # CreateFromLocalPath honours preferred reasoning mode
│   ├── ConcreteLlamaSharpModelAdapter.cs # LLamaSharp (llama.cpp) GGUF inference adapter;
│   │                        # CUDA 12 DLL pre-loading, ggml backend discovery,
│   │                        # reasoning-mode-aware chat templating
│   ├── ConcreteOrtGenAiModelAdapter.cs # ORT GenAI in-process inference adapter
│   ├── GgufModelCatalog.cs  # GGUF model registry (alias → HuggingFace repo + GPU size)
│   ├── ModelDownloadService.cs  # HuggingFace Hub model downloader
│   ├── HardwareProbeService.cs # DXGI VRAM + system RAM detection
│   ├── ModelPromptPolicy.cs # SupportsThinkingMode, NormalizeMode, BuildSystemPrompt,
│   │                        # ApplyPromptControls, DetectAliasFromPath
│   ├── ModelSizeSelector.cs # Hardware-budget → alias + context-token selection
│   └── PromptTemplates.cs   # Model-neutral per-skill prompt templates + FreeformChat
├── SmrtPad.AI.Tests/        # Unit tests for AI engine components
├── SmrtPad (Package)/       # MSIX packaging project
├── SmrtPad.Tests/
│   ├── EditorTests.cs       # ViewModel unit tests
│   ├── IntegrationTests.cs  # Helper + service integration tests
│   ├── LocalizationTests.cs # Locale completeness tests
│   └── ResponseCleanerTests.cs # ResponseCleaner unit tests
├── README.md
└── CHANGELOG.md
```

## Acknowledgments

Ribbon design inspired by [UltraPad](https://github.com/lixkote/ultrapad), a modernized WordPad replacement for Windows 11.

## License

SmrtPad is **source-available, not open source**, under the
[PolyForm Noncommercial License 1.0.0](LICENSE).

You may read, build and modify the source for any noncommercial purpose.
Commercial use — including redistribution, resale, or publishing to an
application store — is reserved to JAD Apps. For a commercial licence,
get in touch via [jadapps.app](https://jadapps.app).

© 2026 John Donnelly, trading as JAD Apps.

