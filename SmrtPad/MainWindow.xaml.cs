using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Printing;
using System.Numerics;
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
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using WinUIPointerPoint = Microsoft.UI.Input.PointerPoint;
using WinRT.Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using SmrtPad.Helpers;
using SmrtPad.Models;
using SmrtPad.ViewModels;
using SmrtPad.Views;
using SmrtPad.Services;
using Res = SmrtPad.Helpers.ResourceHelper;
using AutomationPeer = Microsoft.UI.Xaml.Automation.AutomationProperties;
using Path = System.IO.Path;

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
        private readonly IInkService _inkService;
        private readonly ISessionRestoreService _sessionRestoreService;
        private DispatcherTimer? _autoSaveTimer;
        private DispatcherTimer? _sessionSaveTimer;
        private System.ComponentModel.PropertyChangedEventHandler? _docTitleHandler;
        private PrintDocument? _printDocument;
        private IPrintDocumentSource? _printDocumentSource;
        private readonly List<UIElement> _printPreviewPages = [];
        private bool _rulersVisible;
        private bool _pageViewActive;
        private Color _lastFontColor = Color.FromArgb(255, 0xE8, 0x11, 0x23);
        private bool _fontDropdownStyled;
        private bool _fontsInitialized;
        private bool _printingRegistered;
        private const double Dpi = 96.0;
        private static readonly IReadOnlyDictionary<string, (double WidthIn, double HeightIn)> s_paperSizes =
            new Dictionary<string, (double WidthIn, double HeightIn)>(StringComparer.OrdinalIgnoreCase)
            {
                ["A4"] = (8.27, 11.69),
                ["Letter"] = (8.5, 11.0),
                ["Legal"] = (8.5, 14.0)
            };

        // ?? Tab management ??????????????????????????????????????????????????????
        private readonly List<DocumentTab> _tabs = [];
        private int _activeTabIndex = -1;
        private bool _suppressTabModified;
        private readonly MacroHelper _macro = new();
        private int _nextTabId = 1;

        /// <summary>
        /// Defers un-suppression of <c>_suppressTabModified</c> using a short timer
        /// so the flag stays raised through any asynchronous <c>TextChanged</c> events
        /// the <see cref="RichEditBox"/> fires during its initial layout pass.
        /// Dispatcher-queue deferral alone is insufficient because the Win32 RichEdit
        /// control queues character-format messages across multiple dispatcher cycles.
        /// </summary>
        private void DeferResetTabModified()
        {
            var tabIdx = _activeTabIndex;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                if (tabIdx >= 0 && tabIdx < _tabs.Count)
                {
                    _tabs[tabIdx].IsModified = false;
                    ViewModel.IsModified = false;
                }
                _suppressTabModified = false;
            };
            timer.Start();
        }

        private static readonly char[] s_wordSeparators = [' ', '\r', '\n', '\t'];
        private bool _suppressFontComboChange;
        private bool HasActiveTab => _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count;
        private DocumentTab ActiveTab => _tabs[_activeTabIndex];
        private RichEditBox Editor => ActiveTab.Editor;
        private ScrollViewer EditorScrollViewer => ActiveTab.ScrollViewer;
        private Grid EditorContainer => ActiveTab.EditorContainer;
        private Border PageViewBorder => ActiveTab.PageViewBorder;
        private ScaleTransform EditorScaleTransform => ActiveTab.EditorScaleTransform;
        private Canvas InkOverlay => ActiveTab.InkOverlay;
        public EditorViewModel ViewModel { get; }

        public MainWindow()
        {
            _settings = App.Current.Services.GetRequiredService<ISettingsService>();
            _dialogService = App.Current.Services.GetRequiredService<IDialogService>();
            _fileService = App.Current.Services.GetRequiredService<IFileService>();
            _inkService = App.Current.Services.GetRequiredService<IInkService>();
            _sessionRestoreService = App.Current.Services.GetRequiredService<ISessionRestoreService>();
            ViewModel = App.Current.Services.GetRequiredService<EditorViewModel>();
            InitializeComponent();
            Title = Res.GetFormatted("AppTitle", ViewModel.DocumentTitle);
            _docTitleHandler = (s, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.DocumentTitle))
                    Title = Res.GetFormatted("AppTitle", ViewModel.DocumentTitle);
            };
            ViewModel.PropertyChanged += _docTitleHandler;

            // Create the first document tab before ApplySettings() so Editor is valid
            _suppressTabModified = true;
            CreateTab(Res.GetString("DocumentUntitled"));
            ActiveTab.IsModified = false;
            DeferResetTabModified();

            ApplySettings();
            SetupAutoSave();
            SetupSessionPersistence();
            ScheduleDeferredStartupWork();

            // Clean up on window close: stop auto-save timer and unsubscribe from ViewModel events
            Closed += (_, _) =>
            {
                _autoSaveTimer?.Stop();
                _sessionSaveTimer?.Stop();
                ViewModel.PropertyChanged -= _docTitleHandler;
            };

            FileBackstage.NewRequested += (s, e) => { HideBackstage(); New_Click(this, new RoutedEventArgs()); };
            FileBackstage.OpenRequested += (s, e) => { HideBackstage(); Open_Click(this, new RoutedEventArgs()); };
            FileBackstage.SaveRequested += (s, e) => { HideBackstage(); Save_Click(this, new RoutedEventArgs()); };
            FileBackstage.SaveAsRequested += (s, e) => { HideBackstage(); SaveAs_Click(this, new RoutedEventArgs()); };
            FileBackstage.PrintRequested += (s, e) => { HideBackstage(); Print_Click(this, new RoutedEventArgs()); };
            FileBackstage.ExportPdfRequested += (s, e) => { HideBackstage(); ExportPdf_Click(this, new RoutedEventArgs()); };
            FileBackstage.ExportDocxRequested += (s, e) => { HideBackstage(); ExportDocx_Click(this, new RoutedEventArgs()); };
            FileBackstage.OneDriveRequested  += (s, e)    => { HideBackstage(); SaveToOneDrive_Click(this, new RoutedEventArgs()); };
            FileBackstage.SendEmailRequested += (s, e)    => { HideBackstage(); SendEmail_Click(this, new RoutedEventArgs()); };
            FileBackstage.OptionsRequested   += (s, e)    => { HideBackstage(); Options_Click(this, new RoutedEventArgs()); };
            FileBackstage.ExitRequested      += async (s, e) => { if (await PromptSaveAllTabsAsync()) Close(); };
            FileBackstage.RecentFileRequested += async (s, path) => { HideBackstage(); await OpenFileByPathAsync(path); };
            FileBackstage.TemplateRequested  += (s, template) => { HideBackstage(); ApplyTemplate(template); };

            RegisterForPrinting();

            // Intercept the window close button (X) to prompt for unsaved changes
            AppWindow.Closing += AppWindow_Closing;
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "SmrtPad.ico");
            if (File.Exists(iconPath))
                AppWindow.SetIcon(iconPath);
        }

        private async void AppWindow_Closing(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowClosingEventArgs args)
        {
            // Cancel close so we can show async dialogs if any tab has unsaved changes
            if (_tabs.Any(t => t.IsModified))
            {
                args.Cancel = true;

                if (await PromptSaveAllTabsAsync())
                {
                    // All tabs resolved — close for real.
                    AppWindow.Closing -= AppWindow_Closing;
                    Close();
                }
                // else: user cancelled on one of the tabs — window stays open
            }
        }

        private void SetupSessionPersistence()
        {
            _sessionSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _sessionSaveTimer.Tick += async (_, _) =>
            {
                try
                {
                    await PersistSessionSnapshotAsync();
                }
                catch (IOException ex)
                {
                    Debug.WriteLine($"Session snapshot failed: {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    Debug.WriteLine($"Session snapshot access denied: {ex.Message}");
                }
            };
            _sessionSaveTimer.Start();
        }

        private void ScheduleDeferredStartupWork()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                InitializeFonts();
                RegisterForPrinting();
                ApplyToolbarAutomationNames();
            });
        }

        internal async Task RestoreSessionAsync(IReadOnlyList<SessionTabState> tabs)
        {
            ArgumentNullException.ThrowIfNull(tabs);

            if (tabs.Count == 0)
                return;

            ResetTabsForSessionRestore();

            foreach (var state in tabs)
            {
                CreateTab(string.IsNullOrWhiteSpace(state.Title) ? Res.GetString("DocumentUntitled") : state.Title);
                await RestoreTabStateAsync(state);
            }

            DocumentTabs.SelectedIndex = 0;
            _activeTabIndex = 0;
            SyncViewModelFromActiveTab();
        }

        /// <summary>
        /// Iterates through all tabs that have unsaved changes, switches to each one,
        /// and prompts the user to save individually. Returns <c>true</c> if all tabs
        /// were resolved (saved or discarded); <c>false</c> if the user cancelled on any tab.
        /// </summary>
        private async Task<bool> PromptSaveAllTabsAsync()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (!_tabs[i].IsModified) continue;

                // Switch to the tab so the user sees which document is being asked about
                _activeTabIndex = i;
                DocumentTabs.SelectedIndex = i;
                SyncViewModelFromActiveTab();

                var result = await _dialogService.ShowSavePromptAsync(
                    _tabs[i].CurrentFile?.Name ?? _tabs[i].TabViewItem.Header as string ?? Res.GetString("DocumentUntitled"));

                if (result == SavePromptResult.Save)
                {
                    Save_Click(this, new RoutedEventArgs());
                }
                else if (result == SavePromptResult.Cancel)
                {
                    return false;
                }
                // SavePromptResult.DontSave — continue to the next tab
            }
            return true;
        }

        // ?? Tab management ???????????????????????????????????????????????????????

        private DocumentTab CreateTab(string title)
        {
            var tab = new DocumentTab(title, _settings)
            {
                Id = _nextTabId++,
            };

            tab.Editor.TextChanged += (s, e) =>
            {
                if (_suppressTabModified) return;
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
            // Refresh formatting toggles after keyboard shortcuts (e.g. Ctrl+B/I/U)
            // because the Win32 RichEdit handles these natively without firing SelectionChanged.
            tab.Editor.KeyUp += (s, e) =>
            {
                if (_activeTabIndex >= 0 && tab == ActiveTab)
                    Editor_SelectionChanged(s, new RoutedEventArgs());
            };
            // Ctrl+Alt+V = Paste as plain text (strips rich formatting).
            // Handled via KeyDown because KeyboardAccelerator does not support the Menu (Alt)
            // modifier combination on WinUI 3.
            tab.Editor.KeyDown += async (s, e) =>
            {
                if (e.Key == Windows.System.VirtualKey.V && tab == ActiveTab)
                {
                    var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
                    var altState  = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);
                    if ((ctrlState & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0 &&
                        (altState  & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
                    {
                        e.Handled = true;
                        await PasteAsPlainTextAsync();
                    }
                }
            };
            tab.Editor.DragOver += Editor_DragOver;
            tab.Editor.Drop += Editor_Drop;
            tab.ScrollViewer.PointerWheelChanged += EditorScrollViewer_PointerWheelChanged;
            tab.InkOverlay.PointerPressed += InkOverlay_PointerPressed;
            tab.InkOverlay.PointerMoved += InkOverlay_PointerMoved;
            tab.InkOverlay.PointerReleased += InkOverlay_PointerReleased;
            tab.InkOverlay.PointerCanceled += InkOverlay_PointerCanceled;
            // When the OS system theme changes (e.g. Windows auto dark/light mode),
            // re-normalise text colors so the active document stays readable.
            tab.EditorContainer.ActualThemeChanged += (_, _) =>
            {
                if (tab == ActiveTab) NormalizeDocumentColorsForTheme();
            };

            DocumentTabs.TabItems.Add(tab.TabViewItem);
            _tabs.Add(tab);
            DocumentTabs.SelectedIndex = _tabs.Count - 1;
            _activeTabIndex = _tabs.Count - 1;
            RefreshEmptyState();
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
            _suppressTabModified = true;
            CreateTab(Res.GetString("DocumentUntitled"));
            ViewModel.NewDocument();
            ActiveTab.IsModified = false;
            UpdateEncoding("UTF-8");
            ViewModel.UpdateStatus(Res.GetString("StatusNewTab"));
            DeferResetTabModified();
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

        private void NewDocument_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            New_Click(this, new RoutedEventArgs());
            args.Handled = true;
        }

        private void OpenFind_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            FindButton.Flyout?.ShowAt(FindButton);
            args.Handled = true;
        }

        private void OpenReplace_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            ReplaceButton.Flyout?.ShowAt(ReplaceButton);
            args.Handled = true;
        }

        private void FindNextShortcut_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            FindNext_Click(sender, new RoutedEventArgs());
            args.Handled = true;
        }

        private void DuplicateLine_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            DuplicateLineOrSelection();
            args.Handled = true;
        }

        private async void RecognizeInk_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            await RecognizeActiveInkAsync();
            args.Handled = true;
        }

        private void DuplicateLineOrSelection()
        {
            var selection = Editor.Document.Selection;
            if (selection == null) return;

            if (selection.Length != 0)
            {
                string text = selection.Text;
                int end = selection.EndPosition;
                Editor.Document.Selection.SetRange(end, end);
                Editor.Document.Selection.Text = text;
                ViewModel.UpdateStatus(Res.GetString("StatusDuplicatedSelection"));
            }
            else
            {
                Editor.Document.GetText(TextGetOptions.None, out string fullText);
                int pos = selection.StartPosition;
                int lineStart = pos > 0 ? fullText.LastIndexOf('\r', pos - 1) + 1 : 0;
                int lineEnd = fullText.IndexOf('\r', pos);
                if (lineEnd < 0) lineEnd = fullText.Length;
                string lineText = fullText[lineStart..lineEnd];
                Editor.Document.Selection.SetRange(lineEnd, lineEnd);
                Editor.Document.Selection.Text = "\r" + lineText;
                ViewModel.UpdateStatus(Res.GetString("StatusDuplicatedLine"));
            }
        }

        private async void CloseTab_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            args.Handled = true;
            if (_activeTabIndex >= 0 && _activeTabIndex < _tabs.Count)
            {
                await CloseTabAtIndexAsync(_activeTabIndex);
            }
        }

        private void ApplyTemplate(DocumentTemplate template)
        {
            string title = template.Key == "blank"
                ? Res.GetString("DocumentUntitled")
                : template.DisplayName;

            _suppressTabModified = true;
            CreateTab(title);
            ViewModel.NewDocument();
            ActiveTab.IsModified = false;
            UpdateEncoding("UTF-8");

            if (!string.IsNullOrEmpty(template.PlainContent))
            {
                _suppressTabModified = false;
                Editor.Document.SetText(TextSetOptions.None, template.PlainContent);
                ViewModel.IsModified = true;
            }
            else
            {
                DeferResetTabModified();
            }

            ViewModel.UpdateStatus(Res.GetFormatted("StatusTemplateApplied", template.DisplayName));
        }

        private async void DocumentTabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            int idx = _tabs.FindIndex(t => t.TabViewItem == args.Tab);
            if (idx < 0) return;
            await CloseTabAtIndexAsync(idx);
        }

        private async void DocumentTabs_TabDroppedOutside(TabView sender, TabViewTabDroppedOutsideEventArgs args)
        {
            int idx = _tabs.FindIndex(t => t.TabViewItem == args.Tab);
            if (idx < 0) return;

            var targetWindow = App.NewWindow();
            await targetWindow.ImportDetachedTabAsync(_tabs[idx]);
            RemoveTabAtIndex(idx, null);
        }

        private async Task CloseTabAtIndexAsync(int idx)
        {
            if (idx < 0 || idx >= _tabs.Count) return;

            // If the closing tab has unsaved changes, prompt
            if (_tabs[idx].IsModified)
            {
                _activeTabIndex = idx;
                DocumentTabs.SelectedIndex = idx;
                SyncViewModelFromActiveTab();
                if (!await PromptSaveChangesAsync()) return;
            }

            RemoveTabAtIndex(idx, Res.GetString("StatusTabClosed"));
        }

        private void RemoveTabAtIndex(int idx, string? statusMessage)
        {
            if (idx < 0 || idx >= _tabs.Count) return;

            var tabItem = _tabs[idx].TabViewItem;
            App.Current.AIDispatcher?.RemoveIndexedTab(_tabs[idx].Id);
            DocumentTabs.TabItems.Remove(tabItem);
            _tabs.RemoveAt(idx);
            RefreshEmptyState();

            if (_tabs.Count == 0)
            {
                // Last tab closed — close the application
                AppWindow.Closing -= AppWindow_Closing;
                Close();
            }
            else
            {
                _activeTabIndex = Math.Min(idx, _tabs.Count - 1);
                DocumentTabs.SelectedIndex = _activeTabIndex;
                SyncViewModelFromActiveTab();
            }

            if (!string.IsNullOrWhiteSpace(statusMessage))
                ViewModel.UpdateStatus(statusMessage);
        }

        private async Task ImportDetachedTabAsync(DocumentTab sourceTab)
        {
            ArgumentNullException.ThrowIfNull(sourceTab);

            RemoveInitialBlankTab();

            _suppressTabModified = true;
            CreateTab(sourceTab.TabViewItem.Header as string ?? Res.GetString("DocumentUntitled"));

            using var stream = new InMemoryRandomAccessStream();
            sourceTab.Editor.Document.SaveToStream(TextGetOptions.FormatRtf, stream);
            stream.Seek(0);
            Editor.Document.LoadFromStream(TextSetOptions.FormatRtf, stream);

            ActiveTab.CurrentFile = sourceTab.CurrentFile;
            ActiveTab.IsModified = sourceTab.IsModified;
            ActiveTab.Encoding = sourceTab.Encoding;
            ActiveTab.ZoomLevel = sourceTab.ZoomLevel;
            ViewModel.ZoomLevel = sourceTab.ZoomLevel;
            ApplyZoom();
            SyncViewModelFromActiveTab();

            if (sourceTab.IsModified)
            {
                ViewModel.IsModified = true;
                _suppressTabModified = false;
            }
            else
            {
                DeferResetTabModified();
            }

            await Task.CompletedTask;
        }

        private void RemoveInitialBlankTab()
        {
            if (_tabs.Count != 1 || _tabs[0].CurrentFile is not null || _tabs[0].IsModified)
                return;

            DocumentTabs.TabItems.Clear();
            _tabs.Clear();
            _activeTabIndex = -1;
        }

        private void DocumentTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int newIdx = DocumentTabs.SelectedIndex;
            if (newIdx < 0 || newIdx >= _tabs.Count) return;
            _activeTabIndex = newIdx;
            SyncViewModelFromActiveTab();
        }

        private void RefreshEmptyState()
        {
            EmptyStatePanel.Visibility = _tabs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            DocumentTabs.Visibility = _tabs.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
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

            if (InkModeToggle is not null)
            {
                InkModeToggle.IsChecked = tab.IsInkModeActive;
            }
        }

        private void ToggleInk_Click(object sender, RoutedEventArgs e)
        {
            bool isActive = sender is ToggleMenuFlyoutItem toggle ? toggle.IsChecked : !ActiveTab.IsInkModeActive;
            SetInkMode(isActive);
        }

        private void SetInkMode(bool isActive)
        {
            if (!HasActiveTab)
            {
                return;
            }

            ActiveTab.IsInkModeActive = isActive;
            InkOverlay.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
            Editor.IsReadOnly = isActive;

            if (InkModeToggle is not null && InkModeToggle.IsChecked != isActive)
            {
                InkModeToggle.IsChecked = isActive;
            }
        }

        private async Task RecognizeActiveInkAsync()
        {
            if (!HasActiveTab)
            {
                return;
            }

            IReadOnlyList<InkStroke> strokes = ActiveTab.GetInkStrokes();
            if (strokes.Count == 0)
            {
                return;
            }

            string recognizedText = await _inkService.RecognizeAsync(strokes);
            if (!string.IsNullOrWhiteSpace(recognizedText))
            {
                Editor.Document.Selection.Text = recognizedText;
                RefreshEditorState();
            }

            ActiveTab.ClearInk();
            SetInkMode(false);
            ViewModel.UpdateStatus(Res.GetString("StatusDrawingInserted"));
        }

        private void InkOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Canvas canvas || !ActiveTab.IsInkModeActive)
            {
                return;
            }

            canvas.CapturePointer(e.Pointer);
            ActiveTab.StartInkStroke(e.GetCurrentPoint(canvas));
            e.Handled = true;
        }

        private void InkOverlay_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Canvas canvas || !ActiveTab.IsInkModeActive)
            {
                return;
            }

            ActiveTab.AppendInkPoint(e.GetCurrentPoint(canvas));
            e.Handled = true;
        }

        private void InkOverlay_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Canvas canvas)
            {
                return;
            }

            ActiveTab.CompleteInkStroke(e.GetCurrentPoint(canvas));
            canvas.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }

        private void InkOverlay_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not Canvas canvas)
            {
                return;
            }

            ActiveTab.CancelInkStroke();
            canvas.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }

        public async Task OpenFileByPathAsync(string filePath)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(filePath);
                // Open the file in a new tab (or reuse current blank tab)
                bool currentIsBlank = ActiveTab.CurrentFile == null && !ActiveTab.IsModified;
                if (!currentIsBlank)
                {
                    CreateTab(file.Name);
                }
                await OpenStorageFileAsync(file);
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorOpeningFile"), ex.Message);
            }
        }

        private async Task OpenStorageFileAsync(StorageFile file)
        {
            _suppressTabModified = true;
            string ext = file.FileType.ToLowerInvariant();
            if (ext is ".docx")
            {
                using var stream = await file.OpenStreamForReadAsync();
                string rtf = DocxImportHelper.ConvertToRtf(stream);

                // In dark mode, swap the default black entry (cf1) in the RTF color
                // table with white so that default-coloured text is immediately
                // readable without any post-load fixup.  Only the first occurrence
                // (the default black entry) is replaced; genuinely coloured text is
                // preserved because it uses different colour table indices.
                if (IsCurrentThemeDark())
                {
                    rtf = ReplaceFirstBlackInColorTable(rtf);
                }

                using var rtfStream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(rtf));
                var randAcc = rtfStream.AsRandomAccessStream();
                Editor.Document.LoadFromStream(TextSetOptions.FormatRtf, randAcc);

                ActiveTab.CurrentFile = null;
                ActiveTab.IsModified = false;
                ActiveTab.TabViewItem.Header = file.Name;
                ActiveTab.Encoding = "DOCX";
                ViewModel.DocumentTitle = file.Name;
                ViewModel.IsModified = false;
                ViewModel.UpdateStatus(Res.GetFormatted("StatusOpened", file.Name));
                _settings.AddRecentFile(file.Path);
                UpdateStatusBarCounts();
                UpdateEncoding("DOCX");
            }
            else if (ext is ".odt")
            {
                using var stream = await file.OpenStreamForReadAsync();
                string odtRtf = DocumentImportHelper.ConvertOdtToRtf(stream);
                using var rtfStream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(odtRtf));
                Editor.Document.LoadFromStream(TextSetOptions.FormatRtf, rtfStream.AsRandomAccessStream());
                ActiveTab.CurrentFile = null;
                ActiveTab.IsModified = false;
                ActiveTab.TabViewItem.Header = file.Name;
                ActiveTab.Encoding = "ODT";
                ViewModel.DocumentTitle = file.Name;
                ViewModel.IsModified = false;
                ViewModel.UpdateStatus(Res.GetFormatted("StatusOpened", file.Name));
                _settings.AddRecentFile(file.Path);
                UpdateStatusBarCounts();
                UpdateEncoding("ODT");
            }
            else if (ext is ".htm" or ".html")
            {
                string html = await FileIO.ReadTextAsync(file);
                string plainText = HtmlConverterHelper.ToPlainText(html);
                Editor.Document.SetText(TextSetOptions.None, plainText);
                ActiveTab.CurrentFile = null;
                ActiveTab.IsModified = false;
                ActiveTab.TabViewItem.Header = file.Name;
                ActiveTab.Encoding = "HTML";
                ViewModel.DocumentTitle = file.Name;
                ViewModel.IsModified = false;
                ViewModel.UpdateStatus(Res.GetFormatted("StatusOpened", file.Name));
                _settings.AddRecentFile(file.Path);
                UpdateStatusBarCounts();
                UpdateEncoding("HTML");
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
                ActiveTab.IsModified = false;
                ActiveTab.TabViewItem.Header = file.Name;
                ActiveTab.Encoding = isTxt ? "UTF-8" : "RTF";
                ViewModel.DocumentTitle = file.Name;
                ViewModel.IsModified = false;
                ViewModel.UpdateStatus(Res.GetFormatted("StatusOpened", file.Name));
                _settings.AddRecentFile(file.Path);
                UpdateStatusBarCounts();
                UpdateEncoding(isTxt ? "UTF-8" : "RTF");
            }

            // LoadFromStream is synchronous but the Win32 RichEdit control processes
            // character-format messages (including baking explicit color values) in
            // subsequent dispatcher cycles. Reading ForegroundColor immediately after
            // loading returns the pre-format state, so the normalization finds nothing
            // to fix. Two nested TryEnqueue calls guarantee we run after all current
            // and immediately-queued RTF formatting work has settled.
            DeferResetTabModified();
            DispatcherQueue.TryEnqueue(() =>
                DispatcherQueue.TryEnqueue(NormalizeDocumentColorsForTheme));
        }

        private static async Task<string> ExtractTextFromArchiveAsync(StorageFile file, string ext)
        {
            using var stream = await file.OpenStreamForReadAsync();
            return DocumentImportHelper.ExtractText(stream, ext);
        }

        private void ApplySettings()
        {
            ApplyWordWrapMode(_settings.WordWrapMode);
            ViewModel.IsWordWrap = _settings.WordWrapMode != "Off";
            ViewModel.FontFamily = _settings.DefaultFontFamily;
            ViewModel.FontSize = _settings.DefaultFontSize;
            Editor.IsSpellCheckEnabled = _settings.SpellCheckEnabled;
            SpellCheckToggle?.IsChecked = _settings.SpellCheckEnabled;
            StatusBar.Visibility = _settings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBarToggle != null) StatusBarToggle.IsChecked = _settings.ShowStatusBar;
            ApplyThemeFromSettings();
            if (_pageViewActive)
            {
                ApplyPageViewLayout();
            }
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
            NormalizeDocumentColorsForTheme();
        }

        /// <summary>
        /// Replaces the first <c>\red0\green0\blue0;</c> entry in the RTF color table
        /// with <c>\red255\green255\blue255;</c> so that default-coloured text (cf1)
        /// renders as white instead of black.  This is the pre-load equivalent of
        /// <see cref="NormalizeDocumentColorsForTheme"/> — modifying the RTF before
        /// <c>LoadFromStream</c> avoids the timing and auto-colour detection issues
        /// of the post-load <c>ITextRange</c> approach.
        /// </summary>
        private static string ReplaceFirstBlackInColorTable(string rtf)
        {
            // Locate the colour table group: {\colortbl;...}
            const string colortblTag = @"{\colortbl";
            int ctStart = rtf.IndexOf(colortblTag, StringComparison.Ordinal);
            if (ctStart < 0) return rtf;

            int ctEnd = rtf.IndexOf('}', ctStart + colortblTag.Length);
            if (ctEnd < 0) return rtf;

            // Within the colour table, replace the first black entry with white.
            const string black = @"\red0\green0\blue0;";
            const string white = @"\red255\green255\blue255;";

            int blackIdx = rtf.IndexOf(black, ctStart, ctEnd - ctStart, StringComparison.Ordinal);
            if (blackIdx < 0) return rtf;

            return string.Concat(rtf.AsSpan(0, blackIdx), white, rtf.AsSpan(blackIdx + black.Length));
        }

        /// <summary>Returns true when the currently resolved theme is dark.</summary>
        private bool IsCurrentThemeDark()
        {
            if (Content is FrameworkElement root)
            {
                if (root.RequestedTheme == ElementTheme.Dark) return true;
                if (root.RequestedTheme == ElementTheme.Light) return false;
                // RequestedTheme.Default — follow the actual resolved theme
                return root.ActualTheme == ElementTheme.Dark;
            }
            return false;
        }

        /// <summary>
        /// Resets the active document's text color when runs use an explicit color that
        /// is unreadable in the current theme — e.g., explicit black on a dark background
        /// or explicit white on a light background.
        ///
        /// The replacement color is the theme-appropriate foreground (white in dark mode,
        /// black in light mode) rather than the RTF "auto" transparent value, because
        /// setting <c>ForegroundColor</c> to <c>Color.FromArgb(0,0,0,0)</c> via the
        /// managed API does not reliably map to the Win32 <c>CFE_AUTOCOLOR</c> flag.
        ///
        /// In dark mode, both explicit black <c>Color(255,0,0,0)</c> and RTF auto colour
        /// <c>Color(0,0,0,0)</c> are treated as unreadable and replaced with white.
        /// </summary>
        private void NormalizeDocumentColorsForTheme()
        {
            if (_activeTabIndex < 0) return;

            Editor.Document.GetText(TextGetOptions.None, out string docText);
            if (string.IsNullOrEmpty(docText.TrimEnd('\r'))) return;

            bool isDark = IsCurrentThemeDark();

            // Use an explicit theme-appropriate foreground colour.
            var replacementColor = isDark
                ? Windows.UI.Color.FromArgb(255, 255, 255, 255)
                : Windows.UI.Color.FromArgb(255, 0, 0, 0);

            // The Win32 RichEdit control implicitly adds a trailing '\r' which inherits
            // the document's default text colour (\cf0 / auto). We exclude it from evaluation.
            int length = docText.Length;
            if (docText.EndsWith('\r') && length > 1)
                length--;

            // Iterate through formatting runs and reset any text that has an
            // unreadable colour in the current theme.
            var range = Editor.Document.GetRange(0, 0);

            while (range.StartPosition < length)
            {
                range.Expand(Microsoft.UI.Text.TextRangeUnit.CharacterFormat);

                // If the expand pushes us beyond the actual visible document length,
                // cap the end position so we don't accidentally evaluate/modify the trailing \r
                int endPos = range.EndPosition;
                if (endPos > length)
                {
                    endPos = length;
                    range.SetRange(range.StartPosition, endPos);
                }

                var fg = range.CharacterFormat.ForegroundColor;
                if (IsUnreadableColor(fg, isDark))
                {
                    range.CharacterFormat.ForegroundColor = replacementColor;
                }

                // Move past this run
                int nextStart = range.EndPosition;
                if (nextStart <= range.StartPosition)
                {
                    // Fallback to prevent infinite loops if Expand fails to advance
                    nextStart = range.StartPosition + 1;
                }

                range.SetRange(nextStart, nextStart);
            }
        }

        /// <summary>
        /// Returns <c>true</c> when <paramref name="color"/> is unreadable in the
        /// current theme.  In dark mode, both opaque black <c>(255,0,0,0)</c> and
        /// RTF auto colour <c>(0,0,0,0)</c> are unreadable.  In light mode, opaque
        /// white <c>(255,255,255,255)</c> and auto-white <c>(0,255,255,255)</c> are.
        /// </summary>
        private static bool IsUnreadableColor(Windows.UI.Color color, bool isDark)
        {
            if (isDark)
            {
                // Explicit black (any alpha) or auto/transparent colour
                return (color.R == 0 && color.G == 0 && color.B == 0);
            }
            else
            {
                // Explicit white (opaque)
                return (color.A == 255 && color.R == 255 && color.G == 255 && color.B == 255);
            }
        }

        private void UpdateTitleBarTheme()
        {
            if (!AppWindowTitleBar.IsCustomizationSupported()) return;

            var titleBar = AppWindow.TitleBar;

            // When following the system theme, reset all custom colors so the OS
            // draws the title bar automatically.
            if (_settings.ThemePreference is not "Dark" and not "Light")
            {
                titleBar.BackgroundColor               = null;
                titleBar.ForegroundColor               = null;
                titleBar.InactiveBackgroundColor        = null;
                titleBar.InactiveForegroundColor        = null;
                titleBar.ButtonBackgroundColor          = null;
                titleBar.ButtonForegroundColor          = null;
                titleBar.ButtonHoverBackgroundColor     = null;
                titleBar.ButtonHoverForegroundColor     = null;
                titleBar.ButtonPressedBackgroundColor   = null;
                titleBar.ButtonPressedForegroundColor   = null;
                titleBar.ButtonInactiveBackgroundColor  = null;
                titleBar.ButtonInactiveForegroundColor  = null;
                return;
            }

            bool isDark = _settings.ThemePreference == "Dark";

            if (isDark)
            {
                // Opaque dark background so the title bar is readable even when
                // the system is in light mode.
                titleBar.BackgroundColor               = Color.FromArgb(255, 32, 32, 32);
                titleBar.InactiveBackgroundColor       = Color.FromArgb(255, 43, 43, 43);
                titleBar.ButtonBackgroundColor         = Color.FromArgb(255, 32, 32, 32);
                titleBar.ButtonInactiveBackgroundColor = Color.FromArgb(255, 43, 43, 43);
                titleBar.ForegroundColor               = Color.FromArgb(255, 255, 255, 255);
                titleBar.InactiveForegroundColor       = Color.FromArgb(160, 255, 255, 255);
                titleBar.ButtonForegroundColor         = Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonHoverForegroundColor    = Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonHoverBackgroundColor    = Color.FromArgb(255, 50, 50, 50);
                titleBar.ButtonPressedForegroundColor  = Color.FromArgb(255, 255, 255, 255);
                titleBar.ButtonPressedBackgroundColor  = Color.FromArgb(255, 70, 70, 70);
                titleBar.ButtonInactiveForegroundColor = Color.FromArgb(128, 255, 255, 255);
            }
            else
            {
                // Opaque light background so the title bar is readable even when
                // the system is in dark mode.
                titleBar.BackgroundColor               = Color.FromArgb(255, 243, 243, 243);
                titleBar.InactiveBackgroundColor       = Color.FromArgb(255, 243, 243, 243);
                titleBar.ButtonBackgroundColor         = Color.FromArgb(255, 243, 243, 243);
                titleBar.ButtonInactiveBackgroundColor = Color.FromArgb(255, 243, 243, 243);
                titleBar.ForegroundColor               = Color.FromArgb(255, 0, 0, 0);
                titleBar.InactiveForegroundColor       = Color.FromArgb(160, 0, 0, 0);
                titleBar.ButtonForegroundColor         = Color.FromArgb(255, 0, 0, 0);
                titleBar.ButtonHoverForegroundColor    = Color.FromArgb(255, 0, 0, 0);
                titleBar.ButtonHoverBackgroundColor    = Color.FromArgb(255, 229, 229, 229);
                titleBar.ButtonPressedForegroundColor  = Color.FromArgb(255, 0, 0, 0);
                titleBar.ButtonPressedBackgroundColor  = Color.FromArgb(255, 204, 204, 204);
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
                        catch (Exception ex) { Debug.WriteLine($"Auto-save failed: {ex.Message}"); }
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

        private async Task PersistSessionSnapshotAsync()
        {
            var tabs = new List<SessionTabState>(_tabs.Count);

            foreach (var tab in _tabs)
            {
                string? backupPath = null;
                if (tab.IsModified || tab.CurrentFile is null)
                {
                    backupPath = SaveTabBackup(tab);
                }

                tabs.Add(new SessionTabState(
                    tab.TabViewItem.Header as string ?? Res.GetString("DocumentUntitled"),
                    tab.CurrentFile?.Path,
                    backupPath,
                    GetCursorPosition(tab)));
            }

            await _sessionRestoreService.SaveSessionAsync(tabs);
        }

        private void ResetTabsForSessionRestore()
        {
            DocumentTabs.TabItems.Clear();
            _tabs.Clear();
            _activeTabIndex = -1;
            _nextTabId = 1;
        }

        private async Task RestoreTabStateAsync(SessionTabState state)
        {
            if (!string.IsNullOrWhiteSpace(state.TempBackupPath) && File.Exists(state.TempBackupPath))
            {
                using var stream = File.Open(state.TempBackupPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                Editor.Document.LoadFromStream(TextSetOptions.FormatRtf, stream.AsRandomAccessStream());
                ActiveTab.CurrentFile = await TryGetStorageFileAsync(state.FilePath);
                ActiveTab.IsModified = true;
                ActiveTab.Encoding = "RTF";
                ActiveTab.TabViewItem.Header = string.IsNullOrWhiteSpace(state.Title) ? Res.GetString("DocumentUntitled") : state.Title;
            }
            else if (!string.IsNullOrWhiteSpace(state.FilePath) && File.Exists(state.FilePath))
            {
                await OpenFileByPathAsync(state.FilePath);
            }
            else
            {
                Editor.Document.SetText(TextSetOptions.None, string.Empty);
                ActiveTab.CurrentFile = null;
                ActiveTab.IsModified = false;
                ActiveTab.Encoding = "UTF-8";
                ActiveTab.TabViewItem.Header = string.IsNullOrWhiteSpace(state.Title) ? Res.GetString("DocumentUntitled") : state.Title;
            }

            Editor.Document.GetText(TextGetOptions.None, out var documentText);
            var safeCursorPosition = Math.Clamp(state.CursorPosition, 0, documentText.Length);
            Editor.Document.Selection.SetRange(safeCursorPosition, safeCursorPosition);
        }

        private string SaveTabBackup(DocumentTab tab)
        {
            var backupDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SmrtPad",
                "backups");
            Directory.CreateDirectory(backupDirectory);

            var backupPath = Path.Combine(backupDirectory, $"tab_{tab.Id}.rtf");
            using var stream = File.Open(backupPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
            tab.Editor.Document.SaveToStream(TextGetOptions.FormatRtf, stream.AsRandomAccessStream());
            return backupPath;
        }

        private static async Task<StorageFile?> TryGetStorageFileAsync(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                return await StorageFile.GetFileFromPathAsync(filePath);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }

        private static int GetCursorPosition(DocumentTab tab)
        {
            var selection = tab.Editor.Document.Selection;
            return selection?.StartPosition ?? 0;
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

            // Update ViewModel properties — toggle buttons sync via {x:Bind} TwoWay.
            // Treat FormatEffect.Undefined (mixed selection) as "on" so that toggles
            // correctly reflect partial formatting on selections that include paragraph
            // marks or other non-formatted characters at the selection boundary.
            ViewModel.IsBold = charFormat.Bold != FormatEffect.Off;
            ViewModel.IsItalic = charFormat.Italic != FormatEffect.Off;
            ViewModel.IsUnderline = charFormat.Underline != UnderlineType.None;
            ViewModel.IsStrikethrough = charFormat.Strikethrough != FormatEffect.Off;
            ViewModel.IsSubscript = charFormat.Subscript != FormatEffect.Off;
            ViewModel.IsSuperscript = charFormat.Superscript != FormatEffect.Off;

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
            if (_fontsInitialized)
                return;

            _fontsInitialized = true;
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
            if (_suppressTabModified) return;
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

        private void New_Click(object sender, RoutedEventArgs e)
        {
            _suppressTabModified = true;
            Editor.Document.SetText(TextSetOptions.None, string.Empty);
            ViewModel.NewDocument();
            if (ActiveTab != null)
            {
                ActiveTab.CurrentFile = null;
                ActiveTab.IsModified = false;
                ActiveTab.Encoding = "UTF-8";
                ActiveTab.TabViewItem.Header = ViewModel.DocumentTitle;
            }
            UpdateEncoding("UTF-8");
            DeferResetTabModified();
        }

        private void NewTab_Click(object sender, RoutedEventArgs e)
        {
            DocumentTabs_AddTabButtonClick(DocumentTabs, null!);
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
                var picker = new FileOpenPicker();
                InitializePicker(picker);
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
                    // Open the file in a new tab (or reuse current blank tab)
                    bool currentIsBlank = ActiveTab.CurrentFile == null && !ActiveTab.IsModified;
                    if (!currentIsBlank)
                    {
                        CreateTab(file.Name);
                    }
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
                    InitializePicker(picker);
                    picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                    picker.FileTypeChoices.Add(Res.GetString("FileTypeRtf"), [".rtf"]);
                    picker.FileTypeChoices.Add(Res.GetString("FileTypeTxt"), [".txt"]);
                picker.FileTypeChoices.Add(Res.GetString("FileTypeHtml"), [".html"]);
                picker.FileTypeChoices.Add(Res.GetString("FileTypeOdt"), [".odt"]);
                picker.FileTypeChoices.Add(Res.GetString("FileTypeDocx"), [".docx"]);
                    picker.SuggestedFileName = Res.GetString("FileDefaultName");

                    StorageFile file = await picker.PickSaveFileAsync();
                    if (file != null)
                    {
                        await SaveToFileAsync(file);
                    }
                }
                else
                {
                    await SaveToFileAsync(ActiveTab.CurrentFile!);
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
                InitializePicker(picker);
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add(Res.GetString("FileTypeRtf"), [".rtf"]);
                picker.FileTypeChoices.Add(Res.GetString("FileTypeTxt"), [".txt"]);
                picker.FileTypeChoices.Add(Res.GetString("FileTypeDocx"), [".docx"]);
                picker.FileTypeChoices.Add(Res.GetString("FileTypeOdt"), [".odt"]);
                picker.FileTypeChoices.Add(Res.GetString("FileTypeHtml"), [".html"]);
                picker.SuggestedFileName = ActiveTab.CurrentFile?.DisplayName ?? Res.GetString("FileDefaultName");

                StorageFile file = await picker.PickSaveFileAsync();
                if (file != null)
                {
                    await SaveToFileAsync(file);
                }
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorSavingFile"), ex.Message);
            }
        }

        /// <summary>
        /// Writes the current editor content to the specified file, updates tab state,
        /// and records the file in the MRU list.
        /// </summary>
        private async Task SaveToFileAsync(StorageFile file)
        {
            CachedFileManager.DeferUpdates(file);

            if (file.FileType.Equals(".docx", StringComparison.OrdinalIgnoreCase))
            {
                Editor.Document.GetText(TextGetOptions.FormatRtf, out string rtf);
                using var stream = await file.OpenStreamForWriteAsync();
                DocxAltChunkExporter.ExportToDocx(rtf, stream);
                await stream.FlushAsync();
            }
            else if (file.FileType.Equals(".odt", StringComparison.OrdinalIgnoreCase))
            {
                Editor.Document.GetText(TextGetOptions.None, out string plainText);
                using var stream = await file.OpenStreamForWriteAsync();
                OdtExportHelper.Export(plainText.TrimEnd('\r'), stream);
                await stream.FlushAsync();
            }
            else if (file.FileType.Equals(".htm", StringComparison.OrdinalIgnoreCase)
                || file.FileType.Equals(".html", StringComparison.OrdinalIgnoreCase))
            {
                Editor.Document.GetText(TextGetOptions.None, out string plainText);
                string html = HtmlConverterHelper.FromPlainText(plainText.TrimEnd('\r'));
                await FileIO.WriteTextAsync(file, html);
            }
            else
            {
                using var randAccStream = await file.OpenAsync(FileAccessMode.ReadWrite);
                var options = file.FileType.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                    ? TextGetOptions.None
                    : TextGetOptions.FormatRtf;
                Editor.Document.SaveToStream(options, randAccStream);
            }

            FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
            if (status == FileUpdateStatus.Complete)
            {
                ActiveTab.CurrentFile = file;
                ActiveTab.Encoding = file.FileType.ToLowerInvariant() switch
                {
                    ".txt" => "UTF-8",
                    ".docx" => "DOCX",
                    ".odt" => "ODT",
                    ".htm" or ".html" => "HTML",
                    _ => "RTF"
                };
                ViewModel.DocumentTitle = file.Name;
                ViewModel.IsModified = false;
                ViewModel.UpdateStatus(Res.GetFormatted("StatusSaved", file.Name));
                _settings.AddRecentFile(file.Path);
                UpdateEncoding(ActiveTab.Encoding);
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
            if (_printingRegistered)
                return;

            _printingRegistered = true;
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

            var (pageWidth, pageHeight) = GetConfiguredPageSizePixels();
            Thickness margins = GetConfiguredPageMarginsPixels();
            double contentWidth = Math.Max(100, pageWidth - margins.Left - margins.Right);
            double contentHeight = Math.Max(100, pageHeight - margins.Top - margins.Bottom);
            double lineHeight = Math.Max(14, ViewModel.FontSize * 1.6);

            Editor.Document.GetText(TextGetOptions.None, out string fullText);
            string[] lines = fullText.TrimEnd('\r').Split('\r');

            int linesPerPage = Math.Max(1, (int)(contentHeight / lineHeight));
            int totalPages = Math.Max(1, (int)Math.Ceiling((double)lines.Length / linesPerPage));

            for (int page = 0; page < totalPages; page++)
            {
                var pagePanel = new StackPanel
                {
                    Width = pageWidth,
                    Height = pageHeight,
                    Padding = margins
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
                    Width = contentWidth
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

        private async void PageSetup_Click(object _, RoutedEventArgs _1)
        {
            var panel = new StackPanel { Spacing = 10, MinWidth = 320 };

            var paperSizeBox = new ComboBox { Header = Res.GetString("PageSetupPaperSize") };
            paperSizeBox.Items.Add(new ComboBoxItem { Tag = "A4", Content = Res.GetString("PageSetupPaperA4") });
            paperSizeBox.Items.Add(new ComboBoxItem { Tag = "Letter", Content = Res.GetString("PageSetupPaperLetter") });
            paperSizeBox.Items.Add(new ComboBoxItem { Tag = "Legal", Content = Res.GetString("PageSetupPaperLegal") });
            paperSizeBox.SelectedItem = paperSizeBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag as string, _settings.PagePaperSize, StringComparison.OrdinalIgnoreCase))
                ?? paperSizeBox.Items.OfType<ComboBoxItem>().First(item => string.Equals(item.Tag as string, "Letter", StringComparison.Ordinal));
            panel.Children.Add(paperSizeBox);

            var orientationBox = new ComboBox { Header = Res.GetString("PageSetupOrientation") };
            orientationBox.Items.Add(new ComboBoxItem { Tag = "Portrait", Content = Res.GetString("PageSetupOrientationPortrait") });
            orientationBox.Items.Add(new ComboBoxItem { Tag = "Landscape", Content = Res.GetString("PageSetupOrientationLandscape") });
            orientationBox.SelectedItem = orientationBox.Items
                .OfType<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Tag as string, _settings.PageOrientation, StringComparison.OrdinalIgnoreCase))
                ?? orientationBox.Items.OfType<ComboBoxItem>().First(item => string.Equals(item.Tag as string, "Portrait", StringComparison.Ordinal));
            panel.Children.Add(orientationBox);

            var marginTopBox = new NumberBox
            {
                Header = Res.GetString("PageSetupMarginTop"),
                Minimum = 0,
                Maximum = 5,
                Value = _settings.PageMarginTopInches,
                SmallChange = 0.1,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            panel.Children.Add(marginTopBox);

            var marginBottomBox = new NumberBox
            {
                Header = Res.GetString("PageSetupMarginBottom"),
                Minimum = 0,
                Maximum = 5,
                Value = _settings.PageMarginBottomInches,
                SmallChange = 0.1,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            panel.Children.Add(marginBottomBox);

            var marginLeftBox = new NumberBox
            {
                Header = Res.GetString("PageSetupMarginLeft"),
                Minimum = 0,
                Maximum = 5,
                Value = _settings.PageMarginLeftInches,
                SmallChange = 0.1,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            panel.Children.Add(marginLeftBox);

            var marginRightBox = new NumberBox
            {
                Header = Res.GetString("PageSetupMarginRight"),
                Minimum = 0,
                Maximum = 5,
                Value = _settings.PageMarginRightInches,
                SmallChange = 0.1,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            panel.Children.Add(marginRightBox);

            var dialog = new ContentDialog
            {
                Title = Res.GetString("PageSetupTitle"),
                Content = panel,
                PrimaryButtonText = Res.GetString("ButtonSave"),
                CloseButtonText = Res.GetString("ButtonCancel"),
                XamlRoot = Content.XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            _settings.PagePaperSize = (paperSizeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "Letter";
            _settings.PageOrientation = (orientationBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "Portrait";
            _settings.PageMarginTopInches = marginTopBox.Value;
            _settings.PageMarginBottomInches = marginBottomBox.Value;
            _settings.PageMarginLeftInches = marginLeftBox.Value;
            _settings.PageMarginRightInches = marginRightBox.Value;
            _settings.Save();

            ApplyPageViewLayout();
            ViewModel.UpdateStatus(Res.GetString("StatusPageSetupSaved"));
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
            rulerUnitsBox.Items.Add(Res.GetString("OptionsRulerPoints"));
            rulerUnitsBox.Items.Add(Res.GetString("OptionsRulerPicas"));
            rulerUnitsBox.SelectedIndex = _settings.RulerUnits switch
            {
                "cm" => 1,
                "pt" => 2,
                "pc" => 3,
                _    => 0
            };
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
                _settings.RulerUnits = rulerUnitsBox.SelectedIndex switch
                {
                    1 => "cm",
                    2 => "pt",
                    3 => "pc",
                    _ => "in"
                };
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
                InitializePicker(picker);
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
                InitializePicker(picker);
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add(Res.GetString("FileTypeDocx"), [".docx"]);
                picker.SuggestedFileName = ActiveTab.CurrentFile?.DisplayName ?? Res.GetString("FileDefaultName");

                StorageFile file = await picker.PickSaveFileAsync();
                if (file == null) return;

                Editor.Document.GetText(TextGetOptions.FormatRtf, out string rtf);

                CachedFileManager.DeferUpdates(file);
                using (var stream = await file.OpenStreamForWriteAsync())
                {
                    DocxAltChunkExporter.ExportToDocx(rtf, stream);
                    await stream.FlushAsync();
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
                InitializePicker(picker);
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

        /// <summary>
        /// Saves a temporary RTF copy of the active document and launches the default
        /// mail client with the document attached and a pre-filled subject line.
        /// </summary>
        private async void SendEmail_Click(object _, RoutedEventArgs _1)
        {
            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "SmrtPad", "Email");
                Directory.CreateDirectory(tempDir);

                string safeName = ViewModel.DocumentTitle
                    .Replace(Path.DirectorySeparatorChar, '_')
                    .Replace(Path.AltDirectorySeparatorChar, '_')
                    .TrimEnd('.');
                if (string.IsNullOrWhiteSpace(safeName)) safeName = "Document";
                string tempFile = Path.Combine(tempDir, $"{safeName}.rtf");

                // Write current content to temp file
                var storageFolder = await StorageFolder.GetFolderFromPathAsync(tempDir);
                var storageFile = await storageFolder.CreateFileAsync(
                    Path.GetFileName(tempFile), CreationCollisionOption.ReplaceExisting);
                using (var stream = await storageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    Editor.Document.SaveToStream(TextGetOptions.FormatRtf, stream);
                }

                // Build mailto URI — body is kept empty; attachment handled via shell
                string subject = Uri.EscapeDataString(
                    Res.GetFormatted("SendEmailSubject", ViewModel.DocumentTitle));
                var mailto = new Uri($"mailto:?subject={subject}");
                await Launcher.LaunchUriAsync(mailto);
                ViewModel.UpdateStatus(Res.GetString("StatusEmailSent"));
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorGeneric"), ex.Message);
            }
        }

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
                {
                    var pf = Editor.Document.Selection.ParagraphFormat;
                    pf.Alignment = cmd.Value switch
                    {
                        "Center"  => ParagraphAlignment.Center,
                        "Right"   => ParagraphAlignment.Right,
                        "Justify" => ParagraphAlignment.Justify,
                        _         => ParagraphAlignment.Left,
                    };
                    Editor.Document.Selection.ParagraphFormat = pf;
                    ViewModel.SetAlignment(cmd.Value);
                    break;
                }
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
                InitializePicker(picker);
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
                InitializePicker(picker);
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
            RefreshEditorState();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            Editor.Document.Selection.Copy();
            RefreshEditorState();
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            Editor.Document.Selection.Paste(0);
            RefreshEditorState();
        }

        private void PasteSplitButton_Click(SplitButton sender, SplitButtonClickEventArgs args)
        {
            Editor.Document.Selection.Paste(0);
            RefreshEditorState();
        }

        private async void PastePlain_Click(object sender, RoutedEventArgs e)
        {
            await PasteAsPlainTextAsync();
        }

        /// <summary>
        /// Pastes the current clipboard content as plain (unformatted) text into the active editor.
        /// After insertion, the character format of the pasted range is reset to defaults so that
        /// formatting inherited from the surrounding text (e.g. bold) is stripped (UI-14).
        /// </summary>
        private async Task PasteAsPlainTextAsync()
        {
            var dataView = Clipboard.GetContent();
            if (!dataView.Contains(StandardDataFormats.Text))
                return;
            try
            {
                string text = (await dataView.GetTextAsync()).TrimEnd('\r', '\n');
                int start = Editor.Document.Selection.StartPosition;
                Editor.Document.Selection.Text = text;

                // Expand the selection to cover the entire document (which at this point is
                // just the pasted text) and strip all character formatting. Using Expand
                // instead of SetRange(start, end) avoids an off-by-one in the position
                // calculation and ensures the stripping covers every pasted character.
                Editor.Document.Selection.Expand(TextRangeUnit.Story);
                var fmt = Editor.Document.Selection.CharacterFormat;
                fmt.Bold       = FormatEffect.Off;
                fmt.Italic     = FormatEffect.Off;
                fmt.Underline  = UnderlineType.None;
                fmt.Strikethrough = FormatEffect.Off;
                fmt.Subscript  = FormatEffect.Off;
                fmt.Superscript = FormatEffect.Off;
                Editor.Document.Selection.CharacterFormat = fmt;

                // Collapse selection to end of pasted text
                int end = start + text.Length;
                Editor.Document.Selection.SetRange(end, end);
                RefreshEditorState();
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync(Res.GetString("ErrorPaste"), ex.Message);
            }
        }

        private void Bold_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                charFormatting.Bold = FormatEffect.Toggle;
                selectedText.CharacterFormat = charFormatting;
                RefreshFormattingState();
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
                RefreshFormattingState();
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
                RefreshFormattingState();
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
                RefreshFormattingState();
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
                    ViewModel.IsSuperscript = false;
                }
                selectedText.CharacterFormat = charFormatting;
                // RefreshFormattingState calls Editor_SelectionChanged which may read a stale
                // selection state for Subscript (e.g. because the paragraph mark is excluded).
                // Explicitly set ViewModel.IsSubscript after the call to ensure the ribbon
                // toggle correctly reflects the format that was just applied.
                RefreshFormattingState();
                ViewModel.IsSubscript = isChecked;
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
                if (isChecked)
                {
                    // Clear subscript before setting superscript — ITextCharacterFormat
                    // rejects Superscript=On while Subscript is still On on the same object.
                    charFormatting.Subscript = FormatEffect.Off;
                    SubscriptToggle.IsChecked = false;
                    charFormatting.Superscript = FormatEffect.On;
                }
                else
                {
                    charFormatting.Superscript = FormatEffect.Off;
                }
                selectedText.CharacterFormat = charFormatting;
                RefreshFormattingState();
            }
            _macro.Record(MacroCommandType.Superscript);
        }

        private void RefreshFormattingState()
        {
            RefreshEditorState();
        }

        private void RefreshEditorState()
        {
            Editor.Focus(FocusState.Programmatic);
            UpdateSelectionLength();
            UpdateStatusBarCounts();
            Editor_SelectionChanged(Editor, new RoutedEventArgs());
        }

        private void NewWindow_Click(object _sender, RoutedEventArgs _e)
        {
            App.NewWindow();
        }

        private void ApplyToolbarAutomationNames()
        {
            if (Content is not DependencyObject root)
                return;

            foreach (var descendant in EnumerateDescendants(root))
            {
                switch (descendant)
                {
                    case Button button:
                        ApplyAutomationName(button, button.Content);
                        break;
                    case ToggleButton toggleButton:
                        ApplyAutomationName(toggleButton, toggleButton.Content);
                        break;
                    case SplitButton splitButton:
                        ApplyAutomationName(splitButton, splitButton.Content);
                        break;
                }
            }
        }

        private static void ApplyAutomationName(FrameworkElement element, object? content)
        {
            if (!string.IsNullOrWhiteSpace(AutomationPeer.GetName(element)))
                return;

            if (ToolTipService.GetToolTip(element) is string toolTip && !string.IsNullOrWhiteSpace(toolTip))
            {
                AutomationPeer.SetName(element, toolTip);
                return;
            }

            if (content is string text && !string.IsNullOrWhiteSpace(text))
            {
                AutomationPeer.SetName(element, text);
                return;
            }

            if (content is DependencyObject contentRoot)
            {
                var contentText = FindFirstText(contentRoot);
                if (!string.IsNullOrWhiteSpace(contentText))
                    AutomationPeer.SetName(element, contentText);
            }
        }

        private static IEnumerable<DependencyObject> EnumerateDescendants(DependencyObject root)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                yield return child;

                foreach (var descendant in EnumerateDescendants(child))
                    yield return descendant;
            }
        }

        private static string? FindFirstText(DependencyObject root)
        {
            if (root is TextBlock textBlock && !string.IsNullOrWhiteSpace(textBlock.Text))
                return textBlock.Text;

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                var text = FindFirstText(VisualTreeHelper.GetChild(root, i));
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return null;
        }

        private async void Exit_Click(object _, RoutedEventArgs _1)
        {
            if (!await PromptSaveAllTabsAsync())
                return;
            Close();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            Editor.Document.Selection.Expand(TextRangeUnit.Story);
            RefreshEditorState();
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

        private void ZoomSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            double snapped = Math.Round(e.NewValue / 10.0) * 10.0;
            if (Math.Abs(ViewModel.ZoomLevel - snapped) > 0.01)
            {
                ViewModel.ZoomLevel = snapped;
                ApplyZoom();
            }
        }

        private void ZoomPercentBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ApplyZoomFromPercentBox();
                e.Handled = true;
            }
        }

        private void ZoomPercentBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ApplyZoomFromPercentBox();
        }

        private void ApplyZoomFromPercentBox()
        {
            string raw = ZoomPercentBox.Text.TrimEnd('%').Trim();
            if (double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double level))
            {
                level = Math.Clamp(level, 10.0, 500.0);
                ViewModel.ZoomLevel = level;
                ApplyZoom();
            }
            else
            {
                ZoomPercentBox.Text = ViewModel.ZoomDisplay;
            }
        }

        private void ApplyZoom()
        {
            if (!HasActiveTab)
                return;

            double scale = ViewModel.ZoomLevel / 100.0;
            EditorScaleTransform.ScaleX = scale;
            EditorScaleTransform.ScaleY = scale;

            // Keep the slider and percent box in sync without re-triggering handlers
            if (ZoomSlider != null && Math.Abs(ZoomSlider.Value - ViewModel.ZoomLevel) > 0.01)
                ZoomSlider.Value = ViewModel.ZoomLevel;
            if (ZoomPercentBox != null)
                ZoomPercentBox.Text = ViewModel.ZoomDisplay;

            if (_rulersVisible)
                RedrawRulers();

            if (_pageViewActive)
            {
                ApplyPageViewLayout();
            }
            else
            {
                RefreshEditorViewportLayout();
            }
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
                bool wasEnabled = paragraphFormatting.ListType == MarkerType.Bullet;
                string newListType = wasEnabled ? "None" : "Bullet";
                paragraphFormatting.ListType = wasEnabled ? MarkerType.None : MarkerType.Bullet;
                selectedText.ParagraphFormat = paragraphFormatting;
                ViewModel.SetListType(newListType);
                _macro.Record(MacroCommandType.SetListType, newListType);
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
            FontColorIndicator.Fill = new SolidColorBrush(color);
            string hexName = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            AutomationPeer.SetName(FontColorIndicator, Res.GetFormatted("FontColorIndicatorName", hexName));
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
            }
        }

        private void ApplyHighlightColor(Color color)
        {
            HighlightColorIndicator.Fill = new SolidColorBrush(color);
            string hexName = color.A == 0 ? "None" : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            AutomationPeer.SetName(HighlightColorIndicator, Res.GetFormatted("HighlightColorIndicatorName", hexName));
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                charFormatting.BackgroundColor = color;
                selectedText.CharacterFormat = charFormatting;
            }
        }

        private void RemoveHighlight_Click(object sender, RoutedEventArgs e)
        {
            // Transparent background effectively removes the highlight
            ApplyHighlightColor(Color.FromArgb(0, 255, 255, 255));
        }

        private async void InsertPicture_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializePicker(picker);
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
            if (sender is MenuFlyoutItem item && item.Tag is string mode)
            {
                ApplyWordWrapMode(mode);
                _settings.WordWrapMode = mode;
                _settings.DefaultWordWrap = mode != "Off";
                _settings.Save();
                ViewModel.IsWordWrap = mode != "Off";
            }
        }

        /// <summary>
        /// Applies the word-wrap mode: Off, Wrap, or WrapToRuler.
        /// WrapToRuler constrains the editor width to a page-ruler column (6.5 in default).
        /// </summary>
        private void ApplyWordWrapMode(string mode)
        {
            switch (mode)
            {
                case "Off":
                    Editor.TextWrapping = TextWrapping.NoWrap;
                    Editor.MaxWidth = double.PositiveInfinity;
                    break;
                case "WrapToRuler":
                    Editor.TextWrapping = TextWrapping.Wrap;
                    // Default usable page width: Letter (8.5 in) – 2 × 1 in margin = 6.5 in × 96 dpi
                    double printableWidthPx = (_settings.PageMarginLeftInches == 0 && _settings.PageMarginRightInches == 0)
                        ? 6.5 * 96.0
                        : Math.Max(100,
                            (s_paperSizes.TryGetValue(_settings.PagePaperSize, out var ps) ? ps.WidthIn : 8.5)
                            * 96.0
                            - (_settings.PageMarginLeftInches + _settings.PageMarginRightInches) * 96.0);
                    Editor.MaxWidth = printableWidthPx;
                    break;
                default:
                    Editor.TextWrapping = TextWrapping.Wrap;
                    Editor.MaxWidth = double.PositiveInfinity;
                    break;
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

        private void StatusBarToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem toggle)
            {
                bool show = toggle.IsChecked;
                StatusBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                _settings.ShowStatusBar = show;
                _settings.Save();
                ViewModel.UpdateStatus(show ? Res.GetString("StatusStatusBarShown") : Res.GetString("StatusStatusBarHidden"));
            }
        }

        private async void SmartSidebarToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleMenuFlyoutItem toggle)
                return;

            SmrtSidebarToolbarButton.IsChecked = toggle.IsChecked;
            await ToggleSmrtSidebarAsync(toggle.IsChecked);
        }

        private async void SmrtSidebarToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            bool open = SmrtSidebarToolbarButton.IsChecked == true;
            SmartSidebarToggle.IsChecked = open;
            await ToggleSmrtSidebarAsync(open);
        }

        private async Task ToggleSmrtSidebarAsync(bool open)
        {
            if (open)
            {
                if (!Services.Licensing.FeatureFlags.IsEnabled(Services.Licensing.SmrtPadFeature.SmartSidebar))
                {
                    SmartSidebarToggle.IsChecked = false;
                    SmrtSidebarToolbarButton.IsChecked = false;
                    await ShowProUpsellAsync();
                    return;
                }

                var aiDispatcher = App.Current.AIDispatcher;
                if (aiDispatcher is null)
                {
                    SmartSidebarToggle.IsChecked = false;
                    SmrtSidebarToolbarButton.IsChecked = false;
                    await ShowAiPrerequisiteDialogAsync();
                    return;
                }

                var sidebar = new Controls.SmartSidebar(aiDispatcher);
                sidebar.GetSelectedText = GetSelectionOrDocumentText;
                sidebar.GetRewriteSourceText = GetSelectionOrCurrentParagraphText;
                sidebar.GetTextBeforeCaret = GetCurrentParagraphTextBeforeCaret;
                sidebar.ApplyToneRewrite = text => ApplySidebarRewrite(text, highlightTemporarily: true);
                sidebar.ApplyClarityRewrite = text => ApplySidebarRewrite(text, highlightTemporarily: false);
                sidebar.ApplyGrammarFix = text => ApplySidebarRewrite(text, highlightTemporarily: false);
                sidebar.ApplyShortenRewrite = text => ApplySidebarRewrite(text, highlightTemporarily: false);
                sidebar.InsertGeneratedText = InsertSidebarText;
                sidebar.GetSemanticDocuments = GetSemanticDocumentsSnapshot;
                sidebar.NavigateToSemanticResult = NavigateToSemanticResult;
                sidebar.CloseRequested += (_, _) => CloseSmartSidebar();
                sidebar.ReportStatus = msg => ViewModel.UpdateStatus(msg);
                SidebarHost.Content = sidebar;
                SidebarHost.Visibility = Visibility.Visible;
            }
            else
            {
                CloseSmartSidebar();
            }
        }

        internal void RefreshProGatedUi()
        {
            if (Services.Licensing.FeatureFlags.IsEnabled(Services.Licensing.SmrtPadFeature.SmartSidebar)
                && App.Current.AIDispatcher is not null)
            {
                return;
            }

            CloseSmartSidebar();
        }

        private void CloseSmartSidebar()
        {
            SidebarHost.Content = null;
            SidebarHost.Visibility = Visibility.Collapsed;
            SmartSidebarToggle.IsChecked = false;
            SmrtSidebarToolbarButton.IsChecked = false;
        }

        private string GetSelectionOrDocumentText()
        {
            Editor.Document.GetText(TextGetOptions.None, out var text);
            var selection = Editor.Document.Selection;
            if (selection is not null && selection.Length != 0)
            {
                selection.GetText(TextGetOptions.None, out var selectedText);
                return selectedText;
            }

            return text;
        }

        private string GetSelectionOrCurrentParagraphText()
        {
            var (start, end) = GetSidebarRewriteRange();
            var range = Editor.Document.GetRange(start, end);
            range.GetText(TextGetOptions.None, out var text);
            return text;
        }

        private void ApplySidebarRewrite(string text, bool highlightTemporarily)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var (start, end) = GetSidebarRewriteRange();
            var range = Editor.Document.GetRange(start, end);
            range.Text = text;
            var rewrittenRange = Editor.Document.GetRange(start, start + text.Length);
            if (highlightTemporarily)
            {
                HighlightSidebarRange(rewrittenRange.StartPosition, rewrittenRange.EndPosition);
            }

            Editor.Document.Selection.SetRange(rewrittenRange.StartPosition, rewrittenRange.EndPosition);
            RefreshEditorState();
        }

        private void InsertSidebarText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            var selection = Editor.Document.Selection;
            if (selection is null)
                return;

            selection.Text = text;
            RefreshEditorState();
        }

        private string GetCurrentParagraphTextBeforeCaret()
        {
            var selection = Editor.Document.Selection;
            if (selection is null)
                return string.Empty;

            Editor.Document.GetText(TextGetOptions.None, out var fullText);
            if (string.IsNullOrEmpty(fullText))
                return string.Empty;

            var caretPosition = Math.Clamp(Math.Min(selection.StartPosition, selection.EndPosition), 0, fullText.Length);
            var paragraphStart = caretPosition > 0
                ? fullText.LastIndexOf('\r', caretPosition - 1) + 1
                : 0;

            if (caretPosition <= paragraphStart)
                return string.Empty;

            return fullText[paragraphStart..caretPosition];
        }

        private (int Start, int End) GetSidebarRewriteRange()
        {
            var selection = Editor.Document.Selection;
            if (selection is null)
                return (0, 0);

            var start = Math.Min(selection.StartPosition, selection.EndPosition);
            var end = Math.Max(selection.StartPosition, selection.EndPosition);
            if (end > start)
                return (start, end);

            Editor.Document.GetText(TextGetOptions.None, out var fullText);
            if (string.IsNullOrEmpty(fullText))
                return (0, 0);

            var safePosition = Math.Clamp(start, 0, fullText.Length);
            var paragraphStart = safePosition > 0
                ? fullText.LastIndexOf('\r', safePosition - 1) + 1
                : 0;
            var paragraphEnd = fullText.IndexOf('\r', safePosition);
            if (paragraphEnd < 0)
                paragraphEnd = fullText.Length;

            return (paragraphStart, paragraphEnd);
        }

        private void HighlightSidebarRange(int start, int end)
        {
            if (end <= start)
                return;

            var range = Editor.Document.GetRange(start, end);
            range.CharacterFormat.BackgroundColor = Color.FromArgb(255, 255, 255, 0);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Editor.Document.GetText(TextGetOptions.None, out var currentText);
                var safeStart = Math.Clamp(start, 0, currentText.Length);
                var safeEnd = Math.Clamp(end, safeStart, currentText.Length);
                if (safeEnd <= safeStart)
                    return;

                var clearRange = Editor.Document.GetRange(safeStart, safeEnd);
                clearRange.CharacterFormat.BackgroundColor = Color.FromArgb(0, 0, 0, 0);
            };
            timer.Start();
        }

        private IReadOnlyList<SemanticSearchDocument> GetSemanticDocumentsSnapshot()
        {
            return _tabs
                .Select(tab => new SemanticSearchDocument(tab.Id, GetSemanticTabName(tab), GetTabDocumentText(tab)))
                .ToArray();
        }

        private void NavigateToSemanticResult(int tabId, string chunkText)
        {
            if (string.IsNullOrWhiteSpace(chunkText))
                return;

            var tabIndex = _tabs.FindIndex(tab => tab.Id == tabId);
            if (tabIndex < 0)
                return;

            _activeTabIndex = tabIndex;
            DocumentTabs.SelectedIndex = tabIndex;
            SyncViewModelFromActiveTab();

            var selection = Editor.Document.Selection;
            if (selection is null)
                return;

            Editor.Focus(FocusState.Programmatic);
            var found = selection.FindText(chunkText, TextConstants.MaxUnitCount, FindOptions.None);
            if (found == 0)
            {
                Editor.Document.GetText(TextGetOptions.None, out var fullText);
                var index = fullText.IndexOf(chunkText, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    selection.SetRange(index, index + chunkText.Length);
                }
            }
        }

        private static string GetSemanticTabName(DocumentTab tab) =>
            tab.CurrentFile?.Name ?? tab.TabViewItem.Header?.ToString() ?? string.Empty;

        private static string GetTabDocumentText(DocumentTab tab)
        {
            tab.Editor.Document.GetText(TextGetOptions.None, out var text);
            return text;
        }

        /// <summary>Shows the Pro upsell dialog directing users to the Store.</summary>
        private async Task ShowProUpsellAsync()
        {
            var dialog = new ContentDialog
            {
                Title = Res.GetString("ProUpsellTitle"),
                Content = Res.GetString("ProUpsellContent"),
                PrimaryButtonText = Res.GetString("ProUpsellUpgrade"),
                CloseButtonText = Res.GetString("ProUpsellDismiss"),
                XamlRoot = Content.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await Windows.System.Launcher.LaunchUriAsync(
                    new Uri("ms-windows-store://pdp/?productid=SmrtPadPro"));
            }
        }

        private async Task ShowAiPrerequisiteDialogAsync()
        {
            var foundryPath = TryResolveFoundryExecutablePath();
            var contentKey = string.IsNullOrWhiteSpace(foundryPath)
                ? "SmartSidebarFoundryMissingContent"
                : "SmartSidebarAIDispatcherUnavailableContent";

            var dialog = new ContentDialog
            {
                Title = Res.GetString("SmartSidebarAIDispatcherUnavailableTitle"),
                Content = Res.GetString(contentKey),
                PrimaryButtonText = Res.GetString("SmartSidebarAIDispatcherUnavailableSetup"),
                CloseButtonText = Res.GetString("SmartSidebarAIDispatcherUnavailableDismiss"),
                XamlRoot = Content.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(new Uri("https://learn.microsoft.com/azure/foundry-local/get-started"));
            }
        }

        private static string? TryResolveFoundryExecutablePath()
        {
            var pathCandidates = new List<string>();

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                pathCandidates.Add(Path.Combine(localAppData, "Microsoft", "FoundryLocal", "foundry.exe"));
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                pathCandidates.Add(Path.Combine(programFiles, "Microsoft", "FoundryLocal", "foundry.exe"));
            }

            var path = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrWhiteSpace(path))
            {
                foreach (var part in path.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    pathCandidates.Add(Path.Combine(part, "foundry.exe"));
                }
            }

            return pathCandidates.FirstOrDefault(File.Exists);
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

        private (double PageWidthPx, double PageHeightPx) GetConfiguredPageSizePixels()
        {
            var baseSize = s_paperSizes.TryGetValue(_settings.PagePaperSize, out var selected)
                ? selected
                : s_paperSizes["Letter"];

            bool landscape = string.Equals(_settings.PageOrientation, "Landscape", StringComparison.OrdinalIgnoreCase);
            double widthIn = landscape ? baseSize.HeightIn : baseSize.WidthIn;
            double heightIn = landscape ? baseSize.WidthIn : baseSize.HeightIn;

            return (widthIn * Dpi, heightIn * Dpi);
        }

        private Thickness GetConfiguredPageMarginsPixels()
        {
            return new Thickness(
                Math.Max(0, _settings.PageMarginLeftInches * Dpi),
                Math.Max(0, _settings.PageMarginTopInches * Dpi),
                Math.Max(0, _settings.PageMarginRightInches * Dpi),
                Math.Max(0, _settings.PageMarginBottomInches * Dpi));
        }

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
                var (pageWidthPx, pageHeightPx) = GetConfiguredPageSizePixels();
                Thickness margins = GetConfiguredPageMarginsPixels();
                double printableWidthPx = Math.Max(100, pageWidthPx - margins.Left - margins.Right);

                PageViewBorder.Visibility = Visibility.Visible;
                PageViewBorder.Width = pageWidthPx;
                PageViewBorder.MinHeight = pageHeightPx;
                Editor.HorizontalAlignment = HorizontalAlignment.Center;
                Editor.Width = printableWidthPx;
                Editor.MaxWidth = printableWidthPx;
                Editor.Margin = margins;
            }
            else
            {
                PageViewBorder.Visibility = Visibility.Collapsed;
                Editor.HorizontalAlignment = HorizontalAlignment.Stretch;
                Editor.Width = double.NaN;
                Editor.MaxWidth = double.PositiveInfinity;
                Editor.Margin = new Thickness(0);
            }

            RefreshEditorViewportLayout();
        }

        private void RefreshEditorViewportLayout()
        {
            DocumentTabs.UpdateLayout();
            EditorScrollViewer.UpdateLayout();
            EditorContainer.UpdateLayout();
            PageViewBorder.UpdateLayout();
            Editor.UpdateLayout();
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
            SetInkMode(true);
            await Task.CompletedTask;
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
                InitializePicker(picker);
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

        /// <summary>
        /// Sets the owner window handle on a file picker so it can display correctly in WinUI 3.
        /// </summary>
        private void InitializePicker(object picker)
        {
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        }

        private async Task ShowErrorDialogAsync(string title, string message)
        {
            await _dialogService.ShowErrorAsync(title, message);
        }

        private async void PasteSpecial_Click(object sender, RoutedEventArgs e)
        {
            var dataView = Clipboard.GetContent();
            bool hasRtf   = dataView.Contains(StandardDataFormats.Rtf);
            bool hasText  = dataView.Contains(StandardDataFormats.Text);
            bool hasHtml  = dataView.Contains(StandardDataFormats.Html);

            if (!hasRtf && !hasText && !hasHtml)
            {
                await ShowErrorDialogAsync(Res.GetString("PasteSpecialTitle"), Res.GetString("ErrorPaste"));
                return;
            }

            var hintText = new TextBlock { Text = Res.GetString("PasteSpecialHint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };

            var richRadio  = new RadioButton { Content = Res.GetString("PasteSpecialRichText"),  IsChecked = hasRtf,  IsEnabled = hasRtf,  GroupName = "PasteFormat" };
            var plainRadio = new RadioButton { Content = Res.GetString("PasteSpecialPlainText"), IsChecked = !hasRtf && hasText, IsEnabled = hasText, GroupName = "PasteFormat" };
            var htmlRadio  = new RadioButton { Content = Res.GetString("PasteSpecialHtml"),      IsChecked = false,   IsEnabled = hasHtml, GroupName = "PasteFormat" };
            AutomationPeer.SetAutomationId(richRadio,  "PasteSpecialRichRadio");
            AutomationPeer.SetAutomationId(plainRadio, "PasteSpecialPlainRadio");
            AutomationPeer.SetAutomationId(htmlRadio,  "PasteSpecialHtmlRadio");

            var panel = new StackPanel { Spacing = 6, MinWidth = 260 };
            panel.Children.Add(hintText);
            panel.Children.Add(richRadio);
            panel.Children.Add(plainRadio);
            panel.Children.Add(htmlRadio);

            var dialog = new ContentDialog
            {
                Title = Res.GetString("PasteSpecialTitle"),
                Content = panel,
                PrimaryButtonText = Res.GetString("ButtonOK"),
                CloseButtonText = Res.GetString("ButtonCancel"),
                XamlRoot = Content.XamlRoot
            };
            AutomationPeer.SetAutomationId(dialog, "PasteSpecialDialog");

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            try
            {
                if (richRadio.IsChecked == true && hasRtf)
                {
                    string rtf = await dataView.GetRtfAsync();
                    Editor.Document.Selection.SetText(TextSetOptions.FormatRtf, rtf);
                }
                else if (htmlRadio.IsChecked == true && hasHtml)
                {
                    string html = await dataView.GetHtmlFormatAsync();
                    string plain = HtmlConverterHelper.ToPlainText(html);
                    Editor.Document.Selection.Text = plain;
                }
                else if (hasText)
                {
                    string text = await dataView.GetTextAsync();
                    Editor.Document.Selection.Text = text;
                }
                RefreshEditorState();
                ViewModel.UpdateStatus(Res.GetString("StatusPastedSpecial"));
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
                // Use the theme-appropriate foreground: hardcoding black would make
                // cleared text invisible in dark mode.
                charFormatting.ForegroundColor = IsCurrentThemeDark()
                    ? Windows.UI.Color.FromArgb(255, 255, 255, 255)
                    : Windows.UI.Color.FromArgb(255, 0, 0, 0);
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

                RefreshFormattingState();
                ViewModel.UpdateStatus(Res.GetString("StatusFormattingCleared"));
            }
            _macro.Record(MacroCommandType.ClearFormatting);
        }

        /// <summary>
        /// Opens a consolidated Format → Font dialog that sets family, size, style,
        /// effects, and character color in one place — matching WordPad's Format > Font.
        /// Reads the current selection's character format on open; writes back on OK.
        /// </summary>
        private async void FormatFont_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            ITextCharacterFormat currentFormat = selectedText.CharacterFormat;

            // ── Read current selection state ──
            string currentFamily = string.IsNullOrEmpty(currentFormat.Name)
                ? _settings.DefaultFontFamily
                : currentFormat.Name;
            float currentSize = currentFormat.Size > 0
                ? currentFormat.Size
                : (float)_settings.DefaultFontSize;
            bool currentBold = currentFormat.Bold == FormatEffect.On;
            bool currentItalic = currentFormat.Italic == FormatEffect.On;
            bool currentUnderline = currentFormat.Underline != UnderlineType.None;
            bool currentStrikethrough = currentFormat.Strikethrough == FormatEffect.On;
            bool currentSubscript = currentFormat.Subscript == FormatEffect.On;
            bool currentSuperscript = currentFormat.Superscript == FormatEffect.On;
            Color currentColor = currentFormat.ForegroundColor;

            // ── Build dialog controls ──
            var fonts = Microsoft.Graphics.Canvas.Text.CanvasTextFormat.GetSystemFontFamilies()
                .OrderBy(f => f).ToList();
            var fontFamilyCombo = new ComboBox
            {
                ItemsSource = fonts,
                SelectedItem = fonts.Contains(currentFamily) ? currentFamily : fonts.FirstOrDefault(),
                IsEditable = true,
                Header = Res.GetString("FontDialogFamily"),
                HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
            };
            AutomationPeer.SetAutomationId(fontFamilyCombo, "FontDialogFamilyCombo");

            var sizes = new List<double> { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 };
            var fontSizeCombo = new ComboBox
            {
                ItemsSource = sizes,
                IsEditable = true,
                Header = Res.GetString("FontDialogSize"),
                Width = 100,
            };
            AutomationPeer.SetAutomationId(fontSizeCombo, "FontDialogSizeCombo");

            // Set the size — try to select from list, otherwise set text
            if (sizes.Contains((double)currentSize))
                fontSizeCombo.SelectedItem = (double)currentSize;
            else
                fontSizeCombo.Text = currentSize.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Style checkboxes
            var boldCheck = new CheckBox { Content = "Bold", IsChecked = currentBold };
            AutomationPeer.SetAutomationId(boldCheck, "FontDialogBoldCheck");
            var italicCheck = new CheckBox { Content = "Italic", IsChecked = currentItalic };
            AutomationPeer.SetAutomationId(italicCheck, "FontDialogItalicCheck");

            // Effects checkboxes
            var underlineCheck = new CheckBox { Content = "Underline", IsChecked = currentUnderline };
            AutomationPeer.SetAutomationId(underlineCheck, "FontDialogUnderlineCheck");
            var strikethroughCheck = new CheckBox { Content = "Strikethrough", IsChecked = currentStrikethrough };
            AutomationPeer.SetAutomationId(strikethroughCheck, "FontDialogStrikethroughCheck");
            var subscriptCheck = new CheckBox { Content = "Subscript", IsChecked = currentSubscript };
            AutomationPeer.SetAutomationId(subscriptCheck, "FontDialogSubscriptCheck");
            var superscriptCheck = new CheckBox { Content = "Superscript", IsChecked = currentSuperscript };
            AutomationPeer.SetAutomationId(superscriptCheck, "FontDialogSuperscriptCheck");

            // Mutual exclusion for subscript/superscript
            subscriptCheck.Checked += (_, _) => { if (subscriptCheck.IsChecked == true) superscriptCheck.IsChecked = false; };
            superscriptCheck.Checked += (_, _) => { if (superscriptCheck.IsChecked == true) subscriptCheck.IsChecked = false; };

            // Color picker
            var colorPicker = new ColorPicker
            {
                Color = currentColor,
                IsAlphaEnabled = false,
                IsHexInputVisible = true,
                IsMoreButtonVisible = false,
            };
            AutomationPeer.SetAutomationId(colorPicker, "FontDialogColorPicker");

            // ── Layout ──
            var panel = new StackPanel { Spacing = 12, Width = 340 };

            // Font family row
            panel.Children.Add(fontFamilyCombo);

            // Size row
            panel.Children.Add(fontSizeCombo);

            // Style section
            var styleHeader = new TextBlock
            {
                Text = Res.GetString("FontDialogStyleHeader"),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 0)
            };
            panel.Children.Add(styleHeader);

            var stylePanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            stylePanel.Children.Add(boldCheck);
            stylePanel.Children.Add(italicCheck);
            panel.Children.Add(stylePanel);

            // Effects section
            var effectsHeader = new TextBlock
            {
                Text = Res.GetString("FontDialogEffectsHeader"),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 0)
            };
            panel.Children.Add(effectsHeader);

            var effectsRow1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            effectsRow1.Children.Add(underlineCheck);
            effectsRow1.Children.Add(strikethroughCheck);
            panel.Children.Add(effectsRow1);

            var effectsRow2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
            effectsRow2.Children.Add(subscriptCheck);
            effectsRow2.Children.Add(superscriptCheck);
            panel.Children.Add(effectsRow2);

            // Color section
            var colorHeader = new TextBlock
            {
                Text = Res.GetString("FontDialogColorHeader"),
                FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 0)
            };
            panel.Children.Add(colorHeader);
            panel.Children.Add(colorPicker);

            var scrollViewer = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 500,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            var dialog = new ContentDialog
            {
                Title = Res.GetString("FontDialogTitle"),
                Content = scrollViewer,
                PrimaryButtonText = Res.GetString("ButtonOK"),
                CloseButtonText = Res.GetString("ButtonCancel"),
                XamlRoot = Content.XamlRoot
            };
            AutomationPeer.SetAutomationId(dialog, "FormatFontDialog");

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                // ── Apply all settings back to the selection ──
                ITextCharacterFormat charFormat = selectedText.CharacterFormat;

                // Font family
                string selectedFamily = fontFamilyCombo.SelectedItem as string ?? fontFamilyCombo.Text;
                if (!string.IsNullOrWhiteSpace(selectedFamily))
                {
                    charFormat.Name = selectedFamily;
                    ViewModel.FontFamily = selectedFamily;
                }

                // Font size
                string sizeText = fontSizeCombo.SelectedItem?.ToString() ?? fontSizeCombo.Text;
                if (double.TryParse(sizeText, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double newSize) && newSize >= 1 && newSize <= 999)
                {
                    charFormat.Size = (float)newSize;
                    ViewModel.FontSize = newSize;
                }

                // Style
                charFormat.Bold = boldCheck.IsChecked == true ? FormatEffect.On : FormatEffect.Off;
                charFormat.Italic = italicCheck.IsChecked == true ? FormatEffect.On : FormatEffect.Off;
                ViewModel.IsBold = boldCheck.IsChecked == true;
                ViewModel.IsItalic = italicCheck.IsChecked == true;

                // Effects
                charFormat.Underline = underlineCheck.IsChecked == true ? UnderlineType.Single : UnderlineType.None;
                charFormat.Strikethrough = strikethroughCheck.IsChecked == true ? FormatEffect.On : FormatEffect.Off;
                charFormat.Subscript = subscriptCheck.IsChecked == true ? FormatEffect.On : FormatEffect.Off;
                charFormat.Superscript = superscriptCheck.IsChecked == true ? FormatEffect.On : FormatEffect.Off;
                ViewModel.IsUnderline = underlineCheck.IsChecked == true;
                ViewModel.IsStrikethrough = strikethroughCheck.IsChecked == true;
                ViewModel.IsSubscript = subscriptCheck.IsChecked == true;
                ViewModel.IsSuperscript = superscriptCheck.IsChecked == true;

                // Color
                charFormat.ForegroundColor = colorPicker.Color;
                _lastFontColor = colorPicker.Color;
                FontColorIndicator.Fill = new SolidColorBrush(colorPicker.Color);

                selectedText.CharacterFormat = charFormat;
                ViewModel.UpdateStatus(Res.GetString("StatusFontApplied"));
            }
        }

        /// <summary>
        /// Opens a consolidated Format → Paragraph dialog: alignment, indents, line spacing,
        /// and space before/after — all in one ContentDialog.
        /// </summary>
        private async void FormatParagraph_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection sel = Editor.Document.Selection;
            ITextParagraphFormat pf = sel.ParagraphFormat;

            // Read current state
            string currentAlignment = pf.Alignment switch
            {
                ParagraphAlignment.Center  => "Center",
                ParagraphAlignment.Right   => "Right",
                ParagraphAlignment.Justify => "Justify",
                _                          => "Left"
            };
            double leftIndentIn  = pf.LeftIndent / 72.0;
            double rightIndentIn = pf.RightIndent / 72.0;
            double firstLineIn   = pf.FirstLineIndent / 72.0;
            double spaceBefore   = pf.SpaceBefore;
            double spaceAfter    = pf.SpaceAfter;

            // Alignment
            var alignCombo = new ComboBox
            {
                Header = Res.GetString("ParagraphDialogAlignment"),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            alignCombo.Items.Add("Left");
            alignCombo.Items.Add("Center");
            alignCombo.Items.Add("Right");
            alignCombo.Items.Add("Justify");
            alignCombo.SelectedItem = currentAlignment;
            AutomationPeer.SetAutomationId(alignCombo, "ParagraphAlignCombo");

            // Indentation
            var indentLeftBox = new NumberBox
            {
                Header = Res.GetString("ParagraphDialogIndentLeft"),
                Minimum = 0, Maximum = 22, SmallChange = 0.1,
                Value = Math.Round(leftIndentIn, 2),
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            AutomationPeer.SetAutomationId(indentLeftBox, "ParagraphIndentLeftBox");

            var indentRightBox = new NumberBox
            {
                Header = Res.GetString("ParagraphDialogIndentRight"),
                Minimum = 0, Maximum = 22, SmallChange = 0.1,
                Value = Math.Round(rightIndentIn, 2),
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            AutomationPeer.SetAutomationId(indentRightBox, "ParagraphIndentRightBox");

            var indentFirstBox = new NumberBox
            {
                Header = Res.GetString("ParagraphDialogIndentFirst"),
                Minimum = -5, Maximum = 22, SmallChange = 0.1,
                Value = Math.Round(firstLineIn, 2),
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            AutomationPeer.SetAutomationId(indentFirstBox, "ParagraphIndentFirstBox");

            // Spacing
            var spaceBeforeBox = new NumberBox
            {
                Header = Res.GetString("ParagraphDialogSpaceBefore"),
                Minimum = 0, Maximum = 200, SmallChange = 1,
                Value = spaceBefore,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            AutomationPeer.SetAutomationId(spaceBeforeBox, "ParagraphSpaceBeforeBox");

            var spaceAfterBox = new NumberBox
            {
                Header = Res.GetString("ParagraphDialogSpaceAfter"),
                Minimum = 0, Maximum = 200, SmallChange = 1,
                Value = spaceAfter,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            AutomationPeer.SetAutomationId(spaceAfterBox, "ParagraphSpaceAfterBox");

            var lineSpacingBox = new NumberBox
            {
                Header = Res.GetString("ParagraphDialogLineSpacing"),
                Minimum = 0.5, Maximum = 10, SmallChange = 0.25,
                Value = ViewModel.LineSpacing > 0 ? ViewModel.LineSpacing : 1.0,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };
            AutomationPeer.SetAutomationId(lineSpacingBox, "ParagraphLineSpacingBox");

            var panel = new StackPanel { Spacing = 10, MinWidth = 300 };
            panel.Children.Add(alignCombo);
            panel.Children.Add(new TextBlock { Text = Res.GetString("ParagraphDialogIndentation"), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
            var indentRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            indentRow.Children.Add(indentLeftBox);
            indentRow.Children.Add(indentRightBox);
            panel.Children.Add(indentRow);
            panel.Children.Add(indentFirstBox);
            panel.Children.Add(new TextBlock { Text = Res.GetString("ParagraphDialogSpacing"), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 0) });
            panel.Children.Add(lineSpacingBox);
            var spacingRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            spacingRow.Children.Add(spaceBeforeBox);
            spacingRow.Children.Add(spaceAfterBox);
            panel.Children.Add(spacingRow);

            var dialog = new ContentDialog
            {
                Title = Res.GetString("ParagraphDialogTitle"),
                Content = panel,
                PrimaryButtonText = Res.GetString("ButtonOK"),
                CloseButtonText = Res.GetString("ButtonCancel"),
                XamlRoot = Content.XamlRoot
            };
            AutomationPeer.SetAutomationId(dialog, "FormatParagraphDialog");

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            ITextParagraphFormat newPf = sel.ParagraphFormat;
            newPf.Alignment = (alignCombo.SelectedItem as string) switch
            {
                "Center"  => ParagraphAlignment.Center,
                "Right"   => ParagraphAlignment.Right,
                "Justify" => ParagraphAlignment.Justify,
                _         => ParagraphAlignment.Left
            };
            newPf.SetIndents(
                (float)(indentFirstBox.Value * 72.0),
                (float)(indentLeftBox.Value  * 72.0),
                (float)(indentRightBox.Value * 72.0));
            newPf.SpaceBefore = (float)spaceBeforeBox.Value;
            newPf.SpaceAfter  = (float)spaceAfterBox.Value;
            newPf.SetLineSpacing(LineSpacingRule.Multiple, (float)lineSpacingBox.Value);
            sel.ParagraphFormat = newPf;

            ViewModel.LineSpacing = lineSpacingBox.Value;
            ViewModel.ParagraphSpacingBefore = spaceBeforeBox.Value;
            ViewModel.ParagraphSpacingAfter  = spaceAfterBox.Value;
            ViewModel.UpdateStatus(Res.GetString("StatusParagraphApplied"));
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
        public int Id { get; internal set; }
        public TabViewItem TabViewItem { get; }
        public RichEditBox Editor { get; }
        public Canvas InkOverlay { get; }
        public ScrollViewer ScrollViewer { get; }
        public Grid EditorContainer { get; }
        public Border PageViewBorder { get; }
        public ScaleTransform EditorScaleTransform { get; } = new ScaleTransform();
        public bool IsInkModeActive { get; set; }

        public StorageFile? CurrentFile { get; set; }
        public bool IsModified { get; set; }
        public string Encoding { get; set; } = "UTF-8";
        public double ZoomLevel { get; set; } = 100.0;
        private readonly List<InkStroke> _inkStrokes = [];
        private readonly List<InkPoint> _currentInkPoints = [];
        private Polyline? _activePolyline;

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
                // Transparent background + no border so ScaleTransform only affects text.
                // The EditorContainer provides the full-size visible background.
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
            };

            // Override all RichEditBox visual-state backgrounds so the control is
            // fully transparent in every state (rest, focused, pointer-over, disabled).
            // Without this, the focused/hover background scales with the RenderTransform,
            // creating a visible "shrunken box" inside the layout area below 100 % zoom.
            var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            Editor.Resources["TextControlBackground"] = transparent;
            Editor.Resources["TextControlBackgroundPointerOver"] = transparent;
            Editor.Resources["TextControlBackgroundFocused"] = transparent;
            Editor.Resources["TextControlBackgroundDisabled"] = transparent;
            Editor.Resources["TextControlBorderBrush"] = transparent;
            Editor.Resources["TextControlBorderBrushPointerOver"] = transparent;
            Editor.Resources["TextControlBorderBrushFocused"] = transparent;
            Editor.Resources["TextControlBorderBrushDisabled"] = transparent;

            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(Editor, "Editor");
            Editor.RenderTransform = EditorScaleTransform;

            // Keep the selection highlight visible when the editor loses focus (e.g. the user
            // clicks a ribbon button to apply formatting). Without this the selection disappears
            // as soon as focus leaves the RichEditBox, making it impossible to see what text
            // will be affected by the ribbon action.
            Editor.Loaded += (_, _) =>
            {
                Editor.SelectionHighlightColorWhenNotFocused = Editor.SelectionHighlightColor;
            };

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

            InkOverlay = new Canvas
            {
                Visibility = Visibility.Collapsed,
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(24, 255, 255, 0)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(InkOverlay, "InkOverlay");

            EditorContainer = new Grid 
            { 
                Margin = new Thickness(4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            // Fill the container with the editor's theme background so the panel
            // appears full-size even when the Editor's ScaleTransform makes its
            // own visual smaller than its layout bounds.
            static void ApplyEditorBackground(Grid g)
            {
                if (Application.Current.Resources.TryGetValue("TextControlBackground", out var obj)
                    && obj is Microsoft.UI.Xaml.Media.Brush bg)
                    g.Background = bg;
            }
            EditorContainer.Loaded           += (_, _) => ApplyEditorBackground(EditorContainer);
            EditorContainer.ActualThemeChanged += (_, _) => ApplyEditorBackground(EditorContainer);
            EditorContainer.Children.Add(PageViewBorder);
            EditorContainer.Children.Add(Editor);
            EditorContainer.Children.Add(InkOverlay);

            ScrollViewer = new ScrollViewer
            {
                Content = EditorContainer,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollMode = ScrollMode.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };

            // Keep EditorContainer at least as tall as the ScrollViewer viewport
            // so the background fills the visible area at any zoom level.
            ScrollViewer.SizeChanged += (_, _) =>
            {
                double margin = EditorContainer.Margin.Top + EditorContainer.Margin.Bottom;
                EditorContainer.MinHeight = Math.Max(0, ScrollViewer.ActualHeight - margin);
            };

            TabViewItem = new TabViewItem
            {
                Header = title,
                IsClosable = true,
                Content = ScrollViewer,
            };
        }

        public void StartInkStroke(WinUIPointerPoint point)
        {
            _currentInkPoints.Clear();
            _activePolyline = null;

            _activePolyline = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };

            InkOverlay.Children.Add(_activePolyline);
            AppendInkPoint(point);
        }

        public void AppendInkPoint(WinUIPointerPoint point)
        {
            if (_activePolyline is null)
            {
                return;
            }

            _activePolyline.Points.Add(point.Position);
            _currentInkPoints.Add(new InkPoint(point.Position, point.Properties.Pressure));
        }

        public void CompleteInkStroke(WinUIPointerPoint point)
        {
            if (_activePolyline is null)
            {
                return;
            }

            AppendInkPoint(point);
            if (_currentInkPoints.Count >= 2)
            {
                var builder = new InkStrokeBuilder();
                builder.SetDefaultDrawingAttributes(new InkDrawingAttributes
                {
                    Color = Color.FromArgb(255, 0, 0, 0),
                    IgnorePressure = false,
                    FitToCurve = false,
                    Size = new Windows.Foundation.Size(2, 2)
                });

                _inkStrokes.Add(builder.CreateStrokeFromInkPoints(_currentInkPoints, Matrix3x2.Identity));
            }

            _currentInkPoints.Clear();
            _activePolyline = null;
        }

        public void CancelInkStroke()
        {
            if (_activePolyline is not null)
            {
                InkOverlay.Children.Remove(_activePolyline);
                _activePolyline = null;
            }

            _currentInkPoints.Clear();
        }

        public IReadOnlyList<InkStroke> GetInkStrokes() => _inkStrokes.Select(stroke => stroke.Clone()).ToList();

        public void ClearInk()
        {
            _inkStrokes.Clear();
            _currentInkPoints.Clear();
            _activePolyline = null;
            InkOverlay.Children.Clear();
        }
    }
}

