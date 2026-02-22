using System;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Xunit;
using SmrtPad.UITests.Infrastructure;

namespace SmrtPad.UITests.Tests
{
    /// <summary>
    /// End-to-end UI automation tests for the SmrtPad main window.
    ///
    /// These tests require:
    ///   • Appium 2.x server running at http://127.0.0.1:4723
    ///   • appium-windows-driver installed   (appium driver install windows)
    ///   • WinAppDriver 1.2.1 installed and running
    ///   • SmrtPad.exe built (x64 Debug or Release)
    ///
    /// All tests are <c>[SkippableFact]</c> and skip gracefully when any of the
    /// above prerequisites are absent so that the standard unit-test CI run is
    /// not broken.
    /// </summary>
    public class MainWindowUITests : IDisposable
    {
        private readonly AppiumSession? _session;
        private readonly WindowsDriver?  _driver;

        public MainWindowUITests()
        {
            if (!AppiumSession.IsAvailable()) return;
            string? exe = AppiumSession.FindSmrtPadExe();
            if (exe is null) return;

            try { _session = new AppiumSession(exe); _driver = _session.Driver; }
            catch { _session = null; _driver = null; }
        }

        public void Dispose() => _session?.Dispose();

        // ── helpers ──────────────────────────────────────────────────────────

        private void RequireDriver() =>
            Skip.If(_driver is null,
                "WinAppDriver / Appium not available or SmrtPad.exe not built.");

        // ── tests ─────────────────────────────────────────────────────────────

        [SkippableFact]
        public void App_Launches_AndWindowTitleContainsSmrtPad()
        {
            RequireDriver();
            var title = _driver!.Title;
            Assert.Contains("SmrtPad", title, StringComparison.OrdinalIgnoreCase);
        }

        [SkippableFact]
        public void Editor_IsPresent_AfterLaunch()
        {
            RequireDriver();
            // The RichEditBox is identified by AutomationId "Editor" in the XAML
            var editor = _driver!.FindElement(
                MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);
        }

        [SkippableFact]
        public void StatusBar_IsVisible_AfterLaunch()
        {
            RequireDriver();
            var bar = _driver!.FindElement(
                MobileBy.AccessibilityId("StatusBar"));
            Assert.NotNull(bar);
        }

        [SkippableFact]
        public void BoldToggle_IsPresent_InRibbon()
        {
            RequireDriver();
            var bold = _driver!.FindElement(
                MobileBy.AccessibilityId("BoldToggle"));
            Assert.NotNull(bold);
        }

        [SkippableFact]
        public void ItalicToggle_IsPresent_InRibbon()
        {
            RequireDriver();
            var italic = _driver!.FindElement(
                MobileBy.AccessibilityId("ItalicToggle"));
            Assert.NotNull(italic);
        }

        [SkippableFact]
        public void UnderlineToggle_IsPresent_InRibbon()
        {
            RequireDriver();
            var uline = _driver!.FindElement(
                MobileBy.AccessibilityId("UnderlineToggle"));
            Assert.NotNull(uline);
        }

        [SkippableFact]
        public void FontFamilyComboBox_IsPresent_InRibbon()
        {
            RequireDriver();
            var combo = _driver!.FindElement(
                MobileBy.AccessibilityId("FontFamilyComboBox"));
            Assert.NotNull(combo);
        }

        [SkippableFact]
        public void FontSizeComboBox_IsPresent_InRibbon()
        {
            RequireDriver();
            var combo = _driver!.FindElement(
                MobileBy.AccessibilityId("FontSizeComboBox"));
            Assert.NotNull(combo);
        }

        [SkippableFact]
        public void DocumentTabs_IsPresent()
        {
            RequireDriver();
            var tabs = _driver!.FindElement(
                MobileBy.AccessibilityId("DocumentTabs"));
            Assert.NotNull(tabs);
        }

        [SkippableFact]
        public void SpellCheckToggle_IsPresent_InViewMenu()
        {
            RequireDriver();
            var toggle = _driver!.FindElement(
                MobileBy.AccessibilityId("SpellCheckToggle"));
            Assert.NotNull(toggle);
        }
    }
}
