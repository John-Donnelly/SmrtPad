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
/// </summary>
public sealed class SemanticSearchUITests : IClassFixture<SharedAppFixture>, IDisposable
{
    private readonly SharedAppFixture _fixture;
    private readonly WindowsDriver? _driver;

    public SemanticSearchUITests(SharedAppFixture fixture)
    {
        _fixture = fixture;
        _driver = fixture.Driver;
    }

    public void Dispose()
    {
    }

    private void RequireDriver() =>
        Skip.If(!_fixture.IsAvailable,
            "WinAppDriver / Appium not available or SmrtPad.exe not built.");

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
