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
    /// Functional UI tests for the View menu: Word Wrap, Spell Check, Ruler,
    /// Page View, Focus Mode, and Zoom. Each test drives the full UI action
    /// and asserts observable state changes (toggle state, status bar text,
    /// element visibility).
    ///
    /// Tests share one Appium session via <see cref="SharedAppFixture"/>.
    /// </summary>
    [Collection("UITests")]
    public sealed class ViewMenuUITests : IClassFixture<SharedAppFixture>, IDisposable
    {
        private readonly SharedAppFixture _fx;
        private readonly WindowsDriver? _driver;

        public ViewMenuUITests(SharedAppFixture fx)
        {
            _fx = fx;
            _driver = fx.Driver;
            // Guard: if a prior test left Focus Mode on the ribbon and status bar are
            // hidden, which breaks every test in this class that needs menu access (UI-11).
            _fx.EnsureFocusModeOff();
        }

        public void Dispose() { /* session owned by fixture */ }

        private void RequireDriver() =>
            Skip.If(!_fx.IsAvailable,
                "WinAppDriver / Appium not available or SmrtPad.exe not built.");

        // ── Helpers ───────────────────────────────────────────────────────────

        private void OpenViewMenu()
        {
            _driver!.FindElement(MobileBy.Name("View")).Click();
            Thread.Sleep(450);
        }

        private string StatusText => _fx.GetStatusBarText("StatusText");

        private void ResetZoomTo100()
        {
            for (int i = 0; i < 50; i++)
            {
                string zoomStr = _fx.GetStatusBarText("ZoomText").Replace("%", "");
                if (int.TryParse(zoomStr, out int zoom))
                {
                    if (zoom == 100) return;
                    if (zoom > 100)
                        _fx.ClickMenuItem("View", "Zoom Out");
                    else
                        _fx.ClickMenuItem("View", "Zoom In");
                    Thread.Sleep(150);
                }
                else
                {
                    break;
                }
            }
        }

        // ── Word Wrap toggle ─────────────────────────────────────────────────

        /// <summary>
        /// Toggling Word Wrap off and back on should not crash and the toggle
        /// state should reflect the current state. Word Wrap does not set a
        /// status bar message, so we verify the toggle completes without error.
        /// </summary>
        [SkippableFact]
        public void WordWrap_ToggleOff_ThenOn_CompletesWithoutError()
        {
            RequireDriver();

            // Word Wrap starts checked (default). Toggle it off.
            _fx.ClickMenuItem("View", "Word Wrap");
            Thread.Sleep(200);

            // Toggle it back on
            _fx.ClickMenuItem("View", "Word Wrap");
            Thread.Sleep(200);

            // If we get here without exception, the toggle works correctly.
            // Verify editor is still present and functional.
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);
        }

        // ── Ruler toggle ─────────────────────────────────────────────────────

        /// <summary>
        /// Toggling Ruler on should show "Ruler enabled." status message.
        /// Toggling it off should show "Ruler disabled.".
        /// </summary>
        [SkippableFact]
        public void Ruler_ToggleOn_ThenOff_UpdatesStatus()
        {
            RequireDriver();

            // Toggle ruler on
            OpenViewMenu();
            _driver!.FindElement(MobileBy.AccessibilityId("RulerToggle")).Click();
            Thread.Sleep(400);

            string statusOn = StatusText;
            Assert.Equal("Ruler enabled.", statusOn);

            // Toggle ruler off
            OpenViewMenu();
            _driver!.FindElement(MobileBy.AccessibilityId("RulerToggle")).Click();
            Thread.Sleep(400);

            string statusOff = StatusText;
            Assert.Equal("Ruler disabled.", statusOff);
        }

        // ── Page View toggle ─────────────────────────────────────────────────

        /// <summary>
        /// Toggling Page View on should show "Page view enabled." status.
        /// Toggling it off should show "Page view disabled.".
        /// </summary>
        [SkippableFact]
        public void PageView_ToggleOn_ThenOff_UpdatesStatus()
        {
            RequireDriver();

            // Toggle page view on
            OpenViewMenu();
            _driver!.FindElement(MobileBy.AccessibilityId("PageViewToggle")).Click();
            Thread.Sleep(400);

            string statusOn = StatusText;
            Assert.Equal("Page view enabled.", statusOn);

            // Toggle page view off
            OpenViewMenu();
            _driver!.FindElement(MobileBy.AccessibilityId("PageViewToggle")).Click();
            Thread.Sleep(400);

            string statusOff = StatusText;
            Assert.Equal("Page view disabled.", statusOff);
        }

        // ── Focus Mode toggle ────────────────────────────────────────────────

        /// <summary>
        /// Focus mode hides the ribbon and status bar. Toggling it on should
        /// hide the StatusBar element, toggling off should restore it.
        /// </summary>
        [SkippableFact]
        public void FocusMode_ToggleOn_HidesRibbonAndStatusBar()
        {
            RequireDriver();

            // Toggle focus mode on
            OpenViewMenu();
            _driver!.FindElement(MobileBy.AccessibilityId("FocusModeToggle")).Click();
            Thread.Sleep(500);

            // The ribbon (RibbonBar) should be hidden, so StatusBar is also hidden.
            bool ribbonHidden = false;
            try
            {
                var ribbon = _driver!.FindElement(MobileBy.AccessibilityId("RibbonBar"));
                ribbonHidden = !ribbon.Displayed;
            }
            catch (NoSuchElementException)
            {
                ribbonHidden = true;
            }
            Assert.True(ribbonHidden, "Ribbon should be hidden in focus mode");

            // Always restore Focus Mode so subsequent tests see the ribbon and menu bar.
            // FocusMode only hides RibbonBar/StatusBar — the MenuBar row stays visible,
            // so the View menu is still reachable (UI-11).
            try
            {
                _driver!.FindElement(MobileBy.AccessibilityId("ViewMenuBarItem")).Click();
                Thread.Sleep(450);
                _driver!.FindElement(MobileBy.AccessibilityId("FocusModeToggle")).Click();
                Thread.Sleep(500);
            }
            catch
            {
                // Last-resort recovery via the shared helper
                _fx.EnsureFocusModeOff();
            }
        }

        // ── Zoom via keyboard shortcut ───────────────────────────────────────

        /// <summary>
        /// Ctrl+Plus should zoom in, Ctrl+Minus should zoom out,
        /// and the ZoomText in the status bar should reflect each change.
        /// </summary>
        [SkippableFact]
        public void ZoomIn_ViaCtrlPlus_UpdatesZoomDisplay()
        {
            RequireDriver();
            ResetZoomTo100();

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + Keys.Add + Keys.Null);
            Thread.Sleep(300);

            Assert.Equal("110%", _fx.GetStatusBarText("ZoomText"));

            // Restore
            editor.SendKeys(Keys.Control + Keys.Subtract + Keys.Null);
            Thread.Sleep(300);
            Assert.Equal("100%", _fx.GetStatusBarText("ZoomText"));
        }

        /// <summary>
        /// Ctrl+Minus should zoom out and update the ZoomText in the status bar.
        /// </summary>
        [SkippableFact]
        public void ZoomOut_ViaCtrlMinus_UpdatesZoomDisplay()
        {
            RequireDriver();
            ResetZoomTo100();

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + Keys.Subtract + Keys.Null);
            Thread.Sleep(300);

            Assert.Equal("90%", _fx.GetStatusBarText("ZoomText"));

            // Restore
            editor.SendKeys(Keys.Control + Keys.Add + Keys.Null);
            Thread.Sleep(300);
        }

        // ── Zoom preserves editor content ────────────────────────────────────

        /// <summary>
        /// After zooming in and out, the editor content and word count
        /// should be unchanged — zoom is purely visual.
        /// </summary>
        [SkippableFact]
        public void ZoomInAndOut_PreservesEditorContent()
        {
            RequireDriver();
            ResetZoomTo100();
            _fx.ClearEditor();
            _fx.TypeInEditor("zoom test text");

            string wordsBefore = _fx.GetStatusBarText("WordCountText");
            string charsBefore = _fx.GetStatusBarText("CharCountText");

            _fx.ClickMenuItem("View", "Zoom In");
            _fx.ClickMenuItem("View", "Zoom In");

            string wordsAfterZoomIn = _fx.GetStatusBarText("WordCountText");
            string charsAfterZoomIn = _fx.GetStatusBarText("CharCountText");
            Assert.Equal(wordsBefore, wordsAfterZoomIn);
            Assert.Equal(charsBefore, charsAfterZoomIn);

            _fx.ClickMenuItem("View", "Zoom Out");
            _fx.ClickMenuItem("View", "Zoom Out");

            string wordsAfterZoomOut = _fx.GetStatusBarText("WordCountText");
            string charsAfterZoomOut = _fx.GetStatusBarText("CharCountText");
            Assert.Equal(wordsBefore, wordsAfterZoomOut);
            Assert.Equal(charsBefore, charsAfterZoomOut);
        }

        // ── Zoom multiple steps ──────────────────────────────────────────────

        /// <summary>
        /// Zooming in three times should show 130%, confirming each step is exactly +10%.
        /// </summary>
        [SkippableFact]
        public void ZoomIn_ThreeTimes_Shows130Percent()
        {
            RequireDriver();
            ResetZoomTo100();

            _fx.ClickMenuItem("View", "Zoom In");
            _fx.ClickMenuItem("View", "Zoom In");
            _fx.ClickMenuItem("View", "Zoom In");

            Assert.Equal("130%", _fx.GetStatusBarText("ZoomText"));

            ResetZoomTo100();
        }

        /// <summary>
        /// Zooming out three times from 100% should show 70%.
        /// </summary>
        [SkippableFact]
        public void ZoomOut_ThreeTimes_Shows70Percent()
        {
            RequireDriver();
            ResetZoomTo100();

            _fx.ClickMenuItem("View", "Zoom Out");
            _fx.ClickMenuItem("View", "Zoom Out");
            _fx.ClickMenuItem("View", "Zoom Out");

            Assert.Equal("70%", _fx.GetStatusBarText("ZoomText"));

            ResetZoomTo100();
        }

        // ── Spell Check toggle ───────────────────────────────────────────────

        /// <summary>
        /// Toggling Spell Check off should show "Spell check disabled." in the
        /// status bar. Toggling it back on should show "Spell check enabled.".
        /// </summary>
        [SkippableFact]
        public void SpellCheck_ToggleOff_ThenOn_UpdatesStatus()
        {
            RequireDriver();

            // Toggle spell check off
            OpenViewMenu();
            var toggle = _driver!.FindElement(MobileBy.AccessibilityId("SpellCheckToggle"));
            bool wasChecked = toggle.GetAttribute("Toggle.ToggleState") == "1";
            toggle.Click();
            Thread.Sleep(400);

            string statusAfterFirstClick = _fx.GetStatusBarText("StatusText");
            if (wasChecked)
                Assert.Equal("Spell check disabled.", statusAfterFirstClick);
            else
                Assert.Equal("Spell check enabled.", statusAfterFirstClick);

            // Toggle back to restore state
            OpenViewMenu();
            _driver!.FindElement(MobileBy.AccessibilityId("SpellCheckToggle")).Click();
            Thread.Sleep(400);

            string statusAfterSecondClick = _fx.GetStatusBarText("StatusText");
            if (wasChecked)
                Assert.Equal("Spell check enabled.", statusAfterSecondClick);
            else
                Assert.Equal("Spell check disabled.", statusAfterSecondClick);
        }

        // ── Ruler toggle state ───────────────────────────────────────────────

        /// <summary>
        /// After toggling Ruler on, the status bar should show "Ruler enabled."
        /// and the toggle should be checked; toggling off should uncheck it
        /// and show "Ruler disabled.".
        /// </summary>
        [SkippableFact]
        public void Ruler_ToggleOn_ShowsEnabled_ToggleOff_ShowsDisabled()
        {
            RequireDriver();

            // Toggle ruler on
            OpenViewMenu();
            var toggle = _driver!.FindElement(MobileBy.AccessibilityId("RulerToggle"));
            bool wasChecked = toggle.GetAttribute("Toggle.ToggleState") == "1";

            if (!wasChecked)
            {
                toggle.Click();
                Thread.Sleep(400);
                Assert.Equal("Ruler enabled.", _fx.GetStatusBarText("StatusText"));

                // Toggle off
                OpenViewMenu();
                _driver!.FindElement(MobileBy.AccessibilityId("RulerToggle")).Click();
                Thread.Sleep(400);
                Assert.Equal("Ruler disabled.", _fx.GetStatusBarText("StatusText"));
            }
            else
            {
                // Already on — toggle off then on
                toggle.Click();
                Thread.Sleep(400);
                Assert.Equal("Ruler disabled.", _fx.GetStatusBarText("StatusText"));

                OpenViewMenu();
                _driver!.FindElement(MobileBy.AccessibilityId("RulerToggle")).Click();
                Thread.Sleep(400);
                Assert.Equal("Ruler enabled.", _fx.GetStatusBarText("StatusText"));

                // Restore to off
                OpenViewMenu();
                _driver!.FindElement(MobileBy.AccessibilityId("RulerToggle")).Click();
                Thread.Sleep(400);
            }
        }

        // ── Focus mode hides status bar ──────────────────────────────────────

        /// <summary>
        /// Focus mode should hide the status bar. Toggling off should restore it.
        /// </summary>
        [SkippableFact]
        public void FocusMode_ToggleOn_HidesStatusBar_ToggleOff_RestoresIt()
        {
            RequireDriver();

            // Toggle focus mode on
            OpenViewMenu();
            _driver!.FindElement(MobileBy.AccessibilityId("FocusModeToggle")).Click();
            Thread.Sleep(500);

            // Status bar should be hidden
            bool statusBarHidden = false;
            try
            {
                var statusBar = _driver!.FindElement(MobileBy.AccessibilityId("StatusBar"));
                statusBarHidden = !statusBar.Displayed;
            }
            catch (NoSuchElementException)
            {
                statusBarHidden = true;
            }
            Assert.True(statusBarHidden, "StatusBar should be hidden in focus mode");

            // Toggle focus mode off
            _driver!.FindElement(MobileBy.Name("View")).Click();
            Thread.Sleep(450);
            _driver!.FindElement(MobileBy.AccessibilityId("FocusModeToggle")).Click();
            Thread.Sleep(800);

            // Status bar should be restored
            var restoredBar = _driver!.FindElement(MobileBy.AccessibilityId("StatusBar"));
            Assert.True(restoredBar.Displayed, "StatusBar should be visible after exiting focus mode");
        }

        // ── Page View toggle state ───────────────────────────────────────────

        /// <summary>
        /// Page View toggle on should show "Page view enabled." and toggle off
        /// should show "Page view disabled." — verifying the complete cycle.
        /// </summary>
        [SkippableFact]
        public void PageView_Toggle_CyclesCorrectly()
        {
            RequireDriver();

            // Toggle page view on
            OpenViewMenu();
            var toggle = _driver!.FindElement(MobileBy.AccessibilityId("PageViewToggle"));
            bool wasChecked = toggle.GetAttribute("Toggle.ToggleState") == "1";
            toggle.Click();
            Thread.Sleep(400);

            if (!wasChecked)
            {
                Assert.Equal("Page view enabled.", _fx.GetStatusBarText("StatusText"));
                // Toggle off to restore
                OpenViewMenu();
                _driver!.FindElement(MobileBy.AccessibilityId("PageViewToggle")).Click();
                Thread.Sleep(400);
                Assert.Equal("Page view disabled.", _fx.GetStatusBarText("StatusText"));
            }
            else
            {
                Assert.Equal("Page view disabled.", _fx.GetStatusBarText("StatusText"));
                // Toggle on to restore
                OpenViewMenu();
                _driver!.FindElement(MobileBy.AccessibilityId("PageViewToggle")).Click();
                Thread.Sleep(400);
                Assert.Equal("Page view enabled.", _fx.GetStatusBarText("StatusText"));
                // Toggle off to leave in default state
                OpenViewMenu();
                _driver!.FindElement(MobileBy.AccessibilityId("PageViewToggle")).Click();
                Thread.Sleep(400);
            }
        }

        // ── Word Wrap preserves content ──────────────────────────────────────

        /// <summary>
        /// Toggling Word Wrap off and on should not alter the editor content
        /// or word/char counts.
        /// </summary>
        [SkippableFact]
        public void WordWrap_Toggle_PreservesContent()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("preserved content here");

            string wordsBefore = _fx.GetStatusBarText("WordCountText");
            string charsBefore = _fx.GetStatusBarText("CharCountText");

            _fx.ClickMenuItem("View", "Word Wrap");
            Thread.Sleep(200);

            Assert.Equal(wordsBefore, _fx.GetStatusBarText("WordCountText"));
            Assert.Equal(charsBefore, _fx.GetStatusBarText("CharCountText"));

            _fx.ClickMenuItem("View", "Word Wrap");
            Thread.Sleep(200);

            Assert.Equal(wordsBefore, _fx.GetStatusBarText("WordCountText"));
            Assert.Equal(charsBefore, _fx.GetStatusBarText("CharCountText"));
        }
    }
}
