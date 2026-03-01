using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace SmrtPad.Helpers
{
    /// <summary>
    /// Converts between lightweight HTML and plain text for import/export workflows.
    /// </summary>
    public static class HtmlConverterHelper
    {
        private static readonly Regex s_brRegex = new("<\\s*br\\s*/?\\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_paragraphCloseRegex = new("<\\s*/\\s*p\\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_paragraphOpenRegex = new("<\\s*p(?:\\s+[^>]*)?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_listItemOpenRegex = new("<\\s*li(?:\\s+[^>]*)?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_listItemCloseRegex = new("<\\s*/\\s*li\\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex s_tagRegex = new("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex s_multiLineRegex = new("\\n{3,}", RegexOptions.Compiled);

        /// <summary>
        /// Converts HTML markup into plain text while preserving paragraph and line boundaries.
        /// </summary>
        public static string ToPlainText(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            string normalized = s_brRegex.Replace(html, "\n");
            normalized = s_paragraphCloseRegex.Replace(normalized, "\n\n");
            normalized = s_paragraphOpenRegex.Replace(normalized, string.Empty);
            normalized = s_listItemOpenRegex.Replace(normalized, "• ");
            normalized = s_listItemCloseRegex.Replace(normalized, "\n");
            normalized = s_tagRegex.Replace(normalized, string.Empty);

            normalized = WebUtility.HtmlDecode(normalized);
            normalized = normalized.Replace("\r", string.Empty);
            normalized = s_multiLineRegex.Replace(normalized, "\n\n");
            normalized = normalized.Trim();

            return normalized.Replace("\n", Environment.NewLine);
        }

        /// <summary>
        /// Converts plain text into a simple HTML document preserving line breaks and paragraphs.
        /// </summary>
        public static string FromPlainText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "<html><body></body></html>";
            }

            var sb = new StringBuilder();
            sb.Append("<html><body>");

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] paragraphs = normalized.Split("\n\n", StringSplitOptions.None);
            foreach (string paragraph in paragraphs)
            {
                string encoded = WebUtility.HtmlEncode(paragraph).Replace("\n", "<br/>");
                sb.Append("<p>").Append(encoded).Append("</p>");
            }

            sb.Append("</body></html>");
            return sb.ToString();
        }
    }
}
