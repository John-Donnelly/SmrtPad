using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace SmrtPad.UITests.Infrastructure;

/// <summary>
/// xUnit collection fixture for remote AI model benchmarking.
/// Connects to the Appium server on the remote test PC (configured via
/// <c>SMRTPAD_APPIUM_SERVER</c>), deploys SmrtPad via the existing
/// <c>deploy.ps1</c> pipeline, probes the remote hardware, and filters
/// models to only those the remote system can run — mirroring the app's
/// own <c>ModelSizeSelector</c> behaviour on launch.
///
/// <para>The remote system's eligible models are pre-downloaded before
/// the benchmark suite starts to prevent download timeouts during tests.</para>
/// </summary>
public sealed class RemoteBenchmarkAppFixture : IBenchmarkFixture, IDisposable
{
    private AppiumSession? _session;
    private string? _appId;
    private string? _mainWindowHandle;

    /// <summary>The live Appium driver, or <c>null</c> if initialisation failed.</summary>
    public WindowsDriver? Driver { get; private set; }

    /// <summary>Human-readable reason when <see cref="Driver"/> is <c>null</c>.</summary>
    public string? InitializationFailure { get; private set; }

    /// <summary>True when a live WinAppDriver session was established.</summary>
    public bool IsAvailable => Driver is not null;

    /// <summary>Hardware capabilities of the remote test machine.</summary>
    public RemoteHardwareInfo? Hardware { get; private set; }

    /// <summary>Models eligible to run on the remote system's hardware.</summary>
    public IReadOnlyList<string> EligibleModels { get; private set; } = [];

    /// <summary>Pre-download results for each model (alias → success).</summary>
    public IReadOnlyDictionary<string, bool> PreloadResults { get; private set; }
        = new Dictionary<string, bool>();

    public RemoteBenchmarkAppFixture()
    {
        DotEnvLoader.EnsureLoaded();

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UITEST_DEPLOY_CONFIGURATION")))
        {
            Environment.SetEnvironmentVariable("UITEST_DEPLOY_CONFIGURATION", "Debug");
        }

        var serverUrl = AppiumSession.DefaultServerUrl;
        var remoteHost = Environment.GetEnvironmentVariable("SMRTPAD_REMOTE_HOST") ?? "192.168.0.100";
        var remoteUser = Environment.GetEnvironmentVariable("UITEST_REMOTE_WINRM_USERNAME")
            ?? Environment.GetEnvironmentVariable("SMRTPAD_REMOTE_USER");
        var remotePassword = Environment.GetEnvironmentVariable("UITEST_REMOTE_WINRM_PASSWORD")
            ?? Environment.GetEnvironmentVariable("SMRTPAD_REMOTE_PASS");

        if (!AppiumSession.IsAvailable())
        {
            InitializationFailure = $"Appium server not reachable at {serverUrl}. " +
                "Ensure Appium is running on the remote machine and SMRTPAD_APPIUM_SERVER is set.";
            return;
        }

        try
        {
            // ── Phase 1: Probe remote hardware ──────────────────────────────
            Hardware = RemoteHardwareProbe.Probe(remoteHost, remoteUser, remotePassword);

            // ── Phase 2: Filter models by remote hardware ───────────────────
            EligibleModels = RemoteModelFilter.GetEligibleModels(Hardware);

            // Include Phi Silica if the remote system has an NPU (best-effort detection)
            // Phi Silica is always eligible since it runs on the NPU, not GPU/CPU

            // ── Phase 3: Pre-download eligible models ───────────────────────
            PreloadResults = RemoteModelPreloader.PreloadModels(
                remoteHost,
                EligibleModels,
                Hardware.HasGpu,
                remoteUser,
                remotePassword);

            ClearRemoteFreeTierFlag(remoteHost, remoteUser, remotePassword);

            // ── Phase 4: Deploy app and connect Appium session ──────────────
            _appId = SharedAppFixture.DeployPackageAndGetAppId();
            if (string.IsNullOrWhiteSpace(_appId))
            {
                InitializationFailure = "Remote UI test package deployment did not return an app identity.";
                return;
            }

            _session = new AppiumSession(
                _appId,
                launchArgument: null,
                forceUnpackaged: false,
                launchViaAppId: true,
                serverUrl: serverUrl);
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

    // ── IBenchmarkFixture ────────────────────────────────────────────────────

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public bool TryRestartApp() => TryRestartSession();

    /// <inheritdoc/>
    public void DismissAllBlockingDialogsIfPresent()
    {
        if (Driver is null) return;
        string[] dismissButtonNames = ["Discard", "Don't Save", "Not now", "OK", "Cancel"];
        try
        {
            foreach (var name in dismissButtonNames)
            {
                var btns = Driver.FindElements(MobileBy.Name(name));
                if (btns.Count > 0 && btns[0].Displayed)
                {
                    btns[0].Click();
                    Thread.Sleep(300);
                    return;
                }
            }
        }
        catch { }
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public void TypeInEditor(string text)
    {
        // Use clipboard paste for reliability with long strings on remote
        SetClipboardText(text);
        var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
        editor.Click();
        Thread.Sleep(150);
        editor.SendKeys(Keys.Control + "v");
        Thread.Sleep(300);
    }

    /// <inheritdoc/>
    public void SelectAllInEditor()
    {
        var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
        editor.Click();
        Thread.Sleep(100);
        editor.SendKeys(Keys.Control + "a");
        Thread.Sleep(200);
    }

    // ── Session lifecycle ────────────────────────────────────────────────────

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
                serverUrl: AppiumSession.DefaultServerUrl);
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

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Re-anchors the WinAppDriver session to the main window after flyout popups.</summary>
    public void ReanchorMainWindow()
    {
        if (Driver is null || _mainWindowHandle is null) return;
        try { Driver.SwitchTo().Window(_mainWindowHandle); }
        catch { }
    }

    /// <summary>Dismisses the session-restore dialog if present.</summary>
    public void DismissSessionRestoreDialogIfPresent(int timeoutMs = 5_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var discard = Driver!.FindElements(MobileBy.Name("Discard"));
                if (discard.Count > 0 && discard[0].Displayed)
                {
                    discard[0].Click();
                    Thread.Sleep(300);
                    return;
                }
            }
            catch { }
            Thread.Sleep(200);
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

    /// <summary>Sets clipboard text on the remote machine via the Appium session.</summary>
    private void SetClipboardText(string text)
    {
        // Use PowerShell on the remote machine to set clipboard
        var remoteHost = Environment.GetEnvironmentVariable("SMRTPAD_REMOTE_HOST") ?? "192.168.0.100";
        var remoteUser = Environment.GetEnvironmentVariable("UITEST_REMOTE_WINRM_USERNAME")
            ?? Environment.GetEnvironmentVariable("SMRTPAD_REMOTE_USER");
        var remotePassword = Environment.GetEnvironmentVariable("UITEST_REMOTE_WINRM_PASSWORD")
            ?? Environment.GetEnvironmentVariable("SMRTPAD_REMOTE_PASS");

        var escaped = text.Replace("'", "''");

        var credentialSetup = "";
        if (!string.IsNullOrWhiteSpace(remoteUser) && !string.IsNullOrWhiteSpace(remotePassword))
        {
            var escapedPass = remotePassword.Replace("'", "''");
            credentialSetup = $"$secPass = ConvertTo-SecureString '{escapedPass}' -AsPlainText -Force; " +
                              $"$cred = [pscredential]::new('{remoteUser}', $secPass); ";
        }

        var credArg = string.IsNullOrWhiteSpace(credentialSetup) ? "" : " -Credential $cred";

        // Set-Clipboard requires an interactive session — use a scheduled task like deploy.ps1
        var script = $"""
            $scriptPath = Join-Path $env:TEMP 'SmrtPadSetClipboard.ps1'
            Set-Content -LiteralPath $scriptPath -Value "Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Clipboard]::SetText('{escaped}')" -Encoding UTF8 -Force
            $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$scriptPath`""
            $principal = New-ScheduledTaskPrincipal -UserId '{remoteUser}' -LogonType Interactive
            Register-ScheduledTask -TaskName 'SmrtPadClipboard' -Action $action -Principal $principal -Force | Out-Null
            Start-ScheduledTask -TaskName 'SmrtPadClipboard'
            Start-Sleep -Seconds 2
            Unregister-ScheduledTask -TaskName 'SmrtPadClipboard' -Confirm:$false -ErrorAction SilentlyContinue
            """;

        var command = $"{credentialSetup}Invoke-Command -ComputerName '{remoteHost}'{credArg} -ScriptBlock {{ {script} }}";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

        var psi = new ProcessStartInfo("powershell.exe",
            $"-NoProfile -NonInteractive -EncodedCommand {encoded}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process is not null)
        {
            var exited = process.WaitForExit(15_000);
            if (!exited) try { process.Kill(entireProcessTree: true); } catch { }
        }
    }

    /// <summary>Returns the status text from the sidebar.</summary>
    public string GetStatusBarText(string automationId)
    {
        var els = Driver!.FindElements(MobileBy.AccessibilityId(automationId));
        return els.Count > 0 ? els[0].Text : string.Empty;
    }

    private static void ClearRemoteFreeTierFlag(string remoteHost, string? remoteUser, string? remotePassword)
    {
        var credentialSetup = "";
        if (!string.IsNullOrWhiteSpace(remoteUser) && !string.IsNullOrWhiteSpace(remotePassword))
        {
            var escapedPass = remotePassword.Replace("'", "''");
            credentialSetup = $"$secPass = ConvertTo-SecureString '{escapedPass}' -AsPlainText -Force; " +
                              $"$cred = [pscredential]::new('{remoteUser}', $secPass); ";
        }

        var credArg = string.IsNullOrWhiteSpace(credentialSetup) ? "" : " -Credential $cred";
        var script = """
            $flagPath = Join-Path $env:USERPROFILE 'SmrtPad_FreeTier.flag'
            if (Test-Path $flagPath) {
                Remove-Item -LiteralPath $flagPath -Force -ErrorAction SilentlyContinue
            }
            """;

        var command = $"{credentialSetup}Invoke-Command -ComputerName '{remoteHost}'{credArg} -ScriptBlock {{ {script} }}";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
        var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -EncodedCommand {encoded}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process is not null)
        {
            var exited = process.WaitForExit(10_000);
            if (!exited) try { process.Kill(entireProcessTree: true); } catch { }
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
        Driver = null;
    }
}
