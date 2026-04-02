cd B:\Source\repos\SmrtPad
$env:BENCHMARK_MODEL_FILTER = "phi-4-mini,qwen2.5-0.5b"
# No prompt limit — full run
dotnet test SmrtPad.UITests --filter "FullyQualifiedName~RunFullBenchmark" --no-build -s SmrtPad.UITests.runsettings -v n 2>&1 | Tee-Object -FilePath B:\Source\repos\SmrtPad\benchmark_smoke.log
