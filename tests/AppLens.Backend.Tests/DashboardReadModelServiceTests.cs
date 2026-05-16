namespace AppLens.Backend.Tests;

public sealed class DashboardReadModelServiceTests : IDisposable
{
    private readonly string _root;
    private readonly AppLensRuntimeStorage _storage;
    private readonly BlackboardStore _store;

    public DashboardReadModelServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "AppLens-DashboardReadModelTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _storage = AppLensRuntimeStorage.FromRoot(Path.Combine(_root, "runtime"));
        _store = new BlackboardStore(_storage);
    }

    [Fact]
    public void Module_cards_merge_statuses_and_manifests_for_frontend()
    {
        var llmRoot = Path.Combine(_root, "AppLens-LLM");
        Directory.CreateDirectory(Path.Combine(llmRoot, "src", "applens_llm"));
        File.WriteAllText(Path.Combine(llmRoot, "pyproject.toml"), "[project]\nname='applens-llm'\n");
        File.WriteAllText(Path.Combine(llmRoot, "src", "applens_llm", "cli.py"), "# fake cli");
        var service = CreateService(new ModuleStatusPaths
        {
            AppLensLlmRoot = llmRoot,
            OracleRoot = Path.Combine(_root, "missing-oracle"),
            MailboxRoot = Path.Combine(_root, "missing-mailbox"),
            AppLensZeroRoot = Path.Combine(_root, "missing-zero")
        });

        var cards = service.GetModuleCards();

        Assert.Equal(["llm", "oracle", "mailbox", "zero"], cards.Select(card => card.ModuleId).ToArray());
        var llm = cards.Single(card => card.ModuleId == "llm");
        Assert.Equal(ModuleAvailability.Available, llm.Availability);
        Assert.Equal("local-llm-adapter", llm.ModuleKind);
        Assert.True(llm.CapabilityCount > 0);
        Assert.True(llm.ActionCount > 0);
        Assert.False(llm.HasRunnableActions);
        Assert.Equal(0, llm.RunnableActionCount);
        Assert.True(llm.NotImplementedActionCount > 0);
        Assert.Equal("No runner", llm.ActionRuntimeLabel);
        Assert.Equal("Available", llm.StatusLabel);
        Assert.Contains("executor", llm.NextAction, StringComparison.OrdinalIgnoreCase);

        var oracle = cards.Single(card => card.ModuleId == "oracle");
        Assert.Equal(ModuleAvailability.NotConfigured, oracle.Availability);
        Assert.Contains("configure", oracle.NextAction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Missing_optional_modules_do_not_require_dashboard_action()
    {
        var service = CreateService();

        var dashboard = await service.GetDashboardStateAsync();

        Assert.Equal("Ready", dashboard.Summary.OverallState);
        Assert.Equal(0, dashboard.Summary.AvailableModuleCount);
        Assert.Equal(0, dashboard.Summary.BlockedModuleCount);
        Assert.All(dashboard.ModuleCards, card => Assert.Equal(ModuleAvailability.NotConfigured, card.Availability));
    }

    [Fact]
    public void Registered_executor_makes_matching_module_actions_runnable()
    {
        var llmRoot = Path.Combine(_root, "AppLens-LLM");
        Directory.CreateDirectory(Path.Combine(llmRoot, "src", "applens_llm"));
        File.WriteAllText(Path.Combine(llmRoot, "pyproject.toml"), "[project]\nname='applens-llm'\n");
        File.WriteAllText(Path.Combine(llmRoot, "src", "applens_llm", "cli.py"), "# fake cli");
        var service = CreateService(
            new ModuleStatusPaths
            {
                AppLensLlmRoot = llmRoot,
                OracleRoot = Path.Combine(_root, "missing-oracle"),
                MailboxRoot = Path.Combine(_root, "missing-mailbox"),
                AppLensZeroRoot = Path.Combine(_root, "missing-zero")
            },
            new ModuleActionExecutorRegistry(["module-local-job", "module-report-import"]));

        var llm = service.GetModuleCards().Single(card => card.ModuleId == "llm");

        Assert.True(llm.HasRunnableActions);
        Assert.Equal(2, llm.RunnableActionCount);
        Assert.Equal(0, llm.NotImplementedActionCount);
        Assert.Equal("Runnable", llm.ActionRuntimeLabel);
    }

    [Fact]
    public async Task Configured_module_with_failed_required_check_requires_dashboard_action()
    {
        var oracleRoot = Path.Combine(_root, "Oracle");
        Directory.CreateDirectory(oracleRoot);
        var service = CreateService(new ModuleStatusPaths
        {
            AppLensLlmRoot = Path.Combine(_root, "missing-llm"),
            OracleRoot = oracleRoot,
            MailboxRoot = Path.Combine(_root, "missing-mailbox"),
            AppLensZeroRoot = Path.Combine(_root, "missing-zero")
        });

        var dashboard = await service.GetDashboardStateAsync();

        Assert.Equal("Action Required", dashboard.Summary.OverallState);
        Assert.Equal(1, dashboard.Summary.BlockedModuleCount);
        Assert.Equal(ModuleAvailability.Blocked, dashboard.ModuleCards.Single(card => card.ModuleId == "oracle").Availability);
    }

    [Fact]
    public async Task Pending_actions_return_unapproved_proposals_only()
    {
        var service = CreateService();
        var pending = Proposal("proposal-pending", "pending-startup", "corr-pending");
        var approved = Proposal("proposal-approved", "approved-startup", "corr-approved");
        var pendingItem = StartupItem(pending.PlanItemId, risk: TunePlanRisk.Low, requiresAdmin: false);
        var approvedItem = StartupItem(approved.PlanItemId, risk: TunePlanRisk.Medium, requiresAdmin: true);

        await _store.AppendAsync(BlackboardEvent.ForTuneActionProposed(pending, pendingItem));
        await _store.AppendAsync(BlackboardEvent.ForTuneActionProposed(approved, approvedItem));
        await _store.AppendAsync(BlackboardEvent.ForTuneActionApproved(
            new TuneActionApproval
            {
                ProposalId = approved.ProposalId,
                Approved = true,
                ApprovedBy = "operator",
                Rationale = "Approved."
            },
            approved));

        var actions = await service.GetPendingActionsAsync();

        var action = Assert.Single(actions);
        Assert.Equal("proposal-pending", action.ProposalId);
        Assert.Equal("pending-startup", action.PlanItemId);
        Assert.Equal(ProposedActionKind.DisableStartup, action.Kind);
        Assert.Equal("Docker Desktop", action.Target);
        Assert.Equal("Low", action.RiskLevel);
        Assert.False(action.RequiresAdmin);
        Assert.Equal("corr-pending", action.CorrelationId);
    }

    [Fact]
    public async Task Dashboard_state_includes_summary_recent_events_and_pending_actions()
    {
        var service = CreateService();
        var pending = Proposal("proposal-pending", "pending-startup", "corr-dashboard", new DateTimeOffset(2026, 5, 10, 12, 1, 0, TimeSpan.Zero));
        var item = StartupItem(pending.PlanItemId, risk: TunePlanRisk.Low, requiresAdmin: false);
        await _store.AppendAsync(new BlackboardEvent
        {
            EventId = "evt-scan",
            EventType = BlackboardEventType.ScanCompleted,
            ModuleId = "report",
            CorrelationId = "corr-dashboard",
            CreatedAt = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero),
            Summary = "Scan completed."
        });
        await _store.AppendAsync(BlackboardEvent.ForTuneActionProposed(pending, item));
        await _store.AppendAsync(new BlackboardEvent
        {
            EventId = "evt-verify",
            EventType = BlackboardEventType.VerificationRecorded,
            ModuleId = "report",
            CorrelationId = "corr-dashboard",
            CreatedAt = new DateTimeOffset(2026, 5, 10, 12, 2, 0, TimeSpan.Zero),
            Summary = "Verification recorded."
        });

        var dashboard = await service.GetDashboardStateAsync(recentEventLimit: 2);

        Assert.Equal(4, dashboard.Summary.ModuleCount);
        Assert.Equal(1, dashboard.Summary.PendingActionCount);
        Assert.Equal(2, dashboard.Summary.RecentEventCount);
        Assert.Equal(new DateTimeOffset(2026, 5, 10, 12, 2, 0, TimeSpan.Zero), dashboard.Summary.LastLedgerEventAt);
        Assert.Equal("evt-verify", dashboard.RecentLedgerEvents[0].EventId);
        Assert.Equal(BlackboardEventType.ActionProposed, dashboard.RecentLedgerEvents[1].EventType);
        Assert.Equal("corr-dashboard", dashboard.RecentLedgerEvents[1].CorrelationId);
        Assert.Single(dashboard.PendingActions);
        Assert.Equal(4, dashboard.ModuleCards.Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private DashboardReadModelService CreateService(
        ModuleStatusPaths? paths = null,
        ModuleActionExecutorRegistry? moduleActionExecutors = null) =>
        new(
            new ModuleStatusService(paths ?? new ModuleStatusPaths
            {
                AppLensLlmRoot = Path.Combine(_root, "missing-llm"),
                OracleRoot = Path.Combine(_root, "missing-oracle"),
                MailboxRoot = Path.Combine(_root, "missing-mailbox"),
                AppLensZeroRoot = Path.Combine(_root, "missing-zero")
            }),
            _store,
            moduleActionExecutors);

    private static TuneActionProposal Proposal(
        string proposalId,
        string planItemId,
        string correlationId,
        DateTimeOffset? proposedAt = null) =>
        new()
        {
            ProposalId = proposalId,
            PlanItemId = planItemId,
            Kind = ProposedActionKind.DisableStartup,
            Target = "Docker Desktop",
            TargetContext = @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            CorrelationId = correlationId,
            ProposedAt = proposedAt ?? new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero)
        };

    private static TunePlanItem StartupItem(string id, TunePlanRisk risk, bool requiresAdmin) =>
        new()
        {
            Id = id,
            Category = TunePlanCategory.Optional,
            Risk = risk,
            RequiresAdmin = requiresAdmin,
            Title = "Disable Docker startup",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.DisableStartup,
                ExecutionState = requiresAdmin ? TunePlanExecutionState.RequiresAdmin : TunePlanExecutionState.RequiresUserConsent,
                Target = "Docker Desktop",
                TargetContext = @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                Description = "Disable Docker Desktop startup."
            }
        };
}
