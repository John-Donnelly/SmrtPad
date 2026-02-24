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
    /// Functional UI tests for paragraph formatting: indentation controls,
    /// list type selection, paragraph style application, and line spacing.
    /// Tests verify that clicking ribbon buttons produces the expected UI
    /// state changes.
    ///
    /// Tests share one Appium session via <see cref="SharedAppFixture"/>.
    /// </summary>
    public sealed class ParagraphFormattingUITests : IClassFixture<SharedAppFixture>, IDisposable
    {
        private readonly SharedAppFixture _fx;
        private readonly WindowsDriver? _driver;

        public ParagraphFormattingUITests(SharedAppFixture fx)
        {
            _fx = fx;
            _driver = fx.Driver;
        }

        public void Dispose() { /* session owned by fixture */ }

        private void RequireDriver() =>
            Skip.If(!_fx.IsAvailable,
                "WinAppDriver / Appium not available or SmrtPad.exe not built.");

        // ── Indent buttons ───────────────────────────────────────────────────

        /// <summary>
        /// The Increase Indent button should be present and clickable
        /// without causing an error.
        /// </summary>
        [SkippableFact]
        public void IncreaseIndent_Button_IsPresent_AndClickable()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("indent test");

            var btn = _driver!.FindElement(MobileBy.Name("Increase Indent"));
            Assert.NotNull(btn);

            btn.Click();
            Thread.Sleep(200);

            // Verify editor still functional
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);
        }

        /// <summary>
        /// The Decrease Indent button should be present and clickable.
        /// </summary>
        [SkippableFact]
        public void DecreaseIndent_Button_IsPresent_AndClickable()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("indent test");

            // First increase indent so there's something to decrease
            _driver!.FindElement(MobileBy.Name("Increase Indent")).Click();
            Thread.Sleep(200);

            var btn = _driver!.FindElement(MobileBy.Name("Decrease Indent"));
            Assert.NotNull(btn);

            btn.Click();
            Thread.Sleep(200);

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);
        }

        /// <summary>
        /// Clicking Increase Indent multiple times and then Decrease Indent
        /// should work without errors, testing the indent/deindent round-trip.
        /// </summary>
        [SkippableFact]
        public void IndentRoundTrip_IncreaseAndDecrease_WorksCorrectly()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("round trip indent");

            // Increase indent 3 times
            for (int i = 0; i < 3; i++)
            {
                _driver!.FindElement(MobileBy.Name("Increase Indent")).Click();
                Thread.Sleep(150);
            }

            // Decrease indent 3 times
            for (int i = 0; i < 3; i++)
            {
                _driver!.FindElement(MobileBy.Name("Decrease Indent")).Click();
                Thread.Sleep(150);
            }

            // Editor should still be functional
            _fx.TypeInEditor(" still works");
            string wordCount = _fx.GetStatusBarText("WordCountText");
            Assert.Equal("Words: 5", wordCount);
        }

        // ── List type dropdown ───────────────────────────────────────────────

        /// <summary>
        /// The list type button flyout should contain all seven list type options.
        /// </summary>
        [SkippableFact]
        public void ListTypeButton_FlyoutContains_AllListTypes()
        {
            RequireDriver();

            var listBtn = _driver!.FindElement(MobileBy.AccessibilityId("ListTypeButton"));
            Assert.NotNull(listBtn);

            // Right-click or open the flyout
            listBtn.Click();
            Thread.Sleep(500);

            // Verify all list type menu items are present
            string[] listTypeIds =
            [
                "ListTypeNoneItem", "ListTypeBulletItem", "ListTypeNumberItem",
                "ListTypeLowerLetterItem", "ListTypeUpperLetterItem",
                "ListTypeLowerRomanItem", "ListTypeUpperRomanItem"
            ];

            foreach (string id in listTypeIds)
            {
                var item = _driver!.FindElement(MobileBy.AccessibilityId(id));
                Assert.NotNull(item);
            }

            // Close the flyout by pressing Escape
            _driver!.FindElement(MobileBy.AccessibilityId("ListTypeNoneItem"))
                .SendKeys(Keys.Escape);
            Thread.Sleep(200);
        }

        /// <summary>
        /// Selecting the Bullet list type should apply without error.
        /// </summary>
        [SkippableFact]
        public void ListType_SelectBullet_AppliesWithoutError()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("bullet item");

            var listBtn = _driver!.FindElement(MobileBy.AccessibilityId("ListTypeButton"));
            listBtn.Click();
            Thread.Sleep(500);

            _driver!.FindElement(MobileBy.AccessibilityId("ListTypeBulletItem")).Click();
            Thread.Sleep(300);

            // Verify editor is still functional
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);

            // Reset to None
            listBtn = _driver!.FindElement(MobileBy.AccessibilityId("ListTypeButton"));
            listBtn.Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.AccessibilityId("ListTypeNoneItem")).Click();
            Thread.Sleep(200);
        }

        /// <summary>
        /// Selecting the Number list type should apply without error.
        /// </summary>
        [SkippableFact]
        public void ListType_SelectNumbers_AppliesWithoutError()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("numbered item");

            var listBtn = _driver!.FindElement(MobileBy.AccessibilityId("ListTypeButton"));
            listBtn.Click();
            Thread.Sleep(500);

            _driver!.FindElement(MobileBy.AccessibilityId("ListTypeNumberItem")).Click();
            Thread.Sleep(300);

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);

            // Reset to None
            listBtn = _driver!.FindElement(MobileBy.AccessibilityId("ListTypeButton"));
            listBtn.Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.AccessibilityId("ListTypeNoneItem")).Click();
            Thread.Sleep(200);
        }

        // ── Paragraph styles ─────────────────────────────────────────────────

        /// <summary>
        /// The paragraph styles flyout should contain Normal, Heading 1/2/3,
        /// Subtitle, and Quote options.
        /// </summary>
        [SkippableFact]
        public void ParagraphStyles_FlyoutContains_AllStyles()
        {
            RequireDriver();

            // The Styles button has a tooltip "Styles"
            var stylesBtn = _driver!.FindElement(MobileBy.Name("Styles"));
            Assert.NotNull(stylesBtn);

            stylesBtn.Click();
            Thread.Sleep(500);

            // Verify style menu items are present
            string[] styleNames = ["Normal", "Heading 1", "Heading 2", "Heading 3", "Subtitle", "Quote"];

            foreach (string name in styleNames)
            {
                var item = _driver!.FindElement(MobileBy.Name(name));
                Assert.NotNull(item);
            }

            // Close the flyout
            _driver!.FindElement(MobileBy.Name("Normal")).SendKeys(Keys.Escape);
            Thread.Sleep(200);
        }

        /// <summary>
        /// Applying Heading 1 style should apply without error and the editor
        /// should remain functional.
        /// </summary>
        [SkippableFact]
        public void ParagraphStyle_ApplyHeading1_WorksCorrectly()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("My Heading");
            _fx.SelectAllInEditor();

            var stylesBtn = _driver!.FindElement(MobileBy.Name("Styles"));
            stylesBtn.Click();
            Thread.Sleep(500);

            _driver!.FindElement(MobileBy.Name("Heading 1")).Click();
            Thread.Sleep(300);

            // Verify editor is still functional
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);

            // Reset to Normal
            _fx.SelectAllInEditor();
            stylesBtn = _driver!.FindElement(MobileBy.Name("Styles"));
            stylesBtn.Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.Name("Normal")).Click();
            Thread.Sleep(200);
        }

        // ── Line spacing dropdown ────────────────────────────────────────────

        /// <summary>
        /// The line spacing button flyout should contain presets 1.0, 1.15, 1.5, 2.0
        /// and the Custom option.
        /// </summary>
        [SkippableFact]
        public void LineSpacing_FlyoutContains_AllPresets()
        {
            RequireDriver();

            var lineSpacingBtn = _driver!.FindElement(MobileBy.AccessibilityId("LineSpacingButton"));
            Assert.NotNull(lineSpacingBtn);

            lineSpacingBtn.Click();
            Thread.Sleep(500);

            string[] presets = ["1.0", "1.15", "1.5", "2.0"];
            foreach (string preset in presets)
            {
                var item = _driver!.FindElement(MobileBy.Name(preset));
                Assert.NotNull(item);
            }

            // Also verify Custom option
            var customItem = _driver!.FindElement(MobileBy.AccessibilityId("LineSpacingCustomItem"));
            Assert.NotNull(customItem);

            // Close flyout
            customItem.SendKeys(Keys.Escape);
            Thread.Sleep(200);
        }

        /// <summary>
        /// Selecting 2.0 line spacing should apply without error.
        /// </summary>
        [SkippableFact]
        public void LineSpacing_Select2Point0_AppliesWithoutError()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("spaced text");

            var lineSpacingBtn = _driver!.FindElement(MobileBy.AccessibilityId("LineSpacingButton"));
            lineSpacingBtn.Click();
            Thread.Sleep(500);

            _driver!.FindElement(MobileBy.Name("2.0")).Click();
            Thread.Sleep(300);

            // Verify editor is still functional
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);

            // Reset to 1.0
            lineSpacingBtn = _driver!.FindElement(MobileBy.AccessibilityId("LineSpacingButton"));
            lineSpacingBtn.Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.Name("1.0")).Click();
            Thread.Sleep(200);
        }

        // ── Clear formatting ─────────────────────────────────────────────────

        /// <summary>
        /// Applying bold formatting and then clicking Clear Formatting should
        /// reset the bold toggle to unchecked.
        /// </summary>
        [SkippableFact]
        public void ClearFormatting_AfterBold_ResetsBoldToggle()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("clear format test");
            _fx.SelectAllInEditor();

            // Apply bold
            _driver!.FindElement(MobileBy.AccessibilityId("BoldToggle")).Click();
            Thread.Sleep(200);
            Assert.True(_fx.IsToggleChecked("BoldToggle"));

            // Re-select all
            _fx.SelectAllInEditor();

            // Click Clear Formatting
            var clearBtn = _driver!.FindElement(MobileBy.Name("Clear Formatting"));
            clearBtn.Click();
            Thread.Sleep(300);

            // Re-select and check bold is off
            _fx.SelectAllInEditor();
            Thread.Sleep(200);
            Assert.False(_fx.IsToggleChecked("BoldToggle"),
                "Clear Formatting should reset bold to off");
        }

        // ── Font grow/shrink ─────────────────────────────────────────────────

        /// <summary>
        /// Clicking Grow Font and then Shrink Font should complete without error.
        /// </summary>
        [SkippableFact]
        public void GrowFont_ThenShrinkFont_CompletesWithoutError()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("font size test");
            _fx.SelectAllInEditor();

            _driver!.FindElement(MobileBy.Name("Grow Font")).Click();
            Thread.Sleep(200);

            _driver!.FindElement(MobileBy.Name("Shrink Font")).Click();
            Thread.Sleep(200);

            // Editor should still be functional
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);
        }
    }
}
