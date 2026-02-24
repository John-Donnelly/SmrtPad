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
    /// UI tests for status bar details (encoding, line/column, zoom display),
    /// theme toggle cycling, and comprehensive status bar state verification.
    ///
    /// Tests share one Appium session via <see cref="SharedAppFixture"/>.
    /// </summary>
    public sealed class StatusBarAndThemeUITests : IClassFixture<SharedAppFixture>, IDisposable
    {
        private readonly SharedAppFixture _fx;
        private readonly WindowsDriver? _driver;

        public StatusBarAndThemeUITests(SharedAppFixture fx)
        {
            _fx = fx;
            _driver = fx.Driver;
        }

        public void Dispose() { /* session owned by fixture */ }

        private void RequireDriver() =>
            Skip.If(!_fx.IsAvailable,
                "WinAppDriver / Appium not available or SmrtPad.exe not built.");

        private string StatusText => _fx.GetStatusBarText("StatusText");

        // ── Encoding display ─────────────────────────────────────────────────

        /// <summary>
        /// A fresh editor should display "UTF-8" as the encoding in the status bar.
        /// </summary>
        [SkippableFact]
        public void FreshEditor_EncodingDisplay_ShowsUtf8()
        {
            RequireDriver();
            string encoding = _fx.GetStatusBarText("EncodingText");
            Assert.Equal("UTF-8", encoding);
        }

        // ── Zoom display ─────────────────────────────────────────────────────

        /// <summary>
        /// The default zoom level should be 100%.
        /// </summary>
        [SkippableFact]
        public void DefaultZoom_Shows100Percent()
        {
            RequireDriver();

            // Reset to 100% first to ensure a known state
            for (int i = 0; i < 50; i++)
            {
                string zoomStr = _fx.GetStatusBarText("ZoomText").Replace("%", "");
                if (int.TryParse(zoomStr, out int zoom))
                {
                    if (zoom == 100) break;
                    if (zoom > 100)
                        _fx.ClickMenuItem("View", "Zoom Out");
                    else
                        _fx.ClickMenuItem("View", "Zoom In");
                    Thread.Sleep(150);
                }
                else break;
            }

            Assert.Equal("100%", _fx.GetStatusBarText("ZoomText"));
        }

        // ── Line/Column initial state ────────────────────────────────────────

        /// <summary>
        /// A fresh, empty editor should show the cursor at line 1.
        /// </summary>
        [SkippableFact]
        public void FreshEditor_LineCol_StartsAtLineOne()
        {
            RequireDriver();
            _fx.ClearEditor();

            string lineCol = _fx.GetStatusBarText("LineColText");
            Assert.StartsWith("Ln 1,", lineCol);
        }

        /// <summary>
        /// After inserting multiple newlines, the line number should advance
        /// accordingly (e.g., 3 newlines → Ln 4).
        /// </summary>
        [SkippableFact]
        public void ThreeNewlines_LineNumber_ShowsFour()
        {
            RequireDriver();
            _fx.ClearEditor();

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Enter);
            Thread.Sleep(100);
            editor.SendKeys(Keys.Enter);
            Thread.Sleep(100);
            editor.SendKeys(Keys.Enter);
            Thread.Sleep(200);

            string lineCol = _fx.GetStatusBarText("LineColText");
            Assert.StartsWith("Ln 4,", lineCol);
        }

        // ── Theme toggle ─────────────────────────────────────────────────────

        /// <summary>
        /// Clicking the theme toggle button should cycle through themes
        /// and update the status bar with the theme name.
        /// Three clicks should cycle through all themes.
        /// </summary>
        [SkippableFact]
        public void ThemeToggle_CyclesThemes_UpdatesStatus()
        {
            RequireDriver();

            _driver!.FindElement(MobileBy.AccessibilityId("ThemeToggleButton")).Click();
            Thread.Sleep(400);

            string firstTheme = StatusText;
            Assert.StartsWith("Theme:", firstTheme);

            _driver.FindElement(MobileBy.AccessibilityId("ThemeToggleButton")).Click();
            Thread.Sleep(400);

            string secondTheme = StatusText;
            Assert.StartsWith("Theme:", secondTheme);
            Assert.NotEqual(firstTheme, secondTheme);

            _driver.FindElement(MobileBy.AccessibilityId("ThemeToggleButton")).Click();
            Thread.Sleep(400);

            string thirdTheme = StatusText;
            Assert.StartsWith("Theme:", thirdTheme);
        }

        // ── Selection length on empty editor ─────────────────────────────────

        /// <summary>
        /// An empty editor with no selection should show "Sel: 0".
        /// </summary>
        [SkippableFact]
        public void EmptyEditor_SelectionLength_ShowsZero()
        {
            RequireDriver();
            _fx.ClearEditor();

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.Click();
            Thread.Sleep(200);

            string selText = _fx.GetStatusBarText("SelectionLengthText");
            Assert.Equal("Sel: 0", selText);
        }

        // ── Word count after multiline typing ────────────────────────────────

        /// <summary>
        /// Typing words on multiple lines should correctly count total words
        /// across all lines.
        /// </summary>
        [SkippableFact]
        public void MultilineText_WordCount_CountsAcrossLines()
        {
            RequireDriver();
            _fx.ClearEditor();

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            _fx.TypeInEditor("first line");
            editor.SendKeys(Keys.Enter);
            Thread.Sleep(100);
            _fx.TypeInEditor("second line");

            string wordCount = _fx.GetStatusBarText("WordCountText");
            Assert.Equal("Words: 4", wordCount);
        }

        // ── Status bar visibility ────────────────────────────────────────────

        /// <summary>
        /// The StatusBar ContentControl should be visible and accessible.
        /// </summary>
        [SkippableFact]
        public void StatusBar_IsVisible_AndAccessible()
        {
            RequireDriver();

            var statusBar = _driver!.FindElement(MobileBy.AccessibilityId("StatusBar"));
            Assert.NotNull(statusBar);
            Assert.True(statusBar.Displayed, "StatusBar should be visible");
        }

        // ── All status bar elements present ──────────────────────────────────

        /// <summary>
        /// All seven status bar indicators should be present and readable:
        /// StatusText, WordCountText, CharCountText, SelectionLengthText,
        /// LineColText, EncodingText, ZoomText.
        /// </summary>
        [SkippableFact]
        public void AllStatusBarElements_ArePresent()
        {
            RequireDriver();

            string[] ids =
            [
                "StatusText", "WordCountText", "CharCountText",
                "SelectionLengthText", "LineColText", "EncodingText", "ZoomText"
            ];

            foreach (string id in ids)
            {
                var element = _driver!.FindElement(MobileBy.AccessibilityId(id));
                Assert.NotNull(element);
            }
        }
    }
}
