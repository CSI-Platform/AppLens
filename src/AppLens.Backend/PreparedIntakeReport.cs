namespace AppLens.Backend;

public enum IntakeReportFormat { Markdown, Json, Html }

/// <summary>Freeze the report before review so sharing cannot substitute a later scan or edit.</summary>
public sealed record PreparedIntakeReport(string FileName, string Extension, string Content, string ReviewSummary,
    bool IncludesPersonalIdentifiers)
{
    public static PreparedIntakeReport Create(InventorySnapshot snapshot, IntakeReportFormat format,
        bool includePersonalIdentifiers = false)
    {
        var writer = new ReportWriter();
        var (extension, content) = format switch
        {
            IntakeReportFormat.Json => (".json", writer.WriteJson(snapshot, includePersonalIdentifiers)),
            IntakeReportFormat.Html => (".html", writer.WriteHtml(snapshot, includePersonalIdentifiers)),
            _ => (".md", writer.WriteMarkdown(snapshot, includePersonalIdentifiers))
        };
        var intake = snapshot.WorkflowIntake;
        var context = snapshot.ClientContext;
        var summary = $"Captured: {snapshot.GeneratedAt:g}\n{intake.Coverage}\n\n{intake.ToDisplayText()}\n\n" +
            $"Your notes (not independently verified)\nGoal: {Response(context.Goal)}\n" +
            $"Tools / services: {Response(context.Services)}\nConstraints: {Response(context.Constraints)}";
        if (!includePersonalIdentifiers) summary = new RedactionService().Redact(summary, snapshot.Machine);
        return new($"Installed-Workflow-{snapshot.GeneratedAt:yyyyMMdd-HHmmss-fff}{extension}",
            extension, content, summary, includePersonalIdentifiers);
    }

    private static string Response(string text) => string.IsNullOrWhiteSpace(text) ? "Not provided" : text.Trim();
}
