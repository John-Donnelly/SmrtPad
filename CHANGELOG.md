# Changelog

All notable changes to SmrtPad are documented in this file.

## [Unreleased]

### Added
- **`QuickAccessNewButton` automation ID** — quick-access toolbar New button now carries a stable `AutomationProperties.AutomationId="QuickAccessNewButton"` so UI automation tests can target it without relying on localized text
- **`ClearFormattingButton` automation ID** — Clear Formatting ribbon button now carries `AutomationProperties.AutomationId="ClearFormattingButton"` replacing the previous ambiguous `Name="Clear Formatting"` lookup
- **Edit menu stable automation IDs** — all five Edit menu items now carry `AutomationId`: `CutMenuItem`, `CopyMenuItem`, `PasteMenuItem`, `PasteSpecialMenuItem`, `SelectAllMenuItem`; `MenuBarItem` carries `EditMenuBarItem`
- **View menu stable automation IDs** — `ZoomInMenuItem` and `ZoomOutMenuItem` carry stable `AutomationId`; `MenuBarItem` carries `ViewMenuBarItem`
- **`RefreshEditorState()`** — new helper in `MainWindow.xaml.cs` that forces the editor focus, updates selection-length display, refreshes status-bar counts, and re-fires `Editor_SelectionChanged`; called by every clipboard command and formatting command so the ribbon and status bar reflect state immediately after programmatic changes
- **`RefreshEditorViewportLayout()`** — new helper that calls `UpdateLayout()` on the tab strip, scroll viewer, editor container, page-view border, and editor itself; called by `ApplyZoom()` and `ApplyPageViewLayout()` so zoom and page-view transitions settle synchronously rather than deferring to the next async layout pass
- **`SharedAppFixture.FindElementByIdOrName()`** — new helper that prefers a stable `AutomationId` lookup and falls back to a `Name`-based lookup, reducing test fragility under localization
- **`SharedAppFixture.GetMenuAutomationId()` / `GetMenuItemAutomationId()`** — static maps from friendly menu/item names to their stable automation IDs; `ClickMenuItem()` now routes through these helpers

### Changed
- **`ApplyZoom()`** — now calls `RefreshEditorViewportLayout()` after applying the scale transform and ruler redraw to ensure bounds settle before the next UIA query
- **`ApplyPageViewLayout()`** — now calls `RefreshEditorViewportLayout()` after adjusting the editor and page-view border
- **`Bold_Click`, `Italic_Click`, `Underline_Click`, `Strikethrough_Click`, `Subscript_Click`, `Superscript_Click`** — each now calls `RefreshFormattingState()` after applying the character format so toggle states and status-bar counts are deterministic
- **`ClearFormatting_Click`** — now calls `RefreshFormattingState()` before `UpdateStatus` so cleared-format toggle states are reflected immediately
- **`Cut_Click`, `Copy_Click`, `Paste_Click`, `PasteSplitButton_Click`, `PasteAsPlainTextAsync`, `PasteSpecial_Click`, `SelectAll_Click`** — each now calls `RefreshEditorState()` after its operation
- **`TabManagementUITests.CloseActiveTab()`** — switched from a generic `Close` element name search to keyboard `Ctrl+W` against the active editor, removing dependency on a non-unique element name
- **`TabManagementUITests.FindQuickAccessNewButton()`** — new helper replaces inline `FindElement(MobileBy.Name("New"))` calls with `AccessibilityId("QuickAccessNewButton")`
- **`TabManagementUITests.FindUntitledTabs()`** — new helper scopes `Name("Untitled")` searches inside the `DocumentTabs` container, preventing false matches outside the tab strip
- **`TabManagementUITests.NewButton_CreatesNewTab_PreviousTabStillExists`** — replaced `Assert.True(tabsAfter.Count > countBefore)` with a deterministic count assertion
- **`FormattingFunctionalUITests` Clear Formatting tests** — three tests switched from `MobileBy.Name("Clear Formatting")` to `MobileBy.AccessibilityId("ClearFormattingButton")`
- **`SharedAppFixture.ClickMenuItem()`** — now calls `FindElementByIdOrName()` for both the menu bar item and the flyout item, falling back gracefully to name-based search when the ID is not mapped

### Added
- **Keyboard shortcuts** — `Ctrl+F` programmatically opens the Find flyout (`OpenFind_Invoked`); `Ctrl+H` opens the Replace flyout (`OpenReplace_Invoked`); `F3` triggers Find Next; `Ctrl+D` duplicates the current line or selection (`DuplicateLineOrSelection`) with status feedback
- **Zoom slider** — `Slider` control (Minimum=10, Maximum=500, StepFrequency=10) added to the status bar, two-way bound to `ViewModel.ZoomLevel`; `ZoomSlider_ValueChanged` snaps to nearest 10% step and calls `ApplyZoom()`
- **Zoom text-entry box** — `ZoomPercentBox` `TextBox` in status bar accepts a typed percentage; validated on `Enter` (KeyDown) and LostFocus via `ApplyZoomFromPercentBox`; clamped to 10–500%
- **Format → Paragraph dialog** — consolidated `ContentDialog` accessible from Format > Paragraph…; reads `ITextParagraphFormat` on open and writes back on OK: alignment (ComboBox), left/right/first-line indents (NumberBox, inches), line spacing (NumberBox), space before/after (NumberBox, pt); `AutomationId="FormatParagraphDialog"`
- **Status bar independent show/hide** — `StatusBarToggle` `ToggleMenuFlyoutItem` in View menu independently shows/hides the status bar without affecting the ribbon; state persisted via `SettingsService.ShowStatusBar`
- **Paste Special format selector dialog** — `PasteSpecial_Click` now opens a `ContentDialog` (`AutomationId="PasteSpecialDialog"`) with three `RadioButton`s — Rich Text (RTF), Unformatted Text, HTML Format — enabled/disabled based on actual clipboard availability; RTF pasted via `SetText(TextSetOptions.FormatRtf, ...)`
- **Paste SplitButton** — ribbon Paste button upgraded from `Button` to `SplitButton` (`AutomationId="PasteSplitButton"`); primary click = rich paste `Selection.Paste(0)`; secondary dropdown offers "Paste Plain" (plain-text insert via `PasteAsPlainTextAsync`) and "Paste Special…"
- **Points & Picas measurement units** — `RulerHelper.GetPixelsPerUnit` extended with `"pt"` (96/72 px/unit) and `"pc"` (16 px/unit) cases; Options dialog Measurement Units dropdown adds Points and Picas entries alongside Inches and Centimeters
- **Word wrap "Wrap to Ruler" mode** — third wrap mode added: editor `TextWrapping` = Enabled but column width clamped to a 6.5-inch ruler column (`ApplyWordWrapMode("WrapToRuler")`); View > Word Wrap is now a three-item submenu (No Wrap / Wrap / Wrap to Ruler) with per-item `AutomationId`; mode persisted as `SettingsService.WordWrapMode`
- **Send by email** — backstage "Send by Email…" `NavigationViewItem` (`NavSendEmail`, `AutomationId="NavSendEmail"`) fires `SendEmailRequested` → `SendEmail_Click` → `Launcher.LaunchUriAsync(new Uri("mailto:?subject=…"))` using the default mail client
- **Accessibility improvements** — `AutomationProperties.LiveSetting="Polite"` on `StatusText` and `WordCountText` so Narrator announces updates; `FontColorIndicator` and `HighlightColorIndicator` receive a static `AutomationProperties.Name` plus dynamic `AutomationPeer.SetName(…)` on every color change; `AutomationId` added to `PasteSplitButton`, `FindButton`, `ReplaceButton`, `ZoomSlider`, `ZoomPercentBox`, all three word-wrap sub-items, `StatusBarToggle`, and all Paste Special dialog controls
- **`ISettingsService` / `SettingsService`** — added `ShowStatusBar` (default `true`) and `WordWrapMode` (default `"Wrap"`) properties; both persisted to `settings.json` and round-trip correctly
- **37 new resource strings** added to all 9 locale files: `StatusDuplicatedLine`, `StatusDuplicatedSelection`, `ZoomSliderAccessibleName`, `ZoomPercentBoxAccessibleName`, 9 `ParagraphDialog*` keys, `StatusBarToggle.Text`, `StatusStatusBarShown`, `StatusStatusBarHidden`, 5 `PasteSpecial*` keys, `PastePlainRibbonLabel.Text`, `PasteSpecialRibbonItem.Text`, `OptionsRulerPoints`, `OptionsRulerPicas`, `WordWrapOffItem.Text`, `WordWrapWrapItem.Text`, `WordWrapToRulerItem.Text`, `SendEmailMenuItem.Text`, `SendEmailSubject`, `StatusEmailSent`, `BackstageSendEmailDesc`, `FontColorIndicatorName`, `HighlightColorIndicatorName`
- **26 unit tests** (`NewFeatureTests.cs`) covering: `ZoomLevel`/`ZoomDisplay` defaults, `ZoomIn`/`ZoomOut` increments and boundary clamping, `ShowStatusBar` persistence round-trip, `RulerHelper` Points/Picas at 100% and scaled zoom, `WordWrapMode` default and all three-mode persistence
- **13 Appium UI tests** (`NewFeatureUITests.cs`) covering: `Ctrl+F` opens Find flyout, `Ctrl+H` opens Replace flyout, `Ctrl+D` duplicates selection, `ZoomSlider`/`ZoomPercentBox` presence in status bar, Format→Paragraph dialog opens, StatusBar toggle hides/shows, PasteSpecial dialog opens, PasteSplitButton presence, Send by Email backstage item, `FontColorIndicator`/`HighlightColorIndicator` `AutomationId`, `ZoomSlider` accessible name
- **Format → Font dialog** — new consolidated dialog (Format > Font...) that sets font family, size, style (bold/italic), effects (underline/strikethrough/subscript/superscript with mutual exclusion), and character color in one place — matching WordPad's Format > Font; reads current selection state on open, writes back on OK
- **Format menu** — new "Format" menu bar item between View and Macro containing the Font... command
- **"No Highlight" button** — added a "No Highlight" / Remove Highlight entry to the text highlight color flyout, allowing users to remove background highlighting from selected text
- **30 unit tests** (`FontFormattingUpgradeTests.cs`) covering: ColorHelper hex parsing for all swatch colors (12 tests), remove highlight transparency verification (3 tests), Format > Font dialog ViewModel state management (15 tests)
- **22 Appium UI tests** (`FontFormattingUpgradeUITests.cs`) covering: font-color indicator from color picker (3 tests), No Highlight button presence and functionality (4 tests), Format > Font dialog controls, state reading, formatting application, and cancel behavior (15 tests)
- **10 new resource strings** added to all 9 locale files: `NoHighlightButton`, `FormatMenu`, `FormatFontMenuItem`, `FontDialogTitle`, `FontDialogFamily`, `FontDialogSize`, `FontDialogStyleHeader`, `FontDialogEffectsHeader`, `FontDialogColorHeader`, `StatusFontApplied`

### Fixed
- **Font-color indicator not updating from color picker** — moved `FontColorIndicator.Fill` update into `ApplyTextColor` so both swatch clicks and the `ColorPicker` control update the color-indicator rectangle; previously only swatches updated it, leaving the indicator stale when the picker was used
- **Highlight-color indicator not updating from color picker** — applied the same fix to `ApplyHighlightColor` and `HighlightColorIndicator` for consistency

### Added
- **28 file-management tests** (`FileManagementUpgradeTests.cs`) covering:
  - `HtmlConverterHelperTests` — 12 tests: null/empty input, BR tag conversion, list item bullet conversion, HTML entity decoding, blank-line collapse, empty/null HTML output, special character encoding, single-line-break-to-BR, round-trip fidelity
  - `OdtImportExportTests` — 13 tests: ODT entry creation, mimetype validation, null/read-only stream guards, empty text export, content paragraph verification, DOCX/ODT text extraction, ODT-to-RTF null guard and fallback, font/color table presence
  - `SettingsServiceRecentFilePruningTests` — 8 tests: missing-file pruning, duplicate removal, empty-path guard, MRU reorder-to-top, clear all, page setup defaults, page setup persistence round-trip, max-10 limit enforcement

### Changed
- **Word wrap menu upgraded to three-mode submenu** — View > Word Wrap changed from a binary `ToggleMenuFlyoutItem` to a `MenuFlyoutSubItem` with three items: No Wrap / Wrap / Wrap to Ruler; mode stored as `SettingsService.WordWrapMode` (`"Off"` / `"Wrap"` / `"WrapToRuler"`)
- **Paste button upgraded to SplitButton** — primary action unchanged (rich paste via `Selection.Paste(0)`); dropdown adds "Paste Plain" (plain-text insert) and "Paste Special…" options; `PastePlain_Click` now delegates to `PasteAsPlainTextAsync`
- **FileBackstageView event replaced** — `PageSetupRequested` event removed; `SendEmailRequested` event added (13 events total); `NavPageSetup` backstage item retained for display only; `NavSendEmail` item added

### Fixed
- **Blank tabs no longer show save dialog on close** — Added `_suppressTabModified` flag
- **Last tab close now closes the application** — `DocumentTabs_TabCloseRequested` now calls `Close()` instead of creating a new blank tab when the last tab is closed; the `AppWindow.Closing` handler is unhooked to prevent re-entrance
- **File backstage hover shows pane** — `FileBackstageView` now separates pane display from action execution; `PointerEntered` handlers on each `NavigationViewItem` show the relevant content pane (description, template picker, recent files) on hover, while click still executes the action (New, Open, Save, etc.)

### Changed
- **Tab bar: New always opens a new tab** — `New_Click` now always creates a new tab instead of prompting to save changes on the current tab; save prompts are reserved for tab close and app close only
- **Tab bar: Open opens file in a new tab** — `Open_Click` and `OpenFileByPathAsync` now open files in a new tab (or reuse the current blank unmodified tab), rather than replacing the current document and prompting to save
- **Tab header set for all file types** — `OpenStorageFileAsync` now consistently sets `ActiveTab.TabViewItem.Header`, `ActiveTab.IsModified`, and `ActiveTab.Encoding` for all file types (DOCX, ODT, HTML, RTF, TXT); previously only RTF/TXT branches updated the tab header
- **Save dialog on close iterates all tabs** — `AppWindow_Closing` now checks all tabs for unsaved changes (not just the active tab); when multiple tabs have modifications, each is shown sequentially with its own save prompt, and the user can cancel at any point to abort closing
- **Backstage Exit uses multi-tab save** — the backstage Exit handler and `Exit_Click` now use `PromptSaveAllTabsAsync` to iterate all modified tabs before closing

### Added
- **5 new UI tests** for tab and backstage behavior:
  - New tab via `+` button closes without save dialog
  - New tab via `New` button closes without save dialog
  - Last-tab-close with extra tab does not close app
  - Backstage hover shows correct pane headers
  - Backstage Exit shows description pane
- **`BackstageExitDesc` resource string** — added to all 9 locale files
- **`PromptSaveAllTabsAsync` helper** — new method that iterates all tabs with unsaved changes, switches to each, and prompts save individually; used by `AppWindow_Closing`, `Exit_Click`, and backstage Exit

### Added
- **41 new production-fix tests** (`ProductionFixTests.cs`) covering:
  - `Bullets_Click` ViewModel sync and macro recording (`BulletsClickContractTests` — 7 tests)
  - Macro `SetAlignment` playback RTF application and round-trip (`MacroSetAlignmentPlaybackTests` — 7 tests)
  - Debug logging removal verification (`AppDebugLoggingRemovedTests` — 3 tests)
  - Full macro command coverage across all 15 command types (`MacroHelperFullCommandCoverageTests` — 20 tests)
  - Resource management contracts (`MainWindowResourceManagementTests` — 4 tests)

### Fixed
- **Dark Mode DOCX text visibility** — Fixed an issue where `.docx` files loaded in Dark Mode appeared with invisible black text; `NormalizeDocumentColorsForTheme` now iterates through the document's character formatting runs to reset any text that explicitly uses the unreadable wrong-default colour (e.g. black in dark mode) while safely preserving intentional custom text colours throughout the rest of the document.
- **`Bullets_Click`** — now calls `ViewModel.SetListType()`
- **Macro `SetAlignment` playback** — `ExecuteMacroCommand` now applies the alignment directly to the RTF document's paragraph format in addition to updating the ViewModel; previously only `ViewModel.SetAlignment()` was called, so replaying a recorded alignment macro had no visible effect
- **`App.xaml.cs` debug logging** — removed leftover `System.IO.File.WriteAllText/AppendAllText` calls that wrote `SmrtPad_App_Startup.log` to `%TEMP%` on every application launch; startup diagnostics are not appropriate for production builds
- **Auto-save timer not stopped on close** — `MainWindow.Closed` event now stops `_autoSaveTimer` to prevent the timer from firing after the window is closed
- **`ViewModel.PropertyChanged` handler leak** — handler is now stored in `_docTitleHandler` field and unsubscribed in `Closed` event; prevents secondary windows from keeping a live reference on the shared ViewModel singleton after close
- **Silent auto-save error** — replaced empty `catch {}` in the auto-save timer tick with `catch (Exception ex) { Debug.WriteLine(...) }` so failures are visible in diagnostics without interrupting the user
- **`AppWindow.SetIcon` crash guard** — icon path is now checked with `File.Exists` before calling `SetIcon`, preventing an unhandled exception on launch when the ico asset is absent

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
