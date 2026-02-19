using System;
using Xunit;
using SmrtPad.ViewModels;

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
    }
}