using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using SmrtPad.Services;
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
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SmrtPad_App_Startup.log");
            System.IO.File.WriteAllText(logPath, "Starting App constructor\n");

            try
            {
                Services = ConfigureServices();
                System.IO.File.AppendAllText(logPath, "ConfigureServices done\n");

                // Apply the persisted language override before any resources are loaded.
                var lang = Services.GetRequiredService<ISettingsService>().Language;
                System.IO.File.AppendAllText(logPath, $"Got language: {lang}\n");

                try
                {
                    // PrimaryLanguageOverride requires package identity; skip silently for unpackaged launches.
                    ApplicationLanguages.PrimaryLanguageOverride =
                        string.IsNullOrEmpty(lang) || lang == "en-US" ? string.Empty : lang;
                }
                catch (InvalidOperationException) { }
                System.IO.File.AppendAllText(logPath, "PrimaryLanguageOverride set\n");

                InitializeComponent();
                System.IO.File.AppendAllText(logPath, "InitializeComponent done\n");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logPath, $"Exception: {ex}\n");
                throw;
            }
        }

        private static ServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<EditorViewModel>();
            services.AddTransient<IDialogService, DialogService>();
            services.AddTransient<IFileService, FileService>();

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SmrtPad_App_Startup.log");
            System.IO.File.AppendAllText(logPath, "OnLaunched: creating MainWindow\n");
            MainWindow mainWindow;
            try
            {
                mainWindow = new MainWindow();
                System.IO.File.AppendAllText(logPath, "OnLaunched: MainWindow created\n");
            }
            catch (Exception ex)
            {
                System.IO.File.AppendAllText(logPath, $"OnLaunched: MainWindow ctor threw: {ex}\n");
                throw;
            }
            _window = mainWindow;
            MainWindow = _window;
            Windows.Add(mainWindow);
            mainWindow.Closed += (_, _) => Windows.Remove(mainWindow);
            _window.Activate();

            // Handle startup file argument
            var cmdArgs = Environment.GetCommandLineArgs();
            if (cmdArgs.Length > 1)
            {
                string filePath = cmdArgs[1];
                if (System.IO.File.Exists(filePath))
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
}
