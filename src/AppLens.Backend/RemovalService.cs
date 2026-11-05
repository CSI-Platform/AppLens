using System.Security.Cryptography;
using System.Text.Json;

namespace AppLens.Backend;

public interface IRemovalPlatform
{
    Task<AppEntry?> ReadAppAsync(string id);
    Task<List<DiskSnapshot>> ReadDisksAsync();
    Task<RemovalExecution> ExecuteAsync(AppEntry app, RemovalCommand command, bool administrator);
    Task<bool?> IsInstalledAsync(AppEntry app);
    Task<RemovalExecution> StartAsync(AppEntry app, RemovalCommand command, bool administrator,
        CancellationToken token) => ExecuteAsync(app, command, administrator);
    Task<RemovalExecution?> ObserveExecutionAsync(RemovalRecord record, CancellationToken token) =>
        Task.FromResult<RemovalExecution?>(null);
}

public sealed record RemovalExecution(string Outcome, string Detail, bool RestartRequired = false,
    int? ExitCode = null, RemovalProcessIdentity? Process = null, string? OperationId = null,
    RemovalProcessState ProcessState = RemovalProcessState.Unknown);
public sealed record PreparedRemoval(string Token, AppEntry App, RemovalCommand Command, DateTimeOffset PreparedAt,
    bool PreviousUninstallerFinished = false);

public sealed class RemovalService
{
    private readonly IRemovalPlatform _platform;
    private readonly IBlackboardStore _store;
    private readonly TimeProvider _time;
    private readonly Dictionary<string, (PreparedRemoval Plan, string Fingerprint)> _plans = new();
    private readonly Dictionary<string, RemovalRecord> _unsaved = new();
    private readonly SemaphoreSlim _gate = new(1);
    private DateTimeOffset Now => _time.GetUtcNow();
    public event Action<RemovalRecord>? ActionStarted;
    public RemovalService() : this(new WindowsRemovalPlatform(), new ProtectedRemovalHistoryStore(AppLensRuntimeStorage.Default())) { }
    public RemovalService(IRemovalPlatform platform, IBlackboardStore store, TimeProvider? time = null)
    { _platform = platform; _store = store; _time = time ?? TimeProvider.System; }

    public async Task<PreparedRemoval> PrepareAsync(string appId, bool previousUninstallerFinished = false,
        bool reviewInWindows = false, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_unsaved.Count != 0) throw new InvalidOperationException("Action history could not be saved. Check status to retry saving before another uninstall.");
            if (!reviewInWindows) await CheckRetryAsync(appId, await ReadHistoryCoreAsync(), previousUninstallerFinished, cancellationToken);
            var app = await _platform.ReadAppAsync(appId).WaitAsync(cancellationToken) ??
                throw new InvalidOperationException("This installation is no longer available. Run a fresh scan.");
            var command = RemovalCommand.Resolve(app);
            if (command.Route == RemovalRoute.None) throw new InvalidOperationException(app.RemovalReason);
            if (reviewInWindows) command = new(RemovalRoute.WindowsSettings, "", [], "Review the previous uninstaller in Windows");
            var plan = new PreparedRemoval(Guid.NewGuid().ToString("N"), app, command, Now, previousUninstallerFinished);
            _plans[plan.Token] = (plan, await FingerprintAsync(app, command));
            foreach (var old in _plans.Where(p => Now - p.Value.Plan.PreparedAt > TimeSpan.FromMinutes(5)).Select(p => p.Key).ToList()) _plans.Remove(old);
            return plan;
        }
        finally { _gate.Release(); }
    }

    private async Task CheckRetryAsync(string appId, List<RemovalRecord> history, bool confirmed, CancellationToken token)
    {
        // An older pending action must not be hidden by a newer Settings handoff.
        foreach (var previous in history.Where(RemovalTracking.NeedsReview))
        {
            var observed = await ObserveAsync(previous, token);
            if (observed.ProcessState == RemovalProcessState.Running)
                throw new InvalidOperationException("An uninstaller is still running. Complete its Windows or vendor dialog, then check status.");
            if (previous.AppId.Equals(appId, StringComparison.OrdinalIgnoreCase) && !confirmed)
                throw new RemovalRetryRequiredException();
        }
    }

    public async Task<RemovalRecord> ExecuteAsync(string token, bool approved, bool administrator = false,
        CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken)) throw new InvalidOperationException("Another action is being checked. Try again shortly.");
        try
        {
            if (!_plans.Remove(token, out var saved)) throw new InvalidOperationException("Confirmation is missing or already used. Select the app again.");
            var plan = saved.Plan;
            var record = new RemovalRecord { AppId = plan.App.Id, AppName = plan.App.Name, Route = plan.Command.Route,
                Target = plan.App, AppVersion = plan.App.Version, Publisher = plan.App.Publisher, Scope = plan.App.Scope,
                ScopeEvidence = plan.App.ScopeEvidence, StartedAt = Now, AsAdministrator = administrator,
                Outcome = approved ? "Approved" : "Cancelled", Detail = approved ? "Explicit confirmation received; execution has not completed." : "Cancelled before execution." };
            if (!approved) return await FinishAsync(record, new("Cancelled", record.Detail), null);
            if (_unsaved.Count != 0) throw new InvalidOperationException("Action history is not saved. Check status before another uninstall.");
            if (Now - plan.PreparedAt > TimeSpan.FromMinutes(5)) return await FinishAsync(record, new("Blocked", "Confirmation expired. Select the app again."), null);
            if (administrator && (plan.App.Scope != InstallationScope.AllUsers || plan.Command.Route is not (RemovalRoute.Msi or RemovalRoute.Vendor)))
                return await FinishAsync(record, new("Blocked", "Explicit administrator handoff is limited to all-user desktop installations."), null);
            if (plan.Command.Route != RemovalRoute.WindowsSettings)
                await CheckRetryAsync(plan.App.Id, await ReadHistoryCoreAsync(), plan.PreviousUninstallerFinished, cancellationToken);
            var current = await _platform.ReadAppAsync(plan.App.Id).WaitAsync(cancellationToken);
            var freshCommand = current is null ? null : plan.Command.Route == RemovalRoute.WindowsSettings ? plan.Command : RemovalCommand.Resolve(current);
            if (current is null || await FingerprintAsync(current, freshCommand!) != saved.Fingerprint)
                return await FinishAsync(record, new("Blocked", "The installation or uninstaller changed after confirmation. Run a fresh scan."), null);
            record = record with { BeforeDisks = await SafeDisksAsync(), BeforeCapturedAt = Now, Revision = 1 };
            // Mandatory durable approval: no platform operation starts before this succeeds.
            await AppendAsync(record, BlackboardEventType.ActionApproved);
            RemovalExecution result;
            try { result = await _platform.StartAsync(current, plan.Command, administrator, cancellationToken); }
            catch (Exception ex) { result = new("Outcome unknown", "Launch could not be confirmed: " + ex.Message); }
            // Record process/operation identity before probing installation or disk state.
            record = await SaveAsync(ApplyExecution(record, result) with { Revision = 2 });
            ActionStarted?.Invoke(record);
            bool? installed = await PresenceAsync(record, CancellationToken.None);
            record = record with { AfterDisks = await SafeDisksAsync(), AfterCapturedAt = Now };
            return await FinishAsync(record, result, installed);
        }
        finally { _gate.Release(); }
    }

    private RemovalRecord ApplyExecution(RemovalRecord record, RemovalExecution result) => record with
    {
        ExecutionOutcome = result.Outcome, ExecutionDetail = result.Detail, ExitCode = result.ExitCode ?? record.ExitCode,
        RestartRequired = record.RestartRequired || result.RestartRequired, Process = result.Process ?? record.Process,
        OperationId = result.OperationId ?? record.OperationId, ProcessState = result.ProcessState,
        ExecutionCompletedAt = result.Outcome is "Running" or "Still running" or "Outcome unknown" ? record.ExecutionCompletedAt : record.ExecutionCompletedAt ?? Now
    };

    private async Task<RemovalRecord> FinishAsync(RemovalRecord record, RemovalExecution result, bool? installed)
    {
        var outcome = result.Outcome == "Completed" ? installed switch { false => "Removed", true => "Not verified", null => "Verification unavailable" } : result.Outcome;
        record = ApplyExecution(record, result) with { Revision = record.Revision + 1, CompletedAt =
            result.Outcome is "Running" or "Still running" or "Outcome unknown" ? null : Now,
            Outcome = outcome, StillInstalled = installed, Detail = result.Detail, LastCheckedAt = Now,
            VerifiedAt = installed.HasValue ? Now : null, FirstAbsentAt = installed == false ? Now : null, Verification = Verification(installed) };
        return await SaveAsync(record);
    }

    public async Task<List<RemovalRecord>> ReconcileAsync(RemovalCheckMode mode = RemovalCheckMode.Unresolved,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var records = await ReadHistoryCoreAsync();
            var result = new List<RemovalRecord>();
            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_unsaved.ContainsKey(record.Id))
                {
                    result.Add(await SaveAsync(record with { Revision = record.Revision + 1 }));
                    continue;
                }
                var check = mode == RemovalCheckMode.All || (mode == RemovalCheckMode.Due ? RemovalTracking.IsDue(record, Now) : RemovalTracking.NeedsFollowUp(record));
                if (!check) { result.Add(record); continue; }
                var observed = await ObserveAsync(record, cancellationToken);
                var installed = await PresenceAsync(record, cancellationToken);
                observed = observed with { Revision = record.Revision + 1, LastCheckedAt = Now,
                    Verification = Verification(installed), VerifiedAt = installed.HasValue ? Now : record.VerifiedAt,
                    FirstAbsentAt = record.FirstAbsentAt ?? (installed == false ? Now : null) };
                // Outcome, CompletedAt and StillInstalled retain the original observation.
                result.Add(await SaveAsync(observed));
            }
            return result;
        }
        finally { _gate.Release(); }
    }

    private async Task<RemovalRecord> ObserveAsync(RemovalRecord record, CancellationToken token)
    {
        if (record.ExecutionCompletedAt is not null && record.ProcessState != RemovalProcessState.Running) return record;
        try
        {
            var execution = await _platform.ObserveExecutionAsync(record, token).WaitAsync(TimeSpan.FromSeconds(5), token);
            return execution is null ? record with { ProcessState = RemovalProcessState.Unknown } : ApplyExecution(record, execution);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { return record with { ProcessState = RemovalProcessState.Unknown }; }
    }

    private async Task<bool?> PresenceAsync(RemovalRecord record, CancellationToken token)
    {
        var target = RemovalTracking.Target(record);
        if (target is null) return null;
        try { return await _platform.IsInstalledAsync(target).WaitAsync(TimeSpan.FromSeconds(5), token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch { return null; }
    }
    private static RemovalVerification Verification(bool? installed) => installed switch
    { true => RemovalVerification.Present, false => RemovalVerification.Absent, _ => RemovalVerification.Unavailable };
    private async Task<List<DiskSnapshot>> SafeDisksAsync()
    { try { return await _platform.ReadDisksAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch { return []; } }

    private async Task<RemovalRecord> SaveAsync(RemovalRecord record)
    {
        var clean = record with { PersistenceWarning = null };
        try { await AppendAsync(clean, BlackboardEventType.ActionExecuted); _unsaved.Remove(record.Id); return clean; }
        catch (Exception ex)
        {
            var unsaved = record with { PersistenceWarning = "Local history could not be saved: " + ex.Message };
            _unsaved[record.Id] = unsaved;
            return unsaved;
        }
    }
    private Task AppendAsync(RemovalRecord record, BlackboardEventType type) => _store.AppendAsync(new BlackboardEvent
    {
        EventType = type, ModuleId = "inventory-removal", AppId = record.AppId, CorrelationId = record.Id,
        Summary = record.AppName + ": " + record.StatusDisplay,
        Payload = new() { ["removal_record"] = JsonSerializer.Serialize(record) },
        Provenance = new BlackboardProvenance { Source = nameof(RemovalService), Tool = "AppLens" }
    });

    public async Task<List<RemovalRecord>> ReadHistoryAsync()
    { await _gate.WaitAsync(); try { return await ReadHistoryCoreAsync(); } finally { _gate.Release(); } }
    private async Task<List<RemovalRecord>> ReadHistoryCoreAsync()
    {
        var events = await _store.QueryAsync(new BlackboardEventQuery { ModuleId = "inventory-removal" });
        var candidates = new List<(RemovalRecord Record, BlackboardEvent Event)>();
        foreach (var evt in events)
        {
            if (!evt.Payload.TryGetValue("removal_record", out var json)) continue;
            try
            {
                var record = JsonSerializer.Deserialize<RemovalRecord>(json) ??
                    throw new JsonException("An action history record cannot be null.");
                candidates.Add((record.Revision == 0 && record.CompletedAt is null ? record with
                { Outcome = "Outcome unknown", Detail = "Installed closed before completion was recorded. Check status and review the app in Windows." } : record, evt));
            }
            catch (JsonException ex) { throw new InvalidDataException("An action history record is unreadable. Preserve the history file.", ex); }
        }
        var rows = candidates.GroupBy(x => x.Record.Id).Select(g => g.OrderByDescending(x => x.Record.Revision)
            .ThenByDescending(x => x.Event.EventType == BlackboardEventType.ActionExecuted)
            .ThenByDescending(x => x.Event.CreatedAt).ThenByDescending(x => x.Event.EventId, StringComparer.Ordinal).First().Record)
            .ToDictionary(r => r.Id);
        foreach (var entry in _unsaved) rows[entry.Key] = entry.Value;
        return rows.Values.OrderBy(r => r.StartedAt).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
    }
    private static async Task<string> FingerprintAsync(AppEntry app, RemovalCommand command)
    {
        var hash = "";
        if (command.Route == RemovalRoute.Vendor)
        {
            await using var file = new FileStream(command.Executable, FileMode.Open, FileAccess.Read, FileShare.Read);
            hash = Convert.ToHexString(await SHA256.HashDataAsync(file));
        }
        return JsonSerializer.Serialize(new { App = app, app.UninstallCommand, Command = command, ExecutableHash = hash });
    }
}
