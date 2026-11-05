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

        var enabledStartupCount = tune.StartupEntries.Count(IsEnabledStartup);
        if (enabledStartupCount >= 8)
        {
            findings.Add(new Finding
            {
                Severity = FindingSeverity.Review,
                Category = FindingCategory.Startup,
                Title = "Startup queue needs review",
                Detail = $"{enabledStartupCount} startup entries are enabled or have unknown approval state."
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

        var reviewableAutomaticServices = tune.Services
            .Where(service => service.StartType.Contains("Automatic", StringComparison.OrdinalIgnoreCase))
            .Where(service => IsReviewableService(service.Name) || IsReviewableService(service.DisplayName))
            .Select(service => service.DisplayName.Length == 0 ? service.Name : service.DisplayName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (reviewableAutomaticServices.Count > 0)
        {
            findings.Add(new Finding
            {
                Severity = FindingSeverity.Review,
                Category = FindingCategory.Services,
                Title = "Automatic vendor or developer services need review",
                Detail = string.Join(", ", reviewableAutomaticServices.Take(6))
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

        var largeProcesses = tune.TopProcesses
            .Where(process => process.WorkingSetBytes >= 1_500_000_000)
            .Select(process => $"{process.Name} ({Formatting.Size(process.WorkingSetBytes)})")
            .ToList();

        if (largeProcesses.Count > 0)
        {
            findings.Add(new Finding
            {
                Severity = FindingSeverity.Optional,
                Category = FindingCategory.Stability,
                Title = "Large memory consumers are visible",
                Detail = string.Join(", ", largeProcesses.Take(5))
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

            if (hotspot.Location.Contains("Docker", StringComparison.OrdinalIgnoreCase) &&
                hotspot.Bytes >= 10L * 1024 * 1024 * 1024)
            {
                findings.Add(new Finding
                {
                    Severity = FindingSeverity.Optional,
                    Category = FindingCategory.Storage,
                    Title = "Large Docker local data",
                    Detail = $"{hotspot.Location} is using {Formatting.Size(hotspot.Bytes)}."
                });
            }

            if (hotspot.Location.Contains("Packages", StringComparison.OrdinalIgnoreCase) &&
                hotspot.Bytes >= 20L * 1024 * 1024 * 1024)
            {
                findings.Add(new Finding
                {
                    Severity = FindingSeverity.Review,
                    Category = FindingCategory.Storage,
                    Title = "Large local packages footprint",
                    Detail = $"{hotspot.Location} is using {Formatting.Size(hotspot.Bytes)}."
                });
            }

            if ((hotspot.Location.Contains("Cache", StringComparison.OrdinalIgnoreCase) ||
                 hotspot.Location.Contains("cache", StringComparison.OrdinalIgnoreCase)) &&
                hotspot.Bytes >= 5L * 1024 * 1024 * 1024)
            {
                findings.Add(new Finding
                {
                    Severity = FindingSeverity.Optional,
                    Category = FindingCategory.Storage,
                    Title = "Large developer cache",
                    Detail = $"{hotspot.Location} is using {Formatting.Size(hotspot.Bytes)}."
                });
            }
        }

        var probeIssues = tune.ToolProbes
            .Where(probe => !probe.Status.Equals(ProbeState.Succeeded.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(probe => $"{probe.Name}: {probe.Status}")
            .ToList();

        if (probeIssues.Count > 0)
        {
            findings.Add(new Finding
            {
                Severity = FindingSeverity.Optional,
                Category = FindingCategory.Tooling,
                Title = "Some dev-tool probes were unavailable",
                Detail = string.Join(", ", probeIssues)
            });
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

    private static bool IsEnabledStartup(StartupEntry entry) =>
        entry.State.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ||
        entry.State.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

    private static bool IsReviewableService(string value) =>
        value.Contains("ASUS", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("GlideX", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("StoryCube", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Docker", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Ollama", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("WSAI", StringComparison.OrdinalIgnoreCase);
}
