namespace AppLens.Backend.Tests;

public sealed class PlatformLoopServiceTests : IDisposable
{
    private readonly string _root;
    private readonly AppLensRuntimeStorage _storage;
    private readonly BlackboardStore _store;

    public PlatformLoopServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "AppLens-PlatformLoopTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _storage = AppLensRuntimeStorage.FromRoot(Path.Combine(_root, "runtime"));
        _store = new BlackboardStore(_storage);
    }

    [Fact]
    public async Task Approved_platform_loop_detects_proposes_approves_executes_and_records()
    {
        var runtime = new FakeTuneActionRuntime();
        var service = CreateService(runtime);
        var item = StartupItem("startup-docker");

        var statuses = await service.DetectModulesAsync("corr-loop");
        var proposal = await service.ProposeTuneActionAsync(item, "corr-loop");
        var approval = await service.ApproveTuneActionAsync(proposal, approvedBy: "operator", approved: true, rationale: "Approved for test.", correlationId: "corr-loop");
        var record = await service.ExecuteTuneActionAsync(item, proposal, approval);

        var events = await _store.QueryAsync(new BlackboardEventQuery { CorrelationId = "corr-loop" });

        Assert.Equal(4, statuses.Count);
        Assert.Equal(TuneActionStatus.Succeeded, record.Status);
        Assert.True(runtime.DisableStartupCalled);
        Assert.NotEmpty(approval.GrantId);
        Assert.Contains(events, evt => evt.EventType == BlackboardEventType.ModuleDetected);
        Assert.Contains(events, evt => evt.EventType == BlackboardEventType.ActionProposed && evt.Payload["proposal_id"] == proposal.ProposalId);
        Assert.Contains(events, evt => evt.EventType == BlackboardEventType.ActionApproved && evt.GrantId == approval.GrantId);
        Assert.Contains(events, evt => evt.EventType == BlackboardEventType.ActionExecuted && evt.Payload["status"] == TuneActionStatus.Succeeded.ToString());
    }

    [Fact]
    public async Task Rejected_approval_does_not_call_runtime_and_records_blocked_execution()
    {
        var runtime = new FakeTuneActionRuntime();
        var service = CreateService(runtime);
        var item = StartupItem("startup-docker");

        var proposal = await service.ProposeTuneActionAsync(item, "corr-reject");
        var approval = await service.ApproveTuneActionAsync(proposal, approvedBy: "operator", approved: false, rationale: "Not approved.", correlationId: "corr-reject");
        var record = await service.ExecuteTuneActionAsync(item, proposal, approval, "corr-reject");

        var executionEvents = await _store.QueryAsync(new BlackboardEventQuery
        {
            CorrelationId = "corr-reject",
            EventType = BlackboardEventType.ActionExecuted
        });

        Assert.Equal(TuneActionStatus.Blocked, record.Status);
        Assert.False(runtime.DisableStartupCalled);
        var executionEvent = Assert.Single(executionEvents);
        Assert.Equal(TuneActionStatus.Blocked.ToString(), executionEvent.Payload["status"]);
        Assert.Contains("approval", executionEvent.Payload["message"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Decide_pending_tune_action_by_proposal_id_records_approval_and_closes_dashboard_item()
    {
        var runtime = new FakeTuneActionRuntime();
        var service = CreateService(runtime);
        var item = StartupItem("startup-docker");
        var proposal = await service.ProposeTuneActionAsync(item, "corr-decision");

        var approval = await service.DecidePendingTuneActionAsync(
            proposal.ProposalId,
            approvedBy: "operator",
            approved: true,
            rationale: "Approved from dashboard.",
            correlationId: "corr-decision");

        var dashboard = await CreateDashboardService().GetPendingActionsAsync();
        var approvalEvents = await _store.QueryAsync(new BlackboardEventQuery
        {
            CorrelationId = "corr-decision",
            EventType = BlackboardEventType.ActionApproved
        });

        Assert.Equal(proposal.ProposalId, approval.ProposalId);
        Assert.True(approval.Approved);
        Assert.Empty(dashboard);
        var approvalEvent = Assert.Single(approvalEvents);
        Assert.Equal("True", approvalEvent.Payload["approved"]);
        Assert.Equal("operator", approvalEvent.Payload["approved_by"]);
        Assert.Equal("Approved from dashboard.", approvalEvent.Payload["rationale"]);
    }

    [Fact]
    public async Task Decide_pending_tune_action_by_proposal_id_rejects_duplicate_decisions()
    {
        var runtime = new FakeTuneActionRuntime();
        var service = CreateService(runtime);
        var item = StartupItem("startup-docker");
        var proposal = await service.ProposeTuneActionAsync(item, "corr-duplicate-decision");
        await service.DecidePendingTuneActionAsync(
            proposal.ProposalId,
            approvedBy: "operator",
            approved: false,
            rationale: "Not needed.",
            correlationId: "corr-duplicate-decision");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DecidePendingTuneActionAsync(
                proposal.ProposalId,
                approvedBy: "operator",
                approved: true,
                rationale: "Changed mind.",
                correlationId: "corr-duplicate-decision"));

        Assert.Contains("already decided", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private PlatformLoopService CreateService(FakeTuneActionRuntime runtime) =>
        new(
            new ModuleStatusService(new ModuleStatusPaths
            {
                AppLensLlmRoot = Path.Combine(_root, "missing-llm"),
                OracleRoot = Path.Combine(_root, "missing-oracle"),
                MailboxRoot = Path.Combine(_root, "missing-mailbox"),
                AppLensZeroRoot = Path.Combine(_root, "missing-zero")
            }),
            _store,
            new TuneActionExecutor(runtime));

    private DashboardReadModelService CreateDashboardService() =>
        new(
            new ModuleStatusService(new ModuleStatusPaths
            {
                AppLensLlmRoot = Path.Combine(_root, "missing-llm"),
                OracleRoot = Path.Combine(_root, "missing-oracle"),
                MailboxRoot = Path.Combine(_root, "missing-mailbox"),
                AppLensZeroRoot = Path.Combine(_root, "missing-zero")
            }),
            _store);

    private static TunePlanItem StartupItem(string id) =>
        new()
        {
            Id = id,
            Category = TunePlanCategory.Optional,
            Risk = TunePlanRisk.Low,
            Title = "Disable Docker startup",
            BackupPlan = "Re-enable startup if needed.",
            VerificationStep = "Re-scan startup entries.",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.DisableStartup,
                ExecutionState = TunePlanExecutionState.RequiresUserConsent,
                Target = "Docker Desktop",
                TargetContext = @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                Description = "Disable Docker Desktop startup."
            }
        };

    private sealed class FakeTuneActionRuntime : ITuneActionRuntime
    {
        public bool IsAdministrator => false;

        public bool DisableStartupCalled { get; private set; }

        public Task<long> ClearDirectoryContentsAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(0L);

        public Task SetServiceStartModeManualAsync(string serviceName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DisableStartupEntryAsync(string entryName, string location, CancellationToken cancellationToken = default)
        {
            DisableStartupCalled = true;
            return Task.CompletedTask;
        }

        public Task EnableStartupEntryAsync(string entryName, string location, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
