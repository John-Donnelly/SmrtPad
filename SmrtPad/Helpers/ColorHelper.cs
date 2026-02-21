using System;
using Windows.UI;

namespace SmrtPad.Helpers
{
    public static class ColorHelper
    {
        public static Color ParseHexColor(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                throw new ArgumentException("Hex color string cannot be null or empty.", nameof(hex));

            hex = hex.TrimStart('#');

            if (hex.Length != 6 && hex.Length != 8)
                throw new FormatException($"Invalid hex color format: expected 6 or 8 hex digits, got {hex.Length}.");

            foreach (char c in hex)
            {
                if (!Uri.IsHexDigit(c))
                    throw new FormatException($"Invalid hex character '{c}' in color string.");
            }

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
