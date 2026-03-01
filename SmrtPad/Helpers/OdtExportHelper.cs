using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SmrtPad.Helpers
{
    /// <summary>
    /// Exports plain-text document content into a valid ODT package.
    /// </summary>
    public static class OdtExportHelper
    {
        private static readonly XNamespace OfficeNs = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        private static readonly XNamespace TextNs = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        private static readonly XNamespace ManifestNs = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0";

        /// <summary>
        /// Writes an ODT package with the supplied plain text.
        /// </summary>
        public static void Export(string text, Stream outputStream)
        {
            ArgumentNullException.ThrowIfNull(outputStream);
            if (!outputStream.CanWrite)
            {
                throw new ArgumentException("Output stream must be writable.", nameof(outputStream));
            }

            using var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true);

            var mimeTypeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
            using (var mimeWriter = new StreamWriter(mimeTypeEntry.Open(), new UTF8Encoding(false)))
            {
                mimeWriter.Write("application/vnd.oasis.opendocument.text");
            }

            var contentEntry = archive.CreateEntry("content.xml", CompressionLevel.Optimal);
            using (var contentWriter = new StreamWriter(contentEntry.Open(), new UTF8Encoding(false)))
            {
                BuildContentXml(text).Save(contentWriter);
            }

            var manifestEntry = archive.CreateEntry("META-INF/manifest.xml", CompressionLevel.Optimal);
            using (var manifestWriter = new StreamWriter(manifestEntry.Open(), new UTF8Encoding(false)))
            {
                BuildManifestXml().Save(manifestWriter);
            }
        }

        private static XDocument BuildContentXml(string text)
        {
            var paragraphs = (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n', StringSplitOptions.None)
                .Select(static line => new XElement(TextNs + "p", line));

            return new XDocument(
                new XElement(OfficeNs + "document-content",
                    new XAttribute(XNamespace.Xmlns + "office", OfficeNs),
                    new XAttribute(XNamespace.Xmlns + "text", TextNs),
                    new XAttribute(OfficeNs + "version", "1.2"),
                    new XElement(OfficeNs + "body",
                        new XElement(OfficeNs + "text", paragraphs))));
        }

        private static XDocument BuildManifestXml()
        {
            return new XDocument(
                new XElement(ManifestNs + "manifest",
                    new XAttribute(XNamespace.Xmlns + "manifest", ManifestNs),
                    new XAttribute(ManifestNs + "version", "1.2"),
                    new XElement(ManifestNs + "file-entry",
                        new XAttribute(ManifestNs + "full-path", "/"),
                        new XAttribute(ManifestNs + "media-type", "application/vnd.oasis.opendocument.text")),
                    new XElement(ManifestNs + "file-entry",
                        new XAttribute(ManifestNs + "full-path", "content.xml"),
                        new XAttribute(ManifestNs + "media-type", "text/xml"))));
        }
    }
}
