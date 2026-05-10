using Microsoft.Data.Sqlite;

namespace AppLens.Backend.Tests;

public sealed class BlackboardStoreTests : IDisposable
{
    private readonly string _root;
    private readonly AppLensRuntimeStorage _storage;

    public BlackboardStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "AppLens-BlackboardStoreTests", Guid.NewGuid().ToString("N"));
        _storage = AppLensRuntimeStorage.FromRoot(_root);
    }

    [Fact]
    public async Task Append_writes_jsonl_and_read_all_returns_events()
    {
        var store = new BlackboardStore(_storage);
        var evt = SampleEvent("evt-1");

        await store.AppendAsync(evt);
        var events = await store.ReadAllAsync();

        Assert.True(File.Exists(_storage.EventsJsonl));
        Assert.Single(events);
        Assert.Equal("evt-1", events[0].EventId);
        Assert.Equal(BlackboardEventType.ScanCompleted, events[0].EventType);
    }

    [Fact]
    public async Task Read_all_skips_corrupt_jsonl_lines()
    {
        Directory.CreateDirectory(_storage.LedgerDirectory);
        var valid = BlackboardStore.SerializeEvent(SampleEvent("evt-good"));
        await File.WriteAllTextAsync(_storage.EventsJsonl, valid + Environment.NewLine + "{not json" + Environment.NewLine);

        var events = await new BlackboardStore(_storage).ReadAllAsync();

        Assert.Single(events);
        Assert.Equal("evt-good", events[0].EventId);
    }

    [Fact]
    public async Task Append_creates_sqlite_index_with_queryable_event_row()
    {
        var store = new BlackboardStore(_storage);

        await store.AppendAsync(SampleEvent("evt-indexed"));

        Assert.True(File.Exists(_storage.IndexSqlite));
        using var connection = new SqliteConnection($"Data Source={_storage.IndexSqlite};Pooling=False");
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT event_id, event_type, module_id, app_id, data_state, privacy_state, summary
            FROM events
            WHERE event_id = 'evt-indexed'
            """;
        using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("evt-indexed", reader.GetString(0));
        Assert.Equal("ScanCompleted", reader.GetString(1));
        Assert.Equal("report", reader.GetString(2));
        Assert.Equal("applens-desktop", reader.GetString(3));
        Assert.Equal("Validated", reader.GetString(4));
        Assert.Equal("RawPrivate", reader.GetString(5));
        Assert.Equal("sample scan", reader.GetString(6));
    }

    [Fact]
    public async Task Indexed_event_count_reads_from_sqlite_index()
    {
        var store = new BlackboardStore(_storage);

        await store.AppendAsync(SampleEvent("evt-count-1"));
        await store.AppendAsync(SampleEvent("evt-count-2"));

        Assert.Equal(2, await store.GetIndexedEventCountAsync());
    }

    [Fact]
    public async Task Query_filters_events_and_returns_newest_first()
    {
        var store = new BlackboardStore(_storage);
        await store.AppendAsync(SampleEvent(
            "evt-old",
            BlackboardEventType.ActionProposed,
            "tune",
            "corr-platform",
            new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero)));
        await store.AppendAsync(SampleEvent(
            "evt-ignore-type",
            BlackboardEventType.ScanCompleted,
            "tune",
            "corr-platform",
            new DateTimeOffset(2026, 5, 10, 12, 1, 0, TimeSpan.Zero)));
        await store.AppendAsync(SampleEvent(
            "evt-new",
            BlackboardEventType.ActionProposed,
            "tune",
            "corr-platform",
            new DateTimeOffset(2026, 5, 10, 12, 2, 0, TimeSpan.Zero)));
        await store.AppendAsync(SampleEvent(
            "evt-ignore-correlation",
            BlackboardEventType.ActionProposed,
            "tune",
            "corr-other",
            new DateTimeOffset(2026, 5, 10, 12, 3, 0, TimeSpan.Zero)));

        var events = await store.QueryAsync(new BlackboardEventQuery
        {
            EventType = BlackboardEventType.ActionProposed,
            ModuleId = "tune",
            CorrelationId = "corr-platform",
            Limit = 2
        });

        Assert.Collection(
            events,
            evt => Assert.Equal("evt-new", evt.EventId),
            evt => Assert.Equal("evt-old", evt.EventId));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static BlackboardEvent SampleEvent(
        string eventId,
        BlackboardEventType eventType = BlackboardEventType.ScanCompleted,
        string moduleId = "report",
        string correlationId = "corr-1",
        DateTimeOffset? createdAt = null) =>
        new()
        {
            EventId = eventId,
            EventType = eventType,
            ParticipantIdentity = "applens-desktop",
            ParticipantKind = BlackboardParticipantKind.FirstPartyModule,
            ModuleId = moduleId,
            AppId = "applens-desktop",
            ScopeId = "local_workstation",
            CorrelationId = correlationId,
            CreatedAt = createdAt ?? new DateTimeOffset(2026, 5, 10, 12, 0, 0, TimeSpan.Zero),
            LifecycleState = BlackboardLifecycleState.Created,
            DataState = BlackboardDataState.Validated,
            PrivacyState = BlackboardPrivacyState.RawPrivate,
            Summary = "sample scan",
            Payload = new Dictionary<string, string> { ["readiness_score"] = "100" }
        };
}
