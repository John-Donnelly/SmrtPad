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
    /// Functional UI tests that verify formatting controls produce the correct
    /// observable state changes.  Each test types a short phrase, selects it,
    /// then applies a formatting operation and asserts that the corresponding
    /// toolbar toggle reflects the new format state — confirming the full
    /// pipeline: ribbon button click → RichEditBox CharacterFormat update →
    /// SelectionChanged handler → ViewModel property → ToggleButton IsChecked.
    ///
    /// Alignment and zoom tests verify the paragraph-format and zoom-level
    /// display respectively.
    ///
    /// Tests share one Appium session via <see cref="SharedAppFixture"/>.
    /// Each test clears the editor and resets formatting to a known state
    /// before proceeding, so tests are order-independent.
    /// </summary>
    [Collection("UITests")]
    public sealed class FormattingFunctionalUITests : IClassFixture<SharedAppFixture>, IDisposable
    {
        private readonly SharedAppFixture _fx;
        private readonly WindowsDriver?   _driver;

        public FormattingFunctionalUITests(SharedAppFixture fx)
        {
            _fx     = fx;
            _driver = fx.Driver;
        }

        public void Dispose() { /* session owned by fixture */ }

        private void RequireDriver() =>
            Skip.If(!_fx.IsAvailable,
                "WinAppDriver / Appium not available or SmrtPad.exe not built.");

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Types <paramref name="phrase"/>, selects all of it, and returns so the
        /// caller can assert toggle states against the selected text.
        /// </summary>
        private void TypeAndSelectAll(string phrase)
        {
            _fx.ClearEditor();
            _fx.TypeInEditor(phrase);
            _fx.SelectAllInEditor();
        }

        /// <summary>
        /// Resets alignment back to Left after an alignment test so the next test
        /// starts from a predictable baseline.
        /// </summary>
        private void ResetAlignmentToLeft()
        {
            _driver!.FindElement(
                MobileBy.AccessibilityId("AlignLeftToggle")).Click();
            Thread.Sleep(200);
        }

        /// <summary>
        /// Resets zoom to 100 % by clicking Zoom Out in the View menu until
        /// ZoomText reads "100%".  Runs at most 10 iterations to avoid looping
        /// if something is already correct.
        /// </summary>
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

        // ── Bold ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Selecting text and clicking Bold should make BoldToggle checked,
        /// confirming the format was applied to the selection.
        /// </summary>
        [SkippableFact]
        public void Bold_AppliedToSelection_ChecksBoldToggle()
        {
            RequireDriver();
            TypeAndSelectAll("format me");

            _driver!.FindElement(MobileBy.AccessibilityId("BoldToggle")).Click();
            Thread.Sleep(300);

            Assert.True(_fx.IsToggleChecked("BoldToggle"));

            // Clean up: toggle off so next test starts with no bold
            _driver.FindElement(MobileBy.AccessibilityId("BoldToggle")).Click();
            Thread.Sleep(200);
        }

        /// <summary>
        /// Clicking Bold twice on the same selection should toggle it back off,
        /// leaving BoldToggle unchecked.
        /// </summary>
        [SkippableFact]
        public void Bold_AppliedTwice_ToSameSelection_UnchecksToggle()
        {
            RequireDriver();
            TypeAndSelectAll("format me");

            var boldBtn = _driver!.FindElement(MobileBy.AccessibilityId("BoldToggle"));
            boldBtn.Click(); Thread.Sleep(250);   // on
            boldBtn.Click(); Thread.Sleep(250);   // off

            Assert.False(_fx.IsToggleChecked("BoldToggle"));
        }

        // ── Italic ────────────────────────────────────────────────────────────

        /// <summary>
        /// Clicking Italic with text selected should check ItalicToggle.
        /// </summary>
        [SkippableFact]
        public void Italic_AppliedToSelection_ChecksItalicToggle()
        {
            RequireDriver();
            TypeAndSelectAll("italic text");

            _driver!.FindElement(MobileBy.AccessibilityId("ItalicToggle")).Click();
            Thread.Sleep(200);

            // Re-select so SelectionChanged updates toggle states
            _fx.SelectAllInEditor();
            Thread.Sleep(300);

            Assert.True(_fx.IsToggleChecked("ItalicToggle"));

            // Clean up
            _driver.FindElement(MobileBy.AccessibilityId("ItalicToggle")).Click();
            Thread.Sleep(200);
        }

        /// <summary>
        /// Clicking Italic twice reverts to non-italic: ItalicToggle is unchecked.
        /// </summary>
        [SkippableFact]
        public void Italic_AppliedTwice_ToSameSelection_UnchecksToggle()
        {
            RequireDriver();
            TypeAndSelectAll("italic text");

            var btn = _driver!.FindElement(MobileBy.AccessibilityId("ItalicToggle"));
            btn.Click(); Thread.Sleep(200);
            _fx.SelectAllInEditor(); Thread.Sleep(200);
            btn.Click(); Thread.Sleep(200);

            _fx.SelectAllInEditor();
            Thread.Sleep(200);
            Assert.False(_fx.IsToggleChecked("ItalicToggle"));
        }

        // ── Underline ─────────────────────────────────────────────────────────

        /// <summary>
        /// Clicking Underline with text selected should check UnderlineToggle.
        /// </summary>
        [SkippableFact]
        public void Underline_AppliedToSelection_ChecksUnderlineToggle()
        {
            RequireDriver();
            TypeAndSelectAll("underline me");

            _driver!.FindElement(MobileBy.AccessibilityId("UnderlineToggle")).Click();
            Thread.Sleep(200);

            _fx.SelectAllInEditor();
            Thread.Sleep(300);

            Assert.True(_fx.IsToggleChecked("UnderlineToggle"));

            // Clean up
            _driver.FindElement(MobileBy.AccessibilityId("UnderlineToggle")).Click();
            Thread.Sleep(200);
        }

        /// <summary>
        /// Clicking Underline twice reverts to no underline.
        /// </summary>
        [SkippableFact]
        public void Underline_AppliedTwice_UnchecksToggle()
        {
            RequireDriver();
            TypeAndSelectAll("underline me");

            var btn = _driver!.FindElement(MobileBy.AccessibilityId("UnderlineToggle"));
            btn.Click(); Thread.Sleep(200);
            _fx.SelectAllInEditor(); Thread.Sleep(200);
            btn.Click(); Thread.Sleep(200);

            _fx.SelectAllInEditor();
            Thread.Sleep(200);
            Assert.False(_fx.IsToggleChecked("UnderlineToggle"));
        }

        // ── Strikethrough ─────────────────────────────────────────────────────

        /// <summary>
        /// Clicking Strikethrough with text selected should check StrikethroughToggle.
        /// </summary>
        [SkippableFact]
        public void Strikethrough_AppliedToSelection_ChecksStrikethroughToggle()
        {
            RequireDriver();
            TypeAndSelectAll("strike this");

            _driver!.FindElement(MobileBy.AccessibilityId("StrikethroughToggle")).Click();
            Thread.Sleep(200);

            _fx.SelectAllInEditor();
            Thread.Sleep(300);

            Assert.True(_fx.IsToggleChecked("StrikethroughToggle"));

            // Clean up
            _driver.FindElement(MobileBy.AccessibilityId("StrikethroughToggle")).Click();
            Thread.Sleep(200);
        }

        /// <summary>
        /// Clicking Strikethrough twice removes the strikethrough.
        /// </summary>
        [SkippableFact]
        public void Strikethrough_AppliedTwice_UnchecksToggle()
        {
            RequireDriver();
            TypeAndSelectAll("strike this");

            var btn = _driver!.FindElement(MobileBy.AccessibilityId("StrikethroughToggle"));
            btn.Click(); Thread.Sleep(200);
            _fx.SelectAllInEditor(); Thread.Sleep(200);
            btn.Click(); Thread.Sleep(200);

            _fx.SelectAllInEditor();
            Thread.Sleep(200);
            Assert.False(_fx.IsToggleChecked("StrikethroughToggle"));
        }

        // ── Subscript ─────────────────────────────────────────────────────────

        /// <summary>
        /// Subscript should check SubscriptToggle and must not simultaneously
        /// check SuperscriptToggle (they are mutually exclusive).
        /// </summary>
        [SkippableFact]
        public void Subscript_AppliedToSelection_ChecksSubscript_NotSuperscript()
        {
            RequireDriver();
            TypeAndSelectAll("H2O");

            _driver!.FindElement(MobileBy.AccessibilityId("SubscriptToggle")).Click();
            Thread.Sleep(300);

            Assert.True(_fx.IsToggleChecked("SubscriptToggle"));
            Assert.False(_fx.IsToggleChecked("SuperscriptToggle"));

            // Clean up
            _driver.FindElement(MobileBy.AccessibilityId("SubscriptToggle")).Click();
            Thread.Sleep(200);
        }

        // ── Superscript ───────────────────────────────────────────────────────

        /// <summary>
        /// Superscript should check SuperscriptToggle and must not simultaneously
        /// check SubscriptToggle.
        /// </summary>
        [SkippableFact]
        public void Superscript_AppliedToSelection_ChecksSuperscript_NotSubscript()
        {
            RequireDriver();
            TypeAndSelectAll("x2");

            _driver!.FindElement(MobileBy.AccessibilityId("SuperscriptToggle")).Click();
            Thread.Sleep(300);

            Assert.True(_fx.IsToggleChecked("SuperscriptToggle"));
            Assert.False(_fx.IsToggleChecked("SubscriptToggle"));

            // Clean up
            _driver.FindElement(MobileBy.AccessibilityId("SuperscriptToggle")).Click();
            Thread.Sleep(200);
        }

        // ── Subscript / Superscript mutual exclusion ──────────────────────────

        /// <summary>
        /// Applying superscript when subscript is active must uncheck subscript.
        /// The code-behind enforces: Superscript = On → Subscript = Off.
        /// </summary>
        [SkippableFact]
        public void Superscript_WhenSubscriptActive_UnchecksSubscript()
        {
            RequireDriver();
            TypeAndSelectAll("test");

            // Apply subscript first
            _driver!.FindElement(MobileBy.AccessibilityId("SubscriptToggle")).Click();
            Thread.Sleep(250);
            Assert.True(_fx.IsToggleChecked("SubscriptToggle"));

            // Re-select (clicking SubscriptToggle deselects)
            _fx.SelectAllInEditor();
            Thread.Sleep(150);

            // Apply superscript — should turn off subscript
            _driver.FindElement(MobileBy.AccessibilityId("SuperscriptToggle")).Click();
            Thread.Sleep(300);

            Assert.True(_fx.IsToggleChecked("SuperscriptToggle"));
            Assert.False(_fx.IsToggleChecked("SubscriptToggle"));

            // Clean up
            _driver.FindElement(MobileBy.AccessibilityId("SuperscriptToggle")).Click();
            Thread.Sleep(200);
        }

        // ── Alignment ─────────────────────────────────────────────────────────

        /// <summary>
        /// Clicking AlignCenter should check AlignCenterToggle and uncheck
        /// AlignLeftToggle (the default), confirming mutual exclusion.
        /// </summary>
        [SkippableFact]
        public void AlignCenter_ChecksAlignCenter_AndUnchecks_AlignLeft()
        {
            RequireDriver();
            _fx.ClearEditor();

            _driver!.FindElement(MobileBy.AccessibilityId("AlignCenterToggle")).Click();
            Thread.Sleep(300);

            Assert.True(_fx.IsToggleChecked("AlignCenterToggle"));
            Assert.False(_fx.IsToggleChecked("AlignLeftToggle"));

            ResetAlignmentToLeft();
        }

        /// <summary>
        /// Clicking AlignRight should check AlignRightToggle and uncheck
        /// AlignCenterToggle if previously set.
        /// </summary>
        [SkippableFact]
        public void AlignRight_ChecksAlignRight_AndUnchecks_AlignCenter()
        {
            RequireDriver();
            _fx.ClearEditor();

            // First set center to confirm it changes
            _driver!.FindElement(MobileBy.AccessibilityId("AlignCenterToggle")).Click();
            Thread.Sleep(250);

            // Now set right
            _driver.FindElement(MobileBy.AccessibilityId("AlignRightToggle")).Click();
            Thread.Sleep(300);

            Assert.True(_fx.IsToggleChecked("AlignRightToggle"));
            Assert.False(_fx.IsToggleChecked("AlignCenterToggle"));

            ResetAlignmentToLeft();
        }

        /// <summary>
        /// Clicking AlignJustify should check AlignJustifyToggle.
        /// </summary>
        [SkippableFact]
        public void AlignJustify_ChecksAlignJustifyToggle()
        {
            RequireDriver();
            _fx.ClearEditor();

            _driver!.FindElement(MobileBy.AccessibilityId("AlignJustifyToggle")).Click();
            Thread.Sleep(300);

            Assert.True(_fx.IsToggleChecked("AlignJustifyToggle"));

            ResetAlignmentToLeft();
        }

        /// <summary>
        /// Clicking AlignLeft after setting another alignment should re-check
        /// AlignLeftToggle and uncheck the previously active alignment.
        /// </summary>
        [SkippableFact]
        public void AlignLeft_AfterCenter_RestoresAlignLeftToggle()
        {
            RequireDriver();
            _fx.ClearEditor();

            _driver!.FindElement(MobileBy.AccessibilityId("AlignCenterToggle")).Click();
            Thread.Sleep(250);
            Assert.True(_fx.IsToggleChecked("AlignCenterToggle"));

            _driver.FindElement(MobileBy.AccessibilityId("AlignLeftToggle")).Click();
            Thread.Sleep(300);

            Assert.True(_fx.IsToggleChecked("AlignLeftToggle"));
            Assert.False(_fx.IsToggleChecked("AlignCenterToggle"));
        }

        /// <summary>
        /// Only one alignment toggle should be checked at a time.
        /// After setting each of the four alignments in sequence, exactly one
        /// is checked and the rest are unchecked.
        /// </summary>
        [SkippableFact]
        public void AlignmentToggles_AreMutuallyExclusive_ForAllFour()
        {
            RequireDriver();
            _fx.ClearEditor();

            void AssertOnlyOneAlignChecked(string expected)
            {
                string[] all = ["AlignLeftToggle", "AlignCenterToggle",
                                 "AlignRightToggle", "AlignJustifyToggle"];
                foreach (string id in all)
                {
                    bool expectedState = id == expected;
                    Assert.Equal(expectedState, _fx.IsToggleChecked(id));
                }
            }

            _driver!.FindElement(MobileBy.AccessibilityId("AlignCenterToggle")).Click();
            Thread.Sleep(250);
            AssertOnlyOneAlignChecked("AlignCenterToggle");

            _driver.FindElement(MobileBy.AccessibilityId("AlignRightToggle")).Click();
            Thread.Sleep(250);
            AssertOnlyOneAlignChecked("AlignRightToggle");

            _driver.FindElement(MobileBy.AccessibilityId("AlignJustifyToggle")).Click();
            Thread.Sleep(250);
            AssertOnlyOneAlignChecked("AlignJustifyToggle");

            ResetAlignmentToLeft();
            AssertOnlyOneAlignChecked("AlignLeftToggle");
        }

        // ── Zoom ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Clicking View → Zoom In should increment the zoom level by 10 %,
        /// updating ZoomText from "100%" to "110%".
        /// </summary>
        [SkippableFact]
        public void ZoomIn_ViaViewMenu_UpdatesZoomDisplayTo110()
        {
            RequireDriver();
            ResetZoomTo100();

            Assert.Equal("100%", _fx.GetStatusBarText("ZoomText"));

            _fx.ClickMenuItem("View", "Zoom In");

            Assert.Equal("110%", _fx.GetStatusBarText("ZoomText"));

            // Restore
            _fx.ClickMenuItem("View", "Zoom Out");
        }

        /// <summary>
        /// Clicking View → Zoom Out should decrement the zoom level by 10 %,
        /// updating ZoomText from "100%" to "90%".
        /// </summary>
        [SkippableFact]
        public void ZoomOut_ViaViewMenu_UpdatesZoomDisplayTo90()
        {
            RequireDriver();
            ResetZoomTo100();

            Assert.Equal("100%", _fx.GetStatusBarText("ZoomText"));

            _fx.ClickMenuItem("View", "Zoom Out");

            Assert.Equal("90%", _fx.GetStatusBarText("ZoomText"));

            // Restore
            _fx.ClickMenuItem("View", "Zoom In");
        }

        /// <summary>
        /// Zooming in twice and then out twice must return to exactly 100 %,
        /// confirming that each step is exactly ±10 %.
        /// </summary>
        [SkippableFact]
        public void ZoomIn_ThenZoomOut_RoundTrip_RestoresToOriginal()
        {
            RequireDriver();
            ResetZoomTo100();

            _fx.ClickMenuItem("View", "Zoom In");
            _fx.ClickMenuItem("View", "Zoom In");
            Assert.Equal("120%", _fx.GetStatusBarText("ZoomText"));

            _fx.ClickMenuItem("View", "Zoom Out");
            _fx.ClickMenuItem("View", "Zoom Out");
            Assert.Equal("100%", _fx.GetStatusBarText("ZoomText"));
        }

        /// <summary>
        /// Zoom cannot exceed 500 %.  Attempting to zoom in from 500 % should
        /// keep ZoomText at "500%" and not crash.
        /// </summary>
        [SkippableFact]
        public void ZoomIn_AtMaximum_CapsAt500()
        {
            RequireDriver();
            ResetZoomTo100();

            // Drive zoom to 500 % (40 × +10 %)
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            for (int i = 0; i < 40; i++)
            {
                editor.SendKeys(Keys.Control + Keys.Add + Keys.Null);
                Thread.Sleep(50);
            }

            Assert.Equal("500%", _fx.GetStatusBarText("ZoomText"));

            // One more zoom-in must not go above 500 %
            editor.SendKeys(Keys.Control + Keys.Add + Keys.Null);
            Thread.Sleep(100);
            Assert.Equal("500%", _fx.GetStatusBarText("ZoomText"));

            // Restore
            ResetZoomTo100();
        }

        /// <summary>
        /// Zoom cannot go below 10 %.  Attempting to zoom out from 10 % should
        /// keep ZoomText at "10%" and not crash.
        /// </summary>
        [SkippableFact]
        public void ZoomOut_AtMinimum_FloorAt10()
        {
            RequireDriver();
            ResetZoomTo100();

            // Drive zoom to 10 % (9 × -10 %)
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            for (int i = 0; i < 9; i++)
            {
                editor.SendKeys(Keys.Control + Keys.Subtract + Keys.Null);
                Thread.Sleep(50);
            }

            Assert.Equal("10%", _fx.GetStatusBarText("ZoomText"));

            // One more zoom-out must not go below 10 %
            editor.SendKeys(Keys.Control + Keys.Subtract + Keys.Null);
            Thread.Sleep(100);
            Assert.Equal("10%", _fx.GetStatusBarText("ZoomText"));

            // Restore
            ResetZoomTo100();
        }

        // ── Spell check toggle ────────────────────────────────────────────────

        /// <summary>
        /// Toggling Spell Check off and back on in the View menu should update
        /// the SpellCheckToggle's checked state and the status bar message.
        /// </summary>
        [SkippableFact]
        public void SpellCheck_Toggle_Off_Then_On_ChangesStateAndStatus()
        {
            RequireDriver();

            // Open View menu and click SpellCheck to turn it off
            _driver!.FindElement(MobileBy.Name("View")).Click();
            Thread.Sleep(450);
            var toggle = _driver.FindElement(MobileBy.AccessibilityId("SpellCheckToggle"));

            // Record current state; the menu must be open to read it
            bool wasChecked = toggle.GetAttribute("Toggle.ToggleState") == "1";
            toggle.Click();   // toggle off (or on, depending on prior state)
            Thread.Sleep(400);

            // Read the status bar
            string status = _fx.GetStatusBarText("StatusText");
            bool nowExpectedDisabled = wasChecked;
            if (nowExpectedDisabled)
                Assert.Equal("Spell check disabled.", status);
            else
                Assert.Equal("Spell check enabled.", status);

            // Restore original state
            _driver.FindElement(MobileBy.Name("View")).Click();
            Thread.Sleep(450);
            _driver.FindElement(MobileBy.AccessibilityId("SpellCheckToggle")).Click();
            Thread.Sleep(400);
        }

        // ── Bold via keyboard shortcut ────────────────────────────────────────

        /// <summary>
        /// Pressing Ctrl+B with text selected should toggle Bold on,
        /// confirming the keyboard accelerator works end-to-end.
        /// </summary>
        [SkippableFact]
        public void Bold_ViaCtrlB_TogglesBoldOn()
        {
            RequireDriver();
            TypeAndSelectAll("shortcut bold");

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "b");
            Thread.Sleep(200);

            _fx.SelectAllInEditor();
            Thread.Sleep(300);

            Assert.True(_fx.IsToggleChecked("BoldToggle"));

            // Clean up: toggle off
            _fx.SelectAllInEditor();
            editor.SendKeys(Keys.Control + "b");
            Thread.Sleep(200);
        }

        // ── Italic via keyboard shortcut ──────────────────────────────────────

        /// <summary>
        /// Pressing Ctrl+I with text selected should toggle Italic on.
        /// </summary>
        [SkippableFact]
        public void Italic_ViaCtrlI_TogglesItalicOn()
        {
            RequireDriver();
            TypeAndSelectAll("shortcut italic");

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "i");
            Thread.Sleep(300);

            Assert.True(_fx.IsToggleChecked("ItalicToggle"));

            // Clean up
            _fx.SelectAllInEditor();
            editor.SendKeys(Keys.Control + "i");
            Thread.Sleep(200);
        }

        // ── Underline via keyboard shortcut ───────────────────────────────────

        /// <summary>
        /// Pressing Ctrl+U with text selected should toggle Underline on.
        /// </summary>
        [SkippableFact]
        public void Underline_ViaCtrlU_TogglesUnderlineOn()
        {
            RequireDriver();
            TypeAndSelectAll("shortcut underline");

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "u");
            Thread.Sleep(200);

            _fx.SelectAllInEditor();
            Thread.Sleep(300);

            Assert.True(_fx.IsToggleChecked("UnderlineToggle"));

            // Clean up
            _fx.SelectAllInEditor();
            editor.SendKeys(Keys.Control + "u");
            Thread.Sleep(200);
        }

        // ── Multiple formatting combinations ──────────────────────────────────

        /// <summary>
        /// Applying Bold + Italic simultaneously should check both toggles.
        /// </summary>
        [SkippableFact]
        public void BoldAndItalic_AppliedTogether_BothTogglesChecked()
        {
            RequireDriver();
            TypeAndSelectAll("bold italic combo");

            _driver!.FindElement(MobileBy.AccessibilityId("BoldToggle")).Click();
            Thread.Sleep(200);

            _fx.SelectAllInEditor();
            Thread.Sleep(200);

            _driver!.FindElement(MobileBy.AccessibilityId("ItalicToggle")).Click();
            Thread.Sleep(200);

            // Re-select and verify both
            _fx.SelectAllInEditor();
            Thread.Sleep(300);

            Assert.True(_fx.IsToggleChecked("BoldToggle"));
            Assert.True(_fx.IsToggleChecked("ItalicToggle"));

            // Clean up
            _fx.SelectAllInEditor();
            _driver!.FindElement(MobileBy.AccessibilityId("BoldToggle")).Click();
            Thread.Sleep(150);
            _fx.SelectAllInEditor();
            _driver!.FindElement(MobileBy.AccessibilityId("ItalicToggle")).Click();
            Thread.Sleep(150);
        }

        // ── Clear formatting resets italic ────────────────────────────────────

        /// <summary>
        /// Applying Italic formatting and then clicking Clear Formatting should
        /// reset the ItalicToggle to unchecked.
        /// </summary>
        [SkippableFact]
        public void ClearFormatting_AfterItalic_ResetsItalicToggle()
        {
            RequireDriver();
            TypeAndSelectAll("clear italic test");

            _driver!.FindElement(MobileBy.AccessibilityId("ItalicToggle")).Click();
            Thread.Sleep(200);

            _fx.SelectAllInEditor();
            Thread.Sleep(200);
            Assert.True(_fx.IsToggleChecked("ItalicToggle"));

            _fx.SelectAllInEditor();
            _driver!.FindElement(MobileBy.AccessibilityId("ClearFormattingButton")).Click();
            Thread.Sleep(300);

            _fx.SelectAllInEditor();
            Thread.Sleep(300);
            Assert.False(_fx.IsToggleChecked("ItalicToggle"),
                "Clear Formatting should reset italic to off");
        }

        // ── Clear formatting resets underline ─────────────────────────────────

        /// <summary>
        /// Applying Underline formatting and then clicking Clear Formatting
        /// should reset the UnderlineToggle to unchecked.
        /// </summary>
        [SkippableFact]
        public void ClearFormatting_AfterUnderline_ResetsUnderlineToggle()
        {
            RequireDriver();
            TypeAndSelectAll("clear underline test");

            _driver!.FindElement(MobileBy.AccessibilityId("UnderlineToggle")).Click();
            Thread.Sleep(200);

            _fx.SelectAllInEditor();
            Thread.Sleep(200);
            Assert.True(_fx.IsToggleChecked("UnderlineToggle"));

            _fx.SelectAllInEditor();
            _driver!.FindElement(MobileBy.AccessibilityId("ClearFormattingButton")).Click();
            Thread.Sleep(300);

            _fx.SelectAllInEditor();
            Thread.Sleep(300);
            Assert.False(_fx.IsToggleChecked("UnderlineToggle"),
                "Clear Formatting should reset underline to off");
        }

        // ── Clear formatting resets all formats ───────────────────────────────

        /// <summary>
        /// Applying Bold, Italic, and Strikethrough then Clear Formatting should
        /// reset all three toggles to unchecked.
        /// </summary>
        [SkippableFact]
        public void ClearFormatting_AfterMultipleFormats_ResetsAll()
        {
            RequireDriver();
            TypeAndSelectAll("clear all test");

            _driver!.FindElement(MobileBy.AccessibilityId("BoldToggle")).Click();
            Thread.Sleep(150);
            _fx.SelectAllInEditor();
            Thread.Sleep(150);
            _driver!.FindElement(MobileBy.AccessibilityId("ItalicToggle")).Click();
            Thread.Sleep(150);
            _fx.SelectAllInEditor();
            Thread.Sleep(150);
            _driver!.FindElement(MobileBy.AccessibilityId("StrikethroughToggle")).Click();
            Thread.Sleep(200);

            _fx.SelectAllInEditor();
            Thread.Sleep(150);
            _driver!.FindElement(MobileBy.Name("Clear Formatting")).Click();
            Thread.Sleep(300);

            _fx.SelectAllInEditor();
            Thread.Sleep(300);
            Assert.False(_fx.IsToggleChecked("BoldToggle"));
            Assert.False(_fx.IsToggleChecked("ItalicToggle"));
            Assert.False(_fx.IsToggleChecked("StrikethroughToggle"));
        }

        // ── Formatting preserves word count ───────────────────────────────────

        /// <summary>
        /// Applying Bold formatting should not change the word or character count.
        /// </summary>
        [SkippableFact]
        public void Bold_DoesNotChangeWordOrCharCount()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("count test");

            string wordsBefore = _fx.GetStatusBarText("WordCountText");
            string charsBefore = _fx.GetStatusBarText("CharCountText");

            _fx.SelectAllInEditor();
            _driver!.FindElement(MobileBy.AccessibilityId("BoldToggle")).Click();
            Thread.Sleep(200);

            Assert.Equal(wordsBefore, _fx.GetStatusBarText("WordCountText"));
            Assert.Equal(charsBefore, _fx.GetStatusBarText("CharCountText"));

            // Clean up
            _fx.SelectAllInEditor();
            _driver!.FindElement(MobileBy.AccessibilityId("BoldToggle")).Click();
            Thread.Sleep(200);
        }
    }
}
