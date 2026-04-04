namespace SmrtPad.AI.Benchmarks;

/// <summary>Categories of benchmark cases.</summary>
public enum BenchmarkCategory
{
    /// <summary>Document composition via freeform skill — expects &lt;insert&gt; tags.</summary>
    DocumentComposition,

    /// <summary>Editing skills (summarize, rewrite, grammar, etc.) — expects &lt;insert&gt; tags.</summary>
    EditSkill,

    /// <summary>Conversational freeform questions — expects NO &lt;insert&gt; tags.</summary>
    TagCompliance,
}

/// <summary>A single benchmark test case.</summary>
public sealed record BenchmarkCase(
    string Id,
    string SkillKey,
    string InputText,
    string? DocumentStyle,
    string[] ExpectedKeywords,
    bool ExpectsInsertTag,
    string Description,
    BenchmarkCategory Category);
