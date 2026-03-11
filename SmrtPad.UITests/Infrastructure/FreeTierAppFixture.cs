namespace SmrtPad.UITests.Infrastructure
{
    /// <summary>
    /// xUnit class-fixture that launches SmrtPad with the <c>--free-tier</c> argument,
    /// preventing the DEBUG <see cref="SmrtPad.Services.Licensing.FeatureFlags.SetProFlags"/>
    /// call so that Pro feature-gate UI (upsell dialogs, sidebar hiding) can be exercised.
    ///
    /// Extends <see cref="SharedAppFixture"/> so all shared helpers (ClickMenuItem,
    /// GetStatusBarText, ClearEditor, etc.) are available in free-tier test classes.
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
    public sealed class FreeTierAppFixture : SharedAppFixture
    {
        public FreeTierAppFixture() : base(launchArgument: "--free-tier") { }
    }
}
