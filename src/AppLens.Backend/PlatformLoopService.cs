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
    private readonly IModuleActionRuntime _moduleActionRuntime;

    public PlatformLoopService(
        ModuleStatusService moduleStatusService,
        IBlackboardStore blackboardStore,
        TuneActionExecutor tuneActionExecutor,
        IModuleActionRuntime? moduleActionRuntime = null)
    {
        _moduleStatusService = moduleStatusService;
        _blackboardStore = blackboardStore;
        _tuneActionExecutor = tuneActionExecutor;
        _moduleActionRuntime = moduleActionRuntime ?? new BlockingModuleActionRuntime();
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

        record = WithLoopMetadata(record, proposal, approval, effectiveCorrelationId);

        await _blackboardStore.AppendAsync(
                BlackboardEvent.ForTuneActionExecuted(record, proposal, approval, effectiveCorrelationId),
                cancellationToken)
            .ConfigureAwait(false);

        return record;
    }

    public async Task<List<TuneActionRecord>> ExecuteApprovedTuneActionsAsync(
        IEnumerable<TunePlanItem> items,
        string approvedBy,
        string rationale,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveCorrelationId = NormalizeCorrelationId(correlationId, "corr-tune-batch");
        var records = new List<TuneActionRecord>();

        foreach (var item in items)
        {
            var proposal = await ProposeTuneActionAsync(item, effectiveCorrelationId, cancellationToken)
                .ConfigureAwait(false);
            var approval = await ApproveTuneActionAsync(
                    proposal,
                    approvedBy,
                    approved: true,
                    rationale,
                    effectiveCorrelationId,
                    cancellationToken)
                .ConfigureAwait(false);
            records.Add(await ExecuteTuneActionAsync(
                    item,
                    proposal,
                    approval,
                    effectiveCorrelationId,
                    cancellationToken)
                .ConfigureAwait(false));
        }

        return records;
    }

    public async Task<List<BlackboardEvent>> RecordTuneActionVerificationAsync(
        IEnumerable<TuneActionRecord> actions,
        AuditSnapshot snapshot,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var events = new List<BlackboardEvent>();
        foreach (var action in actions)
        {
            var effectiveCorrelationId = string.IsNullOrWhiteSpace(correlationId)
                ? string.IsNullOrWhiteSpace(action.CorrelationId)
                    ? NormalizeCorrelationId(null, "corr-tune-verify")
                    : action.CorrelationId
                : correlationId;
            var evt = BlackboardEvent.ForTuneActionVerification(action, snapshot, effectiveCorrelationId);
            await _blackboardStore.AppendAsync(evt, cancellationToken).ConfigureAwait(false);
            events.Add(evt);
        }

        return events;
    }

    public async Task<ModuleActionProposal> ProposeModuleActionAsync(
        ModuleActionRequest request,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var proposal = new ModuleActionProposal
        {
            ModuleId = request.ModuleId,
            AppId = request.AppId,
            ActionName = request.ActionName,
            ExecutorKey = request.ExecutorKey,
            Target = SanitizeTarget(request.Target),
            TargetContext = request.TargetContext,
            CommandSummary = request.CommandSummary,
            RequiresApproval = request.RequiresApproval,
            SystemChanging = request.SystemChanging,
            RiskLevel = request.RiskLevel,
            CorrelationId = NormalizeCorrelationId(correlationId, "corr-module-action")
        };

        await _blackboardStore.AppendAsync(
                BlackboardEvent.ForModuleActionProposed(proposal),
                cancellationToken)
            .ConfigureAwait(false);

        return proposal;
    }

    public async Task<ModuleActionApproval> ApproveModuleActionAsync(
        ModuleActionProposal proposal,
        string approvedBy,
        bool approved,
        string rationale,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var approval = new ModuleActionApproval
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
                BlackboardEvent.ForModuleActionApproved(approval, eventProposal),
                cancellationToken)
            .ConfigureAwait(false);

        return approval;
    }

    public async Task<ModuleActionRecord> ExecuteModuleActionAsync(
        ModuleActionProposal proposal,
        ModuleActionApproval approval,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveCorrelationId = string.IsNullOrWhiteSpace(correlationId)
            ? proposal.CorrelationId
            : correlationId;
        ModuleActionRecord record;

        if (!string.Equals(approval.ProposalId, proposal.ProposalId, StringComparison.OrdinalIgnoreCase))
        {
            record = BlockedModuleRecord(proposal, "Action blocked because the approval does not match the proposal.");
        }
        else if (proposal.RequiresApproval && !approval.Approved)
        {
            record = BlockedModuleRecord(proposal, "Action blocked because approval was rejected.");
        }
        else if (!_moduleActionRuntime.CanExecute(proposal))
        {
            record = BlockedModuleRecord(proposal, "Module action executor is not registered.");
        }
        else
        {
            record = await _moduleActionRuntime.ExecuteAsync(proposal, approval, cancellationToken)
                .ConfigureAwait(false);
        }

        record = WithModuleLoopMetadata(record, proposal, approval, effectiveCorrelationId);

        await _blackboardStore.AppendAsync(
                BlackboardEvent.ForModuleActionExecuted(record, proposal, approval, effectiveCorrelationId),
                cancellationToken)
            .ConfigureAwait(false);

        return record;
    }

    public async Task<BlackboardEvent> RecordModuleCapabilityObservedAsync(
        string moduleId,
        string appId,
        string capability,
        string summary,
        Dictionary<string, string> payload,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var evt = BlackboardEvent.ForCapabilityObserved(
            moduleId,
            appId,
            capability,
            summary,
            payload,
            NormalizeCorrelationId(correlationId, "corr-module-observed"));

        await _blackboardStore.AppendAsync(evt, cancellationToken).ConfigureAwait(false);
        return evt;
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

    private static TuneActionRecord WithLoopMetadata(
        TuneActionRecord record,
        TuneActionProposal proposal,
        TuneActionApproval approval,
        string correlationId) =>
        new()
        {
            Id = record.Id,
            ProposalId = proposal.ProposalId,
            ApprovalId = approval.ApprovalId,
            GrantId = approval.GrantId,
            CorrelationId = correlationId,
            PlanItemId = record.PlanItemId,
            Kind = record.Kind,
            Status = record.Status,
            Target = record.Target,
            Message = record.Message,
            BackupDetail = record.BackupDetail,
            VerificationStep = record.VerificationStep,
            StartedAt = record.StartedAt,
            CompletedAt = record.CompletedAt,
            RequiresAdmin = record.RequiresAdmin
        };

    private static ModuleActionRecord BlockedModuleRecord(ModuleActionProposal proposal, string message)
    {
        var now = DateTimeOffset.Now;
        return new ModuleActionRecord
        {
            ModuleId = proposal.ModuleId,
            AppId = proposal.AppId,
            ActionName = proposal.ActionName,
            Target = proposal.Target,
            CommandSummary = proposal.CommandSummary,
            Status = TuneActionStatus.Blocked,
            Message = message,
            StartedAt = now,
            CompletedAt = now
        };
    }

    private static ModuleActionRecord WithModuleLoopMetadata(
        ModuleActionRecord record,
        ModuleActionProposal proposal,
        ModuleActionApproval approval,
        string correlationId) =>
        new()
        {
            Id = record.Id,
            ProposalId = proposal.ProposalId,
            ApprovalId = approval.ApprovalId,
            GrantId = approval.GrantId,
            CorrelationId = correlationId,
            ModuleId = proposal.ModuleId,
            AppId = proposal.AppId,
            ActionName = proposal.ActionName,
            Target = proposal.Target,
            CommandSummary = proposal.CommandSummary,
            Status = record.Status,
            Message = record.Message,
            StartedAt = record.StartedAt,
            CompletedAt = record.CompletedAt
        };

    private static string SanitizeTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return "";
        }

        return target.Contains('@', StringComparison.Ordinal) || target.Contains(':', StringComparison.Ordinal)
            ? "configured-target"
            : target.Trim();
    }

    private static string NormalizeCorrelationId(string? correlationId, string prefix) =>
        string.IsNullOrWhiteSpace(correlationId) ? $"{prefix}-{Guid.NewGuid():N}" : correlationId;
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

    public static ModuleActionProposal WithCorrelation(this ModuleActionProposal proposal, string correlationId) =>
        new()
        {
            ProposalId = proposal.ProposalId,
            ModuleId = proposal.ModuleId,
            AppId = proposal.AppId,
            ActionName = proposal.ActionName,
            ExecutorKey = proposal.ExecutorKey,
            Target = proposal.Target,
            TargetContext = proposal.TargetContext,
            CommandSummary = proposal.CommandSummary,
            RequiresApproval = proposal.RequiresApproval,
            SystemChanging = proposal.SystemChanging,
            RiskLevel = proposal.RiskLevel,
            CorrelationId = correlationId,
            ProposedAt = proposal.ProposedAt
        };
}
