using System.Diagnostics;

namespace AppLens.Backend;

public sealed class AuditService
{
    private readonly Func<CancellationToken, Task<DeviceSummary>> _machine;
    private readonly Func<CancellationToken, Task<InventorySummary>> _inventory;

    public AuditService() : this(new MachineCollector().CollectAsync, new InventoryCollector().CollectAsync) { }
    public AuditService(Func<CancellationToken, Task<DeviceSummary>> machine,
        Func<CancellationToken, Task<InventorySummary>> inventory)
    { _machine = machine; _inventory = inventory; }

    public async Task<InventorySnapshot> RunAsync(CancellationToken cancellationToken = default,
        IProgress<InventorySnapshot>? progress = null)
    {
        var watch = Stopwatch.StartNew();
        var captured = DateTimeOffset.Now;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var machineTask = Collect("Device summary", _machine, new DeviceSummary(), timeout.Token);
        var inventoryTask = Collect("Installed applications", _inventory, new InventorySummary(), timeout.Token);
        var inventory = await inventoryTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var first = watch.Elapsed;
        if (!machineTask.IsCompleted)
        {
            progress?.Report(new InventorySnapshot
            {
                GeneratedAt = captured, Inventory = inventory.Value, FirstResultsDuration = first, Duration = first,
                ProbeStatuses = [inventory.Status, .. inventory.Value.ProbeStatuses,
                    new ProbeStatus { Name = "Device summary", State = ProbeState.Skipped, Message = "Still collecting." }]
            });
        }
        var machine = await machineTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return new InventorySnapshot
        {
            GeneratedAt = captured, Machine = machine.Value, Inventory = inventory.Value,
            ProbeStatuses = [machine.Status, inventory.Status, .. machine.Value.ProbeStatuses, .. inventory.Value.ProbeStatuses],
            Duration = watch.Elapsed, FirstResultsDuration = first
        };
    }

    private static async Task<(T Value, ProbeStatus Status)> Collect<T>(string name,
        Func<CancellationToken, Task<T>> collect, T fallback, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            var value = await collect(token).WaitAsync(token).ConfigureAwait(false);
            return (value, new ProbeStatus { Name = name, State = ProbeState.Succeeded, Duration = watch.Elapsed });
        }
        catch (Exception ex)
        {
            return (fallback, new ProbeStatus
            {
                Name = name, State = ex is OperationCanceledException ? ProbeState.Partial : ProbeState.Failed,
                Message = ex is OperationCanceledException ? "Reading timed out or was cancelled." : ex.Message,
                Duration = watch.Elapsed
            });
        }
    }
}
