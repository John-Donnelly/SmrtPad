using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SmrtPad.Helpers
{
    /// <summary>
    /// Converts a limited Markdown subset into RTF fragments suitable for editor insertion.
    /// </summary>
    public static class MarkdownToRtfConverter
    {
        private static readonly Regex s_orderedListRegex = new(@"^\d+\.\s*", RegexOptions.Compiled);

        /// <summary>
        /// Converts Markdown text into an RTF document.
        /// </summary>
        public static string Convert(string markdown)
        {
            ArgumentNullException.ThrowIfNull(markdown);

            var body = BuildBody(markdown.Replace("\r\n", "\n").Replace('\r', '\n'));
            return @"{\rtf1\ansi\deff0{\fonttbl{\f0 Calibri;}{\f1 Consolas;}}{\colortbl ;\red240\green240\blue240;}" + body + "}";
        }

        private static string BuildBody(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;

            var lines = markdown.Split('\n');
            var builder = new StringBuilder();
            var index = 0;

            while (index < lines.Length)
            {
                var line = lines[index];
                if (string.IsNullOrWhiteSpace(line))
                {
                    index++;
                    continue;
                }

                var trimmed = line.Trim();
                if (trimmed == "```")
                {
                    index = AppendCodeBlock(lines, index + 1, builder);
                    continue;
                }

                if (trimmed == "---")
                {
                    builder.Append(@"\pard\brdrb\brdrs\brdrw10\par");
                    index++;
                    continue;
                }

                if (trimmed.StartsWith("### ", StringComparison.Ordinal))
                {
                    AppendParagraph(builder, @"\pard\sb120\sa20\b\fs24 ", FormatInline(trimmed[4..]), true);
                    index++;
                    continue;
                }

                if (trimmed.StartsWith("## ", StringComparison.Ordinal))
                {
                    AppendParagraph(builder, @"\pard\sb180\sa40\b\fs28 ", FormatInline(trimmed[3..]), true);
                    index++;
                    continue;
                }

                if (trimmed.StartsWith("# ", StringComparison.Ordinal))
                {
                    AppendParagraph(builder, @"\pard\sb240\sa60\b\fs36 ", FormatInline(trimmed[2..]), true);
                    index++;
                    continue;
                }

                if (trimmed.StartsWith("> ", StringComparison.Ordinal))
                {
                    AppendParagraph(builder, @"\pard\li720\ri720 ", FormatInline(trimmed[2..]), false);
                    index++;
                    continue;
                }

                if (trimmed.StartsWith("- ", StringComparison.Ordinal) || trimmed.StartsWith("* ", StringComparison.Ordinal) || trimmed == "-" || trimmed == "*")
                {
                    AppendParagraph(builder, @"\pard\pnlvlblt ", FormatInline(trimmed.Length > 1 ? trimmed[2..] : string.Empty), false);
                    index++;
                    continue;
                }

                if (s_orderedListRegex.IsMatch(trimmed))
                {
                    var content = s_orderedListRegex.Replace(trimmed, string.Empty);
                    AppendParagraph(builder, @"\pard\pnlvlbody ", FormatInline(content), false);
                    index++;
                    continue;
                }

                var paragraphLines = new List<string>();
                while (index < lines.Length)
                {
                    var currentLine = lines[index];
                    var currentTrimmed = currentLine.Trim();
                    if (string.IsNullOrWhiteSpace(currentLine) || currentTrimmed == "```" || currentTrimmed == "---" || currentTrimmed.StartsWith("# ", StringComparison.Ordinal) || currentTrimmed.StartsWith("## ", StringComparison.Ordinal) || currentTrimmed.StartsWith("### ", StringComparison.Ordinal) || currentTrimmed.StartsWith("> ", StringComparison.Ordinal) || currentTrimmed.StartsWith("- ", StringComparison.Ordinal) || currentTrimmed.StartsWith("* ", StringComparison.Ordinal) || currentTrimmed == "-" || currentTrimmed == "*" || s_orderedListRegex.IsMatch(currentTrimmed))
                    {
                        break;
                    }

                    paragraphLines.Add(currentTrimmed);
                    index++;
                }

                if (paragraphLines.Count > 0)
                {
                    var paragraphText = string.Join(" ", paragraphLines);
                    AppendParagraph(builder, @"\pard\sl276\slmult1 ", FormatInline(paragraphText), false);
                }
             }
 
             return builder.ToString();
         }

        private static int AppendCodeBlock(string[] lines, int index, StringBuilder builder)
        {
            var codeLines = new List<string>();
            while (index < lines.Length && lines[index].Trim() != "```")
            {
                codeLines.Add(lines[index]);
                index++;
            }

            var code = string.Join("\\line ", codeLines.ConvertAll(EscapeText));
            builder.Append(@"\pard\sb120\sa120\f1\highlight1 ");
            builder.Append(code);
            builder.Append(@"\highlight0\f0\par");
            return index < lines.Length ? index + 1 : index;
        }

        private static void AppendParagraph(StringBuilder builder, string prefix, string content, bool resetHeading)
        {
            builder.Append(prefix);
            builder.Append(content);
            if (resetHeading)
                builder.Append(@"\b0\fs24");
            builder.Append(@"\par");
        }

        private static string FormatInline(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder();
            var index = 0;
            while (index < text.Length)
            {
                if (TryAppendDelimited(text, ref index, "***", builder, static inner => @"{\b {\i " + FormatInline(inner) + "}}"))
                    continue;

                if (TryAppendDelimited(text, ref index, "**", builder, static inner => @"{\b " + FormatInline(inner) + "}"))
                    continue;

                if (TryAppendCode(text, ref index, builder))
                    continue;

                if (TryAppendItalic(text, ref index, builder))
                    continue;

                builder.Append(EscapeText(text[index].ToString()));
                index++;
            }

            return builder.ToString();
        }

        private static bool TryAppendDelimited(string text, ref int index, string marker, StringBuilder builder, Func<string, string> formatter)
        {
            if (!text.AsSpan(index).StartsWith(marker, StringComparison.Ordinal))
                return false;

            var end = text.IndexOf(marker, index + marker.Length, StringComparison.Ordinal);
            if (end < 0)
                return false;

            builder.Append(formatter(text[(index + marker.Length)..end]));
            index = end + marker.Length;
            return true;
        }

        private static bool TryAppendCode(string text, ref int index, StringBuilder builder)
        {
            if (text[index] != '`')
                return false;

            var end = text.IndexOf('`', index + 1);
            if (end < 0)
                return false;

            builder.Append(@"{\f1\highlight1 ");
            builder.Append(EscapeText(text[(index + 1)..end]));
            builder.Append('}');
            index = end + 1;
            return true;
        }

        private static bool TryAppendItalic(string text, ref int index, StringBuilder builder)
        {
            if (text[index] != '*' || (index + 1 < text.Length && text[index + 1] == '*'))
                return false;

            var end = FindClosingItalic(text, index + 1);
            if (end < 0)
                return false;

            builder.Append(@"{\i ");
            builder.Append(FormatInline(text[(index + 1)..end]));
            builder.Append('}');
            index = end + 1;
            return true;
        }

        private static int FindClosingItalic(string text, int start)
        {
            for (var index = start; index < text.Length; index++)
            {
                if (text[index] == '*' && (index + 1 >= text.Length || text[index + 1] != '*'))
                    return index;
            }

            return -1;
        }

        private static string EscapeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                switch (ch)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '{':
                        builder.Append("\\{");
                        break;
                    case '}':
                        builder.Append("\\}");
                        break;
                    case '\t':
                        builder.Append("\\tab ");
                        break;
                    default:
                        if (ch > 127)
                            builder.Append($"\\u{(int)ch}?");
                        else
                            builder.Append(ch);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
