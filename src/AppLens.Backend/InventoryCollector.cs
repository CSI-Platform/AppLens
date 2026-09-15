using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace AppLens.Backend;

public sealed class InventoryCollector
{
    public const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public Task<InventorySummary> CollectAsync(CancellationToken token) => Task.Run(() =>
    {
        var apps = new List<AppEntry>();
        var statuses = new List<ProbeStatus>();
        ReadRegistry(apps, statuses, RegistryHive.LocalMachine, RegistryView.Registry64, token);
        ReadRegistry(apps, statuses, RegistryHive.LocalMachine, RegistryView.Registry32, token);
        ReadRegistry(apps, statuses, RegistryHive.CurrentUser, RegistryView.Default, token);
        ReadPackages(apps, statuses, token);
        token.ThrowIfCancellationRequested();
        var ordered = apps.DistinctBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
        return new InventorySummary
        {
            DesktopApplications = ordered.Where(a => a.Kind == ApplicationKind.Desktop).ToList(),
            StoreApplications = ordered.Where(a => a.Kind == ApplicationKind.Store).ToList(),
            RuntimesAndFrameworks = ordered.Where(a => a.Kind == ApplicationKind.Runtime).ToList(),
            SystemComponents = ordered.Where(a => a.Kind is ApplicationKind.SystemComponent or ApplicationKind.Driver or ApplicationKind.Update).ToList(),
            ProbeStatuses = statuses
        };
    }, token);

    public static AppEntry FromRegistration(RegistrationData data)
    {
        var scope = data.Hive == RegistryHive.CurrentUser ? InstallationScope.ThisUser : InstallationScope.AllUsers;
        var kind = data.SystemComponent ? ApplicationKind.SystemComponent :
            Regex.IsMatch(data.ReleaseType, "Update|Hotfix|Service Pack", RegexOptions.IgnoreCase) ? ApplicationKind.Update :
            data.ReleaseType.Equals("Driver", StringComparison.OrdinalIgnoreCase) ? ApplicationKind.Driver :
            Regex.IsMatch(data.Name, @"^Microsoft (Visual C\+\+|\.NET|Windows Desktop Runtime|ASP\.NET Core|\.NET Framework)", RegexOptions.IgnoreCase)
                ? ApplicationKind.Runtime : ApplicationKind.Desktop;
        var code = data.WindowsInstaller && Guid.TryParse(data.KeyName, out var guid) ? guid.ToString("B").ToUpperInvariant() : "";
        if (data.WindowsInstaller) scope = data.MsiContext switch { 1 or 2 => InstallationScope.ThisUser, 4 => InstallationScope.AllUsers, _ => InstallationScope.Unknown };
        var protectedEntry = data.NoRemove || kind is not ApplicationKind.Desktop;
        var route = protectedEntry ? RemovalRoute.None :
            data.WindowsInstaller && data.MsiContext == 0 ? RemovalRoute.WindowsSettings :
            code.Length > 0 ? RemovalRoute.Msi :
            !string.IsNullOrWhiteSpace(data.UninstallCommand) ? RemovalRoute.Vendor : RemovalRoute.WindowsSettings;
        long? size = data.EstimatedSizeKiB is > 0 and <= (long.MaxValue / 1024) ? data.EstimatedSizeKiB.Value * 1024 : null;
        var date = DateOnly.TryParseExact(data.InstallDate, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
        return new AppEntry
        {
            Id = $"registry:{data.Hive}:{data.View}:{data.KeyName}",
            Name = data.Name.Trim(), Version = data.Version.Trim(), Publisher = data.Publisher.Trim(),
            Source = $"{data.Hive} {data.View}", Scope = scope, Kind = kind,
            ReportedSizeBytes = size, SizeSource = size.HasValue ? "Installer EstimatedSize (KiB)" : "Not reported",
            InstallLocation = data.InstallLocation, InstalledOrServicedDate = date,
            CandidateRoute = route,
            RemovalReason = data.NoRemove ? "Installer marks NoRemove; ordinary removal unavailable." :
                kind != ApplicationKind.Desktop ? "Shared or system component; excluded from ordinary removal." :
                data.WindowsInstaller && data.MsiContext == 0 ? "Windows Installer context unavailable or ambiguous; review in Windows Installed Apps." :
                route == RemovalRoute.WindowsSettings ? "No registered uninstaller; review Windows Installed Apps." :
                "Registered removal route; validation required before use.",
            AdminRequirement = data.MsiContext == 1 ? "Windows Installer managed app; policy may apply" : "May require administrator approval",
            RegistryHive = data.Hive.ToString(), RegistryView = data.View.ToString(), RegistryKey = data.KeyName,
            ProductCode = code, MsiContext = data.MsiContext, UninstallCommand = data.UninstallCommand,
            ScopeEvidence = data.WindowsInstaller ? data.MsiContext switch { 1 => "Windows Installer current-user managed context", 2 => "Windows Installer current-user context", 4 => "Windows Installer machine context", _ => "Windows Installer context unavailable or ambiguous" } : $"Registered in {data.Hive}",
            // Compatibility only; the v1 UI uses installation scope rather than inferring the installer.
            UserInstalled = scope == InstallationScope.ThisUser
        };
    }

    private static void ReadRegistry(List<AppEntry> apps, List<ProbeStatus> statuses,
        RegistryHive hive, RegistryView view, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        var failures = 0;
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = root.OpenSubKey(UninstallKey);
            foreach (var keyName in uninstall?.GetSubKeyNames() ?? [])
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    using var key = uninstall!.OpenSubKey(keyName);
                    if (key is null) continue;
                    var name = Text(key, "DisplayName");
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    apps.Add(FromRegistration(new RegistrationData
                    {
                        Hive = hive, View = view, KeyName = keyName, Name = name,
                        Version = Text(key, "DisplayVersion"), Publisher = Text(key, "Publisher"),
                        InstallLocation = Text(key, "InstallLocation"), InstallDate = Text(key, "InstallDate"),
                        UninstallCommand = Text(key, "UninstallString"), ReleaseType = Text(key, "ReleaseType"),
                        WindowsInstaller = Number(key, "WindowsInstaller") == 1,
                        MsiContext = Number(key, "WindowsInstaller") == 1 ? MsiInstallation.ReadUniqueContext(keyName) : 0,
                        NoRemove = Number(key, "NoRemove") == 1, SystemComponent = Number(key, "SystemComponent") == 1,
                        EstimatedSizeKiB = Number(key, "EstimatedSize")
                    }));
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { failures++; }
            }
            statuses.Add(new ProbeStatus { Name = $"{hive} {view}", State = failures == 0 ? ProbeState.Succeeded : ProbeState.Partial,
                Duration = watch.Elapsed, Message = failures == 0 ? "" : $"{failures} registrations could not be read." });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            statuses.Add(new ProbeStatus { Name = $"{hive} {view}", State = ProbeState.Failed, Message = ex.Message, Duration = watch.Elapsed });
        }
    }

    private static void ReadPackages(List<AppEntry> apps, List<ProbeStatus> statuses, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        var failures = 0;
        try
        {
            foreach (var package in new PackageManager().FindPackagesForUser(""))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var id = package.Id;
                    var kind = package.IsFramework ? ApplicationKind.Runtime :
                        package.IsResourcePackage || package.SignatureKind == PackageSignatureKind.System
                            ? ApplicationKind.SystemComponent : ApplicationKind.Store;
                    string name;
                    try { name = package.DisplayName; } catch { name = id.Name; }
                    if (string.IsNullOrWhiteSpace(name) || name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase)) name = id.Name;
                    string location;
                    try { location = package.InstalledPath; } catch { location = ""; }
                    var version = id.Version;
                    apps.Add(new AppEntry
                    {
                        Id = "msix:" + id.FullName, PackageFullName = id.FullName,
                        Name = name, Version = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}",
                        Publisher = id.Publisher, Source = "AppX/MSIX", Scope = InstallationScope.ThisUser,
                        ScopeEvidence = "Current-user package enumeration",
                        Kind = kind, InstallLocation = location, SizeSource = "Not reported by package metadata",
                        CandidateRoute = kind == ApplicationKind.Store ? RemovalRoute.Msix : RemovalRoute.None,
                        RemovalReason = kind == ApplicationKind.Store ? "Current-user package; Windows eligibility check required." :
                            "Framework, resource, or system-signed package; excluded from ordinary removal.",
                        AdminRequirement = "Windows determines eligibility"
                    });
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { failures++; }
            }
            statuses.Add(new ProbeStatus { Name = "Current-user MSIX packages", State = failures == 0 ? ProbeState.Succeeded : ProbeState.Partial,
                Duration = watch.Elapsed, Message = failures == 0 ? "" : $"{failures} packages could not be read." });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            statuses.Add(new ProbeStatus { Name = "Current-user MSIX packages", State = ProbeState.Failed, Message = ex.Message, Duration = watch.Elapsed });
        }
    }

    private static string Text(RegistryKey key, string name) =>
        key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames)?.ToString() ?? "";
    private static long? Number(RegistryKey key, string name)
    {
        var value = key.GetValue(name);
        // REG_DWORD is unsigned even when .NET exposes it as an Int32.
        return value switch { int number => unchecked((uint)number), long number when number >= 0 => number, _ => null };
    }
}
