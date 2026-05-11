namespace AppLens.Backend;

public sealed class TuneActionProposal
{
    public string ProposalId { get; init; } = $"proposal-{Guid.NewGuid():N}";
    public string PlanItemId { get; init; } = "";
    public ProposedActionKind Kind { get; init; } = ProposedActionKind.None;
    public string Target { get; init; } = "";
    public string TargetContext { get; init; } = "";
    public string CorrelationId { get; init; } = "";
    public DateTimeOffset ProposedAt { get; init; } = DateTimeOffset.Now;
}

public sealed class TuneActionApproval
{
    public string ApprovalId { get; init; } = $"approval-{Guid.NewGuid():N}";
    public string GrantId { get; init; } = $"grant-{Guid.NewGuid():N}";
    public string ProposalId { get; init; } = "";
    public bool Approved { get; init; }
    public string ApprovedBy { get; init; } = "";
    public string Rationale { get; init; } = "";
    public DateTimeOffset DecidedAt { get; init; } = DateTimeOffset.Now;
}

public sealed class PlatformLoopService
{
    private readonly ModuleStatusService _moduleStatusService;
    private readonly IBlackboardStore _blackboardStore;
    private readonly TuneActionExecutor _tuneActionExecutor;

    public PlatformLoopService(
        ModuleStatusService moduleStatusService,
        IBlackboardStore blackboardStore,
        TuneActionExecutor tuneActionExecutor)
    {
        _moduleStatusService = moduleStatusService;
        _blackboardStore = blackboardStore;
        _tuneActionExecutor = tuneActionExecutor;
    }

    public async Task<List<ModuleStatus>> DetectModulesAsync(
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveCorrelationId = NormalizeCorrelationId(correlationId, "corr-detect");
        var manifests = _moduleStatusService.GetManifests().ToDictionary(
            manifest => manifest.ModuleId,
            StringComparer.OrdinalIgnoreCase);
        var statuses = _moduleStatusService.GetStatuses();

        foreach (var status in statuses)
        {
            if (!manifests.TryGetValue(status.ModuleId, out var manifest))
            {
                continue;
            }

            await _blackboardStore.AppendAsync(
                    BlackboardEvent.ForModuleDetected(status, manifest, effectiveCorrelationId),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return statuses;
    }

    public async Task<TuneActionProposal> ProposeTuneActionAsync(
        TunePlanItem item,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var proposal = new TuneActionProposal
        {
            PlanItemId = item.Id,
            Kind = item.ProposedAction.Kind,
            Target = item.ProposedAction.Target,
            TargetContext = item.ProposedAction.TargetContext,
            CorrelationId = NormalizeCorrelationId(correlationId, "corr-tune-proposal")
        };

        await _blackboardStore.AppendAsync(
                BlackboardEvent.ForTuneActionProposed(proposal, item),
                cancellationToken)
            .ConfigureAwait(false);

        return proposal;
    }

    public async Task<TuneActionApproval> ApproveTuneActionAsync(
        TuneActionProposal proposal,
        string approvedBy,
        bool approved,
        string rationale,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var approval = new TuneActionApproval
        {
            ProposalId = proposal.ProposalId,
            Approved = approved,
            ApprovedBy = approvedBy,
            Rationale = rationale
        };

        var eventProposal = string.IsNullOrWhiteSpace(correlationId)
            ? proposal
            : proposal.WithCorrelation(correlationId);

        await _blackboardStore.AppendAsync(
                BlackboardEvent.ForTuneActionApproved(approval, eventProposal),
                cancellationToken)
            .ConfigureAwait(false);

        return approval;
    }

    public async Task<TuneActionApproval> DecidePendingTuneActionAsync(
        string proposalId,
        string approvedBy,
        bool approved,
        string rationale,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(proposalId))
        {
            throw new InvalidOperationException("Tune action proposal id is missing.");
        }

        var events = await _blackboardStore.ReadAllAsync(cancellationToken).ConfigureAwait(false);
        var isAlreadyDecided = events.Any(evt =>
            evt.EventType is BlackboardEventType.ActionApproved or BlackboardEventType.ActionExecuted &&
            string.Equals(Payload(evt, "proposal_id"), proposalId, StringComparison.OrdinalIgnoreCase));

        if (isAlreadyDecided)
        {
            throw new InvalidOperationException($"Tune action proposal {proposalId} was already decided.");
        }

        var proposedEvent = events
            .Where(evt => evt.EventType == BlackboardEventType.ActionProposed)
            .Where(evt => string.Equals(Payload(evt, "proposal_id"), proposalId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(evt => evt.CreatedAt)
            .FirstOrDefault();

        if (proposedEvent is null)
        {
            throw new InvalidOperationException($"Tune action proposal {proposalId} was not found.");
        }

        return await ApproveTuneActionAsync(
                ToProposal(proposedEvent),
                approvedBy,
                approved,
                rationale,
                correlationId ?? proposedEvent.CorrelationId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<TuneActionRecord> ExecuteTuneActionAsync(
        TunePlanItem item,
        TuneActionProposal proposal,
        TuneActionApproval approval,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? proposal.CorrelationId
            : correlationId;
        TuneActionRecord record;

        if (!string.Equals(proposal.PlanItemId, item.Id, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(approval.ProposalId, proposal.ProposalId, StringComparison.OrdinalIgnoreCase))
        {
            record = BlockedRecord(item, "Action blocked because the approval does not match the proposal.");
        }
        else if (!approval.Approved)
        {
            record = BlockedRecord(item, "Action blocked because approval was rejected.");
        }
        else
        {
            record = await _tuneActionExecutor.ExecuteAsync(item, userApproved: true, cancellationToken)
                .ConfigureAwait(false);
        }

        await _blackboardStore.AppendAsync(
                BlackboardEvent.ForTuneActionExecuted(record, proposal, approval, effectiveCorrelationId),
                cancellationToken)
            .ConfigureAwait(false);

        return record;
    }

    private static TuneActionRecord BlockedRecord(TunePlanItem item, string message)
    {
        var now = DateTimeOffset.Now;
        return new TuneActionRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            PlanItemId = item.Id,
            Kind = item.ProposedAction.Kind,
            Status = TuneActionStatus.Blocked,
            Target = item.ProposedAction.Target,
            Message = message,
            BackupDetail = item.BackupPlan,
            VerificationStep = item.VerificationStep,
            StartedAt = now,
            CompletedAt = now,
            RequiresAdmin = item.RequiresAdmin || item.ProposedAction.ExecutionState == TunePlanExecutionState.RequiresAdmin
        };
    }

    private static string NormalizeCorrelationId(string? correlationId, string prefix) =>
        string.IsNullOrWhiteSpace(correlationId) ? $"{prefix}-{Guid.NewGuid():N}" : correlationId;

    private static TuneActionProposal ToProposal(BlackboardEvent evt) =>
        new()
        {
            ProposalId = Payload(evt, "proposal_id"),
            PlanItemId = Payload(evt, "plan_item_id"),
            Kind = Enum.TryParse<ProposedActionKind>(Payload(evt, "kind"), ignoreCase: true, out var kind)
                ? kind
                : ProposedActionKind.None,
            Target = Payload(evt, "target"),
            TargetContext = Payload(evt, "target_context"),
            CorrelationId = evt.CorrelationId,
            ProposedAt = evt.CreatedAt
        };

    private static string Payload(BlackboardEvent evt, string key) =>
        evt.Payload.TryGetValue(key, out var value) ? value : "";
}

file static class TuneActionProposalExtensions
{
    public static TuneActionProposal WithCorrelation(this TuneActionProposal proposal, string correlationId) =>
        new()
        {
            ProposalId = proposal.ProposalId,
            PlanItemId = proposal.PlanItemId,
            Kind = proposal.Kind,
            Target = proposal.Target,
            TargetContext = proposal.TargetContext,
            CorrelationId = correlationId,
            ProposedAt = proposal.ProposedAt
        };
}
