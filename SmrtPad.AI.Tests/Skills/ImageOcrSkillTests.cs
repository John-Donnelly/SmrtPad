using SmrtPad.AI.Skills;
using Windows.Graphics.Imaging;

namespace SmrtPad.AI.Tests.Skills;

public sealed class ImageOcrSkillTests
{
    [Fact]
    public async Task RecognizeAsync_NullBitmap_ThrowsArgumentNullException()
    {
        var skill = new ImageOcrSkill();

        await Assert.ThrowsAsync<ArgumentNullException>(() => skill.RecognizeAsync(null));
    }

    [Fact]
    public async Task RecognizeAsync_EngineUnavailable_ReturnsEmptyString()
    {
        var engine = new Mock<IOcrEngineAdapter>();
        engine.SetupGet(e => e.IsAvailable).Returns(false);
        var skill = new ImageOcrSkill(engine.Object);
        using var bitmap = CreateBitmap();

        var result = await skill.RecognizeAsync(bitmap);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task RecognizeAsync_EngineAvailable_ReturnsEngineResult()
    {
        var engine = new Mock<IOcrEngineAdapter>();
        engine.SetupGet(e => e.IsAvailable).Returns(true);
        engine.Setup(e => e.RecognizeAsync(It.IsAny<SoftwareBitmap>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("recognized text");
        var skill = new ImageOcrSkill(engine.Object);
        using var bitmap = CreateBitmap();

        var result = await skill.RecognizeAsync(bitmap);

        Assert.Equal("recognized text", result);
    }

    [Fact]
    public async Task RecognizeAsync_EngineThrows_ReturnsEmptyString()
    {
        var engine = new Mock<IOcrEngineAdapter>();
        engine.SetupGet(e => e.IsAvailable).Returns(true);
        engine.Setup(e => e.RecognizeAsync(It.IsAny<SoftwareBitmap>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ocr failed"));
        var skill = new ImageOcrSkill(engine.Object);
        using var bitmap = CreateBitmap();

        var result = await skill.RecognizeAsync(bitmap);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task RecognizeAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        var engine = new Mock<IOcrEngineAdapter>();
        engine.SetupGet(e => e.IsAvailable).Returns(true);
        var skill = new ImageOcrSkill(engine.Object);
        using var bitmap = CreateBitmap();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => skill.RecognizeAsync(bitmap, cts.Token));
    }

    [Fact]
    public async Task RecognizeAsync_EmptyOcrResult_ReturnsEmptyString()
    {
        var engine = new Mock<IOcrEngineAdapter>();
        engine.SetupGet(e => e.IsAvailable).Returns(true);
        engine.Setup(e => e.RecognizeAsync(It.IsAny<SoftwareBitmap>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);
        var skill = new ImageOcrSkill(engine.Object);
        using var bitmap = CreateBitmap();

        var result = await skill.RecognizeAsync(bitmap);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task RecognizeAsync_OcrResultWithWhitespaceOnly_ReturnsWhitespace()
    {
        var engine = new Mock<IOcrEngineAdapter>();
        engine.SetupGet(e => e.IsAvailable).Returns(true);
        engine.Setup(e => e.RecognizeAsync(It.IsAny<SoftwareBitmap>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("   ");
        var skill = new ImageOcrSkill(engine.Object);
        using var bitmap = CreateBitmap();

        var result = await skill.RecognizeAsync(bitmap);

        Assert.Equal("   ", result);
    }

    private static SoftwareBitmap CreateBitmap() => new(BitmapPixelFormat.Bgra8, 1, 1);
}
