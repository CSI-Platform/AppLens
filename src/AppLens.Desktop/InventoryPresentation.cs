using AppLens.Backend;

namespace AppLens.Desktop;

public static class InventoryPresentation
{
    public static bool HasNewlyAbsentAction(IEnumerable<RemovalRecord> previous, IEnumerable<RemovalRecord> current)
    {
        var previousById = previous.ToDictionary(a => a.Id);
        return current.Any(a => a.Verification == RemovalVerification.Absent &&
            (!previousById.TryGetValue(a.Id, out var old) || old.Verification != RemovalVerification.Absent));
    }

    public static IReadOnlyList<AppEntry> Filter(IEnumerable<AppEntry> apps, string query = "", int sort = 0,
        int scope = 0, int kind = 0, int removal = 0)
    {
        query = query.Trim();
        var rows = apps.Where(a => (query.Length == 0 || a.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || a.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
            (scope == 0 || a.Scope == (scope == 3 ? InstallationScope.Unknown : (InstallationScope)scope)) &&
            (kind == -1 ? a.Kind is ApplicationKind.Desktop or ApplicationKind.Store : kind == 0 || (int)a.Kind == kind - 1) &&
            (removal == 0 || removal == 1 && a.Removal.Route is RemovalRoute.Msi or RemovalRoute.Vendor or RemovalRoute.Msix ||
                removal == 2 && a.Removal.Route == RemovalRoute.WindowsSettings || removal == 3 && a.Removal.Route == RemovalRoute.None));
        IOrderedEnumerable<AppEntry> ordered = sort switch
        {
            1 => rows.OrderByDescending(a => a.Name, StringComparer.OrdinalIgnoreCase),
            2 => rows.OrderBy(a => !a.ReportedSizeBytes.HasValue).ThenByDescending(a => a.ReportedSizeBytes),
            3 => rows.OrderBy(a => !a.ReportedSizeBytes.HasValue).ThenBy(a => a.ReportedSizeBytes),
            4 => rows.OrderBy(a => a.Publisher, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase),
            _ => rows.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
        };
        return ordered.ThenBy(a => a.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string Details(AppEntry a) =>
        $"{a.Name} · {a.Version}\n{a.Publisher}\n{a.ScopeDisplay} · {a.Kind} · {a.Removal.AdministratorGuidance}\n{a.ScopeEvidence}\n{a.ClassificationEvidence}\n" +
        $"Reported size: {a.SizeDisplay} ({a.SizeSource})\nInstalled / serviced: {(a.InstalledOrServicedDate.Length == 0 ? "Unknown" : a.InstalledOrServicedDate)}\n" +
        $"Location: {(a.InstallLocation.Length == 0 ? "Unknown" : a.InstallLocation)}\nIdentity: {a.Id}\n{a.Removal.Explanation}";
}

public sealed class InventoryRow(AppEntry app, RemovalRecord? action = null, bool busy = false)
{
    public AppEntry App { get; } = app;
    private RemovalAssessment Assessment => App.Removal;
    public string Name => App.Name;
    public string Version => App.Version;
    public string Publisher => App.Publisher;
    // Store packages report a certificate subject; show its common name and keep the full subject in the tooltip.
    public string PublisherDisplay => App.Publisher.StartsWith("CN=", StringComparison.OrdinalIgnoreCase)
        ? App.Publisher[3..].Split(',')[0].Trim().Trim('"') : App.Publisher;
    public string SizeDisplay => App.SizeDisplay;
    public string ScopeDisplay => App.ScopeDisplay;
    public string KindDisplay => App.Kind == ApplicationKind.SystemComponent ? "System component" : App.Kind.ToString();
    public string RemovalReason => Assessment.Explanation;
    public string ActionLabel => Assessment.Route == RemovalRoute.WindowsSettings ? "Windows…" : "Uninstall";
    public string AccessibleSummary => $"{Name}, version {Version}, {PublisherDisplay}, {ScopeDisplay}, {KindDisplay}, {SizeDisplay}, {RemovalDisplay}";
    public override string ToString() => AccessibleSummary;
    public string AccessibleAction => $"{(Assessment.Route == RemovalRoute.WindowsSettings ? "Review in Windows" : "Uninstall")} {Name}, version {Version}, {ScopeDisplay}, {KindDisplay}";
    public bool CanAct => !busy && Assessment.CanAct && action?.ProcessState != RemovalProcessState.Running;
    public string RemovalDisplay => Assessment.Label + (action is null ? "" : $"\nLast action: {action.StatusDisplay}");
}

public sealed partial class DiskRow(DiskSnapshot disk)
{
    public string Volume { get; } = disk.Volume.TrimEnd('\\');
    public double UsedPercent { get; } = Math.Clamp(disk.UsedPercent ?? 0, 0, 100);
    public string PercentDisplay { get; } = disk.UsedPercent is { } used ? $"{used:N0}%" : "—";
    public string Figures { get; } = disk.UsedPercent is null ? "Size unknown" :
        $"{InventoryFormatting.Size(disk.FreeBytes)} free of {InventoryFormatting.Size(disk.TotalBytes)}";
    public string AccessibleSummary => $"{Volume} {PercentDisplay} used, {Figures}";
    public override string ToString() => AccessibleSummary;
}
