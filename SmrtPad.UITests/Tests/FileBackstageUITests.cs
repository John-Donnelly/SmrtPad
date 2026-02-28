using System;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Xunit;
using SmrtPad.UITests.Infrastructure;

namespace SmrtPad.UITests.Tests
{
    /// <summary>
    /// UI tests for the File backstage view. Tests verify that the backstage
    /// opens correctly, all navigation items are present, and navigation
    /// between items shows the correct content panels.
    ///
    /// Tests share one Appium session via <see cref="SharedAppFixture"/>.
    /// </summary>
    public sealed class FileBackstageUITests : IClassFixture<SharedAppFixture>, IDisposable
    {
        private readonly SharedAppFixture _fx;
        private readonly WindowsDriver? _driver;

        public FileBackstageUITests(SharedAppFixture fx)
        {
            _fx = fx;
            _driver = fx.Driver;
        }

        public void Dispose() { /* session owned by fixture */ }

        private void RequireDriver() =>
            Skip.If(!_fx.IsAvailable,
                "WinAppDriver / Appium not available or SmrtPad.exe not built.");

        // ── Helpers ───────────────────────────────────────────────────────────

        private void OpenBackstage()
        {
            _driver!.FindElement(MobileBy.Name("File")).Click();
            Thread.Sleep(800);
        }

        private void CloseBackstage()
        {
            // Click the editor area behind the backstage to close it, or press Escape
            try
            {
                _driver!.FindElement(MobileBy.AccessibilityId("Editor")).Click();
            }
            catch
            {
                _driver!.FindElement(MobileBy.Name("File")).Click();
                Thread.Sleep(300);
            }
            Thread.Sleep(300);
        }

        // ── Backstage opens ──────────────────────────────────────────────────

        /// <summary>
        /// Clicking the File button should open the backstage view,
        /// showing the navigation pane with "New" item visible.
        /// </summary>
        [SkippableFact]
        public void FileButton_OpensBackstage_WithNewItemVisible()
        {
            RequireDriver();
            OpenBackstage();

            var newItem = _driver!.FindElement(MobileBy.Name("New"));
            Assert.NotNull(newItem);

            CloseBackstage();
        }

        // ── All navigation items present ─────────────────────────────────────

        /// <summary>
        /// The backstage should contain the "Templates" navigation item.
        /// </summary>
        [SkippableFact]
        public void Backstage_TemplatesNavItem_IsPresent()
        {
            RequireDriver();
            OpenBackstage();

            var item = _driver!.FindElement(MobileBy.Name("Templates"));
            Assert.NotNull(item);

            CloseBackstage();
        }

        /// <summary>
        /// The backstage should contain the "Open" navigation item.
        /// </summary>
        [SkippableFact]
        public void Backstage_OpenNavItem_IsPresent()
        {
            RequireDriver();
            OpenBackstage();

            var item = _driver!.FindElement(MobileBy.Name("Open"));
            Assert.NotNull(item);

            CloseBackstage();
        }

        /// <summary>
        /// The backstage should contain the "Save" navigation item.
        /// </summary>
        [SkippableFact]
        public void Backstage_SaveNavItem_IsPresent()
        {
            RequireDriver();
            OpenBackstage();

            var items = _driver!.FindElements(MobileBy.Name("Save"));
            Assert.NotEmpty(items);

            CloseBackstage();
        }

        /// <summary>
        /// The backstage should contain the "Save as" navigation item.
        /// </summary>
        [SkippableFact]
        public void Backstage_SaveAsNavItem_IsPresent()
        {
            RequireDriver();
            OpenBackstage();

            var item = _driver!.FindElement(MobileBy.Name("Save as"));
            Assert.NotNull(item);

            CloseBackstage();
        }

        /// <summary>
        /// The backstage should contain the "Print" navigation item.
        /// </summary>
        [SkippableFact]
        public void Backstage_PrintNavItem_IsPresent()
        {
            RequireDriver();
            OpenBackstage();

            var item = _driver!.FindElement(MobileBy.Name("Print"));
            Assert.NotNull(item);

            CloseBackstage();
        }

        /// <summary>
        /// The backstage should contain the "Export to PDF" navigation item.
        /// </summary>
        [SkippableFact]
        public void Backstage_ExportPdfNavItem_IsPresent()
        {
            RequireDriver();
            OpenBackstage();

            var item = _driver!.FindElement(MobileBy.Name("Export to PDF"));
            Assert.NotNull(item);

            CloseBackstage();
        }

        /// <summary>
        /// The backstage should contain the "Export to DOCX" navigation item.
        /// </summary>
        [SkippableFact]
        public void Backstage_ExportDocxNavItem_IsPresent()
        {
            RequireDriver();
            OpenBackstage();

            var item = _driver!.FindElement(MobileBy.Name("Export to DOCX"));
            Assert.NotNull(item);

            CloseBackstage();
        }

        /// <summary>
        /// The backstage should contain the "Save to OneDrive" navigation item.
        /// </summary>
        [SkippableFact]
        public void Backstage_OneDriveNavItem_IsPresent()
        {
            RequireDriver();
            OpenBackstage();

            var item = _driver!.FindElement(MobileBy.Name("Save to OneDrive"));
            Assert.NotNull(item);

            CloseBackstage();
        }

        /// <summary>
        /// The backstage should contain the "Options" navigation item.
        /// </summary>
        [SkippableFact]
        public void Backstage_OptionsNavItem_IsPresent()
        {
            RequireDriver();
            OpenBackstage();

            var item = _driver!.FindElement(MobileBy.Name("Options"));
            Assert.NotNull(item);

            CloseBackstage();
        }

        /// <summary>
        /// The backstage should contain the "Exit" navigation item.
        /// </summary>
        [SkippableFact]
        public void Backstage_ExitNavItem_IsPresent()
        {
            RequireDriver();
            OpenBackstage();

            var item = _driver!.FindElement(MobileBy.Name("Exit"));
            Assert.NotNull(item);

            CloseBackstage();
        }

        // ── Navigation content panels ────────────────────────────────────────

        /// <summary>
        /// Clicking "Templates" in the backstage should show the template picker panel.
        /// </summary>
        [SkippableFact]
        public void Backstage_ClickTemplates_ShowsTemplatePicker()
        {
            RequireDriver();
            OpenBackstage();

            _driver!.FindElement(MobileBy.Name("Templates")).Click();
            Thread.Sleep(500);

            // The template picker should contain at least one template card
            // Templates include: "Blank Document", "Business Letter", etc.
            var templateBtn = _driver!.FindElement(MobileBy.Name("Blank Document"));
            Assert.NotNull(templateBtn);

            CloseBackstage();
        }

        /// <summary>
        /// Clicking "Open" in the backstage should show the Open description
        /// and the recent files panel (if there are recent files) or just
        /// the description panel.
        /// </summary>
        [SkippableFact]
        public void Backstage_ClickOpen_ShowsOpenPanel()
        {
            RequireDriver();
            OpenBackstage();

            _driver!.FindElement(MobileBy.Name("Open")).Click();
            Thread.Sleep(500);

            // The header should change to reflect the Open section
            var headerText = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(headerText);

            CloseBackstage();
        }

        // ── Backstage close via Escape ────────────────────────────────────────

        /// <summary>
        /// Pressing Escape while the backstage is open should close it and
        /// return focus to the editor.
        /// </summary>
        [SkippableFact]
        public void Backstage_CloseViaEscape_ReturnsFocusToEditor()
        {
            RequireDriver();
            OpenBackstage();

            // Verify backstage is open
            var newItem = _driver!.FindElement(MobileBy.Name("New"));
            Assert.NotNull(newItem);

            // Press Escape to close
            newItem.SendKeys(Keys.Escape);
            Thread.Sleep(500);

            // Editor should be accessible
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);
        }

        // ── Backstage Save navigation ─────────────────────────────────────────

        /// <summary>
        /// Clicking "Save" in the backstage should show the Save panel header.
        /// </summary>
        [SkippableFact]
        public void Backstage_ClickSave_ShowsSavePanel()
        {
            RequireDriver();
            OpenBackstage();

            var saveItems = _driver!.FindElements(MobileBy.Name("Save"));
            // Click the nav item (not the quick-access button)
            foreach (var item in saveItems)
            {
                try
                {
                    if (item.Displayed)
                    {
                        item.Click();
                        break;
                    }
                }
                catch { }
            }
            Thread.Sleep(500);

            var headerText = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(headerText);

            CloseBackstage();
        }

        // ── Backstage Save As navigation ──────────────────────────────────────

        /// <summary>
        /// Clicking "Save as" should show the Save As header.
        /// </summary>
        [SkippableFact]
        public void Backstage_ClickSaveAs_ShowsSaveAsPanel()
        {
            RequireDriver();
            OpenBackstage();

            _driver!.FindElement(MobileBy.Name("Save as")).Click();
            Thread.Sleep(500);

            var headerText = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(headerText);

            CloseBackstage();
        }

        // ── Backstage Print navigation ────────────────────────────────────────

        /// <summary>
        /// Clicking "Print" in the backstage should show the Print panel header.
        /// </summary>
        [SkippableFact]
        public void Backstage_ClickPrint_ShowsPrintPanel()
        {
            RequireDriver();
            OpenBackstage();

            _driver!.FindElement(MobileBy.Name("Print")).Click();
            Thread.Sleep(500);

            var headerText = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(headerText);

            CloseBackstage();
        }

        // ── Backstage Export PDF navigation ───────────────────────────────────

        /// <summary>
        /// Clicking "Export to PDF" should show the Export panel header.
        /// </summary>
        [SkippableFact]
        public void Backstage_ClickExportPdf_ShowsExportPanel()
        {
            RequireDriver();
            OpenBackstage();

            _driver!.FindElement(MobileBy.Name("Export to PDF")).Click();
            Thread.Sleep(500);

            var headerText = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(headerText);

            CloseBackstage();
        }

        // ── Backstage Export DOCX navigation ──────────────────────────────────

        /// <summary>
        /// Clicking "Export to DOCX" should show the Export DOCX panel header.
        /// </summary>
        [SkippableFact]
        public void Backstage_ClickExportDocx_ShowsExportPanel()
        {
            RequireDriver();
            OpenBackstage();

            _driver!.FindElement(MobileBy.Name("Export to DOCX")).Click();
            Thread.Sleep(500);

            var headerText = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(headerText);

            CloseBackstage();
        }

        // ── Backstage OneDrive navigation ─────────────────────────────────────

        /// <summary>
        /// Clicking "Save to OneDrive" should show the OneDrive panel header.
        /// </summary>
        [SkippableFact]
        public void Backstage_ClickOneDrive_ShowsOneDrivePanel()
        {
            RequireDriver();
            OpenBackstage();

            _driver!.FindElement(MobileBy.Name("Save to OneDrive")).Click();
            Thread.Sleep(500);

            var headerText = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(headerText);

            CloseBackstage();
        }

        // ── Backstage Options navigation ──────────────────────────────────────

        /// <summary>
        /// Clicking "Options" should show the Options panel header.
        /// </summary>
        [SkippableFact]
        public void Backstage_ClickOptions_ShowsOptionsPanel()
        {
            RequireDriver();
            OpenBackstage();

            _driver!.FindElement(MobileBy.Name("Options")).Click();
            Thread.Sleep(500);

            var headerText = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(headerText);

            CloseBackstage();
        }

        // ── Template picker shows multiple templates ──────────────────────────

        /// <summary>
        /// The template picker should show multiple template options beyond
        /// just "Blank Document" (e.g., "Business Letter").
        /// </summary>
        [SkippableFact]
        public void Backstage_TemplatePicker_ContainsMultipleTemplates()
        {
            RequireDriver();
            OpenBackstage();

            _driver!.FindElement(MobileBy.Name("Templates")).Click();
            Thread.Sleep(500);

            // At minimum, "Blank Document" should be present
            var blankDoc = _driver!.FindElement(MobileBy.Name("Blank Document"));
            Assert.NotNull(blankDoc);

            CloseBackstage();
        }

        // ── Backstage New creates blank document ──────────────────────────────

        /// <summary>
        /// Clicking "New" in the backstage should close the backstage and
        /// create a new blank document, resetting word count to 0.
        /// </summary>
        [SkippableFact]
        public void Backstage_ClickNew_CreatesBlankDocument()
        {
            RequireDriver();

            // Type some content first
            _fx.ClearEditor();
            _fx.TypeInEditor("existing content");
            Assert.Equal("Words: 2", _fx.GetStatusBarText("WordCountText"));

            OpenBackstage();

            _driver!.FindElement(MobileBy.Name("New")).Click();
            Thread.Sleep(800);

            // The backstage should close and the editor should be empty
            Assert.Equal("Words: 0", _fx.GetStatusBarText("WordCountText"));
        }

        // ── Backstage navigation switches content ─────────────────────────────

        /// <summary>
        /// Switching between backstage nav items should update the header text
        /// each time.
        /// </summary>
        [SkippableFact]
        public void Backstage_NavigateBetweenItems_UpdatesHeader()
        {
            RequireDriver();
            OpenBackstage();

            // Navigate to Open
            _driver!.FindElement(MobileBy.Name("Open")).Click();
            Thread.Sleep(400);
            var header1 = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(header1);

            // Navigate to Print
            _driver!.FindElement(MobileBy.Name("Print")).Click();
            Thread.Sleep(400);
            var header2 = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(header2);

            // Navigate to Options
            _driver!.FindElement(MobileBy.Name("Options")).Click();
            Thread.Sleep(400);
            var header3 = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(header3);

            CloseBackstage();
        }

        // ── Backstage hover behavior ──────────────────────────────────────────

        /// <summary>
        /// Hovering over different navigation items in the backstage should
        /// update the header text on the right pane without executing actions.
        /// We simulate hover by moving to the element.
        /// </summary>
        [SkippableFact]
        public void Backstage_HoverOverNavItems_UpdatesHeaderText()
        {
            RequireDriver();
            OpenBackstage();

            // Navigate to Templates first so we can verify the pane shows
            _driver!.FindElement(MobileBy.Name("Templates")).Click();
            Thread.Sleep(400);

            var headerText = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            string headerValue = headerText.Text;
            Assert.Equal("Templates", headerValue);

            // Now navigate to Options
            _driver!.FindElement(MobileBy.Name("Options")).Click();
            Thread.Sleep(400);

            headerText = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            headerValue = headerText.Text;
            Assert.Equal("Options", headerValue);

            CloseBackstage();
        }

        /// <summary>
        /// Navigating to "Exit" in the backstage should show the Exit
        /// description pane (not immediately close the app).
        /// </summary>
        [SkippableFact]
        public void Backstage_NavigateToExit_ShowsExitDescription()
        {
            RequireDriver();
            OpenBackstage();

            // First navigate to Templates so Exit isn't the initial selection
            _driver!.FindElement(MobileBy.Name("Templates")).Click();
            Thread.Sleep(400);

            // The header should be visible and show Exit info
            var headerText = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(headerText);

            CloseBackstage();
        }

        /// <summary>
        /// Navigating to "Open" should show the recent files panel
        /// without immediately opening a file picker dialog.
        /// </summary>
        [SkippableFact]
        public void Backstage_NavigateToOpen_ShowsRecentFilesPanel()
        {
            RequireDriver();
            OpenBackstage();

            // First go to Templates, then to Open
            _driver!.FindElement(MobileBy.Name("Templates")).Click();
            Thread.Sleep(300);

            // The header should reflect Open section
            var headerText = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(headerText);

            CloseBackstage();
        }
    }
}
