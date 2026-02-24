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
    /// Functional UI tests for the tabbed document interface: creating tabs,
    /// closing tabs, switching between tabs, and verifying that each tab
    /// maintains independent state.
    ///
    /// Tests share one Appium session via <see cref="SharedAppFixture"/>.
    /// </summary>
    public sealed class TabManagementUITests : IClassFixture<SharedAppFixture>, IDisposable
    {
        private readonly SharedAppFixture _fx;
        private readonly WindowsDriver? _driver;

        public TabManagementUITests(SharedAppFixture fx)
        {
            _fx = fx;
            _driver = fx.Driver;
        }

        public void Dispose() { /* session owned by fixture */ }

        private void RequireDriver() =>
            Skip.If(!_fx.IsAvailable,
                "WinAppDriver / Appium not available or SmrtPad.exe not built.");

        private string StatusText => _fx.GetStatusBarText("StatusText");

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Clicks the TabView "+" button to add a new tab.
        /// </summary>
        private void AddNewTab()
        {
            _driver!.FindElement(MobileBy.AccessibilityId("AddButton")).Click();
            Thread.Sleep(500);
        }

        /// <summary>
        /// Closes the currently active tab by clicking its Close button.
        /// </summary>
        private void CloseActiveTab()
        {
            try
            {
                var closeBtn = _driver!.FindElement(MobileBy.Name("Close"));
                closeBtn.Click();
                Thread.Sleep(400);
            }
            catch { /* tab may have been already closed */ }
        }

        // ── Create tab ───────────────────────────────────────────────────────

        /// <summary>
        /// Clicking the add-tab button should create a new tab and set
        /// the status bar to "New tab created."
        /// </summary>
        [SkippableFact]
        public void AddTab_CreatesNewTab_StatusShowsNewTabCreated()
        {
            RequireDriver();

            AddNewTab();

            Assert.Equal("New tab created.", StatusText);

            // Clean up: close the extra tab
            CloseActiveTab();
        }

        /// <summary>
        /// Using Ctrl+T keyboard shortcut should also create a new tab.
        /// </summary>
        [SkippableFact]
        public void AddTab_ViaCtrlT_CreatesNewTab()
        {
            RequireDriver();

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "t");
            Thread.Sleep(500);

            Assert.Equal("New tab created.", StatusText);

            // Clean up
            CloseActiveTab();
        }

        // ── Close tab ────────────────────────────────────────────────────────

        /// <summary>
        /// Closing a tab should show "Tab closed." in the status bar.
        /// After closing all extra tabs, at least one tab should remain
        /// (the app always keeps at least one tab open).
        /// </summary>
        [SkippableFact]
        public void CloseTab_ShowsTabClosedStatus()
        {
            RequireDriver();

            // Create an extra tab so we can close it
            AddNewTab();
            Thread.Sleep(200);

            CloseActiveTab();

            Assert.Equal("Tab closed.", StatusText);
        }

        /// <summary>
        /// Closing the last tab should create a new blank tab automatically,
        /// ensuring the app always has at least one tab open.
        /// </summary>
        [SkippableFact]
        public void CloseLastTab_CreatesNewBlankTab()
        {
            RequireDriver();

            // Close the current tab — since there's only one, a new blank one should open
            CloseActiveTab();

            // The editor should still be present
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);

            // Word count should be 0 (blank tab)
            Assert.Equal("Words: 0", _fx.GetStatusBarText("WordCountText"));
        }

        // ── Tab switching ────────────────────────────────────────────────────

        /// <summary>
        /// Creating two tabs, typing in each, and switching between them
        /// should preserve independent word counts per tab.
        /// </summary>
        [SkippableFact]
        public void SwitchTabs_PreservesIndependentContent()
        {
            RequireDriver();

            // Type in the first tab
            _fx.ClearEditor();
            _fx.TypeInEditor("tab one content");
            string firstTabWords = _fx.GetStatusBarText("WordCountText");
            Assert.Equal("Words: 3", firstTabWords);

            // Create a second tab (auto-focused)
            AddNewTab();

            // Type different content in the second tab
            _fx.TypeInEditor("second tab");
            string secondTabWords = _fx.GetStatusBarText("WordCountText");
            Assert.Equal("Words: 2", secondTabWords);

            // Switch back to first tab by clicking it
            var firstTab = _driver!.FindElement(MobileBy.Name("Untitled"));
            firstTab.Click();
            Thread.Sleep(400);

            // Word count should reflect first tab's content
            string firstTabWordsAgain = _fx.GetStatusBarText("WordCountText");
            Assert.Equal("Words: 3", firstTabWordsAgain);

            // Clean up: close second tab, switch to first
            // Navigate back to second tab and close it
            AddNewTab(); // This creates a third, switch to second
            CloseActiveTab(); // Close third
            CloseActiveTab(); // Close remaining extra
        }

        // ── Multiple tabs ────────────────────────────────────────────────────

        /// <summary>
        /// Creating multiple tabs should not crash and each creation
        /// should report "New tab created." in the status bar.
        /// </summary>
        [SkippableFact]
        public void CreateMultipleTabs_AllSucceed()
        {
            RequireDriver();

            for (int i = 0; i < 3; i++)
            {
                AddNewTab();
                Assert.Equal("New tab created.", StatusText);
            }

            // Clean up: close the extra tabs
            for (int i = 0; i < 3; i++)
            {
                CloseActiveTab();
            }
        }
    }
}
