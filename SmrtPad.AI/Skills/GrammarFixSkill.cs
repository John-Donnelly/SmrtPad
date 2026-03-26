namespace SmrtPad.AI.Skills;

/// <summary>Streams grammar-corrected text for the current selection.</summary>
public sealed class GrammarFixSkill
{
    private readonly AIDispatcher _dispatcher;

    public GrammarFixSkill(AIDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    /// <summary>Builds a grammar-fix prompt and streams the corrected text.</summary>
    public Task FixGrammarAsync(
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
            "grammar",
            text,
            onToken,
            onComplete,
            onError,
            ct);
    }
}
