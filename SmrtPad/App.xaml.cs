using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SmrtPad.Services;
using SmrtPad.Services.Licensing;
using SmrtPad.ViewModels;
using System;
using System.Linq;
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
                XamlRoot = mainWindow.Content.XamlRoot
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

        /// <summary>
        /// Attempts to load <c>SmrtPad.AI.dll</c> via a dedicated <see cref="System.Runtime.Loader.AssemblyLoadContext"/>
        /// and create a proxy dispatcher. Returns <c>null</c> if the DLL is absent or loading fails.
        /// </summary>
        private static IAIDispatcher? TryLoadAIDispatcher()
        {
            try
            {
                var aiDllPath = Path.Combine(AppContext.BaseDirectory, "SmrtPad.AI.dll");
                if (!File.Exists(aiDllPath))
                    return null;

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
    }
}
