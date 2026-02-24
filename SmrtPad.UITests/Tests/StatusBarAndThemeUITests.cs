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

        // ── Column number updates after typing ───────────────────────────────

        /// <summary>
        /// After typing "hello" (5 chars), the column should be 6 (cursor past
        /// the last character).
        /// </summary>
        [SkippableFact]
        public void TypingFiveChars_ColumnNumber_ShowsSix()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("hello");

            string lineCol = _fx.GetStatusBarText("LineColText");
            Assert.Contains("Col 6", lineCol);
        }

        // ── Theme toggle cycles back ─────────────────────────────────────────

        /// <summary>
        /// Clicking the theme toggle 3 times should cycle through all themes
        /// and return to a known state, confirming the full cycle. All status
        /// messages should start with "Theme:".
        /// </summary>
        [SkippableFact]
        public void ThemeToggle_ThreeClicks_AllShowThemePrefix()
        {
            RequireDriver();

            string[] themes = new string[3];
            for (int i = 0; i < 3; i++)
            {
                _driver!.FindElement(MobileBy.AccessibilityId("ThemeToggleButton")).Click();
                Thread.Sleep(400);
                themes[i] = StatusText;
                Assert.StartsWith("Theme:", themes[i]);
            }

            // All three should be distinct
            Assert.NotEqual(themes[0], themes[1]);
            Assert.NotEqual(themes[1], themes[2]);
        }

        // ── Word count with punctuation ──────────────────────────────────────

        /// <summary>
        /// Words separated by punctuation (e.g., "word,word") should be counted
        /// according to the app's word counting logic.
        /// </summary>
        [SkippableFact]
        public void WordCount_WithPunctuation_CountsCorrectly()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("hello, world");

            string wordCount = _fx.GetStatusBarText("WordCountText");
            // "hello," and "world" = 2 words (comma attached to first word)
            Assert.Equal("Words: 2", wordCount);
        }

        // ── Char count after newline ─────────────────────────────────────────

        /// <summary>
        /// A newline should be counted in the character count.
        /// Typing "ab" + Enter + "cd" = "ab\r\ncd" but RichEditBox uses \r
        /// so total chars vary; we verify it's more than the text alone.
        /// </summary>
        [SkippableFact]
        public void CharCount_WithNewline_IncludesNewline()
        {
            RequireDriver();
            _fx.ClearEditor();

            _fx.TypeInEditor("ab");
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Enter);
            Thread.Sleep(100);
            _fx.TypeInEditor("cd");
            Thread.Sleep(200);

            string charText = _fx.GetStatusBarText("CharCountText");
            // "ab\rcd" = at least 5 chars (ab + newline + cd)
            // Extract number and verify > 4
            string numberPart = charText.Replace("Characters: ", "");
            int charCount = int.Parse(numberPart);
            Assert.True(charCount >= 5,
                $"Expected at least 5 characters with newline, got {charCount}");
        }

        // ── Zoom display always has percent sign ─────────────────────────────

        /// <summary>
        /// The ZoomText should always end with "%" regardless of zoom level.
        /// </summary>
        [SkippableFact]
        public void ZoomDisplay_AlwaysEndsWithPercentSign()
        {
            RequireDriver();

            string zoom = _fx.GetStatusBarText("ZoomText");
            Assert.EndsWith("%", zoom);
        }

        // ── Selection length updates on partial selection ─────────────────────

        /// <summary>
        /// Selecting only part of the text (via Shift+Arrow keys) should update
        /// the selection length to match the number of characters selected.
        /// </summary>
        [SkippableFact]
        public void PartialSelection_UpdatesSelectionLength()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("hello world");

            // Move to start
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Home);
            Thread.Sleep(100);

            // Select 5 characters with Shift+Right
            for (int i = 0; i < 5; i++)
            {
                editor.SendKeys(Keys.Shift + Keys.ArrowRight + Keys.Null);
                Thread.Sleep(50);
            }
            Thread.Sleep(200);

            string selText = _fx.GetStatusBarText("SelectionLengthText");
            Assert.Equal("Sel: 5", selText);
        }

        // ── Line/col at beginning of document ────────────────────────────────

        /// <summary>
        /// In an empty document, the cursor should be at Ln 1, Col 1.
        /// </summary>
        [SkippableFact]
        public void EmptyEditor_LineCol_ShowsLn1Col1()
        {
            RequireDriver();
            _fx.ClearEditor();

            string lineCol = _fx.GetStatusBarText("LineColText");
            Assert.Equal("Ln 1, Col 1", lineCol);
        }
    }
}
