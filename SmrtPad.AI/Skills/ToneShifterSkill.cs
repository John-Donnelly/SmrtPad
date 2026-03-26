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

        if (target is not (ToneTarget.Professional or ToneTarget.Casual))
            throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported tone target.");

        var skillKey = target == ToneTarget.Professional ? "tone-professional" : "tone-casual";
        return _dispatcher.StreamResponseAsync(skillKey, text, onToken, onComplete, onError, ct);
    }
}
