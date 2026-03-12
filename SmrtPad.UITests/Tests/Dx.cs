using Xunit; using SmrtPad.UITests.Infrastructure; using System;
namespace SmrtPad.UITests.Tests {
  [Collection("UITests")]
    public class Dx {
    private readonly SharedAppFixture _fx;
    public Dx(SharedAppFixture fx) { _fx = fx; }

    /// <summary>Diagnostic: verifies the shared Appium session is live.</summary>
    [SkippableFact] public void Check() {
      Skip.If(!AppiumSession.IsAvailable(), "WinAppDriver / Appium not available.");
      Skip.If(_fx.Driver is null, "SmrtPad.exe not built or Appium session not started.");
      Assert.NotNull(_fx.Driver);
    }
  }
}
