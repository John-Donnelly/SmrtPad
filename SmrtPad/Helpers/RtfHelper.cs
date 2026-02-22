using System;
using System.Text;

namespace SmrtPad.Helpers
{
    /// <summary>
    /// Generates RTF markup for insertable document elements.
    /// </summary>
    public static class RtfHelper
    {
        /// <summary>
        /// Generates an RTF table with the specified number of rows and columns.
        /// Each cell is 2000 twips wide with single-line borders.
        /// </summary>
        public static string GenerateTable(int rows, int cols)
        {
            if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows), "Rows must be positive.");
            if (cols <= 0) throw new ArgumentOutOfRangeException(nameof(cols), "Columns must be positive.");

            var rtf = new StringBuilder();
            rtf.Append(@"{\rtf1\ansi ");

            for (int r = 0; r < rows; r++)
            {
                rtf.Append(@"\trowd ");
                for (int c = 0; c < cols; c++)
                {
                    int cellRight = (c + 1) * 2000;
                    rtf.Append($@"\clbrdrt\brdrs\clbrdrl\brdrs\clbrdrb\brdrs\clbrdrr\brdrs\cellx{cellRight} ");
                }
                for (int c = 0; c < cols; c++)
                {
                    rtf.Append($@" \cell ");
                }
                rtf.Append(@"\row ");
            }
            rtf.Append('}');
            return rtf.ToString();
        }
    }
}
