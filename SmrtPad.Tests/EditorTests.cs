using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using SmrtPad.ViewModels;
using SmrtPad.Helpers;
using SmrtPad.Services;

namespace SmrtPad.Tests
{
    public class EditorTests
    {
        [Fact]
        public void NewDocument_ResetsTitleAndStatus()
        {
            // Arrange
            var viewModel = new EditorViewModel();
            viewModel.DocumentTitle = "SomeFile.rtf";
            viewModel.StatusMessage = "Saved SomeFile.rtf";
            viewModel.IsModified = true;
            viewModel.FontFamily = "Arial";
            viewModel.FontSize = 14.0;
            viewModel.IsBold = true;
            viewModel.IsItalic = true;
            viewModel.IsUnderline = true;
            viewModel.IsStrikethrough = true;

            // Act
            viewModel.NewDocument();

            // Assert
            Assert.Equal("Untitled", viewModel.DocumentTitle);
            Assert.Equal("New document created.", viewModel.StatusMessage);
            Assert.False(viewModel.IsModified);
            Assert.Equal("Segoe UI", viewModel.FontFamily);
            Assert.Equal(11.0, viewModel.FontSize);
            Assert.False(viewModel.IsBold);
            Assert.False(viewModel.IsItalic);
            Assert.False(viewModel.IsUnderline);
            Assert.False(viewModel.IsStrikethrough);
        }

        [Fact]
        public void NewDocument_ResetsWordWrapAndZoom()
        {
            var viewModel = new EditorViewModel();
            viewModel.IsWordWrap = false;
            viewModel.ZoomLevel = 200.0;

            viewModel.NewDocument();

            Assert.True(viewModel.IsWordWrap);
            Assert.Equal(100.0, viewModel.ZoomLevel);
        }

        [Fact]
        public void NewDocument_ResetsListTypeAndLineSpacing()
        {
            var viewModel = new EditorViewModel();
            viewModel.ListType = "Bullet";
            viewModel.LineSpacing = 2.0;

            viewModel.NewDocument();

            Assert.Equal("None", viewModel.ListType);
            Assert.Equal(1.0, viewModel.LineSpacing);
        }

        [Fact]
        public void UpdateStatus_ChangesStatusMessage()
        {
            // Arrange
            var viewModel = new EditorViewModel();

            // Act
            viewModel.UpdateStatus("Test Status");

            // Assert
            Assert.Equal("Test Status", viewModel.StatusMessage);
        }

        [Fact]
        public void ToggleBold_TogglesIsBold()
        {
            var viewModel = new EditorViewModel();
            Assert.False(viewModel.IsBold);
            viewModel.ToggleBold();
            Assert.True(viewModel.IsBold);
            viewModel.ToggleBold();
            Assert.False(viewModel.IsBold);
        }

        [Fact]
        public void ToggleItalic_TogglesIsItalic()
        {
            var viewModel = new EditorViewModel();
            Assert.False(viewModel.IsItalic);
            viewModel.ToggleItalic();
            Assert.True(viewModel.IsItalic);
            viewModel.ToggleItalic();
            Assert.False(viewModel.IsItalic);
        }

        [Fact]
        public void ToggleUnderline_TogglesIsUnderline()
        {
            var viewModel = new EditorViewModel();
            Assert.False(viewModel.IsUnderline);
            viewModel.ToggleUnderline();
            Assert.True(viewModel.IsUnderline);
            viewModel.ToggleUnderline();
            Assert.False(viewModel.IsUnderline);
        }

        [Fact]
        public void ToggleStrikethrough_TogglesIsStrikethrough()
        {
            var viewModel = new EditorViewModel();
            Assert.False(viewModel.IsStrikethrough);
            viewModel.ToggleStrikethrough();
            Assert.True(viewModel.IsStrikethrough);
            viewModel.ToggleStrikethrough();
            Assert.False(viewModel.IsStrikethrough);
        }

        [Fact]
        public void ToggleSubscript_TogglesIsSubscriptAndClearsSuperscript()
        {
            var viewModel = new EditorViewModel();
            viewModel.IsSuperscript = true;
            Assert.False(viewModel.IsSubscript);

            viewModel.ToggleSubscript();

            Assert.True(viewModel.IsSubscript);
            Assert.False(viewModel.IsSuperscript);

            viewModel.ToggleSubscript();
            Assert.False(viewModel.IsSubscript);
        }

        [Fact]
        public void ToggleSuperscript_TogglesIsSuperscriptAndClearsSubscript()
        {
            var viewModel = new EditorViewModel();
            viewModel.IsSubscript = true;
            Assert.False(viewModel.IsSuperscript);

            viewModel.ToggleSuperscript();

            Assert.True(viewModel.IsSuperscript);
            Assert.False(viewModel.IsSubscript);

            viewModel.ToggleSuperscript();
            Assert.False(viewModel.IsSuperscript);
        }

        [Fact]
        public void SetAlignment_ChangesAlignment()
        {
            var viewModel = new EditorViewModel();
            Assert.Equal("Left", viewModel.Alignment);

            viewModel.SetAlignment("Center");
            Assert.Equal("Center", viewModel.Alignment);

            viewModel.SetAlignment("Right");
            Assert.Equal("Right", viewModel.Alignment);
        }

        [Fact]
        public void ToggleBullets_TogglesIsBullets()
        {
            var viewModel = new EditorViewModel();
            Assert.False(viewModel.IsBullets);
            viewModel.ToggleBullets();
            Assert.True(viewModel.IsBullets);
            viewModel.ToggleBullets();
            Assert.False(viewModel.IsBullets);
        }

        // ═══ New Ribbon Feature Tests ═══

        [Fact]
        public void ToggleWordWrap_TogglesIsWordWrap()
        {
            var viewModel = new EditorViewModel();
            Assert.True(viewModel.IsWordWrap);
            viewModel.ToggleWordWrap();
            Assert.False(viewModel.IsWordWrap);
            viewModel.ToggleWordWrap();
            Assert.True(viewModel.IsWordWrap);
        }

        [Fact]
        public void ZoomIn_IncreasesZoomLevel()
        {
            var viewModel = new EditorViewModel();
            Assert.Equal(100.0, viewModel.ZoomLevel);

            viewModel.ZoomIn();
            Assert.Equal(110.0, viewModel.ZoomLevel);

            viewModel.ZoomIn();
            Assert.Equal(120.0, viewModel.ZoomLevel);
        }

        [Fact]
        public void ZoomOut_DecreasesZoomLevel()
        {
            var viewModel = new EditorViewModel();
            Assert.Equal(100.0, viewModel.ZoomLevel);

            viewModel.ZoomOut();
            Assert.Equal(90.0, viewModel.ZoomLevel);

            viewModel.ZoomOut();
            Assert.Equal(80.0, viewModel.ZoomLevel);
        }

        [Fact]
        public void ZoomIn_ClampsAtMaximum()
        {
            var viewModel = new EditorViewModel();
            viewModel.ZoomLevel = 500.0;

            viewModel.ZoomIn();
            Assert.Equal(500.0, viewModel.ZoomLevel);
        }

        [Fact]
        public void ZoomOut_ClampsAtMinimum()
        {
            var viewModel = new EditorViewModel();
            viewModel.ZoomLevel = 10.0;

            viewModel.ZoomOut();
            Assert.Equal(10.0, viewModel.ZoomLevel);
        }

        [Fact]
        public void SetListType_SetsListTypeAndUpdatesBullets()
        {
            var viewModel = new EditorViewModel();
            Assert.Equal("None", viewModel.ListType);
            Assert.False(viewModel.IsBullets);

            viewModel.SetListType("Bullet");
            Assert.Equal("Bullet", viewModel.ListType);
            Assert.True(viewModel.IsBullets);

            viewModel.SetListType("Number");
            Assert.Equal("Number", viewModel.ListType);
            Assert.True(viewModel.IsBullets);

            viewModel.SetListType("None");
            Assert.Equal("None", viewModel.ListType);
            Assert.False(viewModel.IsBullets);
        }

        [Theory]
        [InlineData("LowercaseLetter")]
        [InlineData("UppercaseLetter")]
        [InlineData("LowercaseRoman")]
        [InlineData("UppercaseRoman")]
        public void SetListType_AllListTypes_SetIsBulletsTrue(string listType)
        {
            var viewModel = new EditorViewModel();

            viewModel.SetListType(listType);

            Assert.Equal(listType, viewModel.ListType);
            Assert.True(viewModel.IsBullets);
        }

        [Fact]
        public void SetLineSpacing_ChangesLineSpacing()
        {
            var viewModel = new EditorViewModel();
            Assert.Equal(1.0, viewModel.LineSpacing);

            viewModel.SetLineSpacing(1.5);
            Assert.Equal(1.5, viewModel.LineSpacing);

            viewModel.SetLineSpacing(2.0);
            Assert.Equal(2.0, viewModel.LineSpacing);
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(1.15)]
        [InlineData(1.5)]
        [InlineData(2.0)]
        public void SetLineSpacing_SupportsAllStandardValues(double spacing)
        {
            var viewModel = new EditorViewModel();

            viewModel.SetLineSpacing(spacing);

            Assert.Equal(spacing, viewModel.LineSpacing);
        }

        [Fact]
        public void SetAlignment_SupportsJustify()
        {
            var viewModel = new EditorViewModel();

            viewModel.SetAlignment("Justify");
            Assert.Equal("Justify", viewModel.Alignment);
        }

        [Theory]
        [InlineData("Left")]
        [InlineData("Center")]
        [InlineData("Right")]
        [InlineData("Justify")]
        public void SetAlignment_AllAlignments(string alignment)
        {
            var viewModel = new EditorViewModel();
            viewModel.SetAlignment(alignment);
            Assert.Equal(alignment, viewModel.Alignment);
        }

        [Fact]
        public void NewDocument_ResetsAllFormattingState()
        {
            var viewModel = new EditorViewModel();

            // Set everything to non-default
            viewModel.DocumentTitle = "Test.rtf";
            viewModel.StatusMessage = "Saved";
            viewModel.IsModified = true;
            viewModel.FontFamily = "Arial";
            viewModel.FontSize = 24.0;
            viewModel.IsBold = true;
            viewModel.IsItalic = true;
            viewModel.IsUnderline = true;
            viewModel.IsStrikethrough = true;
            viewModel.IsSubscript = true;
            viewModel.IsSuperscript = true;
            viewModel.Alignment = "Center";
            viewModel.IsBullets = true;
            viewModel.IsWordWrap = false;
            viewModel.ZoomLevel = 200.0;
            viewModel.ListType = "Bullet";
            viewModel.LineSpacing = 2.0;

            viewModel.NewDocument();

            // Verify everything is reset
            Assert.Equal("Untitled", viewModel.DocumentTitle);
            Assert.Equal("New document created.", viewModel.StatusMessage);
            Assert.False(viewModel.IsModified);
            Assert.Equal("Segoe UI", viewModel.FontFamily);
            Assert.Equal(11.0, viewModel.FontSize);
            Assert.False(viewModel.IsBold);
            Assert.False(viewModel.IsItalic);
            Assert.False(viewModel.IsUnderline);
            Assert.False(viewModel.IsStrikethrough);
            Assert.False(viewModel.IsSubscript);
            Assert.False(viewModel.IsSuperscript);
            Assert.Equal("Left", viewModel.Alignment);
            Assert.False(viewModel.IsBullets);
            Assert.True(viewModel.IsWordWrap);
            Assert.Equal(100.0, viewModel.ZoomLevel);
            Assert.Equal("None", viewModel.ListType);
            Assert.Equal(1.0, viewModel.LineSpacing);
        }

        [Fact]
        public void ViewModel_DefaultValues()
        {
            var viewModel = new EditorViewModel();

            Assert.Equal("Untitled", viewModel.DocumentTitle);
            Assert.Equal("Ready", viewModel.StatusMessage);
            Assert.False(viewModel.IsModified);
            Assert.Equal("Segoe UI", viewModel.FontFamily);
            Assert.Equal(11.0, viewModel.FontSize);
            Assert.False(viewModel.IsBold);
            Assert.False(viewModel.IsItalic);
            Assert.False(viewModel.IsUnderline);
            Assert.False(viewModel.IsStrikethrough);
            Assert.False(viewModel.IsSubscript);
            Assert.False(viewModel.IsSuperscript);
            Assert.Equal("Left", viewModel.Alignment);
            Assert.False(viewModel.IsBullets);
            Assert.True(viewModel.IsWordWrap);
            Assert.Equal(100.0, viewModel.ZoomLevel);
            Assert.Equal("None", viewModel.ListType);
            Assert.Equal(1.0, viewModel.LineSpacing);
        }

        [Fact]
        public void PropertyChanged_FiredOnDocumentTitleChange()
        {
            var viewModel = new EditorViewModel();
            string? changedProperty = null;
            viewModel.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

            viewModel.DocumentTitle = "NewDoc.rtf";

            Assert.Equal(nameof(EditorViewModel.DocumentTitle), changedProperty);
        }

        [Fact]
        public void PropertyChanged_FiredOnZoomLevelChange()
        {
            var viewModel = new EditorViewModel();
            string? changedProperty = null;
            viewModel.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

            viewModel.ZoomLevel = 150.0;

            Assert.Equal(nameof(EditorViewModel.ZoomLevel), changedProperty);
        }

        [Fact]
        public void PropertyChanged_FiredOnListTypeChange()
        {
            var viewModel = new EditorViewModel();
            var changedProperties = new System.Collections.Generic.List<string>();
            viewModel.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

            viewModel.SetListType("Bullet");

            Assert.Contains(nameof(EditorViewModel.ListType), changedProperties);
            Assert.Contains(nameof(EditorViewModel.IsBullets), changedProperties);
        }

        [Fact]
        public void PropertyChanged_FiredOnLineSpacingChange()
        {
            var viewModel = new EditorViewModel();
            string? changedProperty = null;
            viewModel.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

            viewModel.LineSpacing = 1.5;

            Assert.Equal(nameof(EditorViewModel.LineSpacing), changedProperty);
        }

        [Fact]
        public void ZoomIn_MultipleIncrements()
        {
            var viewModel = new EditorViewModel();
            for (int i = 0; i < 5; i++)
                viewModel.ZoomIn();
            Assert.Equal(150.0, viewModel.ZoomLevel);
        }

        [Fact]
        public void ZoomOut_MultipleDecrements()
        {
            var viewModel = new EditorViewModel();
            for (int i = 0; i < 5; i++)
                viewModel.ZoomOut();
            Assert.Equal(50.0, viewModel.ZoomLevel);
        }
    }

    public class ParseHexColorTests
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
            var color = ColorHelper.ParseHexColor("#ffffff");
            Assert.Equal(255, color.A);
            Assert.Equal(255, color.R);
            Assert.Equal(255, color.G);
            Assert.Equal(255, color.B);
        }

        [Fact]
        public void ParseHexColor_Red()
        {
            var color = ColorHelper.ParseHexColor("#ff0000");
            Assert.Equal(255, color.A);
            Assert.Equal(255, color.R);
            Assert.Equal(0, color.G);
            Assert.Equal(0, color.B);
        }

        [Fact]
        public void ParseHexColor_Green()
        {
            var color = ColorHelper.ParseHexColor("#00b050");
            Assert.Equal(255, color.A);
            Assert.Equal(0, color.R);
            Assert.Equal(176, color.G);
            Assert.Equal(80, color.B);
        }

        [Fact]
        public void ParseHexColor_WithoutHash()
        {
            var color = ColorHelper.ParseHexColor("004dbb");
            Assert.Equal(255, color.A);
            Assert.Equal(0, color.R);
            Assert.Equal(77, color.G);
            Assert.Equal(187, color.B);
        }

        [Fact]
        public void ParseHexColor_WithAlpha()
        {
            var color = ColorHelper.ParseHexColor("#80FF0000");
            Assert.Equal(128, color.A);
            Assert.Equal(255, color.R);
            Assert.Equal(0, color.G);
            Assert.Equal(0, color.B);
        }

        [Theory]
        [InlineData("#FFFF00", 255, 255, 0)]
        [InlineData("#00FFFF", 0, 255, 255)]
        [InlineData("#FF00FF", 255, 0, 255)]
        [InlineData("#9b00d3", 155, 0, 211)]
        [InlineData("#c0504d", 192, 80, 77)]
        [InlineData("#f79646", 247, 150, 70)]
        [InlineData("#4bacc6", 75, 172, 198)]
        public void ParseHexColor_VariousColorSwatches(string hex, byte r, byte g, byte b)
        {
            var color = ColorHelper.ParseHexColor(hex);
            Assert.Equal(255, color.A);
            Assert.Equal(r, color.R);
            Assert.Equal(g, color.G);
            Assert.Equal(b, color.B);
        }

        [Fact]
        public void ParseHexColor_NullInput_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ColorHelper.ParseHexColor(null!));
        }

        [Fact]
        public void ParseHexColor_EmptyString_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ColorHelper.ParseHexColor(string.Empty));
        }

        [Fact]
        public void ParseHexColor_InvalidLength_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ColorHelper.ParseHexColor("#FFF"));
        }

        [Fact]
        public void ParseHexColor_InvalidHexCharacters_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ColorHelper.ParseHexColor("#GGHHII"));
        }

        [Fact]
        public void ParseHexColor_OddLength_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ColorHelper.ParseHexColor("#12345"));
        }

        [Fact]
        public void ParseHexColor_HashOnly_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ColorHelper.ParseHexColor("#"));
        }
    }

    // ═══ New ViewModel Property Tests ═══

    public class EditorViewModelNewPropertiesTests
    {
        [Fact]
        public void ViewModel_DefaultValues_NewProperties()
        {
            var vm = new EditorViewModel();
            Assert.Equal(0, vm.WordCount);
            Assert.Equal(0, vm.CharCount);
            Assert.Equal(1, vm.LineNumber);
            Assert.Equal(1, vm.ColumnNumber);
            Assert.Equal(0.0, vm.ParagraphSpacingBefore);
            Assert.Equal(0.0, vm.ParagraphSpacingAfter);
            Assert.False(vm.FindMatchCase);
            Assert.False(vm.FindWholeWord);
            Assert.NotNull(vm.RecentFiles);
            Assert.Empty(vm.RecentFiles);
        }

        [Fact]
        public void NewDocument_ResetsNewProperties()
        {
            var vm = new EditorViewModel();
            vm.WordCount = 100;
            vm.CharCount = 500;
            vm.LineNumber = 10;
            vm.ColumnNumber = 20;
            vm.ParagraphSpacingBefore = 12.0;
            vm.ParagraphSpacingAfter = 6.0;
            vm.FindMatchCase = true;
            vm.FindWholeWord = true;

            vm.NewDocument();

            Assert.Equal(0, vm.WordCount);
            Assert.Equal(0, vm.CharCount);
            Assert.Equal(1, vm.LineNumber);
            Assert.Equal(1, vm.ColumnNumber);
            Assert.Equal(0.0, vm.ParagraphSpacingBefore);
            Assert.Equal(0.0, vm.ParagraphSpacingAfter);
            Assert.False(vm.FindMatchCase);
            Assert.False(vm.FindWholeWord);
        }

        [Fact]
        public void UpdateWordCount_SetsWordCount()
        {
            var vm = new EditorViewModel();
            vm.UpdateWordCount(42);
            Assert.Equal(42, vm.WordCount);
        }

        [Fact]
        public void UpdateCharCount_SetsCharCount()
        {
            var vm = new EditorViewModel();
            vm.UpdateCharCount(256);
            Assert.Equal(256, vm.CharCount);
        }

        [Fact]
        public void UpdateCursorPosition_SetsLineAndColumn()
        {
            var vm = new EditorViewModel();
            vm.UpdateCursorPosition(new[] { 5, 10 });
            Assert.Equal(5, vm.LineNumber);
            Assert.Equal(10, vm.ColumnNumber);
        }

        [Fact]
        public void UpdateCursorPosition_IgnoresShortArray()
        {
            var vm = new EditorViewModel();
            vm.LineNumber = 3;
            vm.ColumnNumber = 7;
            vm.UpdateCursorPosition(new[] { 1 });
            Assert.Equal(3, vm.LineNumber);
            Assert.Equal(7, vm.ColumnNumber);
        }

        [Fact]
        public void SetParagraphSpacing_SetsValues()
        {
            var vm = new EditorViewModel();
            vm.SetParagraphSpacing(new[] { 12.0, 6.0 });
            Assert.Equal(12.0, vm.ParagraphSpacingBefore);
            Assert.Equal(6.0, vm.ParagraphSpacingAfter);
        }

        [Fact]
        public void SetParagraphSpacing_IgnoresShortArray()
        {
            var vm = new EditorViewModel();
            vm.ParagraphSpacingBefore = 5.0;
            vm.SetParagraphSpacing(new[] { 10.0 });
            Assert.Equal(5.0, vm.ParagraphSpacingBefore);
        }

        [Fact]
        public void PropertyChanged_FiredOnWordCountChange()
        {
            var vm = new EditorViewModel();
            string? changed = null;
            vm.PropertyChanged += (s, e) => changed = e.PropertyName;
            vm.WordCount = 10;
            Assert.Equal(nameof(EditorViewModel.WordCount), changed);
        }

        [Fact]
        public void PropertyChanged_FiredOnCharCountChange()
        {
            var vm = new EditorViewModel();
            string? changed = null;
            vm.PropertyChanged += (s, e) => changed = e.PropertyName;
            vm.CharCount = 50;
            Assert.Equal(nameof(EditorViewModel.CharCount), changed);
        }

        [Fact]
        public void PropertyChanged_FiredOnLineNumberChange()
        {
            var vm = new EditorViewModel();
            string? changed = null;
            vm.PropertyChanged += (s, e) => changed = e.PropertyName;
            vm.LineNumber = 7;
            Assert.Equal(nameof(EditorViewModel.LineNumber), changed);
        }

        [Fact]
        public void PropertyChanged_FiredOnColumnNumberChange()
        {
            var vm = new EditorViewModel();
            string? changed = null;
            vm.PropertyChanged += (s, e) => changed = e.PropertyName;
            vm.ColumnNumber = 15;
            Assert.Equal(nameof(EditorViewModel.ColumnNumber), changed);
        }

        [Fact]
        public void FindMatchCase_PropertyChange()
        {
            var vm = new EditorViewModel();
            var changed = new List<string>();
            vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName!);
            vm.FindMatchCase = true;
            Assert.Contains(nameof(EditorViewModel.FindMatchCase), changed);
            Assert.True(vm.FindMatchCase);
        }

        [Fact]
        public void FindWholeWord_PropertyChange()
        {
            var vm = new EditorViewModel();
            var changed = new List<string>();
            vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName!);
            vm.FindWholeWord = true;
            Assert.Contains(nameof(EditorViewModel.FindWholeWord), changed);
            Assert.True(vm.FindWholeWord);
        }

        [Fact]
        public void SetLineSpacing_CustomValue()
        {
            var vm = new EditorViewModel();
            vm.SetLineSpacing(2.5);
            Assert.Equal(2.5, vm.LineSpacing);
        }

        [Fact]
        public void RecentFiles_CanBeSet()
        {
            var vm = new EditorViewModel();
            var files = new List<string> { "file1.rtf", "file2.txt" };
            vm.RecentFiles = files;
            Assert.Equal(2, vm.RecentFiles.Count);
            Assert.Equal("file1.rtf", vm.RecentFiles[0]);
        }

        [Fact]
        public void SelectionLength_DefaultIsZero()
        {
            var vm = new EditorViewModel();
            Assert.Equal(0, vm.SelectionLength);
        }

        [Fact]
        public void SelectionLength_CanBeSet()
        {
            var vm = new EditorViewModel();
            vm.SelectionLength = 42;
            Assert.Equal(42, vm.SelectionLength);
        }

        [Fact]
        public void PropertyChanged_FiredOnSelectionLengthChange()
        {
            var vm = new EditorViewModel();
            string? changed = null;
            vm.PropertyChanged += (s, e) => changed = e.PropertyName;
            vm.SelectionLength = 10;
            Assert.Equal(nameof(EditorViewModel.SelectionLength), changed);
        }

        [Fact]
        public void Encoding_DefaultIsUtf8()
        {
            var vm = new EditorViewModel();
            Assert.Equal("UTF-8", vm.Encoding);
        }

        [Fact]
        public void Encoding_CanBeSet()
        {
            var vm = new EditorViewModel();
            vm.Encoding = "RTF";
            Assert.Equal("RTF", vm.Encoding);
        }

        [Fact]
        public void PropertyChanged_FiredOnEncodingChange()
        {
            var vm = new EditorViewModel();
            string? changed = null;
            vm.PropertyChanged += (s, e) => changed = e.PropertyName;
            vm.Encoding = "RTF";
            Assert.Equal(nameof(EditorViewModel.Encoding), changed);
        }

        [Fact]
        public void NewDocument_ResetsSelectionLengthAndEncoding()
        {
            var vm = new EditorViewModel();
            vm.SelectionLength = 50;
            vm.Encoding = "RTF";

            vm.NewDocument();

            Assert.Equal(0, vm.SelectionLength);
            Assert.Equal("UTF-8", vm.Encoding);
        }
    }

    // ═══ Settings Service Tests ═══

    public class SettingsServiceTests : IDisposable
    {
        private readonly string _testDir;
        private readonly string _testSettingsPath;

        public SettingsServiceTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), $"SmrtPadTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDir);
            _testSettingsPath = Path.Combine(_testDir, "settings.json");
        }

        public void Dispose()
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }

        [Fact]
        public void SettingsService_DefaultValues()
        {
            var svc = new SettingsService();
            Assert.Equal("Segoe UI", svc.DefaultFontFamily);
            Assert.Equal(11.0, svc.DefaultFontSize);
            Assert.True(svc.DefaultWordWrap);
            Assert.Equal(".rtf", svc.DefaultSaveFormat);
            Assert.Equal("System", svc.ThemePreference);
            Assert.False(svc.AutoSaveEnabled);
            Assert.Equal(300, svc.AutoSaveIntervalSeconds);
            Assert.Empty(svc.RecentFiles);
        }

        [Fact]
        public void SettingsService_AddRecentFile_AddsToFront()
        {
            var svc = new SettingsService();
            svc.ClearRecentFiles();
            svc.AddRecentFile("C:\\file1.rtf");
            svc.AddRecentFile("C:\\file2.rtf");
            Assert.Equal(2, svc.RecentFiles.Count);
            Assert.Equal("C:\\file2.rtf", svc.RecentFiles[0]);
            Assert.Equal("C:\\file1.rtf", svc.RecentFiles[1]);
        }

        [Fact]
        public void SettingsService_AddRecentFile_NoDuplicates()
        {
            var svc = new SettingsService();
            svc.ClearRecentFiles();
            svc.AddRecentFile("C:\\file1.rtf");
            svc.AddRecentFile("C:\\file2.rtf");
            svc.AddRecentFile("C:\\file1.rtf");
            Assert.Equal(2, svc.RecentFiles.Count);
            Assert.Equal("C:\\file1.rtf", svc.RecentFiles[0]);
        }

        [Fact]
        public void SettingsService_AddRecentFile_CapsAt10()
        {
            var svc = new SettingsService();
            svc.ClearRecentFiles();
            for (int i = 0; i < 15; i++)
                svc.AddRecentFile($"C:\\file{i}.rtf");
            Assert.Equal(10, svc.RecentFiles.Count);
            Assert.Equal("C:\\file14.rtf", svc.RecentFiles[0]);
        }

        [Fact]
        public void SettingsService_AddRecentFile_IgnoresNullOrWhitespace()
        {
            var svc = new SettingsService();
            svc.ClearRecentFiles();
            svc.AddRecentFile(null!);
            svc.AddRecentFile("");
            svc.AddRecentFile("  ");
            Assert.Empty(svc.RecentFiles);
        }

        [Fact]
        public void SettingsService_ClearRecentFiles()
        {
            var svc = new SettingsService();
            svc.AddRecentFile("C:\\file1.rtf");
            svc.AddRecentFile("C:\\file2.rtf");
            svc.ClearRecentFiles();
            Assert.Empty(svc.RecentFiles);
        }

        [Fact]
        public void SettingsService_SetAndGetProperties()
        {
            var svc = new SettingsService();
            svc.DefaultFontFamily = "Arial";
            svc.DefaultFontSize = 14.0;
            svc.DefaultWordWrap = false;
            svc.DefaultSaveFormat = ".txt";
            svc.ThemePreference = "Dark";
            svc.AutoSaveEnabled = true;
            svc.AutoSaveIntervalSeconds = 60;

            Assert.Equal("Arial", svc.DefaultFontFamily);
            Assert.Equal(14.0, svc.DefaultFontSize);
            Assert.False(svc.DefaultWordWrap);
            Assert.Equal(".txt", svc.DefaultSaveFormat);
            Assert.Equal("Dark", svc.ThemePreference);
            Assert.True(svc.AutoSaveEnabled);
            Assert.Equal(60, svc.AutoSaveIntervalSeconds);
        }
    }

    // ═══ Service Abstraction Tests ═══

    public class ServiceAbstractionTests
    {
        [Fact]
        public void SavePromptResult_HasExpectedValues()
        {
            Assert.Equal(0, (int)SavePromptResult.Save);
            Assert.Equal(1, (int)SavePromptResult.DontSave);
            Assert.Equal(2, (int)SavePromptResult.Cancel);
        }

        [Fact]
        public void IDialogService_InterfaceHasExpectedMethods()
        {
            var type = typeof(IDialogService);
            Assert.NotNull(type.GetMethod("ShowErrorAsync"));
            Assert.NotNull(type.GetMethod("ShowSavePromptAsync"));
        }

        [Fact]
        public void IFileService_InterfaceHasExpectedMethods()
        {
            var type = typeof(IFileService);
            Assert.NotNull(type.GetMethod("PickOpenFileAsync"));
            Assert.NotNull(type.GetMethod("PickSaveFileAsync"));
            Assert.NotNull(type.GetMethod("GetFileFromPathAsync"));
        }

        [Fact]
        public void ISettingsService_InterfaceHasExpectedMembers()
        {
            var type = typeof(ISettingsService);
            Assert.NotNull(type.GetProperty("DefaultFontFamily"));
            Assert.NotNull(type.GetProperty("DefaultFontSize"));
            Assert.NotNull(type.GetProperty("DefaultWordWrap"));
            Assert.NotNull(type.GetProperty("DefaultSaveFormat"));
            Assert.NotNull(type.GetProperty("ThemePreference"));
            Assert.NotNull(type.GetProperty("AutoSaveEnabled"));
            Assert.NotNull(type.GetProperty("AutoSaveIntervalSeconds"));
            Assert.NotNull(type.GetProperty("RecentFiles"));
            Assert.NotNull(type.GetMethod("AddRecentFile"));
            Assert.NotNull(type.GetMethod("ClearRecentFiles"));
            Assert.NotNull(type.GetMethod("Save"));
            Assert.NotNull(type.GetMethod("Load"));
        }

        [Fact]
        public void DialogService_ImplementsIDialogService()
        {
            Assert.True(typeof(IDialogService).IsAssignableFrom(typeof(DialogService)));
        }

        [Fact]
        public void FileService_ImplementsIFileService()
        {
            Assert.True(typeof(IFileService).IsAssignableFrom(typeof(FileService)));
        }

        [Fact]
        public void SettingsService_ImplementsISettingsService()
        {
            Assert.True(typeof(ISettingsService).IsAssignableFrom(typeof(SettingsService)));
        }
    }
}