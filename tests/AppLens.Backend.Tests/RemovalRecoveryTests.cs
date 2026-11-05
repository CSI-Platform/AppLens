using System.Text.Json;

namespace AppLens.Backend.Tests;

public class RemovalRecoveryTests
{
    private static readonly AppEntry Target = new() { Id = "fixture", Name = "Fixture", Version = "1",
        Scope = InstallationScope.ThisUser, Kind = ApplicationKind.Desktop, CandidateRoute = RemovalRoute.Msi,
        ProductCode = "{0CC8958B-BBE6-47A4-BB56-B70FE34AE90C}" };

    [Fact]
    public async Task Null_history_record_blocks_reading_and_new_uninstalls_without_rewriting_evidence()
    {
        var f = new Fixture();
        var plan = await f.Service.PrepareAsync("fixture");
        var damaged = new BlackboardEvent { ModuleId = "inventory-removal",
            EventType = BlackboardEventType.ActionExecuted,
            Payload = new() { ["removal_record"] = "null" } };
        f.Store.Events.Add(damaged);

        await Assert.ThrowsAsync<InvalidDataException>(() => f.Service.ReadHistoryAsync());
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Service.PrepareAsync("fixture"));
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Service.ExecuteAsync(plan.Token, true));

        Assert.Equal(0, f.Platform.Starts);
        Assert.Same(damaged, Assert.Single(f.Store.Events));
        Assert.Equal("null", damaged.Payload["removal_record"]);
    }

    [Fact]
    public async Task Delayed_removal_and_reinstallation_append_observations_without_rewriting_original_result()
    {
        var f = new Fixture();
        var original = await f.Start();
        Assert.Equal("Not verified", original.Outcome);
        f.Platform.Installed = false; f.Clock.Advance(5);
        var absent = Assert.Single(await f.Service.ReconcileAsync());
        Assert.Equal(RemovalVerification.Absent, absent.Verification);
        Assert.Contains("Absent when rechecked", absent.StatusDisplay);
        Assert.Equal(original.Outcome, absent.Outcome);
        Assert.Equal(original.CompletedAt, absent.CompletedAt);
        Assert.True(absent.StillInstalled);
        Assert.True(absent.Revision > original.Revision);
        f.Platform.Installed = true; f.Clock.Advance(10);
        var reinstalled = Assert.Single(await f.Service.ReconcileAsync(RemovalCheckMode.All));
        Assert.Equal(RemovalVerification.Present, reinstalled.Verification);
        Assert.Equal(absent.StartedAt, reinstalled.StartedAt);
        Assert.Contains(f.Store.Events, e => JsonSerializer.Deserialize<RemovalRecord>(e.Payload["removal_record"])!.Verification == RemovalVerification.Absent);
        Assert.Equal(JsonSerializer.Serialize(reinstalled), JsonSerializer.Serialize(Assert.Single(await new RemovalService(f.Platform, f.Store).ReadHistoryAsync())));
        await f.Service.PrepareAsync("fixture");
    }

    [Fact]
    public async Task Running_process_survives_reopening_and_is_blocked_even_after_three_minutes_or_explicit_retry()
    {
        var f = new Fixture();
        f.Platform.StartResult = f.Platform.Observation = Running();
        var original = await f.Start(); f.Clock.Advance(200);
        var reopened = new RemovalService(f.Platform, f.Store, f.Clock);
        var pending = Assert.Single(await reopened.ReconcileAsync());
        Assert.Equal(RemovalProcessState.Running, pending.ProcessState);
        Assert.Equal(original.Process, pending.Process);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reopened.PrepareAsync("fixture", previousUninstallerFinished: true));
        f.Platform.Observation = new("Completed", "Finished", ExitCode: 0, ProcessState: RemovalProcessState.Exited);
        f.Platform.Installed = false;
        var finished = Assert.Single(await reopened.ReconcileAsync());
        Assert.Equal(RemovalVerification.Absent, finished.Verification);
        Assert.Equal(0, finished.ExitCode);
        Assert.False(RemovalTracking.NeedsReview(finished));
    }

    [Fact]
    public async Task Inaccessible_process_requires_confirmation_and_fresh_target_validation()
    {
        var f = new Fixture(); f.Platform.StartResult = Running();
        await f.Start(); f.Platform.Observation = null;
        await f.Service.ReconcileAsync();
        await Assert.ThrowsAsync<RemovalRetryRequiredException>(() => f.Service.PrepareAsync("fixture"));
        var plan = await f.Service.PrepareAsync("fixture", previousUninstallerFinished: true);
        f.Platform.Current = new AppEntry { Id = "fixture", Name = "Changed installation", CandidateRoute = RemovalRoute.WindowsSettings };
        Assert.Equal("Blocked", (await f.Service.ExecuteAsync(plan.Token, true)).Outcome);
        Assert.Equal(1, f.Platform.Starts);
    }

    [Fact]
    public async Task Retry_without_confirmation_cannot_hide_behind_a_settings_handoff()
    {
        var f = new Fixture(); f.Platform.StartResult = Running();
        await f.Start();
        f.Platform.StartResult = new("Handed off to Windows", "Opened Windows");
        var handoff = await f.Service.PrepareAsync("fixture", reviewInWindows: true);
        await f.Service.ExecuteAsync(handoff.Token, true);
        await Assert.ThrowsAsync<RemovalRetryRequiredException>(() => f.Service.PrepareAsync("fixture"));
    }

    [Theory]
    [InlineData("Failed", 20, false)]
    [InlineData("Cancelled", 1602, false)]
    [InlineData("Restricted", 1625, false)]
    [InlineData("Completed", 3010, true)]
    public async Task Presence_does_not_erase_exit_evidence(string outcome, int code, bool restart)
    {
        var f = new Fixture(); f.Platform.StartResult = new(outcome, "Original evidence", restart, code);
        var original = await f.Start(); f.Platform.Installed = false;
        var observed = Assert.Single(await f.Service.ReconcileAsync(RemovalCheckMode.All));
        Assert.Equal(outcome, observed.ExecutionOutcome);
        Assert.Equal(code, observed.ExitCode);
        Assert.Equal(restart, observed.RestartRequired);
        Assert.Equal(original.Outcome, observed.Outcome);
        Assert.Equal(RemovalVerification.Absent, observed.Verification);
    }

    [Fact]
    public async Task Settings_handoff_never_claims_to_have_removed_the_app()
    {
        var f = new Fixture(); f.Platform.StartResult = new("Handed off to Windows", "Opened Windows");
        var plan = await f.Service.PrepareAsync("fixture", reviewInWindows: true);
        await f.Service.ExecuteAsync(plan.Token, true); f.Platform.Installed = false;
        var observed = Assert.Single(await f.Service.ReconcileAsync());
        Assert.Equal("Handed off to Windows", observed.ExecutionOutcome);
        Assert.DoesNotContain("Removed", observed.StatusDisplay);
        Assert.Equal(RemovalVerification.Absent, observed.Verification);
    }

    [Fact]
    public async Task Failed_presence_read_is_unavailable_not_absent()
    {
        var f = new Fixture(); await f.Start(); f.Platform.PresenceFailure = true;
        var observed = Assert.Single(await f.Service.ReconcileAsync());
        Assert.Equal(RemovalVerification.Unavailable, observed.Verification);
        Assert.True(observed.StillInstalled);
    }

    [Fact]
    public async Task History_write_failure_keeps_process_evidence_and_blocks_new_launches_until_saved()
    {
        var f = new Fixture(); f.Platform.StartResult = Running(); f.Store.FailAfter = 1;
        var action = await f.Start();
        Assert.NotNull(action.PersistenceWarning);
        Assert.Equal(Running().Process, action.Process);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.PrepareAsync("fixture"));
        f.Store.FailAfter = int.MaxValue;
        var saved = Assert.Single(await f.Service.ReconcileAsync());
        Assert.Null(saved.PersistenceWarning);
        Assert.Equal(saved.Process, Assert.Single(await new RemovalService(f.Platform, f.Store).ReadHistoryAsync()).Process);
        Assert.Equal(1, f.Platform.Starts);
    }

    [Fact]
    public async Task Revision_order_wins_over_clock_adjustment_and_legacy_completion_wins_over_approval()
    {
        var f = new Fixture();
        var original = await f.Start(); f.Platform.Installed = false; f.Clock.Advance(-100);
        var latest = Assert.Single(await f.Service.ReconcileAsync(RemovalCheckMode.All));
        f.Store.Events.Reverse();
        Assert.Equal(latest.Revision, Assert.Single(await f.Service.ReadHistoryAsync()).Revision);
        Assert.Equal(RemovalVerification.Absent, Assert.Single(await f.Service.ReadHistoryAsync()).Verification);
        var legacy = original with { Id = "legacy", Revision = 0, ExecutionOutcome = null, Target = null, AppId = "registry:CurrentUser:Default:example", Outcome = "Removed" };
        f.Store.Add(legacy, BlackboardEventType.ActionExecuted);
        f.Store.Add(legacy with { Outcome = "Approved", CompletedAt = null }, BlackboardEventType.ActionApproved);
        Assert.Equal("Removed", (await f.Service.ReadHistoryAsync()).Single(r => r.Id == "legacy").Outcome);
    }

    [Fact]
    public void Automatic_schedule_is_bounded_and_uses_two_then_ten_seconds()
    {
        var start = DateTimeOffset.UtcNow;
        var action = new RemovalRecord { StartedAt = start, ExecutionOutcome = "Running", ProcessState = RemovalProcessState.Running };
        Assert.False(RemovalTracking.IsDue(action, start.AddSeconds(1)));
        Assert.True(RemovalTracking.IsDue(action, start.AddSeconds(2)));
        Assert.Equal(TimeSpan.FromSeconds(2), RemovalTracking.CheckInterval(action, start.AddSeconds(29)));
        Assert.Equal(TimeSpan.FromSeconds(10), RemovalTracking.CheckInterval(action, start.AddSeconds(30)));
        Assert.Null(RemovalTracking.CheckInterval(action, start.AddMinutes(10)));
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Blocked")]
    [InlineData("Restricted")]
    public void Declined_or_blocked_actions_do_not_show_as_pending_uninstalls(string outcome)
    {
        var action = new RemovalRecord { ExecutionOutcome = outcome, Outcome = outcome,
            Verification = RemovalVerification.Unavailable, StartedAt = DateTimeOffset.UtcNow };
        Assert.False(RemovalTracking.NeedsFollowUp(action));
    }

    [Fact]
    public async Task Cancelled_check_does_not_record_absence_or_launch_anything()
    {
        var f = new Fixture(); await f.Start(); using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Service.ReconcileAsync(cancellationToken: cancelled.Token));
        Assert.Equal(RemovalVerification.Present, Assert.Single(await f.Service.ReadHistoryAsync()).Verification);
        Assert.Equal(1, f.Platform.Starts);
    }

    private static RemovalExecution Running() => new("Running", "Fixture started", Process: new(123, DateTimeOffset.UnixEpoch), ProcessState: RemovalProcessState.Running);
    private sealed class Fixture
    {
        public readonly Platform Platform = new(); public readonly Store Store = new(); public readonly Clock Clock = new();
        public RemovalService Service { get; }
        public Fixture() { Service = new(Platform, Store, Clock); }
        public async Task<RemovalRecord> Start() => await Service.ExecuteAsync((await Service.PrepareAsync("fixture")).Token, true);
    }
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(int seconds) => _now = _now.AddSeconds(seconds);
    }
    private sealed class Platform : IRemovalPlatform
    {
        public AppEntry Current = Target; public int Starts; public bool? Installed = true; public bool PresenceFailure;
        public RemovalExecution StartResult = new("Completed", "Parent exited", ExitCode: 0, ProcessState: RemovalProcessState.Exited);
        public RemovalExecution? Observation;
        public Task<AppEntry?> ReadAppAsync(string id) => Task.FromResult<AppEntry?>(Current);
        public Task<List<DiskSnapshot>> ReadDisksAsync() => Task.FromResult(new List<DiskSnapshot>());
        public Task<bool?> IsInstalledAsync(AppEntry app) => PresenceFailure ? throw new IOException("Cannot read target") : Task.FromResult(Installed);
        public Task<RemovalExecution> ExecuteAsync(AppEntry app, RemovalCommand command, bool administrator) { Starts++; return Task.FromResult(StartResult); }
        public Task<RemovalExecution?> ObserveExecutionAsync(RemovalRecord record, CancellationToken token) => Task.FromResult(Observation);
    }
    private sealed class Store : IBlackboardStore
    {
        public readonly List<BlackboardEvent> Events = []; public int FailAfter = int.MaxValue;
        public void Add(RemovalRecord record, BlackboardEventType type) => Events.Add(new BlackboardEvent { ModuleId = "inventory-removal", EventType = type, CorrelationId = record.Id, Payload = new() { ["removal_record"] = JsonSerializer.Serialize(record) } });
        public Task AppendAsync(BlackboardEvent evt, CancellationToken cancellationToken = default)
        { if (Events.Count >= FailAfter) throw new IOException("Disk unavailable"); Events.Add(evt); return Task.CompletedTask; }
        public Task<List<BlackboardEvent>> ReadAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(Events.ToList());
        public Task<List<BlackboardEvent>> QueryAsync(BlackboardEventQuery query, CancellationToken cancellationToken = default) => ReadAllAsync(cancellationToken);
        public Task<int> GetIndexedEventCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(Events.Count);
    }
}
