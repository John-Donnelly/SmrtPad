namespace SmrtPad.AI.Skills;

/// <summary>Streams shortened text for the current selection.</summary>
public sealed class ShortenSkill
{
    private readonly AIDispatcher _dispatcher;

    public ShortenSkill(AIDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    /// <summary>Builds a shortening prompt and streams the revised text.</summary>
    public Task ShortenAsync(
        string text,
        Action<string> onToken,
        Action onComplete,
        Action<Exception>? onError = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(onToken);
        ArgumentNullException.ThrowIfNull(onComplete);

        return _dispatcher.StreamResponseAsync(
            PromptTemplates.Shorten(text),
            onToken,
            onComplete,
            onError,
            ct);
    }
}
