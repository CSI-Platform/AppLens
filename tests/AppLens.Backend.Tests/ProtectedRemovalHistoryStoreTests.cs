using System.Text;
using System.Text.Json;

namespace AppLens.Backend.Tests;

public sealed class ProtectedRemovalHistoryStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "AppLens-ProtectedHistoryTests", Guid.NewGuid().ToString("N"));
    private AppLensRuntimeStorage Storage => AppLensRuntimeStorage.FromRoot(_root);
    private string ProtectedPath => Path.Combine(_root, "history", "events.dpapi-v1.jsonl");
    private IBlackboardStore CreateStore() => new ProtectedRemovalHistoryStore(Storage);

    [Fact]
    public async Task New_history_round_trips_without_plaintext_or_a_secondary_index()
    {
        var evt = SampleEvent();
        await CreateStore().AppendAsync(evt);

        var restored = Assert.Single(await CreateStore().ReadAllAsync());
        Assert.Equal(BlackboardStore.SerializeEvent(evt), BlackboardStore.SerializeEvent(restored));
        foreach (var file in Directory.GetFiles(_root, "*", SearchOption.AllDirectories))
        {
            var bytes = await File.ReadAllBytesAsync(file);
            Assert.DoesNotContain("private-fixture", Encoding.UTF8.GetString(bytes));
            Assert.DoesNotContain("private-fixture", Encoding.Unicode.GetString(bytes));
        }
        Assert.False(File.Exists(Storage.IndexSqlite));
        Assert.Single(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Legacy_history_is_read_without_rewriting_log_or_index()
    {
        var legacy = new BlackboardStore(Storage);
        await legacy.AppendAsync(SampleEvent("old-event"));
        var logBefore = await File.ReadAllBytesAsync(Storage.EventsJsonl);
        var indexBefore = await File.ReadAllBytesAsync(Storage.IndexSqlite);

        await CreateStore().AppendAsync(SampleEvent("new-event"));
        Assert.Equal(2, (await CreateStore().ReadAllAsync()).Count);
        Assert.Equal(logBefore, await File.ReadAllBytesAsync(Storage.EventsJsonl));
        Assert.Equal(indexBefore, await File.ReadAllBytesAsync(Storage.IndexSqlite));
        Assert.DoesNotContain("private-fixture", await File.ReadAllTextAsync(ProtectedPath));
    }

    [Fact]
    public async Task Query_filters_decrypted_history_and_preserves_latest_completed_removal()
    {
        var when = DateTimeOffset.UtcNow;
        var record = new RemovalRecord { Id = "private-fixture-action", AppId = "private-fixture-app", AppName = "private-fixture-name",
            StartedAt = when, Outcome = "Approved" };
        await CreateStore().AppendAsync(new BlackboardEvent { ModuleId = "inventory-removal", CorrelationId = record.Id,
            EventType = BlackboardEventType.ActionApproved, CreatedAt = when,
            Payload = new() { ["removal_record"] = JsonSerializer.Serialize(record) } });
        record = record with { CompletedAt = when.AddSeconds(1), Outcome = "Removed", StillInstalled = false };
        await CreateStore().AppendAsync(new BlackboardEvent { ModuleId = "inventory-removal", CorrelationId = record.Id,
            EventType = BlackboardEventType.ActionExecuted, CreatedAt = when.AddSeconds(1),
            Payload = new() { ["removal_record"] = JsonSerializer.Serialize(record) } });

        var latest = Assert.Single(await CreateStore().QueryAsync(new BlackboardEventQuery
            { ModuleId = "inventory-removal", CorrelationId = record.Id, Limit = 1 }));
        Assert.Equal(BlackboardEventType.ActionExecuted, latest.EventType);
        var service = new RemovalService(new WindowsRemovalPlatform(), CreateStore());
        var restored = Assert.Single(await service.ReadHistoryAsync());
        Assert.Equal("Removed", restored.Outcome);
        Assert.False(restored.StillInstalled);
        Assert.Equal(2, await CreateStore().GetIndexedEventCountAsync());
        Assert.Empty(await CreateStore().QueryAsync(new BlackboardEventQuery { AppId = "different" }));
    }

    [Theory]
    [InlineData("{not json")]
    [InlineData("{\"Version\":2,\"Payload\":\"AA==\"}")]
    [InlineData("{\"Version\":1,\"Payload\":\"AA==\"}")]
    public async Task Unreadable_protected_history_is_reported_and_not_appended_to(string damagedLine)
    {
        await CreateStore().AppendAsync(SampleEvent());
        await File.AppendAllTextAsync(ProtectedPath, damagedLine + Environment.NewLine);
        var before = await File.ReadAllBytesAsync(ProtectedPath);

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateStore().ReadAllAsync());
        await Assert.ThrowsAsync<InvalidDataException>(() => CreateStore().AppendAsync(SampleEvent("second")));
        Assert.Equal(before, await File.ReadAllBytesAsync(ProtectedPath));
    }

    [Fact]
    public async Task Tampered_ciphertext_is_not_treated_as_empty_history()
    {
        await CreateStore().AppendAsync(SampleEvent());
        var envelope = JsonSerializer.Deserialize<Envelope>(await File.ReadAllTextAsync(ProtectedPath))!;
        var protectedBytes = Convert.FromBase64String(envelope.Payload);
        protectedBytes[^1] ^= 0x40;
        await File.WriteAllTextAsync(ProtectedPath, JsonSerializer.Serialize(envelope with { Payload = Convert.ToBase64String(protectedBytes) }));
        await Assert.ThrowsAsync<InvalidDataException>(() => CreateStore().ReadAllAsync());
    }

    [Fact]
    public async Task A_valid_last_record_without_newline_survives_the_next_append()
    {
        await CreateStore().AppendAsync(SampleEvent());
        await File.WriteAllTextAsync(ProtectedPath, (await File.ReadAllTextAsync(ProtectedPath)).TrimEnd());
        await CreateStore().AppendAsync(SampleEvent("second"));
        Assert.Equal(2, (await CreateStore().ReadAllAsync()).Count);
    }

    [Fact]
    public async Task Unreadable_history_prevents_an_approved_uninstall_from_starting()
    {
        await CreateStore().AppendAsync(SampleEvent());
        await File.AppendAllTextAsync(ProtectedPath, "{damaged" + Environment.NewLine);
        var platform = new FixturePlatform();
        var service = new RemovalService(platform, CreateStore());
        var plan = await service.PrepareAsync("private-fixture-app");

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ExecuteAsync(plan.Token, approved: true));
        Assert.Equal(0, platform.Executions);
    }

    [Fact]
    public async Task Approved_action_and_report_survive_reopening_the_protected_store()
    {
        var platform = new FixturePlatform();
        var service = new RemovalService(platform, CreateStore());
        var result = await service.ExecuteAsync((await service.PrepareAsync("private-fixture-app")).Token, approved: true);
        Assert.Equal(1, platform.Executions);
        Assert.Equal("Removed", result.Outcome);

        var history = await new RemovalService(platform, CreateStore()).ReadHistoryAsync();
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(Assert.Single(history)));
        Assert.Contains("Removed", new ReportWriter().WriteJson(new InventorySnapshot { Actions = history }));
        Assert.DoesNotContain("private-fixture", await File.ReadAllTextAsync(ProtectedPath));
    }

    private sealed class FixturePlatform : IRemovalPlatform
    {
        public int Executions;
        public Task<AppEntry?> ReadAppAsync(string id) => Task.FromResult<AppEntry?>(new AppEntry
        {
            Id = id, Name = "private-fixture-name", Version = "1", Scope = InstallationScope.ThisUser,
            Kind = ApplicationKind.Desktop, CandidateRoute = RemovalRoute.Msi,
            ProductCode = "{0CC8958B-BBE6-47A4-BB56-B70FE34AE90C}"
        });
        public Task<List<DiskSnapshot>> ReadDisksAsync() => Task.FromResult(new List<DiskSnapshot>());
        public Task<RemovalExecution> ExecuteAsync(AppEntry app, RemovalCommand command, bool administrator)
        {
            Executions++;
            return Task.FromResult(new RemovalExecution("Completed", "Owned simulated fixture removed."));
        }
        public Task<bool?> IsInstalledAsync(AppEntry app) => Task.FromResult<bool?>(false);
    }

    private sealed record Envelope(int Version, string Payload);
    private static BlackboardEvent SampleEvent(string id = "private-fixture-event") => new()
    {
        EventId = id, ModuleId = "inventory-removal", EventType = BlackboardEventType.ActionExecuted,
        AppId = "private-fixture-app", CorrelationId = "private-fixture-correlation",
        Summary = "private-fixture-name: Removed", Payload = new() { ["detail"] = "C:\\Users\\private-fixture-user\\AppData" }
    };

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
