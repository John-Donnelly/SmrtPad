using Microsoft.UI.Xaml;
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
        /// Initializes the singleton application object.
        /// </summary>
        public App()
        {
            InitializeComponent();
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
