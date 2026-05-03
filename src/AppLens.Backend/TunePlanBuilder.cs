using System.Security.Cryptography;
using System.Text;

namespace AppLens.Backend;

public sealed class TunePlanBuilder
{
    public List<TunePlanItem> Build(AuditSnapshot snapshot)
    {
        var items = new List<TunePlanItem>();

        foreach (var finding in snapshot.Findings)
        {
            var item = FromFinding(finding);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        AddStartupPlanItems(snapshot, items);
        AddServicePlanItems(snapshot, items);
        AddLocalAiPlanItem(snapshot, items);

        return items
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(item => item.Category == TunePlanCategory.Keep ? 1 : 0)
            .ThenByDescending(item => item.Risk)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static TunePlanItem? FromFinding(Finding finding)
    {
        if (finding.Category == FindingCategory.Privacy)
        {
            return new TunePlanItem
            {
                Id = StableId(finding.Category.ToString(), finding.Title, finding.Detail),
                Category = TunePlanCategory.Keep,
                Risk = TunePlanRisk.Info,
                Title = finding.Title,
                Evidence = finding.Detail,
                Guidance = "Keep AppLens-Tune in read-only mode for V1. Exports remain user-controlled.",
                BackupPlan = "No backup needed because no system changes are made.",
                VerificationStep = "Confirm the exported report says the scan was read-only.",
                ProposedAction = new ProposedAction
                {
                    Kind = ProposedActionKind.None,
                    ExecutionState = TunePlanExecutionState.ReadOnlyOnly,
                    Description = "No action."
                }
            };
        }

        return finding.Category switch
        {
            FindingCategory.Startup => StartupFinding(finding),
            FindingCategory.Services => ServiceFinding(finding),
            FindingCategory.Storage => StorageFinding(finding),
            FindingCategory.RepoPlacement => RepoFinding(finding),
            FindingCategory.Tooling => ReviewFinding(finding, ProposedActionKind.ManualReview),
            FindingCategory.Stability => ReviewFinding(finding, ProposedActionKind.ManualReview),
            _ => ReviewFinding(finding, ProposedActionKind.ManualReview)
        };
    }

    private static TunePlanItem StartupFinding(Finding finding)
    {
        var isStable = finding.Severity == FindingSeverity.Stable;
        return new TunePlanItem
        {
            Id = StableId(finding.Category.ToString(), finding.Title, finding.Detail),
            Category = isStable ? TunePlanCategory.Keep : TunePlanCategory.UserChoice,
            Risk = isStable ? TunePlanRisk.Info : TunePlanRisk.Low,
            Title = finding.Title,
            Evidence = finding.Detail,
            Guidance = isStable
                ? "No change needed. Keep this startup state unless the user intentionally re-enables it."
                : "Review whether this app needs to launch at sign-in. If not, disable startup through Windows Settings or the app's own preferences.",
            BackupPlan = "Before any future automated change, record the startup entry name, command, location, and approval state.",
            VerificationStep = "Re-scan after the next sign-in and confirm the startup entry state matches the user's choice.",
            ProposedAction = new ProposedAction
            {
                Kind = isStable ? ProposedActionKind.None : ProposedActionKind.DisableStartup,
                ExecutionState = isStable ? TunePlanExecutionState.ReadOnlyOnly : TunePlanExecutionState.FutureUserConsent,
                Target = finding.Title,
                Description = isStable ? "No action." : "Future approved action: disable the startup entry without uninstalling the app."
            }
        };
    }

    private static TunePlanItem ServiceFinding(Finding finding)
    {
        return new TunePlanItem
        {
            Id = StableId(finding.Category.ToString(), finding.Title, finding.Detail),
            Category = TunePlanCategory.AdminRequired,
            Risk = TunePlanRisk.Medium,
            Title = finding.Title,
            Evidence = finding.Detail,
            Guidance = "Review the related service or vendor utility. V1 does not stop services or change start modes.",
            RequiresAdmin = true,
            BackupPlan = "Before any future action, record service name, display name, current status, and start type.",
            VerificationStep = "After any future approved change, reboot or sign out and confirm the service state and related processes.",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.SetServiceManual,
                ExecutionState = TunePlanExecutionState.FutureAdminRequired,
                Target = finding.Title,
                Description = "Future approved action: change selected non-critical services to Manual when an admin permits it."
            }
        };
    }

    private static TunePlanItem StorageFinding(Finding finding)
    {
        return new TunePlanItem
        {
            Id = StableId(finding.Category.ToString(), finding.Title, finding.Detail),
            Category = TunePlanCategory.Optional,
            Risk = TunePlanRisk.Low,
            Title = finding.Title,
            Evidence = finding.Detail,
            Guidance = "Review whether the storage is rebuildable cache, model data, or user-owned files before deleting anything.",
            BackupPlan = "Before any future cleanup, list the path, measured size, and whether the app can rebuild the contents.",
            VerificationStep = "Re-scan storage hotspots and confirm free-space change after cleanup.",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.ClearRebuildableCache,
                ExecutionState = TunePlanExecutionState.FutureUserConsent,
                Target = finding.Title,
                Description = "Future approved action: clear only confirmed rebuildable cache locations."
            }
        };
    }

    private static TunePlanItem RepoFinding(Finding finding)
    {
        return new TunePlanItem
        {
            Id = StableId(finding.Category.ToString(), finding.Title, finding.Detail),
            Category = TunePlanCategory.Review,
            Risk = TunePlanRisk.Medium,
            Title = finding.Title,
            Evidence = finding.Detail,
            Guidance = "Move active development repos out of synced folders when the client uses Git, build tools, or local agents heavily.",
            BackupPlan = "Before moving a repo, confirm all work is committed or backed up and note the current path.",
            VerificationStep = "Re-scan repo placement and confirm active repos are under a local non-synced dev root.",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.MoveRepo,
                ExecutionState = TunePlanExecutionState.ReadOnlyOnly,
                Target = finding.Title,
                Description = "Manual guidance only. Moving repos should stay explicit and user-controlled."
            }
        };
    }

    private static TunePlanItem ReviewFinding(Finding finding, ProposedActionKind actionKind)
    {
        return new TunePlanItem
        {
            Id = StableId(finding.Category.ToString(), finding.Title),
            Category = finding.Severity == FindingSeverity.Optional ? TunePlanCategory.Optional : TunePlanCategory.Review,
            Risk = finding.Severity == FindingSeverity.Optional ? TunePlanRisk.Low : TunePlanRisk.Medium,
            Title = finding.Title,
            Evidence = finding.Detail,
            Guidance = "Review this item with the user before deciding whether it belongs in the workstation baseline.",
            BackupPlan = "Record the current state before making any manual change.",
            VerificationStep = "Re-scan and compare the finding after any manual change.",
            ProposedAction = new ProposedAction
            {
                Kind = actionKind,
                ExecutionState = TunePlanExecutionState.ReadOnlyOnly,
                Target = finding.Title,
                Description = "Read-only guidance."
            }
        };
    }

    private static void AddStartupPlanItems(AuditSnapshot snapshot, List<TunePlanItem> items)
    {
        var enabledStartupCount = snapshot.Tune.StartupEntries.Count(IsEnabled);
        if (enabledStartupCount < 8)
        {
            return;
        }

        items.Add(new TunePlanItem
        {
            Id = "startup-enabled-volume",
            Category = TunePlanCategory.Review,
            Risk = TunePlanRisk.Low,
            Title = "Many startup entries are enabled",
            Evidence = $"{enabledStartupCount} startup entries are currently enabled or appear enabled.",
            Guidance = "Use this as a review queue, not an automatic cleanup list. Confirm app purpose before disabling startup.",
            BackupPlan = "Before any future automated change, export startup entry names, commands, locations, and approval states.",
            VerificationStep = "Re-scan after next sign-in and compare enabled startup count.",
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.DisableStartup,
                ExecutionState = TunePlanExecutionState.FutureUserConsent,
                Description = "Future approved action: disable selected startup entries only after user confirmation."
            }
        });
    }

    private static void AddServicePlanItems(AuditSnapshot snapshot, List<TunePlanItem> items)
    {
        var automaticReviewServices = snapshot.Tune.Services
            .Where(service => service.StartType.Contains("Automatic", StringComparison.OrdinalIgnoreCase))
            .Where(service => IsReviewableService(service.Name) || IsReviewableService(service.DisplayName))
            .Take(8)
            .ToList();

        foreach (var service in automaticReviewServices)
        {
            items.Add(new TunePlanItem
            {
                Id = StableId("service", service.Name),
                Category = TunePlanCategory.AdminRequired,
                Risk = TunePlanRisk.Medium,
                Title = $"Review automatic service: {service.DisplayName}",
                Evidence = $"{service.Name} is {service.Status} with start type {service.StartType}.",
                Guidance = "Review whether this vendor or developer service needs to auto-start for this user's role.",
                RequiresAdmin = true,
                BackupPlan = "Record the service name, status, and start type before any future change.",
                VerificationStep = "After any future approved change, reboot and confirm the service did not restart unexpectedly.",
                ProposedAction = new ProposedAction
                {
                    Kind = ProposedActionKind.SetServiceManual,
                    ExecutionState = TunePlanExecutionState.FutureAdminRequired,
                    Target = service.Name,
                    Description = "Future approved action: set selected non-critical service to Manual."
                }
            });
        }
    }

    private static void AddLocalAiPlanItem(AuditSnapshot snapshot, List<TunePlanItem> items)
    {
        var profile = snapshot.Tune.LocalAiProfile;
        if (profile.Readiness == LocalAiReadiness.Unknown)
        {
            return;
        }

        items.Add(new TunePlanItem
        {
            Id = "local-ai-autoresearch-profile",
            Category = profile.TrainingReady ? TunePlanCategory.Optional : TunePlanCategory.Review,
            Risk = profile.TrainingReady ? TunePlanRisk.Medium : TunePlanRisk.Low,
            Title = "Local autoresearch profile",
            Evidence = $"{profile.Readiness}: {profile.WorkloadClass}",
            Guidance = $"{profile.RecommendedRuntime} Keep training jobs manual-gated until the user approves scope, model, and stop conditions.",
            BackupPlan = "Before future training, save the model path, benchmark output, dataset location, and run manifest.",
            VerificationStep = profile.TrainingGate,
            ProposedAction = new ProposedAction
            {
                Kind = ProposedActionKind.ManualReview,
                ExecutionState = TunePlanExecutionState.ReadOnlyOnly,
                Description = "Read-only guidance: benchmark and plan autoresearch jobs; do not start training automatically."
            }
        });
    }

    private static bool IsEnabled(StartupEntry entry) =>
        entry.State.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ||
        entry.State.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

    private static bool IsReviewableService(string value) =>
        value.Contains("ASUS", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("GlideX", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("StoryCube", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Docker", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("Ollama", StringComparison.OrdinalIgnoreCase);

    private static string StableId(params string[] parts)
    {
        var text = string.Join("|", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes[..6]).ToLowerInvariant();
    }
}
