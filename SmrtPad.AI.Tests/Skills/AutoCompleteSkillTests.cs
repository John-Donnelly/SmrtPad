using SmrtPad.AI.Skills;

namespace SmrtPad.AI.Tests.Skills;

public sealed class AutoCompleteSkillTests
{
    [Fact]
    public async Task CompleteAsync_InvokesDispatcherWithAutoCompletePrompt()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new AutoCompleteSkill(context.Dispatcher);

        await skill.CompleteAsync("Hello there", _ => { }, () => { });

        Assert.Equal(PromptTemplates.AutoComplete("Hello there"), context.LastPrompt);
    }

    [Fact]
    public async Task CompleteAsync_PromptContainsInputText()
    {
        await using var context = new SkillDispatcherTestContext();
        var skill = new AutoCompleteSkill(context.Dispatcher);

        await skill.CompleteAsync("Hello there", _ => { }, () => { });

        Assert.Contains("Hello there", context.LastPrompt!);
    }

    [Fact]
    public async Task CompleteAsync_OnTokenCallback_CalledForEachToken()
    {
        await using var context = new SkillDispatcherTestContext("one", "two");
        var skill = new AutoCompleteSkill(context.Dispatcher);
        var tokens = new List<string>();

        await skill.CompleteAsync("Hello there", tokens.Add, () => { });

        Assert.Equal(2, tokens.Count);
        Assert.Equal("one", tokens[0]);
        Assert.Equal("two", tokens[1]);
    }

    [Fact]
    public async Task CompleteAsync_Cancellation_PropagatedToDispatcher()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new AutoCompleteSkill(context.Dispatcher);
        using var cts = new CancellationTokenSource();

        await skill.CompleteAsync("Hello there", _ => { }, () => { }, ct: cts.Token);

        Assert.Equal(cts.Token, context.CapturedToken);
    }
}
