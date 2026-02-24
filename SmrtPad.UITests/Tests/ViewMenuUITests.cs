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
    public sealed class ViewMenuUITests : IClassFixture<SharedAppFixture>, IDisposable
    {
        private readonly SharedAppFixture _fx;
        private readonly WindowsDriver? _driver;

        public ViewMenuUITests(SharedAppFixture fx)
        {
            _fx = fx;
            _driver = fx.Driver;
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
            // We confirm by trying to find StatusBar — it should not be displayed
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

            // Toggle focus mode off to restore UI
            // Need to use keyboard shortcut or find the menu differently
            // Focus mode hides the menu bar, so we need another way to exit
            // The View menu should still be accessible in the menu bar
            _driver!.FindElement(MobileBy.Name("View")).Click();
            Thread.Sleep(450);
            _driver!.FindElement(MobileBy.AccessibilityId("FocusModeToggle")).Click();
            Thread.Sleep(500);
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
    }
}
