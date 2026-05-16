using AppLens.Backend;

namespace AppLens.Desktop.Tests;

public sealed class DashboardPresentationTests
{
    [Fact]
    public void FromState_formats_summary_cards_and_dashboard_rows()
    {
        var state = new AppLensDashboardState
        {
            Summary = new DashboardSummaryReadModel
            {
                OverallState = "Action Required",
                ModuleCount = 4,
                AvailableModuleCount = 1,
                BlockedModuleCount = 3,
                PendingActionCount = 2,
                RecentEventCount = 3,
                LastLedgerEventAt = new DateTimeOffset(2026, 5, 10, 12, 3, 0, TimeSpan.Zero)
            },
            ModuleCards =
            [
                new ModuleCardReadModel
                {
                    ModuleId = "llm",
                    DisplayName = "AppLens-LLM",
                    ModuleKind = "local-llm-adapter",
                    Availability = ModuleAvailability.Available,
                    StatusLabel = "Available",
                    RiskLevel = "medium",
                    Reason = "Package and CLI source detected.",
                    NextAction = "Review module status in AppLens.",
                    CapabilityCount = 3,
                    ActionCount = 2,
                    HealthCheckCount = 2,
                    StorageRootCount = 2,
                    HasRunnableActions = true
                }
            ],
            PendingActions =
            [
                new PendingTuneActionReadModel
                {
                    ProposalId = "proposal-1",
                    PlanItemId = "startup-1",
                    Kind = ProposedActionKind.DisableStartup,
                    Target = "Docker Desktop",
                    TargetContext = @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                    RiskLevel = "Medium",
                    RequiresAdmin = true,
                    ProposedAt = new DateTimeOffset(2026, 5, 10, 12, 1, 0, TimeSpan.Zero),
                    Summary = "Tune action proposed.",
                    CorrelationId = "corr-1"
                }
            ],
            RecentLedgerEvents =
            [
                new LedgerEventReadModel
                {
                    EventId = "evt-1",
                    EventType = BlackboardEventType.ScanCompleted,
                    ModuleId = "report",
                    AppId = "applens-desktop",
                    CorrelationId = "corr-1",
                    CreatedAt = new DateTimeOffset(2026, 5, 10, 12, 3, 0, TimeSpan.Zero),
                    DataState = BlackboardDataState.Validated,
                    PrivacyState = BlackboardPrivacyState.RawPrivate,
                    Summary = "Scan completed."
                }
            ]
        };

        var model = DashboardPresentation.FromState(state);

        Assert.Equal("Action Required", model.Summary.OverallState);
        Assert.Equal("1 available / 3 blocked", model.Summary.ModuleCoverage);
        Assert.Equal("2 pending", model.Summary.PendingApprovals);
        Assert.Equal("3 events", model.Summary.RecentEvents);
        Assert.Equal("May 10, 2026 12:03 PM", model.Summary.LastEvent);
        Assert.Equal("action", model.Rail.DashboardBadge);
        Assert.Equal("4", model.Rail.InventoryBadge);
        Assert.Equal("2", model.Rail.TunePlanBadge);
        Assert.Equal("3", model.Rail.ReportsBadge);

        var module = Assert.Single(model.ModuleCards);
        Assert.Equal("AppLens-LLM", module.DisplayName);
        Assert.Equal("Available", module.Availability);
        Assert.Equal("medium risk", module.Risk);
        Assert.Equal("3 capabilities", module.CapabilityText);
        Assert.Equal("2 actions", module.ActionText);
        Assert.Equal("Runnable", module.RunnableActionText);

        var action = Assert.Single(model.PendingActions);
        Assert.Equal("Disable startup", action.Kind);
        Assert.Equal("Medium risk", action.Risk);
        Assert.Equal("Admin approval", action.AdminState);
        Assert.Equal("May 10, 2026 12:01 PM", action.ProposedAt);

        var ledgerEvent = Assert.Single(model.RecentLedgerEvents);
        Assert.Equal("Scan completed", ledgerEvent.Type);
        Assert.Equal("validated", ledgerEvent.DataState);
        Assert.Equal("raw private", ledgerEvent.PrivacyState);

        var railModule = Assert.Single(model.Rail.Modules);
        Assert.Equal("AppLens-LLM", railModule.DisplayName);
        Assert.Equal("ok", railModule.Badge);
    }

    [Fact]
    public void FromState_uses_empty_state_copy_when_dashboard_has_no_activity()
    {
        var model = DashboardPresentation.FromState(new AppLensDashboardState());

        Assert.Equal("Ready", model.Summary.OverallState);
        Assert.Equal("0 available / 0 blocked", model.Summary.ModuleCoverage);
        Assert.Equal("0 pending", model.Summary.PendingApprovals);
        Assert.Equal("No ledger events", model.Summary.LastEvent);
        Assert.Equal("No module cards available.", model.ModuleEmptyState);
        Assert.Equal("No pending Tune approvals.", model.PendingApprovalEmptyState);
        Assert.Equal("No recent ledger events.", model.LedgerEmptyState);
    }

    [Fact]
    public void FromState_labels_missing_module_executor_without_calling_it_read_only()
    {
        var state = new AppLensDashboardState
        {
            ModuleCards =
            [
                new ModuleCardReadModel
                {
                    ModuleId = "llm",
                    DisplayName = "AppLens-LLM",
                    ModuleKind = "local-llm-adapter",
                    Availability = ModuleAvailability.Available,
                    StatusLabel = "Available",
                    RiskLevel = "medium",
                    Reason = "Package and CLI source detected.",
                    NextAction = "Module detected; action executor is not implemented yet.",
                    CapabilityCount = 3,
                    ActionCount = 2,
                    HealthCheckCount = 2,
                    StorageRootCount = 2,
                    HasRunnableActions = false,
                    ActionRuntimeLabel = "No runner"
                }
            ]
        };

        var module = Assert.Single(DashboardPresentation.FromState(state).ModuleCards);

        Assert.Equal("No runner", module.RunnableActionText);
        Assert.Contains("executor", module.NextAction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildActiveAppRows_returns_top_five_processes_by_memory_pressure()
    {
        var snapshot = new AuditSnapshot
        {
            Tune = new TuneSummary
            {
                TopProcesses =
                [
                    new ProcessSnapshot { Name = "small-helper", Id = 6, WorkingSetBytes = 128L * 1024 * 1024, CpuSeconds = 1.1 },
                    new ProcessSnapshot { Name = "Docker Desktop", Id = 2, WorkingSetBytes = 1536L * 1024 * 1024, CpuSeconds = 12.25 },
                    new ProcessSnapshot { Name = "Code", Id = 3, WorkingSetBytes = 768L * 1024 * 1024, CpuSeconds = 9.4 },
                    new ProcessSnapshot { Name = "chrome", Id = 4, WorkingSetBytes = 1024L * 1024 * 1024, CpuSeconds = 27.8 },
                    new ProcessSnapshot { Name = "AppLens", Id = 5, WorkingSetBytes = 256L * 1024 * 1024, CpuSeconds = 2 },
                    new ProcessSnapshot { Name = "ollama", Id = 1, WorkingSetBytes = 2048L * 1024 * 1024, CpuSeconds = 41.6 }
                ]
            }
        };

        var rows = DashboardPresentation.BuildActiveAppRows(snapshot);

        Assert.Equal(["ollama", "Docker Desktop", "chrome", "Code", "AppLens"], rows.Select(row => row.Name).ToArray());
        Assert.Equal("PID 1", rows[0].ProcessId);
        Assert.Equal("2.00 GB", rows[0].Memory);
        Assert.Equal("41.6s", rows[0].Cpu);
        Assert.Equal("Local AI workload; verify model jobs are intentional.", rows[0].Relevance);
        Assert.Equal("Container runtime; confirm it needs to be active.", rows[1].Relevance);
    }

    [Fact]
    public void Readiness_gauge_uses_score_and_rating_as_separate_labels()
    {
        var snapshot = new AuditSnapshot
        {
            Readiness = new ReadinessSummary
            {
                Score = 100,
                Rating = "Attention"
            }
        };

        Assert.Equal("100", DashboardPresentation.FormatReadinessScore(snapshot));
        Assert.Equal("Attention", DashboardPresentation.FormatReadinessRating(snapshot));
    }

    [Fact]
    public void CalculateWindowBounds_clamps_dashboard_size_to_work_area()
    {
        var bounds = DashboardWindowSizing.Calculate(
            new DashboardWorkArea(100, 50, 1280, 720),
            scale: 1d);

        Assert.Equal(1180, bounds.Width);
        Assert.Equal(640, bounds.Height);
        Assert.Equal(150, bounds.X);
        Assert.Equal(90, bounds.Y);
    }

    [Fact]
    public void CalculateWindowBounds_scales_for_high_dpi_when_space_allows()
    {
        var bounds = DashboardWindowSizing.Calculate(
            new DashboardWorkArea(0, 0, 2880, 1800),
            scale: 2d);

        Assert.Equal(2360, bounds.Width);
        Assert.Equal(1640, bounds.Height);
        Assert.Equal(260, bounds.X);
        Assert.Equal(80, bounds.Y);
    }
}
