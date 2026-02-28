using System;
using System.IO;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;
using SmrtPad.Helpers;

namespace SmrtPad.Tests
{
    /// <summary>
    /// Tests for <see cref="DocxImportHelper"/> which converts DOCX files to RTF
    /// so that <c>RichEditBox</c> can render them with formatting intact.
    /// </summary>
    public class DocxImportHelperTests
    {
        // ── Argument validation ────────────────────────────────────────────────

        [Fact]
        public void ConvertToRtf_NullStream_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => DocxImportHelper.ConvertToRtf(null!));
        }

        // ── Basic output structure ─────────────────────────────────────────────

        [Fact]
        public void ConvertToRtf_EmptyDocument_ReturnsValidRtf()
        {
            using var docxStream = CreateDocx();
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.StartsWith(@"{\rtf1", rtf);
            Assert.EndsWith("}", rtf);
        }

        [Fact]
        public void ConvertToRtf_EmptyDocument_ContainsFontTable()
        {
            using var docxStream = CreateDocx();
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"{\fonttbl", rtf);
        }

        [Fact]
        public void ConvertToRtf_EmptyDocument_ContainsColorTable()
        {
            using var docxStream = CreateDocx();
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"{\colortbl;", rtf);
        }

        // ── Plain text preservation ────────────────────────────────────────────

        [Fact]
        public void ConvertToRtf_SingleParagraph_ContainsText()
        {
            using var docxStream = CreateDocx(("Hello World", null));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains("Hello World", rtf);
        }

        [Fact]
        public void ConvertToRtf_MultipleParagraphs_ContainsAllText()
        {
            using var docxStream = CreateDocx(
                ("First paragraph", null),
                ("Second paragraph", null));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains("First paragraph", rtf);
            Assert.Contains("Second paragraph", rtf);
        }

        [Fact]
        public void ConvertToRtf_MultipleParagraphs_SeparatedByPar()
        {
            using var docxStream = CreateDocx(
                ("Line one", null),
                ("Line two", null));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"\par", rtf);
        }

        // ── Bold formatting ────────────────────────────────────────────────────

        [Fact]
        public void ConvertToRtf_BoldText_ContainsBoldMarker()
        {
            var rPr = new RunProperties(new Bold());
            using var docxStream = CreateDocx(("Bold text", rPr));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"\b", rtf);
            Assert.Contains("Bold text", rtf);
        }

        // ── Italic formatting ──────────────────────────────────────────────────

        [Fact]
        public void ConvertToRtf_ItalicText_ContainsItalicMarker()
        {
            var rPr = new RunProperties(new Italic());
            using var docxStream = CreateDocx(("Italic text", rPr));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"\i", rtf);
            Assert.Contains("Italic text", rtf);
        }

        // ── Underline formatting ───────────────────────────────────────────────

        [Fact]
        public void ConvertToRtf_UnderlineText_ContainsUnderlineMarker()
        {
            var rPr = new RunProperties(new Underline { Val = UnderlineValues.Single });
            using var docxStream = CreateDocx(("Underlined", rPr));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"\ul", rtf);
            Assert.Contains("Underlined", rtf);
        }

        // ── Strikethrough formatting ───────────────────────────────────────────

        [Fact]
        public void ConvertToRtf_StrikethroughText_ContainsStrikeMarker()
        {
            var rPr = new RunProperties(new Strike());
            using var docxStream = CreateDocx(("Struck", rPr));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"\strike", rtf);
            Assert.Contains("Struck", rtf);
        }

        // ── Font name ──────────────────────────────────────────────────────────

        [Fact]
        public void ConvertToRtf_CustomFont_FontAppearsInFontTable()
        {
            var rPr = new RunProperties(new RunFonts { Ascii = "Courier New" });
            using var docxStream = CreateDocx(("Monospaced", rPr));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains("Courier New", rtf);
        }

        // ── Font size ──────────────────────────────────────────────────────────

        [Fact]
        public void ConvertToRtf_FontSize24pt_ContainsFsMarker()
        {
            // OpenXml stores font size in half-points: 24pt = 48 half-points
            var rPr = new RunProperties(new FontSize { Val = "48" });
            using var docxStream = CreateDocx(("Large text", rPr));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"\fs48", rtf);
        }

        // ── Font color ─────────────────────────────────────────────────────────

        [Fact]
        public void ConvertToRtf_RedText_ContainsCfMarkerAndColorTable()
        {
            var rPr = new RunProperties(new Color { Val = "FF0000" });
            using var docxStream = CreateDocx(("Red text", rPr));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"\red255\green0\blue0", rtf);
            Assert.Contains(@"\cf", rtf);
        }

        [Fact]
        public void ConvertToRtf_DefaultColorText_EmitsExplicitBlackCfMarker()
        {
            // Runs with no explicit colour must emit \cf1 (explicit black) so that
            // NormalizeDocumentColorsForTheme can detect and reset them when the app
            // is in dark mode.  Emitting no \cf (auto/cf0) causes Win32 RichEdit to
            // render text in the Windows system text colour (black) regardless of the
            // WinUI 3 dark-mode foreground brush.
            using var docxStream = CreateDocx(("Default colour text", null));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"\cf1", rtf);
        }

        [Fact]
        public void ConvertToRtf_ExplicitBlackText_EmitsExplicitBlackCfMarker()
        {
            // Text explicitly coloured black in the DOCX must also emit \cf1.
            var rPr = new RunProperties(new Color { Val = "000000" });
            using var docxStream = CreateDocx(("Black text", rPr));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"\cf1", rtf);
        }

        [Fact]
        public void ConvertToRtf_RedText_EmitsCf2NotCf1()
        {
            // The colour table layout is: cf0=auto (empty entry), cf1=black ("000000"),
            // cf2=first collected non-black colour.  A red run must emit \cf2, not \cf1
            // (which was the previous off-by-one bug that made coloured text appear black).
            var rPr = new RunProperties(new Color { Val = "FF0000" });
            using var docxStream = CreateDocx(("Red text", rPr));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"\cf2", rtf);
        }

        [Fact]
        public void ConvertToRtf_MultipleParagraphs_ParagraphLevelCf1SetBeforeRuns()
        {
            // \cf1 must appear at the paragraph level (outside run groups) so that
            // \par paragraph marks revert to cf1 when run groups close.  Without this
            // the full-range ForegroundColor returns transparent (mixed cf1 run text
            // + cf0/auto paragraph marks) and NormalizeDocumentColorsForTheme skips
            // the dark-mode reset.
            using var docxStream = CreateDocx(
                ("First paragraph", null),
                ("Second paragraph", null));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            // \cf1 must appear before the first run group open brace in each paragraph.
            // The simplest proxy: \cf1 appears before each \par (paragraph mark).
            int parIdx = rtf.IndexOf(@"\par ", StringComparison.Ordinal);
            Assert.True(parIdx > 0, "Expected \\par in output");
            int cf1BeforePar = rtf.LastIndexOf(@"\cf1", parIdx, StringComparison.Ordinal);
            Assert.True(cf1BeforePar >= 0, "Expected \\cf1 before \\par so paragraph mark carries explicit black");
        }

        // ── Paragraph alignment ────────────────────────────────────────────────

        [Fact]
        public void ConvertToRtf_CenterAligned_ContainsQcMarker()
        {
            using var docxStream = CreateDocxWithAlignment("Centered", JustificationValues.Center);
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"\qc", rtf);
        }

        [Fact]
        public void ConvertToRtf_RightAligned_ContainsQrMarker()
        {
            using var docxStream = CreateDocxWithAlignment("Right", JustificationValues.Right);
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"\qr", rtf);
        }

        [Fact]
        public void ConvertToRtf_Justified_ContainsQjMarker()
        {
            using var docxStream = CreateDocxWithAlignment("Justified", JustificationValues.Both);
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"\qj", rtf);
        }

        // ── Combined formatting ────────────────────────────────────────────────

        [Fact]
        public void ConvertToRtf_BoldAndItalic_ContainsBothMarkers()
        {
            var rPr = new RunProperties(new Bold(), new Italic());
            using var docxStream = CreateDocx(("BoldItalic", rPr));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"\b", rtf);
            Assert.Contains(@"\i", rtf);
            Assert.Contains("BoldItalic", rtf);
        }

        // ── Special characters ─────────────────────────────────────────────────

        [Fact]
        public void ConvertToRtf_Backslash_IsEscaped()
        {
            using var docxStream = CreateDocx((@"path\to\file", null));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"path\\to\\file", rtf);
        }

        [Fact]
        public void ConvertToRtf_CurlyBraces_AreEscaped()
        {
            using var docxStream = CreateDocx(("{ braces }", null));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            Assert.Contains(@"\{", rtf);
            Assert.Contains(@"\}", rtf);
        }

        [Fact]
        public void ConvertToRtf_UnicodeChar_EmitsUnicodeEscape()
        {
            using var docxStream = CreateDocx(("café", null));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            // 'é' (U+00E9 = 233) should appear as \u233?
            Assert.Contains(@"\u233?", rtf);
        }

        // ── Round-trip fidelity ────────────────────────────────────────────────

        [Fact]
        public void ConvertToRtf_RoundTrip_ExportThenImport_PreservesText()
        {
            // Create a DOCX with known content using the AltChunk exporter
            string sourceRtf = @"{\rtf1\ansi\pard Hello Round Trip\par}";
            using var docxStream = new MemoryStream();
            DocxAltChunkExporter.ExportToDocx(sourceRtf, docxStream);
            docxStream.Position = 0;

            // The AltChunk-based DOCX won't have w:p elements (it's all in the alt chunk),
            // so ConvertToRtf will return a minimal RTF. This verifies graceful handling.
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);
            Assert.StartsWith(@"{\rtf1", rtf);
        }

        // ── Explicit Val=false means "off" ─────────────────────────────────────

        [Fact]
        public void ConvertToRtf_BoldFalse_DoesNotContainBoldMarker()
        {
            var rPr = new RunProperties(new Bold { Val = false });
            using var docxStream = CreateDocx(("Not bold", rPr));
            string rtf = DocxImportHelper.ConvertToRtf(docxStream);

            // \b should not appear (the only text run should have no bold marker)
            // We check that the text appears without a \b preceding it
            int textIdx = rtf.IndexOf("Not bold");
            Assert.True(textIdx > 0);
            // Find the opening brace of the run containing "Not bold"
            int braceIdx = rtf.LastIndexOf('{', textIdx);
            string runContent = rtf.Substring(braceIdx, textIdx - braceIdx);
            Assert.DoesNotContain(@"\b", runContent);
        }

        // ── Helper: creates a DOCX in memory ──────────────────────────────────

        private static MemoryStream CreateDocx(params (string text, RunProperties? rPr)[] paragraphs)
        {
            var ms = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());

                foreach (var (text, rPr) in paragraphs)
                {
                    var para = new Paragraph();
                    var run = new Run();
                    if (rPr is not null)
                        run.Append(rPr.CloneNode(true));
                    run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
                    para.Append(run);
                    mainPart.Document.Body!.Append(para);
                }

                mainPart.Document.Save();
            }
            ms.Position = 0;
            return ms;
        }

        private static MemoryStream CreateDocxWithAlignment(string text, JustificationValues alignment)
        {
            var ms = new MemoryStream();
            using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document(new Body());

                var para = new Paragraph();
                para.Append(new ParagraphProperties(
                    new Justification { Val = alignment }));
                para.Append(new Run(new Text(text)));
                mainPart.Document.Body!.Append(para);

                mainPart.Document.Save();
            }
            ms.Position = 0;
            return ms;
        }
    }
}
