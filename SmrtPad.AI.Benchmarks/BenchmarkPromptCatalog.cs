namespace SmrtPad.AI.Benchmarks;

/// <summary>
/// Catalog of all benchmark prompt cases covering document composition (multiple tones),
/// editing skills (5+ cases per skill), and tag-compliance chat queries.
/// </summary>
public static class BenchmarkPromptCatalog
{
    public static IReadOnlyList<BenchmarkCase> All { get; } = BuildCatalog();

    private static List<BenchmarkCase> BuildCatalog()
    {
        var cases = new List<BenchmarkCase>();

        // ══════════════════════════════════════════════════════════════════
        //  Document Composition — freeform skill, expects <insert> tags
        //  Covers: formal, casual, technical, creative tones
        // ══════════════════════════════════════════════════════════════════

        // ── Formal documents ──
        cases.Add(new("doc-formal-request", "freeform",
            "Write a formal business letter requesting a meeting with the VP of Engineering to discuss the Q3 roadmap.",
            "letter", ["Dear", "meeting", "roadmap"], true,
            "Formal business request letter", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-complaint", "freeform",
            "Write a formal complaint letter to a hotel manager about a noisy room and poor service during a recent stay.",
            "letter", ["Dear", "complaint", "stay"], true,
            "Business complaint letter", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-cover-letter", "freeform",
            "Write a cover letter for a software engineer position at a technology company. Highlight 5 years of experience with C# and cloud services.",
            "letter", ["experience", "position", "software"], true,
            "Job application cover letter", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-apology-letter", "freeform",
            "Write a formal business apology letter from a company to a client for missing a project deadline. The new delivery date is April 30, 2026.",
            "letter", ["apolog", "deadline", "April"], true,
            "Business apology letter", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-termination-letter", "freeform",
            "Write a formal employee termination letter citing repeated violations of the company's attendance policy. The effective date is May 1, 2026. Include information about final pay and returning company equipment.",
            "letter", ["termination", "attendance", "effective"], true,
            "Employee termination letter", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-legal-disclaimer", "freeform",
            "Write a legal disclaimer for a software product stating that the software is provided 'as is' without warranty, the company is not liable for data loss, and users agree to the terms by using the product.",
            "disclaimer", ["warranty", "liab", "agree"], true,
            "Legal disclaimer for software product", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-board-resolution", "freeform",
            "Write a board resolution approving the acquisition of CloudTech Solutions Inc. for $15 million. The resolution should state the board has reviewed the due diligence report and authorizes the CEO to execute all necessary documents.",
            "resolution", ["RESOLVED", "acquisition", "authorized"], true,
            "Board resolution for acquisition", BenchmarkCategory.DocumentComposition));

        // ── Business reports / structured documents ──
        cases.Add(new("doc-status-report", "freeform",
            "Write a project status report for the week. The authentication module is completed, the dashboard is 70% done, and the payment integration is blocked by a third-party API issue.",
            "report", ["status", "completed", "blocked"], true,
            "Project status report", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-exec-summary", "freeform",
            "Write an executive summary of quarterly results. Revenue was $4.2M, up 12% from last quarter. Customer acquisition grew 8% and churn decreased to 3.1%.",
            "report", ["revenue", "quarter", "summary"], true,
            "Executive summary of quarterly results", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-meeting-agenda", "freeform",
            "Write a meeting agenda with 5 items for a product planning meeting. Include topics: sprint review, backlog grooming, resource allocation, timeline review, and Q&A.",
            "agenda", ["agenda", "sprint", "review"], true,
            "Meeting agenda with 5 items", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-meeting-minutes", "freeform",
            "Write meeting minutes for a design review meeting held on March 15. Attendees: Alice, Bob, Carol. Key decisions: approved the new color scheme, deferred the logo redesign to next sprint, assigned Carol to complete the style guide by March 22.",
            "minutes", ["minutes", "decision", "March"], true,
            "Meeting minutes", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-press-release", "freeform",
            "Write a press release announcing the launch of SmrtPad 2.0, a next-generation text editor with built-in AI writing assistance. The product is available starting April 15, 2026.",
            "press release", ["announces", "available", "SmrtPad"], true,
            "Press release for product launch", BenchmarkCategory.DocumentComposition));

        // ── Technical documents ──
        cases.Add(new("doc-api-reference", "freeform",
            "Write a technical API reference documentation section for a REST endpoint POST /api/documents that accepts a JSON body with fields 'title' (string, required), 'content' (string, required), and 'tags' (string array, optional). Returns 201 Created with the document ID.",
            "technical", ["Parameters", "Returns", "POST"], true,
            "Technical API reference doc section", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-bug-report", "freeform",
            "Write a detailed bug report for a text editor where pressing Ctrl+Z after pasting rich text from a web page causes the application to freeze for 10 seconds. The issue occurs on Windows 11 with SmrtPad version 1.8.3.",
            "bug report", ["Steps", "Expected", "Actual"], true,
            "Technical bug report", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-adr", "freeform",
            "Write an Architecture Decision Record (ADR) titled 'ADR-005: Adopt SQLite for Local Settings Storage'. The decision is to use SQLite instead of the Windows Registry for storing user preferences. List the context, decision, and consequences.",
            "architecture", ["Context", "Decision", "Consequences"], true,
            "Architecture Decision Record (ADR)", BenchmarkCategory.DocumentComposition));

        // ── Casual / conversational documents ──
        cases.Add(new("doc-informal-email", "freeform",
            "Write a casual email to a colleague inviting them to a team lunch this Friday at noon.",
            "email", ["lunch", "Friday"], true,
            "Informal email to colleague", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-thank-you-note", "freeform",
            "Write a warm, casual thank-you note to a colleague who helped you troubleshoot a production outage last weekend. Mention that their quick response saved the team hours of work.",
            "note", ["thank", "help", "weekend"], true,
            "Casual thank-you note to colleague", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-birthday-invite", "freeform",
            "Write a fun, casual birthday party invitation for a colleague turning 30. The party is on Saturday, April 12 at 7pm at Rosie's Bar & Grill. Include that it's a surprise party.",
            "invitation", ["birthday", "surprise", "Saturday"], true,
            "Casual birthday party invitation", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-social-media-post", "freeform",
            "Write a LinkedIn announcement post about being promoted to Senior Software Engineer. Keep it professional but enthusiastic, about 150 words.",
            "announcement", ["promoted", "Senior", "excited"], true,
            "LinkedIn announcement post", BenchmarkCategory.DocumentComposition));

        // ── Creative documents ──
        cases.Add(new("doc-short-story", "freeform",
            "Write a short adventure story of about 300 words about a young explorer who discovers an ancient map leading to a hidden temple in the jungle.",
            "story", ["explorer", "map", "temple"], true,
            "Short creative adventure story ~300 words", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-essay", "freeform",
            "Write a formal argumentative essay of about 400 words arguing that remote work improves employee productivity. Include at least two supporting arguments and a counterargument.",
            "essay", ["productivity", "remote", "argue"], true,
            "Formal argumentative essay", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-poem", "freeform",
            "Write a four-stanza poem about the joy of writing. Each stanza should have four lines and the poem should use a consistent ABAB rhyme scheme.",
            "poem", ["write", "word"], true,
            "Four-stanza poem about writing", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-product-tagline", "freeform",
            "Write 5 creative product taglines for SmrtPad, an AI-powered text editor. Each tagline should be one sentence, catchy, and highlight the AI writing assistance feature.",
            "tagline", ["SmrtPad", "AI"], true,
            "Product taglines for SmrtPad", BenchmarkCategory.DocumentComposition));

        cases.Add(new("doc-dialogue", "freeform",
            "Write a short fictional dialogue between a project manager and a developer debating whether to delay a release to fix a bug or ship on time with a known issue. About 200 words.",
            "dialogue", ["bug", "release", "ship"], true,
            "Fictional dialogue: PM vs developer", BenchmarkCategory.DocumentComposition));

        // ── Professional / personal docs ──
        cases.Add(new("doc-resume-summary", "freeform",
            "Write a professional summary section for a resume. The candidate has 8 years of experience in full-stack development, specializing in .NET and React, with leadership experience managing teams of up to 10 engineers.",
            "resume", ["years", "experience", "development"], true,
            "Resume professional summary", BenchmarkCategory.DocumentComposition));

        // ══════════════════════════════════════════════════════════════════
        //  Edit Skills — expects <insert> tags, 5+ cases per skill
        // ══════════════════════════════════════════════════════════════════

        // ── Summarize ×5 ──
        cases.Add(new("edit-summarize-news", "summarize",
            "The Federal Reserve announced today that it will hold interest rates steady at the current range of 4.25% to 4.50%, citing persistent inflation concerns and a strong labor market. Chair Jerome Powell stated that the committee remains data-dependent and will assess incoming economic indicators before making any changes. Markets reacted positively, with the S&P 500 rising 0.8% on the news. Analysts expect the Fed to begin cutting rates in the second half of the year if inflation continues to moderate toward the 2% target.",
            null, ["Fed", "rates", "inflation"], true,
            "Summarize: news article about Fed rates", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-summarize-tech", "summarize",
            "Kubernetes orchestrates containerized applications by managing deployment, scaling, and operations across clusters of machines. It uses declarative configuration to specify desired state, and controllers continuously reconcile actual state with the target. Key abstractions include Pods (the smallest deployable units), Services (stable network endpoints), and Deployments (declarative update strategies). The control plane consists of the API server, scheduler, controller manager, and etcd for persistent storage.",
            null, ["Kubernetes", "container", "deploy"], true,
            "Summarize: technical paragraph about Kubernetes", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-summarize-narrative", "summarize",
            "After months of preparation, the expedition team finally reached the summit of Mount Kailash at dawn. The air was thin and bitterly cold, but the panoramic view stretching across the Tibetan Plateau made every hardship worthwhile. Dr. Chen documented the unique geological formations while Ravi captured photographs that would later appear in National Geographic. They spent two hours at the peak before beginning the treacherous descent, knowing that the return journey would test their endurance even further.",
            null, ["summit", "expedition", "Mount"], true,
            "Summarize: narrative passage about expedition", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-summarize-legal", "summarize",
            "This Software License Agreement ('Agreement') is entered into as of January 1, 2026, by and between Acme Corp ('Licensor') and the end user ('Licensee'). The Licensor grants the Licensee a non-exclusive, non-transferable license to use the software for internal business purposes only. The Licensee may not sublicense, sell, or distribute the software to third parties. This Agreement shall remain in effect for a period of one year from the date of execution, unless terminated earlier by either party with thirty days' written notice.",
            null, ["license", "Licensor", "Agreement"], true,
            "Summarize: legal license agreement", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-summarize-medical", "summarize",
            "A randomized controlled trial involving 1,200 participants evaluated the efficacy of a novel mRNA-based vaccine against influenza A (H3N2). Participants were randomly assigned to receive either the experimental vaccine or a placebo. After 12 months, the vaccine group showed a 78% reduction in symptomatic influenza compared to the placebo group (95% CI: 71-84%, p < 0.001). Adverse events were mild and transient, with injection-site pain (32%) and fatigue (18%) being the most common. The study concluded that the mRNA vaccine is both safe and highly effective.",
            null, ["vaccine", "efficacy", "trial"], true,
            "Summarize: medical research abstract", BenchmarkCategory.EditSkill));

        // ── Rewrite ×5 ──
        cases.Add(new("edit-rewrite-jargon", "rewrite",
            "We need to leverage our core competencies to synergize cross-functional alignment and drive holistic value creation across the enterprise ecosystem going forward.",
            null, ["competenc", "value", "align"], true,
            "Rewrite: jargon-heavy corporate text", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-rewrite-ambiguous", "rewrite",
            "The thing with the system is that it does stuff when you click on the button but sometimes it doesn't work right and the other thing happens instead which is not what you want.",
            null, ["system", "button", "click"], true,
            "Rewrite: ambiguous paragraph for clarity", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-rewrite-passive", "rewrite",
            "The report was written by the analyst. The data was collected by the team over a period of three months. The findings were then reviewed by the committee and a decision was made to implement the recommendations.",
            null, ["report", "data", "decision"], true,
            "Rewrite: passive voice text to active", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-rewrite-emotional", "rewrite",
            "I'm SO frustrated with this stupid software!!! It keeps crashing every five minutes and I've lost all my work TWICE today. This is absolutely unacceptable and I want a refund RIGHT NOW or I'm going to leave the worst review ever!!!",
            null, ["software", "crash", "refund"], true,
            "Rewrite: highly emotional complaint for clarity", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-rewrite-runon", "rewrite",
            "The team met yesterday and we discussed the roadmap and then we talked about the budget and someone mentioned that we need more developers and also the design team wants new tools and the marketing department asked for a bigger budget for Q2 campaigns and then we ran out of time before covering the remaining agenda items.",
            null, ["team", "roadmap", "budget"], true,
            "Rewrite: extremely long run-on sentence", BenchmarkCategory.EditSkill));

        // ── Grammar Fix ×5 ──
        cases.Add(new("edit-grammar-1", "grammar",
            "Their going to the store becuase they need to by some grocerys for tonights dinner party.",
            null, ["going", "store", "dinner"], true,
            "Grammar fix: common spelling and homophone errors", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-grammar-2", "grammar",
            "Me and him went to the meeting yesterday, and we was told that the project deadline have been moved up. Neither the manager or the team lead were happy about it.",
            null, ["meeting", "project", "deadline"], true,
            "Grammar fix: subject-verb agreement and pronoun errors", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-grammar-3", "grammar",
            "The companies profits has increased significently over the last quarter, witch is a testiment to there hard work and dedicaton.",
            null, ["profit", "quarter", "work"], true,
            "Grammar fix: possessives and spelling errors", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-grammar-4", "grammar",
            "Walking down the street, the trees were beautiful. After finishing the report, the printer was used. Being a sunny day, we decided to went outside, the park is near from our office.",
            null, ["walk", "report", "park"], true,
            "Grammar fix: dangling modifiers and tense errors", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-grammar-5", "grammar",
            "The team lead said, that the deadline was moved up however nobody was informed. Lets make sure we dont miss any more deadlines, its really important for the teams moral.",
            null, ["deadline", "team", "important"], true,
            "Grammar fix: comma splices, apostrophes, and homophones", BenchmarkCategory.EditSkill));

        // ── Tone Professional ×5 ──
        cases.Add(new("edit-tone-pro-1", "tone-professional",
            "Hey team! Just wanted to give you guys a heads up that the deadline got pushed back. No biggie, but let's try to wrap things up by next Friday, cool?",
            null, ["deadline", "Friday"], true,
            "Tone professional: casual team message", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-tone-pro-2", "tone-professional",
            "So basically the app crashed again lol. The devs are looking into it but honestly it's been super buggy lately. We gotta fix this ASAP or users are gonna bail.",
            null, ["app", "fix", "user"], true,
            "Tone professional: informal bug report", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-tone-pro-3", "tone-professional",
            "Yo, quick question — can we push the meeting to like 3pm? I've got a thing at 2 and it's gonna run over. Thx!",
            null, ["meeting", "time"], true,
            "Tone professional: very casual meeting reschedule", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-tone-pro-4", "tone-professional",
            "OMG you won't believe what happened at the client demo today — the whole thing froze right when we were showing the new dashboard. Super embarrassing but kinda funny tbh 😂",
            null, ["demo", "client", "dashboard"], true,
            "Tone professional: emoji-laden incident recap", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-tone-pro-5", "tone-professional",
            "Just wanna say the new feature is sick 🔥 Users are totally loving it and downloads are through the roof this week. We should def celebrate or something!",
            null, ["feature", "user", "download"], true,
            "Tone professional: slang-heavy success announcement", BenchmarkCategory.EditSkill));

        // ── Tone Casual ×5 ──
        cases.Add(new("edit-tone-casual-1", "tone-casual",
            "Please be advised that the quarterly performance review meeting has been rescheduled to March 28, 2026. All department heads are required to submit their reports no later than March 25.",
            null, ["review", "meeting", "March"], true,
            "Tone casual: formal meeting notice", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-tone-casual-2", "tone-casual",
            "In accordance with company policy, all employees must complete the mandatory cybersecurity training module by the end of the fiscal quarter. Failure to comply may result in restricted system access.",
            null, ["training", "complete"], true,
            "Tone casual: formal company policy notice", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-tone-casual-3", "tone-casual",
            "The Board of Directors hereby resolves that the annual general meeting shall be convened on the fifteenth day of May, 2026, at the corporate headquarters, for the purpose of reviewing the fiscal year's financial statements.",
            null, ["meeting", "May", "financial"], true,
            "Tone casual: very formal board notice", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-tone-casual-4", "tone-casual",
            "It is hereby notified that the office premises shall undergo scheduled maintenance on April 10, 2026. All personnel are requested to vacate the premises by 5:00 PM on the preceding business day.",
            null, ["maintenance", "office", "April"], true,
            "Tone casual: building maintenance notice", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-tone-casual-5", "tone-casual",
            "We regret to inform you that your application for the position of Senior Data Analyst has not been successful on this occasion. We encourage you to apply for future openings that match your qualifications.",
            null, ["application", "position", "future"], true,
            "Tone casual: formal job rejection", BenchmarkCategory.EditSkill));

        // ── Shorten ×5 ──
        cases.Add(new("edit-shorten-1", "shorten",
            "In my personal opinion, I really truly believe that at the end of the day, when all is said and done, the most important thing that we need to focus on and pay attention to is actually making sure that our customers are satisfied and happy with the products and services that we provide to them on a regular and consistent basis.",
            null, ["customer", "satisf", "product"], true,
            "Shorten: extremely verbose paragraph", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-shorten-2", "shorten",
            "Due to the fact that there has been a significant and substantial increase in the total number of support tickets that have been submitted and filed by our end users over the course of the past several weeks, we have made the decision to hire and bring on additional support staff members to help with the situation.",
            null, ["support", "ticket", "hire"], true,
            "Shorten: wordy business communication", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-shorten-3", "shorten",
            "At this point in time, we are currently in the process of conducting a comprehensive and thorough evaluation and assessment of all of the various different options and alternatives that are presently available to us in order to determine which particular course of action would be the most optimal and beneficial for our organization going forward into the future.",
            null, ["evaluat", "option", "organization"], true,
            "Shorten: absurdly bureaucratic sentence", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-shorten-4", "shorten",
            "I wanted to reach out to you and let you know that I am writing to you today in order to provide you with an update on the status of the project that we have been working on together for the last couple of months. The project is going well and we are making good progress.",
            null, ["project", "update", "progress"], true,
            "Shorten: unnecessarily long project update", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-shorten-5", "shorten",
            "It is absolutely essential and critically important that each and every member of our team takes the necessary steps and precautions to ensure that all sensitive and confidential data is properly secured and protected at all times without exception.",
            null, ["team", "data", "secur"], true,
            "Shorten: over-emphasized security reminder", BenchmarkCategory.EditSkill));

        // ── Autocomplete ×5 ──
        cases.Add(new("edit-autocomplete-1", "autocomplete",
            "The benefits of regular exercise extend far beyond physical fitness. Studies have shown that",
            null, ["exercise", "health"], true,
            "Autocomplete: partial sentence about exercise", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-autocomplete-2", "autocomplete",
            "Dear Hiring Manager,\n\nI am writing to express my interest in the Software Engineer position at your company. With over five years of experience in",
            null, ["experience", "develop"], true,
            "Autocomplete: partial cover letter", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-autocomplete-3", "autocomplete",
            "The quarterly sales report indicates that our North American division exceeded targets by 15%, while the European market",
            null, ["market", "sales"], true,
            "Autocomplete: partial business report", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-autocomplete-4", "autocomplete",
            "In conclusion, the migration to cloud infrastructure has resulted in a 40% reduction in operational costs. However,",
            null, ["cloud", "cost"], true,
            "Autocomplete: partial technical conclusion", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-autocomplete-5", "autocomplete",
            "The patient presented with persistent headaches and blurred vision over the past two weeks. Initial examination revealed",
            null, ["patient", "examination"], true,
            "Autocomplete: partial medical note", BenchmarkCategory.EditSkill));

        // ── OCR Fallback ×3 ──
        cases.Add(new("edit-ocr-1", "ocr",
            "Th3 qu1ck br0wn f0x jurnps ov3r the 1azy d0g. Ths sentence is a w3ll-known typlng exercse.",
            null, ["quick", "brown", "fox"], true,
            "OCR fallback: garbled pangram", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-ocr-2", "ocr",
            "lnvoice #2847\nDat3: March 15, 2O26\nBi11 To: Acme Corp\nArnount Due: $1,250.OO\nPayrnent Terrns: Net 3O",
            null, ["Invoice", "March", "Amount"], true,
            "OCR fallback: garbled invoice", BenchmarkCategory.EditSkill));

        cases.Add(new("edit-ocr-3", "ocr",
            "EMPL0YEE HANDB00K\nChapter 7: Leave P0licy\nAll full-tirne ernployees are ent1tled to 20 days of pa1d annual l3ave per calendar y3ar. Unused l3ave rnay be carr1ed forward up to a rnaxirnum of 5 days.",
            null, ["Employee", "leave", "annual"], true,
            "OCR fallback: garbled employee handbook", BenchmarkCategory.EditSkill));

        // ══════════════════════════════════════════════════════════════════
        //  Tag Compliance — freeform chat, expects NO <insert> tags
        //  Chat responses should be plain conversational answers.
        // ══════════════════════════════════════════════════════════════════

        cases.Add(new("chat-naming", "freeform",
            "What should I name my document about quarterly sales performance?",
            null, ["name", "title"], false,
            "Chat: naming question", BenchmarkCategory.TagCompliance));

        cases.Add(new("chat-length", "freeform",
            "How long should an executive summary be?",
            null, ["page", "paragraph"], false,
            "Chat: length question", BenchmarkCategory.TagCompliance));

        cases.Add(new("chat-difference", "freeform",
            "What's the difference between a memo and a letter?",
            null, ["memo", "letter"], false,
            "Chat: format question", BenchmarkCategory.TagCompliance));

        cases.Add(new("chat-writing-tip", "freeform",
            "What are three tips for writing a persuasive email?",
            null, ["tip", "email"], false,
            "Chat: writing tips advice", BenchmarkCategory.TagCompliance));

        cases.Add(new("chat-grammar-rule", "freeform",
            "When should I use 'who' versus 'whom' in a sentence?",
            null, ["who", "whom"], false,
            "Chat: grammar rule explanation", BenchmarkCategory.TagCompliance));

        cases.Add(new("chat-word-choice", "freeform",
            "What's the difference between 'affect' and 'effect'?",
            null, ["affect", "effect"], false,
            "Chat: word choice question", BenchmarkCategory.TagCompliance));

        cases.Add(new("chat-style-guide", "freeform",
            "Should I use Oxford commas in business writing?",
            null, ["comma", "Oxford"], false,
            "Chat: style guide question", BenchmarkCategory.TagCompliance));

        cases.Add(new("chat-structure", "freeform",
            "What sections should a project proposal include?",
            null, ["section", "proposal"], false,
            "Chat: document structure question", BenchmarkCategory.TagCompliance));

        cases.Add(new("chat-howto", "freeform",
            "How do I start a cover letter without saying 'I am writing to apply'?",
            null, ["cover letter", "start"], false,
            "Chat: how-to question about writing", BenchmarkCategory.TagCompliance));

        cases.Add(new("chat-comparison", "freeform",
            "Is it better to use bullet points or numbered lists in a status report?",
            null, ["bullet", "list"], false,
            "Chat: comparison question (bullets vs numbers)", BenchmarkCategory.TagCompliance));

        return cases;
    }
}
