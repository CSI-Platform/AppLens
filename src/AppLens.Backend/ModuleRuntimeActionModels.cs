namespace AppLens.Backend;

public sealed class ModuleActionRequest
{
    public string ModuleId { get; init; } = "";
    public string AppId { get; init; } = "";
    public string ActionName { get; init; } = "";
    public string ExecutorKey { get; init; } = "";
    public string Target { get; init; } = "";
    public string TargetContext { get; init; } = "";
    public string CommandSummary { get; init; } = "";
    public bool RequiresApproval { get; init; } = true;
    public bool SystemChanging { get; init; }
    public string RiskLevel { get; init; } = "low";
}

public sealed class ModuleActionProposal
{
    public string ProposalId { get; init; } = $"proposal-{Guid.NewGuid():N}";
    public string ModuleId { get; init; } = "";
    public string AppId { get; init; } = "";
    public string ActionName { get; init; } = "";
    public string ExecutorKey { get; init; } = "";
    public string Target { get; init; } = "";
    public string TargetContext { get; init; } = "";
    public string CommandSummary { get; init; } = "";
    public bool RequiresApproval { get; init; } = true;
    public bool SystemChanging { get; init; }
    public string RiskLevel { get; init; } = "low";
    public string CorrelationId { get; init; } = "";
    public DateTimeOffset ProposedAt { get; init; } = DateTimeOffset.Now;
}

public sealed class ModuleActionApproval
{
    public string ApprovalId { get; init; } = $"approval-{Guid.NewGuid():N}";
    public string GrantId { get; init; } = $"grant-{Guid.NewGuid():N}";
    public string ProposalId { get; init; } = "";
    public bool Approved { get; init; }
    public string ApprovedBy { get; init; } = "";
    public string Rationale { get; init; } = "";
    public DateTimeOffset DecidedAt { get; init; } = DateTimeOffset.Now;
}

public sealed class ModuleActionRecord
{
    public string Id { get; init; } = $"act-{Guid.NewGuid():N}";
    public string ProposalId { get; init; } = "";
    public string ApprovalId { get; init; } = "";
    public string GrantId { get; init; } = "";
    public string CorrelationId { get; init; } = "";
    public string ModuleId { get; init; } = "";
    public string AppId { get; init; } = "";
    public string ActionName { get; init; } = "";
    public string Target { get; init; } = "";
    public string CommandSummary { get; init; } = "";
    public TuneActionStatus Status { get; init; } = TuneActionStatus.Blocked;
    public string Message { get; init; } = "";
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset CompletedAt { get; init; } = DateTimeOffset.Now;
}

public interface IModuleActionRuntime
{
    bool CanExecute(ModuleActionProposal proposal);

    Task<ModuleActionRecord> ExecuteAsync(
        ModuleActionProposal proposal,
        ModuleActionApproval approval,
        CancellationToken cancellationToken = default);
}

public sealed class BlockingModuleActionRuntime : IModuleActionRuntime
{
    public bool CanExecute(ModuleActionProposal proposal) => false;

    public Task<ModuleActionRecord> ExecuteAsync(
        ModuleActionProposal proposal,
        ModuleActionApproval approval,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ModuleActionRecord
        {
            ModuleId = proposal.ModuleId,
            AppId = proposal.AppId,
            ActionName = proposal.ActionName,
            Target = proposal.Target,
            CommandSummary = proposal.CommandSummary,
            Status = TuneActionStatus.Blocked,
            Message = "Module action executor is not registered."
        });
}
