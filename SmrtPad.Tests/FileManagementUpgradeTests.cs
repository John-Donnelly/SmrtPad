using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using SmrtPad.Helpers;
using SmrtPad.Services;
using Xunit;

namespace SmrtPad.Tests
{
    public sealed class HtmlConverterHelperTests
    {
        [Fact]
        public void ToPlainText_StripsHtmlTagsAndPreservesParagraphBreaks()
        {
            const string html = "<html><body><p>Hello <b>World</b></p><p>Second&nbsp;Line</p></body></html>";

            string text = HtmlConverterHelper.ToPlainText(html);

            Assert.Contains("Hello World", text);
            Assert.Contains("Second\u00A0Line", text);
            Assert.Contains(Environment.NewLine + Environment.NewLine, text);
        }

        [Fact]
        public void ToPlainText_NullOrWhitespace_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, HtmlConverterHelper.ToPlainText(null!));
            Assert.Equal(string.Empty, HtmlConverterHelper.ToPlainText(""));
            Assert.Equal(string.Empty, HtmlConverterHelper.ToPlainText("   "));
        }

        [Fact]
        public void ToPlainText_BrTags_ConvertToNewlines()
        {
            string text = HtmlConverterHelper.ToPlainText("Line1<br/>Line2<BR >Line3");

            Assert.Contains("Line1", text);
            Assert.Contains("Line2", text);
            Assert.Contains("Line3", text);
        }

        [Fact]
        public void ToPlainText_ListItems_ConvertToBullets()
        {
            string text = HtmlConverterHelper.ToPlainText("<ul><li>Alpha</li><li>Beta</li></ul>");

            Assert.Contains("• Alpha", text);
            Assert.Contains("• Beta", text);
        }

        [Fact]
        public void ToPlainText_DivBlocks_PreserveParagraphBreaks()
        {
            string text = HtmlConverterHelper.ToPlainText("<div>First</div><div>Second</div>");

            Assert.Contains($"First{Environment.NewLine}{Environment.NewLine}Second", text);
        }

        [Fact]
        public void ToPlainText_TableRows_PreserveParagraphBreaks()
        {
            string text = HtmlConverterHelper.ToPlainText("<table><tr><td>Row 1</td></tr><tr><td>Row 2</td></tr></table>");

            Assert.Contains($"Row 1{Environment.NewLine}{Environment.NewLine}Row 2", text);
        }

        [Fact]
        public void ToPlainText_HtmlEntities_AreDecoded()
        {
            string text = HtmlConverterHelper.ToPlainText("<p>&amp; &lt; &gt; &quot;</p>");

            Assert.Contains("& < > \"", text);
        }

        [Fact]
        public void ToPlainText_MultipleBlankLines_CollapsedToTwo()
        {
            string text = HtmlConverterHelper.ToPlainText("<p>A</p><p></p><p></p><p>B</p>");
            int maxConsecutiveNewlines = 0;
            int current = 0;
            foreach (char c in text)
            {
                if (c == '\n' || c == '\r')
                    current++;
                else
                {
                    maxConsecutiveNewlines = Math.Max(maxConsecutiveNewlines, current);
                    current = 0;
                }
            }
            maxConsecutiveNewlines = Math.Max(maxConsecutiveNewlines, current);
            // Environment.NewLine is \r\n on Windows, so 2 blank lines = 4 chars max
            Assert.True(maxConsecutiveNewlines <= 4);
        }

        [Fact]
        public void FromPlainText_CreatesHtmlParagraphs()
        {
            string html = HtmlConverterHelper.FromPlainText("First line\n\nSecond line");

            Assert.Contains("<html><body>", html);
            Assert.Contains("<p>First line</p>", html);
            Assert.Contains("<p>Second line</p>", html);
            Assert.Contains("</body></html>", html);
        }

        [Fact]
        public void FromPlainText_EmptyString_ReturnsEmptyHtmlBody()
        {
            string html = HtmlConverterHelper.FromPlainText("");

            Assert.Equal("<html><body></body></html>", html);
        }

        [Fact]
        public void FromPlainText_Null_ReturnsEmptyHtmlBody()
        {
            string html = HtmlConverterHelper.FromPlainText(null!);

            Assert.Equal("<html><body></body></html>", html);
        }

        [Fact]
        public void FromPlainText_SpecialChars_AreEncoded()
        {
            string html = HtmlConverterHelper.FromPlainText("a < b & c > d");

            Assert.Contains("&lt;", html);
            Assert.Contains("&amp;", html);
            Assert.Contains("&gt;", html);
        }

        [Fact]
        public void FromPlainText_SingleLineBreaks_ConvertToBr()
        {
            string html = HtmlConverterHelper.FromPlainText("Line1\nLine2");

            Assert.Contains("<br/>", html);
        }

        [Fact]
        public void RoundTrip_PreservesContent()
        {
            const string original = "Hello World\n\nSecond paragraph";

            string html = HtmlConverterHelper.FromPlainText(original);
            string restored = HtmlConverterHelper.ToPlainText(html);

            Assert.Contains("Hello World", restored);
            Assert.Contains("Second paragraph", restored);
        }
    }

    public sealed class OdtImportExportTests
    {
        [Fact]
        public void Export_CreatesRequiredOdtEntries()
        {
            using var output = new MemoryStream();
            OdtExportHelper.Export("Alpha\nBeta", output);
            output.Position = 0;

            using var archive = new ZipArchive(output, ZipArchiveMode.Read, leaveOpen: true);
            Assert.NotNull(archive.GetEntry("mimetype"));
            Assert.NotNull(archive.GetEntry("content.xml"));
            Assert.NotNull(archive.GetEntry("META-INF/manifest.xml"));
        }

        [Fact]
        public void Export_MimetypeIsCorrect()
        {
            using var output = new MemoryStream();
            OdtExportHelper.Export("test", output);
            output.Position = 0;

            using var archive = new ZipArchive(output, ZipArchiveMode.Read, leaveOpen: true);
            using var reader = new StreamReader(archive.GetEntry("mimetype")!.Open());
            Assert.Equal("application/vnd.oasis.opendocument.text", reader.ReadToEnd());
        }

        [Fact]
        public void Export_NullStream_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => OdtExportHelper.Export("text", null!));
        }

        [Fact]
        public void Export_ReadOnlyStream_ThrowsArgumentException()
        {
            using var readOnly = new MemoryStream(Array.Empty<byte>(), writable: false);
            Assert.Throws<ArgumentException>(() => OdtExportHelper.Export("text", readOnly));
        }

        [Fact]
        public void Export_EmptyText_ProducesValidOdt()
        {
            using var output = new MemoryStream();
            OdtExportHelper.Export("", output);
            output.Position = 0;

            using var archive = new ZipArchive(output, ZipArchiveMode.Read, leaveOpen: true);
            Assert.NotNull(archive.GetEntry("content.xml"));
        }

        [Fact]
        public void Export_ContentXmlContainsParagraphs()
        {
            using var output = new MemoryStream();
            OdtExportHelper.Export("Line1\nLine2", output);
            output.Position = 0;

            using var archive = new ZipArchive(output, ZipArchiveMode.Read, leaveOpen: true);
            using var reader = new StreamReader(archive.GetEntry("content.xml")!.Open());
            string xml = reader.ReadToEnd();

            Assert.Contains("Line1", xml);
            Assert.Contains("Line2", xml);
        }

        [Fact]
        public void ExtractText_Docx_ReturnsContent()
        {
            using var docxStream = BuildSimpleDocx("Hello Test");

            string text = DocumentImportHelper.ExtractText(docxStream, ".docx");

            Assert.Contains("Hello Test", text);
        }

        [Fact]
        public void ExtractText_Odt_ReturnsContent()
        {
            using var odtStream = BuildSimpleOdt("ODT content");

            string text = DocumentImportHelper.ExtractText(odtStream, ".odt");

            Assert.Contains("ODT content", text);
        }

        [Fact]
        public void ConvertOdtToRtf_PreservesParagraphsAndBasicFormatting()
        {
            using var odt = BuildStyledOdt();

            string rtf = DocumentImportHelper.ConvertOdtToRtf(odt);

            Assert.Contains("Plain ", rtf);
            Assert.Contains("Bold", rtf);
            Assert.Contains(@"\b", rtf);
            Assert.Contains(@"\par", rtf);
        }

        [Fact]
        public void ConvertOdtToRtf_NullStream_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => DocumentImportHelper.ConvertOdtToRtf(null!));
        }

        [Fact]
        public void ConvertOdtToRtf_EmptyOdt_ReturnsFallbackRtf()
        {
            var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                // No content.xml entry
                archive.CreateEntry("mimetype");
            }

            ms.Position = 0;
            string rtf = DocumentImportHelper.ConvertOdtToRtf(ms);

            Assert.StartsWith(@"{\rtf1", rtf);
        }

        [Fact]
        public void ConvertOdtToRtf_ContainsFontTable()
        {
            using var odt = BuildStyledOdt();
            string rtf = DocumentImportHelper.ConvertOdtToRtf(odt);

            Assert.Contains(@"{\fonttbl", rtf);
        }

        [Fact]
        public void ConvertOdtToRtf_ContainsColorTable()
        {
            using var odt = BuildStyledOdt();
            string rtf = DocumentImportHelper.ConvertOdtToRtf(odt);

            Assert.Contains(@"{\colortbl", rtf);
        }

        private static MemoryStream BuildStyledOdt()
        {
            const string contentXml = """
                <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                                         xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
                                         xmlns:style="urn:oasis:names:tc:opendocument:xmlns:style:1.0"
                                         office:version="1.2">
                  <office:automatic-styles>
                    <style:style style:name="T1" style:family="text">
                      <style:text-properties fo:font-weight="bold"
                                             xmlns:fo="urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0" />
                    </style:style>
                  </office:automatic-styles>
                  <office:body>
                    <office:text>
                      <text:p>Plain <text:span text:style-name="T1">Bold</text:span></text:p>
                      <text:p>Second paragraph</text:p>
                    </office:text>
                  </office:body>
                </office:document-content>
                """;

            var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var contentEntry = archive.CreateEntry("content.xml");
                using (var writer = new StreamWriter(contentEntry.Open(), new UTF8Encoding(false)))
                {
                    writer.Write(contentXml);
                }

                var stylesEntry = archive.CreateEntry("styles.xml");
                using (var writer = new StreamWriter(stylesEntry.Open(), new UTF8Encoding(false)))
                {
                    writer.Write("<office:document-styles xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" office:version=\"1.2\" />");
                }
            }

            ms.Position = 0;
            return ms;
        }

        private static MemoryStream BuildSimpleDocx(string text)
        {
            var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("word/document.xml");
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write($"""
                    <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                      <w:body><w:p><w:r><w:t>{text}</w:t></w:r></w:p></w:body>
                    </w:document>
                    """);
            }

            ms.Position = 0;
            return ms;
        }

        private static MemoryStream BuildSimpleOdt(string text)
        {
            var ms = new MemoryStream();
            using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("content.xml");
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write($"""
                    <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                                             xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0"
                                             office:version="1.2">
                      <office:body><office:text><text:p>{text}</text:p></office:text></office:body>
                    </office:document-content>
                    """);
            }

            ms.Position = 0;
            return ms;
        }
    }

    public sealed class SettingsServiceRecentFilePruningTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _settingsPath;

        public SettingsServiceRecentFilePruningTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "SmrtPad_SettingsPrune_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _settingsPath = Path.Combine(_tempDir, "settings.json");
        }

        [Fact]
        public void Load_RemovesMissingRecentFiles()
        {
            string existing = Path.Combine(_tempDir, "existing.rtf");
            File.WriteAllText(existing, "data");

            File.WriteAllText(
                _settingsPath,
                $$"""
                {
                  "RecentFiles": [
                    "{{existing.Replace("\\", "\\\\")}}",
                    "C:\\does-not-exist\\missing.rtf"
                  ]
                }
                """);

            var service = new SettingsService(_settingsPath);

            Assert.Single(service.RecentFiles);
            Assert.Equal(existing, service.RecentFiles[0]);
        }

        [Fact]
        public void Load_RemovesDuplicateRecentFiles()
        {
            string file1 = Path.Combine(_tempDir, "file1.rtf");
            File.WriteAllText(file1, "data");

            File.WriteAllText(
                _settingsPath,
                $$"""
                {
                  "RecentFiles": [
                    "{{file1.Replace("\\", "\\\\")}}",
                    "{{file1.Replace("\\", "\\\\")}}"
                  ]
                }
                """);

            var service = new SettingsService(_settingsPath);

            Assert.Single(service.RecentFiles);
        }

        [Fact]
        public void AddRecentFile_EmptyPath_IsIgnored()
        {
            var service = new SettingsService(_settingsPath);

            service.AddRecentFile("");
            service.AddRecentFile("   ");

            Assert.Empty(service.RecentFiles);
        }

        [Fact]
        public void AddRecentFile_MovesExistingToTop()
        {
            string file1 = Path.Combine(_tempDir, "file1.rtf");
            string file2 = Path.Combine(_tempDir, "file2.rtf");
            File.WriteAllText(file1, "data");
            File.WriteAllText(file2, "data");

            var service = new SettingsService(_settingsPath);
            service.AddRecentFile(file1);
            service.AddRecentFile(file2);
            service.AddRecentFile(file1);

            Assert.Equal(2, service.RecentFiles.Count);
            Assert.Equal(file1, service.RecentFiles[0]);
            Assert.Equal(file2, service.RecentFiles[1]);
        }

        [Fact]
        public void ClearRecentFiles_RemovesAll()
        {
            string file1 = Path.Combine(_tempDir, "file1.rtf");
            File.WriteAllText(file1, "data");

            var service = new SettingsService(_settingsPath);
            service.AddRecentFile(file1);
            service.ClearRecentFiles();

            Assert.Empty(service.RecentFiles);
        }

        [Fact]
        public void PageSetup_DefaultsAreCorrect()
        {
            var service = new SettingsService(_settingsPath);

            Assert.Equal("Letter", service.PagePaperSize);
            Assert.Equal("Portrait", service.PageOrientation);
            Assert.Equal(1.0, service.PageMarginTopInches);
            Assert.Equal(1.0, service.PageMarginBottomInches);
            Assert.Equal(1.0, service.PageMarginLeftInches);
            Assert.Equal(1.0, service.PageMarginRightInches);
        }

        [Fact]
        public void PageSetup_PersistsAcrossReload()
        {
            var service = new SettingsService(_settingsPath);
            service.PagePaperSize = "A4";
            service.PageOrientation = "Landscape";
            service.PageMarginTopInches = 0.5;
            service.PageMarginBottomInches = 0.75;
            service.PageMarginLeftInches = 1.25;
            service.PageMarginRightInches = 1.5;
            service.Save();

            var reloaded = new SettingsService(_settingsPath);

            Assert.Equal("A4", reloaded.PagePaperSize);
            Assert.Equal("Landscape", reloaded.PageOrientation);
            Assert.Equal(0.5, reloaded.PageMarginTopInches);
            Assert.Equal(0.75, reloaded.PageMarginBottomInches);
            Assert.Equal(1.25, reloaded.PageMarginLeftInches);
            Assert.Equal(1.5, reloaded.PageMarginRightInches);
        }

        [Fact]
        public void AddRecentFile_LimitsToMaxTen()
        {
            var service = new SettingsService(_settingsPath);

            for (int i = 0; i < 15; i++)
            {
                string path = Path.Combine(_tempDir, $"file{i}.rtf");
                File.WriteAllText(path, "data");
                service.AddRecentFile(path);
            }

            Assert.Equal(10, service.RecentFiles.Count);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
            }
        }
    }
}
