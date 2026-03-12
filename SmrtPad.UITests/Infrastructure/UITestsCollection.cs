using SmrtPad.UITests.Tests;
using Xunit;

namespace SmrtPad.UITests.Infrastructure
{
    /// <summary>
    /// Places all Pro-mode UI test classes into a single xUnit collection with a single
    /// shared <see cref="SharedAppFixture"/> instance.  This guarantees:
    /// <list type="bullet">
    ///   <item><c>ClearStartupBlockers()</c> is called exactly once (at collection start),
    ///         not once per class, eliminating the cross-session process-kill cascade (N-1).</item>
    ///   <item>All tests share the same live WinAppDriver window handle, so there is no
    ///         per-class launch/teardown overhead and no races between fixture constructors.</item>
    ///   <item>Tests execute sequentially (<c>DisableParallelization = true</c>) so
    ///         Appium interactions never overlap.</item>
    /// </list>
    /// Free-tier tests live in <c>FreeTierUITestsCollection</c>.
    /// DOCX dark-mode tests live in <c>DocxDarkModeUITestsCollection</c>.
    /// </summary>
    [CollectionDefinition("UITests", DisableParallelization = true)]
    public sealed class UITestsCollection : ICollectionFixture<SharedAppFixture> { }

    /// <summary>
    /// Separate collection for free-tier UI tests (<see cref="FreeTierAppFixture"/>).
    /// Runs sequentially after "UITests" due to the assembly-level
    /// <c>CollectionBehavior(DisableTestParallelization = true)</c>.
    /// </summary>
    [CollectionDefinition("FreeTierUITests", DisableParallelization = true)]
    public sealed class FreeTierUITestsCollection : ICollectionFixture<FreeTierAppFixture> { }

    /// <summary>
    /// Separate collection for the DOCX dark-mode tests that use
    /// <see cref="DocxDarkModeFixture"/> (a specialised session that opens the app with
    /// a DOCX file in dark mode and does not share state with the main session).
    /// </summary>
    [CollectionDefinition("DocxDarkModeUITests", DisableParallelization = true)]
    public sealed class DocxDarkModeUITestsCollection : ICollectionFixture<DocxDarkModeFixture> { }
}
