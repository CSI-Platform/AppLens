using System.Text.Json.Serialization;

namespace AppLens.Backend;

public sealed class AuditSnapshot
{
    public string SchemaVersion { get; init; } = "1.0";
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;
    public MachineSummary Machine { get; init; } = new();
    public InventorySummary Inventory { get; init; } = new();
    public TuneSummary Tune { get; init; } = new();
    public List<Finding> Findings { get; init; } = [];
    public List<ProbeStatus> ProbeStatuses { get; init; } = [];
}

public sealed class MachineSummary
{
    public string ComputerName { get; init; } = Environment.MachineName;
    public string UserName { get; init; } = Environment.UserName;
    public string OSDescription { get; init; } = "";
    public string OSArchitecture { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string Model { get; init; } = "";
    public long TotalMemoryBytes { get; init; }
    public long SystemDriveFreeBytes { get; init; }
}

public sealed class InventorySummary
{
    public List<AppEntry> DesktopApplications { get; init; } = [];
    public List<AppEntry> StoreApplications { get; init; } = [];
    public List<AppEntry> RuntimesAndFrameworks { get; init; } = [];
}

public sealed class AppEntry
{
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string Source { get; init; } = "";
    public bool UserInstalled { get; init; }
}

public sealed class TuneSummary
{
    public List<ProcessSnapshot> TopProcesses { get; init; } = [];
    public List<StartupEntry> StartupEntries { get; init; } = [];
    public List<ServiceSnapshot> Services { get; init; } = [];
    public List<StorageHotspot> StorageHotspots { get; init; } = [];
    public List<RepoPlacement> RepoPlacements { get; init; } = [];
    public List<ToolProbe> ToolProbes { get; init; } = [];
}

public sealed class ProcessSnapshot
{
    public string Name { get; init; } = "";
    public int Id { get; init; }
    public long WorkingSetBytes { get; init; }
    public double CpuSeconds { get; init; }
}

public sealed class StartupEntry
{
    public string Name { get; init; } = "";
    public string State { get; init; } = "Unknown";
    public string Location { get; init; } = "";
    public string Command { get; init; } = "";
}

public sealed class ServiceSnapshot
{
    public string Name { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Status { get; init; } = "";
    public string StartType { get; init; } = "";
}

public sealed class StorageHotspot
{
    public string Location { get; init; } = "";
    public string Path { get; init; } = "";
    public long? Bytes { get; init; }
}

public sealed class RepoPlacement
{
    public string Root { get; init; } = "";
    public int RepoCount { get; init; }
    public string Sample { get; init; } = "";
    public bool Truncated { get; init; }
}

public sealed class ToolProbe
{
    public string Name { get; init; } = "";
    public string Status { get; init; } = "";
    public string Output { get; init; } = "";
}

public sealed class Finding
{
    public FindingSeverity Severity { get; init; }
    public FindingCategory Category { get; init; }
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
}

[JsonConverter(typeof(JsonStringEnumConverter<FindingSeverity>))]
public enum FindingSeverity
{
    Stable,
    Review,
    Optional
}

[JsonConverter(typeof(JsonStringEnumConverter<FindingCategory>))]
public enum FindingCategory
{
    Stability,
    Startup,
    Services,
    Storage,
    RepoPlacement,
    Tooling,
    Privacy
}

public sealed class ProbeStatus
{
    public string Name { get; init; } = "";
    public ProbeState State { get; init; }
    public string Message { get; init; } = "";
    public TimeSpan Duration { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<ProbeState>))]
public enum ProbeState
{
    Succeeded,
    Partial,
    Failed,
    Skipped
}
