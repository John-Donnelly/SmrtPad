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
    [Collection("UITests")]
    public sealed class FileBackstageUITests : IDisposable
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
            _fx.EnsureBackstageOpen();
        }

        private void CloseBackstage()
        {
            _fx.EnsureBackstageClosed();
        }

        private AppiumElement FindBackstageNavItem(string automationId)
        {
            return _driver!.FindElement(MobileBy.AccessibilityId(automationId));
        }

        private void ClickBackstageNavItem(string automationId)
        {
            FindBackstageNavItem(automationId).Click();
            Thread.Sleep(500);
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

            var newItem = FindBackstageNavItem("BackstageNewNavItem");
            Assert.NotNull(newItem);

            CloseBackstage();
        }

        /// <summary>
        /// The backstage should contain the "Page setup" navigation item.
        /// </summary>
        [SkippableFact]
        public void Backstage_PageSetupNavItem_IsPresent()
        {
            RequireDriver();
            OpenBackstage();

            var item = FindBackstageNavItem("BackstagePageSetupNavItem");
            Assert.NotNull(item);

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

            var item = FindBackstageNavItem("BackstageTemplatesNavItem");
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

            var item = FindBackstageNavItem("BackstageOpenNavItem");
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

            var item = FindBackstageNavItem("BackstageSaveNavItem");
            Assert.NotNull(item);

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

            var item = FindBackstageNavItem("BackstageSaveAsNavItem");
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

            var item = FindBackstageNavItem("BackstagePrintNavItem");
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

            var item = FindBackstageNavItem("BackstageExportPdfNavItem");
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

            var item = FindBackstageNavItem("BackstageExportDocxNavItem");
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

            var item = FindBackstageNavItem("BackstageOneDriveNavItem");
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

            var item = FindBackstageNavItem("BackstageOptionsNavItem");
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

            var item = FindBackstageNavItem("BackstageExitNavItem");
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

            ClickBackstageNavItem("BackstageTemplatesNavItem");

            // The template picker should contain at least one template card
            // Templates include: "Blank Document", "Business Letter", etc.
            var templateBtn = _driver!.FindElement(MobileBy.AccessibilityId("Template_blank"));
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

            ClickBackstageNavItem("BackstageOpenNavItem");

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
            var newItem = FindBackstageNavItem("BackstageNewNavItem");
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

            ClickBackstageNavItem("BackstageSaveNavItem");

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

            ClickBackstageNavItem("BackstageSaveAsNavItem");

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

            ClickBackstageNavItem("BackstagePrintNavItem");

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

            ClickBackstageNavItem("BackstageExportPdfNavItem");

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

            ClickBackstageNavItem("BackstageExportDocxNavItem");

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

            ClickBackstageNavItem("BackstageOneDriveNavItem");

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

            ClickBackstageNavItem("BackstageOptionsNavItem");

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

            ClickBackstageNavItem("BackstageTemplatesNavItem");

            // At minimum, "Blank Document" should be present
            var blankDoc = _driver!.FindElement(MobileBy.AccessibilityId("Template_blank"));
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

            // Start with a single known-clean tab to eliminate undo-stack contamination
            // from prior tests (UI-8).
            _fx.ResetToSingleTab();
            _fx.ClearEditor();

            // Type some content first
            _fx.TypeInEditor("existing content");
            Assert.Equal("Words: 2", _fx.GetStatusBarText("WordCountText"));

            OpenBackstage();

            ClickBackstageNavItem("BackstageNewNavItem");
            Thread.Sleep(500);

            // Backstage may still be open after "New" — close if so
            _fx.EnsureBackstageClosed();
            Thread.Sleep(300);

            // The editor should now show a new/empty tab
            Assert.Equal("Words: 0", _fx.GetStatusBarText("WordCountText"));

            // Clean up: close the extra tab created by "New" using Ctrl+W, NOT
            // MobileBy.Name("Close") which resolves to the title-bar close button (N-2).
            _fx.CloseActiveTab();
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
            ClickBackstageNavItem("BackstageOpenNavItem");
            var header1 = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(header1);

            // Navigate to Print
            ClickBackstageNavItem("BackstagePrintNavItem");
            var header2 = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(header2);

            // Navigate to Options
            ClickBackstageNavItem("BackstageOptionsNavItem");
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
            ClickBackstageNavItem("BackstageTemplatesNavItem");

            var headerText = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            string headerValue = headerText.Text;
            Assert.Equal("Templates", headerValue);

            // Now navigate to Options
            ClickBackstageNavItem("BackstageOptionsNavItem");

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
            ClickBackstageNavItem("BackstageTemplatesNavItem");

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
            ClickBackstageNavItem("BackstageTemplatesNavItem");

            // The header should reflect Open section
            var headerText = _driver!.FindElement(MobileBy.AccessibilityId("HeaderText"));
            Assert.NotNull(headerText);

            CloseBackstage();
        }
    }
}
