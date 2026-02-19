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

            // Act
            viewModel.NewDocument();

            // Assert
            Assert.Equal("Untitled", viewModel.DocumentTitle);
            Assert.Equal("New document created.", viewModel.StatusMessage);
            Assert.False(viewModel.IsModified);
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
    }
}