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
            _fx.SelectAllInEditor();
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "c");
            Thread.Sleep(200);

            // Move to end and paste
            editor.SendKeys(Keys.End);
            Thread.Sleep(100);
            editor.SendKeys(Keys.Control + "v");
            Thread.Sleep(300);

            // "hello" + "hello" pasted = "hellohello" — 1 word (no space between)
            string charCount = _fx.GetStatusBarText("CharCountText");
            Assert.Equal("Characters: 10", charCount);
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
            _fx.ClearEditor();
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

        // ── Cut then Paste ───────────────────────────────────────────────────

        /// <summary>
        /// Cutting text and then pasting it back should restore the content,
        /// confirming the clipboard round-trip works correctly.
        /// </summary>
        [SkippableFact]
        public void CutThenPaste_RestoresContent()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("clipboard round trip");
            Assert.Equal("Words: 3", _fx.GetStatusBarText("WordCountText"));

            // Cut all
            _fx.SelectAllInEditor();
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "x");
            Thread.Sleep(300);
            Assert.Equal("Words: 0", _fx.GetStatusBarText("WordCountText"));

            // Paste back
            editor.SendKeys(Keys.Control + "v");
            Thread.Sleep(300);
            Assert.Equal("Words: 3", _fx.GetStatusBarText("WordCountText"));
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
            _fx.SelectAllInEditor();
            _driver!.FindElement(MobileBy.AccessibilityId("BoldToggle")).Click();
            Thread.Sleep(200);

            _fx.SelectAllInEditor();
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "c");
            Thread.Sleep(200);

            // Clear editor and paste special
            _fx.ClearEditor();
            editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + Keys.Shift + "v");
            Thread.Sleep(300);

            // Select pasted text and verify it's not bold
            _fx.SelectAllInEditor();
            Thread.Sleep(200);
            Assert.False(_fx.IsToggleChecked("BoldToggle"),
                "Paste Special should paste plain text without bold formatting");
        }
    }
}
