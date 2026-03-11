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
    /// Structural UI tests verifying that all ribbon Insert group buttons,
    /// Editing group elements, and ribbon section labels are present and
    /// accessible in the SmrtPad main window.
    ///
    /// Tests share one Appium session via <see cref="SharedAppFixture"/>.
    /// </summary>
    [Collection("UITests")]
    public sealed class RibbonInsertAndEditingUITests : IClassFixture<SharedAppFixture>, IDisposable
    {
        private readonly SharedAppFixture _fx;
        private readonly WindowsDriver? _driver;

        public RibbonInsertAndEditingUITests(SharedAppFixture fx)
        {
            _fx = fx;
            _driver = fx.Driver;
        }

        public void Dispose() { /* session owned by fixture */ }

        private void RequireDriver() =>
            Skip.If(!_fx.IsAvailable,
                "WinAppDriver / Appium not available or SmrtPad.exe not built.");

        // ── Insert group buttons ─────────────────────────────────────────────

        /// <summary>
        /// The "Picture" button should be present in the Insert ribbon group.
        /// </summary>
        [SkippableFact]
        public void InsertGroup_PictureButton_IsPresent()
        {
            RequireDriver();
            var btn = _driver!.FindElement(MobileBy.Name("Picture"));
            Assert.NotNull(btn);
        }

        /// <summary>
        /// The "SmrtDoodle" button should be present in the Insert ribbon group.
        /// </summary>
        [SkippableFact]
        public void InsertGroup_SmrtDoodleButton_IsPresent()
        {
            RequireDriver();
            var btn = _driver!.FindElement(MobileBy.AccessibilityId("SmrtDoodleButton"));
            Assert.NotNull(btn);
        }

        /// <summary>
        /// The "Object" button should be present in the Insert ribbon group.
        /// </summary>
        [SkippableFact]
        public void InsertGroup_ObjectButton_IsPresent()
        {
            RequireDriver();
            var btn = _driver!.FindElement(MobileBy.AccessibilityId("ObjectButton"));
            Assert.NotNull(btn);
        }

        /// <summary>
        /// The "Date/Time" button should be present in the Insert ribbon group.
        /// </summary>
        [SkippableFact]
        public void InsertGroup_DateTimeButton_IsPresent()
        {
            RequireDriver();
            var btn = _driver!.FindElement(MobileBy.AccessibilityId("DateTimeButton"));
            Assert.NotNull(btn);
        }

        /// <summary>
        /// The "Link" button should be present in the Insert ribbon group.
        /// </summary>
        [SkippableFact]
        public void InsertGroup_HyperlinkButton_IsPresent()
        {
            RequireDriver();
            var btn = _driver!.FindElement(MobileBy.AccessibilityId("HyperlinkButton"));
            Assert.NotNull(btn);
        }

        /// <summary>
        /// The "Table" button should be present in the Insert ribbon group.
        /// </summary>
        [SkippableFact]
        public void InsertGroup_TableButton_IsPresent()
        {
            RequireDriver();
            var btn = _driver!.FindElement(MobileBy.AccessibilityId("TableButton"));
            Assert.NotNull(btn);
        }

        /// <summary>
        /// The "Symbol" button should be present in the Insert ribbon group.
        /// </summary>
        [SkippableFact]
        public void InsertGroup_SymbolButton_IsPresent()
        {
            RequireDriver();
            var btn = _driver!.FindElement(MobileBy.AccessibilityId("SymbolButton"));
            Assert.NotNull(btn);
        }

        // ── Editing group elements ───────────────────────────────────────────

        /// <summary>
        /// The "Find" button should be present in the Editing ribbon group.
        /// </summary>
        [SkippableFact]
        public void EditingGroup_FindButton_IsPresent()
        {
            RequireDriver();
            var btn = _driver!.FindElement(MobileBy.Name("Find"));
            Assert.NotNull(btn);
        }

        /// <summary>
        /// The "Replace" button should be present in the Editing ribbon group.
        /// </summary>
        [SkippableFact]
        public void EditingGroup_ReplaceButton_IsPresent()
        {
            RequireDriver();
            var btn = _driver!.FindElement(MobileBy.Name("Replace"));
            Assert.NotNull(btn);
        }

        /// <summary>
        /// The "Select all" button should be present in the Editing ribbon group.
        /// </summary>
        [SkippableFact]
        public void EditingGroup_SelectAllButton_IsPresent()
        {
            RequireDriver();
            var btn = _driver!.FindElement(MobileBy.Name("Select all"));
            Assert.NotNull(btn);
        }

        // ── Ribbon section labels ────────────────────────────────────────────

        /// <summary>
        /// The "Clipboard" section label should be present in the ribbon.
        /// </summary>
        [SkippableFact]
        public void RibbonLabel_Clipboard_IsPresent()
        {
            RequireDriver();
            var label = _driver!.FindElement(MobileBy.Name("Clipboard"));
            Assert.NotNull(label);
        }

        /// <summary>
        /// The "Font" section label should be present in the ribbon.
        /// </summary>
        [SkippableFact]
        public void RibbonLabel_Font_IsPresent()
        {
            RequireDriver();
            var label = _driver!.FindElement(MobileBy.Name("Font"));
            Assert.NotNull(label);
        }

        /// <summary>
        /// The "Paragraph" section label should be present in the ribbon.
        /// </summary>
        [SkippableFact]
        public void RibbonLabel_Paragraph_IsPresent()
        {
            RequireDriver();
            var label = _driver!.FindElement(MobileBy.Name("Paragraph"));
            Assert.NotNull(label);
        }

        /// <summary>
        /// The "Insert" section label should be present in the ribbon.
        /// </summary>
        [SkippableFact]
        public void RibbonLabel_Insert_IsPresent()
        {
            RequireDriver();
            var label = _driver!.FindElement(MobileBy.Name("Insert"));
            Assert.NotNull(label);
        }

        /// <summary>
        /// The "Editing" section label should be present in the ribbon.
        /// </summary>
        [SkippableFact]
        public void RibbonLabel_Editing_IsPresent()
        {
            RequireDriver();
            var label = _driver!.FindElement(MobileBy.Name("Editing"));
            Assert.NotNull(label);
        }

        // ── Quick-access toolbar buttons ─────────────────────────────────────

        /// <summary>
        /// The Save button should be accessible via its tooltip.
        /// </summary>
        [SkippableFact]
        public void QuickAccess_SaveButton_IsPresent()
        {
            RequireDriver();
            var btn = _driver!.FindElement(MobileBy.AccessibilityId("SaveButton"));
            Assert.NotNull(btn);
        }

        /// <summary>
        /// The New button should be accessible in the quick-access toolbar.
        /// </summary>
        [SkippableFact]
        public void QuickAccess_NewButton_IsPresent()
        {
            RequireDriver();
            var btn = _driver!.FindElement(MobileBy.AccessibilityId("NewButton"));
            Assert.NotNull(btn);
        }

        /// <summary>
        /// The Undo button should be accessible via its AutomationId.
        /// </summary>
        [SkippableFact]
        public void QuickAccess_UndoButton_IsPresent()
        {
            RequireDriver();
            var btn = _driver!.FindElement(MobileBy.AccessibilityId("UndoButton"));
            Assert.NotNull(btn);
        }

        /// <summary>
        /// The Redo button should be accessible via its tooltip.
        /// </summary>
        [SkippableFact]
        public void QuickAccess_RedoButton_IsPresent()
        {
            RequireDriver();
            var btn = _driver!.FindElement(MobileBy.AccessibilityId("RedoButton"));
            Assert.NotNull(btn);
        }
    }
}
