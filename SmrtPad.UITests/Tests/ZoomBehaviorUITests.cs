using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using Xunit;
using SmrtPad.UITests.Infrastructure;

namespace SmrtPad.UITests.Tests
{
    /// <summary>
    /// Comprehensive zoom behaviour tests.
    ///
    /// Covers:
    ///   - Status bar reflects correct zoom percentage after in/out
    ///   - Editor element remains accessible and functional at any zoom level
    ///   - Layout bounds of the editor are stable (ScaleTransform must not affect layout)
    ///   - Content is preserved across zoom operations
    ///   - Typing works at any zoom level
    ///   - Zoom clamping at min (10%) and max (500%)
    ///   - Round-tripping back to 100%
    ///   - Visual: the editor panel background fills the full viewport even when zoomed
    ///     below 100% (screenshot pixel test — verifies the fix for the "panel shrinks"
    ///     regression).
    /// </summary>
    public sealed class ZoomBehaviorUITests : IClassFixture<SharedAppFixture>, IDisposable
    {
        private readonly SharedAppFixture _fx;
        private readonly WindowsDriver? _driver;

        public ZoomBehaviorUITests(SharedAppFixture fx)
        {
            _fx = fx;
            _driver = fx.Driver;
        }

        public void Dispose() { }

        private void RequireDriver() =>
            Skip.If(!_fx.IsAvailable,
                "WinAppDriver / Appium not available or SmrtPad.exe not built.");

        // ── Zoom helpers ──────────────────────────────────────────────────────

        private string ZoomPercent =>
            _fx.GetStatusBarText("ZoomText");

        private int CurrentZoom()
        {
            string raw = ZoomPercent.Replace("%", "").Trim();
            return int.TryParse(raw, out int v) ? v : -1;
        }

        private void ResetZoomTo100()
        {
            for (int i = 0; i < 60; i++)
            {
                int z = CurrentZoom();
                if (z == 100) return;
                _fx.ClickMenuItem("View", z > 100 ? "Zoom Out" : "Zoom In");
                Thread.Sleep(120);
            }
        }

        private void SetZoom(int target)
        {
            for (int i = 0; i < 60; i++)
            {
                int z = CurrentZoom();
                if (z == target) return;
                _fx.ClickMenuItem("View", z > target ? "Zoom Out" : "Zoom In");
                Thread.Sleep(120);
            }
        }

        private AppiumElement Editor =>
            _driver!.FindElement(MobileBy.AccessibilityId("Editor"));

        // ── Status bar ────────────────────────────────────────────────────────

        [SkippableFact]
        public void ZoomIn_StatusBar_ShowsIncreasedPercent()
        {
            RequireDriver();
            ResetZoomTo100();

            _fx.ClickMenuItem("View", "Zoom In");
            Thread.Sleep(200);

            Assert.Equal("110%", ZoomPercent);
            ResetZoomTo100();
        }

        [SkippableFact]
        public void ZoomOut_StatusBar_ShowsDecreasedPercent()
        {
            RequireDriver();
            ResetZoomTo100();

            _fx.ClickMenuItem("View", "Zoom Out");
            Thread.Sleep(200);

            Assert.Equal("90%", ZoomPercent);
            ResetZoomTo100();
        }

        [SkippableFact]
        public void ZoomIn_ThreeSteps_Shows130Percent()
        {
            RequireDriver();
            ResetZoomTo100();

            _fx.ClickMenuItem("View", "Zoom In");
            _fx.ClickMenuItem("View", "Zoom In");
            _fx.ClickMenuItem("View", "Zoom In");

            Assert.Equal("130%", ZoomPercent);
            ResetZoomTo100();
        }

        [SkippableFact]
        public void ZoomOut_ThreeSteps_Shows70Percent()
        {
            RequireDriver();
            ResetZoomTo100();

            _fx.ClickMenuItem("View", "Zoom Out");
            _fx.ClickMenuItem("View", "Zoom Out");
            _fx.ClickMenuItem("View", "Zoom Out");

            Assert.Equal("70%", ZoomPercent);
            ResetZoomTo100();
        }

        // ── Editor accessibility at extreme zoom levels ───────────────────────

        [SkippableFact]
        public void ZoomIn_EditorRemainsAccessible()
        {
            RequireDriver();
            ResetZoomTo100();
            SetZoom(130);
            Thread.Sleep(300);

            var editor = Editor;
            Assert.NotNull(editor);
            Assert.True(editor.Enabled);
            Assert.True(editor.Displayed);

            ResetZoomTo100();
        }

        [SkippableFact]
        public void ZoomOut_EditorRemainsAccessible()
        {
            RequireDriver();
            ResetZoomTo100();
            SetZoom(70);
            Thread.Sleep(300);

            var editor = Editor;
            Assert.NotNull(editor);
            Assert.True(editor.Enabled);
            Assert.True(editor.Displayed);

            ResetZoomTo100();
        }

        // ── Layout stability: ScaleTransform must NOT change the layout rect ──

        /// <summary>
        /// The editor element's reported bounding rectangle (UIA layout bounds) must
        /// be identical regardless of zoom level — <see cref="System.Windows.Media.ScaleTransform"/>
        /// is render-only and must not affect layout.
        /// </summary>
        [SkippableFact]
        public void ZoomOut_EditorLayoutBoundsUnchanged()
        {
            RequireDriver();
            ResetZoomTo100();
            Thread.Sleep(200);

            var at100 = Editor.Rect;

            SetZoom(70);
            Thread.Sleep(300);

            var at70 = Editor.Rect;

            const int tol = 4;
            Assert.InRange(at70.Width,  at100.Width  - tol, at100.Width  + tol);
            Assert.InRange(at70.Height, at100.Height - tol, at100.Height + tol);
            Assert.InRange(at70.X,      at100.X      - tol, at100.X      + tol);
            Assert.InRange(at70.Y,      at100.Y      - tol, at100.Y      + tol);

            ResetZoomTo100();
        }

        [SkippableFact]
        public void ZoomIn_EditorLayoutBoundsUnchanged()
        {
            RequireDriver();
            ResetZoomTo100();
            Thread.Sleep(200);

            var at100 = Editor.Rect;

            SetZoom(130);
            Thread.Sleep(300);

            var at130 = Editor.Rect;

            const int tol = 4;
            Assert.InRange(at130.Width,  at100.Width  - tol, at100.Width  + tol);
            Assert.InRange(at130.Height, at100.Height - tol, at100.Height + tol);

            ResetZoomTo100();
        }

        // ── Content preservation ──────────────────────────────────────────────

        [SkippableFact]
        public void ZoomOut_ContentPreserved()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("Hello zoom test");
            ResetZoomTo100();

            SetZoom(70);
            Thread.Sleep(300);

            var editorText = Editor.Text;
            Assert.Contains("Hello zoom test", editorText);

            _fx.ClearEditor();
            ResetZoomTo100();
        }

        [SkippableFact]
        public void ZoomIn_ContentPreserved()
        {
            RequireDriver();
            _fx.ClearEditor();
            _fx.TypeInEditor("Hello zoom test");
            ResetZoomTo100();

            SetZoom(130);
            Thread.Sleep(300);

            var editorText = Editor.Text;
            Assert.Contains("Hello zoom test", editorText);

            _fx.ClearEditor();
            ResetZoomTo100();
        }

        // ── Typing at non-100% zoom ───────────────────────────────────────────

        [SkippableFact]
        public void ZoomOut_CanStillTypeInEditor()
        {
            RequireDriver();
            _fx.ClearEditor();
            ResetZoomTo100();
            SetZoom(70);
            Thread.Sleep(300);

            _fx.TypeInEditor("typed at 70 percent");

            Assert.Contains("typed at 70 percent", Editor.Text);

            _fx.ClearEditor();
            ResetZoomTo100();
        }

        [SkippableFact]
        public void ZoomIn_CanStillTypeInEditor()
        {
            RequireDriver();
            _fx.ClearEditor();
            ResetZoomTo100();
            SetZoom(130);
            Thread.Sleep(300);

            _fx.TypeInEditor("typed at 130 percent");

            Assert.Contains("typed at 130 percent", Editor.Text);

            _fx.ClearEditor();
            ResetZoomTo100();
        }

        // ── Zoom clamping ─────────────────────────────────────────────────────

        [SkippableFact]
        public void ZoomOut_DoesNotGoBelowMinimum()
        {
            RequireDriver();

            // Drive to minimum
            for (int i = 0; i < 100; i++)
                _fx.ClickMenuItem("View", "Zoom Out");
            Thread.Sleep(500);

            int z = CurrentZoom();
            Assert.True(z >= 10, $"Zoom should be >= 10% at minimum, was {z}%");

            ResetZoomTo100();
        }

        [SkippableFact]
        public void ZoomIn_DoesNotExceedMaximum()
        {
            RequireDriver();

            // Drive to maximum
            for (int i = 0; i < 60; i++)
                _fx.ClickMenuItem("View", "Zoom In");
            Thread.Sleep(500);

            int z = CurrentZoom();
            Assert.True(z <= 500, $"Zoom should be <= 500% at maximum, was {z}%");

            ResetZoomTo100();
        }

        // ── Round-trip ────────────────────────────────────────────────────────

        [SkippableFact]
        public void Zoom_RoundTrip_ReturnsTo100()
        {
            RequireDriver();
            ResetZoomTo100();

            _fx.ClickMenuItem("View", "Zoom In");
            _fx.ClickMenuItem("View", "Zoom In");
            _fx.ClickMenuItem("View", "Zoom In");
            _fx.ClickMenuItem("View", "Zoom Out");
            _fx.ClickMenuItem("View", "Zoom Out");
            _fx.ClickMenuItem("View", "Zoom Out");
            Thread.Sleep(300);

            Assert.Equal("100%", ZoomPercent);
        }

        // ── Editor bounds at multiple zoom-out levels ──────────────────────────

        /// <summary>
        /// The editor's UIA bounding rect must remain stable at 50 % zoom (extreme
        /// zoom-out).  If the pane shrinks, Width/Height will decrease.
        /// </summary>
        [SkippableFact]
        public void ZoomOut_50Percent_EditorBoundsStable()
        {
            RequireDriver();
            ResetZoomTo100();
            Thread.Sleep(200);

            var at100 = Editor.Rect;

            SetZoom(50);
            Thread.Sleep(300);

            var at50 = Editor.Rect;

            const int tol = 4;
            Assert.InRange(at50.Width,  at100.Width  - tol, at100.Width  + tol);
            Assert.InRange(at50.Height, at100.Height - tol, at100.Height + tol);
            Assert.InRange(at50.X,      at100.X      - tol, at100.X      + tol);
            Assert.InRange(at50.Y,      at100.Y      - tol, at100.Y      + tol);

            ResetZoomTo100();
        }

        /// <summary>
        /// Steps from 100 % down to 50 % in 10 % increments, verifying that the
        /// editor layout bounds never shrink at any intermediate step.
        /// </summary>
        [SkippableFact]
        public void ZoomOut_StepByStep_EditorBoundsNeverShrink()
        {
            RequireDriver();
            ResetZoomTo100();
            Thread.Sleep(200);

            var baseline = Editor.Rect;
            const int tol = 4;

            for (int target = 90; target >= 50; target -= 10)
            {
                SetZoom(target);
                Thread.Sleep(200);
                var rect = Editor.Rect;

                Assert.InRange(rect.Width,  baseline.Width  - tol, baseline.Width  + tol);
                Assert.InRange(rect.Height, baseline.Height - tol, baseline.Height + tol);
            }

            ResetZoomTo100();
        }

        /// <summary>
        /// Verifies that the editor height occupies a reasonable portion of the
        /// window at both 100 % and 70 % zoom — i.e. the pane fills the viewport.
        /// </summary>
        [SkippableFact]
        public void ZoomOut_EditorFillsViewport()
        {
            RequireDriver();
            ResetZoomTo100();
            Thread.Sleep(200);

            var winSize = _driver!.Manage().Window.Size;
            var at100   = Editor.Rect;

            // Editor should use at least 40 % of the window height (ribbon + tabs + status bar take the rest).
            Assert.True(at100.Height >= winSize.Height * 0.4,
                $"Editor height ({at100.Height}) is less than 40% of window height ({winSize.Height}) at 100% zoom.");

            SetZoom(70);
            Thread.Sleep(300);

            var at70 = Editor.Rect;

            // After zoom-out the editor LAYOUT height must still be the same fraction.
            Assert.True(at70.Height >= winSize.Height * 0.4,
                $"Editor height ({at70.Height}) is less than 40% of window height ({winSize.Height}) at 70% zoom. The pane shrank.");

            const int tol = 4;
            Assert.InRange(at70.Height, at100.Height - tol, at100.Height + tol);

            ResetZoomTo100();
        }

        // ── Visual: panel background fills viewport below 100% ────────────────

        /// <summary>
        /// At zoom-out (70 %), the editor panel background must still fill the full
        /// viewport.  The <see cref="System.Windows.Media.ScaleTransform"/> is render-
        /// only; the <c>EditorContainer</c> always occupies the full layout space.
        /// When this test fails it means the VISUAL panel is shrinking — i.e. the
        /// container background is missing and the window backdrop bleeds through.
        ///
        /// The test samples a pixel at 85 % of the editor's layout height (below the
        /// 70 % visual edge of the scaled RichEditBox).  That pixel should be the same
        /// "editor-background" colour at 100 % and at 70 % zoom.
        /// </summary>
        [SkippableFact]
        public void ZoomOut_PanelBackground_FillsFullViewport()
        {
            RequireDriver();
            _fx.ClearEditor();
            Thread.Sleep(200);

            // ── Baseline at 100 % ─────────────────────────────────────────────
            ResetZoomTo100();
            Thread.Sleep(300);

            var editorRect = Editor.Rect;
            (int px, int py) = SamplePoint(editorRect, xFraction: 0.5, yFraction: 0.85);

            Color color100 = SampleScreenPixel(px, py);

            // ── Zoom to 70 % ──────────────────────────────────────────────────
            SetZoom(70);
            Thread.Sleep(400);

            Color color70 = SampleScreenPixel(px, py);

            // At 70 % zoom the RichEditBox's visual bottom edge is at 70 % of the
            // layout height.  Our sample point is at 85 %, so it is OUTSIDE the
            // scaled editor visual.  Without a container background it shows the
            // window backdrop (Mica/Acrylic — not the editor colour).
            // With the fix the EditorContainer background fills that area with the
            // same colour as the editor background.
            Assert.True(
                ColorsAreSimilar(color100, color70, tolerance: 30),
                $"Pixel at ({px},{py}) changed after zoom-out: 100%={color100} 70%={color70}. " +
                $"The EditorContainer background is not filling the viewport below 100% zoom.");

            _fx.ClearEditor();
            ResetZoomTo100();
        }

        /// <summary>
        /// Symmetric check: at zoom-in (130%) the editor panel must also look stable
        /// — the scaled editor visual overflows its layout bounds slightly but the
        /// container clips it; the panel must not visually detach or shift.
        /// </summary>
        [SkippableFact]
        public void ZoomIn_PanelBackground_DoesNotShift()
        {
            RequireDriver();
            _fx.ClearEditor();
            Thread.Sleep(200);

            ResetZoomTo100();
            Thread.Sleep(300);

            var editorRect = Editor.Rect;
            // Sample near the top-left where text begins — should always be editor bg.
            (int px, int py) = SamplePoint(editorRect, xFraction: 0.1, yFraction: 0.1);

            Color color100 = SampleScreenPixel(px, py);

            SetZoom(130);
            Thread.Sleep(400);

            Color color130 = SampleScreenPixel(px, py);

            Assert.True(
                ColorsAreSimilar(color100, color130, tolerance: 40),
                $"Pixel at ({px},{py}) changed after zoom-in: 100%={color100} 130%={color130}. " +
                $"The editor top-left anchor is shifting on zoom-in.");

            _fx.ClearEditor();
            ResetZoomTo100();
        }

        // ── Screenshot helpers ────────────────────────────────────────────────

        /// <summary>
        /// Converts a fraction of the element rect to screen pixel coordinates,
        /// accounting for the display DPI scale factor.
        /// </summary>
        private (int x, int y) SamplePoint(System.Drawing.Rectangle rect,
                                            double xFraction, double yFraction)
        {
            // WinAppDriver element rects are in logical (DPI-independent) pixels.
            // Screenshots are at physical pixels.  Infer scale from window vs screenshot.
            double scale = GetDpiScale();
            int x = (int)((rect.X + rect.Width  * xFraction) * scale);
            int y = (int)((rect.Y + rect.Height * yFraction) * scale);
            return (x, y);
        }

        private double _cachedDpiScale = -1;

        private double GetDpiScale()
        {
            if (_cachedDpiScale > 0) return _cachedDpiScale;

            var shot = _driver!.GetScreenshot();
            using var bmp = ScreenshotToBitmap(shot);
            var winSize = _driver.Manage().Window.Size;

            // Guard against zero/negative window dimensions
            if (winSize.Width <= 0 || winSize.Height <= 0)
            {
                _cachedDpiScale = 1.0;
                return _cachedDpiScale;
            }

            double sx = (double)bmp.Width  / winSize.Width;
            double sy = (double)bmp.Height / winSize.Height;
            _cachedDpiScale = (sx + sy) / 2.0;  // average of X and Y scale
            return _cachedDpiScale;
        }

        private Color SampleScreenPixel(int x, int y)
        {
            var shot = _driver!.GetScreenshot();
            using var bmp = ScreenshotToBitmap(shot);

            // Clamp to bitmap dimensions
            x = Math.Clamp(x, 0, bmp.Width  - 1);
            y = Math.Clamp(y, 0, bmp.Height - 1);
            return bmp.GetPixel(x, y);
        }

        private static Bitmap ScreenshotToBitmap(OpenQA.Selenium.Screenshot shot)
        {
            var bytes = shot.AsByteArray;
            using var ms = new MemoryStream(bytes);
            return new Bitmap(ms);
        }

        private static bool ColorsAreSimilar(Color a, Color b, int tolerance)
        {
            return Math.Abs(a.R - b.R) <= tolerance
                && Math.Abs(a.G - b.G) <= tolerance
                && Math.Abs(a.B - b.B) <= tolerance;
        }
    }
}
