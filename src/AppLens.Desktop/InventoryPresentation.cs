using AppLens.Backend;

namespace AppLens.Desktop;

public static class InventoryPresentation
{
    public static IReadOnlyList<AppEntry> Filter(IEnumerable<AppEntry> apps, string query = "", int sort = 0,
        int scope = 0, int kind = 0, int removal = 0)
    {
        query = query.Trim();
        var rows = apps.Where(a => (query.Length == 0 || a.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || a.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
            (scope == 0 || a.Scope == (scope == 3 ? InstallationScope.Unknown : (InstallationScope)scope)) &&
            (kind == -1 ? a.Kind is ApplicationKind.Desktop or ApplicationKind.Store : kind == 0 || (int)a.Kind == kind - 1) &&
            (removal == 0 || removal == 1 && a.CandidateRoute is RemovalRoute.Msi or RemovalRoute.Vendor or RemovalRoute.Msix ||
                removal == 2 && a.CandidateRoute == RemovalRoute.WindowsSettings || removal == 3 && a.CandidateRoute == RemovalRoute.None));
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
        $"{a.Name} · {a.Version}\n{a.Publisher}\n{a.ScopeDisplay} · {a.Kind} · {a.AdminRequirement}\n{a.ScopeEvidence}\n" +
        $"Reported size: {a.SizeDisplay} ({a.SizeSource})\nInstalled / serviced: {(a.InstalledOrServicedDate.Length == 0 ? "Unknown" : a.InstalledOrServicedDate)}\n" +
        $"Location: {(a.InstallLocation.Length == 0 ? "Unknown" : a.InstallLocation)}\nIdentity: {a.Id}\n{a.RemovalReason}";
}

public sealed class InventoryRow(AppEntry app, RemovalRecord? action = null, bool busy = false)
{
    public AppEntry App { get; } = app;
    private RemovalCommand Command { get; } = RemovalCommand.Resolve(app);
    public string Name => App.Name;
    public string Version => App.Version;
    public string Publisher => App.Publisher;
    public string SizeDisplay => App.SizeDisplay;
    public string ScopeDisplay => App.ScopeDisplay;
    public string KindDisplay => App.Kind == ApplicationKind.SystemComponent ? "System component" : App.Kind.ToString();
    public string RemovalReason => App.RemovalReason;
    public string ActionLabel => Command.Route == RemovalRoute.WindowsSettings ? "Windows…" : "Uninstall";
    public string AccessibleSummary => $"{Name}, version {Version}, {Publisher}, {ScopeDisplay}, {KindDisplay}, {SizeDisplay}, {RemovalDisplay}";
    public override string ToString() => AccessibleSummary;
    public string AccessibleAction => $"{(Command.Route == RemovalRoute.WindowsSettings ? "Review in Windows" : "Uninstall")} {Name}, version {Version}, {ScopeDisplay}, {KindDisplay}";
    public bool CanAct => !busy && Command.Route != RemovalRoute.None &&
        (Command.Route != RemovalRoute.Msix || !WindowsRemovalPlatform.IsOwnPackage(App.PackageFullName));
    public string RemovalDisplay => (Command.Route switch
    {
        RemovalRoute.Msi => "Windows Installer",
        RemovalRoute.Vendor => "Vendor uninstaller",
        RemovalRoute.Msix => "Current user · Store",
        RemovalRoute.WindowsSettings => "Review in Windows",
        _ => "Removal restricted"
    }) + (action is null ? "" : $"\nLast action: {action.Outcome}");
}
