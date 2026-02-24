using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Printing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Graphics.Printing;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage.Streams;
using Microsoft.UI.Text;
using Windows.UI;
using WinRT.Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using SmrtPad.Helpers;
using SmrtPad.Models;
using SmrtPad.ViewModels;
using SmrtPad.Views;
using SmrtPad.Services;
using Res = SmrtPad.Helpers.ResourceHelper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SmrtPad
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private readonly ISettingsService _settings;
        private readonly IDialogService _dialogService;
        private readonly IFileService _fileService;
        private DispatcherTimer? _autoSaveTimer;
        private PrintDocument? _printDocument;
        private IPrintDocumentSource? _printDocumentSource;
        private readonly List<UIElement> _printPreviewPages = [];
        private bool _rulersVisible;
        private bool _pageViewActive;
        private Color _lastFontColor = Color.FromArgb(255, 0xE8, 0x11, 0x23);
        private bool _fontDropdownStyled;

        // ?? Tab management ??????????????????????????????????????????????????????
        private readonly List<DocumentTab> _tabs = [];
        private int _activeTabIndex = -1;
        private readonly MacroHelper _macro = new();

        private static readonly char[] s_wordSeparators = [' ', '\r', '\n', '\t'];
        private bool _suppressFontComboChange;
        private DocumentTab ActiveTab => _tabs[_activeTabIndex];
        private RichEditBox Editor => ActiveTab.Editor;
        private ScrollViewer EditorScrollViewer => ActiveTab.ScrollViewer;
        private Grid EditorContainer => ActiveTab.EditorContainer;
        private Border PageViewBorder => ActiveTab.PageViewBorder;
        private ScaleTransform ActiveScaleTransform => ActiveTab.ScaleTransform;
        public EditorViewModel ViewModel { get; }

        public MainWindow()
        {
            _settings = App.Current.Services.GetRequiredService<ISettingsService>();
            _dialogService = App.Current.Services.GetRequiredService<IDialogService>();
            _fileService = App.Current.Services.GetRequiredService<IFileService>();
            ViewModel = App.Current.Services.GetRequiredService<EditorViewModel>();
            InitializeComponent();
            Title = Res.GetFormatted("AppTitle", ViewModel.DocumentTitle);
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.DocumentTitle))
                {
                    Title = Res.GetFormatted("AppTitle", ViewModel.DocumentTitle);
                }
            };

            // Create the first document tab before ApplySettings() so Editor is valid
            CreateTab(Res.GetString("DocumentUntitled"));

            InitializeFonts();
            ApplySettings();
            SetupAutoSave();

            FileBackstage.NewRequested += (s, e) => { HideBackstage(); New_Click(this, new RoutedEventArgs()); };
            FileBackstage.OpenRequested += (s, e) => { HideBackstage(); Open_Click(this, new RoutedEventArgs()); };
            FileBackstage.SaveRequested += (s, e) => { HideBackstage(); Save_Click(this, new RoutedEventArgs()); };
            FileBackstage.SaveAsRequested += (s, e) => { HideBackstage(); SaveAs_Click(this, new RoutedEventArgs()); };
            FileBackstage.PrintRequested += (s, e) => { HideBackstage(); Print_Click(this, new RoutedEventArgs()); };
            FileBackstage.ExportPdfRequested += (s, e) => { HideBackstage(); ExportPdf_Click(this, new RoutedEventArgs()); };
            FileBackstage.ExportDocxRequested += (s, e) => { HideBackstage(); ExportDocx_Click(this, new RoutedEventArgs()); };
            FileBackstage.OneDriveRequested  += (s, e)    => { HideBackstage(); SaveToOneDrive_Click(this, new RoutedEventArgs()); };
            FileBackstage.OptionsRequested   += (s, e)    => { HideBackstage(); Options_Click(this, new RoutedEventArgs()); };
            FileBackstage.ExitRequested      += async (s, e) => { if (await PromptSaveChangesAsync()) Close(); };
            FileBackstage.RecentFileRequested += async (s, path) => { HideBackstage(); await OpenFileByPathAsync(path); };
            FileBackstage.TemplateRequested  += (s, template) => { HideBackstage(); ApplyTemplate(template); };

            RegisterForPrinting();

            // Intercept the window close button (X) to prompt for unsaved changes
            AppWindow.Closing += AppWindow_Closing;
            AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "SmrtPad.ico"));
        }

        private async void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            if (ViewModel.IsModified)
            {
                // Cancel close so we can show the async dialog
                args.Cancel = true;

                if (await PromptSaveChangesAsync())
                {
                    // User chose Save or Don't Save — close for real.
                    // Unhook to prevent re-entrance, then close.
                    AppWindow.Closing -= AppWindow_Closing;
                    Close();
                }
                // else: user cancelled — window stays open
            }
        }

        // ?? Tab management ???????????????????????????????????????????????????????

        private DocumentTab CreateTab(string title)
        {
            var tab = new DocumentTab(title, _settings);

            tab.Editor.TextChanged += (s, e) =>
            {
                if (_activeTabIndex >= 0 && tab == ActiveTab)
                {
                    ViewModel.IsModified = true;
                    tab.IsModified = true;
                    UpdateStatusBarCounts();
                }
            };
            tab.Editor.SelectionChanged += (s, e) =>
            {
                if (_activeTabIndex >= 0 && tab == ActiveTab)
                    Editor_SelectionChanged(s, e);
            };
            tab.Editor.DragOver += Editor_DragOver;
            tab.Editor.Drop += Editor_Drop;
            tab.ScrollViewer.PointerWheelChanged += EditorScrollViewer_PointerWheelChanged;
            tab.ScrollViewer.SizeChanged += (s, e) =>
            {
                if (_activeTabIndex >= 0 && tab == ActiveTab)
                    ApplyZoom();
            };

            DocumentTabs.TabItems.Add(tab.TabViewItem);
            _tabs.Add(tab);
            DocumentTabs.SelectedIndex = _tabs.Count - 1;
            _activeTabIndex = _tabs.Count - 1;
            return tab;
        }

        private void DocumentTabs_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TabView tabView)
            {
                return;
            }

            var addButton = FindDescendantByName<Button>(tabView, "AddButton");
            if (addButton != null)
            {
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(addButton, "AddButton");
            }
        }

        private void DocumentTabs_AddTabButtonClick(TabView sender, object args)
        {
            CreateTab(Res.GetString("DocumentUntitled"));
            ViewModel.NewDocument();
            UpdateEncoding("UTF-8");
            ViewModel.UpdateStatus(Res.GetString("StatusNewTab"));
        }

        private static T? FindDescendantByName<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && typedChild.Name == name)
                {
                    return typedChild;
                }

                var result = FindDescendantByName<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        private void NewTab_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            DocumentTabs_AddTabButtonClick(DocumentTabs, null!);
            args.Handled = true;
        }

        private void ApplyTemplate(DocumentTemplate template)
        {
            string title = template.Key == "blank"
                ? Res.GetString("DocumentUntitled")
                : template.DisplayName;

            CreateTab(title);
            ViewModel.NewDocument();
            UpdateEncoding("UTF-8");

            if (!string.IsNullOrEmpty(template.PlainContent))
            {
                Editor.Document.SetText(TextSetOptions.None, template.PlainContent);
                ViewModel.IsModified = true;
            }

            ViewModel.UpdateStatus(Res.GetFormatted("StatusTemplateApplied", template.DisplayName));
        }

        private async void DocumentTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            int idx = _tabs.FindIndex(t => t.TabViewItem == args.Tab);
            if (idx < 0) return;

            // If the closing tab has unsaved changes, prompt
            if (_tabs[idx].IsModified)
            {
                _activeTabIndex = idx;
                if (!await PromptSaveChangesAsync()) return;
            }

            DocumentTabs.TabItems.Remove(args.Tab);
            _tabs.RemoveAt(idx);

            if (_tabs.Count == 0)
            {
                // Reopen a blank tab so there is always at least one
                CreateTab(Res.GetString("DocumentUntitled"));
                ViewModel.NewDocument();
                UpdateEncoding("UTF-8");
            }
            else
            {
                _activeTabIndex = Math.Min(idx, _tabs.Count - 1);
                DocumentTabs.SelectedIndex = _activeTabIndex;
                SyncViewModelFromActiveTab();
            }
            ViewModel.UpdateStatus(Res.GetString("StatusTabClosed"));
        }

        private void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int newIdx = DocumentTabs.SelectedIndex;
            if (newIdx < 0 || newIdx >= _tabs.Count) return;
            _activeTabIndex = newIdx;
            SyncViewModelFromActiveTab();
        }

        private void SyncViewModelFromActiveTab()
        {
            if (_activeTabIndex < 0 || _activeTabIndex >= _tabs.Count) return;
            var tab = _tabs[_activeTabIndex];
            ViewModel.DocumentTitle = tab.CurrentFile?.Name ?? Res.GetString("DocumentUntitled");
            ViewModel.IsModified = tab.IsModified;
            ViewModel.ZoomLevel = tab.ZoomLevel;
            UpdateEncoding(tab.Encoding);
            UpdateStatusBarCounts();
        }

        public async Task OpenFileByPathAsync(string filePath)
        {
            try
            {
                if (!await PromptSaveChangesAsync()) return;
                var file = await StorageFile.GetFileFromPathAsync(filePath);
                await OpenStorageFileAsync(file);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorOpeningFile"), ex.Message);
            }
        }

        private async Task OpenStorageFileAsync(StorageFile file)
        {
            string ext = file.FileType.ToLowerInvariant();
            if (ext is ".docx" or ".odt")
            {
                string text = await ExtractTextFromArchiveAsync(file, ext);
                Editor.Document.SetText(TextSetOptions.None, text);
                ActiveTab.CurrentFile = null;
                ViewModel.DocumentTitle = file.Name;
                ViewModel.IsModified = false;
                ViewModel.UpdateStatus(Res.GetFormatted("StatusOpened", file.Name));
                _settings.AddRecentFile(file.Path);
                UpdateStatusBarCounts();
                UpdateEncoding("UTF-8");
            }
            else if (ext is ".htm" or ".html")
            {
                string html = await FileIO.ReadTextAsync(file);
                Editor.Document.SetText(TextSetOptions.None, html);
                ActiveTab.CurrentFile = null;
                ViewModel.DocumentTitle = file.Name;
                ViewModel.IsModified = false;
                ViewModel.UpdateStatus(Res.GetFormatted("StatusOpened", file.Name));
                _settings.AddRecentFile(file.Path);
                UpdateStatusBarCounts();
                UpdateEncoding("UTF-8");
            }
            else
            {
                bool isTxt = ext == ".txt";
                using (var randAccStream = await file.OpenAsync(FileAccessMode.Read))
                {
                    var options = isTxt ? TextSetOptions.None : TextSetOptions.FormatRtf;
                    Editor.Document.LoadFromStream(options, randAccStream);
                }
                ActiveTab.CurrentFile = file;
                ViewModel.DocumentTitle = file.Name;
                ViewModel.IsModified = false;
                ActiveTab.IsModified = false;
                ActiveTab.TabViewItem.Header = file.Name;
                ViewModel.UpdateStatus(Res.GetFormatted("StatusOpened", file.Name));
                _settings.AddRecentFile(file.Path);
                UpdateStatusBarCounts();
                UpdateEncoding(isTxt ? "UTF-8" : "RTF");
                ActiveTab.Encoding = isTxt ? "UTF-8" : "RTF";
            }
        }

        private static async Task<string> ExtractTextFromArchiveAsync(StorageFile file, string ext)
        {
            using var stream = await file.OpenStreamForReadAsync();
            return DocumentImportHelper.ExtractText(stream, ext);
        }

        private void ApplySettings()
        {
            Editor.TextWrapping = _settings.DefaultWordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
            ViewModel.IsWordWrap = _settings.DefaultWordWrap;
            ViewModel.FontFamily = _settings.DefaultFontFamily;
            ViewModel.FontSize = _settings.DefaultFontSize;
            Editor.IsSpellCheckEnabled = _settings.SpellCheckEnabled;
            SpellCheckToggle?.IsChecked = _settings.SpellCheckEnabled;
            ApplyThemeFromSettings();
        }

        private void ApplyThemeFromSettings()
        {
            if (Content is FrameworkElement root)
            {
                root.RequestedTheme = _settings.ThemePreference switch
                {
                    "Light" => ElementTheme.Light,
                    "Dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };
            }
            UpdateTitleBarTheme();
        }

        private void UpdateTitleBarTheme()
        {
            if (!AppWindowTitleBar.IsCustomizationSupported()) return;

            bool isDark = _settings.ThemePreference switch
            {
                "Dark"  => true,
                "Light" => false,
                _       => Application.Current.RequestedTheme == ApplicationTheme.Dark
            };

            var titleBar = AppWindow.TitleBar;
            titleBar.ButtonBackgroundColor         = Color.FromArgb(0, 0, 0, 0);
            titleBar.ButtonInactiveBackgroundColor = Color.FromArgb(0, 0, 0, 0);

            if (isDark)
            {
                titleBar.ForegroundColor               = Color.FromArgb(255, 255, 255, 255);
                titleBar.InactiveForegroundColor       = Color.FromArgb(160, 255, 255, 255);
                titleBar.ButtonForegroundColor         = Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonHoverForegroundColor    = Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonHoverBackgroundColor    = Color.FromArgb(25,  255, 255, 255);
                titleBar.ButtonPressedForegroundColor  = Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonPressedBackgroundColor  = Color.FromArgb(50,  255, 255, 255);
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(128, 255, 255, 255);
            }
            else
            {
                titleBar.ForegroundColor               = Color.FromArgb(255, 0, 0, 0);
                titleBar.InactiveForegroundColor       = Color.FromArgb(160, 0, 0, 0);
                titleBar.ButtonForegroundColor         = Color.FromArgb(255, 0, 0, 0);
                titleBar.ButtonHoverForegroundColor    = Color.FromArgb(255, 0, 0, 0);
                titleBar.ButtonHoverBackgroundColor    = Color.FromArgb(25,  0, 0, 0);
                titleBar.ButtonPressedForegroundColor  = Color.FromArgb(255, 0, 0, 0);
                titleBar.ButtonPressedBackgroundColor  = Color.FromArgb(50,  0, 0, 0);
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(128, 0, 0, 0);
            }
        }

        private void SetupAutoSave()
        {
            if (_settings.AutoSaveEnabled && _settings.AutoSaveIntervalSeconds > 0)
            {
                _autoSaveTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(_settings.AutoSaveIntervalSeconds)
                };
                _autoSaveTimer.Tick += async (s, e) =>
                {
                    if (ViewModel.IsModified && ActiveTab.CurrentFile != null)
                    {
                        try
                        {
                            using (var stream = await ActiveTab.CurrentFile.OpenAsync(FileAccessMode.ReadWrite))
                            {
                                Editor.Document.SaveToStream(TextGetOptions.FormatRtf, stream);
                            }
                            ViewModel.IsModified = false;
                            ViewModel.UpdateStatus(Res.GetFormatted("StatusAutoSaved", ActiveTab.CurrentFile.Name));
                        }
                        catch { }
                    }
                    else if (ViewModel.IsModified && ActiveTab.CurrentFile == null)
                    {
                        await AutoSaveRecoveryAsync();
                    }
                };
                _autoSaveTimer.Start();
            }
        }

        private async Task AutoSaveRecoveryAsync()
        {
            try
            {
                string recoveryDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SmrtPad", "Recovery");
                Directory.CreateDirectory(recoveryDir);

                var folder = await StorageFolder.GetFolderFromPathAsync(recoveryDir);
                var recoveryFile = await folder.CreateFileAsync(
                    $"recovery_{DateTime.Now:yyyyMMdd_HHmmss}.rtf",
                    CreationCollisionOption.GenerateUniqueName);
                using (var stream = await recoveryFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    Editor.Document.SaveToStream(TextGetOptions.FormatRtf, stream);
                }
                ViewModel.UpdateStatus(Res.GetString("StatusRecoverySaved"));
            }
            catch { }
        }

        private void UpdateStatusBarCounts()
        {
            Editor.Document.GetText(TextGetOptions.None, out string text);
            text = text.TrimEnd('\r');
            int wordCount = string.IsNullOrWhiteSpace(text)
                ? 0
                : text.Split(s_wordSeparators, StringSplitOptions.RemoveEmptyEntries).Length;
            int charCount = text.Length;

            ViewModel.WordCount = wordCount;
            ViewModel.CharCount = charCount;
        }

        private void UpdateLineColumn()
        {
            var selection = Editor.Document.Selection;
            if (selection == null) return;

            Editor.Document.GetText(TextGetOptions.None, out string fullText);
            int pos = selection.StartPosition;
            if (pos > fullText.Length) pos = fullText.Length;

            string textBefore = fullText[..pos];
            int line = 1 + textBefore.Count(c => c == '\r');
            int lastNewLine = textBefore.LastIndexOf('\r');
            int col = (lastNewLine >= 0) ? pos - lastNewLine : pos + 1;

            ViewModel.LineNumber = line;
            ViewModel.ColumnNumber = col;
        }

        private void UpdateSelectionLength()
        {
            var selection = Editor.Document.Selection;
            if (selection == null) return;

            int length = Math.Abs(selection.EndPosition - selection.StartPosition);
            ViewModel.SelectionLength = length;
        }

        private void UpdateEncoding(string encoding)
        {
            ViewModel.Encoding = encoding;
            EncodingText.Text = encoding;
        }

        // Image hosting now uses native RichEdit OLE objects.

        private void HideBackstage()
        {
            FileBackstage.Visibility = Visibility.Collapsed;
        }

        private void ShowBackstage()
        {
            FileBackstage.Visibility = Visibility.Visible;
            FileBackstage.SetDocumentProperties(
                ActiveTab.CurrentFile?.Name ?? ViewModel.DocumentTitle,
                ViewModel.WordCount,
                ViewModel.CharCount,
                ActiveTab.Encoding,
                ActiveTab.IsModified);
            FileBackstage.SetRecentFiles(_settings.RecentFiles);
        }

        private void Editor_SelectionChanged(object _, RoutedEventArgs _1)
        {
            ITextSelection selection = Editor.Document.Selection;
            if (selection == null) return;

            ITextCharacterFormat charFormat = selection.CharacterFormat;

            // Update ViewModel properties — toggle buttons sync via {x:Bind} TwoWay
            ViewModel.IsBold = charFormat.Bold == FormatEffect.On;
            ViewModel.IsItalic = charFormat.Italic == FormatEffect.On;
            ViewModel.IsUnderline = charFormat.Underline != UnderlineType.None;
            ViewModel.IsStrikethrough = charFormat.Strikethrough == FormatEffect.On;
            ViewModel.IsSubscript = charFormat.Subscript == FormatEffect.On;
            ViewModel.IsSuperscript = charFormat.Superscript == FormatEffect.On;

            if (!string.IsNullOrEmpty(charFormat.Name))
            {
                ViewModel.FontFamily = charFormat.Name;
                _suppressFontComboChange = true;
                FontFamilyComboBox.Text = charFormat.Name;
                _suppressFontComboChange = false;
            }
            if (charFormat.Size > 0)
            {
                ViewModel.FontSize = charFormat.Size;
                FontSizeComboBox.Text = ((int)charFormat.Size).ToString();
            }

            ITextParagraphFormat paraFormat = selection.ParagraphFormat;
            switch (paraFormat.Alignment)
            {
                case ParagraphAlignment.Left:
                    SetAlignmentToggle(AlignLeftToggle);
                    ViewModel.Alignment = "Left";
                    break;
                case ParagraphAlignment.Center:
                    SetAlignmentToggle(AlignCenterToggle);
                    ViewModel.Alignment = "Center";
                    break;
                case ParagraphAlignment.Right:
                    SetAlignmentToggle(AlignRightToggle);
                    ViewModel.Alignment = "Right";
                    break;
                case ParagraphAlignment.Justify:
                    SetAlignmentToggle(AlignJustifyToggle);
                    ViewModel.Alignment = "Justify";
                    break;
            }

            UpdateLineColumn();
            UpdateSelectionLength();
        }

        private void InitializeFonts()
        {
            var fonts = Microsoft.Graphics.Canvas.Text.CanvasTextFormat.GetSystemFontFamilies();
            FontFamilyComboBox.ItemsSource = fonts.OrderBy(f => f).ToList();
            _suppressFontComboChange = true;
            FontFamilyComboBox.SelectedItem = _settings.DefaultFontFamily;
            _suppressFontComboChange = false;

            var sizes = new List<double> { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 };
            FontSizeComboBox.ItemsSource = sizes;
            FontSizeComboBox.SelectedItem = _settings.DefaultFontSize;
            FontSizeComboBox.Text = _settings.DefaultFontSize.ToString();

            FontSizeComboBox.KeyDown += FontSizeComboBox_KeyDown;
            FontSizeComboBox.LostFocus += FontSizeComboBox_LostFocus;
        }

        private void FontFamilyComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            // Editable ComboBox in WinUI 3 doesn't reliably display SelectedItem text
            // until after the layout pass completes. Defer via DispatcherQueue so the
            // internal TextBox is fully initialized before we set its Text.
            DispatcherQueue.TryEnqueue(() =>
            {
                _suppressFontComboChange = true;
                FontFamilyComboBox.Text = _settings.DefaultFontFamily;
                _suppressFontComboChange = false;
            });
        }

        private void FontFamilyComboBox_DropDownOpened(object sender, object e)
        {
            if (_fontDropdownStyled) return;

            // ComboBox uses a non-virtualizing CarouselPanel, so all containers
            // are created when the dropdown opens. Set each item's FontFamily
            // so font names preview in their own typeface.
            for (int i = 0; i < FontFamilyComboBox.Items.Count; i++)
            {
                if (FontFamilyComboBox.ContainerFromIndex(i) is ComboBoxItem container
                    && container.Content is string fontName)
                {
                    container.FontFamily = new FontFamily(fontName);
                }
            }

            _fontDropdownStyled = true;
        }

        private void ApplyFontSizeFromText()
        {
            string text = FontSizeComboBox.Text;
            if (double.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double size) && size >= 1 && size <= 999)
            {
                ITextSelection selectedText = Editor.Document.Selection;
                if (selectedText != null)
                {
                    ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                    charFormatting.Size = (float)size;
                    selectedText.CharacterFormat = charFormatting;
                    ViewModel.FontSize = size;
                    _macro.Record(MacroCommandType.SetFontSize, size.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        }

        private void FontSizeComboBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ApplyFontSizeFromText();
                e.Handled = true;
            }
        }

        private void FontSizeComboBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyFontSizeFromText();
        }

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressFontComboChange) return;
            if (FontFamilyComboBox.SelectedItem is string fontName)
            {
                ITextSelection selectedText = Editor.Document.Selection;
                if (selectedText != null)
                {
                    ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                    charFormatting.Name = fontName;
                    selectedText.CharacterFormat = charFormatting;
                    _macro.Record(MacroCommandType.SetFontFamily, fontName);
                }
            }
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontSizeComboBox.SelectedItem is double fontSize)
            {
                ITextSelection selectedText = Editor.Document.Selection;
                if (selectedText != null)
                {
                    ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                    charFormatting.Size = (float)fontSize;
                    selectedText.CharacterFormat = charFormatting;
                    _macro.Record(MacroCommandType.SetFontSize, fontSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
        }

        private async Task<bool> PromptSaveChangesAsync()
        {
            if (!ViewModel.IsModified)
                return true;

            var result = await _dialogService.ShowSavePromptAsync(ViewModel.DocumentTitle);
            if (result == SavePromptResult.Save)
            {
                Save_Click(this, new RoutedEventArgs());
                return true;
            }
            return result == SavePromptResult.DontSave;
        }

        private async void New_Click(object sender, RoutedEventArgs e)
        {
            if (!await PromptSaveChangesAsync())
                return;

            // Reuse the current tab if it is already a blank untitled document,
            // otherwise open a new tab so the user's document isn't lost.
            bool currentIsBlank = ActiveTab.CurrentFile == null && !ActiveTab.IsModified;
            if (!currentIsBlank)
            {
                CreateTab(Res.GetString("DocumentUntitled"));
            }

            Editor.Document.SetText(TextSetOptions.None, string.Empty);
            ActiveTab.CurrentFile = null;
            ActiveTab.IsModified = false;
            ActiveTab.Encoding = "UTF-8";
            ViewModel.NewDocument();
            ActiveTab.TabViewItem.Header = ViewModel.DocumentTitle;
            UpdateEncoding("UTF-8");
        }

        private void FileMenu_Tapped(object sender, RoutedEventArgs e)
        {
            if (FileBackstage.Visibility == Visibility.Visible)
                HideBackstage();
            else
                ShowBackstage();
        }

        private async void Open_Click(object _, RoutedEventArgs _1)
        {
            try
            {
                if (!await PromptSaveChangesAsync())
                    return;

                var picker = new FileOpenPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                picker.ViewMode = PickerViewMode.List;
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".rtf");
                picker.FileTypeFilter.Add(".txt");
                picker.FileTypeFilter.Add(".docx");
                picker.FileTypeFilter.Add(".htm");
                picker.FileTypeFilter.Add(".html");
                picker.FileTypeFilter.Add(".odt");

                StorageFile file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    await OpenStorageFileAsync(file);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorOpeningFile"), ex.Message);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ActiveTab.CurrentFile == null)
                {
                    var picker = new FileSavePicker();
                    InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                    picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                    picker.FileTypeChoices.Add(Res.GetString("FileTypeRtf"), [".rtf"]);
                    picker.FileTypeChoices.Add(Res.GetString("FileTypeTxt"), [".txt"]);
                    picker.SuggestedFileName = Res.GetString("FileDefaultName");

                    StorageFile file = await picker.PickSaveFileAsync();
                    if (file != null)
                    {
                        CachedFileManager.DeferUpdates(file);
                        using (var randAccStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                        {
                            Editor.Document.SaveToStream(TextGetOptions.FormatRtf, randAccStream);
                        }
                        FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                        if (status == FileUpdateStatus.Complete)
                        {
                            ActiveTab.CurrentFile = file;
                            ViewModel.DocumentTitle = file.Name;
                            ViewModel.IsModified = false;
                            ViewModel.UpdateStatus(Res.GetFormatted("StatusSaved", file.Name));
                            _settings.AddRecentFile(file.Path);
                        }
                    }
                }
                else
                {
                    using (var randAccStream = await ActiveTab.CurrentFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        Editor.Document.SaveToStream(TextGetOptions.FormatRtf, randAccStream);
                    }
                    ViewModel.IsModified = false;
                    ViewModel.UpdateStatus(Res.GetFormatted("StatusSaved", ActiveTab.CurrentFile.Name));
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorSavingFile"), ex.Message);
            }
        }

        private async void SaveAs_Click(object _, RoutedEventArgs _1)
        {
            try
            {
                var picker = new FileSavePicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add(Res.GetString("FileTypeRtf"), [".rtf"]);
                picker.FileTypeChoices.Add(Res.GetString("FileTypeTxt"), [".txt"]);
                picker.SuggestedFileName = ActiveTab.CurrentFile?.DisplayName ?? Res.GetString("FileDefaultName");

                StorageFile file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    CachedFileManager.DeferUpdates(file);
                    using (var randAccStream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        var options = file.FileType.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                            ? TextGetOptions.None
                            : TextGetOptions.FormatRtf;
                        Editor.Document.SaveToStream(options, randAccStream);
                    }
                    FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                    if (status == FileUpdateStatus.Complete)
                    {
                        ActiveTab.CurrentFile = file;
                        ViewModel.DocumentTitle = file.Name;
                        ViewModel.IsModified = false;
                        ViewModel.UpdateStatus(Res.GetFormatted("StatusSaved", file.Name));
                        _settings.AddRecentFile(file.Path);
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorSavingFile"), ex.Message);
            }
        }

        private async void Print_Click(object _, RoutedEventArgs _1)
        {
            if (!PrintManager.IsSupported())
            {
                await ShowErrorDialogAsync(Res.GetString("PrintNotSupported"), Res.GetString("PrintNotSupportedMessage"));
                return;
            }

            Editor.Document.GetText(TextGetOptions.None, out string plainText);
            if (string.IsNullOrWhiteSpace(plainText.TrimEnd('\r')))
            {
                await ShowErrorDialogAsync(Res.GetString("PrintTitle"), Res.GetString("PrintNoContent"));
                return;
            }

            try
            {
                ViewModel.UpdateStatus(Res.GetString("StatusPrinting"));
                var hWnd = WindowNative.GetWindowHandle(this);
                await PrintManagerInterop.ShowPrintUIForWindowAsync(hWnd);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorPrint"), ex.Message);
            }
        }

        private void RegisterForPrinting()
        {
            _printDocument = new PrintDocument();
            _printDocumentSource = _printDocument.DocumentSource;
            _printDocument.Paginate += PrintDocument_Paginate;
            _printDocument.GetPreviewPage += PrintDocument_GetPreviewPage;
            _printDocument.AddPages += PrintDocument_AddPages;

            var hWnd = WindowNative.GetWindowHandle(this);
            PrintManager printManager = PrintManagerInterop.GetForWindow(hWnd);
            printManager.PrintTaskRequested += PrintTask_Requested;
        }

        private void PrintTask_Requested(PrintManager sender, PrintTaskRequestedEventArgs args)
        {
            PrintTask printTask = args.Request.CreatePrintTask(
                Res.GetFormatted("PrintJobTitle", ViewModel.DocumentTitle),
                PrintTaskSourceRequested);

            printTask.Completed += PrintTask_Completed;
        }

        private void PrintTaskSourceRequested(PrintTaskSourceRequestedArgs args)
        {
            args.SetSource(_printDocumentSource);
        }

        private void PrintTask_Completed(PrintTask sender, PrintTaskCompletedEventArgs args)
        {
            string status = args.Completion switch
            {
                PrintTaskCompletion.Failed => Res.GetString("StatusPrintFailed"),
                PrintTaskCompletion.Canceled => Res.GetString("StatusPrintCancelled"),
                _ => Res.GetString("StatusPrintCompleted")
            };

            DispatcherQueue.TryEnqueue(() => ViewModel.UpdateStatus(status));
        }

        private void PrintDocument_Paginate(object sender, PaginateEventArgs e)
        {
            _printPreviewPages.Clear();

            PrintTaskOptions options = (PrintTaskOptions)e.PrintTaskOptions;
            PrintPageDescription pageDesc = options.GetPageDescription(0);
            double pageWidth = pageDesc.PageSize.Width;
            double pageHeight = pageDesc.PageSize.Height;
            double margin = 48;

            Editor.Document.GetText(TextGetOptions.None, out string fullText);
            string[] lines = fullText.TrimEnd('\r').Split('\r');

            int linesPerPage = Math.Max(1, (int)((pageHeight - margin * 2) / 18));
            int totalPages = Math.Max(1, (int)Math.Ceiling((double)lines.Length / linesPerPage));

            for (int page = 0; page < totalPages; page++)
            {
                var pagePanel = new StackPanel
                {
                    Width = pageWidth,
                    Height = pageHeight,
                    Padding = new Thickness(margin)
                };

                int startLine = page * linesPerPage;
                int endLine = Math.Min(startLine + linesPerPage, lines.Length);
                string pageText = string.Join(Environment.NewLine, lines[startLine..endLine]);

                pagePanel.Children.Add(new TextBlock
                {
                    Text = pageText,
                    FontFamily = new FontFamily(ViewModel.FontFamily),
                    FontSize = ViewModel.FontSize,
                    TextWrapping = TextWrapping.Wrap,
                    Width = pageWidth - margin * 2
                });

                _printPreviewPages.Add(pagePanel);
            }

            PrintDocument printDoc = (PrintDocument)sender;
            printDoc.SetPreviewPageCount(_printPreviewPages.Count, PreviewPageCountType.Final);
        }

        private void PrintDocument_GetPreviewPage(object sender, GetPreviewPageEventArgs e)
        {
            PrintDocument printDoc = (PrintDocument)sender;
            printDoc.SetPreviewPage(e.PageNumber, _printPreviewPages[e.PageNumber - 1]);
        }

        private void PrintDocument_AddPages(object sender, AddPagesEventArgs e)
        {
            PrintDocument printDoc = (PrintDocument)sender;
            foreach (var page in _printPreviewPages)
            {
                printDoc.AddPage(page);
            }
            printDoc.AddPagesComplete();
        }

        private async void Options_Click(object _, RoutedEventArgs _1)
        {
            var panel = new StackPanel { Spacing = 12, MinWidth = 350 };

            var fontFamilyBox = new ComboBox { Header = Res.GetString("OptionsDefaultFont"), Width = 200, IsEditable = true };
            var systemFonts = Microsoft.Graphics.Canvas.Text.CanvasTextFormat.GetSystemFontFamilies();
            fontFamilyBox.ItemsSource = systemFonts.OrderBy(f => f).ToList();
            fontFamilyBox.SelectedItem = _settings.DefaultFontFamily;
            panel.Children.Add(fontFamilyBox);

            var fontSizeBox = new NumberBox { Header = Res.GetString("OptionsDefaultFontSize"), Minimum = 1, Maximum = 999, Value = _settings.DefaultFontSize, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            panel.Children.Add(fontSizeBox);

            var wordWrapCheck = new CheckBox { Content = Res.GetString("OptionsWordWrap"), IsChecked = _settings.DefaultWordWrap };
            panel.Children.Add(wordWrapCheck);

            var saveFormatBox = new ComboBox { Header = Res.GetString("OptionsDefaultSaveFormat"), Width = 200 };
            saveFormatBox.Items.Add(".rtf");
            saveFormatBox.Items.Add(".txt");
            saveFormatBox.SelectedItem = _settings.DefaultSaveFormat;
            panel.Children.Add(saveFormatBox);

            var themeBox = new ComboBox { Header = Res.GetString("OptionsTheme"), Width = 200 };
            themeBox.Items.Add(Res.GetString("OptionsThemeSystem"));
            themeBox.Items.Add(Res.GetString("OptionsThemeLight"));
            themeBox.Items.Add(Res.GetString("OptionsThemeDark"));
            themeBox.SelectedItem = _settings.ThemePreference;
            panel.Children.Add(themeBox);

            var autoSaveCheck = new CheckBox { Content = Res.GetString("OptionsAutoSave"), IsChecked = _settings.AutoSaveEnabled };
            panel.Children.Add(autoSaveCheck);

            var autoSaveInterval = new NumberBox { Header = Res.GetString("OptionsAutoSaveInterval"), Minimum = 30, Maximum = 3600, Value = _settings.AutoSaveIntervalSeconds, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            panel.Children.Add(autoSaveInterval);

            var languageBox = new ComboBox { Header = Res.GetString("OptionsLanguage"), Width = 200 };
            var supportedLocales = new (string Tag, string Display)[]
            {
                ("en-US", "English (United States)"),
                ("de-DE", "Deutsch (Deutschland)"),
                ("es-ES", "Español (España)"),
                ("fr-FR", "Français (France)"),
                ("ja-JP", "日本語 (日本)"),
                ("zh-Hans", "中文 (简体)"),
                ("ar-SA", "العربية (السعودية)"),
                ("ru-RU", "Русский (Россия)"),
                ("ur-PK", "اردو (پاکستان)")
            };
            foreach (var (tag, display) in supportedLocales)
                languageBox.Items.Add(display);
            int selectedLocaleIndex = Array.FindIndex(supportedLocales, l => l.Tag == _settings.Language);
            languageBox.SelectedIndex = selectedLocaleIndex >= 0 ? selectedLocaleIndex : 0;
            panel.Children.Add(languageBox);
            string originalLanguage = _settings.Language;

            var rulerUnitsBox = new ComboBox { Header = Res.GetString("OptionsRulerUnits"), Width = 200 };
            rulerUnitsBox.Items.Add(Res.GetString("OptionsRulerInches"));
            rulerUnitsBox.Items.Add(Res.GetString("OptionsRulerCentimeters"));
            rulerUnitsBox.SelectedIndex = _settings.RulerUnits == "cm" ? 1 : 0;
            panel.Children.Add(rulerUnitsBox);

            var spellCheckBox = new CheckBox { Content = Res.GetString("OptionsSpellCheck"), IsChecked = _settings.SpellCheckEnabled };
            panel.Children.Add(spellCheckBox);

            var dialog = new ContentDialog
            {
                Title = Res.GetString("OptionsTitle"),
                Content = new ScrollViewer { Content = panel, MaxHeight = 400 },
                PrimaryButtonText = Res.GetString("ButtonSave"),
                CloseButtonText = Res.GetString("ButtonCancel"),
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _settings.DefaultFontFamily = fontFamilyBox.SelectedItem as string ?? "Segoe UI";
                _settings.DefaultFontSize = fontSizeBox.Value;
                _settings.DefaultWordWrap = wordWrapCheck.IsChecked == true;
                _settings.DefaultSaveFormat = saveFormatBox.SelectedItem as string ?? ".rtf";
                _settings.ThemePreference = themeBox.SelectedItem as string ?? "System";
                _settings.AutoSaveEnabled = autoSaveCheck.IsChecked == true;
                _settings.AutoSaveIntervalSeconds = (int)autoSaveInterval.Value;
                int langIdx = languageBox.SelectedIndex;
                _settings.Language = langIdx >= 0 && langIdx < supportedLocales.Length
                    ? supportedLocales[langIdx].Tag
                    : "en-US";
                _settings.RulerUnits = rulerUnitsBox.SelectedIndex == 1 ? "cm" : "in";
                _settings.SpellCheckEnabled = spellCheckBox.IsChecked == true;
                Editor.IsSpellCheckEnabled = _settings.SpellCheckEnabled;
                SpellCheckToggle?.IsChecked = _settings.SpellCheckEnabled;
                _settings.Save();
                ApplyThemeFromSettings();
                SetupAutoSave();
                if (_rulersVisible) RedrawRulers();
                ViewModel.UpdateStatus(Res.GetString("StatusOptionsSaved"));

                if (_settings.Language != originalLanguage)
                {
                    Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride =
                        _settings.Language == "en-US" ? string.Empty : _settings.Language;

                    var restartDialog = new ContentDialog
                    {
                        Title = Res.GetString("LanguageRestartTitle"),
                        Content = Res.GetString("LanguageRestartMessage"),
                        PrimaryButtonText = Res.GetString("LanguageRestartNow"),
                        CloseButtonText = Res.GetString("DlgOK"),
                        XamlRoot = Content.XamlRoot
                    };
                    if (await restartDialog.ShowAsync() == ContentDialogResult.Primary)
                        Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
                }
            }
        }

        // ── Spell Check ──────────────────────────────────────────────────────────

        private void SpellCheck_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem toggle)
            {
                bool enabled = toggle.IsChecked;
                Editor.IsSpellCheckEnabled = enabled;
                _settings.SpellCheckEnabled = enabled;
                _settings.Save();
                ViewModel.UpdateStatus(enabled
                    ? Res.GetString("StatusSpellCheckEnabled")
                    : Res.GetString("StatusSpellCheckDisabled"));
            }
        }

        // ── Export to PDF ────────────────────────────────────────────────────────

        private async void ExportPdf_Click(object _, RoutedEventArgs _1)
        {
            try
            {
                var picker = new FileSavePicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add(Res.GetString("FileTypePdf"), [".pdf"]);
                picker.SuggestedFileName = ActiveTab.CurrentFile?.DisplayName ?? Res.GetString("FileDefaultName");

                StorageFile file = await picker.PickSaveFileAsync();
                if (file == null) return;

                Editor.Document.GetText(TextGetOptions.None, out string text);
                byte[] pdf = PdfHelper.GeneratePdf(text.TrimEnd('\r'));

                CachedFileManager.DeferUpdates(file);
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                using (var writer = stream.AsStreamForWrite())
                {
                    await writer.WriteAsync(pdf.AsMemory());
                    await writer.FlushAsync();
                    stream.Size = (ulong)pdf.Length;
                }
                await CachedFileManager.CompleteUpdatesAsync(file);
                ViewModel.UpdateStatus(Res.GetFormatted("StatusExportedPdf", file.Name));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorExportingPdf"), ex.Message);
            }
        }

        // ── Export to DOCX ───────────────────────────────────────────────────────

        private async void ExportDocx_Click(object _, RoutedEventArgs _1)
        {
            try
            {
                var picker = new FileSavePicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add(Res.GetString("FileTypeDocx"), [".docx"]);
                picker.SuggestedFileName = ActiveTab.CurrentFile?.DisplayName ?? Res.GetString("FileDefaultName");

                StorageFile file = await picker.PickSaveFileAsync();
                if (file == null) return;

                Editor.Document.GetText(TextGetOptions.FormatRtf, out string rtf);
                byte[] docx = DocxExportHelper.GenerateRichDocx(rtf);

                CachedFileManager.DeferUpdates(file);
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                using (var writer = stream.AsStreamForWrite())
                {
                    await writer.WriteAsync(docx.AsMemory());
                    await writer.FlushAsync();
                    stream.Size = (ulong)docx.Length;
                }
                await CachedFileManager.CompleteUpdatesAsync(file);
                ViewModel.UpdateStatus(Res.GetFormatted("StatusExportedDocx", file.Name));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorExportingDocx"), ex.Message);
            }
        }

        // ── Save to OneDrive ─────────────────────────────────────────────────────

        private async void SaveToOneDrive_Click(object _, RoutedEventArgs _1)
        {
            try
            {
                if (!OneDriveHelper.IsAvailable())
                {
                    await ShowErrorDialogAsync(
                        Res.GetString("OneDriveNotFound"),
                        Res.GetString("OneDriveNotFoundMessage"));
                    return;
                }

                var picker = new FileSavePicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add(Res.GetString("FileTypeRtf"), [".rtf"]);
                picker.FileTypeChoices.Add(Res.GetString("FileTypeTxt"), [".txt"]);
                picker.SuggestedFileName = ActiveTab.CurrentFile?.DisplayName ?? Res.GetString("FileDefaultName");

                StorageFile file = await picker.PickSaveFileAsync();
                if (file == null) return;

                CachedFileManager.DeferUpdates(file);
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var options = file.FileType.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                        ? TextGetOptions.None : TextGetOptions.FormatRtf;
                    Editor.Document.SaveToStream(options, stream);
                }
                await CachedFileManager.CompleteUpdatesAsync(file);

                ActiveTab.CurrentFile = file;
                ActiveTab.IsModified = false;
                ViewModel.DocumentTitle = file.Name;
                ViewModel.IsModified = false;
                ActiveTab.TabViewItem.Header = file.Name;
                _settings.AddRecentFile(file.Path);
                ViewModel.UpdateStatus(Res.GetFormatted("StatusSavedToOneDrive", file.Name));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorSavingFile"), ex.Message);
            }
        }

        // ── Macro recording & playback ───────────────────────────────────────────

        private void MacroRecord_Click(object _sender, RoutedEventArgs _e)
        {
            _macro.StartRecording();
            MacroRecordItem.Text = Res.GetString("MacroRecord");
            MacroStopItem.IsEnabled = true;
            MacroRecordItem.IsEnabled = false;
            ViewModel.UpdateStatus(Res.GetString("StatusMacroRecording"));
        }

        private void MacroStop_Click(object _sender, RoutedEventArgs _e)
        {
            _macro.StopRecording();
            MacroStopItem.IsEnabled = false;
            MacroRecordItem.IsEnabled = true;
            ViewModel.UpdateStatus(Res.GetString("StatusMacroStopped"));
        }

        private void MacroRun_Click(object sender, RoutedEventArgs e)
        {
            if (_macro.Count == 0)
            {
                ViewModel.UpdateStatus(Res.GetString("MacroNoCommands"));
                return;
            }
            foreach (var cmd in _macro.Commands)
                ExecuteMacroCommand(cmd);
            ViewModel.UpdateStatus(Res.GetString("StatusMacroDone"));
        }

        private void ExecuteMacroCommand(MacroCommand cmd)
        {
            switch (cmd.Type)
            {
                case MacroCommandType.Bold:         Bold_Click(this, new RoutedEventArgs()); break;
                case MacroCommandType.Italic:       Italic_Click(this, new RoutedEventArgs()); break;
                case MacroCommandType.Underline:    Underline_Click(this, new RoutedEventArgs()); break;
                case MacroCommandType.Strikethrough: Strikethrough_Click(this, new RoutedEventArgs()); break;
                case MacroCommandType.Subscript:    Subscript_Click(this, new RoutedEventArgs()); break;
                case MacroCommandType.Superscript:  Superscript_Click(this, new RoutedEventArgs()); break;
                case MacroCommandType.ClearFormatting: ClearFormatting_Click(this, new RoutedEventArgs()); break;
                case MacroCommandType.ZoomIn:       ZoomIn_Click(this, new RoutedEventArgs()); break;
                case MacroCommandType.ZoomOut:      ZoomOut_Click(this, new RoutedEventArgs()); break;
                case MacroCommandType.SetAlignment when cmd.Value is not null:
                    ViewModel.SetAlignment(cmd.Value); break;
                case MacroCommandType.SetFontFamily when cmd.Value is not null:
                {
                    var cf = Editor.Document.Selection.CharacterFormat;
                    cf.Name = cmd.Value;
                    Editor.Document.Selection.CharacterFormat = cf;
                    break;
                }
                case MacroCommandType.SetFontSize when cmd.Value is not null
                     && float.TryParse(cmd.Value, System.Globalization.NumberStyles.Any,
                         System.Globalization.CultureInfo.InvariantCulture, out float sz):
                {
                    var cf = Editor.Document.Selection.CharacterFormat;
                    cf.Size = sz;
                    Editor.Document.Selection.CharacterFormat = cf;
                    break;
                }
                case MacroCommandType.InsertText when cmd.Value is not null:
                    Editor.Document.Selection.Text = cmd.Value; break;
                case MacroCommandType.SetListType when cmd.Value is not null:
                {
                    var marker = cmd.Value switch
                    {
                        "Bullet"          => MarkerType.Bullet,
                        "Number"          => MarkerType.Arabic,
                        "LowercaseLetter" => MarkerType.LowercaseEnglishLetter,
                        "UppercaseLetter" => MarkerType.UppercaseEnglishLetter,
                        "LowercaseRoman"  => MarkerType.LowercaseRoman,
                        "UppercaseRoman"  => MarkerType.UppercaseRoman,
                        _                 => MarkerType.None,
                    };
                    ApplyListType(marker, cmd.Value);
                    break;
                }
                case MacroCommandType.SetLineSpacing when cmd.Value is not null
                     && double.TryParse(cmd.Value, System.Globalization.NumberStyles.Any,
                         System.Globalization.CultureInfo.InvariantCulture, out double sp):
                {
                    var pf = Editor.Document.Selection.ParagraphFormat;
                    if (sp == 1.0)      pf.SetLineSpacing(LineSpacingRule.Single, 0);
                    else if (sp == 1.5) pf.SetLineSpacing(LineSpacingRule.OneAndHalf, 0);
                    else if (sp == 2.0) pf.SetLineSpacing(LineSpacingRule.Double, 0);
                    else                pf.SetLineSpacing(LineSpacingRule.Multiple, (float)sp);
                    Editor.Document.Selection.ParagraphFormat = pf;
                    ViewModel.SetLineSpacing(sp);
                    break;
                }
            }
        }

        private async void MacroSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileSavePicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add(Res.GetString("MacroFilter"), [".smacro"]);
                picker.SuggestedFileName = "macro";

                StorageFile file = await picker.PickSaveFileAsync();
                if (file == null) return;

                await Windows.Storage.FileIO.WriteTextAsync(file, _macro.Serialize());
                ViewModel.UpdateStatus(Res.GetString("StatusMacroSaved"));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorGeneric"), ex.Message);
            }
        }

        private async void MacroLoad_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".smacro");

                StorageFile file = await picker.PickSingleFileAsync();
                if (file == null) return;

                string json = await Windows.Storage.FileIO.ReadTextAsync(file);
                _macro.Deserialize(json);
                ViewModel.UpdateStatus(Res.GetString("StatusMacroLoaded"));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorGeneric"), ex.Message);
            }
        }

        private void Cut_Click(object sender, RoutedEventArgs e)
        {
            Editor.Document.Selection.Cut();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            Editor.Document.Selection.Copy();
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            Editor.Document.Selection.Paste(0);
        }

        private void Bold_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                charFormatting.Bold = FormatEffect.Toggle;
                selectedText.CharacterFormat = charFormatting;
            }
            _macro.Record(MacroCommandType.Bold);
        }

        private void Italic_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                charFormatting.Italic = FormatEffect.Toggle;
                selectedText.CharacterFormat = charFormatting;
            }
            _macro.Record(MacroCommandType.Italic);
        }

        private void Underline_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                if (charFormatting.Underline == UnderlineType.None)
                {
                    charFormatting.Underline = UnderlineType.Single;
                }
                else
                {
                    charFormatting.Underline = UnderlineType.None;
                }
                selectedText.CharacterFormat = charFormatting;
            }
            _macro.Record(MacroCommandType.Underline);
        }

        private void Strikethrough_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                if (charFormatting.Strikethrough == FormatEffect.On)
                {
                    charFormatting.Strikethrough = FormatEffect.Off;
                }
                else
                {
                    charFormatting.Strikethrough = FormatEffect.On;
                }
                selectedText.CharacterFormat = charFormatting;
            }
            _macro.Record(MacroCommandType.Strikethrough);
        }

        private void Subscript_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                bool isChecked = SubscriptToggle.IsChecked == true;
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                charFormatting.Subscript = isChecked ? FormatEffect.On : FormatEffect.Off;
                if (isChecked)
                {
                    charFormatting.Superscript = FormatEffect.Off;
                    SuperscriptToggle.IsChecked = false;
                }
                selectedText.CharacterFormat = charFormatting;
            }
            _macro.Record(MacroCommandType.Subscript);
        }

        private void Superscript_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                bool isChecked = SuperscriptToggle.IsChecked == true;
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                charFormatting.Superscript = isChecked ? FormatEffect.On : FormatEffect.Off;
                if (isChecked)
                {
                    charFormatting.Subscript = FormatEffect.Off;
                    SubscriptToggle.IsChecked = false;
                }
                selectedText.CharacterFormat = charFormatting;
            }
            _macro.Record(MacroCommandType.Superscript);
        }

        private void NewWindow_Click(object _sender, RoutedEventArgs _e)
        {
            App.NewWindow();
        }

        private async void Exit_Click(object _, RoutedEventArgs _1)
        {
            if (!await PromptSaveChangesAsync())
                return;
            Close();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            Editor.Document.Selection.Expand(TextRangeUnit.Story);
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ZoomIn();
            ApplyZoom();
            _macro.Record(MacroCommandType.ZoomIn);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ZoomOut();
            ApplyZoom();
            _macro.Record(MacroCommandType.ZoomOut);
        }

        private void ApplyZoom()
        {
            double scale = ViewModel.ZoomLevel / 100.0;

            // Scale the editor container (true visual zoom, not font size change)
            ActiveScaleTransform.ScaleX = scale;
            ActiveScaleTransform.ScaleY = scale;

            // Compute container dimensions so the scaled content fills the viewport.
            // The ScaleTransform shrinks the visual area — compensate by expanding
            // the logical size so that (logical size * scale) == viewport size.
            double viewportWidth = EditorScrollViewer.ActualWidth;
            double viewportHeight = EditorScrollViewer.ActualHeight;
            if (viewportWidth > 0)
            {
                EditorContainer.Width = viewportWidth / scale;
            }
            if (viewportHeight > 0)
            {
                EditorContainer.MinHeight = viewportHeight / scale;
            }

            // Redraw rulers at the new scale
            if (_rulersVisible)
                RedrawRulers();
        }

        private void EditorScrollViewer_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            var props = e.GetCurrentPoint(EditorScrollViewer).Properties;
            var keyState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
            bool ctrlDown = (keyState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

            if (ctrlDown)
            {
                int delta = props.MouseWheelDelta;
                if (delta > 0)
                    ViewModel.ZoomIn();
                else if (delta < 0)
                    ViewModel.ZoomOut();
                ApplyZoom();
                e.Handled = true;
            }
        }

        private void SetAlignmentToggle(ToggleButton active)
        {
            AlignLeftToggle.IsChecked = (active == AlignLeftToggle);
            AlignCenterToggle.IsChecked = (active == AlignCenterToggle);
            AlignRightToggle.IsChecked = (active == AlignRightToggle);
            AlignJustifyToggle.IsChecked = (active == AlignJustifyToggle);
        }

        private void AlignLeft_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextParagraphFormat paragraphFormatting = selectedText.ParagraphFormat;
                paragraphFormatting.Alignment = ParagraphAlignment.Left;
                selectedText.ParagraphFormat = paragraphFormatting;
            }
            ViewModel.SetAlignment("Left");
            SetAlignmentToggle(AlignLeftToggle);
            _macro.Record(MacroCommandType.SetAlignment, "Left");
        }

        private void AlignCenter_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextParagraphFormat paragraphFormatting = selectedText.ParagraphFormat;
                paragraphFormatting.Alignment = ParagraphAlignment.Center;
                selectedText.ParagraphFormat = paragraphFormatting;
            }
            ViewModel.SetAlignment("Center");
            SetAlignmentToggle(AlignCenterToggle);
            _macro.Record(MacroCommandType.SetAlignment, "Center");
        }

        private void AlignRight_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextParagraphFormat paragraphFormatting = selectedText.ParagraphFormat;
                paragraphFormatting.Alignment = ParagraphAlignment.Right;
                selectedText.ParagraphFormat = paragraphFormatting;
            }
            ViewModel.SetAlignment("Right");
            SetAlignmentToggle(AlignRightToggle);
            _macro.Record(MacroCommandType.SetAlignment, "Right");
        }

        private void AlignJustify_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextParagraphFormat paragraphFormatting = selectedText.ParagraphFormat;
                paragraphFormatting.Alignment = ParagraphAlignment.Justify;
                selectedText.ParagraphFormat = paragraphFormatting;
            }
            ViewModel.SetAlignment("Justify");
            SetAlignmentToggle(AlignJustifyToggle);
            _macro.Record(MacroCommandType.SetAlignment, "Justify");
        }

        private void DecreaseIndent_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextParagraphFormat paragraphFormatting = selectedText.ParagraphFormat;
                if (paragraphFormatting.LeftIndent > 0)
                {
                    paragraphFormatting.SetIndents(paragraphFormatting.FirstLineIndent, paragraphFormatting.LeftIndent - 36, paragraphFormatting.RightIndent);
                }
            }
        }

        private void IncreaseIndent_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextParagraphFormat paragraphFormatting = selectedText.ParagraphFormat;
                paragraphFormatting.SetIndents(paragraphFormatting.FirstLineIndent, paragraphFormatting.LeftIndent + 36, paragraphFormatting.RightIndent);
            }
        }

        private void Bullets_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextParagraphFormat paragraphFormatting = selectedText.ParagraphFormat;
                if (paragraphFormatting.ListType == MarkerType.Bullet)
                {
                    paragraphFormatting.ListType = MarkerType.None;
                }
                else
                {
                    paragraphFormatting.ListType = MarkerType.Bullet;
                }
                selectedText.ParagraphFormat = paragraphFormatting;
            }
        }

        private void ApplyListType(MarkerType markerType, string listTypeName)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextParagraphFormat paragraphFormatting = selectedText.ParagraphFormat;
                paragraphFormatting.ListType = markerType;
                selectedText.ParagraphFormat = paragraphFormatting;
            }
            ViewModel.SetListType(listTypeName);
            _macro.Record(MacroCommandType.SetListType, listTypeName);
        }

        private void ListTypeNone_Click(object sender, RoutedEventArgs e) => ApplyListType(MarkerType.None, "None");
        private void ListTypeBullet_Click(object sender, RoutedEventArgs e) => ApplyListType(MarkerType.Bullet, "Bullet");
        private void ListTypeNumber_Click(object sender, RoutedEventArgs e) => ApplyListType(MarkerType.Arabic, "Number");
        private void ListTypeLowerLetter_Click(object sender, RoutedEventArgs e) => ApplyListType(MarkerType.LowercaseEnglishLetter, "LowercaseLetter");
        private void ListTypeUpperLetter_Click(object sender, RoutedEventArgs e) => ApplyListType(MarkerType.UppercaseEnglishLetter, "UppercaseLetter");
        private void ListTypeLowerRoman_Click(object sender, RoutedEventArgs e) => ApplyListType(MarkerType.LowercaseRoman, "LowercaseRoman");
        private void ListTypeUpperRoman_Click(object sender, RoutedEventArgs e) => ApplyListType(MarkerType.UppercaseRoman, "UppercaseRoman");

        private void LineSpacing_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string tagStr && double.TryParse(tagStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double spacing))
            {
                ITextSelection selectedText = Editor.Document.Selection;
                if (selectedText != null)
                {
                    ITextParagraphFormat paragraphFormatting = selectedText.ParagraphFormat;
                    if (spacing == 1.0)
                        paragraphFormatting.SetLineSpacing(LineSpacingRule.Single, 0);
                    else if (spacing == 1.5)
                        paragraphFormatting.SetLineSpacing(LineSpacingRule.OneAndHalf, 0);
                    else if (spacing == 2.0)
                        paragraphFormatting.SetLineSpacing(LineSpacingRule.Double, 0);
                    else
                        paragraphFormatting.SetLineSpacing(LineSpacingRule.Multiple, (float)spacing);
                    selectedText.ParagraphFormat = paragraphFormatting;
                }
                ViewModel.SetLineSpacing(spacing);
                _macro.Record(MacroCommandType.SetLineSpacing, spacing.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        private void TextColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            ApplyTextColor(args.NewColor);
        }

        private void TextColorSwatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                var color = ColorHelper.ParseHexColor(hex);
                ApplyTextColor(color);
                FontColorIndicator.Fill = new SolidColorBrush(color);
            }
        }

        private void TextColorMoreColors_Click(object sender, RoutedEventArgs e)
        {
            TextColorPicker.Visibility = TextColorPicker.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void ApplyTextColor(Color color)
        {
            _lastFontColor = color;
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                charFormatting.ForegroundColor = color;
                selectedText.CharacterFormat = charFormatting;
            }
        }

        private void ApplyLastFontColor_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ApplyTextColor(_lastFontColor);
            args.Handled = true;
        }

        private void HighlightColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            ApplyHighlightColor(args.NewColor);
        }

        private void HighlightSwatchButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                var color = ColorHelper.ParseHexColor(hex);
                ApplyHighlightColor(color);
                HighlightColorIndicator.Fill = new SolidColorBrush(color);
            }
        }

        private void ApplyHighlightColor(Color color)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                charFormatting.BackgroundColor = color;
                selectedText.CharacterFormat = charFormatting;
            }
        }

        private async void InsertPicture_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bmp");

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                using var randAccStream = await file.OpenAsync(FileAccessMode.Read);
                Editor.Document.Selection.InsertImage(0, 0, 0, VerticalCharacterAlignment.Baseline, file.Name, randAccStream);
            }
        }

        private async void InsertDateTime_Click(object sender, RoutedEventArgs e)
        {
            var now = DateTime.Now;
            var formats = new[]
            {
                now.ToString("g"),
                now.ToString("G"),
                now.ToString("f"),
                now.ToString("F"),
                now.ToString("d"),
                now.ToString("D"),
                now.ToString("t"),
                now.ToString("T"),
                now.ToString("yyyy-MM-dd"),
                now.ToString("yyyy-MM-dd HH:mm:ss"),
                now.ToString("MMMM dd, yyyy"),
                now.ToString("dddd, MMMM dd, yyyy")
            };

            var listBox = new ListView
            {
                ItemsSource = formats,
                SelectionMode = ListViewSelectionMode.Single,
                Height = 250,
                SelectedIndex = 0
            };

            var dialog = new ContentDialog
            {
                Title = Res.GetString("DateTimeTitle"),
                Content = listBox,
                PrimaryButtonText = Res.GetString("ButtonInsert"),
                CloseButtonText = Res.GetString("ButtonCancel"),
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && listBox.SelectedItem is string selected)
            {
                Editor.Document.Selection.Text = selected;
            }
        }

        private FindOptions GetFindOptions()
        {
            var options = FindOptions.None;
            if (FindMatchCaseCheckBox.IsChecked == true) options |= FindOptions.Case;
            if (FindWholeWordCheckBox.IsChecked == true) options |= FindOptions.Word;
            return options;
        }

        private RegexOptions GetRegexOptions()
        {
            var options = RegexOptions.None;
            if (FindMatchCaseCheckBox.IsChecked != true) options |= RegexOptions.IgnoreCase;
            return options;
        }

        private string GetFullDocumentText()
        {
            Editor.Document.GetText(TextGetOptions.None, out string text);
            return text;
        }

        private void FindNext_Click(object sender, RoutedEventArgs e)
        {
            string textToFind = FindTextBox.Text;
            if (string.IsNullOrEmpty(textToFind)) return;

            if (FindRegexCheckBox.IsChecked == true)
            {
                FindNextRegex(textToFind, forward: true);
            }
            else
            {
                int found = Editor.Document.Selection.FindText(textToFind, TextConstants.MaxUnitCount, GetFindOptions());
                if (found == 0)
                    ViewModel.UpdateStatus(Res.GetString("StatusNoMatch"));
            }
        }

        private void FindPrevious_Click(object sender, RoutedEventArgs e)
        {
            string textToFind = FindTextBox.Text;
            if (string.IsNullOrEmpty(textToFind)) return;

            if (FindRegexCheckBox.IsChecked == true)
            {
                FindNextRegex(textToFind, forward: false);
            }
            else
            {
                int found = Editor.Document.Selection.FindText(textToFind, -TextConstants.MaxUnitCount, GetFindOptions());
                if (found == 0)
                    ViewModel.UpdateStatus(Res.GetString("StatusNoMatch"));
            }
        }

        private void FindNextRegex(string pattern, bool forward)
        {
            try
            {
                var regex = new Regex(pattern, GetRegexOptions());
                string text = GetFullDocumentText();
                var matches = regex.Matches(text);
                if (matches.Count == 0)
                {
                    ViewModel.UpdateStatus(Res.GetString("StatusNoMatch"));
                    return;
                }

                int currentPos = forward ? Editor.Document.Selection.EndPosition : Editor.Document.Selection.StartPosition;
                Match? target = null;

                if (forward)
                {
                    foreach (Match m in matches)
                    {
                        if (m.Index >= currentPos) { target = m; break; }
                    }
                    target ??= matches[0];
                }
                else
                {
                    for (int i = matches.Count - 1; i >= 0; i--)
                    {
                        if (matches[i].Index < currentPos) { target = matches[i]; break; }
                    }
                    target ??= matches[^1];
                }

                Editor.Document.Selection.SetRange(target.Index, target.Index + target.Length);
            }
            catch (ArgumentException)
            {
                ViewModel.UpdateStatus(Res.GetString("StatusInvalidRegex"));
            }
        }

        private static readonly Color HighlightColor = Color.FromArgb(255, 255, 255, 0);
        private static readonly Color TransparentColor = Color.FromArgb(0, 255, 255, 255);

        private void HighlightAllMatches_Click(object sender, RoutedEventArgs e)
        {
            string textToFind = FindTextBox.Text;
            if (string.IsNullOrEmpty(textToFind)) return;

            int count = 0;

            // Save current selection
            int savedStart = Editor.Document.Selection.StartPosition;
            int savedEnd = Editor.Document.Selection.EndPosition;

            if (FindRegexCheckBox.IsChecked == true)
            {
                try
                {
                    var regex = new Regex(textToFind, GetRegexOptions());
                    string text = GetFullDocumentText();
                    foreach (Match m in regex.Matches(text))
                    {
                        Editor.Document.Selection.SetRange(m.Index, m.Index + m.Length);
                        Editor.Document.Selection.CharacterFormat.BackgroundColor = HighlightColor;
                        count++;
                    }
                }
                catch (ArgumentException)
                {
                    ViewModel.UpdateStatus(Res.GetString("StatusInvalidRegex"));
                    return;
                }
            }
            else
            {
                var options = GetFindOptions();
                Editor.Document.Selection.SetRange(0, 0);
                while (Editor.Document.Selection.FindText(textToFind, TextConstants.MaxUnitCount, options) > 0)
                {
                    Editor.Document.Selection.CharacterFormat.BackgroundColor = HighlightColor;
                    count++;
                }
            }

            // Restore selection
            Editor.Document.Selection.SetRange(savedStart, savedEnd);
            ViewModel.UpdateStatus(count > 0 ? Res.GetFormatted("StatusHighlighted", count) : Res.GetString("StatusNoMatches"));
        }

        private void ClearHighlights_Click(object sender, RoutedEventArgs e)
        {
            int savedStart = Editor.Document.Selection.StartPosition;
            int savedEnd = Editor.Document.Selection.EndPosition;

            Editor.Document.Selection.Expand(TextRangeUnit.Story);
            Editor.Document.Selection.CharacterFormat.BackgroundColor = TransparentColor;
            Editor.Document.Selection.SetRange(savedStart, savedEnd);
            ViewModel.UpdateStatus(Res.GetString("StatusHighlightsCleared"));
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            string textToFind = ReplaceFindTextBox.Text;
            string replaceWith = ReplaceWithTextBox.Text;
            if (string.IsNullOrEmpty(textToFind)) return;

            if (FindRegexCheckBox.IsChecked == true)
            {
                try
                {
                    var regex = new Regex(textToFind, GetRegexOptions());
                    string selectedText = Editor.Document.Selection.Text;
                    if (!string.IsNullOrEmpty(selectedText) && regex.IsMatch(selectedText))
                    {
                        Editor.Document.Selection.Text = regex.Replace(selectedText, replaceWith);
                    }
                    FindNextRegex(textToFind, forward: true);
                }
                catch (ArgumentException)
                {
                    ViewModel.UpdateStatus(Res.GetString("StatusInvalidRegex"));
                }
            }
            else
            {
                var options = GetFindOptions();
                if (Editor.Document.Selection.FindText(textToFind, 0, options) > 0
                    || Editor.Document.Selection.Text.Equals(textToFind,
                        options.HasFlag(FindOptions.Case) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                {
                    Editor.Document.Selection.Text = replaceWith;
                }
                Editor.Document.Selection.FindText(textToFind, TextConstants.MaxUnitCount, options);
            }
        }

        private void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            string textToFind = ReplaceFindTextBox.Text;
            string replaceWith = ReplaceWithTextBox.Text;
            if (string.IsNullOrEmpty(textToFind)) return;

            if (FindRegexCheckBox.IsChecked == true)
            {
                try
                {
                    var regex = new Regex(textToFind, GetRegexOptions());
                    string text = GetFullDocumentText();
                    int count = regex.Count(text);
                    string replaced = regex.Replace(text, replaceWith);
                    Editor.Document.SetText(TextSetOptions.None, replaced);
                    ViewModel.UpdateStatus(Res.GetFormatted("StatusReplaced", count));
                }
                catch (ArgumentException)
                {
                    ViewModel.UpdateStatus(Res.GetString("StatusInvalidRegex"));
                }
            }
            else
            {
                var options = GetFindOptions();
                int count = 0;
                Editor.Document.Selection.SetRange(0, 0);
                while (Editor.Document.Selection.FindText(textToFind, TextConstants.MaxUnitCount, options) > 0)
                {
                    Editor.Document.Selection.Text = replaceWith;
                    count++;
                }
                ViewModel.UpdateStatus(Res.GetFormatted("StatusReplaced", count));
            }
        }

        private void WordWrap_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem toggleItem)
            {
                Editor.TextWrapping = toggleItem.IsChecked ? TextWrapping.Wrap : TextWrapping.NoWrap;
                ViewModel.IsWordWrap = toggleItem.IsChecked;
            }
        }

        private void FocusMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem toggleItem)
            {
                var visibility = toggleItem.IsChecked ? Visibility.Collapsed : Visibility.Visible;
                RibbonBar.Visibility = visibility;
                StatusBar.Visibility = visibility;
                ViewModel.UpdateStatus(toggleItem.IsChecked ? Res.GetString("StatusFocusModeEnabled") : Res.GetString("StatusFocusModeDisabled"));
            }
        }

        private void Ruler_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem toggleItem)
            {
                _rulersVisible = toggleItem.IsChecked;
                UpdateRulerVisibility();
                if (_rulersVisible)
                    RedrawRulers();
                string state = _rulersVisible ? Res.GetString("StatusEnabled") : Res.GetString("StatusDisabled");
                ViewModel.UpdateStatus(Res.GetFormatted("StatusRulerToggled", state));
            }
        }

        private void UpdateRulerVisibility()
        {
            HorizontalRulerRow.Height = _rulersVisible ? new GridLength(26) : new GridLength(0);
            VerticalRulerColumn.Width = _rulersVisible ? new GridLength(26) : new GridLength(0);
        }

        private void HRulerCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_rulersVisible) DrawHorizontalRuler();
        }

        private void VRulerCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_rulersVisible) DrawVerticalRuler();
        }

        private void RedrawRulers()
        {
            DrawHorizontalRuler();
            DrawVerticalRuler();
        }

        private double GetPixelsPerUnit(out string unitLabel)
        {
            return RulerHelper.GetPixelsPerUnit(_settings.RulerUnits, ViewModel.ZoomLevel, out unitLabel);
        }

        private void DrawHorizontalRuler()
        {
            HRulerCanvas.Children.Clear();
            double width = HRulerCanvas.ActualWidth > 0 ? HRulerCanvas.ActualWidth : 1200;
            double pixelsPerUnit = GetPixelsPerUnit(out _);
            double canvasHeight = 24;
            var brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
            var lightBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray);

            int maxUnits = (int)(width / pixelsPerUnit) + 1;
            for (int i = 0; i <= maxUnits; i++)
            {
                double x = i * pixelsPerUnit;
                if (x > width) break;

                // Major tick
                HRulerCanvas.Children.Add(new Microsoft.UI.Xaml.Shapes.Line
                {
                    X1 = x, Y1 = canvasHeight - 10, X2 = x, Y2 = canvasHeight,
                    Stroke = brush, StrokeThickness = 1
                });

                // Label
                var label = new TextBlock { Text = i.ToString(), FontSize = 9, Foreground = brush };
                Canvas.SetLeft(label, x + 2);
                Canvas.SetTop(label, 1);
                HRulerCanvas.Children.Add(label);

                // Half tick
                double halfX = x + pixelsPerUnit / 2;
                if (halfX < width)
                {
                    HRulerCanvas.Children.Add(new Microsoft.UI.Xaml.Shapes.Line
                    {
                        X1 = halfX, Y1 = canvasHeight - 6, X2 = halfX, Y2 = canvasHeight,
                        Stroke = lightBrush, StrokeThickness = 1
                    });
                }

                // Quarter ticks
                for (int q = 1; q <= 3; q += 2)
                {
                    double qx = x + q * pixelsPerUnit / 4;
                    if (qx < width)
                    {
                        HRulerCanvas.Children.Add(new Microsoft.UI.Xaml.Shapes.Line
                        {
                            X1 = qx, Y1 = canvasHeight - 4, X2 = qx, Y2 = canvasHeight,
                            Stroke = lightBrush, StrokeThickness = 0.5
                        });
                    }
                }
            }
        }

        private void DrawVerticalRuler()
        {
            VRulerCanvas.Children.Clear();
            double height = VRulerCanvas.ActualHeight > 0 ? VRulerCanvas.ActualHeight : 800;
            double pixelsPerUnit = GetPixelsPerUnit(out _);
            double canvasWidth = 24;
            var brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
            var lightBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray);

            int maxUnits = (int)(height / pixelsPerUnit) + 1;
            for (int i = 0; i <= maxUnits; i++)
            {
                double y = i * pixelsPerUnit;
                if (y > height) break;

                // Major tick
                VRulerCanvas.Children.Add(new Microsoft.UI.Xaml.Shapes.Line
                {
                    X1 = canvasWidth - 10, Y1 = y, X2 = canvasWidth, Y2 = y,
                    Stroke = brush, StrokeThickness = 1
                });

                // Label (rotated for vertical)
                if (i > 0)
                {
                    var label = new TextBlock { Text = i.ToString(), FontSize = 9, Foreground = brush };
                    Canvas.SetLeft(label, 2);
                    Canvas.SetTop(label, y + 2);
                    VRulerCanvas.Children.Add(label);
                }

                // Half tick
                double halfY = y + pixelsPerUnit / 2;
                if (halfY < height)
                {
                    VRulerCanvas.Children.Add(new Microsoft.UI.Xaml.Shapes.Line
                    {
                        X1 = canvasWidth - 6, Y1 = halfY, X2 = canvasWidth, Y2 = halfY,
                        Stroke = lightBrush, StrokeThickness = 1
                    });
                }

                // Quarter ticks
                for (int q = 1; q <= 3; q += 2)
                {
                    double qy = y + q * pixelsPerUnit / 4;
                    if (qy < height)
                    {
                        VRulerCanvas.Children.Add(new Microsoft.UI.Xaml.Shapes.Line
                        {
                            X1 = canvasWidth - 4, Y1 = qy, X2 = canvasWidth, Y2 = qy,
                            Stroke = lightBrush, StrokeThickness = 0.5
                        });
                    }
                }
            }
        }

        // Page dimensions at 96 DPI: US Letter 8.5×11 = 816×1056px
        // 1-inch margins on each side ? printable area = 624px wide
        private const double PageWidthPx = 816;
        private const double PageHeightPx = 1056;
        private const double PageMarginPx = 96; // 1 inch each side
        private const double PrintableWidthPx = PageWidthPx - (PageMarginPx * 2); // 624

        private void PageView_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem toggleItem)
            {
                _pageViewActive = toggleItem.IsChecked;
                ApplyPageViewLayout();
                string state = _pageViewActive ? Res.GetString("StatusEnabled") : Res.GetString("StatusDisabled");
                ViewModel.UpdateStatus(Res.GetFormatted("StatusPageViewToggled", state));
            }
        }

        private void ApplyPageViewLayout()
        {
            if (_pageViewActive)
            {
                PageViewBorder.Visibility = Visibility.Visible;
                PageViewBorder.Width = PageWidthPx;
                PageViewBorder.MinHeight = PageHeightPx;
                Editor.HorizontalAlignment = HorizontalAlignment.Center;
                Editor.Width = PrintableWidthPx;
                Editor.MaxWidth = PrintableWidthPx;
                Editor.Margin = new Thickness(0, PageMarginPx, 0, PageMarginPx);
            }
            else
            {
                PageViewBorder.Visibility = Visibility.Collapsed;
                Editor.HorizontalAlignment = HorizontalAlignment.Stretch;
                Editor.Width = double.NaN;
                Editor.MaxWidth = double.PositiveInfinity;
                Editor.Margin = new Thickness(0);
            }
        }

        private void GrowFont_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selection = Editor.Document.Selection;
            if (selection != null)
            {
                float current = selection.CharacterFormat.Size;
                if (current is float.NaN or <= 0) current = 11f;
                selection.CharacterFormat.Size = current + 1f;
                FontSizeComboBox.Text = ((int)(current + 1f)).ToString();
            }
        }

        private void ShrinkFont_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selection = Editor.Document.Selection;
            if (selection != null)
            {
                float current = selection.CharacterFormat.Size;
                if (current is float.NaN or <= 1) current = 12f;
                float next = Math.Max(1f, current - 1f);
                selection.CharacterFormat.Size = next;
                FontSizeComboBox.Text = ((int)next).ToString();
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            Editor.Document.Undo();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            Editor.Document.Redo();
        }

        private async void PaintDrawing_Click(object sender, RoutedEventArgs e)
        {
            if (!IsSmrtDoodleInstalled())
            {
                var notInstalledDialog = new ContentDialog
                {
                    Title = Res.GetString("SmrtDoodleNotFound"),
                    Content = Res.GetString("SmrtDoodleNotFoundMessage"),
                    PrimaryButtonText = Res.GetString("SmrtDoodleGetFromStore"),
                    CloseButtonText = Res.GetString("DlgOK"),
                    XamlRoot = Content.XamlRoot
                };
                var notInstalledResult = await notInstalledDialog.ShowAsync();
                if (notInstalledResult == ContentDialogResult.Primary)
                    await Launcher.LaunchUriAsync(new Uri("ms-windows-store://search/?query=SmrtDoodle"));
                return;
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "SmrtPad");
            Directory.CreateDirectory(tempDir);
            string tempFile = Path.Combine(tempDir, $"drawing_{DateTime.Now:yyyyMMdd_HHmmss}.png");

            try
            {
                var process = new Process();
                process.StartInfo.FileName = "SmrtDoodle.exe";
                process.StartInfo.Arguments = $"\"{tempFile}\"";
                process.StartInfo.UseShellExecute = true;
                process.Start();
                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode == 0 && File.Exists(tempFile))
                {
                    var file = await StorageFile.GetFileFromPathAsync(tempFile);
                    using (var stream = await file.OpenAsync(FileAccessMode.Read))
                    {
                        Editor.Document.Selection.InsertImage(0, 0, 0, VerticalCharacterAlignment.Baseline, file.Name, stream);
                    }
                    ViewModel.UpdateStatus(Res.GetString("StatusDrawingInserted"));
                }
                else if (process.ExitCode != 0)
                {
                    ViewModel.UpdateStatus(Res.GetString("StatusDrawingCancelled"));
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                ViewModel.UpdateStatus(Res.GetString("StatusDrawingCancelled"));
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        private static bool IsSmrtDoodleInstalled()
        {
            var windowsAppsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WindowsApps", "SmrtDoodle.exe");
            if (File.Exists(windowsAppsPath)) return true;

            var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? [];
            return pathDirs.Any(dir => !string.IsNullOrWhiteSpace(dir) &&
                File.Exists(Path.Combine(dir.Trim(), "SmrtDoodle.exe")));
        }

        private async void InsertObject_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".bmp");
                picker.FileTypeFilter.Add(".gif");
                picker.FileTypeFilter.Add(".tif");
                picker.FileTypeFilter.Add(".tiff");
                picker.FileTypeFilter.Add(".ico");
                picker.FileTypeFilter.Add(".svg");

                StorageFile file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    string ext = file.FileType.ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".ico")
                    {
                        using (var stream = await file.OpenAsync(FileAccessMode.Read))
                        {
                            Editor.Document.Selection.InsertImage(0, 0, 0, VerticalCharacterAlignment.Baseline, file.Name, stream);
                        }
                        ViewModel.UpdateStatus(Res.GetFormatted("StatusInserted", file.Name));
                    }
                    else
                    {
                        Editor.Document.Selection.Text = Res.GetFormatted("EmbeddedObject", file.Name);
                        ViewModel.UpdateStatus(Res.GetFormatted("StatusInsertedReference", file.Name));
                    }
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorInsertingObject"), ex.Message);
            }
        }

        private async Task ShowErrorDialogAsync(string title, string message)
        {
            await _dialogService.ShowErrorAsync(title, message);
        }

        private void PasteSpecial_Click(object sender, RoutedEventArgs e)
        {
            var dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                PasteAsPlainTextAsync(dataPackageView);
            }
        }

        private async void PasteAsPlainTextAsync(DataPackageView dataPackageView)
        {
            try
            {
                string text = await dataPackageView.GetTextAsync();
                Editor.Document.Selection.Text = text;
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorPaste"), ex.Message);
            }
        }

        private void ClearFormatting_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                charFormatting.Bold = FormatEffect.Off;
                charFormatting.Italic = FormatEffect.Off;
                charFormatting.Underline = UnderlineType.None;
                charFormatting.Strikethrough = FormatEffect.Off;
                charFormatting.Subscript = FormatEffect.Off;
                charFormatting.Superscript = FormatEffect.Off;
                charFormatting.Name = _settings.DefaultFontFamily;
                charFormatting.Size = (float)_settings.DefaultFontSize;
                charFormatting.ForegroundColor = Color.FromArgb(255, 0, 0, 0);
                charFormatting.BackgroundColor = Color.FromArgb(0, 255, 255, 255);
                selectedText.CharacterFormat = charFormatting;

                ITextParagraphFormat paraFormatting = selectedText.ParagraphFormat;
                paraFormatting.Alignment = ParagraphAlignment.Left;
                paraFormatting.ListType = MarkerType.None;
                paraFormatting.SetLineSpacing(LineSpacingRule.Single, 0);
                paraFormatting.SetIndents(0, 0, 0);
                paraFormatting.SpaceBefore = 0;
                paraFormatting.SpaceAfter = 0;
                selectedText.ParagraphFormat = paraFormatting;

                ViewModel.UpdateStatus(Res.GetString("StatusFormattingCleared"));
            }
            _macro.Record(MacroCommandType.ClearFormatting);
        }

        private async void CustomLineSpacing_Click(object sender, RoutedEventArgs e)
        {
            var spacingBox = new NumberBox
            {
                Header = Res.GetString("LineSpacingHeader"),
                Minimum = 0.5,
                Maximum = 10.0,
                Value = ViewModel.LineSpacing,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                SmallChange = 0.25
            };

            var dialog = new ContentDialog
            {
                Title = Res.GetString("LineSpacingTitle"),
                Content = spacingBox,
                PrimaryButtonText = Res.GetString("ButtonApply"),
                CloseButtonText = Res.GetString("ButtonCancel"),
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                double spacing = spacingBox.Value;
                ITextSelection selectedText = Editor.Document.Selection;
                if (selectedText != null)
                {
                    ITextParagraphFormat paragraphFormatting = selectedText.ParagraphFormat;
                    paragraphFormatting.SetLineSpacing(LineSpacingRule.Multiple, (float)spacing);
                    selectedText.ParagraphFormat = paragraphFormatting;
                }
                ViewModel.SetLineSpacing(spacing);
                _macro.Record(MacroCommandType.SetLineSpacing, spacing.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        private void ApplyParagraphSpacing_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextParagraphFormat paragraphFormatting = selectedText.ParagraphFormat;
                paragraphFormatting.SpaceBefore = (float)SpacingBeforeBox.Value;
                paragraphFormatting.SpaceAfter = (float)SpacingAfterBox.Value;
                selectedText.ParagraphFormat = paragraphFormatting;
                ViewModel.ParagraphSpacingBefore = SpacingBeforeBox.Value;
                ViewModel.ParagraphSpacingAfter = SpacingAfterBox.Value;
                ViewModel.UpdateStatus(Res.GetFormatted("StatusParagraphSpacing", SpacingBeforeBox.Value, SpacingAfterBox.Value));
            }
        }

        private async void TabStops_Click(object sender, RoutedEventArgs e)
        {
            var positionBox = new NumberBox
            {
                Header = Res.GetString("TabStopPosition"),
                Minimum = 0.1,
                Maximum = 22.0,
                Value = 0.5,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                SmallChange = 0.25
            };

            var alignmentCombo = new ComboBox
            {
                Header = Res.GetString("TabStopAlignment"),
                ItemsSource = new string[] { Res.GetString("TabAlignLeft"), Res.GetString("TabAlignCenter"), Res.GetString("TabAlignRight"), Res.GetString("TabAlignDecimal") },
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var leaderCombo = new ComboBox
            {
                Header = Res.GetString("TabStopLeader"),
                ItemsSource = new string[] { Res.GetString("TabLeaderNone"), Res.GetString("TabLeaderDots"), Res.GetString("TabLeaderDashes"), Res.GetString("TabLeaderLines") },
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var tabListBox = new ListBox { MaxHeight = 120, MinHeight = 60, HorizontalAlignment = HorizontalAlignment.Stretch };
            RefreshTabStopList(tabListBox);

            var addButton = new Button { Content = Res.GetString("TabStopAdd"), HorizontalAlignment = HorizontalAlignment.Right };
            addButton.Click += (s, args) =>
            {
                float positionPts = (float)(positionBox.Value * 72.0);
                var align = alignmentCombo.SelectedIndex switch
                {
                    1 => TabAlignment.Center,
                    2 => TabAlignment.Right,
                    3 => TabAlignment.Decimal,
                    _ => TabAlignment.Left
                };
                var leader = leaderCombo.SelectedIndex switch
                {
                    1 => TabLeader.Dots,
                    2 => TabLeader.Dashes,
                    3 => TabLeader.Lines,
                    _ => TabLeader.Spaces
                };

                ITextSelection sel = Editor.Document.Selection;
                if (sel != null)
                {
                    ITextParagraphFormat pf = sel.ParagraphFormat;
                    pf.AddTab(positionPts, align, leader);
                    sel.ParagraphFormat = pf;
                }
                RefreshTabStopList(tabListBox);
            };

            var clearButton = new Button { Content = Res.GetString("TabStopClearAll"), HorizontalAlignment = HorizontalAlignment.Right };
            clearButton.Click += (s, args) =>
            {
                ITextSelection sel = Editor.Document.Selection;
                if (sel != null)
                {
                    ITextParagraphFormat pf = sel.ParagraphFormat;
                    pf.ClearAllTabs();
                    sel.ParagraphFormat = pf;
                }
                RefreshTabStopList(tabListBox);
            };

            var panel = new StackPanel { Spacing = 8, MinWidth = 280 };
            panel.Children.Add(positionBox);
            panel.Children.Add(alignmentCombo);
            panel.Children.Add(leaderCombo);
            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 6 };
            buttonRow.Children.Add(addButton);
            buttonRow.Children.Add(clearButton);
            panel.Children.Add(buttonRow);
            panel.Children.Add(new TextBlock { Text = Res.GetString("TabStopCurrent"), FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            panel.Children.Add(tabListBox);

            var dialog = new ContentDialog
            {
                Title = Res.GetString("TabStopTitle"),
                Content = panel,
                CloseButtonText = Res.GetString("DlgOK"),
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();
            ViewModel.UpdateStatus(Res.GetString("StatusTabStopsUpdated"));
        }

        private void RefreshTabStopList(ListBox listBox)
        {
            listBox.Items.Clear();
            ITextSelection sel = Editor.Document.Selection;
            if (sel == null) return;

            ITextParagraphFormat pf = sel.ParagraphFormat;
            for (int i = 0; i < pf.TabCount; i++)
            {
                pf.GetTab(i, out float pos, out TabAlignment align, out TabLeader leader);
                double inches = pos / 72.0;
                listBox.Items.Add($"{inches:F2}\" — {align} — {leader}");
            }

            if (pf.TabCount == 0)
                listBox.Items.Add(Res.GetString("TabStopNone"));
        }

        private void ApplyParagraphStyle(string fontName, float fontSize, bool bold, bool italic, ParagraphAlignment alignment, float spaceBefore, float spaceAfter)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText == null) return;

            ITextCharacterFormat cf = selectedText.CharacterFormat;
            cf.Name = fontName;
            cf.Size = fontSize;
            cf.Bold = bold ? FormatEffect.On : FormatEffect.Off;
            cf.Italic = italic ? FormatEffect.On : FormatEffect.Off;
            cf.Underline = UnderlineType.None;
            cf.Strikethrough = FormatEffect.Off;
            selectedText.CharacterFormat = cf;

            ITextParagraphFormat pf = selectedText.ParagraphFormat;
            pf.Alignment = alignment;
            pf.SpaceBefore = spaceBefore;
            pf.SpaceAfter = spaceAfter;
            selectedText.ParagraphFormat = pf;
        }

        private void StyleNormal_Click(object sender, RoutedEventArgs e)
        {
            var s = ParagraphStyleHelper.Normal;
            ApplyParagraphStyle(s.FontName, s.FontSize, s.Bold, s.Italic, ParagraphAlignment.Left, s.SpaceBefore, s.SpaceAfter);
            ViewModel.UpdateStatus(Res.GetString("StatusStyleApplied"));
        }

        private void StyleHeading1_Click(object sender, RoutedEventArgs e)
        {
            var s = ParagraphStyleHelper.Heading1;
            ApplyParagraphStyle(s.FontName, s.FontSize, s.Bold, s.Italic, ParagraphAlignment.Left, s.SpaceBefore, s.SpaceAfter);
            ViewModel.UpdateStatus(Res.GetString("StatusStyleApplied"));
        }

        private void StyleHeading2_Click(object sender, RoutedEventArgs e)
        {
            var s = ParagraphStyleHelper.Heading2;
            ApplyParagraphStyle(s.FontName, s.FontSize, s.Bold, s.Italic, ParagraphAlignment.Left, s.SpaceBefore, s.SpaceAfter);
            ViewModel.UpdateStatus(Res.GetString("StatusStyleApplied"));
        }

        private void StyleHeading3_Click(object sender, RoutedEventArgs e)
        {
            var s = ParagraphStyleHelper.Heading3;
            ApplyParagraphStyle(s.FontName, s.FontSize, s.Bold, s.Italic, ParagraphAlignment.Left, s.SpaceBefore, s.SpaceAfter);
            ViewModel.UpdateStatus(Res.GetString("StatusStyleApplied"));
        }

        private void StyleSubtitle_Click(object sender, RoutedEventArgs e)
        {
            var s = ParagraphStyleHelper.Subtitle;
            ApplyParagraphStyle(s.FontName, s.FontSize, s.Bold, s.Italic, ParagraphAlignment.Left, s.SpaceBefore, s.SpaceAfter);
            ViewModel.UpdateStatus(Res.GetString("StatusStyleApplied"));
        }

        private void StyleQuote_Click(object sender, RoutedEventArgs e)
        {
            var s = ParagraphStyleHelper.Quote;
            ApplyParagraphStyle(s.FontName, s.FontSize, s.Bold, s.Italic, ParagraphAlignment.Left, s.SpaceBefore, s.SpaceAfter);
            ViewModel.UpdateStatus(Res.GetString("StatusStyleApplied"));
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (Content is FrameworkElement root)
            {
                string newTheme = root.RequestedTheme switch
                {
                    ElementTheme.Light => "Dark",
                    ElementTheme.Dark => "Default",
                    _ => "Light"
                };
                _settings.ThemePreference = newTheme == "Default" ? "System" : newTheme;
                _settings.Save();
                ApplyThemeFromSettings();
                ViewModel.UpdateStatus(Res.GetFormatted("StatusTheme", _settings.ThemePreference));
            }
        }

        private async void InsertHyperlink_Click(object sender, RoutedEventArgs e)
        {
            var panel = new StackPanel { Spacing = 8, MinWidth = 300 };
            var urlBox = new TextBox { Header = Res.GetString("HyperlinkUrlHeader"), PlaceholderText = "https://example.com" };
            var textBox = new TextBox { Header = Res.GetString("HyperlinkDisplayHeader"), PlaceholderText = "" };

            string selectedText = Editor.Document.Selection.Text;
            if (!string.IsNullOrEmpty(selectedText))
                textBox.Text = selectedText;

            panel.Children.Add(urlBox);
            panel.Children.Add(textBox);

            var dialog = new ContentDialog
            {
                Title = Res.GetString("HyperlinkTitle"),
                Content = panel,
                PrimaryButtonText = Res.GetString("ButtonInsert"),
                CloseButtonText = Res.GetString("ButtonCancel"),
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(urlBox.Text))
            {
                string display = string.IsNullOrWhiteSpace(textBox.Text) ? urlBox.Text : textBox.Text;
                string url = urlBox.Text.Trim();

                Editor.Document.Selection.Text = display;
                int end = Editor.Document.Selection.EndPosition;
                int start = end - display.Length;
                ITextRange range = Editor.Document.GetRange(start, end);
                range.Link = $"\"{url}\"";
                range.CharacterFormat.ForegroundColor = Color.FromArgb(255, 0, 102, 204);
                range.CharacterFormat.Underline = UnderlineType.Single;
                ViewModel.UpdateStatus(Res.GetString("StatusHyperlinkInserted"));
            }
        }

        private async void InsertTable_Click(object sender, RoutedEventArgs e)
        {
            var panel = new StackPanel { Spacing = 8, MinWidth = 250 };
            var rowsBox = new NumberBox { Header = Res.GetString("TableRows"), Minimum = 1, Maximum = 50, Value = 3, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            var colsBox = new NumberBox { Header = Res.GetString("TableColumns"), Minimum = 1, Maximum = 20, Value = 3, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            panel.Children.Add(rowsBox);
            panel.Children.Add(colsBox);

            var dialog = new ContentDialog
            {
                Title = Res.GetString("TableTitle"),
                Content = panel,
                PrimaryButtonText = Res.GetString("ButtonInsert"),
                CloseButtonText = Res.GetString("ButtonCancel"),
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                int rows = (int)rowsBox.Value;
                int cols = (int)colsBox.Value;

                string rtf = RtfHelper.GenerateTable(rows, cols);

                Editor.Document.Selection.SetText(TextSetOptions.FormatRtf, rtf);
                ViewModel.UpdateStatus(Res.GetFormatted("StatusInsertedTable", rows, cols));
            }
        }

        private async void InsertSymbol_Click(object sender, RoutedEventArgs e)
        {
            var symbols = new[]
            {
                "©", "®", "™", "°", "±", "µ", "¶", "·", "÷", "×",
                "€", "£", "¥", "¢", "§", "†", "‡", "•", "…", "‰",
                "?", "?", "?", "?", "?", "?", "?", "?", "?", "?",
                "?", "?", "?", "?", "?", "?", "?", "?", "?", "?",
                "?", "?", "?", "?", "?", "?", "?", "?", "?", "?",
                "¼", "½", "¾", "?", "?", "—", "–", "«", "»", "¿",
            };

            var grid = new GridView
            {
                ItemsSource = symbols,
                SelectionMode = ListViewSelectionMode.Single,
                MaxHeight = 280,
                IsItemClickEnabled = true,
            };

            string? selectedSymbol = null;
            grid.ItemClick += (s, args) => { selectedSymbol = args.ClickedItem as string; };

            var dialog = new ContentDialog
            {
                Title = Res.GetString("SymbolTitle"),
                Content = grid,
                PrimaryButtonText = Res.GetString("ButtonInsert"),
                CloseButtonText = Res.GetString("ButtonCancel"),
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && selectedSymbol != null)
            {
                Editor.Document.Selection.Text = selectedSymbol;
                ViewModel.UpdateStatus(Res.GetFormatted("StatusInsertedSymbol", selectedSymbol));
            }
        }

        private void Editor_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = Res.GetString("DragDropCaption");
            }
        }

        private async void Editor_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0 && items[0] is StorageFile file)
                {
                    string ext = file.FileType.ToLowerInvariant();
                    if (ext is ".rtf" or ".txt" or ".docx" or ".htm" or ".html" or ".odt")
                    {
                        await OpenFileByPathAsync(file.Path);
                    }
                    else if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif")
                    {
                        try
                        {
                            using (var stream = await file.OpenAsync(FileAccessMode.Read))
                            {
                                Editor.Document.Selection.InsertImage(0, 0, 0, VerticalCharacterAlignment.Baseline, file.Name, stream);
                            }
                            ViewModel.UpdateStatus(Res.GetFormatted("StatusInserted", file.Name));
                        }
                        catch (Exception ex)
                        {
                            await ShowErrorDialogAsync(Res.GetString("ErrorGeneric"), ex.Message);
                        }
                    }
                }
            }
        }
    }

    // ── DocumentTab — per-tab document state + UI ──────────────────────────────

    internal sealed class DocumentTab
    {
        public TabViewItem TabViewItem { get; }
        public RichEditBox Editor { get; }
        public ScrollViewer ScrollViewer { get; }
        public Grid EditorContainer { get; }
        public Border PageViewBorder { get; }
        public ScaleTransform ScaleTransform { get; } = new ScaleTransform();

        public StorageFile? CurrentFile { get; set; }
        public bool IsModified { get; set; }
        public string Encoding { get; set; } = "UTF-8";
        public double ZoomLevel { get; set; } = 100.0;

        public DocumentTab(string title, ISettingsService settings)
        {
            Editor = new RichEditBox
            {
                AcceptsReturn = true,
                TextWrapping = settings.DefaultWordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                AllowDrop = true,
                IsSpellCheckEnabled = settings.SpellCheckEnabled,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                MinHeight = 200,
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(Editor, "Editor");

            PageViewBorder = new Border
            {
                Visibility = Visibility.Collapsed,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 255, 255, 255)),
                BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 200, 200, 200)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                MinHeight = 1056,
            };

            EditorContainer = new Grid 
            { 
                Margin = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            EditorContainer.Children.Add(PageViewBorder);
            EditorContainer.Children.Add(Editor);
            EditorContainer.RenderTransform = ScaleTransform;
            EditorContainer.RenderTransformOrigin = new Windows.Foundation.Point(0, 0);

            ScrollViewer = new ScrollViewer
            {
                Content = EditorContainer,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };

            TabViewItem = new TabViewItem
            {
                Header = title,
                IsClosable = true,
                Content = ScrollViewer,
            };
        }
    }
}

