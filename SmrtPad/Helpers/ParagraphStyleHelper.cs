using System.Collections.Generic;

namespace SmrtPad.Helpers
{
    /// <summary>
    /// Defines paragraph style presets (font, size, bold/italic, alignment, spacing)
    /// used by the Styles dropdown in the ribbon.
    /// </summary>
    public static class ParagraphStyleHelper
    {
        public static readonly ParagraphStyleDefinition Normal = new(
            "Segoe UI", 11f, false, false, "Left", 0f, 0f);

        public static readonly ParagraphStyleDefinition Heading1 = new(
            "Segoe UI", 20f, true, false, "Left", 12f, 4f);

        public static readonly ParagraphStyleDefinition Heading2 = new(
            "Segoe UI", 16f, true, false, "Left", 10f, 3f);

        public static readonly ParagraphStyleDefinition Heading3 = new(
            "Segoe UI", 13f, true, false, "Left", 8f, 2f);

        public static readonly ParagraphStyleDefinition Subtitle = new(
            "Segoe UI", 14f, false, true, "Left", 6f, 4f);

        public static readonly ParagraphStyleDefinition Quote = new(
            "Segoe UI", 11f, false, true, "Left", 8f, 8f);

        /// <summary>
        /// Returns all built-in style definitions keyed by name.
        /// </summary>
        public static IReadOnlyDictionary<string, ParagraphStyleDefinition> All { get; } =
            new Dictionary<string, ParagraphStyleDefinition>
            {
                ["Normal"] = Normal,
                ["Heading1"] = Heading1,
                ["Heading2"] = Heading2,
                ["Heading3"] = Heading3,
                ["Subtitle"] = Subtitle,
                ["Quote"] = Quote,
            };
    }

    /// <summary>
    /// Immutable definition of a paragraph style preset.
    /// </summary>
    public sealed record ParagraphStyleDefinition(
        string FontName,
        float FontSize,
        bool Bold,
        bool Italic,
        string Alignment,
        float SpaceBefore,
        float SpaceAfter);
}
