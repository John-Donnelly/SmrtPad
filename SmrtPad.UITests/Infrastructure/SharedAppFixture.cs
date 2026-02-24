using System;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace SmrtPad.UITests.Infrastructure
{
    /// <summary>
    /// xUnit class-fixture that creates a single <see cref="AppiumSession"/> shared
    /// across every test method in a test class.  Using one session per class
    /// eliminates per-test launch overhead while keeping each test class isolated.
    ///
    /// Usage:
    /// <code>
    ///   public class MyTests : IClassFixture&lt;SharedAppFixture&gt;
    ///   {
    ///       private readonly SharedAppFixture _fx;
    ///       public MyTests(SharedAppFixture fx) => _fx = fx;
    ///   }
    /// </code>
    ///
    /// Call <see cref="RequireDriver"/> at the start of every test — it skips the
    /// test gracefully when Appium / WinAppDriver is unavailable.
    /// </summary>
    public sealed class SharedAppFixture : IDisposable
    {
        private readonly AppiumSession? _session;

        public WindowsDriver? Driver { get; }

        /// <summary>True when a live WinAppDriver session was established.</summary>
        public bool IsAvailable => Driver is not null;

        public SharedAppFixture()
        {
            if (!AppiumSession.IsAvailable()) return;
            string? exe = AppiumSession.FindSmrtPadExe();
            if (exe is null) return;

            try
            {
                _session = new AppiumSession(exe);
                Driver   = _session.Driver;
            }
            catch
            {
                _session = null;
                Driver   = null;
            }
        }

        public void Dispose() => _session?.Dispose();

        // ── Shared helpers ────────────────────────────────────────────────────

        /// <summary>
        /// Clears all text in the editor via Ctrl+A → Delete.
        /// Waits briefly for the UI to settle after each step.
        /// </summary>
        public void ClearEditor()
        {
            var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.Click();
            Thread.Sleep(150);
            editor.SendKeys(Keys.Control + "a");
            Thread.Sleep(150);
            editor.SendKeys(Keys.Delete);
            Thread.Sleep(300);
        }

        /// <summary>
        /// Clicks the editor to focus it, then sends <paramref name="text"/> as keystrokes.
        /// </summary>
        public void TypeInEditor(string text)
        {
            var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.Click();
            Thread.Sleep(100);
            editor.SendKeys(text);
            Thread.Sleep(250);
        }

        /// <summary>Sends Ctrl+A to the editor, selecting all content.</summary>
        public void SelectAllInEditor()
        {
            var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.Click();
            Thread.Sleep(100);
            editor.SendKeys(Keys.Control + "a");
            Thread.Sleep(200);
        }

        /// <summary>Sends Ctrl+Z to the editor to undo the last action.</summary>
        public void UndoInEditor()
        {
            var undoBtn = Driver!.FindElement(MobileBy.AccessibilityId("UndoButton"));
            undoBtn.Click();
            Thread.Sleep(250);
        }

        /// <summary>
        /// Opens <paramref name="menuName"/> (by UIA Name) and clicks the item
        /// whose UIA Name matches <paramref name="itemName"/>.
        /// </summary>
        public void ClickMenuItem(string menuName, string itemName)
        {
            Driver!.FindElement(MobileBy.Name(menuName)).Click();
            Thread.Sleep(450);
            Driver!.FindElement(MobileBy.Name(itemName)).Click();
            Thread.Sleep(300);
        }

        /// <summary>
        /// Returns the current toggle state of a ToggleButton or ToggleMenuFlyoutItem
        /// identified by <paramref name="automationId"/>.
        /// Uses the UIA <c>Toggle.ToggleState</c> attribute: "1" = checked.
        /// </summary>
        public bool IsToggleChecked(string automationId)
        {
            var el = Driver!.FindElement(MobileBy.AccessibilityId(automationId));
            return el.GetAttribute("Toggle.ToggleState") == "1";
        }

        /// <summary>
        /// Returns the visible text of a status-bar <c>TextBlock</c> element
        /// identified by <paramref name="automationId"/>.
        /// </summary>
        public string GetStatusBarText(string automationId)
            => Driver!.FindElement(MobileBy.AccessibilityId(automationId)).Text;
    }
}
