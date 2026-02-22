namespace SmrtPad.Helpers
{
    /// <summary>
    /// Provides ruler measurement calculations independent of UI rendering.
    /// </summary>
    public static class RulerHelper
    {
        private const double ScreenDpi = 96.0;
        private const double CmPerInch = 2.54;

        /// <summary>
        /// Calculates the number of pixels per ruler unit at the given zoom level.
        /// </summary>
        /// <param name="rulerUnits">"cm" for centimeters, anything else for inches.</param>
        /// <param name="zoomPercent">Zoom percentage (e.g. 100.0 for 100%).</param>
        /// <param name="unitLabel">Returns "cm" or "in" based on the ruler unit setting.</param>
        /// <returns>Pixels per ruler unit, scaled by zoom.</returns>
        public static double GetPixelsPerUnit(string rulerUnits, double zoomPercent, out string unitLabel)
        {
            bool useCm = rulerUnits == "cm";
            unitLabel = useCm ? "cm" : "in";
            double basePixels = useCm ? ScreenDpi / CmPerInch : ScreenDpi;
            double scale = zoomPercent / 100.0;
            return basePixels * scale;
        }
    }
}
