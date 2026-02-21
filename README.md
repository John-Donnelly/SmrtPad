# SmrtPad

A modern WordPad-inspired rich text editor built with WinUI 3 and .NET 10, featuring a Microsoft WordPad-style ribbon interface.

## Features

### Ribbon Toolbar (WordPad-style)
- **Clipboard** — Large Paste button with stacked Cut/Copy, matching the classic WordPad layout
- **Font** — Font family picker, font size selector, grow/shrink buttons, toggle buttons for Bold, Italic, Underline, Strikethrough, Subscript, and Superscript, plus color swatch grids for font color and text highlight
- **Paragraph** — Indent increase/decrease, list type dropdown (None, Bullet, Numbers, Letters, Roman numerals), line spacing selector (1.0, 1.15, 1.5, 2.0), and toggle-style alignment buttons (Left, Center, Right, Justify)
- **Insert** — Picture, Paint Drawing, Insert Object, and Date/Time with large icon+label buttons
- **Editing** — Find and Replace with flyout dialogs, and Select All

### Editor
- Rich text editing via WinUI `RichEditBox`
- RTF and TXT file format support (open and save)
- Undo/Redo support
- Word wrap toggle
- Zoom in/out with level display in the status bar

### File Backstage
- WordPad-style backstage view for New, Open, Save, Save As, Print, Options, and Exit

### UI
- Mica backdrop for a modern Windows 11 appearance
- Segoe Fluent Icons throughout the ribbon
- Status bar with document status and zoom level indicator

## Requirements

- Windows 10 version 1809 (build 17763) or later
- .NET 10 SDK
- Windows App SDK 1.8+

## Building

1. Clone the repository:
   ```
   git clone https://github.com/John-Donnelly/SmrtPad.git
   ```
2. Open `SmrtPad.sln` in Visual Studio 2022 or later.
3. Set the platform to **x64** (or ARM64).
4. Build and run the `SmrtPad` project.

## Running Tests

```
dotnet test SmrtPad.Tests\SmrtPad.Tests.csproj -c Debug -p:Platform=x64
```

The test suite includes 59 tests covering:
- ViewModel default values and property change notifications
- All formatting toggle commands (Bold, Italic, Underline, Strikethrough, Subscript, Superscript)
- Alignment setting for all four modes
- List type selection for all seven marker types
- Line spacing for all standard values
- Zoom in/out with min/max clamping
- Word wrap toggle
- NewDocument full state reset
- Hex color parsing for 6-digit and 8-digit formats
- Negative/error-case tests for ColorHelper input validation

## Project Structure

```
SmrtPad/
├── SmrtPad/
│   ├── Helpers/             # Utility classes (ColorHelper)
│   ├── ViewModels/          # MVVM view models (EditorViewModel)
│   ├── Views/               # User controls (FileBackstageView)
│   ├── MainWindow.xaml      # Main window with ribbon UI
│   ├── MainWindow.xaml.cs   # Code-behind with editor logic
│   └── App.xaml             # Application entry point
├── SmrtPad.Tests/
│   └── EditorTests.cs       # Unit tests
├── README.md
└── CHANGELOG.md
```

## Acknowledgments

Ribbon design inspired by [UltraPad](https://github.com/lixkote/ultrapad), a modernized WordPad replacement for Windows 11.

## License

See repository for license details.
