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
}
