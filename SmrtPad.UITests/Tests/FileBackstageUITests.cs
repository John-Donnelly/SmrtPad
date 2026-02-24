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
    }
}
