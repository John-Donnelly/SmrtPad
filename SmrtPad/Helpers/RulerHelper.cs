namespace SmrtPad.Helpers
{
    /// <summary>
    /// Provides ruler measurement calculations independent of UI rendering.
    /// </summary>
    public static class RulerHelper
    {
        private const double ScreenDpi = 96.0;
        private const double CmPerInch = 2.54;
        private const double PtPerInch = 72.0;
        private const double PicaPerInch = 6.0;  // 1 pica = 12 pt; 72 pt/in ÷ 12 = 6 pica/in

        /// <summary>
        /// Calculates the number of pixels per ruler unit at the given zoom level.
        /// </summary>
        /// <param name="rulerUnits">"cm", "pt", "pc", or anything else for inches.</param>
        /// <param name="zoomPercent">Zoom percentage (e.g. 100.0 for 100%).</param>
        /// <param name="unitLabel">Returns the short unit label.</param>
        /// <returns>Pixels per ruler unit, scaled by zoom.</returns>
        public static double GetPixelsPerUnit(string rulerUnits, double zoomPercent, out string unitLabel)
        {
            double scale = zoomPercent / 100.0;
            switch (rulerUnits)
            {
                case "cm":
                    unitLabel = "cm";
                    return (ScreenDpi / CmPerInch) * scale;
                case "pt":
                    unitLabel = "pt";
                    return (ScreenDpi / PtPerInch) * scale;
                case "pc":
                    unitLabel = "pc";
                    return (ScreenDpi / PicaPerInch) * scale;
                default:
                    unitLabel = "in";
                    return ScreenDpi * scale;
            }
        }
    }
}
