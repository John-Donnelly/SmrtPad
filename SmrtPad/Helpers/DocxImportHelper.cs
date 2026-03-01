using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SmrtPad.Helpers
{
    /// <summary>
    /// Converts a DOCX file to an RTF string that <c>RichEditBox</c> can render with
    /// formatting intact. Uses <c>DocumentFormat.OpenXml</c> to read paragraphs, runs,
    /// and character/paragraph properties, then emits well-formed RTF 1.9.
    /// </summary>
    public static class DocxImportHelper
    {
        /// <summary>
        /// Reads a DOCX stream and returns an RTF string preserving bold, italic,
        /// underline, strikethrough, font name, font size, foreground color, and
        /// paragraph alignment.
        /// </summary>
        /// <param name="docxStream">A readable stream containing a valid .docx file.</param>
        /// <returns>A well-formed RTF string ready for <c>RichEditBox.LoadFromStream</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="docxStream"/> is null.</exception>
        public static string ConvertToRtf(Stream docxStream)
        {
            ArgumentNullException.ThrowIfNull(docxStream);

            using var doc = WordprocessingDocument.Open(docxStream, false);
            var mainPart = doc.MainDocumentPart;
            if (mainPart?.Document?.Body is null)
                return @"{\rtf1\ansi\pard\par}";

            // Collect unique fonts from all runs
            var fontSet = new List<string> { "Segoe UI" }; // f0 = default
            CollectFonts(mainPart.Document.Body, fontSet);

            var rtf = new StringBuilder();
            rtf.Append(@"{\rtf1\ansi\deff0");

            // Font table
            rtf.Append(@"{\fonttbl");
            for (int i = 0; i < fontSet.Count; i++)
            {
                rtf.Append($@"{{\f{i}\fswiss {fontSet[i]};}}");
            }
            rtf.Append('}');

            // Color table
            var colorList = new List<string> { "000000" }; // cf0 is auto; cf1 = black
            CollectColors(mainPart.Document.Body, colorList);
            rtf.Append(@"{\colortbl;");
            foreach (var hex in colorList)
            {
                var (r, g, b) = ParseHexColor(hex);
                rtf.Append($@"\red{r}\green{g}\blue{b};");
            }
            rtf.Append('}');

            // Body paragraphs
            var paragraphs = mainPart.Document.Body.Elements<Paragraph>().ToList();
            for (int pi = 0; pi < paragraphs.Count; pi++)
            {
                var para = paragraphs[pi];
                rtf.Append(@"\pard");

                // Paragraph alignment
                var pPr = para.ParagraphProperties;
                var jc = pPr?.Justification?.Val;
                if (jc is not null)
                {
                    if (jc.Value == JustificationValues.Center)
                        rtf.Append(@"\qc");
                    else if (jc.Value == JustificationValues.Right)
                        rtf.Append(@"\qr");
                    else if (jc.Value == JustificationValues.Both || jc.Value == JustificationValues.Distribute)
                        rtf.Append(@"\qj");
                    else
                        rtf.Append(@"\ql");
                }

                // Runs
                // Set explicit colour at paragraph level so the \par mark and any
                // inter-run whitespace reference cf1 (the default colour entry in the
                // colour table).  In dark mode, OpenStorageFileAsync swaps cf1 from
                // black to white before loading the RTF.
                rtf.Append(@"\cf1");
                foreach (var run in para.Elements<Run>())
                {
                    var rPr = run.RunProperties;
                    bool bold = IsOn(rPr?.Bold);
                    bool italic = IsOn(rPr?.Italic);
                    bool underline = rPr?.Underline?.Val is not null
                        && rPr.Underline.Val != UnderlineValues.None;
                    bool strike = IsOn(rPr?.Strike);

                    // Font
                    string? fontName = rPr?.RunFonts?.Ascii?.Value
                        ?? rPr?.RunFonts?.HighAnsi?.Value;
                    int fontIdx = 0;
                    if (!string.IsNullOrEmpty(fontName))
                    {
                        fontIdx = fontSet.IndexOf(fontName);
                        if (fontIdx < 0) fontIdx = 0;
                    }

                    // Font size (OpenXml stores in half-points)
                    int fontSizeHp = 0;
                    if (rPr?.FontSize?.Val?.Value is string fsVal &&
                        int.TryParse(fsVal, out int parsed))
                    {
                        fontSizeHp = parsed;
                    }

                    // Color
                    int colorIdx = 0;
                    string? colorVal = rPr?.Color?.Val?.Value;
                    if (!string.IsNullOrEmpty(colorVal) && colorVal != "auto")
                    {
                        int ci = colorList.IndexOf(colorVal);
                        if (ci >= 0) colorIdx = ci;
                    }

                    // Emit run formatting.
                    // RTF color table layout: cf0 = auto (empty entry), cf1 = colorList[0] (black),
                    // cf2 = colorList[1], etc.  All default-coloured text emits \cf1 so it
                    // references the first colour table entry.  In dark mode,
                    // OpenStorageFileAsync swaps that entry from black to white before
                    // loading the RTF, giving us theme-aware text without post-load fixup.
                    rtf.Append('{');
                    if (bold) rtf.Append(@"\b");
                    if (italic) rtf.Append(@"\i");
                    if (underline) rtf.Append(@"\ul");
                    if (strike) rtf.Append(@"\strike");
                    rtf.Append($@"\f{fontIdx}");
                    if (fontSizeHp > 0) rtf.Append($@"\fs{fontSizeHp}");
                    rtf.Append($@"\cf{colorIdx + 1}");
                    rtf.Append(' ');

                    // Text content — handle Text and Break elements
                    foreach (var child in run.ChildElements)
                    {
                        if (child is Text t)
                        {
                            rtf.Append(EscapeRtf(t.Text ?? string.Empty));
                        }
                        else if (child is Break br)
                        {
                            if (br.Type?.Value == BreakValues.Page)
                                rtf.Append(@"\page ");
                            else
                                rtf.Append(@"\line ");
                        }
                        else if (child is TabChar)
                        {
                            rtf.Append(@"\tab ");
                        }
                        else if (child is Drawing drawing)
                        {
                            string? pict = ConvertDrawingToRtf(mainPart, drawing);
                            if (!string.IsNullOrEmpty(pict))
                            {
                                rtf.Append(pict);
                            }
                        }
                    }

                    rtf.Append('}');
                }

                // Paragraph break (don't append \par after the very last paragraph)
                if (pi < paragraphs.Count - 1)
                    rtf.Append(@"\par ");
            }

            rtf.Append('}');
            return rtf.ToString();
        }

        /// <summary>
        /// Checks whether an <see cref="OnOffType"/> element is logically "on".
        /// In OOXML, &lt;w:b/&gt; (present with no val) means on; &lt;w:b w:val="false"/&gt; means off.
        /// </summary>
        private static bool IsOn(OnOffType? element)
        {
            if (element is null) return false;
            // If Val is null, the element is present without a value → "on"
            if (element.Val is null) return true;
            return element.Val.Value;
        }

        private static void CollectFonts(Body body, List<string> fontSet)
        {
            foreach (var rFonts in body.Descendants<RunFonts>())
            {
                string? name = rFonts.Ascii?.Value ?? rFonts.HighAnsi?.Value;
                if (!string.IsNullOrEmpty(name) && !fontSet.Contains(name))
                    fontSet.Add(name);
            }
        }

        private static void CollectColors(Body body, List<string> colorList)
        {
            foreach (var color in body.Descendants<Color>())
            {
                string? val = color.Val?.Value;
                if (!string.IsNullOrEmpty(val) && val != "auto" && !colorList.Contains(val))
                    colorList.Add(val);
            }
        }

        private static (int r, int g, int b) ParseHexColor(string hex)
        {
            if (hex.Length >= 6 &&
                int.TryParse(hex.AsSpan(0, 2), System.Globalization.NumberStyles.HexNumber, null, out int r) &&
                int.TryParse(hex.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out int g) &&
                int.TryParse(hex.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out int b))
            {
                return (r, g, b);
            }
            return (0, 0, 0);
        }

        private static string EscapeRtf(string text)
        {
            var sb = new StringBuilder(text.Length);
            foreach (char ch in text)
            {
                switch (ch)
                {
                    case '\\': sb.Append(@"\\"); break;
                    case '{': sb.Append(@"\{"); break;
                    case '}': sb.Append(@"\}"); break;
                    default:
                        if (ch > 127)
                            sb.Append($@"\u{(int)ch}?");
                        else
                            sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string? ConvertDrawingToRtf(MainDocumentPart mainPart, DocumentFormat.OpenXml.Wordprocessing.Drawing drawing)
        {
            string? relationshipId = drawing
                .Descendants<DocumentFormat.OpenXml.Drawing.Blip>()
                .Select(static blip => blip.Embed?.Value)
                .FirstOrDefault(static id => !string.IsNullOrWhiteSpace(id));

            if (string.IsNullOrWhiteSpace(relationshipId))
            {
                return null;
            }

            ImagePart? imagePart = mainPart.GetPartById(relationshipId) as ImagePart;
            if (imagePart is null)
            {
                return null;
            }

            string blipControl = imagePart.ContentType switch
            {
                "image/png" => @"\pngblip",
                "image/jpeg" or "image/jpg" => @"\jpegblip",
                "image/gif" => @"\pngblip",
                "image/bmp" => @"\dibitmap0",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(blipControl))
            {
                return null;
            }

            using var imageStream = imagePart.GetStream();
            using var imageBytesStream = new MemoryStream();
            imageStream.CopyTo(imageBytesStream);
            byte[] imageBytes = imageBytesStream.ToArray();
            if (imageBytes.Length == 0)
            {
                return null;
            }

            long cx = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>()
                .Select(static extent => extent.Cx?.Value ?? 0L)
                .FirstOrDefault();
            long cy = drawing.Descendants<DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent>()
                .Select(static extent => extent.Cy?.Value ?? 0L)
                .FirstOrDefault();

            int picwgoal = cx > 0 ? (int)Math.Max(1, cx * 1440 / 914400) : 0;
            int pichgoal = cy > 0 ? (int)Math.Max(1, cy * 1440 / 914400) : 0;

            var pict = new StringBuilder();
            pict.Append(@"{\pict");
            pict.Append(blipControl);
            if (picwgoal > 0) pict.Append($@"\picwgoal{picwgoal}");
            if (pichgoal > 0) pict.Append($@"\pichgoal{pichgoal}");
            pict.Append(' ');
            foreach (byte b in imageBytes)
            {
                pict.Append(b.ToString("x2"));
            }

            pict.Append('}');
            return pict.ToString();
        }
    }
}
