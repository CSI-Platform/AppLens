using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace AppLens.Backend;

public sealed class MachineCollector
{
    public async Task<DeviceSummary> CollectAsync(CancellationToken token)
    {
        var tasks = new[]
        {
            Query("Computer", "SELECT Manufacturer, Model FROM Win32_ComputerSystem", token),
            Query("Windows and memory", "SELECT Caption, Version, BuildNumber, TotalVisibleMemorySize, FreePhysicalMemory, LastBootUpTime FROM Win32_OperatingSystem", token),
            Query("Processor", "SELECT Name FROM Win32_Processor", token),
            Query("Graphics", "SELECT Name FROM Win32_VideoController", token),
            Query("Local fixed disks", "SELECT DeviceID, FileSystem, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 3", token)
        };
        var reads = await Task.WhenAll(tasks).ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        var computer = reads[0].Rows.FirstOrDefault();
        var os = reads[1].Rows.FirstOrDefault();
        DateTimeOffset? boot = null;
        try { if (Text(os, "LastBootUpTime") is { Length: > 0 } value) boot = ManagementDateTimeConverter.ToDateTime(value); } catch { }
        return new DeviceSummary
        {
            OSDescription = os is null ? RuntimeInformation.OSDescription :
                $"{Text(os, "Caption")} ({Text(os, "Version")}, build {Text(os, "BuildNumber")})",
            OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            Manufacturer = Text(computer, "Manufacturer"), Model = Text(computer, "Model"),
            TotalMemoryBytes = KiB(Number(os, "TotalVisibleMemorySize")), FreeMemoryBytes = KiB(Number(os, "FreePhysicalMemory")),
            LastBootAt = boot,
            Processors = reads[2].Rows.Select(r => Text(r, "Name")).Where(n => n.Length > 0).ToList(),
            Graphics = reads[3].Rows.Select(r => Text(r, "Name")).Where(n => n.Length > 0).ToList(),
            Disks = reads[4].Rows.Select(r => new DiskSnapshot
            { Volume = Text(r, "DeviceID"), FileSystem = Text(r, "FileSystem"), TotalBytes = Number(r, "Size"), FreeBytes = Number(r, "FreeSpace") }).ToList(),
            ProbeStatuses = reads.Select(r => r.Status).ToList()
        };
    }

    public async Task<MachineSummary> CollectLegacyAsync(CancellationToken token)
    {
        var data = await CollectAsync(token).ConfigureAwait(false);
        return new MachineSummary { ComputerName = data.ComputerName, UserName = data.UserName,
            OSDescription = data.OSDescription, OSArchitecture = data.OSArchitecture,
            Manufacturer = data.Manufacturer, Model = data.Model, TotalMemoryBytes = data.TotalMemoryBytes ?? 0,
            SystemDriveFreeBytes = data.SystemDriveFreeBytes ?? 0 };
    }

    private sealed record Reading(List<Dictionary<string, object?>> Rows, ProbeStatus Status);

    private static async Task<Reading> Query(string name, string query, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var rows = await Task.Run(() =>
            {
                using var searcher = new ManagementObjectSearcher(query);
                searcher.Options.Timeout = TimeSpan.FromSeconds(5);
                using var results = searcher.Get();
                var data = new List<Dictionary<string, object?>>();
                foreach (ManagementObject item in results)
                {
                    using (item)
                    {
                        token.ThrowIfCancellationRequested();
                        data.Add(item.Properties.Cast<PropertyData>().ToDictionary(p => p.Name, p => (object?)p.Value));
                    }
                }
                return data;
            }, token).WaitAsync(TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
            return new Reading(rows, new ProbeStatus { Name = name, State = rows.Count > 0 ? ProbeState.Succeeded : ProbeState.Partial,
                Message = rows.Count > 0 ? "" : "No reading available.", Duration = watch.Elapsed });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new Reading([], new ProbeStatus { Name = name, State = ProbeState.Failed,
                Message = ex is TimeoutException ? "Reading exceeded five seconds." : ex.Message, Duration = watch.Elapsed });
        }
    }

    private static string Text(Dictionary<string, object?>? row, string key) => row?.GetValueOrDefault(key)?.ToString() ?? "";
    private static long? Number(Dictionary<string, object?>? row, string key) =>
        long.TryParse(Text(row, key), out var value) && value >= 0 ? value : null;
    private static long? KiB(long? value) => value is >= 0 and <= (long.MaxValue / 1024) ? value * 1024 : null;
}
