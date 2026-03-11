// Forces the xUnit test runner to execute ALL tests sequentially — no parallel
// test collections and no parallel threads — regardless of external runner settings.
//
// This is critical for SmrtPad.UITests because every SharedAppFixture / FreeTierAppFixture
// constructor calls AppiumSession.ClearStartupBlockers(), which terminates ALL running
// SmrtPad.exe processes.  Concurrent fixture creation kills each other's live sessions.
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
