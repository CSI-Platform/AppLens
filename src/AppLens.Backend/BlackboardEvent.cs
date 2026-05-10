using System.Text.Json.Serialization;

namespace AppLens.Backend;

public sealed class BlackboardEvent
{
    public string SchemaVersion { get; init; } = "1.0";
    public string EventId { get; init; } = $"evt-{Guid.NewGuid():N}";
    public BlackboardEventType EventType { get; init; }
    public string ParticipantIdentity { get; init; } = "applens-desktop";
    public BlackboardParticipantKind ParticipantKind { get; init; } = BlackboardParticipantKind.FirstPartyModule;
    public string ModuleId { get; init; } = "";
    public string AppId { get; init; } = "applens-desktop";
    public string ScopeId { get; init; } = "local_workstation";
    public string? GrantId { get; init; }
    public string CorrelationId { get; init; } = $"corr-{Guid.NewGuid():N}";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public BlackboardLifecycleState LifecycleState { get; init; } = BlackboardLifecycleState.Created;
    public BlackboardDataState DataState { get; init; } = BlackboardDataState.Validated;
    public BlackboardPrivacyState PrivacyState { get; init; } = BlackboardPrivacyState.RawPrivate;
    public string Summary { get; init; } = "";
    public Dictionary<string, string> Payload { get; init; } = [];
    public List<BlackboardArtifactRef> ArtifactRefs { get; init; } = [];
    public BlackboardProvenance Provenance { get; init; } = new();
    public BlackboardPolicyResult? PolicyResult { get; init; }
    public string? CanonicalHash { get; init; }
    public string? SignerKeyId { get; init; }
    public string? Signature { get; init; }

    public static BlackboardEvent ForScanCompleted(AuditSnapshot snapshot, string? correlationId = null) =>
        new()
        {
            EventType = BlackboardEventType.ScanCompleted,
            ModuleId = "report",
            CorrelationId = correlationId ?? $"corr-scan-{Guid.NewGuid():N}",
            CreatedAt = snapshot.GeneratedAt,
            Summary = $"Scan completed with readiness {snapshot.Readiness.Score}/100 {snapshot.Readiness.Rating}.",
            Payload = new Dictionary<string, string>
            {
                ["readiness_score"] = snapshot.Readiness.Score.ToString(),
                ["readiness_rating"] = snapshot.Readiness.Rating,
                ["finding_count"] = snapshot.Findings.Count.ToString(),
                ["tune_plan_count"] = snapshot.TunePlan.Count.ToString(),
                ["review_count"] = snapshot.Readiness.ReviewCount.ToString(),
                ["optional_count"] = snapshot.Readiness.OptionalCount.ToString(),
                ["admin_required_count"] = snapshot.Readiness.AdminRequiredCount.ToString()
            },
            Provenance = new BlackboardProvenance
            {
                Source = "AuditService.RunAsync",
                Tool = "AppLens",
                ToolVersion = typeof(AuditService).Assembly.GetName().Version?.ToString() ?? "unknown"
            }
        };

    public static BlackboardEvent ForTuneAction(TuneActionRecord action, string? correlationId = null)
    {
        var allowed = action.Status == TuneActionStatus.Succeeded;
        return new BlackboardEvent
        {
            EventType = BlackboardEventType.TuneActionCompleted,
            ModuleId = "tune",
            CorrelationId = correlationId ?? $"corr-tune-{Guid.NewGuid():N}",
            CreatedAt = action.CompletedAt,
            DataState = action.Status switch
            {
                TuneActionStatus.Succeeded => BlackboardDataState.Validated,
                TuneActionStatus.Blocked => BlackboardDataState.Blocked,
                TuneActionStatus.Failed => BlackboardDataState.Invalidated,
                TuneActionStatus.RolledBack => BlackboardDataState.Invalidated,
                _ => BlackboardDataState.Blocked
            },
            Summary = $"Tune action {action.Kind} for {action.Target} ended with {action.Status}.",
            Payload = new Dictionary<string, string>
            {
                ["action_id"] = action.Id,
                ["plan_item_id"] = action.PlanItemId,
                ["kind"] = action.Kind.ToString(),
                ["status"] = action.Status.ToString(),
                ["target"] = action.Target,
                ["message"] = action.Message,
                ["started_at"] = action.StartedAt.ToString("O"),
                ["completed_at"] = action.CompletedAt.ToString("O")
            },
            Provenance = new BlackboardProvenance
            {
                Source = "TuneActionExecutor.ExecuteAsync",
                Tool = "AppLens-Tune",
                ToolVersion = typeof(TuneActionExecutor).Assembly.GetName().Version?.ToString() ?? "unknown"
            },
            PolicyResult = new BlackboardPolicyResult
            {
                Allowed = allowed,
                BlockedReason = allowed ? "" : action.Message,
                RequiresApproval = true,
                RequiresAdmin = action.RequiresAdmin,
                RiskLevel = action.RequiresAdmin ? "medium" : "low",
                PolicyId = "applens-tune-v1"
            }
        };
    }
}

public sealed class BlackboardArtifactRef
{
    public string Path { get; init; } = "";
    public string Kind { get; init; } = "";
    public string PrivacyState { get; init; } = "raw_private";
}

public sealed class BlackboardProvenance
{
    public string Source { get; init; } = "";
    public string Tool { get; init; } = "";
    public string ToolVersion { get; init; } = "";
    public string Command { get; init; } = "";
    public string NodeId { get; init; } = Environment.MachineName;
    public string ModelId { get; init; } = "";
    public List<string> InputRefs { get; init; } = [];
}

public sealed class BlackboardPolicyResult
{
    public bool Allowed { get; init; }
    public string BlockedReason { get; init; } = "";
    public bool RequiresApproval { get; init; }
    public bool RequiresAdmin { get; init; }
    public string RiskLevel { get; init; } = "low";
    public string PolicyId { get; init; } = "";
}

[JsonConverter(typeof(JsonStringEnumConverter<BlackboardEventType>))]
public enum BlackboardEventType
{
    CapabilityObserved,
    EvidenceCaptured,
    ReportGenerated,
    ActionProposed,
    ActionApproved,
    ActionExecuted,
    VerificationRecorded,
    ModelRunRecorded,
    BlockedState,
    ScanCompleted,
    TuneActionCompleted
}

[JsonConverter(typeof(JsonStringEnumConverter<BlackboardParticipantKind>))]
public enum BlackboardParticipantKind
{
    FirstPartyModule,
    NodeAgent,
    McpApp,
    Connector,
    HumanOperator,
    System,
    ThirdPartyAgent
}

[JsonConverter(typeof(JsonStringEnumConverter<BlackboardLifecycleState>))]
public enum BlackboardLifecycleState
{
    Created,
    Imported,
    Validated,
    UsedInReport,
    Invalidated,
    Expired
}

[JsonConverter(typeof(JsonStringEnumConverter<BlackboardDataState>))]
public enum BlackboardDataState
{
    Validated,
    Fixture,
    Stale,
    Blocked,
    Invalidated,
    Unavailable
}

[JsonConverter(typeof(JsonStringEnumConverter<BlackboardPrivacyState>))]
public enum BlackboardPrivacyState
{
    RawPrivate,
    Sanitized,
    Exportable
}
