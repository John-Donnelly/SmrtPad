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
    /// Functional UI tests for the Find and Replace features.
    /// Tests open the flyout-based Find/Replace panels from the Editing
    /// ribbon group, enter search/replace terms, and verify results via
    /// the status bar messages and editor state.
    ///
    /// Tests share one Appium session via <see cref="SharedAppFixture"/>.
    /// </summary>
    public sealed class FindReplaceUITests : IClassFixture<SharedAppFixture>, IDisposable
    {
        private readonly SharedAppFixture _fx;
        private readonly WindowsDriver? _driver;

        public FindReplaceUITests(SharedAppFixture fx)
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
        /// Opens the Find flyout by clicking the "Find" button in the Editing group.
        /// </summary>
        private void OpenFindFlyout()
        {
            // The Find button is identified by its text "Find" in the Editing group
            var findButtons = _driver!.FindElements(MobileBy.Name("Find"));
            // Click the button (not the ribbon label)
            foreach (var btn in findButtons)
            {
                try
                {
                    if (btn.TagName == "Button" || btn.Displayed)
                    {
                        btn.Click();
                        Thread.Sleep(500);
                        return;
                    }
                }
                catch { /* try next */ }
            }
        }

        /// <summary>
        /// Opens the Replace flyout by clicking the "Replace" button in the Editing group.
        /// </summary>
        private void OpenReplaceFlyout()
        {
            var replaceButtons = _driver!.FindElements(MobileBy.Name("Replace"));
            foreach (var btn in replaceButtons)
            {
                try
                {
                    if (btn.TagName == "Button" || btn.Displayed)
                    {
                        btn.Click();
                        Thread.Sleep(500);
                        return;
                    }
                }
                catch { /* try next */ }
            }
        }

        /// <summary>
        /// Closes any open flyout by pressing Escape.
        /// </summary>
        private void CloseFlyout()
        {
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Escape);
            Thread.Sleep(300);
        }

        // ── Find Next ────────────────────────────────────────────────────────

        /// <summary>
        /// Typing text in the editor, opening Find, searching for existing text
        /// should find a match (no "No match found." message).
        /// </summary>
        [SkippableFact]
        public void FindNext_ExistingText_FindsMatch()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("the quick brown fox jumps over the lazy dog");

            OpenFindFlyout();

            // Type search term in FindTextBox
            var findBox = _driver!.FindElement(MobileBy.AccessibilityId("FindTextBox"));
            findBox.Clear();
            findBox.SendKeys("fox");
            Thread.Sleep(200);

            // Click Find Next
            _driver.FindElement(MobileBy.Name("Find Next")).Click();
            Thread.Sleep(300);

            // Selection should have moved to "fox" — selection length should be 3
            string selText = _fx.GetStatusBarText("SelectionLengthText");
            Assert.Equal("Sel: 3", selText);

            CloseFlyout();
        }

        /// <summary>
        /// Searching for text that doesn't exist should show "No match found." status.
        /// </summary>
        [SkippableFact]
        public void FindNext_NonExistentText_ShowsNoMatchStatus()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("hello world");

            OpenFindFlyout();

            var findBox = _driver!.FindElement(MobileBy.AccessibilityId("FindTextBox"));
            findBox.Clear();
            findBox.SendKeys("xyz123");
            Thread.Sleep(200);

            _driver.FindElement(MobileBy.Name("Find Next")).Click();
            Thread.Sleep(300);

            Assert.Equal("No match found.", StatusText);

            CloseFlyout();
        }

        /// <summary>
        /// Find Previous should also find matches when searching backwards.
        /// </summary>
        [SkippableFact]
        public void FindPrevious_ExistingText_FindsMatch()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("apple banana apple cherry");

            // Move cursor to end
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + Keys.End);
            Thread.Sleep(200);

            OpenFindFlyout();

            var findBox = _driver!.FindElement(MobileBy.AccessibilityId("FindTextBox"));
            findBox.Clear();
            findBox.SendKeys("apple");
            Thread.Sleep(200);

            _driver.FindElement(MobileBy.Name("Find Previous")).Click();
            Thread.Sleep(300);

            // Should have found "apple" — selection length 5
            string selText = _fx.GetStatusBarText("SelectionLengthText");
            Assert.Equal("Sel: 5", selText);

            CloseFlyout();
        }

        // ── Replace All ──────────────────────────────────────────────────────

        /// <summary>
        /// Replace All should replace all occurrences and report the count
        /// in the status bar.
        /// </summary>
        [SkippableFact]
        public void ReplaceAll_ReplacesAllOccurrences_ReportsCount()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("cat cat cat dog");

            OpenReplaceFlyout();

            var findBox = _driver!.FindElement(MobileBy.AccessibilityId("ReplaceFindTextBox"));
            findBox.Clear();
            findBox.SendKeys("cat");
            Thread.Sleep(200);

            var replaceBox = _driver!.FindElement(MobileBy.AccessibilityId("ReplaceWithTextBox"));
            replaceBox.Clear();
            replaceBox.SendKeys("bat");
            Thread.Sleep(200);

            _driver.FindElement(MobileBy.Name("Replace All")).Click();
            Thread.Sleep(400);

            Assert.Equal("Replaced 3 occurrences.", StatusText);

            CloseFlyout();
        }

        /// <summary>
        /// Replace All with no matches should report "Replaced 0 occurrences."
        /// </summary>
        [SkippableFact]
        public void ReplaceAll_NoMatches_ReportsZero()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("hello world");

            OpenReplaceFlyout();

            var findBox = _driver!.FindElement(MobileBy.AccessibilityId("ReplaceFindTextBox"));
            findBox.Clear();
            findBox.SendKeys("xyz");
            Thread.Sleep(200);

            var replaceBox = _driver!.FindElement(MobileBy.AccessibilityId("ReplaceWithTextBox"));
            replaceBox.Clear();
            replaceBox.SendKeys("abc");
            Thread.Sleep(200);

            _driver.FindElement(MobileBy.Name("Replace All")).Click();
            Thread.Sleep(400);

            Assert.Equal("Replaced 0 occurrences.", StatusText);

            CloseFlyout();
        }

        // ── Highlight All ────────────────────────────────────────────────────

        /// <summary>
        /// Highlight All should highlight matching text without crashing.
        /// This tests the end-to-end highlight/clear flow.
        /// </summary>
        [SkippableFact]
        public void HighlightAll_ThenClearHighlights_CompletesWithoutError()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("test word test phrase test");

            OpenFindFlyout();

            var findBox = _driver!.FindElement(MobileBy.AccessibilityId("FindTextBox"));
            findBox.Clear();
            findBox.SendKeys("test");
            Thread.Sleep(200);

            // Click Highlight All
            _driver.FindElement(MobileBy.Name("Highlight All")).Click();
            Thread.Sleep(400);

            // Click Clear Highlights
            _driver.FindElement(MobileBy.Name("Clear Highlights")).Click();
            Thread.Sleep(300);

            // Verify editor is still functional
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);

            CloseFlyout();
        }
    }
}
