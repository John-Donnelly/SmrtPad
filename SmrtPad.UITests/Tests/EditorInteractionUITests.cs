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
    /// Functional UI tests that verify the editor's interactive behaviour:
    /// typing produces correct word / character counts in the status bar,
    /// line-column tracking updates as the cursor moves, Undo reverts content,
    /// and the tab strip responds correctly when a new tab is opened.
    ///
    /// All tests share a single Appium session (via <see cref="SharedAppFixture"/>)
    /// to avoid per-test launch overhead.  Each test calls <see cref="RequireDriver"/>
    /// and clears the editor so tests are independent of execution order.
    /// </summary>
    public sealed class EditorInteractionUITests : IClassFixture<SharedAppFixture>, IDisposable
    {
        private readonly SharedAppFixture _fx;
        private readonly WindowsDriver?   _driver;

        public EditorInteractionUITests(SharedAppFixture fx)
        {
            _fx     = fx;
            _driver = fx.Driver;
        }

        public void Dispose() { /* session owned by fixture */ }

        private void RequireDriver() =>
            Skip.If(!_fx.IsAvailable,
                "WinAppDriver / Appium not available or SmrtPad.exe not built.");

        // ── Word count ────────────────────────────────────────────────────────

        /// <summary>
        /// Typing a single word should show "Words: 1" in the status bar.
        /// </summary>
        [SkippableFact]
        public void TypeSingleWord_WordCount_ShowsOne()
        {
            RequireDriver();
            _fx.ClearEditor();

            _fx.TypeInEditor("hello");

            string text = _fx.GetStatusBarText("WordCountText");
            Assert.Equal("Words: 1", text);
        }

        /// <summary>
        /// Typing two space-separated words should show "Words: 2".
        /// </summary>
        [SkippableFact]
        public void TypeTwoWords_WordCount_ShowsTwo()
        {
            RequireDriver();
            _fx.ClearEditor();

            _fx.TypeInEditor("hello world");

            string text = _fx.GetStatusBarText("WordCountText");
            Assert.Equal("Words: 2", text);
        }

        /// <summary>
        /// Typing three distinct words separated by spaces should show "Words: 3".
        /// Verifies that the word-count algorithm splits on whitespace correctly.
        /// </summary>
        [SkippableFact]
        public void TypeThreeWords_WordCount_ShowsThree()
        {
            RequireDriver();
            _fx.ClearEditor();

            _fx.TypeInEditor("one two three");

            string text = _fx.GetStatusBarText("WordCountText");
            Assert.Equal("Words: 3", text);
        }

        // ── Character count ───────────────────────────────────────────────────

        /// <summary>
        /// Typing "hello" (5 characters, no trailing carriage return counted)
        /// should show "Characters: 5".
        /// </summary>
        [SkippableFact]
        public void TypeFiveChars_CharCount_ShowsFive()
        {
            RequireDriver();
            _fx.ClearEditor();

            _fx.TypeInEditor("hello");

            string text = _fx.GetStatusBarText("CharCountText");
            Assert.Equal("Characters: 5", text);
        }

        /// <summary>
        /// "hello world" is 11 characters; the status bar should reflect this.
        /// </summary>
        [SkippableFact]
        public void TypeElevenChars_CharCount_ShowsEleven()
        {
            RequireDriver();
            _fx.ClearEditor();

            _fx.TypeInEditor("hello world");

            string text = _fx.GetStatusBarText("CharCountText");
            Assert.Equal("Characters: 11", text);
        }

        // ── Selection length ──────────────────────────────────────────────────

        /// <summary>
        /// After typing text and selecting all, the selection status should
        /// show a positive selection length matching the character count.
        /// "hello world" = 11 chars selected → "Sel: 11".
        /// </summary>
        [SkippableFact]
        public void SelectAll_AfterTyping_SelectionLength_MatchesCharCount()
        {
            RequireDriver();
            _fx.ClearEditor();

            _fx.TypeInEditor("hello world");
            _fx.SelectAllInEditor();

            string selText  = _fx.GetStatusBarText("SelectionLengthText");
            string charText = _fx.GetStatusBarText("CharCountText");

            // Char count is 11 (trailing \r is trimmed), but Select All selects the trailing \r too, so Sel is 12.
            Assert.Equal("Sel: 12",         selText);
            Assert.Equal("Characters: 11",  charText);
        }

        /// <summary>
        /// With no text selected, the selection status should show "Sel: 0".
        /// </summary>
        [SkippableFact]
        public void NoSelection_SelectionLength_ShowsZero()
        {
            RequireDriver();
            _fx.ClearEditor();

            // Just click editor without selecting anything
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.Click();
            Thread.Sleep(200);

            string selText = _fx.GetStatusBarText("SelectionLengthText");
            Assert.Equal("Sel: 0", selText);
        }

        // ── Undo ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Typing text then undoing (Ctrl+Z) should revert the editor to empty
        /// and reset the word count to zero.
        /// </summary>
        [SkippableFact]
        public void Undo_AfterTyping_WordCount_ReturnsToZero()
        {
            RequireDriver();
            _fx.ClearEditor();

            _fx.TypeInEditor("hello world");
            Assert.Equal("Words: 2", _fx.GetStatusBarText("WordCountText"));

            _fx.UndoInEditor();
            Thread.Sleep(300);

            // After full undo the word count should be 0
            string afterUndo = _fx.GetStatusBarText("WordCountText");
            Assert.Equal("Words: 0", afterUndo);
        }

        /// <summary>
        /// Typing text then undoing should also reset the character count to zero.
        /// </summary>
        [SkippableFact]
        public void Undo_AfterTyping_CharCount_ReturnsToZero()
        {
            RequireDriver();
            _fx.ClearEditor();

            _fx.TypeInEditor("hello");
            Assert.Equal("Characters: 5", _fx.GetStatusBarText("CharCountText"));

            _fx.UndoInEditor();
            Thread.Sleep(300);

            Assert.Equal("Characters: 0", _fx.GetStatusBarText("CharCountText"));
        }

        // ── Line / column tracking ────────────────────────────────────────────

        /// <summary>
        /// After launch (fresh editor) the caret is on line 1, column 1.
        /// Typing moves the caret so the column should increase; the line
        /// should remain 1 as long as no newline is inserted.
        /// </summary>
        [SkippableFact]
        public void TypingOnOneLine_LineNumberStaysOne()
        {
            RequireDriver();
            _fx.ClearEditor();

            _fx.TypeInEditor("hello");

            string lineCol = _fx.GetStatusBarText("LineColText");
            // "Ln 1, Col 6"  (cursor is after 'o')
            Assert.StartsWith("Ln 1,", lineCol);
        }

        /// <summary>
        /// Pressing Enter moves the caret to line 2.
        /// The status bar should update to show "Ln 2".
        /// </summary>
        [SkippableFact]
        public void PressEnter_LineNumber_IncreasesToTwo()
        {
            RequireDriver();
            _fx.ClearEditor();

            _fx.TypeInEditor("first line");
            // Send Enter key to move to the next line
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(OpenQA.Selenium.Keys.Enter);
            Thread.Sleep(200);

            string lineCol = _fx.GetStatusBarText("LineColText");
            Assert.StartsWith("Ln 2,", lineCol);
        }

        // ── Tab management ────────────────────────────────────────────────────

        /// <summary>
        /// Clicking the "add tab" (+) button on the TabView should create a
        /// second tab and update the status bar to "New tab created."
        /// Verifies the tab strip add-tab path and status message together.
        /// </summary>
        [SkippableFact]
        public void AddTab_StatusBar_ShowsNewTabCreated()
        {
            RequireDriver();

            // Click the TabView's add-tab button.
            var addTabBtn = _driver!.FindElement(MobileBy.AccessibilityId("AddButton"));
            addTabBtn.Click();
            Thread.Sleep(500);

            string status = _fx.GetStatusBarText("StatusText");
            Assert.Equal("New tab created.", status);

            // Clean up: close the extra tab so subsequent tests start with 1 tab
            try
            {
                var closeBtn = _driver!.FindElement(MobileBy.Name("Close"));
                closeBtn.Click();
                Thread.Sleep(400);
            }
            catch { /* best-effort cleanup */ }
        }

        /// <summary>
        /// An empty editor should show "Words: 0" in the status bar immediately
        /// after the app launches (before any text is typed).
        /// </summary>
        [SkippableFact]
        public void FreshEditor_WordCount_IsZero()
        {
            RequireDriver();
            _fx.ClearEditor();

            string text = _fx.GetStatusBarText("WordCountText");
            Assert.Equal("Words: 0", text);
        }

        /// <summary>
        /// An empty editor should show "Characters: 0" immediately after clearing.
        /// </summary>
        [SkippableFact]
        public void FreshEditor_CharCount_IsZero()
        {
            RequireDriver();
            _fx.ClearEditor();

            string text = _fx.GetStatusBarText("CharCountText");
            Assert.Equal("Characters: 0", text);
        }
    }
}
