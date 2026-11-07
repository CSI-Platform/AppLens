using System.Text.Json;
using AppLens.Backend;
using Xunit;

namespace AppLens.Backend.Tests;

public sealed class DiagnosticsTests
{
    [Theory]
    [InlineData("Empty", "Empty", "[]", 0)]
    [InlineData("Unavailable", "Unavailable", "[]", 0)]
    [InlineData("PermissionLimited", "PermissionLimited", "[]", 0)]
    [InlineData("Inapplicable", "Inapplicable", "[]", 0)]
    [InlineData("Succeeded", "Partial", "[{\"Count\":1}, []]", 1)]
    [InlineData("Succeeded", "Failed", "[[]]", 0)]
    public void ProbeEnvelopePreservesCoverageAndValidRows(string incoming, string expected, string rows, int count)
    {
        var probe = DiagnosticsService.Parse("Test", "Fixture", $"{{\"State\":\"{incoming}\",\"Rows\":{rows}}}", 0.02);
        Assert.Equal(expected, probe.State);
        Assert.Equal(count, probe.Rows.Length);
    }

    [Fact]
    public async Task CancelledDiagnosticsDoNotReplaceInventory()
    {
        var source = new InventorySnapshot();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DiagnosticsService().RunAsync(source, new CancellationToken(true)));
        Assert.Empty(source.Diagnostics);
        Assert.Equal("Inventory", source.Mode);
    }

    [Fact]
    public async Task HungProbeTimesOutWithoutBlockingOtherResults()
    {
        var result = await DiagnosticsService.RunProbeAsync(("Fixture", "Probe", "Fixture"), "Start-Sleep -Seconds 30",
            TimeSpan.FromMilliseconds(300), CancellationToken.None);
        Assert.Equal("TimedOut", result.State);
        Assert.Empty(result.Rows);
        Assert.True(result.DurationSeconds < 5);
    }

    [Fact]
    public void AllExportFormatsRedactProbeValuesAndRetainMode()
    {
        var source = new InventorySnapshot
        {
            Mode = "Diagnostics",
            Machine = new() { UserName = "fixture-user", ComputerName = "fixture-host" },
            Diagnostics =
            [
                new("Storage", "Fixture", "Partial",
                    [JsonSerializer.SerializeToElement(new { Name = @"C:\Users\fixture-user\model", Host = "fixture-host" })], 1)
            ]
        };
        var writer = new ReportWriter();
        foreach (var report in new[] { writer.WriteJson(source), writer.WriteMarkdown(source), writer.WriteHtml(source) })
        {
            Assert.DoesNotContain("fixture-user", report);
            Assert.DoesNotContain("fixture-host", report);
            Assert.Contains("Diagnostics", report);
            Assert.Contains("Partial", report);
        }
        Assert.True(source.IsPartial);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0L, true)]
    [InlineData(9L, true)]
    [InlineData(10L, false)]
    public void DiskWarningDistinguishesUnknownAndFull(long? free, bool? low)
    {
        Assert.Equal(low, new DiskSnapshot { TotalBytes = 100, FreeBytes = free }.LowSpace);
    }
}
