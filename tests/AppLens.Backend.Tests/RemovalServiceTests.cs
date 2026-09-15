namespace AppLens.Backend.Tests;

public class RemovalServiceTests
{
    private static AppEntry App(string version = "1") => new() { Id = "fixture", Name = "Disposable fixture",
        Version = version, Scope = InstallationScope.ThisUser, Kind = ApplicationKind.Desktop,
        CandidateRoute = RemovalRoute.Msi, ProductCode = "{0CC8958B-BBE6-47A4-BB56-B70FE34AE90C}" };

    [Fact]
    public async Task ChangedInstallationBlocksExecutionAfterConfirmation()
    {
        var platform = new FakePlatform(); var store = new FakeStore(); var service = new RemovalService(platform, store);
        var plan = await service.PrepareAsync("fixture");
        platform.App = App("2");
        Assert.Equal("Blocked", (await service.ExecuteAsync(plan.Token, true)).Outcome);
        Assert.Equal(0, platform.Executions);
    }

    [Fact]
    public async Task DeclinedConfirmationNeverExecutesAndTokensCannotBeReused()
    {
        var platform = new FakePlatform(); var service = new RemovalService(platform, new FakeStore());
        var plan = await service.PrepareAsync("fixture");
        Assert.Equal("Cancelled", (await service.ExecuteAsync(plan.Token, false)).Outcome);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExecuteAsync(plan.Token, true));
        Assert.Equal(0, platform.Executions);
    }

    [Fact]
    public async Task ApprovalMustBeRecordedBeforeLaunching()
    {
        var platform = new FakePlatform(); var service = new RemovalService(platform, new FakeStore { FailWrites = true });
        var plan = await service.PrepareAsync("fixture");
        await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(plan.Token, true));
        Assert.Equal(0, platform.Executions);
    }

    [Theory]
    [InlineData(true, "Not verified")]
    [InlineData(false, "Removed")]
    [InlineData(null, "Verification unavailable")]
    public async Task ExitSuccessRequiresIndependentInstallationVerification(bool? installed, string outcome)
    {
        var platform = new FakePlatform { Installed = installed, Result = new("Completed", "Restart pending.", true) };
        var service = new RemovalService(platform, new FakeStore());
        var result = await service.ExecuteAsync((await service.PrepareAsync("fixture")).Token, true);
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(installed, result.StillInstalled);
        Assert.Equal(InstallationScope.ThisUser, result.Scope);
        Assert.Equal("1", result.AppVersion);
        Assert.True(result.RestartRequired);
        Assert.Equal("+1.00 KiB", result.DiskChangeDisplay.Split(": ")[1].Split(" free")[0]);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(result), System.Text.Json.JsonSerializer.Serialize(Assert.Single(await service.ReadHistoryAsync())));
        var snapshot = new InventorySnapshot { Actions = [result] };
        Assert.Contains(outcome, new ReportWriter().WriteJson(snapshot));
        Assert.Contains(outcome, new ReportWriter().WriteMarkdown(snapshot));
    }

    [Theory]
    [InlineData("Cancelled")]
    [InlineData("Restricted")]
    [InlineData("Still running")]
    [InlineData("Failed")]
    public async Task FailureStatesSurviveVerificationAndPendingDoesNotRetry(string outcome)
    {
        var platform = new FakePlatform { Result = new(outcome, "Fixture result") };
        var service = new RemovalService(platform, new FakeStore());
        var result = await service.ExecuteAsync((await service.PrepareAsync("fixture")).Token, true);
        Assert.Equal(outcome, result.Outcome);
        if (outcome == "Still running") await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareAsync("fixture"));
        Assert.Equal(1, platform.Executions);
    }

    [Fact]
    public async Task AlternateAdministratorCannotTargetAnotherUsersPerUserMsi()
    {
        var platform = new FakePlatform(); var service = new RemovalService(platform, new FakeStore());
        Assert.Equal("Blocked", (await service.ExecuteAsync((await service.PrepareAsync("fixture")).Token, true, true)).Outcome);
        Assert.Equal(0, platform.Executions);
    }

    [Theory]
    [InlineData(1602, "Cancelled", false)]
    [InlineData(3010, "Completed", true)]
    [InlineData(1625, "Restricted", false)]
    [InlineData(1603, "Failed", false)]
    public void MsiExitCodesPreserveMeaning(int code, string outcome, bool restart)
    {
        var result = WindowsRemovalPlatform.InterpretExitCode(RemovalRoute.Msi, code);
        Assert.Equal(outcome, result.Outcome); Assert.Equal(restart, result.RestartRequired);
        Assert.Equal("Failed", WindowsRemovalPlatform.InterpretExitCode(RemovalRoute.Vendor, code).Outcome);
    }

    private sealed class FakePlatform : IRemovalPlatform
    {
        public AppEntry App = RemovalServiceTests.App();
        public int Executions;
        private int _diskReads;
        public bool? Installed = false;
        public RemovalExecution Result = new("Completed", "Fixture removed");
        public Task<AppEntry?> ReadAppAsync(string id) => Task.FromResult<AppEntry?>(App);
        public Task<List<DiskSnapshot>> ReadDisksAsync() => Task.FromResult<List<DiskSnapshot>>([new() { Volume = "C:", TotalBytes = 10000, FreeBytes = ++_diskReads * 1024 }]);
        public Task<RemovalExecution> ExecuteAsync(AppEntry app, RemovalCommand command, bool administrator) { Executions++; return Task.FromResult(Result); }
        public Task<bool?> IsInstalledAsync(AppEntry app) => Task.FromResult(Installed);
    }

    private sealed class FakeStore : IBlackboardStore
    {
        private readonly List<BlackboardEvent> _events = [];
        public bool FailWrites;
        public Task AppendAsync(BlackboardEvent evt, CancellationToken cancellationToken = default)
        { if (FailWrites) throw new IOException("Fixture disk unavailable"); _events.Add(evt); return Task.CompletedTask; }
        public Task<List<BlackboardEvent>> ReadAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(_events);
        public Task<List<BlackboardEvent>> QueryAsync(BlackboardEventQuery query, CancellationToken cancellationToken = default) => Task.FromResult(_events);
        public Task<int> GetIndexedEventCountAsync(CancellationToken cancellationToken = default) => Task.FromResult(_events.Count);
    }
}
