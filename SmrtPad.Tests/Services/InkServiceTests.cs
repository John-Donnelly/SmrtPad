using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmrtPad.Services;
using Xunit;

namespace SmrtPad.Tests.Services;

public sealed class InkServiceTests
{
    private readonly InkService _service = new();

    [Fact]
    public async Task RecognizeAsync_WithNullStrokes_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.RecognizeAsync(null!));
    }

    [Fact]
    public async Task RecognizeAsync_WithEmptyStrokes_ReturnsEmptyString()
    {
        string result = await _service.RecognizeAsync(Array.Empty<Windows.UI.Input.Inking.InkStroke>());

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task RecognizeAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _service.RecognizeAsync(Array.Empty<Windows.UI.Input.Inking.InkStroke>(), cancellationTokenSource.Token));
    }
}
