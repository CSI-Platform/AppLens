namespace AppLens.Backend;

public sealed class AppLensDashboardState
{
    public DashboardSummaryReadModel Summary { get; init; } = new();
    public List<ModuleCardReadModel> ModuleCards { get; init; } = [];
    public List<PendingTuneActionReadModel> PendingActions { get; init; } = [];
    public List<TuneActionLifecycleReadModel> TuneActionLifecycles { get; init; } = [];
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
    public int RunnableActionCount { get; init; }
    public int BlockedActionCount { get; init; }
    public int NotImplementedActionCount { get; init; }
    public string ActionRuntimeLabel { get; init; } = "";
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

public sealed class TuneActionLifecycleReadModel
{
    public string ProposalId { get; init; } = "";
    public string PlanItemId { get; init; } = "";
    public string ActionId { get; init; } = "";
    public ProposedActionKind Kind { get; init; } = ProposedActionKind.None;
    public string Target { get; init; } = "";
    public string Evidence { get; init; } = "";
    public string RiskLevel { get; init; } = "";
    public string ApprovalState { get; init; } = "Pending approval";
    public string ExecutionStatus { get; init; } = "Not executed";
    public string ExecutionMessage { get; init; } = "";
    public string VerificationStatus { get; init; } = "Not recorded";
    public string VerificationStep { get; init; } = "";
    public string CorrelationId { get; init; } = "";
}

public sealed class DashboardReadModelService
{
    private readonly ModuleStatusService _moduleStatusService;
    private readonly IBlackboardStore _blackboardStore;
    private readonly ModuleActionExecutorRegistry _moduleActionExecutors;

    public DashboardReadModelService(
        ModuleStatusService moduleStatusService,
        IBlackboardStore blackboardStore,
        ModuleActionExecutorRegistry? moduleActionExecutors = null)
    {
        _moduleStatusService = moduleStatusService;
        _blackboardStore = blackboardStore;
        _moduleActionExecutors = moduleActionExecutors ?? new ModuleActionExecutorRegistry();
    }

    public async Task<AppLensDashboardState> GetDashboardStateAsync(
        int recentEventLimit = 20,
        CancellationToken cancellationToken = default)
    {
        var moduleCards = GetModuleCards();
        var pendingActions = await GetPendingActionsAsync(cancellationToken).ConfigureAwait(false);
        var tuneActionLifecycles = await GetTuneActionLifecyclesAsync(cancellationToken).ConfigureAwait(false);
        var recentEvents = await GetRecentLedgerEventsAsync(recentEventLimit, cancellationToken).ConfigureAwait(false);

        return new AppLensDashboardState
        {
            Summary = BuildSummary(moduleCards, pendingActions, recentEvents),
            ModuleCards = moduleCards,
            PendingActions = pendingActions,
            TuneActionLifecycles = tuneActionLifecycles,
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

                var actionStates = manifest.Actions
                    .Select(action => _moduleActionExecutors.StateFor(action, status.Availability))
                    .ToList();
                var runnableActionCount = actionStates.Count(state => state == ModuleActionExecutorState.Runnable);
                var blockedActionCount = actionStates.Count(state => state == ModuleActionExecutorState.Blocked);
                var notImplementedActionCount = actionStates.Count(state => state == ModuleActionExecutorState.NotImplemented);

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
                    NextAction = NextActionFor(status, notImplementedActionCount, runnableActionCount),
                    CapabilityCount = manifest.Capabilities.Count,
                    ActionCount = manifest.Actions.Count,
                    HealthCheckCount = manifest.HealthChecks.Count,
                    StorageRootCount = manifest.StorageRoots.Count,
                    HasRunnableActions = runnableActionCount > 0,
                    RunnableActionCount = runnableActionCount,
                    BlockedActionCount = blockedActionCount,
                    NotImplementedActionCount = notImplementedActionCount,
                    ActionRuntimeLabel = ActionRuntimeLabelFor(
                        status.Availability,
                        manifest.Actions.Count,
                        runnableActionCount,
                        blockedActionCount,
                        notImplementedActionCount)
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

    public async Task<List<TuneActionLifecycleReadModel>> GetTuneActionLifecyclesAsync(
        CancellationToken cancellationToken = default)
    {
        var events = await _blackboardStore.ReadAllAsync(cancellationToken).ConfigureAwait(false);
        var approvals = events
            .Where(evt => evt.EventType == BlackboardEventType.ActionApproved)
            .Where(evt => !string.IsNullOrWhiteSpace(Payload(evt, "proposal_id")))
            .GroupBy(evt => Payload(evt, "proposal_id"), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(evt => evt.CreatedAt).First(), StringComparer.OrdinalIgnoreCase);
        var executions = events
            .Where(evt => evt.EventType == BlackboardEventType.ActionExecuted)
            .Where(evt => !string.IsNullOrWhiteSpace(Payload(evt, "proposal_id")))
            .GroupBy(evt => Payload(evt, "proposal_id"), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(evt => evt.CreatedAt).First(), StringComparer.OrdinalIgnoreCase);
        var verificationsByAction = events
            .Where(evt => evt.EventType == BlackboardEventType.VerificationRecorded)
            .Where(evt => !string.IsNullOrWhiteSpace(Payload(evt, "action_id")))
            .GroupBy(evt => Payload(evt, "action_id"), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(evt => evt.CreatedAt).First(), StringComparer.OrdinalIgnoreCase);

        return events
            .Where(evt => evt.EventType == BlackboardEventType.ActionProposed)
            .Where(evt => !string.IsNullOrWhiteSpace(Payload(evt, "proposal_id")))
            .OrderByDescending(evt => evt.CreatedAt)
            .Select(proposal =>
            {
                var proposalId = Payload(proposal, "proposal_id");
                approvals.TryGetValue(proposalId, out var approval);
                executions.TryGetValue(proposalId, out var execution);
                var actionId = execution is null ? "" : Payload(execution, "action_id");
                var verification = !string.IsNullOrWhiteSpace(actionId) && verificationsByAction.TryGetValue(actionId, out var value)
                    ? value
                    : null;

                return ToTuneActionLifecycle(proposal, approval, execution, verification);
            })
            .ToList();
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

    private static TuneActionLifecycleReadModel ToTuneActionLifecycle(
        BlackboardEvent proposal,
        BlackboardEvent? approval,
        BlackboardEvent? execution,
        BlackboardEvent? verification)
    {
        var actionId = execution is null ? "" : Payload(execution, "action_id");
        return new TuneActionLifecycleReadModel
        {
            ProposalId = Payload(proposal, "proposal_id"),
            PlanItemId = Payload(proposal, "plan_item_id"),
            ActionId = actionId,
            Kind = Enum.TryParse<ProposedActionKind>(Payload(proposal, "kind"), ignoreCase: true, out var kind)
                ? kind
                : ProposedActionKind.None,
            Target = Payload(proposal, "target"),
            Evidence = Payload(proposal, "evidence"),
            RiskLevel = Payload(proposal, "risk"),
            ApprovalState = ApprovalState(approval),
            ExecutionStatus = execution is null ? "Not executed" : Payload(execution, "status"),
            ExecutionMessage = execution is null ? "" : Payload(execution, "message"),
            VerificationStatus = verification is null ? "Not recorded" : Payload(verification, "verification_status"),
            VerificationStep = !string.IsNullOrWhiteSpace(Payload(proposal, "verification_step"))
                ? Payload(proposal, "verification_step")
                : verification is null ? "" : Payload(verification, "verification_step"),
            CorrelationId = proposal.CorrelationId
        };
    }

    private static string ApprovalState(BlackboardEvent? approval)
    {
        if (approval is null)
        {
            return "Pending approval";
        }

        var approvedBy = Payload(approval, "approved_by");
        var actor = string.IsNullOrWhiteSpace(approvedBy) ? "operator" : approvedBy;
        return bool.TryParse(Payload(approval, "approved"), out var approved) && approved
            ? $"Approved by {actor}"
            : $"Rejected by {actor}";
    }

    private static string Payload(BlackboardEvent evt, string key) =>
        evt.Payload.TryGetValue(key, out var value) ? value : "";

    private static string NextActionFor(ModuleStatus status, int notImplementedActionCount, int runnableActionCount)
    {
        if (status.Availability == ModuleAvailability.Available
            && runnableActionCount == 0
            && notImplementedActionCount > 0)
        {
            return "Module detected; action executor is not implemented yet.";
        }

        return status.NextAction;
    }

    private static string ActionRuntimeLabelFor(
        ModuleAvailability availability,
        int actionCount,
        int runnableActionCount,
        int blockedActionCount,
        int notImplementedActionCount)
    {
        if (actionCount == 0)
        {
            return "No actions";
        }

        if (availability == ModuleAvailability.NotConfigured)
        {
            return "Not configured";
        }

        if (availability is ModuleAvailability.Blocked or ModuleAvailability.Unavailable || blockedActionCount > 0)
        {
            return "Blocked";
        }

        if (runnableActionCount > 0)
        {
            return "Runnable";
        }

        if (notImplementedActionCount > 0)
        {
            return "No runner";
        }

        return "Read-only";
    }
}

public sealed class ModuleActionExecutorRegistry
{
    private readonly HashSet<string> _registeredExecutorKeys;

    public ModuleActionExecutorRegistry(IEnumerable<string>? registeredExecutorKeys = null)
    {
        _registeredExecutorKeys = (registeredExecutorKeys ?? [])
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public ModuleActionExecutorState StateFor(ModuleActionContract action, ModuleAvailability moduleAvailability)
    {
        if (!RequiresExecutor(action))
        {
            return ModuleActionExecutorState.NotRequired;
        }

        if (moduleAvailability != ModuleAvailability.Available)
        {
            return ModuleActionExecutorState.Blocked;
        }

        if (!string.IsNullOrWhiteSpace(action.ExecutorKey)
            && _registeredExecutorKeys.Contains(action.ExecutorKey))
        {
            return ModuleActionExecutorState.Runnable;
        }

        return ModuleActionExecutorState.NotImplemented;
    }

    private static bool RequiresExecutor(ModuleActionContract action) =>
        !action.Permission.Equals("read", StringComparison.OrdinalIgnoreCase)
        || !string.IsNullOrWhiteSpace(action.ExecutorKey);
}
