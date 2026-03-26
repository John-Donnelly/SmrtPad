using System.Text.RegularExpressions;

namespace SmrtPad.Helpers;

/// <summary>
/// Removes structural contamination that reasoning models emit around their actual answer:
/// preamble lines, code fences, closing remarks, and reasoning fragments that escape
/// the &lt;think&gt; block.
/// </summary>
internal static partial class ResponseCleaner
{
    // Lines that are purely a code fence delimiter (``` or ''', optionally with a language tag)
    [GeneratedRegex(@"^\s*(`{3,}|'{3,})\s*\w*\s*$", RegexOptions.Multiline)]
    private static partial Regex CodeFenceLine();

    // A short line (≤120 chars) ending with a colon — classic preamble pattern:
    // "Sure, here is your letter:", "Here is the draft:", "Of course:"
    [GeneratedRegex(@"^[^\n]{1,120}:\s*$", RegexOptions.Multiline)]
    private static partial Regex PreambleLine();

    // Closing remark patterns the model appends after the real content
    [GeneratedRegex(
        @"^(if you (need|have|want|require)|let me know|please (let me|feel free|contact)|feel free to|i hope this|this should|hope this helps|don't hesitate|should you (need|have|require)).*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ClosingRemarkLine();

    // Reasoning fragments that leak out of <think> — lines starting with thinking-style openers
    [GeneratedRegex(
        @"^(okay[,.]?|alright[,.]?|let('s| us) (see|think|break)|so[,] |right[,] |now[,] ).*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ReasoningLeakLine();

    /// <summary>
    /// Cleans <paramref name="text"/> by removing preamble, code fences, closing remarks,
    /// and reasoning-leak lines. Safe to call on the final accumulated answer text.
    /// </summary>
    public static string Clean(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Remove code fence delimiter lines first (``` python, ''', etc.)
        text = CodeFenceLine().Replace(text, string.Empty);

        var lines = text.Split('\n');
        var result = new List<string>(lines.Length);
        bool contentStarted = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimEnd('\r');

            if (!contentStarted)
            {
                // Skip blank lines and preamble before any real content has begun
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                if (PreambleLine().IsMatch(trimmed) || ReasoningLeakLine().IsMatch(trimmed))
                    continue;

                contentStarted = true;
            }

            // Always drop closing remarks regardless of position
            if (ClosingRemarkLine().IsMatch(trimmed))
                continue;

            result.Add(trimmed);
        }

        // Trim trailing blank lines
        while (result.Count > 0 && string.IsNullOrWhiteSpace(result[^1]))
            result.RemoveAt(result.Count - 1);

        return string.Join("\n", result);
    }
}
