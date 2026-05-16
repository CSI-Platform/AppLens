namespace AppLens.Backend.Tests;

public sealed class AuditSnapshotStoreTests
{
    [Fact]
    public async Task Load_latest_returns_null_when_missing()
    {
        var root = Path.Combine(Path.GetTempPath(), "AppLens-AuditSnapshotStoreTests", Guid.NewGuid().ToString("N"));
        var storage = AppLensRuntimeStorage.FromRoot(root);
        var store = new AuditSnapshotStore(storage);

        var latest = await store.LoadLatestAsync();

        Assert.Null(latest);
    }

    [Fact]
    public async Task Save_persists_latest_and_archives_snapshot()
    {
        var root = Path.Combine(Path.GetTempPath(), "AppLens-AuditSnapshotStoreTests", Guid.NewGuid().ToString("N"));
        var storage = AppLensRuntimeStorage.FromRoot(root);
        var store = new AuditSnapshotStore(storage);

        var snapshot = new AuditSnapshot
        {
            GeneratedAt = new DateTimeOffset(2026, 05, 11, 14, 50, 26, TimeSpan.Zero),
            Machine = new MachineSummary { ComputerName = "TESTBOX", UserName = "tester" },
            Readiness = new ReadinessSummary { Score = 97, Rating = "Ready", Highlights = ["OK"] },
            Inventory = new InventorySummary
            {
                DesktopApplications = [new AppEntry { Name = "Foo", Version = "1.0", Publisher = "Bar", Source = "msi", UserInstalled = true }]
            },
            TunePlan = [new TunePlanItem { Id = "startup-docker", Title = "Disable startup entry", Category = TunePlanCategory.Optional, Risk = TunePlanRisk.Low }]
        };

        await store.SaveAsync(snapshot);

        Assert.True(Directory.Exists(storage.SnapshotsDirectory));
        Assert.True(File.Exists(storage.LatestSnapshotJson));

        var archives = Directory.GetFiles(storage.SnapshotsDirectory, "audit-*.json", SearchOption.TopDirectoryOnly);
        Assert.NotEmpty(archives);

        var loaded = await store.LoadLatestAsync();

        Assert.NotNull(loaded);
        Assert.Equal(snapshot.GeneratedAt, loaded!.GeneratedAt);
        Assert.Equal("TESTBOX", loaded.Machine.ComputerName);
        Assert.Equal(97, loaded.Readiness.Score);
        Assert.Equal("Foo", loaded.Inventory.DesktopApplications.Single().Name);
        Assert.Equal("startup-docker", loaded.TunePlan.Single().Id);
    }
}

