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
    public async Task Execute_approved_tune_actions_records_full_approval_lifecycle()
    {
        var runtime = new FakeTuneActionRuntime();
        var service = CreateService(runtime);
        var item = StartupItem("startup-docker");

        var records = await service.ExecuteApprovedTuneActionsAsync(
            [item],
            approvedBy: "operator",
            rationale: "Approved from AppLens control board.",
            correlationId: "corr-batch");

        var events = await _store.QueryAsync(new BlackboardEventQuery { CorrelationId = "corr-batch" });

        var record = Assert.Single(records);
        Assert.Equal(TuneActionStatus.Succeeded, record.Status);
        Assert.True(runtime.DisableStartupCalled);
        Assert.Contains(events, evt => evt.EventType == BlackboardEventType.ActionProposed);
        Assert.Contains(events, evt => evt.EventType == BlackboardEventType.ActionApproved && !string.IsNullOrWhiteSpace(evt.GrantId));
        Assert.Contains(events, evt => evt.EventType == BlackboardEventType.ActionExecuted && evt.GrantId == events.First(e => e.EventType == BlackboardEventType.ActionApproved).GrantId);
        Assert.DoesNotContain(events, evt => evt.EventType == BlackboardEventType.TuneActionCompleted);
    }

    [Fact]
    public async Task Record_tune_action_verification_records_post_scan_event_for_executed_action()
    {
        var runtime = new FakeTuneActionRuntime();
        var service = CreateService(runtime);
        var item = StartupItem("startup-docker");
        var records = await service.ExecuteApprovedTuneActionsAsync(
            [item],
            approvedBy: "operator",
            rationale: "Approved from AppLens control board.",
            correlationId: "corr-verify");
        var snapshot = new AuditSnapshot
        {
            GeneratedAt = new DateTimeOffset(2026, 5, 17, 12, 10, 0, TimeSpan.Zero),
            Readiness = new ReadinessSummary { Score = 96, Rating = "Ready" },
            TunePlan = []
        };

        var verificationEvents = await service.RecordTuneActionVerificationAsync(records, snapshot);

        var verification = Assert.Single(verificationEvents);
        var events = await _store.QueryAsync(new BlackboardEventQuery
        {
            EventType = BlackboardEventType.VerificationRecorded,
            CorrelationId = "corr-verify"
        });
        var persisted = Assert.Single(events);
        Assert.Equal(verification.EventId, persisted.EventId);
        Assert.Equal(records[0].Id, persisted.Payload["action_id"]);
        Assert.Equal(records[0].ProposalId, persisted.Payload["proposal_id"]);
        Assert.Equal(records[0].GrantId, persisted.Payload["grant_id"]);
        Assert.Equal("Recorded", persisted.Payload["verification_status"]);
        Assert.Equal("96", persisted.Payload["readiness_score"]);
        Assert.Equal("0", persisted.Payload["tune_plan_count"]);
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
