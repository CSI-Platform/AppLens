using AppLens.Backend;
using AppLens.Desktop;

namespace AppLens.Desktop.Tests;

public class InventoryPresentationTests
{
    [Fact]
    public void Fast_removal_first_observed_as_absent_requests_an_inventory_refresh()
    {
        var action = new RemovalRecord { Id = "fast", AppId = "fixture", Verification = RemovalVerification.Absent };

        Assert.True(InventoryPresentation.HasNewlyAbsentAction([], [action]));
    }

    [Fact]
    public void Delayed_removal_requests_a_refresh_once_when_absence_is_observed()
    {
        var pending = new RemovalRecord { Id = "delayed", AppId = "fixture", Verification = RemovalVerification.Present };
        var absent = pending with { Verification = RemovalVerification.Absent };

        Assert.True(InventoryPresentation.HasNewlyAbsentAction([pending], [absent]));
        Assert.False(InventoryPresentation.HasNewlyAbsentAction([absent], [absent]));
    }

    [Theory]
    [InlineData(RemovalVerification.Present)]
    [InlineData(RemovalVerification.Unavailable)]
    public void Unconfirmed_removal_does_not_request_an_inventory_refresh(RemovalVerification verification)
    {
        var action = new RemovalRecord { Id = "unconfirmed", AppId = "fixture", Verification = verification };

        Assert.False(InventoryPresentation.HasNewlyAbsentAction([], [action]));
    }

    [Fact]
    public void Assessment_is_cached_and_all_reports_use_the_same_current_action_status_as_the_row()
    {
        var app = new AppEntry { Id = "route", Name = "Fixture", Kind = ApplicationKind.Desktop,
            CandidateRoute = RemovalRoute.Vendor, UninstallCommand = "missing.exe" };
        Assert.Same(app.Removal, app.Removal);
        var action = new RemovalRecord { AppId = app.Id, Outcome = "Not verified", ExecutionOutcome = "Completed",
            Verification = RemovalVerification.Absent, Revision = 4, LastCheckedAt = DateTimeOffset.UtcNow };
        var row = new InventoryRow(app, action);
        var snapshot = new InventorySnapshot { Inventory = new InventorySummary { DesktopApplications = [app] }, Actions = [action] };
        var writer = new ReportWriter();
        Assert.Contains(action.StatusDisplay, row.RemovalDisplay);
        Assert.Contains(action.StatusDisplay, writer.WriteMarkdown(snapshot));
        Assert.Contains("Absent when rechecked", writer.WriteHtml(snapshot));
        using var json = System.Text.Json.JsonDocument.Parse(writer.WriteJson(snapshot));
        Assert.Equal(action.StatusDisplay, json.RootElement.GetProperty("Actions")[0].GetProperty("StatusDisplay").GetString());
        Assert.Equal("WindowsSettings", json.RootElement.GetProperty("Inventory").GetProperty("DesktopApplications")[0].GetProperty("Removal").GetProperty("Route").GetString());
    }
    [Fact]
    public void Unsupported_registered_vendor_route_is_in_the_settings_filter_and_matches_the_button()
    {
        var app = new AppEntry { Id = "broken-route", Name = "Fixture", Kind = ApplicationKind.Desktop,
            CandidateRoute = RemovalRoute.Vendor, UninstallCommand = "missing-uninstaller.exe" };
        Assert.Equal("Windows…", new InventoryRow(app).ActionLabel);
        Assert.Single(InventoryPresentation.Filter([app], removal: 2));
        Assert.Empty(InventoryPresentation.Filter([app], removal: 1));
    }
    [Fact]
    public void StorePublisherShowsCommonNameAndDiskRowSummarizesUsage()
    {
        var row = new InventoryRow(new AppEntry { Id = "store", Publisher = "CN=Microsoft Corporation, O=Microsoft Corporation, C=US" });
        Assert.Equal("Microsoft Corporation", row.PublisherDisplay);
        Assert.Equal("ACME", new InventoryRow(new AppEntry { Id = "plain", Publisher = "ACME" }).PublisherDisplay);
        var disk = new DiskRow(new DiskSnapshot { Volume = "C:\\", TotalBytes = 1000, FreeBytes = 250 });
        Assert.Equal("C:", disk.Volume);
        Assert.Equal(75, disk.UsedPercent);
        Assert.Equal("75%", disk.PercentDisplay);
        Assert.Equal("Size unknown", new DiskRow(new DiskSnapshot { Volume = "D:\\" }).Figures);
    }
    [Fact]
    public void AccessibleRowTextIdentifiesSameNamedInstallations()
    {
        var personal = new InventoryRow(new AppEntry { Id = "personal", Name = "Suite", Version = "1.0", Publisher = "ACME", Scope = InstallationScope.ThisUser });
        var machine = new InventoryRow(new AppEntry { Id = "machine", Name = "Suite", Version = "2.0", Publisher = "ACME", Scope = InstallationScope.AllUsers });
        // WinUI's item automation peer falls back to the item text, not the
        // AutomationProperties.Name binding on its container style.
        Assert.Contains("Suite", personal.ToString());
        Assert.Contains("1.0", personal.ToString());
        Assert.Contains("ACME", personal.ToString());
        Assert.Contains("This user", personal.ToString());
        Assert.Contains("All users", machine.ToString());
        Assert.NotEqual(personal.ToString(), machine.ToString());
    }

    [Fact]
    public void NumericSortKeepsUnknownLastInBothDirections()
    {
        AppEntry[] apps = [new() { Id = "unknown" }, new() { Id = "large", ReportedSizeBytes = 2048 }, new() { Id = "small", ReportedSizeBytes = 1024 }];
        Assert.Equal(["large", "small", "unknown"], InventoryPresentation.Filter(apps, sort: 2).Select(a => a.Id));
        Assert.Equal(["small", "large", "unknown"], InventoryPresentation.Filter(apps, sort: 3).Select(a => a.Id));
    }

    [Fact]
    public void FiltersCombineWithoutChangingCapturedInventoryOrMergingNames()
    {
        AppEntry[] apps = [new() { Id = "a", Name = "Suite", Publisher = "ACME", Scope = InstallationScope.ThisUser, Kind = ApplicationKind.Desktop, CandidateRoute = RemovalRoute.Msi, ProductCode = "{0CC8958B-BBE6-47A4-BB56-B70FE34AE90C}" },
            new() { Id = "b", Name = "Suite", Publisher = "ACME", Scope = InstallationScope.AllUsers, Kind = ApplicationKind.Desktop, CandidateRoute = RemovalRoute.Msi, ProductCode = "{0CC8958B-BBE6-47A4-BB56-B70FE34AE90C}" },
            new() { Id = "c", Name = "Framework", Kind = ApplicationKind.Runtime }];
        Assert.Equal(2, InventoryPresentation.Filter(apps, "acme").Count);
        Assert.Equal("a", Assert.Single(InventoryPresentation.Filter(apps, "acme", scope: 1, kind: 1, removal: 1)).Id);
        Assert.Equal(3, apps.Length);
        Assert.Equal("c", Assert.Single(InventoryPresentation.Filter(apps, removal: 3)).Id);
    }

    [Fact]
    public void NameSortIsCaseInsensitiveAndStableForDuplicateNames()
    {
        AppEntry[] apps = [new() { Id = "2", Name = "z" }, new() { Id = "b", Name = "Alpha" }, new() { Id = "a", Name = "alpha" }];
        Assert.Equal(["a", "b", "2"], InventoryPresentation.Filter(apps).Select(a => a.Id));
    }
}
