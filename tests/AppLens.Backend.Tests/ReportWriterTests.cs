using System.Text.Json;

namespace AppLens.Backend.Tests;

public sealed class ReportWriterTests
{
    [Fact]
    public void Json_export_redacts_user_machine_and_profile_by_default()
    {
        var snapshot = FixtureSnapshot();
        var json = new ReportWriter().WriteJson(snapshot);

        Assert.Contains("[computer]", json);
        Assert.Contains("[user]", json);
        Assert.Contains("%USERPROFILE%", json);
        Assert.DoesNotContain(snapshot.Machine.ComputerName, json);
        Assert.DoesNotContain(snapshot.Machine.UserName, json);
        Assert.DoesNotContain(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), json);
    }

    [Fact]
    public void Raw_json_export_preserves_details_when_requested()
    {
        var snapshot = FixtureSnapshot();
        var json = new ReportWriter().WriteJson(snapshot, includeRawDetails: true);

        Assert.Contains(snapshot.Machine.ComputerName, json);
        Assert.Contains(snapshot.Machine.UserName, json);
    }

    [Fact]
    public void Json_export_matches_contract_shape()
    {
        var snapshot = FixtureSnapshot();
        using var document = JsonDocument.Parse(new ReportWriter().WriteJson(snapshot));
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("SchemaVersion", out _));
        Assert.True(root.TryGetProperty("Machine", out _));
        Assert.True(root.TryGetProperty("Inventory", out _));
        Assert.True(root.TryGetProperty("Tune", out _));
        Assert.True(root.TryGetProperty("Readiness", out _));
        Assert.True(root.TryGetProperty("Findings", out _));
        Assert.True(root.TryGetProperty("TunePlan", out _));
        Assert.True(root.TryGetProperty("ProbeStatuses", out _));
    }

    [Fact]
    public void Markdown_and_html_exports_include_core_sections()
    {
        var snapshot = FixtureSnapshot();
        var writer = new ReportWriter();

        var markdown = writer.WriteMarkdown(snapshot);
        var html = writer.WriteHtml(snapshot);

        Assert.Contains("## Readiness Summary", markdown);
        Assert.Contains("## Findings", markdown);
        Assert.Contains("## Tune Plan", markdown);
        Assert.Contains("## Local AI Readiness", markdown);
        Assert.Contains("InferenceReady", markdown);
        Assert.Contains("## App Inventory", markdown);
        Assert.Contains("## Workstation Diagnostics", markdown);
        Assert.Contains("<h2>Findings</h2>", html);
        Assert.Contains("<h2>Tune Plan</h2>", html);
        Assert.Contains("<h2>Local AI Readiness</h2>", html);
        Assert.Contains("AppLens-desktop", html);
    }

    [Fact]
    public void Html_export_uses_applens_typography_standard()
    {
        var html = new ReportWriter().WriteHtml(FixtureSnapshot());

        Assert.Contains("--font-ui:\"Inter\", \"Segoe UI\", system-ui, sans-serif;", html);
        Assert.Contains("--font-mono:\"JetBrains Mono\", \"Cascadia Mono\", Consolas, monospace;", html);
        Assert.Contains("font-family:var(--font-ui);", html);
    }

    private static AuditSnapshot FixtureSnapshot()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new AuditSnapshot
        {
            GeneratedAt = new DateTimeOffset(2026, 4, 26, 12, 0, 0, TimeSpan.Zero),
            Machine = new MachineSummary
            {
                ComputerName = Environment.MachineName,
                UserName = Environment.UserName,
                OSDescription = "Windows 11",
                TotalMemoryBytes = 32L * 1024 * 1024 * 1024,
                SystemDriveFreeBytes = 90L * 1024 * 1024 * 1024
            },
            Inventory = new InventorySummary
            {
                DesktopApplications =
                [
                    new AppEntry { Name = "Microsoft 365 (Office)", Version = "3 detected apps", Source = "Group" },
                    new AppEntry { Name = "Google Chrome", Version = "1.2.3", Publisher = "Google", Source = "HKLM uninstall" }
                ],
                StoreApplications =
                [
                    new AppEntry { Name = "ChatGPT", Version = "1.0.0.0", Source = "AppX/MSIX" }
                ],
                RuntimesAndFrameworks =
                [
                    new AppEntry { Name = ".NET Runtime", Version = "10.0" }
                ]
            },
            Tune = new TuneSummary
            {
                TopProcesses =
                [
                    new ProcessSnapshot { Name = "AppLens", Id = 10, WorkingSetBytes = 512 * 1024 * 1024, CpuSeconds = 2.5 }
                ],
                StartupEntries =
                [
                    new StartupEntry { Name = "Docker Desktop", State = "Enabled", Location = "HKCU", Command = "Docker Desktop.exe" }
                ],
                StorageHotspots =
                [
                    new StorageHotspot { Location = ".codex", Path = Path.Combine(profile, ".codex"), Bytes = 1024 }
                ],
                RepoPlacements =
                [
                    new RepoPlacement { Root = Path.Combine(profile, "OneDrive", "Documents"), RepoCount = 1, Sample = Path.Combine(profile, "OneDrive", "Documents", "repo") }
                ],
                LocalAiProfile = new LocalAiProfile
                {
                    Readiness = LocalAiReadiness.InferenceReady,
                    WorkloadClass = "Small-model/autoresearch worker",
                    RecommendedRuntime = "llama.cpp CUDA-MMQ with full offload.",
                    TrainingReady = false,
                    TrainingGate = "Training remains gated until PyTorch CUDA passes a smoke test.",
                    Signals =
                    [
                        new LocalAiSignal { Name = "NVIDIA GPU", Status = LocalAiSignalStatus.Present, Detail = "GTX 1660 SUPER" }
                    ]
                }
            },
            Readiness = new ReadinessSummary
            {
                Score = 86,
                Rating = "Ready",
                ReviewCount = 1,
                OptionalCount = 0,
                AdminRequiredCount = 0,
                StartupEnabledCount = 1,
                StartupTotalCount = 1,
                StorageHotspotBytes = 1024,
                Highlights = ["Scanner evidence is local-first; Tune changes require explicit approval and blackboard records."]
            },
            Findings =
            [
                new Finding { Severity = FindingSeverity.Stable, Category = FindingCategory.Privacy, Title = "Read-only audit", Detail = "No changes were made." }
            ],
            TunePlan =
            [
                new TunePlanItem
                {
                    Id = "fixture",
                    Category = TunePlanCategory.Keep,
                    Risk = TunePlanRisk.Info,
                    Title = "Read-only audit",
                    Evidence = "No changes were made.",
                    Guidance = "Keep scanner evidence local-first.",
                    BackupPlan = "No backup needed.",
                    VerificationStep = "Confirm no changes were made.",
                    ProposedAction = new ProposedAction
                    {
                        Kind = ProposedActionKind.None,
                        ExecutionState = TunePlanExecutionState.ReadOnlyOnly,
                        Description = "No action."
                    }
                }
            ],
            ProbeStatuses =
            [
                new ProbeStatus { Name = "Fixture", State = ProbeState.Succeeded, Duration = TimeSpan.FromMilliseconds(10) }
            ]
        };
    }
}
