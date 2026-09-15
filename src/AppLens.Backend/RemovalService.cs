using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;

namespace AppLens.Backend;

public interface IRemovalPlatform
{
    Task<AppEntry?> ReadAppAsync(string id);
    Task<List<DiskSnapshot>> ReadDisksAsync();
    Task<RemovalExecution> ExecuteAsync(AppEntry app, RemovalCommand command, bool administrator);
    Task<bool?> IsInstalledAsync(AppEntry app);
}

public sealed record RemovalExecution(string Outcome, string Detail, bool RestartRequired = false);
public sealed record PreparedRemoval(string Token, AppEntry App, RemovalCommand Command, DateTimeOffset PreparedAt);

public sealed class RemovalService
{
    private readonly IRemovalPlatform _platform;
    private readonly IBlackboardStore _store;
    private readonly ConcurrentDictionary<string, (PreparedRemoval Plan, string Fingerprint)> _plans = new();
    private readonly ConcurrentDictionary<string, byte> _pending = new();
    private readonly SemaphoreSlim _execution = new(1);
    public RemovalService() : this(new WindowsRemovalPlatform(), new BlackboardStore(AppLensRuntimeStorage.Default())) { }
    public RemovalService(IRemovalPlatform platform, IBlackboardStore store) { _platform = platform; _store = store; }

    public async Task<PreparedRemoval> PrepareAsync(string appId)
    {
        if (_pending.ContainsKey(appId)) throw new InvalidOperationException("The previous uninstaller may still be running. Review it in Windows before retrying.");
        var app = await _platform.ReadAppAsync(appId) ?? throw new InvalidOperationException("This installation is no longer available. Run a fresh scan.");
        var command = RemovalCommand.Resolve(app);
        if (command.Route == RemovalRoute.None) throw new InvalidOperationException(app.RemovalReason);
        var plan = new PreparedRemoval(Guid.NewGuid().ToString("N"), app, command, DateTimeOffset.Now);
        _plans[plan.Token] = (plan, await FingerprintAsync(app, command));
        foreach (var old in _plans.Where(p => DateTimeOffset.Now - p.Value.Plan.PreparedAt > TimeSpan.FromMinutes(5))) _plans.TryRemove(old.Key, out _);
        return plan;
    }

    public async Task<RemovalRecord> ExecuteAsync(string token, bool approved, bool administrator = false)
    {
        if (!_plans.TryRemove(token, out var saved)) throw new InvalidOperationException("Confirmation is missing or already used. Select the app again.");
        if (!await _execution.WaitAsync(0)) throw new InvalidOperationException("Another uninstall is in progress.");
        var plan = saved.Plan;
        var record = new RemovalRecord { AppId = plan.App.Id, AppName = plan.App.Name, Route = plan.Command.Route,
            AppVersion = plan.App.Version, Publisher = plan.App.Publisher, Scope = plan.App.Scope, ScopeEvidence = plan.App.ScopeEvidence,
            StartedAt = DateTimeOffset.Now, AsAdministrator = administrator, Outcome = approved ? "Approved" : "Cancelled",
            Detail = approved ? "Explicit confirmation received; execution has not completed." : "Cancelled before execution." };
        try
        {
            if (!approved) return await FinishAsync(record, new("Cancelled", record.Detail), null);
            if (DateTimeOffset.Now - plan.PreparedAt > TimeSpan.FromMinutes(5))
                return await FinishAsync(record, new("Blocked", "Confirmation expired. Select the app again."), null);
            if (administrator && (plan.App.Scope != InstallationScope.AllUsers || plan.Command.Route is not (RemovalRoute.Msi or RemovalRoute.Vendor)))
                return await FinishAsync(record, new("Blocked", "Explicit administrator handoff is limited to all-user desktop installations."), null);
            var current = await _platform.ReadAppAsync(plan.App.Id);
            if (current is null || await FingerprintAsync(current, RemovalCommand.Resolve(current)) != saved.Fingerprint)
                return await FinishAsync(record, new("Blocked", "The installation or uninstaller changed after confirmation. Run a fresh scan."), null);
            record = record with { BeforeDisks = await SafeDisksAsync(), BeforeCapturedAt = DateTimeOffset.Now };
            // Failure to persist approval prevents execution. The pending record survives app closure.
            await AppendAsync(record, BlackboardEventType.ActionApproved);
            RemovalExecution result;
            try { result = await _platform.ExecuteAsync(current, plan.Command, administrator); }
            catch (Exception ex) { result = new("Failed", ex.Message); }
            if (result.Outcome == "Still running") _pending.TryAdd(current.Id, 0);
            bool? installed;
            try { installed = await _platform.IsInstalledAsync(current); } catch { installed = null; }
            record = record with { AfterDisks = await SafeDisksAsync(), AfterCapturedAt = DateTimeOffset.Now };
            return await FinishAsync(record, result, installed);
        }
        finally { _execution.Release(); }
    }

    private async Task<List<DiskSnapshot>> SafeDisksAsync()
    {
        try { return await _platform.ReadDisksAsync(); } catch { return []; }
    }

    private async Task<RemovalRecord> FinishAsync(RemovalRecord record, RemovalExecution result, bool? installed)
    {
        var outcome = result.Outcome == "Completed" ? installed switch { false => "Removed", true => "Not verified", null => "Verification unavailable" } : result.Outcome;
        record = record with { CompletedAt = DateTimeOffset.Now, Outcome = outcome, StillInstalled = installed,
            RestartRequired = result.RestartRequired, Detail = result.Detail +
                (result.Outcome == "Completed" && installed != false ? " Removal was not confirmed; the uninstaller may still be working." : "") };
        try { await AppendAsync(record, BlackboardEventType.ActionExecuted); }
        catch (Exception ex) { record = record with { Detail = record.Detail + " Local history could not be saved: " + ex.Message }; }
        return record;
    }

    private Task AppendAsync(RemovalRecord record, BlackboardEventType type) => _store.AppendAsync(new BlackboardEvent
    {
        EventType = type, ModuleId = "inventory-removal", AppId = record.AppId, CorrelationId = record.Id,
        Summary = record.AppName + ": " + record.Outcome,
        Payload = new() { ["removal_record"] = JsonSerializer.Serialize(record) },
        Provenance = new BlackboardProvenance { Source = nameof(RemovalService), Tool = "AppLens" }
    });

    public async Task<List<RemovalRecord>> ReadHistoryAsync()
    {
        var events = await _store.QueryAsync(new BlackboardEventQuery { ModuleId = "inventory-removal" });
        var rows = new List<RemovalRecord>();
        // Query order is newest-first. Prefer the recorded completion over its
        // approval, regardless of enumeration order or a clock adjustment.
        foreach (var evt in events.GroupBy(e => e.CorrelationId).Select(g => g
            .OrderByDescending(e => e.EventType == BlackboardEventType.ActionExecuted)
            .ThenByDescending(e => e.CreatedAt).First()))
        {
            try
            {
                if (evt.Payload.TryGetValue("removal_record", out var json) && JsonSerializer.Deserialize<RemovalRecord>(json) is { } record)
                    rows.Add(record.CompletedAt is null ? record with { Outcome = "Outcome unknown", Detail = "AppLens closed before completion was recorded. Run a scan and review the app in Windows." } : record);
            }
            catch (JsonException) { }
        }
        return rows.OrderBy(r => r.StartedAt).ToList();
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
