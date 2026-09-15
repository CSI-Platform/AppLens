using System.Text.Json.Serialization;

namespace AppLens.Backend;

[JsonConverter(typeof(JsonStringEnumConverter<InstallationScope>))]
public enum InstallationScope { Unknown, ThisUser, AllUsers }
[JsonConverter(typeof(JsonStringEnumConverter<ApplicationKind>))]
public enum ApplicationKind { Desktop, Store, Runtime, Update, Driver, SystemComponent }
[JsonConverter(typeof(JsonStringEnumConverter<RemovalRoute>))]
public enum RemovalRoute { None, Msi, Vendor, Msix, WindowsSettings }

public sealed class DeviceSummary
{
    public string ComputerName { get; init; } = Environment.MachineName;
    public string UserName { get; init; } = Environment.UserName;
    public string OSDescription { get; init; } = "";
    public string OSArchitecture { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string Model { get; init; } = "";
    public long? TotalMemoryBytes { get; init; }
    public long? FreeMemoryBytes { get; init; }
    public long? UsedMemoryBytes => TotalMemoryBytes is > 0 && FreeMemoryBytes is >= 0 && FreeMemoryBytes <= TotalMemoryBytes
        ? TotalMemoryBytes - FreeMemoryBytes : null;
    public double? MemoryUsedPercent => UsedMemoryBytes is { } used && TotalMemoryBytes is > 0
        ? 100d * used / TotalMemoryBytes.Value : null;
    public DateTimeOffset? LastBootAt { get; init; }
    public List<string> Processors { get; init; } = [];
    public List<string> Graphics { get; init; } = [];
    public List<DiskSnapshot> Disks { get; init; } = [];
    [JsonIgnore] public List<ProbeStatus> ProbeStatuses { get; init; } = [];
    public long? SystemDriveFreeBytes => Disks.FirstOrDefault(d =>
        string.Equals(d.Volume.TrimEnd('\\'), Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase))?.FreeBytes;
}

public sealed class DiskSnapshot
{
    public string Volume { get; init; } = "";
    public string FileSystem { get; init; } = "";
    public long? TotalBytes { get; init; }
    public long? FreeBytes { get; init; }
    public long? UsedBytes => TotalBytes is > 0 && FreeBytes is >= 0 && FreeBytes <= TotalBytes
        ? TotalBytes - FreeBytes : null;
    public double? UsedPercent => UsedBytes is { } used && TotalBytes is > 0 ? 100d * used / TotalBytes.Value : null;
    public double? FreePercent => UsedPercent is { } used ? 100 - used : null;
    [JsonIgnore] public string Summary => $"{Volume}  {InventoryFormatting.Size(UsedBytes)} used / {InventoryFormatting.Size(TotalBytes)} · {InventoryFormatting.Size(FreeBytes)} free";
}

public sealed record RemovalRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public string AppId { get; init; } = "";
    public string AppName { get; init; } = "";
    public string AppVersion { get; init; } = "";
    public string Publisher { get; init; } = "";
    public InstallationScope Scope { get; init; }
    public string ScopeEvidence { get; init; } = "";
    public RemovalRoute Route { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public bool AsAdministrator { get; init; }
    public DateTimeOffset? BeforeCapturedAt { get; init; }
    public DateTimeOffset? AfterCapturedAt { get; init; }
    public string Outcome { get; init; } = "";
    public string Detail { get; init; } = "";
    public bool? StillInstalled { get; init; }
    public bool RestartRequired { get; init; }
    public List<DiskSnapshot> BeforeDisks { get; init; } = [];
    public List<DiskSnapshot> AfterDisks { get; init; } = [];
    [JsonIgnore] public string DiskChangeDisplay => string.Join("; ", BeforeDisks.Select(before =>
    {
        var after = AfterDisks.FirstOrDefault(d => d.Volume.Equals(before.Volume, StringComparison.OrdinalIgnoreCase));
        return before.FreeBytes is { } first && after?.FreeBytes is { } last && before.TotalBytes == after.TotalBytes
            ? $"{before.Volume}: {(last >= first ? "+" : "−")}{InventoryFormatting.Size(Math.Abs(last - first))} free space (observed)"
            : $"{before.Volume}: change unknown";
    }));
}

public static class InventoryFormatting
{
    public static string Size(long? bytes)
    {
        if (bytes is null || bytes < 0) return "Unknown";
        var value = (double)bytes.Value;
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB"];
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return index == 0 ? $"{value:N0} B" : $"{value:N2} {units[index]}";
    }
    public static string Scope(InstallationScope scope) => scope switch
    {
        InstallationScope.ThisUser => "This user", InstallationScope.AllUsers => "All users", _ => "Unknown"
    };
}
