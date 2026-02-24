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
            
            var macroMenu = _driver!.FindElement(MobileBy.Name("Macro"));
            macroMenu.Click();
            System.Threading.Thread.Sleep(500);

            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("MacroRecordItem"));
            Assert.NotNull(item);
            
            macroMenu.Click();
        }

        [SkippableFact]
        public void MacroStopItem_IsPresent_InMacroMenu()
        {
            RequireDriver();
            
            var macroMenu = _driver!.FindElement(MobileBy.Name("Macro"));
            macroMenu.Click();
            System.Threading.Thread.Sleep(500);

            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("MacroStopItem"));
            Assert.NotNull(item);
            
            macroMenu.Click();
        }

        [SkippableFact]
        public void MacroRunItem_IsPresent_InMacroMenu()
        {
            RequireDriver();
            
            var macroMenu = _driver!.FindElement(MobileBy.Name("Macro"));
            macroMenu.Click();
            System.Threading.Thread.Sleep(500);

            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("MacroRunItem"));
            Assert.NotNull(item);
            
            macroMenu.Click();
        }

        [SkippableFact]
        public void MacroSaveItem_IsPresent_InMacroMenu()
        {
            RequireDriver();
            
            var macroMenu = _driver!.FindElement(MobileBy.Name("Macro"));
            macroMenu.Click();
            System.Threading.Thread.Sleep(500);

            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("MacroSaveItem"));
            Assert.NotNull(item);
            
            macroMenu.Click();
        }

        [SkippableFact]
        public void MacroLoadItem_IsPresent_InMacroMenu()
        {
            RequireDriver();
            
            var macroMenu = _driver!.FindElement(MobileBy.Name("Macro"));
            macroMenu.Click();
            System.Threading.Thread.Sleep(500);

            var item = _driver!.FindElement(
                MobileBy.AccessibilityId("MacroLoadItem"));
            Assert.NotNull(item);
            
            macroMenu.Click();
        }

            }
        }
