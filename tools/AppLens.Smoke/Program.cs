using AppLens.Backend;
using System.Text.Json;
using Microsoft.Data.Sqlite;

// Explicit developer verification entry point; no scan runs as part of normal builds.
var output = Path.GetFullPath(args.Length > 0 ? args[0] :
    Path.Combine("artifacts", "core-smoke-" + DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fffffff")));
if (Directory.Exists(output)) throw new IOException("Refusing to reuse an evidence directory.");
using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
var snapshot = await new AuditService().RunAsync(cancellation.Token);
using var database = new SqliteConnection("Data Source=:memory:");
database.Open();
await new ReportWriter().WriteAllAsync(snapshot, output, false, cancellation.Token);
var jsonFile = Directory.GetFiles(output, "*.json").Single();
using var json = JsonDocument.Parse(await File.ReadAllTextAsync(jsonFile));
if (json.RootElement.TryGetProperty("Tune", out _)) throw new InvalidDataException("Deferred diagnostics leaked into core output.");
Console.WriteLine(JsonSerializer.Serialize(new
{
    snapshot.SchemaVersion, AppCount = snapshot.Applications.Count(), snapshot.IsPartial,
    DurationSeconds = snapshot.Duration.TotalSeconds, FirstResultsSeconds = snapshot.FirstResultsDuration?.TotalSeconds,
    KnownSizes = snapshot.Applications.Count(a => a.ReportedSizeBytes.HasValue), Disks = snapshot.Machine.Disks.Count,
    SqliteVersion = database.ServerVersion,
    OutputDirectory = output,
    ReportFiles = Directory.GetFiles(output).Length
}));
