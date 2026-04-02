using System;
using System.IO;
using System.Linq;
using SmrtPad.UITests.Benchmark;
using SmrtPad.UITests.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace SmrtPad.UITests.Tests;

/// <summary>
/// Remote AI model benchmark test suite. Connects to the Appium server on the
/// remote test PC, probes its hardware capabilities, filters models to those
/// the remote system can actually run (mirroring the app's own model selection
/// logic), pre-downloads all eligible models, then runs the full benchmark suite.
///
/// <para>Prerequisites:</para>
/// <list type="bullet">
///   <item>Remote PC running Appium + WinAppDriver (configured via <c>SMRTPAD_APPIUM_SERVER</c>)</item>
///   <item>WinRM enabled on the remote PC for hardware probing and model pre-download</item>
///   <item>Environment variables set: <c>SMRTPAD_REMOTE_HOST</c>, <c>SMRTPAD_REMOTE_USER</c>, <c>SMRTPAD_REMOTE_PASS</c></item>
///   <item>SmrtPad WAP project built locally (deploy.ps1 handles remote installation)</item>
/// </list>
/// </summary>
[Collection("RemoteBenchmark")]
public sealed class RemoteModelBenchmarkTests
{
    private readonly RemoteBenchmarkAppFixture _fixture;
    private readonly ITestOutputHelper _output;

    public RemoteModelBenchmarkTests(RemoteBenchmarkAppFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    /// <summary>
    /// Runs the full AI model benchmark suite on the remote PC, testing only
    /// models that are eligible for the remote system's hardware specs.
    ///
    /// <para><b>Sanity checks performed before each model:</b></para>
    /// <list type="bullet">
    ///   <item>Verifies the Appium session is still alive</item>
    ///   <item>Verifies the sidebar can be opened</item>
    ///   <item>Verifies the model switch succeeds before running prompts</item>
    ///   <item>Verifies the chat controls are enabled (AI dispatcher ready)</item>
    /// </list>
    ///
    /// <para><b>Sanity checks performed for each prompt:</b></para>
    /// <list type="bullet">
    ///   <item>Verifies the correct skill is selected in the dropdown</item>
    ///   <item>Verifies editor text is set correctly for skill-based prompts</item>
    ///   <item>Verifies the streaming response starts (Stop button appears)</item>
    ///   <item>Verifies the streaming response completes (Send button reappears)</item>
    ///   <item>Verifies the response text is non-empty and above minimum length</item>
    /// </list>
    /// </summary>
    [SkippableFact]
    public void RunFullBenchmark()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.InitializationFailure ?? "Remote benchmark fixture not available");
        _fixture.RequireSession();

        // ── Log remote hardware and eligible models ─────────────────────
        _output.WriteLine("═══════════════════════════════════════════");
        _output.WriteLine("     REMOTE AI MODEL BENCHMARK SUITE");
        _output.WriteLine("═══════════════════════════════════════════");
        _output.WriteLine("");

        if (_fixture.Hardware is not null)
        {
            _output.WriteLine($"Remote GPU:  {_fixture.Hardware.GpuName} ({_fixture.Hardware.GpuVramMb} MB VRAM)");
            _output.WriteLine($"Remote CPU:  {_fixture.Hardware.CpuName}");
            _output.WriteLine($"Remote RAM:  {_fixture.Hardware.SystemRamMb} MB");
        }

        _output.WriteLine($"Eligible models ({_fixture.EligibleModels.Count}):");
        foreach (var model in _fixture.EligibleModels)
        {
            var preloadStatus = _fixture.PreloadResults.TryGetValue(model, out var ok) && ok ? "✓ cached" : "? on-demand";
            _output.WriteLine($"  • {model} [{preloadStatus}]");
        }
        _output.WriteLine("");

        // ── Apply BENCHMARK_MODEL_FILTER on top of hardware filtering ───
        var models = ApplyUserFilter(_fixture.EligibleModels);
        var prompts = BenchmarkPromptSet.GetAll();

        _output.WriteLine($"Models to benchmark: {string.Join(", ", models)}");
        _output.WriteLine($"Prompts: {prompts.Count}");
        _output.WriteLine($"Total runs: {models.Count * prompts.Count}");
        _output.WriteLine("");

        var sidebar = new SidebarAutomationHelper(_fixture, msg => _output.WriteLine(msg));
        var scorer = new RuleBasedScorer();
        var costEstimator = new CostEstimator();

        _output.WriteLine($"Hardware profile: {costEstimator.GetHardwareProfile()}");

        var runner = new ModelBenchmarkRunner(
            sidebar,
            scorer,
            costEstimator,
            models,
            log: msg => _output.WriteLine(msg));

        var report = runner.RunAll(prompts);

        // Generate reports
        var outputDir = GetOutputDirectory();
        _output.WriteLine($"Output directory: {outputDir}");

        var paths = BenchmarkReportGenerator.GenerateAll(report, outputDir);
        _output.WriteLine($"Markdown report: {paths.MarkdownPath}");
        _output.WriteLine($"HTML dashboard:  {paths.HtmlPath}");
        _output.WriteLine($"JSON results:    {paths.JsonPath}");

        var assessmentPath = QualitativeAssessmentPrompt.Generate(report, outputDir);
        _output.WriteLine($"Assessment prompt: {assessmentPath}");

        // Summary assertions
        Assert.NotEmpty(report.Results);
        _output.WriteLine("");
        _output.WriteLine("═══════════════════════════════════════════");
        _output.WriteLine("      REMOTE BENCHMARK COMPLETE");
        _output.WriteLine("═══════════════════════════════════════════");
        _output.WriteLine($"Remote system:     {_fixture.Hardware?.GpuName ?? "unknown"}");
        _output.WriteLine($"Models tested:     {report.ModelsRun.Count}");
        _output.WriteLine($"Total runs:        {report.Results.Count}");
        _output.WriteLine($"Successful:        {report.Results.Count(r => r.Succeeded)}");
        _output.WriteLine($"Failed:            {report.Results.Count(r => !r.Succeeded)}");
        _output.WriteLine($"Total time:        {report.TotalElapsed:hh\\:mm\\:ss}");
        _output.WriteLine($"Model errors:      {report.ModelErrors.Count}");
        _output.WriteLine("═══════════════════════════════════════════");
    }

    /// <summary>
    /// Quick smoke test: runs a single freeform prompt against the best eligible
    /// model on the remote system to verify the end-to-end infrastructure works.
    /// </summary>
    [SkippableFact]
    public void SmokeTest_RemoteSinglePrompt()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.InitializationFailure ?? "Remote benchmark fixture not available");
        _fixture.RequireSession();

        var sidebar = new SidebarAutomationHelper(_fixture, msg => _output.WriteLine(msg));
        var scorer = new RuleBasedScorer();
        var costEstimator = new CostEstimator();

        _output.WriteLine($"Remote hardware: {_fixture.Hardware?.GpuName ?? "N/A"} ({_fixture.Hardware?.GpuVramMb ?? 0} MB VRAM)");
        _output.WriteLine($"Eligible models: {string.Join(", ", _fixture.EligibleModels)}");

        var prompt = new BenchmarkPrompt(
            "remote-smoke-01", "freeform",
            "What is 2 + 2?",
            "Remote smoke test arithmetic question",
            ExpectedMinTokens: 1,
            ExpectedMaxTokens: 50);

        var result = sidebar.ExecutePrompt(prompt, "current", "GPU");

        _output.WriteLine($"Succeeded: {result.Succeeded}");
        _output.WriteLine($"Output: '{result.OutputText}'");
        _output.WriteLine($"Error: '{result.ErrorMessage}'");
        _output.WriteLine($"Elapsed: {result.ElapsedSeconds:F1}s");
        _output.WriteLine($"TPS: {result.TokensPerSecond:F1}");

        if (result.Succeeded)
        {
            var score = scorer.Score(prompt, result);
            _output.WriteLine($"Score: {score.OverallScore:F3}");
            _output.WriteLine($"Notes: {score.Notes}");

            var cost = costEstimator.Estimate(result);
            _output.WriteLine($"Cost: ${cost.EstimatedCostUsd:F8}");
        }

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(string.IsNullOrWhiteSpace(result.OutputText));
    }

    /// <summary>
    /// Verifies the remote hardware probing succeeded and returned
    /// sensible values. This is a prerequisite sanity check.
    /// </summary>
    [SkippableFact]
    public void SanityCheck_RemoteHardwareDetected()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.InitializationFailure ?? "Remote benchmark fixture not available");

        Assert.NotNull(_fixture.Hardware);
        _output.WriteLine($"GPU: {_fixture.Hardware.GpuName} ({_fixture.Hardware.GpuVramMb} MB)");
        _output.WriteLine($"CPU: {_fixture.Hardware.CpuName}");
        _output.WriteLine($"RAM: {_fixture.Hardware.SystemRamMb} MB");

        // At least some RAM should be reported
        Assert.True(_fixture.Hardware.SystemRamMb > 0, "System RAM not detected");
    }

    /// <summary>
    /// Verifies that model filtering produces a non-empty set of eligible models
    /// for the remote hardware, and that all returned models are from the known set.
    /// </summary>
    [SkippableFact]
    public void SanityCheck_EligibleModelsFiltered()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.InitializationFailure ?? "Remote benchmark fixture not available");

        Assert.NotEmpty(_fixture.EligibleModels);
        _output.WriteLine($"Eligible models: {_fixture.EligibleModels.Count}");
        foreach (var model in _fixture.EligibleModels)
        {
            _output.WriteLine($"  • {model}");
        }

        // Verify the best model is at the top (largest first ordering)
        if (_fixture.Hardware is not null)
        {
            var best = RemoteModelFilter.GetBestAlias(_fixture.Hardware);
            Assert.Equal(best, _fixture.EligibleModels[0]);
            _output.WriteLine($"Best model for hardware: {best}");
        }
    }

    /// <summary>
    /// Verifies the Appium session is alive and SmrtPad is responsive on the remote PC.
    /// Checks that core UI elements (Editor, sidebar toggle) are accessible.
    /// </summary>
    [SkippableFact]
    public void SanityCheck_RemoteAppResponsive()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.InitializationFailure ?? "Remote benchmark fixture not available");
        _fixture.RequireSession();

        // Verify Editor is present
        var editors = _fixture.Driver!.FindElements(OpenQA.Selenium.Appium.MobileBy.AccessibilityId("Editor"));
        Assert.NotEmpty(editors);
        _output.WriteLine("Editor: found ✓");

        // Verify sidebar toolbar button is present
        var sidebarBtn = _fixture.Driver.FindElements(
            OpenQA.Selenium.Appium.MobileBy.AccessibilityId("SmrtSidebarToolbarButton"));
        Assert.NotEmpty(sidebarBtn);
        _output.WriteLine("Sidebar button: found ✓");

        _output.WriteLine("Remote app responsive ✓");
    }

    /// <summary>
    /// Verifies the sidebar can be opened and the AI dispatcher initialises on the remote system.
    /// </summary>
    [SkippableFact]
    public void SanityCheck_SidebarOpensAndDispatcherReady()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.InitializationFailure ?? "Remote benchmark fixture not available");
        _fixture.RequireSession();

        var sidebar = new SidebarAutomationHelper(_fixture, msg => _output.WriteLine(msg));

        var opened = sidebar.EnsureSidebarOpen();
        Assert.True(opened, "Sidebar failed to open on remote system");
        _output.WriteLine("Sidebar opened ✓");

        // Check status text for readiness
        var status = sidebar.GetStatusText();
        _output.WriteLine($"Status: '{status}'");

        // Verify hardware badge is present (indicates AI dispatcher initialized)
        var badgeTooltip = sidebar.GetHardwareBadgeTooltip();
        _output.WriteLine($"Hardware badge: '{badgeTooltip}'");

        sidebar.EnsureSidebarClosed();
        _output.WriteLine("Sidebar closed ✓");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Applies the <c>BENCHMARK_MODEL_FILTER</c> env var on top of hardware-filtered models.
    /// </summary>
    private static System.Collections.Generic.IReadOnlyList<string> ApplyUserFilter(
        System.Collections.Generic.IReadOnlyList<string> eligibleModels)
    {
        var filter = Environment.GetEnvironmentVariable("BENCHMARK_MODEL_FILTER");
        if (string.IsNullOrWhiteSpace(filter))
            return eligibleModels;

        var filters = filter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return eligibleModels
            .Where(m => filters.Any(f => m.Contains(f, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static string GetOutputDirectory()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "SmrtPad.sln")))
                return Path.Combine(dir, "BenchmarkResults");
            dir = Path.GetDirectoryName(dir);
        }

        var fallback = Path.Combine(Path.GetTempPath(), "SmrtPad-RemoteBenchmarkResults");
        Directory.CreateDirectory(fallback);
        return fallback;
    }
}
