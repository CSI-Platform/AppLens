namespace AppLens.Backend;

public enum ModuleHealthState
{
    Ok,
    Missing,
    Unknown
}

public sealed class ModuleHealthCheckReadModel
{
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Target { get; init; } = "";
    public bool Required { get; init; } = true;
    public ModuleHealthState State { get; init; } = ModuleHealthState.Unknown;
    public string Detail { get; init; } = "";
    public bool IsBlocking { get; init; }
}

public sealed class ModuleStorageRootReadModel
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public string PrivacyState { get; init; } = "";
    public string Description { get; init; } = "";
    public bool Exists { get; init; }
    public string Detail { get; init; } = "";
}

public sealed class ModuleActionReadModel
{
    public string Name { get; init; } = "";
    public string Permission { get; init; } = "";
    public bool RequiresApproval { get; init; } = true;
    public bool SystemChanging { get; init; }
    public string Description { get; init; } = "";
    public bool IsRunnable { get; init; }
}

public sealed class ModuleLedgerEventReadModel
{
    public string EventId { get; init; } = "";
    public BlackboardEventType EventType { get; init; }
    public string CorrelationId { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public BlackboardDataState DataState { get; init; }
    public BlackboardPrivacyState PrivacyState { get; init; }
    public string Summary { get; init; } = "";
}

public sealed class ModuleDetailReadModel
{
    public string ModuleId { get; init; } = "";
    public string AppId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string ModuleKind { get; init; } = "";
    public string Version { get; init; } = "";
    public string RiskLevel { get; init; } = "";
    public ModuleAvailability Availability { get; init; } = ModuleAvailability.Unavailable;
    public string StatusLabel { get; init; } = "";
    public string Reason { get; init; } = "";
    public string ExpectedSource { get; init; } = "";
    public string NextAction { get; init; } = "";
    public List<string> Capabilities { get; init; } = [];
    public List<string> Entrypoints { get; init; } = [];
    public List<string> DataContracts { get; init; } = [];
    public List<string> ActionContracts { get; init; } = [];
    public List<string> Privacy { get; init; } = [];
    public string StatusContract { get; init; } = "";
    public List<string> ReportRoots { get; init; } = [];
    public List<ModuleStorageRootReadModel> StorageRoots { get; init; } = [];
    public List<ModuleHealthCheckReadModel> HealthChecks { get; init; } = [];
    public List<ModuleActionReadModel> Actions { get; init; } = [];
    public List<ModuleLedgerEventReadModel> RecentLedgerEvents { get; init; } = [];
}

public sealed class ModuleDetailReadModelService
{
    private readonly ModuleStatusService _moduleStatusService;
    private readonly IBlackboardStore _blackboardStore;

    public ModuleDetailReadModelService(ModuleStatusService moduleStatusService, IBlackboardStore blackboardStore)
    {
        _moduleStatusService = moduleStatusService;
        _blackboardStore = blackboardStore;
    }

    public async Task<ModuleDetailReadModel?> GetModuleDetailAsync(
        string moduleId,
        int recentEventLimit = 25,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return null;
        }

        var manifest = _moduleStatusService.GetManifests()
            .FirstOrDefault(item => string.Equals(item.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase));

        if (manifest is null)
        {
            return null;
        }

        var status = _moduleStatusService.GetStatuses()
            .FirstOrDefault(item => string.Equals(item.ModuleId, manifest.ModuleId, StringComparison.OrdinalIgnoreCase))
            ?? new ModuleStatus
            {
                ModuleId = manifest.ModuleId,
                AppId = manifest.AppId,
                DisplayName = manifest.DisplayName,
                Availability = ModuleAvailability.Unavailable,
                Reason = "Module status is unavailable.",
                NextAction = "Re-run module detection."
            };

        var healthChecks = manifest.HealthChecks.Select(EvaluateHealthCheck).ToList();
        var storageRoots = manifest.StorageRoots.Select(EvaluateStorageRoot).ToList();
        var actions = manifest.Actions.Select(ToAction).ToList();
        var events = await GetRecentModuleEventsAsync(manifest.ModuleId, recentEventLimit, cancellationToken).ConfigureAwait(false);

        return new ModuleDetailReadModel
        {
            ModuleId = manifest.ModuleId,
            AppId = manifest.AppId,
            DisplayName = manifest.DisplayName,
            ModuleKind = manifest.ModuleKind,
            Version = manifest.Version,
            RiskLevel = manifest.RiskLevel,
            Availability = status.Availability,
            StatusLabel = status.Availability.ToString(),
            Reason = status.Reason,
            ExpectedSource = status.ExpectedSource,
            NextAction = status.NextAction,
            Capabilities = manifest.Capabilities,
            Entrypoints = manifest.Entrypoints,
            DataContracts = manifest.DataContracts,
            ActionContracts = manifest.ActionContracts,
            Privacy = manifest.Privacy,
            StatusContract = manifest.StatusContract,
            ReportRoots = manifest.ReportRoots,
            StorageRoots = storageRoots,
            HealthChecks = healthChecks,
            Actions = actions,
            RecentLedgerEvents = events
        };
    }

    private async Task<List<ModuleLedgerEventReadModel>> GetRecentModuleEventsAsync(
        string moduleId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            return [];
        }

        var events = await _blackboardStore.QueryAsync(
                new BlackboardEventQuery
                {
                    ModuleId = moduleId,
                    Limit = limit
                },
                cancellationToken)
            .ConfigureAwait(false);

        return events.Select(ToLedgerEvent).ToList();
    }

    private static ModuleHealthCheckReadModel EvaluateHealthCheck(ModuleHealthCheck check)
    {
        var kind = check.Kind ?? "";
        if (kind.Equals("file", StringComparison.OrdinalIgnoreCase))
        {
            var exists = File.Exists(check.Target);
            return new ModuleHealthCheckReadModel
            {
                Name = check.Name,
                Kind = kind,
                Target = check.Target,
                Required = check.Required,
                State = exists ? ModuleHealthState.Ok : ModuleHealthState.Missing,
                Detail = exists ? "File present." : "File missing.",
                IsBlocking = check.Required && !exists
            };
        }

        if (kind.Equals("directory", StringComparison.OrdinalIgnoreCase))
        {
            var exists = Directory.Exists(check.Target);
            return new ModuleHealthCheckReadModel
            {
                Name = check.Name,
                Kind = kind,
                Target = check.Target,
                Required = check.Required,
                State = exists ? ModuleHealthState.Ok : ModuleHealthState.Missing,
                Detail = exists ? "Directory present." : "Directory missing.",
                IsBlocking = check.Required && !exists
            };
        }

        return new ModuleHealthCheckReadModel
        {
            Name = check.Name,
            Kind = kind,
            Target = check.Target,
            Required = check.Required,
            State = ModuleHealthState.Unknown,
            Detail = "Unsupported health check kind.",
            IsBlocking = check.Required
        };
    }

    private static ModuleStorageRootReadModel EvaluateStorageRoot(ModuleStorageRoot root)
    {
        var exists = Directory.Exists(root.Path) || File.Exists(root.Path);
        return new ModuleStorageRootReadModel
        {
            Name = root.Name,
            Path = root.Path,
            PrivacyState = root.PrivacyState,
            Description = root.Description,
            Exists = exists,
            Detail = exists ? "Detected." : "Missing."
        };
    }

    private static ModuleActionReadModel ToAction(ModuleActionContract action) =>
        new()
        {
            Name = action.Name,
            Permission = action.Permission,
            RequiresApproval = action.RequiresApproval,
            SystemChanging = action.SystemChanging,
            Description = action.Description,
            IsRunnable = action.Permission.Contains("execute", StringComparison.OrdinalIgnoreCase)
        };

    private static ModuleLedgerEventReadModel ToLedgerEvent(BlackboardEvent evt) =>
        new()
        {
            EventId = evt.EventId,
            EventType = evt.EventType,
            CorrelationId = evt.CorrelationId,
            CreatedAt = evt.CreatedAt,
            DataState = evt.DataState,
            PrivacyState = evt.PrivacyState,
            Summary = evt.Summary
        };
}

