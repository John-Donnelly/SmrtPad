using System.Threading;

namespace SmrtPad.Services.Licensing;

/// <summary>
/// Bitmask flags representing features that can be enabled or disabled.
/// Free tier grants <see cref="CoreEditor"/> only; Pro tier unlocks bits 1–8.
/// </summary>
[Flags]
public enum SmrtPadFeature : uint
{
    None           = 0,
    CoreEditor     = 1 << 0,   // Always granted
    SmartSidebar   = 1 << 1,   // Pro
    AISummarize    = 1 << 2,   // Pro
    AIToneShift    = 1 << 3,   // Pro
    SemanticSearch = 1 << 4,   // Pro
    ImageOCR       = 1 << 5,   // Pro
    AIRewrite      = 1 << 6,   // Pro
    InkAnalytics   = 1 << 7,   // Pro
    HWBadge        = 1 << 8,   // Pro
}

/// <summary>
/// Thread-safe, global feature-flag store. Defaults to Free tier (CoreEditor only).
/// </summary>
public static class FeatureFlags
{
    private const uint ProBitsMask = 0x1FEu; // bits 1–8

    private static volatile uint _activeFlags = (uint)SmrtPadFeature.CoreEditor;

    /// <summary>Returns <see langword="true"/> when all bits in <paramref name="feature"/> are active.</summary>
    public static bool IsEnabled(SmrtPadFeature feature) =>
        (_activeFlags & (uint)feature) == (uint)feature;

    /// <summary>Enables all Pro-tier feature bits (1–8). Idempotent.</summary>
    internal static void SetProFlags() => _activeFlags |= ProBitsMask;

    /// <summary>Clears Pro-tier bits, preserving <see cref="SmrtPadFeature.CoreEditor"/>.</summary>
    internal static void ClearProFlags() => _activeFlags = (uint)SmrtPadFeature.CoreEditor;

    /// <summary>Resets to default Free-tier state. Primarily for test isolation.</summary>
    internal static void Reset() => _activeFlags = (uint)SmrtPadFeature.CoreEditor;
}
