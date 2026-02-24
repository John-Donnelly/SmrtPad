using Xunit; using SmrtPad.UITests.Infrastructure; using System;
namespace SmrtPad.UITests.Tests {
  public class Dx {
    [SkippableFact] public void Check() {
      Skip.If(!AppiumSession.IsAvailable(), "WinAppDriver / Appium not available.");
      var exe = AppiumSession.FindSmrtPadExe();
      Skip.If(exe is null, "SmrtPad.exe not built.");
      var ex = Record.Exception(() => { using var s = new AppiumSession(exe!); Assert.NotNull(s.Driver); });
      if (ex != null) throw new Exception($"{ex.GetType().Name}: {ex.Message}{Environment.NewLine}Inner: {ex.InnerException?.Message}", ex);
    }
  }
}
