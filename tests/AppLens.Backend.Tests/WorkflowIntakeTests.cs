using System.Text.Json;

namespace AppLens.Backend.Tests;

public sealed class WorkflowIntakeTests
{
    private static InventorySnapshot Capture() => new()
    {
        GeneratedAt = new DateTimeOffset(2026, 11, 6, 12, 0, 0, TimeSpan.Zero),
        Machine = new DeviceSummary
        {
            ComputerName = "client-laptop", UserName = "client-person", Manufacturer = "Dell", Model = "Example",
            OSDescription = "Windows 11", OSArchitecture = "X64", Processors = ["Example CPU"], Graphics = ["Example GPU"],
            TotalMemoryBytes = 16L * 1024 * 1024 * 1024, FreeMemoryBytes = 4L * 1024 * 1024 * 1024,
            Disks = [new() { Volume = "C:", TotalBytes = 1000, FreeBytes = 250 }]
        },
        Inventory = new InventorySummary
        {
            DesktopApplications = [new() { Id = "desktop-1", Name = "Work tool", Version = "3.2", Kind = ApplicationKind.Desktop }],
            RuntimesAndFrameworks = [new() { Id = "runtime-1", Name = "Runtime", Version = "10.0", Kind = ApplicationKind.Runtime }]
        },
        ProbeStatuses = [new() { Name = "Fixture source", State = ProbeState.Succeeded }],
        ClientContext = new() { Goal = "Schedule work", Services = "An online calendar", Constraints = "Keep client-person's work local" }
    };

    [Fact]
    public void Intake_uses_captured_specs_and_counts_apps_separately_from_runtimes()
    {
        var intake = Capture().WorkflowIntake;
        var facts = intake.Facts.ToDictionary(f => f.Label, f => f.Value);
        Assert.Equal("Dell Example", facts["Device"]);
        Assert.Equal("Example CPU", facts["CPU"]);
        Assert.Equal("Example GPU", facts["GPU"]);
        Assert.Equal("16.00 GiB", facts["RAM total"]);
        Assert.Contains("250 B free", facts["Storage"]);
        Assert.Equal("1", facts["Application entries observed"]);
        Assert.Equal("1", facts["Runtime entries observed"]);
        Assert.Contains("listed sources", intake.Coverage);
    }

    [Fact]
    public void Missing_readings_and_absent_source_results_are_unknown_not_readiness_claims()
    {
        var intake = new InventorySnapshot().WorkflowIntake;
        Assert.StartsWith("Unknown", intake.Coverage);
        foreach (var label in new[] { "Device", "CPU", "GPU", "RAM total", "Storage" })
            Assert.Equal("Unknown", intake.Facts.Single(f => f.Label == label).Value);
        Assert.Contains(intake.PlanningLimits, p => p.Contains("not a workload benchmark"));
        Assert.Contains(intake.PlanningLimits, p => p.Contains("firmware update availability"));
    }

    [Fact]
    public void Partial_scan_keeps_useful_observations_and_explains_the_missing_source()
    {
        var capture = Capture();
        capture.ProbeStatuses.Add(new() { Name = "Graphics", State = ProbeState.Failed, Message = "Access denied" });
        var writer = new ReportWriter();
        Assert.StartsWith("Partial", capture.WorkflowIntake.Coverage);
        foreach (var output in new[] { writer.WriteMarkdown(capture), writer.WriteHtml(capture), writer.WriteJson(capture) })
        {
            Assert.Contains("Access denied", output);
            Assert.Contains("Work tool", output);
            Assert.Contains("3.2", output);
            Assert.Contains("Example CPU", output);
        }
    }

    [Theory]
    [InlineData(IntakeReportFormat.Markdown)]
    [InlineData(IntakeReportFormat.Json)]
    [InlineData(IntakeReportFormat.Html)]
    public void Prepared_report_contains_the_full_inventory_context_and_default_redaction(IntakeReportFormat format)
    {
        var report = PreparedIntakeReport.Create(Capture(), format);
        Assert.False(report.IncludesPersonalIdentifiers);
        Assert.DoesNotContain("client-person", report.Content);
        Assert.DoesNotContain("client-laptop", report.Content);
        Assert.DoesNotContain("client-person", report.ReviewSummary);
        Assert.Contains("Schedule work", report.ReviewSummary);
        Assert.Contains("Schedule work", report.Content);
        Assert.Contains("An online calendar", report.Content);
        Assert.Contains("desktop-1", report.Content);
        Assert.Contains("runtime-1", report.Content);
        Assert.Contains("10.0", report.Content);
        Assert.Contains("not a workload benchmark", report.Content);
        Assert.EndsWith(report.Extension, report.FileName);
    }

    [Fact]
    public void Json_keeps_original_machine_and_inventory_contract_with_additive_intake_fields()
    {
        using var json = JsonDocument.Parse(new ReportWriter().WriteJson(Capture()));
        var root = json.RootElement;
        Assert.Equal("2.1", root.GetProperty("SchemaVersion").GetString());
        Assert.Equal("Dell", root.GetProperty("Machine").GetProperty("Manufacturer").GetString());
        Assert.Equal("3.2", root.GetProperty("Inventory").GetProperty("DesktopApplications")[0].GetProperty("Version").GetString());
        Assert.Equal("Schedule work", root.GetProperty("ClientContext").GetProperty("Goal").GetString());
        Assert.NotEmpty(root.GetProperty("WorkflowIntake").GetProperty("Facts").EnumerateArray());
    }

    [Theory]
    [InlineData(IntakeReportFormat.Markdown)]
    [InlineData(IntakeReportFormat.Json)]
    [InlineData(IntakeReportFormat.Html)]
    public void Short_usernames_preserve_specs_and_apps_in_the_review_and_export(IntakeReportFormat format)
    {
        var snapshot = new InventorySnapshot
        {
            Machine = new() { UserName = "a", ComputerName = "pc", Processors = ["AMD Ryzen"], Graphics = ["Radeon"] },
            Inventory = new() { DesktopApplications = [new() { Name = "Slack" }, new() { Name = "gRPC" }] },
            ClientContext = new() { Goal = "A on PC" }
        };

        var report = PreparedIntakeReport.Create(snapshot, format);

        foreach (var value in new[] { "AMD Ryzen", "Radeon", "Slack", "gRPC" })
            Assert.Contains(value, report.Content);
        Assert.Contains("AMD Ryzen", report.ReviewSummary);
        Assert.Contains("Radeon", report.ReviewSummary);
        Assert.Contains("Goal: [user] on [computer]", report.ReviewSummary);
        Assert.DoesNotContain("A on PC", report.Content);
    }

    [Fact]
    public void Reviewed_content_cannot_change_when_later_scan_data_changes()
    {
        var capture = Capture();
        var reviewed = PreparedIntakeReport.Create(capture, IntakeReportFormat.Markdown);
        capture.Inventory.DesktopApplications.Add(new() { Id = "later", Name = "Added after review" });
        capture.Machine.Processors.Add("Changed CPU reading");
        Assert.DoesNotContain("Added after review", reviewed.Content);
        Assert.DoesNotContain("Changed CPU reading", reviewed.Content);
        Assert.DoesNotContain("Changed CPU reading", reviewed.ReviewSummary);
        Assert.Contains("Added after review", PreparedIntakeReport.Create(capture, IntakeReportFormat.Markdown).Content);
    }

    [Fact]
    public void Personal_identifiers_require_explicit_opt_in()
    {
        var report = PreparedIntakeReport.Create(Capture(), IntakeReportFormat.Json, includePersonalIdentifiers: true);
        Assert.True(report.IncludesPersonalIdentifiers);
        Assert.Contains("client-person", report.Content);
        Assert.Contains("client-laptop", report.Content);
        Assert.Contains("client-person", report.ReviewSummary);
    }

    [Fact]
    public void Missing_client_answers_remain_unanswered_and_html_does_not_execute_client_notes()
    {
        var snapshot = new InventorySnapshot { ClientContext = new() { Goal = "<script>alert('x')</script> | goal" } };
        var writer = new ReportWriter();
        var html = writer.WriteHtml(snapshot);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("Not provided", html);
        Assert.Contains("provided by the client, not verified", html);
        Assert.Contains("\\| goal", writer.WriteMarkdown(snapshot));
    }

    [Fact]
    public void Redaction_preserves_json_property_names_and_handles_escaped_identifiers()
    {
        var snapshot = new InventorySnapshot
        {
            Machine = new() { UserName = "Name", ComputerName = "Client<Device>" },
            ClientContext = new() { Goal = "Use Client<Device> for work" }
        };
        var writer = new ReportWriter();
        using var json = JsonDocument.Parse(writer.WriteJson(snapshot));
        Assert.Equal("[user]", json.RootElement.GetProperty("Machine").GetProperty("UserName").GetString());
        Assert.Equal("[computer]", json.RootElement.GetProperty("Machine").GetProperty("ComputerName").GetString());
        Assert.Equal("Use [computer] for work", json.RootElement.GetProperty("ClientContext").GetProperty("Goal").GetString());
        Assert.DoesNotContain("Client&lt;Device&gt;", writer.WriteHtml(snapshot));
        Assert.DoesNotContain("Client&lt;Device&gt;", writer.WriteMarkdown(snapshot));
    }

    [Fact]
    public void Markdown_keeps_client_links_and_images_as_literal_text()
    {
        var snapshot = new InventorySnapshot
        {
            ClientContext = new() { Goal = "![image](https://example.invalid/image) [link](https://example.invalid) **important**" }
        };
        var markdown = new ReportWriter().WriteMarkdown(snapshot);
        Assert.Contains("!\\[image\\]", markdown);
        Assert.Contains("\\[link\\]", markdown);
        Assert.Contains("\\*\\*important\\*\\*", markdown);
        Assert.DoesNotContain("![image]", markdown);
    }
}
