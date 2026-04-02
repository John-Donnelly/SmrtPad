using Xunit;

namespace SmrtPad.UITests.Infrastructure;

/// <summary>
/// xUnit collection for remote AI model benchmark tests.
/// Uses <see cref="RemoteBenchmarkAppFixture"/> that connects to the remote
/// test PC, probes its hardware, filters models, pre-downloads them,
/// then runs benchmarks. Runs sequentially to avoid overlapping Appium interactions.
/// </summary>
[CollectionDefinition("RemoteBenchmark", DisableParallelization = true)]
public sealed class RemoteBenchmarkCollection : ICollectionFixture<RemoteBenchmarkAppFixture> { }
