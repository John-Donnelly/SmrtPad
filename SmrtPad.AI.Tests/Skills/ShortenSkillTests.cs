using SmrtPad.AI.Skills;

namespace SmrtPad.AI.Tests.Skills;

public sealed class ShortenSkillTests
{
    [Fact]
    public async Task ShortenAsync_InvokesDispatcherWithShortenPrompt()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new ShortenSkill(context.Dispatcher);

        await skill.ShortenAsync("A much longer sentence than needed.", _ => { }, () => { });

        Assert.Equal(PromptTemplates.Shorten("A much longer sentence than needed."), context.LastPrompt);
    }

    [Fact]
    public async Task ShortenAsync_PromptContainsInputText()
    {
        await using var context = new SkillDispatcherTestContext();
        var skill = new ShortenSkill(context.Dispatcher);

        await skill.ShortenAsync("A much longer sentence than needed.", _ => { }, () => { });

        Assert.Contains("A much longer sentence than needed.", context.LastPrompt!);
    }

    [Fact]
    public async Task ShortenAsync_OnTokenCallback_CalledForEachToken()
    {
        await using var context = new SkillDispatcherTestContext("one", "two");
        var skill = new ShortenSkill(context.Dispatcher);
        var tokens = new List<string>();

        await skill.ShortenAsync("A much longer sentence than needed.", tokens.Add, () => { });

        Assert.Equal(2, tokens.Count);
        Assert.Equal("one", tokens[0]);
        Assert.Equal("two", tokens[1]);
    }

    [Fact]
    public async Task ShortenAsync_Cancellation_PropagatedToDispatcher()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new ShortenSkill(context.Dispatcher);
        using var cts = new CancellationTokenSource();

        await skill.ShortenAsync("A much longer sentence than needed.", _ => { }, () => { }, ct: cts.Token);

        Assert.Equal(cts.Token, context.CapturedToken);
    }
}
