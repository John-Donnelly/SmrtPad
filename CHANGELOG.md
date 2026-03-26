# Changelog

All notable changes to SmrtPad are documented in this file.

# Changelog

All notable changes to SmrtPad are documented in this file.

## [Unreleased]

### Added
- **ModelSizeSelector** (`SmrtPad.AI/ModelSizeSelector.cs`) — selects the best Foundry Local model alias and context-token limit at runtime based on probed GPU VRAM or available system RAM; ordered preference list from `phi-4-mini-reasoning` down to `qwen2.5-0.5b`; GPU/CPU headroom factors prevent OOM; context window scales proportionally to free headroom and is clamped to 512–16 384 tokens
- **ResponseCleaner** (`SmrtPad/Helpers/ResponseCleaner.cs`) — post-processes LLM output to strip preamble lines, code-fence delimiters, closing remarks ("Let me know if…"), and reasoning-leak lines emitted by thinking models; applied automatically before text is inserted into the document or displayed in the sidebar
- **SidebarChatEntry** (`SmrtPad/Controls/SidebarChatEntry.cs`) — INPC chat-message model carrying role, text, streaming flag, thinking text, thinking-phase flag, and thinking-label; backed by `SetField` to minimise PropertyChanged noise
- **SidebarChatTemplateSelector** (`SmrtPad/Controls/SidebarChatTemplateSelector.cs`) — `DataTemplateSelector` that routes `SidebarChatRole.User` entries to a user-bubble template and all other entries to an assistant-bubble template
- **NonEmptyStringToVisibilityConverter** (`SmrtPad/Converters/NonEmptyStringToVisibilityConverter.cs`) — one-way converter; `Visibility.Visible` when string is non-empty, `Visibility.Collapsed` otherwise; used in sidebar thinking-panel binding
- **DXGI VRAM enumeration** in `HardwareProbeService` — `QueryDxgiVramMb()` uses `CreateDXGIFactory1` P/Invoke and raw vtable dispatch (`IDXGIFactory1::EnumAdapters1`, `IDXGIAdapter1::GetDesc1`) to read `DedicatedVideoMemory` for every adapter; skips Microsoft Basic Render Driver (software adapter)
- **WMI VRAM fallback** — `QueryWmiVramMb()` falls back to `Win32_VideoController.AdapterRAM` via `System.Management` when DXGI returns zero (some NVIDIA configurations)
- **Available system RAM query** — `QueryAvailableRamMb()` uses `GlobalMemoryStatusEx` P/Invoke with a GC `GetGCMemoryInfo` fallback; result stored in `AIBackendCapability` and used by `ModelSizeSelector`
- **Freeform chat prompt** — `PromptTemplates.FreeformChat(message)` routes open-ended user questions directly to the model with an explicit writing-assistant persona and clean-output instructions
- **Skill-key routing in AIDispatcher** — `StreamResponseAsync` now accepts a `skillKey` string and builds the final prompt internally via `PromptTemplates`; callers no longer format prompts themselves
- **New i18n strings** across all 9 locales — `SmartSidebarSummarize`, `SmartSidebarToneRewrite`, `SmartSidebarNewSession`, `SmartSidebarApplySkill`, `SmartSidebarSkillPlaceholder`, `SmartSidebarThinkingLabel`, `SmartSidebarThinkingDoneLabel`
- `InternalsVisibleTo SmrtPad.AI.Tests` added to `SmrtPad.AI.csproj` so `ModelSizeSelector` internal methods are reachable from tests

### Changed
- **SmartSidebar redesigned** — replaced seven independent per-skill accordion sections with a unified chat-bubble UI; skills are selected via a `ComboBox` dropdown (`SkillDropdown`) and dispatched through a single `ApplySkillButton_Click`; chat history rendered in a `ListView` of `SidebarChatEntry` items using `SidebarChatTemplateSelector`; thinking/reasoning panel shown as a collapsible `Expander` during model inference
- **Tone toggle** is now hidden until the `tone-professional` skill is selected, eliminating clutter for all other skills
- **AIDispatcherFactory** model-factory delegate signature changed to `Func<AIExecutionTarget, HardwareProbeResult, CancellationToken, Task<ILanguageModelAdapter>>`; `HardwareProbeResult` carries the probed `AIBackendCapability` through to `CreateFoundryModelAdapterAsync` which feeds it to `ModelSizeSelector`
- **ConcreteFoundryModelAdapter** — `MaxContextTokens` constant removed; context window is now an instance field set at construction from the hardware-selected value; `ResolveModelAlias` removed; `alias` and `maxContextTokens` are passed into `CreateAsync` from `ModelSizeSelector`
- **Prompt templates hardened** — all six skill prompts (`Rewrite`, `GrammarFix`, `Shorten`, `AutoComplete`, `OcrFallback`, and the existing `Summarize`/`Tone` prompts) now open with an explicit "You are a writing assistant" persona and instruct the model to return only the result with no preamble or labels, reducing unwanted boilerplate in output
- **`AllowUnsafeBlocks` enabled** in `SmrtPad.AI.csproj` to support DXGI vtable P/Invoke
- `System.Management` v9.0.7 added to `Directory.Packages.props` and referenced in `SmrtPad.AI.csproj`
- `SmrtPad.slnx` platform mappings updated — `SmrtPad.AI` and `SmrtPad.AI.Tests` explicit overrides removed (inherit solution defaults); `SmrtPad.Tests` and `SmrtPad.UITests` gain explicit ARM64/x86 `Build=false` entries
- **Package publisher** changed from `CN=John_` to certificate thumbprint GUID for code-signing

### Fixed
- **File backstage navigation hover** — removed `_suppressSelectionEvent` workaround that set `Nav.SelectedItem` on hover, which caused subsequent clicks to not fire `SelectionChanged`; backstage pane now previewed on `PointerEntered` without touching the selection

### Refactored
- `IAIDispatcher` and `AIDispatcherProxy` updated to match the new `skillKey` parameter on `StreamResponseAsync`

---

### Added
- **App icon** — `SmrtPad.ico`
- **SmrtDoodle install check** — `PaintDrawing_Click` calls `IsSmrtDoodleInstalled()` before launching; checks `%LOCALAPPDATA%\Microsoft\WindowsApps\SmrtDoodle.exe` (Store/MSIX install) and every directory on `PATH`; if not found shows a `ContentDialog` with a **Get from Store** primary button that opens `ms-windows-store://search/?query=SmrtDoodle`; removed the crash-prone built-in fallback drawing dialog (`ShowBuiltInDrawingDialogAsync`)
- `SmrtDoodleGetFromStore` resource string added to all 9 locale files

### Changed
- **SmrtDoodle ribbon button** — redesigned to match all other ribbon buttons: `StackPanel` with a 22 px `Image` icon above a `TextBlock "SmrtDoodle"` label; button width reduced 72 → 52, padding corrected to 0; tooltip updated to `"SmrtDoodle - Create A Drawing"`
- `SmrtDoodleNotFoundMessage` resource updated to reference the Microsoft Store
- **SmrtDoodle assets** — `Assets/SmrtDoodle.png` and `Assets/SmrtDoodle-LM.png` replaced with new clean icons (no baked-in text)
- **Theme-aware title bar** — caption button foreground/hover/press colours set via `AppWindowTitleBar` to match the app's current light/dark/system theme; updated on theme toggle
- **SmrtDoodle button ThemeResource** — `App.xaml` ThemeDictionaries serve `SmrtDoodle-LM.png` in Light and `SmrtDoodle.png` in Dark/HighContrast themes via `BitmapImage x:Key="SmrtDoodleSource"`
- Macro menu items now all carry visible `Text` labels alongside their icons

### Fixed
- **File backstage background** — changed from semi-transparent `LayerFillColorDefaultBrush` (≈4% opaque in dark mode with Mica) to fully opaque `SolidBackgroundFillColorBaseBrush` so the backstage properly covers the tab strip and document editor when open
- Font family `ComboBox` now shows the document's current font on window load; re-entrancy guarded with `_suppressFontComboChange` to prevent unwanted font changes during programmatic sync
- Replaced `RadioButton` with `ToggleButton` for paragraph alignment buttons to fix WinUI 3 runtime crash (`E_INVALIDARG` / `E_UNEXPECTED`) caused by applying `DefaultToggleButtonStyle` to `RadioButton`
- Alignment buttons now enforce mutual exclusivity via code-behind helper
- Paragraph alignment button icons are now horizontally and vertically centred
- Font size dropdown width doubled (56 → 112 px) so selected values are fully visible
- Color swatch buttons now render as filled circles — replaced `Border`+`Rectangle` (no explicit size) with `Ellipse` (20×20) for font color and highlight color grids
- Line spacing no longer produces massive gaps — removed erroneous `× 12` multiplier; `LineSpacingRule.Single`/`OneAndHalf`/`Double` used for standard values
- Exit no longer throws `winrt::hresult_error` — removed duplicate `Application.Current.Exit()` from backstage, replaced with `Window.Close()`

### Performance
- `DocxExportHelper` RTF hex parsing — `body.Substring(i+1, 2)` replaced with `body.AsSpan(i+1, 2)` to avoid a heap allocation per character

### Refactored
- `NewDocument_Fires_MultiplePropertyChangedEvents` test converted to object-initializer style for `EditorViewModel`

## [0.3.0] - 2025-06-25

### Added
- Comprehensive unit test suite (53 tests) covering all ViewModel commands and properties
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
