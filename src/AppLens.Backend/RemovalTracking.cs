using System.Text.Json.Serialization;

namespace AppLens.Backend;

[JsonConverter(typeof(JsonStringEnumConverter<RemovalProcessState>))]
public enum RemovalProcessState { Unknown, Running, Exited }
[JsonConverter(typeof(JsonStringEnumConverter<RemovalVerification>))]
public enum RemovalVerification { NotChecked, Present, Absent, Unavailable }
public enum RemovalCheckMode { Unresolved, All, Due }

public sealed record RemovalProcessIdentity(int Id, DateTimeOffset StartedAt);
public sealed class RemovalRetryRequiredException() : InvalidOperationException(
    "The previous uninstaller may still be working. Review it in Windows, or confirm it has finished before trying again.");

public static class RemovalTracking
{
    public static string Execution(RemovalRecord record) => record.ExecutionOutcome ?? record.Outcome;
    public static bool NeedsReview(RemovalRecord record) =>
        record.Route != RemovalRoute.WindowsSettings &&
        (record.ProcessState == RemovalProcessState.Running ||
         (record.ProcessState != RemovalProcessState.Exited && Execution(record) is "Approved" or "Running" or "Still running" or "Outcome unknown") ||
         (record.FirstAbsentAt is null && record.Verification != RemovalVerification.Absent && record.StillInstalled != false &&
          Execution(record) is "Approved" or "Running" or "Still running" or "Outcome unknown" or
              "Not verified" or "Verification unavailable" or "Execution ended" or "Completed"));
    public static bool NeedsFollowUp(RemovalRecord record) =>
        record.ProcessState == RemovalProcessState.Running ||
        (Execution(record) is not ("Cancelled" or "Blocked" or "Restricted") &&
         (record.Verification is RemovalVerification.Unavailable or RemovalVerification.NotChecked ||
          (record.Verification != RemovalVerification.Absent &&
           (NeedsReview(record) || record.Route == RemovalRoute.WindowsSettings))));
    public static TimeSpan? CheckInterval(RemovalRecord record, DateTimeOffset now)
    {
        var elapsed = now - record.StartedAt;
        if (!NeedsFollowUp(record) || elapsed >= TimeSpan.FromMinutes(10)) return null;
        return elapsed < TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(2) : TimeSpan.FromSeconds(10);
    }
    public static bool IsDue(RemovalRecord record, DateTimeOffset now) =>
        CheckInterval(record, now) is { } interval && now - (record.LastCheckedAt ?? record.StartedAt) >= interval;
    public static string Status(RemovalRecord record)
    {
        if (Execution(record) is "Cancelled" or "Blocked" or "Restricted" && record.Verification == RemovalVerification.Unavailable)
            return Execution(record);
        var observation = record.Verification switch
        {
            RemovalVerification.Absent => "Absent when rechecked",
            RemovalVerification.Present => "Still installed when checked",
            RemovalVerification.Unavailable => "Verification unavailable",
            _ => "Not yet checked"
        };
        return record.ExecutionOutcome is null && record.LastCheckedAt is null ? record.Outcome :
            $"{Execution(record)} · {observation}";
    }
    // Legacy records contain identities, not executable commands. Never match by name.
    public static AppEntry? Target(RemovalRecord record)
    {
        if (record.Target is { } target) return target;
        if (record.AppId.StartsWith("msix:", StringComparison.OrdinalIgnoreCase))
            return new AppEntry { Id = record.AppId, PackageFullName = record.AppId[5..], CandidateRoute = RemovalRoute.Msix };
        var parts = record.AppId.Split(':', 4);
        if (parts.Length != 4 || parts[0] != "registry") return null;
        return new AppEntry { Id = record.AppId, RegistryHive = parts[1], RegistryView = parts[2], RegistryKey = parts[3],
            CandidateRoute = record.Route, ProductCode = record.Route == RemovalRoute.Msi ? parts[3] : "" };
    }
}
