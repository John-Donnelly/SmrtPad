using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Xunit;
using SmrtPad.Helpers;

namespace SmrtPad.Tests
{
    /// <summary>
    /// Tests for <see cref="DocxAltChunkExporter"/> which uses the OpenXml AltChunk
    /// mechanism for perfect-fidelity RTF-to-DOCX export.
    /// </summary>
    public class DocxAltChunkExporterTests
    {
        private const string SimpleRtf = @"{\rtf1\ansi\pard Hello World\par}";
        private const string BoldRtf = @"{\rtf1\ansi\pard \b Bold Text\b0\par}";

        // ── Argument validation ────────────────────────────────────────────────

        [Fact]
        public void ExportToDocx_NullRtf_ThrowsArgumentException()
        {
            using var ms = new MemoryStream();
            Assert.Throws<ArgumentException>(() => DocxAltChunkExporter.ExportToDocx(null!, ms));
        }

        [Fact]
        public void ExportToDocx_EmptyRtf_ThrowsArgumentException()
        {
            using var ms = new MemoryStream();
            Assert.Throws<ArgumentException>(() => DocxAltChunkExporter.ExportToDocx(string.Empty, ms));
        }

        [Fact]
        public void ExportToDocx_WhitespaceRtf_ThrowsArgumentException()
        {
            using var ms = new MemoryStream();
            Assert.Throws<ArgumentException>(() => DocxAltChunkExporter.ExportToDocx("   ", ms));
        }

        [Fact]
        public void ExportToDocx_NullStream_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => DocxAltChunkExporter.ExportToDocx(SimpleRtf, null!));
        }

        // ── Valid output structure ─────────────────────────────────────────────

        [Fact]
        public void ExportToDocx_SimpleRtf_ProducesNonEmptyOutput()
        {
            using var ms = new MemoryStream();
            DocxAltChunkExporter.ExportToDocx(SimpleRtf, ms);
            Assert.True(ms.Length > 0);
        }

        [Fact]
        public void ExportToDocx_SimpleRtf_OutputIsValidZip()
        {
            using var ms = new MemoryStream();
            DocxAltChunkExporter.ExportToDocx(SimpleRtf, ms);
            ms.Position = 0;

            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            Assert.NotEmpty(zip.Entries);
        }

        [Fact]
        public void ExportToDocx_SimpleRtf_ContainsDocumentXml()
        {
            using var ms = new MemoryStream();
            DocxAltChunkExporter.ExportToDocx(SimpleRtf, ms);
            ms.Position = 0;

            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var docEntry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("document.xml", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(docEntry);
        }

        [Fact]
        public void ExportToDocx_SimpleRtf_ContainsContentTypesXml()
        {
            using var ms = new MemoryStream();
            DocxAltChunkExporter.ExportToDocx(SimpleRtf, ms);
            ms.Position = 0;

            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var contentTypes = zip.GetEntry("[Content_Types].xml");
            Assert.NotNull(contentTypes);
        }

        [Fact]
        public void ExportToDocx_SimpleRtf_ContainsAltChunkPart()
        {
            using var ms = new MemoryStream();
            DocxAltChunkExporter.ExportToDocx(SimpleRtf, ms);
            ms.Position = 0;

            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            // AltChunk parts are stored under word/ with a unique name
            var altChunkEntry = zip.Entries.FirstOrDefault(e =>
                e.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase) &&
                !e.FullName.EndsWith("document.xml", StringComparison.OrdinalIgnoreCase) &&
                !e.FullName.Contains("_rels", StringComparison.OrdinalIgnoreCase) &&
                !e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(altChunkEntry);
        }

        [Fact]
        public void ExportToDocx_SimpleRtf_DocumentXmlReferencesAltChunk()
        {
            using var ms = new MemoryStream();
            DocxAltChunkExporter.ExportToDocx(SimpleRtf, ms);
            ms.Position = 0;

            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var docEntry = zip.Entries.First(e => e.FullName.EndsWith("document.xml", StringComparison.OrdinalIgnoreCase));
            using var stream = docEntry.Open();
            var doc = XDocument.Load(stream);

            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var altChunkElements = doc.Descendants(w + "altChunk").ToList();
            Assert.Single(altChunkElements);
        }

        [Fact]
        public void ExportToDocx_SimpleRtf_AltChunkPartContainsRtfContent()
        {
            using var ms = new MemoryStream();
            DocxAltChunkExporter.ExportToDocx(SimpleRtf, ms);
            ms.Position = 0;

            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var altChunkEntry = zip.Entries.FirstOrDefault(e =>
                e.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase) &&
                !e.FullName.EndsWith("document.xml", StringComparison.OrdinalIgnoreCase) &&
                !e.FullName.Contains("_rels", StringComparison.OrdinalIgnoreCase) &&
                !e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(altChunkEntry);

            using var partStream = altChunkEntry.Open();
            using var reader = new StreamReader(partStream);
            string content = reader.ReadToEnd();
            Assert.Contains("Hello World", content);
        }

        // ── RTF content fidelity ──────────────────────────────────────────────

        [Fact]
        public void ExportToDocx_BoldRtf_AltChunkPreservesBoldMarkers()
        {
            using var ms = new MemoryStream();
            DocxAltChunkExporter.ExportToDocx(BoldRtf, ms);
            ms.Position = 0;

            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var altChunkEntry = zip.Entries.FirstOrDefault(e =>
                e.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase) &&
                !e.FullName.EndsWith("document.xml", StringComparison.OrdinalIgnoreCase) &&
                !e.FullName.Contains("_rels", StringComparison.OrdinalIgnoreCase) &&
                !e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(altChunkEntry);

            using var partStream = altChunkEntry.Open();
            using var reader = new StreamReader(partStream);
            string content = reader.ReadToEnd();
            // The original RTF is embedded verbatim — bold markers should be present
            Assert.Contains(@"\b", content);
            Assert.Contains("Bold Text", content);
        }

        [Theory]
        [InlineData(@"{\rtf1\ansi\pard Café\par}", @"Caf\u233?")]
        [InlineData(@"{\rtf1\ansi\pard 漢字\par}", @"\u28450?\u23383?")]
        public void ExportToDocx_RawUnicodeRtf_AltChunkPreservesCharacters(string rtf, string expectedFragment)
        {
            using var ms = new MemoryStream();
            DocxAltChunkExporter.ExportToDocx(rtf, ms);
            ms.Position = 0;

            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var altChunkEntry = zip.Entries.FirstOrDefault(e =>
                e.FullName.StartsWith("word/", StringComparison.OrdinalIgnoreCase) &&
                !e.FullName.EndsWith("document.xml", StringComparison.OrdinalIgnoreCase) &&
                !e.FullName.Contains("_rels", StringComparison.OrdinalIgnoreCase) &&
                !e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(altChunkEntry);

            using var partStream = altChunkEntry.Open();
            using var reader = new StreamReader(partStream);
            string content = reader.ReadToEnd();

            Assert.Contains(expectedFragment, content);
        }

        [Fact]
        public void ExportToDocx_ContentTypesIncludesRtfMimeType()
        {
            using var ms = new MemoryStream();
            DocxAltChunkExporter.ExportToDocx(SimpleRtf, ms);
            ms.Position = 0;

            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var contentTypesEntry = zip.GetEntry("[Content_Types].xml");
            Assert.NotNull(contentTypesEntry);

            using var stream = contentTypesEntry.Open();
            string xml = new StreamReader(stream).ReadToEnd();
            // OpenXml registers the RTF AltChunk part with the application/rtf content type
            Assert.Contains("application/rtf", xml);
        }

        [Fact]
        public void ExportToDocx_DocumentHasTrailingParagraph()
        {
            using var ms = new MemoryStream();
            DocxAltChunkExporter.ExportToDocx(SimpleRtf, ms);
            ms.Position = 0;

            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var docEntry = zip.Entries.First(e => e.FullName.EndsWith("document.xml", StringComparison.OrdinalIgnoreCase));
            using var stream = docEntry.Open();
            var doc = XDocument.Load(stream);

            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var body = doc.Descendants(w + "body").First();
            var lastChild = body.Elements().Last();
            // Trailing paragraph after the AltChunk
            Assert.Equal(w + "p", lastChild.Name);
        }

        // ── Multiple exports ──────────────────────────────────────────────────

        [Fact]
        public void ExportToDocx_CalledTwice_ProducesIndependentOutputs()
        {
            using var ms1 = new MemoryStream();
            using var ms2 = new MemoryStream();

            DocxAltChunkExporter.ExportToDocx(SimpleRtf, ms1);
            DocxAltChunkExporter.ExportToDocx(BoldRtf, ms2);

            Assert.True(ms1.Length > 0);
            Assert.True(ms2.Length > 0);
            // Both should be valid and contain different embedded RTF content
            Assert.NotEqual(ms1.ToArray(), ms2.ToArray());
        }
    }
}
