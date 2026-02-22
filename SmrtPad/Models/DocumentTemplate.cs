namespace SmrtPad.Models;

/// <summary>A named document template that seeds a new editor tab with pre-formatted content.</summary>
/// <param name="Key">Unique identifier used internally (e.g. "blank", "letter").</param>
/// <param name="DisplayName">Human-readable title shown in the template picker.</param>
/// <param name="Description">Short description shown below the title.</param>
/// <param name="PlainContent">
///     Plain-text seed content placed into the editor when the template is applied.
///     Use \n for paragraph breaks; the editor will display each line as a paragraph.
/// </param>
public sealed record DocumentTemplate(
    string Key,
    string DisplayName,
    string Description,
    string PlainContent);
