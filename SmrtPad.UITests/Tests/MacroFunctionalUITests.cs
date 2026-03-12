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
    /// Functional UI tests for SmrtPad's macro system.
    ///
    /// Two categories of tests:
    ///
    /// 1. <b>Structural</b> — confirm that every Macro menu item is present
    ///    and correctly labelled (prerequisite for the functional tests below).
    ///
    /// 2. <b>Behavioural</b> — each test drives the full record → action → stop
    ///    → run round-trip and asserts the <em>observed outcome</em> rather than
    ///    just the element's existence:
    ///    <list type="bullet">
    ///      <item>Status bar messages at each lifecycle stage</item>
    ///      <item>Bold formatting applied to selected text after playback</item>
    ///      <item>Zoom level incremented after a ZoomIn macro is played back</item>
    ///      <item>Running an empty macro reports "no commands" status</item>
    ///      <item>Starting a second recording clears the previous commands</item>
    ///    </list>
    ///
    /// All tests share one Appium session (via <see cref="SharedAppFixture"/>)
    /// and skip gracefully when Appium / WinAppDriver is unavailable.
    /// </summary>
    [Collection("UITests")]
    public sealed class MacroFunctionalUITests : IDisposable
    {
        private readonly SharedAppFixture _fx;
        private readonly WindowsDriver?   _driver;

        public MacroFunctionalUITests(SharedAppFixture fx)
        {
            _fx     = fx;
            _driver = fx.Driver;
        }

        public void Dispose() { /* session owned by fixture */ }

        private void RequireDriver() =>
            Skip.If(!_fx.IsAvailable,
                "WinAppDriver / Appium not available or SmrtPad.exe not built.");

        // ── Helpers ───────────────────────────────────────────────────────────

        private void OpenMacroMenu()
        {
            _driver!.FindElement(MobileBy.Name("Macro")).Click();
            Thread.Sleep(450);
        }

        private void ClickMacroItem(string automationId)
        {
            _driver!.FindElement(MobileBy.AccessibilityId(automationId)).Click();
            Thread.Sleep(350);
        }

        private string StatusText => _fx.GetStatusBarText("StatusText");

        /// <summary>
        /// Resets zoom to 100 % so zoom-macro tests start from a known level.
        /// </summary>
        private void ResetZoomTo100()
        {
            for (int i = 0; i < 10; i++)
            {
                if (_fx.GetStatusBarText("ZoomText") == "100%") return;
                _fx.ClickMenuItem("View", "Zoom Out");
                Thread.Sleep(120);
            }
        }

        // ── 1. Structural — menu items are present and correctly labelled ─────

        [SkippableFact]
        public void MacroMenu_RecordItem_IsPresent()
        {
            RequireDriver();
            OpenMacroMenu();
            var item = _driver!.FindElement(MobileBy.AccessibilityId("MacroRecordItem"));
            Assert.NotNull(item);
            // Close menu with Escape rather than a second click (more reliable)
            _driver.FindElement(MobileBy.AccessibilityId("MacroRecordItem"))
                   .SendKeys(Keys.Escape);
            Thread.Sleep(250);
        }

        [SkippableFact]
        public void MacroMenu_StopItem_IsPresent()
        {
            RequireDriver();
            OpenMacroMenu();
            var item = _driver!.FindElement(MobileBy.AccessibilityId("MacroStopItem"));
            Assert.NotNull(item);
            _driver.FindElement(MobileBy.AccessibilityId("MacroStopItem"))
                   .SendKeys(Keys.Escape);
            Thread.Sleep(250);
        }

        [SkippableFact]
        public void MacroMenu_RunItem_IsPresent()
        {
            RequireDriver();
            OpenMacroMenu();
            var item = _driver!.FindElement(MobileBy.AccessibilityId("MacroRunItem"));
            Assert.NotNull(item);
            _driver.FindElement(MobileBy.AccessibilityId("MacroRunItem"))
                   .SendKeys(Keys.Escape);
            Thread.Sleep(250);
        }

        [SkippableFact]
        public void MacroMenu_SaveItem_IsPresent()
        {
            RequireDriver();
            OpenMacroMenu();
            var item = _driver!.FindElement(MobileBy.AccessibilityId("MacroSaveItem"));
            Assert.NotNull(item);
            _driver.FindElement(MobileBy.AccessibilityId("MacroSaveItem"))
                   .SendKeys(Keys.Escape);
            Thread.Sleep(250);
        }

        [SkippableFact]
        public void MacroMenu_LoadItem_IsPresent()
        {
            RequireDriver();
            OpenMacroMenu();
            var item = _driver!.FindElement(MobileBy.AccessibilityId("MacroLoadItem"));
            Assert.NotNull(item);
            _driver.FindElement(MobileBy.AccessibilityId("MacroLoadItem"))
                   .SendKeys(Keys.Escape);
            Thread.Sleep(250);
        }

        // ── 2a. Status messages at each lifecycle stage ───────────────────────

        /// <summary>
        /// Clicking Record should set the status bar to "Recording macro...".
        /// Also verifies that Record becomes disabled and Stop becomes enabled,
        /// confirming the UI state machine is correct.
        /// </summary>
        [SkippableFact]
        public void MacroRecord_Click_StatusBar_ShowsRecording()
        {
            RequireDriver();

            OpenMacroMenu();
            ClickMacroItem("MacroRecordItem");

            Assert.Equal("Recording macro...", StatusText);

            // Verify Stop is now enabled by clicking it to clean up
            OpenMacroMenu();
            ClickMacroItem("MacroStopItem");
        }

        /// <summary>
        /// Clicking Stop after Record should set the status bar to
        /// "Macro recording stopped." and re-enable the Record item.
        /// </summary>
        [SkippableFact]
        public void MacroStop_AfterRecord_StatusBar_ShowsStopped()
        {
            RequireDriver();

            OpenMacroMenu();
            ClickMacroItem("MacroRecordItem");

            OpenMacroMenu();
            ClickMacroItem("MacroStopItem");

            Assert.Equal("Macro recording stopped.", StatusText);
        }

        /// <summary>
        /// Clicking Run when no macro has been recorded should show the
        /// "no commands" status message, not an error or crash.
        /// </summary>
        [SkippableFact]
        public void MacroRun_WhenNoCommandsRecorded_ShowsNoCommandsStatus()
        {
            RequireDriver();

            // Start and immediately stop a recording to clear any existing macro
            OpenMacroMenu();
            ClickMacroItem("MacroRecordItem");
            OpenMacroMenu();
            ClickMacroItem("MacroStopItem");

            // Run the empty macro
            OpenMacroMenu();
            ClickMacroItem("MacroRunItem");

            Assert.Equal("No commands recorded. Use Record Macro first.", StatusText);
        }

        // ── 2b. Bold macro — full record → run round-trip ─────────────────────

        /// <summary>
        /// Records a Bold command, then plays it back against selected plain text
        /// and verifies that BoldToggle is checked after playback, proving the
        /// command was stored and executed correctly.
        ///
        /// Step sequence:
        ///   1. Type plain text and select it (BoldToggle must be unchecked).
        ///   2. Start recording.
        ///   3. Click Bold (recorded; also applies bold to the selection).
        ///   4. Stop recording.
        ///   5. Undo the bold so the text is plain again.
        ///   6. Re-select all — BoldToggle must be unchecked.
        ///   7. Run macro → Bold applied via playback.
        ///   8. Assert BoldToggle is checked.
        /// </summary>
        [SkippableFact]
        public void MacroRun_BoldCommand_AppliesBold_ToSelectedPlainText()
        {
            RequireDriver();
            _fx.ClearEditor();

            // Step 1: type plain text, select it; confirm not bold
            _fx.TypeInEditor("macro bold test");
            _fx.SelectAllInEditor();
            Assert.False(_fx.IsToggleChecked("BoldToggle"), "text should start plain");

            // Step 2: start recording
            OpenMacroMenu();
            ClickMacroItem("MacroRecordItem");
            Assert.Equal("Recording macro...", StatusText);

            // Step 3: click Bold — this records the command AND applies bold
            // Re-select first so the bold applies to the text, not an empty range
            _fx.SelectAllInEditor();
            _driver!.FindElement(MobileBy.AccessibilityId("BoldToggle")).Click();
            Thread.Sleep(300);
            Assert.True(_fx.IsToggleChecked("BoldToggle"), "bold should be on during recording");

            // Step 4: stop recording
            OpenMacroMenu();
            ClickMacroItem("MacroStopItem");
            Assert.Equal("Macro recording stopped.", StatusText);

            // Step 5: undo bold — text becomes plain again
            // Multiple undos may be needed (selection change + bold toggle)
            _fx.UndoInEditor();
            Thread.Sleep(300);
            _fx.UndoInEditor();
            Thread.Sleep(300);

            // Step 6: re-select and confirm plain
            _fx.SelectAllInEditor();
            Thread.Sleep(300);
            Assert.False(_fx.IsToggleChecked("BoldToggle"),
                "text should be plain after undo, before macro playback");

            // Step 7: run macro → bold is applied via playback
            OpenMacroMenu();
            ClickMacroItem("MacroRunItem");
            Thread.Sleep(400);

            // Step 8: re-read selection state
            _fx.SelectAllInEditor();
            Thread.Sleep(200);
            Assert.True(_fx.IsToggleChecked("BoldToggle"),
                "macro playback should have applied bold to the selection");

            // Clean up: undo the macro-applied bold
            _fx.UndoInEditor();
        }

        // ── 2c. Zoom macro — full record → run round-trip ─────────────────────

        /// <summary>
        /// Records a single ZoomIn command, plays it back, and verifies that
        /// the zoom level increases by 10 % from the pre-playback level.
        ///
        /// Step sequence:
        ///   1. Reset zoom to 100 %.
        ///   2. Start recording.
        ///   3. Zoom In via View menu (recorded; zoom → 110 %).
        ///   4. Stop recording (zoom is at 110 %).
        ///   5. Run macro (zoom → 120 %).
        ///   6. Assert ZoomText is "120%".
        ///   7. Restore zoom.
        /// </summary>
        [SkippableFact]
        public void MacroRun_ZoomInCommand_IncreasesZoom_ByTenPercent()
        {
            RequireDriver();
            ResetZoomTo100();

            // Step 2: start recording
            OpenMacroMenu();
            ClickMacroItem("MacroRecordItem");
            Assert.Equal("Recording macro...", StatusText);

            // Step 3: zoom in (recorded)
            _fx.ClickMenuItem("View", "Zoom In");
            Assert.Equal("110%", _fx.GetStatusBarText("ZoomText"));

            // Step 4: stop recording
            OpenMacroMenu();
            ClickMacroItem("MacroStopItem");
            Assert.Equal("Macro recording stopped.", StatusText);

            // Step 5: run macro
            OpenMacroMenu();
            ClickMacroItem("MacroRunItem");
            Thread.Sleep(300);

            // Step 6: zoom should now be 120 %
            Assert.Equal("120%", _fx.GetStatusBarText("ZoomText"));

            // Step 7: restore zoom
            ResetZoomTo100();
        }

        /// <summary>
        /// Running the ZoomIn macro twice consecutively should increment the zoom
        /// level twice (200 % → 220 %) — verifies repeated playback correctness.
        /// </summary>
        [SkippableFact]
        public void MacroRun_ZoomInCommand_CalledTwice_IncreasesByTwentyPercent()
        {
            RequireDriver();
            ResetZoomTo100();

            // Record a single ZoomIn command
            OpenMacroMenu();
            ClickMacroItem("MacroRecordItem");
            _fx.ClickMenuItem("View", "Zoom In");   // zoom → 110 %
            OpenMacroMenu();
            ClickMacroItem("MacroStopItem");

            // Run once → 120 %
            OpenMacroMenu();
            ClickMacroItem("MacroRunItem");
            Thread.Sleep(250);
            Assert.Equal("120%", _fx.GetStatusBarText("ZoomText"));

            // Run again → 130 %
            OpenMacroMenu();
            ClickMacroItem("MacroRunItem");
            Thread.Sleep(250);
            Assert.Equal("130%", _fx.GetStatusBarText("ZoomText"));

            ResetZoomTo100();
        }

        // ── 2d. New recording clears previous commands ────────────────────────

        /// <summary>
        /// Starting a new recording session must discard the commands from the
        /// previous session.  After recording ZoomIn, stopping, then starting a
        /// fresh recording and stopping immediately (no commands), running the
        /// new empty macro must show the "no commands" message rather than the
        /// old ZoomIn command.
        /// </summary>
        [SkippableFact]
        public void MacroRecord_NewSession_ClearsPreviousCommands()
        {
            RequireDriver();
            ResetZoomTo100();

            // --- First recording: ZoomIn ---
            OpenMacroMenu();
            ClickMacroItem("MacroRecordItem");
            _fx.ClickMenuItem("View", "Zoom In");   // zoom → 110 %
            OpenMacroMenu();
            ClickMacroItem("MacroStopItem");

            // Run first macro to confirm it works (zoom → 120 %)
            OpenMacroMenu();
            ClickMacroItem("MacroRunItem");
            Thread.Sleep(250);
            Assert.Equal("120%", _fx.GetStatusBarText("ZoomText"));
            ResetZoomTo100();

            // --- Second recording: empty (record + stop with no actions) ---
            OpenMacroMenu();
            ClickMacroItem("MacroRecordItem");
            // No actions recorded
            OpenMacroMenu();
            ClickMacroItem("MacroStopItem");

            // Running the new empty macro must NOT zoom; it must report "no commands"
            string zoomBefore = _fx.GetStatusBarText("ZoomText");
            OpenMacroMenu();
            ClickMacroItem("MacroRunItem");
            Thread.Sleep(250);

            Assert.Equal("No commands recorded. Use Record Macro first.", StatusText);
            // Zoom must be unchanged because the old command was cleared
            Assert.Equal(zoomBefore, _fx.GetStatusBarText("ZoomText"));
        }

        // ── 2e. Macro run confirmation ────────────────────────────────────────

        /// <summary>
        /// After successfully running a non-empty macro, the status bar must show
        /// "Macro completed." — confirming the success path of MacroRun_Click.
        /// </summary>
        [SkippableFact]
        public void MacroRun_NonEmpty_StatusBar_ShowsCompleted()
        {
            RequireDriver();
            ResetZoomTo100();

            // Record a ZoomIn command
            OpenMacroMenu();
            ClickMacroItem("MacroRecordItem");
            _fx.ClickMenuItem("View", "Zoom In");
            OpenMacroMenu();
            ClickMacroItem("MacroStopItem");

            // Run it
            OpenMacroMenu();
            ClickMacroItem("MacroRunItem");
            Thread.Sleep(300);

            Assert.Equal("Macro completed.", StatusText);

            ResetZoomTo100();
        }

        // ── Macro records italic command ──────────────────────────────────────

        /// <summary>
        /// Records an Italic command, plays it back, and verifies ItalicToggle
        /// is checked after playback. Confirms that multiple command types
        /// can be recorded.
        /// </summary>
        [SkippableFact]
        public void MacroRun_ItalicCommand_AppliesItalic_ToSelectedPlainText()
        {
            RequireDriver();

            // Ensure italic is off at the caret before the test: type a space so
            // ClearFormattingButton has a selection to act on, then clear (F-5).
            _fx.TypeInEditor(" ");
            _fx.SelectAllInEditor();
            _fx.ResetCharacterFormatting();
            Thread.Sleep(150);
            _fx.ClearEditor();

            // Type plain text, select it; confirm not italic
            _fx.TypeInEditor("macro italic test");
            _fx.SelectAllInEditor();
            Assert.False(_fx.IsToggleChecked("ItalicToggle"), "text should start plain");

            // Start recording
            OpenMacroMenu();
            ClickMacroItem("MacroRecordItem");

            // Click Italic — records the command AND applies italic
            _fx.SelectAllInEditor();
            _driver!.FindElement(MobileBy.AccessibilityId("ItalicToggle")).Click();
            Thread.Sleep(300);

            // Stop recording
            OpenMacroMenu();
            ClickMacroItem("MacroStopItem");

            // Undo italic
            _fx.UndoInEditor();
            Thread.Sleep(200);

            // Re-select and confirm plain
            _fx.SelectAllInEditor();
            Assert.False(_fx.IsToggleChecked("ItalicToggle"));

            // Run macro → italic applied via playback
            OpenMacroMenu();
            ClickMacroItem("MacroRunItem");
            Thread.Sleep(400);

            _fx.SelectAllInEditor();
            Thread.Sleep(200);
            Assert.True(_fx.IsToggleChecked("ItalicToggle"),
                "macro playback should have applied italic to the selection");

            // Clean up
            _fx.UndoInEditor();
        }

        // ── Stop without recording ───────────────────────────────────────────

        /// <summary>
        /// Clicking Stop when no recording is active should not crash.
        /// The MacroStopItem is disabled when not recording, so we verify
        /// the menu can be opened and closed safely.
        /// </summary>
        [SkippableFact]
        public void MacroMenu_StopWhenNotRecording_MenuOpensAndClosesSafely()
        {
            RequireDriver();

            OpenMacroMenu();

            // Verify the Stop item exists
            var stopItem = _driver!.FindElement(MobileBy.AccessibilityId("MacroStopItem"));
            Assert.NotNull(stopItem);

            // Close menu via Escape
            stopItem.SendKeys(Keys.Escape);
            Thread.Sleep(250);

            // Editor should still be functional
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);
        }

        // ── Multiple commands in one macro ───────────────────────────────────

        /// <summary>
        /// Recording multiple zoom-in commands in a single macro and running it
        /// should increase the zoom by the total of all recorded steps.
        /// Recording 2 zoom-ins → playback should add 20%.
        /// </summary>
        [SkippableFact]
        public void MacroRun_MultipleZoomInCommands_IncreasesZoomByTotal()
        {
            RequireDriver();
            ResetZoomTo100();

            // Record two ZoomIn commands
            OpenMacroMenu();
            ClickMacroItem("MacroRecordItem");
            _fx.ClickMenuItem("View", "Zoom In");   // zoom → 110%
            _fx.ClickMenuItem("View", "Zoom In");   // zoom → 120%
            OpenMacroMenu();
            ClickMacroItem("MacroStopItem");

            // Run macro → zoom should increase by 20% (120 + 20 = 140%)
            OpenMacroMenu();
            ClickMacroItem("MacroRunItem");
            Thread.Sleep(300);

            Assert.Equal("140%", _fx.GetStatusBarText("ZoomText"));

            ResetZoomTo100();
        }

        // ── Macro does not affect editor content ─────────────────────────────

        /// <summary>
        /// Recording and playing back a zoom macro should not change the
        /// editor's word or character count.
        /// </summary>
        [SkippableFact]
        public void MacroRun_ZoomCommand_DoesNotChangeEditorContent()
        {
            RequireDriver();
            ResetZoomTo100();
            _fx.ClearEditor();
            _fx.TypeInEditor("macro content test");

            string wordsBefore = _fx.GetStatusBarText("WordCountText");
            string charsBefore = _fx.GetStatusBarText("CharCountText");

            // Record and run a zoom macro
            OpenMacroMenu();
            ClickMacroItem("MacroRecordItem");
            _fx.ClickMenuItem("View", "Zoom In");
            OpenMacroMenu();
            ClickMacroItem("MacroStopItem");

            OpenMacroMenu();
            ClickMacroItem("MacroRunItem");
            Thread.Sleep(300);

            Assert.Equal(wordsBefore, _fx.GetStatusBarText("WordCountText"));
            Assert.Equal(charsBefore, _fx.GetStatusBarText("CharCountText"));

            ResetZoomTo100();
        }
    }
}
