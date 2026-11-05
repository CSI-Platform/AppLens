namespace AppLens.Backend;

/// <summary>The core client/report contract; contains no deferred diagnostics.</summary>
public sealed class InventorySnapshot
{
    public string SchemaVersion { get; init; } = "2.0";
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;
    public DeviceSummary Machine { get; init; } = new();
    public InventorySummary Inventory { get; init; } = new();
    public List<ProbeStatus> ProbeStatuses { get; init; } = [];
    public TimeSpan Duration { get; init; }
    public TimeSpan? FirstResultsDuration { get; init; }
    public List<RemovalRecord> Actions { get; init; } = [];
    public bool IsPartial => ProbeStatuses.Any(p => p.State != ProbeState.Succeeded);
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<AppEntry> Applications => Inventory.DesktopApplications
        .Concat(Inventory.StoreApplications).Concat(Inventory.RuntimesAndFrameworks).Concat(Inventory.SystemComponents)
        .Where(app => app.Source != "Group");
}
