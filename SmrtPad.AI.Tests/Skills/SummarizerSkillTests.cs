using SmrtPad.AI.Skills;

namespace SmrtPad.AI.Tests.Skills;

public sealed class SummarizerSkillTests
{
    [Fact]
    public async Task SummarizeAsync_InvokesDispatcherStreamResponseAsync()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new SummarizerSkill(context.Dispatcher);

        await skill.SummarizeAsync("hello", _ => { }, () => { });

        Assert.Equal(1, context.StreamCallCount);
    }

    [Fact]
    public async Task SummarizeAsync_PassesCorrectSummarizePrompt()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new SummarizerSkill(context.Dispatcher);

        await skill.SummarizeAsync("hello", _ => { }, () => { });

        Assert.Equal(PromptTemplates.Summarize("hello"), context.LastPrompt);
    }

    [Fact]
    public async Task SummarizeAsync_EmptyText_PassesEmptyPrompt_NoException()
    {
        await using var context = new SkillDispatcherTestContext();
        var skill = new SummarizerSkill(context.Dispatcher);

        await skill.SummarizeAsync(string.Empty, _ => { }, () => { });

        Assert.Equal(PromptTemplates.Summarize(string.Empty), context.LastPrompt);
    }

    [Fact]
    public async Task SummarizeAsync_NullText_ThrowsArgumentNullException()
    {
        await using var context = new SkillDispatcherTestContext();
        var skill = new SummarizerSkill(context.Dispatcher);

        await Assert.ThrowsAsync<ArgumentNullException>(() => skill.SummarizeAsync(null!, _ => { }, () => { }));
    }

    [Fact]
    public async Task SummarizeAsync_OnTokenCallback_CalledForEachToken()
    {
        await using var context = new SkillDispatcherTestContext("one", "two");
        var skill = new SummarizerSkill(context.Dispatcher);
        var tokens = new List<string>();

        await skill.SummarizeAsync("hello", tokens.Add, () => { });

        Assert.Equal(2, tokens.Count);
        Assert.Equal("one", tokens[0]);
        Assert.Equal("two", tokens[1]);
    }

    [Fact]
    public async Task SummarizeAsync_OnCompleteCallback_CalledOnce()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new SummarizerSkill(context.Dispatcher);
        var completeCallCount = 0;

        await skill.SummarizeAsync("hello", _ => { }, () => completeCallCount++);

        Assert.Equal(1, completeCallCount);
    }

    [Fact]
    public async Task SummarizeAsync_Cancellation_PropagatedToDispatcher()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new SummarizerSkill(context.Dispatcher);
        using var cts = new CancellationTokenSource();

        await skill.SummarizeAsync("hello", _ => { }, () => { }, ct: cts.Token);

        Assert.Equal(cts.Token, context.CapturedToken);
    }

    [Fact]
    public async Task SummarizeAsync_DispatcherThrows_CallsOnError()
    {
        await using var context = new SkillDispatcherTestContext();
        var expected = new InvalidOperationException("stream failed");
        context.UseThrowingStream(expected);
        var skill = new SummarizerSkill(context.Dispatcher);
        Exception? captured = null;

        await skill.SummarizeAsync("hello", _ => { }, () => { }, ex => captured = ex);

        Assert.Same(expected, captured);
    }

    [Fact]
    public async Task SummarizeAsync_DispatcherThrows_OnErrorNull_DoesNotThrow()
    {
        await using var context = new SkillDispatcherTestContext();
        context.UseThrowingStream(new InvalidOperationException("stream failed"));
        var skill = new SummarizerSkill(context.Dispatcher);

        await skill.SummarizeAsync("hello", _ => { }, () => { });

        Assert.Equal(1, context.StreamCallCount);
    }

    [Fact]
    public async Task SummarizeAsync_VeryLongText_DoesNotTruncatePrompt()
    {
        await using var context = new SkillDispatcherTestContext();
        var skill = new SummarizerSkill(context.Dispatcher);
        var longText = new string('x', 10_000);

        await skill.SummarizeAsync(longText, _ => { }, () => { });

        Assert.Contains(longText, context.LastPrompt!);
    }
}
