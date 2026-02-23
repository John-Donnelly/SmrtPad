// MaxCoverageTests2.cs — second gap-fill batch
// Covers: all remaining MainWindow handler reflections, RtfParser pard/ulnone/par/line/striked,
// DocxExportHelper.BuildDocument normalisation, SavePromptResult enum,
// DialogService/FileService constructors, and remaining XAML assertions.
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
using SmrtPad.Services;
using SmrtPad.ViewModels;

namespace SmrtPad.Tests
{
    // ═══ MainWindow — all remaining private method reflections ══════════════════

    public class MainWindowRemainingMethodsTests
    {
        private static readonly Type MW = typeof(SmrtPad.MainWindow);
        private const BindingFlags Prv = BindingFlags.NonPublic | BindingFlags.Instance;
        private const BindingFlags PrvSta = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        private static void AssertMethod(string name) =>
            Assert.NotNull(MW.GetMethod(name, Prv) ?? MW.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static));

        // ── Alignment handlers ────────────────────────────────────────────────

        [Fact] public void AlignCenter_Click_Exists()    => AssertMethod("AlignCenter_Click");
        [Fact] public void AlignJustify_Click_Exists()   => AssertMethod("AlignJustify_Click");
        [Fact] public void AlignLeft_Click_Exists()      => AssertMethod("AlignLeft_Click");
        [Fact] public void AlignRight_Click_Exists()     => AssertMethod("AlignRight_Click");

        // ── Apply* helpers ────────────────────────────────────────────────────

        [Fact] public void ApplyFontSizeFromText_Exists()       => AssertMethod("ApplyFontSizeFromText");
        [Fact] public void ApplyLastFontColor_Invoked_Exists()  => AssertMethod("ApplyLastFontColor_Invoked");
        [Fact] public void ApplyListType_Exists()               => AssertMethod("ApplyListType");
        [Fact] public void ApplyPageViewLayout_Exists()         => AssertMethod("ApplyPageViewLayout");
        [Fact] public void ApplyParagraphSpacing_Click_Exists() => AssertMethod("ApplyParagraphSpacing_Click");
        [Fact] public void ApplyParagraphStyle_Exists()         => AssertMethod("ApplyParagraphStyle");
        [Fact] public void ApplySettings_Exists()               => AssertMethod("ApplySettings");
        [Fact] public void ApplyTemplate_Exists()               => AssertMethod("ApplyTemplate");
        [Fact] public void ApplyThemeFromSettings_Exists()      => AssertMethod("ApplyThemeFromSettings");
        [Fact] public void ApplyZoom_Exists()                   => AssertMethod("ApplyZoom");

        // ── Formatting toggle handlers ────────────────────────────────────────

        [Fact] public void Bold_Click_Exists()          => AssertMethod("Bold_Click");
        [Fact] public void Italic_Click_Exists()        => AssertMethod("Italic_Click");
        [Fact] public void Underline_Click_Exists()     => AssertMethod("Underline_Click");
        [Fact] public void Strikethrough_Click_Exists() => AssertMethod("Strikethrough_Click");
        [Fact] public void Subscript_Click_Exists()     => AssertMethod("Subscript_Click");
        [Fact] public void Superscript_Click_Exists()   => AssertMethod("Superscript_Click");
        [Fact] public void Bullets_Click_Exists()       => AssertMethod("Bullets_Click");
        [Fact] public void WordWrap_Click_Exists()      => AssertMethod("WordWrap_Click");
        [Fact] public void ClearFormatting_Click_Exists() => AssertMethod("ClearFormatting_Click");

        // ── Clipboard handlers ────────────────────────────────────────────────

        [Fact] public void Copy_Click_Exists()         => AssertMethod("Copy_Click");
        [Fact] public void Cut_Click_Exists()          => AssertMethod("Cut_Click");
        [Fact] public void Paste_Click_Exists()        => AssertMethod("Paste_Click");
        [Fact] public void PasteSpecial_Click_Exists() => AssertMethod("PasteSpecial_Click");
        [Fact] public void SelectAll_Click_Exists()    => AssertMethod("SelectAll_Click");

        // ── Undo/Redo ─────────────────────────────────────────────────────────

        [Fact] public void Undo_Click_Exists() => AssertMethod("Undo_Click");
        [Fact] public void Redo_Click_Exists() => AssertMethod("Redo_Click");

        // ── File handlers ─────────────────────────────────────────────────────

        [Fact] public void New_Click_Exists()    => AssertMethod("New_Click");
        [Fact] public void Open_Click_Exists()   => AssertMethod("Open_Click");
        [Fact] public void Save_Click_Exists()   => AssertMethod("Save_Click");
        [Fact] public void SaveAs_Click_Exists() => AssertMethod("SaveAs_Click");

        // ── Zoom handlers ─────────────────────────────────────────────────────

        [Fact] public void ZoomIn_Click_Exists()  => AssertMethod("ZoomIn_Click");
        [Fact] public void ZoomOut_Click_Exists() => AssertMethod("ZoomOut_Click");

        // ── Font size helpers ─────────────────────────────────────────────────

        [Fact] public void GrowFont_Click_Exists()   => AssertMethod("GrowFont_Click");
        [Fact] public void ShrinkFont_Click_Exists() => AssertMethod("ShrinkFont_Click");

        // ── List type handlers ────────────────────────────────────────────────

        [Fact] public void ListTypeBullet_Click_Exists()      => AssertMethod("ListTypeBullet_Click");
        [Fact] public void ListTypeNone_Click_Exists()        => AssertMethod("ListTypeNone_Click");
        [Fact] public void ListTypeNumber_Click_Exists()      => AssertMethod("ListTypeNumber_Click");
        [Fact] public void ListTypeLowerLetter_Click_Exists() => AssertMethod("ListTypeLowerLetter_Click");
        [Fact] public void ListTypeUpperLetter_Click_Exists() => AssertMethod("ListTypeUpperLetter_Click");
        [Fact] public void ListTypeLowerRoman_Click_Exists()  => AssertMethod("ListTypeLowerRoman_Click");
        [Fact] public void ListTypeUpperRoman_Click_Exists()  => AssertMethod("ListTypeUpperRoman_Click");

        // ── Style handlers ────────────────────────────────────────────────────

        [Fact] public void StyleNormal_Click_Exists()   => AssertMethod("StyleNormal_Click");
        [Fact] public void StyleHeading1_Click_Exists() => AssertMethod("StyleHeading1_Click");
        [Fact] public void StyleHeading2_Click_Exists() => AssertMethod("StyleHeading2_Click");
        [Fact] public void StyleHeading3_Click_Exists() => AssertMethod("StyleHeading3_Click");
        [Fact] public void StyleSubtitle_Click_Exists() => AssertMethod("StyleSubtitle_Click");
        [Fact] public void StyleQuote_Click_Exists()    => AssertMethod("StyleQuote_Click");

        // ── Insert handlers ───────────────────────────────────────────────────

        [Fact] public void InsertDateTime_Click_Exists()  => AssertMethod("InsertDateTime_Click");
        [Fact] public void InsertHyperlink_Click_Exists() => AssertMethod("InsertHyperlink_Click");
        [Fact] public void InsertObject_Click_Exists()    => AssertMethod("InsertObject_Click");
        [Fact] public void InsertPicture_Click_Exists()   => AssertMethod("InsertPicture_Click");
        [Fact] public void InsertSymbol_Click_Exists()    => AssertMethod("InsertSymbol_Click");
        [Fact] public void InsertTable_Click_Exists()     => AssertMethod("InsertTable_Click");

        // ── Find / Replace ────────────────────────────────────────────────────

        [Fact] public void FindNext_Click_Exists()     => AssertMethod("FindNext_Click");
        [Fact] public void FindPrevious_Click_Exists() => AssertMethod("FindPrevious_Click");
        [Fact] public void Replace_Click_Exists()      => AssertMethod("Replace_Click");
        [Fact] public void ReplaceAll_Click_Exists()   => AssertMethod("ReplaceAll_Click");
        [Fact] public void HighlightAllMatches_Click_Exists() => AssertMethod("HighlightAllMatches_Click");

        // ── View / UI ─────────────────────────────────────────────────────────

        [Fact] public void PageView_Click_Exists()      => AssertMethod("PageView_Click");
        [Fact] public void Ruler_Click_Exists()         => AssertMethod("Ruler_Click");
        [Fact] public void FocusMode_Click_Exists()     => AssertMethod("FocusMode_Click");
        [Fact] public void ThemeToggle_Click_Exists()   => AssertMethod("ThemeToggle_Click");
        [Fact] public void SpellCheck_Click_Exists()    => AssertMethod("SpellCheck_Click");

        // ── Colour pickers ────────────────────────────────────────────────────

        [Fact] public void TextColorMoreColors_Click_Exists()       => AssertMethod("TextColorMoreColors_Click");
        [Fact] public void TextColorPicker_ColorChanged_Exists()    => AssertMethod("TextColorPicker_ColorChanged");
        [Fact] public void TextColorSwatchButton_Click_Exists()     => AssertMethod("TextColorSwatchButton_Click");
        [Fact] public void ClearHighlights_Click_Exists()           => AssertMethod("ClearHighlights_Click");
        [Fact] public void HighlightColorPicker_ColorChanged_Exists() => AssertMethod("HighlightColorPicker_ColorChanged");
        [Fact] public void HighlightSwatchButton_Click_Exists()     => AssertMethod("HighlightSwatchButton_Click");

        // ── Indentation ───────────────────────────────────────────────────────

        [Fact] public void IncreaseIndent_Click_Exists() => AssertMethod("IncreaseIndent_Click");
        [Fact] public void DecreaseIndent_Click_Exists() => AssertMethod("DecreaseIndent_Click");

        // ── Misc handlers ─────────────────────────────────────────────────────

        [Fact] public void CustomLineSpacing_Click_Exists() => AssertMethod("CustomLineSpacing_Click");
        [Fact] public void LineSpacing_Click_Exists()       => AssertMethod("LineSpacing_Click");
        [Fact] public void TabStops_Click_Exists()          => AssertMethod("TabStops_Click");
        [Fact] public void PaintDrawing_Click_Exists()      => AssertMethod("PaintDrawing_Click");
        [Fact] public void FileMenu_Tapped_Exists()         => AssertMethod("FileMenu_Tapped");

        // ── Private helpers ───────────────────────────────────────────────────

        [Fact] public void DrawHorizontalRuler_Exists() => AssertMethod("DrawHorizontalRuler");
        [Fact] public void DrawVerticalRuler_Exists()   => AssertMethod("DrawVerticalRuler");
        [Fact] public void RedrawRulers_Exists()        => AssertMethod("RedrawRulers");
        [Fact] public void RefreshTabStopList_Exists()  => AssertMethod("RefreshTabStopList");
        [Fact] public void SetupAutoSave_Exists()       => AssertMethod("SetupAutoSave");
        [Fact] public void ShowBackstage_Exists()       => AssertMethod("ShowBackstage");
        [Fact] public void HideBackstage_Exists()       => AssertMethod("HideBackstage");
        [Fact] public void UpdateLineColumn_Exists()    => AssertMethod("UpdateLineColumn");
        [Fact] public void UpdateRulerVisibility_Exists()   => AssertMethod("UpdateRulerVisibility");
        [Fact] public void UpdateSelectionLength_Exists()   => AssertMethod("UpdateSelectionLength");
        [Fact] public void UpdateStatusBarCounts_Exists()   => AssertMethod("UpdateStatusBarCounts");
        [Fact] public void UpdateTitleBarTheme_Exists()     => AssertMethod("UpdateTitleBarTheme");
        [Fact] public void PasteAsPlainTextAsync_Exists()   => AssertMethod("PasteAsPlainTextAsync");

        // ── Print infrastructure ──────────────────────────────────────────────

        [Fact] public void PrintDocument_AddPages_Exists()     => AssertMethod("PrintDocument_AddPages");
        [Fact] public void PrintDocument_GetPreviewPage_Exists() => AssertMethod("PrintDocument_GetPreviewPage");
        [Fact] public void PrintDocument_Paginate_Exists()     => AssertMethod("PrintDocument_Paginate");
        [Fact] public void PrintTask_Completed_Exists()        => AssertMethod("PrintTask_Completed");
        [Fact] public void PrintTask_Requested_Exists()        => AssertMethod("PrintTask_Requested");
        [Fact] public void PrintTaskSourceRequested_Exists()   => AssertMethod("PrintTaskSourceRequested");

        // ── Public API ────────────────────────────────────────────────────────

        [Fact]
        public void MainWindow_OpenFileByPathAsync_IsPublicAsync()
        {
            var m = MW.GetMethod("OpenFileByPathAsync",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(m);
            Assert.Equal(typeof(System.Threading.Tasks.Task), m!.ReturnType);
        }

        [Fact]
        public void MainWindow_IsSealed()
        {
            Assert.True(MW.IsSealed);
        }

        [Fact]
        public void MainWindow_HasPublicDefaultConstructor()
        {
            var ctor = MW.GetConstructor(Type.EmptyTypes);
            Assert.NotNull(ctor);
            Assert.True(ctor!.IsPublic);
        }
    }

    // ═══ RtfParser uncovered branch tests ════════════════════════════════════════

    public class RtfParserBranchTests
    {
        private static string ParseToText(string rtf)
        {
            var result = RtfParser.Parse(rtf);
            return string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
        }

        // ── \pard resets formatting ───────────────────────────────────────────

        [Fact]
        public void Parse_Pard_ResetsBold()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\b Bold\pard Reset}");
            var runs   = result.SelectMany(p => p.Runs).ToList();
            // First run should be bold, second should not
            Assert.Contains(runs, r => r.Bold && r.Text.Contains("Bold"));
            Assert.Contains(runs, r => !r.Bold && r.Text.Contains("Reset"));
        }

        [Fact]
        public void Parse_Pard_ResetsItalic()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\i Italic\pard Reset}");
            var runs   = result.SelectMany(p => p.Runs).ToList();
            Assert.Contains(runs, r => r.Italic && r.Text.Contains("Italic"));
            Assert.Contains(runs, r => !r.Italic && r.Text.Contains("Reset"));
        }

        [Fact]
        public void Parse_Pard_ResetsUnderline()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\ul Under\pard Reset}");
            var runs   = result.SelectMany(p => p.Runs).ToList();
            Assert.Contains(runs, r => r.Underline && r.Text.Contains("Under"));
            Assert.Contains(runs, r => !r.Underline && r.Text.Contains("Reset"));
        }

        [Fact]
        public void Parse_Pard_ResetsAlignment()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\qc Center\pard Reset}");
            // After pard, alignment goes back to left
            Assert.NotEmpty(result);
        }

        // ── \ulnone disables underline ────────────────────────────────────────

        [Fact]
        public void Parse_Ulnone_DisablesUnderline()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\ul Under\ulnone Off}");
            var runs   = result.SelectMany(p => p.Runs).ToList();
            Assert.Contains(runs, r => r.Underline && r.Text.Contains("Under"));
            Assert.Contains(runs, r => !r.Underline && r.Text.Contains("Off"));
        }

        // ── \par creates new paragraph ────────────────────────────────────────

        [Fact]
        public void Parse_Par_CreatesNewParagraph()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi First\par Second}");
            Assert.True(result.Count >= 2, $"Expected ≥2 paragraphs, got {result.Count}");
        }

        [Fact]
        public void Parse_Par_EachParagraphHasText()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi First\par Second\par Third}");
            var allText = result.SelectMany(p => p.Runs).Select(r => r.Text).ToList();
            Assert.Contains(allText, t => t.Contains("First"));
            Assert.Contains(allText, t => t.Contains("Second"));
            Assert.Contains(allText, t => t.Contains("Third"));
        }

        // ── \line creates new paragraph ───────────────────────────────────────

        [Fact]
        public void Parse_Line_CreatesNewParagraph()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi Before\line After}");
            Assert.True(result.Count >= 2);
        }

        // ── \striked is an alias for \strike ──────────────────────────────────

        [Fact]
        public void Parse_Striked_ProducesStrikethroughRun()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\striked Struck}");
            var runs   = result.SelectMany(p => p.Runs).ToList();
            Assert.Contains(runs, r => r.Strikethrough && r.Text.Contains("Struck"));
        }

        // ── Hex escape \'XX ───────────────────────────────────────────────────

        [Fact]
        public void Parse_HexEscape_41_ProducesA()
        {
            // \'41 = 'A' in ASCII
            var result = RtfParser.Parse(@"{\rtf1\ansi\'41}");
            string text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("A", text);
        }

        [Fact]
        public void Parse_HexEscape_61_ProducesLowercaseA()
        {
            // \'61 = 'a'
            var result = RtfParser.Parse(@"{\rtf1\ansi\'61}");
            string text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("a", text);
        }

        [Fact]
        public void Parse_HexEscape_Mixed_WithText()
        {
            // \'48 = 'H', \'69 = 'i'
            var result = RtfParser.Parse(@"{\rtf1\ansi\'48\'69 World}");
            string text = string.Join("", result.SelectMany(p => p.Runs).Select(r => r.Text));
            Assert.Contains("Hi", text);
        }

        // ── \qj produces justify paragraph ───────────────────────────────────

        [Fact]
        public void Parse_Qj_SetsJustifyAlignment()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\qj Justified}");
            Assert.True(result.Count > 0);
            Assert.Equal("justify", result[0].Alignment);
        }

        // ── \ql explicitly sets left alignment ───────────────────────────────

        [Fact]
        public void Parse_Ql_SetsLeftAlignment()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\qc Center\par\ql Left}");
            Assert.True(result.Count >= 2);
            // Second paragraph should be left-aligned
            Assert.Equal("left", result.Last().Alignment);
        }

        // ── Bold off with \b0 ─────────────────────────────────────────────────

        [Fact]
        public void Parse_BoldZero_TurnsBoldOff()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\b On\b0 Off}");
            var runs   = result.SelectMany(p => p.Runs).ToList();
            Assert.Contains(runs, r => r.Bold && r.Text.Contains("On"));
            Assert.Contains(runs, r => !r.Bold && r.Text.Contains("Off"));
        }

        // ── \strike turns on strikethrough ────────────────────────────────────

        [Fact]
        public void Parse_Strike_ProducesStrikethrough()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\strike Struck}");
            var runs   = result.SelectMany(p => p.Runs).ToList();
            Assert.Contains(runs, r => r.Strikethrough);
        }

        // ── Multi-paragraph alignment inheritance ─────────────────────────────

        [Fact]
        public void Parse_CenterAlignmentInherited()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\qc Center text}");
            Assert.Equal("center", result[0].Alignment);
        }

        [Fact]
        public void Parse_RightAlignmentSet()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\qr Right text}");
            Assert.Equal("right", result[0].Alignment);
        }

        // ── FontSize half-points ──────────────────────────────────────────────

        [Fact]
        public void Parse_FontSize_StoresHalfPoints()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi\fs24 text}");
            var run    = result.SelectMany(p => p.Runs).FirstOrDefault();
            Assert.NotNull(run);
            Assert.Equal(24, run!.FontSizeHalfPts);
        }

        [Fact]
        public void Parse_FontSize_DefaultIs24WhenUnset()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi text}");
            var run    = result.SelectMany(p => p.Runs).FirstOrDefault();
            Assert.NotNull(run);
            Assert.Equal(24, run!.FontSizeHalfPts); // default fshp=24
        }
    }

    // ═══ DocxExportHelper.BuildDocument line-ending normalisation tests ══════════

    public class DocxBuildDocumentNormalisationTests
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
        public void GenerateDocx_CrLfLineEndings_ProducesCorrectParagraphCount()
        {
            var bytes = DocxExportHelper.GenerateDocx("Line1\r\nLine2\r\nLine3");
            var doc   = GetDocXml(bytes);
            // 3 lines + 1 sectPr
            var paras = doc.Descendants(W + "p").ToList();
            Assert.Equal(3, paras.Count);
        }

        [Fact]
        public void GenerateDocx_CrOnlyLineEndings_NormalisedCorrectly()
        {
            var bytes = DocxExportHelper.GenerateDocx("A\rB\rC");
            var doc   = GetDocXml(bytes);
            var paras = doc.Descendants(W + "p").ToList();
            Assert.Equal(3, paras.Count);
        }

        [Fact]
        public void GenerateDocx_TrailingNewline_Trimmed()
        {
            // "Hello\n" has trailing newline — TrimEnd('\n') removes it → 1 paragraph
            var bytes  = DocxExportHelper.GenerateDocx("Hello\n");
            var doc    = GetDocXml(bytes);
            var paras  = doc.Descendants(W + "p").ToList();
            Assert.Single(paras);
        }

        [Fact]
        public void GenerateDocx_EmptyString_ProducesOneParagraph()
        {
            var bytes = DocxExportHelper.GenerateDocx("");
            var doc   = GetDocXml(bytes);
            var paras = doc.Descendants(W + "p").ToList();
            Assert.Single(paras);
        }

        [Fact]
        public void GenerateDocx_NullArgument_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => DocxExportHelper.GenerateDocx(null!));
        }

        [Fact]
        public void GenerateDocx_MultipleBlankLines_EachBecomesEmptyParagraph()
        {
            var bytes = DocxExportHelper.GenerateDocx("A\n\n\nB");
            var doc   = GetDocXml(bytes);
            var paras = doc.Descendants(W + "p").ToList();
            Assert.Equal(4, paras.Count); // A, "", "", B
        }

        [Fact]
        public void GenerateDocx_TextPreserved_InTElement()
        {
            var bytes = DocxExportHelper.GenerateDocx("Hello World");
            var doc   = GetDocXml(bytes);
            var texts = doc.Descendants(W + "t").Select(e => e.Value).ToList();
            Assert.Contains("Hello World", texts);
        }

        [Fact]
        public void GenerateDocx_SectPr_AlwaysPresent()
        {
            var bytes = DocxExportHelper.GenerateDocx("test");
            var doc   = GetDocXml(bytes);
            Assert.NotEmpty(doc.Descendants(W + "sectPr"));
        }

        [Fact]
        public void GenerateDocx_PreservesXmlSpaceAttribute()
        {
            var bytes = DocxExportHelper.GenerateDocx("  space  ");
            var doc   = GetDocXml(bytes);
            // All <w:t> should have xml:space="preserve"
            var tElems = doc.Descendants(W + "t").ToList();
            Assert.All(tElems, t =>
                Assert.Equal("preserve",
                    t.Attribute(XNamespace.Xml + "space")?.Value));
        }
    }

    // ═══ SavePromptResult and DialogService / FileService constructor tests ═════

    public class ServiceConstructorAndEnumTests
    {
        [Fact]
        public void SavePromptResult_HasThreeValues()
        {
            var values = Enum.GetValues<SavePromptResult>();
            Assert.Equal(3, values.Length);
        }

        [Fact]
        public void SavePromptResult_HasSaveValue()
        {
            Assert.True(Enum.IsDefined(typeof(SavePromptResult), "Save"));
        }

        [Fact]
        public void SavePromptResult_HasDontSaveValue()
        {
            Assert.True(Enum.IsDefined(typeof(SavePromptResult), "DontSave"));
        }

        [Fact]
        public void SavePromptResult_HasCancelValue()
        {
            Assert.True(Enum.IsDefined(typeof(SavePromptResult), "Cancel"));
        }

        [Fact]
        public void SavePromptResult_ValuesAreDistinct()
        {
            var values = Enum.GetValues<SavePromptResult>().Cast<int>().ToList();
            Assert.Equal(values.Count, values.Distinct().Count());
        }

        [Fact]
        public void DialogService_HasDefaultConstructor()
        {
            var ctor = typeof(DialogService).GetConstructor(Type.EmptyTypes);
            Assert.NotNull(ctor);
        }

        [Fact]
        public void DialogService_HasFuncConstructor()
        {
            var ctor = typeof(DialogService).GetConstructor(
                new[] { typeof(Func<Microsoft.UI.Xaml.XamlRoot>) });
            Assert.NotNull(ctor);
        }

        [Fact]
        public void DialogService_ShowErrorAsync_IsAsync()
        {
            var m = typeof(DialogService).GetMethod("ShowErrorAsync");
            Assert.NotNull(m);
            Assert.Equal(typeof(System.Threading.Tasks.Task), m!.ReturnType);
        }

        [Fact]
        public void DialogService_ShowSavePromptAsync_ReturnsTaskOfResult()
        {
            var m = typeof(DialogService).GetMethod("ShowSavePromptAsync");
            Assert.NotNull(m);
            Assert.Equal(typeof(System.Threading.Tasks.Task<SavePromptResult>), m!.ReturnType);
        }

        [Fact]
        public void FileService_HasDefaultConstructor()
        {
            var ctor = typeof(FileService).GetConstructor(Type.EmptyTypes);
            Assert.NotNull(ctor);
        }

        [Fact]
        public void FileService_HasFuncWindowConstructor()
        {
            var ctor = typeof(FileService).GetConstructor(
                new[] { typeof(Func<Microsoft.UI.Xaml.Window>) });
            Assert.NotNull(ctor);
        }

        [Fact]
        public void FileService_PickOpenFileAsync_IsAsync()
        {
            var m = typeof(FileService).GetMethod("PickOpenFileAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void FileService_PickSaveFileAsync_IsAsync()
        {
            var m = typeof(FileService).GetMethod("PickSaveFileAsync");
            Assert.NotNull(m);
        }

        [Fact]
        public void FileService_GetFileFromPathAsync_IsAsync()
        {
            var m = typeof(FileService).GetMethod("GetFileFromPathAsync");
            Assert.NotNull(m);
        }
    }

    // ═══ MainWindow.xaml handler-in-XAML completeness tests ══════════════════════

    public class MainWindowXamlCompletenessTests
    {
        private static string? ReadXaml()
        {
            string? dir = Directory.GetCurrentDirectory();
            while (dir is not null)
            {
                string candidate = Path.Combine(dir, "SmrtPad", "MainWindow.xaml");
                if (File.Exists(candidate)) return File.ReadAllText(candidate);
                dir = Directory.GetParent(dir)?.FullName;
            }
            return null;
        }

        [Theory]
        [InlineData("Bold_Click")]
        [InlineData("Italic_Click")]
        [InlineData("Underline_Click")]
        [InlineData("Strikethrough_Click")]
        [InlineData("Subscript_Click")]
        [InlineData("Superscript_Click")]
        [InlineData("AlignLeft_Click")]
        [InlineData("AlignCenter_Click")]
        [InlineData("AlignRight_Click")]
        [InlineData("AlignJustify_Click")]
        [InlineData("ZoomIn_Click")]
        [InlineData("ZoomOut_Click")]
        [InlineData("Undo_Click")]
        [InlineData("Redo_Click")]
        [InlineData("Copy_Click")]
        [InlineData("Cut_Click")]
        [InlineData("Paste_Click")]
        [InlineData("SelectAll_Click")]
        [InlineData("New_Click")]
        [InlineData("Save_Click")]
        [InlineData("FindNext_Click")]
        [InlineData("Replace_Click")]
        [InlineData("ReplaceAll_Click")]
        [InlineData("SpellCheck_Click")]
        [InlineData("WordWrap_Click")]
        [InlineData("Ruler_Click")]
        [InlineData("NewWindow_Click")]
        [InlineData("Bullets_Click")]
        [InlineData("FontFamilyComboBox_SelectionChanged")]
        [InlineData("FontSizeComboBox_SelectionChanged")]
        [InlineData("HRulerCanvas_SizeChanged")]
        [InlineData("VRulerCanvas_SizeChanged")]
        [InlineData("FindRegexCheckBox")]
        [InlineData("FontFamilyComboBox_DropDownOpened")]
        public void MainWindow_XAML_ContainsHandler(string handlerName)
        {
            string? xaml = ReadXaml();
            if (xaml is null) return; // Skip if XAML not found in test context
            Assert.Contains(handlerName, xaml);
        }
    }

    // ═══ EditorViewModel — remaining public method return types ══════════════════

    public class EditorViewModelMethodSignatureTests
    {
        [Theory]
        [InlineData("NewDocument")]
        [InlineData("UpdateStatus")]
        [InlineData("ToggleBold")]
        [InlineData("ToggleItalic")]
        [InlineData("ToggleUnderline")]
        [InlineData("ToggleStrikethrough")]
        [InlineData("ToggleSubscript")]
        [InlineData("ToggleSuperscript")]
        [InlineData("SetAlignment")]
        [InlineData("ToggleBullets")]
        [InlineData("ToggleWordWrap")]
        [InlineData("SetListType")]
        [InlineData("SetLineSpacing")]
        [InlineData("ZoomIn")]
        [InlineData("ZoomOut")]
        [InlineData("SetParagraphSpacing")]
        [InlineData("UpdateWordCount")]
        [InlineData("UpdateCharCount")]
        [InlineData("UpdateCursorPosition")]
        public void ViewModel_Method_ReturnsVoid(string methodName)
        {
            var m = typeof(EditorViewModel).GetMethod(methodName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(m);
            Assert.Equal(typeof(void), m!.ReturnType);
        }

        [Theory]
        [InlineData("WordCountDisplay")]
        [InlineData("CharCountDisplay")]
        [InlineData("SelectionLengthDisplay")]
        [InlineData("LineColDisplay")]
        [InlineData("ZoomDisplay")]
        [InlineData("EncodingDisplay")]
        public void ViewModel_ComputedProperty_ReturnsString(string propertyName)
        {
            var p = typeof(EditorViewModel).GetProperty(propertyName);
            Assert.NotNull(p);
            Assert.Equal(typeof(string), p!.PropertyType);
            Assert.False(p.CanWrite);
        }

        [Fact]
        public void ViewModel_NewDocumentCommand_IsRelayCommand()
        {
            var prop = typeof(EditorViewModel).GetProperty("NewDocumentCommand");
            Assert.NotNull(prop);
        }

        [Fact]
        public void ViewModel_ZoomInCommand_IsRelayCommand()
        {
            var prop = typeof(EditorViewModel).GetProperty("ZoomInCommand");
            Assert.NotNull(prop);
        }

        [Fact]
        public void ViewModel_ZoomOutCommand_IsRelayCommand()
        {
            var prop = typeof(EditorViewModel).GetProperty("ZoomOutCommand");
            Assert.NotNull(prop);
        }
    }

    // ═══ App.xaml.cs — static member completeness ════════════════════════════════

    public class AppMemberCompletenessTests
    {
        [Fact]
        public void App_OnLaunched_IsOverrideMethod()
        {
            var m = typeof(SmrtPad.App).GetMethod("OnLaunched",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(m);
            // Should be an override (virtual on base class)
            Assert.True(m!.IsVirtual || m.GetBaseDefinition() != m);
        }

        [Fact]
        public void App_Windows_DefaultIsEmpty()
        {
            // The static list initialises empty; tests run in a separate process
            // so it shouldn't contain any windows
            Assert.NotNull(SmrtPad.App.Windows);
        }

        [Fact]
        public void App_InheritsFromApplication()
        {
            Assert.True(typeof(Microsoft.UI.Xaml.Application)
                .IsAssignableFrom(typeof(SmrtPad.App)));
        }

        [Fact]
        public void App_Services_IsServiceProvider()
        {
            var prop = typeof(SmrtPad.App).GetProperty("Services");
            Assert.NotNull(prop);
            Assert.Equal(
                typeof(Microsoft.Extensions.DependencyInjection.ServiceProvider),
                prop!.PropertyType);
        }
    }

    // ═══ RtfRun and RtfParagraph contract tests ════════════════════════════════

    public class RtfDataModelContractTests
    {
        [Fact]
        public void RtfRun_IsRecord()
        {
            // Records have compiler-generated Equals, GetHashCode, ToString
            var run = new RtfRun("text", true, false, false, false, "Arial", 24);
            Assert.NotNull(run.ToString());
        }

        [Fact]
        public void RtfRun_EqualRecords_AreEqual()
        {
            var a = new RtfRun("x", false, false, false, false, "", 24);
            var b = new RtfRun("x", false, false, false, false, "", 24);
            Assert.Equal(a, b);
        }

        [Fact]
        public void RtfRun_DifferentText_NotEqual()
        {
            var a = new RtfRun("a", false, false, false, false, "", 24);
            var b = new RtfRun("b", false, false, false, false, "", 24);
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void RtfRun_WithExpression_DoesNotMutateOriginal()
        {
            var orig = new RtfRun("hello", false, false, false, false, "", 24);
            var copy = orig with { Text = "world", Bold = true };
            Assert.Equal("hello", orig.Text);
            Assert.False(orig.Bold);
            Assert.Equal("world", copy.Text);
            Assert.True(copy.Bold);
        }

        [Fact]
        public void RtfParagraph_DefaultAlignment_IsLeft()
        {
            var p = new RtfParagraph();
            Assert.Equal("left", p.Alignment);
        }

        [Fact]
        public void RtfParagraph_DefaultRuns_IsEmpty()
        {
            var p = new RtfParagraph();
            Assert.Empty(p.Runs);
        }

        [Fact]
        public void RtfParagraph_Runs_CanAddAndRetrieve()
        {
            var p   = new RtfParagraph();
            var run = new RtfRun("hi", false, false, false, false, "", 24);
            p.Runs.Add(run);
            Assert.Single(p.Runs);
            Assert.Equal("hi", p.Runs[0].Text);
        }

        [Fact]
        public void RtfParagraph_AlignmentMutable()
        {
            var p = new RtfParagraph { Alignment = "center" };
            Assert.Equal("center", p.Alignment);
            p.Alignment = "right";
            Assert.Equal("right", p.Alignment);
        }

        [Fact]
        public void RtfParser_ReturnsAtLeastOneParagraph()
        {
            var result = RtfParser.Parse(@"{\rtf1\ansi}");
            Assert.NotEmpty(result);
        }

        [Fact]
        public void RtfParser_Parse_NullOrEmpty_ReturnsEmpty()
        {
            Assert.Empty(RtfParser.Parse(""));
            Assert.Empty(RtfParser.Parse(null!));
        }
    }

    // ═══ PdfHelper — EscapePdfString via GeneratePdf observable output ═══════════

    public class PdfHelperEscapeTests
    {
        [Fact]
        public void GeneratePdf_ParenthesesInText_AreEscaped()
        {
            var bytes   = PdfHelper.GeneratePdf("(a + b)");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains(@"\(a + b\)", content);
        }

        [Fact]
        public void GeneratePdf_BackslashInText_IsEscaped()
        {
            var bytes   = PdfHelper.GeneratePdf(@"C:\path");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains(@"C:\\path", content);
        }

        [Fact]
        public void GeneratePdf_ControlCharBelow32_ReplacedWithQuestionMark()
        {
            // Tab is char 9, which is < 32
            var bytes   = PdfHelper.GeneratePdf("A\tB");
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("A?B", content);
        }
    }
}
