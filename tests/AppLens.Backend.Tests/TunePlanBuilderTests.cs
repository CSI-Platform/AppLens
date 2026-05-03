namespace AppLens.Backend.Tests;

public sealed class TunePlanBuilderTests
{
    [Fact]
    public void Startup_findings_become_future_user_consent_guidance()
    {
        var snapshot = new AuditSnapshot
        {
            Findings =
            [
                new Finding
                {
                    Severity = FindingSeverity.Review,
                    Category = FindingCategory.Startup,
                    Title = "Docker Desktop starts at sign-in",
                    Detail = "Docker startup should be validated for this user."
                }
            ]
        };

        var plan = new TunePlanBuilder().Build(snapshot);

        var item = Assert.Single(plan);
        Assert.Equal(TunePlanCategory.UserChoice, item.Category);
        Assert.Equal(ProposedActionKind.DisableStartup, item.ProposedAction.Kind);
        Assert.Equal(TunePlanExecutionState.FutureUserConsent, item.ProposedAction.ExecutionState);
        Assert.False(item.RequiresAdmin);
    }

    [Fact]
    public void Automatic_review_services_are_admin_required_and_not_executable_in_v1()
    {
        var snapshot = new AuditSnapshot
        {
            Tune = new TuneSummary
            {
                Services =
                [
                    new ServiceSnapshot
                    {
                        Name = "ASUSSoftwareManager",
                        DisplayName = "ASUS Software Manager",
                        Status = "Running",
                        StartType = "Automatic"
                    }
                ]
            }
        };

        var plan = new TunePlanBuilder().Build(snapshot);

        var item = Assert.Single(plan);
        Assert.Equal(TunePlanCategory.AdminRequired, item.Category);
        Assert.Equal(ProposedActionKind.SetServiceManual, item.ProposedAction.Kind);
        Assert.Equal(TunePlanExecutionState.FutureAdminRequired, item.ProposedAction.ExecutionState);
        Assert.True(item.RequiresAdmin);
    }

    [Fact]
    public void Privacy_finding_keeps_v1_read_only()
    {
        var snapshot = new AuditSnapshot
        {
            Findings =
            [
                new Finding
                {
                    Severity = FindingSeverity.Stable,
                    Category = FindingCategory.Privacy,
                    Title = "Read-only audit",
                    Detail = "No system settings were changed."
                }
            ]
        };

        var plan = new TunePlanBuilder().Build(snapshot);

        var item = Assert.Single(plan);
        Assert.Equal(TunePlanCategory.Keep, item.Category);
        Assert.Equal(TunePlanExecutionState.ReadOnlyOnly, item.ProposedAction.ExecutionState);
        Assert.Equal(ProposedActionKind.None, item.ProposedAction.Kind);
    }

    [Fact]
    public void Local_ai_profile_adds_read_only_autoresearch_guidance()
    {
        var snapshot = new AuditSnapshot
        {
            Tune = new TuneSummary
            {
                LocalAiProfile = new LocalAiProfile
                {
                    Readiness = LocalAiReadiness.InferenceReady,
                    WorkloadClass = "Small-model/autoresearch worker",
                    RecommendedRuntime = "llama.cpp CUDA-MMQ with full offload.",
                    TrainingReady = false,
                    TrainingGate = "Training remains gated until PyTorch CUDA passes a smoke test."
                }
            }
        };

        var plan = new TunePlanBuilder().Build(snapshot);

        var item = Assert.Single(plan, item => item.Title.Contains("autoresearch", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(TunePlanCategory.Review, item.Category);
        Assert.Equal(TunePlanRisk.Low, item.Risk);
        Assert.Equal(TunePlanExecutionState.ReadOnlyOnly, item.ProposedAction.ExecutionState);
        Assert.Contains("llama.cpp", item.Guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Training remains gated", item.VerificationStep, StringComparison.OrdinalIgnoreCase);
    }
}
