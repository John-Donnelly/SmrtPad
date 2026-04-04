using System.Collections.Generic;

namespace SmrtPad.UITests.Benchmark;

/// <summary>
/// Extended prompt descriptor that adds insert-tag expectation and explicit
/// keyword list on top of the base <see cref="BenchmarkPrompt"/> schema.
/// </summary>
/// <param name="Prompt">The underlying benchmark prompt for Appium execution.</param>
/// <param name="ExpectsInsertTag">
/// <c>true</c> when the model is expected to wrap its output in <c>&lt;insert&gt;</c> tags
/// (document composition and edit-skill cases). <c>false</c> for conversational queries.
/// </param>
/// <param name="ExpectedKeywords">
/// Case-insensitive substrings that must appear somewhere in the response to pass the keyword check.
/// Maps to <c>BenchmarkCase.ExpectedKeywords</c> from the AI benchmarks catalog.
/// </param>
public sealed record AiBenchmarkPrompt(
    BenchmarkPrompt Prompt,
    bool ExpectsInsertTag,
    string[] ExpectedKeywords);

/// <summary>
/// Authoritative catalog of AI benchmark prompts for Appium-driven end-to-end testing.
/// Mirrors the 38 cases defined in <c>SmrtPad.AI.Benchmarks.BenchmarkPromptCatalog</c>
/// and is kept in sync manually when cases are added or changed there.
/// </summary>
public static class AiBenchmarkCatalog
{
    public static IReadOnlyList<AiBenchmarkPrompt> GetAll() => _all;

    private static readonly IReadOnlyList<AiBenchmarkPrompt> _all = Build();

    private static List<AiBenchmarkPrompt> Build()
    {
        var cases = new List<AiBenchmarkPrompt>();

        // ── Document Composition (freeform, expects <insert> tags) ───────────

        cases.Add(Freeform("doc-formal-request",
            "Write a formal business letter requesting a meeting with the VP of Engineering to discuss the Q3 roadmap.",
            "Formal business request letter",
            ["Dear", "meeting", "roadmap"]));

        cases.Add(Freeform("doc-complaint",
            "Write a formal complaint letter to a hotel manager about a noisy room and poor service during a recent stay.",
            "Business complaint letter",
            ["Dear", "complaint", "stay"]));

        cases.Add(Freeform("doc-cover-letter",
            "Write a cover letter for a software engineer position at a technology company. Highlight 5 years of experience with C# and cloud services.",
            "Job application cover letter",
            ["experience", "position", "software"]));

        cases.Add(Freeform("doc-informal-email",
            "Write a casual email to a colleague inviting them to a team lunch this Friday at noon.",
            "Informal email to colleague",
            ["lunch", "Friday"]));

        cases.Add(Freeform("doc-status-report",
            "Write a project status report for the week. The authentication module is completed, the dashboard is 70% done, and the payment integration is blocked by a third-party API issue.",
            "Project status report",
            ["status", "completed", "blocked"]));

        cases.Add(Freeform("doc-exec-summary",
            "Write an executive summary of quarterly results. Revenue was $4.2M, up 12% from last quarter. Customer acquisition grew 8% and churn decreased to 3.1%.",
            "Executive summary of quarterly results",
            ["revenue", "quarter", "summary"]));

        cases.Add(Freeform("doc-meeting-agenda",
            "Write a meeting agenda with 5 items for a product planning meeting. Include topics: sprint review, backlog grooming, resource allocation, timeline review, and Q&A.",
            "Meeting agenda with 5 items",
            ["agenda", "sprint", "review"]));

        cases.Add(Freeform("doc-meeting-minutes",
            "Write meeting minutes for a design review meeting held on March 15. Attendees: Alice, Bob, Carol. Key decisions: approved the new color scheme, deferred the logo redesign to next sprint, assigned Carol to complete the style guide by March 22.",
            "Meeting minutes",
            ["minutes", "decision", "March"]));

        cases.Add(Freeform("doc-press-release",
            "Write a press release announcing the launch of SmrtPad 2.0, a next-generation text editor with built-in AI writing assistance. The product is available starting April 15, 2026.",
            "Press release for product launch",
            ["announces", "available", "SmrtPad"]));

        cases.Add(Freeform("doc-api-reference",
            "Write a technical API reference documentation section for a REST endpoint POST /api/documents that accepts a JSON body with fields 'title' (string, required), 'content' (string, required), and 'tags' (string array, optional). Returns 201 Created with the document ID.",
            "Technical API reference doc section",
            ["Parameters", "Returns", "POST"]));

        cases.Add(Freeform("doc-short-story",
            "Write a short adventure story of about 300 words about a young explorer who discovers an ancient map leading to a hidden temple in the jungle.",
            "Short creative adventure story ~300 words",
            ["explorer", "map", "temple"]));

        cases.Add(Freeform("doc-essay",
            "Write a formal argumentative essay of about 400 words arguing that remote work improves employee productivity. Include at least two supporting arguments and a counterargument.",
            "Formal argumentative essay",
            ["productivity", "remote", "argue"]));

        cases.Add(Freeform("doc-resume-summary",
            "Write a professional summary section for a resume. The candidate has 8 years of experience in full-stack development, specializing in .NET and React, with leadership experience managing teams of up to 10 engineers.",
            "Resume professional summary",
            ["years", "experience", "development"]));

        cases.Add(Freeform("doc-linkedin-post",
            "Write a LinkedIn announcement post about being promoted to Senior Software Engineer. Keep it professional but enthusiastic, about 150 words.",
            "LinkedIn announcement post",
            ["promoted", "Senior", "excited"]));

        cases.Add(Freeform("doc-apology-letter",
            "Write a formal business apology letter from a company to a client for missing a project deadline. The new delivery date is April 30, 2026.",
            "Business apology letter",
            ["apolog", "deadline", "April"]));

        // ── Edit Skills ──────────────────────────────────────────────────────

        cases.Add(Skill("edit-summarize-news", "summarize",
            "The Federal Reserve announced today that it will hold interest rates steady at the current range of 4.25% to 4.50%, citing persistent inflation concerns and a strong labor market. Chair Jerome Powell stated that the committee remains data-dependent and will assess incoming economic indicators before making any changes. Markets reacted positively, with the S&P 500 rising 0.8% on the news. Analysts expect the Fed to begin cutting rates in the second half of the year if inflation continues to moderate toward the 2% target.",
            "Summarize: news article about Fed rates",
            ["Fed", "rates", "inflation"],
            expectedMaxTokens: 150));

        cases.Add(Skill("edit-summarize-tech", "summarize",
            "Kubernetes orchestrates containerized applications by managing deployment, scaling, and operations across clusters of machines. It uses declarative configuration to specify desired state, and controllers continuously reconcile actual state with the target. Key abstractions include Pods (the smallest deployable units), Services (stable network endpoints), and Deployments (declarative update strategies). The control plane consists of the API server, scheduler, controller manager, and etcd for persistent storage.",
            "Summarize: technical paragraph about Kubernetes",
            ["Kubernetes", "container", "deploy"],
            expectedMaxTokens: 150));

        cases.Add(Skill("edit-summarize-narrative", "summarize",
            "After months of preparation, the expedition team finally reached the summit of Mount Kailash at dawn. The air was thin and bitterly cold, but the panoramic view stretching across the Tibetan Plateau made every hardship worthwhile. Dr. Chen documented the unique geological formations while Ravi captured photographs that would later appear in National Geographic. They spent two hours at the peak before beginning the treacherous descent, knowing that the return journey would test their endurance even further.",
            "Summarize: narrative passage about expedition",
            ["summit", "expedition", "Mount"],
            expectedMaxTokens: 150));

        cases.Add(Skill("edit-rewrite-jargon", "rewrite",
            "We need to leverage our core competencies to synergize cross-functional alignment and drive holistic value creation across the enterprise ecosystem going forward.",
            "Rewrite: jargon-heavy corporate text",
            ["competenc", "value", "align"]));

        cases.Add(Skill("edit-rewrite-ambiguous", "rewrite",
            "The thing with the system is that it does stuff when you click on the button but sometimes it doesn't work right and the other thing happens instead which is not what you want.",
            "Rewrite: ambiguous paragraph for clarity",
            ["system", "button", "click"]));

        cases.Add(Skill("edit-rewrite-passive", "rewrite",
            "The report was written by the analyst. The data was collected by the team over a period of three months. The findings were then reviewed by the committee and a decision was made to implement the recommendations.",
            "Rewrite: passive voice text to active",
            ["report", "data", "decision"]));

        cases.Add(Skill("edit-grammar-1", "grammar",
            "Their going to the store becuase they need to by some grocerys for tonights dinner party.",
            "Grammar fix: common spelling and homophone errors",
            ["going", "store", "dinner"]));

        cases.Add(Skill("edit-grammar-2", "grammar",
            "Me and him went to the meeting yesterday, and we was told that the project deadline have been moved up. Neither the manager or the team lead were happy about it.",
            "Grammar fix: subject-verb agreement and pronoun errors",
            ["meeting", "project", "deadline"]));

        cases.Add(Skill("edit-grammar-3", "grammar",
            "The companies profits has increased significently over the last quarter, witch is a testiment to there hard work and dedicaton.",
            "Grammar fix: possessives and spelling errors",
            ["profit", "quarter", "work"]));

        cases.Add(Skill("edit-tone-pro-1", "tone-professional",
            "Hey team! Just wanted to give you guys a heads up that the deadline got pushed back. No biggie, but let's try to wrap things up by next Friday, cool?",
            "Tone professional: casual team message",
            ["deadline", "Friday"]));

        cases.Add(Skill("edit-tone-pro-2", "tone-professional",
            "So basically the app crashed again lol. The devs are looking into it but honestly it's been super buggy lately. We gotta fix this ASAP or users are gonna bail.",
            "Tone professional: informal bug report",
            ["app", "fix", "user"]));

        cases.Add(Skill("edit-tone-casual-1", "tone-casual",
            "Please be advised that the quarterly performance review meeting has been rescheduled to March 28, 2026. All department heads are required to submit their reports no later than March 25.",
            "Tone casual: formal meeting notice",
            ["review", "meeting", "March"]));

        cases.Add(Skill("edit-tone-casual-2", "tone-casual",
            "In accordance with company policy, all employees must complete the mandatory cybersecurity training module by the end of the fiscal quarter. Failure to comply may result in restricted system access.",
            "Tone casual: formal company policy notice",
            ["training", "complete"]));

        cases.Add(Skill("edit-shorten-1", "shorten",
            "In my personal opinion, I really truly believe that at the end of the day, when all is said and done, the most important thing that we need to focus on and pay attention to is actually making sure that our customers are satisfied and happy with the products and services that we provide to them on a regular and consistent basis.",
            "Shorten: extremely verbose paragraph",
            ["customer", "satisf", "product"]));

        cases.Add(Skill("edit-shorten-2", "shorten",
            "Due to the fact that there has been a significant and substantial increase in the total number of support tickets that have been submitted and filed by our end users over the course of the past several weeks, we have made the decision to hire and bring on additional support staff members to help with the situation.",
            "Shorten: wordy business communication",
            ["support", "ticket", "hire"]));

        cases.Add(Skill("edit-autocomplete-1", "autocomplete",
            "The benefits of regular exercise extend far beyond physical fitness. Studies have shown that",
            "Autocomplete: partial sentence about exercise",
            ["exercise", "health"]));

        cases.Add(Skill("edit-autocomplete-2", "autocomplete",
            "Dear Hiring Manager,\n\nI am writing to express my interest in the Software Engineer position at your company. With over five years of experience in",
            "Autocomplete: partial cover letter",
            ["experience", "develop"]));

        cases.Add(Skill("edit-autocomplete-3", "autocomplete",
            "The quarterly sales report indicates that our North American division exceeded targets by 15%, while the European market",
            "Autocomplete: partial business report",
            ["market", "sales"]));

        cases.Add(Skill("edit-ocr-1", "ocr",
            "Th3 qu1ck br0wn f0x jurnps ov3r the 1azy d0g. Ths sentence is a w3ll-known typlng exercse.",
            "OCR fallback: garbled pangram",
            ["quick", "brown", "fox"]));

        cases.Add(Skill("edit-ocr-2", "ocr",
            "lnvoice #2847\nDat3: March 15, 2O26\nBi11 To: Acme Corp\nArnount Due: $1,250.OO\nPayrnent Terrns: Net 3O",
            "OCR fallback: garbled invoice",
            ["Invoice", "March", "Amount"]));

        // ── Tag Compliance (freeform, expects NO <insert> tags) ──────────────

        cases.Add(Chat("chat-naming",
            "What should I name my document about quarterly sales performance?",
            "Chat: naming question (should NOT produce <insert> tags)"));

        cases.Add(Chat("chat-length",
            "How long should an executive summary be?",
            "Chat: length question (should NOT produce <insert> tags)"));

        cases.Add(Chat("chat-difference",
            "What's the difference between a memo and a letter?",
            "Chat: format question (should NOT produce <insert> tags)"));

        return cases;
    }

    // ── Factory helpers ──────────────────────────────────────────────────────

    private static AiBenchmarkPrompt Freeform(
        string id, string inputText, string description, string[] keywords,
        int expectedMinTokens = 30, int expectedMaxTokens = 2000) =>
        new(
            new BenchmarkPrompt(id, "freeform", inputText, description,
                ExpectedMinTokens: expectedMinTokens,
                ExpectedMaxTokens: expectedMaxTokens),
            ExpectsInsertTag: true,
            ExpectedKeywords: keywords);

    private static AiBenchmarkPrompt Skill(
        string id, string skillKey, string inputText, string description, string[] keywords,
        int expectedMinTokens = 10, int expectedMaxTokens = 2000) =>
        new(
            new BenchmarkPrompt(id, skillKey, inputText, description,
                ExpectedMinTokens: expectedMinTokens,
                ExpectedMaxTokens: expectedMaxTokens,
                MustContainTags: keywords),
            ExpectsInsertTag: true,
            ExpectedKeywords: keywords);

    private static AiBenchmarkPrompt Chat(
        string id, string inputText, string description) =>
        new(
            new BenchmarkPrompt(id, "freeform", inputText, description),
            ExpectsInsertTag: false,
            ExpectedKeywords: []);
}
