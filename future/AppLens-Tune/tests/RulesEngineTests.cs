namespace AppLens.Backend.Tests;

public sealed class RulesEngineTests
{
    [Fact]
    public void Rules_emit_review_for_enabled_browser_startup()
    {
        var snapshot = new AuditSnapshot
        {
            Tune = new TuneSummary
            {
                StartupEntries =
                [
                    new StartupEntry
                    {
                        Name = "GoogleChromeAutoLaunch_123",
                        State = "Enabled",
                        Command = "chrome.exe"
                    }
                ]
            }
        };

        var findings = new RulesEngine().Evaluate(snapshot);

        Assert.Contains(findings, finding =>
            finding.Severity == FindingSeverity.Review &&
            finding.Category == FindingCategory.Startup &&
            finding.Title.Contains("Browser", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rules_emit_repo_review_for_onedrive_repos()
    {
        var snapshot = new AuditSnapshot
        {
            Tune = new TuneSummary
            {
                RepoPlacements =
                [
                    new RepoPlacement { Root = @"C:\Users\test\OneDrive\Documents", RepoCount = 2 }
                ]
            }
        };

        var findings = new RulesEngine().Evaluate(snapshot);

        Assert.Contains(findings, finding =>
            finding.Severity == FindingSeverity.Review &&
            finding.Category == FindingCategory.RepoPlacement);
    }

    [Fact]
    public void Rules_emit_storage_optional_for_low_free_space()
    {
        var snapshot = new AuditSnapshot
        {
            Machine = new MachineSummary
            {
                SystemDriveFreeBytes = 50L * 1024 * 1024 * 1024
            }
        };

        var findings = new RulesEngine().Evaluate(snapshot);

        Assert.Contains(findings, finding =>
            finding.Severity == FindingSeverity.Optional &&
            finding.Category == FindingCategory.Storage);
    }

    [Fact]
    public void Rules_emit_review_for_many_startup_entries()
    {
        var snapshot = new AuditSnapshot
        {
            Tune = new TuneSummary
            {
                StartupEntries = Enumerable.Range(1, 8)
                    .Select(index => new StartupEntry { Name = $"Startup {index}", State = "Enabled" })
                    .ToList()
            }
        };

        var findings = new RulesEngine().Evaluate(snapshot);

        Assert.Contains(findings, finding =>
            finding.Severity == FindingSeverity.Review &&
            finding.Category == FindingCategory.Startup &&
            finding.Title.Contains("queue", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rules_emit_review_for_automatic_vendor_services()
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

        var findings = new RulesEngine().Evaluate(snapshot);

        Assert.Contains(findings, finding =>
            finding.Severity == FindingSeverity.Review &&
            finding.Category == FindingCategory.Services);
    }
}
