using AppLens.Backend;
using System.Text.Json;
using Microsoft.Data.Sqlite;

// Explicit developer verification entry point; no scan runs as part of normal builds.
var diagnostics = args.Contains("--diagnostics");
var directory = args.FirstOrDefault(arg => arg != "--diagnostics");
var output = Path.GetFullPath(directory ??
    Path.Combine("artifacts", "core-smoke-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fffffff")));
if (Directory.Exists(output)) throw new IOException("Refusing to reuse an evidence directory.");
using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(diagnostics ? 120 : 20));
var snapshot = await new AuditService().RunAsync(cancellation.Token);
if (diagnostics) snapshot = await new DiagnosticsService().RunAsync(snapshot, cancellation.Token);
using var database = new SqliteConnection("Data Source=:memory:");
database.Open();
await new ReportWriter().WriteAllAsync(snapshot, output, false, cancellation.Token);
var jsonFile = Directory.GetFiles(output, "*.json").Single();
using var json = JsonDocument.Parse(await File.ReadAllTextAsync(jsonFile));
if (json.RootElement.GetProperty("Diagnostics").GetArrayLength() != snapshot.Diagnostics.Count ||
    diagnostics != (snapshot.Diagnostics.Count > 0))
    throw new InvalidDataException("The report did not match the requested scan mode.");
Console.WriteLine(JsonSerializer.Serialize(new
{
    snapshot.SchemaVersion, snapshot.Mode, AppCount = snapshot.Applications.Count(), snapshot.IsPartial,
    DurationSeconds = snapshot.Duration.TotalSeconds, FirstResultsSeconds = snapshot.FirstResultsDuration?.TotalSeconds,
    KnownSizes = snapshot.Applications.Count(a => a.ReportedSizeBytes.HasValue), Disks = snapshot.Machine.Disks.Count,
    SqliteVersion = database.ServerVersion,
    OutputDirectory = output,
    ReportFiles = Directory.GetFiles(output).Length,
    Diagnostics = snapshot.Diagnostics.Select(probe => new { probe.Name, probe.State, Rows = probe.Rows.Length })
}));
