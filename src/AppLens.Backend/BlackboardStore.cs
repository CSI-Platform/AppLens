using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace AppLens.Backend;

public interface IBlackboardStore
{
    Task AppendAsync(BlackboardEvent evt, CancellationToken cancellationToken = default);

    Task<List<BlackboardEvent>> ReadAllAsync(CancellationToken cancellationToken = default);

    Task<int> GetIndexedEventCountAsync(CancellationToken cancellationToken = default);
}

public sealed class BlackboardStore : IBlackboardStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AppLensRuntimeStorage _storage;

    public BlackboardStore(AppLensRuntimeStorage storage)
    {
        _storage = storage;
    }

    public async Task AppendAsync(BlackboardEvent evt, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_storage.LedgerDirectory);
        await File.AppendAllTextAsync(
            _storage.EventsJsonl,
            SerializeEvent(evt) + Environment.NewLine,
            cancellationToken).ConfigureAwait(false);

        await IndexAsync(evt, cancellationToken).ConfigureAwait(false);
    }

    public async Task<List<BlackboardEvent>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_storage.EventsJsonl))
        {
            return [];
        }

        var events = new List<BlackboardEvent>();
        foreach (var line in await File.ReadAllLinesAsync(_storage.EventsJsonl, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var evt = JsonSerializer.Deserialize<BlackboardEvent>(line, SerializerOptions);
                if (evt is not null)
                {
                    events.Add(evt);
                }
            }
            catch (JsonException)
            {
                // Keep the append log readable even if a partial write or manual edit corrupts one row.
            }
        }

        return events;
    }

    public static string SerializeEvent(BlackboardEvent evt) =>
        JsonSerializer.Serialize(evt, SerializerOptions);

    public async Task<int> GetIndexedEventCountAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_storage.IndexSqlite))
        {
            return 0;
        }

        using var connection = new SqliteConnection($"Data Source={_storage.IndexSqlite};Pooling=False");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM events";
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result);
    }

    private async Task IndexAsync(BlackboardEvent evt, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_storage.LedgerDirectory);
        using var connection = new SqliteConnection($"Data Source={_storage.IndexSqlite};Pooling=False");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO events (
                event_id,
                schema_version,
                event_type,
                participant_identity,
                participant_kind,
                module_id,
                app_id,
                scope_id,
                correlation_id,
                created_at,
                lifecycle_state,
                data_state,
                privacy_state,
                summary
            )
            VALUES (
                $event_id,
                $schema_version,
                $event_type,
                $participant_identity,
                $participant_kind,
                $module_id,
                $app_id,
                $scope_id,
                $correlation_id,
                $created_at,
                $lifecycle_state,
                $data_state,
                $privacy_state,
                $summary
            )
            """;
        command.Parameters.AddWithValue("$event_id", evt.EventId);
        command.Parameters.AddWithValue("$schema_version", evt.SchemaVersion);
        command.Parameters.AddWithValue("$event_type", evt.EventType.ToString());
        command.Parameters.AddWithValue("$participant_identity", evt.ParticipantIdentity);
        command.Parameters.AddWithValue("$participant_kind", evt.ParticipantKind.ToString());
        command.Parameters.AddWithValue("$module_id", evt.ModuleId);
        command.Parameters.AddWithValue("$app_id", evt.AppId);
        command.Parameters.AddWithValue("$scope_id", evt.ScopeId);
        command.Parameters.AddWithValue("$correlation_id", evt.CorrelationId);
        command.Parameters.AddWithValue("$created_at", evt.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$lifecycle_state", evt.LifecycleState.ToString());
        command.Parameters.AddWithValue("$data_state", evt.DataState.ToString());
        command.Parameters.AddWithValue("$privacy_state", evt.PrivacyState.ToString());
        command.Parameters.AddWithValue("$summary", evt.Summary);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS events (
                event_id TEXT PRIMARY KEY,
                schema_version TEXT NOT NULL,
                event_type TEXT NOT NULL,
                participant_identity TEXT NOT NULL,
                participant_kind TEXT NOT NULL,
                module_id TEXT NOT NULL,
                app_id TEXT NOT NULL,
                scope_id TEXT NOT NULL,
                correlation_id TEXT NOT NULL,
                created_at TEXT NOT NULL,
                lifecycle_state TEXT NOT NULL,
                data_state TEXT NOT NULL,
                privacy_state TEXT NOT NULL,
                summary TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_events_module_created ON events(module_id, created_at);
            CREATE INDEX IF NOT EXISTS idx_events_type_created ON events(event_type, created_at);
            CREATE INDEX IF NOT EXISTS idx_events_data_state ON events(data_state);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
