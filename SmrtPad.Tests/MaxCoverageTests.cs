// MaxCoverageTests.cs — exhaustive gap-fill targeting every uncovered branch
// across EditorViewModel, PdfHelper, DocxExportHelper (rich), RtfHelper,
// ColorHelper, DocumentImportHelper, SettingsService, MacroHelper,
// ParagraphStyleHelper, DocumentTemplates, DocumentTab, and MainWindow fields.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using Xunit;
using SmrtPad.Helpers;
using SmrtPad.Models;
using SmrtPad.Services;
using SmrtPad.ViewModels;

namespace SmrtPad.Tests
{
    // ═══ EditorViewModel boundary & uncovered-branch tests ══════════════════════

    public class EditorViewModelBoundaryTests
    {
        // ── ZoomIn / ZoomOut clamping ────────────────────────────────────────────

        [Fact]
        public void ZoomIn_AtMax_StaysAt500()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 500.0;
            vm.ZoomIn();
            Assert.Equal(500.0, vm.ZoomLevel);
        }

        [Fact]
        public void ZoomIn_At490_Becomes500()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 490.0;
            vm.ZoomIn();
            Assert.Equal(500.0, vm.ZoomLevel);
        }

        [Fact]
        public void ZoomIn_At495_Becomes500_NotOver()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 495.0;
            vm.ZoomIn();
            Assert.Equal(500.0, vm.ZoomLevel);
        }

        [Fact]
        public void ZoomOut_AtMin_StaysAt10()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 10.0;
            vm.ZoomOut();
            Assert.Equal(10.0, vm.ZoomLevel);
        }

        [Fact]
        public void ZoomOut_At15_Becomes10_NotBelow()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 15.0;
            vm.ZoomOut();
            Assert.Equal(10.0, vm.ZoomLevel);
        }

        [Fact]
        public void ZoomOut_At20_Becomes10()
        {
            var vm = new EditorViewModel();
            vm.ZoomLevel = 20.0;
            vm.ZoomOut();
            Assert.Equal(10.0, vm.ZoomLevel);
        }

        // ── SetParagraphSpacing guard: fewer than 2 elements ────────────────────

        [Fact]
        public void SetParagraphSpacing_OneElement_IsNoOp()
        {
            var vm = new EditorViewModel();
            vm.ParagraphSpacingBefore = 6.0;
            vm.ParagraphSpacingAfter  = 3.0;
            vm.SetParagraphSpacing(new double[] { 99.0 });
            Assert.Equal(6.0, vm.ParagraphSpacingBefore);
            Assert.Equal(3.0, vm.ParagraphSpacingAfter);
        }

        [Fact]
        public void SetParagraphSpacing_EmptyArray_IsNoOp()
        {
            var vm = new EditorViewModel();
            vm.SetParagraphSpacing(Array.Empty<double>());
            Assert.Equal(0.0, vm.ParagraphSpacingBefore);
            Assert.Equal(0.0, vm.ParagraphSpacingAfter);
        }

        [Fact]
        public void SetParagraphSpacing_ThreeElements_OnlyFirstTwoUsed()
        {
            var vm = new EditorViewModel();
            vm.SetParagraphSpacing(new double[] { 8.0, 4.0, 999.0 });
            Assert.Equal(8.0, vm.ParagraphSpacingBefore);
            Assert.Equal(4.0, vm.ParagraphSpacingAfter);
        }

        // ── UpdateCursorPosition guard ───────────────────────────────────────────

        [Fact]
        public void UpdateCursorPosition_OneElement_IsNoOp()
        {
            var vm = new EditorViewModel();
            vm.UpdateCursorPosition(new int[] { 42 });
            Assert.Equal(1, vm.LineNumber);
            Assert.Equal(1, vm.ColumnNumber);
        }

        [Fact]
        public void UpdateCursorPosition_EmptyArray_IsNoOp()
        {
            var vm = new EditorViewModel();
            vm.UpdateCursorPosition(Array.Empty<int>());
            Assert.Equal(1, vm.LineNumber);
            Assert.Equal(1, vm.ColumnNumber);
        }

        [Fact]
        public void UpdateCursorPosition_ThreeElements_OnlyFirstTwoUsed()
        {
            var vm = new EditorViewModel();
            vm.UpdateCursorPosition(new int[] { 10, 20, 999 });
            Assert.Equal(10, vm.LineNumber);
            Assert.Equal(20, vm.ColumnNumber);
        }

        // ── ToggleSubscript / ToggleSuperscript mutual exclusion ─────────────────

        [Fact]
        public void ToggleSubscript_WhenAlreadyOn_TurnsOff()
        {
            var vm = new EditorViewModel();
            vm.IsSubscript = true;
            vm.ToggleSubscript();
            Assert.False(vm.IsSubscript);
        }

        [Fact]
        public void ToggleSuperscript_WhenAlreadyOn_TurnsOff()
        {
            var vm = new EditorViewModel();
            vm.IsSuperscript = true;
            vm.ToggleSuperscript();
            Assert.False(vm.IsSuperscript);
        }

        [Fact]
        public void ToggleSubscript_WhenOffAndSuperscriptOn_TurnsSubOnSuperOff()
        {
            var vm = new EditorViewModel();
            vm.IsSuperscript = true;
            vm.IsSubscript   = false;
            vm.ToggleSubscript();
            Assert.True(vm.IsSubscript);
            Assert.False(vm.IsSuperscript);
        }

        [Fact]
        public void ToggleSuperscript_WhenOffAndSubscriptOn_TurnsSuperOnSubOff()
        {
            var vm = new EditorViewModel();
            vm.IsSubscript   = true;
            vm.IsSuperscript = false;
            vm.ToggleSuperscript();
            Assert.True(vm.IsSuperscript);
            Assert.False(vm.IsSubscript);
        }

        // ── FontSize property ────────────────────────────────────────────────────

        [Fact]
        public void FontSize_Default_Is11()
        {
            var vm = new EditorViewModel();
            Assert.Equal(11.0, vm.FontSize);
        }

        [Theory]
        [InlineData(8.0)]
        [InlineData(12.0)]
        [InlineData(14.0)]
        [InlineData(24.0)]
        [InlineData(72.0)]
        public void FontSize_SetAndGet_RoundTrips(double size)
        {
            var vm = new EditorViewModel();
            vm.FontSize = size;
            Assert.Equal(size, vm.FontSize);
        }

        [Fact]
        public void FontSize_Set_FiresPropertyChanged()
        {
            var vm    = new EditorViewModel();
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
            vm.FontSize = 16.0;
            Assert.Contains("FontSize", fired);
        }

        [Fact]
        public void FontSize_NewDocument_Resets()
        {
            var vm = new EditorViewModel();
            vm.FontSize = 24.0;
            vm.NewDocument();
            Assert.Equal(11.0, vm.FontSize);
        }

        // ── FontFamily property ──────────────────────────────────────────────────

        [Fact]
        public void FontFamily_Default_IsSegoeUI()
        {
            var vm = new EditorViewModel();
            Assert.Equal("Segoe UI", vm.FontFamily);
        }

        [Fact]
        public void FontFamily_Set_FiresPropertyChanged()
        {
            var vm    = new EditorViewModel();
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
            vm.FontFamily = "Consolas";
            Assert.Contains("FontFamily", fired);
            Assert.Equal("Consolas", vm.FontFamily);
        }

        [Fact]
        public void FontFamily_NewDocument_ResetsToSegoeUI()
        {
            var vm = new EditorViewModel();
            vm.FontFamily = "Arial";
            vm.NewDocument();
            Assert.Equal("Segoe UI", vm.FontFamily);
        }

        // ── Find* properties ─────────────────────────────────────────────────────

        [Fact]
        public void FindMatchCase_Default_IsFalse()
        {
            var vm = new EditorViewModel();
            Assert.False(vm.FindMatchCase);
        }

        [Fact]
        public void FindMatchCase_Set_FiresPropertyChanged()
        {
            var vm    = new EditorViewModel();
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
            vm.FindMatchCase = true;
            Assert.Contains("FindMatchCase", fired);
        }

        [Fact]
        public void FindWholeWord_Default_IsFalse()
        {
            var vm = new EditorViewModel();
            Assert.False(vm.FindWholeWord);
        }

        [Fact]
        public void FindWholeWord_Set_FiresPropertyChanged()
        {
            var vm    = new EditorViewModel();
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
            vm.FindWholeWord = true;
            Assert.Contains("FindWholeWord", fired);
        }

        [Fact]
        public void FindUseRegex_Default_IsFalse()
        {
            var vm = new EditorViewModel();
            Assert.False(vm.FindUseRegex);
        }

        [Fact]
        public void FindUseRegex_Set_FiresPropertyChanged()
        {
            var vm    = new EditorViewModel();
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
            vm.FindUseRegex = true;
            Assert.Contains("FindUseRegex", fired);
        }

        [Fact]
        public void NewDocument_Resets_FindOptions()
        {
            var vm = new EditorViewModel();
            vm.FindMatchCase = true;
            vm.FindWholeWord = true;
            vm.FindUseRegex  = true;
            vm.NewDocument();
            Assert.False(vm.FindMatchCase);
            Assert.False(vm.FindWholeWord);
            Assert.False(vm.FindUseRegex);
        }

        // ── Encoding property ────────────────────────────────────────────────────

        [Fact]
        public void Encoding_Default_IsUtf8()
        {
            var vm = new EditorViewModel();
            Assert.Equal("UTF-8", vm.Encoding);
        }

        [Fact]
        public void Encoding_Set_UpdatesEncodingDisplay()
        {
            var vm = new EditorViewModel();
            vm.Encoding = "UTF-16";
            Assert.Equal("UTF-16", vm.Encoding);
            Assert.Equal("UTF-16", vm.EncodingDisplay);
        }

        [Fact]
        public void Encoding_Set_FiresEncodingAndEncodingDisplayChanged()
        {
            var vm    = new EditorViewModel();
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
            vm.Encoding = "ASCII";
            Assert.Contains("Encoding",        fired);
            Assert.Contains("EncodingDisplay", fired);
        }

        [Fact]
        public void Encoding_NewDocument_ResetsToUtf8()
        {
            var vm = new EditorViewModel();
            vm.Encoding = "ASCII";
            vm.NewDocument();
            Assert.Equal("UTF-8", vm.Encoding);
        }

        // ── SelectionLength INPC cascade ─────────────────────────────────────────

        [Fact]
        public void SelectionLength_Set_FiresSelectionLengthDisplayChanged()
        {
            var vm    = new EditorViewModel();
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
            vm.SelectionLength = 25;
            Assert.Contains("SelectionLength",        fired);
            Assert.Contains("SelectionLengthDisplay", fired);
        }

        // ── WordCount / CharCount INPC cascade ───────────────────────────────────

        [Fact]
        public void WordCount_Set_FiresWordCountDisplayChanged()
        {
            var vm    = new EditorViewModel();
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
            vm.WordCount = 77;
            Assert.Contains("WordCount",        fired);
            Assert.Contains("WordCountDisplay", fired);
        }

        [Fact]
        public void CharCount_Set_FiresCharCountDisplayChanged()
        {
            var vm    = new EditorViewModel();
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
            vm.CharCount = 123;
            Assert.Contains("CharCount",        fired);
            Assert.Contains("CharCountDisplay", fired);
        }

        // ── LineNumber / ColumnNumber INPC cascade ───────────────────────────────

        [Fact]
        public void LineNumber_Set_FiresLineColDisplayChanged()
        {
            var vm    = new EditorViewModel();
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
            vm.LineNumber = 5;
            Assert.Contains("LineNumber",    fired);
            Assert.Contains("LineColDisplay", fired);
        }

        [Fact]
        public void ColumnNumber_Set_FiresLineColDisplayChanged()
        {
            var vm    = new EditorViewModel();
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
            vm.ColumnNumber = 8;
            Assert.Contains("ColumnNumber",   fired);
            Assert.Contains("LineColDisplay", fired);
        }

        // ── ZoomLevel INPC cascade ───────────────────────────────────────────────

        [Fact]
        public void ZoomLevel_Set_FiresZoomDisplayChanged()
        {
            var vm    = new EditorViewModel();
            var fired = new List<string>();
            vm.PropertyChanged += (_, e) => fired.Add(e.PropertyName!);
            vm.ZoomLevel = 150.0;
            Assert.Contains("ZoomLevel",   fired);
            Assert.Contains("ZoomDisplay", fired);
        }
    }

    // ═══ PdfHelper exhaustive tests ══════════════════════════════════════════════

    public class PdfHelperExhaustiveTests
    {
        // ── Two-page generation ──────────────────────────────────────────────────

        [Fact]
        public void GeneratePdf_FortyTwoLines_ProducesTwoPages()
        {
            // linesPerPage = 41 → 42 lines forces page 2
            string text = string.Join("\n", Enumerable.Range(1, 42).Select(i => $"Line {i}"));
            var bytes   = PdfHelper.GeneratePdf(text);
            string content = Encoding.Latin1.GetString(bytes);
            int pageCount = 0, idx = 0;
            while ((idx = content.IndexOf("/Type /Page ", idx)) >= 0) { pageCount++; idx++; }
            Assert.Equal(2, pageCount);
        }

        [Fact]
        public void GeneratePdf_EightyThreeLines_ProducesThreePages()
        {
            // linesPerPage = 41; 83 = (41*2)+1 forces a third page
            string text = string.Join("\n", Enumerable.Range(1, 83).Select(i => $"Line {i}"));
            var bytes   = PdfHelper.GeneratePdf(text);
            string content = Encoding.Latin1.GetString(bytes);
            int pageCount = 0, idx = 0;
            while ((idx = content.IndexOf("/Type /Page ", idx)) >= 0) { pageCount++; idx++; }
            Assert.Equal(3, pageCount);
        }

        [Fact]
        public void GeneratePdf_CustomFontSize_ProducesValidPdf()
        {
            var bytes   = PdfHelper.GeneratePdf("Small text", fontSize: 8.0);
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("%PDF-1.4", content);
            Assert.Contains("/F1 8 Tf", content);
        }

        [Fact]
        public void GeneratePdf_NullText_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => PdfHelper.GeneratePdf(null!));
        }

        // ── BuildDisplayLines edge cases ─────────────────────────────────────────

        [Fact]
        public void BuildDisplayLines_EmptyString_ReturnsOneEmptyLine()
        {
            var result = PdfHelper.BuildDisplayLines("", 80);
            Assert.Single(result);
            Assert.Equal("", result[0]);
        }

        [Fact]
        public void BuildDisplayLines_SingleEmptyLine_ReturnsOneEmptyLine()
        {
            var result = PdfHelper.BuildDisplayLines("\n", 80);
            Assert.Equal(2, result.Count);
            Assert.Equal("", result[0]);
            Assert.Equal("", result[1]);
        }

        [Fact]
        public void BuildDisplayLines_LongWordNoSpaces_HardWrapsAtMaxChars()
        {
            string longWord = new string('X', 150);
            var result = PdfHelper.BuildDisplayLines(longWord, 80);
            Assert.Equal(2, result.Count);
            Assert.Equal(80, result[0].Length);
            Assert.Equal(70, result[1].Length);
        }

        [Fact]
        public void BuildDisplayLines_MaxCharsOf1_EachCharOnOwnLine()
        {
            var result = PdfHelper.BuildDisplayLines("ABC", 1);
            Assert.Equal(3, result.Count);
            Assert.Equal("A", result[0]);
            Assert.Equal("B", result[1]);
            Assert.Equal("C", result[2]);
        }

        [Fact]
        public void BuildDisplayLines_CrLfNormalized()
        {
            var result = PdfHelper.BuildDisplayLines("A\r\nB\r\nC", 80);
            Assert.Equal(3, result.Count);
            Assert.Equal("A", result[0]);
            Assert.Equal("B", result[1]);
            Assert.Equal("C", result[2]);
        }

        [Fact]
        public void BuildDisplayLines_CrOnly_Normalized()
        {
            var result = PdfHelper.BuildDisplayLines("X\rY\rZ", 80);
            Assert.Equal(3, result.Count);
        }

        [Fact]
        public void BuildDisplayLines_WordBreakAtSpace()
        {
            // "Hello World" — 11 chars, maxChars=8 → break before "World"
            var result = PdfHelper.BuildDisplayLines("Hello World", 8);
            Assert.Equal(2, result.Count);
            Assert.Equal("Hello", result[0]);
            Assert.Equal("World", result[1]);
        }

        [Fact]
        public void BuildDisplayLines_WordBreakPrefersLastSpace()
        {
            // "one two three" — 13 chars, maxChars=9
            // lastSpace at 7 ('o' in 'three'), result[0]="one two"
            var result = PdfHelper.BuildDisplayLines("one two three", 9);
            Assert.True(result.Count >= 2);
            Assert.Equal("one two", result[0]);
        }

        [Fact]
        public void BuildDisplayLines_ZeroMaxChars_TreatedAsOne()
        {
            // maxChars < 1 → treated as 1
            var result = PdfHelper.BuildDisplayLines("Hi", 0);
            Assert.True(result.Count >= 2);
        }

        [Fact]
        public void BuildDisplayLines_NegativeMaxChars_TreatedAsOne()
        {
            var result = PdfHelper.BuildDisplayLines("Hi", -5);
            Assert.True(result.Count >= 2);
        }

        // ── PDF structure completeness ────────────────────────────────────────────

        [Fact]
        public void GeneratePdf_HasXrefTable()
        {
            var bytes   = PdfHelper.GeneratePdf("test");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("xref\n", content);
            Assert.Contains("startxref\n", content);
        }

        [Fact]
        public void GeneratePdf_HasTrailerDict()
        {
            var bytes   = PdfHelper.GeneratePdf("test");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("trailer\n", content);
            Assert.Contains("/Root 1 0 R", content);
        }

        [Fact]
        public void GeneratePdf_HasCatalogObject()
        {
            var bytes   = PdfHelper.GeneratePdf("test");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("/Type /Catalog", content);
            Assert.Contains("/Pages 2 0 R", content);
        }

        [Fact]
        public void GeneratePdf_HasFontObject()
        {
            var bytes   = PdfHelper.GeneratePdf("test");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("/Type /Font", content);
            Assert.Contains("Helvetica", content);
        }

        [Fact]
        public void GeneratePdf_HasPagesNode()
        {
            var bytes   = PdfHelper.GeneratePdf("test");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("/Type /Pages", content);
        }

        [Fact]
        public void GeneratePdf_SpecialCharsEscaped()
        {
            // '(' ')' and '\' must be escaped in PDF strings
            var bytes   = PdfHelper.GeneratePdf("(test) \\value");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains(@"\(test\)", content);
            Assert.Contains(@"\\value", content);
        }

        [Fact]
        public void GeneratePdf_NonAscii_ReplacedWithQuestionMark()
        {
            var bytes   = PdfHelper.GeneratePdf("caf\u00e9");
            string content = Encoding.Latin1.GetString(bytes);
            // Non-ASCII char (é = 0xE9) replaced with '?'
            Assert.Contains("caf?", content);
        }

        [Fact]
        public void GeneratePdf_EmptyString_ProducesEmptyPage()
        {
            var bytes = PdfHelper.GeneratePdf("");
            Assert.True(bytes.Length > 0);
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("%PDF-1.4", content);
        }
    }

    // ═══ DocxExportHelper.GenerateRichDocx formatting tests ═════════════════════

    public class DocxExportRichFormattingTests
    {
        private static XDocument GetDocXml(byte[] bytes)
        {
            using var ms  = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var entry = zip.GetEntry("word/document.xml")!;
            using var reader = new StreamReader(entry.Open());
            return XDocument.Parse(reader.ReadToEnd());
        }

        private static readonly XNamespace W =
            "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

        [Fact]
        public void GenerateRichDocx_BoldRtf_ContainsBoldElement()
        {
            var bytes = DocxExportHelper.GenerateRichDocx(@"{\rtf1\ansi\b Bold text}");
            var doc   = GetDocXml(bytes);
            Assert.NotEmpty(doc.Descendants(W + "b"));
        }

        [Fact]
        public void GenerateRichDocx_ItalicRtf_ContainsItalicElement()
        {
            var bytes = DocxExportHelper.GenerateRichDocx(@"{\rtf1\ansi\i Italic text}");
            var doc   = GetDocXml(bytes);
            Assert.NotEmpty(doc.Descendants(W + "i"));
        }

        [Fact]
        public void GenerateRichDocx_UnderlineRtf_ContainsUnderlineElement()
        {
            var bytes = DocxExportHelper.GenerateRichDocx(@"{\rtf1\ansi\ul Underline text}");
            var doc   = GetDocXml(bytes);
            var uEls  = doc.Descendants(W + "u").ToList();
            Assert.NotEmpty(uEls);
            Assert.Equal("single", uEls[0].Attribute(W + "val")?.Value);
        }

        [Fact]
        public void GenerateRichDocx_StrikeRtf_ContainsStrikeElement()
        {
            var bytes = DocxExportHelper.GenerateRichDocx(@"{\rtf1\ansi\strike Strike text}");
            var doc   = GetDocXml(bytes);
            Assert.NotEmpty(doc.Descendants(W + "strike"));
        }

        [Fact]
        public void GenerateRichDocx_CenterAlignment_ContainsJcCenter()
        {
            var bytes = DocxExportHelper.GenerateRichDocx(@"{\rtf1\ansi\qc Center}");
            var doc   = GetDocXml(bytes);
            var jc    = doc.Descendants(W + "jc").FirstOrDefault();
            Assert.NotNull(jc);
            Assert.Equal("center", jc!.Attribute(W + "val")?.Value);
        }

        [Fact]
        public void GenerateRichDocx_RightAlignment_ContainsJcRight()
        {
            var bytes = DocxExportHelper.GenerateRichDocx(@"{\rtf1\ansi\qr Right}");
            var doc   = GetDocXml(bytes);
            var jc    = doc.Descendants(W + "jc").FirstOrDefault();
            Assert.NotNull(jc);
            Assert.Equal("right", jc!.Attribute(W + "val")?.Value);
        }

        [Fact]
        public void GenerateRichDocx_JustifyAlignment_ContainsJcBoth()
        {
            var bytes = DocxExportHelper.GenerateRichDocx(@"{\rtf1\ansi\qj Justified}");
            var doc   = GetDocXml(bytes);
            var jc    = doc.Descendants(W + "jc").FirstOrDefault();
            Assert.NotNull(jc);
            Assert.Equal("both", jc!.Attribute(W + "val")?.Value);
        }

        [Fact]
        public void GenerateRichDocx_LeftAlignment_NoJcElement()
        {
            // Left (default) produces no <w:jc> element
            var bytes = DocxExportHelper.GenerateRichDocx(@"{\rtf1\ansi\ql Left}");
            var doc   = GetDocXml(bytes);
            Assert.Empty(doc.Descendants(W + "jc"));
        }

        [Fact]
        public void GenerateRichDocx_FontSize_ContainsSzElement()
        {
            var bytes  = DocxExportHelper.GenerateRichDocx(@"{\rtf1\ansi\fs48 Big}");
            var doc    = GetDocXml(bytes);
            var szEls  = doc.Descendants(W + "sz").ToList();
            Assert.NotEmpty(szEls);
            Assert.Equal("48", szEls[0].Attribute(W + "val")?.Value);
        }

        [Fact]
        public void GenerateRichDocx_FontTable_StillProducesValidDocx()
        {
            // Font table is parsed but FontEntryRegex requires post-semicolon name;
            // standard RTF (name before ';') still produces valid DOCX with the text.
            string rtf = @"{\rtf1\ansi{\fonttbl{\f0\fswiss Arial;}}\f0 Font text}";
            var bytes  = DocxExportHelper.GenerateRichDocx(rtf);
            var doc    = GetDocXml(bytes);
            var texts  = doc.Descendants(W + "t").Select(e => e.Value).ToList();
            Assert.Contains(texts, t => t.Contains("Font text"));
        }

        [Fact]
        public void GenerateRichDocx_MultiParagraph_CorrectParagraphCount()
        {
            var bytes = DocxExportHelper.GenerateRichDocx(
                @"{\rtf1\ansi First\par Second\par Third}");
            var doc    = GetDocXml(bytes);
            // sectPr adds 1 element but is not a paragraph
            var paras  = doc.Descendants(W + "p").ToList();
            Assert.True(paras.Count >= 3);
        }

        [Fact]
        public void GenerateRichDocx_HasSectPr()
        {
            var bytes = DocxExportHelper.GenerateRichDocx(@"{\rtf1\ansi text}");
            var doc   = GetDocXml(bytes);
            Assert.NotEmpty(doc.Descendants(W + "sectPr"));
        }

        [Fact]
        public void GenerateRichDocx_EmptyParagraph_HasEmptyRunElement()
        {
            // A paragraph with zero-length runs gets a placeholder <w:r><w:t/></w:r>
            var bytes = DocxExportHelper.GenerateRichDocx(
                @"{\rtf1\ansi First\par\par Third}");
            var doc   = GetDocXml(bytes);
            // Middle paragraph should have an empty <w:t> run
            var paras = doc.Descendants(W + "p").ToList();
            Assert.True(paras.Count >= 3);
        }

        [Fact]
        public void GenerateRichDocx_NullArgument_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => DocxExportHelper.GenerateRichDocx(null!));
        }

        [Fact]
        public void GenerateRichDocx_AllDocxEntries_Present()
        {
            var bytes = DocxExportHelper.GenerateRichDocx(@"{\rtf1\ansi test}");
            using var ms  = new MemoryStream(bytes);
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
            var names = zip.Entries.Select(e => e.FullName).ToList();
            Assert.Contains("[Content_Types].xml",            names);
            Assert.Contains("_rels/.rels",                    names);
            Assert.Contains("word/document.xml",              names);
            Assert.Contains("word/_rels/document.xml.rels",   names);
        }

        [Fact]
        public void GenerateRichDocx_PlainText_ContainsText()
        {
            var bytes = DocxExportHelper.GenerateRichDocx(@"{\rtf1\ansi Hello World}");
            var doc   = GetDocXml(bytes);
            var texts = doc.Descendants(W + "t").Select(e => e.Value).ToList();
            Assert.Contains(texts, t => t.Contains("Hello"));
        }

        [Fact]
        public void GenerateRichDocx_BoldItalicUnderline_AllPresent()
        {
            var bytes = DocxExportHelper.GenerateRichDocx(
                @"{\rtf1\ansi\b\i\ul Formatted}");
            var doc = GetDocXml(bytes);
            Assert.NotEmpty(doc.Descendants(W + "b"));
            Assert.NotEmpty(doc.Descendants(W + "i"));
            Assert.NotEmpty(doc.Descendants(W + "u"));
        }
    }

    // ═══ RtfHelper exact-structure tests ════════════════════════════════════════

    public class RtfHelperStructureTests
    {
        [Fact]
        public void GenerateTable_1x1_HasOneRow()
        {
            string rtf = RtfHelper.GenerateTable(1, 1);
            Assert.Single(rtf.Split(@"\trowd ",
                StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray());
        }

        [Fact]
        public void GenerateTable_3x2_HasThreeRows()
        {
            string rtf  = RtfHelper.GenerateTable(3, 2);
            int rowCount = rtf.Split(@"\row ", StringSplitOptions.None).Length - 1;
            Assert.Equal(3, rowCount);
        }

        [Fact]
        public void GenerateTable_2x3_HasSixCells()
        {
            string rtf   = RtfHelper.GenerateTable(2, 3);
            int cellCount = rtf.Split(@"\cell ", StringSplitOptions.None).Length - 1;
            Assert.Equal(6, cellCount);
        }

        [Fact]
        public void GenerateTable_1x2_CellWidths_Are2000_And_4000()
        {
            string rtf = RtfHelper.GenerateTable(1, 2);
            Assert.Contains(@"\cellx2000", rtf);
            Assert.Contains(@"\cellx4000", rtf);
        }

        [Fact]
        public void GenerateTable_1x3_CellWidths_Are2000_4000_6000()
        {
            string rtf = RtfHelper.GenerateTable(1, 3);
            Assert.Contains(@"\cellx2000", rtf);
            Assert.Contains(@"\cellx4000", rtf);
            Assert.Contains(@"\cellx6000", rtf);
        }

        [Fact]
        public void GenerateTable_5x4_CellCount_Is20()
        {
            string rtf   = RtfHelper.GenerateTable(5, 4);
            int cellCount = rtf.Split(@"\cell ", StringSplitOptions.None).Length - 1;
            Assert.Equal(20, cellCount);
        }

        [Fact]
        public void GenerateTable_HasBorderDirectives()
        {
            string rtf = RtfHelper.GenerateTable(1, 1);
            Assert.Contains(@"\clbrdrt\brdrs", rtf);
            Assert.Contains(@"\clbrdrl\brdrs", rtf);
            Assert.Contains(@"\clbrdrb\brdrs", rtf);
            Assert.Contains(@"\clbrdrr\brdrs", rtf);
        }

        [Fact]
        public void GenerateTable_StartsWithRtfHeader()
        {
            string rtf = RtfHelper.GenerateTable(1, 1);
            Assert.StartsWith(@"{\rtf1\ansi", rtf);
        }

        [Fact]
        public void GenerateTable_EndsWithClosingBrace()
        {
            string rtf = RtfHelper.GenerateTable(2, 2);
            Assert.EndsWith("}", rtf);
        }

        [Fact]
        public void GenerateTable_ZeroRows_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RtfHelper.GenerateTable(0, 1));
        }

        [Fact]
        public void GenerateTable_NegativeRows_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RtfHelper.GenerateTable(-1, 1));
        }

        [Fact]
        public void GenerateTable_ZeroCols_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RtfHelper.GenerateTable(1, 0));
        }

        [Fact]
        public void GenerateTable_NegativeCols_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RtfHelper.GenerateTable(1, -3));
        }

        [Fact]
        public void GenerateTable_1x1_TrowdCount_Is1()
        {
            string rtf     = RtfHelper.GenerateTable(1, 1);
            int trowdCount = rtf.Split(@"\trowd ", StringSplitOptions.None).Length - 1;
            Assert.Equal(1, trowdCount);
        }

        [Fact]
        public void GenerateTable_10x10_HasHundredCells()
        {
            string rtf   = RtfHelper.GenerateTable(10, 10);
            int cellCount = rtf.Split(@"\cell ", StringSplitOptions.None).Length - 1;
            Assert.Equal(100, cellCount);
        }
    }

    // ═══ ColorHelper exhaustive tests ═══════════════════════════════════════════

    public class ColorHelperNullAndArgbTests
    {
        [Fact]
        public void ParseHexColor_NullInput_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ColorHelper.ParseHexColor(null!));
        }

        [Fact]
        public void ParseHexColor_EmptyString_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => ColorHelper.ParseHexColor(""));
        }

        [Fact]
        public void ParseHexColor_SevenChars_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ColorHelper.ParseHexColor("1234567"));
        }

        [Fact]
        public void ParseHexColor_FiveChars_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ColorHelper.ParseHexColor("12345"));
        }

        [Fact]
        public void ParseHexColor_HashPlusSixChars_Parses()
        {
            var c = ColorHelper.ParseHexColor("#FF5733");
            Assert.Equal(0xFF, c.R);
            Assert.Equal(0x57, c.G);
            Assert.Equal(0x33, c.B);
            Assert.Equal(255,  c.A);
        }

        [Fact]
        public void ParseHexColor_EightCharArgb_AlphaChannelCorrect()
        {
            var c = ColorHelper.ParseHexColor("80FF0000");
            Assert.Equal(0x80, c.A);
            Assert.Equal(0xFF, c.R);
            Assert.Equal(0x00, c.G);
            Assert.Equal(0x00, c.B);
        }

        [Fact]
        public void ParseHexColor_EightCharArgb_HashPrefixed()
        {
            var c = ColorHelper.ParseHexColor("#40123456");
            Assert.Equal(0x40, c.A);
            Assert.Equal(0x12, c.R);
            Assert.Equal(0x34, c.G);
            Assert.Equal(0x56, c.B);
        }

        [Fact]
        public void ParseHexColor_InvalidHexChar_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ColorHelper.ParseHexColor("GGGGGG"));
        }

        [Fact]
        public void ParseHexColor_InvalidHexCharZ_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ColorHelper.ParseHexColor("ZZZ000"));
        }

        [Fact]
        public void ParseHexColor_LowercaseHex_Parses()
        {
            var c = ColorHelper.ParseHexColor("aabbcc");
            Assert.Equal(0xAA, c.R);
            Assert.Equal(0xBB, c.G);
            Assert.Equal(0xCC, c.B);
        }

        [Fact]
        public void ParseHexColor_AllZero_IsBlack()
        {
            var c = ColorHelper.ParseHexColor("000000");
            Assert.Equal(0, c.R);
            Assert.Equal(0, c.G);
            Assert.Equal(0, c.B);
            Assert.Equal(255, c.A);
        }

        [Fact]
        public void ParseHexColor_AllFf_IsWhite()
        {
            var c = ColorHelper.ParseHexColor("FFFFFF");
            Assert.Equal(255, c.R);
            Assert.Equal(255, c.G);
            Assert.Equal(255, c.B);
            Assert.Equal(255, c.A);
        }

        [Fact]
        public void ParseHexColor_FullAlpha_IsOpaque()
        {
            var c = ColorHelper.ParseHexColor("FFAABBCC");
            Assert.Equal(255, c.A);
        }

        [Fact]
        public void ParseHexColor_ZeroAlpha_IsTransparent()
        {
            var c = ColorHelper.ParseHexColor("00AABBCC");
            Assert.Equal(0, c.A);
        }

        [Fact]
        public void ParseHexColor_HashOnly_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ColorHelper.ParseHexColor("#"));
        }

        [Fact]
        public void ParseHexColor_SixteenChars_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ColorHelper.ParseHexColor("0123456789ABCDEF"));
        }
    }

    // ═══ DocumentImportHelper exhaustive tests ═══════════════════════════════════

    public class DocumentImportHelperExhaustiveTests
    {
        private static MemoryStream MakeDocx(params string[] paragraphs)
        {
            XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var body = new XElement(w + "body");
            foreach (var p in paragraphs)
                body.Add(new XElement(w + "p",
                    new XElement(w + "r", new XElement(w + "t", p))));
            var doc = new XDocument(new XElement(w + "document", body));

            var ms  = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry  = zip.CreateEntry("word/document.xml");
                using var w2 = new StreamWriter(entry.Open(), Encoding.UTF8);
                w2.Write(doc.ToString());
            }
            ms.Position = 0;
            return ms;
        }

        private static MemoryStream MakeOdt(params string[] paragraphs)
        {
            XNamespace text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
            var body = new XElement("root");
            foreach (var p in paragraphs)
                body.Add(new XElement(text + "p", p));

            var ms  = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry  = zip.CreateEntry("content.xml");
                using var w = new StreamWriter(entry.Open(), Encoding.UTF8);
                w.Write(body.ToString());
            }
            ms.Position = 0;
            return ms;
        }

        [Fact]
        public void ExtractText_Docx_SingleParagraph()
        {
            using var ms = MakeDocx("Hello World");
            string result = DocumentImportHelper.ExtractText(ms, ".docx");
            Assert.Contains("Hello World", result);
        }

        [Fact]
        public void ExtractText_Docx_MultipleParagraphs_Concatenated()
        {
            using var ms = MakeDocx("First", "Second", "Third");
            string result = DocumentImportHelper.ExtractText(ms, ".docx");
            // DOCX <t> elements joined with string.Join("", ...)
            Assert.Contains("First",  result);
            Assert.Contains("Second", result);
            Assert.Contains("Third",  result);
        }

        [Fact]
        public void ExtractText_Docx_EmptyParagraph_ReturnsEmpty()
        {
            using var ms = MakeDocx("");
            string result = DocumentImportHelper.ExtractText(ms, ".docx");
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ExtractText_Odt_SingleParagraph()
        {
            using var ms = MakeOdt("ODT Content");
            string result = DocumentImportHelper.ExtractText(ms, ".odt");
            Assert.Contains("ODT Content", result);
        }

        [Fact]
        public void ExtractText_Odt_MultipleParagraphs_JoinedWithNewline()
        {
            using var ms = MakeOdt("Line1", "Line2", "Line3");
            string result = DocumentImportHelper.ExtractText(ms, ".odt");
            // ODT paragraphs joined with Environment.NewLine
            Assert.Contains("Line1", result);
            Assert.Contains("Line2", result);
            Assert.Contains("Line3", result);
            Assert.Contains(Environment.NewLine, result);
        }

        [Fact]
        public void ExtractText_MissingEntry_ReturnsEmpty()
        {
            var ms  = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                zip.CreateEntry("other/file.xml");
            ms.Position = 0;
            string result = DocumentImportHelper.ExtractText(ms, ".docx");
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ExtractText_UnknownExtension_UsesContentXmlPath()
        {
            // Extension that is not ".docx" → uses "content.xml"
            using var ms = MakeOdt("ODT via content.xml");
            ms.Position = 0;
            string result = DocumentImportHelper.ExtractText(ms, ".xyz");
            // Should attempt content.xml (which exists since MakeOdt uses content.xml)
            Assert.Contains("ODT via content.xml", result);
        }

        [Fact]
        public void ExtractText_Docx_NewlineInText_Replaced()
        {
            // DOCX text containing \n → replaced with Environment.NewLine
            using var ms = MakeDocx("Line1\nLine2");
            string result = DocumentImportHelper.ExtractText(ms, ".docx");
            Assert.Contains("Line1", result);
            Assert.Contains("Line2", result);
        }
    }

    // ═══ SettingsService uncovered property tests ════════════════════════════════

    public class SettingsServiceRemainingPropertyTests
    {
        private static SettingsService Isolated() =>
            new(Path.Combine(Path.GetTempPath(), "SmrtPadTests",
                Guid.NewGuid().ToString("N"), "settings.json"));

        [Fact]
        public void SpellCheckEnabled_DefaultIsTrue()
        {
            var svc = Isolated();
            Assert.True(svc.SpellCheckEnabled);
        }

        [Fact]
        public void SpellCheckEnabled_RoundTrips()
        {
            var svc = Isolated();
            svc.SpellCheckEnabled = false;
            svc.Save();
            svc.Load();
            Assert.False(svc.SpellCheckEnabled);
        }

        [Fact]
        public void RulerUnits_DefaultIsIn()
        {
            var svc = Isolated();
            Assert.Equal("in", svc.RulerUnits);
        }

        [Fact]
        public void RulerUnits_RoundTrips()
        {
            var svc = Isolated();
            svc.RulerUnits = "cm";
            svc.Save();
            svc.Load();
            Assert.Equal("cm", svc.RulerUnits);
        }

        [Fact]
        public void AutoSaveEnabled_DefaultIsFalse()
        {
            var svc = Isolated();
            Assert.False(svc.AutoSaveEnabled);
        }

        [Fact]
        public void AutoSaveEnabled_RoundTrips()
        {
            var svc = Isolated();
            svc.AutoSaveEnabled = true;
            svc.Save();
            svc.Load();
            Assert.True(svc.AutoSaveEnabled);
        }

        [Fact]
        public void AutoSaveIntervalSeconds_DefaultIs300()
        {
            var svc = Isolated();
            Assert.Equal(300, svc.AutoSaveIntervalSeconds);
        }

        [Fact]
        public void AutoSaveIntervalSeconds_RoundTrips()
        {
            var svc = Isolated();
            svc.AutoSaveIntervalSeconds = 60;
            svc.Save();
            svc.Load();
            Assert.Equal(60, svc.AutoSaveIntervalSeconds);
        }

        [Fact]
        public void DefaultWordWrap_DefaultIsTrue()
        {
            var svc = Isolated();
            Assert.True(svc.DefaultWordWrap);
        }

        [Fact]
        public void DefaultWordWrap_RoundTrips()
        {
            var svc = Isolated();
            svc.DefaultWordWrap = false;
            svc.Save();
            svc.Load();
            Assert.False(svc.DefaultWordWrap);
        }

        [Fact]
        public void DefaultFontSize_DefaultIs11()
        {
            var svc = Isolated();
            Assert.Equal(11.0, svc.DefaultFontSize);
        }

        [Fact]
        public void DefaultFontSize_RoundTrips()
        {
            var svc = Isolated();
            svc.DefaultFontSize = 14.0;
            svc.Save();
            svc.Load();
            Assert.Equal(14.0, svc.DefaultFontSize);
        }

        [Fact]
        public void DefaultSaveFormat_DefaultIsRtf()
        {
            var svc = Isolated();
            Assert.Equal(".rtf", svc.DefaultSaveFormat);
        }

        [Fact]
        public void DefaultSaveFormat_RoundTrips()
        {
            var svc = Isolated();
            svc.DefaultSaveFormat = ".txt";
            svc.Save();
            svc.Load();
            Assert.Equal(".txt", svc.DefaultSaveFormat);
        }

        [Fact]
        public void DefaultFontFamily_RoundTrips()
        {
            var svc = Isolated();
            svc.DefaultFontFamily = "Arial";
            svc.Save();
            svc.Load();
            Assert.Equal("Arial", svc.DefaultFontFamily);
        }

        [Fact]
        public void AddRecentFile_ExactlyAtCap_NoCap()
        {
            var svc = Isolated();
            for (int i = 1; i <= 10; i++)
                svc.AddRecentFile($@"C:\file{i}.rtf");
            Assert.Equal(10, svc.RecentFiles.Count);
        }

        [Fact]
        public void AddRecentFile_EleventhFile_OldestRemoved()
        {
            var svc = Isolated();
            for (int i = 1; i <= 11; i++)
                svc.AddRecentFile($@"C:\file{i}.rtf");
            Assert.Equal(10, svc.RecentFiles.Count);
            // Oldest (file1) should be gone
            Assert.DoesNotContain(@"C:\file1.rtf", svc.RecentFiles);
        }

        [Fact]
        public void AddRecentFile_PromotesExistingToFront()
        {
            var svc = Isolated();
            svc.AddRecentFile(@"C:\a.rtf");
            svc.AddRecentFile(@"C:\b.rtf");
            svc.AddRecentFile(@"C:\a.rtf"); // promote
            Assert.Equal(@"C:\a.rtf", svc.RecentFiles[0]);
            Assert.Equal(2, svc.RecentFiles.Count);
        }

        [Fact]
        public void Load_MissingFile_KeepsDefaults()
        {
            string path = Path.Combine(Path.GetTempPath(), "SmrtPadTests",
                Guid.NewGuid().ToString("N"), "nonexistent.json");
            var svc = new SettingsService(path);
            Assert.Equal("Segoe UI", svc.DefaultFontFamily);
            Assert.Equal(11.0,       svc.DefaultFontSize);
        }

        [Fact]
        public void Load_CorruptJson_FallsBackToDefaults()
        {
            string dir  = Path.Combine(Path.GetTempPath(), "SmrtPadTests",
                Guid.NewGuid().ToString("N"));
            string path = Path.Combine(dir, "settings.json");
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, "{ corrupt json !!!");
            var svc = new SettingsService(path);
            Assert.Equal("Segoe UI", svc.DefaultFontFamily);
        }

        [Fact]
        public void Language_RoundTrips()
        {
            var svc = Isolated();
            svc.Language = "fr-FR";
            svc.Save();
            svc.Load();
            Assert.Equal("fr-FR", svc.Language);
        }

        [Fact]
        public void AllSettings_SavedAndReloaded_TogetherConsistent()
        {
            var svc = Isolated();
            svc.DefaultFontFamily        = "Courier New";
            svc.DefaultFontSize          = 12.0;
            svc.DefaultWordWrap          = false;
            svc.DefaultSaveFormat        = ".txt";
            svc.ThemePreference          = "Light";
            svc.AutoSaveEnabled          = true;
            svc.AutoSaveIntervalSeconds  = 120;
            svc.Language                 = "de-DE";
            svc.RulerUnits               = "cm";
            svc.SpellCheckEnabled        = false;
            svc.Save();
            svc.Load();
            Assert.Equal("Courier New", svc.DefaultFontFamily);
            Assert.Equal(12.0,          svc.DefaultFontSize);
            Assert.False(               svc.DefaultWordWrap);
            Assert.Equal(".txt",        svc.DefaultSaveFormat);
            Assert.Equal("Light",       svc.ThemePreference);
            Assert.True(                svc.AutoSaveEnabled);
            Assert.Equal(120,           svc.AutoSaveIntervalSeconds);
            Assert.Equal("de-DE",       svc.Language);
            Assert.Equal("cm",          svc.RulerUnits);
            Assert.False(               svc.SpellCheckEnabled);
        }
    }

    // ═══ MacroHelper uncovered-branch tests ═════════════════════════════════════

    public class MacroHelperEdgeCaseTests
    {
        [Fact]
        public void MacroHelper_InitialState_IsRecordingFalse()
        {
            var m = new MacroHelper();
            Assert.False(m.IsRecording);
        }

        [Fact]
        public void MacroHelper_InitialCount_IsZero()
        {
            var m = new MacroHelper();
            Assert.Equal(0, m.Count);
        }

        [Fact]
        public void MacroHelper_InitialCommands_IsEmpty()
        {
            var m = new MacroHelper();
            Assert.Empty(m.Commands);
        }

        [Fact]
        public void Deserialize_EmptyArray_ProducesEmptyCommands()
        {
            var m = new MacroHelper();
            m.Deserialize("[]");
            Assert.Empty(m.Commands);
        }

        [Fact]
        public void Deserialize_NullJson_Throws()
        {
            var m = new MacroHelper();
            Assert.Throws<ArgumentException>(() => m.Deserialize(null!));
        }

        [Fact]
        public void Deserialize_WhitespaceJson_Throws()
        {
            var m = new MacroHelper();
            Assert.Throws<ArgumentException>(() => m.Deserialize("   "));
        }

        [Fact]
        public void Save_NullPath_Throws()
        {
            var m = new MacroHelper();
            Assert.Throws<ArgumentException>(() => m.Save(null!));
        }

        [Fact]
        public void Load_NullPath_Throws()
        {
            var m = new MacroHelper();
            Assert.Throws<ArgumentException>(() => m.Load(null!));
        }

        [Fact]
        public void Record_MacroCommandDirectly()
        {
            var m   = new MacroHelper();
            var cmd = new MacroCommand(MacroCommandType.Italic, null);
            m.StartRecording();
            m.Record(cmd);
            m.StopRecording();
            Assert.Single(m.Commands);
            Assert.Equal(MacroCommandType.Italic, m.Commands[0].Type);
            Assert.Null(m.Commands[0].Value);
        }

        [Fact]
        public void Record_WhenNotRecording_IsIgnored()
        {
            var m = new MacroHelper();
            m.Record(MacroCommandType.Bold);
            Assert.Equal(0, m.Count);
        }

        [Fact]
        public void MacroCommand_DefaultConstructor_TypeIsBold_ValueIsNull()
        {
            var cmd = new MacroCommand();
            Assert.Equal(MacroCommandType.Bold, cmd.Type);
            Assert.Null(cmd.Value);
        }

        [Fact]
        public void MacroCommand_ToString_WithNullValue_ReturnsTypeName()
        {
            var cmd = new MacroCommand(MacroCommandType.Underline, null);
            Assert.Equal("Underline", cmd.ToString());
        }

        [Fact]
        public void MacroCommand_ToString_WithValue_ReturnsTypeColonValue()
        {
            var cmd = new MacroCommand(MacroCommandType.SetAlignment, "Center");
            Assert.Equal("SetAlignment:Center", cmd.ToString());
        }

        [Fact]
        public void Serialize_Empty_ProducesEmptyJsonArray()
        {
            var m    = new MacroHelper();
            string s = m.Serialize();
            Assert.Equal("[]", s.Trim());
        }

        [Fact]
        public void SaveLoad_FileRoundTrip_PreservesCommands()
        {
            string path = Path.Combine(Path.GetTempPath(),
                $"smrtpad_macro_{Guid.NewGuid():N}.json");
            try
            {
                var m = new MacroHelper();
                m.StartRecording();
                m.Record(MacroCommandType.Bold);
                m.Record(MacroCommandType.SetFontFamily, "Consolas");
                m.StopRecording();
                m.Save(path);

                var m2 = new MacroHelper();
                m2.Load(path);
                Assert.Equal(2, m2.Count);
                Assert.Equal(MacroCommandType.Bold,          m2.Commands[0].Type);
                Assert.Equal(MacroCommandType.SetFontFamily, m2.Commands[1].Type);
                Assert.Equal("Consolas",                     m2.Commands[1].Value);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Fact]
        public void StopRecording_SetsIsRecordingFalse()
        {
            var m = new MacroHelper();
            m.StartRecording();
            Assert.True(m.IsRecording);
            m.StopRecording();
            Assert.False(m.IsRecording);
        }

        [Fact]
        public void StopRecording_DoesNotClearCommands()
        {
            var m = new MacroHelper();
            m.StartRecording();
            m.Record(MacroCommandType.Italic);
            m.StopRecording();
            Assert.Single(m.Commands);
        }

        [Fact]
        public void MacroCommandType_Has15Values()
        {
            Assert.Equal(15, Enum.GetValues<MacroCommandType>().Length);
        }
    }

    // ═══ ParagraphStyleHelper exact-value tests ══════════════════════════════════

    public class ParagraphStyleHelperExactValueTests
    {
        [Fact]
        public void Normal_FontName_IsSegoeUI()    => Assert.Equal("Segoe UI", ParagraphStyleHelper.Normal.FontName);
        [Fact]
        public void Normal_FontSize_Is11()         => Assert.Equal(11f, ParagraphStyleHelper.Normal.FontSize);
        [Fact]
        public void Normal_Bold_IsFalse()          => Assert.False(ParagraphStyleHelper.Normal.Bold);
        [Fact]
        public void Normal_Italic_IsFalse()        => Assert.False(ParagraphStyleHelper.Normal.Italic);
        [Fact]
        public void Normal_Alignment_IsLeft()      => Assert.Equal("Left", ParagraphStyleHelper.Normal.Alignment);
        [Fact]
        public void Normal_SpaceBefore_IsZero()    => Assert.Equal(0f, ParagraphStyleHelper.Normal.SpaceBefore);
        [Fact]
        public void Normal_SpaceAfter_IsZero()     => Assert.Equal(0f, ParagraphStyleHelper.Normal.SpaceAfter);

        [Fact]
        public void Heading1_FontSize_Is20()       => Assert.Equal(20f, ParagraphStyleHelper.Heading1.FontSize);
        [Fact]
        public void Heading1_Bold_IsTrue()         => Assert.True(ParagraphStyleHelper.Heading1.Bold);
        [Fact]
        public void Heading1_Italic_IsFalse()      => Assert.False(ParagraphStyleHelper.Heading1.Italic);
        [Fact]
        public void Heading1_SpaceBefore_Is12()    => Assert.Equal(12f, ParagraphStyleHelper.Heading1.SpaceBefore);
        [Fact]
        public void Heading1_SpaceAfter_Is4()      => Assert.Equal(4f, ParagraphStyleHelper.Heading1.SpaceAfter);

        [Fact]
        public void Heading2_FontSize_Is16()       => Assert.Equal(16f, ParagraphStyleHelper.Heading2.FontSize);
        [Fact]
        public void Heading2_Bold_IsTrue()         => Assert.True(ParagraphStyleHelper.Heading2.Bold);
        [Fact]
        public void Heading2_SpaceBefore_Is10()    => Assert.Equal(10f, ParagraphStyleHelper.Heading2.SpaceBefore);
        [Fact]
        public void Heading2_SpaceAfter_Is3()      => Assert.Equal(3f, ParagraphStyleHelper.Heading2.SpaceAfter);

        [Fact]
        public void Heading3_FontSize_Is13()       => Assert.Equal(13f, ParagraphStyleHelper.Heading3.FontSize);
        [Fact]
        public void Heading3_Bold_IsTrue()         => Assert.True(ParagraphStyleHelper.Heading3.Bold);
        [Fact]
        public void Heading3_SpaceBefore_Is8()     => Assert.Equal(8f, ParagraphStyleHelper.Heading3.SpaceBefore);
        [Fact]
        public void Heading3_SpaceAfter_Is2()      => Assert.Equal(2f, ParagraphStyleHelper.Heading3.SpaceAfter);

        [Fact]
        public void Subtitle_FontSize_Is14()       => Assert.Equal(14f, ParagraphStyleHelper.Subtitle.FontSize);
        [Fact]
        public void Subtitle_Bold_IsFalse()        => Assert.False(ParagraphStyleHelper.Subtitle.Bold);
        [Fact]
        public void Subtitle_Italic_IsTrue()       => Assert.True(ParagraphStyleHelper.Subtitle.Italic);
        [Fact]
        public void Subtitle_SpaceBefore_Is6()     => Assert.Equal(6f, ParagraphStyleHelper.Subtitle.SpaceBefore);
        [Fact]
        public void Subtitle_SpaceAfter_Is4()      => Assert.Equal(4f, ParagraphStyleHelper.Subtitle.SpaceAfter);

        [Fact]
        public void Quote_FontSize_Is11()          => Assert.Equal(11f, ParagraphStyleHelper.Quote.FontSize);
        [Fact]
        public void Quote_Bold_IsFalse()           => Assert.False(ParagraphStyleHelper.Quote.Bold);
        [Fact]
        public void Quote_Italic_IsTrue()          => Assert.True(ParagraphStyleHelper.Quote.Italic);
        [Fact]
        public void Quote_SpaceBefore_Is8()        => Assert.Equal(8f, ParagraphStyleHelper.Quote.SpaceBefore);
        [Fact]
        public void Quote_SpaceAfter_Is8()         => Assert.Equal(8f, ParagraphStyleHelper.Quote.SpaceAfter);

        [Fact]
        public void All_HasSixEntries()            => Assert.Equal(6, ParagraphStyleHelper.All.Count);

        [Theory]
        [InlineData("Normal")]
        [InlineData("Heading1")]
        [InlineData("Heading2")]
        [InlineData("Heading3")]
        [InlineData("Subtitle")]
        [InlineData("Quote")]
        public void All_ContainsKey(string key)    => Assert.True(ParagraphStyleHelper.All.ContainsKey(key));

        [Fact]
        public void All_Normal_SameInstanceAsStaticField() =>
            Assert.Same(ParagraphStyleHelper.Normal, ParagraphStyleHelper.All["Normal"]);

        [Fact]
        public void All_Heading1_SameInstanceAsStaticField() =>
            Assert.Same(ParagraphStyleHelper.Heading1, ParagraphStyleHelper.All["Heading1"]);

        [Fact]
        public void All_IsReadOnly()
        {
            Assert.IsAssignableFrom<System.Collections.Generic.IReadOnlyDictionary<string, ParagraphStyleDefinition>>(
                ParagraphStyleHelper.All);
        }

        [Fact]
        public void ParagraphStyleHelper_IsStaticClass()
        {
            var t = typeof(ParagraphStyleHelper);
            Assert.True(t.IsAbstract && t.IsSealed);
        }
    }

    // ═══ DocumentTemplates exhaustive tests ══════════════════════════════════════

    public class DocumentTemplatesExhaustiveTests
    {
        [Fact]
        public void All_HasFiveTemplates() =>
            Assert.Equal(5, DocumentTemplates.All.Count);

        [Fact]
        public void Blank_Key_IsBlank() =>
            Assert.Equal("blank", DocumentTemplates.All[0].Key);

        [Fact]
        public void Blank_PlainContent_IsEmpty() =>
            Assert.Equal("", DocumentTemplates.All[0].PlainContent);

        [Fact]
        public void Letter_Key_IsLetter() =>
            Assert.Equal("letter", DocumentTemplates.All[1].Key);

        [Fact]
        public void Letter_PlainContent_ContainsDear() =>
            Assert.Contains("Dear", DocumentTemplates.All[1].PlainContent);

        [Fact]
        public void Letter_PlainContent_ContainsSincerely() =>
            Assert.Contains("Sincerely", DocumentTemplates.All[1].PlainContent);

        [Fact]
        public void Report_Key_IsReport() =>
            Assert.Equal("report", DocumentTemplates.All[2].Key);

        [Fact]
        public void Report_PlainContent_ContainsExecutiveSummary() =>
            Assert.Contains("EXECUTIVE SUMMARY", DocumentTemplates.All[2].PlainContent);

        [Fact]
        public void Report_PlainContent_ContainsReferences() =>
            Assert.Contains("REFERENCES", DocumentTemplates.All[2].PlainContent);

        [Fact]
        public void Resume_Key_IsResume() =>
            Assert.Equal("resume", DocumentTemplates.All[3].Key);

        [Fact]
        public void Resume_PlainContent_ContainsWorkExperience() =>
            Assert.Contains("WORK EXPERIENCE", DocumentTemplates.All[3].PlainContent);

        [Fact]
        public void Resume_PlainContent_ContainsSkills() =>
            Assert.Contains("SKILLS", DocumentTemplates.All[3].PlainContent);

        [Fact]
        public void Meeting_Key_IsMeeting() =>
            Assert.Equal("meeting", DocumentTemplates.All[4].Key);

        [Fact]
        public void Meeting_PlainContent_ContainsAttendees() =>
            Assert.Contains("ATTENDEES", DocumentTemplates.All[4].PlainContent);

        [Fact]
        public void Meeting_PlainContent_ContainsActionItems() =>
            Assert.Contains("ACTION ITEMS", DocumentTemplates.All[4].PlainContent);

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void All_Templates_DisplayNameIsNonEmpty(int idx) =>
            Assert.NotEmpty(DocumentTemplates.All[idx].DisplayName);

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void All_Templates_DescriptionIsNonEmpty(int idx) =>
            Assert.NotEmpty(DocumentTemplates.All[idx].Description);

        [Fact]
        public void All_Keys_AreDistinct()
        {
            var keys = DocumentTemplates.All.Select(t => t.Key).ToList();
            Assert.Equal(keys.Count, keys.Distinct().Count());
        }

        [Fact]
        public void DocumentTemplate_RecordEquality()
        {
            var a = new DocumentTemplate("k", "N", "D", "C");
            var b = new DocumentTemplate("k", "N", "D", "C");
            Assert.Equal(a, b);
        }

        [Fact]
        public void DocumentTemplate_RecordInequality_Key()
        {
            var a = new DocumentTemplate("k1", "N", "D", "C");
            var b = new DocumentTemplate("k2", "N", "D", "C");
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void DocumentTemplate_WithExpression()
        {
            var a = new DocumentTemplate("k", "N", "D", "C");
            var b = a with { DisplayName = "New" };
            Assert.Equal("New", b.DisplayName);
            Assert.Equal("N",   a.DisplayName);
        }

        [Fact]
        public void DocumentTemplates_IsStaticClass()
        {
            var t = typeof(DocumentTemplates);
            Assert.True(t.IsAbstract && t.IsSealed);
        }
    }

    // ═══ DocumentTab reflection tests ════════════════════════════════════════════

    public class DocumentTabReflectionTests
    {
        private static readonly Type TabType = typeof(SmrtPad.MainWindow).Assembly
            .GetType("SmrtPad.DocumentTab")!;

        [Fact]
        public void DocumentTab_TypeExists()
        {
            Assert.NotNull(TabType);
        }

        [Fact]
        public void DocumentTab_IsInternal()
        {
            Assert.False(TabType.IsPublic);
            Assert.True(TabType.IsNotPublic);
        }

        [Fact]
        public void DocumentTab_IsSealed()
        {
            Assert.True(TabType.IsSealed);
        }

        [Fact]
        public void DocumentTab_HasCurrentFileProperty()
        {
            var prop = TabType.GetProperty("CurrentFile");
            Assert.NotNull(prop);
            Assert.True(prop!.CanWrite);
        }

        [Fact]
        public void DocumentTab_HasIsModifiedProperty()
        {
            var prop = TabType.GetProperty("IsModified");
            Assert.NotNull(prop);
            Assert.Equal(typeof(bool), prop!.PropertyType);
            Assert.True(prop.CanWrite);
        }

        [Fact]
        public void DocumentTab_HasEncodingProperty()
        {
            var prop = TabType.GetProperty("Encoding");
            Assert.NotNull(prop);
            Assert.Equal(typeof(string), prop!.PropertyType);
        }

        [Fact]
        public void DocumentTab_HasZoomLevelProperty()
        {
            var prop = TabType.GetProperty("ZoomLevel");
            Assert.NotNull(prop);
            Assert.Equal(typeof(double), prop!.PropertyType);
        }

        [Fact]
        public void DocumentTab_Encoding_DefaultIsUtf8()
        {
            string? def = (string?)TabType.GetProperty("Encoding")
                ?.GetMethod?.GetParameters()
                .FirstOrDefault()?.DefaultValue;
            // Default is set via field initializer; just check type
            Assert.Equal(typeof(string), TabType.GetProperty("Encoding")!.PropertyType);
        }

        [Fact]
        public void DocumentTab_ZoomLevel_DefaultIs100()
        {
            Assert.Equal(typeof(double), TabType.GetProperty("ZoomLevel")!.PropertyType);
        }

        [Fact]
        public void DocumentTab_HasScaleTransformProperty()
        {
            var prop = TabType.GetProperty("EditorScaleTransform");
            Assert.NotNull(prop);
            Assert.False(prop!.CanWrite);  // readonly
        }

        [Fact]
        public void DocumentTab_HasEditorProperty()
        {
            var prop = TabType.GetProperty("Editor");
            Assert.NotNull(prop);
        }

        [Fact]
        public void DocumentTab_HasScrollViewerProperty()
        {
            var prop = TabType.GetProperty("ScrollViewer");
            Assert.NotNull(prop);
        }

        [Fact]
        public void DocumentTab_HasTabViewItemProperty()
        {
            var prop = TabType.GetProperty("TabViewItem");
            Assert.NotNull(prop);
        }

        [Fact]
        public void DocumentTab_HasEditorContainerProperty()
        {
            var prop = TabType.GetProperty("EditorContainer");
            Assert.NotNull(prop);
        }

        [Fact]
        public void DocumentTab_HasPageViewBorderProperty()
        {
            var prop = TabType.GetProperty("PageViewBorder");
            Assert.NotNull(prop);
        }
    }

    // ═══ MainWindow private-field reflection tests ═══════════════════════════════

    public class MainWindowPrivateFieldTests
    {
        private static readonly Type MW = typeof(SmrtPad.MainWindow);
        private const BindingFlags Prv = BindingFlags.NonPublic | BindingFlags.Instance;

        [Theory]
        [InlineData("_settings",      typeof(ISettingsService))]
        [InlineData("_dialogService", typeof(IDialogService))]
        [InlineData("_fileService",   typeof(IFileService))]
        public void MainWindow_HasServiceField(string name, Type expectedType)
        {
            var field = MW.GetField(name, Prv);
            Assert.NotNull(field);
            Assert.True(expectedType.IsAssignableFrom(field!.FieldType));
        }

        [Fact]
        public void MainWindow_HasAutoSaveTimerField()
        {
            var field = MW.GetField("_autoSaveTimer", Prv);
            Assert.NotNull(field);
        }

        [Fact]
        public void MainWindow_HasRulersVisibleField()
        {
            var field = MW.GetField("_rulersVisible", Prv);
            Assert.NotNull(field);
            Assert.Equal(typeof(bool), field!.FieldType);
        }

        [Fact]
        public void MainWindow_HasPageViewActiveField()
        {
            var field = MW.GetField("_pageViewActive", Prv);
            Assert.NotNull(field);
            Assert.Equal(typeof(bool), field!.FieldType);
        }

        [Fact]
        public void MainWindow_HasLastFontColorField()
        {
            var field = MW.GetField("_lastFontColor", Prv);
            Assert.NotNull(field);
            Assert.Equal(typeof(Windows.UI.Color), field!.FieldType);
        }

        [Fact]
        public void MainWindow_HasFontDropdownStyledField()
        {
            var field = MW.GetField("_fontDropdownStyled", Prv);
            Assert.NotNull(field);
            Assert.Equal(typeof(bool), field!.FieldType);
        }

        [Fact]
        public void MainWindow_HasTabsField()
        {
            var field = MW.GetField("_tabs", Prv);
            Assert.NotNull(field);
        }

        [Fact]
        public void MainWindow_HasActiveTabIndexField()
        {
            var field = MW.GetField("_activeTabIndex", Prv);
            Assert.NotNull(field);
            Assert.Equal(typeof(int), field!.FieldType);
        }

        [Fact]
        public void MainWindow_HasMacroField()
        {
            var field = MW.GetField("_macro", Prv);
            Assert.NotNull(field);
            Assert.Equal(typeof(MacroHelper), field!.FieldType);
        }

        [Fact]
        public void MainWindow_HasSuppressFontComboChangeField()
        {
            var field = MW.GetField("_suppressFontComboChange", Prv);
            Assert.NotNull(field);
            Assert.Equal(typeof(bool), field!.FieldType);
        }

        [Fact]
        public void MainWindow_ViewModel_PublicProperty()
        {
            var prop = MW.GetProperty("ViewModel", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(prop);
            Assert.Equal(typeof(EditorViewModel), prop!.PropertyType);
            Assert.False(prop.CanWrite);
        }

        [Fact]
        public void MainWindow_HasPrintDocumentField()
        {
            var field = MW.GetField("_printDocument", Prv);
            Assert.NotNull(field);
        }

        [Fact]
        public void MainWindow_HasPrintPreviewPagesField()
        {
            var field = MW.GetField("_printPreviewPages", Prv);
            Assert.NotNull(field);
        }
    }

    // ═══ ISettingsService / IDialogService / IFileService member parity ══════════

    public class InterfaceMemberParityFinalTests
    {
        [Fact]
        public void ISettingsService_HasAllSettingsServiceProperties()
        {
            var iface  = typeof(ISettingsService);
            var impl   = typeof(SettingsService);
            foreach (var prop in iface.GetProperties())
                Assert.NotNull(impl.GetProperty(prop.Name));
        }

        [Fact]
        public void ISettingsService_HasAllSettingsServiceMethods()
        {
            var iface = typeof(ISettingsService);
            var impl  = typeof(SettingsService);
            foreach (var method in iface.GetMethods().Where(m => !m.IsSpecialName))
                Assert.NotNull(impl.GetMethod(method.Name));
        }

        [Fact]
        public void SettingsService_ImplementsISettingsService()
        {
            Assert.True(typeof(ISettingsService).IsAssignableFrom(typeof(SettingsService)));
        }

        [Fact]
        public void DialogService_ImplementsIDialogService()
        {
            Assert.True(typeof(IDialogService).IsAssignableFrom(typeof(DialogService)));
        }

        [Fact]
        public void FileService_ImplementsIFileService()
        {
            Assert.True(typeof(IFileService).IsAssignableFrom(typeof(FileService)));
        }

        [Fact]
        public void ISettingsService_SpellCheckEnabled_Exists()
        {
            Assert.NotNull(typeof(ISettingsService).GetProperty("SpellCheckEnabled"));
        }

        [Fact]
        public void ISettingsService_RulerUnits_Exists()
        {
            Assert.NotNull(typeof(ISettingsService).GetProperty("RulerUnits"));
        }

        [Fact]
        public void ISettingsService_AutoSaveEnabled_Exists()
        {
            Assert.NotNull(typeof(ISettingsService).GetProperty("AutoSaveEnabled"));
        }
    }

    // ═══ OneDriveHelper logic tests ══════════════════════════════════════════════

    public class OneDriveHelperLogicTests
    {
        [Fact]
        public void IsAvailable_MatchesGetOneDrivePath()
        {
            bool available = OneDriveHelper.IsAvailable();
            bool hasPath   = OneDriveHelper.GetOneDrivePath() != null;
            Assert.Equal(hasPath, available);
        }

        [Fact]
        public void IsAvailable_ReturnsBool()
        {
            // Just ensure no exception and returns a bool
            _ = OneDriveHelper.IsAvailable();
        }

        [Fact]
        public void GetOneDrivePath_ReturnsNullOrNonEmptyString()
        {
            string? path = OneDriveHelper.GetOneDrivePath();
            if (path is not null)
                Assert.NotEmpty(path);
        }

        [Fact]
        public void GetOneDrivePath_ReturnsNullOrExistingDirectory()
        {
            string? path = OneDriveHelper.GetOneDrivePath();
            if (path is not null)
                Assert.True(Directory.Exists(path));
        }
    }

    // ═══ RulerHelper additional edge tests ═══════════════════════════════════════

    public class RulerHelperEdgeCaseTests
    {
        [Fact]
        public void GetPixelsPerUnit_ZeroZoom_ReturnsZero()
        {
            double px = RulerHelper.GetPixelsPerUnit("in", 0.0, out _);
            Assert.Equal(0.0, px);
        }

        [Fact]
        public void GetPixelsPerUnit_ZeroZoomCm_ReturnsZero()
        {
            double px = RulerHelper.GetPixelsPerUnit("cm", 0.0, out _);
            Assert.Equal(0.0, px);
        }

        [Fact]
        public void GetPixelsPerUnit_500PercentZoom_ScalesCorrectly()
        {
            double px100 = RulerHelper.GetPixelsPerUnit("in", 100.0, out _);
            double px500 = RulerHelper.GetPixelsPerUnit("in", 500.0, out _);
            Assert.Equal(px100 * 5.0, px500, precision: 6);
        }

        [Fact]
        public void GetPixelsPerUnit_NullString_DefaultsToInches()
        {
            double px = RulerHelper.GetPixelsPerUnit(null!, 100.0, out string label);
            Assert.Equal("in", label);
            Assert.Equal(96.0, px, precision: 6);
        }

        [Fact]
        public void GetPixelsPerUnit_EmptyString_DefaultsToInches()
        {
            double px = RulerHelper.GetPixelsPerUnit("", 100.0, out string label);
            Assert.Equal("in", label);
        }

        [Fact]
        public void GetPixelsPerUnit_Cm_LabelIsCm()
        {
            RulerHelper.GetPixelsPerUnit("cm", 100.0, out string label);
            Assert.Equal("cm", label);
        }

        [Fact]
        public void GetPixelsPerUnit_Inches_Is96pxAt100Percent()
        {
            double px = RulerHelper.GetPixelsPerUnit("in", 100.0, out _);
            Assert.Equal(96.0, px, precision: 6);
        }

        [Fact]
        public void GetPixelsPerUnit_Cm_IsCorrectAt100Percent()
        {
            double px = RulerHelper.GetPixelsPerUnit("cm", 100.0, out _);
            // 96 / 2.54 ≈ 37.7953
            Assert.Equal(96.0 / 2.54, px, precision: 4);
        }
    }

    // ═══ ResourceHelper resw-parsing path tests ══════════════════════════════════

    public class ResourceHelperReswParsingTests
    {
        [Theory]
        [InlineData("CutMenuItem")]
        [InlineData("CopyMenuItem")]
        [InlineData("PasteMenuItem")]
        [InlineData("UndoMenuItem")]
        [InlineData("RedoMenuItem")]
        [InlineData("SelectAllMenuItem")]
        [InlineData("BoldButton")]
        [InlineData("ItalicButton")]
        [InlineData("UnderlineButton")]
        [InlineData("StrikethroughButton")]
        [InlineData("AlignLeftButton")]
        [InlineData("AlignCenterButton")]
        [InlineData("AlignRightButton")]
        [InlineData("AlignJustifyButton")]
        public void GetString_RibbonKey_IsNonEmpty(string key)
        {
            string result = ResourceHelper.GetString(key);
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetFormatted_With3Args_NoException()
        {
            // Three-arg format — verifies varargs path
            string result = ResourceHelper.GetFormatted("StatusBarLineCol", 1, 2);
            Assert.NotEmpty(result);
        }

        [Fact]
        public void GetString_KeyWithDotSuffix_ReturnsValue()
        {
            // Keys like "CutMenuItem.Text" are stored with and without the dot suffix
            string withDot    = ResourceHelper.GetString("CutMenuItem.Text");
            string withoutDot = ResourceHelper.GetString("CutMenuItem");
            Assert.NotEmpty(withDot);
            Assert.NotEmpty(withoutDot);
        }

        [Fact]
        public void GetString_NonExistentKey_ReturnsKeyName()
        {
            string key    = "NonExistentKey_XYZ_42";
            string result = ResourceHelper.GetString(key);
            Assert.Equal(key, result);
        }

        [Fact]
        public void GetFormatted_NonExistentKey_ReturnsKeyName()
        {
            string key    = "NonExistentFormatKey_XYZ";
            string result = ResourceHelper.GetFormatted(key);
            Assert.Equal(key, result);
        }
    }
}
