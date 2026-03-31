using Xunit;

namespace SmrtPad.UITests.Infrastructure;

/// <summary>
/// xUnit collection for AI model benchmark tests.
/// Uses a dedicated <see cref="BenchmarkAppFixture"/> that connects to a local
/// Appium server (127.0.0.1:4723) and discovers the installed AUMID automatically.
/// Runs sequentially to avoid overlapping Appium interactions.
/// </summary>
[CollectionDefinition("Benchmark", DisableParallelization = true)]
public sealed class BenchmarkCollection : ICollectionFixture<BenchmarkAppFixture> { }
