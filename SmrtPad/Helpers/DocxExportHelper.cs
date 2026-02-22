using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace SmrtPad.Helpers
{
    /// <summary>
    /// Generates minimal but valid OOXML .docx files from plain-text content.
    /// The output is a ZIP archive with [Content_Types].xml, _rels/.rels and
    /// word/document.xml — sufficient for all modern Word / LibreOffice versions.
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

        /// <summary>
        /// Creates a .docx byte array from plain text.
        /// Each \r, \n, or \r\n in <paramref name="plainText"/> becomes a paragraph.
        /// </summary>
        public static byte[] GenerateDocx(string plainText)
        {
            if (plainText == null) throw new ArgumentNullException(nameof(plainText));

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

        // ── XML builders ─────────────────────────────────────────────────────────

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
                ? new[] { string.Empty }
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
}
