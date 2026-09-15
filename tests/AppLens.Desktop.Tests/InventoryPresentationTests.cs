using AppLens.Backend;
using AppLens.Desktop;

namespace AppLens.Desktop.Tests;

public class InventoryPresentationTests
{
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
        AppEntry[] apps = [new() { Id = "a", Name = "Suite", Publisher = "ACME", Scope = InstallationScope.ThisUser, Kind = ApplicationKind.Desktop, CandidateRoute = RemovalRoute.Msi },
            new() { Id = "b", Name = "Suite", Publisher = "ACME", Scope = InstallationScope.AllUsers, Kind = ApplicationKind.Desktop, CandidateRoute = RemovalRoute.Msi },
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
