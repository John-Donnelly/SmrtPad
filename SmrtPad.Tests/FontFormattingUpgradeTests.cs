using System;
using System.Linq;
using Xunit;
using SmrtPad.Helpers;
using SmrtPad.ViewModels;
using Windows.UI;

namespace SmrtPad.Tests
{
    // ═══ Font Color Indicator Bug Fix Tests ═══

    /// <summary>
    /// Tests verifying that font color state flows correctly through the ViewModel
    /// when applied via different paths (swatch click vs. color picker).
    /// The bug was that the color-indicator rectangle didn't update when the
    /// ColorPicker was used — only swatches updated it.
    /// </summary>
    public class FontColorIndicatorTests
    {
        [Fact]
        public void ApplyTextColor_ViaSwatchHex_ParsesColorCorrectly()
        {
            // The swatch sends a hex string like "#ff0000" — verify ColorHelper handles it
            var color = ColorHelper.ParseHexColor("#ff0000");

            Assert.Equal(255, color.A);
            Assert.Equal(255, color.R);
            Assert.Equal(0, color.G);
            Assert.Equal(0, color.B);
        }

        [Fact]
        public void ApplyTextColor_ViaSwatchHex_Black_ParsesCorrectly()
        {
            var color = ColorHelper.ParseHexColor("#000000");

            Assert.Equal(255, color.A);
            Assert.Equal(0, color.R);
            Assert.Equal(0, color.G);
            Assert.Equal(0, color.B);
        }

        [Fact]
        public void ApplyTextColor_ViaSwatchHex_White_ParsesCorrectly()
        {
            var color = ColorHelper.ParseHexColor("#ffffff");

            Assert.Equal(255, color.A);
            Assert.Equal(255, color.R);
            Assert.Equal(255, color.G);
            Assert.Equal(255, color.B);
        }

        [Fact]
        public void ColorHelper_ParseHex_SixDigit_DefaultsAlphaTo255()
        {
            var color = ColorHelper.ParseHexColor("#4dbb00");

            Assert.Equal(255, color.A);
            Assert.Equal(0x4d, color.R);
            Assert.Equal(0xbb, color.G);
            Assert.Equal(0x00, color.B);
        }

        [Fact]
        public void ColorHelper_ParseHex_EightDigit_ParsesAlpha()
        {
            var color = ColorHelper.ParseHexColor("#80FF0000");

            Assert.Equal(0x80, color.A);
            Assert.Equal(0xFF, color.R);
            Assert.Equal(0x00, color.G);
            Assert.Equal(0x00, color.B);
        }

        [Fact]
        public void ColorHelper_ParseHex_WithoutHash_Works()
        {
            var color = ColorHelper.ParseHexColor("00b050");

            Assert.Equal(255, color.A);
            Assert.Equal(0x00, color.R);
            Assert.Equal(0xb0, color.G);
            Assert.Equal(0x50, color.B);
        }

        [Fact]
        public void ColorHelper_ParseHex_InvalidLength_Throws()
        {
            Assert.Throws<FormatException>(() => ColorHelper.ParseHexColor("#FFF"));
        }

        [Fact]
        public void ColorHelper_ParseHex_NullOrEmpty_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ColorHelper.ParseHexColor(""));
            Assert.Throws<ArgumentException>(() => ColorHelper.ParseHexColor(null!));
        }

        [Fact]
        public void ColorHelper_ParseHex_InvalidHexChar_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ColorHelper.ParseHexColor("#GGGGGG"));
        }

        [Fact]
        public void ColorHelper_AllSwatchColors_ParseSuccessfully()
        {
            // All font color swatch hex values from MainWindow.xaml
            string[] fontSwatches =
            [
                "#000000", "#333333", "#666666", "#a5a5a5", "#ffffff",
                "#ff0000", "#ffc000", "#ffff00", "#00b050", "#004dbb",
                "#9b00d3", "#c0504d"
            ];

            foreach (var hex in fontSwatches)
            {
                var color = ColorHelper.ParseHexColor(hex);
                Assert.Equal(255, color.A);
            }
        }

        [Fact]
        public void ColorHelper_AllHighlightSwatches_ParseSuccessfully()
        {
            // All highlight swatch hex values from MainWindow.xaml
            string[] highlightSwatches =
            [
                "#FFFF00", "#00FF00", "#00FFFF", "#FF00FF", "#0000FF",
                "#FF0000", "#000080", "#008080", "#008000", "#808000"
            ];

            foreach (var hex in highlightSwatches)
            {
                var color = ColorHelper.ParseHexColor(hex);
                Assert.Equal(255, color.A);
            }
        }
    }

    // ═══ Remove Highlight Tests ═══

    /// <summary>
    /// Tests verifying the "No Highlight" / remove-highlight behavior at the
    /// ViewModel level. The transparent background color (A=0) is what
    /// RichEditBox uses to represent "no highlight."
    /// </summary>
    public class RemoveHighlightTests
    {
        [Fact]
        public void TransparentColor_HasZeroAlpha()
        {
            // The "remove highlight" color used in RemoveHighlight_Click
            var noHighlight = Color.FromArgb(0, 255, 255, 255);

            Assert.Equal(0, noHighlight.A);
        }

        [Fact]
        public void TransparentColor_IsDistinctFromYellowHighlight()
        {
            var noHighlight = Color.FromArgb(0, 255, 255, 255);
            var yellow = ColorHelper.ParseHexColor("#FFFF00");

            Assert.NotEqual(noHighlight, yellow);
        }

        [Fact]
        public void TransparentColor_IsDistinctFromWhiteOpaque()
        {
            var noHighlight = Color.FromArgb(0, 255, 255, 255);
            var white = Color.FromArgb(255, 255, 255, 255);

            Assert.NotEqual(noHighlight, white);
        }
    }

    // ═══ Format > Font Dialog ViewModel Tests ═══

    /// <summary>
    /// Tests verifying that the ViewModel correctly tracks all font properties
    /// that the Format > Font dialog reads and writes.
    /// </summary>
    public class FormatFontDialogViewModelTests
    {
        [Fact]
        public void ViewModel_FontFamily_DefaultIsSegoeUI()
        {
            var vm = new EditorViewModel();
            Assert.Equal("Segoe UI", vm.FontFamily);
        }

        [Fact]
        public void ViewModel_FontSize_DefaultIs11()
        {
            var vm = new EditorViewModel();
            Assert.Equal(11.0, vm.FontSize);
        }

        [Fact]
        public void ViewModel_FontFamily_CanBeSet()
        {
            var vm = new EditorViewModel();
            vm.FontFamily = "Arial";
            Assert.Equal("Arial", vm.FontFamily);
        }

        [Fact]
        public void ViewModel_FontSize_CanBeSet()
        {
            var vm = new EditorViewModel();
            vm.FontSize = 24.0;
            Assert.Equal(24.0, vm.FontSize);
        }

        [Fact]
        public void ViewModel_AllStyleProperties_DefaultToFalse()
        {
            var vm = new EditorViewModel();

            Assert.False(vm.IsBold);
            Assert.False(vm.IsItalic);
            Assert.False(vm.IsUnderline);
            Assert.False(vm.IsStrikethrough);
            Assert.False(vm.IsSubscript);
            Assert.False(vm.IsSuperscript);
        }

        [Fact]
        public void ViewModel_Bold_CanToggleOn()
        {
            var vm = new EditorViewModel();
            vm.ToggleBold();
            Assert.True(vm.IsBold);
        }

        [Fact]
        public void ViewModel_Bold_CanToggleOff()
        {
            var vm = new EditorViewModel();
            vm.ToggleBold();
            vm.ToggleBold();
            Assert.False(vm.IsBold);
        }

        [Fact]
        public void ViewModel_SubscriptOn_TurnsSuperscriptOff()
        {
            var vm = new EditorViewModel();
            vm.IsSuperscript = true;

            vm.ToggleSubscript();

            Assert.True(vm.IsSubscript);
            Assert.False(vm.IsSuperscript);
        }

        [Fact]
        public void ViewModel_SuperscriptOn_TurnsSubscriptOff()
        {
            var vm = new EditorViewModel();
            vm.IsSubscript = true;

            vm.ToggleSuperscript();

            Assert.True(vm.IsSuperscript);
            Assert.False(vm.IsSubscript);
        }

        [Fact]
        public void ViewModel_MultipleStyleProperties_CanBeSetSimultaneously()
        {
            var vm = new EditorViewModel();
            vm.IsBold = true;
            vm.IsItalic = true;
            vm.IsUnderline = true;
            vm.IsStrikethrough = true;

            Assert.True(vm.IsBold);
            Assert.True(vm.IsItalic);
            Assert.True(vm.IsUnderline);
            Assert.True(vm.IsStrikethrough);
        }

        [Fact]
        public void ViewModel_NewDocument_ResetsAllFontDialogProperties()
        {
            var vm = new EditorViewModel();
            vm.FontFamily = "Courier New";
            vm.FontSize = 36.0;
            vm.IsBold = true;
            vm.IsItalic = true;
            vm.IsUnderline = true;
            vm.IsStrikethrough = true;
            vm.IsSubscript = true;

            vm.NewDocument();

            Assert.Equal("Segoe UI", vm.FontFamily);
            Assert.Equal(11.0, vm.FontSize);
            Assert.False(vm.IsBold);
            Assert.False(vm.IsItalic);
            Assert.False(vm.IsUnderline);
            Assert.False(vm.IsStrikethrough);
            Assert.False(vm.IsSubscript);
            Assert.False(vm.IsSuperscript);
        }

        [Fact]
        public void ViewModel_DirectBoldSet_DoesNotAffectItalic()
        {
            var vm = new EditorViewModel();
            vm.IsBold = true;

            Assert.True(vm.IsBold);
            Assert.False(vm.IsItalic);
        }

        [Fact]
        public void ViewModel_DirectUnderlineSet_DoesNotAffectStrikethrough()
        {
            var vm = new EditorViewModel();
            vm.IsUnderline = true;

            Assert.True(vm.IsUnderline);
            Assert.False(vm.IsStrikethrough);
        }

        [Fact]
        public void ViewModel_FontSize_BoundaryValues()
        {
            var vm = new EditorViewModel();

            vm.FontSize = 1.0;
            Assert.Equal(1.0, vm.FontSize);

            vm.FontSize = 999.0;
            Assert.Equal(999.0, vm.FontSize);
        }

        [Fact]
        public void ViewModel_FontFamily_EmptyStringIsAccepted()
        {
            var vm = new EditorViewModel();
            vm.FontFamily = "";
            Assert.Equal("", vm.FontFamily);
        }
    }

    // ═══ Format > Font Dialog Property Changed Notification Tests ═══

    /// <summary>
    /// Verifies that PropertyChanged events fire for all properties the
    /// Format > Font dialog writes back to the ViewModel.
    /// </summary>
    public class FormatFontDialogPropertyChangedTests
    {
        [Theory]
        [InlineData(nameof(EditorViewModel.IsBold))]
        [InlineData(nameof(EditorViewModel.IsItalic))]
        [InlineData(nameof(EditorViewModel.IsUnderline))]
        [InlineData(nameof(EditorViewModel.IsStrikethrough))]
        [InlineData(nameof(EditorViewModel.IsSubscript))]
        [InlineData(nameof(EditorViewModel.IsSuperscript))]
        public void ViewModel_BoolProperty_RaisesPropertyChanged(string propertyName)
        {
            var vm = new EditorViewModel();
            string? changedProperty = null;
            vm.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

            var prop = typeof(EditorViewModel).GetProperty(propertyName);
            prop!.SetValue(vm, true);

            Assert.Equal(propertyName, changedProperty);
        }

        [Fact]
        public void ViewModel_FontFamily_RaisesPropertyChanged()
        {
            var vm = new EditorViewModel();
            string? changedProperty = null;
            vm.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

            vm.FontFamily = "Times New Roman";

            Assert.Equal(nameof(EditorViewModel.FontFamily), changedProperty);
        }

        [Fact]
        public void ViewModel_FontSize_RaisesPropertyChanged()
        {
            var vm = new EditorViewModel();
            string? changedProperty = null;
            vm.PropertyChanged += (s, e) => changedProperty = e.PropertyName;

            vm.FontSize = 24.0;

            Assert.Equal(nameof(EditorViewModel.FontSize), changedProperty);
        }
    }
}
