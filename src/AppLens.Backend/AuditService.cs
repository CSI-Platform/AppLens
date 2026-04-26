namespace AppLens.Backend;

public sealed class AuditService
{
    private readonly InventoryCollector _inventoryCollector;
    private readonly TuneCollector _tuneCollector;
    private readonly ProbeRunner _probeRunner;
    private readonly RulesEngine _rulesEngine;
    private readonly TunePlanBuilder _tunePlanBuilder;

    public AuditService()
    {
        _probeRunner = new ProbeRunner();
        _inventoryCollector = new InventoryCollector();
        _tuneCollector = new TuneCollector(_probeRunner);
        _rulesEngine = new RulesEngine();
        _tunePlanBuilder = new TunePlanBuilder();
    }

    public async Task<AuditSnapshot> RunAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(90));

        var machine = await _probeRunner.RunAsync(
            "Machine summary",
            _tuneCollector.CollectMachineAsync,
            new MachineSummary(),
            timeout.Token).ConfigureAwait(false);

        var inventory = await _probeRunner.RunAsync(
            "Inventory collector",
            _inventoryCollector.CollectAsync,
            new InventorySummary(),
            timeout.Token).ConfigureAwait(false);

        var tune = await _probeRunner.RunAsync(
            "Tune collector",
            _tuneCollector.CollectTuneAsync,
            new TuneSummary(),
            timeout.Token).ConfigureAwait(false);

        var snapshot = new AuditSnapshot
        {
            GeneratedAt = DateTimeOffset.Now,
            Machine = machine,
            Inventory = inventory,
            Tune = tune,
            ProbeStatuses = _probeRunner.Statuses.ToList()
        };

        var findings = _rulesEngine.Evaluate(snapshot);
        var completedSnapshot = new AuditSnapshot
        {
            GeneratedAt = snapshot.GeneratedAt,
            Machine = snapshot.Machine,
            Inventory = snapshot.Inventory,
            Tune = snapshot.Tune,
            Findings = findings,
            ProbeStatuses = snapshot.ProbeStatuses
        };

        return new AuditSnapshot
        {
            GeneratedAt = completedSnapshot.GeneratedAt,
            Machine = completedSnapshot.Machine,
            Inventory = completedSnapshot.Inventory,
            Tune = completedSnapshot.Tune,
            Findings = completedSnapshot.Findings,
            TunePlan = _tunePlanBuilder.Build(completedSnapshot),
            ProbeStatuses = completedSnapshot.ProbeStatuses
        };
    }
}
