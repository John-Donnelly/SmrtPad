using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using SmrtPad.Services;
using SmrtPad.Services.Licensing;
using SmrtPad.ViewModels;
using System;
using Windows.Globalization;

namespace SmrtPad
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        public static Window MainWindow { get; private set; } = null!;

        /// <summary>All currently open <see cref="MainWindow"/> instances.</summary>
        public static System.Collections.Generic.List<MainWindow> Windows { get; } = [];

        /// <summary>Opens a new editor window and activates it.</summary>
        public static MainWindow NewWindow()
        {
            var w = new MainWindow();
            Windows.Add(w);
            w.Closed += (_, _) => Windows.Remove(w);
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
            services.AddSingleton<IStoreContextAdapter, StubStoreContextAdapter>();
            services.AddSingleton<LocalKeyValidator>();
            services.AddSingleton<LicenseOrchestrator>();

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            var mainWindow = new MainWindow();
            _window = mainWindow;
            MainWindow = _window;
            Windows.Add(mainWindow);
            mainWindow.Closed += (_, _) => Windows.Remove(mainWindow);
            _window.Activate();

            // Initialize licence orchestrator — enables Pro features if licensed.
            try
            {
                var orchestrator = Services.GetService<LicenseOrchestrator>();
                if (orchestrator is not null)
                {
                    await orchestrator.InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Licence check failed: {ex.Message}");
            }

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

            if (string.IsNullOrEmpty(filePath) && !string.IsNullOrEmpty(args.Arguments))
            {
                filePath = args.Arguments.Trim('"');
            }

            if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
            {
                try
                {
                    await mainWindow.OpenFileByPathAsync(filePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to open startup file: {ex.Message}");
                }
            }
        }
    }
}
