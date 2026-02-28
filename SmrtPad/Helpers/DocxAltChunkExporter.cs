using System;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace SmrtPad.Helpers
{
    /// <summary>
    /// Exports RTF content to a .docx file using the OpenXml AltChunk mechanism.
    /// AltChunk embeds the original RTF stream inside the OOXML package, preserving
    /// 100% of RTF formatting (tables, images, fonts, colors, footnotes, etc.)
    /// without lossy manual parsing.
    /// </summary>
    public static class DocxAltChunkExporter
    {
        /// <summary>
        /// Writes a valid .docx file to <paramref name="outputStream"/> that embeds the
        /// supplied <paramref name="rtfContent"/> via an AltChunk part.
        /// </summary>
        /// <param name="rtfContent">A non-empty RTF string obtained from <c>RichEditBox</c>.</param>
        /// <param name="outputStream">A writable stream that will receive the .docx bytes.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="rtfContent"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="outputStream"/> is null.</exception>
        public static void ExportToDocx(string rtfContent, Stream outputStream)
        {
            if (string.IsNullOrWhiteSpace(rtfContent))
                throw new ArgumentException("RTF content cannot be empty.", nameof(rtfContent));
            ArgumentNullException.ThrowIfNull(outputStream);

            using var doc = WordprocessingDocument.Create(outputStream, WordprocessingDocumentType.Document);

            MainDocumentPart mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());

            string altChunkId = "altChunkId_" + Guid.NewGuid().ToString("N")[..8];

            AlternativeFormatImportPart altChunkPart =
                mainPart.AddAlternativeFormatImportPart(
                    AlternativeFormatImportPartType.Rtf, altChunkId);

            byte[] rtfBytes = Encoding.ASCII.GetBytes(rtfContent);
            using (var rtfStream = new MemoryStream(rtfBytes))
            {
                altChunkPart.FeedData(rtfStream);
            }

            AltChunk altChunk = new() { Id = altChunkId };
            mainPart.Document.Body!.Append(altChunk);
            mainPart.Document.Body.Append(new Paragraph());

            mainPart.Document.Save();
        }
    }
}
