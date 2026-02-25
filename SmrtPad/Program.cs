// Program.cs — custom entry point for SmrtPad (packaged WinUI 3)
//
// Suppresses the auto-generated Main via DISABLE_XAML_GENERATED_MAIN and
// mirrors its implementation. Bootstrap.Initialize/Shutdown are intentionally
// omitted: those APIs are only valid for unpackaged apps. The MSIX package
// declares the Windows App SDK framework dependency, so the runtime is
// resolved automatically by the OS loader.

using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace SmrtPad
{
    public static class Program
    {
        [global::System.STAThreadAttribute]
        static void Main(string[] args)
        {
            global::WinRT.ComWrappersSupport.InitializeComWrappers();

            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(
                    DispatcherQueue.GetForCurrentThread());
                global::System.Threading.SynchronizationContext
                    .SetSynchronizationContext(context);
                new App();
            });

        }
    }
}
