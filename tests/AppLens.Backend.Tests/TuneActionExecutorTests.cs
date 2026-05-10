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
    public async Task Admin_required_service_action_is_blocked_when_runtime_is_not_elevated()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: false);
        var item = new TunePlanItem
        {
            Id = "service-asus",
            RequiresAdmin = true,
            VerificationStep = "Re-scan service state.",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.SetServiceManual,
                ExecutionState = TunePlanExecutionState.RequiresAdmin,
                Target = "ASUSSoftwareManager"
            }
        };

        var result = await new TuneActionExecutor(runtime).ExecuteAsync(item, userApproved: true);

        Assert.Equal(TuneActionStatus.Blocked, result.Status);
        Assert.True(result.RequiresAdmin);
        Assert.False(runtime.SetServiceManualCalled);
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
    public async Task Disable_startup_entry_calls_runtime_with_entry_name_and_location()
    {
        var runtime = new FakeTuneActionRuntime(isAdministrator: false);
        var item = new TunePlanItem
        {
            Id = "startup-docker",
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

    private sealed class FakeTuneActionRuntime(bool isAdministrator) : ITuneActionRuntime
    {
        public bool IsAdministrator { get; } = isAdministrator;

        public bool SetServiceManualCalled { get; private set; }

        public string DisabledStartupName { get; private set; } = "";

        public string DisabledStartupLocation { get; private set; } = "";

        public string EnabledStartupName { get; private set; } = "";

        public string EnabledStartupLocation { get; private set; } = "";

        public Task<long> ClearDirectoryContentsAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(0L);

        public Task SetServiceStartModeManualAsync(string serviceName, CancellationToken cancellationToken = default)
        {
            SetServiceManualCalled = true;
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
    }
}
