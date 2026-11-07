namespace AppLens.Backend;

public sealed record InventorySnapshot
{
    public string SchemaVersion { get; init; } = "3.0";
    public string Mode { get; init; } = "Inventory";
    public string Platform { get; init; } = "Windows";
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;
    public DeviceSummary Machine { get; init; } = new();
    public InventorySummary Inventory { get; init; } = new();
    public List<ProbeStatus> ProbeStatuses { get; init; } = [];
    public TimeSpan Duration { get; init; }
    public TimeSpan? FirstResultsDuration { get; init; }
    public List<RemovalRecord> Actions { get; init; } = [];
    public WorkflowClientContext ClientContext { get; init; } = new();
    public WorkflowIntakeSummary WorkflowIntake => WorkflowIntakeSummary.From(this);
    public IReadOnlyList<DiagnosticReading> Diagnostics { get; init; } = [];
    public double DurationSeconds => Duration.TotalSeconds;
    public bool IsPartial => ProbeStatuses.Any(p => p.State != ProbeState.Succeeded) ||
        Diagnostics.Any(p => p.State is not ("Succeeded" or "Empty" or "Inapplicable"));
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<AppEntry> Applications => Inventory.DesktopApplications
        .Concat(Inventory.StoreApplications).Concat(Inventory.RuntimesAndFrameworks).Concat(Inventory.SystemComponents)
        .Where(app => app.Source != "Group");
}
