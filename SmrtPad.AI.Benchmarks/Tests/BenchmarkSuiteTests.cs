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
            Environment.GetEnvironmentVariable("BENCHMARK_SCORE_THRESHOLD"), out var t) ? t : 60;
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
}
