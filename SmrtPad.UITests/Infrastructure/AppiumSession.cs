using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace SmrtPad.UITests.Infrastructure
{
    /// <summary>
    /// Creates and owns a single WinAppDriver / Appium-Windows-Driver session for a test class.
    /// Call <see cref="IsAvailable"/> before attempting to create a session — all UI tests are
    /// decorated with <c>[SkippableFact]</c> and skip gracefully when the server is absent.
    ///
    /// Setup requirements (one-time, developer machine or CI agent):
    ///   1. Install Node / npm and Appium 2.x:  npm install -g appium
    ///   2. Install the windows driver:          appium driver install windows
    ///   3. Install WinAppDriver 1.2.1 from https://github.com/microsoft/WinAppDriver/releases
    ///   4. Start the Appium server:             appium  (listens on http://127.0.0.1:4723)
    /// </summary>
    public sealed class AppiumSession : IDisposable
    {
        private const string ServerUrl    = "http://127.0.0.1:4723";
        private const string StatusEndpoint = ServerUrl + "/status";

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };

        public WindowsDriver Driver { get; }

        public AppiumSession(string appPath)
        {
            var options = new AppiumOptions();
            options.App            = appPath;
            options.PlatformName   = "Windows";
            options.AutomationName = "Windows"; // required by Appium 2.x / appium-windows-driver
            options.AddAdditionalAppiumOption("ms:waitForAppLaunch", 5); // seconds (max 50)

            Driver = new WindowsDriver(new Uri(ServerUrl), options,
                TimeSpan.FromSeconds(30));
        }

        /// <summary>
        /// Returns <c>true</c> when an Appium / WinAppDriver server is reachable on port 4723.
        /// </summary>
        public static bool IsAvailable()
        {
            try
            {
                using var response = _http.GetAsync(StatusEndpoint).GetAwaiter().GetResult();
                if (!response.IsSuccessStatusCode) return false;

                string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(body);
                // Appium 2.x: { "value": { "ready": true, ... } }
                // WinAppDriver: { "status": 0, ... }
                if (doc.RootElement.TryGetProperty("value", out var val) &&
                    val.TryGetProperty("ready", out var ready))
                    return ready.GetBoolean();

                if (doc.RootElement.TryGetProperty("status", out var status))
                    return status.GetInt32() == 0;

                return true; // any 2xx without known schema → assume available
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Searches the solution tree for the SmrtPad.exe build artefact.
        /// Returns <c>null</c> if not found (app not yet built).
        /// </summary>
        public static string? FindSmrtPadExe()
        {
            string? dir = AppContext.BaseDirectory;
            for (int i = 0; i < 12 && dir is not null; i++)
            {
                // Debug and Release, x64 only (CI and default dev config)
                foreach (var config in new[] { "Debug", "Release" })
                {
                    string candidate = Path.Combine(dir, "SmrtPad", "bin", "x64",
                        config, "net10.0-windows10.0.19041.0", "SmrtPad.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                dir = Directory.GetParent(dir)?.FullName;
            }
            return null;
        }

        public void Dispose()
        {
            try { Driver?.Quit(); } catch { }
        }
    }
}
