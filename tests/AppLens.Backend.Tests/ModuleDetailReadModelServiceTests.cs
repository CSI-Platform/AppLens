namespace AppLens.Backend.Tests;

public sealed class ModuleDetailReadModelServiceTests : IDisposable
{
    private readonly string _root;
    private readonly AppLensRuntimeStorage _storage;
    private readonly BlackboardStore _store;

    public ModuleDetailReadModelServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "AppLens-ModuleDetailReadModelTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _storage = AppLensRuntimeStorage.FromRoot(Path.Combine(_root, "runtime"));
        _store = new BlackboardStore(_storage);
    }

    [Fact]
    public async Task Unknown_module_returns_null()
    {
        var service = CreateService();

        var result = await service.GetModuleDetailAsync("missing-module");

        Assert.Null(result);
    }

    [Fact]
    public async Task Module_detail_includes_health_storage_and_recent_events()
    {
        var llmRoot = Path.Combine(_root, "AppLens-LLM");
        Directory.CreateDirectory(Path.Combine(llmRoot, "src", "applens_llm"));
        Directory.CreateDirectory(Path.Combine(llmRoot, "out"));
        File.WriteAllText(Path.Combine(llmRoot, "pyproject.toml"), "[project]\nname='applens-llm'\n");
        File.WriteAllText(Path.Combine(llmRoot, "src", "applens_llm", "cli.py"), "# fake cli");

        await _store.AppendAsync(new BlackboardEvent
        {
            EventId = "evt-old",
            EventType = BlackboardEventType.ScanCompleted,
            ModuleId = "llm",
            CorrelationId = "corr-module",
            CreatedAt = new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero),
            Summary = "Scan completed."
        });
        await _store.AppendAsync(new BlackboardEvent
        {
            EventId = "evt-new",
            EventType = BlackboardEventType.VerificationRecorded,
            ModuleId = "llm",
            CorrelationId = "corr-module",
            CreatedAt = new DateTimeOffset(2026, 5, 10, 12, 5, 0, TimeSpan.Zero),
            Summary = "Verification recorded."
        });
        await _store.AppendAsync(new BlackboardEvent
        {
            EventId = "evt-other",
            EventType = BlackboardEventType.ScanCompleted,
            ModuleId = "oracle",
            CorrelationId = "corr-other",
            CreatedAt = new DateTimeOffset(2026, 5, 10, 12, 10, 0, TimeSpan.Zero),
            Summary = "Other module scan."
        });

        var service = CreateService(new ModuleStatusPaths
        {
            AppLensLlmRoot = llmRoot,
            OracleRoot = Path.Combine(_root, "missing-oracle"),
            MailboxRoot = Path.Combine(_root, "missing-mailbox"),
            AppLensZeroRoot = Path.Combine(_root, "missing-zero")
        });

        var detail = await service.GetModuleDetailAsync("llm", recentEventLimit: 2);

        Assert.NotNull(detail);
        Assert.Equal("llm", detail!.ModuleId);
        Assert.Equal(ModuleAvailability.Available, detail.Availability);
        Assert.Equal("Available", detail.StatusLabel);
        Assert.Equal("AppLens-LLM", detail.DisplayName);
        Assert.Contains(detail.HealthChecks, check => check.Name == "package" && check.State == ModuleHealthState.Ok);
        Assert.Contains(detail.HealthChecks, check => check.Name == "cli" && check.State == ModuleHealthState.Ok);
        Assert.Contains(detail.StorageRoots, root => root.Name == "repository" && root.Exists);
        Assert.Contains(detail.StorageRoots, root => root.Name == "runtime-output" && root.Exists);
        Assert.Equal(["evt-new", "evt-old"], detail.RecentLedgerEvents.Select(evt => evt.EventId).ToArray());
        Assert.All(detail.RecentLedgerEvents, evt => Assert.Equal("corr-module", evt.CorrelationId));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private ModuleDetailReadModelService CreateService(ModuleStatusPaths? paths = null) =>
        new(
            new ModuleStatusService(paths ?? new ModuleStatusPaths
            {
                AppLensLlmRoot = Path.Combine(_root, "missing-llm"),
                OracleRoot = Path.Combine(_root, "missing-oracle"),
                MailboxRoot = Path.Combine(_root, "missing-mailbox"),
                AppLensZeroRoot = Path.Combine(_root, "missing-zero")
            }),
            _store);
}

