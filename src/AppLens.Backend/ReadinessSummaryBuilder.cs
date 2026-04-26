namespace AppLens.Backend;

public sealed class ReadinessSummaryBuilder
{
    public ReadinessSummary Build(AuditSnapshot snapshot)
    {
        var startupEnabledCount = snapshot.Tune.StartupEntries.Count(IsEnabled);
        var automaticServiceCount = snapshot.Tune.Services.Count(service =>
            service.StartType.Contains("Automatic", StringComparison.OrdinalIgnoreCase));
        var reviewCount = snapshot.TunePlan.Count(item =>
            item.Category is TunePlanCategory.Review or TunePlanCategory.UserChoice);
        var optionalCount = snapshot.TunePlan.Count(item => item.Category == TunePlanCategory.Optional);
        var adminRequiredCount = snapshot.TunePlan.Count(item => item.Category == TunePlanCategory.AdminRequired);
        var toolProbeIssueCount = snapshot.Tune.ToolProbes.Count(tool =>
            !tool.Status.Equals(ProbeState.Succeeded.ToString(), StringComparison.OrdinalIgnoreCase));
        var storageHotspotBytes = snapshot.Tune.StorageHotspots.Sum(item => item.Bytes ?? 0);
        var topProcessMemoryBytes = snapshot.Tune.TopProcesses.Take(5).Sum(process => process.WorkingSetBytes);

        var score = 100;
        score -= reviewCount * 7;
        score -= optionalCount * 3;
        score -= adminRequiredCount * 6;
        score -= Math.Max(0, startupEnabledCount - 6) * 2;
        score -= toolProbeIssueCount * 2;

        if (snapshot.Machine.SystemDriveFreeBytes > 0 &&
            snapshot.Machine.SystemDriveFreeBytes < 75L * 1024 * 1024 * 1024)
        {
            score -= 8;
        }

        score = Math.Clamp(score, 0, 100);

        return new ReadinessSummary
        {
            Score = score,
            Rating = score >= 85 ? "Ready" : score >= 70 ? "Review" : "Attention",
            ReviewCount = reviewCount,
            OptionalCount = optionalCount,
            AdminRequiredCount = adminRequiredCount,
            StartupEnabledCount = startupEnabledCount,
            StartupTotalCount = snapshot.Tune.StartupEntries.Count,
            AutomaticServiceCount = automaticServiceCount,
            ToolProbeIssueCount = toolProbeIssueCount,
            StorageHotspotBytes = storageHotspotBytes,
            TopProcessMemoryBytes = topProcessMemoryBytes,
            Highlights = BuildHighlights(snapshot, score, reviewCount, adminRequiredCount, startupEnabledCount)
        };
    }

    private static List<string> BuildHighlights(
        AuditSnapshot snapshot,
        int score,
        int reviewCount,
        int adminRequiredCount,
        int startupEnabledCount)
    {
        var highlights = new List<string>
        {
            "Read-only Store V1: no settings, services, startup entries, apps, or files were changed."
        };

        highlights.Add(score >= 85
            ? "This machine looks ready for a client-readiness review."
            : "This machine has review items worth walking through before workflow or AI rollout work.");

        if (reviewCount > 0)
        {
            highlights.Add($"{reviewCount} tune-plan items need review or user choice.");
        }

        if (adminRequiredCount > 0)
        {
            highlights.Add($"{adminRequiredCount} item(s) are admin-bound and intentionally left as guidance.");
        }

        if (startupEnabledCount > 0)
        {
            highlights.Add($"{startupEnabledCount} startup item(s) appear enabled or unknown.");
        }

        if (snapshot.Tune.StorageHotspots.Count > 0)
        {
            highlights.Add($"{snapshot.Tune.StorageHotspots.Count} storage hotspot(s) were measured for review.");
        }

        return highlights;
    }

    private static bool IsEnabled(StartupEntry entry) =>
        entry.State.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ||
        entry.State.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
}
