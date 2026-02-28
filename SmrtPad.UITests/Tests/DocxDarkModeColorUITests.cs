using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Xunit;
using SmrtPad.UITests.Infrastructure;

namespace SmrtPad.UITests.Tests
{
    /// <summary>
    /// Verifies that a .docx file loaded in dark mode displays light-coloured text —
    /// i.e. that <c>NormalizeDocumentColorsForTheme</c> correctly resets the uniform
    /// explicit black introduced by <c>DocxImportHelper</c> to auto-colour so the
    /// WinUI 3 dark-mode foreground brush takes effect.
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
        // ── Settings file path (same location as SettingsService uses) ─────────

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmrtPad", "settings.json");

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
        private readonly string?        _originalSettings;

        public WindowsDriver? Driver        { get; }
        public bool           IsAvailable   => Driver is not null;
        public string?        DocxPath      { get; }
        /// <summary>Populated when <see cref="IsAvailable"/> is false; explains why.</summary>
        public string         SkipReason    { get; private set; } = "";

        public DocxDarkModeFixture()
        {
            if (!AppiumSession.IsAvailable()) { SkipReason = "Appium server not reachable on port 4723."; return; }

            string? exe  = AppiumSession.FindSmrtPadExe();
            if (exe is null)            { SkipReason = "SmrtPad.exe not found (build the project first)."; return; }

            string? docx = FindDocx();
            if (docx is null)           { SkipReason = $"DOCX not found. Tried OneDrive/Documents/Desktop for 'CelestiPets Business Plan 2026.docx'."; return; }

            DocxPath = docx;

            try
            {
                // Backup and patch settings → Dark theme
                _originalSettings = File.Exists(SettingsPath)
                    ? File.ReadAllText(SettingsPath)
                    : null;

                SetThemeInSettings(SettingsPath, "Dark");

                _session = new AppiumSession(exe, docx);
                Driver   = _session.Driver;
            }
            catch (Exception ex)
            {
                SkipReason = $"AppiumSession failed: {ex.GetType().Name}: {ex.Message}";
                RestoreSettings();
            }
        }

        public void Dispose()
        {
            try { _session?.Dispose(); } catch { }
            RestoreSettings();
        }

        // ── Settings helpers ───────────────────────────────────────────────────

        private void RestoreSettings()
        {
            try
            {
                if (_originalSettings is not null)
                    File.WriteAllText(SettingsPath, _originalSettings);
            }
            catch { }
        }

        /// <summary>
        /// Reads (or creates) the SmrtPad settings JSON and writes the
        /// <c>ThemePreference</c> key without touching any other values.
        /// </summary>
        private static void SetThemeInSettings(string path, string theme)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            JsonNode root = File.Exists(path)
                ? JsonNode.Parse(File.ReadAllText(path)) ?? new JsonObject()
                : new JsonObject();

            root["ThemePreference"] = theme;

            File.WriteAllText(path, root.ToJsonString(
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
    }

    /// <summary>
    /// Tests in this class each get a fresh SmrtPad session launched in dark mode
    /// with the CelestiPets Business Plan DOCX open. Tests skip gracefully when
    /// Appium, the executable, or the DOCX file is unavailable.
    /// </summary>
    public sealed class DocxDarkModeColorUITests
        : IClassFixture<DocxDarkModeFixture>, IDisposable
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

        // ── Tests ─────────────────────────────────────────────────────────────

        /// <summary>
        /// After opening the DOCX in dark mode the status bar must confirm the
        /// file was loaded (not crash, not stay as Untitled).
        /// </summary>
        [SkippableFact]
        public void DocxOpenInDarkMode_StatusBar_ShowsOpenedMessage()
        {
            RequireAvailable();

            string status = WaitForStatus("Opened:", 20_000);
            Assert.Contains("Opened:", status, StringComparison.OrdinalIgnoreCase);
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
            WaitForStatus("Opened:", 20_000);
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

            // Wait for the file to finish loading (status bar says "Opened: ...")
            string status = WaitForStatus("Opened:", 20_000);
            Assert.Contains("Opened:", status, StringComparison.OrdinalIgnoreCase);

            // Give NormalizeDocumentColorsForTheme (double TryEnqueue) time to run
            // and the renderer time to paint the updated colours.
            Thread.Sleep(1_500);

            // Scroll to the top so text is visible in the viewport
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
            catch { /* non-fatal — proceed with screenshot */ }

            int maxBrightness = MaxEditorBrightness();

            // Save screenshot for debugging when the assertion fails
            if (maxBrightness <= 150)
            {
                try
                {
                    string debugPath = Path.Combine(
                        Path.GetTempPath(),
                        $"smrtpad_dark_mode_test_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                    _driver!.GetScreenshot().SaveAsFile(debugPath);
                    Console.WriteLine($"[DEBUG] Screenshot saved to: {debugPath}");
                }
                catch { }
            }

            Assert.True(maxBrightness > 150,
                $"Expected light-coloured text in dark mode (max pixel brightness > 150) " +
                $"but the highest brightness found in the editor was {maxBrightness}/255. " +
                $"This indicates the document text is still being rendered in black " +
                $"(dark text on dark background). " +
                $"NormalizeDocumentColorsForTheme may not have detected or reset the " +
                $"uniform black colour introduced by DocxImportHelper.");
        }
    }
}
