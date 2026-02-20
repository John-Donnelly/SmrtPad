using System;
using Xunit;
using SmrtPad.ViewModels;
using SmrtPad.Helpers;

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
    }
}