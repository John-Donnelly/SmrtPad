using System.Text;
using System.Text.Json;

namespace SmrtPad.AI.Benchmarks.Reporting;

/// <summary>
/// Generates a live benchmark dashboard: static HTML shell (written once) + a <c>.js</c>
/// data sidecar (written after every case). The shell injects the sidecar via a dynamic
/// <c>&lt;script&gt;</c> tag on a 5-second interval — this JSONP-style approach works on
/// <c>file://</c> origins where <c>fetch()</c> is blocked by CORS in Chrome and Edge.
/// </summary>
public static class BenchmarkDashboardGenerator
{
    /// <summary>
    /// Writes the JSON data sidecar (always) and the HTML shell (first call only).
    /// Returns the path to the HTML shell file.
    /// </summary>
    /// <param name="currentStatus">
    /// The current CLI progress line to display above the progress bar, e.g.
    /// "[42/73] Summarize a long document". Pass <c>null</c> when not streaming.
    /// </param>
    public static string Generate(BenchmarkRun run, int totalCases, string outputDir, string? currentStatus = null)
    {
        Directory.CreateDirectory(outputDir);
        var htmlPath = Path.Combine(outputDir, $"{run.RunId}-dashboard.html");
        var dataFileName = $"{run.RunId}-data.js";

        WriteDataScript(run, totalCases, Path.Combine(outputDir, dataFileName), currentStatus);

        if (!File.Exists(htmlPath))
            File.WriteAllText(htmlPath, BuildShellHtml(run.RunId, run.ModelAlias, run.BackendTarget, dataFileName));

        return htmlPath;
    }

    // ── JSON data sidecar ────────────────────────────────────────────────

    private static void WriteDataScript(BenchmarkRun run, int totalCases, string path, string? currentStatus)
    {
        const int threshold = BenchmarkReportGenerator.PassThreshold;
        var results = run.Results;
        int completed = results.Count;
        int remaining = Math.Max(0, totalCases - completed);
        int passed = results.Count(r => r.Evaluation.RuleScore >= threshold);
        int failed = completed - passed;
        double passRatePct = completed > 0 ? Math.Round(100.0 * passed / completed, 1) : 0;
        double avgRule = completed > 0 ? results.Average(r => r.Evaluation.RuleScore) : 0;
        var llmList = results.Where(r => r.Evaluation.LlmQualityScore.HasValue).ToList();
        double avgLlm = llmList.Count > 0 ? llmList.Average(r => r.Evaluation.LlmQualityScore!.Value) : 0;
        int totalTokens = results.Sum(r => r.TotalTokens);
        double elecCost = results.Sum(r => r.ElectricityCostUsd);
        double avgToksPerSec = completed > 0 ? results.Average(r => r.TokensPerSecond) : 0;
        var elapsed = completed > 0 ? DateTimeOffset.UtcNow - run.StartedAt : TimeSpan.Zero;
        double secondsPerCase = elapsed.TotalSeconds / Math.Max(1, completed);
        int etaSeconds = completed > 0 && remaining > 0 ? (int)(secondsPerCase * remaining) : 0;
        string currentModel = results.Count > 0 ? results[^1].ModelDisplayLabel : $"{run.ModelAlias} [{run.ReasoningTag}]";

        double avgCostPerToken = totalTokens > 0 ? elecCost / totalTokens : 0;
        double currentModelCost = results.Where(r => r.ModelDisplayLabel == currentModel).Sum(r => r.ElectricityCostUsd);

        // ── Per-model data for multi-model charts ────────────────────────
        var modelKeys = results.Select(r => r.ModelDisplayLabel).Distinct().ToList();
        var allSkillKeys = results.Select(r => r.Case.SkillKey).Distinct().OrderBy(k => k).ToList();
        var allCatKeys = results.Select(r => r.Case.Category.ToString()).Distinct().OrderBy(k => k).ToList();

        // Per-model: running average scores
        var modelRunAvgs = new Dictionary<string, List<double>>();
        // Per-model: skill averages
        var modelSkillAvgs = new Dictionary<string, List<double>>();
        // Per-model: pass/fail counts
        var modelPassed = new Dictionary<string, int>();
        var modelFailed = new Dictionary<string, int>();
        // Per-model: category pass/fail
        var modelCatPasses = new Dictionary<string, List<int>>();
        var modelCatFails = new Dictionary<string, List<int>>();
        // Per-model: case-level data
        var modelCaseData = new Dictionary<string, object>();

        foreach (var mk in modelKeys)
        {
            var mr = results.Where(r => r.ModelDisplayLabel == mk).ToList();

            // Running average
            var ra = new List<double>();
            double rSum = 0;
            for (int i = 0; i < mr.Count; i++)
            {
                rSum += mr[i].Evaluation.RuleScore;
                ra.Add(Math.Round(rSum / (i + 1), 1));
            }
            modelRunAvgs[mk] = ra;

            // Skill averages (aligned to allSkillKeys)
            var skillAvgs = new List<double>();
            foreach (var sk in allSkillKeys)
            {
                var s = mr.Where(r => r.Case.SkillKey == sk).ToList();
                skillAvgs.Add(s.Count > 0 ? Math.Round(s.Average(r => r.Evaluation.RuleScore), 1) : 0);
            }
            modelSkillAvgs[mk] = skillAvgs;

            // Pass/fail
            int mp = mr.Count(r => r.Evaluation.RuleScore >= threshold);
            modelPassed[mk] = mp;
            modelFailed[mk] = mr.Count - mp;

            // Category pass/fail (aligned to allCatKeys)
            var cp = new List<int>();
            var cf = new List<int>();
            foreach (var ck in allCatKeys)
            {
                var c = mr.Where(r => r.Case.Category.ToString() == ck).ToList();
                cp.Add(c.Count(r => r.Evaluation.RuleScore >= threshold));
                cf.Add(c.Count(r => r.Evaluation.RuleScore < threshold));
            }
            modelCatPasses[mk] = cp;
            modelCatFails[mk] = cf;

            // Case-level data for by-case charts
            modelCaseData[mk] = new
            {
                caseIds = mr.Select(r => r.Case.Id).ToList(),
                caseScores = mr.Select(r => r.Evaluation.RuleScore).ToList(),
                caseLatencies = mr.Select(r => r.LatencyMs).ToList(),
                caseToksPerSec = mr.Select(r => Math.Round(r.TokensPerSecond, 1)).ToList(),
                caseTtft = mr.Select(r => r.TimeToFirstTokenMs).ToList(),
                barColors = mr.Select(r =>
                    r.Evaluation.RuleScore >= threshold ? "#2ea043"
                    : r.Evaluation.RuleScore >= 60 ? "#d29922" : "#f85149").ToList(),
            };
        }

        // Per-model: the primary backend target (first seen per alias)
        var modelBackends = results
            .GroupBy(r => r.ModelDisplayLabel)
            .ToDictionary(g => g.Key, g => g.First().BackendTarget);

        var modelReasoningTags = results
            .GroupBy(r => r.ModelDisplayLabel)
            .ToDictionary(g => g.Key, g => g.First().ReasoningTag);

        // Per-model hardware tier tag ([GPU] / [CPU] / [NPU])
        static string HardwareTag(string backend)
        {
            var b = backend.ToUpperInvariant();
            if (b.Contains("NPU")) return "[NPU]";
            if (b.Contains("GPU")) return "[GPU]";
            return "[CPU]";
        }
        var modelHardwareTags = modelBackends.ToDictionary(
            kvp => kvp.Key, kvp => HardwareTag(kvp.Value));

        // Overall model comparison
        var modelGroups = results
            .GroupBy(r => r.ModelDisplayLabel)
            .OrderByDescending(g => g.Average(r => r.Evaluation.RuleScore))
            .ToList();

        // ── Detailed results table (grouped by model) ────────────────────
        var tableRows = new StringBuilder();
        var modelOrder = results.Select(r => (r.ModelDisplayLabel, r.ModelAlias, r.BackendTarget, r.ReasoningTag)).Distinct().ToList();
        foreach (var (grDisplay, grAlias, grBackend, grReasoning) in modelOrder)
        {
            var gr = results.Where(r => r.ModelDisplayLabel == grDisplay && r.BackendTarget == grBackend).ToList();
            int grPass = gr.Count(r => r.Evaluation.RuleScore >= threshold);
            double grElec = gr.Sum(r => r.ElectricityCostUsd);
            double grAvg = gr.Count > 0 ? gr.Average(r => r.Evaluation.RuleScore) : 0;
            int grTokens = gr.Sum(r => r.TotalTokens);
            double grCptok = grTokens > 0 ? grElec / grTokens : 0;
            double grPassPct = gr.Count > 0 ? 100.0 * grPass / gr.Count : 0;
            double grAvgTps = gr.Count > 0 ? gr.Average(r => r.TokensPerSecond) : 0;
            var grTag = HardwareTag(grBackend);
            tableRows.Append($"<tr class=\"model-group-header\" data-backend=\"{HtmlEncode(grBackend)}\" data-model=\"{HtmlEncode(grDisplay)}\">")
                .AppendLine($"<td colspan=\"15\" style=\"background:#21262d;color:#f0883e;font-weight:600;padding:8px 10px\">" +
                    $"{grTag} [{HtmlEncode(grReasoning)}] \U0001f4e6 {HtmlEncode(grAlias)} \u2013 {HtmlEncode(grBackend)}" +
                    $" &nbsp;|&nbsp; {gr.Count} cases" +
                    $" &nbsp;|&nbsp; Passed: {grPass}/{gr.Count} ({grPassPct:F0}%)" +
                    $" &nbsp;|&nbsp; Avg: {grAvg:F1}" +
                    $" &nbsp;|&nbsp; {grAvgTps:F1} tok/s" +
                    $" &nbsp;|&nbsp; Elec: \u00a3{grElec:F4}" +
                    $" &nbsp;|&nbsp; \u00a3/tok: {grCptok:F8}" +
                    $"</td></tr>");
            foreach (var r in gr)
            {
                var icon = r.Evaluation.RuleScore >= threshold ? "\u2705" : "\u274c";
                var scoreStyle = r.Evaluation.RuleScore >= threshold ? "color:#2ea043"
                               : r.Evaluation.RuleScore >= 60 ? "color:#d29922"
                               : "color:#f85149";
                var rTag = HardwareTag(r.BackendTarget);
                tableRows.Append($"<tr data-backend=\"{HtmlEncode(r.BackendTarget)}\" data-model=\"{HtmlEncode(r.ModelDisplayLabel)}\">")
                    .Append($"<td>{icon} {HtmlEncode(r.Case.Id)}</td>")
                    .Append($"<td>{HtmlEncode(r.Case.SkillKey)}</td>")
                    .Append($"<td>{HtmlEncode(Truncate(r.Case.Description, 50))}</td>")
                    .Append($"<td>{rTag} [{HtmlEncode(r.ReasoningTag)}] {HtmlEncode(r.ModelAlias)}</td>")
                    .Append($"<td>{HtmlEncode(r.BackendTarget)}</td>")
                    .Append($"<td class=\"num\" style=\"{scoreStyle}\">{r.Evaluation.RuleScore}/100</td>")
                    .Append($"<td class=\"num\">{r.LatencyMs}ms</td>")
                    .Append($"<td class=\"num\">{r.TimeToFirstTokenMs}ms</td>")
                    .Append($"<td class=\"num\">{r.TokensPerSecond:F1}</td>")
                    .Append($"<td class=\"num\">{r.EstimatedInputTokens}</td>")
                    .Append($"<td class=\"num\">{r.EstimatedOutputTokens}</td>")
                    .Append($"<td class=\"num\">{r.TotalTokens}</td>")
                    .Append($"<td class=\"num\">\u00a3{r.TokenCostUsd:F8}/tok</td>")
                    .Append($"<td class=\"num\">\u00a3{r.ElectricityCostUsd:F6}</td>")
                    .Append($"<td class=\"num\">{r.GenerationMs}ms</td>")
                    .AppendLine("</tr>");
            }
        }

        var data = new
        {
            updatedAt = DateTimeOffset.UtcNow.ToString("HH:mm:ss") + " UTC",
            currentStatus = currentStatus ?? string.Empty,
            completed,
            totalCases,
            passed,
            failed,
            remaining,
            progressPct = totalCases > 0 ? (int)(100.0 * completed / totalCases) : 0,
            avgRule = Math.Round(avgRule, 1),
            avgToksPerSec = Math.Round(avgToksPerSec, 1),
            totalTokens,
            avgCostPerToken,
            totalElecCost = Math.Round(elecCost, 4),
            totalCost = Math.Round(elecCost, 4),
            currentModelCost = Math.Round(currentModelCost, 6),
            elapsed = elapsed.ToString(@"hh\:mm\:ss"),
            etaSeconds,
            currentModel,
            threshold,
            done = completed >= totalCases && totalCases > 0,
            passRatePct,
            tableRows = tableRows.ToString(),
            // Per-model data
            modelKeys,
            modelBackends,       // alias → backendTarget (for filter matching in JS)
            modelHardwareTags,   // alias → [GPU]/[CPU]/[NPU]
            modelReasoningTags,
            modelCaseData,
            modelRunAvgs,
            skillLabels = allSkillKeys,
            modelSkillAvgs,
            modelPassed,
            modelFailed,
            categoryLabels = allCatKeys,
            modelCatPasses,
            modelCatFails,
            // Overall model comparison
            modelLabels = modelGroups.Select(g => g.Key).ToList(),
            modelAvgScores = modelGroups.Select(g => Math.Round(g.Average(r => r.Evaluation.RuleScore), 1)).ToList(),
            modelPassRates = modelGroups.Select(g =>
                Math.Round(100.0 * g.Count(r => r.Evaluation.RuleScore >= threshold) / Math.Max(1, g.Count()), 1)).ToList(),
            modelElecCosts = modelGroups.Select(g => Math.Round(g.Sum(r => r.ElectricityCostUsd), 4)).ToList(),
            modelAvgTps = modelGroups.Select(g => Math.Round(g.Average(r => r.TokensPerSecond), 1)).ToList(),
        };

        File.WriteAllText(path, "window.__benchData=" + JsonSerializer.Serialize(data) + ";");
    }

    // ── HTML shell (written once) ────────────────────────────────────────

    private static string BuildShellHtml(string runId, string modelAlias, string backendTarget, string dataFileName)
    {
        var js = BuildPollingScript(dataFileName);

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>Benchmark Dashboard — {{HtmlEncode(runId)}}</title>
<script src="https://cdn.jsdelivr.net/npm/chart.js@4"></script>
<style>
  :root{--bg:#0d1117;--card:#161b22;--border:#30363d;--text:#e6edf3;--muted:#8b949e;--green:#2ea043;--yellow:#d29922;--red:#f85149;--blue:#58a6ff}
  *{box-sizing:border-box;margin:0;padding:0}
  body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Helvetica,Arial,sans-serif;background:var(--bg);color:var(--text);padding:24px}
  h1{font-size:1.6rem;margin-bottom:4px}
  .subtitle{color:var(--muted);font-size:.85rem;margin-bottom:4px}
  #status-line{color:var(--text);font-size:.8rem;min-height:1.2em;margin-bottom:2px;font-family:monospace}
  #status{color:var(--muted);font-size:.75rem;margin-bottom:10px}
  .filter-bar{display:flex;gap:8px;margin-bottom:16px;align-items:center;flex-wrap:wrap}
  .filter-btn{background:var(--card);border:1px solid var(--border);border-radius:6px;color:var(--text);cursor:pointer;padding:6px 14px;font-size:.8rem;transition:background .15s,border-color .15s}
  .filter-btn.active{background:var(--blue);border-color:var(--blue);color:#0d1117;font-weight:600}
  .filter-btn[data-filter="gpu"].active{background:#3fb950;border-color:#3fb950}
  .filter-btn[data-filter="cpu"].active{background:#f0883e;border-color:#f0883e}
  .filter-btn[data-filter="npu"].active{background:#d2a8ff;border-color:#d2a8ff;color:#0d1117}
  .model-select{background:var(--card);border:1px solid var(--border);border-radius:6px;color:var(--text);padding:6px 10px;font-size:.8rem}
  .model-cb-row{display:flex;gap:10px;flex-wrap:wrap;margin-bottom:8px;align-items:center}
  .model-cb-row label{font-size:.78rem;color:var(--text);cursor:pointer;display:flex;align-items:center;gap:4px}
  .kpi-row{display:grid;grid-template-columns:repeat(auto-fit,minmax(120px,1fr));gap:10px;margin-bottom:24px}
  .kpi{background:var(--card);border:1px solid var(--border);border-radius:8px;padding:12px 10px;text-align:center}
  .kpi .value{font-size:1.6rem;font-weight:700}
  .kpi .label{color:var(--muted);font-size:.72rem;margin-top:4px}
  .charts{display:grid;grid-template-columns:repeat(auto-fit,minmax(480px,1fr));gap:16px;margin-bottom:16px}
  .charts-sm{display:grid;grid-template-columns:repeat(auto-fit,minmax(380px,1fr));gap:16px;margin-bottom:24px}
  .chart-card{background:var(--card);border:1px solid var(--border);border-radius:8px;padding:16px;overflow:hidden}
  .chart-card h3{font-size:.95rem;margin-bottom:8px}
  .chart-wrap{position:relative;overflow:hidden}
  table{width:100%;border-collapse:collapse;font-size:.8rem;margin-bottom:24px}
  th,td{border:1px solid var(--border);padding:6px 10px;text-align:left}
  th{background:var(--card);position:sticky;top:0;z-index:1}
  .num{text-align:right;font-variant-numeric:tabular-nums}
  .progress{height:6px;background:var(--border);border-radius:3px;margin-bottom:8px}
  .progress .bar{height:100%;background:var(--green);border-radius:3px;transition:width .4s}
  .model-group-header td{background:#21262d!important;border-top:2px solid var(--blue)}
  .hw-gpu{color:#3fb950;font-size:.75rem;font-weight:600}
  .hw-cpu{color:#f0883e;font-size:.75rem;font-weight:600}
  .hw-npu{color:#d2a8ff;font-size:.75rem;font-weight:600}
</style>
</head>
<body>
<h1>🏋️ AI Benchmark Dashboard</h1>
<p class="subtitle" id="subtitle">Run: {{HtmlEncode(runId)}} &middot; Model: {{HtmlEncode(modelAlias)}} &middot; Backend: {{HtmlEncode(backendTarget)}}</p>
<p id="status-line"></p>
<p id="status">Loading…</p>
<div class="filter-bar">
  <span style="color:var(--muted);font-size:.8rem">Backend:</span>
  <button class="filter-btn" data-filter="all" onclick="setFilter('all')">All</button>
  <button class="filter-btn" data-filter="gpu" onclick="setFilter('gpu')">GPU only</button>
  <button class="filter-btn" data-filter="cpu" onclick="setFilter('cpu')">CPU only</button>
  <button class="filter-btn" data-filter="npu" onclick="setFilter('npu')">NPU only</button>
  <span style="color:var(--muted);font-size:.8rem;margin-left:16px">By-case model:</span>
  <select id="caseModelSelect" class="model-select" onchange="onCaseModelChange()"><option value="">Loading…</option></select>
</div>
<div class="progress"><div class="bar" id="progressBar" style="width:0%"></div></div>

<div class="kpi-row">
  <div class="kpi"><div class="value" id="kpi-completed">—</div><div class="label">Completed</div></div>
  <div class="kpi"><div class="value" style="color:var(--green)" id="kpi-passed">—</div><div class="label" id="kpi-passed-lbl">Passed</div></div>
  <div class="kpi"><div class="value" style="color:var(--red)" id="kpi-failed">—</div><div class="label">Failed</div></div>
  <div class="kpi"><div class="value" id="kpi-passrate">—</div><div class="label">Pass Rate</div></div>
  <div class="kpi"><div class="value" id="kpi-avgrule">—</div><div class="label">Avg Rule Score</div></div>
  <div class="kpi"><div class="value" id="kpi-tokspersec">—</div><div class="label">Avg Tok/s</div></div>
  <div class="kpi"><div class="value" id="kpi-tokens">—</div><div class="label">Total Tokens</div></div>
  <div class="kpi"><div class="value" id="kpi-tokencost">—</div><div class="label">Avg £/Token</div></div>
  <div class="kpi"><div class="value" id="kpi-totalcost">—</div><div class="label">Total Cost (All)</div></div>
  <div class="kpi"><div class="value" id="kpi-modelcost">—</div><div class="label">Model Cost</div></div>
  <div class="kpi"><div class="value" id="kpi-elapsed">—</div><div class="label">Elapsed</div></div>
  <div class="kpi"><div class="value" id="kpi-eta">—</div><div class="label">ETA</div></div>
  <div class="kpi"><div class="value" id="kpi-model" style="font-size:1rem">—</div><div class="label">Current Model</div></div>
</div>

<div class="charts">
  <div class="chart-card"><h3>Rule Score by Case</h3><div class="chart-wrap" id="scoreWrap" style="height:200px"><canvas id="scoreChart"></canvas></div></div>
  <div class="chart-card"><h3>Throughput (tok/s) by Case</h3><div class="chart-wrap" id="tpsWrap" style="height:200px"><canvas id="tpsChart"></canvas></div></div>
  <div class="chart-card"><h3>Latency (ms) by Case</h3><div class="chart-wrap" id="latencyWrap" style="height:200px"><canvas id="latencyChart"></canvas></div></div>
  <div class="chart-card"><h3>TTFT (ms) by Case</h3><div class="chart-wrap" id="ttftWrap" style="height:200px"><canvas id="ttftChart"></canvas></div></div>
</div>
<div class="charts-sm">
  <div class="chart-card">
    <h3>Running Average Score <span style="font-size:.7rem;color:var(--muted)">(per model)</span></h3>
    <div class="model-cb-row" id="avgModelCbs"></div>
    <div class="chart-wrap" style="height:280px"><canvas id="avgChart"></canvas></div>
  </div>
  <div class="chart-card">
    <h3>Average Score by Skill <span style="font-size:.7rem;color:var(--muted)">(per model)</span></h3>
    <div class="model-cb-row" id="radarModelCbs"></div>
    <div class="chart-wrap" style="height:280px"><canvas id="radarChart"></canvas></div>
  </div>
  <div class="chart-card">
    <h3>Pass / Fail <span style="font-size:.7rem;color:var(--muted)">(per model)</span></h3>
    <div class="model-cb-row" id="pieModelCbs"></div>
    <div class="chart-wrap" style="height:280px"><canvas id="pieChart"></canvas></div>
  </div>
  <div class="chart-card">
    <h3>Results by Category <span style="font-size:.7rem;color:var(--muted)">(per model)</span></h3>
    <div class="model-cb-row" id="catModelCbs"></div>
    <div class="chart-wrap" style="height:280px"><canvas id="categoryChart"></canvas></div>
  </div>
  <div class="chart-card" id="modelCompareCard" style="display:none"><h3>Model Comparison</h3><div class="chart-wrap" style="height:280px"><canvas id="modelChart"></canvas></div></div>
</div>

<h3 style="margin-bottom:8px">Detailed Results</h3>
<div style="overflow-x:auto">
<table>
<thead><tr>
  <th>Case</th><th>Skill</th><th>Description</th><th>Model</th><th>Backend</th>
  <th>Rule Score</th><th>Latency</th><th>TTFT</th><th>Tok/s</th>
  <th>In Tokens</th><th>Out Tokens</th><th>Total Tokens</th>
  <th>£/Token</th><th>Elec Cost</th><th>Gen Time</th>
</tr></thead>
<tbody id="tableBody"></tbody>
</table>
</div>
{{js}}
</body>
</html>
""";
    }

    private static string BuildPollingScript(string dataFileName)
    {
        return
            "<script>\n"
          + "const DATA_FILE=" + JsonSerializer.Serialize(dataFileName) + ";\n"
          + @"
let sC,lC,rC,pC,tpsC,avgC,catC,modC,ttftC,pollTimer;
let _d=null; // latest data snapshot
const grid={color:'#30363d'};
const MC=['#58a6ff','#f0883e','#3fb950','#f778ba','#d2a8ff','#79c0ff','#ffa657','#ff7b72','#7ee787','#a5d6ff','#f2cc60','#b392f0'];
function set(id,v){const e=document.getElementById(id);if(e)e.textContent=v;}
function eta(s){if(s<=0)return'—';const h=Math.floor(s/3600),m=Math.floor((s%3600)/60),sec=Math.floor(s%60);return(h>0?h+'h ':'')+m+'m '+sec+'s';}
function getFilter(){return localStorage.getItem('benchFilter')||'all';}

// Returns true when a backendTarget string matches the active filter.
function backendMatch(be,f){
  be=(be||'').toLowerCase();
  if(f==='all') return true;
  if(f==='npu') return be.includes('npu');
  if(f==='gpu') return be.includes('gpu') && !be.includes('npu');
  return !be.includes('gpu'); // cpu (also catches npu-proxy / cpu combos)
}

// Returns subset of modelKeys that match the active filter using the modelBackends map.
function getFilteredModels(allKeys){
  const f=getFilter();
  if(f==='all') return allKeys;
  if(!_d||!_d.modelBackends) return allKeys;
  return allKeys.filter(m=>backendMatch(_d.modelBackends[m]||'',f));
}

function applyFilter(){
  const f=getFilter();
  document.querySelectorAll('[data-filter]').forEach(b=>b.classList.toggle('active',b.dataset.filter===f));
  document.querySelectorAll('#tableBody tr').forEach(row=>{
    const be=(row.dataset.backend||'').toLowerCase();
    const show=backendMatch(be,f);
    row.style.display=show?'':'none';
  });
  if(_d) rebuildCharts();
}
function setFilter(f){localStorage.setItem('benchFilter',f);applyFilter();}
function destroyCharts(){[sC,lC,rC,pC,tpsC,avgC,catC,modC,ttftC].forEach(c=>{if(c)c.destroy();});sC=lC=rC=pC=tpsC=avgC=catC=modC=ttftC=null;}
function getCaseModel(){return document.getElementById('caseModelSelect').value;}

// Build model checkboxes with persistence.
function buildModelCbs(containerId,models,onChange){
  const c=document.getElementById(containerId);if(!c)return;
  c.innerHTML='';
  models.forEach((m,i)=>{
    const id=containerId+'_'+i;
    const storedKey='benchCb_'+containerId+'_'+m;
    const stored=localStorage.getItem(storedKey);
    const checked=stored===null?true:(stored==='1');
    const lb=document.createElement('label');
    const cb=document.createElement('input');cb.type='checkbox';cb.checked=checked;cb.value=m;cb.id=id;
    cb.addEventListener('change',()=>{localStorage.setItem(storedKey,cb.checked?'1':'0');onChange();});
    const tag=_d&&_d.modelHardwareTags&&_d.modelHardwareTags[m]?_d.modelHardwareTags[m]:'';
    const dot=document.createElement('span');dot.style.cssText='display:inline-block;width:10px;height:10px;border-radius:50%;background:'+MC[i%MC.length];
    lb.appendChild(cb);lb.appendChild(dot);lb.appendChild(document.createTextNode(' '+tag+' '+m));
    c.appendChild(lb);
  });
}
function getCheckedModels(containerId){
  return Array.from(document.querySelectorAll('#'+containerId+' input:checked')).map(cb=>cb.value);
}

function onCaseModelChange(){if(_d)rebuildCaseCharts();}

function rebuildCaseCharts(){
  const d=_d;if(!d)return;
  const sel=getCaseModel();
  const md=d.modelCaseData[sel];
  if(!md)return;
  [sC,lC,tpsC,ttftC].forEach(c=>{if(c)c.destroy();});sC=lC=tpsC=ttftC=null;
  const barH=Math.max(200,md.caseIds.length*22)+'px';
  document.getElementById('scoreWrap').style.height=barH;
  document.getElementById('latencyWrap').style.height=barH;
  document.getElementById('tpsWrap').style.height=barH;
  document.getElementById('ttftWrap').style.height=barH;
  sC=new Chart(document.getElementById('scoreChart'),{type:'bar',data:{labels:md.caseIds,datasets:[{label:'Score',data:md.caseScores,backgroundColor:md.barColors}]},options:{indexAxis:'y',scales:{x:{min:0,max:100,grid:grid},y:{ticks:{font:{size:9}},grid:grid}},plugins:{legend:{display:false}},maintainAspectRatio:false}});
  tpsC=new Chart(document.getElementById('tpsChart'),{type:'bar',data:{labels:md.caseIds,datasets:[{label:'tok/s',data:md.caseToksPerSec,backgroundColor:'#3fb950'}]},options:{indexAxis:'y',scales:{x:{grid:grid},y:{ticks:{font:{size:9}},grid:grid}},plugins:{legend:{display:false}},maintainAspectRatio:false}});
  lC=new Chart(document.getElementById('latencyChart'),{type:'bar',data:{labels:md.caseIds,datasets:[{label:'ms',data:md.caseLatencies,backgroundColor:'#58a6ff'}]},options:{indexAxis:'y',scales:{x:{grid:grid},y:{ticks:{font:{size:9}},grid:grid}},plugins:{legend:{display:false}},maintainAspectRatio:false}});
  ttftC=new Chart(document.getElementById('ttftChart'),{type:'bar',data:{labels:md.caseIds,datasets:[{label:'TTFT ms',data:md.caseTtft,backgroundColor:'#d2a8ff'}]},options:{indexAxis:'y',scales:{x:{grid:grid},y:{ticks:{font:{size:9}},grid:grid}},plugins:{legend:{display:false}},maintainAspectRatio:false}});
}

function rebuildCharts(){
  const d=_d;if(!d)return;
  destroyCharts();

  // Filtered model list for the current backend filter
  const filteredKeys=getFilteredModels(d.modelKeys||[]);

  // Populate model dropdown — only filtered models, preserve selection if still available
  const sel=document.getElementById('caseModelSelect');
  const prev=sel.value;
  sel.innerHTML='';
  filteredKeys.forEach(m=>{const o=document.createElement('option');o.value=m;o.text=(d.modelHardwareTags&&d.modelHardwareTags[m]?d.modelHardwareTags[m]+' ':'')+m;sel.appendChild(o);});
  sel.value=filteredKeys.includes(prev)?prev:(filteredKeys.includes(d.currentModel)?d.currentModel:filteredKeys[0]||'');

  // Case-level charts for selected model
  rebuildCaseCharts();

  // Build model checkboxes for multi-model charts (filtered)
  buildModelCbs('avgModelCbs',filteredKeys,()=>rebuildMultiCharts());
  buildModelCbs('radarModelCbs',filteredKeys,()=>rebuildMultiCharts());
  buildModelCbs('pieModelCbs',filteredKeys,()=>rebuildMultiCharts());
  buildModelCbs('catModelCbs',filteredKeys,()=>rebuildMultiCharts());

  rebuildMultiCharts();
}

function rebuildMultiCharts(){
  const d=_d;if(!d)return;
  [avgC,rC,pC,catC,modC].forEach(c=>{if(c)c.destroy();});avgC=rC=pC=catC=modC=null;
  const mk=getFilteredModels(d.modelKeys||[]);

  // Running Average — per model overlay
  const avgSel=getCheckedModels('avgModelCbs');
  if(avgSel.length>0){
    const ds=avgSel.map((m,i)=>{
      const ci=mk.indexOf(m);
      const ra=d.modelRunAvgs[m]||[];
      const tag=d.modelHardwareTags&&d.modelHardwareTags[m]?d.modelHardwareTags[m]+' ':'';
      return{label:tag+m,data:ra,borderColor:MC[ci%MC.length],backgroundColor:'transparent',tension:.3,pointRadius:1,borderWidth:2};
    });
    const maxLen=Math.max(...ds.map(x=>x.data.length));
    const labels=Array.from({length:maxLen},(_,i)=>String(i+1));
    avgC=new Chart(document.getElementById('avgChart'),{type:'line',data:{labels:labels,datasets:ds},options:{scales:{x:{grid:grid,ticks:{font:{size:8},maxRotation:0}},y:{min:0,max:100,grid:grid}},plugins:{legend:{labels:{color:'#e6edf3',font:{size:10}}}},maintainAspectRatio:false}});
  }

  // Radar — skill averages per model
  const radarSel=getCheckedModels('radarModelCbs');
  if(d.skillLabels&&d.skillLabels.length>0&&radarSel.length>0){
    const ds=radarSel.map((m,i)=>{
      const ci=mk.indexOf(m);
      const tag=d.modelHardwareTags&&d.modelHardwareTags[m]?d.modelHardwareTags[m]+' ':'';
      return{label:tag+m,data:d.modelSkillAvgs[m]||[],borderColor:MC[ci%MC.length],backgroundColor:MC[ci%MC.length]+'22',pointBackgroundColor:MC[ci%MC.length],borderWidth:2};
    });
    rC=new Chart(document.getElementById('radarChart'),{type:'radar',data:{labels:d.skillLabels,datasets:ds},options:{scales:{r:{min:0,max:100,ticks:{stepSize:20,color:'#8b949e'},grid:grid,pointLabels:{color:'#e6edf3'}}},plugins:{legend:{labels:{color:'#e6edf3',font:{size:10}}}},maintainAspectRatio:false}});
  }

  // Pass/Fail — stacked bar per model
  const pieSel=getCheckedModels('pieModelCbs');
  if(pieSel.length>0){
    const taggedPieLabels=pieSel.map(m=>(d.modelHardwareTags&&d.modelHardwareTags[m]?d.modelHardwareTags[m]+' ':'')+m);
    const passData=pieSel.map(m=>d.modelPassed[m]||0);
    const failData=pieSel.map(m=>d.modelFailed[m]||0);
    pC=new Chart(document.getElementById('pieChart'),{type:'bar',data:{labels:taggedPieLabels,datasets:[{label:'Passed',data:passData,backgroundColor:'#2ea043',stack:'s'},{label:'Failed',data:failData,backgroundColor:'#f85149',stack:'s'}]},options:{indexAxis:'x',scales:{x:{grid:grid,ticks:{color:'#e6edf3',font:{size:9}}},y:{stacked:true,grid:grid}},plugins:{legend:{labels:{color:'#e6edf3'}}},maintainAspectRatio:false}});
  }

  // Category — grouped bar per model
  const catSel=getCheckedModels('catModelCbs');
  if(d.categoryLabels&&d.categoryLabels.length>0&&catSel.length>0){
    const ds=[];
    catSel.forEach((m,i)=>{
      const ci=mk.indexOf(m);
      const tag=d.modelHardwareTags&&d.modelHardwareTags[m]?d.modelHardwareTags[m]+' ':'';
      const cp=d.modelCatPasses[m]||[];
      ds.push({label:tag+m+' Pass',data:cp,backgroundColor:MC[ci%MC.length],stack:'s'+i});
      const cf=d.modelCatFails[m]||[];
      ds.push({label:tag+m+' Fail',data:cf,backgroundColor:MC[ci%MC.length]+'66',stack:'s'+i});
    });
    catC=new Chart(document.getElementById('categoryChart'),{type:'bar',data:{labels:d.categoryLabels,datasets:ds},options:{scales:{x:{grid:grid,ticks:{color:'#e6edf3',font:{size:9}}},y:{stacked:true,grid:grid}},plugins:{legend:{labels:{color:'#e6edf3',font:{size:9}}}},maintainAspectRatio:false}});
  }

  // Model comparison — filter by active backend filter
  const mc=document.getElementById('modelCompareCard');
  if(d.modelLabels&&d.modelLabels.length>1){
    const filteredLabels=getFilteredModels(d.modelLabels);
    if(filteredLabels.length>1){
      mc.style.display='';
      const labelIdx=filteredLabels.map(m=>d.modelLabels.indexOf(m));
      const taggedLabels=filteredLabels.map(m=>(d.modelHardwareTags&&d.modelHardwareTags[m]?d.modelHardwareTags[m]+' ':'')+m);
      modC=new Chart(document.getElementById('modelChart'),{type:'bar',data:{labels:taggedLabels,datasets:[{label:'Avg Score',data:labelIdx.map(i=>d.modelAvgScores[i]||0),backgroundColor:'#58a6ff'},{label:'Pass Rate %',data:labelIdx.map(i=>d.modelPassRates[i]||0),backgroundColor:'#2ea043'},{label:'Avg tok/s',data:labelIdx.map(i=>d.modelAvgTps[i]||0),backgroundColor:'#f0883e'}]},options:{scales:{x:{grid:grid,ticks:{color:'#e6edf3',font:{size:9}}},y:{min:0,grid:grid}},plugins:{legend:{labels:{color:'#e6edf3'}}},maintainAspectRatio:false}});
    } else { mc.style.display='none'; }
  }else{mc.style.display='none';}
}

function update(d){
  if(!d)return;
  _d=d;
  // Live status line: show current progress message; clear on completion
  set('status-line', d.done ? '' : (d.currentStatus||''));
  set('status', d.done ? '✅ Benchmark complete — ' + d.updatedAt : 'Updated: ' + d.updatedAt);
  document.getElementById('subtitle').innerHTML='Run: '+d.currentModel+' · Backend: '+(d.done?'Complete':'Running');
  document.getElementById('progressBar').style.width=d.progressPct+'%';
  set('kpi-completed',d.completed+'/'+d.totalCases);
  set('kpi-passed',d.passed);
  set('kpi-passed-lbl','Passed (≥'+d.threshold+')');
  set('kpi-failed',d.failed);
  set('kpi-passrate',d.passRatePct+'%');
  set('kpi-avgrule',d.avgRule+'/100');
  set('kpi-tokspersec',d.avgToksPerSec+' tok/s');
  set('kpi-tokens',d.totalTokens.toLocaleString());
  set('kpi-tokencost',d.avgCostPerToken>0?'£'+d.avgCostPerToken.toFixed(8)+'/tok':'—');
  set('kpi-totalcost','£'+d.totalCost.toFixed(4));
  set('kpi-modelcost','£'+d.currentModelCost.toFixed(4));
  set('kpi-elapsed',d.elapsed);
  set('kpi-eta',d.done?'—':eta(d.etaSeconds));
  set('kpi-model',d.currentModel||'—');
  document.getElementById('tableBody').innerHTML=d.tableRows;
  applyFilter();
  rebuildCharts();
  if(d.done&&pollTimer){clearInterval(pollTimer);pollTimer=null;}
}
function poll(){
  const old=document.getElementById('__dp');if(old)old.remove();
  window.__benchData=null;
  const s=document.createElement('script');
  s.id='__dp';
  s.src=DATA_FILE+'?_='+Date.now();
  s.onload=()=>{update(window.__benchData);};
  s.onerror=()=>{set('status','Waiting for data…');};
  document.head.appendChild(s);
}
applyFilter();
poll();
pollTimer=setInterval(poll,5000);
" + "</script>\n";
    }

    private static string HtmlEncode(string text) =>
        System.Net.WebUtility.HtmlEncode(text);

    private static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..maxLen] + "\u2026";
}
