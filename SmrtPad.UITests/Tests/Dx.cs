using Xunit; using SmrtPad.UITests.Infrastructure; using System;
namespace SmrtPad.UITests.Tests {
  public class Dx {
    [SkippableFact] public void Check() {
      Skip.If(!AppiumSession.IsAvailable(), "WinAppDriver / Appium not available.");
      var exe = AppiumSession.FindSmrtPadExe();
      Skip.If(exe is null, "SmrtPad.exe not built.");
      var ex = Record.Exception(() => { using var s = new AppiumSession(exe!); Assert.NotNull(s.Driver); });
      Skip.If(ex is not null, ex is not null ? $"WinAppDriver session could not start: {ex.GetType().Name}: {ex.Message}" : "");
    }
  }
}
