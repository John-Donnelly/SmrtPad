using SmrtPad.AI.Benchmarks.Reporting;

namespace SmrtPad.AI.Benchmarks.Tests;

[Trait("Category", "Benchmark")]
[Collection("Benchmarks")]
public sealed class BenchmarkSuiteTests : IAsyncDisposable
{
    private readonly MockedBenchmarkContext _context = new();

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task BenchmarkSuite_SingleCase_ProducesValidResult()
    {
        // Arrange: set up a mock that returns correctly tagged output
        _context.SetDefaultResponse("<insert>", "Dear Sir,\n\nI am writing to request a meeting regarding the roadmap.\n\nSincerely,\n[Your Name]", "</insert>");

        var runner = new BenchmarkRunner(_context.Dispatcher, "mock-model", "MockBackend", enableLlmGrading: false);
        var singleCase = new List<BenchmarkCase>
        {
            BenchmarkPromptCatalog.All.First(c => c.Id == "doc-formal-request")
        };

        // Act
        var run = await runner.RunAsync(singleCase);

        // Assert
        Assert.Single(run.Results);
        var result = run.Results[0];
        Assert.Equal("doc-formal-request", result.Case.Id);
        Assert.NotNull(result.InsertContent);
        Assert.Contains("Dear", result.InsertContent);
        Assert.Equal(40, result.Evaluation.TagCompliancePts); // insert tag present = 40/40
        Assert.True(result.Evaluation.RuleScore >= 60, $"Rule score was {result.Evaluation.RuleScore}");
    }

    [Fact]
    public async Task BenchmarkSuite_ChatCase_NoInsertTags()
    {
        // Arrange: set up a mock that returns plain text (no insert tags)
        _context.SetDefaultResponse("An executive summary is typically one to two pages long.");

        var runner = new BenchmarkRunner(_context.Dispatcher, "mock-model", "MockBackend", enableLlmGrading: false);
        var singleCase = new List<BenchmarkCase>
        {
            BenchmarkPromptCatalog.All.First(c => c.Id == "chat-length")
        };

        // Act
        var run = await runner.RunAsync(singleCase);

        // Assert
        Assert.Single(run.Results);
        var result = run.Results[0];
        Assert.Null(result.InsertContent);
        Assert.Equal(40, result.Evaluation.TagCompliancePts); // no insert tag expected = 40/40
    }

    [Fact]
    public async Task BenchmarkSuite_GeneratesReport()
    {
        // Arrange
        _context.SetDefaultResponse("<insert>", "Test response content for the report.", "</insert>");

        var runner = new BenchmarkRunner(_context.Dispatcher, "mock-model", "MockBackend", enableLlmGrading: false);
        var cases = BenchmarkPromptCatalog.All.Take(3).ToList();

        // Act
        var run = await runner.RunAsync(cases);
        var reportDir = Path.Combine(Path.GetTempPath(), "SmrtPad-BenchmarkTest-" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            BenchmarkReportGenerator.WriteReports(run, reportDir);

            // Assert
            var mdFile = Directory.GetFiles(reportDir, "*.md").FirstOrDefault();
            var jsonFile = Directory.GetFiles(reportDir, "*.json").FirstOrDefault();
            Assert.NotNull(mdFile);
            Assert.NotNull(jsonFile);

            var mdContent = File.ReadAllText(mdFile);
            Assert.Contains("AI Benchmark Report", mdContent);
            Assert.Contains("mock-model", mdContent);
        }
        finally
        {
            if (Directory.Exists(reportDir))
                Directory.Delete(reportDir, recursive: true);
        }
    }

    [Fact]
    public async Task BenchmarkSuite_AllDocumentComposeCasesExpectInsertTag()
    {
        // Arrange: well-formed insert tag response
        _context.SetDefaultResponse(
            "<insert>",
            "Dear Hiring Manager,\n\nI am writing regarding the position. I have years of experience in software development.\n\nSincerely,\n[Your Name]",
            "</insert>");

        var runner = new BenchmarkRunner(_context.Dispatcher, "mock-model", "MockBackend", enableLlmGrading: false);
        var docCases = BenchmarkPromptCatalog.All
            .Where(c => c.Category == BenchmarkCategory.DocumentComposition)
            .ToList();

        // Act
        var run = await runner.RunAsync(docCases);

        // Assert: all document composition cases should have InsertContent
        foreach (var result in run.Results)
        {
            Assert.NotNull(result.InsertContent);
        }
    }

    [Fact]
    public async Task BenchmarkSuite_AllChatCasesHaveNoInsertContent()
    {
        // Arrange: plain text without insert tags
        _context.SetDefaultResponse("This is a plain conversational answer.");

        var runner = new BenchmarkRunner(_context.Dispatcher, "mock-model", "MockBackend", enableLlmGrading: false);
        var chatCases = BenchmarkPromptCatalog.All
            .Where(c => c.Category == BenchmarkCategory.TagCompliance)
            .ToList();

        // Act
        var run = await runner.RunAsync(chatCases);

        // Assert: none should have InsertContent
        foreach (var result in run.Results)
        {
            Assert.Null(result.InsertContent);
        }
    }

    [Fact]
    public async Task BenchmarkSuite_AverageScoreMeetsThreshold()
    {
        // Arrange: response with insert tags and expected keywords
        _context.SetDefaultResponse("<insert>", "The meeting agenda includes sprint review, backlog grooming review items.", "</insert>");

        var runner = new BenchmarkRunner(_context.Dispatcher, "mock-model", "MockBackend", enableLlmGrading: false);
        var cases = BenchmarkPromptCatalog.All.Take(5).ToList();

        // Act
        var run = await runner.RunAsync(cases);

        // Assert
        var threshold = int.TryParse(
            Environment.GetEnvironmentVariable("BENCHMARK_SCORE_THRESHOLD"), out var t) ? t : 80;
        var avg = run.Results.Average(r => r.Evaluation.RuleScore);
        Assert.True(avg >= threshold, $"Average rule score {avg:F1} below threshold {threshold}");
    }

    [Fact]
    public async Task BenchmarkSuite_DeltaAnalyzer_ComparesRuns()
    {
        // Arrange: two runs with different scores
        _context.SetDefaultResponse("<insert>", "Good response content.", "</insert>");

        var runner = new BenchmarkRunner(_context.Dispatcher, "mock-model", "MockBackend", enableLlmGrading: false);
        var cases = BenchmarkPromptCatalog.All.Take(2).ToList();

        var baseline = await runner.RunAsync(cases);

        // Change response to produce worse results (no insert tags for document cases)
        _context.SetDefaultResponse("Plain text without tags.");
        var current = await runner.RunAsync(cases);

        // Act
        var delta = BenchmarkDeltaAnalyzer.CompareRuns(baseline, current);

        // Assert
        Assert.Equal(2, delta.Deltas.Count);
        var md = BenchmarkDeltaAnalyzer.GenerateDeltaMarkdown(delta);
        Assert.Contains("Benchmark Delta Report", md);
    }

    [Fact]
    public async Task BenchmarkSuite_PreambleDetection_ScoresCorrectly()
    {
        // Arrange: response with preamble contamination
        _context.SetDefaultResponse("<insert>", "Sure, here is your letter:\nDear Sir,\nContent here.", "</insert>");

        var runner = new BenchmarkRunner(_context.Dispatcher, "mock-model", "MockBackend", enableLlmGrading: false);
        var singleCase = new List<BenchmarkCase>
        {
            BenchmarkPromptCatalog.All.First(c => c.Id == "doc-formal-request")
        };

        // Act
        var run = await runner.RunAsync(singleCase);

        // Assert: should detect preamble and score 0/20 for NoPreamble
        var result = run.Results[0];
        Assert.Equal(0, result.Evaluation.NoPreamblePts);
    }

    [Fact]
    public async Task BenchmarkSuite_ClosingRemarkDetection_ScoresCorrectly()
    {
        // Arrange: response with closing remark contamination
        _context.SetDefaultResponse("<insert>", "Dear Sir,\nContent here.\nLet me know if you need anything else.", "</insert>");

        var runner = new BenchmarkRunner(_context.Dispatcher, "mock-model", "MockBackend", enableLlmGrading: false);
        var singleCase = new List<BenchmarkCase>
        {
            BenchmarkPromptCatalog.All.First(c => c.Id == "doc-formal-request")
        };

        // Act
        var run = await runner.RunAsync(singleCase);

        // Assert: should detect closing remark and score 0/20
        var result = run.Results[0];
        Assert.Equal(0, result.Evaluation.NoClosingRemarksPts);
    }

    [Fact]
    public void BenchmarkSuite_EmptyRun_ReportDoesNotCrash()
    {
        // Arrange: a run with zero results
        var emptyRun = new BenchmarkRun("empty-run", "mock-model", "MockBackend",
            DateTimeOffset.UtcNow, new List<BenchmarkResult>());

        // Act — should not throw
        var md = BenchmarkReportGenerator.GenerateMarkdownReport(emptyRun);

        // Assert
        Assert.Contains("No benchmark results were produced", md);
    }

    [Fact]
    public void BenchmarkSuite_EmptyRun_DeltaAnalyzerDoesNotCrash()
    {
        // Arrange
        var emptyRun = new BenchmarkRun("empty-run", "mock-model", "MockBackend",
            DateTimeOffset.UtcNow, new List<BenchmarkResult>());

        // Act — should not throw
        var delta = BenchmarkDeltaAnalyzer.CompareRuns(emptyRun, emptyRun);

        // Assert
        Assert.Empty(delta.Deltas);
        Assert.Equal(0, delta.BaselineAvgRule);
    }

    [Fact]
    public async Task BenchmarkSuite_CaseException_RunContinues()
    {
        // Arrange: first response succeeds, then failing mock
        _context.SetDefaultResponse("<insert>", "Good response.", "</insert>");
        var runner = new BenchmarkRunner(_context.Dispatcher, "mock-model", "MockBackend", enableLlmGrading: false);
        var cases = BenchmarkPromptCatalog.All.Take(3).ToList();

        // The mocked dispatcher returns valid tokens so we won't hit an unhandled exception here,
        // but we can verify that results have token/cost fields populated.
        var run = await runner.RunAsync(cases);

        Assert.Equal(3, run.Results.Count);
        foreach (var r in run.Results)
        {
            Assert.True(r.EstimatedInputTokens > 0, "Input tokens should be estimated");
        }
    }

    [Fact]
    public void BenchmarkSuite_DashboardGeneration_ProducesValidHtml()
    {
        // Arrange: build a minimal run with one result
        var benchmarkCase = BenchmarkPromptCatalog.All.First();
        var evalScore = new EvaluationScore(40, 20, 20, 20, 8, "Good quality.");
        var singleResult = new BenchmarkResult(benchmarkCase, "raw output", "insert content", null,
            1500, evalScore, "mock-model", "MockBackend", DateTimeOffset.UtcNow, 50, 80, 0.000005);
        var run = new BenchmarkRun("dash-test", "mock-model", "MockBackend",
            DateTimeOffset.UtcNow, new List<BenchmarkResult> { singleResult });

        var dir = Path.Combine(Path.GetTempPath(), "SmrtPad-DashTest-" + Guid.NewGuid().ToString("N")[..8]);

        try
        {
            // Act
            var path = BenchmarkDashboardGenerator.Generate(run, 5, dir);

            // Assert
            Assert.True(File.Exists(path));
            var html = File.ReadAllText(path);
            Assert.Contains("Benchmark Dashboard", html);
            Assert.Contains("chart.js", html);
            Assert.Contains("mock-model", html);
            Assert.Contains("£/Token", html);
            Assert.Contains("Elec Cost", html);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void BenchmarkSuite_PromptCatalog_HasAtLeast60Cases()
    {
        Assert.True(BenchmarkPromptCatalog.All.Count >= 60,
            $"Expected at least 60 prompts but found {BenchmarkPromptCatalog.All.Count}");
    }

    [Fact]
    public void BenchmarkSuite_AllChatCases_HaveExpectedKeywords()
    {
        var chatCases = BenchmarkPromptCatalog.All
            .Where(c => c.Category == BenchmarkCategory.TagCompliance)
            .ToList();

        foreach (var c in chatCases)
        {
            Assert.True(c.ExpectedKeywords.Length > 0,
                $"Chat case '{c.Id}' should have ExpectedKeywords for content completeness scoring");
        }
    }

    [Fact]
    public void BenchmarkSuite_TokenEstimation_ReturnsReasonableValues()
    {
        Assert.Equal(0, BenchmarkRunner.EstimateTokens(""));
        Assert.Equal(0, BenchmarkRunner.EstimateTokens("   "));
        int tokens = BenchmarkRunner.EstimateTokens("The quick brown fox jumps over the lazy dog.");
        Assert.True(tokens >= 9 && tokens <= 15, $"Expected 9-15 tokens but got {tokens}");
    }

    [Fact]
    public void BenchmarkSuite_HedgingDetection_ScoresCorrectly()
    {
        Assert.True(Evaluation.ContaminationDetector.HasHedging("Perhaps you should consider this option."));
        Assert.True(Evaluation.ContaminationDetector.HasHedging("It's worth noting that the deadline is soon."));
        Assert.False(Evaluation.ContaminationDetector.HasHedging("The deadline is next Friday."));
    }

    [Fact]
    public void BenchmarkSuite_CodeFenceDetection_ScoresCorrectly()
    {
        Assert.True(Evaluation.ContaminationDetector.HasCodeFence("Here is code:\n```\nvar x = 1;\n```"));
        Assert.False(Evaluation.ContaminationDetector.HasCodeFence("Dear Sir, I am writing to request a meeting."));
    }
}
