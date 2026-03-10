using SmrtPad.AI.Skills;

namespace SmrtPad.AI.Tests.Skills;

public sealed class AIRewriteSkillTests
{
    [Fact]
    public async Task RewriteAsync_InvokesDispatcherWithRewritePrompt()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new AIRewriteSkill(context.Dispatcher);

        await skill.RewriteAsync("hello", _ => { }, () => { });

        Assert.Equal(PromptTemplates.Rewrite("hello"), context.LastPrompt);
    }

    [Fact]
    public async Task RewriteAsync_PromptContainsInputText()
    {
        await using var context = new SkillDispatcherTestContext();
        var skill = new AIRewriteSkill(context.Dispatcher);

        await skill.RewriteAsync("hello", _ => { }, () => { });

        Assert.Contains("hello", context.LastPrompt!);
    }

    [Fact]
    public async Task RewriteAsync_NullText_ThrowsArgumentNullException()
    {
        await using var context = new SkillDispatcherTestContext();
        var skill = new AIRewriteSkill(context.Dispatcher);

        await Assert.ThrowsAsync<ArgumentNullException>(() => skill.RewriteAsync(null!, _ => { }, () => { }));
    }

    [Fact]
    public async Task RewriteAsync_EmptyText_DoesNotThrow()
    {
        await using var context = new SkillDispatcherTestContext();
        var skill = new AIRewriteSkill(context.Dispatcher);

        await skill.RewriteAsync(string.Empty, _ => { }, () => { });

        Assert.Equal(PromptTemplates.Rewrite(string.Empty), context.LastPrompt);
    }

    [Fact]
    public async Task RewriteAsync_OnTokenCallback_CalledForEachToken()
    {
        await using var context = new SkillDispatcherTestContext("one", "two");
        var skill = new AIRewriteSkill(context.Dispatcher);
        var tokens = new List<string>();

        await skill.RewriteAsync("hello", tokens.Add, () => { });

        Assert.Equal(2, tokens.Count);
        Assert.Equal("one", tokens[0]);
        Assert.Equal("two", tokens[1]);
    }

    [Fact]
    public async Task RewriteAsync_OnCompleteCallback_CalledOnce()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new AIRewriteSkill(context.Dispatcher);
        var completeCallCount = 0;

        await skill.RewriteAsync("hello", _ => { }, () => completeCallCount++);

        Assert.Equal(1, completeCallCount);
    }

    [Fact]
    public async Task RewriteAsync_Cancellation_PropagatedToDispatcher()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new AIRewriteSkill(context.Dispatcher);
        using var cts = new CancellationTokenSource();

        await skill.RewriteAsync("hello", _ => { }, () => { }, ct: cts.Token);

        Assert.Equal(cts.Token, context.CapturedToken);
    }

    [Fact]
    public async Task RewriteAsync_DispatcherThrows_CallsOnError()
    {
        await using var context = new SkillDispatcherTestContext();
        var expected = new InvalidOperationException("stream failed");
        context.UseThrowingStream(expected);
        var skill = new AIRewriteSkill(context.Dispatcher);
        Exception? captured = null;

        await skill.RewriteAsync("hello", _ => { }, () => { }, ex => captured = ex);

        Assert.Same(expected, captured);
    }

    [Fact]
    public async Task RewriteAsync_DispatcherThrows_OnErrorNull_DoesNotThrow()
    {
        await using var context = new SkillDispatcherTestContext();
        context.UseThrowingStream(new InvalidOperationException("stream failed"));
        var skill = new AIRewriteSkill(context.Dispatcher);

        await skill.RewriteAsync("hello", _ => { }, () => { });

        Assert.Equal(1, context.StreamCallCount);
    }
}
