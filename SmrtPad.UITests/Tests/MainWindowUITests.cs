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
    [Collection("UITests")]
    public class MainWindowUITests : IDisposable
    {
        private readonly SharedAppFixture _fx;
        private WindowsDriver?   _driver;

        public MainWindowUITests(SharedAppFixture fx)
        {
            _fx     = fx;
            _driver = fx.Driver;
        }

        public void Dispose() { /* session owned by UITests collection fixture */ }

        // ── helpers ──────────────────────────────────────────────────────────

        private void RequireDriver()
        {
            _fx.RequireSession();
            _driver = _fx.Driver;
        }
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
                MobileBy.AccessibilityId("StatusText"));
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

            // Open the View menu first
            var viewMenu = _driver!.FindElement(MobileBy.Name("View"));
            viewMenu.Click();
            System.Threading.Thread.Sleep(500);

            var toggle = _driver!.FindElement(
                MobileBy.AccessibilityId("SpellCheckToggle"));
            Assert.NotNull(toggle);

            // Close the menu
            viewMenu.Click();
        }

        // ── Font formatting toggles ──────────────────────────────────────────

        [SkippableFact]
        public void StrikethroughToggle_IsPresent_InRibbon()
        {
            RequireDriver();
            var toggle = _driver!.FindElement(
                MobileBy.AccessibilityId("StrikethroughToggle"));
            Assert.NotNull(toggle);
        }

        [SkippableFact]
        public void SubscriptToggle_IsPresent_InRibbon()
        {
            RequireDriver();
            var toggle = _driver!.FindElement(
                MobileBy.AccessibilityId("SubscriptToggle"));
            Assert.NotNull(toggle);
        }

        [SkippableFact]
        public void SuperscriptToggle_IsPresent_InRibbon()
        {
            RequireDriver();
            var toggle = _driver!.FindElement(
                MobileBy.AccessibilityId("SuperscriptToggle"));
            Assert.NotNull(toggle);
        }

        // ── Paragraph alignment toggles ──────────────────────────────────────

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

        // ── View menu items ─────────────────────────────────────────────────

        [SkippableFact]
        public void RulerToggle_IsPresent_InViewMenu()
        {
            RequireDriver();

            var viewMenu = _driver!.FindElement(MobileBy.Name("View"));
            viewMenu.Click();
            System.Threading.Thread.Sleep(500);

            var toggle = _driver!.FindElement(
                MobileBy.AccessibilityId("RulerToggle"));
            Assert.NotNull(toggle);

            viewMenu.Click();
        }

        [SkippableFact]
        public void PageViewToggle_IsPresent_InViewMenu()
        {
            RequireDriver();

            var viewMenu = _driver!.FindElement(MobileBy.Name("View"));
            viewMenu.Click();
            System.Threading.Thread.Sleep(500);

            var toggle = _driver!.FindElement(
                MobileBy.AccessibilityId("PageViewToggle"));
            Assert.NotNull(toggle);

            viewMenu.Click();
        }

        [SkippableFact]
        public void FocusModeToggle_IsPresent_InViewMenu()
        {
            RequireDriver();

            var viewMenu = _driver!.FindElement(MobileBy.Name("View"));
            viewMenu.Click();
            System.Threading.Thread.Sleep(500);

            var toggle = _driver!.FindElement(
                MobileBy.AccessibilityId("FocusModeToggle"));
            Assert.NotNull(toggle);

            viewMenu.Click();
        }

        // ── Status bar detail elements ───────────────────────────────────────

        [SkippableFact]
        public void EncodingText_IsPresent_InStatusBar()
        {
            RequireDriver();
            var text = _driver!.FindElement(
                MobileBy.AccessibilityId("EncodingText"));
            Assert.NotNull(text);
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
        public void ZoomText_IsPresent_InStatusBar()
        {
            RequireDriver();
            var text = _driver!.FindElement(
                MobileBy.AccessibilityId("ZoomText"));
            Assert.NotNull(text);
        }

        // ── Quick-access toolbar ─────────────────────────────────────────────

        [SkippableFact]
        public void ThemeToggleButton_IsPresent_InToolbar()
        {
            RequireDriver();
            var btn = _driver!.FindElement(
                MobileBy.AccessibilityId("ThemeToggleButton"));
            Assert.NotNull(btn);
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
        public void SelectionLengthText_IsPresent_InStatusBar()
        {
            RequireDriver();
            var text = _driver!.FindElement(
                MobileBy.AccessibilityId("SelectionLengthText"));
            Assert.NotNull(text);
        }

        // ── Window menu ──────────────────────────────────────────────────────

        [SkippableFact]
        public void WindowMenu_NewWindowItem_IsPresent()
        {
            RequireDriver();

            var windowMenu = _driver!.FindElement(MobileBy.Name("Window"));
            windowMenu.Click();
            System.Threading.Thread.Sleep(500);

            var item = _driver!.FindElement(MobileBy.Name("New Window"));
            Assert.NotNull(item);

            windowMenu.Click();
        }

        // ── Edit menu items ─────────────────────────────────────────────────

        [SkippableFact]
        public void EditMenu_CutItem_IsPresent()
        {
            RequireDriver();

            var editMenu = _driver!.FindElement(MobileBy.Name("Edit"));
            editMenu.Click();
            System.Threading.Thread.Sleep(500);

            var item = _driver!.FindElement(MobileBy.Name("Cut"));
            Assert.NotNull(item);

            editMenu.Click();
        }

        [SkippableFact]
        public void EditMenu_CopyItem_IsPresent()
        {
            RequireDriver();

            var editMenu = _driver!.FindElement(MobileBy.Name("Edit"));
            editMenu.Click();
            System.Threading.Thread.Sleep(500);

            var item = _driver!.FindElement(MobileBy.Name("Copy"));
            Assert.NotNull(item);

            editMenu.Click();
        }

        [SkippableFact]
        public void EditMenu_PasteItem_IsPresent()
        {
            RequireDriver();

            var editMenu = _driver!.FindElement(MobileBy.Name("Edit"));
            editMenu.Click();
            System.Threading.Thread.Sleep(500);

            var item = _driver!.FindElement(MobileBy.Name("Paste"));
            Assert.NotNull(item);

            editMenu.Click();
        }

        [SkippableFact]
        public void EditMenu_SelectAllItem_IsPresent()
        {
            RequireDriver();

            var editMenu = _driver!.FindElement(MobileBy.Name("Edit"));
            editMenu.Click();
            System.Threading.Thread.Sleep(500);

            var item = _driver!.FindElement(MobileBy.Name("Select All"));
            Assert.NotNull(item);

            editMenu.Click();
        }

        // ── File backstage ───────────────────────────────────────────────────

        [SkippableFact]
        public void FileMenu_OpensBackstage()
        {
            RequireDriver();

            var fileBtn = _driver!.FindElement(MobileBy.AccessibilityId("FileMenuButton"));
            fileBtn.Click();
            System.Threading.Thread.Sleep(1000);

            // The backstage NavigationView contains a "New" item; its presence
            // confirms the backstage overlay opened successfully.
            var newItem = _driver!.FindElement(MobileBy.AccessibilityId("BackstageNewNavItem"));
            Assert.NotNull(newItem);
        }
    }
}

