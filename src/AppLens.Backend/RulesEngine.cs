namespace AppLens.Backend;

public sealed class RulesEngine
{
    public List<Finding> Evaluate(AuditSnapshot snapshot)
    {
        var findings = new List<Finding>
        {
            new()
            {
                Severity = FindingSeverity.Stable,
                Category = FindingCategory.Privacy,
                Title = "Read-only audit",
                Detail = "AppLens-desktop collected a local snapshot only. No system settings were changed."
            }
        };

        var tune = snapshot.Tune;
        var disabledStartupTargets = tune.StartupEntries.Count(entry =>
            entry.State == "Disabled" &&
            (entry.Name.Contains("Docker Desktop", StringComparison.OrdinalIgnoreCase) ||
             entry.Name.Contains("ChromeAutoLaunch", StringComparison.OrdinalIgnoreCase) ||
             entry.Name.Contains("EdgeAutoLaunch", StringComparison.OrdinalIgnoreCase)));

        if (disabledStartupTargets >= 2)
        {
            findings.Add(new Finding
            {
                Severity = FindingSeverity.Stable,
                Category = FindingCategory.Startup,
                Title = "Common heavy startup entries are disabled",
                Detail = "Docker or browser auto-launch entries appear disabled in the startup-approved snapshot."
            });
        }

        var enabledBrowserLaunchers = tune.StartupEntries
            .Where(entry => entry.State == "Enabled" &&
                            (entry.Name.Contains("ChromeAutoLaunch", StringComparison.OrdinalIgnoreCase) ||
                             entry.Name.Contains("EdgeAutoLaunch", StringComparison.OrdinalIgnoreCase)))
            .Select(entry => entry.Name)
            .ToList();

        if (enabledBrowserLaunchers.Count > 0)
        {
            findings.Add(new Finding
            {
                Severity = FindingSeverity.Review,
                Category = FindingCategory.Startup,
                Title = "Browser auto-launch entries are enabled",
                Detail = string.Join(", ", enabledBrowserLaunchers)
            });
        }

        if (tune.StartupEntries.Any(entry => entry.State == "Enabled" && entry.Name.Equals("Docker Desktop", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(new Finding
            {
                Severity = FindingSeverity.Review,
                Category = FindingCategory.Startup,
                Title = "Docker Desktop starts at sign-in",
                Detail = "Docker startup can be useful for developers, but it is worth validating for non-developer users."
            });
        }

        var asusProcesses = tune.TopProcesses.Count(process => process.Name.Contains("Asus", StringComparison.OrdinalIgnoreCase));
        if (asusProcesses >= 3)
        {
            findings.Add(new Finding
            {
                Severity = FindingSeverity.Review,
                Category = FindingCategory.Services,
                Title = "ASUS background activity is visible",
                Detail = $"{asusProcesses} ASUS processes are present in the top memory snapshot."
            });
        }

        if (tune.RepoPlacements.Any(repo =>
                repo.RepoCount > 0 &&
                repo.Root.Contains("OneDrive", StringComparison.OrdinalIgnoreCase)))
        {
            findings.Add(new Finding
            {
                Severity = FindingSeverity.Review,
                Category = FindingCategory.RepoPlacement,
                Title = "Git repos detected under OneDrive",
                Detail = "Cloud-synced active repos can cause file-locking, sync churn, and development tooling slowdowns."
            });
        }

        foreach (var hotspot in tune.StorageHotspots)
        {
            if (hotspot.Bytes is null)
            {
                continue;
            }

            if (hotspot.Location.Contains(".ollama", StringComparison.OrdinalIgnoreCase) &&
                hotspot.Bytes >= 15L * 1024 * 1024 * 1024)
            {
                findings.Add(new Finding
                {
                    Severity = FindingSeverity.Optional,
                    Category = FindingCategory.Storage,
                    Title = "Large Ollama model cache",
                    Detail = $".ollama is using {Formatting.Size(hotspot.Bytes)}."
                });
            }

            if (hotspot.Location.Contains("Temp", StringComparison.OrdinalIgnoreCase) &&
                hotspot.Bytes >= 2L * 1024 * 1024 * 1024)
            {
                findings.Add(new Finding
                {
                    Severity = FindingSeverity.Optional,
                    Category = FindingCategory.Storage,
                    Title = "Large local temp directory",
                    Detail = $"{hotspot.Location} is using {Formatting.Size(hotspot.Bytes)}."
                });
            }
        }

        if (snapshot.Machine.SystemDriveFreeBytes > 0 &&
            snapshot.Machine.SystemDriveFreeBytes < 100L * 1024 * 1024 * 1024)
        {
            findings.Add(new Finding
            {
                Severity = FindingSeverity.Optional,
                Category = FindingCategory.Storage,
                Title = "System drive free space is below 100 GB",
                Detail = $"Free space: {Formatting.Size(snapshot.Machine.SystemDriveFreeBytes)}."
            });
        }

        return findings;
    }
}
