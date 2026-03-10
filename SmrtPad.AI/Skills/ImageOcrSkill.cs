using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace SmrtPad.AI.Skills;

/// <summary>Abstraction over the Windows OCR engine for unit testing.</summary>
public interface IOcrEngineAdapter
{
    /// <summary>Whether the OCR engine is available on the current device.</summary>
    bool IsAvailable { get; }

    /// <summary>Recognizes text from the supplied bitmap.</summary>
    Task<string> RecognizeAsync(SoftwareBitmap bitmap, CancellationToken ct);
}

internal sealed class ConcreteOcrEngineAdapter : IOcrEngineAdapter
{
    private readonly OcrEngine? _engine = OcrEngine.TryCreateFromUserProfileLanguages();

    public bool IsAvailable => _engine is not null;

    public async Task<string> RecognizeAsync(SoftwareBitmap bitmap, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ct.ThrowIfCancellationRequested();

        if (_engine is null)
            return string.Empty;

        var result = await _engine.RecognizeAsync(bitmap);
        ct.ThrowIfCancellationRequested();
        return result.Text;
    }
}

/// <summary>Runs OCR against a bitmap using the Windows OCR engine when available.</summary>
public sealed class ImageOcrSkill
{
    private readonly IOcrEngineAdapter _engine;

    public ImageOcrSkill(IOcrEngineAdapter? engine = null)
    {
        _engine = engine ?? new ConcreteOcrEngineAdapter();
    }

    /// <summary>Recognizes text from <paramref name="bitmap"/> or returns an empty string when OCR is unavailable.</summary>
    public async Task<string> RecognizeAsync(SoftwareBitmap? bitmap, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        ct.ThrowIfCancellationRequested();

        if (!_engine.IsAvailable)
            return string.Empty;

        try
        {
            return await _engine.RecognizeAsync(bitmap, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return string.Empty;
        }
    }
}
