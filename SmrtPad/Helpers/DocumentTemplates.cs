using System.Collections.Generic;
using SmrtPad.Models;

namespace SmrtPad.Helpers;

/// <summary>
/// Provides the built-in document templates available from the backstage "Templates" panel.
/// </summary>
public static class DocumentTemplates
{
    public static IReadOnlyList<DocumentTemplate> All { get; } =
    [
        new DocumentTemplate(
            Key:         "blank",
            DisplayName: "Blank Document",
            Description: "Start with a clean, empty page.",
            PlainContent: ""),

        new DocumentTemplate(
            Key:         "letter",
            DisplayName: "Business Letter",
            Description: "Formal letter with sender, recipient, and body sections.",
            PlainContent:
                "[Your Name]\n" +
                "[Your Address]\n" +
                "[City, State, ZIP]\n" +
                "[Date]\n" +
                "\n" +
                "[Recipient Name]\n" +
                "[Title]\n" +
                "[Company]\n" +
                "[Address]\n" +
                "\n" +
                "Dear [Recipient Name],\n" +
                "\n" +
                "[Opening paragraph — introduce the purpose of your letter.]\n" +
                "\n" +
                "[Body paragraph — provide details, background, or supporting information.]\n" +
                "\n" +
                "[Closing paragraph — summarise your request or next steps.]\n" +
                "\n" +
                "Sincerely,\n" +
                "\n" +
                "[Your Name]\n" +
                "[Your Title]"),

        new DocumentTemplate(
            Key:         "report",
            DisplayName: "Report",
            Description: "Structured report with title, summary, and sections.",
            PlainContent:
                "REPORT TITLE\n" +
                "\n" +
                "Prepared by: [Author]\n" +
                "Date: [Date]\n" +
                "\n" +
                "EXECUTIVE SUMMARY\n" +
                "\n" +
                "[One or two paragraphs summarising the key findings and recommendations.]\n" +
                "\n" +
                "1. INTRODUCTION\n" +
                "\n" +
                "[Describe the purpose, scope, and background of the report.]\n" +
                "\n" +
                "2. FINDINGS\n" +
                "\n" +
                "[Present the main findings, data, and analysis.]\n" +
                "\n" +
                "3. RECOMMENDATIONS\n" +
                "\n" +
                "[List actionable recommendations based on the findings.]\n" +
                "\n" +
                "4. CONCLUSION\n" +
                "\n" +
                "[Summarise the report and reinforce the key takeaways.]\n" +
                "\n" +
                "REFERENCES\n" +
                "\n" +
                "[1] [Source]\n" +
                "[2] [Source]"),

        new DocumentTemplate(
            Key:         "resume",
            DisplayName: "Resume / CV",
            Description: "Professional resume with experience, education, and skills.",
            PlainContent:
                "[Full Name]\n" +
                "[Phone] | [Email] | [LinkedIn] | [City, State]\n" +
                "\n" +
                "PROFESSIONAL SUMMARY\n" +
                "\n" +
                "[Two or three sentences describing your professional background and key strengths.]\n" +
                "\n" +
                "WORK EXPERIENCE\n" +
                "\n" +
                "[Job Title] — [Company Name], [City]          [Start Date] – [End Date]\n" +
                "• [Key responsibility or achievement]\n" +
                "• [Key responsibility or achievement]\n" +
                "\n" +
                "[Job Title] — [Company Name], [City]          [Start Date] – [End Date]\n" +
                "• [Key responsibility or achievement]\n" +
                "\n" +
                "EDUCATION\n" +
                "\n" +
                "[Degree], [Major] — [University], [Year]\n" +
                "\n" +
                "SKILLS\n" +
                "\n" +
                "[Skill 1], [Skill 2], [Skill 3], [Skill 4]"),

        new DocumentTemplate(
            Key:         "meeting",
            DisplayName: "Meeting Notes",
            Description: "Capture attendees, agenda items, and action points.",
            PlainContent:
                "MEETING NOTES\n" +
                "\n" +
                "Date:       [Date]\n" +
                "Time:       [Time]\n" +
                "Location:   [Location / Video call link]\n" +
                "Facilitator:[Name]\n" +
                "\n" +
                "ATTENDEES\n" +
                "\n" +
                "• [Name, Role]\n" +
                "• [Name, Role]\n" +
                "\n" +
                "AGENDA\n" +
                "\n" +
                "1. [Topic]\n" +
                "2. [Topic]\n" +
                "3. Any other business\n" +
                "\n" +
                "DISCUSSION NOTES\n" +
                "\n" +
                "[Summary of decisions and key points raised during the meeting.]\n" +
                "\n" +
                "ACTION ITEMS\n" +
                "\n" +
                "# | Action                           | Owner    | Due Date\n" +
                "--|----------------------------------|----------|----------\n" +
                "1 | [Action]                         | [Owner]  | [Date]\n" +
                "2 | [Action]                         | [Owner]  | [Date]\n" +
                "\n" +
                "NEXT MEETING\n" +
                "\n" +
                "Date: [Date]   Time: [Time]   Location: [Location]"),
    ];
}
