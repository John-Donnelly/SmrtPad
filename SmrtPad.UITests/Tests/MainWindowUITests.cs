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

        [SkippableFact]
        public void AlignLeftToggle_IsPresent_InRibbon()
        {
            RequireDriver();
            var toggle = _driver!.FindElement(
                MobileBy.AccessibilityId("AlignLeftToggle"));
            Assert.NotNull(toggle);
        }

        [SkippableFact]
        public void AlignCenterToggle_IsPresent_InRibbon()
        {
            RequireDriver();
            var toggle = _driver!.FindElement(
                MobileBy.AccessibilityId("AlignCenterToggle"));
            Assert.NotNull(toggle);
        }

        [SkippableFact]
        public void AlignRightToggle_IsPresent_InRibbon()
        {
            RequireDriver();
            var toggle = _driver!.FindElement(
                MobileBy.AccessibilityId("AlignRightToggle"));
            Assert.NotNull(toggle);
        }

        [SkippableFact]
        public void AlignJustifyToggle_IsPresent_InRibbon()
        {
            RequireDriver();
            var toggle = _driver!.FindElement(
                MobileBy.AccessibilityId("AlignJustifyToggle"));
            Assert.NotNull(toggle);
        }

        [SkippableFact]
        public void WordCountText_IsPresent_InStatusBar()
        {
            RequireDriver();
            var text = _driver!.FindElement(
                MobileBy.AccessibilityId("WordCountText"));
            Assert.NotNull(text);
        }

        [SkippableFact]
        public void CharCountText_IsPresent_InStatusBar()
        {
            RequireDriver();
            var text = _driver!.FindElement(
                MobileBy.AccessibilityId("CharCountText"));
            Assert.NotNull(text);
        }

        [SkippableFact]
        public void LineColText_IsPresent_InStatusBar()
        {
            RequireDriver();
            var text = _driver!.FindElement(
                MobileBy.AccessibilityId("LineColText"));
            Assert.NotNull(text);
        }

        [SkippableFact]
        public void EncodingText_IsPresent_InStatusBar()
        {
            RequireDriver();
            var text = _driver!.FindElement(
                MobileBy.AccessibilityId("EncodingText"));
            Assert.NotNull(text);
        }

        [SkippableFact]
        public void ThemeToggleButton_IsPresent_InTitleBar()
        {
            RequireDriver();
            var btn = _driver!.FindElement(
                MobileBy.AccessibilityId("ThemeToggleButton"));
            Assert.NotNull(btn);
        }

        [SkippableFact]
        public void FindTextBox_IsPresent_InRibbon()
        {
            RequireDriver();
            var box = _driver!.FindElement(
                MobileBy.AccessibilityId("FindTextBox"));
            Assert.NotNull(box);
        }

        [SkippableFact]
        public void FindMatchCaseCheckBox_IsPresent_InRibbon()
        {
            RequireDriver();
            var chk = _driver!.FindElement(
                MobileBy.AccessibilityId("FindMatchCaseCheckBox"));
            Assert.NotNull(chk);
        }

        [SkippableFact]
        public void FindWholeWordCheckBox_IsPresent_InRibbon()
        {
            RequireDriver();
            var chk = _driver!.FindElement(
                MobileBy.AccessibilityId("FindWholeWordCheckBox"));
            Assert.NotNull(chk);
        }

        [SkippableFact]
        public void FindRegexCheckBox_IsPresent_InRibbon()
        {
            RequireDriver();
            var chk = _driver!.FindElement(
                MobileBy.AccessibilityId("FindRegexCheckBox"));
            Assert.NotNull(chk);
        }

        [SkippableFact]
        public void HighlightAllButton_IsPresent_InRibbon()
        {
            RequireDriver();
            var btn = _driver!.FindElement(
                MobileBy.AccessibilityId("HighlightAllButton"));
            Assert.NotNull(btn);
        }

        [SkippableFact]
        public void ClearHighlightsButton_IsPresent_InRibbon()
        {
            RequireDriver();
            var btn = _driver!.FindElement(
                MobileBy.AccessibilityId("ClearHighlightsButton"));
            Assert.NotNull(btn);
        }

        [SkippableFact]
        public void FindNextButton_IsPresent_InRibbon()
        {
            RequireDriver();
            var btn = _driver!.FindElement(
                MobileBy.AccessibilityId("FindNextButton"));
            Assert.NotNull(btn);
        }

        [SkippableFact]
        public void ReplaceFindTextBox_IsPresent_InRibbon()
        {
            RequireDriver();
            var box = _driver!.FindElement(
                MobileBy.AccessibilityId("ReplaceFindTextBox"));
            Assert.NotNull(box);
        }

        [SkippableFact]
        public void ReplaceWithTextBox_IsPresent_InRibbon()
        {
            RequireDriver();
            var box = _driver!.FindElement(
                MobileBy.AccessibilityId("ReplaceWithTextBox"));
            Assert.NotNull(box);
        }

        [SkippableFact]
        public void ReplaceButton_IsPresent_InRibbon()
        {
            RequireDriver();
            var btn = _driver!.FindElement(
                MobileBy.AccessibilityId("ReplaceButton"));
            Assert.NotNull(btn);
        }

        [SkippableFact]
        public void ReplaceAllButton_IsPresent_InRibbon()
        {
            RequireDriver();
            var btn = _driver!.FindElement(
                MobileBy.AccessibilityId("ReplaceAllButton"));
            Assert.NotNull(btn);
        }

        [SkippableFact]
        public void ZoomText_IsPresent_InStatusBar()
        {
            RequireDriver();
            var text = _driver!.FindElement(
                MobileBy.AccessibilityId("ZoomText"));
            Assert.NotNull(text);
        }

        [SkippableFact]
        public void FileBackstage_IsPresent()
        {
            RequireDriver();
            var backstage = _driver!.FindElement(
                MobileBy.AccessibilityId("FileBackstage"));
            Assert.NotNull(backstage);
        }
    }
}
