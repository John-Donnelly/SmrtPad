# Changelog

All notable changes to SmrtPad are documented in this file.

## [Unreleased]

### Fixed
- Replaced `RadioButton` with `ToggleButton` for paragraph alignment buttons to fix WinUI 3 runtime crash (`E_INVALIDARG` / `E_UNEXPECTED`) caused by applying `DefaultToggleButtonStyle` to `RadioButton`
- Alignment buttons now enforce mutual exclusivity via code-behind helper
- Paragraph alignment button icons are now horizontally and vertically centered
- Font size dropdown width doubled (56 → 112px) so selected values are fully visible
- Color swatch buttons now render as filled circles instead of collapsed dots — replaced `Border`+`Rectangle` (no explicit size) with `Ellipse` (20×20) for both font color and highlight color grids
- Line spacing no longer produces massive gaps — removed erroneous `× 12` multiplier; standard values (1.0, 1.5, 2.0) now use dedicated `LineSpacingRule.Single`/`OneAndHalf`/`Double` rules
- Exit no longer throws `winrt::hresult_error` — removed duplicate `Application.Current.Exit()` from backstage, replaced with `Window.Close()` for proper WinUI 3 shutdown

### Added
- **Paint Drawing** button launches SmrtDoodle (external companion app) with a temp file path, awaits exit, and inserts the resulting image; shows a dialog if SmrtDoodle is not installed
- **Insert Object** button opens a file picker for image formats (PNG, JPG, BMP, GIF, TIFF, ICO, SVG) and inserts them into the document
- README.md with project overview, build instructions, and test documentation
- CHANGELOG.md

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
