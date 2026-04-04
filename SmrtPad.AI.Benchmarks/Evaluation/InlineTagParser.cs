using System.Text.RegularExpressions;

namespace SmrtPad.AI.Benchmarks.Evaluation;

/// <summary>
/// Parses raw streamed output into separate answer, insert, and think segments,
/// mirroring the logic in SmartSidebar.ParseThinkingToken().
/// </summary>
internal sealed class InlineTagParser
{
    private readonly StringBuilder _answerBuilder = new();
    private readonly StringBuilder _insertBuilder = new();
    private readonly StringBuilder _thinkBuilder = new();
    private readonly StringBuilder _rawBuffer = new();
    private readonly StringBuilder _tagBuffer = new();

    private enum ParseState { Normal, PossibleTag, InsideInsert, InsideThink }
    private ParseState _state = ParseState.Normal;

    public void Feed(string token)
    {
        _rawBuffer.Append(token);

        foreach (char c in token)
        {
            switch (_state)
            {
                case ParseState.Normal:
                    if (c == '<')
                    {
                        _tagBuffer.Clear();
                        _tagBuffer.Append(c);
                        _state = ParseState.PossibleTag;
                    }
                    else
                    {
                        _answerBuilder.Append(c);
                    }
                    break;

                case ParseState.PossibleTag:
                    _tagBuffer.Append(c);
                    if (c == '>')
                    {
                        var tag = _tagBuffer.ToString();
                        if (tag.Equals("<insert>", StringComparison.OrdinalIgnoreCase))
                        {
                            _state = ParseState.InsideInsert;
                        }
                        else if (tag.Equals("<think>", StringComparison.OrdinalIgnoreCase))
                        {
                            _state = ParseState.InsideThink;
                        }
                        else if (tag.Equals("</insert>", StringComparison.OrdinalIgnoreCase) ||
                                 tag.Equals("</think>", StringComparison.OrdinalIgnoreCase))
                        {
                            _state = ParseState.Normal;
                        }
                        else
                        {
                            _answerBuilder.Append(tag);
                            _state = ParseState.Normal;
                        }
                        _tagBuffer.Clear();
                    }
                    else if (_tagBuffer.Length > 15)
                    {
                        // Not a tag we recognize — flush buffer to answer
                        _answerBuilder.Append(_tagBuffer);
                        _tagBuffer.Clear();
                        _state = ParseState.Normal;
                    }
                    break;

                case ParseState.InsideInsert:
                    if (c == '<')
                    {
                        _tagBuffer.Clear();
                        _tagBuffer.Append(c);
                    }
                    else if (_tagBuffer.Length > 0)
                    {
                        _tagBuffer.Append(c);
                        if (c == '>')
                        {
                            var tag = _tagBuffer.ToString();
                            if (tag.Equals("</insert>", StringComparison.OrdinalIgnoreCase))
                            {
                                _state = ParseState.Normal;
                            }
                            else
                            {
                                _insertBuilder.Append(tag);
                            }
                            _tagBuffer.Clear();
                        }
                        else if (_tagBuffer.Length > 15)
                        {
                            _insertBuilder.Append(_tagBuffer);
                            _tagBuffer.Clear();
                        }
                    }
                    else
                    {
                        _insertBuilder.Append(c);
                    }
                    break;

                case ParseState.InsideThink:
                    if (c == '<')
                    {
                        _tagBuffer.Clear();
                        _tagBuffer.Append(c);
                    }
                    else if (_tagBuffer.Length > 0)
                    {
                        _tagBuffer.Append(c);
                        if (c == '>')
                        {
                            var tag = _tagBuffer.ToString();
                            if (tag.Equals("</think>", StringComparison.OrdinalIgnoreCase))
                            {
                                _state = ParseState.Normal;
                            }
                            else
                            {
                                _thinkBuilder.Append(tag);
                            }
                            _tagBuffer.Clear();
                        }
                        else if (_tagBuffer.Length > 15)
                        {
                            _thinkBuilder.Append(_tagBuffer);
                            _tagBuffer.Clear();
                        }
                    }
                    else
                    {
                        _thinkBuilder.Append(c);
                    }
                    break;
            }
        }
    }

    public string GetRawOutput() => _rawBuffer.ToString();
    public string GetAnswerText() => _answerBuilder.ToString().Trim();
    public string? GetInsertContent() => _insertBuilder.Length > 0 ? _insertBuilder.ToString().Trim() : null;
    public string? GetThinkContent() => _thinkBuilder.Length > 0 ? _thinkBuilder.ToString().Trim() : null;
}

/// <summary>
/// Detects common model contamination patterns (preamble, closing remarks)
/// matching the patterns used in the production ResponseCleaner.
/// </summary>
internal static partial class ContaminationDetector
{
    [GeneratedRegex(@"^[^\n]{1,120}:\s*$", RegexOptions.Multiline)]
    private static partial Regex PreambleLine();

    [GeneratedRegex(
        @"^(okay[,.]?|alright[,.]?|let('s| us) (see|think|break)|so[,] |right[,] |now[,] ).*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ReasoningLeakLine();

    [GeneratedRegex(
        @"^(if you (need|have|want|require)|let me know|please (let me|feel free|contact)|feel free to|i hope this|this should|hope this helps|don't hesitate|should you (need|have|require)).*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ClosingRemarkLine();

    /// <summary>Returns true if the first non-blank line matches a preamble or reasoning leak pattern.</summary>
    public static bool HasPreamble(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line)) continue;
            return PreambleLine().IsMatch(line) || ReasoningLeakLine().IsMatch(line);
        }

        return false;
    }

    /// <summary>Returns true if any line in the text matches a closing remark pattern.</summary>
    public static bool HasClosingRemark(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (ClosingRemarkLine().IsMatch(line))
                return true;
        }

        return false;
    }
}
