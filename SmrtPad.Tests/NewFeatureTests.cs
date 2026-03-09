using System;
using System.IO;
using Xunit;
using SmrtPad.Helpers;
using SmrtPad.Services;
using SmrtPad.ViewModels;

namespace SmrtPad.Tests
{
    // ═══ Step 1: Ctrl+D duplicate — ViewModel helpers ═══

    public class DuplicateLineTests
    {
        [Fact]
        public void ZoomLevel_DefaultsTo100()
        {
            var vm = new EditorViewModel();
            Assert.Equal(100.0, vm.ZoomLevel);
        }

        [Fact]
        public void ZoomDisplay_ShowsPercentSuffix()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 150.0;
            Assert.Equal("150%", vm.ZoomDisplay);
        }
    }

    // ═══ Step 2: Zoom slider clamp logic ═══

    public class ZoomSliderTests
    {
        [Theory]
        [InlineData(10.0)]
        [InlineData(100.0)]
        [InlineData(500.0)]
        public void ZoomLevel_WithinBounds_IsAccepted(double level)
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = level;
            Assert.Equal(level, vm.ZoomLevel);
        }

        [Fact]
        public void ZoomIn_IncreasesLevelBy10()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 100.0;
            vm.ZoomIn();
            Assert.Equal(110.0, vm.ZoomLevel);
        }

        [Fact]
        public void ZoomOut_DecreasesLevelBy10()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 100.0;
            vm.ZoomOut();
            Assert.Equal(90.0, vm.ZoomLevel);
        }

        [Fact]
        public void ZoomIn_ClampsAt500()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 500.0;
            vm.ZoomIn();
            Assert.Equal(500.0, vm.ZoomLevel);
        }

        [Fact]
        public void ZoomOut_ClampsAt10()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 10.0;
            vm.ZoomOut();
            Assert.Equal(10.0, vm.ZoomLevel);
        }

        [Fact]
        public void ZoomDisplay_Formats_WithPercentSign()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 75.0;
            Assert.Equal("75%", vm.ZoomDisplay);
        }
    }

    // ═══ Step 4: Status bar visibility setting persistence ═══

    public class StatusBarSettingTests
    {
        [Fact]
        public void ShowStatusBar_DefaultsToTrue()
        {
            using var tempFile = new TempSettingsFile();
            var settings = new SettingsService(tempFile.Path);
            Assert.True(settings.ShowStatusBar);
        }

        [Fact]
        public void ShowStatusBar_CanBePersisted()
        {
            using var tempFile = new TempSettingsFile();
            var settings = new SettingsService(tempFile.Path);
            settings.ShowStatusBar = false;
            settings.Save();

            var reloaded = new SettingsService(tempFile.Path);
            Assert.False(reloaded.ShowStatusBar);
        }

        [Fact]
        public void ShowStatusBar_RoundTrip_TrueValue()
        {
            using var tempFile = new TempSettingsFile();
            var settings = new SettingsService(tempFile.Path);
            settings.ShowStatusBar = true;
            settings.Save();

            var reloaded = new SettingsService(tempFile.Path);
            Assert.True(reloaded.ShowStatusBar);
        }
    }

    // ═══ Step 7: RulerHelper Points & Picas ═══

    public class RulerHelperPointsPicasTests
    {
        private const double ScreenDpi = 96.0;

        [Fact]
        public void GetPixelsPerUnit_Inches_Returns96AtZoom100()
        {
            double result = RulerHelper.GetPixelsPerUnit("in", 100.0, out string label);
            Assert.Equal(ScreenDpi, result, precision: 5);
            Assert.Equal("in", label);
        }

        [Fact]
        public void GetPixelsPerUnit_Cm_ReturnsCorrectValue()
        {
            double expected = ScreenDpi / 2.54;
            double result = RulerHelper.GetPixelsPerUnit("cm", 100.0, out string label);
            Assert.Equal(expected, result, precision: 5);
            Assert.Equal("cm", label);
        }

        [Fact]
        public void GetPixelsPerUnit_Points_Returns96Over72AtZoom100()
        {
            double expected = ScreenDpi / 72.0;
            double result = RulerHelper.GetPixelsPerUnit("pt", 100.0, out string label);
            Assert.Equal(expected, result, precision: 5);
            Assert.Equal("pt", label);
        }

        [Fact]
        public void GetPixelsPerUnit_Picas_Returns96Over6AtZoom100()
        {
            double expected = ScreenDpi / 6.0;
            double result = RulerHelper.GetPixelsPerUnit("pc", 100.0, out string label);
            Assert.Equal(expected, result, precision: 5);
            Assert.Equal("pc", label);
        }

        [Fact]
        public void GetPixelsPerUnit_ScalesWithZoom()
        {
            double at100 = RulerHelper.GetPixelsPerUnit("in", 100.0, out _);
            double at200 = RulerHelper.GetPixelsPerUnit("in", 200.0, out _);
            Assert.Equal(at100 * 2, at200, precision: 5);
        }

        [Fact]
        public void GetPixelsPerUnit_Points_1ptIs96Over72Pixels()
        {
            // 1 point = 1/72 inch = 96/72 px ≈ 1.333 px at 100% zoom
            double result = RulerHelper.GetPixelsPerUnit("pt", 100.0, out _);
            Assert.Equal(96.0 / 72.0, result, precision: 4);
        }

        [Fact]
        public void GetPixelsPerUnit_Picas_1PicaIs16Pixels()
        {
            // 1 pica = 12 pt = 1/6 inch = 96/6 = 16 px at 100% zoom
            double result = RulerHelper.GetPixelsPerUnit("pc", 100.0, out _);
            Assert.Equal(16.0, result, precision: 5);
        }
    }

    // ═══ Step 8: Word wrap mode setting persistence ═══

    public class WordWrapModeTests
    {
        [Fact]
        public void WordWrapMode_DefaultsToWrap()
        {
            using var tempFile = new TempSettingsFile();
            var settings = new SettingsService(tempFile.Path);
            Assert.Equal("Wrap", settings.WordWrapMode);
        }

        [Theory]
        [InlineData("Off")]
        [InlineData("Wrap")]
        [InlineData("WrapToRuler")]
        public void WordWrapMode_CanBePersistedAndReloaded(string mode)
        {
            using var tempFile = new TempSettingsFile();
            var settings = new SettingsService(tempFile.Path);
            settings.WordWrapMode = mode;
            settings.Save();

            var reloaded = new SettingsService(tempFile.Path);
            Assert.Equal(mode, reloaded.WordWrapMode);
        }
    }

    // ═══ Helpers ═══

    /// <summary>
    /// Creates a temporary settings file path that is deleted on dispose.
    /// </summary>
    internal sealed class TempSettingsFile : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"smrtpad_test_{Guid.NewGuid():N}.json");

        public void Dispose()
        {
            if (File.Exists(Path))
                File.Delete(Path);
        }
    }
}
