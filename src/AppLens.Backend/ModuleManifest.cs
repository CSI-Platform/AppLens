namespace AppLens.Backend;

public sealed class ModuleManifest
{
    public const string PlatformSchemaVersion = "1.0";

    public string SchemaVersion { get; init; } = PlatformSchemaVersion;
    public string AppId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string ModuleId { get; init; } = "";
    public string ModuleKind { get; init; } = "";
    public string Version { get; init; } = "1.0";
    public string RiskLevel { get; init; } = "low";
    public List<string> Capabilities { get; init; } = [];
    public List<string> Entrypoints { get; init; } = [];
    public List<string> DataContracts { get; init; } = [];
    public List<string> ActionContracts { get; init; } = [];
    public List<string> Privacy { get; init; } = [];
    public string StatusContract { get; init; } = "";
    public List<string> ReportRoots { get; init; } = [];
    public List<ModuleStorageRoot> StorageRoots { get; init; } = [];
    public List<ModuleHealthCheck> HealthChecks { get; init; } = [];
    public List<ModuleActionContract> Actions { get; init; } = [];
}

public sealed class ModuleStorageRoot
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public string PrivacyState { get; init; } = "raw_private";
    public string Description { get; init; } = "";
}

public sealed class ModuleHealthCheck
{
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "file";
    public string Target { get; init; } = "";
    public bool Required { get; init; } = true;
}

public sealed class ModuleActionContract
{
    public string Name { get; init; } = "";
    public string Permission { get; init; } = "read";
    public string ExecutorKey { get; init; } = "";
    public bool RequiresApproval { get; init; } = true;
    public bool SystemChanging { get; init; }
    public string Description { get; init; } = "";
}

public sealed class ModuleStatus
{
    public string ModuleId { get; init; } = "";
    public string AppId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public ModuleAvailability Availability { get; init; } = ModuleAvailability.Blocked;
    public string Reason { get; init; } = "";
    public string ExpectedSource { get; init; } = "";
    public string NextAction { get; init; } = "";
}

public enum ModuleAvailability
{
    Available,
    Blocked,
    Unavailable,
    NotConfigured
}

public enum ModuleActionExecutorState
{
    NotRequired,
    Runnable,
    NotImplemented,
    Blocked
}
