using System.Diagnostics;

namespace AppLens.Backend.Tests;

public class RemovalProcessObservationTests
{
    [Fact]
    public async Task Matching_live_process_is_running_but_reused_id_is_not_the_recorded_process()
    {
        using var current = Process.GetCurrentProcess();
        var platform = new WindowsRemovalPlatform();
        var record = new RemovalRecord { Process = new(current.Id, current.StartTime.ToUniversalTime()) };
        Assert.Equal(RemovalProcessState.Running, (await platform.ObserveExecutionAsync(record, default))!.ProcessState);
        var differentStart = record with { Process = new(current.Id, current.StartTime.ToUniversalTime().AddSeconds(-1)) };
        var observed = await platform.ObserveExecutionAsync(differentStart, default);
        Assert.Equal(RemovalProcessState.Exited, observed!.ProcessState);
        Assert.Null(observed.ExitCode);
        Assert.False(current.HasExited);
    }

    [Fact]
    public async Task Missing_identity_is_unknown_and_missing_process_is_ended_without_invented_exit_code()
    {
        var platform = new WindowsRemovalPlatform();
        Assert.Null(await platform.ObserveExecutionAsync(new RemovalRecord(), default));
        var observed = await platform.ObserveExecutionAsync(new RemovalRecord { Process = new(int.MaxValue, DateTimeOffset.UnixEpoch) }, default);
        Assert.Equal(RemovalProcessState.Exited, observed!.ProcessState);
        Assert.Null(observed.ExitCode);
    }
}
