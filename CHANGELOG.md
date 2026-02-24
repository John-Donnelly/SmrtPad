# Changelog

All notable changes to SmrtPad are documented in this file.

## [Unreleased]

### Added
- **37 new production-fix tests** (`ProductionFixTests.cs`) covering:
  - `Bullets_Click` ViewModel sync and macro recording (`BulletsClickContractTests` — 7 tests)
  - Macro `SetAlignment` playback RTF application and round-trip (`MacroSetAlignmentPlaybackTests` — 7 tests)
  - Debug logging removal verification (`AppDebugLoggingRemovedTests` — 3 tests)
  - Full macro command coverage across all 15 command types (`MacroHelperFullCommandCoverageTests` — 20 tests)

### Fixed
- **`Bullets_Click`** — now calls `ViewModel.SetListType()` (keeping `ViewModel.ListType` in sync with `IsBullets`) and `_macro.Record(MacroCommandType.SetListType, ...)` (bullets now recorded in macros) when toggling via the toolbar button
- **Macro `SetAlignment` playback** — `ExecuteMacroCommand` now applies the alignment directly to the RTF document's paragraph format in addition to updating the ViewModel; previously only `ViewModel.SetAlignment()` was called, so replaying a recorded alignment macro had no visible effect
- **`App.xaml.cs` debug logging** — removed leftover `System.IO.File.WriteAllText/AppendAllText` calls that wrote `SmrtPad_App_Startup.log` to `%TEMP%` on every application launch; startup diagnostics are not appropriate for production builds

### Added
- **Comprehensive UI test expansion** — expanded UI automation test suite from ~84 to 240 tests across 13 test classes, covering all application features methodically
  - **EditMenuUITests** (15 tests) — added Delete key, Backspace, Cut/Copy/Paste/Select All via Edit menu items, multiple Redo operations, Copy without selection safety, Paste into existing content
  - **FindReplaceUITests** (13 tests) — added Match Case filtering, Whole Word filtering, single Replace, Replace All with empty string (deletion), Find wraps around document, empty search box safety, Replace All changes character count
  - **FileBackstageUITests** (24 tests) — added close via Escape key, navigation to Save/Save As/Print/Export PDF/Export DOCX/OneDrive/Options panels with header verification, multiple template validation, New creates blank document, switching between backstage nav items updates header
  - **TabManagementUITests** (11 tests) — added Ctrl+W close shortcut, new tab shows "Untitled" title, new tab has empty editor, rapid tab creation/close stress test, formatting state independence between tabs
  - **ViewMenuUITests** (14 tests) — added Spell Check toggle status messages, Ruler toggle state verification, Focus mode hides status bar and restores it, Page View toggle cycle, Word Wrap toggle preserves content
  - **FormattingFunctionalUITests** (30 tests) — added Ctrl+B/I/U keyboard shortcut tests, Bold+Italic combination, Clear Formatting resets italic/underline/all formats simultaneously, formatting does not change word/char count
  - **ParagraphFormattingUITests** (24 tests) — added all remaining list types (lowercase/uppercase letters, lowercase/uppercase Roman), list type switch preserves word count, line spacing 1.15 and 1.5, Heading 2/3/Subtitle/Quote styles, multiple indent levels preserve word count
  - **StatusBarAndThemeUITests** (16 tests) — added column number update after typing, theme toggle full cycle with distinct theme verification, punctuation word count, newline character count, zoom percent sign validation, partial selection length, empty editor Ln 1/Col 1
  - **MacroFunctionalUITests** (17 tests) — added italic macro record/playback, Stop menu item safety when not recording, multiple commands in single macro, macro does not change editor content
  - **EditorInteractionUITests** (23 tests) — added three Enter keys advance line count, Left arrow decreases column, Home key returns to column 1, End key moves to end of line, typing after undo updates word count, Backspace reduces char count, empty editor Ln 1/Col 1, multiple spaces between words, second line accumulates word count
- **App icon** — `SmrtPad.ico` (16/32/48/256 px, PNG-in-ICO) generated from `SmrtPad.png` and added to `Assets/`; `AppWindow.SetIcon()` called in `MainWindow` constructor so the window, taskbar, and Alt-Tab thumbnail all show the correct icon; all 7 package visual asset slots updated with the new icon image
- **SmrtDoodle install check** — `PaintDrawing_Click` calls `IsSmrtDoodleInstalled()` before launching; checks `%LOCALAPPDATA%\Microsoft\WindowsApps\SmrtDoodle.exe` (Store/MSIX install) and every directory on `PATH`; if not found shows a `ContentDialog` with a **Get from Store** primary button that opens `ms-windows-store://search/?query=SmrtDoodle`; removed the crash-prone built-in fallback drawing dialog (`ShowBuiltInDrawingDialogAsync`)
- `SmrtDoodleGetFromStore` resource string added to all 9 locale files

### Changed
- **UI automation launch robustness** — `AppiumSession` now falls back from MSIX/AUMID activation to direct `SmrtPad.exe` launch when the packaged app fails to start, enabling tests to run in both packaged and unpackaged configurations; diagnostic `Dx` test now skips gracefully (instead of throwing) when the WinAppDriver session cannot start
- **`SharedAppFixture` helpers improved** — `ClearEditor` and `SelectAllInEditor` prefer Edit-menu clicks over raw keyboard shortcuts; `UndoInEditor` uses multi-strategy element lookup with a clear error when the Undo button is not found
- **Additional `AutomationProperties.AutomationId` attributes** — status bar elements (`StatusText`, `WordCountText`, `CharCountText`, `SelectionLengthText`, `LineColText`, `EncodingText`, `ZoomText`), `DocumentTabs`, `ReplaceWithTextBox`, and `FileBackstageView.HeaderText`; `DocumentTabs_Loaded` handler sets `AutomationId` on the dynamically-created Add-tab button via `FindDescendantByName<T>`
- **App startup diagnostics** — `App.xaml.cs` logs key startup events to a temp file during `OnLaunched` to aid debugging of packaged/unpackaged launch issues; `PrimaryLanguageOverride` wrapped in `try/catch(InvalidOperationException)` for unpackaged launches
- **Custom entry point** — `SmrtPad.csproj` now uses `DefineConstants DISABLE_XAML_GENERATED_MAIN` instead of `Compile Remove="Program.cs"` for better compatibility with the .NET 10 SDK
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
