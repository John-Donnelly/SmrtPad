using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace SmrtPad.Helpers
{
    /// <summary>
    /// Extracts text content from DOCX and ODT document archives.
    /// </summary>
    public static class DocumentImportHelper
    {
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
    }
}
