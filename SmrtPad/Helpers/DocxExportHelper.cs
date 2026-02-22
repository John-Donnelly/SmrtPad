using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SmrtPad.Helpers
{
    /// <summary>
    /// Generates valid OOXML .docx files from plain text or RTF.
    /// <see cref="GenerateDocx"/> accepts plain text (backwards-compatible).
    /// <see cref="GenerateRichDocx"/> accepts RTF and preserves bold, italic,
    /// underline, strikethrough, font name, font size, and paragraph alignment.
    /// </summary>
    public static class DocxExportHelper
    {
        private static readonly XNamespace W =
            "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        private static readonly XNamespace RelNs =
            "http://schemas.openxmlformats.org/package/2006/relationships";

        private static readonly XNamespace CtNs =
            "http://schemas.openxmlformats.org/package/2006/content-types";

        private static readonly XNamespace OfficeRel =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";

        /// <summary>Plain text → .docx (backwards-compatible). Each line break becomes a paragraph.</summary>
        public static byte[] GenerateDocx(string plainText)
        {
            ArgumentNullException.ThrowIfNull(plainText);

            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteEntry(zip, "[Content_Types].xml", BuildContentTypes());
                WriteEntry(zip, "_rels/.rels", BuildRootRels());
                WriteEntry(zip, "word/document.xml", BuildDocument(plainText));
                WriteEntry(zip, "word/_rels/document.xml.rels", BuildDocumentRels());
            }
            return ms.ToArray();
        }

        /// <summary>
        /// RTF string → .docx preserving bold, italic, underline, strikethrough,
        /// font name, font size (half-points), and paragraph alignment.
        /// </summary>
        public static byte[] GenerateRichDocx(string rtfContent)
        {
            ArgumentNullException.ThrowIfNull(rtfContent);
            var paragraphs = RtfParser.Parse(rtfContent);

            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteEntry(zip, "[Content_Types].xml", BuildContentTypes());
                WriteEntry(zip, "_rels/.rels", BuildRootRels());
                WriteEntry(zip, "word/document.xml", BuildRichDocument(paragraphs));
                WriteEntry(zip, "word/_rels/document.xml.rels", BuildDocumentRels());
            }
            return ms.ToArray();
        }

        // ── XML builders ─────────────────────────────────────────────────────────

        private static string BuildRichDocument(List<RtfParagraph> paragraphs)
        {
            var body = new XElement(W + "body");
            foreach (var para in paragraphs)
            {
                var p = new XElement(W + "p");
                if (para.Alignment != "left")
                {
                    string jc = para.Alignment switch
                    {
                        "center"  => "center",
                        "right"   => "right",
                        "justify" => "both",
                        _         => "left",
                    };
                    p.Add(new XElement(W + "pPr",
                        new XElement(W + "jc", new XAttribute(W + "val", jc))));
                }
                foreach (var run in para.Runs)
                {
                    if (run.Text.Length == 0) continue;
                    var r   = new XElement(W + "r");
                    var rPr = new XElement(W + "rPr");
                    if (run.Bold)          rPr.Add(new XElement(W + "b"));
                    if (run.Italic)        rPr.Add(new XElement(W + "i"));
                    if (run.Underline)     rPr.Add(new XElement(W + "u",  new XAttribute(W + "val", "single")));
                    if (run.Strikethrough) rPr.Add(new XElement(W + "strike"));
                    if (!string.IsNullOrEmpty(run.FontName))
                        rPr.Add(new XElement(W + "rFonts",
                            new XAttribute(W + "ascii", run.FontName),
                            new XAttribute(W + "hAnsi", run.FontName)));
                    if (run.FontSizeHalfPts > 0)
                        rPr.Add(new XElement(W + "sz", new XAttribute(W + "val", run.FontSizeHalfPts)));
                    if (rPr.HasElements) r.Add(rPr);
                    r.Add(new XElement(W + "t",
                        new XAttribute(XNamespace.Xml + "space", "preserve"), run.Text));
                    p.Add(r);
                }
                if (!p.HasElements)
                    p.Add(new XElement(W + "r", new XElement(W + "t", "")));
                body.Add(p);
            }
            body.Add(new XElement(W + "sectPr"));
            var doc = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(W + "document", body));
            return doc.ToString();
        }

        private static string BuildContentTypes()
        {
            var xml = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(CtNs + "Types",
                    new XElement(CtNs + "Default",
                        new XAttribute("Extension", "rels"),
                        new XAttribute("ContentType",
                            "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(CtNs + "Default",
                        new XAttribute("Extension", "xml"),
                        new XAttribute("ContentType", "application/xml")),
                    new XElement(CtNs + "Override",
                        new XAttribute("PartName", "/word/document.xml"),
                        new XAttribute("ContentType",
                            "application/vnd.openxmlformats-officedocument" +
                            ".wordprocessingml.document.main+xml"))));
            return xml.ToString();
        }

        private static string BuildRootRels()
        {
            var xml = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(RelNs + "Relationships",
                    new XElement(RelNs + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute("Type",
                            "http://schemas.openxmlformats.org/officeDocument/" +
                            "2006/relationships/officeDocument"),
                        new XAttribute("Target", "word/document.xml"))));
            return xml.ToString();
        }

        private static string BuildDocumentRels()
        {
            var xml = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(RelNs + "Relationships"));
            return xml.ToString();
        }

        private static string BuildDocument(string plainText)
        {
            string normalized = plainText
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .TrimEnd('\n');

            string[] paragraphs = normalized.Length == 0
                ? [string.Empty]
                : normalized.Split('\n');

            var body = new XElement(W + "body");
            foreach (var para in paragraphs)
            {
                body.Add(new XElement(W + "p",
                    new XElement(W + "r",
                        new XElement(W + "t",
                            new XAttribute(XNamespace.Xml + "space", "preserve"),
                            para))));
            }
            // Required section properties sentinel
            body.Add(new XElement(W + "sectPr"));

            var doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(W + "document", body));

            return doc.ToString();
        }

        // ── ZIP helpers ──────────────────────────────────────────────────────────

        private static void WriteEntry(ZipArchive zip, string entryName, string content)
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
    }

    // ── RTF data model ───────────────────────────────────────────────────────────

    internal sealed record RtfRun(
        string Text, bool Bold, bool Italic, bool Underline,
        bool Strikethrough, string FontName, int FontSizeHalfPts);

    internal sealed class RtfParagraph
    {
        public List<RtfRun> Runs  { get; } = [];
        public string Alignment   { get; set; } = "left";
    }

    /// <summary>
    /// Minimal RTF 1.9 parser: extracts bold/italic/underline/strikethrough/font/
    /// font-size/alignment and paragraph breaks from a well-formed RTF string.
    /// </summary>
    internal static partial class RtfParser
    {
        [GeneratedRegex(@"\{\\fonttbl[^}]*(\{[^}]*\})*[^}]*\}")]
        private static partial Regex FontTblRemoveRegex();
        [GeneratedRegex(@"\{\\colortbl[^}]*\}")]
        private static partial Regex ColorTblRemoveRegex();
        [GeneratedRegex(@"\{\\fonttbl(.+?)\}", RegexOptions.Singleline)]
        private static partial Regex FontTblMatchRegex();
        [GeneratedRegex(@"\{\\f(\d+)[^;]*;\s*([^}]+)\}")]
        private static partial Regex FontEntryRegex();
        [GeneratedRegex(@"\\[a-z]+\d*\s?")]
        private static partial Regex FontNameCleanRegex();

        public static List<RtfParagraph> Parse(string rtf)
        {
            List<RtfParagraph> result = [];
            if (string.IsNullOrEmpty(rtf)) return result;

            var fonts = ParseFontTable(rtf);

            // Remove the font/colour tables so their text doesn't become runs
            string body = FontTblRemoveRegex().Replace(rtf, "");
            body = ColorTblRemoveRegex().Replace(body, "");

            var cur   = new RtfParagraph(); result.Add(cur);
            bool bold = false, italic = false, ul = false, strike = false;
            int  fi = 0, fshp = 24;        // font index, font size in half-points
            string align = "left";

            int i = 0;
            while (i < body.Length)
            {
                char ch = body[i];

                if (ch == '{')
                {
                    // Skip destination groups {\* ...} and content groups we can't use
                    // (pictures, objects, headers, footers).
                    // All other groups — including the root {\rtf1...} wrapper and
                    // inline formatting groups like {\b text} — are entered by simply
                    // advancing past the opening brace.
                    if (i + 2 < body.Length && body[i + 1] == '\\')
                    {
                        if (body[i + 2] == '*') { i = SkipGroup(body, i); continue; }
                        int look = i + 2;
                        while (look < body.Length && char.IsLetter(body[look])) look++;
                        var gw = body.AsSpan(i + 2, look - i - 2);
                        if (gw is "pict" or "object" or "header" or "footer"
                                 or "info" or "stylesheet" or "listtext"
                                 or "listtable" or "listoverridetable")
                        { i = SkipGroup(body, i); continue; }
                    }
                    i++; // enter all other groups
                    continue;
                }
                if (ch == '}') { i++; continue; }

                if (ch != '\\')
                {
                    AddChar(cur, bold, italic, ul, strike, fonts, fi, fshp, ch);
                    i++;
                    continue;
                }

                // Control sequence
                i++;
                if (i >= body.Length) break;
                char nx = body[i];

                if (nx == '\\') { AddChar(cur,bold,italic,ul,strike,fonts,fi,fshp,'\\'); i++; continue; }
                if (nx == '{')  { AddChar(cur,bold,italic,ul,strike,fonts,fi,fshp,'{');  i++; continue; }
                if (nx == '}')  { AddChar(cur,bold,italic,ul,strike,fonts,fi,fshp,'}');  i++; continue; }
                if (nx == '\'')
                {
                    if (i + 2 < body.Length &&
                        int.TryParse(body.Substring(i+1, 2),
                            System.Globalization.NumberStyles.HexNumber, null, out int code))
                    { AddChar(cur,bold,italic,ul,strike,fonts,fi,fshp,(char)code); i += 3; }
                    else { i++; }
                    continue;
                }
                if ("*~-_|:!;".Contains(nx)) { i++; continue; }

                int ws = i;
                while (i < body.Length && char.IsLetter(body[i])) i++;
                string word = body[ws..i];

                int param = int.MinValue;
                if (i < body.Length && (body[i] == '-' || char.IsDigit(body[i])))
                {
                    int ns = i; if (body[i] == '-') i++;
                    while (i < body.Length && char.IsDigit(body[i])) i++;
                    if (!int.TryParse(body.AsSpan(ns, i - ns), out param)) param = int.MinValue;
                }
                if (i < body.Length && body[i] == ' ') i++;

                switch (word)
                {
                    case "pard":    bold=false; italic=false; ul=false; strike=false; align="left"; cur.Alignment = align; break;
                    case "par":
                    case "line":    cur = new RtfParagraph { Alignment = align }; result.Add(cur); break;
                    case "b":       bold   = param != 0; break;
                    case "i":       italic = param != 0; break;
                    case "ul":      ul     = param != 0; break;
                    case "ulnone":  ul     = false;      break;
                    case "strike":
                    case "striked": strike = param != 0; break;
                    case "f":       fi    = param != int.MinValue ? param : 0;  break;
                    case "fs":      fshp  = param != int.MinValue ? param : 24; break;
                    case "ql":      align = "left";    cur.Alignment = align; break;
                    case "qc":      align = "center";  cur.Alignment = align; break;
                    case "qr":      align = "right";   cur.Alignment = align; break;
                    case "qj":      align = "justify"; cur.Alignment = align; break;
                }
            }

            // Trim empty paragraphs introduced by RTF header/footer noise
            while (result.Count > 1 && result[0].Runs.Count == 0)
                result.RemoveAt(0);
            while (result.Count > 1 && result[^1].Runs.Count == 0)
                result.RemoveAt(result.Count - 1);
            if (result.Count == 0) result.Add(new RtfParagraph());

            return result;
        }

        private static void AddChar(RtfParagraph para, bool bold, bool italic, bool ul, bool strike,
            Dictionary<int, string> fonts, int fi, int fshp, char ch)
        {
            string fn   = fonts.TryGetValue(fi, out var f) ? f : string.Empty;
            var    runs = para.Runs;
            if (runs.Count > 0)
            {
                var last = runs[^1];
                if (last.Bold == bold && last.Italic == italic && last.Underline == ul &&
                    last.Strikethrough == strike && last.FontName == fn &&
                    last.FontSizeHalfPts == fshp)
                { runs[^1] = last with { Text = last.Text + ch }; return; }
            }
            runs.Add(new RtfRun(ch.ToString(), bold, italic, ul, strike, fn, fshp));
        }

        private static Dictionary<int, string> ParseFontTable(string rtf)
        {
            var d = new Dictionary<int, string>();
            var m = FontTblMatchRegex().Match(rtf);
            if (!m.Success) return d;
            foreach (Match e in FontEntryRegex().Matches(m.Value))
                if (int.TryParse(e.Groups[1].Value, out int idx))
                {
                    string name = FontNameCleanRegex().Replace(e.Groups[2].Value, "").Trim();
                    if (name.Length > 0) d[idx] = name;
                }
            return d;
        }

        private static int SkipGroup(string s, int start)
        {
            int depth = 0, i = start;
            while (i < s.Length)
            {
                if      (s[i] == '{') { depth++; i++; }
                else if (s[i] == '}') { depth--; i++; if (depth == 0) break; }
                else if (s[i] == '\\') { i++; if (i < s.Length) i++; }
                else i++;
            }
            return i;
        }
    }
}
