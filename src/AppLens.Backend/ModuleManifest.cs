namespace AppLens.Backend;

public sealed class ModuleManifest
{
    public string AppId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string ModuleId { get; init; } = "";
    public string Version { get; init; } = "1.0";
    public string RiskLevel { get; init; } = "low";
    public List<string> Capabilities { get; init; } = [];
    public List<string> Entrypoints { get; init; } = [];
    public List<string> DataContracts { get; init; } = [];
    public List<string> ActionContracts { get; init; } = [];
    public List<string> Privacy { get; init; } = [];
    public string StatusContract { get; init; } = "";
    public List<string> ReportRoots { get; init; } = [];
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
    Unavailable
}
