namespace AppLens.Backend.Tests;

public sealed class BlackboardEventTests
{
    [Fact]
    public void Scan_completed_event_uses_future_proof_contract_fields()
    {
        var snapshot = new AuditSnapshot
        {
            GeneratedAt = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero),
            Readiness = new ReadinessSummary
            {
                Score = 91,
                Rating = "Ready",
                ReviewCount = 2,
                OptionalCount = 1,
                AdminRequiredCount = 1
            },
            Findings =
            [
                new Finding { Severity = FindingSeverity.Review, Category = FindingCategory.Startup, Title = "Startup review" }
            ],
            TunePlan =
            [
                new TunePlanItem { Id = "startup-docker", Title = "Disable startup entry" }
            ]
        };

        var evt = BlackboardEvent.ForScanCompleted(snapshot, correlationId: "corr-scan");

        Assert.Equal("1.0", evt.SchemaVersion);
        Assert.Equal(BlackboardEventType.ScanCompleted, evt.EventType);
        Assert.Equal("applens-desktop", evt.ParticipantIdentity);
        Assert.Equal(BlackboardParticipantKind.FirstPartyModule, evt.ParticipantKind);
        Assert.Equal("report", evt.ModuleId);
        Assert.Equal("applens-desktop", evt.AppId);
        Assert.Equal("local_workstation", evt.ScopeId);
        Assert.Equal("corr-scan", evt.CorrelationId);
        Assert.Equal(BlackboardLifecycleState.Created, evt.LifecycleState);
        Assert.Equal(BlackboardDataState.Validated, evt.DataState);
        Assert.Equal(BlackboardPrivacyState.RawPrivate, evt.PrivacyState);
        Assert.Contains("91/100", evt.Summary, StringComparison.Ordinal);
        Assert.Equal("91", evt.Payload["readiness_score"]);
        Assert.Equal("1", evt.Payload["finding_count"]);
        Assert.Equal("1", evt.Payload["tune_plan_count"]);
        Assert.Null(evt.GrantId);
        Assert.Null(evt.CanonicalHash);
        Assert.Null(evt.SignerKeyId);
        Assert.Null(evt.Signature);
    }

    [Fact]
    public void Tune_action_event_records_policy_result()
    {
        var action = new TuneActionRecord
        {
            Id = "act-1",
            PlanItemId = "startup-docker",
            Kind = ProposedActionKind.DisableStartup,
            Status = TuneActionStatus.Succeeded,
            Target = "Docker Desktop",
            Message = "Startup entry was disabled.",
            RequiresAdmin = false,
            StartedAt = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero),
            CompletedAt = new DateTimeOffset(2026, 5, 10, 12, 0, 1, TimeSpan.Zero)
        };

        var evt = BlackboardEvent.ForTuneAction(action, correlationId: "corr-tune");

        Assert.Equal(BlackboardEventType.TuneActionCompleted, evt.EventType);
        Assert.Equal("tune", evt.ModuleId);
        Assert.Equal("corr-tune", evt.CorrelationId);
        Assert.Equal(BlackboardDataState.Validated, evt.DataState);
        Assert.Equal("Docker Desktop", evt.Payload["target"]);
        Assert.NotNull(evt.PolicyResult);
        Assert.True(evt.PolicyResult.Allowed);
        Assert.False(evt.PolicyResult.RequiresAdmin);
        Assert.True(evt.PolicyResult.RequiresApproval);
        Assert.Equal("low", evt.PolicyResult.RiskLevel);
    }

    [Fact]
    public void Not_configured_module_detection_is_unavailable_not_blocked()
    {
        var status = new ModuleStatus
        {
            ModuleId = "llm",
            AppId = "applens-llm",
            DisplayName = "AppLens-LLM",
            Availability = ModuleAvailability.NotConfigured,
            Reason = "AppLens-LLM module path is not configured."
        };
        var manifest = new ModuleManifest
        {
            ModuleId = "llm",
            AppId = "applens-llm",
            DisplayName = "AppLens-LLM",
            ModuleKind = "local-llm-adapter"
        };

        var evt = BlackboardEvent.ForModuleDetected(status, manifest, "corr-module");

        Assert.Equal(BlackboardDataState.Unavailable, evt.DataState);
        Assert.Equal("NotConfigured", evt.Payload["availability"]);
    }
}
