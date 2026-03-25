namespace SmrtPad.AI.Skills;

/// <summary>Streams a concise inline completion for the text before the caret.</summary>
public sealed class AutoCompleteSkill
{
    private readonly AIDispatcher _dispatcher;

    public AutoCompleteSkill(AIDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    /// <summary>Builds an inline completion prompt and streams the generated continuation.</summary>
    public Task CompleteAsync(
        string textBeforeCaret,
        Action<string> onToken,
        Action onComplete,
        Action<Exception>? onError = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(textBeforeCaret);
        ArgumentNullException.ThrowIfNull(onToken);
        ArgumentNullException.ThrowIfNull(onComplete);

        return _dispatcher.StreamResponseAsync(
            PromptTemplates.AutoComplete(textBeforeCaret),
            onToken,
            onComplete,
            onError,
            ct);
    }
}
