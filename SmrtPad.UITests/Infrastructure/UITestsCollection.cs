using Xunit;

namespace SmrtPad.UITests.Infrastructure
{
    /// <summary>
    /// Forces all UI test classes into a single xUnit collection so they run
    /// **sequentially**, never concurrently.  Without this, Visual Studio Test Explorer
    /// launches test-class fixtures in parallel; each fixture calls
    /// <see cref="AppiumSession"/>'s <c>ClearStartupBlockers()</c> which kills every
    /// running SmrtPad.exe process — including the ones owned by sibling fixtures.
    /// </summary>
    [CollectionDefinition("UITests", DisableParallelization = true)]
    public sealed class UITestsCollection { }
}
