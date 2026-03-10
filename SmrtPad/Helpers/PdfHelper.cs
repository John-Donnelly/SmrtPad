using System;
using System.Collections.Generic;
using System.Text;

namespace SmrtPad.Helpers
{
    /// <summary>
    /// Generates minimal valid PDF 1.4 documents from plain text.
    /// Supports multi-page output with word-wrap; uses Helvetica (Type1) embedded font.
    /// </summary>
    public static class PdfHelper
    {
        // A4 page dimensions in PDF user-units (1/72 inch)
        private const double PageWidth = 595;
        private const double PageHeight = 842;
        private const double MarginLeft = 72;
        private const double MarginRight = 72;
        private const double MarginTop = 72;
        private const double MarginBottom = 72;

        private const double DefaultFontSize = 12.0;
        private const double LineHeightFactor = 1.4;

        private static double ContentWidth => PageWidth - MarginLeft - MarginRight;
        private static double ContentHeight => PageHeight - MarginTop - MarginBottom;

        /// <summary>Generates a PDF byte array from plain text content.</summary>
        public static byte[] GeneratePdf(string text, double fontSize = DefaultFontSize)
        {
            ArgumentNullException.ThrowIfNull(text);
            if (!double.IsFinite(fontSize) || fontSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fontSize), "Font size must be a positive finite value.");
            }

            double lineHeight = fontSize * LineHeightFactor;
            int linesPerPage = Math.Max(1, (int)(ContentHeight / lineHeight));

            // Estimate characters per line (Helvetica ~0.55 em average)
            int charsPerLine = Math.Max(1, (int)(ContentWidth / (fontSize * 0.55)));

            // Normalise line endings and split into display lines
            var displayLines = BuildDisplayLines(text, charsPerLine);

            // Partition display lines into pages
            List<List<string>> pages = [];
            for (int i = 0; i < displayLines.Count; i += linesPerPage)
            {
                int count = Math.Min(linesPerPage, displayLines.Count - i);
                pages.Add(displayLines.GetRange(i, count));
            }
            if (pages.Count == 0) pages.Add([]);

            // ── Build PDF object table ──────────────────────────────────────────
            // Objects:
            //   1 = Catalog
            //   2 = Pages node
            //   3 = Font (Helvetica)
            //   4 .. 4+N-1 = Page objects     (N pages)
            //   4+N .. 4+2N-1 = Content streams (one per page)

            int N = pages.Count;
            int firstPageObj = 4;
            int firstContentObj = 4 + N;
            int totalObjects = 4 + 2 * N;   // 1-based count; object 0 is always the free entry

            var sb = new StringBuilder();
            sb.Append("%PDF-1.4\n");
            sb.Append("%\xc2\xb5\xc2\xb6\n");   // binary hint comment

            var offsets = new int[totalObjects + 1]; // offsets[i] = byte offset of object i
            offsets[0] = 0;

            // Helper: record offset and emit object header
            void BeginObj(int id)
            {
                offsets[id] = sb.Length;
                sb.Append($"{id} 0 obj\n");
            }

            // ── Object 1: Catalog ────────────────────────────────────────────────
            BeginObj(1);
            sb.Append("<</Type /Catalog /Pages 2 0 R>>\n");
            sb.Append("endobj\n");

            // ── Object 2: Pages ──────────────────────────────────────────────────
            BeginObj(2);
            sb.Append("<</Type /Pages /Kids [");
            for (int p = 0; p < N; p++) sb.Append($"{firstPageObj + p} 0 R ");
            sb.Append($"] /Count {N}>>\n");
            sb.Append("endobj\n");

            // ── Object 3: Font ───────────────────────────────────────────────────
            BeginObj(3);
            sb.Append("<</Type /Font /Subtype /Type1 /BaseFont /Helvetica ");
            sb.Append("/Encoding /WinAnsiEncoding>>\n");
            sb.Append("endobj\n");

            // ── Page objects ────────────────────────────────────────────────────
            for (int p = 0; p < N; p++)
            {
                BeginObj(firstPageObj + p);
                sb.Append("<</Type /Page /Parent 2 0 R ");
                sb.Append($"/MediaBox [0 0 {(int)PageWidth} {(int)PageHeight}] ");
                sb.Append($"/Contents {firstContentObj + p} 0 R ");
                sb.Append("/Resources <</Font <</F1 3 0 R>>>>>>\n");
                sb.Append("endobj\n");
            }

            // ── Content streams ─────────────────────────────────────────────────
            for (int p = 0; p < N; p++)
            {
                string stream = BuildPageStream(pages[p], fontSize, lineHeight);
                byte[] streamBytes = Encoding.Latin1.GetBytes(stream);
                BeginObj(firstContentObj + p);
                sb.Append($"<</Length {streamBytes.Length}>>\n");
                sb.Append("stream\n");

                // Flush current sb to bytes, then append stream bytes
                byte[] preamble = Encoding.Latin1.GetBytes(sb.ToString());
                offsets[firstContentObj + p] = preamble.Length;

                // We need to rebuild using byte arrays; delegate to a two-pass approach
                // (simpler: just append as latin1 string since stream is latin1-safe)
                sb.Append(stream);
                sb.Append("\nendstream\n");
                sb.Append("endobj\n");
            }

            // ── Cross-reference table ───────────────────────────────────────────
            // Recalculate offsets properly (two-pass)
            string pdfBody = BuildPdfBody(pages, fontSize, lineHeight, N,
                firstPageObj, firstContentObj, totalObjects);

            return Encoding.Latin1.GetBytes(pdfBody);
        }

        private static string BuildPdfBody(
            List<List<string>> pages, double fontSize, double lineHeight,
            int N, int firstPageObj, int firstContentObj, int totalObjects)
        {
            var offsets = new int[totalObjects + 1];
            var buf = new StringBuilder();

            buf.Append("%PDF-1.4\n%\xc2\xb5\xc2\xb6\n");

            void BeginObj(int id)
            {
                offsets[id] = buf.Length;
                buf.Append($"{id} 0 obj\n");
            }

            BeginObj(1);
            buf.Append("<</Type /Catalog /Pages 2 0 R>>\nendobj\n");

            BeginObj(2);
            buf.Append("<</Type /Pages /Kids [");
            for (int p = 0; p < N; p++) buf.Append($"{firstPageObj + p} 0 R ");
            buf.Append($"] /Count {N}>>\nendobj\n");

            BeginObj(3);
            buf.Append("<</Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding>>\nendobj\n");

            for (int p = 0; p < N; p++)
            {
                BeginObj(firstPageObj + p);
                buf.Append($"<</Type /Page /Parent 2 0 R /MediaBox [0 0 {(int)PageWidth} {(int)PageHeight}] ");
                buf.Append($"/Contents {firstContentObj + p} 0 R ");
                buf.Append("/Resources <</Font <</F1 3 0 R>>>>>>\nendobj\n");
            }

            for (int p = 0; p < N; p++)
            {
                string stream = BuildPageStream(pages[p], fontSize, lineHeight);
                BeginObj(firstContentObj + p);
                buf.Append($"<</Length {stream.Length}>>\nstream\n");
                buf.Append(stream);
                buf.Append("\nendstream\nendobj\n");
            }

            int xrefOffset = buf.Length;
            buf.Append($"xref\n0 {totalObjects + 1}\n");
            buf.Append("0000000000 65535 f \n");
            for (int i = 1; i <= totalObjects; i++)
                buf.Append($"{offsets[i]:D10} 00000 n \n");

            buf.Append("trailer\n");
            buf.Append($"<</Size {totalObjects + 1} /Root 1 0 R>>\n");
            buf.Append("startxref\n");
            buf.Append($"{xrefOffset}\n");
            buf.Append("%%EOF\n");

            return buf.ToString();
        }

        private static string BuildPageStream(List<string> lines, double fontSize, double lineHeight)
        {
            var s = new StringBuilder();
            s.Append("BT\n");
            s.Append($"/F1 {(int)fontSize} Tf\n");

            double y = PageHeight - MarginTop - fontSize;
            s.Append($"{(int)MarginLeft} {(int)y} Td\n");
            s.Append($"{lineHeight:F2} TL\n");

            bool first = true;
            foreach (var line in lines)
            {
                if (first) { first = false; }
                else { s.Append("T*\n"); }
                s.Append($"({EscapePdfString(line)}) Tj\n");
            }

            s.Append("ET");
            return s.ToString();
        }

        private static string EscapePdfString(string s)
        {
            var result = new StringBuilder(s.Length + 4);
            foreach (char c in s)
            {
                if (c == '(') result.Append("\\(");
                else if (c == ')') result.Append("\\)");
                else if (c == '\\') result.Append("\\\\");
                else if (c < 32 || c > 126) result.Append('?');
                else result.Append(c);
            }
            return result.ToString();
        }

        /// <summary>
        /// Splits text (with \r, \n, or \r\n line endings) into display lines,
        /// applying simple word-wrap at <paramref name="maxChars"/> per line.
        /// </summary>
        public static List<string> BuildDisplayLines(string text, int maxChars)
        {
            var result = new List<string>();
            if (maxChars < 1) maxChars = 1;

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] paragraphs = normalized.Split('\n');

            foreach (var para in paragraphs)
            {
                if (para.Length <= maxChars)
                {
                    result.Add(para);
                    continue;
                }
                // Word-wrap
                string remaining = para;
                while (remaining.Length > maxChars)
                {
                    int breakAt = maxChars;
                    int lastSpace = remaining.LastIndexOf(' ', maxChars);
                    if (lastSpace > 0) breakAt = lastSpace;
                    result.Add(remaining[..breakAt]);
                    remaining = remaining[breakAt..].TrimStart(' ');
                }
                if (remaining.Length > 0) result.Add(remaining);
            }

            return result;
        }
    }
}
