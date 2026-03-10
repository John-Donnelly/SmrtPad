using SmrtPad.AI.Skills;

namespace SmrtPad.AI.Tests.Skills;

public sealed class ToneShifterSkillTests
{
    [Fact]
    public async Task ShiftToneAsync_Professional_UsesCorrectPromptTemplate()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new ToneShifterSkill(context.Dispatcher);

        await skill.ShiftToneAsync("hello", ToneTarget.Professional, _ => { }, () => { });

        Assert.Equal(PromptTemplates.ToneProfessional("hello"), context.LastPrompt);
    }

    [Fact]
    public async Task ShiftToneAsync_Casual_UsesCorrectPromptTemplate()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new ToneShifterSkill(context.Dispatcher);

        await skill.ShiftToneAsync("hello", ToneTarget.Casual, _ => { }, () => { });

        Assert.Equal(PromptTemplates.ToneCasual("hello"), context.LastPrompt);
    }

    [Fact]
    public async Task ShiftToneAsync_Professional_PromptContainsInputText()
    {
        await using var context = new SkillDispatcherTestContext();
        var skill = new ToneShifterSkill(context.Dispatcher);

        await skill.ShiftToneAsync("hello", ToneTarget.Professional, _ => { }, () => { });

        Assert.Contains("hello", context.LastPrompt!);
    }

    [Fact]
    public async Task ShiftToneAsync_Casual_PromptContainsInputText()
    {
        await using var context = new SkillDispatcherTestContext();
        var skill = new ToneShifterSkill(context.Dispatcher);

        await skill.ShiftToneAsync("hello", ToneTarget.Casual, _ => { }, () => { });

        Assert.Contains("hello", context.LastPrompt!);
    }

    [Fact]
    public async Task ShiftToneAsync_NullText_ThrowsArgumentNullException()
    {
        await using var context = new SkillDispatcherTestContext();
        var skill = new ToneShifterSkill(context.Dispatcher);

        await Assert.ThrowsAsync<ArgumentNullException>(() => skill.ShiftToneAsync(null!, ToneTarget.Professional, _ => { }, () => { }));
    }

    [Fact]
    public async Task ShiftToneAsync_EmptyText_DoesNotThrow()
    {
        await using var context = new SkillDispatcherTestContext();
        var skill = new ToneShifterSkill(context.Dispatcher);

        await skill.ShiftToneAsync(string.Empty, ToneTarget.Casual, _ => { }, () => { });

        Assert.Equal(PromptTemplates.ToneCasual(string.Empty), context.LastPrompt);
    }

    [Fact]
    public async Task ShiftToneAsync_OnTokenCallback_CalledForEachToken()
    {
        await using var context = new SkillDispatcherTestContext("one", "two");
        var skill = new ToneShifterSkill(context.Dispatcher);
        var tokens = new List<string>();

        await skill.ShiftToneAsync("hello", ToneTarget.Professional, tokens.Add, () => { });

        Assert.Equal(2, tokens.Count);
        Assert.Equal("one", tokens[0]);
        Assert.Equal("two", tokens[1]);
    }

    [Fact]
    public async Task ShiftToneAsync_OnCompleteCallback_CalledOnce()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new ToneShifterSkill(context.Dispatcher);
        var completeCallCount = 0;

        await skill.ShiftToneAsync("hello", ToneTarget.Professional, _ => { }, () => completeCallCount++);

        Assert.Equal(1, completeCallCount);
    }

    [Fact]
    public async Task ShiftToneAsync_Cancellation_PropagatedToDispatcher()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new ToneShifterSkill(context.Dispatcher);
        using var cts = new CancellationTokenSource();

        await skill.ShiftToneAsync("hello", ToneTarget.Casual, _ => { }, () => { }, ct: cts.Token);

        Assert.Equal(cts.Token, context.CapturedToken);
    }

    [Fact]
    public async Task ShiftToneAsync_DispatcherThrows_CallsOnError()
    {
        await using var context = new SkillDispatcherTestContext();
        var expected = new InvalidOperationException("stream failed");
        context.UseThrowingStream(expected);
        var skill = new ToneShifterSkill(context.Dispatcher);
        Exception? captured = null;

        await skill.ShiftToneAsync("hello", ToneTarget.Professional, _ => { }, () => { }, ex => captured = ex);

        Assert.Same(expected, captured);
    }

    [Fact]
    public async Task ShiftToneAsync_DispatcherThrows_OnErrorNull_DoesNotThrow()
    {
        await using var context = new SkillDispatcherTestContext();
        context.UseThrowingStream(new InvalidOperationException("stream failed"));
        var skill = new ToneShifterSkill(context.Dispatcher);

        await skill.ShiftToneAsync("hello", ToneTarget.Casual, _ => { }, () => { });

        Assert.Equal(1, context.StreamCallCount);
    }

    [Fact]
    public async Task ShiftToneAsync_ProfessionalAndCasual_UseDistinctPrompts()
    {
        await using var professionalContext = new SkillDispatcherTestContext();
        await using var casualContext = new SkillDispatcherTestContext();
        var professionalSkill = new ToneShifterSkill(professionalContext.Dispatcher);
        var casualSkill = new ToneShifterSkill(casualContext.Dispatcher);

        await professionalSkill.ShiftToneAsync("hello", ToneTarget.Professional, _ => { }, () => { });
        await casualSkill.ShiftToneAsync("hello", ToneTarget.Casual, _ => { }, () => { });

        Assert.NotEqual(professionalContext.LastPrompt, casualContext.LastPrompt);
    }
}
