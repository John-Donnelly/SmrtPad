using System;
using System.Linq;
using System.Threading;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using SmrtPad.UITests.Infrastructure;
using Xunit;

namespace SmrtPad.UITests.Tests;

/// <summary>
/// UI tests for semantic-search gating in the Smart Sidebar.
/// In a free-tier build, the Smart Sidebar gate should prevent semantic search from becoming visible.
/// Requires a free-tier session (app launched with --free-tier) so that
/// the Pro feature gate is active even in DEBUG builds.
/// </summary>
public sealed class SemanticSearchUITests : IClassFixture<FreeTierAppFixture>, IDisposable
{
    private readonly FreeTierAppFixture _fixture;
    private readonly WindowsDriver? _driver;

    public SemanticSearchUITests(FreeTierAppFixture fixture)
    {
        _fixture = fixture;
        _driver = fixture.Driver;
    }

    public void Dispose()
    {
    }

    private void RequireDriver() =>
        Skip.If(!_fixture.IsAvailable,
            "WinAppDriver / Appium not available, SmrtPad.exe not built, or free-tier session unavailable.");

    [SkippableFact]
    public void SemanticSearch_FreeTier_SectionNotVisible()
    {
        RequireDriver();

        _fixture.ClickMenuItem("View", "✨ Smart Sidebar");
        Thread.Sleep(600);

        var searchBoxes = _driver!.FindElements(MobileBy.AccessibilityId("SemanticSearchBox"));
        Assert.Empty(searchBoxes);

        var dismissButton = _driver.FindElement(MobileBy.Name("Not now"));
        dismissButton.Click();
        Thread.Sleep(300);
    }

    [SkippableFact]
    public void SemanticSearch_FreeTier_TriggerShowsUpsellDialog()
    {
        RequireDriver();

        _fixture.ClickMenuItem("View", "✨ Smart Sidebar");
        Thread.Sleep(600);

        var dialog = _driver!.FindElement(MobileBy.Name("Upgrade to SmrtPad Pro"));
        Assert.NotNull(dialog);

        var dismissButton = _driver.FindElement(MobileBy.Name("Not now"));
        dismissButton.Click();
        Thread.Sleep(300);
    }
}
