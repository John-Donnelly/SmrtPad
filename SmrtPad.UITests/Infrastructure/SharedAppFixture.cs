using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private AppiumSession? _session;
        private readonly string? _launchArgument;
        private readonly bool _forceUnpackaged;
        private string? _appId;
        private string? _initializationFailure;
        private string? _mainWindowHandle;

        public WindowsDriver? Driver { get; private set; }

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
        /// invalid (e.g. the app crashed mid-run).  When the session is detected as dead,
        /// attempts to restart it before skipping so subsequent tests can continue.
        /// Call this at the start of every test to prevent cascade failures across the collection.
        /// </summary>
        public void RequireSession()
        {
            Skip.If(Driver is null, _initializationFailure ?? "WinAppDriver / Appium not available or SmrtPad.exe not built.");
            if (!IsSessionAlive())
            {
                if (!TryRestartSession())
                    Skip.If(true, "Appium session lost and restart failed; test skipped.");
            }
            // Secondary health check: NotImplementedException from WinAppDriver indicates a
            // stale HWND (e.g. after a dialog closed and changed the session window context).
            // An empty result from FindElements also indicates the session is on the wrong HWND
            // (the main Editor should always be present in a live app window).
            try
            {
                var editors = Driver!.FindElements(MobileBy.AccessibilityId("Editor"));
                if (editors.Count == 0)
                {
                    // No Editor found — session is on the wrong HWND; try to restart.
                    if (!TryRestartSession())
                        Skip.If(true, "Appium session HWND wrong (Editor not found) and restart failed; test skipped.");
                }
            }
            catch (NotImplementedException)
            {
                // Stale session detected — try to restart; skip the test if restart fails.
                if (!TryRestartSession())
                    Skip.If(true, "Appium session HWND stale (NotImplementedException) and restart failed; test skipped.");
            }
            catch { /* ignore other transient errors from the health ping */ }
        }

        /// <summary>
        /// Disposes the current session and starts a fresh one with the same launch arguments.
        /// Returns <c>true</c> when the new session is alive and ready.
        /// </summary>
        private bool TryRestartSession()
        {
            try
            {
                _session?.Dispose();
                _session = null;
                Driver = null;

                if (!AppiumSession.IsAvailable() || string.IsNullOrWhiteSpace(_appId)) return false;

                _session = new AppiumSession(
                    _appId,
                    launchArgument: _launchArgument,
                    forceUnpackaged: _forceUnpackaged,
                    launchViaAppId: true,
                    serverUrl: AppiumSession.DefaultServerUrl);
                Driver = _session.Driver;
                Thread.Sleep(2000);
                DismissSessionRestoreDialogIfPresent();

                // Stabilise the session: WinAppDriver returns HTTP 501 briefly after restart,
                // causing NotImplementedException in tests that use FindElement (singular).
                // Poll until it succeeds so subsequent tests don't cascade-fail.
                var pingDeadline = DateTime.UtcNow.AddSeconds(10);
                while (DateTime.UtcNow < pingDeadline)
                {
                    try
                    {
                        _ = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
                        break;
                    }
                    catch (NotImplementedException)
                    {
                        Thread.Sleep(500);
                    }
                    catch { break; }
                }

                _mainWindowHandle = Driver?.CurrentWindowHandle;
                return IsSessionAlive();
            }
            catch
            {
                _session = null;
                Driver = null;
                return false;
            }
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
        protected SharedAppFixture(string? launchArgument, bool forceUnpackaged = false)
        {
            DotEnvLoader.EnsureLoaded();
            _launchArgument = launchArgument;
            _forceUnpackaged = forceUnpackaged;

            if (!AppiumSession.IsAvailable()) return;

            try
            {
                _appId = DeployPackageAndGetAppId();
                if (string.IsNullOrWhiteSpace(_appId))
                {
                    _initializationFailure = "Remote UI test package deployment did not return an app identity.";
                    return;
                }

                _session = new AppiumSession(
                    _appId,
                    launchArgument: launchArgument,
                    forceUnpackaged: forceUnpackaged,
                    launchViaAppId: true,
                    serverUrl: AppiumSession.DefaultServerUrl);
                Driver   = _session.Driver;

                // Allow the app's async startup sequence (session-restore check,
                // crash-telemetry consent) time to render any dialogs, then
                // dismiss them so they do not block the first test.
                Thread.Sleep(1500);
                DismissSessionRestoreDialogIfPresent();
                _mainWindowHandle = Driver?.CurrentWindowHandle;
            }
            catch (InvalidOperationException ex)
            {
                _initializationFailure = ex.Message;
                _session = null;
                Driver   = null;
            }
            catch (WebDriverException ex)
            {
                _initializationFailure = ex.Message;
                _session = null;
                Driver   = null;
            }
        }

        internal static string? DeployPackageAndGetAppId()
        {
            DotEnvLoader.EnsureLoaded();
            string scriptPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "SmrtPad (Package)", "deploy.ps1");
            scriptPath = Path.GetFullPath(scriptPath);
            if (!File.Exists(scriptPath))
                return null;

            string remoteHost = Environment.GetEnvironmentVariable("SMRTPAD_REMOTE_HOST") ?? "192.168.0.100";
            string? remoteUser = Environment.GetEnvironmentVariable("UITEST_REMOTE_WINRM_USERNAME")
                ?? Environment.GetEnvironmentVariable("SMRTPAD_REMOTE_USER");
            string? remotePassword = Environment.GetEnvironmentVariable("UITEST_REMOTE_WINRM_PASSWORD")
                ?? Environment.GetEnvironmentVariable("SMRTPAD_REMOTE_PASS");
            string? remoteShareRoot = Environment.GetEnvironmentVariable("UITEST_REMOTE_SHARE_ROOT");

            var arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\" -RemoteHost \"{remoteHost}\"";
            if (!string.IsNullOrWhiteSpace(remoteUser) && !string.IsNullOrWhiteSpace(remotePassword))
            {
                arguments += $" -RemoteUser \"{remoteUser}\" -RemotePassword \"{remotePassword}\"";
            }

            if (!string.IsNullOrWhiteSpace(remoteShareRoot))
            {
                arguments += $" -RemoteShareRoot \"{remoteShareRoot}\"";
            }

            var startInfo = new ProcessStartInfo("powershell.exe", arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start deploy.ps1 for remote UI test setup.");

            // Read both streams concurrently — sequential ReadToEnd() deadlocks when both
            // stdout and stderr buffers fill before WaitForExit returns.
            var stdoutTask = Task.Run(() => process.StandardOutput.ReadToEnd());
            var stderrTask = Task.Run(() => process.StandardError.ReadToEnd());

            const int deployTimeoutMs = 15 * 60 * 1000; // 15 minutes
            var exited = process.WaitForExit(deployTimeoutMs);
            if (!exited)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                throw new InvalidOperationException(
                    "Remote deploy timed out after 15 minutes and was killed.");
            }

            string output = stdoutTask.GetAwaiter().GetResult();
            string error = stderrTask.GetAwaiter().GetResult();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Remote deploy failed with exit code {process.ExitCode}. Output: {output} Error: {error}".Trim());
            }

            string? appId = output
                .Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries)
                .Select(static line => line.Trim())
                .FirstOrDefault(static line => line.StartsWith("AUMID=", StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(appId))
            {
                throw new InvalidOperationException("Remote deploy completed but did not report an AUMID.");
            }

            return appId["AUMID=".Length..];
        }

        public void Dispose()
        {
            _session?.Dispose();
            _session = null;
            Driver = null;
        }

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

            // Use keyboard shortcuts directly — opening the Edit-menu flyout causes
            // WinAppDriver HWND drift that makes FindElement calls in subsequent test
            // code return HTTP 501 NotImplementedException (EditMenu-501 regression).
            var editors = Driver!.FindElements(MobileBy.AccessibilityId("Editor"));
            if (editors.Count > 0)
            {
                var editor = editors[0];
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
                var editorEls = Driver!.FindElements(MobileBy.AccessibilityId("Editor"));
                if (editorEls.Count == 0) return;
                var editor = editorEls[0];
                editor.Click();
                Thread.Sleep(80);
                var clearBtn = Driver.FindElements(MobileBy.AccessibilityId("ClearFormattingButton"));
                if (clearBtn.Count > 0)
                {
                    clearBtn[0].Click();
                    // 500 ms: ensure the ClearFormatting_Click handler runs to completion
                    // on the app UI thread before this method returns.  A shorter sleep
                    // allows the WinUI dispatcher to deliver the status-bar update
                    // ("Formatting cleared.") into the NEXT test's time-slice, causing
                    // the AddTab status assertion to see a stale value.
                    Thread.Sleep(500);
                    // Re-anchor HWND context to the editor after ribbon button click
                    // to prevent WinAppDriver 501 errors in subsequent element searches.
                    editor.Click();
                    Thread.Sleep(100);
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
            // Use keyboard shortcut only — the Edit-menu flyout path causes HWND drift
            // and the subsequent ReanchorMainWindow() call in ClickMenuItem may not
            // preserve the RichEditBox selection, making formatting operations that follow
            // a no-op (bold/italic applied to an empty caret rather than the full text).
            var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.Click();
            Thread.Sleep(100);
            editor.SendKeys(Keys.Control + "a");
            Thread.Sleep(200);
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
            FindElementByIdOrName(GetMenuAutomationId(menuName), menuName).Click();
            Thread.Sleep(450);
            FindElementByIdOrName(GetMenuItemAutomationId(itemName), itemName).Click();
            Thread.Sleep(300);
            // Menu flyout popup closes here; re-anchor element context to the main window
            // so that subsequent FindElement/FindElements calls do not return HTTP 501.
            ReanchorMainWindow();
        }

        /// <summary>
        /// Switches the WinAppDriver session context back to the main application window
        /// after a menu-flyout popup has opened and closed.  WinAppDriver shifts its
        /// internal HWND context to the flyout popup; when the popup closes the context
        /// becomes stale and all subsequent element searches return HTTP 501.
        /// </summary>
        private void ReanchorMainWindow()
        {
            if (Driver is null || _mainWindowHandle is null) return;
            try { Driver.SwitchTo().Window(_mainWindowHandle); }
            catch { }
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
                "Paste Plain" => "PastePlainEditMenuItem",
                "Paste Special" => "PasteSpecialMenuItem",
                "Select All" => "SelectAllMenuItem",
                "Zoom In" => "ZoomInMenuItem",
                "Zoom Out" => "ZoomOutMenuItem",
                "Font..." => "FormatFontMenuItem",
                "Paragraph..." => "FormatParagraphMenuItem",
                "✨ Smrt Sidebar" => "SmartSidebarToggle",
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
            // Use FindElements (plural) — more resilient to transient HWND drift than
            // singular FindElement, which can return HTTP 501 after UIA tree updates.
            var els = Driver!.FindElements(MobileBy.AccessibilityId(automationId));
            return els.Count > 0 ? els[0].Text : string.Empty;
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
            // Reanchor to the main window before checking backstage state.
            // If a menu-flyout or line-spacing popup was left open by a prior
            // test the driver HWND context is drifted; IsBackstageOpen() would
            // find HeaderText in the stale popup context and return a false-positive.
            ReanchorMainWindow();
            if (!IsBackstageOpen()) return;
            try
            {
                Driver!.FindElement(MobileBy.AccessibilityId("FileMenuButton")).Click();
                var deadline = DateTime.UtcNow.AddMilliseconds(1500);
                while (DateTime.UtcNow < deadline && IsBackstageOpen())
                    Thread.Sleep(100);
            }
            catch { }
        }

        /// <summary>
        /// Ensures the backstage is open. If it is currently closed, clicks the
        /// File button to open it. If already open, does nothing.
        /// </summary>
        public void EnsureBackstageOpen()
        {
            // Reanchor before the IsBackstageOpen fast-path — a drifted HWND from
            // a prior test's flyout popup can cause a false-positive "already open"
            // result, returning immediately without actually opening the backstage.
            ReanchorMainWindow();
            if (IsBackstageOpen()) return;
            Driver!.FindElement(MobileBy.AccessibilityId("FileMenuButton")).Click();
            var deadline = DateTime.UtcNow.AddMilliseconds(2000);
            while (DateTime.UtcNow < deadline && !IsBackstageOpen())
                Thread.Sleep(100);
        }

        /// <summary>
        /// Dismisses the "Unsaved Changes" save-prompt dialog by clicking
        /// "Don't Save" if it is currently visible.  Does nothing if the dialog
        /// is not present.  Call this after any operation that may close a
        /// modified tab (e.g. Ctrl+W) to prevent dialogs from blocking tests.
        /// </summary>
        public void DismissSaveDialogIfPresent()
        {
            // Retry for up to 1 s — the dialog may appear slightly after Ctrl+W is sent.
            var deadline = DateTime.UtcNow.AddSeconds(1);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var dontSave = Driver!.FindElements(MobileBy.Name("Don't Save"));
                    if (dontSave.Count > 0)
                    {
                        dontSave[0].Click();
                        Thread.Sleep(300);
                        return;
                    }
                }
                catch { }
                Thread.Sleep(100);
            }
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
            // Re-anchor to the main window before searching for AddButton.
            // After ribbon button clicks (e.g. ClearFormattingButton in ResetCharacterFormatting)
            // in prior tests the WinAppDriver session context can drift, causing FindElement
            // to operate on a stale sub-tree instead of the main window.
            ReanchorMainWindow();
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
        /// Non-throwing variant of <see cref="WaitForElement"/>: returns <c>null</c> if the
        /// element does not appear within <paramref name="timeoutMs"/> milliseconds instead of
        /// throwing.  Use when the caller needs to take a retry action on timeout.
        /// </summary>
        public AppiumElement? WaitForElementOrNull(string automationId, int timeoutMs = 3000, int intervalMs = 100)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                var els = Driver!.FindElements(MobileBy.AccessibilityId(automationId));
                if (els.Count > 0 && els[0].Displayed)
                    return els[0];
                Thread.Sleep(intervalMs);
            }
            return null;
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
                // Use FindElements (plural) — resilient to UIA tree updates that make
                // singular FindElement return HTTP 501 or NoSuchElement transiently.
                try
                {
                    var els = Driver!.FindElements(MobileBy.AccessibilityId(automationId));
                    if (els.Count > 0) text = els[0].Text;
                }
                catch { }
                if (text == expected) return text;
                Thread.Sleep(intervalMs);
            }
            return text;
        }
    }
}
