using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace SmrtPad.UITests.Infrastructure;

/// <summary>
/// xUnit collection fixture for local AI model benchmarking.
/// Connects to a local Appium server at <c>http://127.0.0.1:4723</c> and discovers the
/// SmrtPad AUMID from the locally deployed AppX package.  Does NOT use the remote
/// deploy path in <see cref="SharedAppFixture"/> — designed for on-machine benchmarking.
/// </summary>
public sealed class BenchmarkAppFixture : IDisposable
{
    private const string LocalServerUrl = "http://127.0.0.1:4723/";

    private AppiumSession? _session;
    private string? _appId;
    private string? _mainWindowHandle;

    /// <summary>The live Appium driver, or <c>null</c> if initialisation failed.</summary>
    public WindowsDriver? Driver { get; private set; }

    /// <summary>Human-readable reason when <see cref="Driver"/> is <c>null</c>.</summary>
    public string? InitializationFailure { get; private set; }

    /// <summary>True when a live WinAppDriver session was established.</summary>
    public bool IsAvailable => Driver is not null;

    public BenchmarkAppFixture()
    {
        DotEnvLoader.EnsureLoaded();

        // Force local Appium server for benchmark runs
        Environment.SetEnvironmentVariable("SMRTPAD_APPIUM_SERVER", LocalServerUrl);

        if (!AppiumSession.IsAvailable())
        {
            InitializationFailure = $"Appium server not reachable at {LocalServerUrl}. Run Scripts/start-benchmark.ps1 first.";
            return;
        }

        try
        {
            _appId = DiscoverLocalAumid();
            if (string.IsNullOrWhiteSpace(_appId))
            {
                InitializationFailure = "SmrtPad AppX package not found locally. Build and deploy the WAP project first.";
                return;
            }

            _session = new AppiumSession(
                _appId,
                launchArgument: null,
                forceUnpackaged: false,
                launchViaAppId: true,
                serverUrl: LocalServerUrl);
            Driver = _session.Driver;

            Thread.Sleep(2000);
            DismissSessionRestoreDialogIfPresent();
            _mainWindowHandle = Driver?.CurrentWindowHandle;

            // Stabilise the session
            var pingDeadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < pingDeadline)
            {
                try
                {
                    _ = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
                    break;
                }
                catch (NotImplementedException) { Thread.Sleep(500); }
                catch { break; }
            }
        }
        catch (InvalidOperationException ex)
        {
            InitializationFailure = ex.Message;
            _session = null;
            Driver = null;
        }
        catch (WebDriverException ex)
        {
            InitializationFailure = ex.Message;
            _session = null;
            Driver = null;
        }
    }

    /// <summary>
    /// Discovers the AUMID of the locally deployed SmrtPad package via <c>Get-AppxPackage</c>.
    /// </summary>
    private static string? DiscoverLocalAumid()
    {
        var startInfo = new ProcessStartInfo("powershell.exe",
            "-NoProfile -Command \"$p = Get-AppxPackage -Name '*SmrtPad*'; if ($p) { Write-Output ($p.PackageFamilyName + '!App') }\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process is null) return null;

        string output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        return string.IsNullOrWhiteSpace(output) ? null : output;
    }

    // ── Session lifecycle ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> when the Appium session is pointing at a live window.
    /// Retries up to three times with a short back-off to tolerate transient
    /// window-handle changes.
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
    /// Skips the test if the Appium driver is unavailable or the session has died.
    /// Attempts a restart on a dead session before skipping.
    /// </summary>
    public void RequireSession()
    {
        Skip.If(Driver is null, InitializationFailure ?? "Appium not available or SmrtPad not deployed.");
        if (!IsSessionAlive())
        {
            if (!TryRestartSession())
                Skip.If(true, "Appium session lost and restart failed; test skipped.");
        }
        try
        {
            var editors = Driver!.FindElements(MobileBy.AccessibilityId("Editor"));
            if (editors.Count == 0)
            {
                if (!TryRestartSession())
                    Skip.If(true, "Editor not found and restart failed; test skipped.");
            }
        }
        catch (NotImplementedException)
        {
            if (!TryRestartSession())
                Skip.If(true, "Session stale and restart failed; test skipped.");
        }
        catch { /* transient */ }
    }

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
                launchArgument: null,
                forceUnpackaged: false,
                launchViaAppId: true,
                serverUrl: LocalServerUrl);
            Driver = _session.Driver;
            Thread.Sleep(2000);
            DismissSessionRestoreDialogIfPresent();

            var pingDeadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < pingDeadline)
            {
                try
                {
                    _ = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
                    break;
                }
                catch (NotImplementedException) { Thread.Sleep(500); }
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

    // ── Shared helpers (replicated from SharedAppFixture for local use) ────

    /// <summary>Clears all text in the editor via Ctrl+A → Ctrl+X.</summary>
    public void ClearEditor()
    {
        EnsureBackstageClosed();
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
    }

    /// <summary>Clicks the editor to focus it, then sends <paramref name="text"/> as keystrokes.</summary>
    public void TypeInEditor(string text)
    {
        var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
        editor.Click();
        Thread.Sleep(100);
        editor.SendKeys(text);
        Thread.Sleep(250);
    }

    /// <summary>Sends Ctrl+A to the editor, selecting all content.</summary>
    public void SelectAllInEditor()
    {
        var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
        editor.Click();
        Thread.Sleep(100);
        editor.SendKeys(Keys.Control + "a");
        Thread.Sleep(200);
    }

    /// <summary>Returns the visible text of a status-bar element.</summary>
    public string GetStatusBarText(string automationId)
    {
        var els = Driver!.FindElements(MobileBy.AccessibilityId(automationId));
        return els.Count > 0 ? els[0].Text : string.Empty;
    }

    /// <summary>
    /// Polls <paramref name="automationId"/> up to <paramref name="timeoutMs"/> milliseconds
    /// and returns the element once it is found and displayed.
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

    /// <summary>Non-throwing variant of <see cref="WaitForElement"/>.</summary>
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

    /// <summary>Dismisses the session-restore dialog if present.</summary>
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

    /// <summary>Dismisses an unsaved-changes dialog if present.</summary>
    public void DismissSaveDialogIfPresent()
    {
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

    /// <summary>Returns <c>true</c> when the File backstage overlay is visible.</summary>
    public bool IsBackstageOpen()
    {
        try
        {
            var header = Driver!.FindElements(MobileBy.AccessibilityId("HeaderText"));
            return header.Count > 0 && header[0].Displayed;
        }
        catch { return false; }
    }

    /// <summary>Ensures the backstage is closed.</summary>
    public void EnsureBackstageClosed()
    {
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

    /// <summary>Re-anchors the WinAppDriver session to the main window after flyout popups.</summary>
    public void ReanchorMainWindow()
    {
        if (Driver is null || _mainWindowHandle is null) return;
        try { Driver.SwitchTo().Window(_mainWindowHandle); }
        catch { }
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
        Driver = null;
    }
}
