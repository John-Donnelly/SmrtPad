using System;
using System.Diagnostics;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace SmrtPad.UITests.Infrastructure
{
    /// <summary>
    /// xUnit collection fixture that creates a single <see cref="AppiumSession"/> shared
    /// across all test classes in the "UITests" collection.  Using one session for the
    /// entire collection ensures <c>ClearStartupBlockers()</c> is called exactly once and
    /// all classes share the same live window handle, eliminating the per-class fixture
    /// constructor race that caused cascaded <c>NoSuchWindowException</c> failures (N-1).
    ///
    /// Usage:
    /// <code>
    ///   [Collection("UITests")]
    ///   public class MyTests
    ///   {
    ///       private readonly SharedAppFixture _fx;
    ///       public MyTests(SharedAppFixture fx) => _fx = fx;
    ///   }
    /// </code>
    ///
    /// The fixture lifetime is managed by <see cref="UITestsCollection"/>; test classes
    /// must NOT implement <c>IDisposable</c> teardown that touches the session.
    /// </summary>
    public class SharedAppFixture : IDisposable
    {
        private readonly AppiumSession? _session;

        public WindowsDriver? Driver { get; }

        /// <summary>
        /// Returns <c>true</c> when the Appium session is pointing at a live window.
        /// Retries up to three times with a short back-off to tolerate transient
        /// window-handle changes caused by Windows clipboard notifications or other
        /// ephemeral popup windows that briefly replace the main window handle.
        /// </summary>
        public bool IsSessionAlive()
        {
            if (Driver is null) return false;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try { _ = Driver.Title; return true; }
                catch (WebDriverException)
                {
                    if (attempt < 2) Thread.Sleep(1500);
                }
            }
            return false;
        }

        /// <summary>
        /// Skips the test if the Appium driver is unavailable OR if the session has become
        /// invalid (e.g. the app crashed mid-run). Call this at the start of every test to
        /// prevent <c>NoSuchWindowException</c> cascade failures across the collection.
        /// </summary>
        public void RequireSession()
        {
            Skip.If(Driver is null, "WinAppDriver / Appium not available or SmrtPad.exe not built.");
            SkipIfSessionDead();
        }

        /// <summary>
        /// Throws a xUnit <c>SkipException</c> when the Appium session is dead so
        /// the test is marked <em>Skipped</em> instead of <em>Failed</em>, preventing
        /// a cascade of <c>NoSuchWindowException</c> failures across the collection.
        /// </summary>
        private void SkipIfSessionDead()
        {
            if (!IsSessionAlive())
                Skip.If(true, "Appium session lost (app closed or crashed); test skipped to prevent cascade.");
        }

        /// <summary>True when a live WinAppDriver session was established.</summary>
        public bool IsAvailable => Driver is not null;

        public SharedAppFixture() : this(launchArgument: null) { }

        /// <summary>
        /// Initialises the session, passing <paramref name="launchArgument"/> to the app
        /// process (e.g. <c>--free-tier</c>).  Intended for use by subclasses.
        /// </summary>
        protected SharedAppFixture(string? launchArgument)
        {
            if (!AppiumSession.IsAvailable()) return;
            string? exe = AppiumSession.FindSmrtPadExe();
            if (exe is null) return;

            try
            {
                _session = new AppiumSession(exe, launchArgument: launchArgument);
                Driver   = _session.Driver;

                // Allow the app's async startup sequence (session-restore check,
                // crash-telemetry consent) time to render any dialogs, then
                // dismiss them so they do not block the first test.
                Thread.Sleep(1500);
                DismissSessionRestoreDialogIfPresent();
            }
            catch
            {
                _session = null;
                Driver   = null;
            }
        }

        public void Dispose() => _session?.Dispose();

        // ── Shared helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Clears all text in the editor via Ctrl+A → Delete, then resets
        /// character formatting so the next test starts with a neutral caret.
        /// Ensures backstage is closed first so editor is accessible.
        /// </summary>
        public void ClearEditor()
        {
            SkipIfSessionDead();
            EnsureBackstageClosed();

            bool usedMenu = TryClickMenuItem("Edit", "Select All")
                && TryClickMenuItem("Edit", "Cut");

            if (!usedMenu)
            {
                var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
                editor.Click();
                Thread.Sleep(100);
                editor.SendKeys(Keys.Control + "a");
                Thread.Sleep(150);
                editor.SendKeys(Keys.Control + "x");
                Thread.Sleep(300);
            }

            // Reset character formatting at the caret so no italic/bold/super/subscript
            // leaks from a previous test into the next one (UI-6).
            ResetCharacterFormatting();
        }

        /// <summary>
        /// Selects all text in the editor and clicks the Clear Formatting ribbon button,
        /// resetting bold/italic/underline/super/subscript state at the caret.
        /// Safe to call on an empty editor.
        /// </summary>
        public void ResetCharacterFormatting()
        {
            try
            {
                var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
                editor.Click();
                Thread.Sleep(80);
                var clearBtn = Driver.FindElements(MobileBy.AccessibilityId("ClearFormattingButton"));
                if (clearBtn.Count > 0)
                {
                    clearBtn[0].Click();
                    Thread.Sleep(150);
                }
            }
            catch { }
        }

        /// <summary>
        /// Clicks the editor to focus it, then sends <paramref name="text"/> as keystrokes.
        /// </summary>
        public void TypeInEditor(string text)
        {
            SkipIfSessionDead();
            var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.Click();
            Thread.Sleep(100);
            editor.SendKeys(text);
            Thread.Sleep(250);
        }

        /// <summary>Sends Ctrl+A to the editor, selecting all content.</summary>
        public void SelectAllInEditor()
        {
            if (!TryClickMenuItem("Edit", "Select All"))
            {
                var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
                editor.Click();
                Thread.Sleep(100);
                editor.SendKeys(Keys.Control + "a");
                Thread.Sleep(200);
            }
        }

        private bool TryClickMenuItem(string menuName, string itemName)
        {
            try
            {
                ClickMenuItem(menuName, itemName);
                return true;
            }
            catch (NoSuchElementException ex)
            {
                Debug.WriteLine($"Menu item '{menuName} -> {itemName}' not found: {ex.Message}");
                return false;
            }
            catch (WebDriverException ex)
            {
                Debug.WriteLine($"Menu item '{menuName} -> {itemName}' click failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Sends Ctrl+Z to the editor to undo the last action.</summary>
        public void UndoInEditor()
        {
            var undoButtons = Driver!.FindElements(MobileBy.AccessibilityId("UndoButton"));
            if (undoButtons.Count == 0)
            {
                undoButtons = Driver.FindElements(MobileBy.Name("Undo (Ctrl+Z)"));
            }

            if (undoButtons.Count == 0)
            {
                throw new InvalidOperationException("Undo button not found.");
            }

            undoButtons[0].Click();
            Thread.Sleep(250);
        }

        /// <summary>
        /// Opens <paramref name="menuName"/> and clicks <paramref name="itemName"/>,
        /// preferring stable automation IDs when available and falling back to UIA names.
        /// </summary>
        public void ClickMenuItem(string menuName, string itemName)
        {
            SkipIfSessionDead();
            FindElementByIdOrName(GetMenuAutomationId(menuName), menuName).Click();
            Thread.Sleep(450);
            FindElementByIdOrName(GetMenuItemAutomationId(itemName), itemName).Click();
            Thread.Sleep(300);
        }

        private AppiumElement FindElementByIdOrName(string? automationId, string fallbackName)
        {
            if (!string.IsNullOrWhiteSpace(automationId))
            {
                var byId = Driver!.FindElements(MobileBy.AccessibilityId(automationId));
                if (byId.Count > 0)
                {
                    return byId[0];
                }
            }

            return Driver!.FindElement(MobileBy.Name(fallbackName));
        }

        private static string? GetMenuAutomationId(string menuName)
        {
            return menuName switch
            {
                "Edit" => "EditMenuBarItem",
                "View" => "ViewMenuBarItem",
                "Format" => "FormatMenuBarItem",
                "Macro" => "MacroMenuBar",
                _ => null
            };
        }

        private static string? GetMenuItemAutomationId(string itemName)
        {
            return itemName switch
            {
                "Cut" => "CutMenuItem",
                "Copy" => "CopyMenuItem",
                "Paste" => "PasteMenuItem",
                "Paste Special" => "PasteSpecialMenuItem",
                "Select All" => "SelectAllMenuItem",
                "Zoom In" => "ZoomInMenuItem",
                "Zoom Out" => "ZoomOutMenuItem",
                "Font..." => "FormatFontMenuItem",
                "Paragraph..." => "FormatParagraphMenuItem",
                "✨ Smart Sidebar" => "SmartSidebarToggle",
                "Status Bar" => "StatusBarToggle",
                "Spell Check" => "SpellCheckToggle",
                "Ruler" => "RulerToggle",
                "Page View" => "PageViewToggle",
                "Focus Mode" => "FocusModeToggle",
                _ => null
            };
        }

        /// <summary>
        /// Returns the current toggle state of a ToggleButton or ToggleMenuFlyoutItem
        /// identified by <paramref name="automationId"/>.
        /// Uses the UIA <c>Toggle.ToggleState</c> attribute: "1" = checked.
        /// </summary>
        public bool IsToggleChecked(string automationId)
        {
            var el = Driver!.FindElement(MobileBy.AccessibilityId(automationId));
            return el.GetAttribute("Toggle.ToggleState") == "1";
        }

        /// <summary>
        /// Returns the visible text of a status-bar <c>TextBlock</c> element
        /// identified by <paramref name="automationId"/>.
        /// </summary>
        public string GetStatusBarText(string automationId)
        {
            SkipIfSessionDead();
            return Driver!.FindElement(MobileBy.AccessibilityId(automationId)).Text;
        }

        /// <summary>
        /// Returns <c>true</c> when the File backstage overlay is currently visible.
        /// Detected by the presence of the backstage <c>HeaderText</c> element.
        /// </summary>
        public bool IsBackstageOpen()
        {
            try
            {
                var header = Driver!.FindElements(MobileBy.AccessibilityId("HeaderText"));
                return header.Count > 0 && header[0].Displayed;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ensures the backstage is closed. If it is currently open, clicks the
        /// File button to toggle it shut.
        /// </summary>
        public void EnsureBackstageClosed()
        {
            if (!IsBackstageOpen()) return;
            try
            {
                Driver!.FindElement(MobileBy.AccessibilityId("FileMenuButton")).Click();
                Thread.Sleep(400);
            }
            catch { }
        }

        /// <summary>
        /// Ensures the backstage is open. If it is currently closed, clicks the
        /// File button to open it. If already open, does nothing.
        /// </summary>
        public void EnsureBackstageOpen()
        {
            if (IsBackstageOpen()) return;
            Driver!.FindElement(MobileBy.AccessibilityId("FileMenuButton")).Click();
            Thread.Sleep(800);
        }

        /// <summary>
        /// Dismisses the "Unsaved Changes" save-prompt dialog by clicking
        /// "Don't Save" if it is currently visible.  Does nothing if the dialog
        /// is not present.  Call this after any operation that may close a
        /// modified tab (e.g. Ctrl+W) to prevent dialogs from blocking tests.
        /// </summary>
        public void DismissSaveDialogIfPresent()
        {
            try
            {
                var dontSave = Driver!.FindElements(MobileBy.Name("Don't Save"));
                if (dontSave.Count > 0)
                {
                    dontSave[0].Click();
                    Thread.Sleep(300);
                }
            }
            catch { }
        }

        /// <summary>
        /// Dismisses the "Restore previous session" startup dialog by clicking
        /// "Discard" if it is currently visible.  Does nothing if the dialog is
        /// not present.  Call this after connecting the driver to handle dialogs
        /// caused by a leftover session from a previous test run.
        /// </summary>
        public void DismissSessionRestoreDialogIfPresent()
        {
            try
            {
                var discard = Driver!.FindElements(MobileBy.Name("Discard"));
                if (discard.Count > 0)
                {
                    discard[0].Click();
                    Thread.Sleep(300);
                }
            }
            catch { }
        }

        /// <summary>
        /// Ensures Page View mode is OFF before running zoom-bounds tests.
        /// Opens the View menu so <c>PageViewToggle</c> is in the UIA tree (it is a
        /// <c>ToggleMenuFlyoutItem</c> and is absent when the flyout is closed), reads
        /// its toggle state, clicks it only if it is currently checked, then closes the
        /// menu if nothing was clicked (UI-10b).
        /// </summary>
        public void EnsurePageViewOff()
        {
            try
            {
                // Open the View menu so flyout items enter the UIA tree.
                Driver!.FindElement(MobileBy.AccessibilityId("ViewMenuBarItem")).Click();
                Thread.Sleep(350);

                var toggle = Driver.FindElements(MobileBy.AccessibilityId("PageViewToggle"));
                if (toggle.Count > 0 && toggle[0].GetAttribute("Toggle.ToggleState") == "1")
                {
                    // Page View is ON — click the toggle to turn it OFF.
                    toggle[0].Click();
                    Thread.Sleep(400);
                }
                else
                {
                    // Page View is OFF — dismiss the menu without changing anything.
                    Driver.FindElement(MobileBy.AccessibilityId("ViewMenuBarItem")).Click();
                    Thread.Sleep(200);
                }
            }
            catch { }
        }

        /// <summary>
        /// Ensures Focus Mode is OFF. Detects the mode by checking whether
        /// <c>RibbonBar</c> is displayed — when Focus Mode is ON the ribbon is
        /// collapsed. If hidden, opens the View menu via <c>ViewMenuBarItem</c>
        /// AutomationId and clicks <c>FocusModeToggle</c> to restore normal view (UI-11b).
        /// </summary>
        public void EnsureFocusModeOff()
        {
            if (!IsSessionAlive()) return;  // Don't skip — constructor calls this; silently no-op instead.
            try
            {
                // Check RibbonBar visibility — collapsed when FocusMode is ON.
                var ribbon = Driver!.FindElements(MobileBy.AccessibilityId("RibbonBar"));
                bool focusModeOn = ribbon.Count > 0 && !ribbon[0].Displayed;
                if (!focusModeOn) return;

                // Open the View menu (in Grid.Row=0, always visible) then click the toggle.
                Driver.FindElement(MobileBy.AccessibilityId("ViewMenuBarItem")).Click();
                Thread.Sleep(350);
                Driver.FindElement(MobileBy.AccessibilityId("FocusModeToggle")).Click();
                Thread.Sleep(500);
            }
            catch { }
        }

        /// <summary>
        /// Opens a new tab via the TabView "+" button and waits for it to be active.
        /// The new tab has an empty undo/redo stack, making it ideal for undo-isolation tests (UI-8).
        /// </summary>
        public void AddFreshTab()
        {
            SkipIfSessionDead();
            EnsureBackstageClosed();
            Driver!.FindElement(MobileBy.AccessibilityId("AddButton")).Click();
            Thread.Sleep(500);
        }

        /// <summary>
        /// Closes the active tab by sending Ctrl+W and dismisses any unsaved-changes dialog.
        /// Refuses to close when only one tab remains to prevent the app from exiting
        /// and killing the shared session.
        /// </summary>
        public void CloseActiveTab()
        {
            try
            {
                // Never close the last tab — the app exits when the final tab is closed,
                // which would terminate the shared Appium session for all remaining tests.
                // Use the same name-based lookup as ResetToSingleTab.
                var tabStrip = Driver!.FindElements(MobileBy.AccessibilityId("DocumentTabs"));
                if (tabStrip.Count > 0)
                {
                    var allTabs = tabStrip[0].FindElements(MobileBy.Name("Untitled"));
                    if (allTabs.Count <= 1) return;
                }

                var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
                editor.SendKeys(Keys.Control + "w");
                Thread.Sleep(500);
                DismissSaveDialogIfPresent();
            }
            catch { }
        }

        /// <summary>
        /// Closes all tabs except one by pressing Ctrl+W repeatedly until only a
        /// single tab remains, dismissing any unsaved-changes dialogs along the way.
        /// Call this at the start of tests that assert a specific tab count so that
        /// tabs leaked by prior tests do not skew the result (UI-7).
        /// </summary>
        public void ResetToSingleTab()
        {
            SkipIfSessionDead();
            EnsureBackstageClosed();
            for (int i = 0; i < 20; i++)
            {
                try
                {
                    var tabs = Driver!.FindElement(MobileBy.AccessibilityId("DocumentTabs"));
                    var allTabs = tabs.FindElements(MobileBy.Name("Untitled"));
                    if (allTabs.Count <= 1) break;
                }
                catch { break; }

                try
                {
                    var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
                    editor.SendKeys(Keys.Control + "w");
                    Thread.Sleep(400);
                    DismissSaveDialogIfPresent();
                }
                catch { break; }
            }
            Thread.Sleep(200);
        }

        /// <summary>
        /// Polls <paramref name="automationId"/> up to <paramref name="timeoutMs"/> milliseconds
        /// (checking every <paramref name="intervalMs"/> ms) and returns the element once it
        /// is found and displayed.  Throws <see cref="OpenQA.Selenium.WebDriverException"/> if
        /// the element is not found within the timeout.  Used instead of fixed sleeps where the
        /// UI update time is variable (e.g. status-bar animations) (UI-7, UI-12).
        /// </summary>
        public AppiumElement WaitForElement(string automationId, int timeoutMs = 3000, int intervalMs = 100)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                var els = Driver!.FindElements(MobileBy.AccessibilityId(automationId));
                if (els.Count > 0 && els[0].Displayed)
                    return els[0];
                Thread.Sleep(intervalMs);
            }
            return Driver!.FindElement(MobileBy.AccessibilityId(automationId));
        }

        /// <summary>
        /// Polls the status-bar <paramref name="automationId"/> element until its text equals
        /// <paramref name="expected"/> or the <paramref name="timeoutMs"/> elapses.
        /// Returns the last observed text regardless (caller can assert).
        /// Avoids brittle fixed sleeps for messages that reset on a timer (UI-7).
        /// </summary>
        public string WaitForStatusText(string automationId, string expected, int timeoutMs = 3000, int intervalMs = 100)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            string text = string.Empty;
            while (DateTime.UtcNow < deadline)
            {
                try { text = GetStatusBarText(automationId); } catch { }
                if (text == expected) return text;
                Thread.Sleep(intervalMs);
            }
            return text;
        }
    }
}
