# Copilot Instructions

## Project Guidelines
- In xUnit tests: do not use Assert.True() to check if a value exists in a collection — use Assert.Contains instead. Do not use Assert.Equal() to check for collection size — use Assert.Empty (count==0), Assert.Single (count==1), or Assert.Equal(n, collection.Count) for n>1.
- PowerShell remoting is allowed for this repository's remote UI test setup and troubleshooting.

### Benchmarks
- For live benchmarks in this repository, prefer the LLamaSharp runner instead of the ORT GenAI path when retrying benchmark execution.
- When models support thinking mode, run benchmarks in both thinking and no-thinking modes; execute GPU runs first (both modes), then CPU runs (both modes where applicable).