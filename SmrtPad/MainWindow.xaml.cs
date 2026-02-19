using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Microsoft.UI.Text;
using WinRT.Interop;
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
            FileBackstage.ExitRequested += (s, e) => { HideBackstage(); Exit_Click(this, new RoutedEventArgs()); };
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

        private void FileMenu_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (FileBackstage.Visibility == Visibility.Visible)
                HideBackstage();
            else
                ShowBackstage();

            e.Handled = true;
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
                    Editor.Document.LoadFromStream(TextSetOptions.FormatRtf, randAccStream);
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
            Application.Current.Exit();
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            Editor.Document.Selection.Expand(TextRangeUnit.Story);
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            if (Editor.RenderTransform is not ScaleTransform scaleTransform)
            {
                scaleTransform = new ScaleTransform { ScaleX = 1.0, ScaleY = 1.0 };
                Editor.RenderTransform = scaleTransform;
                Editor.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            scaleTransform.ScaleX += 0.1;
            scaleTransform.ScaleY += 0.1;
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            if (Editor.RenderTransform is not ScaleTransform scaleTransform)
            {
                scaleTransform = new ScaleTransform { ScaleX = 1.0, ScaleY = 1.0 };
                Editor.RenderTransform = scaleTransform;
                Editor.RenderTransformOrigin = new Point(0.5, 0.5);
            }
            if (scaleTransform.ScaleX > 0.2)
            {
                scaleTransform.ScaleX -= 0.1;
                scaleTransform.ScaleY -= 0.1;
            }
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

        private void TextColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                charFormatting.ForegroundColor = args.NewColor;
                selectedText.CharacterFormat = charFormatting;
            }
        }

        private void HighlightColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            ITextSelection selectedText = Editor.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                charFormatting.BackgroundColor = args.NewColor;
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
            }
        }
    }
}
