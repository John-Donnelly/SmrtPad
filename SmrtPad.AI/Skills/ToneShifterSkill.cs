namespace SmrtPad.AI.Skills;

/// <summary>Supported tone-shift targets for AI rewriting.</summary>
public enum ToneTarget
{
    Professional,
    Casual,
}

/// <summary>Streams text rewritten into the requested tone.</summary>
public sealed class ToneShifterSkill
{
    private readonly AIDispatcher _dispatcher;

    public ToneShifterSkill(AIDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _dispatcher = dispatcher;
    }

    /// <summary>Builds a tone-shift prompt and streams the rewritten text.</summary>
    public Task ShiftToneAsync(
        string text,
        ToneTarget target,
        Action<string> onToken,
        Action onComplete,
        Action<Exception>? onError = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(onToken);
        ArgumentNullException.ThrowIfNull(onComplete);

        var prompt = target switch
        {
            ToneTarget.Professional => PromptTemplates.ToneProfessional(text),
            ToneTarget.Casual => PromptTemplates.ToneCasual(text),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported tone target.")
        };

        return _dispatcher.StreamResponseAsync(prompt, onToken, onComplete, onError, ct);
    }
}
