using SmrtPad.AI.Skills;

namespace SmrtPad.AI.Tests.Skills;

public sealed class GrammarFixSkillTests
{
    [Fact]
    public async Task FixGrammarAsync_InvokesDispatcherWithGrammarPrompt()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new GrammarFixSkill(context.Dispatcher);

        await skill.FixGrammarAsync("teh sentence", _ => { }, () => { });

        Assert.Equal(PromptTemplates.GrammarFix("teh sentence"), context.LastPrompt);
    }

    [Fact]
    public async Task FixGrammarAsync_PromptContainsInputText()
    {
        await using var context = new SkillDispatcherTestContext();
        var skill = new GrammarFixSkill(context.Dispatcher);

        await skill.FixGrammarAsync("teh sentence", _ => { }, () => { });

        Assert.Contains("teh sentence", context.LastPrompt!);
    }

    [Fact]
    public async Task FixGrammarAsync_OnTokenCallback_CalledForEachToken()
    {
        await using var context = new SkillDispatcherTestContext("one", "two");
        var skill = new GrammarFixSkill(context.Dispatcher);
        var tokens = new List<string>();

        await skill.FixGrammarAsync("teh sentence", tokens.Add, () => { });

        Assert.Equal(2, tokens.Count);
        Assert.Equal("one", tokens[0]);
        Assert.Equal("two", tokens[1]);
    }

    [Fact]
    public async Task FixGrammarAsync_Cancellation_PropagatedToDispatcher()
    {
        await using var context = new SkillDispatcherTestContext("token");
        var skill = new GrammarFixSkill(context.Dispatcher);
        using var cts = new CancellationTokenSource();

        await skill.FixGrammarAsync("teh sentence", _ => { }, () => { }, ct: cts.Token);

        Assert.Equal(cts.Token, context.CapturedToken);
    }
}
