using SmrtPad.Services;
using System.IO;
using Xunit;

namespace SmrtPad.Tests.Services;

public sealed class SettingsServiceCrashTelemetryTests : IDisposable
{
    private readonly string _tempFile;
    private readonly SettingsService _sut;

    public SettingsServiceCrashTelemetryTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"smrtpad_crash_telemetry_{Guid.NewGuid()}.json");
        _sut = new SettingsService(_tempFile);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    [Fact]
    public void CrashTelemetryEnabled_DefaultValue_IsFalse()
    {
        Assert.False(_sut.CrashTelemetryEnabled);
    }

    [Fact]
    public void CrashTelemetryEnabled_SetTrue_Persists()
    {
        _sut.CrashTelemetryEnabled = true;
        _sut.Save();

        var reloaded = new SettingsService(_tempFile);
        Assert.True(reloaded.CrashTelemetryEnabled);
    }

    [Fact]
    public void CrashTelemetryEnabled_SetFalse_AfterTrue_Persists()
    {
        _sut.CrashTelemetryEnabled = true;
        _sut.Save();
        _sut.CrashTelemetryEnabled = false;
        _sut.Save();

        var reloaded = new SettingsService(_tempFile);
        Assert.False(reloaded.CrashTelemetryEnabled);
    }

    [Fact]
    public void CrashTelemetryConsentAsked_DefaultValue_IsFalse()
    {
        Assert.False(_sut.CrashTelemetryConsentAsked);
    }

    [Fact]
    public void CrashTelemetryConsentAsked_SetTrue_Persists()
    {
        _sut.CrashTelemetryConsentAsked = true;
        _sut.Save();

        var reloaded = new SettingsService(_tempFile);
        Assert.True(reloaded.CrashTelemetryConsentAsked);
    }
}
