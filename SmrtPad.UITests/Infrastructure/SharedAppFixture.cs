using System;
using System.Diagnostics;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

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
        /// Ensures backstage is closed first so editor is accessible.
        /// </summary>
        public void ClearEditor()
        {
            EnsureBackstageClosed();

            bool usedMenu = TryClickMenuItem("Edit", "Select All")
                && TryClickMenuItem("Edit", "Cut");

            if (!usedMenu)
            {
                var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
                editor.Click();
                Thread.Sleep(100);
                editor.SendKeys(Keys.Control + "a");
                Thread.Sleep(150);
                editor.SendKeys(Keys.Control + "x");
                Thread.Sleep(300);
            }
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
            if (!TryClickMenuItem("Edit", "Select All"))
            {
                var editor = Driver!.FindElement(MobileBy.AccessibilityId("Editor"));
                editor.Click();
                Thread.Sleep(100);
                editor.SendKeys(Keys.Control + "a");
                Thread.Sleep(200);
            }
        }

        private bool TryClickMenuItem(string menuName, string itemName)
        {
            try
            {
                ClickMenuItem(menuName, itemName);
                return true;
            }
            catch (NoSuchElementException ex)
            {
                Debug.WriteLine($"Menu item '{menuName} -> {itemName}' not found: {ex.Message}");
                return false;
            }
            catch (WebDriverException ex)
            {
                Debug.WriteLine($"Menu item '{menuName} -> {itemName}' click failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Sends Ctrl+Z to the editor to undo the last action.</summary>
        public void UndoInEditor()
        {
            var undoButtons = Driver!.FindElements(MobileBy.AccessibilityId("UndoButton"));
            if (undoButtons.Count == 0)
            {
                undoButtons = Driver.FindElements(MobileBy.Name("Undo (Ctrl+Z)"));
            }

            if (undoButtons.Count == 0)
            {
                throw new InvalidOperationException("Undo button not found.");
            }

            undoButtons[0].Click();
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

        /// <summary>
        /// Returns <c>true</c> when the File backstage overlay is currently visible.
        /// Detected by the presence of the backstage <c>HeaderText</c> element.
        /// </summary>
        public bool IsBackstageOpen()
        {
            try
            {
                var header = Driver!.FindElements(MobileBy.AccessibilityId("HeaderText"));
                return header.Count > 0 && header[0].Displayed;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ensures the backstage is closed. If it is currently open, clicks the
        /// File button to toggle it shut.
        /// </summary>
        public void EnsureBackstageClosed()
        {
            if (!IsBackstageOpen()) return;
            try
            {
                Driver!.FindElement(MobileBy.Name("File")).Click();
                Thread.Sleep(400);
            }
            catch { }
        }

        /// <summary>
        /// Ensures the backstage is open. If it is currently closed, clicks the
        /// File button to open it. If already open, does nothing.
        /// </summary>
        public void EnsureBackstageOpen()
        {
            if (IsBackstageOpen()) return;
            Driver!.FindElement(MobileBy.Name("File")).Click();
            Thread.Sleep(800);
        }
    }
}
