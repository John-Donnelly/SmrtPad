using System;
using SmrtPad.Helpers;
using Xunit;

namespace SmrtPad.Tests.Helpers
{
    public sealed class MarkdownToRtfConverterTests
    {
        [Fact]
        public void Convert_NullInput_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => MarkdownToRtfConverter.Convert(null!));
        }

        [Fact]
        public void Convert_EmptyString_ReturnsMinimalRtfHeader()
        {
            var rtf = MarkdownToRtfConverter.Convert(string.Empty);

            Assert.Equal(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Calibri;}{\f1 Consolas;}}{\colortbl ;\red240\green240\blue240;}}", rtf);
        }

        [Fact]
        public void Convert_PlainParagraph_WrapsInPard()
        {
            var rtf = MarkdownToRtfConverter.Convert("plain text");

            Assert.Contains(@"\pard\sl276\slmult1 plain text\par", rtf);
        }

        [Fact]
        public void Convert_H1_ProducesLargeBoldFragment()
        {
            var rtf = MarkdownToRtfConverter.Convert("# Heading");

            Assert.Contains(@"\pard\sb240\sa60\b\fs36 Heading", rtf);
        }

        [Fact]
        public void Convert_H2_ProducesMediumBoldFragment()
        {
            var rtf = MarkdownToRtfConverter.Convert("## Heading");

            Assert.Contains(@"\pard\sb180\sa40\b\fs28 Heading", rtf);
        }

        [Fact]
        public void Convert_H3_ProducesSmallBoldFragment()
        {
            var rtf = MarkdownToRtfConverter.Convert("### Heading");

            Assert.Contains(@"\pard\sb120\sa20\b\fs24 Heading", rtf);
        }

        [Fact]
        public void Convert_BoldText_WrapsBold()
        {
            var rtf = MarkdownToRtfConverter.Convert("**bold**");

            Assert.Contains(@"{\b bold}", rtf);
        }

        [Fact]
        public void Convert_ItalicText_WrapsItalic()
        {
            var rtf = MarkdownToRtfConverter.Convert("*italic*");

            Assert.Contains(@"{\i italic}", rtf);
        }

        [Fact]
        public void Convert_InlineCode_UsesMonospaceFont()
        {
            var rtf = MarkdownToRtfConverter.Convert("`code`");

            Assert.Contains(@"{\f1\highlight1 code}", rtf);
        }

        [Fact]
        public void Convert_UnorderedList_ProducesBulletedList()
        {
            var rtf = MarkdownToRtfConverter.Convert("- item");

            Assert.Contains(@"\pnlvlblt item\par", rtf);
        }

        [Fact]
        public void Convert_OrderedList_ProducesNumberedList()
        {
            var rtf = MarkdownToRtfConverter.Convert("1. item");

            Assert.Contains(@"\pnlvlbody item\par", rtf);
        }

        [Fact]
        public void Convert_HorizontalRule_ProducesHrFragment()
        {
            var rtf = MarkdownToRtfConverter.Convert("---");

            Assert.Contains(@"\brdrb\brdrs\brdrw10", rtf);
        }

        [Fact]
        public void Convert_Blockquote_ProducesIndentedParagraph()
        {
            var rtf = MarkdownToRtfConverter.Convert("> quote");

            Assert.Contains(@"\pard\li720\ri720 quote\par", rtf);
        }

        [Fact]
        public void Convert_FencedCodeBlock_UsesMonospaceFont()
        {
            var rtf = MarkdownToRtfConverter.Convert("```\ncode\n```");

            Assert.Contains(@"\f1\highlight1 code", rtf);
        }

        [Fact]
        public void Convert_NestedBoldAndItalic_BothApplied()
        {
            var rtf = MarkdownToRtfConverter.Convert("***both***");

            Assert.Contains(@"{\b {\i both}}", rtf);
        }

        [Fact]
        public void Convert_MultiParagraph_EachParagraphSeparated()
        {
            var rtf = MarkdownToRtfConverter.Convert("first\n\nsecond");

            Assert.Contains(@"first\par\pard\sl276\slmult1 second\par", rtf);
        }

        [Fact]
        public void Convert_HeadingFollowedByParagraph_BothPresent()
        {
            var rtf = MarkdownToRtfConverter.Convert("# Heading\n\nparagraph");

            Assert.Contains(@"\fs36 Heading", rtf);
            Assert.Contains(@"\sl276\slmult1 paragraph\par", rtf);
        }

        [Fact]
        public void Convert_EmptyListItem_HandledGracefully()
        {
            var rtf = MarkdownToRtfConverter.Convert("-");

            Assert.Contains(@"\pnlvlblt \par", rtf);
        }

        [Fact]
        public void Convert_OnlyWhitespaceParagraph_SkippedOrEmpty()
        {
            var rtf = MarkdownToRtfConverter.Convert("   \n\n\t");

            Assert.Equal(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Calibri;}{\f1 Consolas;}}{\colortbl ;\red240\green240\blue240;}}", rtf);
        }

        [Fact]
        public void Convert_UnicodeChars_PreservedInOutput()
        {
            var rtf = MarkdownToRtfConverter.Convert("こんにちは");

            Assert.Contains(@"\u12371?", rtf);
        }

        [Fact]
        public void Convert_SpecialRtfChars_Escaped()
        {
            var rtf = MarkdownToRtfConverter.Convert("\\ { }");

            Assert.Contains(@"\{", rtf);
        }

        [Fact]
        public void Convert_LargeDocument_CompletesWithoutException()
        {
            var markdown = string.Join("\n\n", new string('a', 1000), new string('b', 1000), new string('c', 1000));

            var rtf = MarkdownToRtfConverter.Convert(markdown);

            Assert.Contains(@"\rtf1", rtf);
        }

        [Fact]
        public void Convert_MixedContent_AllElementsPresent()
        {
            var markdown = "# Heading\n\nParagraph with **bold** and *italic* and `code`.\n\n- item\n\n> quote\n\n---";

            var rtf = MarkdownToRtfConverter.Convert(markdown);

            Assert.Contains(@"\fs36 Heading", rtf);
            Assert.Contains(@"{\b bold}", rtf);
            Assert.Contains(@"{\i italic}", rtf);
            Assert.Contains(@"{\f1\highlight1 code}", rtf);
            Assert.Contains(@"\pnlvlblt item\par", rtf);
            Assert.Contains(@"\li720\ri720 quote\par", rtf);
            Assert.Contains(@"\brdrb\brdrs\brdrw10", rtf);
        }
    }
}
