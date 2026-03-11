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
    /// UI tests for the features added in the new-feature batch:
    /// keyboard shortcuts, zoom slider, paragraph dialog, status bar toggle,
    /// paste special dialog, paste split-button, and send by email menu item.
    /// </summary>
    [Collection("UITests")]
    public sealed class NewFeatureUITests : IClassFixture<SharedAppFixture>, IDisposable
    {
        private readonly SharedAppFixture _fx;
        private readonly WindowsDriver? _driver;

        public NewFeatureUITests(SharedAppFixture fx)
        {
            _fx = fx;
            _driver = fx.Driver;
        }

        public void Dispose() { /* session owned by fixture */ }

        private void RequireDriver() =>
            Skip.If(!_fx.IsAvailable,
                "WinAppDriver / Appium not available or SmrtPad.exe not built.");

        private string StatusText => _fx.GetStatusBarText("StatusText");

        // ── Step 1: Ctrl+F opens Find flyout ─────────────────────────────────

        /// <summary>
        /// Pressing Ctrl+F should open the Find flyout and make the FindTextBox visible.
        /// </summary>
        [SkippableFact]
        public void CtrlF_OpensFindFlyout()
        {
            RequireDriver();
            _fx.EnsureBackstageClosed();
            _fx.ClearEditor();

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.Click();
            Thread.Sleep(200);
            editor.SendKeys(Keys.Control + "f");
            Thread.Sleep(600);

            // FindTextBox should be visible inside the opened flyout
            var findBox = _driver.FindElements(MobileBy.AccessibilityId("FindTextBox"));
            Assert.NotEmpty(findBox);

            // Close flyout
            editor.SendKeys(Keys.Escape);
            Thread.Sleep(300);
        }

        /// <summary>
        /// Pressing Ctrl+H should open the Replace flyout and make ReplaceWithTextBox visible.
        /// </summary>
        [SkippableFact]
        public void CtrlH_OpensReplaceFlyout()
        {
            RequireDriver();
            _fx.EnsureBackstageClosed();
            _fx.ClearEditor();

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.Click();
            Thread.Sleep(200);
            editor.SendKeys(Keys.Control + "h");
            Thread.Sleep(600);

            var replaceBox = _driver.FindElements(MobileBy.AccessibilityId("ReplaceWithTextBox"));
            Assert.NotEmpty(replaceBox);

            editor.SendKeys(Keys.Escape);
            Thread.Sleep(300);
        }

        /// <summary>
        /// Pressing Ctrl+D when text is selected should duplicate it (selection length doubles).
        /// </summary>
        [SkippableFact]
        public void CtrlD_DuplicatesSelection()
        {
            RequireDriver();
            _fx.EnsureBackstageClosed();
            _fx.ClearEditor();
            _fx.TypeInEditor("hello");

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.Click();
            Thread.Sleep(100);
            editor.SendKeys(Keys.Control + "a");
            Thread.Sleep(200);
            editor.SendKeys(Keys.Control + "d");
            Thread.Sleep(400);

            Assert.Contains("Duplicated", StatusText, StringComparison.OrdinalIgnoreCase);
        }

        // ── Step 2: Zoom slider ───────────────────────────────────────────────

        /// <summary>
        /// The ZoomSlider element should be present in the status bar.
        /// </summary>
        [SkippableFact]
        public void ZoomSlider_IsPresentInStatusBar()
        {
            RequireDriver();
            _fx.EnsureBackstageClosed();

            var slider = _driver!.FindElements(MobileBy.AccessibilityId("ZoomSlider"));
            Assert.NotEmpty(slider);
        }

        /// <summary>
        /// The ZoomPercentBox element should be present in the status bar.
        /// </summary>
        [SkippableFact]
        public void ZoomPercentBox_IsPresentInStatusBar()
        {
            RequireDriver();
            _fx.EnsureBackstageClosed();

            var box = _driver!.FindElements(MobileBy.AccessibilityId("ZoomPercentBox"));
            Assert.NotEmpty(box);
        }

        // ── Step 3: Format → Paragraph dialog ────────────────────────────────

        /// <summary>
        /// Clicking Format → Paragraph should open the FormatParagraphDialog.
        /// </summary>
        [SkippableFact]
        public void FormatParagraph_OpensDialog()
        {
            RequireDriver();
            _fx.EnsureBackstageClosed();

            _driver!.FindElement(MobileBy.Name("Format")).Click();
            Thread.Sleep(450);
            _driver.FindElement(MobileBy.Name("Paragraph...")).Click();
            Thread.Sleep(600);

            var dialog = _driver.FindElements(MobileBy.AccessibilityId("FormatParagraphDialog"));
            Assert.NotEmpty(dialog);

            // Close dialog
            _driver.FindElement(MobileBy.Name("Cancel")).Click();
            Thread.Sleep(300);
        }

        // ── Step 4: Status bar toggle ─────────────────────────────────────────

        /// <summary>
        /// Clicking View → Status Bar should hide the status bar.
        /// </summary>
        [SkippableFact]
        public void StatusBarToggle_HidesAndShowsStatusBar()
        {
            RequireDriver();
            _fx.EnsureBackstageClosed();

            // Hide status bar
            _fx.ClickMenuItem("View", "Status Bar");
            Thread.Sleep(400);

            var statusBarElements = _driver!.FindElements(MobileBy.AccessibilityId("StatusBar"));
            bool isHidden = statusBarElements.Count == 0 || !statusBarElements[0].Displayed;
            Assert.True(isHidden, "Status bar should be hidden after toggle.");

            // Show status bar again — poll instead of a fixed sleep because the
            // re-show animation and accessibility tree update take variable time (UI-12).
            _fx.ClickMenuItem("View", "Status Bar");

            var statusBarAfter = _driver.FindElements(MobileBy.AccessibilityId("StatusBar"));
            bool found = statusBarAfter.Count > 0 && statusBarAfter[0].Displayed;
            if (!found)
            {
                // Retry for up to 3 s
                var deadline = DateTime.UtcNow.AddSeconds(3);
                while (DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(100);
                    statusBarAfter = _driver.FindElements(MobileBy.AccessibilityId("StatusBar"));
                    if (statusBarAfter.Count > 0 && statusBarAfter[0].Displayed) { found = true; break; }
                }
            }
            Assert.True(found, "Status bar should be visible after second toggle.");
        }

        // ── Step 5: Paste Special dialog ─────────────────────────────────────

        /// <summary>
        /// Clicking Edit → Paste Special should open the PasteSpecialDialog.
        /// </summary>
        [SkippableFact]
        public void PasteSpecial_OpensDialog()
        {
            RequireDriver();
            _fx.EnsureBackstageClosed();
            _fx.TypeInEditor("test clipboard content");
            _fx.SelectAllInEditor();

            // Copy first so clipboard has content
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.SendKeys(Keys.Control + "c");
            Thread.Sleep(300);

            _fx.ClickMenuItem("Edit", "Paste Special");
            Thread.Sleep(600);

            var dialog = _driver.FindElements(MobileBy.AccessibilityId("PasteSpecialDialog"));
            Assert.NotEmpty(dialog);

            _driver.FindElement(MobileBy.Name("Cancel")).Click();
            Thread.Sleep(300);
        }

        // ── Step 6: Paste SplitButton ─────────────────────────────────────────

        /// <summary>
        /// The PasteSplitButton should be present in the ribbon clipboard group.
        /// </summary>
        [SkippableFact]
        public void PasteSplitButton_IsPresentInRibbon()
        {
            RequireDriver();
            _fx.EnsureBackstageClosed();

            var btn = _driver!.FindElements(MobileBy.AccessibilityId("PasteSplitButton"));
            Assert.NotEmpty(btn);
        }

        // ── Step 9: Send by Email ─────────────────────────────────────────────

        /// <summary>
        /// The Send by Email item should be visible in the File backstage.
        /// </summary>
        [SkippableFact(Skip = "Send Email feature not yet implemented (NavSendEmail element absent)")]
        public void SendEmail_IsVisibleInBackstage()
        {
            RequireDriver();

            _driver!.FindElement(MobileBy.Name("File")).Click();
            Thread.Sleep(600);

            var navItem = _driver.FindElements(MobileBy.AccessibilityId("NavSendEmail"));
            Assert.NotEmpty(navItem);

            // Close backstage
            _driver.FindElement(MobileBy.Name("File")).Click();
            Thread.Sleep(400);
        }

        // ── Step 10: Accessibility ────────────────────────────────────────────

        /// <summary>
        /// FontColorIndicator should have an AutomationId set for accessibility.
        /// </summary>
        [SkippableFact]
        public void FontColorIndicator_HasAutomationId()
        {
            RequireDriver();
            _fx.EnsureBackstageClosed();

            var indicator = _driver!.FindElements(MobileBy.AccessibilityId("FontColorIndicator"));
            Assert.NotEmpty(indicator);
        }

        /// <summary>
        /// HighlightColorIndicator should have an AutomationId set for accessibility.
        /// </summary>
        [SkippableFact]
        public void HighlightColorIndicator_HasAutomationId()
        {
            RequireDriver();
            _fx.EnsureBackstageClosed();

            var indicator = _driver!.FindElements(MobileBy.AccessibilityId("HighlightColorIndicator"));
            Assert.NotEmpty(indicator);
        }

        /// <summary>
        /// ZoomSlider accessible name should indicate its purpose.
        /// </summary>
        [SkippableFact]
        public void ZoomSlider_HasAccessibleName()
        {
            RequireDriver();
            _fx.EnsureBackstageClosed();

            var slider = _driver!.FindElement(MobileBy.AccessibilityId("ZoomSlider"));
            string name = slider.GetAttribute("Name") ?? string.Empty;
            Assert.Contains("zoom", name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
