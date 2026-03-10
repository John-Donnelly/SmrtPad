# SmrtPad

A modern WordPad-inspired rich text editor built with WinUI 3 and .NET 10, featuring a Microsoft WordPad-style ribbon interface, tabbed documents, macro recording, and a full suite of export options.

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
- **Save/Save As** supports RTF, TXT, DOCX, ODT, and HTML formats
- **Export to PDF** — multi-page PDF 1.4 (Helvetica, A4, 72 pt margins)
- **Export to DOCX** — lossless RTF-to-DOCX via OpenXml AltChunk; import preserves bold, italic, underline, strikethrough, fonts, colors, alignment, page breaks, and embedded images
- **Save to OneDrive** — saves via standard file picker to the user's OneDrive folder; guarded with availability check
- Auto-save to recovery folder for unnamed documents; saves in-place for named documents
- Recent files list (MRU, max 10, auto-pruned on load) in the backstage

### File Backstage
- WordPad-style backstage for New, Templates, Open, Save, Save As, Print, Export PDF, Export DOCX, OneDrive, Page Setup, Options, and Exit
- **Page Setup** — paper size (Letter, A4, Legal), orientation (Portrait, Landscape), and custom margins; persisted in settings
- **Document Templates** — 5 built-in templates (Blank, Letter, Meeting Notes, To-Do List, Report)
- Fully opaque overlay that covers the tab strip and editor when open

### SmrtDoodle Integration
- **SmrtDoodle** ribbon button launches the SmrtDoodle companion drawing app, awaits exit, and inserts the resulting image into the document
- Pre-launch installation check; if not installed a dialog offers a **Get from Store** button that opens the Microsoft Store search for SmrtDoodle

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
- .NET 10 SDK
- Windows App SDK 1.8+
- (Optional) [SmrtDoodle](https://www.microsoft.com/store/apps) for in-document drawing

## Building

1. Clone the repository:
   ```
   git clone https://github.com/John-Donnelly/SmrtPad.git
   ```
2. Open `SmrtPad.slnx` in Visual Studio 2022 or later (Visual Studio 2026 also supported).
3. Set the platform to **x64** (or ARM64).
4. Build and run the **SmrtPad (Package)** project for a fully packaged experience, or the **SmrtPad** project for unpackaged debug.

## Running Tests

```
dotnet test SmrtPad.Tests\SmrtPad.Tests.csproj -c Debug -p:Platform=x64
```

The test suite has **2,600+ tests** (2,355+ unit/integration + 241 UI automation across 14 classes) covering:
- ViewModel default values and all property-change notifications
- All formatting toggle commands (Bold, Italic, Underline, Strikethrough, Subscript, Superscript)
- Alignment, list type, and line spacing for all defined values
- Zoom in/out with min/max clamping; word wrap; spell check settings
- `NewDocument` full state reset
- `ColorHelper` hex parsing (6-digit, 8-digit, error cases)
- PDF generation (page count, header content, byte-array structure)
- DOCX generation (ZIP structure, `word/document.xml` content, paragraph mapping, rich formatting via RTF parser)
- OneDrive availability detection
- Document import (DOCX, ODT text extraction; DOCX-to-RTF with formatting and images)
- HTML import/export (tag stripping, entity decoding, paragraph preservation, round-trip)
- ODT export (valid ODF packages with mimetype, content.xml, manifest)
- Macro recording and playback
- Settings persistence, concurrency, recent-files MRU, and page setup round-trip
- Localization — all 9 locales, all 255 resource keys present and non-empty
- UI automation (WinAppDriver/Appium 2.x) — editor interaction, formatting, tabs, find/replace, file backstage, macros, view menu, paragraph formatting, status bar, zoom behaviour
- Stable automation IDs on all ribbon toggles, menu items, and quick-access buttons for deterministic UI test addressing

## Performance Notes

Cold-start profiling for `Task 8.2` was captured with Visual Studio 2026 CPU Usage on release-style startup traces.

- **Baseline trace:** startup was dominated by WinUI app initialization (`Microsoft.UI.Xaml.Application.Start` at 52.74% total CPU / 42.38% self CPU), while the app cold path still performed settings construction, license initialization, window setup, and session-restore checks before or during first use.
- **Current trace after deferrals:** WinUI/XAML startup remains the dominant cost (`Microsoft.UI.Xaml.Application.Start` at 54.67% total CPU / 34.06% self CPU; `Application.LoadComponent` at 7.12%), with app-visible costs reduced to smaller items such as `SettingsService.Load()` (~1.04% total CPU) and title-bar theming (`MainWindow.UpdateTitleBarTheme()` ~1.67% total CPU).
- **Implemented changes:** `MainWindow` now activates before post-launch license/session/file-open work completes, recent-file validation in `SettingsService` is deferred until the MRU list is needed, and non-critical `MainWindow` setup such as font enumeration and print registration is queued after initial startup.
- **Status:** the CPU traces confirm the launch-blocking app work was reduced, but an exact `<= 800 ms` first-interactive-frame confirmation still requires a dedicated Timeline pass on the target reference hardware.

## Project Structure

```
SmrtPad/
├── SmrtPad/
│   ├── Assets/              # App icon (SmrtPad.ico/.png), SmrtDoodle icons
│   ├── Helpers/             # ColorHelper, DocxAltChunkExporter, DocxImportHelper,
│   │                        # DocumentImportHelper, DocumentTemplates, HtmlConverterHelper,
│   │                        # MacroHelper, OdtExportHelper, OneDriveHelper,
│   │                        # ParagraphStyleHelper, PdfHelper, ResourceHelper,
│   │                        # RtfHelper, RulerHelper
│   ├── Models/              # DocumentTemplate
│   ├── Services/            # DialogService, FileService, SettingsService
│   │                        # (+ IDialogService, IFileService, ISettingsService)
│   ├── Strings/             # 9 locale .resw files (en-US, de-DE, es-ES, fr-FR,
│   │                        # ja-JP, zh-Hans, ar-SA, ru-RU, ur-PK)
│   ├── ViewModels/          # EditorViewModel
│   ├── Views/               # FileBackstageView
│   ├── MainWindow.xaml      # Main window with ribbon UI
│   ├── MainWindow.xaml.cs   # Code-behind — editor logic, ribbon handlers
│   ├── App.xaml             # Application resources and ThemeDictionaries
│   └── App.xaml.cs          # Entry point, DI container, multi-window factory
├── SmrtPad (Package)/       # MSIX packaging project
├── SmrtPad.Tests/
│   ├── EditorTests.cs               # ViewModel unit tests
│   ├── IntegrationTests.cs          # Helper + service integration tests
│   ├── LocalizationTests.cs         # Locale completeness tests
│   ├── CoverageCompletionTests.cs
│   ├── MaxCoverageTests.cs
│   ├── MaxCoverageTests2.cs
│   ├── MaxCoverageTests3.cs
│   ├── MaxCoverageTests4.cs
│   ├── NewFeatureTests.cs
│   ├── FontFormattingUpgradeTests.cs
│   ├── FileManagementUpgradeTests.cs
│   ├── ProductionFixTests.cs
│   └── ReleaseReadinessBehaviorTests.cs
├── SmrtPad.UITests/
│   ├── Infrastructure/          # AppiumSession, SharedAppFixture
│   └── Tests/                   # 14 WinAppDriver/Appium 2.x test classes (241 tests)
├── README.md
└── CHANGELOG.md
```

## Acknowledgments

Ribbon design inspired by [UltraPad](https://github.com/lixkote/ultrapad), a modernized WordPad replacement for Windows 11.

## License

See repository for license details.

