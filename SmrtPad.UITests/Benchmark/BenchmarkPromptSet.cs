namespace SmrtPad.UITests.Benchmark;

/// <summary>
/// Curated set of benchmark prompts covering all 10 Smart Sidebar skill keys
/// with varying lengths, topics, and edge cases.
/// </summary>
public static class BenchmarkPromptSet
{
    /// <summary>
    /// Returns the full set of ~40 benchmark prompts.
    /// </summary>
    public static IReadOnlyList<BenchmarkPrompt> GetAll() =>
    [
        // ── Summarize (4 prompts) ──────────────────────────────────────
        new("summarize-01", "summarize",
            "The Industrial Revolution, which took place from the 18th to 19th centuries, was a period during which predominantly agrarian, rural societies in Europe and America became industrial and urban. Prior to the Industrial Revolution, which began in Britain in the late 1700s, manufacturing was often done in people's homes, using hand tools or basic machines. Industrialization marked a shift to powered, special-purpose machinery, factories, and mass production. The iron and textile industries, along with the development of the steam engine, played central roles in the Industrial Revolution.",
            "Summarize a history paragraph",
            ExpectedMinTokens: 20, ExpectedMaxTokens: 150),

        new("summarize-02", "summarize",
            "Quantum computing leverages quantum mechanical phenomena such as superposition and entanglement to process information. Unlike classical bits, which are either 0 or 1, quantum bits (qubits) can exist in multiple states simultaneously. This allows quantum computers to solve certain problems exponentially faster than classical computers. Key applications include cryptography, drug discovery, optimization problems, and materials science. However, current quantum computers are noisy and require error correction, making large-scale practical use still years away.",
            "Summarize a technology paragraph",
            ExpectedMinTokens: 20, ExpectedMaxTokens: 150),

        new("summarize-03", "summarize",
            "Once upon a time in a small village, there lived an old clockmaker. He was known for crafting the most precise timepieces in the land. One day, a mysterious traveler arrived and asked for a clock that could tell the future. The clockmaker laughed, but the traveler placed a single golden gear on the counter. The clockmaker examined it closely and realized it was unlike anything he had ever seen.",
            "Summarize a short story",
            ExpectedMinTokens: 15, ExpectedMaxTokens: 100),

        new("summarize-04", "summarize",
            "Meeting notes: Discussed Q3 budget allocations. Marketing requested 15% increase for digital campaigns. Engineering needs two additional headcount for the platform migration project. Legal flagged compliance risk with the new EU AI Act requirements. Action items: CFO to model scenarios by Friday. VP Eng to submit hiring justification. Legal to draft compliance checklist. Next meeting: Thursday 3pm.",
            "Summarize meeting notes",
            ExpectedMinTokens: 20, ExpectedMaxTokens: 120),

        // ── Tone Professional (4 prompts) ──────────────────────────────
        new("tone-professional-01", "tone-professional",
            "Hey team, just wanted to let you know that the server is down again. We gotta fix this ASAP or the boss is gonna flip. Can someone look into it?",
            "Casual message to professional tone",
            ExpectedMinTokens: 20, ExpectedMaxTokens: 200),

        new("tone-professional-02", "tone-professional",
            "yo the new feature is totally broken lol, users are complaining like crazy and I have no idea why it shipped",
            "Very casual/slang to professional tone",
            ExpectedMinTokens: 20, ExpectedMaxTokens: 200),

        new("tone-professional-03", "tone-professional",
            "I think we should probably maybe consider looking at the possibility of potentially updating the documentation at some point in the near future if that's okay with everyone.",
            "Hedging/uncertain to professional tone",
            ExpectedMinTokens: 15, ExpectedMaxTokens: 200),

        new("tone-professional-04", "tone-professional",
            "The analysis of the third-quarter financial data indicates a downward trend. We need to address this immediately to prevent further losses and restore stakeholder confidence.",
            "Already professional — should remain professional",
            ExpectedMinTokens: 15, ExpectedMaxTokens: 200),

        // ── Tone Casual (4 prompts) ────────────────────────────────────
        new("tone-casual-01", "tone-casual",
            "Dear Colleagues, I am writing to inform you that the quarterly performance review meeting has been rescheduled to Friday, October 15th at 2:00 PM. Please ensure all preparatory materials are submitted by end of day Thursday.",
            "Formal business to casual tone",
            ExpectedMinTokens: 15, ExpectedMaxTokens: 200),

        new("tone-casual-02", "tone-casual",
            "The committee has determined that the implementation of the proposed regulatory framework shall commence no later than the first business day of the subsequent fiscal quarter.",
            "Legal/regulatory to casual tone",
            ExpectedMinTokens: 15, ExpectedMaxTokens: 200),

        new("tone-casual-03", "tone-casual",
            "It is with great pleasure that I extend an invitation to the annual company gala. The event will be held at the Grand Ballroom on December 12th. Formal attire is required.",
            "Formal invitation to casual tone",
            ExpectedMinTokens: 15, ExpectedMaxTokens: 200),

        new("tone-casual-04", "tone-casual",
            "Hey what's up, just chilling and thought I'd drop a note about the project timeline.",
            "Already casual — should remain casual",
            ExpectedMinTokens: 10, ExpectedMaxTokens: 150),

        // ── Rewrite (4 prompts) ────────────────────────────────────────
        new("rewrite-01", "rewrite",
            "The dog ran fast across the big field and jumped over the tall fence to get to the other side where the cat was sitting quietly under a large tree.",
            "Rewrite a simple run-on sentence",
            ExpectedMinTokens: 15, ExpectedMaxTokens: 200),

        new("rewrite-02", "rewrite",
            "In consideration of the fact that the aforementioned provisions have been duly executed in accordance with the stipulated requirements, it is hereby acknowledged that the contractual obligations have been satisfactorily fulfilled.",
            "Rewrite verbose legalese",
            ExpectedMinTokens: 10, ExpectedMaxTokens: 150),

        new("rewrite-03", "rewrite",
            "Machine learning is a type of AI. It uses data. The data trains models. Models make predictions. Predictions can be accurate. They can also be wrong. It depends on the data quality.",
            "Rewrite choppy sentences into flowing prose",
            ExpectedMinTokens: 15, ExpectedMaxTokens: 200),

        new("rewrite-04", "rewrite",
            "The very extremely absolutely incredibly important thing that we really truly definitely need to do is to make sure that everything is working properly and correctly without any issues or problems whatsoever.",
            "Rewrite redundant/wordy text",
            ExpectedMinTokens: 10, ExpectedMaxTokens: 100),

        // ── Grammar (4 prompts) ────────────────────────────────────────
        new("grammar-01", "grammar",
            "Their going to the store too buy some items that there friend's recommended. Its a long drive but its worth it.",
            "Fix common homophones (their/they're/there, its/it's, too/to)",
            ExpectedMinTokens: 15, ExpectedMaxTokens: 150),

        new("grammar-02", "grammar",
            "the quick brown fox jump over the lazy dog and then it run away very fast yesterday",
            "Fix verb tense and capitalization",
            ExpectedMinTokens: 10, ExpectedMaxTokens: 100),

        new("grammar-03", "grammar",
            "Me and him went to the store, we buyed some groceries and then drived home. The childs was happy to see us.",
            "Fix pronoun case, irregular verbs, plurals",
            ExpectedMinTokens: 15, ExpectedMaxTokens: 100),

        new("grammar-04", "grammar",
            "Despite the rain we went outside, however we forgot our umbrellas so we got wet which was unfortunate but we still had fun because the concert was amazing.",
            "Fix punctuation and run-on sentence",
            ExpectedMinTokens: 15, ExpectedMaxTokens: 150),

        // ── Shorten (4 prompts) ────────────────────────────────────────
        new("shorten-01", "shorten",
            "In the contemporary era of digital transformation and technological advancement, organizations across various industry verticals are increasingly recognizing the paramount importance of implementing comprehensive and robust cybersecurity measures to safeguard their critical digital infrastructure, sensitive data assets, and proprietary intellectual property from the ever-evolving landscape of sophisticated cyber threats and malicious attack vectors.",
            "Shorten an extremely verbose paragraph",
            ExpectedMinTokens: 10, ExpectedMaxTokens: 80),

        new("shorten-02", "shorten",
            "Please find attached the document that I mentioned during our meeting earlier today. I hope you will find it useful and informative. Please do not hesitate to reach out to me if you have any questions, concerns, or comments about the content of the document.",
            "Shorten a polite but wordy email",
            ExpectedMinTokens: 10, ExpectedMaxTokens: 60),

        new("shorten-03", "shorten",
            "The quick brown fox jumps over the lazy dog.",
            "Already short — edge case for minimal input",
            ExpectedMinTokens: 5, ExpectedMaxTokens: 50),

        new("shorten-04", "shorten",
            "It is absolutely essential and critically important that each and every member of the team takes the time to carefully review and thoroughly examine all of the relevant documentation and supporting materials before the upcoming deadline, which is scheduled for the end of this month, in order to ensure that we are fully prepared and adequately equipped to handle any potential issues or challenges that may arise during the implementation phase of the project.",
            "Shorten a bureaucratic sentence",
            ExpectedMinTokens: 10, ExpectedMaxTokens: 80),

        // ── Autocomplete (4 prompts) ───────────────────────────────────
        new("autocomplete-01", "autocomplete",
            "The future of artificial intelligence depends on",
            "Complete a technology sentence",
            ExpectedMinTokens: 10, ExpectedMaxTokens: 200),

        new("autocomplete-02", "autocomplete",
            "Dear Hiring Manager, I am writing to express my interest in the",
            "Complete a cover letter opening",
            ExpectedMinTokens: 15, ExpectedMaxTokens: 300),

        new("autocomplete-03", "autocomplete",
            "Once upon a time in a land far away, there lived a",
            "Complete a fairy tale opening",
            ExpectedMinTokens: 15, ExpectedMaxTokens: 300),

        new("autocomplete-04", "autocomplete",
            "The three main benefits of regular exercise are",
            "Complete a list-style sentence",
            ExpectedMinTokens: 15, ExpectedMaxTokens: 300),

        // ── Semantic Search (3 prompts) ────────────────────────────────
        new("semantic-01", "semantic",
            "What are the key differences between TCP and UDP protocols?",
            "Technical question",
            ExpectedMinTokens: 20, ExpectedMaxTokens: 500),

        new("semantic-02", "semantic",
            "Explain the concept of compound interest in simple terms.",
            "Finance question in simple language",
            ExpectedMinTokens: 20, ExpectedMaxTokens: 400),

        new("semantic-03", "semantic",
            "How does photosynthesis work?",
            "Short science question",
            ExpectedMinTokens: 20, ExpectedMaxTokens: 400),

        // ── OCR Fallback (2 prompts) ───────────────────────────────────
        new("ocr-01", "ocr",
            "Th1s t3xt h@s b33n sc4nn3d fr0m a d0cum3nt w1th p00r qu4l1ty. Pl3as3 h3lp m3 f1x 1t.",
            "OCR-style character substitution errors",
            ExpectedMinTokens: 10, ExpectedMaxTokens: 150),

        new("ocr-02", "ocr",
            "lnvoice #12345 Date: 2024-0l-15 Amount: $l,234.56 Payable to: Acme lnc. Due: 30 days frorn receipt",
            "OCR with l/1/I confusion and minor errors",
            ExpectedMinTokens: 10, ExpectedMaxTokens: 150),

        // ── Freeform Chat (5 prompts) ──────────────────────────────────
        new("freeform-01", "freeform",
            "What is the capital of France?",
            "Simple factual question",
            ExpectedMinTokens: 3, ExpectedMaxTokens: 100),

        new("freeform-02", "freeform",
            "Write a haiku about programming.",
            "Creative short-form generation",
            ExpectedMinTokens: 5, ExpectedMaxTokens: 80),

        new("freeform-03", "freeform",
            "Explain the difference between a stack and a queue data structure.",
            "Technical explanation",
            ExpectedMinTokens: 20, ExpectedMaxTokens: 400),

        new("freeform-04", "freeform",
            "List 5 tips for writing clean code.",
            "Numbered list generation",
            ExpectedMinTokens: 20, ExpectedMaxTokens: 400,
            MustContainTags: ["1", "2", "3", "4", "5"]),

        new("freeform-05", "freeform",
            "Translate 'Hello, how are you?' into Spanish, French, and German.",
            "Multi-language translation",
            ExpectedMinTokens: 10, ExpectedMaxTokens: 200),
    ];

    /// <summary>
    /// Returns prompts filtered to specific skill keys.
    /// </summary>
    public static IReadOnlyList<BenchmarkPrompt> GetBySkill(params string[] skillKeys)
    {
        var keys = new HashSet<string>(skillKeys, StringComparer.OrdinalIgnoreCase);
        return GetAll().Where(p => keys.Contains(p.SkillKey)).ToList();
    }
}
