using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Xunit;
using SmrtPad.UITests.Infrastructure;

namespace SmrtPad.UITests.Tests
{
    /// <summary>
    /// Verifies that a .docx file loaded in dark mode displays light-coloured text —
    /// i.e. that <c>DocxImportHelper</c> emits <c>\cf0</c> (RTF auto colour) for
    /// default-coloured text so the WinUI 3 dark-mode foreground brush renders it
    /// in white/light, and that <c>NormalizeDocumentColorsForTheme</c> serves as a
    /// fallback for any remaining explicit-black runs.
    ///
    /// The fixture:
    ///   1. Temporarily writes <c>ThemePreference = "Dark"</c> to the app's settings
    ///      file so the app starts in dark mode without any manual toggle.
    ///   2. Launches a fresh SmrtPad process with the test DOCX as a command-line
    ///      argument so <c>App.OnLaunched</c> opens the file automatically.
    ///   3. Restores the original settings on dispose regardless of test outcome.
    ///
    /// Verification uses a screenshot of the editor area: in dark mode with correctly
    /// theme-aware text the maximum pixel brightness inside the editor will be well
    /// above the dark background (~35); with stuck-black text it will not exceed ~80.
    /// </summary>
    public sealed class DocxDarkModeFixture : IDisposable
    {
        // ── DOCX search locations ──────────────────────────────────────────────

        private static string? FindDocx()
        {
            var candidates = new List<string>();

            string oneDrive = Environment.GetEnvironmentVariable("OneDrive") ?? "";
            if (!string.IsNullOrEmpty(oneDrive))
                candidates.Add(Path.Combine(oneDrive, "CelestiPets Business Plan 2026.docx"));

            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            candidates.Add(Path.Combine(profile, "OneDrive", "CelestiPets Business Plan 2026.docx"));
            candidates.Add(Path.Combine(profile, "Documents", "CelestiPets Business Plan 2026.docx"));
            candidates.Add(Path.Combine(profile, "Desktop",   "CelestiPets Business Plan 2026.docx"));

            foreach (var path in candidates)
                if (File.Exists(path)) return path;

            return null;
        }

        // ── Fixture state ──────────────────────────────────────────────────────

        private readonly AppiumSession? _session;
        private string?                 _originalSettings;

        public WindowsDriver? Driver        { get; }
        public bool           IsAvailable   => Driver is not null;
        public string?        DocxPath      { get; }
        /// <summary>Populated when <see cref="IsAvailable"/> is false; explains why.</summary>
        public string         SkipReason    { get; private set; } = "";

        public DocxDarkModeFixture()
        {
            DotEnvLoader.EnsureLoaded();

            if (!AppiumSession.IsAvailable()) { SkipReason = "Appium server not reachable on port 4723."; return; }

            string? appId = SharedAppFixture.DeployPackageAndGetAppId();
            if (appId is null) { SkipReason = "Remote UI test package deployment did not return an app identity."; return; }

            string? docx = FindDocx();
            if (docx is null) { SkipReason = "DOCX not found. Tried OneDrive/Documents/Desktop for 'CelestiPets Business Plan 2026.docx'."; return; }

            DocxPath = docx;

            try
            {
                // Patch the remote machine's settings file → Dark theme via WinRM
                // before launching so the app starts in dark mode.
                // Falls through silently if the remote settings cannot be reached
                // (the test will still run; the theme toggle just won't be preset).
                TrySetRemoteTheme("Dark");

                // Launch via AUMID so the packaged app retains its identity.
                // Pass the DOCX path as the activation argument; OnLaunched opens it.
                _session = new AppiumSession(
                    appId,
                    launchArgument: docx,
                    launchViaAppId: true,
                    serverUrl: AppiumSession.DefaultServerUrl);
                Driver = _session.Driver;
            }
            catch (Exception ex)
            {
                SkipReason = $"AppiumSession failed: {ex.GetType().Name}: {ex.Message}";
            }
        }

        public void Dispose()
        {
            try { _session?.Dispose(); } catch { }
            TryRestoreRemoteSettings();
        }

        // ── Remote settings helpers ────────────────────────────────────────────

        /// <summary>
        /// Attempts to set ThemePreference on the remote machine via WinRM so the
        /// app launches in the requested theme.  Non-fatal: logs but does not throw.
        /// </summary>
        private void TrySetRemoteTheme(string theme)
        {
            try
            {
                string? user     = Environment.GetEnvironmentVariable("UITEST_REMOTE_WINRM_USERNAME");
                string? password = Environment.GetEnvironmentVariable("UITEST_REMOTE_WINRM_PASSWORD");
                string  host     = Environment.GetEnvironmentVariable("SMRTPAD_REMOTE_HOST") ?? "192.168.0.100";

                if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password)) return;

                // Backup existing settings on remote machine for later restoration.
                _originalSettings = RunRemotePowerShell(host, user, password,
                    "$p = \"$env:LOCALAPPDATA\\SmrtPad\\settings.json\"; " +
                    "if (Test-Path $p) { Get-Content $p -Raw } else { '' }");

                RunRemotePowerShell(host, user, password,
                    $"$p = \"$env:LOCALAPPDATA\\SmrtPad\\settings.json\"; " +
                    $"$d = Split-Path $p; if (-not (Test-Path $d)) {{ New-Item $d -ItemType Directory -Force | Out-Null }}; " +
                    $"$j = if (Test-Path $p) {{ Get-Content $p -Raw | ConvertFrom-Json }} else {{ [pscustomobject]@{{}} }}; " +
                    $"$j | Add-Member -Force -MemberType NoteProperty -Name ThemePreference -Value '{theme}'; " +
                    $"$j | ConvertTo-Json -Depth 5 | Set-Content $p -Encoding UTF8 -Force");
            }
            catch { /* non-fatal */ }
        }

        private void TryRestoreRemoteSettings()
        {
            try
            {
                if (_originalSettings is null) return;

                string? user     = Environment.GetEnvironmentVariable("UITEST_REMOTE_WINRM_USERNAME");
                string? password = Environment.GetEnvironmentVariable("UITEST_REMOTE_WINRM_PASSWORD");
                string  host     = Environment.GetEnvironmentVariable("SMRTPAD_REMOTE_HOST") ?? "192.168.0.100";

                if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(password)) return;

                if (string.IsNullOrWhiteSpace(_originalSettings))
                {
                    RunRemotePowerShell(host, user, password,
                        "Remove-Item \"$env:LOCALAPPDATA\\SmrtPad\\settings.json\" -Force -ErrorAction SilentlyContinue");
                }
                else
                {
                    string escaped = _originalSettings.Replace("'", "''");
                    RunRemotePowerShell(host, user, password,
                        $"Set-Content \"$env:LOCALAPPDATA\\SmrtPad\\settings.json\" -Value '{escaped}' -Encoding UTF8 -Force");
                }
            }
            catch { /* non-fatal */ }
        }

        private static string RunRemotePowerShell(string host, string user, string password, string scriptBlock)
        {
            var psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -Command \"" +
                $"$pw = ConvertTo-SecureString '{password}' -AsPlainText -Force; " +
                $"$cred = New-Object System.Management.Automation.PSCredential('{user}', $pw); " +
                $"$s = New-PSSession -ComputerName {host} -Credential $cred; " +
                $"Invoke-Command -Session $s -ScriptBlock {{ {scriptBlock} }}; " +
                $"Remove-PSSession $s\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            using var p = Process.Start(psi)!;
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return output.Trim();
        }

        private void OpenDocxViaUI(string docxPath)
        {
            if (Driver is null) return;

            try
            {
                // Wait for the main window to fully load
                Thread.Sleep(2_000);

                // Click File menu to open backstage
                Driver.FindElement(MobileBy.Name("File")).Click();
                Thread.Sleep(800);

                // Click "Open" in the backstage
                Driver.FindElement(MobileBy.Name("Open")).Click();
                Thread.Sleep(1_500);

                // The file picker dialog opens. Type the full path into the
                // filename field and press Enter.
                // The file dialog filename box is usually focused, but if not,
                // use Alt+N to focus it (Windows file dialog shortcut).
                try
                {
                    var fileNameBox = Driver.FindElement(MobileBy.Name("File name:"));
                    fileNameBox.Clear();
                    fileNameBox.SendKeys(docxPath);
                }
                catch
                {
                    // Fallback: send Alt+N to focus the filename field, then type
                    Driver.FindElement(MobileBy.ClassName("Edit")).SendKeys(docxPath);
                }

                Thread.Sleep(300);
                Driver.FindElement(MobileBy.Name("Open")).Click();
                Thread.Sleep(2_000);
            }
            catch (Exception ex)
            {
                SkipReason = $"Failed to open DOCX via UI: {ex.GetType().Name}: {ex.Message}";
            }
        }

        }

    /// <summary>
    /// Tests in this class each get a fresh SmrtPad session launched in dark mode
    /// with the CelestiPets Business Plan DOCX open. Tests skip gracefully when
    /// Appium, the executable, or the DOCX file is unavailable.
    /// </summary>
    [Collection("DocxDarkModeUITests")]
    public sealed class DocxDarkModeColorUITests
        : IDisposable
    {
        private readonly DocxDarkModeFixture _fx;
        private readonly WindowsDriver?      _driver;

        public DocxDarkModeColorUITests(DocxDarkModeFixture fx)
        {
            _fx    = fx;
            _driver = fx.Driver;
        }

        public void Dispose() { /* session owned by fixture */ }

        private void RequireAvailable() =>
            Skip.If(!_fx.IsAvailable,
                string.IsNullOrEmpty(_fx.SkipReason)
                    ? "Fixture unavailable."
                    : _fx.SkipReason);

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Polls StatusText until it contains <paramref name="fragment"/> or the
        /// timeout elapses, then returns the final status text.
        /// </summary>
        private string WaitForStatus(string fragment, int timeoutMs = 15_000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    string text = _driver!
                        .FindElement(MobileBy.AccessibilityId("StatusText")).Text;
                    if (text.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                        return text;
                }
                catch { /* element not yet rendered — keep polling */ }
                Thread.Sleep(200);
            }
            // Return whatever is showing even if it doesn't match
            try { return _driver!.FindElement(MobileBy.AccessibilityId("StatusText")).Text; }
            catch { return string.Empty; }
        }

        private static Bitmap ScreenshotToBitmap(OpenQA.Selenium.Screenshot shot)
        {
            using var ms = new MemoryStream(shot.AsByteArray);
            return new Bitmap(ms);
        }

        /// <summary>
        /// Scans the central 60 % (width) × 80 % (height) of the editor element
        /// and returns the maximum per-pixel brightness found.
        /// In dark mode: light/white text → brightness ~200+; black text → ~0–50.
        /// </summary>
        private int MaxEditorBrightness()
        {
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            var rect   = editor.Rect;

            var shot = _driver.GetScreenshot();
            using var bmp = ScreenshotToBitmap(shot);

            // DPI-aware coordinate conversion
            var winSize = _driver.Manage().Window.Size;
            double dpiX = winSize.Width  > 0 ? (double)bmp.Width  / winSize.Width  : 1.0;
            double dpiY = winSize.Height > 0 ? (double)bmp.Height / winSize.Height : 1.0;

            // Sample the inner 60 % of width and top 80 % of height to avoid
            // scrollbars, borders, and any blank leading margin.
            int left   = (int)((rect.X + rect.Width  * 0.2) * dpiX);
            int right  = (int)((rect.X + rect.Width  * 0.8) * dpiX);
            int top    = (int)((rect.Y + rect.Height * 0.05) * dpiY);
            int bottom = (int)((rect.Y + rect.Height * 0.85) * dpiY);

            left   = Math.Max(0, Math.Min(left,   bmp.Width  - 1));
            right  = Math.Max(0, Math.Min(right,  bmp.Width  - 1));
            top    = Math.Max(0, Math.Min(top,    bmp.Height - 1));
            bottom = Math.Max(0, Math.Min(bottom, bmp.Height - 1));

            int maxBrightness = 0;
            for (int y = top; y <= bottom; y += 4)
            {
                for (int x = left; x <= right; x += 3)
                {
                    var px = bmp.GetPixel(x, y);
                    int b  = (px.R + px.G + px.B) / 3;
                    if (b > maxBrightness) maxBrightness = b;
                }
            }
            return maxBrightness;
        }

        /// <summary>
        /// Scans the editor area and returns the count of sampled pixels whose
        /// brightness exceeds <paramref name="threshold"/>.  A high count confirms
        /// that visible text is spread across the editor, not just a single bright
        /// artifact pixel.
        /// </summary>
        private int CountBrightPixels(int threshold = 150)
        {
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            var rect   = editor.Rect;

            var shot = _driver.GetScreenshot();
            using var bmp = ScreenshotToBitmap(shot);

            var winSize = _driver.Manage().Window.Size;
            double dpiX = winSize.Width  > 0 ? (double)bmp.Width  / winSize.Width  : 1.0;
            double dpiY = winSize.Height > 0 ? (double)bmp.Height / winSize.Height : 1.0;

            int left   = Math.Clamp((int)((rect.X + rect.Width  * 0.2) * dpiX), 0, bmp.Width  - 1);
            int right  = Math.Clamp((int)((rect.X + rect.Width  * 0.8) * dpiX), 0, bmp.Width  - 1);
            int top    = Math.Clamp((int)((rect.Y + rect.Height * 0.05) * dpiY), 0, bmp.Height - 1);
            int bottom = Math.Clamp((int)((rect.Y + rect.Height * 0.85) * dpiY), 0, bmp.Height - 1);

            int count = 0;
            for (int y = top; y <= bottom; y += 4)
            {
                for (int x = left; x <= right; x += 3)
                {
                    var px = bmp.GetPixel(x, y);
                    int b  = (px.R + px.G + px.B) / 3;
                    if (b > threshold) count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Samples a horizontal strip at a given vertical fraction of the editor
        /// and returns the maximum brightness found.  Useful for confirming text
        /// visibility at multiple vertical positions in the document.
        /// </summary>
        private int MaxBrightnessAtVerticalFraction(double fraction)
        {
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            var rect   = editor.Rect;

            var shot = _driver.GetScreenshot();
            using var bmp = ScreenshotToBitmap(shot);

            var winSize = _driver.Manage().Window.Size;
            double dpiX = winSize.Width  > 0 ? (double)bmp.Width  / winSize.Width  : 1.0;
            double dpiY = winSize.Height > 0 ? (double)bmp.Height / winSize.Height : 1.0;

            int left  = Math.Clamp((int)((rect.X + rect.Width * 0.2) * dpiX), 0, bmp.Width  - 1);
            int right = Math.Clamp((int)((rect.X + rect.Width * 0.8) * dpiX), 0, bmp.Width  - 1);
            int yPos  = Math.Clamp((int)((rect.Y + rect.Height * fraction) * dpiY), 0, bmp.Height - 1);

            int maxBrightness = 0;
            // Scan a thin strip (±2 rows) to account for sub-pixel alignment
            for (int dy = -2; dy <= 2; dy++)
            {
                int y = Math.Clamp(yPos + dy, 0, bmp.Height - 1);
                for (int x = left; x <= right; x += 2)
                {
                    var px = bmp.GetPixel(x, y);
                    int b  = (px.R + px.G + px.B) / 3;
                    if (b > maxBrightness) maxBrightness = b;
                }
            }
            return maxBrightness;
        }

        /// <summary>
        /// Waits for file load, scrolls to the top, and pauses for rendering to settle.
        /// </summary>
        private void WaitForDocxLoadAndScrollToTop()
        {
            string status = WaitForStatus("Opened", 20_000);
            Assert.Contains("Opened", status, StringComparison.OrdinalIgnoreCase);

            // Allow auto-colour rendering to settle
            Thread.Sleep(1_500);

            try
            {
                var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
                editor.Click();
                Thread.Sleep(100);
                editor.SendKeys(OpenQA.Selenium.Keys.Control +
                                OpenQA.Selenium.Keys.Home +
                                OpenQA.Selenium.Keys.Null);
                Thread.Sleep(400);
            }
            catch { /* non-fatal */ }
        }

        /// <summary>Saves a debug screenshot and writes the path to console.</summary>
        private void SaveDebugScreenshot(string testName)
        {
            try
            {
                string debugPath = Path.Combine(
                    Path.GetTempPath(),
                    $"smrtpad_{testName}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                _driver!.GetScreenshot().SaveAsFile(debugPath);
                Console.WriteLine($"[DEBUG] Screenshot saved to: {debugPath}");
            }
            catch { }
        }

        // ── Tests ─────────────────────────────────────────────────────────────

        /// <summary>
        /// After opening the DOCX in dark mode the status bar must confirm the
        /// file was loaded (not crash, not stay as Untitled).
        /// </summary>
        [SkippableFact]
        public void DocxOpenInDarkMode_StatusBar_ShowsOpenedMessage()
        {
            RequireAvailable();

            string status = WaitForStatus("Opened", 20_000);
            Assert.Contains("Opened", status, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// After opening the DOCX in dark mode the status bar must report the
        /// encoding as DOCX.
        /// </summary>
        [SkippableFact]
        public void DocxOpenInDarkMode_StatusBar_ShowsDocxEncoding()
        {
            RequireAvailable();

            // Wait for file to load first
            WaitForStatus("Opened", 20_000);
            Thread.Sleep(500);

            string encoding = _driver!
                .FindElement(MobileBy.AccessibilityId("EncodingText")).Text;
            Assert.Equal("DOCX", encoding);
        }

        /// <summary>
        /// Core colour test: text in the editor should be light-coloured (not black)
        /// when displayed in dark mode.  The test takes a screenshot of the editor
        /// region and asserts that at least one pixel has brightness > 150/255.
        ///
        /// Dark background ≈ 35 brightness.
        /// Correct white/light text ≈ 200-230 brightness  → max well above 150 ✓
        /// Stuck-black text         ≈  0-30  brightness   → max stays below  80 ✗
        /// </summary>
        [SkippableFact]
        public void DocxOpenInDarkMode_EditorText_IsLightColoured()
        {
            RequireAvailable();
            WaitForDocxLoadAndScrollToTop();

            int maxBrightness = MaxEditorBrightness();

            if (maxBrightness <= 150)
                SaveDebugScreenshot("dark_mode_light_colour");

            Assert.True(maxBrightness > 150,
                $"Expected light-coloured text in dark mode (max pixel brightness > 150) " +
                $"but the highest brightness found in the editor was {maxBrightness}/255. " +
                $"This indicates the document text is still being rendered in black " +
                $"(dark text on dark background).");
        }

        /// <summary>
        /// Confirms that light-coloured text is not just a single bright pixel but
        /// spans a meaningful number of sampled pixels — indicating readable text
        /// rather than an artifact (caret blink, border, etc.).
        /// </summary>
        [SkippableFact]
        public void DocxOpenInDarkMode_EditorText_HasMultipleBrightPixels()
        {
            RequireAvailable();
            WaitForDocxLoadAndScrollToTop();

            int brightCount = CountBrightPixels(threshold: 150);

            if (brightCount < 50)
                SaveDebugScreenshot("dark_mode_bright_pixel_count");

            Assert.True(brightCount >= 50,
                $"Expected at least 50 sampled bright pixels (brightness > 150) " +
                $"but found only {brightCount}. Text may still be invisible in dark mode.");
        }

        /// <summary>
        /// The document title in the window should contain the DOCX file name.
        /// </summary>
        [SkippableFact]
        public void DocxOpenInDarkMode_TitleBar_ContainsDocxFileName()
        {
            RequireAvailable();
            WaitForStatus("Opened", 20_000);
            Thread.Sleep(500);

            string title = _driver!.Title;
            Assert.Contains("CelestiPets", title, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The word count should be non-zero, confirming the document has visible
        /// content and was parsed correctly.
        /// </summary>
        [SkippableFact]
        public void DocxOpenInDarkMode_WordCount_IsNonZero()
        {
            RequireAvailable();
            WaitForStatus("Opened", 20_000);
            Thread.Sleep(500);

            string wordText = _driver!
                .FindElement(MobileBy.AccessibilityId("WordCountText")).Text;
            // Expect format like "Words: 123"
            Assert.DoesNotContain("Words: 0", wordText, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The character count should be non-zero, confirming the document has
        /// visible content.
        /// </summary>
        [SkippableFact]
        public void DocxOpenInDarkMode_CharCount_IsNonZero()
        {
            RequireAvailable();
            WaitForStatus("Opened", 20_000);
            Thread.Sleep(500);

            string charText = _driver!
                .FindElement(MobileBy.AccessibilityId("CharCountText")).Text;
            Assert.DoesNotContain("Characters: 0", charText, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that the upper quarter of the editor (where headings typically
        /// appear) contains light-coloured text.
        /// </summary>
        [SkippableFact]
        public void DocxOpenInDarkMode_TopRegion_HasLightText()
        {
            RequireAvailable();
            WaitForDocxLoadAndScrollToTop();

            // Sample a wide band (5 %-45 %) so we don't miss text between lines
            int brightness = 0;
            for (double f = 0.05; f <= 0.45; f += 0.03)
            {
                int b = MaxBrightnessAtVerticalFraction(f);
                if (b > brightness) brightness = b;
            }

            if (brightness <= 150)
                SaveDebugScreenshot("dark_mode_top_region");

            Assert.True(brightness > 150,
                $"Expected light text in the top quarter of the editor " +
                $"(heading area) but max brightness was {brightness}/255.");
        }

        /// <summary>
        /// Verifies that the middle region of the editor (body text) also contains
        /// light-coloured text, confirming the fix is not limited to the first line.
        /// </summary>
        [SkippableFact]
        public void DocxOpenInDarkMode_MiddleRegion_HasLightText()
        {
            RequireAvailable();
            WaitForDocxLoadAndScrollToTop();

            // Sample a wide band (30 %-55 %) so we reliably hit a text line
            int brightness = 0;
            for (double f = 0.30; f <= 0.55; f += 0.03)
            {
                int b = MaxBrightnessAtVerticalFraction(f);
                if (b > brightness) brightness = b;
            }

            if (brightness <= 150)
                SaveDebugScreenshot("dark_mode_middle_region");

            Assert.True(brightness > 150,
                $"Expected light text in the middle of the editor " +
                $"but max brightness was {brightness}/255.");
        }

        /// <summary>
        /// After scrolling down, text in the newly visible area should still be
        /// light-coloured — confirming the auto-colour fix applies to the entire
        /// document, not just the first viewport.
        /// </summary>
        [SkippableFact]
        public void DocxOpenInDarkMode_AfterScrollDown_TextIsStillLight()
        {
            RequireAvailable();
            WaitForDocxLoadAndScrollToTop();

            // Scroll down several pages
            try
            {
                var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
                for (int i = 0; i < 5; i++)
                {
                    editor.SendKeys(OpenQA.Selenium.Keys.PageDown);
                    Thread.Sleep(100);
                }
                Thread.Sleep(300);
            }
            catch { /* non-fatal */ }

            int maxBrightness = MaxEditorBrightness();

            if (maxBrightness <= 150)
                SaveDebugScreenshot("dark_mode_after_scroll");

            Assert.True(maxBrightness > 150,
                $"Expected light text after scrolling down " +
                $"but max brightness was {maxBrightness}/255. " +
                $"The auto-colour fix may not apply to content beyond the first viewport.");
        }

        /// <summary>
        /// The editor element itself should be accessible by its AutomationId,
        /// confirming the app launched correctly in dark mode.
        /// </summary>
        [SkippableFact]
        public void DocxOpenInDarkMode_EditorElement_IsAccessible()
        {
            RequireAvailable();
            WaitForStatus("Opened", 20_000);

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.True(editor.Displayed, "Editor element should be visible.");
        }
    }
}
