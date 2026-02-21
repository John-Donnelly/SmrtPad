using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Microsoft.UI.Text;
using Windows.UI;
using WinRT.Interop;
using SmrtPad.Helpers;
using SmrtPad.ViewModels;
using SmrtPad.Views;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SmrtPad
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private StorageFile _currentFile;
        public EditorViewModel ViewModel { get; } = new EditorViewModel();

        // reserved for future image selection tracking

        public MainWindow()
        {
            InitializeComponent();
            Title = $"SmrtPad - {ViewModel.DocumentTitle}";
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.DocumentTitle))
                {
                    Title = $"SmrtPad - {ViewModel.DocumentTitle}";
                }
                else if (e.PropertyName == nameof(ViewModel.StatusMessage))
                {
                    StatusText.Text = ViewModel.StatusMessage;
                }
            };

            InitializeFonts();

            // Editor is now a native RichEdit host; WinUI RichEditBox events/APIs no longer apply.

            FileBackstage.NewRequested += (s, e) => { HideBackstage(); New_Click(this, new RoutedEventArgs()); };
            FileBackstage.OpenRequested += (s, e) => { HideBackstage(); Open_Click(this, new RoutedEventArgs()); };
            FileBackstage.SaveRequested += (s, e) => { HideBackstage(); Save_Click(this, new RoutedEventArgs()); };
            FileBackstage.SaveAsRequested += (s, e) => { HideBackstage(); SaveAs_Click(this, new RoutedEventArgs()); };
            FileBackstage.PrintRequested += (s, e) => { HideBackstage(); Print_Click(this, new RoutedEventArgs()); };
            FileBackstage.OptionsRequested += (s, e) => { HideBackstage(); Options_Click(this, new RoutedEventArgs()); };
            FileBackstage.ExitRequested += (s, e) => { Close(); };
        }

        // Image hosting now uses native RichEdit OLE objects.

        private void ShowBackstage()
        {
            FileBackstage.Visibility = Visibility.Visible;
            Editor.Visibility = Visibility.Collapsed;
        }

        private void HideBackstage()
        {
            FileBackstage.Visibility = Visibility.Collapsed;
            Editor.Visibility = Visibility.Visible;
        }

        private void InitializeFonts()
        {
            var fonts = Microsoft.Graphics.Canvas.Text.CanvasTextFormat.GetSystemFontFamilies();
            FontFamilyComboBox.ItemsSource = fonts.OrderBy(f => f).ToList();
            FontFamilyComboBox.SelectedItem = "Segoe UI";

            var sizes = new List<double> { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 };
            FontSizeComboBox.ItemsSource = sizes;
            FontSizeComboBox.SelectedItem = 11.0;
        }

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontFamilyComboBox.SelectedItem is string fontName)
            {
                ITextSelection selectedText = Editor.Document.Selection;
                if (selectedText != null)
                {
                    ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                    charFormatting.Name = fontName;
                    selectedText.CharacterFormat = charFormatting;
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
                }
            }
        }

        private void New_Click(object sender, RoutedEventArgs e)
        {
            Editor.Document.SetText(TextSetOptions.None, string.Empty);
            _currentFile = null;
            ViewModel.NewDocument();
        }

        private void FileMenu_Tapped(object sender, RoutedEventArgs e)
        {
            if (FileBackstage.Visibility == Visibility.Visible)
                HideBackstage();
            else
                ShowBackstage();
        }

        private async void Open_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".rtf");
            picker.FileTypeFilter.Add(".txt");

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                using (var randAccStream = await file.OpenAsync(FileAccessMode.Read))
                {
                    var options = file.FileType.Equals(".txt", StringComparison.OrdinalIgnoreCase)
                        ? TextSetOptions.None
                        : TextSetOptions.FormatRtf;
                    Editor.Document.LoadFromStream(options, randAccStream);
                }
                _currentFile = file;
                ViewModel.DocumentTitle = file.Name;
                ViewModel.UpdateStatus($"Opened {file.Name}");
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_currentFile == null)
            {
                var picker = new FileSavePicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.FileTypeChoices.Add("Rich Text Format", new List<string>() { ".rtf" });
                picker.FileTypeChoices.Add("Text Document", new List<string>() { ".txt" });
                picker.SuggestedFileName = "Document";

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
                        _currentFile = file;
                        ViewModel.DocumentTitle = file.Name;
                        ViewModel.UpdateStatus($"Saved {file.Name}");
                    }
                }
            }
            else
            {
                using (var randAccStream = await _currentFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    Editor.Document.SaveToStream(TextGetOptions.FormatRtf, randAccStream);
                }
                ViewModel.UpdateStatus($"Saved {_currentFile.Name}");
            }
        }

        private async void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("Rich Text Format", new List<string>() { ".rtf" });
            picker.FileTypeChoices.Add("Text Document", new List<string>() { ".txt" });
            picker.SuggestedFileName = _currentFile?.DisplayName ?? "Document";

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
                    _currentFile = file;
                    ViewModel.DocumentTitle = file.Name;
                    ViewModel.UpdateStatus($"Saved {file.Name}");
                }
            }
        }

        private async void Print_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Print",
                Content = "Printing is not yet implemented. This feature will be available in a future update.",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private async void Options_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Options",
                Content = "Options are not yet implemented. This feature will be available in a future update.",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
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
        }

        private void Subscript_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                if (charFormatting.Subscript == FormatEffect.On)
                {
                    charFormatting.Subscript = FormatEffect.Off;
                }
                else
                {
                    charFormatting.Subscript = FormatEffect.On;
                    charFormatting.Superscript = FormatEffect.Off;
                }
                selectedText.CharacterFormat = charFormatting;
            }
        }

        private void Superscript_Click(object sender, RoutedEventArgs e)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                if (charFormatting.Superscript == FormatEffect.On)
                {
                    charFormatting.Superscript = FormatEffect.Off;
                }
                else
                {
                    charFormatting.Superscript = FormatEffect.On;
                    charFormatting.Subscript = FormatEffect.Off;
                }
                selectedText.CharacterFormat = charFormatting;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
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
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ZoomOut();
            ApplyZoom();
        }

        private void ApplyZoom()
        {
            double scale = ViewModel.ZoomLevel / 100.0;
            if (Editor.RenderTransform is not ScaleTransform scaleTransform)
            {
                scaleTransform = new ScaleTransform();
                Editor.RenderTransform = scaleTransform;
                Editor.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            scaleTransform.ScaleX = scale;
            scaleTransform.ScaleY = scale;
            ZoomText.Text = $"{ViewModel.ZoomLevel:0}%";
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
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                charFormatting.ForegroundColor = color;
                selectedText.CharacterFormat = charFormatting;
            }
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
                using (var randAccStream = await file.OpenAsync(FileAccessMode.Read))
                {
                    Editor.Document.Selection.InsertImage(0, 0, 0, VerticalCharacterAlignment.Baseline, file.Name, randAccStream);
                }
            }
        }

        private void InsertDateTime_Click(object sender, RoutedEventArgs e)
        {
            Editor.Document.Selection.Text = DateTime.Now.ToString("g");
        }

        private void FindNext_Click(object sender, RoutedEventArgs e)
        {
            string textToFind = FindTextBox.Text;
            if (!string.IsNullOrEmpty(textToFind))
            {
                Editor.Document.Selection.FindText(textToFind, TextConstants.MaxUnitCount, FindOptions.None);
            }
        }

        private void Replace_Click(object sender, RoutedEventArgs e)
        {
            string textToFind = ReplaceFindTextBox.Text;
            string replaceWith = ReplaceWithTextBox.Text;
            if (!string.IsNullOrEmpty(textToFind))
            {
                if (Editor.Document.Selection.Text == textToFind)
                {
                    Editor.Document.Selection.Text = replaceWith;
                }
                Editor.Document.Selection.FindText(textToFind, TextConstants.MaxUnitCount, FindOptions.None);
            }
        }

        private void ReplaceAll_Click(object sender, RoutedEventArgs e)
        {
            string textToFind = ReplaceFindTextBox.Text;
            string replaceWith = ReplaceWithTextBox.Text;
            if (!string.IsNullOrEmpty(textToFind))
            {
                int count = 0;
                Editor.Document.Selection.SetRange(0, 0);
                while (Editor.Document.Selection.FindText(textToFind, TextConstants.MaxUnitCount, FindOptions.None) > 0)
                {
                    Editor.Document.Selection.Text = replaceWith;
                    count++;
                }
                ViewModel.UpdateStatus($"Replaced {count} occurrences.");
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
                    ViewModel.UpdateStatus("Drawing inserted.");
                }
                else if (process.ExitCode != 0)
                {
                    ViewModel.UpdateStatus("Drawing cancelled.");
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                var dialog = new ContentDialog
                {
                    Title = "SmrtDoodle Not Found",
                    Content = "SmrtDoodle is not installed or could not be found in the system PATH. Please install SmrtDoodle to use the Paint Drawing feature.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        private async void InsertObject_Click(object sender, RoutedEventArgs e)
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
                    ViewModel.UpdateStatus($"Inserted {file.Name}.");
                }
                else
                {
                    Editor.Document.Selection.Text = $"[Embedded object: {file.Name}]";
                    ViewModel.UpdateStatus($"Inserted reference to {file.Name}.");
                }
            }
        }
    }
}
