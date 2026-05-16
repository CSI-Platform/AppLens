using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppLens.Backend;

public interface IAuditSnapshotStore
{
    Task SaveAsync(AuditSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<AuditSnapshot?> LoadLatestAsync(CancellationToken cancellationToken = default);
}

public sealed class AuditSnapshotStore : IAuditSnapshotStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AppLensRuntimeStorage _storage;

    public AuditSnapshotStore(AppLensRuntimeStorage storage)
    {
        _storage = storage;
    }

    public async Task SaveAsync(AuditSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Directory.CreateDirectory(_storage.SnapshotsDirectory);

        var json = JsonSerializer.Serialize(snapshot, SerializerOptions);

        var stamp = snapshot.GeneratedAt.ToUniversalTime().ToString("yyyyMMdd-HHmmss");
        var archivePath = Path.Combine(_storage.SnapshotsDirectory, $"audit-{stamp}-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(archivePath, json, cancellationToken).ConfigureAwait(false);

        var tempPath = Path.Combine(_storage.SnapshotsDirectory, $"latest-{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, _storage.LatestSnapshotJson, overwrite: true);
    }

    public async Task<AuditSnapshot?> LoadLatestAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_storage.LatestSnapshotJson))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(_storage.LatestSnapshotJson, cancellationToken).ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize<AuditSnapshot>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Latest audit snapshot could not be deserialized.", exception);
        }
    }
}

