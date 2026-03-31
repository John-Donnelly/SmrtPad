using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SmrtPad.UITests.Benchmark;

/// <summary>
/// Generates benchmark reports in three formats:
/// <list type="bullet">
///   <item>Markdown (.md) — human-readable summary tables</item>
///   <item>HTML (.html) — interactive dashboard with Chart.js visualizations</item>
///   <item>JSON (.json) — machine-readable data for LLM-as-judge assessment</item>
/// </list>
/// </summary>
public static class BenchmarkReportGenerator
{
    /// <summary>
    /// Generates all three report formats and returns the paths of generated files.
    /// </summary>
    public static ReportPaths GenerateAll(BenchmarkRunReport report, string outputDir)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(outputDir);

        Directory.CreateDirectory(outputDir);
        var timestamp = report.Timestamp.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        var mdPath = Path.Combine(outputDir, $"benchmark-report-{timestamp}.md");
        File.WriteAllText(mdPath, GenerateMarkdown(report));

        var htmlPath = Path.Combine(outputDir, $"benchmark-dashboard-{timestamp}.html");
        File.WriteAllText(htmlPath, GenerateHtml(report));

        var jsonPath = Path.Combine(outputDir, $"benchmark-results-{timestamp}.json");
        File.WriteAllText(jsonPath, GenerateJson(report));

        return new ReportPaths(mdPath, htmlPath, jsonPath);
    }

    // ── Markdown ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a Markdown report with summary and per-model detail tables.
    /// </summary>
    public static string GenerateMarkdown(BenchmarkRunReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# SmrtPad AI Model Benchmark Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {report.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Duration:** {report.TotalElapsed:hh\\:mm\\:ss}");
        sb.AppendLine($"**Models tested:** {report.ModelsRun.Count}");
        sb.AppendLine($"**Prompts per model:** {report.PromptsRun.Count}");
        sb.AppendLine($"**Total runs:** {report.Results.Count}");
        sb.AppendLine();

        // Summary table
        sb.AppendLine("## Model Summary");
        sb.AppendLine();
        sb.AppendLine("| Model | Target | Avg Score | Avg TPS | Success Rate | Avg Cost/Req | Total Time |");
        sb.AppendLine("|-------|--------|-----------|---------|--------------|--------------|------------|");

        foreach (var model in report.ModelsRun)
        {
            var results = report.Results.Where(r => r.ModelAlias == model).ToList();
            var scores = report.Scores.Where(s => s.ModelAlias == model).ToList();
            var costs = report.Costs.Where(c => c.ModelAlias == model).ToList();

            var successCount = results.Count(r => r.Succeeded);
            var successRate = results.Count > 0 ? (double)successCount / results.Count : 0;
            var avgScore = scores.Count > 0 ? scores.Average(s => s.OverallScore) : 0;
            var avgTps = results.Where(r => r.Succeeded && r.TokensPerSecond > 0)
                .Select(r => r.TokensPerSecond)
                .DefaultIfEmpty(0)
                .Average();
            var avgCost = costs.Count > 0 ? costs.Average(c => c.EstimatedCostUsd) : 0;
            var totalTime = results.Sum(r => r.ElapsedSeconds);
            var target = results.FirstOrDefault()?.ExecutionTarget ?? "N/A";

            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"| {model} | {target} | {avgScore:F3} | {avgTps:F1} | {successRate:P0} | ${avgCost:F6} | {totalTime:F0}s |"));
        }

        sb.AppendLine();

        // Per-skill breakdown
        sb.AppendLine("## Per-Skill Breakdown");
        sb.AppendLine();
        sb.AppendLine("| Skill | Avg Score | Avg TPS | Best Model | Worst Model |");
        sb.AppendLine("|-------|-----------|---------|------------|-------------|");

        var skills = report.PromptsRun.Select(p => p.SkillKey).Distinct().Order();
        foreach (var skill in skills)
        {
            var skillScores = report.Scores
                .Where(s => report.PromptsRun.Any(p => p.Id == s.PromptId && p.SkillKey == skill))
                .ToList();

            if (skillScores.Count == 0) continue;

            var avgScore = skillScores.Average(s => s.OverallScore);
            var skillResults = report.Results
                .Where(r => report.PromptsRun.Any(p => p.Id == r.PromptId && p.SkillKey == skill))
                .ToList();
            var avgTps = skillResults
                .Where(r => r.Succeeded && r.TokensPerSecond > 0)
                .Select(r => r.TokensPerSecond)
                .DefaultIfEmpty(0)
                .Average();

            var modelScores = skillScores
                .GroupBy(s => s.ModelAlias)
                .Select(g => (Model: g.Key, AvgScore: g.Average(s => s.OverallScore)))
                .OrderByDescending(x => x.AvgScore)
                .ToList();

            var best = modelScores.FirstOrDefault().Model ?? "N/A";
            var worst = modelScores.LastOrDefault().Model ?? "N/A";

            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"| {skill} | {avgScore:F3} | {avgTps:F1} | {best} | {worst} |"));
        }

        sb.AppendLine();

        // Model errors
        if (report.ModelErrors.Count > 0)
        {
            sb.AppendLine("## Model Errors");
            sb.AppendLine();
            foreach (var (model, error) in report.ModelErrors)
            {
                sb.AppendLine($"- **{model}**: {error}");
            }
            sb.AppendLine();
        }

        // Detailed results table
        sb.AppendLine("## Detailed Results");
        sb.AppendLine();
        sb.AppendLine("| Prompt | Model | Score | TPS | Output Tokens | Time (s) | Cost | Notes |");
        sb.AppendLine("|--------|-------|-------|-----|---------------|----------|------|-------|");

        foreach (var result in report.Results)
        {
            var score = report.Scores.FirstOrDefault(s => s.PromptId == result.PromptId && s.ModelAlias == result.ModelAlias);
            var cost = report.Costs.FirstOrDefault(c => c.PromptId == result.PromptId && c.ModelAlias == result.ModelAlias);

            var scoreText = score is not null ? score.OverallScore.ToString("F3", CultureInfo.InvariantCulture) : "N/A";
            var costText = cost is not null ? $"${cost.EstimatedCostUsd:F6}" : "N/A";
            var notes = score?.Notes ?? (result.ErrorMessage ?? "");
            // Truncate notes for table readability
            if (notes.Length > 60) notes = string.Concat(notes.AsSpan(0, 57), "...");

            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"| {result.PromptId} | {result.ModelAlias} | {scoreText} | {result.TokensPerSecond:F1} | {result.EstimatedOutputTokens} | {result.ElapsedSeconds:F1} | {costText} | {notes} |"));
        }

        return sb.ToString();
    }

    // ── HTML Dashboard ───────────────────────────────────────────────────────

    /// <summary>
    /// Generates a self-contained HTML dashboard with Chart.js visualizations.
    /// </summary>
    public static string GenerateHtml(BenchmarkRunReport report)
    {
        var modelLabels = JsonSerializer.Serialize(report.ModelsRun);

        // Compute per-model averages
        var avgScores = new List<double>();
        var avgTps = new List<double>();
        var avgCosts = new List<double>();
        var successRates = new List<double>();

        foreach (var model in report.ModelsRun)
        {
            var results = report.Results.Where(r => r.ModelAlias == model).ToList();
            var scores = report.Scores.Where(s => s.ModelAlias == model).ToList();
            var costs = report.Costs.Where(c => c.ModelAlias == model).ToList();

            avgScores.Add(scores.Count > 0 ? scores.Average(s => s.OverallScore) : 0);
            avgTps.Add(results.Where(r => r.Succeeded && r.TokensPerSecond > 0)
                .Select(r => r.TokensPerSecond).DefaultIfEmpty(0).Average());
            avgCosts.Add(costs.Count > 0 ? costs.Average(c => c.EstimatedCostUsd) * 1_000_000 : 0); // scale for visibility
            successRates.Add(results.Count > 0 ? (double)results.Count(r => r.Succeeded) / results.Count * 100 : 0);
        }

        var avgScoresJson = JsonSerializer.Serialize(avgScores.Select(v => Math.Round(v, 4)));
        var avgTpsJson = JsonSerializer.Serialize(avgTps.Select(v => Math.Round(v, 1)));
        var avgCostsJson = JsonSerializer.Serialize(avgCosts.Select(v => Math.Round(v, 2)));
        var successRatesJson = JsonSerializer.Serialize(successRates.Select(v => Math.Round(v, 1)));

        // Per-skill data for radar chart
        var skillKeys = report.PromptsRun.Select(p => p.SkillKey).Distinct().Order().ToList();
        var skillLabelsJson = JsonSerializer.Serialize(skillKeys);

        // Pick top 5 models by overall score for radar chart
        var topModels = report.ModelsRun
            .Select(m => (Model: m, Avg: report.Scores.Where(s => s.ModelAlias == m).Select(s => s.OverallScore).DefaultIfEmpty(0).Average()))
            .OrderByDescending(x => x.Avg)
            .Take(5)
            .Select(x => x.Model)
            .ToList();

        var radarDatasets = new StringBuilder();
        var colors = new[] { "#FF6384", "#36A2EB", "#FFCE56", "#4BC0C0", "#9966FF" };
        for (var i = 0; i < topModels.Count; i++)
        {
            var model = topModels[i];
            var skillScores = new List<double>();
            foreach (var skill in skillKeys)
            {
                var sScores = report.Scores
                    .Where(s => s.ModelAlias == model &&
                        report.PromptsRun.Any(p => p.Id == s.PromptId && p.SkillKey == skill))
                    .ToList();
                skillScores.Add(sScores.Count > 0 ? Math.Round(sScores.Average(s => s.OverallScore), 3) : 0);
            }
            var dataJson = JsonSerializer.Serialize(skillScores);
            if (radarDatasets.Length > 0) radarDatasets.Append(',');
            radarDatasets.Append(CultureInfo.InvariantCulture,
                $$"""
                {label:'{{model}}',data:{{dataJson}},borderColor:'{{colors[i]}}',backgroundColor:'{{colors[i]}}33',fill:true}
                """);
        }

        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>SmrtPad AI Benchmark Dashboard</title>
            <script src="https://cdn.jsdelivr.net/npm/chart.js@4"></script>
            <style>
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #0d1117; color: #c9d1d9; padding: 20px; }
                h1 { color: #58a6ff; margin-bottom: 8px; }
                .meta { color: #8b949e; margin-bottom: 24px; font-size: 14px; }
                .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(500px, 1fr)); gap: 20px; margin-bottom: 24px; }
                .card { background: #161b22; border: 1px solid #30363d; border-radius: 8px; padding: 16px; }
                .card h2 { color: #58a6ff; font-size: 16px; margin-bottom: 12px; }
                canvas { max-height: 350px; }
                table { width: 100%; border-collapse: collapse; font-size: 13px; }
                th, td { padding: 8px 12px; border-bottom: 1px solid #21262d; text-align: left; }
                th { color: #58a6ff; font-weight: 600; }
                tr:hover { background: #1c2129; }
                .score-good { color: #3fb950; }
                .score-mid { color: #d29922; }
                .score-bad { color: #f85149; }
                .kpi-row { display: flex; gap: 16px; margin-bottom: 24px; flex-wrap: wrap; }
                .kpi { background: #161b22; border: 1px solid #30363d; border-radius: 8px; padding: 16px 24px; text-align: center; min-width: 150px; }
                .kpi-value { font-size: 28px; font-weight: bold; color: #58a6ff; }
                .kpi-label { font-size: 12px; color: #8b949e; margin-top: 4px; }
            </style>
        </head>
        <body>
            <h1>SmrtPad AI Model Benchmark Dashboard</h1>
            <p class="meta">Generated: {{report.Timestamp:yyyy-MM-dd HH:mm:ss}} UTC | Duration: {{report.TotalElapsed:hh\:mm\:ss}} | Models: {{report.ModelsRun.Count}} | Prompts: {{report.PromptsRun.Count}}</p>

            <div class="kpi-row">
                <div class="kpi"><div class="kpi-value">{{report.ModelsRun.Count}}</div><div class="kpi-label">Models Tested</div></div>
                <div class="kpi"><div class="kpi-value">{{report.Results.Count}}</div><div class="kpi-label">Total Runs</div></div>
                <div class="kpi"><div class="kpi-value">{{report.Results.Count(r => r.Succeeded)}}</div><div class="kpi-label">Successful</div></div>
                <div class="kpi"><div class="kpi-value">{{report.Results.Count(r => !r.Succeeded)}}</div><div class="kpi-label">Failed</div></div>
                <div class="kpi"><div class="kpi-value">{{report.TotalElapsed:hh\:mm\:ss}}</div><div class="kpi-label">Total Time</div></div>
            </div>

            <div class="grid">
                <div class="card">
                    <h2>Quality Score by Model</h2>
                    <canvas id="scoreChart"></canvas>
                </div>
                <div class="card">
                    <h2>Tokens/Second by Model</h2>
                    <canvas id="tpsChart"></canvas>
                </div>
                <div class="card">
                    <h2>Success Rate by Model</h2>
                    <canvas id="successChart"></canvas>
                </div>
                <div class="card">
                    <h2>Skill Scores (Top 5 Models)</h2>
                    <canvas id="radarChart"></canvas>
                </div>
            </div>

            <div class="card">
                <h2>Model Comparison Table</h2>
                <table id="summaryTable">
                    <thead><tr><th>Model</th><th>Target</th><th>Avg Score</th><th>Avg TPS</th><th>Success</th><th>Avg Cost</th></tr></thead>
                    <tbody></tbody>
                </table>
            </div>

            <script>
                const labels = {{modelLabels}};
                const scores = {{avgScoresJson}};
                const tps = {{avgTpsJson}};
                const costs = {{avgCostsJson}};
                const successRates = {{successRatesJson}};

                function scoreColor(v) { return v >= 0.7 ? '#3fb950' : v >= 0.4 ? '#d29922' : '#f85149'; }

                new Chart(document.getElementById('scoreChart'), {
                    type: 'bar',
                    data: { labels, datasets: [{ label: 'Avg Score', data: scores, backgroundColor: scores.map(scoreColor) }] },
                    options: { responsive: true, scales: { y: { min: 0, max: 1, ticks: { color: '#8b949e' } }, x: { ticks: { color: '#8b949e', maxRotation: 45 } } }, plugins: { legend: { display: false } } }
                });

                new Chart(document.getElementById('tpsChart'), {
                    type: 'bar',
                    data: { labels, datasets: [{ label: 'Tokens/sec', data: tps, backgroundColor: '#36A2EB' }] },
                    options: { responsive: true, scales: { y: { ticks: { color: '#8b949e' } }, x: { ticks: { color: '#8b949e', maxRotation: 45 } } }, plugins: { legend: { display: false } } }
                });

                new Chart(document.getElementById('successChart'), {
                    type: 'bar',
                    data: { labels, datasets: [{ label: 'Success %', data: successRates, backgroundColor: '#3fb950' }] },
                    options: { responsive: true, scales: { y: { min: 0, max: 100, ticks: { color: '#8b949e' } }, x: { ticks: { color: '#8b949e', maxRotation: 45 } } }, plugins: { legend: { display: false } } }
                });

                new Chart(document.getElementById('radarChart'), {
                    type: 'radar',
                    data: { labels: {{skillLabelsJson}}, datasets: [{{radarDatasets}}] },
                    options: { responsive: true, scales: { r: { min: 0, max: 1, ticks: { color: '#8b949e', backdropColor: 'transparent' }, grid: { color: '#30363d' }, angleLines: { color: '#30363d' }, pointLabels: { color: '#c9d1d9' } } }, plugins: { legend: { labels: { color: '#c9d1d9' } } } }
                });

                // Populate summary table
                const tbody = document.querySelector('#summaryTable tbody');
                labels.forEach((model, i) => {
                    const sc = scores[i];
                    const cls = sc >= 0.7 ? 'score-good' : sc >= 0.4 ? 'score-mid' : 'score-bad';
                    const row = document.createElement('tr');
                    row.innerHTML = `<td>${model}</td><td>GPU</td><td class="${cls}">${sc.toFixed(3)}</td><td>${tps[i].toFixed(1)}</td><td>${successRates[i].toFixed(0)}%</td><td>$${(costs[i]/1000000).toFixed(6)}</td>`;
                    tbody.appendChild(row);
                });
            </script>
        </body>
        </html>
        """;
    }

    // ── JSON ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a JSON file with all benchmark data for programmatic analysis.
    /// </summary>
    public static string GenerateJson(BenchmarkRunReport report) =>
        JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
}

/// <summary>
/// Paths to generated report files.
/// </summary>
public sealed record ReportPaths(string MarkdownPath, string HtmlPath, string JsonPath);
