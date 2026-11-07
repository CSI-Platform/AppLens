using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AppLens.Backend;

public sealed record DiagnosticReading(string Name, string Source, string State, JsonElement[] Rows, double DurationSeconds);

public sealed class DiagnosticsService
{
    private static readonly (string Id, string Name, string Source)[] Probes =
    [
        ("Processes", "Processes", "Windows process table"), ("Startup", "Startup", "StartupApproved registry and startup folders"),
        ("Services", "Services", "Service Control Manager"), ("Storage", "Storage", "Selected folders"),
        ("Git", "Git placement", "Selected folders"), ("Docker", "Docker storage", "Local Docker named pipe"),
        ("Wsl", "WSL", "Registered distributions"), ("Tools", "AI tools", "PATH"),
        ("Models", "Local models", "Selected model stores"), ("Graphics", "Graphics memory", "Windows GPU counters / nvidia-smi"),
        ("Llama", "llama.cpp", "Local device enumeration"), ("Cuda", "CUDA toolkit", "nvcc"),
        ("Torch", "PyTorch", "Local Python runtime"), ("Disk", "Disk health", "Storage reliability counters"),
        ("Battery", "Battery", "Battery CIM"), ("Encryption", "Disk encryption", "BitLocker CIM"),
        ("Network", "Network", "Windows network adapters"), ("Dns", "DNS", "Windows DNS client"),
        ("Listeners", "Listeners", "Windows TCP and UDP tables"), ("Firewall", "Firewall", "Windows firewall profiles"),
        ("Backup", "Backup outcomes", "Windows Backup event log"), ("Jobs", "Scheduled jobs", "Task Scheduler"),
        ("Updates", "Security updates", "Cached Windows Update metadata"), ("Reboot", "Reboot", "Windows servicing registry")
    ];

    private static readonly Lazy<string> Script = new(() =>
    {
        using var stream = typeof(DiagnosticsService).Assembly.GetManifestResourceStream("AppLens.Backend.WindowsDiagnostics.ps1")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public async Task<InventorySnapshot> RunAsync(InventorySnapshot inventory, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var readings = new DiagnosticReading[Probes.Length];
        await Parallel.ForEachAsync(Enumerable.Range(0, Probes.Length),
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken },
            async (index, token) =>
            {
                var probe = Probes[index];
                var limit = TimeSpan.FromSeconds(probe.Id is "Storage" or "Updates" ? 14 : 9);
                readings[index] = await RunProbeAsync(probe, Script.Value, limit, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        var findings = new List<JsonElement>();
        if (inventory.Machine.Disks.Any(disk => disk.LowSpace == true))
            findings.Add(JsonSerializer.SerializeToElement(new { Category = "Storage", Observation = "A volume is below the free space threshold." }));
        if (inventory.Machine.HighMemoryUse == true)
            findings.Add(JsonSerializer.SerializeToElement(new { Category = "Memory", Observation = "At least 85% of RAM was in use at capture." }));
        if (readings.Where(reading => reading.Name == "Disk health").SelectMany(reading => reading.Rows).Any(row =>
            row.TryGetProperty("Health", out var health) && health.GetString() is "Warning" or "Unhealthy"))
            findings.Add(JsonSerializer.SerializeToElement(new { Category = "Disk health", Observation = "A disk reported degraded health." }));
        return inventory with
        {
            Mode = "Diagnostics",
            Diagnostics = [.. readings, new("Findings", "Local rules", findings.Count > 0 ? "Succeeded" : "Empty", [.. findings], 0)],
            Duration = inventory.Duration + watch.Elapsed
        };
    }

    internal static DiagnosticReading Parse(string name, string source, string output, double elapsed)
    {
        using var json = JsonDocument.Parse(output);
        var root = json.RootElement;
        var state = root.GetProperty("State").GetString();
        var values = root.GetProperty("Rows");
        var rows = values.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row => row.Clone()).ToArray();
        if (rows.Length != values.GetArrayLength()) state = rows.Length == 0 ? "Failed" : "Partial";
        return new(name, source, state is "Succeeded" or "Empty" or "Inapplicable" or "Unavailable" or
            "PermissionLimited" or "Partial" or "Failed" ? state : "Failed", rows, elapsed);
    }

    internal static async Task<DiagnosticReading> RunProbeAsync(
        (string Id, string Name, string Source) probe, string script, TimeSpan limit, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(limit);
        // Pipe the embedded script to a fixed launcher so it never hits Windows'
        // command-line length limit or needs a writable script file.
        var launcher = Convert.ToBase64String(Encoding.Unicode.GetBytes(
            "[Console]::InputEncoding=[Text.Encoding]::UTF8; & ([ScriptBlock]::Create([Console]::In.ReadToEnd()))"));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true, RedirectStandardOutput = true, RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8, StandardInputEncoding = new UTF8Encoding(false),
                ArgumentList = { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand", launcher }
            }
        };
        DiagnosticReading Status(string state) => new(probe.Name, probe.Source, state, [], watch.Elapsed.TotalSeconds);
        try
        {
            budget.Token.ThrowIfCancellationRequested();
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync(budget.Token);
            var stderr = process.StandardError.ReadToEndAsync(budget.Token);
            await process.StandardInput.WriteAsync(("$Probe = '" + probe.Id + "'\n" + script).AsMemory(), budget.Token)
                .ConfigureAwait(false);
            process.StandardInput.Close();
            await process.WaitForExitAsync(budget.Token).ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).WaitAsync(budget.Token).ConfigureAwait(false);
            if (process.ExitCode != 0) return Status("Failed");
            return Parse(probe.Name, probe.Source, await stdout, watch.Elapsed.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
            cancellationToken.ThrowIfCancellationRequested();
            return Status("TimedOut");
        }
        catch (Win32Exception ex) { return Status(ex.NativeErrorCode == 5 ? "PermissionLimited" : "Unavailable"); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or IOException)
        { return Status("Failed"); }
    }
}
