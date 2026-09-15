using Microsoft.Win32;
using System.Text.Json;

namespace AppLens.Backend.Tests;

public sealed class InventoryMetadataTests
{
    [Theory]
    [InlineData(2u, InstallationScope.ThisUser)]
    [InlineData(4u, InstallationScope.AllUsers)]
    [InlineData(0u, InstallationScope.Unknown)]
    public void Msi_context_overrides_registry_hive_for_scope(uint context, InstallationScope expected)
    {
        var app = InventoryCollector.FromRegistration(new RegistrationData { Hive = RegistryHive.LocalMachine,
            KeyName = Guid.NewGuid().ToString("B"), WindowsInstaller = true, MsiContext = context, Name = "Fixture" });
        Assert.Equal(expected, app.Scope);
        if (context == 0) Assert.Equal(RemovalRoute.WindowsSettings, app.CandidateRoute);
    }

    [Fact]
    public void Same_display_name_retains_distinct_registration_identities()
    {
        var first = InventoryCollector.FromRegistration(new RegistrationData
        { Name = "Example", KeyName = "product-one", Hive = RegistryHive.CurrentUser });
        var second = InventoryCollector.FromRegistration(new RegistrationData
        { Name = "Example", KeyName = "product-two", Hive = RegistryHive.LocalMachine });
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(InstallationScope.ThisUser, first.Scope);
        Assert.Equal(InstallationScope.AllUsers, second.Scope);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(-1L, null)]
    [InlineData(0L, null)]
    [InlineData(1024L, 1048576L)]
    public void Reported_size_preserves_unknown_and_converts_installer_kib(long? input, long? expected)
    {
        var app = InventoryCollector.FromRegistration(new RegistrationData { EstimatedSizeKiB = input });
        Assert.Equal(expected, app.ReportedSizeBytes);
    }

    [Fact]
    public void Installer_no_remove_is_a_recorded_restriction_not_a_false_managed_claim()
    {
        var app = InventoryCollector.FromRegistration(new RegistrationData
        { Name = "Example", KeyName = Guid.NewGuid().ToString("B"), WindowsInstaller = true, NoRemove = true });
        Assert.Equal(RemovalRoute.None, app.CandidateRoute);
        Assert.Contains("NoRemove", app.RemovalReason);
        Assert.DoesNotContain("organization", app.RemovalReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Disk_readings_distinguish_full_disk_from_unknown_capacity()
    {
        var full = new DiskSnapshot { TotalBytes = 100, FreeBytes = 0 };
        Assert.Equal(100, full.UsedBytes);
        Assert.Equal(100d, full.UsedPercent);
        Assert.Null(new DiskSnapshot().UsedBytes);
        Assert.Null(new DiskSnapshot { TotalBytes = 0, FreeBytes = 0 }.UsedPercent);
        Assert.Null(new DiskSnapshot { TotalBytes = 100, FreeBytes = 101 }.UsedBytes);
    }

    [Fact]
    public void Reports_preserve_size_scope_identity_and_exclude_executable_command_text()
    {
        var app = InventoryCollector.FromRegistration(new RegistrationData
        { Name = "Example", KeyName = "fixture-key", EstimatedSizeKiB = 1024, UninstallCommand = "do-not-export.exe" });
        var snapshot = new InventorySnapshot { Inventory = new InventorySummary { DesktopApplications = [app] } };
        var writer = new ReportWriter();
        var json = writer.WriteJson(snapshot, true);
        using var document = JsonDocument.Parse(json);
        var saved = document.RootElement.GetProperty("Inventory").GetProperty("DesktopApplications")[0];
        Assert.Equal(1048576, saved.GetProperty("ReportedSizeBytes").GetInt64());
        Assert.Equal(app.Id, saved.GetProperty("Id").GetString());
        Assert.DoesNotContain("do-not-export.exe", json);
        var markdown = writer.WriteMarkdown(snapshot, true);
        Assert.Contains("Reported size", markdown);
        Assert.Contains("1.00 MiB", markdown);
    }
}
