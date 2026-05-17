using System.Globalization;
using System.Text;
using AppLens.Backend;

namespace AppLens.Desktop;

public sealed class DashboardPresentation
{
    public DashboardSummaryPresentation Summary { get; init; } = new();
    public DashboardRailPresentation Rail { get; init; } = new();
    public List<ModuleCardPresentation> ModuleCards { get; init; } = [];
    public List<PendingTuneApprovalPresentation> PendingActions { get; init; } = [];
    public List<TuneActionLifecyclePresentation> TuneActionLifecycles { get; init; } = [];
    public List<LedgerEventPresentation> RecentLedgerEvents { get; init; } = [];
    public string ModuleEmptyState { get; init; } = "No module cards available.";
    public string PendingApprovalEmptyState { get; init; } = "No pending Tune approvals.";
    public string TuneLifecycleEmptyState { get; init; } = "No Tune action lifecycle records.";
    public string LedgerEmptyState { get; init; } = "No recent ledger events.";
    public string ActiveAppsEmptyState { get; init; } = "Run a scan to populate active app rows.";

    public static DashboardPresentation FromState(AppLensDashboardState state) =>
        new()
        {
            Summary = new DashboardSummaryPresentation
            {
                OverallState = state.Summary.OverallState,
                ModuleCoverage = $"{state.Summary.AvailableModuleCount} available / {state.Summary.BlockedModuleCount} blocked",
                PendingApprovals = $"{state.Summary.PendingActionCount} pending",
                RecentEvents = CountLabel(state.Summary.RecentEventCount, "event"),
                LastEvent = state.Summary.LastLedgerEventAt is { } lastEvent
                    ? FormatTimestamp(lastEvent)
                    : "No ledger events"
            },
            Rail = new DashboardRailPresentation
            {
                DashboardBadge = state.Summary.OverallState.Equals("Ready", StringComparison.OrdinalIgnoreCase) ? "live" : "action",
                InventoryBadge = state.Summary.ModuleCount.ToString(CultureInfo.InvariantCulture),
                TunePlanBadge = state.Summary.PendingActionCount.ToString(CultureInfo.InvariantCulture),
                ReportsBadge = state.Summary.RecentEventCount.ToString(CultureInfo.InvariantCulture),
                Modules = state.ModuleCards.Select(ToModuleRailBadge).ToList()
            },
            ModuleCards = state.ModuleCards.Select(ToModuleCard).ToList(),
            PendingActions = state.PendingActions.Select(ToPendingAction).ToList(),
            TuneActionLifecycles = state.TuneActionLifecycles.Select(ToTuneActionLifecycle).ToList(),
            RecentLedgerEvents = state.RecentLedgerEvents.Select(ToLedgerEvent).ToList()
        };

    public static List<ActiveAppRowPresentation> BuildActiveAppRows(AuditSnapshot snapshot, int limit = 5) =>
        snapshot.Tune.TopProcesses
            .OrderByDescending(process => process.WorkingSetBytes)
            .ThenBy(process => process.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(process => new ActiveAppRowPresentation
            {
                Name = process.Name,
                ProcessId = $"PID {process.Id}",
                Memory = Formatting.Size(process.WorkingSetBytes),
                Cpu = $"{process.CpuSeconds.ToString("N1", CultureInfo.InvariantCulture)}s",
                Relevance = RelevanceFor(process.Name)
            })
            .ToList();

    public static string FormatReadinessScore(AuditSnapshot snapshot) =>
        snapshot.Readiness.Score.ToString(CultureInfo.InvariantCulture);

    public static string FormatReadinessRating(AuditSnapshot snapshot) =>
        string.IsNullOrWhiteSpace(snapshot.Readiness.Rating) ? "Review" : snapshot.Readiness.Rating;

    private static ModuleCardPresentation ToModuleCard(ModuleCardReadModel card) =>
        new()
        {
            DisplayName = card.DisplayName,
            ModuleKind = card.ModuleKind,
            Availability = card.StatusLabel,
            Risk = string.IsNullOrWhiteSpace(card.RiskLevel) ? "unknown risk" : $"{card.RiskLevel} risk",
            Reason = card.Reason,
            NextAction = card.NextAction,
            CapabilityText = CountLabel(card.CapabilityCount, "capability", "capabilities"),
            ActionText = CountLabel(card.ActionCount, "action"),
            HealthCheckText = CountLabel(card.HealthCheckCount, "health check"),
            StorageRootText = CountLabel(card.StorageRootCount, "storage root"),
            RunnableActionText = string.IsNullOrWhiteSpace(card.ActionRuntimeLabel)
                ? card.HasRunnableActions ? "Runnable" : "Read-only"
                : card.ActionRuntimeLabel
        };

    private static ModuleRailBadgePresentation ToModuleRailBadge(ModuleCardReadModel card) =>
        new()
        {
            DisplayName = card.DisplayName,
            Badge = card.Availability switch
            {
                ModuleAvailability.Available => "ok",
                ModuleAvailability.Blocked => "blocked",
                _ => "check"
            }
        };

    private static PendingTuneApprovalPresentation ToPendingAction(PendingTuneActionReadModel action) =>
        new()
        {
            ProposalId = action.ProposalId,
            Kind = Humanize(action.Kind.ToString()),
            Target = action.Target,
            TargetContext = action.TargetContext,
            Risk = string.IsNullOrWhiteSpace(action.RiskLevel) ? "Unknown risk" : $"{action.RiskLevel} risk",
            AdminState = action.RequiresAdmin ? "Admin approval" : "User approval",
            ProposedAt = FormatTimestamp(action.ProposedAt),
            Summary = action.Summary,
            CorrelationId = action.CorrelationId
        };

    private static LedgerEventPresentation ToLedgerEvent(LedgerEventReadModel evt) =>
        new()
        {
            Type = Humanize(evt.EventType.ToString()),
            Module = string.IsNullOrWhiteSpace(evt.ModuleId) ? evt.AppId : evt.ModuleId,
            CreatedAt = FormatTimestamp(evt.CreatedAt),
            DataState = Humanize(evt.DataState.ToString()).ToLowerInvariant(),
            PrivacyState = Humanize(evt.PrivacyState.ToString()).ToLowerInvariant(),
            Summary = evt.Summary,
            CorrelationId = evt.CorrelationId
        };

    private static TuneActionLifecyclePresentation ToTuneActionLifecycle(TuneActionLifecycleReadModel lifecycle) =>
        new()
        {
            ProposalId = lifecycle.ProposalId,
            Kind = Humanize(lifecycle.Kind.ToString()),
            Target = lifecycle.Target,
            Evidence = lifecycle.Evidence,
            Risk = string.IsNullOrWhiteSpace(lifecycle.RiskLevel) ? "Unknown risk" : $"{lifecycle.RiskLevel} risk",
            Approval = lifecycle.ApprovalState,
            Execution = FormatStateWithDetail(lifecycle.ExecutionStatus, lifecycle.ExecutionMessage),
            Verification = FormatStateWithDetail(lifecycle.VerificationStatus, lifecycle.VerificationStep),
            CorrelationId = lifecycle.CorrelationId
        };

    private static string CountLabel(int count, string noun, string? plural = null) =>
        count == 1 ? $"1 {noun}" : $"{count} {plural ?? $"{noun}s"}";

    private static string FormatStateWithDetail(string state, string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return state;
        }

        return $"{state}: {detail}";
    }

    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp.ToString("MMM d, yyyy h:mm tt", CultureInfo.InvariantCulture);

    private static string RelevanceFor(string processName)
    {
        if (processName.Contains("ollama", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("llama", StringComparison.OrdinalIgnoreCase))
        {
            return "Local AI workload; verify model jobs are intentional.";
        }

        if (processName.Contains("docker", StringComparison.OrdinalIgnoreCase))
        {
            return "Container runtime; confirm it needs to be active.";
        }

        if (processName.Contains("chrome", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("msedge", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("firefox", StringComparison.OrdinalIgnoreCase))
        {
            return "Browser workload; review tab pressure if memory is high.";
        }

        if (processName.Equals("code", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("devenv", StringComparison.OrdinalIgnoreCase)
            || processName.Contains("rider", StringComparison.OrdinalIgnoreCase))
        {
            return "Developer tool; check workspace and extension load.";
        }

        return "Ranked by current memory pressure.";
    }

    private static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var builder = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (i > 0 && char.IsUpper(current) && !char.IsWhiteSpace(value[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(i == 0 ? char.ToUpperInvariant(current) : char.ToLowerInvariant(current));
        }

        return builder.ToString();
    }
}

public sealed class DashboardSummaryPresentation
{
    public string OverallState { get; init; } = "Ready";
    public string ModuleCoverage { get; init; } = "0 available / 0 blocked";
    public string PendingApprovals { get; init; } = "0 pending";
    public string RecentEvents { get; init; } = "0 events";
    public string LastEvent { get; init; } = "No ledger events";
}

public sealed class DashboardRailPresentation
{
    public string DashboardBadge { get; init; } = "live";
    public string InventoryBadge { get; init; } = "0";
    public string TunePlanBadge { get; init; } = "0";
    public string ReportsBadge { get; init; } = "0";
    public List<ModuleRailBadgePresentation> Modules { get; init; } = [];
}

public sealed class ModuleRailBadgePresentation
{
    public string DisplayName { get; init; } = "";
    public string Badge { get; init; } = "";
}

public sealed class ModuleCardPresentation
{
    public string DisplayName { get; init; } = "";
    public string ModuleKind { get; init; } = "";
    public string Availability { get; init; } = "";
    public string Risk { get; init; } = "";
    public string Reason { get; init; } = "";
    public string NextAction { get; init; } = "";
    public string CapabilityText { get; init; } = "";
    public string ActionText { get; init; } = "";
    public string HealthCheckText { get; init; } = "";
    public string StorageRootText { get; init; } = "";
    public string RunnableActionText { get; init; } = "";
}

public sealed class PendingTuneApprovalPresentation
{
    public string ProposalId { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Target { get; init; } = "";
    public string TargetContext { get; init; } = "";
    public string Risk { get; init; } = "";
    public string AdminState { get; init; } = "";
    public string ProposedAt { get; init; } = "";
    public string Summary { get; init; } = "";
    public string CorrelationId { get; init; } = "";
}

public sealed class TuneActionLifecyclePresentation
{
    public string ProposalId { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Target { get; init; } = "";
    public string Evidence { get; init; } = "";
    public string Risk { get; init; } = "";
    public string Approval { get; init; } = "";
    public string Execution { get; init; } = "";
    public string Verification { get; init; } = "";
    public string CorrelationId { get; init; } = "";
}

public sealed class LedgerEventPresentation
{
    public string Type { get; init; } = "";
    public string Module { get; init; } = "";
    public string CreatedAt { get; init; } = "";
    public string DataState { get; init; } = "";
    public string PrivacyState { get; init; } = "";
    public string Summary { get; init; } = "";
    public string CorrelationId { get; init; } = "";
}

public sealed class ActiveAppRowPresentation
{
    public string Name { get; init; } = "";
    public string ProcessId { get; init; } = "";
    public string Memory { get; init; } = "";
    public string Cpu { get; init; } = "";
    public string Relevance { get; init; } = "";
}

public static class DashboardWindowSizing
{
    public static DashboardWindowBounds Calculate(DashboardWorkArea workArea, double scale)
    {
        var safeScale = double.IsFinite(scale) && scale > 0 ? scale : 1d;
        var desiredWidth = (int)Math.Round(1180 * safeScale);
        var desiredHeight = (int)Math.Round(820 * safeScale);
        var margin = (int)Math.Round(40 * safeScale);
        var availableWidth = Math.Max(1, workArea.Width - margin * 2);
        var availableHeight = Math.Max(1, workArea.Height - margin * 2);
        var width = Math.Min(desiredWidth, availableWidth);
        var height = Math.Min(desiredHeight, availableHeight);
        var x = workArea.X + Math.Max(0, (workArea.Width - width) / 2);
        var y = workArea.Y + Math.Max(0, (workArea.Height - height) / 2);

        return new DashboardWindowBounds(x, y, width, height);
    }
}

public sealed record DashboardWorkArea(int X, int Y, int Width, int Height);

public sealed record DashboardWindowBounds(int X, int Y, int Width, int Height);
