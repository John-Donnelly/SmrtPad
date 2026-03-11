using System;
using System.Threading;
using OpenQA.Selenium.Appium.Windows;

namespace SmrtPad.UITests.Infrastructure
{
    /// <summary>
    /// xUnit class-fixture that launches SmrtPad with the <c>--free-tier</c> argument,
    /// preventing the DEBUG <see cref="SmrtPad.Services.Licensing.FeatureFlags.SetProFlags"/>
    /// call so that Pro feature-gate UI (upsell dialogs, sidebar hiding) is exercised.
    ///
    /// Usage:
    /// <code>
    ///   public class MyFreeTierTests : IClassFixture&lt;FreeTierAppFixture&gt;
    ///   {
    ///       private readonly FreeTierAppFixture _fx;
    ///       public MyFreeTierTests(FreeTierAppFixture fx) => _fx = fx;
    ///   }
    /// </code>
    /// </summary>
    public sealed class FreeTierAppFixture : IDisposable
    {
        private readonly AppiumSession? _session;

        public WindowsDriver? Driver { get; }

        /// <summary>True when a live WinAppDriver session was established.</summary>
        public bool IsAvailable => Driver is not null;

        public FreeTierAppFixture()
        {
            if (!AppiumSession.IsAvailable()) return;
            string? exe = AppiumSession.FindSmrtPadExe();
            if (exe is null) return;

            try
            {
                _session = new AppiumSession(exe, launchArgument: "--free-tier");
                Driver   = _session.Driver;

                Thread.Sleep(1500);
                DismissSessionRestoreDialogIfPresent();
            }
            catch
            {
                _session = null;
                Driver   = null;
            }
        }

        public void Dispose() => _session?.Dispose();

        private void DismissSessionRestoreDialogIfPresent()
        {
            try
            {
                var discard = Driver!.FindElements(OpenQA.Selenium.Appium.MobileBy.Name("Discard"));
                if (discard.Count > 0)
                {
                    discard[0].Click();
                    Thread.Sleep(300);
                }
            }
            catch { }
        }
    }
}
