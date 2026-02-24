// Program.cs — custom entry point for SmrtPad (unpackaged WinUI 3)
//
// The auto-generated Program in App.g.i.cs does not call Bootstrap.Initialize()
// before Application.Start(), which causes a fail-fast inside Microsoft.UI.Xaml.dll
// when the app runs without MSIX package identity.
//
// This file:
//   1. Suppresses the auto-generated Main via DISABLE_XAML_GENERATED_MAIN
//   2. Explicitly calls Bootstrap.Initialize(1.8) before any WinRT activation
//   3. Mirrors the rest of the generated Main exactly

using System;
using Microsoft.Windows.ApplicationModel.DynamicDependency;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace SmrtPad
{
    public static class Program
    {
        [global::System.STAThreadAttribute]
        static void Main(string[] args)
        {
            // Must run before any WinRT / Windows App SDK type is activated.
            // 0x00010008 = WinApp SDK major 1, minor 8.
            try
            {
                Bootstrap.Initialize(0x00010008);
            }
            catch (Exception ex)
            {
                // Write to debug output and a temp log so we can diagnose startup failures.
                string msg = $"Bootstrap.Initialize failed: {ex.GetType().Name}: {ex.Message}";
                global::System.Diagnostics.Debug.WriteLine(msg);
                global::System.IO.File.WriteAllText(
                    global::System.IO.Path.Combine(
                        global::System.IO.Path.GetTempPath(), "SmrtPad_bootstrap_error.txt"),
                    msg + "\n" + ex.ToString());
                // Re-throw so the process exits cleanly with useful event-log info.
                throw;
            }

            global::WinRT.ComWrappersSupport.InitializeComWrappers();

            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                global::System.Threading.SynchronizationContext
                    .SetSynchronizationContext(context);
                new App();
            });

            Bootstrap.Shutdown();
        }
    }
}
