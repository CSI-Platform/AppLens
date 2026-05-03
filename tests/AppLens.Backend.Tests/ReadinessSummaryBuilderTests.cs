namespace AppLens.Backend.Tests;

public sealed class ReadinessSummaryBuilderTests
{
    [Fact]
    public void Readiness_summary_counts_tune_plan_and_startup_state()
    {
        var snapshot = new AuditSnapshot
        {
            Machine = new MachineSummary
            {
                SystemDriveFreeBytes = 50L * 1024 * 1024 * 1024
            },
            Tune = new TuneSummary
            {
                StartupEntries =
                [
                    new StartupEntry { Name = "A", State = "Enabled" },
                    new StartupEntry { Name = "B", State = "Disabled" },
                    new StartupEntry { Name = "C", State = "Unknown" }
                ],
                Services =
                [
                    new ServiceSnapshot { Name = "ASUSSoftwareManager", StartType = "Automatic" }
                ],
                ToolProbes =
                [
                    new ToolProbe { Name = "Docker Summary", Status = "Skipped" }
                ],
                StorageHotspots =
                [
                    new StorageHotspot { Location = "Temp", Bytes = 10 }
                ]
            },
            TunePlan =
            [
                new TunePlanItem { Category = TunePlanCategory.Review, Risk = TunePlanRisk.Medium },
                new TunePlanItem { Category = TunePlanCategory.Optional, Risk = TunePlanRisk.Low },
                new TunePlanItem { Category = TunePlanCategory.AdminRequired, Risk = TunePlanRisk.Medium }
            ]
        };

        var summary = new ReadinessSummaryBuilder().Build(snapshot);

        Assert.Equal(2, summary.StartupEnabledCount);
        Assert.Equal(3, summary.StartupTotalCount);
        Assert.Equal(1, summary.ReviewCount);
        Assert.Equal(1, summary.OptionalCount);
        Assert.Equal(1, summary.AdminRequiredCount);
        Assert.Equal(1, summary.ToolProbeIssueCount);
        Assert.Equal(10, summary.StorageHotspotBytes);
        Assert.InRange(summary.Score, 0, 99);
    }

    [Fact]
    public void Readiness_summary_highlights_local_ai_profile()
    {
        var snapshot = new AuditSnapshot
        {
            Tune = new TuneSummary
            {
                LocalAiProfile = new LocalAiProfile
                {
                    Readiness = LocalAiReadiness.InferenceReady,
                    WorkloadClass = "Small-model/autoresearch worker"
                }
            }
        };

        var summary = new ReadinessSummaryBuilder().Build(snapshot);

        Assert.Contains(summary.Highlights, highlight =>
            highlight.Contains("Local AI", StringComparison.OrdinalIgnoreCase) &&
            highlight.Contains("InferenceReady", StringComparison.OrdinalIgnoreCase));
    }
}
