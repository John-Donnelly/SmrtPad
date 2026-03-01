using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SmrtPad.Helpers
{
    /// <summary>
    /// Extracts text content from DOCX and ODT document archives.
    /// </summary>
    public static class DocumentImportHelper
    {
        private static readonly XNamespace OdfTextNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        private static readonly XNamespace OdfStyleNs = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
        private static readonly XNamespace OdfFoNs = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";

        private sealed record OdtTextStyle(
            bool Bold,
            bool Italic,
            bool Underline,
            string? FontName,
            int? FontSizeHalfPoints,
            string? ColorHex);

        /// <summary>
        /// Extracts plain text from a DOCX or ODT archive stream.
        /// </summary>
        /// <param name="archiveStream">A readable stream containing the zip archive.</param>
        /// <param name="extension">The file extension (e.g. ".docx" or ".odt").</param>
        /// <returns>Extracted text content, or <see cref="string.Empty"/> if the entry is missing.</returns>
        public static string ExtractText(Stream archiveStream, string extension)
        {
            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);

            string entryPath = extension == ".docx" ? "word/document.xml" : "content.xml";
            var entry = archive.GetEntry(entryPath);
            if (entry == null)
                return string.Empty;

            using var entryStream = entry.Open();
            var doc = XDocument.Load(entryStream);

            var texts = doc.Descendants()
                .Where(el => el.Name.LocalName == (extension == ".docx" ? "t" : "p"))
                .Select(el => el.Value);

            return extension == ".docx"
                ? string.Join("", texts).Replace("\n", Environment.NewLine)
                : string.Join(Environment.NewLine, texts);
        }

        /// <summary>
        /// Converts ODT content into RTF while preserving paragraph boundaries and
        /// basic character formatting from ODF text styles.
        /// </summary>
        public static string ConvertOdtToRtf(Stream archiveStream)
        {
            ArgumentNullException.ThrowIfNull(archiveStream);

            using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
            var contentEntry = archive.GetEntry("content.xml");
            if (contentEntry is null)
            {
                return @"{\rtf1\ansi\pard\par}";
            }

            var stylesByName = LoadStyles(archive);
            using var contentStream = contentEntry.Open();
            var contentDoc = XDocument.Load(contentStream);

            var paragraphs = contentDoc.Descendants(OdfTextNs + "p").ToList();
            var fontSet = new List<string> { "Segoe UI" };
            var colorSet = new List<string> { "000000" };

            foreach (var style in stylesByName.Values)
            {
                if (!string.IsNullOrWhiteSpace(style.FontName) && !fontSet.Contains(style.FontName))
                {
                    fontSet.Add(style.FontName);
                }

                if (!string.IsNullOrWhiteSpace(style.ColorHex) && !colorSet.Contains(style.ColorHex))
                {
                    colorSet.Add(style.ColorHex);
                }
            }

            var rtf = new StringBuilder();
            rtf.Append(@"{\rtf1\ansi\deff0");

            rtf.Append(@"{\fonttbl");
            for (int i = 0; i < fontSet.Count; i++)
            {
                rtf.Append($@"{{\f{i}\fswiss {fontSet[i]};}}");
            }

            rtf.Append('}');

            rtf.Append(@"{\colortbl;");
            foreach (var hex in colorSet)
            {
                var (r, g, b) = ParseHexColor(hex);
                rtf.Append($@"\red{r}\green{g}\blue{b};");
            }

            rtf.Append('}');

            for (int i = 0; i < paragraphs.Count; i++)
            {
                rtf.Append(@"\pard\cf1 ");
                AppendParagraphRuns(rtf, paragraphs[i], stylesByName, fontSet, colorSet);
                if (i < paragraphs.Count - 1)
                {
                    rtf.Append(@"\par ");
                }
            }

            rtf.Append('}');
            return rtf.ToString();
        }

        private static Dictionary<string, OdtTextStyle> LoadStyles(ZipArchive archive)
        {
            var stylesByName = new Dictionary<string, OdtTextStyle>(StringComparer.Ordinal);

            AddStylesFromEntry(archive.GetEntry("styles.xml"), stylesByName);
            AddStylesFromEntry(archive.GetEntry("content.xml"), stylesByName);

            return stylesByName;
        }

        private static void AddStylesFromEntry(ZipArchiveEntry? entry, Dictionary<string, OdtTextStyle> stylesByName)
        {
            if (entry is null)
            {
                return;
            }

            using var stream = entry.Open();
            var doc = XDocument.Load(stream);

            foreach (var style in doc.Descendants(OdfStyleNs + "style"))
            {
                string? family = style.Attribute(OdfStyleNs + "family")?.Value;
                if (!string.Equals(family, "text", StringComparison.Ordinal))
                {
                    continue;
                }

                string? name = style.Attribute(OdfStyleNs + "name")?.Value;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var textProps = style.Element(OdfStyleNs + "text-properties");
                if (textProps is null)
                {
                    continue;
                }

                bool bold = string.Equals(textProps.Attribute(OdfFoNs + "font-weight")?.Value, "bold", StringComparison.OrdinalIgnoreCase);
                bool italic = string.Equals(textProps.Attribute(OdfFoNs + "font-style")?.Value, "italic", StringComparison.OrdinalIgnoreCase);
                bool underline = !string.IsNullOrWhiteSpace(textProps.Attribute(OdfStyleNs + "text-underline-style")?.Value)
                    && !string.Equals(textProps.Attribute(OdfStyleNs + "text-underline-style")?.Value, "none", StringComparison.OrdinalIgnoreCase);

                string? fontName = textProps.Attribute(OdfStyleNs + "font-name")?.Value;
                int? fontSizeHp = ParseFontSizeHalfPoints(textProps.Attribute(OdfFoNs + "font-size")?.Value);
                string? color = NormalizeHexColor(textProps.Attribute(OdfFoNs + "color")?.Value);

                stylesByName[name] = new OdtTextStyle(bold, italic, underline, fontName, fontSizeHp, color);
            }
        }

        private static void AppendParagraphRuns(
            StringBuilder rtf,
            XElement paragraph,
            IReadOnlyDictionary<string, OdtTextStyle> stylesByName,
            List<string> fontSet,
            List<string> colorSet)
        {
            bool wroteContent = false;
            foreach (var node in paragraph.Nodes())
            {
                if (node is XText textNode)
                {
                    string text = textNode.Value;
                    if (!string.IsNullOrEmpty(text))
                    {
                        rtf.Append(EscapeRtf(text));
                        wroteContent = true;
                    }

                    continue;
                }

                if (node is not XElement element)
                {
                    continue;
                }

                if (element.Name == OdfTextNs + "span")
                {
                    string styleName = element.Attribute(OdfTextNs + "style-name")?.Value ?? string.Empty;
                    string spanText = element.Value;
                    if (string.IsNullOrEmpty(spanText))
                    {
                        continue;
                    }

                    if (!stylesByName.TryGetValue(styleName, out var style))
                    {
                        rtf.Append(EscapeRtf(spanText));
                        wroteContent = true;
                        continue;
                    }

                    int fontIdx = !string.IsNullOrWhiteSpace(style.FontName)
                        ? Math.Max(0, fontSet.IndexOf(style.FontName))
                        : 0;
                    int colorIdx = !string.IsNullOrWhiteSpace(style.ColorHex)
                        ? Math.Max(0, colorSet.IndexOf(style.ColorHex))
                        : 0;

                    rtf.Append('{');
                    if (style.Bold) rtf.Append(@"\b");
                    if (style.Italic) rtf.Append(@"\i");
                    if (style.Underline) rtf.Append(@"\ul");
                    rtf.Append($@"\f{fontIdx}");
                    if (style.FontSizeHalfPoints.HasValue)
                    {
                        rtf.Append($@"\fs{style.FontSizeHalfPoints.Value}");
                    }

                    rtf.Append($@"\cf{colorIdx + 1} ");
                    rtf.Append(EscapeRtf(spanText));
                    rtf.Append('}');
                    wroteContent = true;
                }
                else if (element.Name == OdfTextNs + "line-break")
                {
                    rtf.Append(@"\line ");
                    wroteContent = true;
                }
                else if (element.Name == OdfTextNs + "tab")
                {
                    rtf.Append(@"\tab ");
                    wroteContent = true;
                }
            }

            if (!wroteContent)
            {
                rtf.Append(' ');
            }
        }

        private static int? ParseFontSizeHalfPoints(string? sizeValue)
        {
            if (string.IsNullOrWhiteSpace(sizeValue))
            {
                return null;
            }

            if (sizeValue.EndsWith("pt", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(sizeValue[..^2], out double points)
                && points > 0)
            {
                return (int)Math.Round(points * 2, MidpointRounding.AwayFromZero);
            }

            return null;
        }

        private static string? NormalizeHexColor(string? colorValue)
        {
            if (string.IsNullOrWhiteSpace(colorValue))
            {
                return null;
            }

            string normalized = colorValue.Trim();
            if (normalized.StartsWith('#'))
            {
                normalized = normalized[1..];
            }

            if (normalized.Length == 6 && normalized.All(static c => Uri.IsHexDigit(c)))
            {
                return normalized.ToUpperInvariant();
            }

            return null;
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
                    case '\\': sb.Append(@"\\\\"); break;
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
    }
}
