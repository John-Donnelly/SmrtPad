using OpenQA.Selenium.Appium.Windows;

namespace SmrtPad.UITests.Infrastructure;

/// <summary>
/// Abstraction over the Appium session fixture used by <see cref="Benchmark.SidebarAutomationHelper"/>.
/// Implemented by both <see cref="BenchmarkAppFixture"/> (local) and
/// <see cref="RemoteBenchmarkAppFixture"/> (remote) so the sidebar helper and
/// benchmark runner work identically against either target machine.
/// </summary>
public interface IBenchmarkFixture
{
    /// <summary>The live Appium driver, or <c>null</c> if initialisation failed.</summary>
    WindowsDriver? Driver { get; }

    /// <summary>True when a live WinAppDriver session was established.</summary>
    bool IsAvailable { get; }

    /// <summary>Human-readable reason when <see cref="Driver"/> is <c>null</c>.</summary>
    string? InitializationFailure { get; }

    /// <summary>Returns <c>true</c> when the Appium session is pointing at a live window.</summary>
    bool IsSessionAlive();

    /// <summary>Skips the test if the Appium driver is unavailable or the session has died.</summary>
    void RequireSession();

    /// <summary>Attempts to restart the Appium session after a crash.</summary>
    bool TryRestartApp();

    /// <summary>Dismisses any blocking modal dialog currently on screen.</summary>
    void DismissAllBlockingDialogsIfPresent();

    /// <summary>Clears all text in the editor.</summary>
    void ClearEditor();

    /// <summary>Types text into the editor.</summary>
    void TypeInEditor(string text);

    /// <summary>Selects all text in the editor.</summary>
    void SelectAllInEditor();
}
