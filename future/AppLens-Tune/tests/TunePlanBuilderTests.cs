namespace AppLens.Backend.Tests;

public sealed class TunePlanBuilderTests
{
    [Fact]
    public void Startup_findings_become_user_consent_actions()
    {
        var snapshot = new AuditSnapshot
        {
            Findings =
            [
                new Finding
                {
                    Severity = FindingSeverity.Review,
                    Category = FindingCategory.Startup,
                    Title = "Docker Desktop starts at sign-in",
                    Detail = "Docker startup should be validated for this user."
                }
            ]
        };

        var plan = new TunePlanBuilder().Build(snapshot);

        var item = Assert.Single(plan);
        Assert.Equal(TunePlanCategory.UserChoice, item.Category);
        Assert.Equal(ProposedActionKind.DisableStartup, item.ProposedAction.Kind);
        Assert.Equal(TunePlanExecutionState.RequiresUserConsent, item.ProposedAction.ExecutionState);
        Assert.False(item.RequiresAdmin);
    }

    [Fact]
    public void Automatic_review_services_are_admin_required_actions()
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

        var plan = new TunePlanBuilder().Build(snapshot);

        var item = Assert.Single(plan);
        Assert.Equal(TunePlanCategory.AdminRequired, item.Category);
        Assert.Equal(ProposedActionKind.SetServiceManual, item.ProposedAction.Kind);
        Assert.Equal(TunePlanExecutionState.RequiresAdmin, item.ProposedAction.ExecutionState);
        Assert.True(item.RequiresAdmin);
    }

    [Fact]
    public void Privacy_finding_keeps_v1_read_only()
    {
        var snapshot = new AuditSnapshot
        {
            Findings =
            [
                new Finding
                {
                    Severity = FindingSeverity.Stable,
                    Category = FindingCategory.Privacy,
                    Title = "Read-only audit",
                    Detail = "No system settings were changed."
                }
            ]
        };

        var plan = new TunePlanBuilder().Build(snapshot);

        var item = Assert.Single(plan);
        Assert.Equal(TunePlanCategory.Keep, item.Category);
        Assert.Equal(TunePlanExecutionState.ReadOnlyOnly, item.ProposedAction.ExecutionState);
        Assert.Equal(ProposedActionKind.None, item.ProposedAction.Kind);
    }

    [Fact]
    public void Local_ai_profile_adds_read_only_autoresearch_guidance()
    {
        var snapshot = new AuditSnapshot
        {
            Tune = new TuneSummary
            {
                LocalAiProfile = new LocalAiProfile
                {
                    Readiness = LocalAiReadiness.InferenceReady,
                    WorkloadClass = "Small-model/autoresearch worker",
                    RecommendedRuntime = "llama.cpp CUDA-MMQ with full offload.",
                    TrainingReady = false,
                    TrainingGate = "Training remains gated until PyTorch CUDA passes a smoke test."
                }
            }
        };

        var plan = new TunePlanBuilder().Build(snapshot);

        var item = Assert.Single(plan, item => item.Title.Contains("autoresearch", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(TunePlanCategory.Review, item.Category);
        Assert.Equal(TunePlanRisk.Low, item.Risk);
        Assert.Equal(ProposedActionKind.RunLocalAiBenchmark, item.ProposedAction.Kind);
        Assert.Equal(TunePlanExecutionState.FutureUserConsent, item.ProposedAction.ExecutionState);
        Assert.Contains("Future action", item.ProposedAction.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("llama.cpp", item.Guidance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Training remains gated", item.VerificationStep, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Configured_local_llm_runtime_adds_health_and_start_actions()
    {
        var snapshot = new AuditSnapshot
        {
            Tune = new TuneSummary
            {
                LocalAiProfile = new LocalAiProfile
                {
                    RuntimeProfile = RuntimeProfile(LocalLlmRuntimeState.Configured)
                }
            }
        };

        var plan = new TunePlanBuilder().Build(snapshot);

        var health = Assert.Single(plan, item => item.ProposedAction.Kind == ProposedActionKind.CheckLocalLlmHealth);
        Assert.Equal(TunePlanExecutionState.RequiresUserConsent, health.ProposedAction.ExecutionState);
        Assert.Equal("http://127.0.0.1:18080/health", health.ProposedAction.Target);
        Assert.Equal("local-llm:health", health.ProposedAction.TargetContext);

        var start = Assert.Single(plan, item => item.ProposedAction.Kind == ProposedActionKind.StartLocalLlmServer);
        Assert.Equal(TunePlanExecutionState.RequiresUserConsent, start.ProposedAction.ExecutionState);
        Assert.Contains("logs", start.BackupPlan, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_model_blocks_local_llm_start_action()
    {
        var snapshot = new AuditSnapshot
        {
            Tune = new TuneSummary
            {
                LocalAiProfile = new LocalAiProfile
                {
                    RuntimeProfile = RuntimeProfile(LocalLlmRuntimeState.MissingModel)
                }
            }
        };

        var plan = new TunePlanBuilder().Build(snapshot);

        var start = Assert.Single(plan, item => item.ProposedAction.Kind == ProposedActionKind.StartLocalLlmServer);
        Assert.Equal(TunePlanExecutionState.Unsupported, start.ProposedAction.ExecutionState);
        Assert.Contains("model", start.Evidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Port_conflict_blocks_local_llm_start_action()
    {
        var snapshot = new AuditSnapshot
        {
            Tune = new TuneSummary
            {
                LocalAiProfile = new LocalAiProfile
                {
                    RuntimeProfile = RuntimeProfile(LocalLlmRuntimeState.PortConflict)
                }
            }
        };

        var plan = new TunePlanBuilder().Build(snapshot);

        var start = Assert.Single(plan, item => item.ProposedAction.Kind == ProposedActionKind.StartLocalLlmServer);
        Assert.Equal(TunePlanExecutionState.Unsupported, start.ProposedAction.ExecutionState);
        Assert.Contains("port", start.Evidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Running_local_llm_runtime_adds_stop_action()
    {
        var snapshot = new AuditSnapshot
        {
            Tune = new TuneSummary
            {
                LocalAiProfile = new LocalAiProfile
                {
                    RuntimeProfile = RuntimeProfile(LocalLlmRuntimeState.Running)
                }
            }
        };

        var plan = new TunePlanBuilder().Build(snapshot);

        var stop = Assert.Single(plan, item => item.ProposedAction.Kind == ProposedActionKind.StopLocalLlmServer);
        Assert.Equal(TunePlanExecutionState.RequiresUserConsent, stop.ProposedAction.ExecutionState);
        Assert.Equal("local-llm:stop", stop.ProposedAction.TargetContext);
    }

    [Fact]
    public void Rebuildable_storage_hotspots_become_user_consent_actions_with_path_targets()
    {
        var cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp",
            "AppLens-Tune-Test");
        var snapshot = new AuditSnapshot
        {
            Tune = new TuneSummary
            {
                StorageHotspots =
                [
                    new StorageHotspot
                    {
                        Location = @"LocalAppData\Temp",
                        Path = cachePath,
                        Bytes = 1024
                    }
                ]
            }
        };

        var plan = new TunePlanBuilder().Build(snapshot);

        var item = Assert.Single(plan, item => item.ProposedAction.Kind == ProposedActionKind.ClearRebuildableCache);
        Assert.Equal(TunePlanExecutionState.RequiresUserConsent, item.ProposedAction.ExecutionState);
        Assert.Equal(cachePath, item.ProposedAction.Target);
    }

    [Fact]
    public void Enabled_startup_entries_become_user_consent_actions_with_entry_targets()
    {
        var snapshot = new AuditSnapshot
        {
            Tune = new TuneSummary
            {
                StartupEntries =
                [
                    new StartupEntry
                    {
                        Name = "Docker Desktop",
                        State = "Enabled",
                        Location = @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                        Command = "Docker Desktop.exe"
                    }
                ]
            }
        };

        var plan = new TunePlanBuilder().Build(snapshot);

        var item = Assert.Single(plan, item => item.Title == "Disable startup entry: Docker Desktop");
        Assert.Equal(ProposedActionKind.DisableStartup, item.ProposedAction.Kind);
        Assert.Equal(TunePlanExecutionState.RequiresUserConsent, item.ProposedAction.ExecutionState);
        Assert.Equal("Docker Desktop", item.ProposedAction.Target);
        Assert.Equal(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run", item.ProposedAction.TargetContext);
    }

    [Fact]
    public void Disabled_startup_entries_become_user_consent_enable_actions_with_entry_targets()
    {
        var snapshot = new AuditSnapshot
        {
            Tune = new TuneSummary
            {
                StartupEntries =
                [
                    new StartupEntry
                    {
                        Name = "Docker Desktop",
                        State = "Disabled",
                        Location = @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
                        Command = "Docker Desktop.exe"
                    }
                ]
            }
        };

        var plan = new TunePlanBuilder().Build(snapshot);

        var item = Assert.Single(plan, item => item.Title == "Enable startup entry: Docker Desktop");
        Assert.Equal(ProposedActionKind.EnableStartup, item.ProposedAction.Kind);
        Assert.Equal(TunePlanExecutionState.RequiresUserConsent, item.ProposedAction.ExecutionState);
        Assert.Equal("Docker Desktop", item.ProposedAction.Target);
        Assert.Equal(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run", item.ProposedAction.TargetContext);
    }

    private static LocalLlmRuntimeProfile RuntimeProfile(LocalLlmRuntimeState state) =>
        new()
        {
            Backend = "llama.cpp",
            ModelPath = @"C:\models\qwen.gguf",
            ModelName = "qwen",
            Port = 18080,
            HealthEndpoint = "http://127.0.0.1:18080/health",
            LogPath = @"C:\logs\llama-server.log",
            StartCommand = "llama-server --model qwen",
            StopCommand = "stop llama-server",
            State = state,
            Detail = state.ToString()
        };
}
