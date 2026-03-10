using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using SmrtPad.Helpers;
using Xunit;

namespace SmrtPad.Tests
{
    public sealed class ReleaseReadinessBehaviorTests
    {
        [Fact]
        public void ToPlainText_ScriptAndStyleContent_IsRemoved()
        {
            const string html = "<html><head><style>.hidden{display:none;}</style><script>alert('x');</script></head><body><p>Visible</p></body></html>";

            string text = HtmlConverterHelper.ToPlainText(html);

            Assert.Equal("Visible", text);
        }

        [Fact]
        public void ExtractText_NullStream_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => DocumentImportHelper.ExtractText(null!, ".docx"));
        }

        [Fact]
        public void ExtractText_WhitespaceExtension_ThrowsArgumentException()
        {
            using var stream = new MemoryStream();

            Assert.Throws<ArgumentException>(() => DocumentImportHelper.ExtractText(stream, "   "));
        }

        [Fact]
        public void ExtractText_MixedCaseDocxExtension_ReturnsContent()
        {
            using var stream = CreateDocxStream("Case insensitive import");

            string result = DocumentImportHelper.ExtractText(stream, ".DoCx");

            Assert.Equal("Case insensitive import", result);
        }

        [Fact]
        public void ExportToDocx_ReadOnlyStream_ThrowsArgumentException()
        {
            using var stream = new MemoryStream(new byte[32], writable: false);

            Assert.Throws<ArgumentException>(() => DocxAltChunkExporter.ExportToDocx(@"{\rtf1\ansi\pard Test\par}", stream));
        }

        [Fact]
        public void GeneratePdf_ZeroFontSize_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PdfHelper.GeneratePdf("text", 0));
        }

        [Fact]
        public void GeneratePdf_NegativeFontSize_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PdfHelper.GeneratePdf("text", -1));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void GeneratePdf_NonFiniteFontSize_ThrowsArgumentOutOfRangeException(double fontSize)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PdfHelper.GeneratePdf("text", fontSize));
        }

        private static MemoryStream CreateDocxStream(string text)
        {
            const string ns = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var doc = new XDocument(
                new XElement(XName.Get("document", ns),
                    new XElement(XName.Get("body", ns),
                        new XElement(XName.Get("p", ns),
                            new XElement(XName.Get("r", ns),
                                new XElement(XName.Get("t", ns), text))))));

            var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("word/document.xml");
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8, leaveOpen: false);
                doc.Save(writer);
            }

            stream.Position = 0;
            return stream;
        }
    }
}
