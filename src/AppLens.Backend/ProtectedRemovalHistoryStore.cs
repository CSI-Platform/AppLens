using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AppLens.Backend;

/// <summary>Current-user Windows protection for v1 history; legacy evidence is read-only.</summary>
public sealed class ProtectedRemovalHistoryStore(AppLensRuntimeStorage storage) : IBlackboardStore
{
    private const string Module = "inventory-removal";
    private static readonly byte[] Purpose = Encoding.UTF8.GetBytes("AppLens.RemovalHistory.v1");
    private static readonly JsonSerializerOptions EventOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };
    private sealed record Envelope(int Version, string Payload);

    public async Task AppendAsync(BlackboardEvent evt, CancellationToken cancellationToken = default)
    {
        if (evt.ModuleId != Module) throw new ArgumentException("Only removal history belongs in this store.", nameof(evt));
        cancellationToken.ThrowIfCancellationRequested();
        var plaintext = Encoding.UTF8.GetBytes(BlackboardStore.SerializeEvent(evt));
        byte[] ciphertext;
        try { ciphertext = ProtectedData.Protect(plaintext, Purpose, DataProtectionScope.CurrentUser); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
        var line = JsonSerializer.Serialize(new Envelope(1, Convert.ToBase64String(ciphertext)));

        Directory.CreateDirectory(Path.GetDirectoryName(storage.ProtectedRemovalHistory)!);
        // Refuse a concurrent writer and validate the existing log before extending it.
        await using var file = new FileStream(storage.ProtectedRemovalHistory, FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.Asynchronous);
        using var reader = new StreamReader(file, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var previous = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        Decode(previous);
        file.Position = file.Length;
        var separator = previous.Length > 0 && !previous.EndsWith('\n') ? Environment.NewLine : "";
        await file.WriteAsync(Encoding.UTF8.GetBytes(separator + line + Environment.NewLine), cancellationToken).ConfigureAwait(false);
        // RemovalService starts the uninstaller only after this durable approval write succeeds.
        file.Flush(flushToDisk: true);
    }

    public async Task<List<BlackboardEvent>> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var events = new List<BlackboardEvent>();
        if (File.Exists(storage.ProtectedRemovalHistory))
        {
            await using var file = new FileStream(storage.ProtectedRemovalHistory, FileMode.Open,
                FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous);
            using var reader = new StreamReader(file);
            events.AddRange(Decode(await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false)));
        }

        // Pre-release developer logs/indexes are never migrated, appended to or deleted here.
        var legacy = await new BlackboardStore(storage).QueryAsync(
            new BlackboardEventQuery { ModuleId = Module }, cancellationToken).ConfigureAwait(false);
        events.InsertRange(0, legacy);
        return events;
    }

    public async Task<List<BlackboardEvent>> QueryAsync(BlackboardEventQuery query, CancellationToken cancellationToken = default) =>
        BlackboardStore.FilterEvents(await ReadAllAsync(cancellationToken).ConfigureAwait(false), query);

    // Keep the shared interface's logical count without creating a plaintext SQLite cache.
    public async Task<int> GetIndexedEventCountAsync(CancellationToken cancellationToken = default) =>
        (await ReadAllAsync(cancellationToken).ConfigureAwait(false)).Select(evt => evt.EventId).Distinct().Count();

    private static List<BlackboardEvent> Decode(string content)
    {
        var events = new List<BlackboardEvent>();
        foreach (var line in content.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var envelope = JsonSerializer.Deserialize<Envelope>(line);
                if (envelope is null || envelope.Version != 1 || string.IsNullOrEmpty(envelope.Payload))
                    throw new InvalidDataException("Unknown protected history format.");
                var plaintext = ProtectedData.Unprotect(Convert.FromBase64String(envelope.Payload), Purpose, DataProtectionScope.CurrentUser);
                try
                {
                    var evt = JsonSerializer.Deserialize<BlackboardEvent>(plaintext, EventOptions);
                    if (evt is null || evt.ModuleId != Module || string.IsNullOrWhiteSpace(evt.EventId))
                        throw new InvalidDataException("Invalid protected history record.");
                    events.Add(evt);
                }
                finally { CryptographicOperations.ZeroMemory(plaintext); }
            }
            catch (Exception ex) when (ex is JsonException or FormatException or CryptographicException or InvalidDataException)
            {
                throw new InvalidDataException(
                    "Protected action history is damaged or unavailable to this Windows user. Keep the history file and restore it with the original Windows profile before recording more actions.", ex);
            }
        }
        return events;
    }
}
