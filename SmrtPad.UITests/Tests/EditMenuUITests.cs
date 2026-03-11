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
    /// Functional UI tests for the Edit menu operations: Cut, Copy, Paste,
    /// Paste Special, Select All, Undo, and Redo. Each test drives the full
    /// UI action and verifies the outcome via observable state (editor content,
    /// status bar counts, toggle states).
    ///
    /// Tests share one Appium session via <see cref="SharedAppFixture"/>.
    /// </summary>
    [Collection("UITests")]
    public sealed class EditMenuUITests : IClassFixture<SharedAppFixture>, IDisposable
    {
        private readonly SharedAppFixture _fx;
        private readonly WindowsDriver? _driver;

        public EditMenuUITests(SharedAppFixture fx)
        {
            _fx = fx;
            _driver = fx.Driver;
        }

        public void Dispose() { /* session owned by fixture */ }

        private void RequireDriver() =>
            Skip.If(!_fx.IsAvailable,
                "WinAppDriver / Appium not available or SmrtPad.exe not built.");

        // ── Select All ───────────────────────────────────────────────────────

        /// <summary>
        /// Using Ctrl+A after typing text should select all content,
        /// confirmed by a positive selection length in the status bar.
        /// </summary>
        [SkippableFact]
        public void SelectAll_ViaCtrlA_SelectsEntireContent()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("select all test");

            _fx.SelectAllInEditor();

            string selText = _fx.GetStatusBarText("SelectionLengthText");
            // "select all test" = 15 chars + trailing \r = Sel: 16
            Assert.Equal("Sel: 16", selText);
        }

        // ── Cut ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Selecting all text and cutting (Ctrl+X) should clear the editor
        /// and reset word/char counts to zero.
        /// </summary>
        [SkippableFact]
        public void Cut_ViaCtrlX_RemovesSelectedText()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("cut this text");
            Assert.Equal("Words: 3", _fx.GetStatusBarText("WordCountText"));

            _fx.SelectAllInEditor();

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "x");
            Thread.Sleep(300);

            Assert.Equal("Words: 0", _fx.GetStatusBarText("WordCountText"));
            Assert.Equal("Characters: 0", _fx.GetStatusBarText("CharCountText"));
        }

        // ── Copy + Paste ─────────────────────────────────────────────────────

        /// <summary>
        /// Copying text and pasting should duplicate the content. Type "hello",
        /// select all, copy, move to end, paste — should result in doubled text.
        /// </summary>
        [SkippableFact]
        public void CopyPaste_ViaCtrlCCtrlV_DuplicatesText()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("hello");
            Assert.Equal("Words: 1", _fx.GetStatusBarText("WordCountText"));

            // Select all and copy
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "a");
            Thread.Sleep(200);
            editor.SendKeys(Keys.Control + "c");
            Thread.Sleep(300);

            // Move to end of text and paste
            editor.SendKeys(Keys.Control + Keys.End);
            Thread.Sleep(100);
            editor.SendKeys(Keys.Control + "v");
            Thread.Sleep(400);

            // "hello" + pasted "hello" — verify char count increased
            string charCount = _fx.GetStatusBarText("CharCountText");
            int count = int.Parse(charCount.Replace("Characters: ", ""));
            Assert.True(count >= 10, $"Expected at least 10 characters after paste, got {count}");
        }

        // ── Undo then Redo ───────────────────────────────────────────────────

        /// <summary>
        /// After typing text and undoing, redo should restore the text.
        /// Verified through word count going back to its pre-undo value.
        /// </summary>
        [SkippableFact]
        public void UndoThenRedo_RestoresContent()
        {
            RequireDriver();
            _fx.AddFreshTab();
            try
            {
                _fx.TypeInEditor("redo test");

                string beforeUndo = _fx.GetStatusBarText("WordCountText");
                Assert.Equal("Words: 2", beforeUndo);

                // Undo
                _fx.UndoInEditor();
                Thread.Sleep(300);

                string afterUndo = _fx.GetStatusBarText("WordCountText");
                Assert.Equal("Words: 0", afterUndo);

                // Redo via Ctrl+Y
                var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
                editor.SendKeys(Keys.Control + "y");
                Thread.Sleep(300);

                string afterRedo = _fx.GetStatusBarText("WordCountText");
                Assert.Equal("Words: 2", afterRedo);
            }
            finally
            {
                _fx.CloseActiveTab();
            }
        }

        // ── Cut then Paste ───────────────────────────────────────────────────

        /// <summary>
        /// Cutting text and then pasting it back should restore the content,
        /// confirming the clipboard round-trip works correctly.
        /// </summary>
        [SkippableFact]
        public void CutThenPaste_RestoresContent()
        {
            RequireDriver();
            _fx.AddFreshTab();
            try
            {
                _fx.TypeInEditor("clipboard round trip");
                Assert.Equal("Words: 3", _fx.GetStatusBarText("WordCountText"));

                // Cut all
                var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
                editor.SendKeys(Keys.Control + "a");
                Thread.Sleep(200);
                editor.SendKeys(Keys.Control + "x");
                Thread.Sleep(300);
                Assert.Equal("Words: 0", _fx.GetStatusBarText("WordCountText"));

                // Paste back
                editor.SendKeys(Keys.Control + "v");
                Thread.Sleep(400);
                Assert.Equal("Words: 3", _fx.GetStatusBarText("WordCountText"));
            }
            finally
            {
                _fx.CloseActiveTab();
            }
        }

        // ── Multiple Undo ────────────────────────────────────────────────────

        /// <summary>
        /// Pressing Undo multiple times should progressively revert content.
        /// After clearing and typing two separate words with a pause, multiple
        /// undos should eventually reach zero words.
        /// </summary>
        [SkippableFact]
        public void MultipleUndo_ProgressivelyRevertsContent()
        {
            RequireDriver();
            _fx.ClearEditor();

            _fx.TypeInEditor("first");
            Thread.Sleep(200);
            _fx.TypeInEditor(" second");
            Thread.Sleep(200);

            Assert.Equal("Words: 2", _fx.GetStatusBarText("WordCountText"));

            // Undo multiple times until word count reaches 0
            for (int i = 0; i < 5; i++)
            {
                _fx.UndoInEditor();
                Thread.Sleep(200);
                string wordCount = _fx.GetStatusBarText("WordCountText");
                if (wordCount == "Words: 0") break;
            }

            Assert.Equal("Words: 0", _fx.GetStatusBarText("WordCountText"));
        }

        // ── Paste Special (Ctrl+Shift+V) ─────────────────────────────────────

        /// <summary>
        /// After copying bold text, Paste Special (Ctrl+Shift+V) should paste
        /// as plain text — the BoldToggle should be unchecked after paste special.
        /// </summary>
        [SkippableFact]
        public void PasteSpecial_PastesPlainText()
        {
            RequireDriver();
            _fx.ClearEditor();

            // Type text, make it bold, and copy
            _fx.TypeInEditor("bold text");
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "a");
            Thread.Sleep(200);
            editor.SendKeys(Keys.Control + "b");
            Thread.Sleep(200);

            editor.SendKeys(Keys.Control + "a");
            Thread.Sleep(200);
            editor.SendKeys(Keys.Control + "c");
            Thread.Sleep(300);

            // Clear editor and paste special
            _fx.ClearEditor();
            editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + Keys.Shift + "v");
            Thread.Sleep(400);

            // Select pasted text and verify it's not bold
            editor.SendKeys(Keys.Control + "a");
            Thread.Sleep(300);
            Assert.False(_fx.IsToggleChecked("BoldToggle"),
                "Paste Special should paste plain text without bold formatting");
        }

        // ── Delete key ───────────────────────────────────────────────────────

        /// <summary>
        /// Selecting all text and pressing Delete should clear the editor
        /// and reset word/char counts to zero.
        /// </summary>
        [SkippableFact]
        public void Delete_AfterSelectAll_RemovesAllText()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("delete me");
            Assert.Equal("Words: 2", _fx.GetStatusBarText("WordCountText"));

            _fx.SelectAllInEditor();
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Delete);
            Thread.Sleep(300);

            Assert.Equal("Words: 0", _fx.GetStatusBarText("WordCountText"));
            Assert.Equal("Characters: 0", _fx.GetStatusBarText("CharCountText"));
        }

        // ── Backspace key ────────────────────────────────────────────────────

        /// <summary>
        /// Pressing Backspace after typing a single character should reduce
        /// the character count by one.
        /// </summary>
        [SkippableFact]
        public void Backspace_AfterTyping_ReducesCharCount()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("abc");
            Assert.Equal("Characters: 3", _fx.GetStatusBarText("CharCountText"));

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Backspace);
            Thread.Sleep(300);

            Assert.Equal("Characters: 2", _fx.GetStatusBarText("CharCountText"));
        }

        // ── Edit menu Cut item via menu ──────────────────────────────────────

        /// <summary>
        /// Using the Edit → Cut menu item (not keyboard shortcut) should
        /// cut selected text and clear the editor.
        /// </summary>
        [SkippableFact]
        public void Cut_ViaEditMenu_RemovesSelectedText()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("menu cut");
            Assert.Equal("Words: 2", _fx.GetStatusBarText("WordCountText"));

            _fx.SelectAllInEditor();
            _fx.ClickMenuItem("Edit", "Cut");

            Assert.Equal("Words: 0", _fx.GetStatusBarText("WordCountText"));
        }

        // ── Edit menu Copy then Paste via menu ───────────────────────────────

        /// <summary>
        /// Using the Edit → Copy then Edit → Paste menu items should
        /// duplicate the text content.
        /// </summary>
        [SkippableFact]
        public void CopyPaste_ViaEditMenu_DuplicatesContent()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("menu");
            Assert.Equal("Characters: 4", _fx.GetStatusBarText("CharCountText"));

            _fx.SelectAllInEditor();
            _fx.ClickMenuItem("Edit", "Copy");
            Thread.Sleep(200);

            // Move to end and paste
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + Keys.End);
            Thread.Sleep(100);
            _fx.ClickMenuItem("Edit", "Paste");
            Thread.Sleep(300);

            int count = int.Parse(_fx.GetStatusBarText("CharCountText").Replace("Characters: ", ""));
            Assert.True(count >= 8, $"Expected at least 8 characters after paste, got {count}");
        }

        // ── Edit menu Select All via menu ────────────────────────────────────

        /// <summary>
        /// Using the Edit → Select All menu item should select all content,
        /// confirmed by a positive selection length in the status bar.
        /// </summary>
        [SkippableFact]
        public void SelectAll_ViaEditMenu_SelectsEntireContent()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("select via menu");

            _fx.ClickMenuItem("Edit", "Select All");
            Thread.Sleep(200);

            string selText = _fx.GetStatusBarText("SelectionLengthText");
            // "select via menu" = 15 chars + trailing \r = Sel: 16
            Assert.Equal("Sel: 16", selText);
        }

        // ── Multiple redo ────────────────────────────────────────────────────

        /// <summary>
        /// After multiple undos, the same number of redos should restore
        /// the content back to the original state.
        /// </summary>
        [SkippableFact]
        public void MultipleRedo_RestoresAllContent()
        {
            RequireDriver();
            _fx.AddFreshTab();
            try
            {
                _fx.TypeInEditor("redo multi test");
                Assert.Equal("Words: 3", _fx.GetStatusBarText("WordCountText"));

                // Undo until empty
                for (int i = 0; i < 5; i++)
                {
                    _fx.UndoInEditor();
                    Thread.Sleep(200);
                    if (_fx.GetStatusBarText("WordCountText") == "Words: 0") break;
                }
                Assert.Equal("Words: 0", _fx.GetStatusBarText("WordCountText"));

                // Redo until content restored
                var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
                for (int i = 0; i < 5; i++)
                {
                    editor.SendKeys(Keys.Control + "y");
                    Thread.Sleep(200);
                    if (_fx.GetStatusBarText("WordCountText") == "Words: 3") break;
                }
                Assert.Equal("Words: 3", _fx.GetStatusBarText("WordCountText"));
            }
            finally
            {
                _fx.CloseActiveTab();
            }
        }

        // ── Copy without selection ───────────────────────────────────────────

        /// <summary>
        /// Pressing Ctrl+C without any selection should not crash or alter
        /// the editor content.
        /// </summary>
        [SkippableFact]
        public void Copy_WithoutSelection_DoesNotCrash()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("stable text");

            // Click editor to deselect all, then copy
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.Click();
            Thread.Sleep(100);
            editor.SendKeys(Keys.Control + "c");
            Thread.Sleep(200);

            // Content should be unchanged
            Assert.Equal("Words: 2", _fx.GetStatusBarText("WordCountText"));
            Assert.Equal("Characters: 11", _fx.GetStatusBarText("CharCountText"));
        }

        // ── Paste into non-empty editor ──────────────────────────────────────

        /// <summary>
        /// Pasting text into an editor that already has content should append
        /// at the cursor position without replacing existing text.
        /// </summary>
        [SkippableFact]
        public void Paste_IntoExistingContent_AppendsAtCursor()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("first");
            Assert.Equal("Characters: 5", _fx.GetStatusBarText("CharCountText"));

            // Copy "first" to clipboard
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "a");
            Thread.Sleep(200);
            editor.SendKeys(Keys.Control + "c");
            Thread.Sleep(300);

            // Move to end and paste
            editor.SendKeys(Keys.Control + Keys.End);
            Thread.Sleep(100);
            editor.SendKeys(Keys.Control + "v");
            Thread.Sleep(400);

            // "first" + pasted "first" — verify char count increased
            int count = int.Parse(_fx.GetStatusBarText("CharCountText").Replace("Characters: ", ""));
            Assert.True(count >= 10, $"Expected at least 10 characters after paste, got {count}");
        }
    }
}
