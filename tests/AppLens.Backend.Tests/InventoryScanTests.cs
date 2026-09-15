using System.Text.Json;

namespace AppLens.Backend.Tests;

public sealed class InventoryScanTests
{
    [Fact]
    public async Task Inventory_is_published_while_optional_device_read_is_still_running()
    {
        var device = new TaskCompletionSource<DeviceSummary>();
        var published = new TaskCompletionSource<InventorySnapshot>();
        var service = new AuditService(_ => device.Task, _ => Task.FromResult(new InventorySummary
        { DesktopApplications = [new AppEntry { Name = "Ready first" }] }));
        var run = service.RunAsync(progress: new ImmediateProgress(snapshot => published.SetResult(snapshot)));
        var partial = await published.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(run.IsCompleted);
        Assert.True(partial.IsPartial);
        Assert.Equal("Ready first", Assert.Single(partial.Applications).Name);
        device.SetResult(new DeviceSummary());
        Assert.False((await run).IsPartial);
    }

    private sealed class ImmediateProgress(Action<InventorySnapshot> report) : IProgress<InventorySnapshot>
    {
        public void Report(InventorySnapshot value) => report(value);
    }

    [Fact]
    public async Task Failed_device_read_preserves_inventory_and_records_partial_coverage()
    {
        var service = new AuditService(_ => throw new InvalidOperationException("Unavailable"),
            _ => Task.FromResult(new InventorySummary { DesktopApplications = [new AppEntry { Name = "Fixture" }] }));
        var snapshot = await service.RunAsync();
        Assert.True(snapshot.IsPartial);
        Assert.Equal("Fixture", Assert.Single(snapshot.Applications).Name);
        Assert.Equal(ProbeState.Failed, snapshot.ProbeStatuses[0].State);
    }

    [Fact]
    public async Task Cancellation_does_not_report_a_successful_empty_scan()
    {
        var blocked = new TaskCompletionSource<InventorySummary>();
        var service = new AuditService(_ => Task.FromResult(new DeviceSummary()), _ => blocked.Task);
        using var cancellation = new CancellationTokenSource();
        var run = service.RunAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        blocked.SetResult(new InventorySummary());
    }

    [Fact]
    public async Task Repeated_scans_do_not_accumulate_previous_probe_statuses()
    {
        var service = new AuditService(_ => Task.FromResult(new DeviceSummary()),
            _ => Task.FromResult(new InventorySummary()));
        await service.RunAsync();
        Assert.Equal(2, (await service.RunAsync()).ProbeStatuses.Count);
    }

    [Fact]
    public void All_core_exports_are_inventory_only_and_redacted_by_default()
    {
        var snapshot = new InventorySnapshot
        {
            Machine = new DeviceSummary { ComputerName = "fixture-device", UserName = "fixture-user" },
            Inventory = new InventorySummary { DesktopApplications = [new AppEntry { Name = "Example | app" }] }
        };
        var writer = new ReportWriter();
        var outputs = new[] { writer.WriteJson(snapshot), writer.WriteMarkdown(snapshot), writer.WriteHtml(snapshot) };
        foreach (var output in outputs)
        {
            Assert.DoesNotContain("fixture-user", output);
            Assert.DoesNotContain("fixture-device", output);
            Assert.DoesNotContain("Top Processes", output);
            Assert.DoesNotContain("TunePlan", output);
        }
        using var json = JsonDocument.Parse(outputs[0]);
        Assert.False(json.RootElement.TryGetProperty("Tune", out _));
        Assert.Contains("fixture-user", writer.WriteJson(snapshot, true));
        Assert.Contains("Example \\| app", outputs[1]);
    }
}

