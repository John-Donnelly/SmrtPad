namespace SmrtPad.UITests.Infrastructure
{
    /// <summary>
    /// xUnit collection fixture that launches SmrtPad with the <c>--free-tier</c> argument,
    /// preventing the DEBUG <see cref="SmrtPad.Services.Licensing.FeatureFlags.SetProFlags"/>
    /// call so that Pro feature-gate UI (upsell dialogs, sidebar hiding) can be exercised.
    ///
    /// Extends <see cref="SharedAppFixture"/> so all shared helpers (ClickMenuItem,
    /// GetStatusBarText, ClearEditor, etc.) are available in free-tier test classes.
    ///
    /// The fixture lifetime is managed by <see cref="UITestsCollection"/>; test classes
    /// must NOT implement <c>IDisposable</c> teardown that touches the session.
    /// </summary>
    public sealed class FreeTierAppFixture : SharedAppFixture
    {
        // Launch via AUMID. The --free-tier sentinel file is written by AppiumSession
        // before activation so the app detects it in OnLaunched regardless of whether
        // LaunchActivatedEventArgs.Arguments is populated (AUMID vs exe activation).
        public FreeTierAppFixture() : base(launchArgument: "--free-tier") { }
    }
}
