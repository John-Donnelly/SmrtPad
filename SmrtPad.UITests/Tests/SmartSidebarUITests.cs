using System;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Xunit;
using SmrtPad.UITests.Infrastructure;

namespace SmrtPad.UITests.Tests
{
    /// <summary>
    /// UI tests for the Smart Sidebar feature gate.
    /// In a free-tier (unlicensed) build the sidebar toggle should show
    /// the Pro upsell dialog rather than opening the sidebar.
    /// Requires a free-tier session (app launched with --free-tier) so that
    /// the Pro feature gate is active even in DEBUG builds.
    /// </summary>
    [Collection("FreeTierUITests")]
    public sealed class SmartSidebarUITests : IDisposable
    {
        private readonly FreeTierAppFixture _fx;
        private WindowsDriver? _driver;

        public SmartSidebarUITests(FreeTierAppFixture fx)
        {
            _fx = fx;
            _driver = fx.Driver;
        }

        public void Dispose() { /* session owned by fixture */ }

        private void RequireDriver()
        {
            _fx.RequireSession();
            _driver = _fx.Driver;
        }
        /// <summary>
        /// The View menu should contain the Smrt Sidebar toggle item.
        /// </summary>
        [SkippableFact]
        public void ViewMenu_ContainsSmartSidebarToggle()
        {
            RequireDriver();

            _driver!.FindElement(MobileBy.Name("View")).Click();
            Thread.Sleep(450);

            var toggle = _driver.FindElement(MobileBy.AccessibilityId("SmartSidebarToggle"));
            Assert.NotNull(toggle);

            // Close the menu without toggling
            _driver.FindElement(MobileBy.Name("View")).Click();
            Thread.Sleep(200);
        }

        /// <summary>
        /// When the user is not Pro-licensed, clicking the Smart Sidebar toggle
        /// should show the Pro upsell dialog with the expected title.
        /// </summary>
        [SkippableFact]
        public void SidebarToggle_FreeTier_ShowsUpsellDialog()
        {
            RequireDriver();

            OpenUpsellDialog();

            var dialog = _driver!.FindElement(MobileBy.Name("Upgrade to SmrtPad Pro"));
            Assert.NotNull(dialog);

            DismissUpsellDialog();
        }

        /// <summary>
        /// In the free tier, the upsell dialog should expose its upgrade action.
        /// </summary>
        [SkippableFact]
        public void SidebarToggle_FreeTier_UpsellDialog_HasUpgradeButton()
        {
            RequireDriver();

            OpenUpsellDialog();

            var upgradeButton = _driver!.FindElement(MobileBy.Name("Upgrade"));
            Assert.NotNull(upgradeButton);

            DismissUpsellDialog();
        }

        /// <summary>
        /// Dismissing the free-tier upsell dialog should close it.
        /// </summary>
        [SkippableFact]
        public void SidebarToggle_FreeTier_UpsellDialog_Dismiss_ClosesDialog()
        {
            RequireDriver();

            OpenUpsellDialog();

            DismissUpsellDialog();

            var dialogs = _driver!.FindElements(MobileBy.Name("Upgrade to SmrtPad Pro"));
            Assert.Empty(dialogs);
        }

        /// <summary>
        /// In the free tier, toggling Smart Sidebar should not display the sidebar shell.
        /// </summary>
        [SkippableFact]
        public void SidebarToggle_FreeTier_SidebarNotVisible()
        {
            RequireDriver();

            OpenUpsellDialog();

            var summarizeButtons = _driver!.FindElements(MobileBy.AccessibilityId("SummarizeSectionButton"));
            Assert.Empty(summarizeButtons);

            DismissUpsellDialog();
        }

        private void OpenUpsellDialog()
        {
            _fx.ClickMenuItem("View", "✨ Smrt Sidebar");
            Thread.Sleep(600);
        }

        private void DismissUpsellDialog()
        {
            var dismissButton = _driver!.FindElement(MobileBy.Name("Not now"));
            dismissButton.Click();
            Thread.Sleep(300);
        }
    }
}

