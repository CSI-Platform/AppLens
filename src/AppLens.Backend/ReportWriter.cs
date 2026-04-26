using System.Text;
using System.Text.Json;

namespace AppLens.Backend;

public sealed class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly RedactionService _redactionService;

    public ReportWriter()
        : this(new RedactionService())
    {
    }

    public ReportWriter(RedactionService redactionService)
    {
        _redactionService = redactionService;
    }

    public string WriteJson(AuditSnapshot snapshot, bool includeRawDetails = false)
    {
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        return includeRawDetails ? json : _redactionService.Redact(json, snapshot);
    }

    public string WriteMarkdown(AuditSnapshot snapshot, bool includeRawDetails = false)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# AppLens-desktop Audit Report");
        builder.AppendLine();
        builder.AppendLine($"Generated: {snapshot.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"Computer: {snapshot.Machine.ComputerName}");
        builder.AppendLine($"User: {snapshot.Machine.UserName}");
        builder.AppendLine($"OS: {snapshot.Machine.OSDescription}");
        builder.AppendLine($"RAM: {Formatting.Size(snapshot.Machine.TotalMemoryBytes)}");
        builder.AppendLine($"System Drive Free: {Formatting.Size(snapshot.Machine.SystemDriveFreeBytes)}");
        builder.AppendLine();

        AppendFindings(builder, snapshot);
        AppendInventory(builder, snapshot);
        AppendTune(builder, snapshot);
        AppendProbeStatuses(builder, snapshot);

        var markdown = builder.ToString();
        return includeRawDetails ? markdown : _redactionService.Redact(markdown, snapshot);
    }

    public string WriteHtml(AuditSnapshot snapshot, bool includeRawDetails = false)
    {
        var findings = snapshot.Findings
            .Select(finding => $"""
                <tr>
                  <td><span class="pill {finding.Severity.ToString().ToLowerInvariant()}">{Formatting.Html(finding.Severity.ToString())}</span></td>
                  <td>{Formatting.Html(finding.Category.ToString())}</td>
                  <td>{Formatting.Html(finding.Title)}</td>
                  <td>{Formatting.Html(finding.Detail)}</td>
                </tr>
                """);

        var html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>AppLens-desktop Audit Report</title>
              <style>
                :root { color-scheme: light; --ink:#14151a; --muted:#656b76; --line:#d9dde5; --accent:#0b6bcb; --surface:#f5f7fb; }
                body { margin:0; font-family:"Segoe UI", Arial, sans-serif; color:var(--ink); background:white; }
                header { padding:32px 40px; color:white; background:#101820; }
                main { padding:28px 40px 44px; }
                h1 { margin:0 0 8px; font-size:32px; letter-spacing:0; }
                h2 { margin:28px 0 12px; font-size:20px; }
                .brand { font-size:13px; text-transform:uppercase; letter-spacing:.08em; opacity:.8; }
                .summary { display:grid; grid-template-columns:repeat(4,minmax(0,1fr)); gap:12px; margin-top:18px; }
                .metric { border:1px solid var(--line); padding:14px; background:var(--surface); }
                .label { color:var(--muted); font-size:12px; }
                .value { margin-top:6px; font-size:16px; font-weight:600; overflow-wrap:anywhere; }
                table { width:100%; border-collapse:collapse; font-size:13px; }
                th, td { border-bottom:1px solid var(--line); padding:9px 8px; text-align:left; vertical-align:top; }
                th { color:var(--muted); font-weight:600; }
                .pill { display:inline-block; min-width:58px; padding:3px 8px; border-radius:999px; font-size:12px; text-align:center; background:#e9edf5; }
                .stable { color:#0b5e35; background:#dff5e9; }
                .review { color:#7a3e00; background:#fff1d8; }
                .optional { color:#174ea6; background:#e8f0fe; }
                @media (max-width: 900px) { .summary { grid-template-columns:1fr 1fr; } main, header { padding-left:20px; padding-right:20px; } }
              </style>
            </head>
            <body>
              <header>
                <div class="brand">CSI / AppLens-desktop</div>
                <h1>Workstation Audit Report</h1>
                <div>Read-only local snapshot generated {{Formatting.Html(snapshot.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss"))}}</div>
              </header>
              <main>
                <section class="summary">
                  <div class="metric"><div class="label">Computer</div><div class="value">{{Formatting.Html(snapshot.Machine.ComputerName)}}</div></div>
                  <div class="metric"><div class="label">User</div><div class="value">{{Formatting.Html(snapshot.Machine.UserName)}}</div></div>
                  <div class="metric"><div class="label">RAM</div><div class="value">{{Formatting.Html(Formatting.Size(snapshot.Machine.TotalMemoryBytes))}}</div></div>
                  <div class="metric"><div class="label">Free Space</div><div class="value">{{Formatting.Html(Formatting.Size(snapshot.Machine.SystemDriveFreeBytes))}}</div></div>
                </section>

                <h2>Findings</h2>
                <table><thead><tr><th>Severity</th><th>Category</th><th>Finding</th><th>Detail</th></tr></thead><tbody>
                {{string.Join(Environment.NewLine, findings)}}
                </tbody></table>

                {{HtmlTable("Desktop Applications", ["Name", "Version", "Publisher", "Source"], snapshot.Inventory.DesktopApplications.Select(app => new[] { app.Name, app.Version, app.Publisher, app.Source }))}}
                {{HtmlTable("Store Applications", ["Name", "Version", "Publisher", "Source"], snapshot.Inventory.StoreApplications.Select(app => new[] { app.Name, app.Version, app.Publisher, app.Source }))}}
                {{HtmlTable("Top Processes", ["Name", "PID", "Memory", "CPU Seconds"], snapshot.Tune.TopProcesses.Select(process => new[] { process.Name, process.Id.ToString(), Formatting.Size(process.WorkingSetBytes), process.CpuSeconds.ToString("N1") }))}}
                {{HtmlTable("Startup Entries", ["Name", "State", "Location", "Command"], snapshot.Tune.StartupEntries.Select(entry => new[] { entry.Name, entry.State, entry.Location, entry.Command }))}}
                {{HtmlTable("Storage Hotspots", ["Location", "Size", "Path"], snapshot.Tune.StorageHotspots.Select(item => new[] { item.Location, Formatting.Size(item.Bytes), item.Path }))}}
              </main>
            </body>
            </html>
            """;

        return includeRawDetails ? html : _redactionService.Redact(html, snapshot);
    }

    public async Task WriteAllAsync(AuditSnapshot snapshot, string directory, bool includeRawDetails, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        var stamp = snapshot.GeneratedAt.ToString("yyyyMMdd-HHmmss");
        await File.WriteAllTextAsync(Path.Combine(directory, $"AppLens-{stamp}.json"), WriteJson(snapshot, includeRawDetails), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, $"AppLens-{stamp}.md"), WriteMarkdown(snapshot, includeRawDetails), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(directory, $"AppLens-{stamp}.html"), WriteHtml(snapshot, includeRawDetails), cancellationToken);
    }

    private static void AppendFindings(StringBuilder builder, AuditSnapshot snapshot)
    {
        builder.AppendLine("## Findings");
        builder.AppendLine();
        builder.AppendLine("| Severity | Category | Finding | Detail |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var finding in snapshot.Findings)
        {
            builder.AppendLine($"| {finding.Severity} | {finding.Category} | {Formatting.MarkdownEscape(finding.Title)} | {Formatting.MarkdownEscape(finding.Detail)} |");
        }

        builder.AppendLine();
    }

    private static void AppendInventory(StringBuilder builder, AuditSnapshot snapshot)
    {
        builder.AppendLine("## App Inventory");
        AppendApps(builder, "Desktop Applications", snapshot.Inventory.DesktopApplications);
        AppendApps(builder, "Store Applications", snapshot.Inventory.StoreApplications);
        AppendApps(builder, "Runtimes & Frameworks", snapshot.Inventory.RuntimesAndFrameworks);
    }

    private static void AppendApps(StringBuilder builder, string title, IEnumerable<AppEntry> apps)
    {
        builder.AppendLine();
        builder.AppendLine($"### {title}");
        builder.AppendLine();
        builder.AppendLine("| Name | Version | Publisher | Source |");
        builder.AppendLine("| --- | --- | --- | --- |");
        foreach (var app in apps)
        {
            builder.AppendLine($"| {Formatting.MarkdownEscape(app.Name)} | {Formatting.MarkdownEscape(app.Version)} | {Formatting.MarkdownEscape(app.Publisher)} | {Formatting.MarkdownEscape(app.Source)} |");
        }
    }

    private static void AppendTune(StringBuilder builder, AuditSnapshot snapshot)
    {
        builder.AppendLine();
        builder.AppendLine("## Workstation Diagnostics");
        AppendTable(builder, "Top Processes", ["Name", "PID", "Memory", "CPU Seconds"],
            snapshot.Tune.TopProcesses.Select(process => new[] { process.Name, process.Id.ToString(), Formatting.Size(process.WorkingSetBytes), process.CpuSeconds.ToString("N1") }));
        AppendTable(builder, "Startup Entries", ["Name", "State", "Location", "Command"],
            snapshot.Tune.StartupEntries.Select(entry => new[] { entry.Name, entry.State, entry.Location, entry.Command }));
        AppendTable(builder, "Key Services", ["Name", "Display Name", "Status", "Start Type"],
            snapshot.Tune.Services.Select(service => new[] { service.Name, service.DisplayName, service.Status, service.StartType }));
        AppendTable(builder, "Storage Hotspots", ["Location", "Size", "Path"],
            snapshot.Tune.StorageHotspots.Select(item => new[] { item.Location, Formatting.Size(item.Bytes), item.Path }));
        AppendTable(builder, "Repo Placement", ["Root", "Repo Count", "Sample"],
            snapshot.Tune.RepoPlacements.Select(repo => new[] { repo.Root, repo.Truncated ? $"{repo.RepoCount}+" : repo.RepoCount.ToString(), repo.Sample }));
        AppendTable(builder, "Tool Probes", ["Name", "Status", "Output"],
            snapshot.Tune.ToolProbes.Select(tool => new[] { tool.Name, tool.Status, tool.Output }));
    }

    private static void AppendProbeStatuses(StringBuilder builder, AuditSnapshot snapshot)
    {
        AppendTable(builder, "Probe Statuses", ["Name", "State", "Duration", "Message"],
            snapshot.ProbeStatuses.Select(probe => new[] { probe.Name, probe.State.ToString(), probe.Duration.TotalSeconds.ToString("N1") + "s", probe.Message }));
    }

    private static void AppendTable(StringBuilder builder, string title, string[] columns, IEnumerable<string[]> rows)
    {
        builder.AppendLine();
        builder.AppendLine($"### {title}");
        builder.AppendLine();
        builder.AppendLine("| " + string.Join(" | ", columns.Select(Formatting.MarkdownEscape)) + " |");
        builder.AppendLine("| " + string.Join(" | ", columns.Select(_ => "---")) + " |");
        foreach (var row in rows)
        {
            builder.AppendLine("| " + string.Join(" | ", row.Select(Formatting.MarkdownEscape)) + " |");
        }
    }

    private static string HtmlTable(string title, string[] columns, IEnumerable<string[]> rows)
    {
        var header = string.Join("", columns.Select(column => $"<th>{Formatting.Html(column)}</th>"));
        var body = string.Join(Environment.NewLine, rows.Select(row =>
            "<tr>" + string.Join("", row.Select(cell => $"<td>{Formatting.Html(cell)}</td>")) + "</tr>"));

        return $"""
            <h2>{Formatting.Html(title)}</h2>
            <table>
              <thead><tr>{header}</tr></thead>
              <tbody>{body}</tbody>
            </table>
            """;
    }
}
