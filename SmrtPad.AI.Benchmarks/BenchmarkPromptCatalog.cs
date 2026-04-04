namespace SmrtPad.AI.Benchmarks;

/// <summary>
/// Catalog of all benchmark prompt cases covering document composition,
/// editing skills, and tag compliance verification.
/// </summary>
public static class BenchmarkPromptCatalog
{
    public static IReadOnlyList<BenchmarkCase> All { get; } = BuildCatalog();

    private static List<BenchmarkCase> BuildCatalog()
    {
        var cases = new List<BenchmarkCase>();

        // ── Document Composition (freeform skill, expects <insert> tags) ──

        cases.Add(new BenchmarkCase(
            "doc-formal-request", "freeform",
            "Write a formal business letter requesting a meeting with the VP of Engineering to discuss the Q3 roadmap.",
            "letter", ["Dear", "meeting", "roadmap"], true,
            "Formal business request letter", BenchmarkCategory.DocumentComposition));

        cases.Add(new BenchmarkCase(
            "doc-complaint", "freeform",
            "Write a formal complaint letter to a hotel manager about a noisy room and poor service during a recent stay.",
            "letter", ["Dear", "complaint", "stay"], true,
            "Business complaint letter", BenchmarkCategory.DocumentComposition));

        cases.Add(new BenchmarkCase(
            "doc-cover-letter", "freeform",
            "Write a cover letter for a software engineer position at a technology company. Highlight 5 years of experience with C# and cloud services.",
            "letter", ["experience", "position", "software"], true,
            "Job application cover letter", BenchmarkCategory.DocumentComposition));

        cases.Add(new BenchmarkCase(
            "doc-informal-email", "freeform",
            "Write a casual email to a colleague inviting them to a team lunch this Friday at noon.",
            "email", ["lunch", "Friday"], true,
            "Informal email to colleague", BenchmarkCategory.DocumentComposition));

        cases.Add(new BenchmarkCase(
            "doc-status-report", "freeform",
            "Write a project status report for the week. The authentication module is completed, the dashboard is 70% done, and the payment integration is blocked by a third-party API issue.",
            "report", ["status", "completed", "blocked"], true,
            "Project status report", BenchmarkCategory.DocumentComposition));

        cases.Add(new BenchmarkCase(
            "doc-exec-summary", "freeform",
            "Write an executive summary of quarterly results. Revenue was $4.2M, up 12% from last quarter. Customer acquisition grew 8% and churn decreased to 3.1%.",
            "report", ["revenue", "quarter", "summary"], true,
            "Executive summary of quarterly results", BenchmarkCategory.DocumentComposition));

        cases.Add(new BenchmarkCase(
            "doc-meeting-agenda", "freeform",
            "Write a meeting agenda with 5 items for a product planning meeting. Include topics: sprint review, backlog grooming, resource allocation, timeline review, and Q&A.",
            "agenda", ["agenda", "sprint", "review"], true,
            "Meeting agenda with 5 items", BenchmarkCategory.DocumentComposition));

        cases.Add(new BenchmarkCase(
            "doc-meeting-minutes", "freeform",
            "Write meeting minutes for a design review meeting held on March 15. Attendees: Alice, Bob, Carol. Key decisions: approved the new color scheme, deferred the logo redesign to next sprint, assigned Carol to complete the style guide by March 22.",
            "minutes", ["minutes", "decision", "March"], true,
            "Meeting minutes", BenchmarkCategory.DocumentComposition));

        cases.Add(new BenchmarkCase(
            "doc-press-release", "freeform",
            "Write a press release announcing the launch of SmrtPad 2.0, a next-generation text editor with built-in AI writing assistance. The product is available starting April 15, 2026.",
            "press release", ["announces", "available", "SmrtPad"], true,
            "Press release for product launch", BenchmarkCategory.DocumentComposition));

        cases.Add(new BenchmarkCase(
            "doc-api-reference", "freeform",
            "Write a technical API reference documentation section for a REST endpoint POST /api/documents that accepts a JSON body with fields 'title' (string, required), 'content' (string, required), and 'tags' (string array, optional). Returns 201 Created with the document ID.",
            "technical", ["Parameters", "Returns", "POST"], true,
            "Technical API reference doc section", BenchmarkCategory.DocumentComposition));

        cases.Add(new BenchmarkCase(
            "doc-short-story", "freeform",
            "Write a short adventure story of about 300 words about a young explorer who discovers an ancient map leading to a hidden temple in the jungle.",
            "story", ["explorer", "map", "temple"], true,
            "Short creative adventure story ~300 words", BenchmarkCategory.DocumentComposition));

        cases.Add(new BenchmarkCase(
            "doc-essay", "freeform",
            "Write a formal argumentative essay of about 400 words arguing that remote work improves employee productivity. Include at least two supporting arguments and a counterargument.",
            "essay", ["productivity", "remote", "argue"], true,
            "Formal argumentative essay", BenchmarkCategory.DocumentComposition));

        cases.Add(new BenchmarkCase(
            "doc-resume-summary", "freeform",
            "Write a professional summary section for a resume. The candidate has 8 years of experience in full-stack development, specializing in .NET and React, with leadership experience managing teams of up to 10 engineers.",
            "resume", ["years", "experience", "development"], true,
            "Resume professional summary", BenchmarkCategory.DocumentComposition));

        cases.Add(new BenchmarkCase(
            "doc-linkedin-post", "freeform",
            "Write a LinkedIn announcement post about being promoted to Senior Software Engineer. Keep it professional but enthusiastic, about 150 words.",
            "announcement", ["promoted", "Senior", "excited"], true,
            "LinkedIn announcement post", BenchmarkCategory.DocumentComposition));

        cases.Add(new BenchmarkCase(
            "doc-apology-letter", "freeform",
            "Write a formal business apology letter from a company to a client for missing a project deadline. The new delivery date is April 30, 2026.",
            "letter", ["apolog", "deadline", "April"], true,
            "Business apology letter", BenchmarkCategory.DocumentComposition));

        // ── Edit Skills (expects <insert> tags) ──

        // Summarize ×3
        cases.Add(new BenchmarkCase(
            "edit-summarize-news", "summarize",
            "The Federal Reserve announced today that it will hold interest rates steady at the current range of 4.25% to 4.50%, citing persistent inflation concerns and a strong labor market. Chair Jerome Powell stated that the committee remains data-dependent and will assess incoming economic indicators before making any changes. Markets reacted positively, with the S&P 500 rising 0.8% on the news. Analysts expect the Fed to begin cutting rates in the second half of the year if inflation continues to moderate toward the 2% target.",
            null, ["Fed", "rates", "inflation"], true,
            "Summarize: news article about Fed rates", BenchmarkCategory.EditSkill));

        cases.Add(new BenchmarkCase(
            "edit-summarize-tech", "summarize",
            "Kubernetes orchestrates containerized applications by managing deployment, scaling, and operations across clusters of machines. It uses declarative configuration to specify desired state, and controllers continuously reconcile actual state with the target. Key abstractions include Pods (the smallest deployable units), Services (stable network endpoints), and Deployments (declarative update strategies). The control plane consists of the API server, scheduler, controller manager, and etcd for persistent storage.",
            null, ["Kubernetes", "container", "deploy"], true,
            "Summarize: technical paragraph about Kubernetes", BenchmarkCategory.EditSkill));

        cases.Add(new BenchmarkCase(
            "edit-summarize-narrative", "summarize",
            "After months of preparation, the expedition team finally reached the summit of Mount Kailash at dawn. The air was thin and bitterly cold, but the panoramic view stretching across the Tibetan Plateau made every hardship worthwhile. Dr. Chen documented the unique geological formations while Ravi captured photographs that would later appear in National Geographic. They spent two hours at the peak before beginning the treacherous descent, knowing that the return journey would test their endurance even further.",
            null, ["summit", "expedition", "Mount"], true,
            "Summarize: narrative passage about expedition", BenchmarkCategory.EditSkill));

        // Rewrite ×3
        cases.Add(new BenchmarkCase(
            "edit-rewrite-jargon", "rewrite",
            "We need to leverage our core competencies to synergize cross-functional alignment and drive holistic value creation across the enterprise ecosystem going forward.",
            null, ["competenc", "value", "align"], true,
            "Rewrite: jargon-heavy corporate text", BenchmarkCategory.EditSkill));

        cases.Add(new BenchmarkCase(
            "edit-rewrite-ambiguous", "rewrite",
            "The thing with the system is that it does stuff when you click on the button but sometimes it doesn't work right and the other thing happens instead which is not what you want.",
            null, ["system", "button", "click"], true,
            "Rewrite: ambiguous paragraph for clarity", BenchmarkCategory.EditSkill));

        cases.Add(new BenchmarkCase(
            "edit-rewrite-passive", "rewrite",
            "The report was written by the analyst. The data was collected by the team over a period of three months. The findings were then reviewed by the committee and a decision was made to implement the recommendations.",
            null, ["report", "data", "decision"], true,
            "Rewrite: passive voice text to active", BenchmarkCategory.EditSkill));

        // Grammar Fix ×3
        cases.Add(new BenchmarkCase(
            "edit-grammar-1", "grammar",
            "Their going to the store becuase they need to by some grocerys for tonights dinner party.",
            null, ["going", "store", "dinner"], true,
            "Grammar fix: common spelling and homophone errors", BenchmarkCategory.EditSkill));

        cases.Add(new BenchmarkCase(
            "edit-grammar-2", "grammar",
            "Me and him went to the meeting yesterday, and we was told that the project deadline have been moved up. Neither the manager or the team lead were happy about it.",
            null, ["meeting", "project", "deadline"], true,
            "Grammar fix: subject-verb agreement and pronoun errors", BenchmarkCategory.EditSkill));

        cases.Add(new BenchmarkCase(
            "edit-grammar-3", "grammar",
            "The companies profits has increased significently over the last quarter, witch is a testiment to there hard work and dedicaton.",
            null, ["profit", "quarter", "work"], true,
            "Grammar fix: possessives and spelling errors", BenchmarkCategory.EditSkill));

        // Tone Professional ×2
        cases.Add(new BenchmarkCase(
            "edit-tone-pro-1", "tone-professional",
            "Hey team! Just wanted to give you guys a heads up that the deadline got pushed back. No biggie, but let's try to wrap things up by next Friday, cool?",
            null, ["deadline", "Friday"], true,
            "Tone professional: casual team message", BenchmarkCategory.EditSkill));

        cases.Add(new BenchmarkCase(
            "edit-tone-pro-2", "tone-professional",
            "So basically the app crashed again lol. The devs are looking into it but honestly it's been super buggy lately. We gotta fix this ASAP or users are gonna bail.",
            null, ["app", "fix", "user"], true,
            "Tone professional: informal bug report", BenchmarkCategory.EditSkill));

        // Tone Casual ×2
        cases.Add(new BenchmarkCase(
            "edit-tone-casual-1", "tone-casual",
            "Please be advised that the quarterly performance review meeting has been rescheduled to March 28, 2026. All department heads are required to submit their reports no later than March 25.",
            null, ["review", "meeting", "March"], true,
            "Tone casual: formal meeting notice", BenchmarkCategory.EditSkill));

        cases.Add(new BenchmarkCase(
            "edit-tone-casual-2", "tone-casual",
            "In accordance with company policy, all employees must complete the mandatory cybersecurity training module by the end of the fiscal quarter. Failure to comply may result in restricted system access.",
            null, ["training", "complete"], true,
            "Tone casual: formal company policy notice", BenchmarkCategory.EditSkill));

        // Shorten ×2
        cases.Add(new BenchmarkCase(
            "edit-shorten-1", "shorten",
            "In my personal opinion, I really truly believe that at the end of the day, when all is said and done, the most important thing that we need to focus on and pay attention to is actually making sure that our customers are satisfied and happy with the products and services that we provide to them on a regular and consistent basis.",
            null, ["customer", "satisf", "product"], true,
            "Shorten: extremely verbose paragraph", BenchmarkCategory.EditSkill));

        cases.Add(new BenchmarkCase(
            "edit-shorten-2", "shorten",
            "Due to the fact that there has been a significant and substantial increase in the total number of support tickets that have been submitted and filed by our end users over the course of the past several weeks, we have made the decision to hire and bring on additional support staff members to help with the situation.",
            null, ["support", "ticket", "hire"], true,
            "Shorten: wordy business communication", BenchmarkCategory.EditSkill));

        // Autocomplete ×3
        cases.Add(new BenchmarkCase(
            "edit-autocomplete-1", "autocomplete",
            "The benefits of regular exercise extend far beyond physical fitness. Studies have shown that",
            null, ["exercise", "health"], true,
            "Autocomplete: partial sentence about exercise", BenchmarkCategory.EditSkill));

        cases.Add(new BenchmarkCase(
            "edit-autocomplete-2", "autocomplete",
            "Dear Hiring Manager,\n\nI am writing to express my interest in the Software Engineer position at your company. With over five years of experience in",
            null, ["experience", "develop"], true,
            "Autocomplete: partial cover letter", BenchmarkCategory.EditSkill));

        cases.Add(new BenchmarkCase(
            "edit-autocomplete-3", "autocomplete",
            "The quarterly sales report indicates that our North American division exceeded targets by 15%, while the European market",
            null, ["market", "sales"], true,
            "Autocomplete: partial business report", BenchmarkCategory.EditSkill));

        // OCR Fallback ×2
        cases.Add(new BenchmarkCase(
            "edit-ocr-1", "ocr",
            "Th3 qu1ck br0wn f0x jurnps ov3r the 1azy d0g. Ths sentence is a w3ll-known typlng exercse.",
            null, ["quick", "brown", "fox"], true,
            "OCR fallback: garbled pangram", BenchmarkCategory.EditSkill));

        cases.Add(new BenchmarkCase(
            "edit-ocr-2", "ocr",
            "lnvoice #2847\nDat3: March 15, 2O26\nBi11 To: Acme Corp\nArnount Due: $1,250.OO\nPayrnent Terrns: Net 3O",
            null, ["Invoice", "March", "Amount"], true,
            "OCR fallback: garbled invoice", BenchmarkCategory.EditSkill));

        // ── Tag Compliance (freeform chat, expects NO <insert> tags) ──

        cases.Add(new BenchmarkCase(
            "chat-naming", "freeform",
            "What should I name my document about quarterly sales performance?",
            null, [], false,
            "Chat: naming question (should NOT produce <insert> tags)", BenchmarkCategory.TagCompliance));

        cases.Add(new BenchmarkCase(
            "chat-length", "freeform",
            "How long should an executive summary be?",
            null, [], false,
            "Chat: length question (should NOT produce <insert> tags)", BenchmarkCategory.TagCompliance));

        cases.Add(new BenchmarkCase(
            "chat-difference", "freeform",
            "What's the difference between a memo and a letter?",
            null, [], false,
            "Chat: format question (should NOT produce <insert> tags)", BenchmarkCategory.TagCompliance));

        return cases;
    }
}
