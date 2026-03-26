namespace SmrtPad.AI.Skills;

/// <summary>Streams summarization output for a document selection.</summary>
public sealed class SummarizerSkill
{
    private readonly AIDispatcher _dispatcher;

    public SummarizerSkill(AIDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    /// <summary>Builds a summarization prompt and streams the model response.</summary>
    public Task SummarizeAsync(
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
            "summarize",
            text,
            onToken,
            onComplete,
            onError,
            ct);
    }
}
