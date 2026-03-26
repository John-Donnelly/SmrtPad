namespace SmrtPad.AI.Skills;

/// <summary>Streams AI-assisted clarity rewrites for selected text.</summary>
public sealed class AIRewriteSkill
{
    private readonly AIDispatcher _dispatcher;

    public AIRewriteSkill(AIDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    /// <summary>Builds a rewrite prompt and streams the rewritten text.</summary>
    public Task RewriteAsync(
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
            "rewrite",
            text,
            onToken,
            onComplete,
            onError,
            ct);
    }
}
