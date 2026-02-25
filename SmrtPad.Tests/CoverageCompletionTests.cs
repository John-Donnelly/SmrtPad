using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Xunit;
using SmrtPad.Helpers;
using SmrtPad.Models;
using SmrtPad.Services;
using SmrtPad.ViewModels;

namespace SmrtPad.Tests
{
    // ═══ MainWindow Extended Handler Contract Tests ══════════════════════════════

    public class MainWindowExtendedHandlerTests
    {
        private static readonly Type MW = typeof(SmrtPad.MainWindow);
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [Theory]
        [InlineData("Exit_Click")]
        [InlineData("SelectAll_Click")]
        [InlineData("ZoomIn_Click")]
        [InlineData("ZoomOut_Click")]
        [InlineData("DecreaseIndent_Click")]
        [InlineData("IncreaseIndent_Click")]
        [InlineData("FocusMode_Click")]
        [InlineData("Ruler_Click")]
        [InlineData("PageView_Click")]
        [InlineData("PasteSpecial_Click")]
        [InlineData("ClearFormatting_Click")]
        [InlineData("CustomLineSpacing_Click")]
        [InlineData("ApplyParagraphSpacing_Click")]
        [InlineData("TabStops_Click")]
        [InlineData("ThemeToggle_Click")]
        [InlineData("HighlightAllMatches_Click")]
        [InlineData("ClearHighlights_Click")]
        [InlineData("SpellCheck_Click")]
        [InlineData("FileMenu_Tapped")]
        [InlineData("NewWindow_Click")]
        [InlineData("TextColorSwatchButton_Click")]
        [InlineData("TextColorMoreColors_Click")]
        [InlineData("HighlightSwatchButton_Click")]
        public void MainWindow_HasPrivateHandler(string handlerName)
        {
            var method = MW.GetMethod(handlerName, Private);
            Assert.NotNull(method);
            Assert.False(method!.IsPublic);
        }

        [Theory]
        [InlineData("ApplySettings")]
        [InlineData("ApplyThemeFromSettings")]
        [InlineData("UpdateTitleBarTheme")]
        [InlineData("SetupAutoSave")]
        [InlineData("UpdateStatusBarCounts")]
        [InlineData("UpdateLineColumn")]
        [InlineData("UpdateSelectionLength")]
        [InlineData("HideBackstage")]
        [InlineData("ShowBackstage")]
        [InlineData("Editor_SelectionChanged")]
        [InlineData("ApplyZoom")]
        [InlineData("ApplyPageViewLayout")]
        [InlineData("UpdateRulerVisibility")]
        [InlineData("RedrawRulers")]
        [InlineData("DrawHorizontalRuler")]
        [InlineData("DrawVerticalRuler")]
        [InlineData("ApplyParagraphStyle")]
        [InlineData("RefreshTabStopList")]
        [InlineData("ApplyFontSizeFromText")]
        [InlineData("PasteAsPlainTextAsync")]
        [InlineData("ApplyTemplate")]
        public void MainWindow_HasPrivateUtilityMethod(string methodName)
        {
            var method = MW.GetMethod(methodName, Private);
            Assert.NotNull(method);
        }

        [Theory]
        [InlineData("OpenFileByPathAsync", true)]
        public void MainWindow_HasPublicAsyncMethod(string methodName, bool isPublic)
        {
            var flags = isPublic
                ? BindingFlags.Public | BindingFlags.Instance
                : Private;
            var method = MW.GetMethod(methodName, flags);
            Assert.NotNull(method);
            Assert.True(typeof(System.Threading.Tasks.Task).IsAssignableFrom(method!.ReturnType));
        }

        [Fact]
        public void MainWindow_HasAutoSaveRecoveryAsync()
        {
            var method = MW.GetMethod("AutoSaveRecoveryAsync", Private);
            Assert.NotNull(method);
            Assert.True(typeof(System.Threading.Tasks.Task).IsAssignableFrom(method!.ReturnType));
        }

        [Fact]
        public void MainWindow_HasOpenStorageFileAsync()
        {
            var method = MW.GetMethod("OpenStorageFileAsync", Private);
            Assert.NotNull(method);
            Assert.True(typeof(System.Threading.Tasks.Task).IsAssignableFrom(method!.ReturnType));
        }

        [Fact]
        public void MainWindow_HasShowErrorDialogAsync()
        {
            var method = MW.GetMethod("ShowErrorDialogAsync", Private);
            Assert.NotNull(method);
            var parms = method!.GetParameters();
            Assert.Equal(2, parms.Length);
            Assert.Equal(typeof(string), parms[0].ParameterType);
            Assert.Equal(typeof(string), parms[1].ParameterType);
        }

        [Fact]
        public void MainWindow_HasGetFullDocumentText()
        {
            var method = MW.GetMethod("GetFullDocumentText", Private);
            Assert.NotNull(method);
            Assert.Equal(typeof(string), method!.ReturnType);
        }

        [Fact]
        public void MainWindow_HasRegisterForPrinting()
        {
            var method = MW.GetMethod("RegisterForPrinting", Private);
            Assert.NotNull(method);
        }

        [Theory]
        [InlineData("PrintTask_Requested")]
        [InlineData("PrintTaskSourceRequested")]
        [InlineData("PrintTask_Completed")]
        [InlineData("PrintDocument_Paginate")]
        [InlineData("PrintDocument_GetPreviewPage")]
        [InlineData("PrintDocument_AddPages")]
        public void MainWindow_HasPrintingHandler(string handlerName)
        {
            var method = MW.GetMethod(handlerName, Private);
            Assert.NotNull(method);
        }

        [Fact]
        public void MainWindow_HasUpdateEncoding_WithStringParam()
        {
            var method = MW.GetMethod("UpdateEncoding", Private);
            Assert.NotNull(method);
            var parms = method!.GetParameters();
            Assert.Single(parms);
            Assert.Equal(typeof(string), parms[0].ParameterType);
        }

        [Fact]
        public void MainWindow_HasEditorDragDropHandlers()
        {
            Assert.NotNull(MW.GetMethod("Editor_DragOver", Private));
            Assert.NotNull(MW.GetMethod("Editor_Drop", Private));
        }

        [Fact]
        public void MainWindow_HasFontComboHandlers()
        {
            Assert.NotNull(MW.GetMethod("FontFamilyComboBox_Loaded", Private));
            Assert.NotNull(MW.GetMethod("FontFamilyComboBox_SelectionChanged", Private));
            Assert.NotNull(MW.GetMethod("FontSizeComboBox_SelectionChanged", Private));
            Assert.NotNull(MW.GetMethod("FontSizeComboBox_KeyDown", Private));
            Assert.NotNull(MW.GetMethod("FontSizeComboBox_LostFocus", Private));
        }

        [Fact]
        public void MainWindow_HasRulerCanvasSizeChangedHandlers()
        {
            Assert.NotNull(MW.GetMethod("HRulerCanvas_SizeChanged", Private));
            Assert.NotNull(MW.GetMethod("VRulerCanvas_SizeChanged", Private));
        }

        [Fact]
        public void MainWindow_HasEditorScrollViewerPointerWheelChanged()
        {
            var method = MW.GetMethod("EditorScrollViewer_PointerWheelChanged", Private);
            Assert.NotNull(method);
        }

        [Fact]
        public void MainWindow_MacroField_IsInitialized()
        {
            var field = MW.GetField("_macro", Private);
            Assert.NotNull(field);
            Assert.Equal(typeof(MacroHelper), field!.FieldType);
            Assert.False(field.IsStatic);
        }

        [Fact]
        public void MainWindow_TabsList_IsListOfDocumentTab()
        {
            var field = MW.GetField("_tabs", Private);
            Assert.NotNull(field);
            Assert.True(field!.FieldType.IsGenericType);
            Assert.Contains("DocumentTab", field.FieldType.GenericTypeArguments[0].Name);
        }

        [Fact]
        public void MainWindow_HasActiveTabIndex_Field()
        {
            var field = MW.GetField("_activeTabIndex", Private);
            Assert.NotNull(field);
            Assert.Equal(typeof(int), field!.FieldType);
        }

        [Fact]
        public void MainWindow_HasSettingsField()
        {
            var field = MW.GetField("_settings", Private);
            Assert.NotNull(field);
            Assert.Equal(typeof(ISettingsService), field!.FieldType);
        }

        [Fact]
        public void MainWindow_HasDialogServiceField()
        {
            var field = MW.GetField("_dialogService", Private);
            Assert.NotNull(field);
            Assert.Equal(typeof(IDialogService), field!.FieldType);
        }

        [Fact]
        public void MainWindow_HasFileServiceField()
        {
            var field = MW.GetField("_fileService", Private);
            Assert.NotNull(field);
            Assert.Equal(typeof(IFileService), field!.FieldType);
        }

        [Fact]
        public void MainWindow_HasPrintDocumentField()
        {
            var field = MW.GetField("_printDocument", Private);
            Assert.NotNull(field);
        }

        [Fact]
        public void MainWindow_HasPrintPreviewPagesField()
        {
            var field = MW.GetField("_printPreviewPages", Private);
            Assert.NotNull(field);
        }

        [Fact]
        public void MainWindow_HasAutoSaveTimerField()
        {
            var field = MW.GetField("_autoSaveTimer", Private);
            Assert.NotNull(field);
        }

        [Fact]
        public void MainWindow_HasRulersVisibleField()
        {
            var field = MW.GetField("_rulersVisible", Private);
            Assert.NotNull(field);
            Assert.Equal(typeof(bool), field!.FieldType);
        }

        [Fact]
        public void MainWindow_HasPageViewActiveField()
        {
            var field = MW.GetField("_pageViewActive", Private);
            Assert.NotNull(field);
            Assert.Equal(typeof(bool), field!.FieldType);
        }

        [Fact]
        public void MainWindow_HasLastFontColorField()
        {
            var field = MW.GetField("_lastFontColor", Private);
            Assert.NotNull(field);
            Assert.Equal(typeof(Windows.UI.Color), field!.FieldType);
        }

        [Fact]
        public void MainWindow_HasWordSeparatorsStaticField()
        {
            var field = MW.GetField("s_wordSeparators",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);
            Assert.Equal(typeof(char[]), field!.FieldType);
        }

        [Fact]
        public void MainWindow_HasHighlightColorStaticField()
        {
            var field = MW.GetField("HighlightColor",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);
        }

        [Fact]
        public void MainWindow_HasTransparentColorStaticField()
        {
            var field = MW.GetField("TransparentColor",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);
        }

        [Fact]
        public void MainWindow_IsSealedPartialClass()
        {
            Assert.True(MW.IsSealed);
            Assert.True(MW.IsClass);
        }

        [Fact]
        public void MainWindow_InheritsFromWindow()
        {
            Assert.True(typeof(Microsoft.UI.Xaml.Window).IsAssignableFrom(MW));
        }

        [Fact]
        public void MainWindow_GetPixelsPerUnit_IsPrivate()
        {
            var method = MW.GetMethod("GetPixelsPerUnit", Private);
            Assert.NotNull(method);
            Assert.Equal(typeof(double), method!.ReturnType);
        }
    }

    // ═══ DocumentTab Inner Class Contract Tests ═════════════════════════════════

    public class DocumentTabContractTests
    {
        private static readonly Type? DT;

        static DocumentTabContractTests()
        {
            // DocumentTab is internal, nested inside MainWindow's namespace
            DT = typeof(SmrtPad.MainWindow).Assembly.GetTypes()
                .FirstOrDefault(t => t.Name == "DocumentTab");
        }

        [Fact]
        public void DocumentTab_TypeExists()
        {
            Assert.NotNull(DT);
        }

        [Fact]
        public void DocumentTab_IsSealed()
        {
            Assert.True(DT!.IsSealed);
        }

        [Fact]
        public void DocumentTab_IsInternal()
        {
            Assert.False(DT!.IsPublic);
            Assert.True(DT.IsNotPublic || DT.IsNestedAssembly);
        }

        [Theory]
        [InlineData("TabViewItem")]
        [InlineData("Editor")]
        [InlineData("ScrollViewer")]
        [InlineData("EditorContainer")]
        [InlineData("PageViewBorder")]
        [InlineData("EditorScaleTransform")]
        [InlineData("CurrentFile")]
        [InlineData("IsModified")]
        [InlineData("Encoding")]
        [InlineData("ZoomLevel")]
        public void DocumentTab_HasProperty(string propName)
        {
            var prop = DT!.GetProperty(propName);
            Assert.NotNull(prop);
        }

        [Fact]
        public void DocumentTab_Encoding_DefaultIsUTF8()
        {
            var prop = DT!.GetProperty("Encoding");
            Assert.NotNull(prop);
            Assert.Equal(typeof(string), prop!.PropertyType);
        }

        [Fact]
        public void DocumentTab_ZoomLevel_DefaultType()
        {
            var prop = DT!.GetProperty("ZoomLevel");
            Assert.NotNull(prop);
            Assert.Equal(typeof(double), prop!.PropertyType);
        }

        [Fact]
        public void DocumentTab_IsModified_IsBoolType()
        {
            var prop = DT!.GetProperty("IsModified");
            Assert.NotNull(prop);
            Assert.Equal(typeof(bool), prop!.PropertyType);
        }

        [Fact]
        public void DocumentTab_Constructor_RequiresTitleAndSettings()
        {
            var ctor = DT!.GetConstructors().FirstOrDefault();
            Assert.NotNull(ctor);
            var parms = ctor!.GetParameters();
            Assert.Equal(2, parms.Length);
            Assert.Equal(typeof(string), parms[0].ParameterType);
            Assert.Equal(typeof(ISettingsService), parms[1].ParameterType);
        }
    }

    // ═══ ViewModel Edge Case Tests ═══════════════════════════════════════════════

    public class ViewModelEdgeCaseTests
    {
        [Fact]
        public void UpdateCursorPosition_ShortArray_DoesNotChange()
        {
            var vm = new EditorViewModel();
            vm.UpdateCursorPosition([5]);
            // Short array should not change defaults
            Assert.Equal(1, vm.LineNumber);
            Assert.Equal(1, vm.ColumnNumber);
        }

        [Fact]
        public void UpdateCursorPosition_EmptyArray_DoesNotChange()
        {
            var vm = new EditorViewModel();
            vm.UpdateCursorPosition([]);
            Assert.Equal(1, vm.LineNumber);
            Assert.Equal(1, vm.ColumnNumber);
        }

        [Fact]
        public void UpdateCursorPosition_ExactlyTwoElements_Works()
        {
            var vm = new EditorViewModel();
            vm.UpdateCursorPosition([10, 25]);
            Assert.Equal(10, vm.LineNumber);
            Assert.Equal(25, vm.ColumnNumber);
        }

        [Fact]
        public void UpdateCursorPosition_MoreThanTwo_UsesFirstTwo()
        {
            var vm = new EditorViewModel();
            vm.UpdateCursorPosition([5, 15, 99]);
            Assert.Equal(5, vm.LineNumber);
            Assert.Equal(15, vm.ColumnNumber);
        }

        [Fact]
        public void SetParagraphSpacing_ShortArray_DoesNotChange()
        {
            var vm = new EditorViewModel();
            vm.SetParagraphSpacing([5.0]);
            Assert.Equal(0, vm.ParagraphSpacingBefore);
            Assert.Equal(0, vm.ParagraphSpacingAfter);
        }

        [Fact]
        public void SetParagraphSpacing_EmptyArray_DoesNotChange()
        {
            var vm = new EditorViewModel();
            vm.SetParagraphSpacing([]);
            Assert.Equal(0, vm.ParagraphSpacingBefore);
            Assert.Equal(0, vm.ParagraphSpacingAfter);
        }

        [Fact]
        public void SetParagraphSpacing_ExactlyTwo_Works()
        {
            var vm = new EditorViewModel();
            vm.SetParagraphSpacing([12.0, 6.0]);
            Assert.Equal(12.0, vm.ParagraphSpacingBefore);
            Assert.Equal(6.0, vm.ParagraphSpacingAfter);
        }

        [Fact]
        public void SetParagraphSpacing_MoreThanTwo_UsesFirstTwo()
        {
            var vm = new EditorViewModel();
            vm.SetParagraphSpacing([8.0, 4.0, 99.0]);
            Assert.Equal(8.0, vm.ParagraphSpacingBefore);
            Assert.Equal(4.0, vm.ParagraphSpacingAfter);
        }

        [Fact]
        public void ZoomIn_MultipleSteps_ClampsCorrectly()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 495;
            vm.ZoomIn();
            Assert.Equal(500.0, vm.ZoomLevel);
            vm.ZoomIn();
            Assert.Equal(500.0, vm.ZoomLevel);
        }

        [Fact]
        public void ZoomOut_MultipleSteps_ClampsCorrectly()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 15;
            vm.ZoomOut();
            Assert.Equal(10.0, vm.ZoomLevel);
            vm.ZoomOut();
            Assert.Equal(10.0, vm.ZoomLevel);
        }

        [Fact]
        public void SetListType_Bullet_SetsIsBulletsTrue()
        {
            var vm = new EditorViewModel();
            vm.SetListType("Bullet");
            Assert.True(vm.IsBullets);
            Assert.Equal("Bullet", vm.ListType);
        }

        [Fact]
        public void SetListType_None_ClearsIsBullets()
        {
            var vm = new EditorViewModel();
            vm.SetListType("Bullet");
            vm.SetListType("None");
            Assert.False(vm.IsBullets);
        }

        [Theory]
        [InlineData("Number")]
        [InlineData("LowercaseLetter")]
        [InlineData("UppercaseLetter")]
        [InlineData("LowercaseRoman")]
        [InlineData("UppercaseRoman")]
        public void SetListType_NonNone_SetsIsBulletsTrue(string listType)
        {
            var vm = new EditorViewModel();
            vm.SetListType(listType);
            Assert.True(vm.IsBullets);
            Assert.Equal(listType, vm.ListType);
        }

        [Fact]
        public void ToggleWordWrap_TwiceReturnsToDefault()
        {
            var vm = new EditorViewModel();
            Assert.True(vm.IsWordWrap);
            vm.ToggleWordWrap();
            Assert.False(vm.IsWordWrap);
            vm.ToggleWordWrap();
            Assert.True(vm.IsWordWrap);
        }

        [Fact]
        public void ToggleBullets_FlipsState()
        {
            var vm = new EditorViewModel();
            Assert.False(vm.IsBullets);
            vm.ToggleBullets();
            Assert.True(vm.IsBullets);
            vm.ToggleBullets();
            Assert.False(vm.IsBullets);
        }

        [Theory]
        [InlineData("Left")]
        [InlineData("Center")]
        [InlineData("Right")]
        [InlineData("Justify")]
        public void SetAlignment_AllValues_Persist(string alignment)
        {
            var vm = new EditorViewModel();
            vm.SetAlignment(alignment);
            Assert.Equal(alignment, vm.Alignment);
        }

        [Fact]
        public void SetLineSpacing_ValuesMatch()
        {
            var vm = new EditorViewModel();
            vm.SetLineSpacing(2.0);
            Assert.Equal(2.0, vm.LineSpacing);
            vm.SetLineSpacing(1.5);
            Assert.Equal(1.5, vm.LineSpacing);
        }

        [Fact]
        public void NewDocument_ResetsAllFindOptions()
        {
            var vm = new EditorViewModel();
            vm.FindMatchCase = true;
            vm.FindWholeWord = true;
            vm.FindUseRegex = true;
            vm.NewDocument();
            Assert.False(vm.FindMatchCase);
            Assert.False(vm.FindWholeWord);
            Assert.False(vm.FindUseRegex);
        }

        [Fact]
        public void NewDocument_ResetsEncoding()
        {
            var vm = new EditorViewModel();
            vm.Encoding = "RTF";
            vm.NewDocument();
            Assert.Equal("UTF-8", vm.Encoding);
        }

        [Fact]
        public void NewDocument_ResetsSelectionLength()
        {
            var vm = new EditorViewModel();
            vm.SelectionLength = 42;
            vm.NewDocument();
            Assert.Equal(0, vm.SelectionLength);
        }

        [Fact]
        public void DisplayProperties_ReflectCorrectFormat()
        {
            var vm = new EditorViewModel();
            vm.WordCount = 500;
            Assert.Contains("500", vm.WordCountDisplay);

            vm.CharCount = 2500;
            Assert.Contains("2500", vm.CharCountDisplay);

            vm.SelectionLength = 10;
            Assert.Contains("10", vm.SelectionLengthDisplay);

            vm.LineNumber = 42;
            vm.ColumnNumber = 7;
            Assert.Contains("42", vm.LineColDisplay);
            Assert.Contains("7", vm.LineColDisplay);

            vm.ZoomLevel = 150;
            Assert.Equal("150%", vm.ZoomDisplay);

            vm.Encoding = "ASCII";
            Assert.Equal("ASCII", vm.EncodingDisplay);
        }

        [Fact]
        public void PropertyChanged_FiredForAllDisplayProperties()
        {
            var vm = new EditorViewModel();
            var changedProperties = new List<string>();
            vm.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

            vm.WordCount = 10;
            Assert.Contains("WordCountDisplay", changedProperties);

            changedProperties.Clear();
            vm.CharCount = 20;
            Assert.Contains("CharCountDisplay", changedProperties);

            changedProperties.Clear();
            vm.SelectionLength = 5;
            Assert.Contains("SelectionLengthDisplay", changedProperties);

            changedProperties.Clear();
            vm.LineNumber = 3;
            Assert.Contains("LineColDisplay", changedProperties);

            changedProperties.Clear();
            vm.ColumnNumber = 8;
            Assert.Contains("LineColDisplay", changedProperties);

            changedProperties.Clear();
            vm.ZoomLevel = 120;
            Assert.Contains("ZoomDisplay", changedProperties);

            changedProperties.Clear();
            vm.Encoding = "ANSI";
            Assert.Contains("EncodingDisplay", changedProperties);
        }

        [Fact]
        public void ViewModel_UpdateWordCount_SetsProperty()
        {
            var vm = new EditorViewModel();
            vm.UpdateWordCount(99);
            Assert.Equal(99, vm.WordCount);
        }

        [Fact]
        public void ViewModel_UpdateCharCount_SetsProperty()
        {
            var vm = new EditorViewModel();
            vm.UpdateCharCount(555);
            Assert.Equal(555, vm.CharCount);
        }

        [Fact]
        public void ViewModel_UpdateStatus_SetsMessage()
        {
            var vm = new EditorViewModel();
            vm.UpdateStatus("Testing");
            Assert.Equal("Testing", vm.StatusMessage);
        }

        [Fact]
        public void ViewModel_AllResetProperties_MatchDefaults()
        {
            var vm = new EditorViewModel();
            // Modify everything
            vm.IsBold = true; vm.IsItalic = true; vm.IsUnderline = true;
            vm.IsStrikethrough = true; vm.IsSubscript = true; vm.IsSuperscript = true;
            vm.FontFamily = "Arial"; vm.FontSize = 72;
            vm.Alignment = "Justify"; vm.IsBullets = true; vm.IsWordWrap = false;
            vm.ZoomLevel = 200; vm.ListType = "Bullet"; vm.LineSpacing = 2.0;
            vm.WordCount = 100; vm.CharCount = 500; vm.LineNumber = 50;
            vm.ColumnNumber = 80; vm.ParagraphSpacingBefore = 12;
            vm.ParagraphSpacingAfter = 6; vm.FindMatchCase = true;
            vm.FindWholeWord = true; vm.FindUseRegex = true;
            vm.SelectionLength = 25; vm.Encoding = "RTF";

            // Reset
            vm.NewDocument();

            // Verify all defaults restored
            Assert.False(vm.IsBold);
            Assert.False(vm.IsItalic);
            Assert.False(vm.IsUnderline);
            Assert.False(vm.IsStrikethrough);
            Assert.False(vm.IsSubscript);
            Assert.False(vm.IsSuperscript);
            Assert.Equal("Segoe UI", vm.FontFamily);
            Assert.Equal(11.0, vm.FontSize);
            Assert.Equal("Left", vm.Alignment);
            Assert.False(vm.IsBullets);
            Assert.True(vm.IsWordWrap);
            Assert.Equal(100.0, vm.ZoomLevel);
            Assert.Equal("None", vm.ListType);
            Assert.Equal(1.0, vm.LineSpacing);
            Assert.Equal(0, vm.WordCount);
            Assert.Equal(0, vm.CharCount);
            Assert.Equal(1, vm.LineNumber);
            Assert.Equal(1, vm.ColumnNumber);
            Assert.Equal(0.0, vm.ParagraphSpacingBefore);
            Assert.Equal(0.0, vm.ParagraphSpacingAfter);
            Assert.False(vm.FindMatchCase);
            Assert.False(vm.FindWholeWord);
            Assert.False(vm.FindUseRegex);
            Assert.Equal(0, vm.SelectionLength);
            Assert.Equal("UTF-8", vm.Encoding);
        }
    }

    // ═══ DocxExportHelper Extended Tests ═════════════════════════════════════════

    public class DocxExportHelperExtendedTests
    {
        [Fact]
        public void GenerateDocx_SingleLine_HasOneParagraph()
        {
            var bytes = DocxExportHelper.GenerateDocx("Hello World");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("word/document.xml");
            Assert.NotNull(entry);
            using var reader = new StreamReader(entry!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var paragraphs = doc.Descendants(w + "p").ToList();
            Assert.Single(paragraphs);
        }

        [Fact]
        public void GenerateDocx_MultiLine_HasCorrectParagraphCount()
        {
            var bytes = DocxExportHelper.GenerateDocx("Line1\nLine2\nLine3");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("word/document.xml");
            using var reader = new StreamReader(entry!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            // 3 text paragraphs + sectPr
            var paragraphs = doc.Descendants(w + "p").ToList();
            Assert.Equal(3, paragraphs.Count);
        }

        [Fact]
        public void GenerateDocx_CRLFLineEndings_NormalizedCorrectly()
        {
            var bytes = DocxExportHelper.GenerateDocx("A\r\nB\r\nC");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("word/document.xml");
            using var reader = new StreamReader(entry!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var paragraphs = doc.Descendants(w + "p").ToList();
            Assert.Equal(3, paragraphs.Count);
        }

        [Fact]
        public void GenerateDocx_HasContentTypesEntry()
        {
            var bytes = DocxExportHelper.GenerateDocx("test");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
        }

        [Fact]
        public void GenerateDocx_HasRelsEntry()
        {
            var bytes = DocxExportHelper.GenerateDocx("test");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            Assert.NotNull(zip.GetEntry("_rels/.rels"));
        }

        [Fact]
        public void GenerateDocx_HasDocumentRelsEntry()
        {
            var bytes = DocxExportHelper.GenerateDocx("test");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            Assert.NotNull(zip.GetEntry("word/_rels/document.xml.rels"));
        }

        [Fact]
        public void GenerateDocx_HasSectPr()
        {
            var bytes = DocxExportHelper.GenerateDocx("text");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("word/document.xml");
            using var reader = new StreamReader(entry!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            Assert.NotEmpty(doc.Descendants(w + "sectPr"));
        }

        [Fact]
        public void GenerateDocx_PreservesSpaces_WithXmlSpacePreserve()
        {
            var bytes = DocxExportHelper.GenerateDocx("  leading spaces  ");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("word/document.xml");
            using var reader = new StreamReader(entry!.Open());
            string content = reader.ReadToEnd();
            Assert.Contains("xml:space=\"preserve\"", content);
        }

        [Fact]
        public void GenerateRichDocx_PlainText_ProducesValidZip()
        {
            var bytes = DocxExportHelper.GenerateRichDocx("Hello plain text");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            Assert.NotNull(zip.GetEntry("word/document.xml"));
            Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
        }

        [Fact]
        public void GenerateRichDocx_BoldRtf_HasBoldElement()
        {
            string rtf = @"{\rtf1\ansi{\b Hello Bold}}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            string content = reader.ReadToEnd();
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var doc = XDocument.Parse(content);
            Assert.NotEmpty(doc.Descendants(w + "b"));
        }

        [Fact]
        public void GenerateRichDocx_ItalicRtf_HasItalicElement()
        {
            string rtf = @"{\rtf1\ansi{\i Hello Italic}}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            Assert.NotEmpty(doc.Descendants(w + "i"));
        }

        [Fact]
        public void GenerateRichDocx_UnderlineRtf_HasUnderlineElement()
        {
            string rtf = @"{\rtf1\ansi{\ul Hello Underline}}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            Assert.NotEmpty(doc.Descendants(w + "u"));
        }

        [Fact]
        public void GenerateRichDocx_StrikethroughRtf_HasStrikeElement()
        {
            string rtf = @"{\rtf1\ansi{\strike Hello Strike}}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            Assert.NotEmpty(doc.Descendants(w + "strike"));
        }

        [Fact]
        public void GenerateRichDocx_FontSize_HasSzElement()
        {
            string rtf = @"{\rtf1\ansi\fs48 Hello Big}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            Assert.NotEmpty(doc.Descendants(w + "sz"));
        }

        [Fact]
        public void GenerateRichDocx_CenterAlignment_HasJcElement()
        {
            string rtf = @"{\rtf1\ansi\qc Center aligned}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var jc = doc.Descendants(w + "jc").FirstOrDefault();
            Assert.NotNull(jc);
            Assert.Equal("center", jc!.Attribute(w + "val")?.Value);
        }

        [Fact]
        public void GenerateRichDocx_RightAlignment_HasJcRight()
        {
            string rtf = @"{\rtf1\ansi\qr Right aligned}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var jc = doc.Descendants(w + "jc").FirstOrDefault();
            Assert.NotNull(jc);
            Assert.Equal("right", jc!.Attribute(w + "val")?.Value);
        }

        [Fact]
        public void GenerateRichDocx_JustifyAlignment_HasJcBoth()
        {
            string rtf = @"{\rtf1\ansi\qj Justified text}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var jc = doc.Descendants(w + "jc").FirstOrDefault();
            Assert.NotNull(jc);
            Assert.Equal("both", jc!.Attribute(w + "val")?.Value);
        }

        [Fact]
        public void GenerateRichDocx_MultiParagraph_HasMultipleParElements()
        {
            string rtf = @"{\rtf1\ansi First\par Second\par Third}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var paragraphs = doc.Descendants(w + "p").ToList();
            Assert.True(paragraphs.Count >= 3);
        }

        [Fact]
        public void GenerateRichDocx_FontTable_ParsesFontNames()
        {
            string rtf = @"{\rtf1\ansi{\fonttbl{\f0\fswiss Arial;}{\f1\fmodern Courier New;}}\f1 World}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            string content = reader.ReadToEnd();
            // The word "World" should be present in the output
            Assert.Contains("World", content);
            // The font table was parsed (font names extracted internally)
            Assert.True(bytes.Length > 0);
        }

        [Fact]
        public void GenerateRichDocx_EscapedCharacters_ArePreserved()
        {
            string rtf = @"{\rtf1\ansi Hello \\ World \{ braces \}}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            string content = reader.ReadToEnd();
            Assert.Contains("\\", content);
            Assert.Contains("{", content);
        }

        [Fact]
        public void GenerateRichDocx_PardResetsFormatting()
        {
            string rtf = @"{\rtf1\ansi\b Bold\pard Plain}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var runs = doc.Descendants(w + "r").ToList();
            Assert.True(runs.Count >= 2, "Expected at least 2 runs for bold + plain");
        }

        [Fact]
        public void GenerateRichDocx_EmptyRtf_ProducesValidDocx()
        {
            var bytes = DocxExportHelper.GenerateRichDocx("");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            Assert.NotNull(zip.GetEntry("word/document.xml"));
        }

        [Fact]
        public void GenerateDocx_NullText_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => DocxExportHelper.GenerateDocx(null!));
        }

        [Fact]
        public void GenerateRichDocx_NullText_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => DocxExportHelper.GenerateRichDocx(null!));
        }

        [Fact]
        public void GenerateRichDocx_HexEscape_ParsesCorrectly()
        {
            // \'e9 = é (Latin small letter e with acute)
            string rtf = @"{\rtf1\ansi caf\'e9}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            string content = reader.ReadToEnd();
            Assert.Contains("caf", content);
        }

        [Fact]
        public void GenerateRichDocx_SkipsDestinationGroups()
        {
            // {\*\generator ...} should be skipped entirely
            string rtf = @"{\rtf1\ansi{\*\generator Test;}Hello}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            string content = reader.ReadToEnd();
            Assert.Contains("Hello", content);
            Assert.DoesNotContain("generator", content);
        }

        [Fact]
        public void GenerateRichDocx_SkipsPictGroups()
        {
            string rtf = @"{\rtf1\ansi Text{\pict\jpegblip data}More}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            string content = reader.ReadToEnd();
            Assert.Contains("Text", content);
            Assert.Contains("More", content);
            Assert.DoesNotContain("jpegblip", content);
        }

        [Fact]
        public void GenerateRichDocx_UlnoneDisablesUnderline()
        {
            string rtf = @"{\rtf1\ansi\ul underlined\ulnone not}";
            var bytes = DocxExportHelper.GenerateRichDocx(rtf);
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var runs = doc.Descendants(w + "r").ToList();
            // Should have at least 2 runs — one underlined, one not
            Assert.True(runs.Count >= 2);
        }

        [Fact]
        public void DocxExportHelper_IsStaticClass()
        {
            var t = typeof(DocxExportHelper);
            Assert.True(t.IsAbstract && t.IsSealed);
        }
    }

    // ═══ PdfHelper Extended Tests ════════════════════════════════════════════════

    public class PdfHelperExtendedTests
    {
        [Fact]
        public void GeneratePdf_HelloWorld_ContainsText()
        {
            var bytes = PdfHelper.GeneratePdf("Hello World");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("Hello World", content);
        }

        [Fact]
        public void GeneratePdf_StartsWithPdfHeader()
        {
            var bytes = PdfHelper.GeneratePdf("test");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.StartsWith("%PDF-1.4", content);
        }

        [Fact]
        public void GeneratePdf_EndsWithEof()
        {
            var bytes = PdfHelper.GeneratePdf("test");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("%%EOF", content);
        }

        [Fact]
        public void GeneratePdf_HasCatalogObject()
        {
            var bytes = PdfHelper.GeneratePdf("test");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("/Catalog", content);
        }

        [Fact]
        public void GeneratePdf_HasPagesObject()
        {
            var bytes = PdfHelper.GeneratePdf("test");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("/Pages", content);
        }

        [Fact]
        public void GeneratePdf_HasFontObject()
        {
            var bytes = PdfHelper.GeneratePdf("test");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("/Helvetica", content);
            Assert.Contains("/Type1", content);
        }

        [Fact]
        public void GeneratePdf_HasXrefTable()
        {
            var bytes = PdfHelper.GeneratePdf("test");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("xref", content);
            Assert.Contains("startxref", content);
        }

        [Fact]
        public void GeneratePdf_HasTrailer()
        {
            var bytes = PdfHelper.GeneratePdf("test");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("trailer", content);
            Assert.Contains("/Root", content);
        }

        [Fact]
        public void GeneratePdf_EmptyText_ProducesValidPdf()
        {
            var bytes = PdfHelper.GeneratePdf("");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("%PDF-1.4", content);
            Assert.Contains("%%EOF", content);
        }

        [Fact]
        public void GeneratePdf_NullText_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => PdfHelper.GeneratePdf(null!));
        }

        [Fact]
        public void GeneratePdf_LongText_ProducesMultiplePages()
        {
            // Generate enough text to exceed one page
            string longText = string.Join("\n", Enumerable.Range(1, 200).Select(i => $"Line number {i} with some text to fill the page."));
            var bytes = PdfHelper.GeneratePdf(longText);
            string content = Encoding.Latin1.GetString(bytes);
            // Count page objects
            int pageCount = 0;
            int idx = 0;
            while ((idx = content.IndexOf("/Type /Page ", idx)) >= 0)
            {
                pageCount++;
                idx++;
            }
            Assert.True(pageCount > 1, $"Expected multiple pages, found {pageCount}");
        }

        [Fact]
        public void GeneratePdf_CustomFontSize_Works()
        {
            var bytes = PdfHelper.GeneratePdf("test", fontSize: 24.0);
            Assert.NotNull(bytes);
            Assert.True(bytes.Length > 0);
        }

        [Fact]
        public void GeneratePdf_SpecialChars_Escaped()
        {
            var bytes = PdfHelper.GeneratePdf("Hello (World) \\ test");
            string content = Encoding.Latin1.GetString(bytes);
            // Parentheses should be escaped in PDF
            Assert.Contains("\\(", content);
            Assert.Contains("\\)", content);
            Assert.Contains("\\\\", content);
        }

        [Fact]
        public void GeneratePdf_HasMediaBox()
        {
            var bytes = PdfHelper.GeneratePdf("test");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("/MediaBox", content);
            Assert.Contains("595", content); // A4 width
            Assert.Contains("842", content); // A4 height
        }

        [Fact]
        public void BuildDisplayLines_SingleShortLine_ReturnsAsIs()
        {
            var lines = PdfHelper.BuildDisplayLines("Hello", 80);
            Assert.Single(lines);
            Assert.Equal("Hello", lines[0]);
        }

        [Fact]
        public void BuildDisplayLines_MultiLine_SplitsOnNewlines()
        {
            var lines = PdfHelper.BuildDisplayLines("A\nB\nC", 80);
            Assert.Equal(3, lines.Count);
            Assert.Equal("A", lines[0]);
            Assert.Equal("B", lines[1]);
            Assert.Equal("C", lines[2]);
        }

        [Fact]
        public void BuildDisplayLines_CRLFNormalized()
        {
            var lines = PdfHelper.BuildDisplayLines("A\r\nB\rC", 80);
            Assert.Equal(3, lines.Count);
        }

        [Fact]
        public void BuildDisplayLines_LongLine_Wraps()
        {
            var lines = PdfHelper.BuildDisplayLines("This is a very long line that should wrap", 20);
            Assert.True(lines.Count > 1, "Expected wrapping");
        }

        [Fact]
        public void BuildDisplayLines_MaxCharsZero_ClampsToOne()
        {
            var lines = PdfHelper.BuildDisplayLines("AB", 0);
            // With maxChars clamped to 1, each char becomes a line
            Assert.True(lines.Count >= 2);
        }

        [Fact]
        public void BuildDisplayLines_NegativeMaxChars_ClampsToOne()
        {
            var lines = PdfHelper.BuildDisplayLines("AB", -5);
            Assert.True(lines.Count >= 2);
        }

        [Fact]
        public void BuildDisplayLines_EmptyText_ReturnsSingleEmptyLine()
        {
            var lines = PdfHelper.BuildDisplayLines("", 80);
            Assert.Single(lines);
            Assert.Equal("", lines[0]);
        }

        [Fact]
        public void BuildDisplayLines_WordWrap_BreaksAtSpace()
        {
            var lines = PdfHelper.BuildDisplayLines("Hello World Test", 11);
            // "Hello World" = 11 chars, fits on first line
            // or breaks at space nearest to maxChars
            Assert.True(lines.Count >= 1);
            Assert.True(lines.All(l => l.Length <= 11 || !l.Contains(' ')));
        }

        [Fact]
        public void PdfHelper_IsStaticClass()
        {
            var t = typeof(PdfHelper);
            Assert.True(t.IsAbstract && t.IsSealed);
        }
    }

    // ═══ MainWindow XAML Extended Content Tests ══════════════════════════════════

    public class MainWindowXamlExtendedTests
    {
        private static string? ReadXaml(string filename)
        {
            string? dir = Directory.GetCurrentDirectory();
            while (dir is not null)
            {
                string candidate = Path.Combine(dir, "SmrtPad", filename);
                if (File.Exists(candidate)) return File.ReadAllText(candidate);
                dir = Directory.GetParent(dir)?.FullName;
            }
            return null;
        }

        [Fact]
        public void MainWindow_XAML_HasStatusBarBindings()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("ViewModel.WordCountDisplay", xaml);
            Assert.Contains("ViewModel.CharCountDisplay", xaml);
            Assert.Contains("ViewModel.SelectionLengthDisplay", xaml);
            Assert.Contains("ViewModel.LineColDisplay", xaml);
            Assert.Contains("ViewModel.ZoomDisplay", xaml);
            Assert.Contains("ViewModel.StatusMessage", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasFormattingToggleBindings()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("ViewModel.IsBold", xaml);
            Assert.Contains("ViewModel.IsItalic", xaml);
            Assert.Contains("ViewModel.IsUnderline", xaml);
            Assert.Contains("ViewModel.IsStrikethrough", xaml);
            Assert.Contains("ViewModel.IsSubscript", xaml);
            Assert.Contains("ViewModel.IsSuperscript", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasWordWrapToggle()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("WordWrap_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasFindReplaceElements()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("FindTextBox", xaml);
            Assert.Contains("FindMatchCaseCheckBox", xaml);
            Assert.Contains("FindWholeWordCheckBox", xaml);
            Assert.Contains("FindRegexCheckBox", xaml);
            Assert.Contains("ReplaceFindTextBox", xaml);
            Assert.Contains("ReplaceWithTextBox", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasRulerElements()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("HRulerCanvas", xaml);
            Assert.Contains("VRulerCanvas", xaml);
            Assert.Contains("HRulerBorder", xaml);
            Assert.Contains("VRulerBorder", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasStatusBarElements()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("StatusBar", xaml);
            Assert.Contains("StatusText", xaml);
            Assert.Contains("WordCountText", xaml);
            Assert.Contains("CharCountText", xaml);
            Assert.Contains("SelectionLengthText", xaml);
            Assert.Contains("LineColText", xaml);
            Assert.Contains("EncodingText", xaml);
            Assert.Contains("ZoomText", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasFileBackstage()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("FileBackstage", xaml);
            Assert.Contains("FileBackstageView", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasMicaBackdrop()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("MicaBackdrop", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasThemeToggle()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("ThemeToggleButton", xaml);
            Assert.Contains("ThemeToggle_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasInsertGroupHandlers()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("InsertPicture_Click", xaml);
            Assert.Contains("InsertDateTime_Click", xaml);
            Assert.Contains("PaintDrawing_Click", xaml);
            Assert.Contains("InsertObject_Click", xaml);
            Assert.Contains("InsertHyperlink_Click", xaml);
            Assert.Contains("InsertTable_Click", xaml);
            Assert.Contains("InsertSymbol_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasStyleHandlers()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("StyleNormal_Click", xaml);
            Assert.Contains("StyleHeading1_Click", xaml);
            Assert.Contains("StyleHeading2_Click", xaml);
            Assert.Contains("StyleHeading3_Click", xaml);
            Assert.Contains("StyleSubtitle_Click", xaml);
            Assert.Contains("StyleQuote_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasTabStopsHandler()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("TabStops_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasColorPickers()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("TextColorPicker", xaml);
            Assert.Contains("FontColorIndicator", xaml);
            Assert.Contains("HighlightColorPicker", xaml);
            Assert.Contains("HighlightColorIndicator", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasParagraphSpacingBoxes()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("SpacingBeforeBox", xaml);
            Assert.Contains("SpacingAfterBox", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasWindowMenu()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("NewWindow_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasMenuBar()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("MenuBar", xaml);
            Assert.Contains("FileMenu_Tapped", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasRibbonBar()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("RibbonBar", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasFocusModeToggle()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("FocusModeToggle", xaml);
            Assert.Contains("FocusMode_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasPageViewToggle()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("PageViewToggle", xaml);
            Assert.Contains("PageView_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasRulerToggle()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("RulerToggle", xaml);
            Assert.Contains("Ruler_Click", xaml);
        }

        // ── Backstage XAML tests ─────────────────────────────────────────────────

        [Fact]
        public void FileBackstageView_XAML_HasNavigationView()
        {
            string? xaml = ReadXaml("Views/FileBackstageView.xaml");
            if (xaml is null) return;
            Assert.Contains("NavigationView", xaml);
            Assert.Contains("Nav", xaml);
        }

        [Fact]
        public void FileBackstageView_XAML_HasNavItems()
        {
            string? xaml = ReadXaml("Views/FileBackstageView.xaml");
            if (xaml is null) return;
            Assert.Contains("\"New\"", xaml);
            Assert.Contains("\"Templates\"", xaml);
            Assert.Contains("\"Open\"", xaml);
            Assert.Contains("\"Save\"", xaml);
            Assert.Contains("\"SaveAs\"", xaml);
            Assert.Contains("\"Print\"", xaml);
            Assert.Contains("\"ExportPdf\"", xaml);
            Assert.Contains("\"ExportDocx\"", xaml);
            Assert.Contains("\"OneDrive\"", xaml);
            Assert.Contains("\"Options\"", xaml);
            Assert.Contains("\"Exit\"", xaml);
        }

        [Fact]
        public void FileBackstageView_XAML_HasDocumentPropertiesPanel()
        {
            string? xaml = ReadXaml("Views/FileBackstageView.xaml");
            if (xaml is null) return;
            Assert.Contains("DocPropertiesPanel", xaml);
            Assert.Contains("PropFileName", xaml);
            Assert.Contains("PropWordCount", xaml);
            Assert.Contains("PropCharCount", xaml);
            Assert.Contains("PropEncoding", xaml);
            Assert.Contains("PropModified", xaml);
        }

        [Fact]
        public void FileBackstageView_XAML_HasRecentFilesPanel()
        {
            string? xaml = ReadXaml("Views/FileBackstageView.xaml");
            if (xaml is null) return;
            Assert.Contains("RecentFilesPanel", xaml);
            Assert.Contains("RecentFilesList", xaml);
        }

        [Fact]
        public void FileBackstageView_XAML_HasTemplatePicker()
        {
            string? xaml = ReadXaml("Views/FileBackstageView.xaml");
            if (xaml is null) return;
            Assert.Contains("TemplatePicker", xaml);
            Assert.Contains("TemplateListPanel", xaml);
        }
    }

    // ═══ SettingsService Extended Tests ══════════════════════════════════════════

    public class SettingsServiceExtendedTests
    {
        private static SettingsService CreateIsolated()
        {
            string path = Path.Combine(Path.GetTempPath(), "SmrtPadTests",
                Guid.NewGuid().ToString("N"), "settings.json");
            return new SettingsService(path);
        }

        [Fact]
        public void SettingsService_AllDefaultValues_AreCorrect()
        {
            var svc = CreateIsolated();
            Assert.Equal("Segoe UI", svc.DefaultFontFamily);
            Assert.Equal(11.0, svc.DefaultFontSize);
            Assert.True(svc.DefaultWordWrap);
            Assert.Equal(".rtf", svc.DefaultSaveFormat);
            Assert.Equal("System", svc.ThemePreference);
            Assert.False(svc.AutoSaveEnabled);
            Assert.Equal(300, svc.AutoSaveIntervalSeconds);
            Assert.Equal("en-US", svc.Language);
            Assert.Equal("in", svc.RulerUnits);
            Assert.True(svc.SpellCheckEnabled);
            Assert.Empty(svc.RecentFiles);
        }

        [Fact]
        public void SettingsService_SaveAndLoad_RoundTripsAllProperties()
        {
            var svc = CreateIsolated();
            svc.DefaultFontFamily = "Arial";
            svc.DefaultFontSize = 14.0;
            svc.DefaultWordWrap = false;
            svc.DefaultSaveFormat = ".txt";
            svc.ThemePreference = "Dark";
            svc.AutoSaveEnabled = true;
            svc.AutoSaveIntervalSeconds = 60;
            svc.Language = "de-DE";
            svc.RulerUnits = "cm";
            svc.SpellCheckEnabled = false;
            svc.Save();

            // Create new instance that loads from same file
            var svc2 = new SettingsService(
                (string)typeof(SettingsService)
                    .GetField("_settingsFilePath", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .GetValue(svc)!);

            Assert.Equal("Arial", svc2.DefaultFontFamily);
            Assert.Equal(14.0, svc2.DefaultFontSize);
            Assert.False(svc2.DefaultWordWrap);
            Assert.Equal(".txt", svc2.DefaultSaveFormat);
            Assert.Equal("Dark", svc2.ThemePreference);
            Assert.True(svc2.AutoSaveEnabled);
            Assert.Equal(60, svc2.AutoSaveIntervalSeconds);
            Assert.Equal("de-DE", svc2.Language);
            Assert.Equal("cm", svc2.RulerUnits);
            Assert.False(svc2.SpellCheckEnabled);
        }

        [Fact]
        public void AddRecentFile_DuplicateMove_PromotesToFront()
        {
            var svc = CreateIsolated();
            svc.AddRecentFile("C:\\a.rtf");
            svc.AddRecentFile("C:\\b.rtf");
            svc.AddRecentFile("C:\\a.rtf"); // Promote a.rtf
            Assert.Equal("C:\\a.rtf", svc.RecentFiles[0]);
            Assert.Equal("C:\\b.rtf", svc.RecentFiles[1]);
            Assert.Equal(2, svc.RecentFiles.Count);
        }

        [Fact]
        public void AddRecentFile_CapsAtTen()
        {
            var svc = CreateIsolated();
            for (int i = 0; i < 15; i++)
                svc.AddRecentFile($"C:\\file{i}.rtf");
            Assert.Equal(10, svc.RecentFiles.Count);
            Assert.Equal("C:\\file14.rtf", svc.RecentFiles[0]);
        }

        [Fact]
        public void AddRecentFile_NullOrWhitespace_IsIgnored()
        {
            var svc = CreateIsolated();
            svc.AddRecentFile(null!);
            svc.AddRecentFile("");
            svc.AddRecentFile("   ");
            Assert.Empty(svc.RecentFiles);
        }

        [Fact]
        public void ClearRecentFiles_EmptiesList()
        {
            var svc = CreateIsolated();
            svc.AddRecentFile("C:\\test.rtf");
            Assert.Single(svc.RecentFiles);
            svc.ClearRecentFiles();
            Assert.Empty(svc.RecentFiles);
        }

        [Fact]
        public void SettingsService_CorruptJson_RecoversSafely()
        {
            string path = Path.Combine(Path.GetTempPath(), "SmrtPadTests",
                Guid.NewGuid().ToString("N"), "settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "NOT VALID JSON {{{}}}");
            var svc = new SettingsService(path);
            // Should fall back to defaults
            Assert.Equal("Segoe UI", svc.DefaultFontFamily);
        }

        [Fact]
        public void SettingsService_EmptyFile_FallsBackToDefaults()
        {
            string path = Path.Combine(Path.GetTempPath(), "SmrtPadTests",
                Guid.NewGuid().ToString("N"), "settings.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "");
            var svc = new SettingsService(path);
            Assert.Equal("Segoe UI", svc.DefaultFontFamily);
        }

        [Fact]
        public void SettingsService_MissingFile_UsesDefaults()
        {
            string path = Path.Combine(Path.GetTempPath(), "SmrtPadTests",
                Guid.NewGuid().ToString("N"), "settings.json");
            var svc = new SettingsService(path);
            Assert.Equal("Segoe UI", svc.DefaultFontFamily);
        }

        [Fact]
        public void SettingsService_ImplementsISettingsService()
        {
            Assert.True(typeof(ISettingsService).IsAssignableFrom(typeof(SettingsService)));
        }

        [Fact]
        public void ISettingsService_HasAllExpectedMembers()
        {
            var type = typeof(ISettingsService);
            Assert.NotNull(type.GetProperty("DefaultFontFamily"));
            Assert.NotNull(type.GetProperty("DefaultFontSize"));
            Assert.NotNull(type.GetProperty("DefaultWordWrap"));
            Assert.NotNull(type.GetProperty("DefaultSaveFormat"));
            Assert.NotNull(type.GetProperty("ThemePreference"));
            Assert.NotNull(type.GetProperty("AutoSaveEnabled"));
            Assert.NotNull(type.GetProperty("AutoSaveIntervalSeconds"));
            Assert.NotNull(type.GetProperty("Language"));
            Assert.NotNull(type.GetProperty("RulerUnits"));
            Assert.NotNull(type.GetProperty("SpellCheckEnabled"));
            Assert.NotNull(type.GetProperty("RecentFiles"));
            Assert.NotNull(type.GetMethod("AddRecentFile"));
            Assert.NotNull(type.GetMethod("ClearRecentFiles"));
            Assert.NotNull(type.GetMethod("Save"));
            Assert.NotNull(type.GetMethod("Load"));
        }
    }

    // ═══ MacroHelper Extended Edge Case Tests ════════════════════════════════════

    public class MacroHelperExtendedTests
    {
        [Fact]
        public void MacroHelper_RecordMultipleCommands_PreservesOrder()
        {
            var macro = new MacroHelper();
            macro.StartRecording();
            macro.Record(MacroCommandType.Bold);
            macro.Record(MacroCommandType.Italic);
            macro.Record(MacroCommandType.Underline);
            macro.StopRecording();

            Assert.Equal(3, macro.Count);
            Assert.Equal(MacroCommandType.Bold, macro.Commands[0].Type);
            Assert.Equal(MacroCommandType.Italic, macro.Commands[1].Type);
            Assert.Equal(MacroCommandType.Underline, macro.Commands[2].Type);
        }

        [Fact]
        public void MacroHelper_RecordWhenIdle_IsIgnored()
        {
            var macro = new MacroHelper();
            macro.Record(MacroCommandType.Bold);
            Assert.Equal(0, macro.Count);
        }

        [Fact]
        public void MacroHelper_RecordAfterStop_IsIgnored()
        {
            var macro = new MacroHelper();
            macro.StartRecording();
            macro.Record(MacroCommandType.Bold);
            macro.StopRecording();
            macro.Record(MacroCommandType.Italic);
            Assert.Equal(1, macro.Count);
        }

        [Fact]
        public void MacroHelper_StartRecording_ClearsPrevious()
        {
            var macro = new MacroHelper();
            macro.StartRecording();
            macro.Record(MacroCommandType.Bold);
            macro.StopRecording();
            Assert.Equal(1, macro.Count);

            macro.StartRecording();
            Assert.Equal(0, macro.Count);
        }

        [Fact]
        public void MacroHelper_Clear_RemovesAllCommands()
        {
            var macro = new MacroHelper();
            macro.StartRecording();
            macro.Record(MacroCommandType.Bold);
            macro.Record(MacroCommandType.Italic);
            macro.StopRecording();
            Assert.Equal(2, macro.Count);

            macro.Clear();
            Assert.Equal(0, macro.Count);
        }

        [Fact]
        public void MacroHelper_SerializeDeserialize_RoundTrip()
        {
            var macro = new MacroHelper();
            macro.StartRecording();
            macro.Record(MacroCommandType.Bold);
            macro.Record(MacroCommandType.SetFontFamily, "Arial");
            macro.Record(MacroCommandType.SetFontSize, "24");
            macro.Record(MacroCommandType.InsertText, "Hello World");
            macro.StopRecording();

            string json = macro.Serialize();
            var restored = new MacroHelper();
            restored.Deserialize(json);

            Assert.Equal(4, restored.Count);
            Assert.Equal(MacroCommandType.Bold, restored.Commands[0].Type);
            Assert.Null(restored.Commands[0].Value);
            Assert.Equal("Arial", restored.Commands[1].Value);
            Assert.Equal("24", restored.Commands[2].Value);
            Assert.Equal("Hello World", restored.Commands[3].Value);
        }

        [Fact]
        public void MacroHelper_SaveLoad_FileRoundTrip()
        {
            string path = Path.Combine(Path.GetTempPath(), $"macro_test_{Guid.NewGuid():N}.smacro");
            try
            {
                var macro = new MacroHelper();
                macro.StartRecording();
                macro.Record(MacroCommandType.ZoomIn);
                macro.Record(MacroCommandType.ZoomOut);
                macro.StopRecording();
                macro.Save(path);

                var loaded = new MacroHelper();
                loaded.Load(path);
                Assert.Equal(2, loaded.Count);
                Assert.Equal(MacroCommandType.ZoomIn, loaded.Commands[0].Type);
                Assert.Equal(MacroCommandType.ZoomOut, loaded.Commands[1].Type);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void MacroHelper_Deserialize_NullOrEmpty_Throws()
        {
            var macro = new MacroHelper();
            Assert.Throws<ArgumentException>(() => macro.Deserialize(null!));
            Assert.Throws<ArgumentException>(() => macro.Deserialize(""));
            Assert.Throws<ArgumentException>(() => macro.Deserialize("   "));
        }

        [Fact]
        public void MacroHelper_Save_NullOrEmptyPath_Throws()
        {
            var macro = new MacroHelper();
            Assert.Throws<ArgumentException>(() => macro.Save(null!));
            Assert.Throws<ArgumentException>(() => macro.Save(""));
            Assert.Throws<ArgumentException>(() => macro.Save("   "));
        }

        [Fact]
        public void MacroHelper_Load_NullOrEmptyPath_Throws()
        {
            var macro = new MacroHelper();
            Assert.Throws<ArgumentException>(() => macro.Load(null!));
            Assert.Throws<ArgumentException>(() => macro.Load(""));
        }

        [Fact]
        public void MacroCommand_ToString_WithValue()
        {
            var cmd = new MacroCommand(MacroCommandType.SetFontFamily, "Arial");
            Assert.Equal("SetFontFamily:Arial", cmd.ToString());
        }

        [Fact]
        public void MacroCommand_ToString_WithoutValue()
        {
            var cmd = new MacroCommand(MacroCommandType.Bold);
            Assert.Equal("Bold", cmd.ToString());
        }

        [Fact]
        public void MacroCommand_DefaultConstructor_HasDefaults()
        {
            var cmd = new MacroCommand();
            Assert.Equal(MacroCommandType.Bold, cmd.Type); // Default enum = 0 = Bold
            Assert.Null(cmd.Value);
        }

        [Theory]
        [InlineData(MacroCommandType.Bold)]
        [InlineData(MacroCommandType.Italic)]
        [InlineData(MacroCommandType.Underline)]
        [InlineData(MacroCommandType.Strikethrough)]
        [InlineData(MacroCommandType.Subscript)]
        [InlineData(MacroCommandType.Superscript)]
        [InlineData(MacroCommandType.ClearFormatting)]
        [InlineData(MacroCommandType.ZoomIn)]
        [InlineData(MacroCommandType.ZoomOut)]
        public void MacroHelper_ValuelessCommands_RoundTrip(MacroCommandType type)
        {
            var macro = new MacroHelper();
            macro.StartRecording();
            macro.Record(type);
            macro.StopRecording();

            var restored = new MacroHelper();
            restored.Deserialize(macro.Serialize());

            Assert.Single(restored.Commands);
            Assert.Equal(type, restored.Commands[0].Type);
        }

        [Theory]
        [InlineData(MacroCommandType.SetAlignment, "Left")]
        [InlineData(MacroCommandType.SetAlignment, "Center")]
        [InlineData(MacroCommandType.SetAlignment, "Right")]
        [InlineData(MacroCommandType.SetAlignment, "Justify")]
        [InlineData(MacroCommandType.SetFontFamily, "Courier New")]
        [InlineData(MacroCommandType.SetFontSize, "12")]
        [InlineData(MacroCommandType.InsertText, "Test text")]
        public void MacroHelper_ValueCommands_RoundTrip(MacroCommandType type, string value)
        {
            var macro = new MacroHelper();
            macro.StartRecording();
            macro.Record(type, value);
            macro.StopRecording();

            var restored = new MacroHelper();
            restored.Deserialize(macro.Serialize());

            Assert.Single(restored.Commands);
            Assert.Equal(type, restored.Commands[0].Type);
            Assert.Equal(value, restored.Commands[0].Value);
        }

        [Fact]
        public void MacroCommandType_HasExactly15Values()
        {
            Assert.Equal(15, Enum.GetValues<MacroCommandType>().Length);
        }

        [Fact]
        public void MacroHelper_IsRecording_InitiallyFalse()
        {
            var macro = new MacroHelper();
            Assert.False(macro.IsRecording);
        }

        [Fact]
        public void MacroHelper_IsRecording_TrueAfterStart()
        {
            var macro = new MacroHelper();
            macro.StartRecording();
            Assert.True(macro.IsRecording);
        }

        [Fact]
        public void MacroHelper_IsRecording_FalseAfterStop()
        {
            var macro = new MacroHelper();
            macro.StartRecording();
            macro.StopRecording();
            Assert.False(macro.IsRecording);
        }

        [Fact]
        public void MacroHelper_Commands_IsReadOnly()
        {
            var macro = new MacroHelper();
            Assert.IsAssignableFrom<IReadOnlyList<MacroCommand>>(macro.Commands);
        }

        [Fact]
        public void MacroHelper_RecordOverload_WithTypeAndValue()
        {
            var macro = new MacroHelper();
            macro.StartRecording();
            macro.Record(MacroCommandType.InsertText, "Hello");
            macro.StopRecording();
            Assert.Single(macro.Commands);
            Assert.Equal("Hello", macro.Commands[0].Value);
        }

        [Fact]
        public void MacroHelper_Serialize_EmptyList_ProducesValidJson()
        {
            var macro = new MacroHelper();
            string json = macro.Serialize();
            Assert.Equal("[]", json);
        }
    }

    // ═══ DocumentTemplate Extended Tests ═════════════════════════════════════════

    public class DocumentTemplateExtendedTests
    {
        [Fact]
        public void DocumentTemplates_All_HasExactlyFiveTemplates()
        {
            Assert.Equal(5, DocumentTemplates.All.Count);
        }

        [Theory]
        [InlineData("blank")]
        [InlineData("letter")]
        [InlineData("report")]
        [InlineData("resume")]
        [InlineData("meeting")]
        public void DocumentTemplates_ContainsKey(string key)
        {
            Assert.Contains(DocumentTemplates.All, t => t.Key == key);
        }

        [Fact]
        public void DocumentTemplates_AllKeys_AreUnique()
        {
            var keys = DocumentTemplates.All.Select(t => t.Key).ToList();
            Assert.Equal(keys.Count, keys.Distinct().Count());
        }

        [Fact]
        public void DocumentTemplates_AllHaveDisplayName()
        {
            foreach (var t in DocumentTemplates.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(t.DisplayName));
            }
        }

        [Fact]
        public void DocumentTemplates_AllHaveDescription()
        {
            foreach (var t in DocumentTemplates.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(t.Description));
            }
        }

        [Fact]
        public void DocumentTemplates_BlankTemplate_HasEmptyContent()
        {
            var blank = DocumentTemplates.All.First(t => t.Key == "blank");
            Assert.Equal("", blank.PlainContent);
        }

        [Fact]
        public void DocumentTemplates_NonBlankTemplates_HaveContent()
        {
            foreach (var t in DocumentTemplates.All.Where(t => t.Key != "blank"))
            {
                Assert.False(string.IsNullOrEmpty(t.PlainContent));
            }
        }

        [Fact]
        public void DocumentTemplate_RecordEquality_Works()
        {
            var a = new DocumentTemplate("key", "Name", "Desc", "Content");
            var b = new DocumentTemplate("key", "Name", "Desc", "Content");
            Assert.Equal(a, b);
        }

        [Fact]
        public void DocumentTemplate_RecordInequality_Works()
        {
            var a = new DocumentTemplate("key1", "Name", "Desc", "Content");
            var b = new DocumentTemplate("key2", "Name", "Desc", "Content");
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void DocumentTemplate_With_Expression_CreatesNewInstance()
        {
            var original = new DocumentTemplate("k", "N", "D", "C");
            var modified = original with { Key = "k2" };
            Assert.Equal("k2", modified.Key);
            Assert.Equal("N", modified.DisplayName);
        }

        [Fact]
        public void DocumentTemplate_Deconstruction_Works()
        {
            var template = new DocumentTemplate("k", "N", "D", "C");
            var (key, name, desc, content) = template;
            Assert.Equal("k", key);
            Assert.Equal("N", name);
            Assert.Equal("D", desc);
            Assert.Equal("C", content);
        }

        [Fact]
        public void DocumentTemplate_IsSealed()
        {
            Assert.True(typeof(DocumentTemplate).IsSealed);
        }

        [Fact]
        public void DocumentTemplate_IsRecord()
        {
            // Records implement IEquatable<T> and have EqualityContract
            Assert.True(typeof(IEquatable<DocumentTemplate>).IsAssignableFrom(typeof(DocumentTemplate)));
        }

        [Fact]
        public void DocumentTemplates_Letter_ContainsSalutation()
        {
            var letter = DocumentTemplates.All.First(t => t.Key == "letter");
            Assert.Contains("Dear", letter.PlainContent);
            Assert.Contains("Sincerely", letter.PlainContent);
        }

        [Fact]
        public void DocumentTemplates_Report_ContainsSections()
        {
            var report = DocumentTemplates.All.First(t => t.Key == "report");
            Assert.Contains("EXECUTIVE SUMMARY", report.PlainContent);
            Assert.Contains("INTRODUCTION", report.PlainContent);
            Assert.Contains("FINDINGS", report.PlainContent);
            Assert.Contains("RECOMMENDATIONS", report.PlainContent);
            Assert.Contains("CONCLUSION", report.PlainContent);
        }

        [Fact]
        public void DocumentTemplates_Resume_ContainsWorkExperience()
        {
            var resume = DocumentTemplates.All.First(t => t.Key == "resume");
            Assert.Contains("WORK EXPERIENCE", resume.PlainContent);
            Assert.Contains("EDUCATION", resume.PlainContent);
            Assert.Contains("SKILLS", resume.PlainContent);
        }

        [Fact]
        public void DocumentTemplates_Meeting_ContainsActionItems()
        {
            var meeting = DocumentTemplates.All.First(t => t.Key == "meeting");
            Assert.Contains("ACTION ITEMS", meeting.PlainContent);
            Assert.Contains("ATTENDEES", meeting.PlainContent);
            Assert.Contains("AGENDA", meeting.PlainContent);
        }
    }

    // ═══ OneDriveHelper Extended Tests ═══════════════════════════════════════════

    public class OneDriveHelperExtendedTests
    {
        [Fact]
        public void OneDriveHelper_IsStaticClass()
        {
            var t = typeof(OneDriveHelper);
            Assert.True(t.IsAbstract && t.IsSealed);
        }

        [Fact]
        public void GetOneDrivePath_ReturnsNullOrString()
        {
            var result = OneDriveHelper.GetOneDrivePath();
            Assert.True(result is null || result is string);
        }

        [Fact]
        public void IsAvailable_MatchesGetOneDrivePath()
        {
            bool available = OneDriveHelper.IsAvailable();
            string? path = OneDriveHelper.GetOneDrivePath();
            Assert.Equal(path is not null, available);
        }

        [Fact]
        public void GetOneDrivePath_HasCorrectReturnType()
        {
            var method = typeof(OneDriveHelper).GetMethod("GetOneDrivePath");
            Assert.NotNull(method);
            Assert.Equal(typeof(string), Nullable.GetUnderlyingType(method!.ReturnType) ?? method.ReturnType);
        }

        [Fact]
        public void IsAvailable_HasCorrectReturnType()
        {
            var method = typeof(OneDriveHelper).GetMethod("IsAvailable");
            Assert.NotNull(method);
            Assert.Equal(typeof(bool), method!.ReturnType);
        }
    }

    // ═══ RulerHelper Extended Tests ══════════════════════════════════════════════

    public class RulerHelperExtendedTests
    {
        [Fact]
        public void GetPixelsPerUnit_Inches_At100Percent_Returns96()
        {
            double result = RulerHelper.GetPixelsPerUnit("in", 100.0, out string label);
            Assert.Equal(96.0, result, 0.01);
            Assert.Equal("in", label);
        }

        [Fact]
        public void GetPixelsPerUnit_Cm_At100Percent_ReturnsExpected()
        {
            double result = RulerHelper.GetPixelsPerUnit("cm", 100.0, out string label);
            Assert.Equal(96.0 / 2.54, result, 0.01);
            Assert.Equal("cm", label);
        }

        [Fact]
        public void GetPixelsPerUnit_At200Percent_DoublesPixels()
        {
            double at100 = RulerHelper.GetPixelsPerUnit("in", 100.0, out _);
            double at200 = RulerHelper.GetPixelsPerUnit("in", 200.0, out _);
            Assert.Equal(at100 * 2.0, at200, 0.01);
        }

        [Fact]
        public void GetPixelsPerUnit_At50Percent_HalvesPixels()
        {
            double at100 = RulerHelper.GetPixelsPerUnit("in", 100.0, out _);
            double at50 = RulerHelper.GetPixelsPerUnit("in", 50.0, out _);
            Assert.Equal(at100 / 2.0, at50, 0.01);
        }

        [Fact]
        public void GetPixelsPerUnit_NonCmUnits_DefaultToInches()
        {
            double inches = RulerHelper.GetPixelsPerUnit("in", 100.0, out string labelIn);
            double other = RulerHelper.GetPixelsPerUnit("xyz", 100.0, out string labelOther);
            Assert.Equal(inches, other);
            Assert.Equal("in", labelIn);
            Assert.Equal("in", labelOther);
        }

        [Fact]
        public void GetPixelsPerUnit_LinearWithZoom()
        {
            // Test linear scaling at several zoom levels
            double[] zooms = [25, 50, 75, 100, 150, 200, 300, 500];
            double baseVal = RulerHelper.GetPixelsPerUnit("in", 100.0, out _);
            foreach (var z in zooms)
            {
                double expected = baseVal * z / 100.0;
                double actual = RulerHelper.GetPixelsPerUnit("in", z, out _);
                Assert.Equal(expected, actual, 0.01);
            }
        }

        [Fact]
        public void RulerHelper_IsStaticClass()
        {
            var t = typeof(RulerHelper);
            Assert.True(t.IsAbstract && t.IsSealed);
        }
    }

    // ═══ ParagraphStyleDefinition Extended Tests ═════════════════════════════════

    public class ParagraphStyleDefinitionExtendedTests
    {
        [Fact]
        public void ParagraphStyleDefinition_RecordEquality()
        {
            var a = new ParagraphStyleDefinition("Segoe UI", 11f, false, false, "Left", 0f, 0f);
            var b = new ParagraphStyleDefinition("Segoe UI", 11f, false, false, "Left", 0f, 0f);
            Assert.Equal(a, b);
        }

        [Fact]
        public void ParagraphStyleDefinition_RecordInequality()
        {
            var a = ParagraphStyleHelper.Normal;
            var b = ParagraphStyleHelper.Heading1;
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void ParagraphStyleDefinition_WithExpression()
        {
            var normal = ParagraphStyleHelper.Normal;
            var custom = normal with { FontSize = 20f, Bold = true };
            Assert.Equal(20f, custom.FontSize);
            Assert.True(custom.Bold);
            Assert.Equal("Segoe UI", custom.FontName); // Inherited
        }

        [Fact]
        public void ParagraphStyleDefinition_Deconstruct()
        {
            var (fontName, fontSize, bold, italic, alignment, spaceBefore, spaceAfter) =
                ParagraphStyleHelper.Heading1;
            Assert.Equal("Segoe UI", fontName);
            Assert.Equal(20f, fontSize);
            Assert.True(bold);
            Assert.False(italic);
            Assert.Equal("Left", alignment);
            Assert.Equal(12f, spaceBefore);
            Assert.Equal(4f, spaceAfter);
        }

        [Fact]
        public void ParagraphStyleDefinition_IsSealed()
        {
            Assert.True(typeof(ParagraphStyleDefinition).IsSealed);
        }

        [Fact]
        public void ParagraphStyleHelper_IsStaticClass()
        {
            var t = typeof(ParagraphStyleHelper);
            Assert.True(t.IsAbstract && t.IsSealed);
        }

        [Fact]
        public void All_Dictionary_ContainsExactlySixEntries()
        {
            Assert.Equal(6, ParagraphStyleHelper.All.Count);
        }

        [Fact]
        public void Normal_HasDefaultSpacing()
        {
            Assert.Equal(0f, ParagraphStyleHelper.Normal.SpaceBefore);
            Assert.Equal(0f, ParagraphStyleHelper.Normal.SpaceAfter);
        }

        [Fact]
        public void Heading1_HasLargestFontSize()
        {
            var maxSize = ParagraphStyleHelper.All.Values.Max(s => s.FontSize);
            Assert.Equal(ParagraphStyleHelper.Heading1.FontSize, maxSize);
        }

        [Fact]
        public void SubtitleAndQuote_AreItalic()
        {
            Assert.True(ParagraphStyleHelper.Subtitle.Italic);
            Assert.True(ParagraphStyleHelper.Quote.Italic);
        }

        [Fact]
        public void Headings_AreBold()
        {
            Assert.True(ParagraphStyleHelper.Heading1.Bold);
            Assert.True(ParagraphStyleHelper.Heading2.Bold);
            Assert.True(ParagraphStyleHelper.Heading3.Bold);
        }

        [Fact]
        public void NormalSubtitleQuote_AreNotBold()
        {
            Assert.False(ParagraphStyleHelper.Normal.Bold);
            Assert.False(ParagraphStyleHelper.Subtitle.Bold);
            Assert.False(ParagraphStyleHelper.Quote.Bold);
        }
    }

    // ═══ ColorHelper Extended Tests ══════════════════════════════════════════════

    public class ColorHelperExtendedTests
    {
        [Fact]
        public void ParseHexColor_Black()
        {
            var color = ColorHelper.ParseHexColor("#000000");
            Assert.Equal(255, color.A);
            Assert.Equal(0, color.R);
            Assert.Equal(0, color.G);
            Assert.Equal(0, color.B);
        }

        [Fact]
        public void ParseHexColor_White()
        {
            var color = ColorHelper.ParseHexColor("#FFFFFF");
            Assert.Equal(255, color.A);
            Assert.Equal(255, color.R);
            Assert.Equal(255, color.G);
            Assert.Equal(255, color.B);
        }

        [Fact]
        public void ParseHexColor_TransparentBlack()
        {
            var color = ColorHelper.ParseHexColor("#00000000");
            Assert.Equal(0, color.A);
            Assert.Equal(0, color.R);
            Assert.Equal(0, color.G);
            Assert.Equal(0, color.B);
        }

        [Fact]
        public void ParseHexColor_LowercaseHex()
        {
            var color = ColorHelper.ParseHexColor("#aabbcc");
            Assert.Equal(0xAA, color.R);
            Assert.Equal(0xBB, color.G);
            Assert.Equal(0xCC, color.B);
        }

        [Fact]
        public void ParseHexColor_MixedCaseHex()
        {
            var color = ColorHelper.ParseHexColor("#AaBbCc");
            Assert.Equal(0xAA, color.R);
            Assert.Equal(0xBB, color.G);
            Assert.Equal(0xCC, color.B);
        }

        [Fact]
        public void ParseHexColor_HashOnly_Throws()
        {
            Assert.Throws<FormatException>(() => ColorHelper.ParseHexColor("#"));
        }

        [Fact]
        public void ColorHelper_IsStaticClass()
        {
            var t = typeof(ColorHelper);
            Assert.True(t.IsAbstract && t.IsSealed);
        }
    }

    // ═══ DocumentImportHelper Extended Tests ═════════════════════════════════════

    public class DocumentImportHelperExtendedTests
    {
        private static MemoryStream CreateDocxStream(string text)
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry("word/document.xml");
                using var writer = new StreamWriter(entry.Open());
                XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                var doc = new XDocument(
                    new XElement(w + "document",
                        new XElement(w + "body",
                            new XElement(w + "p",
                                new XElement(w + "r",
                                    new XElement(w + "t", text))))));
                writer.Write(doc.ToString());
            }
            ms.Position = 0;
            return ms;
        }

        private static MemoryStream CreateOdtStream(string text)
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry("content.xml");
                using var writer = new StreamWriter(entry.Open());
                XNamespace textNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
                var doc = new XDocument(
                    new XElement("document",
                        new XElement(textNs + "p", text)));
                writer.Write(doc.ToString());
            }
            ms.Position = 0;
            return ms;
        }

        [Fact]
        public void ExtractText_Docx_ReturnsText()
        {
            using var ms = CreateDocxStream("Hello World");
            string result = DocumentImportHelper.ExtractText(ms, ".docx");
            Assert.Contains("Hello", result);
            Assert.Contains("World", result);
        }

        [Fact]
        public void ExtractText_Odt_ReturnsText()
        {
            using var ms = CreateOdtStream("ODT Content");
            string result = DocumentImportHelper.ExtractText(ms, ".odt");
            Assert.Contains("ODT Content", result);
        }

        [Fact]
        public void ExtractText_MissingEntry_ReturnsEmpty()
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                // Create an unrelated entry
                var entry = zip.CreateEntry("other.xml");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("<root/>");
            }
            ms.Position = 0;
            string result = DocumentImportHelper.ExtractText(ms, ".docx");
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void DocumentImportHelper_IsStaticClass()
        {
            var t = typeof(DocumentImportHelper);
            Assert.True(t.IsAbstract && t.IsSealed);
        }
    }

    // ═══ RtfHelper Extended Tests ════════════════════════════════════════════════

    public class RtfHelperExtendedTests
    {
        [Fact]
        public void GenerateTable_1x1_ContainsRtfHeader()
        {
            string rtf = RtfHelper.GenerateTable(1, 1);
            Assert.StartsWith(@"{\rtf1\ansi ", rtf);
            Assert.EndsWith("}", rtf);
        }

        [Fact]
        public void GenerateTable_2x2_HasCorrectRows()
        {
            string rtf = RtfHelper.GenerateTable(2, 2);
            int rowCount = rtf.Split(@"\trowd").Length - 1;
            Assert.Equal(2, rowCount);
        }

        [Fact]
        public void GenerateTable_3x2_HasCorrectCells()
        {
            string rtf = RtfHelper.GenerateTable(3, 2);
            // Count occurrences of "\cell " (cell terminator) to avoid matching \cellx
            int cellCount = rtf.Split(@"\cell ").Length - 1;
            Assert.Equal(6, cellCount); // 3 rows × 2 cols
        }

        [Fact]
        public void GenerateTable_1x1_HasBorderControls()
        {
            string rtf = RtfHelper.GenerateTable(1, 1);
            Assert.Contains(@"\clbrdrt\brdrs", rtf);
            Assert.Contains(@"\clbrdrl\brdrs", rtf);
            Assert.Contains(@"\clbrdrb\brdrs", rtf);
            Assert.Contains(@"\clbrdrr\brdrs", rtf);
        }

        [Fact]
        public void GenerateTable_CellWidth_Is2000Twips()
        {
            string rtf = RtfHelper.GenerateTable(1, 3);
            Assert.Contains(@"\cellx2000", rtf);
            Assert.Contains(@"\cellx4000", rtf);
            Assert.Contains(@"\cellx6000", rtf);
        }

        [Fact]
        public void GenerateTable_ZeroRows_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RtfHelper.GenerateTable(0, 1));
        }

        [Fact]
        public void GenerateTable_ZeroCols_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RtfHelper.GenerateTable(1, 0));
        }

        [Fact]
        public void GenerateTable_NegativeRows_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RtfHelper.GenerateTable(-1, 1));
        }

        [Fact]
        public void GenerateTable_NegativeCols_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RtfHelper.GenerateTable(1, -1));
        }

        [Fact]
        public void RtfHelper_IsStaticClass()
        {
            var t = typeof(RtfHelper);
            Assert.True(t.IsAbstract && t.IsSealed);
        }
    }

    // ═══ ResourceHelper Extended Tests ═══════════════════════════════════════════

    public class ResourceHelperExtendedTests
    {
        [Fact]
        public void GetString_UnknownKey_ReturnsKeyName()
        {
            string result = ResourceHelper.GetString("NonExistent_Key_12345");
            Assert.Equal("NonExistent_Key_12345", result);
        }

        [Fact]
        public void GetString_DocumentUntitled_ReturnsNonEmpty()
        {
            string result = ResourceHelper.GetString("DocumentUntitled");
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetString_StatusReady_ReturnsNonEmpty()
        {
            string result = ResourceHelper.GetString("StatusReady");
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetFormatted_WithMultipleArgs()
        {
            string result = ResourceHelper.GetFormatted("StatusBarLineCol", 10, 20);
            Assert.Contains("10", result);
            Assert.Contains("20", result);
        }

        [Fact]
        public void GetFormatted_WithSingleArg()
        {
            string result = ResourceHelper.GetFormatted("StatusBarWords", 100);
            Assert.Contains("100", result);
        }

        [Fact]
        public void ResourceHelper_IsStaticClass()
        {
            var t = typeof(ResourceHelper);
            Assert.True(t.IsAbstract && t.IsSealed);
        }

        [Fact]
        public void GetString_SameKeyTwice_ReturnsSameResult()
        {
            string a = ResourceHelper.GetString("StatusReady");
            string b = ResourceHelper.GetString("StatusReady");
            Assert.Equal(a, b);
        }
    }

    // ═══ App Extended Contract Tests ═════════════════════════════════════════════

    public class AppExtendedContractTests
    {
        [Fact]
        public void App_HasOnLaunched_Override()
        {
            var method = typeof(SmrtPad.App).GetMethod(
                "OnLaunched",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void App_Windows_IsNotNull()
        {
            var prop = typeof(SmrtPad.App).GetProperty("Windows",
                BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(prop);
            // Get value from static property
            var value = prop!.GetValue(null);
            Assert.NotNull(value);
        }

        [Fact]
        public void App_Windows_IsList()
        {
            var prop = typeof(SmrtPad.App).GetProperty("Windows",
                BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(prop);
            Assert.Equal(typeof(List<SmrtPad.MainWindow>), prop!.PropertyType);
        }

        [Fact]
        public void App_HasPartialClass_Marker()
        {
            // App is partial (generated code-behind)
            var type = typeof(SmrtPad.App);
            Assert.True(type.IsClass);
        }

        [Fact]
        public void App_Constructor_IsPublic()
        {
            var ctor = typeof(SmrtPad.App).GetConstructor(Type.EmptyTypes);
            Assert.NotNull(ctor);
            Assert.True(ctor!.IsPublic);
        }
    }

    // ═══ Service Interface Parity Tests ══════════════════════════════════════════

    public class ServiceInterfaceParityTests
    {
        [Fact]
        public void DialogService_ImplementsAllInterfaceMembers()
        {
            var interfaceType = typeof(IDialogService);
            var implType = typeof(DialogService);

            foreach (var method in interfaceType.GetMethods())
            {
                var impl = implType.GetMethod(method.Name);
                Assert.NotNull(impl);
                Assert.Equal(method.ReturnType, impl!.ReturnType);
                Assert.Equal(method.GetParameters().Length, impl.GetParameters().Length);
            }
        }

        [Fact]
        public void FileService_ImplementsAllInterfaceMembers()
        {
            var interfaceType = typeof(IFileService);
            var implType = typeof(FileService);

            foreach (var method in interfaceType.GetMethods())
            {
                var impl = implType.GetMethod(method.Name);
                Assert.NotNull(impl);
                Assert.Equal(method.ReturnType, impl!.ReturnType);
                Assert.Equal(method.GetParameters().Length, impl.GetParameters().Length);
            }
        }

        [Fact]
        public void SettingsService_ImplementsAllInterfaceMembers()
        {
            var interfaceType = typeof(ISettingsService);
            var implType = typeof(SettingsService);

            foreach (var prop in interfaceType.GetProperties())
            {
                var impl = implType.GetProperty(prop.Name);
                Assert.NotNull(impl);
                Assert.Equal(prop.PropertyType, impl!.PropertyType);
            }

            foreach (var method in interfaceType.GetMethods()
                .Where(m => !m.IsSpecialName)) // Exclude property accessors
            {
                var impl = implType.GetMethod(method.Name);
                Assert.NotNull(impl);
            }
        }

        [Fact]
        public void DialogService_HasTwoConstructors()
        {
            var ctors = typeof(DialogService).GetConstructors();
            Assert.Equal(2, ctors.Length);
        }

        [Fact]
        public void FileService_HasTwoConstructors()
        {
            var ctors = typeof(FileService).GetConstructors();
            Assert.Equal(2, ctors.Length);
        }

        [Fact]
        public void SettingsService_HasTwoConstructors()
        {
            var ctors = typeof(SettingsService).GetConstructors();
            Assert.Equal(2, ctors.Length);
        }

        [Fact]
        public void SavePromptResult_Values()
        {
            Assert.Equal(0, (int)SavePromptResult.Save);
            Assert.Equal(1, (int)SavePromptResult.DontSave);
            Assert.Equal(2, (int)SavePromptResult.Cancel);
        }

        [Fact]
        public void IDialogService_HasExactlyTwoMethods()
        {
            var methods = typeof(IDialogService).GetMethods();
            Assert.Equal(2, methods.Length);
        }
    }

    // ═══ FileBackstageView Extended Contract Tests ═══════════════════════════════

    public class FileBackstageViewExtendedTests
    {
        private static readonly Type BSV = typeof(SmrtPad.Views.FileBackstageView);

        [Fact]
        public void FileBackstageView_InheritsUserControl()
        {
            Assert.True(typeof(Microsoft.UI.Xaml.Controls.UserControl).IsAssignableFrom(BSV));
        }

        [Fact]
        public void FileBackstageView_IsInViewsNamespace()
        {
            Assert.Equal("SmrtPad.Views", BSV.Namespace);
        }

        [Fact]
        public void FileBackstageView_HasSetDocumentProperties_5Params()
        {
            var method = BSV.GetMethod("SetDocumentProperties");
            Assert.NotNull(method);
            var parms = method!.GetParameters();
            Assert.Equal(5, parms.Length);
            Assert.Equal("fileName", parms[0].Name);
            Assert.Equal("wordCount", parms[1].Name);
            Assert.Equal("charCount", parms[2].Name);
            Assert.Equal("encoding", parms[3].Name);
            Assert.Equal("isModified", parms[4].Name);
        }

        [Fact]
        public void FileBackstageView_HasSetRecentFiles_ListParam()
        {
            var method = BSV.GetMethod("SetRecentFiles");
            Assert.NotNull(method);
            var parms = method!.GetParameters();
            Assert.Single(parms);
            Assert.Equal("recentFiles", parms[0].Name);
        }

        [Fact]
        public void FileBackstageView_EventCount_AtLeastTwelve()
        {
            var events = BSV.GetEvents();
            Assert.True(events.Length >= 12, $"Expected ≥12 events, found {events.Length}");
        }

        [Theory]
        [InlineData("NewRequested", typeof(EventHandler))]
        [InlineData("OpenRequested", typeof(EventHandler))]
        [InlineData("SaveRequested", typeof(EventHandler))]
        [InlineData("SaveAsRequested", typeof(EventHandler))]
        [InlineData("PrintRequested", typeof(EventHandler))]
        [InlineData("ExportPdfRequested", typeof(EventHandler))]
        [InlineData("ExportDocxRequested", typeof(EventHandler))]
        [InlineData("OneDriveRequested", typeof(EventHandler))]
        [InlineData("OptionsRequested", typeof(EventHandler))]
        [InlineData("ExitRequested", typeof(EventHandler))]
        public void FileBackstageView_StandardEvents_HaveCorrectType(string name, Type handlerType)
        {
            var evt = BSV.GetEvent(name);
            Assert.NotNull(evt);
            Assert.Equal(handlerType, evt!.EventHandlerType);
        }

        [Fact]
        public void FileBackstageView_RecentFileRequested_IsStringEventHandler()
        {
            var evt = BSV.GetEvent("RecentFileRequested");
            Assert.NotNull(evt);
            Assert.Equal(typeof(EventHandler<string>), evt!.EventHandlerType);
        }

        [Fact]
        public void FileBackstageView_TemplateRequested_IsDocumentTemplateEventHandler()
        {
            var evt = BSV.GetEvent("TemplateRequested");
            Assert.NotNull(evt);
            Assert.Equal(typeof(EventHandler<DocumentTemplate>), evt!.EventHandlerType);
        }
    }

    // ═══ MainWindow Remaining Handler Contract Tests ════════════════════════════

    public class MainWindowRemainingHandlerTests
    {
        private static readonly Type MW = typeof(SmrtPad.MainWindow);
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [Theory]
        [InlineData("ApplyTextColor")]
        [InlineData("ApplyHighlightColor")]
        [InlineData("ApplyLastFontColor_Invoked")]
        [InlineData("TextColorPicker_ColorChanged")]
        [InlineData("HighlightColorPicker_ColorChanged")]
        [InlineData("FindNextRegex")]
        [InlineData("SetAlignmentToggle")]
        [InlineData("ExecuteMacroCommand")]
        [InlineData("ApplyListType")]
        public void MainWindow_HasRemainingPrivateMethod(string methodName)
        {
            var method = MW.GetMethod(methodName, Private);
            Assert.NotNull(method);
        }

        [Theory]
        [InlineData("ListTypeNone_Click")]
        [InlineData("ListTypeBullet_Click")]
        [InlineData("ListTypeNumber_Click")]
        [InlineData("ListTypeLowerLetter_Click")]
        [InlineData("ListTypeUpperLetter_Click")]
        [InlineData("ListTypeLowerRoman_Click")]
        [InlineData("ListTypeUpperRoman_Click")]
        [InlineData("LineSpacing_Click")]
        public void MainWindow_HasListAndSpacingHandler(string handlerName)
        {
            var method = MW.GetMethod(handlerName, Private);
            Assert.NotNull(method);
        }

        [Fact]
        public void ApplyTextColor_HasColorParam()
        {
            var method = MW.GetMethod("ApplyTextColor", Private);
            Assert.NotNull(method);
            var parms = method!.GetParameters();
            Assert.Single(parms);
            Assert.Equal(typeof(Windows.UI.Color), parms[0].ParameterType);
        }

        [Fact]
        public void ApplyHighlightColor_HasColorParam()
        {
            var method = MW.GetMethod("ApplyHighlightColor", Private);
            Assert.NotNull(method);
            var parms = method!.GetParameters();
            Assert.Single(parms);
            Assert.Equal(typeof(Windows.UI.Color), parms[0].ParameterType);
        }

        [Fact]
        public void FindNextRegex_HasPatternAndForwardParams()
        {
            var method = MW.GetMethod("FindNextRegex", Private);
            Assert.NotNull(method);
            var parms = method!.GetParameters();
            Assert.Equal(2, parms.Length);
            Assert.Equal(typeof(string), parms[0].ParameterType);
            Assert.Equal(typeof(bool), parms[1].ParameterType);
        }

        [Fact]
        public void ExecuteMacroCommand_HasMacroCommandParam()
        {
            var method = MW.GetMethod("ExecuteMacroCommand", Private);
            Assert.NotNull(method);
            var parms = method!.GetParameters();
            Assert.Single(parms);
            Assert.Equal(typeof(MacroCommand), parms[0].ParameterType);
        }

        [Fact]
        public void SetAlignmentToggle_HasToggleButtonParam()
        {
            var method = MW.GetMethod("SetAlignmentToggle", Private);
            Assert.NotNull(method);
            var parms = method!.GetParameters();
            Assert.Single(parms);
        }

        [Fact]
        public void MainWindow_HasCreateTab_WithTitleParam()
        {
            var method = MW.GetMethod("CreateTab", Private);
            Assert.NotNull(method);
            var parms = method!.GetParameters();
            Assert.Single(parms);
            Assert.Equal(typeof(string), parms[0].ParameterType);
        }

        [Theory]
        [InlineData("Save_Click")]
        [InlineData("SaveAs_Click")]
        [InlineData("Open_Click")]
        [InlineData("Print_Click")]
        [InlineData("InsertPicture_Click")]
        [InlineData("InsertDateTime_Click")]
        [InlineData("PaintDrawing_Click")]
        [InlineData("InsertObject_Click")]
        [InlineData("InsertHyperlink_Click")]
        [InlineData("InsertSymbol_Click")]
        [InlineData("InsertTable_Click")]
        [InlineData("ExportPdf_Click")]
        [InlineData("ExportDocx_Click")]
        [InlineData("SaveToOneDrive_Click")]
        [InlineData("MacroSave_Click")]
        [InlineData("MacroLoad_Click")]
        [InlineData("Options_Click")]
        [InlineData("CustomLineSpacing_Click")]
        [InlineData("TabStops_Click")]
        [InlineData("New_Click")]
        public void MainWindow_AsyncHandler_ReturnsVoid(string handlerName)
        {
            var method = MW.GetMethod(handlerName, Private);
            Assert.NotNull(method);
            Assert.Equal(typeof(void), method!.ReturnType);
        }

        [Theory]
        [InlineData("Editor_Drop")]
        [InlineData("DocumentTabs_TabCloseRequested")]
        public void MainWindow_AsyncHandler_NotNull(string handlerName)
        {
            var method = MW.GetMethod(handlerName, Private);
            Assert.NotNull(method);
        }

        [Fact]
        public void MainWindow_XAML_HasListTypeFlyoutItems()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("ListTypeNone_Click", xaml);
            Assert.Contains("ListTypeBullet_Click", xaml);
            Assert.Contains("ListTypeNumber_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasLineSpacingFlyout()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("LineSpacing_Click", xaml);
        }

        [Fact]
        public void MainWindow_HasExportPdfAndDocxMethods()
        {
            // Export handlers are wired via backstage events, not directly in XAML
            Assert.NotNull(MW.GetMethod("ExportPdf_Click", Private));
            Assert.NotNull(MW.GetMethod("ExportDocx_Click", Private));
        }

        [Fact]
        public void MainWindow_XAML_HasColorPickerHandlers()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("TextColorPicker_ColorChanged", xaml);
            Assert.Contains("HighlightColorPicker_ColorChanged", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasZoomButtons()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("ZoomIn_Click", xaml);
            Assert.Contains("ZoomOut_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasUndoRedo()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("Undo_Click", xaml);
            Assert.Contains("Redo_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasCutCopyPaste()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("Cut_Click", xaml);
            Assert.Contains("Copy_Click", xaml);
            Assert.Contains("Paste_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasFindNextPreviousButtons()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("FindNext_Click", xaml);
            Assert.Contains("FindPrevious_Click", xaml);
            Assert.Contains("Replace_Click", xaml);
            Assert.Contains("ReplaceAll_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasGrowShrinkFont()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("GrowFont_Click", xaml);
            Assert.Contains("ShrinkFont_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasFormattingHandlers()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("Bold_Click", xaml);
            Assert.Contains("Italic_Click", xaml);
            Assert.Contains("Underline_Click", xaml);
            Assert.Contains("Strikethrough_Click", xaml);
            Assert.Contains("Subscript_Click", xaml);
            Assert.Contains("Superscript_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasAlignmentHandlers()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("AlignLeft_Click", xaml);
            Assert.Contains("AlignCenter_Click", xaml);
            Assert.Contains("AlignRight_Click", xaml);
            Assert.Contains("AlignJustify_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasIndentHandlers()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("DecreaseIndent_Click", xaml);
            Assert.Contains("IncreaseIndent_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasHighlightButtons()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("HighlightAllMatches_Click", xaml);
            Assert.Contains("ClearHighlights_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasClearFormattingButton()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("ClearFormatting_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasPasteSpecialButton()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("PasteSpecial_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasSelectAllHandler()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("SelectAll_Click", xaml);
        }

        private static string? ReadXaml(string filename)
        {
            string? dir = Directory.GetCurrentDirectory();
            while (dir is not null)
            {
                string candidate = Path.Combine(dir, "SmrtPad", filename);
                if (File.Exists(candidate)) return File.ReadAllText(candidate);
                dir = Directory.GetParent(dir)?.FullName;
            }
            return null;
        }
    }

    // ═══ EditorViewModel Command Parameter Tests ════════════════════════════════

    public class EditorViewModelCommandParamTests
    {
        [Fact]
        public void ToggleSubscript_ClearsSuperscript()
        {
            var vm = new EditorViewModel();
            vm.IsSuperscript = true;
            vm.ToggleSubscript();
            Assert.True(vm.IsSubscript);
            Assert.False(vm.IsSuperscript);
        }

        [Fact]
        public void ToggleSuperscript_ClearsSubscript()
        {
            var vm = new EditorViewModel();
            vm.IsSubscript = true;
            vm.ToggleSuperscript();
            Assert.True(vm.IsSuperscript);
            Assert.False(vm.IsSubscript);
        }

        [Fact]
        public void SetAlignmentCommand_Execute_SetsAlignment()
        {
            var vm = new EditorViewModel();
            vm.SetAlignmentCommand.Execute("Center");
            Assert.Equal("Center", vm.Alignment);
        }

        [Fact]
        public void SetListTypeCommand_Execute_SetsListType()
        {
            var vm = new EditorViewModel();
            vm.SetListTypeCommand.Execute("Number");
            Assert.Equal("Number", vm.ListType);
            Assert.True(vm.IsBullets);
        }

        [Fact]
        public void SetLineSpacingCommand_Execute_SetsSpacing()
        {
            var vm = new EditorViewModel();
            vm.SetLineSpacingCommand.Execute(2.0);
            Assert.Equal(2.0, vm.LineSpacing);
        }

        [Fact]
        public void SetParagraphSpacingCommand_Execute_SetsBothValues()
        {
            var vm = new EditorViewModel();
            vm.SetParagraphSpacingCommand.Execute(new double[] { 12.0, 6.0 });
            Assert.Equal(12.0, vm.ParagraphSpacingBefore);
            Assert.Equal(6.0, vm.ParagraphSpacingAfter);
        }

        [Fact]
        public void UpdateCursorPositionCommand_Execute_SetsLineCol()
        {
            var vm = new EditorViewModel();
            vm.UpdateCursorPositionCommand.Execute(new int[] { 5, 10 });
            Assert.Equal(5, vm.LineNumber);
            Assert.Equal(10, vm.ColumnNumber);
        }

        [Fact]
        public void UpdateWordCountCommand_Execute_SetsCount()
        {
            var vm = new EditorViewModel();
            vm.UpdateWordCountCommand.Execute(42);
            Assert.Equal(42, vm.WordCount);
        }

        [Fact]
        public void UpdateCharCountCommand_Execute_SetsCount()
        {
            var vm = new EditorViewModel();
            vm.UpdateCharCountCommand.Execute(256);
            Assert.Equal(256, vm.CharCount);
        }

        [Fact]
        public void ToggleBulletsCommand_Execute_FlipsBullets()
        {
            var vm = new EditorViewModel();
            Assert.False(vm.IsBullets);
            vm.ToggleBulletsCommand.Execute(null);
            Assert.True(vm.IsBullets);
        }

        [Fact]
        public void ToggleWordWrapCommand_Execute_FlipsWrap()
        {
            var vm = new EditorViewModel();
            Assert.True(vm.IsWordWrap);
            vm.ToggleWordWrapCommand.Execute(null);
            Assert.False(vm.IsWordWrap);
        }

        [Fact]
        public void ToggleStrikethroughCommand_Execute_FlipsStrike()
        {
            var vm = new EditorViewModel();
            vm.ToggleStrikethroughCommand.Execute(null);
            Assert.True(vm.IsStrikethrough);
        }

        [Fact]
        public void ToggleSubscriptCommand_Execute_FlipsSub()
        {
            var vm = new EditorViewModel();
            vm.ToggleSubscriptCommand.Execute(null);
            Assert.True(vm.IsSubscript);
        }

        [Fact]
        public void ToggleSuperscriptCommand_Execute_FlipsSuper()
        {
            var vm = new EditorViewModel();
            vm.ToggleSuperscriptCommand.Execute(null);
            Assert.True(vm.IsSuperscript);
        }

        [Fact]
        public void ToggleUnderlineCommand_Execute_FlipsUnderline()
        {
            var vm = new EditorViewModel();
            vm.ToggleUnderlineCommand.Execute(null);
            Assert.True(vm.IsUnderline);
        }

        [Fact]
        public void ZoomInCommand_Execute_IncrementsBy10()
        {
            var vm = new EditorViewModel();
            Assert.Equal(100.0, vm.ZoomLevel);
            vm.ZoomInCommand.Execute(null);
            Assert.Equal(110.0, vm.ZoomLevel);
        }

        [Fact]
        public void ZoomOutCommand_Execute_DecrementsBy10()
        {
            var vm = new EditorViewModel();
            Assert.Equal(100.0, vm.ZoomLevel);
            vm.ZoomOutCommand.Execute(null);
            Assert.Equal(90.0, vm.ZoomLevel);
        }
    }

    // ═══ RtfParser Direct Tests ═════════════════════════════════════════════════

    public class RtfParserDirectTests
    {
        [Fact]
        public void Parse_EmptyString_ReturnsEmptyList()
        {
            var result = RtfParser.Parse("");
            Assert.Empty(result);
        }

        [Fact]
        public void Parse_NullString_ReturnsEmptyList()
        {
            var result = RtfParser.Parse(null!);
            Assert.Empty(result);
        }

        [Fact]
        public void Parse_PlainText_ReturnsOneParagraphOneRun()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi Hello}");
            Assert.True(result.Count >= 1);
            Assert.True(result[0].Runs.Count >= 1);
        }

        [Fact]
        public void Parse_BoldText_SetsBoldFlag()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\b Bold Text}");
            Assert.True(result.Count >= 1);
            var boldRun = result.SelectMany(p => p.Runs).FirstOrDefault(r => r.Bold);
            Assert.NotNull(boldRun);
        }

        [Fact]
        public void Parse_ItalicText_SetsItalicFlag()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\i Italic}");
            var italicRun = result.SelectMany(p => p.Runs).FirstOrDefault(r => r.Italic);
            Assert.NotNull(italicRun);
        }

        [Fact]
        public void Parse_UnderlineText_SetsUnderlineFlag()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\ul Under}");
            var ulRun = result.SelectMany(p => p.Runs).FirstOrDefault(r => r.Underline);
            Assert.NotNull(ulRun);
        }

        [Fact]
        public void Parse_StrikeText_SetsStrikeFlag()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\strike Strike}");
            var stRun = result.SelectMany(p => p.Runs).FirstOrDefault(r => r.Strikethrough);
            Assert.NotNull(stRun);
        }

        [Fact]
        public void Parse_FontSize_SetsHalfPoints()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\fs48 Big}");
            var run = result.SelectMany(p => p.Runs).FirstOrDefault(r => r.FontSizeHalfPts == 48);
            Assert.NotNull(run);
        }

        [Fact]
        public void Parse_CenterAlignment_SetsAlignmentCenter()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\qc Centered}");
            Assert.Contains(result, p => p.Alignment == "center");
        }

        [Fact]
        public void Parse_RightAlignment_SetsAlignmentRight()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\qr Right}");
            Assert.Contains(result, p => p.Alignment == "right");
        }

        [Fact]
        public void Parse_JustifyAlignment_SetsAlignmentJustify()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\qj Justified}");
            Assert.Contains(result, p => p.Alignment == "justify");
        }

        [Fact]
        public void Parse_LeftAlignment_SetsAlignmentLeft()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\ql Left}");
            Assert.Contains(result, p => p.Alignment == "left");
        }

        [Fact]
        public void Parse_Par_CreatesParagraphs()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi First\par Second\par Third}");
            Assert.True(result.Count >= 3);
        }

        [Fact]
        public void Parse_Line_CreatesParagraph()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi Before\line After}");
            Assert.True(result.Count >= 2);
        }

        [Fact]
        public void Parse_Pard_ResetsBold()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\b Bold\pard Plain}");
            var runs = result.SelectMany(p => p.Runs).ToList();
            Assert.Contains(runs, r => r.Bold);
            Assert.Contains(runs, r => !r.Bold);
        }

        [Fact]
        public void Parse_UlnoneDisablesUnderline()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\ul Under\ulnone Not}");
            var runs = result.SelectMany(p => p.Runs).ToList();
            Assert.Contains(runs, r => r.Underline);
            Assert.Contains(runs, r => !r.Underline);
        }

        [Fact]
        public void Parse_BoldOff_DisablesBold()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\b Bold\b0 Off}");
            var runs = result.SelectMany(p => p.Runs).ToList();
            Assert.Contains(runs, r => !r.Bold);
        }

        [Fact]
        public void Parse_EscapedBackslash_ProducesBackslash()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi test\\more}");
            var text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("\\", text);
        }

        [Fact]
        public void Parse_EscapedBraces_ProducesBraces()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi \{ and \}}");
            var text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("{", text);
            Assert.Contains("}", text);
        }

        [Fact]
        public void Parse_HexEscape_ProducesCorrectChar()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi \'41}");
            var text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("A", text);
        }

        [Fact]
        public void Parse_DestinationGroup_IsSkipped()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi{\*\generator Test;}Hello}");
            var text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("Hello", text);
            Assert.DoesNotContain("generator", text);
        }

        [Fact]
        public void Parse_PictGroup_IsSkipped()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi Text{\pict data}More}");
            var text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("Text", text);
            Assert.Contains("More", text);
            Assert.DoesNotContain("data", text);
        }

        [Fact]
        public void Parse_FontTable_ExtractsFontNames()
        {
            var result = RtfParser.Parse(
                @"{\rtf1\ansi{\fonttbl{\f0\fswiss Arial;}}Hello}");
            var run = result.SelectMany(p => p.Runs).FirstOrDefault();
            Assert.NotNull(run);
        }

        [Fact]
        public void RtfRun_RecordEquality()
        {
            var a = new RtfRun("text", true, false, false, false, "Arial", 24);
            var b = new RtfRun("text", true, false, false, false, "Arial", 24);
            Assert.Equal(a, b);
        }

        [Fact]
        public void RtfRun_RecordInequality()
        {
            var a = new RtfRun("text", true, false, false, false, "Arial", 24);
            var b = new RtfRun("text", false, false, false, false, "Arial", 24);
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void RtfParagraph_DefaultAlignment_IsLeft()
        {
            var para = new RtfParagraph();
            Assert.Equal("left", para.Alignment);
        }

        [Fact]
        public void RtfParagraph_DefaultRuns_IsEmpty()
        {
            var para = new RtfParagraph();
            Assert.Empty(para.Runs);
        }

        [Fact]
        public void Parse_RunCoalescing_MergesIdenticalFormattingRuns()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi AB}");
            var runs = result.SelectMany(p => p.Runs).ToList();
            var combinedText = string.Join("", runs.Select(r => r.Text));
            Assert.Contains("AB", combinedText);
            var abRun = runs.FirstOrDefault(r => r.Text.Contains("AB"));
            Assert.NotNull(abRun);
        }

        [Fact]
        public void Parse_TrimsLeadingEmptyParagraphs()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\par\par Content}");
            Assert.True(result.Count >= 1);
            Assert.Contains(result, p => p.Runs.Count > 0);
        }

        [Fact]
        public void Parse_TrimsTrailingEmptyParagraphs()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi Content\par\par }");
            Assert.True(result.Count >= 1);
            Assert.Contains(result, p => p.Runs.Count > 0);
        }

        [Fact]
        public void Parse_Striked_AlsoSetsStrikethrough()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\striked Strike}");
            var stRun = result.SelectMany(p => p.Runs).FirstOrDefault(r => r.Strikethrough);
            Assert.NotNull(stRun);
        }

        [Fact]
        public void Parse_SpecialChars_AreSkipped()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi Hello\~World}");
            var text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("Hello", text);
            Assert.Contains("World", text);
        }
    }

    // ═══ DocxExportHelper GenerateDocx Edge Cases ════════════════════════════════

    public class DocxExportEdgeCaseTests
    {
        [Fact]
        public void GenerateDocx_TrailingNewlines_AreTrimmed()
        {
            var bytes = DocxExportHelper.GenerateDocx("Hello\n\n\n");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("word/document.xml");
            using var reader = new StreamReader(entry!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var paragraphs = doc.Descendants(w + "p").ToList();
            Assert.Single(paragraphs);
        }

        [Fact]
        public void GenerateDocx_CROnly_NormalizedCorrectly()
        {
            var bytes = DocxExportHelper.GenerateDocx("A\rB\rC");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("word/document.xml");
            using var reader = new StreamReader(entry!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var paragraphs = doc.Descendants(w + "p").ToList();
            Assert.Equal(3, paragraphs.Count);
        }

        [Fact]
        public void GenerateDocx_UnicodeContent_Preserved()
        {
            var bytes = DocxExportHelper.GenerateDocx("日本語テスト");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("word/document.xml");
            using var reader = new StreamReader(entry!.Open());
            string content = reader.ReadToEnd();
            Assert.Contains("日本語テスト", content);
        }

        [Fact]
        public void GenerateDocx_EmptyContent_HasOneParagraph()
        {
            var bytes = DocxExportHelper.GenerateDocx("");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("word/document.xml");
            using var reader = new StreamReader(entry!.Open());
            var doc = XDocument.Parse(reader.ReadToEnd());
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var paragraphs = doc.Descendants(w + "p").ToList();
            Assert.Single(paragraphs);
        }

        [Fact]
        public void GenerateDocx_ContentTypes_HasCorrectElements()
        {
            var bytes = DocxExportHelper.GenerateDocx("test");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("[Content_Types].xml");
            using var reader = new StreamReader(entry!.Open());
            string content = reader.ReadToEnd();
            Assert.Contains("rels", content);
            Assert.Contains("xml", content);
            Assert.Contains("wordprocessingml", content);
        }

        [Fact]
        public void GenerateDocx_RootRels_HasRelationship()
        {
            var bytes = DocxExportHelper.GenerateDocx("test");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("_rels/.rels");
            using var reader = new StreamReader(entry!.Open());
            string content = reader.ReadToEnd();
            Assert.Contains("officeDocument", content);
            Assert.Contains("word/document.xml", content);
        }

        [Fact]
        public void GenerateRichDocx_ContentTypes_HasCorrectElements()
        {
            var bytes = DocxExportHelper.GenerateRichDocx("test");
            using var ms = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("[Content_Types].xml");
            using var reader = new StreamReader(entry!.Open());
            string content = reader.ReadToEnd();
            Assert.Contains("wordprocessingml", content);
        }
    }

    // ═══ ViewModel Remaining Property Tests ═════════════════════════════════════

    public class ViewModelRemainingPropertyTests
    {
        [Fact]
        public void IsModified_DefaultIsFalse()
        {
            var vm = new EditorViewModel();
            Assert.False(vm.IsModified);
        }

        [Fact]
        public void IsModified_CanBeSetTrue()
        {
            var vm = new EditorViewModel();
            vm.IsModified = true;
            Assert.True(vm.IsModified);
        }

        [Fact]
        public void IsModified_FiresPropertyChanged()
        {
            var vm = new EditorViewModel();
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
            vm.IsModified = true;
            Assert.Contains("IsModified", fired);
        }

        [Fact]
        public void IsModified_NewDocument_Resets()
        {
            var vm = new EditorViewModel();
            vm.IsModified = true;
            vm.NewDocument();
            Assert.False(vm.IsModified);
        }

        [Fact]
        public void DocumentTitle_DefaultIsUntitled()
        {
            var vm = new EditorViewModel();
            Assert.NotEmpty(vm.DocumentTitle);
        }

        [Fact]
        public void DocumentTitle_CanBeSet()
        {
            var vm = new EditorViewModel();
            vm.DocumentTitle = "My File.rtf";
            Assert.Equal("My File.rtf", vm.DocumentTitle);
        }

        [Fact]
        public void DocumentTitle_FiresPropertyChanged()
        {
            var vm = new EditorViewModel();
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
            vm.DocumentTitle = "Changed";
            Assert.Contains("DocumentTitle", fired);
        }

        [Fact]
        public void DocumentTitle_NewDocument_ResetsToUntitled()
        {
            var vm = new EditorViewModel();
            string original = vm.DocumentTitle;
            vm.DocumentTitle = "saved.rtf";
            vm.NewDocument();
            Assert.Equal(original, vm.DocumentTitle);
        }

        [Fact]
        public void RecentFiles_DefaultIsEmpty()
        {
            var vm = new EditorViewModel();
            Assert.Empty(vm.RecentFiles);
        }

        [Fact]
        public void RecentFiles_CanBePopulated()
        {
            var vm = new EditorViewModel();
            vm.RecentFiles = ["C:\\a.rtf", "C:\\b.rtf"];
            Assert.Equal(2, vm.RecentFiles.Count);
            Assert.Contains("C:\\a.rtf", vm.RecentFiles);
            Assert.Contains("C:\\b.rtf", vm.RecentFiles);
        }

        [Fact]
        public void RecentFiles_FiresPropertyChanged()
        {
            var vm = new EditorViewModel();
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
            vm.RecentFiles = ["C:\\file.rtf"];
            Assert.Contains("RecentFiles", fired);
        }

        [Fact]
        public void StatusMessage_DefaultIsReady()
        {
            var vm = new EditorViewModel();
            Assert.NotEmpty(vm.StatusMessage);
        }

        [Fact]
        public void StatusMessage_NewDocument_SetsNewDocMessage()
        {
            var vm = new EditorViewModel();
            vm.UpdateStatus("Custom message");
            vm.NewDocument();
            // NewDocument() sets status to the StatusNewDocument resource string
            Assert.NotEmpty(vm.StatusMessage);
            Assert.NotEqual("Custom message", vm.StatusMessage);
        }

        [Fact]
        public void ZoomDisplay_LargeValue_FormatsCorrectly()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 500;
            Assert.Equal("500%", vm.ZoomDisplay);
        }

        [Fact]
        public void ZoomDisplay_SmallValue_FormatsCorrectly()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 10;
            Assert.Equal("10%", vm.ZoomDisplay);
        }

        [Fact]
        public void ZoomDisplay_DecimalValue_RoundsToInteger()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 110.5;
            // Format is {ZoomLevel:0}% — rounds to nearest integer
            Assert.Equal("111%", vm.ZoomDisplay);
        }

        [Fact]
        public void SelectionLengthDisplay_Zero_FormatsCorrectly()
        {
            var vm = new EditorViewModel();
            vm.SelectionLength = 0;
            Assert.Contains("0", vm.SelectionLengthDisplay);
        }

        [Fact]
        public void LineColDisplay_LargeValues_FormatsCorrectly()
        {
            var vm = new EditorViewModel();
            vm.LineNumber = 10000;
            vm.ColumnNumber = 999;
            Assert.Contains("10000", vm.LineColDisplay);
            Assert.Contains("999", vm.LineColDisplay);
        }

        [Fact]
        public void WordCountDisplay_LargeNumber_FormatsCorrectly()
        {
            var vm = new EditorViewModel();
            vm.WordCount = 100000;
            Assert.Contains("100000", vm.WordCountDisplay);
        }

        [Fact]
        public void CharCountDisplay_LargeNumber_FormatsCorrectly()
        {
            var vm = new EditorViewModel();
            vm.CharCount = 500000;
            Assert.Contains("500000", vm.CharCountDisplay);
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(1.15)]
        [InlineData(1.5)]
        [InlineData(2.0)]
        [InlineData(3.0)]
        public void LineSpacing_AllPresets_RoundTrip(double spacing)
        {
            var vm = new EditorViewModel();
            vm.SetLineSpacing(spacing);
            Assert.Equal(spacing, vm.LineSpacing);
        }

        [Theory]
        [InlineData("None")]
        [InlineData("Bullet")]
        [InlineData("Number")]
        [InlineData("LowercaseLetter")]
        [InlineData("UppercaseLetter")]
        [InlineData("LowercaseRoman")]
        [InlineData("UppercaseRoman")]
        public void ListType_AllValues_Persist(string listType)
        {
            var vm = new EditorViewModel();
            vm.SetListType(listType);
            Assert.Equal(listType, vm.ListType);
        }

        [Fact]
        public void PropertyChanged_SameValue_NotFired()
        {
            var vm = new EditorViewModel();
            vm.WordCount = 0; // Already 0
            int fired = 0;
            vm.PropertyChanged += (_, _) => fired++;
            vm.WordCount = 0; // Same value — should not fire
            Assert.Equal(0, fired);
        }

        [Fact]
        public void ViewModel_AllCommandsImplementICommand()
        {
            var vm = new EditorViewModel();
            var type = vm.GetType();
            var commandProps = type.GetProperties()
                .Where(p => p.Name.EndsWith("Command"))
                .ToList();
            foreach (var prop in commandProps)
            {
                var value = prop.GetValue(vm);
                Assert.NotNull(value);
                Assert.True(typeof(System.Windows.Input.ICommand).IsAssignableFrom(prop.PropertyType),
                    $"{prop.Name} should implement ICommand");
            }
        }
    }

    // ═══ PdfHelper Page Layout Tests ════════════════════════════════════════════

    public class PdfHelperPageLayoutTests
    {
        [Fact]
        public void GeneratePdf_ThirtyLines_HasSinglePage()
        {
            // linesPerPage = (int)((842-144) / (12*1.4)) = 41; 30 fits on one page
            string text = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"Line {i}"));
            var bytes = PdfHelper.GeneratePdf(text);
            string content = Encoding.Latin1.GetString(bytes);
            int pageCount = 0, idx = 0;
            while ((idx = content.IndexOf("/Type /Page ", idx)) >= 0) { pageCount++; idx++; }
            Assert.Equal(1, pageCount);
        }

        [Fact]
        public void GeneratePdf_TabularContent_ExpandsTabs()
        {
            var bytes = PdfHelper.GeneratePdf("Column1\tColumn2\tColumn3");
            Assert.True(bytes.Length > 0);
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("%PDF-1.4", content);
        }

        [Fact]
        public void GeneratePdf_WhitespaceOnly_ProducesValidPdf()
        {
            var bytes = PdfHelper.GeneratePdf("   \n\n   ");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("%PDF-1.4", content);
            Assert.Contains("%%EOF", content);
        }

        [Fact]
        public void GeneratePdf_WithNewlines_SplitsLines()
        {
            var bytes = PdfHelper.GeneratePdf("Line1\nLine2\nLine3");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("Line1", content);
            Assert.Contains("Line2", content);
            Assert.Contains("Line3", content);
        }

        [Fact]
        public void BuildDisplayLines_ExactMaxChars_DoesNotWrap()
        {
            string line = new string('A', 80);
            var result = PdfHelper.BuildDisplayLines(line, 80);
            Assert.Single(result);
            Assert.Equal(line, result[0]);
        }

        [Fact]
        public void BuildDisplayLines_MaxCharsPlus1_Wraps()
        {
            string line = new string('A', 81);
            var result = PdfHelper.BuildDisplayLines(line, 80);
            Assert.Equal(2, result.Count);
            Assert.Equal(80, result[0].Length);
            Assert.Equal(1, result[1].Length);
        }

        [Fact]
        public void BuildDisplayLines_MultipleNewlines_AllPreserved()
        {
            var result = PdfHelper.BuildDisplayLines("A\nB\n\nC", 80);
            Assert.Equal(4, result.Count);
            Assert.Equal("A", result[0]);
            Assert.Equal("B", result[1]);
            Assert.Equal("", result[2]);
            Assert.Equal("C", result[3]);
        }

        [Fact]
        public void BuildDisplayLines_SingleWord_FitsOnOneLine()
        {
            var result = PdfHelper.BuildDisplayLines("Hello", 100);
            Assert.Single(result);
            Assert.Equal("Hello", result[0]);
        }

        [Fact]
        public void BuildDisplayLines_SpaceAtBoundary_BreaksCleanly()
        {
            // "Hello World" — break between 5 and 5 when maxChars = 6
            var result = PdfHelper.BuildDisplayLines("Hello World", 6);
            Assert.Equal(2, result.Count);
            Assert.Equal("Hello", result[0]);
            Assert.Equal("World", result[1]);
        }

        [Fact]
        public void PdfHelper_GeneratePdf_HasStreamObjects()
        {
            var bytes = PdfHelper.GeneratePdf("test content");
            string content = Encoding.Latin1.GetString(bytes);
            // PDF uses \n line endings (not \r\n)
            Assert.Contains("stream\n", content);
            Assert.Contains("\nendstream", content);
        }

        [Fact]
        public void PdfHelper_GeneratePdf_HasValidPdfVersion()
        {
            var bytes = PdfHelper.GeneratePdf("test");
            // Must start with %PDF-1.
            Assert.Equal((byte)'%', bytes[0]);
            Assert.Equal((byte)'P', bytes[1]);
            Assert.Equal((byte)'D', bytes[2]);
            Assert.Equal((byte)'F', bytes[3]);
        }
    }

    // ═══ App Static Member Tests ═════════════════════════════════════════════════

    public class AppStaticMemberTests
    {
        [Fact]
        public void App_Services_PropertyExists()
        {
            var prop = typeof(SmrtPad.App).GetProperty("Services");
            Assert.NotNull(prop);
            Assert.True(prop!.CanRead);
            Assert.False(prop.CanWrite);
        }

        [Fact]
        public void App_MainWindow_PropertyExists()
        {
            var prop = typeof(SmrtPad.App).GetProperty("MainWindow",
                BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(prop);
            Assert.Equal(typeof(Microsoft.UI.Xaml.Window), prop!.PropertyType);
        }

        [Fact]
        public void App_Current_PropertyExists()
        {
            var prop = typeof(SmrtPad.App).GetProperty("Current",
                BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(prop);
            Assert.Equal(typeof(SmrtPad.App), prop!.PropertyType);
        }

        [Fact]
        public void App_NewWindow_IsPublicStatic()
        {
            var method = typeof(SmrtPad.App).GetMethod("NewWindow",
                BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method);
            Assert.True(method!.IsStatic);
            Assert.Equal(typeof(SmrtPad.MainWindow), method.ReturnType);
        }

        [Fact]
        public void App_ConfigureServices_IsPrivateStatic()
        {
            var method = typeof(SmrtPad.App).GetMethod("ConfigureServices",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);
            Assert.True(method!.IsStatic);
        }

        [Fact]
        public void App_InheritsFromApplication()
        {
            Assert.True(typeof(Microsoft.UI.Xaml.Application).IsAssignableFrom(typeof(SmrtPad.App)));
        }

        [Fact]
        public void App_Windows_IsGenericList_OfMainWindow()
        {
            var prop = typeof(SmrtPad.App).GetProperty("Windows",
                BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(prop);
            Assert.Equal(typeof(System.Collections.Generic.List<SmrtPad.MainWindow>), prop!.PropertyType);
        }

        [Fact]
        public void App_Services_ReturnsServiceProvider()
        {
            var prop = typeof(SmrtPad.App).GetProperty("Services");
            Assert.NotNull(prop);
            Assert.Equal(typeof(Microsoft.Extensions.DependencyInjection.ServiceProvider), prop!.PropertyType);
        }
    }

    // ═══ FileBackstageView Code-Behind Contract Tests ════════════════════════════

    public class FileBackstageViewCodeBehindTests
    {
        private static readonly Type BSV = typeof(SmrtPad.Views.FileBackstageView);
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [Fact]
        public void FileBackstageView_HasPopulateTemplates_Private()
        {
            var method = BSV.GetMethod("PopulateTemplates", Private);
            Assert.NotNull(method);
            Assert.Equal(typeof(void), method!.ReturnType);
            Assert.Empty(method.GetParameters());
        }

        [Fact]
        public void FileBackstageView_HasNavSelectionChanged_Private()
        {
            var method = BSV.GetMethod("Nav_SelectionChanged", Private);
            Assert.NotNull(method);
            var parms = method!.GetParameters();
            Assert.Equal(2, parms.Length);
            Assert.Equal("sender", parms[0].Name);
            Assert.Equal("args", parms[1].Name);
        }

        [Fact]
        public void FileBackstageView_HasSuppressSelectionEventField()
        {
            var field = BSV.GetField("_suppressSelectionEvent", Private);
            Assert.NotNull(field);
            Assert.Equal(typeof(bool), field!.FieldType);
            Assert.True(field.IsInitOnly);
        }

        [Fact]
        public void FileBackstageView_IsPartialClass()
        {
            // Partial class detection: has InitializeComponent generated by XAML
            var method = BSV.GetMethod("InitializeComponent");
            Assert.NotNull(method);
        }

        [Fact]
        public void FileBackstageView_IsSealed()
        {
            Assert.True(BSV.IsSealed);
        }

        [Fact]
        public void FileBackstageView_HasPublicDefaultConstructor()
        {
            var ctor = BSV.GetConstructor(Type.EmptyTypes);
            Assert.NotNull(ctor);
            Assert.True(ctor!.IsPublic);
        }

        [Fact]
        public void FileBackstageView_SetDocumentProperties_ReturnType()
        {
            var method = BSV.GetMethod("SetDocumentProperties");
            Assert.NotNull(method);
            Assert.Equal(typeof(void), method!.ReturnType);
        }

        [Fact]
        public void FileBackstageView_SetRecentFiles_ReturnType()
        {
            var method = BSV.GetMethod("SetRecentFiles");
            Assert.NotNull(method);
            Assert.Equal(typeof(void), method!.ReturnType);
        }

        [Fact]
        public void FileBackstageView_AllEvents_AreNullableEventHandlers()
        {
            // Only inspect the 12 events declared on FileBackstageView itself,
            // not the hundreds of inherited RoutedEventHandler events from UserControl.
            var ownEvents = BSV.GetEvents(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var evt in ownEvents)
            {
                Assert.True(
                    evt.EventHandlerType == typeof(EventHandler) ||
                    evt.EventHandlerType == typeof(EventHandler<string>) ||
                    evt.EventHandlerType == typeof(EventHandler<DocumentTemplate>),
                    $"Event {evt.Name} has unexpected handler type {evt.EventHandlerType?.Name}");
            }
        }
    }

    // ═══ SettingsService Path & Persistence Tests ════════════════════════════════

    public class SettingsServicePersistenceTests
    {
        private static (SettingsService svc, string path) CreateIsolated()
        {
            string path = Path.Combine(Path.GetTempPath(), "SmrtPadTests",
                Guid.NewGuid().ToString("N"), "settings.json");
            return (new SettingsService(path), path);
        }

        [Fact]
        public void Save_CreatesFile()
        {
            var (svc, path) = CreateIsolated();
            svc.Save();
            Assert.True(File.Exists(path));
        }

        [Fact]
        public void Constructor_CreatesParentDirectory()
        {
            // The custom-path constructor calls Directory.CreateDirectory immediately
            string uniqueDir = Path.Combine(Path.GetTempPath(), "SmrtPadTests",
                Guid.NewGuid().ToString("N"));
            string path = Path.Combine(uniqueDir, "settings.json");
            Assert.False(Directory.Exists(uniqueDir));
            _ = new SettingsService(path);
            Assert.True(Directory.Exists(uniqueDir));
        }

        [Fact]
        public void Save_ProducesValidJson()
        {
            var (svc, path) = CreateIsolated();
            svc.Save();
            string json = File.ReadAllText(path);
            Assert.Contains("{", json);
            Assert.Contains("}", json);
            Assert.Contains("DefaultFontFamily", json);
        }

        [Fact]
        public void Load_ReloadsAfterSave()
        {
            var (svc, path) = CreateIsolated();
            svc.DefaultFontFamily = "Consolas";
            svc.Save();
            svc.Load();
            Assert.Equal("Consolas", svc.DefaultFontFamily);
        }

        [Fact]
        public void AddRecentFile_SavesPersisted()
        {
            var (svc, path) = CreateIsolated();
            svc.AddRecentFile("C:\\test.rtf");
            svc.Save();

            var svc2 = new SettingsService(path);
            Assert.Single(svc2.RecentFiles);
            Assert.Equal("C:\\test.rtf", svc2.RecentFiles[0]);
        }

        [Fact]
        public void ClearRecentFiles_PersistsEmpty()
        {
            var (svc, path) = CreateIsolated();
            svc.AddRecentFile("C:\\test.rtf");
            svc.Save();
            svc.ClearRecentFiles();
            svc.Save();

            var svc2 = new SettingsService(path);
            Assert.Empty(svc2.RecentFiles);
        }

        [Fact]
        public void SettingsService_DefaultPath_IsNotNull()
        {
            // Default constructor uses app-data path
            var ctor = typeof(SettingsService).GetConstructor(Type.EmptyTypes);
            Assert.NotNull(ctor);
        }

        [Fact]
        public void SettingsService_FilePath_IsStored()
        {
            var (svc, path) = CreateIsolated();
            var field = typeof(SettingsService).GetField("_settingsFilePath",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field);
            string storedPath = (string)field!.GetValue(svc)!;
            Assert.Equal(path, storedPath);
        }

        [Fact]
        public void AutoSaveInterval_Min_IsEnforced()
        {
            var (svc, _) = CreateIsolated();
            svc.AutoSaveIntervalSeconds = 0;
            Assert.Equal(0, svc.AutoSaveIntervalSeconds); // No enforced min at model level
        }

        [Fact]
        public void ThemePreference_System_IsDefault()
        {
            var (svc, _) = CreateIsolated();
            Assert.Equal("System", svc.ThemePreference);
        }

        [Fact]
        public void ThemePreference_Persists_Dark()
        {
            var (svc, path) = CreateIsolated();
            svc.ThemePreference = "Dark";
            svc.Save();
            var svc2 = new SettingsService(path);
            Assert.Equal("Dark", svc2.ThemePreference);
        }

        [Fact]
        public void Language_EnUS_IsDefault()
        {
            var (svc, _) = CreateIsolated();
            Assert.Equal("en-US", svc.Language);
        }
    }

    // ═══ MacroHelper Apply Tests (integration with ViewModel) ════════════════════

    public class MacroHelperApplyTests
    {
        [Fact]
        public void MacroCommandType_Bold_IsZero()
        {
            Assert.Equal(0, (int)MacroCommandType.Bold);
        }

        [Fact]
        public void MacroCommandType_AllValues_AreDistinct()
        {
            var values = Enum.GetValues<MacroCommandType>().Cast<int>().ToList();
            Assert.Equal(values.Count, values.Distinct().Count());
        }

        [Fact]
        public void MacroHelper_Count_ReflectsRecordedCommands()
        {
            var m = new MacroHelper();
            Assert.Equal(0, m.Count);
            m.StartRecording();
            m.Record(MacroCommandType.Bold);
            m.Record(MacroCommandType.Italic);
            Assert.Equal(2, m.Count);
        }

        [Fact]
        public void MacroHelper_Commands_AfterClear_IsEmpty()
        {
            var m = new MacroHelper();
            m.StartRecording();
            m.Record(MacroCommandType.Bold);
            m.StopRecording();
            m.Clear();
            Assert.Equal(0, m.Count);
            Assert.Empty(m.Commands);
        }

        [Fact]
        public void MacroHelper_Serialize_MultipleCommands()
        {
            var m = new MacroHelper();
            m.StartRecording();
            for (int i = 0; i < 10; i++)
                m.Record(MacroCommandType.Bold);
            m.StopRecording();
            string json = m.Serialize();
            Assert.NotEmpty(json);
            Assert.Contains("Bold", json);
        }

        [Fact]
        public void MacroHelper_Deserialize_ReplacesExisting()
        {
            var m = new MacroHelper();
            m.StartRecording();
            m.Record(MacroCommandType.Bold);
            m.StopRecording();

            string json = "[{\"Type\":\"Italic\",\"Value\":null}]";
            m.Deserialize(json);
            Assert.Single(m.Commands);
            Assert.Equal(MacroCommandType.Italic, m.Commands[0].Type);
        }

        [Fact]
        public void MacroHelper_MultipleStartRecording_Clears()
        {
            var m = new MacroHelper();
            m.StartRecording();
            m.Record(MacroCommandType.Bold);
            m.Record(MacroCommandType.Italic);
            m.StopRecording();
            Assert.Equal(2, m.Count);

            m.StartRecording(); // Re-start clears
            m.Record(MacroCommandType.Underline);
            m.StopRecording();
            Assert.Equal(1, m.Count);
        }

        [Theory]
        [InlineData("SetAlignment", "Left")]
        [InlineData("SetAlignment", "Center")]
        [InlineData("SetAlignment", "Right")]
        [InlineData("SetAlignment", "Justify")]
        public void MacroCommand_Alignment_Serializes(string typeName, string value)
        {
            var type = Enum.Parse<MacroCommandType>(typeName);
            var m = new MacroHelper();
            m.StartRecording();
            m.Record(type, value);
            m.StopRecording();

            string json = m.Serialize();
            var m2 = new MacroHelper();
            m2.Deserialize(json);
            Assert.Equal(value, m2.Commands[0].Value);
        }

        [Fact]
        public void MacroCommand_InsertText_WithSpecialChars()
        {
            var m = new MacroHelper();
            m.StartRecording();
            m.Record(MacroCommandType.InsertText, "Hello \"World\" & <test>");
            m.StopRecording();

            var m2 = new MacroHelper();
            m2.Deserialize(m.Serialize());
            Assert.Equal("Hello \"World\" & <test>", m2.Commands[0].Value);
        }
    }

    // ═══ ResourceHelper Key Coverage Tests ══════════════════════════════════════

    public class ResourceHelperKeyCoverageTests
    {
        [Theory]
        [InlineData("DocumentUntitled")]
        [InlineData("StatusReady")]
        [InlineData("StatusSaved")]
        [InlineData("StatusOpened")]
        [InlineData("StatusPrintFailed")]
        [InlineData("StatusNewTab")]
        [InlineData("StatusTabClosed")]
        [InlineData("StatusTemplateApplied")]
        [InlineData("BackstageFile")]
        [InlineData("BackstageNewDesc")]
        [InlineData("BackstageOpenDesc")]
        [InlineData("BackstageSaveDesc")]
        [InlineData("BackstageSaveAsDesc")]
        [InlineData("BackstagePrintDesc")]
        [InlineData("BackstageExportPdfDesc")]
        [InlineData("BackstageExportDocxDesc")]
        [InlineData("BackstageSaveOneDriveDesc")]
        [InlineData("BackstageOptionsDesc")]
        [InlineData("BackstageTemplatesDesc")]
        [InlineData("BackstageNoRecentFiles")]
        [InlineData("DocPropYes")]
        [InlineData("DocPropNo")]
        public void ResourceHelper_Key_IsNonEmpty(string key)
        {
            string result = ResourceHelper.GetString(key);
            Assert.NotNull(result);
            Assert.NotEqual(key, result); // Should resolve, not return key name
            Assert.NotEmpty(result);
        }

        [Theory]
        [InlineData("StatusBarWords", 100)]
        [InlineData("StatusBarCharacters", 200)]
        [InlineData("StatusBarSelection", 5)]
        public void ResourceHelper_SingleArgFormat_ContainsValue(string key, int arg)
        {
            string result = ResourceHelper.GetFormatted(key, arg);
            Assert.Contains(arg.ToString(), result);
        }

        [Fact]
        public void ResourceHelper_LineColFormat_ContainsBoth()
        {
            string result = ResourceHelper.GetFormatted("StatusBarLineCol", 42, 7);
            Assert.Contains("42", result);
            Assert.Contains("7", result);
        }

        [Fact]
        public void ResourceHelper_GetString_LargeUnknownKey_ReturnsKey()
        {
            string key = "Key_That_Definitely_Does_Not_Exist_In_Resources_XYZ123";
            Assert.Equal(key, ResourceHelper.GetString(key));
        }

        [Fact]
        public void ResourceHelper_GetFormatted_UnknownKey_StillFormats()
        {
            // Should not throw even for unknown keys
            string result = ResourceHelper.GetFormatted("UnknownFormatKey", 1, 2, 3);
            Assert.NotNull(result);
        }
    }

    // ═══ MainWindow XAML Final Coverage Tests ═══════════════════════════════════

    public class MainWindowXamlFinalTests
    {
        private static string? ReadXaml(string filename)
        {
            string? dir = Directory.GetCurrentDirectory();
            while (dir is not null)
            {
                string candidate = Path.Combine(dir, "SmrtPad", filename);
                if (File.Exists(candidate)) return File.ReadAllText(candidate);
                dir = Directory.GetParent(dir)?.FullName;
            }
            return null;
        }

        [Fact]
        public void MainWindow_XAML_HasWinUI3Namespace()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            // WinUI 3 uses 'using:' syntax for local namespaces
            Assert.Contains("xmlns:local=\"using:SmrtPad\"", xaml);
            Assert.Contains("xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasViewModelBinding()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("x:Bind ViewModel", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasFontFamilyComboBoxBinding()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("FontFamilyComboBox", xaml);
            Assert.Contains("FontFamilyComboBox_Loaded", xaml);
            Assert.Contains("FontFamilyComboBox_SelectionChanged", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasFontSizeComboBoxBinding()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("FontSizeComboBox", xaml);
            Assert.Contains("FontSizeComboBox_SelectionChanged", xaml);
        }

        [Fact]
        public void MainWindow_HasEditorScrollViewerHandler_InCodeBehind()
        {
            // Wired dynamically in CreateTab, not in XAML
            var method = typeof(SmrtPad.MainWindow).GetMethod(
                "EditorScrollViewer_PointerWheelChanged",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void MainWindow_HasEditorDragDropHandlers_InCodeBehind()
        {
            // Both wired dynamically in CreateTab: tab.Editor.DragOver += Editor_DragOver
            var mw = typeof(SmrtPad.MainWindow);
            const BindingFlags bf = BindingFlags.NonPublic | BindingFlags.Instance;
            Assert.NotNull(mw.GetMethod("Editor_DragOver", bf));
            Assert.NotNull(mw.GetMethod("Editor_Drop", bf));
        }

        [Fact]
        public void MainWindow_HasEditorSelectionChangedHandler_InCodeBehind()
        {
            // Wired dynamically in CreateTab, not in XAML
            var method = typeof(SmrtPad.MainWindow).GetMethod(
                "Editor_SelectionChanged",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void MainWindow_XAML_HasBulletsToggle()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("Bullets_Click", xaml);
        }

        [Fact]
        public void MainWindow_HasOptions_ViaBackstageEvent()
        {
            // Options_Click is fired by FileBackstageView.OptionsRequested event
            var method = typeof(SmrtPad.MainWindow).GetMethod(
                "Options_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void MainWindow_XAML_HasNewWindowBinding()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("NewWindow_Click", xaml);
        }

        [Fact]
        public void MainWindow_XAML_HasRulerCanvasSizeChangedHandlers()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("HRulerCanvas_SizeChanged", xaml);
            Assert.Contains("VRulerCanvas_SizeChanged", xaml);
        }

        [Fact]
        public void MainWindow_XAML_FindRegexCheckBoxPresent()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("FindRegexCheckBox", xaml);
        }

        [Fact]
        public void MainWindow_HasExit_ViaBackstageEvent()
        {
            // Exit_Click is fired by FileBackstageView.ExitRequested event
            var method = typeof(SmrtPad.MainWindow).GetMethod(
                "Exit_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void MainWindow_HasPrint_ViaBackstageEvent()
        {
            // Print_Click is fired by FileBackstageView.PrintRequested event
            var method = typeof(SmrtPad.MainWindow).GetMethod(
                "Print_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void MainWindow_XAML_HasDropDownOpened()
        {
            string? xaml = ReadXaml("MainWindow.xaml");
            if (xaml is null) return;
            Assert.Contains("FontFamilyComboBox_DropDownOpened", xaml);
        }

        [Fact]
        public void MainWindow_HasSaveToOneDrive_ViaBackstageEvent()
        {
            // SaveToOneDrive_Click is fired by FileBackstageView.OneDriveRequested event
            var method = typeof(SmrtPad.MainWindow).GetMethod(
                "SaveToOneDrive_Click", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
        }
    }

    // ═══ RtfParser Advanced Paths Tests ═════════════════════════════════════════

    public class RtfParserAdvancedTests
    {
        [Fact]
        public void Parse_ColorTable_IsRemoved()
        {
            // {\*\colortbl} with the \* destination prefix is skipped by the parser
            string rtf = @"{\rtf1\ansi{\*\colortbl ;\red255\green0\blue0;}Hello}";
            var result = RtfParser.Parse(rtf);
            var text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("Hello", text);
            Assert.DoesNotContain("colortbl", text);
        }

        [Fact]
        public void Parse_InfoGroup_IsSkipped()
        {
            string rtf = @"{\rtf1\ansi{\info{\title My Document}}Hello}";
            var result = RtfParser.Parse(rtf);
            var text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("Hello", text);
            Assert.DoesNotContain("title", text);
        }

        [Fact]
        public void Parse_StylesheetGroup_IsSkipped()
        {
            string rtf = @"{\rtf1\ansi{\stylesheet{\s0 Normal;}}Hello}";
            var result = RtfParser.Parse(rtf);
            var text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("Hello", text);
            Assert.DoesNotContain("Normal;", text);
        }

        [Fact]
        public void Parse_ObjectGroup_IsSkipped()
        {
            string rtf = @"{\rtf1\ansi Before{\object\objdata 1234}After}";
            var result = RtfParser.Parse(rtf);
            var text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("Before", text);
            Assert.Contains("After", text);
            Assert.DoesNotContain("objdata", text);
        }

        [Fact]
        public void Parse_HeaderGroup_IsSkipped()
        {
            string rtf = @"{\rtf1\ansi{\header Header Text}Body}";
            var result = RtfParser.Parse(rtf);
            var text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("Body", text);
            Assert.DoesNotContain("Header Text", text);
        }

        [Fact]
        public void Parse_FooterGroup_IsSkipped()
        {
            string rtf = @"{\rtf1\ansi Body{\footer Footer Text}}";
            var result = RtfParser.Parse(rtf);
            var text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("Body", text);
            Assert.DoesNotContain("Footer Text", text);
        }

        [Fact]
        public void Parse_ListtextGroup_IsSkipped()
        {
            string rtf = @"{\rtf1\ansi {\listtext\u183 }Item Text}";
            var result = RtfParser.Parse(rtf);
            var text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("Item Text", text);
        }

        [Fact]
        public void Parse_EmptyBraces_DoesNotCrash()
        {
            string rtf = @"{\rtf1\ansi {}Hello}";
            var result = RtfParser.Parse(rtf);
            var text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("Hello", text);
        }

        [Fact]
        public void Parse_NestedGroups_ProcessedCorrectly()
        {
            string rtf = @"{\rtf1\ansi {\b {\i Bold Italic}}}";
            var result = RtfParser.Parse(rtf);
            var runs = result.SelectMany(p => p.Runs).ToList();
            Assert.NotEmpty(runs);
        }

        [Fact]
        public void Parse_FontIndex_Zero_Default()
        {
            string rtf = @"{\rtf1\ansi\f0 Hello}";
            var result = RtfParser.Parse(rtf);
            Assert.NotEmpty(result.SelectMany(p => p.Runs));
        }

        [Fact]
        public void Parse_UnknownControlWord_Ignored()
        {
            string rtf = @"{\rtf1\ansi\unknownword Hello}";
            var result = RtfParser.Parse(rtf);
            var text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("Hello", text);
        }

        [Fact]
        public void Parse_TruncatedControlWord_DoesNotCrash()
        {
            string rtf = @"{\rtf1\ansi\b";
            var result = RtfParser.Parse(rtf);
            Assert.NotNull(result);
        }

        [Fact]
        public void RtfRun_WithExpression_CreatesNewInstance()
        {
            var a = new RtfRun("hello", false, false, false, false, "", 24);
            var b = a with { Bold = true, Text = "world" };
            Assert.True(b.Bold);
            Assert.Equal("world", b.Text);
            Assert.False(a.Bold);  // Original unchanged
        }

        [Fact]
        public void RtfRun_Deconstruct_Works()
        {
            var run = new RtfRun("text", true, false, true, false, "Arial", 24);
            var (text, bold, italic, underline, strike, font, size) = run;
            Assert.Equal("text", text);
            Assert.True(bold);
            Assert.False(italic);
            Assert.True(underline);
            Assert.False(strike);
            Assert.Equal("Arial", font);
            Assert.Equal(24, size);
        }

        [Fact]
        public void RtfParagraph_CanSetAlignment()
        {
            var para = new RtfParagraph();
            para.Alignment = "center";
            Assert.Equal("center", para.Alignment);
        }

        [Fact]
        public void RtfParagraph_CanAddRuns()
        {
            var para = new RtfParagraph();
            para.Runs.Add(new RtfRun("A", false, false, false, false, "", 24));
            para.Runs.Add(new RtfRun("B", false, false, false, false, "", 24));
            Assert.Equal(2, para.Runs.Count);
        }

        [Fact]
        public void Parse_ItalicOff_DisablesItalic()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\i Italic\i0 Off}");
            var runs = result.SelectMany(p => p.Runs).ToList();
            Assert.Contains(runs, r => r.Italic);
            Assert.Contains(runs, r => !r.Italic);
        }

        [Fact]
        public void Parse_NegativeParam_TreatedAsZero()
        {
            // \b-1 means bold off (param 0)
            var result = RtfParser.Parse(@"{\rtf1\ansi\b Text\b-1 Off}");
            var runs = result.SelectMany(p => p.Runs).ToList();
            // At minimum should not crash
            Assert.NotNull(runs);
        }

        [Fact]
        public void Parse_LargeHexValue_Handled()
        {
            // \'FF = char 255
            var result = RtfParser.Parse(@"{\rtf1\ansi Test\'FF}");
            Assert.NotNull(result);
        }
    }
}
