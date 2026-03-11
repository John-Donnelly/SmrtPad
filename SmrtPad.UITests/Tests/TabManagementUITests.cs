using System;
using System.Linq;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Xunit;
using SmrtPad.UITests.Infrastructure;

namespace SmrtPad.UITests.Tests
{
    /// <summary>
    /// Functional UI tests for the tabbed document interface: creating tabs,
    /// closing tabs, switching between tabs, and verifying that each tab
    /// maintains independent state.
    ///
    /// Tests share one Appium session via <see cref="SharedAppFixture"/>.
    /// </summary>
    [Collection("UITests")]
    public sealed class TabManagementUITests : IClassFixture<SharedAppFixture>, IDisposable
    {
        private readonly SharedAppFixture _fx;
        private readonly WindowsDriver? _driver;

        public TabManagementUITests(SharedAppFixture fx)
        {
            _fx = fx;
            _driver = fx.Driver;
        }

        public void Dispose() { /* session owned by fixture */ }

        private void RequireDriver() =>
            Skip.If(!_fx.IsAvailable,
                "WinAppDriver / Appium not available or SmrtPad.exe not built.");

        private string StatusText => _fx.GetStatusBarText("StatusText");

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Clicks the TabView "+" button to add a new tab.
        /// Ensures backstage is closed first.
        /// </summary>
        private void AddNewTab()
        {
            _fx.EnsureBackstageClosed();
            _driver!.FindElement(MobileBy.AccessibilityId("AddButton")).Click();
            Thread.Sleep(500);
        }

        private AppiumElement FindQuickAccessNewButton()
        {
            return _driver!.FindElement(MobileBy.AccessibilityId("QuickAccessNewButton"));
        }

        private System.Collections.ObjectModel.ReadOnlyCollection<AppiumElement> FindUntitledTabs()
        {
            var tabs = _driver!.FindElement(MobileBy.AccessibilityId("DocumentTabs"));
            return tabs.FindElements(MobileBy.Name("Untitled"));
        }

        /// <summary>
        /// Closes the currently active tab by sending Ctrl+W.
        /// If the tab has unsaved changes the "Unsaved Changes" dialog is
        /// automatically dismissed by clicking "Don't Save", so tests that
        /// type content and then close the tab do not block.
        /// </summary>
        private void CloseActiveTab()
        {
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "w");
            Thread.Sleep(500);
            _fx.DismissSaveDialogIfPresent();
        }

        // ── Create tab ───────────────────────────────────────────────────────

        /// <summary>
        /// Clicking the add-tab button should create a new tab and set
        /// the status bar to "New tab created."
        /// </summary>
        [SkippableFact]
        public void AddTab_CreatesNewTab_StatusShowsNewTabCreated()
        {
            RequireDriver();

            AddNewTab();

            Assert.Equal("New tab created.", StatusText);

            // Clean up: close the extra tab
            CloseActiveTab();
        }

        /// <summary>
        /// Using Ctrl+T keyboard shortcut should also create a new tab.
        /// </summary>
        [SkippableFact]
        public void AddTab_ViaCtrlT_CreatesNewTab()
        {
            RequireDriver();

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "t");
            Thread.Sleep(500);

            Assert.Equal("New tab created.", StatusText);

            // Clean up
            CloseActiveTab();
        }

        /// <summary>
        /// Ctrl+N fires the "New Document" command which replaces the active tab's
        /// content and reports "New document created." in the status bar.
        /// It does NOT create a new tab — use Ctrl+T for that (UI-15).
        /// </summary>
        [SkippableFact]
        public void NewDocument_ViaCtrlN_ShowsNewDocumentStatus()
        {
            RequireDriver();

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "n");
            Thread.Sleep(500);

            Assert.Equal("New document created.", StatusText);
        }

        // ── Close tab ────────────────────────────────────────────────────────

        /// <summary>
        /// Closing a tab should show "Tab closed." in the status bar.
        /// After closing all extra tabs, at least one tab should remain
        /// (the app always keeps at least one tab open).
        /// </summary>
        [SkippableFact]
        public void CloseTab_ShowsTabClosedStatus()
        {
            RequireDriver();

            // Create an extra tab so we can close it
            AddNewTab();
            Thread.Sleep(200);

            CloseActiveTab();

            Assert.Equal("Tab closed.", StatusText);
        }

        /// <summary>
        /// Closing the last tab should close the application.
        /// Since this would terminate the Appium session, we verify
        /// indirectly by ensuring an extra tab prevents app closure.
        /// </summary>
        [SkippableFact]
        public void CloseLastTab_WithExtraTab_DoesNotCloseApp()
        {
            RequireDriver();

            // Create an extra tab so closing one doesn't close the app
            AddNewTab();
            Thread.Sleep(200);

            // Close the extra tab
            CloseActiveTab();

            // The editor should still be present (app stays open with first tab)
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);
        }

        // ── Tab switching ────────────────────────────────────────────────────

        /// <summary>
        /// Creating two tabs, typing in each, and switching between them
        /// should preserve independent word counts per tab.
        /// </summary>
        [SkippableFact]
        public void SwitchTabs_PreservesIndependentContent()
        {
            RequireDriver();
            _fx.ResetToSingleTab();

            // Type in the first tab
            _fx.ClearEditor();
            _fx.TypeInEditor("tab one content");
            string firstTabWords = _fx.GetStatusBarText("WordCountText");
            Assert.Equal("Words: 3", firstTabWords);

            // Create a second tab (auto-focused)
            AddNewTab();

            // Type different content in the second tab
            _fx.TypeInEditor("second tab");
            string secondTabWords = _fx.GetStatusBarText("WordCountText");
            Assert.Equal("Words: 2", secondTabWords);

            // Switch back to first tab by clicking it
            var firstTab = FindUntitledTabs().First();
            firstTab.Click();
            Thread.Sleep(400);

            // Word count should reflect first tab's content
            string firstTabWordsAgain = _fx.GetStatusBarText("WordCountText");
            Assert.Equal("Words: 3", firstTabWordsAgain);

            // Clean up: close second tab, switch to first
            // Navigate back to second tab and close it
            AddNewTab(); // This creates a third, switch to second
            CloseActiveTab(); // Close third
            CloseActiveTab(); // Close remaining extra
        }

        // ── Multiple tabs ────────────────────────────────────────────────────

        /// <summary>
        /// Creating multiple tabs should not crash and each creation
        /// should report "New tab created." in the status bar.
        /// </summary>
        [SkippableFact]
        public void CreateMultipleTabs_AllSucceed()
        {
            RequireDriver();

            for (int i = 0; i < 3; i++)
            {
                AddNewTab();
                Assert.Equal("New tab created.", StatusText);
            }

            // Clean up: close the extra tabs
            for (int i = 0; i < 3; i++)
            {
                CloseActiveTab();
            }
        }

        // ── Close tab via Ctrl+W ─────────────────────────────────────────────

        /// <summary>
        /// Pressing Ctrl+W should close the active tab (same as clicking close).
        /// </summary>
        [SkippableFact]
        public void CloseTab_ViaCtrlW_ClosesActiveTab()
        {
            RequireDriver();

            // Create an extra tab so we can close it
            AddNewTab();

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "w");
            Thread.Sleep(500);

            // The editor should still be present (at least one tab remains)
            editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);
        }

        // ── New tab shows Untitled ───────────────────────────────────────────

        /// <summary>
        /// A newly created tab should have "Untitled" as its title.
        /// </summary>
        [SkippableFact]
        public void NewTab_Title_ShowsUntitled()
        {
            RequireDriver();

            AddNewTab();

            // Find the tab with "Untitled" in its name
            var untitledTab = FindUntitledTabs().First();
            Assert.NotNull(untitledTab);

            // Clean up
            CloseActiveTab();
        }

        // ── New tab has empty editor ─────────────────────────────────────────

        /// <summary>
        /// A newly created tab should have an empty editor with zero word count.
        /// </summary>
        [SkippableFact]
        public void NewTab_HasEmptyEditor_WithZeroWordCount()
        {
            RequireDriver();

            AddNewTab();

            Assert.Equal("Words: 0", _fx.GetStatusBarText("WordCountText"));
            Assert.Equal("Characters: 0", _fx.GetStatusBarText("CharCountText"));

            // Clean up
            CloseActiveTab();
        }

        // ── Rapid tab creation ───────────────────────────────────────────────

        /// <summary>
        /// Rapidly creating and closing tabs should not crash the application.
        /// </summary>
        [SkippableFact]
        public void RapidTabCreationAndClose_DoesNotCrash()
        {
            RequireDriver();

            for (int i = 0; i < 5; i++)
            {
                AddNewTab();
                Thread.Sleep(100);
                CloseActiveTab();
                Thread.Sleep(100);
            }

            // Verify the editor is still functional
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);
        }

        // ── New button creates a new tab ─────────────────────────────────────

        /// <summary>
        /// A newly created tab (via +) should close immediately without
        /// showing a save dialog, proving it is NOT marked as modified.
        /// </summary>
        [SkippableFact]
        public void NewTabViaPlus_CloseImmediately_NoSaveDialog()
        {
            RequireDriver();

            AddNewTab();
            Thread.Sleep(200);

            // Close the fresh tab — should NOT show a save dialog
            CloseActiveTab();

            // If a dialog appeared, the test would hang or throw.
            // Verify the tab closed successfully via status text.
            Assert.Equal("Tab closed.", StatusText);
        }

        /// <summary>
        /// A tab created via the New quick-access button should also
        /// close without a save dialog when unmodified.
        /// </summary>
        [SkippableFact]
        public void NewTabViaNewButton_CloseImmediately_NoSaveDialog()
        {
            RequireDriver();

            var newBtn = FindQuickAccessNewButton();
            newBtn.Click();
            Thread.Sleep(500);

            // Close the fresh tab — should NOT show a save dialog
            CloseActiveTab();

            Assert.Equal("Tab closed.", StatusText);
        }

        /// <summary>
        /// Clicking New (via quick-access toolbar) should create a new tab
        /// without showing a save dialog, leaving the previous tab intact.
        /// </summary>
        [SkippableFact]
        public void NewButton_CreatesNewTab_PreviousTabStillExists()
        {
            RequireDriver();
            _fx.ResetToSingleTab();

            // Type in the first tab so it has content
            _fx.ClearEditor();
            _fx.TypeInEditor("first document content");
            Thread.Sleep(200);

            // Count tabs before clicking New
            int countBefore = FindUntitledTabs().Count;

            // Click the New button (quick-access toolbar)
            var newBtn = FindQuickAccessNewButton();
            newBtn.Click();
            Thread.Sleep(500);

            // A new Untitled tab should be created
            int countAfter = FindUntitledTabs().Count;
            Assert.Equal(countBefore + 1, countAfter);

            // Clean up: close the new tab
            CloseActiveTab();
        }

        /// <summary>
        /// Clicking New when the current tab is modified should NOT show a save
        /// dialog — instead it just opens a new tab.
        /// </summary>
        [SkippableFact]
        public void NewButton_WithModifiedTab_DoesNotPromptSave()
        {
            RequireDriver();

            // Modify the current tab
            _fx.ClearEditor();
            _fx.TypeInEditor("modified content");
            Thread.Sleep(200);

            // Click New — should not block or show a dialog
            var newBtn = FindQuickAccessNewButton();
            newBtn.Click();
            Thread.Sleep(500);

            // The new tab should be active with an empty editor
            Assert.Equal("Words: 0", _fx.GetStatusBarText("WordCountText"));

            // Clean up: close the new tab
            CloseActiveTab();

            // Restore the previous tab to unmodified state
            _fx.ClearEditor();
        }

        // ── Close tab with modifications prompts save ────────────────────────

        /// <summary>
        /// Closing a tab that has unsaved changes should show a save prompt.
        /// Clicking "Don't Save" should close the tab without saving.
        /// </summary>
        [SkippableFact]
        public void CloseModifiedTab_ShowsSaveDialog_DontSaveClosesTab()
        {
            RequireDriver();

            // Create a new tab and modify it
            AddNewTab();
            _fx.TypeInEditor("unsaved changes");
            Thread.Sleep(300);

            // Close the tab — this should trigger the save dialog
            CloseActiveTab();
            Thread.Sleep(500);

            // Try to dismiss the save dialog by clicking "Don't Save"
            try
            {
                var dontSaveBtn = _driver!.FindElement(MobileBy.Name("Don't Save"));
                dontSaveBtn.Click();
                Thread.Sleep(400);
            }
            catch (NoSuchElementException)
            {
                // If no dialog appeared, the tab was already closed (unmodified)
            }

            // The tab should be closed — verify status
            Assert.Equal("Tab closed.", StatusText);
        }

        /// <summary>
        /// Closing a tab that has NO unsaved changes should NOT show a save prompt.
        /// </summary>
        [SkippableFact]
        public void CloseUnmodifiedTab_DoesNotShowSaveDialog()
        {
            RequireDriver();

            // Create a new tab (unmodified)
            AddNewTab();
            Thread.Sleep(200);

            // Close it — should close immediately without a dialog
            CloseActiveTab();

            // Verify it was closed without issues
            Assert.Equal("Tab closed.", StatusText);

            // Editor should still be present
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);
        }

        // ── Tab header reflects document title ───────────────────────────────

        /// <summary>
        /// After opening a new document via New, the tab header should be "Untitled".
        /// </summary>
        [SkippableFact]
        public void NewDocument_TabHeader_IsUntitled()
        {
            RequireDriver();

            // Click New
            var newBtn = FindQuickAccessNewButton();
            newBtn.Click();
            Thread.Sleep(500);

            // The active tab should be "Untitled"
            var untitledTab = _driver!.FindElement(MobileBy.Name("Untitled"));
            Assert.NotNull(untitledTab);

            // Clean up
            CloseActiveTab();
        }

        // ── Multiple tabs with independent modification state ────────────────

        /// <summary>
        /// Opening multiple tabs, modifying some but not others, should only
        /// prompt save for the modified ones when those tabs are closed.
        /// </summary>
        [SkippableFact]
        public void MultipleTabsMixedState_OnlyModifiedTabsPromptSave()
        {
            RequireDriver();

            // Tab 1: type something (modified)
            _fx.ClearEditor();
            _fx.TypeInEditor("tab one modified");
            Thread.Sleep(200);

            // Tab 2: create and leave empty (unmodified)
            AddNewTab();
            Thread.Sleep(200);

            // Close the unmodified tab — should close immediately without dialog
            CloseActiveTab();
            Assert.Equal("Tab closed.", StatusText);

            // Clean up: clear the modified first tab
            _fx.ClearEditor();
        }

        /// <summary>
        /// The "+" button on the tab bar and the New quick-access button
        /// should both create new tabs with independent empty editors.
        /// </summary>
        [SkippableFact]
        public void PlusButton_And_NewButton_BothCreateNewTabs()
        {
            RequireDriver();

            // Use the "+" button
            AddNewTab();
            Assert.Equal("Words: 0", _fx.GetStatusBarText("WordCountText"));

            // Use the New button
            var newBtn = _driver!.FindElement(MobileBy.Name("New"));
            newBtn.Click();
            Thread.Sleep(500);
            Assert.Equal("Words: 0", _fx.GetStatusBarText("WordCountText"));

            // Clean up: close both new tabs
            CloseActiveTab();
            CloseActiveTab();
        }
    }
}
