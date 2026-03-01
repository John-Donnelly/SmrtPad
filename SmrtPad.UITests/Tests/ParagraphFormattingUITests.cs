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

            var btn = _driver!.FindElement(MobileBy.AccessibilityId("IncreaseIndentButton"));
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
            _driver!.FindElement(MobileBy.AccessibilityId("IncreaseIndentButton")).Click();
            Thread.Sleep(200);

            var btn = _driver!.FindElement(MobileBy.AccessibilityId("DecreaseIndentButton"));
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
                _driver!.FindElement(MobileBy.AccessibilityId("IncreaseIndentButton")).Click();
                Thread.Sleep(150);
            }

            // Decrease indent 3 times
            for (int i = 0; i < 3; i++)
            {
                _driver!.FindElement(MobileBy.AccessibilityId("DecreaseIndentButton")).Click();
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
            var stylesBtn = _driver!.FindElement(MobileBy.AccessibilityId("StylesButton"));
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

            var stylesBtn = _driver!.FindElement(MobileBy.AccessibilityId("StylesButton"));
            stylesBtn.Click();
            Thread.Sleep(500);

            _driver!.FindElement(MobileBy.Name("Heading 1")).Click();
            Thread.Sleep(300);

            // Verify editor is still functional
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);

            // Reset to Normal
            _fx.SelectAllInEditor();
            stylesBtn = _driver!.FindElement(MobileBy.AccessibilityId("StylesButton"));
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

            _fx.SelectAllInEditor();
            Thread.Sleep(200);
            Assert.True(_fx.IsToggleChecked("BoldToggle"));

            // Re-select all
            _fx.SelectAllInEditor();
            Thread.Sleep(200);

            // Click Clear Formatting
            var clearBtn = _driver!.FindElement(MobileBy.AccessibilityId("ClearFormattingButton"));
            clearBtn.Click();
            Thread.Sleep(300);

            // Click editor to restore focus, then re-select and check bold is off
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            editor.Click();
            Thread.Sleep(200);
            _fx.SelectAllInEditor();
            Thread.Sleep(300);
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

            _driver!.FindElement(MobileBy.AccessibilityId("GrowFontButton")).Click();
            Thread.Sleep(200);

            _driver!.FindElement(MobileBy.AccessibilityId("ShrinkFontButton")).Click();
            Thread.Sleep(200);

            // Editor should still be functional
            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);
        }

        // ── List type: lowercase letters ─────────────────────────────────────

        /// <summary>
        /// Selecting the Lowercase Letters list type should apply without error.
        /// </summary>
        [SkippableFact]
        public void ListType_SelectLowercaseLetters_AppliesWithoutError()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("letter item");

            var listBtn = _driver!.FindElement(MobileBy.AccessibilityId("ListTypeButton"));
            listBtn.Click();
            Thread.Sleep(500);

            _driver!.FindElement(MobileBy.AccessibilityId("ListTypeLowerLetterItem")).Click();
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

        // ── List type: uppercase letters ─────────────────────────────────────

        /// <summary>
        /// Selecting the Uppercase Letters list type should apply without error.
        /// </summary>
        [SkippableFact]
        public void ListType_SelectUppercaseLetters_AppliesWithoutError()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("upper letter item");

            var listBtn = _driver!.FindElement(MobileBy.AccessibilityId("ListTypeButton"));
            listBtn.Click();
            Thread.Sleep(500);

            _driver!.FindElement(MobileBy.AccessibilityId("ListTypeUpperLetterItem")).Click();
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

        // ── List type: lowercase roman ───────────────────────────────────────

        /// <summary>
        /// Selecting the Lowercase Roman list type should apply without error.
        /// </summary>
        [SkippableFact]
        public void ListType_SelectLowercaseRoman_AppliesWithoutError()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("roman item");

            var listBtn = _driver!.FindElement(MobileBy.AccessibilityId("ListTypeButton"));
            listBtn.Click();
            Thread.Sleep(500);

            _driver!.FindElement(MobileBy.AccessibilityId("ListTypeLowerRomanItem")).Click();
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

        // ── List type: uppercase roman ───────────────────────────────────────

        /// <summary>
        /// Selecting the Uppercase Roman list type should apply without error.
        /// </summary>
        [SkippableFact]
        public void ListType_SelectUppercaseRoman_AppliesWithoutError()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("uppercase roman item");

            var listBtn = _driver!.FindElement(MobileBy.AccessibilityId("ListTypeButton"));
            listBtn.Click();
            Thread.Sleep(500);

            _driver!.FindElement(MobileBy.AccessibilityId("ListTypeUpperRomanItem")).Click();
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

        // ── List type switch preserves content ───────────────────────────────

        /// <summary>
        /// Switching between list types should not alter the word count.
        /// </summary>
        [SkippableFact]
        public void ListType_SwitchTypes_PreservesWordCount()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("list content test");
            string wordsBefore = _fx.GetStatusBarText("WordCountText");

            var listBtn = _driver!.FindElement(MobileBy.AccessibilityId("ListTypeButton"));
            listBtn.Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.AccessibilityId("ListTypeBulletItem")).Click();
            Thread.Sleep(300);

            Assert.Equal(wordsBefore, _fx.GetStatusBarText("WordCountText"));

            listBtn = _driver!.FindElement(MobileBy.AccessibilityId("ListTypeButton"));
            listBtn.Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.AccessibilityId("ListTypeNumberItem")).Click();
            Thread.Sleep(300);

            Assert.Equal(wordsBefore, _fx.GetStatusBarText("WordCountText"));

            // Reset
            listBtn = _driver!.FindElement(MobileBy.AccessibilityId("ListTypeButton"));
            listBtn.Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.AccessibilityId("ListTypeNoneItem")).Click();
            Thread.Sleep(200);
        }

        // ── Line spacing 1.15 ────────────────────────────────────────────────

        /// <summary>
        /// Selecting 1.15 line spacing should apply without error.
        /// </summary>
        [SkippableFact]
        public void LineSpacing_Select1Point15_AppliesWithoutError()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("spacing test");

            var lineSpacingBtn = _driver!.FindElement(MobileBy.AccessibilityId("LineSpacingButton"));
            lineSpacingBtn.Click();
            Thread.Sleep(500);

            _driver!.FindElement(MobileBy.Name("1.15")).Click();
            Thread.Sleep(300);

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);

            // Reset to 1.0
            lineSpacingBtn = _driver!.FindElement(MobileBy.AccessibilityId("LineSpacingButton"));
            lineSpacingBtn.Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.Name("1.0")).Click();
            Thread.Sleep(200);
        }

        // ── Line spacing 1.5 ─────────────────────────────────────────────────

        /// <summary>
        /// Selecting 1.5 line spacing should apply without error.
        /// </summary>
        [SkippableFact]
        public void LineSpacing_Select1Point5_AppliesWithoutError()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("spacing test");

            var lineSpacingBtn = _driver!.FindElement(MobileBy.AccessibilityId("LineSpacingButton"));
            lineSpacingBtn.Click();
            Thread.Sleep(500);

            _driver!.FindElement(MobileBy.Name("1.5")).Click();
            Thread.Sleep(300);

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);

            // Reset to 1.0
            lineSpacingBtn = _driver!.FindElement(MobileBy.AccessibilityId("LineSpacingButton"));
            lineSpacingBtn.Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.Name("1.0")).Click();
            Thread.Sleep(200);
        }

        // ── Heading 2 style ──────────────────────────────────────────────────

        /// <summary>
        /// Applying Heading 2 style should apply without error.
        /// </summary>
        [SkippableFact]
        public void ParagraphStyle_ApplyHeading2_WorksCorrectly()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("Sub Heading");
            _fx.SelectAllInEditor();

            _driver!.FindElement(MobileBy.AccessibilityId("StylesButton")).Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.Name("Heading 2")).Click();
            Thread.Sleep(300);

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);

            // Reset to Normal
            _fx.SelectAllInEditor();
            _driver!.FindElement(MobileBy.AccessibilityId("StylesButton")).Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.Name("Normal")).Click();
            Thread.Sleep(200);
        }

        // ── Heading 3 style ──────────────────────────────────────────────────

        /// <summary>
        /// Applying Heading 3 style should apply without error.
        /// </summary>
        [SkippableFact]
        public void ParagraphStyle_ApplyHeading3_WorksCorrectly()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("Minor Heading");
            _fx.SelectAllInEditor();

            _driver!.FindElement(MobileBy.AccessibilityId("StylesButton")).Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.Name("Heading 3")).Click();
            Thread.Sleep(300);

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);

            // Reset to Normal
            _fx.SelectAllInEditor();
            _driver!.FindElement(MobileBy.AccessibilityId("StylesButton")).Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.Name("Normal")).Click();
            Thread.Sleep(200);
        }

        // ── Subtitle style ───────────────────────────────────────────────────

        /// <summary>
        /// Applying Subtitle style should apply without error.
        /// </summary>
        [SkippableFact]
        public void ParagraphStyle_ApplySubtitle_WorksCorrectly()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("My Subtitle");
            _fx.SelectAllInEditor();

            _driver!.FindElement(MobileBy.AccessibilityId("StylesButton")).Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.Name("Subtitle")).Click();
            Thread.Sleep(300);

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);

            // Reset to Normal
            _fx.SelectAllInEditor();
            _driver!.FindElement(MobileBy.AccessibilityId("StylesButton")).Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.Name("Normal")).Click();
            Thread.Sleep(200);
        }

        // ── Quote style ──────────────────────────────────────────────────────

        /// <summary>
        /// Applying Quote style should apply without error.
        /// </summary>
        [SkippableFact]
        public void ParagraphStyle_ApplyQuote_WorksCorrectly()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("A famous quote");
            _fx.SelectAllInEditor();

            _driver!.FindElement(MobileBy.AccessibilityId("StylesButton")).Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.Name("Quote")).Click();
            Thread.Sleep(300);

            var editor = _driver!.FindElement(MobileBy.AccessibilityId("Editor"));
            Assert.NotNull(editor);

            // Reset to Normal
            _fx.SelectAllInEditor();
            _driver!.FindElement(MobileBy.AccessibilityId("StylesButton")).Click();
            Thread.Sleep(500);
            _driver!.FindElement(MobileBy.Name("Normal")).Click();
            Thread.Sleep(200);
        }

        // ── Indent preserves word count ──────────────────────────────────────

        /// <summary>
        /// Multiple indent levels should not change the word count.
        /// </summary>
        [SkippableFact]
        public void MultipleIndentLevels_PreservesWordCount()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("indent preserves count");

            string wordsBefore = _fx.GetStatusBarText("WordCountText");

            for (int i = 0; i < 3; i++)
            {
                _driver!.FindElement(MobileBy.AccessibilityId("IncreaseIndentButton")).Click();
                Thread.Sleep(150);
            }

            Assert.Equal(wordsBefore, _fx.GetStatusBarText("WordCountText"));

            // Reset
            for (int i = 0; i < 3; i++)
            {
                _driver!.FindElement(MobileBy.AccessibilityId("DecreaseIndentButton")).Click();
                Thread.Sleep(150);
            }
        }
    }
}
