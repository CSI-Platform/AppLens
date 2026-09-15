namespace AppLens.Backend.Tests;

public sealed class TuneActionExecutorTests : IDisposable
{
    private readonly string _root;

    public TuneActionExecutorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "AppLens-Tune-ActionTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task Clear_rebuildable_cache_deletes_contents_and_preserves_root()
    {
        var childDirectory = Path.Combine(_root, "child");
        Directory.CreateDirectory(childDirectory);
        await File.WriteAllTextAsync(Path.Combine(_root, "cache.tmp"), "cache");
        await File.WriteAllTextAsync(Path.Combine(childDirectory, "nested.tmp"), "cache");

        var item = CacheCleanupItem(_root);
        var result = await new TuneActionExecutor().ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Succeeded, result.Status);
        Assert.True(Directory.Exists(_root));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
        Assert.Equal(item.Id, result.PlanItemId);
        Assert.Equal(ProposedActionKind.ClearRebuildableCache, result.Kind);
    }

    [Fact]
    public async Task Clear_rebuildable_cache_without_user_consent_is_blocked()
    {
        var file = Path.Combine(_root, "cache.tmp");
        await File.WriteAllTextAsync(file, "cache");

        var result = await new TuneActionExecutor().ExecuteAsync(CacheCleanupItem(_root), userApproved: false);

        Assert.Equal(TuneActionStatus.Blocked, result.Status);
        Assert.True(File.Exists(file));
        Assert.Contains("consent", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cache_action_with_unknown_target_is_blocked_before_runtime_call()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: true);
        var outsideCache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppLensNotCache");
        var result = await new TuneActionExecutor(runtime)
            .ExecuteAsync(CacheCleanupItem(outsideCache), userApproved: true);

        Assert.Equal(TuneActionStatus.Blocked, result.Status);
        Assert.Contains("cache", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("allowlist", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(runtime.ClearDirectoryCalled);
    }

    [Fact]
    public async Task Admin_required_service_action_is_blocked_when_runtime_is_not_elevated()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: false);
        var item = new TunePlanItem
        {
            Id = "service-asus",
            RequiresAdmin = true,
            BackupPlan = "Record the current service start mode before changing it.",
            VerificationStep = "Re-scan service state.",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.SetServiceManual,
                ExecutionState = TunePlanExecutionState.RequiresAdmin,
                Target = "ASUSSoftwareManager",
                TargetContext = "ASUS Software Manager"
            }
        };

        var result = await new TuneActionExecutor(runtime).ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Blocked, result.Status);
        Assert.True(result.RequiresAdmin);
        Assert.False(runtime.SetServiceManualCalled);
        Assert.Contains("elevated", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Record the current service start mode", result.BackupDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Service_action_requires_elevation_even_if_item_is_malformed_as_user_consent()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: false);
        var item = ServiceItem(
            "ASUSSoftwareManager",
            "ASUS Software Manager",
            TunePlanExecutionState.RequiresUserConsent,
            requiresAdmin: false);

        var result = await new TuneActionExecutor(runtime).ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Blocked, result.Status);
        Assert.Contains("elevated", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(runtime.SetServiceManualCalled);
    }

    [Fact]
    public async Task Allowlisted_service_action_runs_when_elevated()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: true);
        var item = ServiceItem(
            "ASUSSoftwareManager",
            "ASUS Software Manager",
            TunePlanExecutionState.RequiresAdmin,
            requiresAdmin: true);

        var result = await new TuneActionExecutor(runtime).ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Succeeded, result.Status);
        Assert.True(runtime.SetServiceManualCalled);
        Assert.Equal("ASUSSoftwareManager", runtime.ServiceName);
    }

    [Fact]
    public async Task Unsupported_action_is_blocked()
    {
        var item = new TunePlanItem
        {
            Id = "move-repo",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.MoveRepo,
                ExecutionState = TunePlanExecutionState.ReadOnlyOnly,
                Target = @"C:\Users\codyl\OneDrive\Documents"
            }
        };

        var result = await new TuneActionExecutor(new FakeTuneActionRuntime(isAdministrator: true))
            .ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Blocked, result.Status);
        Assert.Contains("not executable", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Run_local_ai_benchmark_ready_to_run_is_blocked_by_executor_allowlist()
    {
        var item = new TunePlanItem
        {
            Id = "local-ai-benchmark",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.RunLocalAiBenchmark,
                ExecutionState = TunePlanExecutionState.ReadyToRun,
                Target = "local runtime"
            }
        };

        var result = await new TuneActionExecutor(new FakeTuneActionRuntime(isAdministrator: true))
            .ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Blocked, result.Status);
        Assert.Contains("not executable", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Local_llm_health_check_calls_runtime_for_loopback_endpoint()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: false);
        var item = LocalLlmItem(
            ProposedActionKind.CheckLocalLlmHealth,
            "http://127.0.0.1:18080/health",
            "local-llm:health");

        var result = await new TuneActionExecutor(runtime).ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Succeeded, result.Status);
        Assert.Equal("http://127.0.0.1:18080/health", runtime.LocalLlmHealthEndpoint);
        Assert.Contains("ok", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Local_llm_health_check_blocks_non_loopback_endpoint()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: false);
        var item = LocalLlmItem(
            ProposedActionKind.CheckLocalLlmHealth,
            "http://example.com/health",
            "local-llm:health");

        var result = await new TuneActionExecutor(runtime).ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Blocked, result.Status);
        Assert.Contains("loopback", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("", runtime.LocalLlmHealthEndpoint);
    }

    [Fact]
    public async Task Windows_runtime_local_llm_health_fails_non_success_status()
    {
        var runtime = new WindowsTuneActionRuntime(new StaticHttpMessageHandler(
            new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = "Unhealthy"
            }));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            runtime.CheckLocalLlmHealthAsync("http://127.0.0.1:18080/health"));

        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    [Fact]
    public async Task Unsupported_runtime_action_is_recorded_as_blocked()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: false)
        {
            LocalLlmStartNotSupported = true
        };
        var item = LocalLlmItem(
            ProposedActionKind.StartLocalLlmServer,
            "Start local LLM lane",
            "local-llm:start");

        var result = await new TuneActionExecutor(runtime).ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Blocked, result.Status);
        Assert.Contains("executor", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Windows_runtime_does_not_report_unregistered_module_actions_as_success()
    {
        var runtime = new WindowsTuneActionRuntime();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            runtime.StartLocalLlmServerAsync("Start local LLM lane"));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            runtime.StopLocalLlmServerAsync("Stop local LLM lane"));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            runtime.TestSshConnectionAsync("local-gpu"));
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            runtime.CheckRemoteLlmHealthAsync("local-gpu"));
    }

    [Fact]
    public async Task Ssh_connection_test_calls_runtime_for_sanitized_alias()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: false);
        var item = SshItem(ProposedActionKind.TestSshConnection, "local-gpu", "ssh:test-connection");

        var result = await new TuneActionExecutor(runtime).ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Succeeded, result.Status);
        Assert.Equal("local-gpu", runtime.SshTargetAlias);
    }

    [Fact]
    public async Task Ssh_connection_test_blocks_raw_user_host_target()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: false);
        var item = SshItem(ProposedActionKind.TestSshConnection, "cody@192.168.68.57", "ssh:test-connection");

        var result = await new TuneActionExecutor(runtime).ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Blocked, result.Status);
        Assert.Contains("alias", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("", runtime.SshTargetAlias);
    }

    [Fact]
    public async Task Disable_startup_entry_calls_runtime_with_entry_name_and_location()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: false);
        var item = new TunePlanItem
        {
            Id = "startup-docker",
            BackupPlan = "Re-enable Docker Desktop startup if the user needs it.",
            VerificationStep = "Re-scan startup state.",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.DisableStartup,
                ExecutionState = TunePlanExecutionState.RequiresUserConsent,
                Target = "Docker Desktop",
                TargetContext = @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
            }
        };

        var result = await new TuneActionExecutor(runtime).ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Succeeded, result.Status);
        Assert.Equal("Docker Desktop", runtime.DisabledStartupName);
        Assert.Equal(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run", runtime.DisabledStartupLocation);
        Assert.Contains("Re-enable Docker Desktop startup", result.BackupDetail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Enable_startup_entry_calls_runtime_with_entry_name_and_location()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: false);
        var item = new TunePlanItem
        {
            Id = "startup-docker-enable",
            VerificationStep = "Re-scan startup state.",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.EnableStartup,
                ExecutionState = TunePlanExecutionState.RequiresUserConsent,
                Target = "Docker Desktop",
                TargetContext = @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
            }
        };

        var result = await new TuneActionExecutor(runtime).ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Succeeded, result.Status);
        Assert.Equal("Docker Desktop", runtime.EnabledStartupName);
        Assert.Equal(@"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run", runtime.EnabledStartupLocation);
    }

    [Fact]
    public async Task Hklm_startup_action_requires_elevation_even_if_item_is_malformed_as_user_consent()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: false);
        var item = new TunePlanItem
        {
            Id = "startup-hklm",
            RequiresAdmin = false,
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.DisableStartup,
                ExecutionState = TunePlanExecutionState.RequiresUserConsent,
                Target = "Docker Desktop",
                TargetContext = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
            }
        };

        var result = await new TuneActionExecutor(runtime).ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Blocked, result.Status);
        Assert.Contains("elevated", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("", runtime.DisabledStartupName);
    }

    [Fact]
    public async Task Startup_action_with_unknown_location_is_blocked_before_runtime_call()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: true);
        var item = new TunePlanItem
        {
            Id = "startup-unknown",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.DisableStartup,
                ExecutionState = TunePlanExecutionState.RequiresUserConsent,
                Target = "Unknown App",
                TargetContext = @"C:\Users\codyl\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup"
            }
        };

        var result = await new TuneActionExecutor(runtime).ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Blocked, result.Status);
        Assert.Contains("startup", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("allowlist", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("", runtime.DisabledStartupName);
    }

    [Fact]
    public async Task Protected_startup_entry_is_blocked_before_runtime_call()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: true);
        var item = new TunePlanItem
        {
            Id = "startup-security-health",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.DisableStartup,
                ExecutionState = TunePlanExecutionState.RequiresUserConsent,
                Target = "SecurityHealth",
                TargetContext = @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
            }
        };

        var result = await new TuneActionExecutor(runtime).ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Blocked, result.Status);
        Assert.Contains("protected", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("", runtime.DisabledStartupName);
    }

    [Fact]
    public async Task Service_action_with_unknown_target_is_blocked_before_runtime_call()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: true);
        var item = new TunePlanItem
        {
            Id = "service-unknown",
            RequiresAdmin = true,
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.SetServiceManual,
                ExecutionState = TunePlanExecutionState.RequiresAdmin,
                Target = "WindowsDefender",
                TargetContext = "Microsoft Defender"
            }
        };

        var result = await new TuneActionExecutor(runtime).ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Blocked, result.Status);
        Assert.Contains("service", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("allowlist", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(runtime.SetServiceManualCalled);
    }

    [Fact]
    public void Action_policy_exposes_auditable_allowlists()
    {
        Assert.Contains(
            @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            TuneActionPolicy.StartupRegistryLocationAllowlist,
            StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Docker", TuneActionPolicy.ServiceAllowlistTokens, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("OneDrive", TuneActionPolicy.ProtectedStartupEntries, StringComparer.OrdinalIgnoreCase);
        Assert.NotEmpty(TuneActionPolicy.ClearableCacheRoots);
    }


    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static TunePlanItem CacheCleanupItem(string path) =>
        new()
        {
            Id = "clear-cache",
            VerificationStep = "Re-scan storage hotspots.",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.ClearRebuildableCache,
                ExecutionState = TunePlanExecutionState.RequiresUserConsent,
                Target = path,
                TargetContext = @"LocalAppData\Temp"
            }
        };

    private static TunePlanItem ServiceItem(
        string serviceName,
        string displayName,
        TunePlanExecutionState executionState,
        bool requiresAdmin) =>
        new()
        {
            Id = $"service-{serviceName}",
            RequiresAdmin = requiresAdmin,
            BackupPlan = "Record the current service start mode before changing it.",
            VerificationStep = "Re-scan service state.",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.SetServiceManual,
                ExecutionState = executionState,
                Target = serviceName,
                TargetContext = displayName
            }
        };

    private static TunePlanItem LocalLlmItem(
        ProposedActionKind kind,
        string target,
        string targetContext) =>
        new()
        {
            Id = $"llm-{kind}",
            BackupPlan = "Record runtime state before changing it.",
            VerificationStep = "Check runtime health.",
            ProposedAction = new ProposedAction
            {
                Kind = kind,
                ExecutionState = TunePlanExecutionState.RequiresUserConsent,
                Target = target,
                TargetContext = targetContext
            }
        };

    private static TunePlanItem SshItem(
        ProposedActionKind kind,
        string target,
        string targetContext) =>
        new()
        {
            Id = $"ssh-{kind}",
            BackupPlan = "SSH checks are bounded to configured aliases.",
            VerificationStep = "Review sanitized command result.",
            ProposedAction = new ProposedAction
            {
                Kind = kind,
                ExecutionState = TunePlanExecutionState.RequiresUserConsent,
                Target = target,
                TargetContext = targetContext
            }
        };

    private sealed class FakeTuneActionRuntime(bool isAdministrator) : ITuneActionRuntime
    {
        public bool IsAdministrator { get; } = isAdministrator;

        public bool LocalLlmStartNotSupported { get; init; }

        public bool SetServiceManualCalled { get; private set; }

        public bool ClearDirectoryCalled { get; private set; }

        public string ServiceName { get; private set; } = "";

        public string LocalLlmHealthEndpoint { get; private set; } = "";

        public string SshTargetAlias { get; private set; } = "";

        public string DisabledStartupName { get; private set; } = "";

        public string DisabledStartupLocation { get; private set; } = "";

        public string EnabledStartupName { get; private set; } = "";

        public string EnabledStartupLocation { get; private set; } = "";

        public Task<long> ClearDirectoryContentsAsync(string path, CancellationToken cancellationToken = default)
        {
            ClearDirectoryCalled = true;
            return Task.FromResult(0L);
        }

        public Task SetServiceStartModeManualAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            SetServiceManualCalled = true;
            ServiceName = serviceName;
            return Task.CompletedTask;
        }

        public Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DisableStartupEntryAsync(string entryName, string location, CancellationToken cancellationToken = default)
        {
            DisabledStartupName = entryName;
            DisabledStartupLocation = location;
            return Task.CompletedTask;
        }

        public Task EnableStartupEntryAsync(string entryName, string location, CancellationToken cancellationToken = default)
        {
            EnabledStartupName = entryName;
            EnabledStartupLocation = location;
            return Task.CompletedTask;
        }

        public Task<string> CheckLocalLlmHealthAsync(string healthEndpoint, CancellationToken cancellationToken = default)
        {
            LocalLlmHealthEndpoint = healthEndpoint;
            return Task.FromResult("Local LLM health check returned ok.");
        }

        public Task<string> StartLocalLlmServerAsync(string commandSummary, CancellationToken cancellationToken = default) =>
            LocalLlmStartNotSupported
                ? Task.FromException<string>(new NotSupportedException("Local LLM runtime executor is not registered."))
                : Task.FromResult("Local LLM server start requested.");

        public Task<string> StopLocalLlmServerAsync(string commandSummary, CancellationToken cancellationToken = default) =>
            Task.FromResult("Local LLM server stop requested.");

        public Task<string> TestSshConnectionAsync(string targetAlias, CancellationToken cancellationToken = default)
        {
            SshTargetAlias = targetAlias;
            return Task.FromResult("SSH connection test completed.");
        }

        public Task<string> CheckRemoteLlmHealthAsync(string targetAlias, CancellationToken cancellationToken = default) =>
            Task.FromResult("Remote LLM health check completed.");
    }

    private sealed class StaticHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(response);
    }
}
