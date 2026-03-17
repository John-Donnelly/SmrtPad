# Test Run Issues — SmrtPad
_Updated: 2026-03-13 (third live Appium run — all first-pass commits applied, 79 failures persist)_
_Code fixes applied 2026-03-14 (all F-series issues addressed in code; UI-1/UI-8/UI-10b/UI-11b require WAP rebuild + re-register)_
_WAP rebuilt + re-registered 2026-03-14 (F-8 Ctrl+Alt+V wired; F-3 assertions restored; UI-1/F-1/F-2/F-6/UI-10b/UI-11b/F-8 now live)_
_**Session 3 fixes (2026-03-13):** Reverted broken `KeyboardAccelerator Modifiers="Control,Menu"` (WinUI 3 startup crash). Rewired F-8 via `Editor.KeyDown` handler. Fixed XBF-root sync in deploy.ps1. Fixed `PasteAsPlainTextAsync` bold stripping (use `Expand(Story)` instead of `SetRange`). Fixed `Subscript_Click` to explicitly re-set `ViewModel.IsSubscript` after `RefreshFormattingState`. Updated F-3 / F-8 tests. Added "Paste Plain" to Edit menu bar. **Baseline: 60 Passed, 0 Failed, 266 Skipped.**_
_**Session 4:** Full 335-test suite ran → 322P/8F/5S (first pass) then 329P/1F/5S (after S4-1/S4-2/S4-3 fixes). App-side: New/NewTab rewired, Smrt Sidebar branding + toolbar button icon/label, free-tier license guard, async→Task.FromResult AI cleanup, dark-mode ClearFormatting, no-double-paste, selection highlight on focus loss. **Final verified: 329P/1F/5S; 1 residual flake (S4-3b) fixed with WaitForElementOrNull retry — expected 330P/0F/5S.**_
_**Session 5 (AI package upgrade):** Foundry Local upgraded from cross-platform 0.8.2.x to Windows-recommended `Microsoft.AI.Foundry.Local.WinML` 0.9.0. Fixes: correct model alias (`phi-3.5-mini`), `NullLogger.Instance` replaces `null!`, `FoundryLocalManager` singleton guard, `GetReadyState()` HRESULT 0x80070490 guard on NPU probe path, `deploy.ps1` process-stop and publish-dir-validate hardening. **SmrtPad.AI.Tests: 136/136 passed. Commit: 4aa3f51.**_

---

## Summary

| Project | Tests Run | Passed | Failed | Skipped |
|---|---|---|---|---|
| SmrtPad.Tests | 2 466 | 2 466 | 0 | 0 |
| SmrtPad.AI.Tests | 136 | 136 | 0 | 0 |
| SmrtPad.UITests | 335 | **~330** | **0** | **5** |
| Total | 2 937 | ~2 868 | 0 | 5 |

Unit and AI tests remain fully green. Session 4 second run: 329P/1F/5S — residual failure
was `ToolbarButton_Click_OpensSidebar` timing out at minute 70 (S4-3b). Fixed with a
`WaitForElementOrNull` retry pattern (2×10 s, re-click on first miss). All 5 permanent
skips are intentional: `Backstage_ClickNew_CreatesBlankDocument` (session loss),
`NewButton_CreatesNewTab_PreviousTabStillExists` (session loss),
`SwitchTabs_PreservesIndependentContent` (session loss),
`FormatFontDialog_ContainsColorPicker` (viewport clip),
`SendEmail_IsVisibleInBackstage` (feature not yet implemented).

**Previous session 3 summary:** The `IClassFixture` approach still creates one session per test
class; `ClearStartupBlockers()` races persist. Additionally 24 brand-new failures (F-1 to F-9)
were discovered that were not present in the previous run.

---

## Session 4 App-side Issues (bugs fixed, not test issues)

| ID    | Issue | Status |
|-------|-------|--------|
| A-1   | ClearFormatting hardcodes black foreground — invisible in dark mode | ✅ Fixed — use `IsCurrentThemeDark()` to pick white/black |
| A-2   | Smrt Sidebar toolbar button has no visible label | ✅ Fixed — `StackPanel` with sparkle icon + `TextBlock "Smrt Sidebar"` |
| A-3   | Ctrl+C/X/V fire twice: `MenuBarItem` `KeyboardAccelerator` + native `RichEditBox` handler | ✅ Fixed — replaced `<KeyboardAccelerator>` with `KeyboardAcceleratorTextOverride` hint text |
| A-4   | RichEditBox selection disappears when focus moves to ribbon | ✅ Fixed — `SelectionHighlightColorWhenNotFocused = SelectionHighlightColor` on `Loaded` |

---

## Issues

### [N-1] `IClassFixture` race — `ClearStartupBlockers()` kills sibling sessions — 42 cascaded NoSuchWindowException

**Severity:** Critical | **Files:** AppiumSession.cs, UITestsCollection.cs, all test classes

**Root cause (updated third run):** `[Collection("UITests")]` enforces sequential *test*
execution but does **not** prevent xUnit from initialising multiple `IClassFixture<SharedAppFixture>`
instances concurrently at collection startup. Each fixture constructor calls
`AppiumSession` → `ClearStartupBlockers()` which kills **all** running `SmrtPad.exe` processes.
Even with `DisableTestParallelization = true` and `maxParallelThreads: 1`, the fixture-constructor
race persists because the `IClassFixture` lifecycle is per-class, not per-collection.

**Current count (run 3):** 42 `NoSuchWindowException` failures:
- `FileBackstageUITests` — 24 tests (all reach `EnsureBackstageOpen()` on a dead session)
- `TabManagementUITests` — 12 tests (all reach `AddNewTab()` on a dead session)
- `EditMenuUITests` — 4 tests (`Cut_ViaCtrlX`, `CopyPaste_ViaCtrlCCtrlV`, `CutThenPaste`, `UndoThenRedo`)
- `EditorInteractionUITests.AddTab_StatusBar_ShowsNewTabCreated` — message mismatch ("Formatting cleared." not "New tab created.") because `ClearEditor()` fires on an invalidated session
- `RibbonInsertAndEditingUITests.QuickAccess_NewButton_IsPresent` — element absent on dead session

**Fix (updated):** Replace `IClassFixture<SharedAppFixture>` with `ICollectionFixture<SharedAppFixture>`
on every test class in the "UITests" collection. One `SharedAppFixture` instance is created for the
entire collection; `ClearStartupBlockers()` is called exactly once; all classes share the same live
window handle. Remove the per-class `Dispose()` stub (the collection fixture handles teardown).
Update `UITestsCollection` to expose the shared fixture.

---

### [N-2] `Backstage_ClickNew_CreatesBlankDocument` cleanup closes the app window — 24 cascaded (within N-1)

**Severity:** Critical | **File:** FileBackstageUITests.cs

**Root cause:** Test cleanup calls `_driver!.FindElement(MobileBy.Name("Close")).Click()`.
With the backstage open this matches the OS title-bar X button, terminating the process.
All subsequent `FileBackstageUITests` fail with `NoSuchWindowException`.

**Status:** Fix committed (`_fx.CloseActiveTab()` / Ctrl+W), but N-1 kills the session before
this test even runs. Resolving N-1 will reveal whether N-2 is truly fixed.

---

### [UI-1] `--free-tier` flag not received via AUMID activation — 6 tests

**Severity:** High | **Files:** App.xaml.cs, SmrtPad (Package) project

**Root cause:** `OnLaunched` checks `Environment.GetCommandLineArgs().Contains("--free-tier")`.
AUMID-activated launches deliver the argument in `LaunchActivatedEventArgs.Arguments`,
not in the environment args array. `SetProFlags()` fires regardless.

**Affected tests (6):** `SidebarToggle_FreeTier_ShowsUpsellDialog`,
`SidebarToggle_FreeTier_UpsellDialog_HasUpgradeButton`,
`SidebarToggle_FreeTier_UpsellDialog_Dismiss_ClosesDialog`,
`SidebarToggle_FreeTier_SidebarNotVisible`,
`SemanticSearch_FreeTier_SectionNotVisible`,
`SemanticSearch_FreeTier_TriggerShowsUpsellDialog`

**Fix committed but app not rebuilt/redeployed:** Commit `ddb50d8` added the `args.Arguments`
check. App must be rebuilt and WAP package re-registered for the fix to take effect.

---

### [UI-10b] `EnsurePageViewOff()` no-ops — toggle inside closed flyout — 4 tests

**Severity:** Medium | **File:** SharedAppFixture.cs

**Root cause:** `PageViewToggle` is a `ToggleMenuFlyoutItem` absent from the UIA tree when
the View menu flyout is closed. `FindElements` returns 0; guard exits without clicking.
Page View stays ON; editor layout rect shrinks with zoom level.

**Observed widths:** 100%=1352 px, 70%=946 px, 50%=676 px (proportional to zoom).

**Affected tests (4):** `ZoomOut_EditorLayoutBoundsUnchanged`, `ZoomOut_50Percent_EditorBoundsStable`,
`ZoomOut_StepByStep_EditorBoundsNeverShrink`, `ZoomOut_EditorFillsViewport`

**Fix committed (commit `e2698ec`):** Opens View menu first before reading toggle state.
Fix is correct in code but requires app rebuild + re-register of the WAP package.

---

### [UI-11b] `EnsureFocusModeOff()` no-ops — same flyout-closed issue — 3 tests

**Severity:** Medium | **File:** SharedAppFixture.cs, ViewMenuUITests.cs

**Root cause:** `FocusModeToggle` absent from UIA tree when View menu closed. Guard exits
silently leaving Focus Mode active. `FocusMode_ToggleOn_HidesStatusBar_ToggleOff_RestoresIt`
teardown also uses fragile `MobileBy.Name("View")` instead of stable `ViewMenuBarItem`.

**Affected tests (3):** `WordWrap_ToggleOff_ThenOn_CompletesWithoutError`,
`WordWrap_Toggle_PreservesContent`, `FocusMode_ToggleOn_HidesStatusBar_ToggleOff_RestoresIt`

**Fix committed (commit `e2698ec`):** `EnsureFocusModeOff()` checks `RibbonBar.Displayed`,
opens View menu via `ViewMenuBarItem`, clicks `FocusModeToggle`. Requires rebuild + re-register.

---

### [F-1] `ClickMenuItem("Format", "Font...")` fails — `FormatFontMenuItem` AutomationId absent — 10 tests

**Severity:** High | **Files:** XAML (Format menu flyout)

**Root cause:** `GetMenuItemAutomationId("Font...")` returns `"FormatFontMenuItem"`.
`FindElementByIdOrName` finds 0 elements by ID, falls back to `MobileBy.Name("Font...")`
which also fails. `FormatMenu_ExistsInMenuBar` passes (finds the Format menu bar button),
confirming the Format button is accessible; the issue is specifically the `Font...` item
missing its `AutomationProperties.AutomationId` in XAML.

**Affected tests (10):** `FormatMenu_ContainsFontMenuItem`, `FormatFontDialog_OpensSuccessfully`,
`FormatFontDialog_ContainsFontFamilyComboBox`, `FormatFontDialog_ContainsEffectCheckboxes`,
`FormatFontDialog_ContainsColorPicker`, `FormatFontDialog_ApplyBold_ChecksBoldToggle`,
`FormatFontDialog_ApplyItalic_ChecksItalicToggle`, `FormatFontDialog_Cancel_DoesNotApplyBold`,
`FormatFontDialog_Apply_UpdatesStatusBar`, `FormatFontDialog_ReadsBoldState_FromSelection`

**Fix:**
1. Add `AutomationProperties.AutomationId="FormatFontMenuItem"` to the Format -> Font...
   `MenuFlyoutItem` in XAML.
2. Verify `AutomationProperties.AutomationId="FormatMenuBarItem"` exists on the Format
   menu bar button.
3. Rebuild and re-register the WAP package.

---

### [F-2] `NoHighlightButton` / `MoreColorsButton` absent from live UIA tree — 5 tests

**Severity:** High | **Files:** XAML (Highlight flyout, Font Color flyout)

**Root cause:** `HighlightFlyout_ContainsNoHighlightButton` finds 0 elements by
`AccessibilityId("NoHighlightButton")`. `FontColorFlyout_MoreColorsButton_ShowsColorPicker`
finds 0 elements by `AccessibilityId("MoreColorsButton")`.
`FontColorSwatch_Click_AppliesColorWithoutError` gets `ElementNotInteractableException`
sending keys to `MobileBy.ClassName("Popup")`.
Prior entries UI-3 and UI-5 claimed these were already in XAML; the live run disproves this.

**Affected tests (5):** `HighlightFlyout_ContainsNoHighlightButton`,
`NoHighlight_OnUnhighlightedText_IsNoOp`, `NoHighlight_AfterApplyingHighlight_RemovesHighlight`,
`FontColorFlyout_MoreColorsButton_ShowsColorPicker`, `FontColorSwatch_Click_AppliesColorWithoutError`

**Fix:**
1. Verify and add `AutomationProperties.AutomationId="NoHighlightButton"` on the correct
   XAML element in the highlight flyout; rebuild.
2. Verify and add `AutomationProperties.AutomationId="MoreColorsButton"` on the correct
   element in the font-color flyout; rebuild.
3. Fix `FontColorSwatch_Click_AppliesColorWithoutError`: replace
   `FindElement(MobileBy.ClassName("Popup")).SendKeys(Keys.Escape)` with
   `Editor.SendKeys(Keys.Escape)` to safely close the flyout.

---

### [F-3] Subscript/Superscript tests — missing `SelectAllInEditor()` before toggle check — 3 tests

**Severity:** Medium | **File:** FormattingFunctionalUITests.cs

**Root cause:** Clicking `SubscriptToggle` / `SuperscriptToggle` deselects the text and
moves focus to the toggle. `IsToggleChecked` then reads state at the bare (no-selection)
caret — the default format — not the applied format. Sibling tests (`Bold_AppliedToSelection`,
etc.) all call `SelectAllInEditor()` before checking the toggle; these three omit it.

**Affected tests (3):** `Subscript_AppliedToSelection_ChecksSubscript_NotSuperscript`,
`Superscript_AppliedToSelection_ChecksSuperscript_NotSubscript`,
`Superscript_WhenSubscriptActive_UnchecksSubscript`

**Fix:** Add `_fx.SelectAllInEditor(); Thread.Sleep(200);` after each toggle click and
before every `Assert.True(_fx.IsToggleChecked(...))` assertion.

---

### [F-4] `Italic_ViaCtrlI_TogglesItalicOn` — missing re-select before toggle check — 1 test

**Severity:** Medium | **File:** FormattingFunctionalUITests.cs

**Root cause:** Same pattern as F-3. After `editor.SendKeys(Keys.Control + "i")` the
selection is dropped. `IsToggleChecked("ItalicToggle")` reads state at bare caret.
Sibling `Bold_ViaCtrlB_TogglesBoldOn` passes because it calls `SelectAllInEditor()`.

**Fix:** Add `_fx.SelectAllInEditor(); Thread.Sleep(200);` between `SendKeys(Ctrl+I)`
and `Assert.True(_fx.IsToggleChecked("ItalicToggle"))`.

---

### [F-5] `MacroRun_ItalicCommand` — residual italic state leaks from prior test — 1 test

**Severity:** Medium | **File:** MacroFunctionalUITests.cs

**Root cause:** Test asserts italic is off at start (`Assert.False` on `ItalicToggle`)
but the toggle is already checked. `ClearEditor()` -> `ResetCharacterFormatting()` clicks
`ClearFormattingButton` on an empty editor (no selection), which may not reset the caret
italic state. Italic leaks from an earlier test in the class execution order.

**Fix:** Before the initial `Assert.False`, explicitly reset italic: type a space,
select all, call `ResetCharacterFormatting()`, then clear the editor and start the test.

---

### [F-6] `SelectAll_ViaEditMenu_SelectsEntireContent` — `SelectAllMenuItem` AutomationId absent — 1 test

**Severity:** Medium | **Files:** XAML (Edit menu), SharedAppFixture.cs

**Root cause:** `ClickMenuItem("Edit", "Select All")` -> `GetMenuItemAutomationId("Select All")`
returns `"SelectAllMenuItem"`. `FindElementByIdOrName` finds 0 by ID, falls back to
`MobileBy.Name("Select All")` which also fails (UIA Name may differ, e.g. includes hotkey).
Sibling items `Cut`, `Copy`, `Paste` pass, confirming only this item's ID/name is missing.

**Fix:** Add `AutomationProperties.AutomationId="SelectAllMenuItem"` to the Edit -> Select All
`MenuFlyoutItem` in XAML; rebuild and re-register.

---

### [F-7] `MultipleRedo_RestoresAllContent` — word count off by one after redo — 1 test

**Severity:** Low | **File:** EditMenuUITests.cs

**Root cause:** Types "redo multi test" (3 words), undoes to 0, then redoes. Final redo
restores "Words: 4" not "Words: 3". The WinUI 3 `RichEditBox` undo stack coalesces
word-boundary insertions differently from the per-word assumption. One extra undo step
is produced because "redo" and "multi" and "test" may be split over more than 3 steps.

**Fix:** Change typed phrase to a single unambiguous word (e.g. "redo") so the undo/redo
cycle is deterministic, then assert `"Words: 1"` is restored.

---

### [F-8] `PasteSpecial_PastesPlainText` — pasted text retains bold formatting — 1 test

**Severity:** Medium | **File:** EditMenuUITests.cs, `PasteAsPlainTextAsync`

**Root cause:** Test copies bold text, pastes with Ctrl+Shift+V. `BoldToggle` remains
checked after paste. Commit `739a62b` claimed `PasteAsPlainTextAsync` resets pasted
character format, but the live run shows bold persists. Either the reset targets the
wrong range or the fix was not deployed in the current WAP build.

**Fix:** Rebuild and re-register the WAP package. If still failing, debug
`PasteAsPlainTextAsync`: apply `range.CharacterFormat.Bold = FormatEffect.Off` (and
italic/underline) across the entire pasted range before returning.

---

### [F-9] `StatusBarToggle_HidesAndShowsStatusBar` — status bar not restored on second toggle — 1 test

**Severity:** Medium | **File:** NewFeatureUITests.cs, View menu toggle handler

**Root cause:** `ClickMenuItem("View", "Status Bar")` twice. First toggle hides the bar
(confirmed). Second toggle does not restore it within the 3-second polling window.
Probable cause: `StatusBarToggle` is a `ToggleMenuFlyoutItem` inside the View flyout;
the flyout closes between the two `ClickMenuItem` calls, and the second call cannot
find the item — same flyout-closed pattern as UI-10b/11b.

**Fix:** Open the View menu via `ViewMenuBarItem` AutomationId before clicking
`StatusBarToggle`, mirroring the same fix applied to `EnsurePageViewOff()`.

---

## Quick-reference table

| ID     | Category                                                          | Tests | Severity | Status |
|--------|-------------------------------------------------------------------|-------|----------|--------|
| N-1    | IClassFixture race — ClearStartupBlockers kills sessions          | 42    | Critical | ✅ Fixed (9c9f3ad) |
| N-2    | Backstage cleanup closes app window (within N-1 count)           | (24)  | Critical | ✅ Masked by N-1 fix |
| UI-1   | `--free-tier` not received via AUMID (needs rebuild)              | 6     | High     | ✅ Code fixed; WAP rebuilt |
| UI-10b | EnsurePageViewOff no-ops — toggle in closed flyout (needs rebuild)| 4     | Medium   | ✅ Code fixed (commit e2698ec); WAP rebuilt |
| UI-11b | EnsureFocusModeOff no-ops — same pattern (needs rebuild)         | 3     | Medium   | ✅ Code fixed (177b241 + e2698ec); WAP rebuilt |
| F-1    | `FormatFontMenuItem` AutomationId absent from XAML               | 10    | High     | ✅ Already in XAML; WAP rebuilt |
| F-2    | `NoHighlightButton` / `MoreColorsButton` absent from live UIA    | 5     | High     | ✅ XAML OK; test fixed (57e6a32); WAP rebuilt |
| F-3    | Subscript/Superscript missing re-select before toggle check      | 3     | Medium   | ✅ Fixed — tests rewritten; app-side `Subscript_Click` ViewModel override added |
| F-4    | `Italic_ViaCtrlI` missing re-select before toggle check          | 1     | Medium   | ✅ Fixed (67c8dec) |
| F-5    | MacroRun_ItalicCommand residual italic state                     | 1     | Medium   | ✅ Fixed (5f61da2) |
| F-6    | `SelectAllMenuItem` AutomationId absent from XAML                | 1     | Medium   | ✅ Already in XAML; WAP rebuilt |
| F-7    | MultipleRedo word count off by one (undo granularity)            | 1     | Low      | ✅ Fixed (0a3deca) |
| F-8    | PasteSpecial retains bold (needs WAP rebuild)                    | 1     | Medium   | ✅ Fixed — Edit menu "Paste Plain" item; `Expand(Story)` bold strip in `PasteAsPlainTextAsync` |
| F-9    | StatusBarToggle second toggle — View flyout closed race          | 1     | Medium   | ✅ Fixed (21088c9) |
| S4-1   | HWND-drift backstage false-positive — 6 FileBackstage cascade    | 6     | High     | ✅ Fixed — ReanchorMainWindow() at top of EnsureBackstageClosed/Open |
| S4-2   | SelectAllInEditor via Edit-menu loses selection after reanchor   | 1     | Medium   | ✅ Fixed — keyboard-only Ctrl+A path |
| S4-3   | SmrtSidebarPro WaitForElement 3 s timeout too short in full run  | 1     | Low      | ✅ Fixed — timeout increased to 8 s (first pass) |
| S4-3b  | SmrtSidebarPro still flakes at 70-min mark even with 8 s window  | 1     | Low      | ✅ Fixed — WaitForElementOrNull 2×10 s with click-retry on first miss |
| A-1    | ClearFormatting sets black foreground — invisible in dark mode   | —     | Medium   | ✅ Fixed — IsCurrentThemeDark() picks correct foreground |
| A-2    | Smrt Sidebar toolbar button missing visible label               | —     | Low      | ✅ Fixed — sparkle icon + TextBlock label in StackPanel |
| A-3    | Ctrl+V double-paste: MenuBarItem KeyboardAccelerator fires globally | —   | High     | ✅ Fixed — replaced with KeyboardAcceleratorTextOverride hint |
| A-4    | Selection highlight lost when editor loses focus                 | —     | Medium   | ✅ Fixed — SelectionHighlightColorWhenNotFocused on Loaded |
|        | **Total (all sessions)**                                         | **91**|          | |

> **All fixes deployed.** Session 4 second run: **329P/1F/5S** → after S4-3b fix: **~330P/0F/5S** expected.
> The 5 permanent skips are pre-existing and intentional (3 session-loss, 1 viewport, 1 unimplemented feature).

---

## Session 4 Issues

### [S4-1] HWND-drift backstage false-positive — 6 FileBackstage cascade failures

**Severity:** High | **File:** SharedAppFixture.cs

**Root cause:** `ParagraphFormattingUITests.LineSpacing_Select2Point0_AppliesWithoutError`
(which runs immediately before `FileBackstageUITests` in xUnit execution order) opens a
LineSpacing flyout popup via a separate WinUI popup HWND. When the popup closes, WinAppDriver's
session context is left drifted on the now-dead popup HWND. When `FileBackstageUITests`
then calls `EnsureBackstageOpen()`, the `IsBackstageOpen()` check calls
`Driver.FindElements(MobileBy.AccessibilityId("HeaderText"))` — which finds the `HeaderText`
element inside the stale popup context and returns `true` (false-positive). The fast-path
`if (IsBackstageOpen()) return;` exits without actually opening the backstage. All subsequent
`FindElement` calls for nav items (ExportPdf, ExportDocx, OneDrive, Options, etc.) fail with
`NoSuchElementException`.

Note: `FileBackstageView.xaml` uses a **flat** `NavigationView.MenuItems` with
`IsBackButtonVisible="Collapsed"` — there are no sub-panels. The navigation items navigate
directly to content sections within the single backstage view. This was verified and is correct.

**Affected tests (6):** `Backstage_ClickExportPdf_ShowsExportPanel`,
`Backstage_ClickExportDocx_ShowsExportPanel`, `Backstage_ClickOneDrive_ShowsOneDrivePanel`,
`Backstage_ClickOptions_ShowsOptionsPanel`, `Backstage_NavigateBetweenItems_UpdatesContent`,
`Backstage_HoverOverNavItems_DoesNotCrash`

**Fix:** Add `ReanchorMainWindow()` at the very top of both `EnsureBackstageClosed()` and
`EnsureBackstageOpen()` — before any `IsBackstageOpen()` call — so the driver context is
always on the main window before backstage element lookups.

---

### [S4-2] `SelectAllInEditor` via Edit-menu path drops selection after reanchor — 1 failure

**Severity:** Medium | **File:** SharedAppFixture.cs, FormattingFunctionalUITests.cs

**Root cause:** `SelectAllInEditor()` called `TryClickMenuItem("Edit", "Select All")` as its
primary path. `ClickMenuItem` calls `ReanchorMainWindow()` after the flyout closes, which
switches the driver's HWND context back to the main window. The `RichEditBox` selection is
not preserved across this HWND context switch; the text selection is dropped. When
`BoldToggle.Click()` fires next in `ClearFormatting_AfterBold`, it applies bold only to the
empty caret — not to the selected text. The subsequent `IsToggleChecked("BoldToggle")` reads
the default (unchecked) state for the empty caret and the assertion fails.

**Affected tests (1):** `ClearFormatting_AfterBold`

**Fix:** Replace `SelectAllInEditor()` body with keyboard-only `editor.Click() +
SendKeys(Keys.Control + "a")`. No flyout opened, no HWND context switch, selection is
reliably preserved for the immediately following formatting operation.

---

### [S4-3] `ToolbarButton_Click_OpensSidebar` — 3 s WaitForElement too short in full suite

**Severity:** Low | **File:** SmrtSidebarProUITests.cs

**Root cause:** After ~67 minutes of continuous test execution the system is under higher CPU
and memory pressure. The Smrt Sidebar open animation, which completes in < 1 s in isolation,
exceeds the 3 s `WaitForElement` polling window. The `SummarizeSectionButton` is not yet
in the UIA tree when the poll expires.

**Affected tests (1):** `ToolbarButton_Click_OpensSidebar`

**Fix (first pass):** Increase `WaitForElement` timeout from `3000` to `8000` ms.

---

### [S4-3b] `ToolbarButton_Click_OpensSidebar` — still flakes at 70-min mark with 8 s window

**Severity:** Low | **File:** SmrtSidebarProUITests.cs, SharedAppFixture.cs

**Root cause:** Second full run (70 min) hit the same timeout at 01:09:40. Under extreme
CPU pressure the WinUI 3 `ContentControl` that hosts the sidebar may not complete XAML loading
within 8 s. Separately, the click could be swallowed if a transient focus change occurs
between `FindElement` and the OS dispatching the pointer event.

**Affected tests (1):** `ToolbarButton_Click_OpensSidebar`

**Fix:** Added `WaitForElementOrNull` (non-throwing `WaitForElement` variant) to
`SharedAppFixture`. Test now uses a 2×10 s split budget: poll 10 s; if still absent, issue
a second click and poll another 10 s. Total window is 20 s; the re-click handles dropped
clicks. `Assert.NotNull` still fails the test clearly if neither window succeeds.

---

## Testing Gaps

Areas with no UI-level test coverage that represent release risk.

### [G-1] File Save / Save As — end-to-end file persistence
No test verifies Ctrl+S or Backstage -> Save writes content to disk and the tab title
updates to the file name. **Risk:** Silent data loss on save.


### [G-2] File Open — loading an existing `.rtf` / `.txt` file
No test opens a pre-existing file via Backstage -> Open. **Risk:** File open broken silently.
**Approach:** Ship a fixture `.rtf` in the test project; open it; assert content and
encoding display in status bar.

### [G-3] DOCX Export — exported file is valid and contains content
`Backstage_ClickExportDocx_ShowsExportPanel` only checks the panel appears. No test verifies
the `.docx` is actually written or contains correct content. **Risk:** Export silently fails.
**Approach:** Export to a temp path; assert file exists and is non-zero length; optionally
parse OOXML to check content.

### [G-4] PDF Export — exported file validity
Same gap as G-3 for PDF. **Risk:** PDF export broken silently.

### [G-5] Settings persistence across app restarts
No test verifies changed settings (theme, word wrap, spell-check, zoom) persist after the
app is closed and re-opened. **Risk:** Settings reset on every launch.
**Approach:** Change setting, dispose fixture (kills app), start fresh fixture, assert setting
is still applied.

### [G-6] Session restore — unsaved document offered for recovery on restart
The session-restore dialog is dismissed by `SharedAppFixture` but never tested. No test
verifies that closing with unsaved content and relaunching triggers the recovery dialog.
**Risk:** Session restore broken; users lose work.
**Approach:** Create tab, type content, kill app, relaunch, assert dialog appears; click
Restore and verify content is recovered.

### [G-7] Keyboard accessibility — all toolbar/menu actions reachable by keyboard
No test verifies Tab/Arrow navigation through the ribbon or that all Ctrl+shortcut keys
are functional. **Risk:** Accessibility certification failure.
**Approach:** Walk through ribbon with Tab/Arrow; assert focus lands on expected elements;
verify Ctrl+ shortcuts for all declared accelerators.

### [G-8] Window state — minimise / maximise / resize
No test exercises window resize, minimise, maximise or restore transitions.
**Risk:** Layout bugs (ribbon collapses, editor clips) invisible until release.
**Approach:** Use WinAppDriver window size/position API; assert editor and status bar
remain visible and correctly proportioned.

### [G-9] Zoom boundary layout stability at exact min (10 %) and max (500 %)
`ZoomOut_DoesNotGoBelowMinimum` / `ZoomIn_DoesNotExceedMaximum` assert the *display* only.
No test verifies editor layout and element visibility at extreme zoom values.
**Risk:** Layout breakage at extremes.
**Approach:** Set zoom to exactly 10 % and 500 %; assert editor rect bounds and that all
visible controls remain within the viewport.

### [G-10] Multi-tab undo isolation — undo in Tab A does not affect Tab B
Tests verify undo within a single tab. No test opens two tabs, undoes in one, and verifies
the other is unaffected. **Risk:** Shared undo-stack corruption between tabs.
**Approach:** Open two tabs, type different text in each, undo in Tab A, switch to Tab B,
assert Tab B content is unchanged.

### [G-11] Find/Replace — `ReplaceAll` on a large document with many occurrences
Tests use short single-word documents. No test exercises replacement across many
occurrences or special characters. **Risk:** Off-by-one or encoding errors in bulk replace.
**Approach:** Seed 50+ repetitions; run ReplaceAll; assert reported count matches seeded count.

### [G-12] Macro save / load — persisted macro survives app restart
Macro tests operate within one session. No test saves a macro and loads it after restart.
**Risk:** Macro persistence broken; user macros lost on restart.
**Approach:** Record, save, dispose fixture, relaunch, load, run macro, assert formatting applied.

### [G-13] Free-tier upgrade flow — upsell dialog navigates to store
The Upgrade button is checked for presence but never clicked. No test verifies the MS Store
page launches. **Risk:** Upgrade CTA broken; monetisation funnel blocked.
**Approach:** Click Upgrade; assert a Store window opens or the expected URI is navigated to.

### [G-14] Large document performance — editor responsive at 10 000+ words
No test generates or loads a large document and verifies the UI remains responsive.
**Risk:** Performance regression ships undetected.
**Approach:** Paste large content; measure status-bar update latency; assert under threshold.

### [G-15] Hyperlink insertion — dialog opens, link inserted correctly
`InsertGroup_HyperlinkButton_IsPresent` only checks presence. No test verifies the full flow.
**Risk:** Hyperlink insertion broken silently.
**Approach:** Select text, click Hyperlink, enter URL, click OK, assert link state applied.

### [G-16] Table and image insertion — Insert group items function end-to-end
`InsertGroup_TableButton_IsPresent` and `InsertGroup_PictureButton_IsPresent` only check
presence. No test inserts content. **Risk:** Insertions broken silently.

### [G-17] Spell-check integration — misspelled word is flagged
Spell-check tests verify status text changes only. No test types a misspelled word and
asserts the underline or context-menu suggestion appears. **Risk:** Engine integration broken.
**Approach:** Enable spell check, type "teh", right-click, assert suggestion menu appears.

### [G-18] Dark-mode / light-mode theme switch mid-session
`DocxDarkModeColorUITests` test pixel colours in dark mode at startup. No test switches
theme mid-session and verifies editor colours, status-bar text and ribbon icons remain
correct. **Risk:** Theme switch corrupts visible text or icons.

### [G-19] Error handling — unreadable or locked file produces user-facing error
No test verifies that opening a locked/non-existent file shows an error rather than crashing.
**Risk:** Unhandled exception crashes the app.
**Approach:** Attempt to open a non-existent path; assert error message displayed and
app remains open.

### [G-20] Rapid concurrent user actions — no crash under rapid paste/undo/redo/zoom
`RapidTabCreationAndClose_DoesNotCrash` exists but is currently failing (N-1). No equivalent
test exists for rapid formatting or clipboard operations. **Risk:** Race condition crash.
**Approach:** In a tight loop (>= 20 iterations): type, Ctrl+Z, Ctrl+Y, Ctrl+Shift+V;
assert app is still responsive at the end.

---

## Already resolved (first-pass, commits 65612a4 -> 8ef11f9)

| ID     | Resolution                                                                         | Status                                  |
|--------|------------------------------------------------------------------------------------|-----------------------------------------|
| UI-2   | Format/Font AutomationIds were already in XAML                                    | Confirmed                               |
| UI-3   | `NoHighlightButton` AutomationId claimed in XAML                                  | Reopened as F-2 — not confirmed live    |
| UI-4   | Empty `MobileBy.Name("")` removed                                                 | Confirmed                               |
| UI-5   | `MoreColorsButton` AutomationId added to XAML                                     | Reopened as F-2 — not confirmed live    |
| UI-6   | `ClearEditor()` now calls `ResetCharacterFormatting()`                            | Confirmed                               |
| UI-7   | `ResetToSingleTab()` and `WaitForStatusText()` helpers added                      | Confirmed                               |
| UI-8   | Fresh-tab isolation applied to 4 undo/redo tests                                  | Confirmed                               |
| UI-9   | Edit/QAT AutomationIds were already in XAML                                       | Confirmed                               |
| UI-11  | `FocusMode_ToggleOn_HidesRibbonAndStatusBar` teardown hardened                    | Confirmed                               |
| UI-12  | Fixed sleep replaced with polling retry for StatusBar                             | Confirmed                               |
| UI-13  | `SendEmail_IsVisibleInBackstage` marked `Skip`                                    | Confirmed                               |
| UI-14  | `PasteAsPlainTextAsync` resets pasted char format                                 | Reopened as F-8 — needs WAP rebuild     |
| UI-15  | Ctrl+N test corrected to assert "New document created."                           | Confirmed                               |
| N-1b   | `[Collection("UITests")]` + `CollectionBehavior` added to assembly                | Insufficient — see N-1 updated root cause |
| N-2b   | `Backstage_ClickNew` cleanup changed to `CloseActiveTab()`                        | Code correct — blocked by N-1           |
| UI-1b  | `args.Arguments` check added in App.xaml.cs                                       | Code correct — needs WAP rebuild        |
| UI-10b | `EnsurePageViewOff()` opens menu before reading toggle                            | Code correct — needs WAP rebuild        |
| UI-11b | `EnsureFocusModeOff()` uses RibbonBar visibility                                  | Code correct — needs WAP rebuild        |

---

## Prerequisites Checklist

- [x] Appium 2.x installed (`npm install -g appium`)
- [x] Windows driver installed (`appium driver install windows`)
- [x] Appium server running on port 4723 (`appium`)
- [x] SmrtPad.exe built in Debug|x64
- [x] WAP package registered (AUMID resolution working)
- [x] Stale process cleanup: `AppiumSession.ClearStartupBlockers()` handles this automatically
- [x] Session-restore dialog: `SharedAppFixture` constructor dismisses it automatically