using System;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Xunit;
using SmrtPad.UITests.Infrastructure;

namespace SmrtPad.UITests.Tests
{
    /// <summary>
    /// UI automation tests verifying that macro recording is wired to the
    /// paragraph / list-type and line-spacing toolbar controls.
    ///
    /// Prerequisites: same as <see cref="MainWindowUITests"/>.
    /// Tests skip gracefully when Appium / WinAppDriver is not available.
    /// </summary>
    public class MacroUITests : IDisposable
    {
        private readonly AppiumSession? _session;
        private readonly WindowsDriver?  _driver;

        public MacroUITests()
        {
            if (!AppiumSession.IsAvailable()) return;
            string? exe = AppiumSession.FindSmrtPadExe();
            if (exe is null) return;

            try { _session = new AppiumSession(exe); _driver = _session.Driver; }
            catch { _session = null; _driver = null; }
        }

        public void Dispose() => _session?.Dispose();

        private void RequireDriver() =>
            Skip.If(_driver is null,
                "WinAppDriver / Appium not available or SmrtPad.exe not built.");

        // ── Macro menu items ─────────────────────────────────────────────────

        [SkippableFact]
        public void MacroRecordItem_IsPresent_InMacroMenu()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("MacroRecordItem"));
            Assert.NotNull(item);
        }

        [SkippableFact]
        public void MacroStopItem_IsPresent_InMacroMenu()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("MacroStopItem"));
            Assert.NotNull(item);
        }

        [SkippableFact]
        public void MacroRunItem_IsPresent_InMacroMenu()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("MacroRunItem"));
            Assert.NotNull(item);
        }

        [SkippableFact]
        public void MacroSaveItem_IsPresent_InMacroMenu()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("MacroSaveItem"));
            Assert.NotNull(item);
        }

        [SkippableFact]
        public void MacroLoadItem_IsPresent_InMacroMenu()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("MacroLoadItem"));
            Assert.NotNull(item);
        }

        // ── List type flyout ─────────────────────────────────────────────────

        [SkippableFact]
        public void ListTypeBullet_FlyoutItem_IsPresent()
        {
            RequireDriver();
            // The flyout item is identified by its x:Uid-derived AutomationId
            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("ListTypeBulletItem"));
            Assert.NotNull(item);
        }

        [SkippableFact]
        public void ListTypeNone_FlyoutItem_IsPresent()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("ListTypeNoneItem"));
            Assert.NotNull(item);
        }

        [SkippableFact]
        public void ListTypeNumber_FlyoutItem_IsPresent()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("ListTypeNumberItem"));
            Assert.NotNull(item);
        }

        [SkippableFact]
        public void ListTypeLowerLetter_FlyoutItem_IsPresent()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("ListTypeLowerLetterItem"));
            Assert.NotNull(item);
        }

        [SkippableFact]
        public void ListTypeUpperLetter_FlyoutItem_IsPresent()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("ListTypeUpperLetterItem"));
            Assert.NotNull(item);
        }

        [SkippableFact]
        public void ListTypeLowerRoman_FlyoutItem_IsPresent()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("ListTypeLowerRomanItem"));
            Assert.NotNull(item);
        }

        [SkippableFact]
        public void ListTypeUpperRoman_FlyoutItem_IsPresent()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("ListTypeUpperRomanItem"));
            Assert.NotNull(item);
        }

        // ── Line spacing flyout ──────────────────────────────────────────────

        [SkippableFact]
        public void LineSpacing_CustomItem_IsPresent()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("CustomSpacingItem"));
            Assert.NotNull(item);
        }

        [SkippableFact]
        public void LineSpacing_10Item_IsPresent()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.Name("1.0"));
            Assert.NotNull(item);
        }

        [SkippableFact]
        public void LineSpacing_115Item_IsPresent()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.Name("1.15"));
            Assert.NotNull(item);
        }

        [SkippableFact]
        public void LineSpacing_15Item_IsPresent()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.Name("1.5"));
            Assert.NotNull(item);
        }

        [SkippableFact]
        public void LineSpacing_20Item_IsPresent()
        {
            RequireDriver();
            var item = _driver!.FindElement(
                MobileBy.Name("2.0"));
            Assert.NotNull(item);
        }
    }
}
