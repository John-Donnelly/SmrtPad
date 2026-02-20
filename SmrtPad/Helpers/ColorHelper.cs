using System;
using Windows.UI;

namespace SmrtPad.Helpers
{
    public static class ColorHelper
    {
        public static Color ParseHexColor(string hex)
        {
            hex = hex.TrimStart('#');
            byte r = 0, g = 0, b = 0, a = 255;
            if (hex.Length == 6)
            {
                r = Convert.ToByte(hex.Substring(0, 2), 16);
                g = Convert.ToByte(hex.Substring(2, 2), 16);
                b = Convert.ToByte(hex.Substring(4, 2), 16);
            }
            else if (hex.Length == 8)
            {
                a = Convert.ToByte(hex.Substring(0, 2), 16);
                r = Convert.ToByte(hex.Substring(2, 2), 16);
                g = Convert.ToByte(hex.Substring(4, 2), 16);
                b = Convert.ToByte(hex.Substring(6, 2), 16);
            }
            return Color.FromArgb(a, r, g, b);
        }
    }
}
