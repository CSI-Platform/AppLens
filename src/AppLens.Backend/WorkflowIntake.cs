namespace AppLens.Backend;

/// <summary>Optional client statements, not facts inferred by the scanner.</summary>
public sealed record WorkflowClientContext
{
    public string Goal { get; init; } = "";
    public string Services { get; init; } = "";
    public string Constraints { get; init; } = "";
}

public sealed record IntakeFact(string Label, string Value);

/// <summary>A planning brief derived from the same capture used by the app table.</summary>
public sealed class WorkflowIntakeSummary
{
    public string Purpose { get; init; } = "Technical intake for designing a workflow that fits this PC.";
    public string Coverage { get; init; } = "Unknown — no source results were recorded.";
    public List<IntakeFact> Facts { get; init; } = [];
    public List<string> PlanningLimits { get; init; } =
    [
        "Installed software does not confirm a working installation, sign-in, license, or access to a service.",
        "Hardware and available memory/storage are observations at capture time, not a workload benchmark or a compatibility guarantee.",
        "Network connectivity between devices, cloud resources, account permissions, GPU memory, and firmware update availability are not assessed by this scan."
    ];

    public static WorkflowIntakeSummary From(InventorySnapshot snapshot)
    {
        var machine = snapshot.Machine;
        return new()
        {
            Coverage = snapshot.ProbeStatuses.Count == 0 ? "Unknown — no source results were recorded." :
                snapshot.IsPartial ? "Partial — see Scan coverage for missing or failed readings." : "Complete for listed sources only.",
            Facts =
            [
                new("Device", Known(string.Join(" ", new[] { machine.Manufacturer, machine.Model }.Where(s => !string.IsNullOrWhiteSpace(s))))),
                new("Operating system", Known(machine.OSDescription)),
                new("Architecture", Known(machine.OSArchitecture)),
                new("CPU", Join(machine.Processors)),
                new("GPU", Join(machine.Graphics)),
                new("RAM total", InventoryFormatting.Size(machine.TotalMemoryBytes)),
                new("RAM available at capture", InventoryFormatting.Size(machine.FreeMemoryBytes)),
                new("Storage", machine.Disks.Count == 0 ? "Unknown" : string.Join("; ", machine.Disks.Select(d =>
                    $"{Known(d.Volume)}: {InventoryFormatting.Size(d.FreeBytes)} free of {InventoryFormatting.Size(d.TotalBytes)}"))),
                new("Application entries observed", snapshot.Applications.Count(a => a.Kind is ApplicationKind.Desktop or ApplicationKind.Store).ToString()),
                new("Runtime entries observed", snapshot.Applications.Count(a => a.Kind == ApplicationKind.Runtime).ToString())
            ]
        };
    }

    public string ToDisplayText() => string.Join("\n", Facts.Select(f => $"{f.Label}: {f.Value}"));
    private static string Join(IEnumerable<string> values) => Known(string.Join("; ", values.Where(v => !string.IsNullOrWhiteSpace(v))));
    private static string Known(string? value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
}
