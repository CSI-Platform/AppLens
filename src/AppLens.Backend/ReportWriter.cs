using System.Text;
using System.Text.Json;

namespace AppLens.Backend;

public sealed class ReportWriter
{
    private readonly RedactionService _redaction;
    public ReportWriter() : this(new RedactionService()) { }
    public ReportWriter(RedactionService redaction) => _redaction = redaction;

    public string WriteJson(InventorySnapshot snapshot, bool includeRawDetails = false) =>
        Protect(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }), snapshot, includeRawDetails);

    public string WriteMarkdown(InventorySnapshot snapshot, bool includeRawDetails = false)
    {
        var text = new StringBuilder("# AppLens inventory report\n\n");
        text.AppendLine("Reported sizes are estimates, not guaranteed reclaimable space. Disk changes are observations, not attribution.");
        foreach (var section in Sections(snapshot))
        {
            text.AppendLine($"\n## {section.Title}\n");
            text.AppendLine("| " + string.Join(" | ", section.Headers.Select(Cell)) + " |");
            text.AppendLine("| " + string.Join(" | ", section.Headers.Select(_ => "---")) + " |");
            foreach (var row in section.Rows) text.AppendLine("| " + string.Join(" | ", row.Select(Cell)) + " |");
        }
        return Protect(text.ToString(), snapshot, includeRawDetails);
    }

    public string WriteHtml(InventorySnapshot snapshot, bool includeRawDetails = false)
    {
        var html = new StringBuilder("""
            <!doctype html><html lang="en"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1"><title>AppLens inventory report</title>
            <style>body{font:15px 'Segoe UI',sans-serif;color:#17191c;background:#f6f7f8;padding:2rem}
            h1{font-weight:600}section{overflow:auto}table{border-collapse:collapse;width:100%;background:white}
            td,th{padding:9px;border-bottom:1px solid #b5bbc2;text-align:left;vertical-align:top;overflow-wrap:anywhere}
            th{background:#e7e9ec}h2{margin-top:2rem}</style></head><body><h1>AppLens inventory report</h1>
            <p>Reported sizes are estimates, not guaranteed reclaimable space. Disk changes are observations, not attribution.</p>
            """);
        foreach (var section in Sections(snapshot))
        {
            html.Append($"<section><h2>{Formatting.Html(section.Title)}</h2><table><thead><tr>");
            foreach (var header in section.Headers) html.Append($"<th>{Formatting.Html(header)}</th>");
            html.Append("</tr></thead><tbody>");
            foreach (var row in section.Rows)
                html.Append("<tr>" + string.Join("", row.Select(c => $"<td>{Formatting.Html(c)}</td>")) + "</tr>");
            html.Append("</tbody></table></section>");
        }
        html.Append("</body></html>");
        return Protect(html.ToString(), snapshot, includeRawDetails);
    }

    public async Task WriteAllAsync(InventorySnapshot snapshot, string directory, bool includeRawDetails,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        var stamp = snapshot.GeneratedAt.ToString("yyyyMMdd-HHmmss-fffffff");
        foreach (var (extension, content) in new[] { ("json", WriteJson(snapshot, includeRawDetails)),
            ("md", WriteMarkdown(snapshot, includeRawDetails)), ("html", WriteHtml(snapshot, includeRawDetails)) })
        {
            await using var stream = new FileStream(Path.Combine(directory, $"AppLens-{stamp}.{extension}"),
                FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content.AsMemory(), cancellationToken);
        }
    }

    private sealed record Section(string Title, string[] Headers, IEnumerable<string[]> Rows);
    private static IEnumerable<Section> Sections(InventorySnapshot snapshot)
    {
        var machine = snapshot.Machine;
        yield return new("Capture", ["Field", "Value"], new[]
        {
            new[] { "Captured", snapshot.GeneratedAt.ToString("O") },
            new[] { "Coverage", snapshot.IsPartial ? "Partial" : "Complete for listed sources" },
            new[] { "Inventory entries", snapshot.Applications.Count().ToString() },
            new[] { "First inventory results", snapshot.FirstResultsDuration is { } first ? $"{first.TotalSeconds:N3}s" : "Unknown" },
            new[] { "Collection duration", $"{snapshot.Duration.TotalSeconds:N3}s" }
        });
        yield return new("Local disks", ["Volume", "Total", "Used", "Used %", "Free", "Free %"], machine.Disks.Select(d => new[]
        { d.Volume, InventoryFormatting.Size(d.TotalBytes), InventoryFormatting.Size(d.UsedBytes),
            Percent(d.UsedPercent), InventoryFormatting.Size(d.FreeBytes), Percent(d.FreePercent) }));
        yield return new("Applications", ["Name", "Publisher", "Version", "Reported size", "Size source", "Scope", "Type",
            "Removal status", "Admin approval", "Identity", "Install location", "Installed / serviced", "Latest action", "Scope evidence"],
            snapshot.Applications.Select(a => new[] { a.Name, a.Publisher, a.Version, a.SizeDisplay, a.SizeSource,
                a.ScopeDisplay, a.Kind.ToString(), a.RemovalReason, a.AdminRequirement, a.Id,
                a.InstallLocation, string.IsNullOrEmpty(a.InstalledOrServicedDate) ? "Unknown" : a.InstalledOrServicedDate,
                snapshot.Actions.LastOrDefault(action => action.AppId == a.Id)?.Outcome ?? "None", a.ScopeEvidence }));
        yield return new("Device details", ["Field", "Value"], new[]
        {
            new[] { "Computer", machine.ComputerName }, new[] { "User", machine.UserName },
            new[] { "Windows", machine.OSDescription }, new[] { "Architecture", machine.OSArchitecture },
            new[] { "Manufacturer / model", $"{machine.Manufacturer} {machine.Model}".Trim() },
            new[] { "CPU", machine.Processors.Count == 0 ? "Unknown" : string.Join("; ", machine.Processors) },
            new[] { "GPU", machine.Graphics.Count == 0 ? "Unknown" : string.Join("; ", machine.Graphics) },
            new[] { "RAM total", InventoryFormatting.Size(machine.TotalMemoryBytes) },
            new[] { "RAM free", InventoryFormatting.Size(machine.FreeMemoryBytes) },
            new[] { "RAM used", Percent(machine.MemoryUsedPercent) },
            new[] { "Uptime at capture", machine.LastBootAt is { } boot ? (snapshot.GeneratedAt - boot).ToString(@"d\.hh\:mm\:ss") : "Unknown" }
        });
        yield return new("Action history", ["App", "Identity", "Route", "Started", "Completed", "Outcome", "Detail", "Still installed", "Restart required", "Version", "Publisher", "Scope", "Scope evidence"],
            snapshot.Actions.Select(a => new[] { a.AppName, a.AppId, a.Route.ToString(), a.StartedAt.ToString("O"),
                a.CompletedAt?.ToString("O") ?? "Pending", a.Outcome, a.Detail, a.StillInstalled?.ToString() ?? "Unknown",
                a.RestartRequired ? "Yes" : "No", a.AppVersion, a.Publisher, InventoryFormatting.Scope(a.Scope), a.ScopeEvidence }));
        yield return new("Scan coverage", ["Source", "State", "Duration", "Detail"], snapshot.ProbeStatuses.Select(p =>
            new[] { p.Name, p.State.ToString(), $"{p.Duration.TotalSeconds:N3}s", p.Message }));
        yield return new("Observed disk changes after actions", ["Action ID", "App", "Before reading", "After reading", "Change", "Administrator handoff"],
            snapshot.Actions.Select(a => new[] { a.Id, a.AppName, a.BeforeCapturedAt?.ToString("O") ?? "Unknown",
                a.AfterCapturedAt?.ToString("O") ?? "Unknown", a.DiskChangeDisplay.Length == 0 ? "Unknown" : a.DiskChangeDisplay,
                a.AsAdministrator ? "Requested" : "No explicit handoff; Windows or vendor may request approval" }));
    }

    private string Protect(string value, InventorySnapshot snapshot, bool raw) =>
        raw ? value : _redaction.Redact(value, snapshot.Machine);
    private static string Cell(string value) => Formatting.MarkdownEscape(value.Replace("\r", " ").Replace("\n", " ")).Replace("<", "&lt;").Replace(">", "&gt;");
    private static string Percent(double? value) => value.HasValue ? $"{value:N1}%" : "Unknown";
}
