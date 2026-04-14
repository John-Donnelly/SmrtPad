using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmrtPad.Services;
using SmrtPad.Services.Licensing;
using SmrtPad.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Windows.Globalization;
using Res = SmrtPad.Helpers.ResourceHelper;

namespace SmrtPad
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;
        private IAIDispatcher? _aiDispatcher;

        public static Window MainWindow { get; private set; } = null!;

        /// <summary>The AI dispatcher loaded via ALC when Pro is licensed; null for Free tier.</summary>
        public IAIDispatcher? AIDispatcher => _aiDispatcher;

#if DEBUG
        // True when the app was launched with --free-tier (e.g. from UI tests).
        // Stored so that async post-launch tasks can skip the real licence check
        // which would otherwise override the free-tier feature-flag state.
        private bool _isFreeTierSession;
#endif

        /// <summary>All currently open <see cref="MainWindow"/> instances.</summary>
        public static System.Collections.Generic.List<MainWindow> Windows { get; } = [];

        /// <summary>Opens a new editor window and activates it.</summary>
        public static MainWindow NewWindow()
        {
            var w = new MainWindow();
            Windows.Add(w);
            w.Closed += async (_, _) => await Current.HandleWindowClosedAsync(w);
            w.RefreshProGatedUi();
            w.Activate();
            return w;
        }

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> for the application.
        /// </summary>
        public ServiceProvider Services { get; }

        /// <summary>
        /// Gets the current <see cref="App"/> instance.
        /// </summary>
        public new static App Current => (App)Application.Current;

        /// <summary>
        /// Initializes the singleton application object.
        /// </summary>
        public App()
        {
            Services = ConfigureServices();

            // Apply the persisted language override before any resources are loaded.
            var lang = Services.GetRequiredService<ISettingsService>().Language;
            try
            {
                // PrimaryLanguageOverride requires package identity; skip silently for unpackaged launches.
                ApplicationLanguages.PrimaryLanguageOverride =
                    string.IsNullOrEmpty(lang) || lang == "en-US" ? string.Empty : lang;
            }
            catch (InvalidOperationException) { }

            InitializeComponent();
        }

        private static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<EditorViewModel>();
            services.AddTransient<IDialogService, DialogService>();
            services.AddTransient<IFileService, FileService>();
            services.AddSingleton<IInkService, InkService>();
            services.AddSingleton<ISessionRestoreService, SessionRestoreService>();
            services.AddSingleton<IStoreContextAdapter, StubStoreContextAdapter>();
            services.AddSingleton<LocalKeyValidator>();
            services.AddSingleton<LicenseOrchestrator>();

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            var mainWindow = new MainWindow();
            _window = mainWindow;
            MainWindow = _window;
            Windows.Add(mainWindow);
            mainWindow.Closed += async (_, _) => await HandleWindowClosedAsync(mainWindow);

            #if DEBUG
            // In debug builds grant pro immediately so AI features are available from the first
            // interaction, without waiting for the async licence check to complete.
            // Pass --free-tier on the command line (e.g. from UI tests) to suppress this so that
            // free-tier gate logic can be exercised in the same binary.
            // Check both GetCommandLineArgs() (unpackaged/direct launch) and args.Arguments
            // (AUMID/packaged launch) because the activation path determines which is populated.
            // Also check a sentinel file written by AppiumSession; the file-based approach is
            // needed because GetTempPath2W in Windows 11 returns a package-specific temp dir for
            // MSIX apps (not the same path the test process writes to).
            bool isFreeTier = Environment.GetCommandLineArgs().Contains("--free-tier")
                           || (args.Arguments ?? string.Empty).Contains("--free-tier")
                           || System.IO.File.Exists(System.IO.Path.Combine(
                               System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                               "SmrtPad_FreeTier.flag"));
            // Consume the sentinel file immediately so it doesn't affect later launches.
            if (isFreeTier)
            {
                try { System.IO.File.Delete(System.IO.Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                    "SmrtPad_FreeTier.flag")); } catch { }
            }
            _isFreeTierSession = isFreeTier;
            if (!isFreeTier)
            {
                FeatureFlags.SetProFlags();
                _aiDispatcher = TryLoadAIDispatcher();
            }
#endif
            mainWindow.RefreshProGatedUi();
            _window.Activate();

            _ = RunPostActivationStartupAsync(mainWindow, args);
        }

        private async Task RunPostActivationStartupAsync(MainWindow mainWindow, Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            await Task.Yield();

            _ = InitializeLicenseAfterLaunchAsync(mainWindow);

            try
            {
                await PromptForCrashTelemetryConsentAsync(mainWindow);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Crash telemetry consent failed: {ex.Message}");
            }

            try
            {
                await PromptForSessionRestoreAsync(mainWindow);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Session restore check failed: {ex.Message}");
            }

            var filePath = GetStartupFilePath(args);
            if (string.IsNullOrEmpty(filePath) || !System.IO.File.Exists(filePath))
                return;

            try
            {
                await mainWindow.OpenFileByPathAsync(filePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open startup file: {ex.Message}");
            }
        }

        private async Task InitializeLicenseAfterLaunchAsync(MainWindow mainWindow)
        {
#if DEBUG
            // Skip the real licence check in free-tier test mode so that
            // LicenseOrchestrator.ApplyProState() cannot override the free-tier
            // feature-flag state that OnLaunched established.
            if (_isFreeTierSession) return;
#endif
            try
            {
                await InitializeLicenseOrchestratorAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Licence check failed: {ex.Message}");
            }
            mainWindow.DispatcherQueue.TryEnqueue(mainWindow.RefreshProGatedUi);
        }

        private static string? GetStartupFilePath(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // Handle startup file argument — check command-line args first (exe launch),
            // then fall back to activation arguments (AUMID / package activation).
            // AUMID activation may split a space-containing path across multiple
            // GetCommandLineArgs() entries, so we try progressively joining them.
            string? filePath = null;
            var cmdArgs = Environment.GetCommandLineArgs();

            if (cmdArgs.Length > 1)
            {
                // First try the simple case: single quoted argument
                filePath = cmdArgs[1];
                if (!System.IO.File.Exists(filePath))
                {
                    // Join all remaining args in case the path was split on spaces
                    filePath = string.Join(" ", cmdArgs, 1, cmdArgs.Length - 1);
                    if (!System.IO.File.Exists(filePath))
                        filePath = null;
                }
            }

            // Fall back to activation arguments
            if (string.IsNullOrEmpty(filePath) && !string.IsNullOrEmpty(args.Arguments))
            {
                filePath = args.Arguments.Trim('"');
            }

            return filePath;
        }

        private async Task InitializeLicenseOrchestratorAsync()
        {
            var orchestrator = Services.GetService<LicenseOrchestrator>();
            if (orchestrator is null)
                return;

#if DEBUG
            // Force Pro mode on for local debug builds without requiring a Store licence or .lic file.
            FeatureFlags.SetProFlags();
            _aiDispatcher = TryLoadAIDispatcher();
#else
            await Task.Run(() => orchestrator.InitializeAsync());

            if (orchestrator.IsPro)
            {
                _aiDispatcher = TryLoadAIDispatcher();
            }

            orchestrator.ProLicenseChanged += (_, isPro) =>
            {
                if (_window is not MainWindow mainWindow)
                    return;

                mainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    _aiDispatcher = isPro ? _aiDispatcher ?? TryLoadAIDispatcher() : null;

                    foreach (var window in Windows.ToArray())
                    {
                        window.RefreshProGatedUi();
                    }
                });
            };
#endif
        }

        private async Task PromptForSessionRestoreAsync(MainWindow mainWindow)
        {
            var sessionRestoreService = Services.GetRequiredService<ISessionRestoreService>();
            var sessionTabs = await sessionRestoreService.LoadSessionAsync();
            if (sessionTabs.Count == 0)
                return;

            var dialog = new ContentDialog
            {
                Title = Res.GetString("SessionRestoreTitle"),
                Content = Res.GetString("SessionRestoreContent"),
                PrimaryButtonText = Res.GetString("SessionRestoreRestore"),
                CloseButtonText = Res.GetString("SessionRestoreDiscard"),
                XamlRoot = await WaitForXamlRootAsync(mainWindow)
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await mainWindow.RestoreSessionAsync(sessionTabs);
                return;
            }

            await sessionRestoreService.ClearSessionAsync();
        }

        private async Task HandleWindowClosedAsync(MainWindow window)
        {
            Windows.Remove(window);

            if (Windows.Count == 0)
            {
                var sessionRestoreService = Services.GetRequiredService<ISessionRestoreService>();
                await sessionRestoreService.ClearSessionAsync();
            }
        }

        private static async Task<XamlRoot> WaitForXamlRootAsync(
            MainWindow mainWindow, CancellationToken ct = default)
        {
            // XamlRoot is null until the window's content has been added to the
            // visual tree and rendered at least one frame. Poll briefly.
            for (int i = 0; i < 80; i++)
            {
                var root = mainWindow.Content?.XamlRoot;
                if (root is not null) return root;
                await Task.Delay(25, ct);
            }
            throw new InvalidOperationException("XamlRoot was not available within the expected window.");
        }

        private async Task PromptForCrashTelemetryConsentAsync(MainWindow mainWindow)
        {
            var settings = Services.GetRequiredService<ISettingsService>();
            if (settings.CrashTelemetryConsentAsked)
            {
                if (settings.CrashTelemetryEnabled)
                    AttachCrashHandler();
                return;
            }

            var dialog = new ContentDialog
            {
                Title = Res.GetString("CrashTelemetryTitle"),
                Content = Res.GetString("CrashTelemetryContent"),
                PrimaryButtonText = Res.GetString("CrashTelemetryAccept"),
                CloseButtonText = Res.GetString("CrashTelemetryDecline"),
                XamlRoot = await WaitForXamlRootAsync(mainWindow)
            };

            var result = await dialog.ShowAsync();
            settings.CrashTelemetryConsentAsked = true;
            settings.CrashTelemetryEnabled = result == ContentDialogResult.Primary;
            settings.Save();

            if (settings.CrashTelemetryEnabled)
                AttachCrashHandler();
        }

        private void AttachCrashHandler()
        {
            UnhandledException += App_UnhandledException;
        }

        private static void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            try
            {
                var crashDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "SmrtPad", "crashes");
                Directory.CreateDirectory(crashDir);

                var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
                var crashFilePath = Path.Combine(crashDir, $"crash_{timestamp}.json");

                var crashReport = new
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Message = e.Message,
                    StackTrace = e.Exception?.StackTrace,
                    ExceptionType = e.Exception?.GetType().FullName,
                    AppVersion = typeof(App).Assembly.GetName().Version?.ToString()
                };

                File.WriteAllText(crashFilePath, JsonSerializer.Serialize(crashReport, new JsonSerializerOptions { WriteIndented = true }));

                // Notify Windows Error Reporting.
                WerReportFault(nint.Zero, 1 /* STATUS_ILLEGAL_INSTRUCTION */, 0);
            }
            catch
            {
                // Best-effort crash recording — never rethrow inside an exception handler.
            }
        }

        [DllImport("wer.dll", SetLastError = false, ExactSpelling = true)]
        private static extern uint WerReportFault(nint hwnd, uint dwFaultType, uint dwFlags);

        /// <summary>
        /// Attempts to load <c>SmrtPad.AI.dll</c> via a dedicated <see cref="System.Runtime.Loader.AssemblyLoadContext"/>
        /// and create a proxy dispatcher. Returns <c>null</c> if the DLL is absent or loading fails.
        /// </summary>
        private static IAIDispatcher? TryLoadAIDispatcher()
        {
            try
            {
                var aiDllPath = Path.Combine(AppContext.BaseDirectory, "AI", "SmrtPad.AI.dll");
                if (!File.Exists(aiDllPath))
                    return null;

                // Pre-load native ORT DLLs from the AI subdirectory before any managed or native
                // code triggers a LoadLibrary("onnxruntime.dll") search. Without this, the Windows
                // App Runtime's onnxruntime.dll (1.23) is found on the activation-context search
                // path before the AI-directory copy (1.24). ORT GenAI then runs against ORT 1.23
                // while the CUDA EP is built for ORT 1.24, causing an internal vtable mismatch
                // and a 0xC0000005 access violation during model load. Once a DLL is resident in
                // the process by base name, a subsequent LoadLibrary("onnxruntime.dll") from native
                // code returns the already-loaded module instead of walking the search path again.
                PreloadNativeOrtDlls(Path.GetDirectoryName(aiDllPath)!);

                var alc = new AIAssemblyLoadContext(aiDllPath);
                var assembly = alc.LoadFromAssemblyPath(aiDllPath);
                var factoryType = assembly.GetType("SmrtPad.AI.AIDispatcherFactory");
                if (factoryType is null)
                    return null;

                dynamic factory = Activator.CreateInstance(factoryType)!;
                dynamic dispatcher = factory.Create();
                return new AIDispatcherProxy(dispatcher);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load SmrtPad.AI: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads ORT native DLLs from <paramref name="aiDir"/> into the process module table so
        /// that a later bare-name <c>LoadLibrary</c> from native code returns the already-resident
        /// module rather than searching the path and finding a mismatched version shipped by the
        /// Windows App Runtime.
        /// </summary>
        private static void PreloadNativeOrtDlls(string aiDir)
        {
            // Load order matters:
            // 1. onnxruntime_providers_shared.dll — ORT validates this handle during its own init.
            // 2. onnxruntime.dll                  — core runtime; genai depends on it.
            // 3. onnxruntime-genai.dll             — GenAI layer; must follow onnxruntime.dll.
            foreach (var name in (ReadOnlySpan<string>)[
                "onnxruntime_providers_shared.dll",
                "onnxruntime.dll",
                "onnxruntime-genai.dll"])
            {
                var fullPath = Path.Combine(aiDir, name);
                if (File.Exists(fullPath))
                    System.Runtime.InteropServices.NativeLibrary.TryLoad(fullPath, out _);
            }
        }
    }
}
