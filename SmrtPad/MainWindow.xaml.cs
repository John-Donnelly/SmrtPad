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

        private bool _isImageSelection;
        private double? _imageAspectRatio;

        private const string ImageTokenPrefix = "[IMG:";
        private const string ImageTokenSuffix = "]";
        private readonly Dictionary<string, HostedImage> _hostedImages = new();
        private HostedImage? _selectedHostedImage;

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

            Editor.SelectionChanged += Editor_SelectionChanged;
            Editor.TextChanged += Editor_TextChanged;

            FileBackstage.NewRequested += (s, e) => { HideBackstage(); New_Click(this, new RoutedEventArgs()); };
            FileBackstage.OpenRequested += (s, e) => { HideBackstage(); Open_Click(this, new RoutedEventArgs()); };
            FileBackstage.SaveRequested += (s, e) => { HideBackstage(); Save_Click(this, new RoutedEventArgs()); };
            FileBackstage.ExitRequested += (s, e) => { HideBackstage(); Exit_Click(this, new RoutedEventArgs()); };
        }

        private void Editor_TextChanged(object sender, RoutedEventArgs e)
        {
            // If a user deletes the placeholder token, remove the hosted image.
            // This is a lightweight consistency pass.
            Editor.Document.GetText(TextGetOptions.NoHidden, out var text);
            var existingIds = new HashSet<string>(StringComparer.Ordinal);

            var idx = 0;
            while (idx < text.Length)
            {
                var start = text.IndexOf(ImageTokenPrefix, idx, StringComparison.Ordinal);
                if (start < 0)
                    break;
                var end = text.IndexOf(ImageTokenSuffix, start + ImageTokenPrefix.Length, StringComparison.Ordinal);
                if (end < 0)
                    break;

                var id = text.Substring(start + ImageTokenPrefix.Length, end - (start + ImageTokenPrefix.Length));
                if (!string.IsNullOrWhiteSpace(id))
                    existingIds.Add(id);

                idx = end + 1;
            }

            var toRemove = _hostedImages.Keys.Where(id => !existingIds.Contains(id)).ToList();
            foreach (var id in toRemove)
                RemoveHostedImage(id, updateText: false);
        }

        private void Editor_SelectionChanged(object sender, RoutedEventArgs e)
        {
            var isImage = IsHostedImageSelected(out var imageId);
            if (_isImageSelection == isImage)
                return;

            _isImageSelection = isImage;
            UpdateImageControlsVisibility(isImage);

            if (isImage)
                PopulateImageControlsFromSelection(imageId);
            else
                SelectHostedImage(null);
        }

        private void UpdateImageControlsVisibility(bool visible)
        {
            var v = visible ? Visibility.Visible : Visibility.Collapsed;
            ImageControlsSeparator.Visibility = v;
            RemoveImageButton.Visibility = v;
            ImageRotateLeftButton.Visibility = v;
            ImageRotateRightButton.Visibility = v;
            ImageAlignLeftButton.Visibility = v;
            ImageAlignCenterButton.Visibility = v;
            ImageAlignRightButton.Visibility = v;
            ImageSetSizeButton.Visibility = v;
        }

        private bool IsHostedImageSelected(out string? imageId)
        {
            imageId = null;
            ITextSelection selection = Editor.Document.Selection;
            if (selection == null)
                return false;

            var text = selection.Text;
            if (string.IsNullOrEmpty(text))
                return false;

            // In this model, images are represented in the document as "[IMG:{id}]".
            var start = text.IndexOf(ImageTokenPrefix, StringComparison.Ordinal);
            if (start < 0)
                return false;
            var end = text.IndexOf(ImageTokenSuffix, start + ImageTokenPrefix.Length, StringComparison.Ordinal);
            if (end < 0)
                return false;

            imageId = text.Substring(start + ImageTokenPrefix.Length, end - (start + ImageTokenPrefix.Length));
            return !string.IsNullOrWhiteSpace(imageId) && _hostedImages.ContainsKey(imageId);
        }

        private void PopulateImageControlsFromSelection(string? imageId)
        {
            if (imageId is null || !_hostedImages.TryGetValue(imageId, out var hosted))
                return;

            SelectHostedImage(hosted);

            ImageWidthBox.Value = hosted.Width;
            ImageHeightBox.Value = hosted.Height;
            _imageAspectRatio = hosted.Height > 0 ? hosted.Width / hosted.Height : null;
        }

        private void SelectHostedImage(HostedImage? hosted)
        {
            if (_selectedHostedImage != null)
                _selectedHostedImage.SetSelected(false);

            _selectedHostedImage = hosted;
            if (_selectedHostedImage != null)
                _selectedHostedImage.SetSelected(true);
        }

        private void RemoveImage_Click(object sender, RoutedEventArgs e)
        {
            if (!_isImageSelection || _selectedHostedImage == null)
                return;

            RemoveHostedImage(_selectedHostedImage.Id, updateText: true);
        }

        private void RotateImageLeft_Click(object sender, RoutedEventArgs e) => RotateSelectedImage(-90);
        private void RotateImageRight_Click(object sender, RoutedEventArgs e) => RotateSelectedImage(90);

        private void RotateSelectedImage(int degrees)
        {
            if (!_isImageSelection || _selectedHostedImage == null)
                return;

            _selectedHostedImage.RotationDegrees = (_selectedHostedImage.RotationDegrees + degrees) % 360;
            _selectedHostedImage.ApplyTransforms();
        }

        private void ImageAlignLeft_Click(object sender, RoutedEventArgs e) => AlignImageParagraph(ParagraphAlignment.Left);
        private void ImageAlignCenter_Click(object sender, RoutedEventArgs e) => AlignImageParagraph(ParagraphAlignment.Center);
        private void ImageAlignRight_Click(object sender, RoutedEventArgs e) => AlignImageParagraph(ParagraphAlignment.Right);

        private void AlignImageParagraph(ParagraphAlignment alignment)
        {
            if (!_isImageSelection)
                return;

            ITextParagraphFormat paragraphFormatting = Editor.Document.Selection.ParagraphFormat;
            paragraphFormatting.Alignment = alignment;
            Editor.Document.Selection.ParagraphFormat = paragraphFormatting;
        }

        private void ImageLockAspectToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isImageSelection || _selectedHostedImage == null)
                return;

            _imageAspectRatio = ImageHeightBox.Value > 0 ? ImageWidthBox.Value / ImageHeightBox.Value : null;
        }

        private void ImageSizeBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (!_isImageSelection || _selectedHostedImage == null)
                return;

            if (ImageLockAspectToggle.IsOn && _imageAspectRatio is double ar)
            {
                if (sender == ImageWidthBox && ImageHeightBox.Value > 0)
                    ImageHeightBox.Value = Math.Max(1, ImageWidthBox.Value / ar);
                else if (sender == ImageHeightBox && ImageWidthBox.Value > 0)
                    ImageWidthBox.Value = Math.Max(1, ImageHeightBox.Value * ar);
            }

            _selectedHostedImage.Width = ImageWidthBox.Value;
            _selectedHostedImage.Height = ImageHeightBox.Value;
            _selectedHostedImage.ApplySize();
        }

        private static string NewImageId() => Guid.NewGuid().ToString("N");

        private async System.Threading.Tasks.Task AddHostedImageAsync(StorageFile file)
        {
            var id = NewImageId();

            var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            using (var stream = await file.OpenAsync(FileAccessMode.Read))
            {
                await bmp.SetSourceAsync(stream);
            }

            var image = new Microsoft.UI.Xaml.Controls.Image
            {
                Source = bmp,
                Stretch = Stretch.Uniform,
                Width = 320,
                Height = 200,
            };

            var border = new Border
            {
                Child = image,
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                CornerRadius = new CornerRadius(4)
            };

            var thumb = new Thumb
            {
                Width = border.Width,
                Height = border.Height,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent)
            };

            // Container used for hit testing + transforms
            var container = new Grid
            {
                Width = border.Width,
                Height = border.Height,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            container.Children.Add(border);

            // A resize handle in bottom-right
            var resizeHandle = new Thumb
            {
                Width = 14,
                Height = 14,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -7, -7),
                Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                BorderThickness = new Thickness(1)
            };
            container.Children.Add(resizeHandle);

            var hosted = new HostedImage(id, file.Path, container, border, resizeHandle);
            _hostedImages[id] = hosted;

            hosted.Tapped += (s, e) =>
            {
                SelectTokenInEditor(hosted.Id);
                SelectHostedImage(hosted);
                _isImageSelection = true;
                UpdateImageControlsVisibility(true);
                PopulateImageControlsFromSelection(hosted.Id);
            };

            hosted.DragDelta += (s, e) =>
            {
                Canvas.SetLeft(container, Canvas.GetLeft(container) + e.HorizontalChange);
                Canvas.SetTop(container, Canvas.GetTop(container) + e.VerticalChange);
            };

            hosted.ResizeDelta += (s, e) =>
            {
                var newW = Math.Max(10, container.Width + e.HorizontalChange);
                var newH = Math.Max(10, container.Height + e.VerticalChange);
                hosted.Width = newW;
                hosted.Height = newH;
                hosted.ApplySize();
                ImageWidthBox.Value = newW;
                ImageHeightBox.Value = newH;
            };

            // Initial position near the caret
            Canvas.SetLeft(container, 20);
            Canvas.SetTop(container, 20);
            AttachmentLayer.Children.Add(container);

            // Insert placeholder token into text
            Editor.Document.Selection.Text = $"{ImageTokenPrefix}{id}{ImageTokenSuffix}";

            ViewModel.UpdateStatus($"Inserted image {file.Name}");
        }

        private void RemoveHostedImage(string id, bool updateText)
        {
            if (!_hostedImages.TryGetValue(id, out var hosted))
                return;

            AttachmentLayer.Children.Remove(hosted.Container);
            _hostedImages.Remove(id);

            if (_selectedHostedImage?.Id == id)
                SelectHostedImage(null);

            if (updateText)
                RemoveTokenFromEditor(id);
        }

        private void RemoveTokenFromEditor(string id)
        {
            var token = $"{ImageTokenPrefix}{id}{ImageTokenSuffix}";
            Editor.Document.GetText(TextGetOptions.NoHidden, out var text);
            var idx = text.IndexOf(token, StringComparison.Ordinal);
            if (idx < 0)
                return;

            Editor.Document.Selection.SetRange(idx, idx + token.Length);
            Editor.Document.Selection.Text = string.Empty;
        }

        private void SelectTokenInEditor(string id)
        {
            var token = $"{ImageTokenPrefix}{id}{ImageTokenSuffix}";
            Editor.Document.GetText(TextGetOptions.NoHidden, out var text);
            var idx = text.IndexOf(token, StringComparison.Ordinal);
            if (idx < 0)
                return;

            Editor.Document.Selection.SetRange(idx, idx + token.Length);
        }

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
                await AddHostedImageAsync(file);
            }
        }

        private sealed class HostedImage
        {
            public string Id { get; }
            public string SourcePath { get; }
            public Grid Container { get; }
            private readonly Border _chrome;
            private readonly Thumb _resizeThumb;

            public double Width
            {
                get => Container.Width;
                set => Container.Width = value;
            }

            public double Height
            {
                get => Container.Height;
                set => Container.Height = value;
            }

            public int RotationDegrees { get; set; }

            public event TappedEventHandler? Tapped;
            public event DragDeltaEventHandler? DragDelta;
            public event DragDeltaEventHandler? ResizeDelta;

            public HostedImage(string id, string sourcePath, Grid container, Border chrome, Thumb resizeThumb)
            {
                Id = id;
                SourcePath = sourcePath;
                Container = container;
                _chrome = chrome;
                _resizeThumb = resizeThumb;

                Container.Tapped += (s, e) => Tapped?.Invoke(s, e);
                Container.PointerPressed += (s, e) =>
                {
                    // allow dragging by holding anywhere except resize handle
                    if (e.OriginalSource == _resizeThumb)
                        return;
                    Container.CapturePointer(e.Pointer);
                };

                var dragThumb = new Thumb { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent) };
                dragThumb.DragDelta += (s, e) => DragDelta?.Invoke(s, e);
                container.Children.Insert(0, dragThumb);

                _resizeThumb.DragDelta += (s, e) => ResizeDelta?.Invoke(s, e);

                ApplySize();
                ApplyTransforms();
            }

            public void ApplySize()
            {
                _chrome.Width = Container.Width;
                _chrome.Height = Container.Height;
            }

            public void ApplyTransforms()
            {
                Container.RenderTransform = new RotateTransform { Angle = RotationDegrees };
            }

            public void SetSelected(bool selected)
            {
                _chrome.BorderBrush = selected
                    ? new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
                    : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
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
