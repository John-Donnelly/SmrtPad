using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Win32;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

namespace SmrtPad.UITests.Infrastructure
{
    /// <summary>
    /// Creates and owns a single WinAppDriver / Appium-Windows-Driver session for a test class.
    /// Call <see cref="IsAvailable"/> before attempting to create a session — all UI tests are
    /// decorated with <c>[SkippableFact]</c> and skip gracefully when the server is absent.
    ///
    /// The app is launched via <c>IApplicationActivationManager</c> (AUMID / package activation)
    /// so that it receives its MSIX package identity, which WinUI 3 (3.1.8+) requires at startup.
    /// After launch, the session attaches via the <c>appTopLevelWindow</c> capability because
    /// WinAppDriver 1.2.1 hangs when asked to launch WinUI 3 apps directly.
    ///
    /// Setup requirements (one-time, developer machine or CI agent):
    ///   1. Install Node / npm and Appium 2.x:  npm install -g appium
    ///   2. Install the windows driver:          appium driver install windows
    ///   3. Install WinAppDriver 1.2.1 from https://github.com/microsoft/WinAppDriver/releases
    ///   4. Start the Appium server:             appium  (listens on http://127.0.0.1:4723)
    ///   5. Build the WAP project (SmrtPad (Package)) in Debug|x64 once so the AppX folder is
    ///      registered; re-run after every rebuild of SmrtPad.
    /// </summary>
    public sealed class AppiumSession : IDisposable
    {
        private const string ServerUrl      = "http://127.0.0.1:4723";
        private const string StatusEndpoint = ServerUrl + "/status";

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(2) };

        private readonly int _launchedPid;

        public WindowsDriver Driver { get; }

        /// <summary>
        /// Launches SmrtPad and attaches an Appium session.
        /// When <paramref name="launchArgument"/> is supplied (e.g. a file path to open on startup)
        /// it is forwarded to AUMID activation so the packaged app retains its identity while
        /// also receiving the argument through <c>Environment.GetCommandLineArgs()</c>.
        /// The exe-direct fallback is only used when AUMID activation is unavailable or fails.
        /// </summary>
        public AppiumSession(string appPath, string? launchArgument = null)
        {
            Process process;
            bool usedAumid = false;
            string? aumid = FindWapAumid(appPath);
            if (!string.IsNullOrWhiteSpace(aumid))
            {
                try
                {
                    _launchedPid = ActivateApplication(aumid, launchArgument);
                    process = Process.GetProcessById(_launchedPid);
                    usedAumid = true;
                }
                catch (InvalidOperationException ex)
                {
                    process = StartUnpackaged(appPath, ex, launchArgument);
                    _launchedPid = process.Id;
                }
            }
            else
            {
                process = StartUnpackaged(appPath, null, launchArgument);
                _launchedPid = process.Id;
            }

            // Wait for the main WinUI 3 window (up to 30 s).
            // If the AUMID-activated process exits before a window appears (e.g. stale package),
            // fall back to launching the exe directly.
            nint hwnd;
            try
            {
                hwnd = WaitForMainWindow(process, TimeSpan.FromSeconds(30));
            }
            catch (InvalidOperationException) when (usedAumid)
            {
                process = StartUnpackaged(appPath, null);
                _launchedPid = process.Id;
                hwnd = WaitForMainWindow(process, TimeSpan.FromSeconds(30));
            }

            var options = new AppiumOptions();
            options.PlatformName   = "Windows";
            options.AutomationName = "Windows";
            options.AddAdditionalAppiumOption("appTopLevelWindow", $"0x{hwnd:X}");

            Driver = new WindowsDriver(new Uri(ServerUrl), options,
                TimeSpan.FromSeconds(30));
        }

        private static Process StartUnpackaged(string appPath, Exception? activationException, string? argument = null)
        {
            var startInfo = new ProcessStartInfo(appPath)
            {
                UseShellExecute = true
            };
            if (!string.IsNullOrEmpty(argument))
                startInfo.Arguments = $"\"{argument}\"";

            var process = Process.Start(startInfo);
            if (process is null)
            {
                string message = activationException is null
                    ? "Failed to start SmrtPad.exe for UI automation."
                    : $"Failed to start SmrtPad.exe after package activation failed: {activationException.Message}";
                throw new InvalidOperationException(message, activationException);
            }

            return process;
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
                if (doc.RootElement.TryGetProperty("value", out var val) &&
                    val.TryGetProperty("ready", out var ready))
                    return ready.GetBoolean();

                if (doc.RootElement.TryGetProperty("status", out var status))
                    return status.GetInt32() == 0;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Searches the solution tree for the SmrtPad.exe build artefact (main project output).
        /// Returns <c>null</c> if not found (app not yet built).
        /// </summary>
        public static string? FindSmrtPadExe()
        {
            string? dir = AppContext.BaseDirectory;
            for (int i = 0; i < 12 && dir is not null; i++)
            {
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

            try
            {
                var p = Process.GetProcessById(_launchedPid);
                if (!p.HasExited)
                    p.Kill(entireProcessTree: true);
            }
            catch { }
        }

        // ── WAP package AUMID resolution ─────────────────────────────────────────

        /// <summary>
        /// Finds the AUMID of the installed WAP package by locating the WAP AppxManifest.xml
        /// in the solution tree, then looking up the matching registered package in the Windows
        /// AppModel repository registry key.
        /// Returns <c>null</c> when the WAP package directory or registration is absent.
        /// </summary>
        private static string? FindWapAumid(string mainExePath)
        {
            // Walk up from the main exe to find the WAP AppX directory.
            string? dir = Path.GetDirectoryName(mainExePath);
            for (int i = 0; i < 15 && dir is not null; i++)
            {
                string manifestPath = Path.Combine(dir,
                    "SmrtPad (Package)", "bin", "x64", "Debug", "AppX", "AppxManifest.xml");
                if (File.Exists(manifestPath))
                    return FindAumidFromRegistry(manifestPath);

                dir = Directory.GetParent(dir)?.FullName;
            }
            return null;
        }

        /// <summary>
        /// Reads the package Name and Application Id from <paramref name="manifestPath"/>,
        /// then searches the Windows AppModel package registry for a matching package family
        /// and returns its AUMID.
        /// </summary>
        private static string? FindAumidFromRegistry(string manifestPath)
        {
            try
            {
                var ns   = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
                var doc  = XDocument.Load(manifestPath);
                var root = doc.Root;
                if (root is null) return null;

                string name  = root.Element(XName.Get("Identity", ns))?.Attribute("Name")?.Value ?? "";
                string appId = root.Element(XName.Get("Applications", ns))
                                   ?.Element(XName.Get("Application", ns))
                                   ?.Attribute("Id")?.Value ?? "App";

                if (string.IsNullOrEmpty(name)) return null;

                // Windows registers installed packages at this registry path.
                const string repoKey =
                    @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion" +
                    @"\AppModel\Repository\Packages";

                using var key = Registry.CurrentUser.OpenSubKey(repoKey);
                if (key is null) return null;

                foreach (string pkgFullName in key.GetSubKeyNames())
                {
                    // PackageFullName = "{Name}_{Version}_{Arch}_{ResourceId}_{PublisherHash}"
                    // or "{Name}_{Version}_{Arch}_{PublisherHash}" — split from the right.
                    if (!pkgFullName.StartsWith(name + "_", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Derive the PackageFamilyName: "{Name}_{last_segment_of_full_name}".
                    int lastUnderscore = pkgFullName.LastIndexOf('_');
                    if (lastUnderscore < 0) continue;

                    string familyName = $"{name}_{pkgFullName[(lastUnderscore + 1)..]}";
                    return $"{familyName}!{appId}";
                }
            }
            catch
            {
                // Registry access or XML parse failure → return null so the test skips.
            }
            return null;
        }

        // ── IApplicationActivationManager ────────────────────────────────────────

        [ComImport]
        [Guid("2e941141-7f97-4756-ba1d-9decde894a3d")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IApplicationActivationManager
        {
            int ActivateApplication(
                [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
                [MarshalAs(UnmanagedType.LPWStr)] string? arguments,
                uint options,
                out uint processId);

            int ActivateForFile(
                [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
                nint pSFI,
                [MarshalAs(UnmanagedType.LPWStr)] string verb,
                out uint processId);

            int ActivateForProtocol(
                [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
                nint eventArgs,
                out uint processId);
        }

        [ComImport]
        [Guid("45ba127d-10a8-46ea-8ab7-56ea9078943c")]
        [ClassInterface(ClassInterfaceType.None)]
        private class ApplicationActivationManagerClass { }

        /// <summary>
        /// Activates the app via its AUMID and returns the process ID.
        /// When <paramref name="argument"/> is supplied it is forwarded to the activation call
        /// so the app receives it via <c>Environment.GetCommandLineArgs()</c>.
        /// Throws <see cref="InvalidOperationException"/> if COM activation fails.
        /// </summary>
        private static int ActivateApplication(string aumid, string? argument = null)
        {
            var manager = (IApplicationActivationManager)
                Activator.CreateInstance(typeof(ApplicationActivationManagerClass))!;

            int hr = manager.ActivateApplication(aumid, argument, 0, out uint pid);
            if (hr < 0)
                throw new InvalidOperationException(
                    $"IApplicationActivationManager.ActivateApplication failed for AUMID '{aumid}'" +
                    $" with HRESULT 0x{hr:X8}.");

            return (int)pid;
        }

        // ── WinUI 3 window detection ─────────────────────────────────────────────

        /// <summary>
        /// Polls until a visible top-level window belonging to the process appears.
        /// Uses <c>EnumWindows</c> instead of <c>Process.MainWindowHandle</c> because
        /// WinUI 3 windows do not always register as the Win32 "main window".
        /// </summary>
        private static nint WaitForMainWindow(Process process, TimeSpan timeout)
        {
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < timeout)
            {
                process.Refresh();
                if (process.HasExited)
                {
                    string exitInfo;
                    try
                    {
                        exitInfo = $" (exit code {process.ExitCode})";
                    }
                    catch (InvalidOperationException ex)
                    {
                        exitInfo = " (exit code unavailable)";
                        throw new InvalidOperationException(
                            $"App exited before a window appeared{exitInfo}.", ex);
                    }

                    throw new InvalidOperationException(
                        $"App exited before a window appeared{exitInfo}.");
                }

                nint hwnd = FindTopLevelWindowForProcess(process.Id);
                if (hwnd != 0)
                    return hwnd;

                Thread.Sleep(500);
            }

            throw new TimeoutException(
                $"App did not produce a main window within {timeout.TotalSeconds}s.");
        }

        private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(nint hWnd);

        private static nint FindTopLevelWindowForProcess(int processId)
        {
            nint found = 0;
            EnumWindows((hWnd, _) =>
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == (uint)processId && IsWindowVisible(hWnd))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, 0);
            return found;
        }
    }
}
