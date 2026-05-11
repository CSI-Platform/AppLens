namespace AppLens.Backend;

public sealed class AppLensDashboardState
{
    public DashboardSummaryReadModel Summary { get; init; } = new();
    public List<ModuleCardReadModel> ModuleCards { get; init; } = [];
    public List<PendingTuneActionReadModel> PendingActions { get; init; } = [];
    public List<LedgerEventReadModel> RecentLedgerEvents { get; init; } = [];
}

public sealed class DashboardSummaryReadModel
{
    public int ModuleCount { get; init; }
    public int AvailableModuleCount { get; init; }
    public int BlockedModuleCount { get; init; }
    public int PendingActionCount { get; init; }
    public int RecentEventCount { get; init; }
    public DateTimeOffset? LastLedgerEventAt { get; init; }
    public string OverallState { get; init; } = "Ready";
}

public sealed class ModuleCardReadModel
{
    public string ModuleId { get; init; } = "";
    public string AppId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string ModuleKind { get; init; } = "";
    public ModuleAvailability Availability { get; init; } = ModuleAvailability.Unavailable;
    public string StatusLabel { get; init; } = "";
    public string RiskLevel { get; init; } = "";
    public string Reason { get; init; } = "";
    public string NextAction { get; init; } = "";
    public int CapabilityCount { get; init; }
    public int ActionCount { get; init; }
    public int HealthCheckCount { get; init; }
    public int StorageRootCount { get; init; }
    public bool HasRunnableActions { get; init; }
    public List<string> Capabilities { get; init; } = [];
    public List<string> Entrypoints { get; init; } = [];
    public List<string> DataContracts { get; init; } = [];
    public List<string> ActionContracts { get; init; } = [];
    public List<string> Privacy { get; init; } = [];
    public List<ModuleStorageRoot> StorageRoots { get; init; } = [];
    public List<ModuleHealthCheck> HealthChecks { get; init; } = [];
    public List<ModuleActionContract> Actions { get; init; } = [];
}

public sealed class PendingTuneActionReadModel
{
    public string ProposalId { get; init; } = "";
    public string PlanItemId { get; init; } = "";
    public ProposedActionKind Kind { get; init; } = ProposedActionKind.None;
    public string Target { get; init; } = "";
    public string TargetContext { get; init; } = "";
    public string RiskLevel { get; init; } = "";
    public bool RequiresAdmin { get; init; }
    public DateTimeOffset ProposedAt { get; init; }
    public string Summary { get; init; } = "";
    public string CorrelationId { get; init; } = "";
}

public sealed class LedgerEventReadModel
{
    public string EventId { get; init; } = "";
    public BlackboardEventType EventType { get; init; }
    public string ModuleId { get; init; } = "";
    public string AppId { get; init; } = "";
    public string CorrelationId { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public BlackboardDataState DataState { get; init; }
    public BlackboardPrivacyState PrivacyState { get; init; }
    public string Summary { get; init; } = "";
}

public sealed class DashboardReadModelService
{
    private readonly ModuleStatusService _moduleStatusService;
    private readonly IBlackboardStore _blackboardStore;

    public DashboardReadModelService(ModuleStatusService moduleStatusService, IBlackboardStore blackboardStore)
    {
        _moduleStatusService = moduleStatusService;
        _blackboardStore = blackboardStore;
    }

    public async Task<AppLensDashboardState> GetDashboardStateAsync(
        int recentEventLimit = 20,
        CancellationToken cancellationToken = default)
    {
        var moduleCards = GetModuleCards();
        var pendingActions = await GetPendingActionsAsync(cancellationToken).ConfigureAwait(false);
        var recentEvents = await GetRecentLedgerEventsAsync(recentEventLimit, cancellationToken).ConfigureAwait(false);

        return new AppLensDashboardState
        {
            Summary = BuildSummary(moduleCards, pendingActions, recentEvents),
            ModuleCards = moduleCards,
            PendingActions = pendingActions,
            RecentLedgerEvents = recentEvents
        };
    }

    public List<ModuleCardReadModel> GetModuleCards()
    {
        var statuses = _moduleStatusService.GetStatuses().ToDictionary(
            status => status.ModuleId,
            StringComparer.OrdinalIgnoreCase);

        return _moduleStatusService.GetManifests()
            .Select(manifest =>
            {
                var status = statuses.TryGetValue(manifest.ModuleId, out var value)
                    ? value
                    : new ModuleStatus
                    {
                        ModuleId = manifest.ModuleId,
                        AppId = manifest.AppId,
                        DisplayName = manifest.DisplayName,
                        Availability = ModuleAvailability.Unavailable,
                        Reason = "Module status is unavailable.",
                        NextAction = "Re-run module detection."
                    };

                return new ModuleCardReadModel
                {
                    ModuleId = manifest.ModuleId,
                    AppId = manifest.AppId,
                    DisplayName = manifest.DisplayName,
                    ModuleKind = manifest.ModuleKind,
                    Availability = status.Availability,
                    StatusLabel = status.Availability.ToString(),
                    RiskLevel = manifest.RiskLevel,
                    Reason = status.Reason,
                    NextAction = status.NextAction,
                    CapabilityCount = manifest.Capabilities.Count,
                    ActionCount = manifest.Actions.Count,
                    HealthCheckCount = manifest.HealthChecks.Count,
                    StorageRootCount = manifest.StorageRoots.Count,
                    HasRunnableActions = manifest.Actions.Any(action =>
                        action.Permission.Contains("execute", StringComparison.OrdinalIgnoreCase)),
                    Capabilities = manifest.Capabilities,
                    Entrypoints = manifest.Entrypoints,
                    DataContracts = manifest.DataContracts,
                    ActionContracts = manifest.ActionContracts,
                    Privacy = manifest.Privacy,
                    StorageRoots = manifest.StorageRoots,
                    HealthChecks = manifest.HealthChecks,
                    Actions = manifest.Actions
                };
            })
            .ToList();
    }

    public async Task<List<PendingTuneActionReadModel>> GetPendingActionsAsync(
        CancellationToken cancellationToken = default)
    {
        var events = await _blackboardStore.ReadAllAsync(cancellationToken).ConfigureAwait(false);
        var closedProposalIds = events
            .Where(evt => evt.EventType is BlackboardEventType.ActionApproved or BlackboardEventType.ActionExecuted)
            .Select(evt => Payload(evt, "proposal_id"))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return events
            .Where(evt => evt.EventType == BlackboardEventType.ActionProposed)
            .Where(evt => !closedProposalIds.Contains(Payload(evt, "proposal_id")))
            .OrderByDescending(evt => evt.CreatedAt)
            .Select(ToPendingAction)
            .ToList();
    }

    public async Task<List<LedgerEventReadModel>> GetRecentLedgerEventsAsync(
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var events = await _blackboardStore.QueryAsync(
                new BlackboardEventQuery { Limit = limit },
                cancellationToken)
            .ConfigureAwait(false);

        return events.Select(ToLedgerEvent).ToList();
    }

    private static DashboardSummaryReadModel BuildSummary(
        List<ModuleCardReadModel> moduleCards,
        List<PendingTuneActionReadModel> pendingActions,
        List<LedgerEventReadModel> recentEvents)
    {
        var blockedCount = moduleCards.Count(card => card.Availability == ModuleAvailability.Blocked);
        return new DashboardSummaryReadModel
        {
            ModuleCount = moduleCards.Count,
            AvailableModuleCount = moduleCards.Count(card => card.Availability == ModuleAvailability.Available),
            BlockedModuleCount = blockedCount,
            PendingActionCount = pendingActions.Count,
            RecentEventCount = recentEvents.Count,
            LastLedgerEventAt = recentEvents.FirstOrDefault()?.CreatedAt,
            OverallState = pendingActions.Count > 0 || blockedCount > 0 ? "Action Required" : "Ready"
        };
    }

    private static PendingTuneActionReadModel ToPendingAction(BlackboardEvent evt) =>
        new()
        {
            ProposalId = Payload(evt, "proposal_id"),
            PlanItemId = Payload(evt, "plan_item_id"),
            Kind = Enum.TryParse<ProposedActionKind>(Payload(evt, "kind"), ignoreCase: true, out var kind)
                ? kind
                : ProposedActionKind.None,
            Target = Payload(evt, "target"),
            TargetContext = Payload(evt, "target_context"),
            RiskLevel = Payload(evt, "risk"),
            RequiresAdmin = bool.TryParse(Payload(evt, "requires_admin"), out var requiresAdmin) && requiresAdmin,
            ProposedAt = evt.CreatedAt,
            Summary = evt.Summary,
            CorrelationId = evt.CorrelationId
        };

    private static LedgerEventReadModel ToLedgerEvent(BlackboardEvent evt) =>
        new()
        {
            EventId = evt.EventId,
            EventType = evt.EventType,
            ModuleId = evt.ModuleId,
            AppId = evt.AppId,
            CorrelationId = evt.CorrelationId,
            CreatedAt = evt.CreatedAt,
            DataState = evt.DataState,
            PrivacyState = evt.PrivacyState,
            Summary = evt.Summary
        };

    private static string Payload(BlackboardEvent evt, string key) =>
        evt.Payload.TryGetValue(key, out var value) ? value : "";
}
