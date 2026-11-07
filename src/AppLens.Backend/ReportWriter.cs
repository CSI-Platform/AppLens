using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AppLens.Backend;

public sealed class ReportWriter
{
    private readonly RedactionService _redaction;
    public ReportWriter() : this(new RedactionService()) { }
    public ReportWriter(RedactionService redaction) => _redaction = redaction;

    public string WriteJson(InventorySnapshot snapshot, bool includeRawDetails = false)
    {
        var document = JsonSerializer.SerializeToNode(snapshot)!;
        if (!includeRawDetails) RedactValues(document, snapshot);
        return document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    public string WriteMarkdown(InventorySnapshot snapshot, bool includeRawDetails = false)
    {
        var text = new StringBuilder("# Installed workflow intake report\n\n");
        text.AppendLine("A snapshot to help your consultant design a workflow around this PC and its installed tools.");
        text.AppendLine("Reported sizes are estimates, not guaranteed reclaimable space. Disk changes are observations, not attribution.");
        foreach (var section in Sections(snapshot))
        {
            text.AppendLine($"\n## {section.Title}\n");
            text.AppendLine("| " + string.Join(" | ", section.Headers.Select(Cell)) + " |");
            text.AppendLine("| " + string.Join(" | ", section.Headers.Select(_ => "---")) + " |");
            foreach (var row in section.Rows) text.AppendLine("| " + string.Join(" | ", row.Select(value => Cell(Protect(value, snapshot, includeRawDetails)))) + " |");
        }
        return text.ToString();
    }

    public string WriteHtml(InventorySnapshot snapshot, bool includeRawDetails = false)
    {
        var html = new StringBuilder("""
            <!doctype html><html lang="en"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1"><title>Installed workflow intake report</title>
            <style>body{font:15px 'Segoe UI',sans-serif;color:#17191c;background:#f6f7f8;padding:2rem}
            h1{font-weight:600}section{overflow:auto}table{border-collapse:collapse;width:100%;background:white}
            td,th{padding:9px;border-bottom:1px solid #b5bbc2;text-align:left;vertical-align:top;overflow-wrap:anywhere}
            th{background:#e7e9ec}h2{margin-top:2rem}</style></head><body><h1>Installed workflow intake report</h1>
            <p>A snapshot to help your consultant design a workflow around this PC and its installed tools.</p>
            <p>Reported sizes are estimates, not guaranteed reclaimable space. Disk changes are observations, not attribution.</p>
            """);
        foreach (var section in Sections(snapshot))
        {
            html.Append($"<section><h2>{Formatting.Html(section.Title)}</h2><table><thead><tr>");
            foreach (var header in section.Headers) html.Append($"<th>{Formatting.Html(header)}</th>");
            html.Append("</tr></thead><tbody>");
            foreach (var row in section.Rows)
                html.Append("<tr>" + string.Join("", row.Select(c => $"<td>{Formatting.Html(Protect(c, snapshot, includeRawDetails))}</td>")) + "</tr>");
            html.Append("</tbody></table></section>");
        }
        html.Append("</body></html>");
        return html.ToString();
    }

    public async Task WriteAllAsync(InventorySnapshot snapshot, string directory, bool includeRawDetails,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        var stamp = snapshot.GeneratedAt.ToString("yyyyMMdd-HHmmss-fffffff");
        foreach (var (extension, content) in new[] { ("json", WriteJson(snapshot, includeRawDetails)),
            ("md", WriteMarkdown(snapshot, includeRawDetails)), ("html", WriteHtml(snapshot, includeRawDetails)) })
        {
            await using var stream = new FileStream(Path.Combine(directory, $"Installed-{stamp}.{extension}"),
                FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(content.AsMemory(), cancellationToken);
        }
    }

    private sealed record Section(string Title, string[] Headers, IEnumerable<string[]> Rows);
    private static IEnumerable<Section> Sections(InventorySnapshot snapshot)
    {
        var machine = snapshot.Machine;
        var intake = snapshot.WorkflowIntake;
        yield return new("Workflow intake", ["Field", "Observation"],
            intake.Facts.Select(f => new[] { f.Label, f.Value }).Prepend(["Coverage", intake.Coverage]));
        yield return new("Client context — provided by the client, not verified", ["Question", "Client response"], new[]
        {
            new[] { "What would you like to accomplish?", Response(snapshot.ClientContext.Goal) },
            new[] { "Which tools or online services do you use?", Response(snapshot.ClientContext.Services) },
            new[] { "Any constraints or requirements?", Response(snapshot.ClientContext.Constraints) }
        });
        yield return new("Planning boundaries", ["What this report does not establish"],
            intake.PlanningLimits.Select(limit => new[] { limit }));
        yield return new("Capture", ["Field", "Value"], new[]
        {
            new[] { "Captured", snapshot.GeneratedAt.ToString("O") },
            new[] { "Mode", snapshot.Mode },
            new[] { "Coverage", intake.Coverage },
            new[] { "Inventory entries", snapshot.Applications.Count().ToString() },
            new[] { "Entries without reported sizes", snapshot.Applications.Count(a => !a.ReportedSizeBytes.HasValue).ToString() },
            new[] { "First inventory results", snapshot.FirstResultsDuration is { } first ? $"{first.TotalSeconds:N3}s" : "Unknown" },
            new[] { "Collection duration", $"{snapshot.Duration.TotalSeconds:N3}s" }
        });
        yield return new("Local disks", ["Volume", "Total", "Used", "Used %", "Free", "Free %", "Attention"], machine.Disks.Select(d => new[]
        { d.Volume, InventoryFormatting.Size(d.TotalBytes), InventoryFormatting.Size(d.UsedBytes),
            Percent(d.UsedPercent), InventoryFormatting.Size(d.FreeBytes), Percent(d.FreePercent),
            d.LowSpace == true ? "Below 10% free" : d.LowSpace is null ? "Unknown" : "—" }));
        yield return new("Applications", ["Name", "Publisher", "Version", "Reported size", "Size source", "Scope", "Type",
            "Removal status", "Admin approval", "Identity", "Install location", "Installed / serviced", "Latest action", "Scope evidence", "Classification evidence"],
            snapshot.Applications.Select(a => new[] { a.Name, a.Publisher, a.Version, a.SizeDisplay, a.SizeSource,
                a.ScopeDisplay, a.Kind.ToString(), a.Removal.Label + ": " + a.Removal.Explanation, a.Removal.AdministratorGuidance, a.Id,
                a.InstallLocation, string.IsNullOrEmpty(a.InstalledOrServicedDate) ? "Unknown" : a.InstalledOrServicedDate,
                snapshot.Actions.LastOrDefault(action => action.AppId == a.Id)?.StatusDisplay ?? "None", a.ScopeEvidence, a.ClassificationEvidence }));
        yield return new("Device details", ["Field", "Value"], new[]
        {
            new[] { "Computer", machine.ComputerName }, new[] { "User", machine.UserName },
            new[] { "Windows", machine.OSDescription }, new[] { "Architecture", machine.OSArchitecture },
            new[] { "Manufacturer / model", $"{machine.Manufacturer} {machine.Model}".Trim() },
            new[] { "CPU", machine.Processors.Count == 0 ? "Unknown" : string.Join("; ", machine.Processors) },
            new[] { "GPU", machine.Graphics.Count == 0 ? "Unknown" : string.Join("; ", machine.Graphics) },
            new[] { "RAM total", InventoryFormatting.Size(machine.TotalMemoryBytes) },
            new[] { "RAM free", InventoryFormatting.Size(machine.FreeMemoryBytes) },
            new[] { "RAM used", Percent(machine.MemoryUsedPercent) + (machine.HighMemoryUse == true ? " (high)" : "") },
            new[] { "Uptime at capture", machine.LastBootAt is { } boot ? (snapshot.GeneratedAt - boot).ToString(@"d\.hh\:mm\:ss") : "Unknown" }
        });
        yield return new("Action history — original observations", ["App", "Identity", "Route", "Started", "Original completion", "Original outcome", "Original detail", "Originally still installed", "Restart required", "Version", "Publisher", "Scope", "Scope evidence"],
            snapshot.Actions.Select(a => new[] { a.AppName, a.AppId, a.Route.ToString(), a.StartedAt.ToString("O"),
                a.CompletedAt?.ToString("O") ?? "Pending", a.Outcome, a.Detail, a.StillInstalled?.ToString() ?? "Unknown",
                a.RestartRequired ? "Yes" : "No", a.AppVersion, a.Publisher, InventoryFormatting.Scope(a.Scope), a.ScopeEvidence }));
        yield return new("Current action verification", ["App", "Action ID", "Status", "Execution detail", "Exit code", "Process state", "Last checked", "First observed absent", "Revision", "History warning"],
            snapshot.Actions.Select(a => new[] { a.AppName, a.Id, a.StatusDisplay, a.ExecutionDetail ?? a.Detail,
                a.ExitCode?.ToString() ?? "Unknown", a.ProcessState.ToString(), a.LastCheckedAt?.ToString("O") ?? "Not checked",
                a.FirstAbsentAt?.ToString("O") ?? "Not observed", a.Revision.ToString(), a.PersistenceWarning ?? "None" }));
        yield return new("Scan coverage", ["Source", "State", "Duration", "Detail"], snapshot.ProbeStatuses.Select(p =>
            new[] { p.Name, p.State.ToString(), $"{p.Duration.TotalSeconds:N3}s", p.Message }));
        yield return new("CLI tools", ["Tool", "State"], snapshot.Inventory.CliTools.Select(tool =>
            new[] { tool.Name, tool.State }));
        if (snapshot.Diagnostics.Count > 0)
        {
            yield return new("Diagnostics", ["Probe", "Source", "State", "Observation"], snapshot.Diagnostics.SelectMany(reading =>
                reading.Rows.Select(row => string.Join(", ", row.EnumerateObject().Select(field =>
                    $"{field.Name} = {field.Value}"))).DefaultIfEmpty("—").Select(observation =>
                    new[] { reading.Name, reading.Source, reading.State, observation })));
        }
        yield return new("Observed disk changes after actions", ["Action ID", "App", "Before reading", "After reading", "Change", "Administrator handoff"],
            snapshot.Actions.Select(a => new[] { a.Id, a.AppName, a.BeforeCapturedAt?.ToString("O") ?? "Unknown",
                a.AfterCapturedAt?.ToString("O") ?? "Unknown", a.DiskChangeDisplay.Length == 0 ? "Unknown" : a.DiskChangeDisplay,
                a.AsAdministrator ? "Requested" : "No explicit handoff; Windows or vendor may request approval" }));
    }

    private string Protect(string value, InventorySnapshot snapshot, bool raw) =>
        raw ? value : _redaction.Redact(value, snapshot.Machine);
    private void RedactValues(JsonNode node, InventorySnapshot snapshot)
    {
        // Redact values before encoding: replacing serialized text can rename JSON
        // properties or miss identifiers that contain HTML/JSON escape characters.
        if (node is JsonObject obj)
        {
            foreach (var (key, child) in obj.ToArray())
            {
                if (key is "Mode" or "Platform" or "SchemaVersion" or "State") continue;
                if (child is JsonValue value && value.TryGetValue<string>(out var text)) obj[key] = Protect(text, snapshot, false);
                else if (child is not null) RedactValues(child, snapshot);
            }
        }
        else if (node is JsonArray array)
        {
            for (var i = 0; i < array.Count; i++)
                if (array[i] is JsonValue value && value.TryGetValue<string>(out var text)) array[i] = Protect(text, snapshot, false);
                else if (array[i] is { } child) RedactValues(child, snapshot);
        }
    }
    private static string Cell(string value)
    {
        var text = value.Replace("\r", " ").Replace("\n", " ").Replace("&", "&amp;")
            .Replace("<", "&lt;").Replace(">", "&gt;");
        // Captured strings and client notes are data, including Markdown links/images.
        foreach (var character in new[] { "\\", "`", "*", "_", "[", "]", "|" })
            text = text.Replace(character, "\\" + character);
        return text;
    }
    private static string Percent(double? value) => value.HasValue ? $"{value:N1}%" : "Unknown";
    private static string Response(string value) => string.IsNullOrWhiteSpace(value) ? "Not provided" : value.Trim();
}
