using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using SmrtPad.Services;
using SmrtPad.ViewModels;
using System;

namespace SmrtPad
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private Window? _window;

        public static Window MainWindow { get; private set; } = null!;

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> for the application.
        /// </summary>
        public IServiceProvider Services { get; }

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
            InitializeComponent();
        }

        private static IServiceProvider ConfigureServices()
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
            var mainWindow = new MainWindow();
            _window = mainWindow;
            MainWindow = _window;
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
