using System.Text.RegularExpressions;
using Microsoft.Win32;
using Windows.Management.Deployment;

namespace AppLens.Backend;

public sealed class InventoryCollector
{
    private static readonly Regex[] RuntimePatterns =
    [
        new(@"\.NET", RegexOptions.IgnoreCase),
        new(@"Visual C\+\+", RegexOptions.IgnoreCase),
        new(@"Java\(TM\)|Java SE|Oracle Java", RegexOptions.IgnoreCase),
        new(@"Python \d", RegexOptions.IgnoreCase),
        new(@"Node\.js", RegexOptions.IgnoreCase),
        new(@"PowerShell \d", RegexOptions.IgnoreCase),
        new(@"Windows Software Development Kit|Windows SDK|MSVC", RegexOptions.IgnoreCase)
    ];

    private static readonly Regex[] ExcludePatterns =
    [
        new(@"^Update for |^Security Update|^Hotfix for|^Service Pack", RegexOptions.IgnoreCase),
        new(@"KB\d{6,}", RegexOptions.IgnoreCase),
        new(@"^\{[0-9A-F-]+\}$", RegexOptions.IgnoreCase),
        new(@"^Windows Driver|Driver Update", RegexOptions.IgnoreCase),
        new(@"NVIDIA (Graphics Driver|GeForce|FrameView|HD Audio|PhysX|Update|nView|Install Application)", RegexOptions.IgnoreCase),
        new(@"AMD Software|AMD Chipset|Realtek |Python Launcher", RegexOptions.IgnoreCase),
        new(@"AI ?Framework ?Service|AI ?Image ?Agent|GlideX Service|StoryCube Service|ProArt Creator Hub Service", RegexOptions.IgnoreCase),
        new(@"ASUS .*(Service|Toolkit|Control Panel)", RegexOptions.IgnoreCase),
        new(@"Intel\(R\) (Chipset|Management|Network|Graphics|Serial|USB|Rapid|Optane|Wireless|Wi-Fi|Bluetooth|HID|Trusted)", RegexOptions.IgnoreCase),
        new(@"Broadcom |Synaptics |Conexant |Qualcomm ", RegexOptions.IgnoreCase),
        new(@"Microsoft Update Health Tools|Update for Windows|Microsoft Security Client|Windows Malicious Software Removal Tool", RegexOptions.IgnoreCase)
    ];

    private static readonly Regex[] StoreExcludePatterns =
    [
        new(@"Microsoft\.(VCLibs|UI\.Xaml|NET|Services\.Store|DirectX|Windows\.|DesktopAppInstaller|StorePurchaseApp|WindowsStore)", RegexOptions.IgnoreCase),
        new(@"Microsoft\.(VP9|HEVC|HEIF|WebMedia|RawImage|AV1|Webp)", RegexOptions.IgnoreCase),
        new(@"Microsoft\.(LanguageExperiencePack|MicrosoftEdge|Advertising|Xbox|Gaming|SecHealthUI|Office|OneDriveSync)", RegexOptions.IgnoreCase),
        new(@"MicrosoftWindows\.|windows\.|InputApp|NcsiUwpApp|Realtek|AppUp\.|NVIDIA\.|NVIDIACorp\.|AsusTek\.|ASUS", RegexOptions.IgnoreCase),
        new(@"Microsoft\.(Getstarted|MixedReality|3DBuilder|Print3D|OneConnect|MSPaint|Microsoft3DViewer)", RegexOptions.IgnoreCase),
        new(@"Microsoft\.(Bing|Messaging|ConnectivityStore|PPIProjection|People|WindowsMaps|WindowsCamera|WindowsAlarms)", RegexOptions.IgnoreCase),
        new(@"Microsoft\.(ZuneVideo|ZuneMusic|Wallet|SkypeApp|YourPhone|GetHelp|Cortana|ScreenSketch)", RegexOptions.IgnoreCase),
        new(@"Microsoft\.(WindowsTerminal|Paint|Todos|OutlookForWindows|MicrosoftPCManager|Copilot|DevHome|Photos)", RegexOptions.IgnoreCase),
        new(@"Clipchamp|Dolby|AMDRadeon|ActionsServer|aimgr|WindowsWorkload", RegexOptions.IgnoreCase),
        new(@"^\d+$|^[0-9a-fA-F]{4,}|^[0-9a-fA-F]{8}-", RegexOptions.IgnoreCase)
    ];

    private static readonly Regex[] M365Patterns =
    [
        new(@"^Microsoft (Excel|Word|Outlook|PowerPoint|Access|Publisher|OneNote|OneDrive|Teams|Lync|Visio|Project)(\s|$)", RegexOptions.IgnoreCase),
        new(@"^Microsoft Office", RegexOptions.IgnoreCase),
        new(@"^Microsoft 365", RegexOptions.IgnoreCase)
    ];

    public Task<InventorySummary> CollectAsync(CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var win32Apps = GetWin32Apps(cancellationToken);
            var storeApps = GetStoreApps(cancellationToken);

            var desktopApps = new List<AppEntry>();
            var runtimes = new List<AppEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var app in win32Apps.OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!seen.Add(app.Name) || IsExcluded(app.Name))
                {
                    continue;
                }

                if (IsRuntime(app.Name))
                {
                    runtimes.Add(app);
                }
                else
                {
                    desktopApps.Add(app);
                }
            }

            return new InventorySummary
            {
                DesktopApplications = GroupM365(desktopApps),
                StoreApplications = storeApps,
                RuntimesAndFrameworks = runtimes.OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase).ToList()
            };
        }, cancellationToken);

    private static List<AppEntry> GetWin32Apps(CancellationToken cancellationToken)
    {
        var apps = new List<AppEntry>();
        ReadUninstallKey(apps, RegistryHive.LocalMachine, RegistryView.Registry64, false, cancellationToken);
        ReadUninstallKey(apps, RegistryHive.LocalMachine, RegistryView.Registry32, false, cancellationToken);
        ReadUninstallKey(apps, RegistryHive.CurrentUser, RegistryView.Default, true, cancellationToken);
        return apps;
    }

    private static void ReadUninstallKey(
        List<AppEntry> apps,
        RegistryHive hive,
        RegistryView view,
        bool userInstalled,
        CancellationToken cancellationToken)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
        using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        if (uninstall is null)
        {
            return;
        }

        foreach (var subKeyName in uninstall.GetSubKeyNames())
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var appKey = uninstall.OpenSubKey(subKeyName);
            if (appKey is null)
            {
                continue;
            }

            var name = ReadString(appKey, "DisplayName");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (ReadInt(appKey, "SystemComponent") == 1)
            {
                continue;
            }

            var releaseType = ReadString(appKey, "ReleaseType");
            if (Regex.IsMatch(releaseType, "Update|Hotfix|Security Update|Service Pack", RegexOptions.IgnoreCase))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(ReadString(appKey, "UninstallString")) &&
                string.IsNullOrWhiteSpace(ReadString(appKey, "InstallLocation")))
            {
                continue;
            }

            apps.Add(new AppEntry
            {
                Name = name.Trim(),
                Version = ReadString(appKey, "DisplayVersion").Trim(),
                Publisher = ReadString(appKey, "Publisher").Trim(),
                Source = userInstalled ? "HKCU uninstall" : "HKLM uninstall",
                UserInstalled = userInstalled
            });
        }
    }

    private static List<AppEntry> GetStoreApps(CancellationToken cancellationToken)
    {
        var apps = new List<AppEntry>();
        try
        {
            var packages = new PackageManager().FindPackagesForUser(string.Empty);
            foreach (var package in packages)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = package.Id.Name;
                if (package.IsFramework || StoreExcludePatterns.Any(pattern => pattern.IsMatch(name)))
                {
                    continue;
                }

                var displayName = FriendlyStoreName(name);
                if (string.IsNullOrWhiteSpace(displayName))
                {
                    continue;
                }

                var version = package.Id.Version;
                apps.Add(new AppEntry
                {
                    Name = displayName,
                    Version = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}",
                    Publisher = package.Id.Publisher,
                    Source = "AppX/MSIX"
                });
            }
        }
        catch
        {
            return [];
        }

        return apps
            .GroupBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string FriendlyStoreName(string packageName)
    {
        var friendly = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Microsoft.MicrosoftTo-Do"] = "Microsoft To Do",
            ["Microsoft.Whiteboard"] = "Microsoft Whiteboard",
            ["SpotifyAB.SpotifyMusic"] = "Spotify",
            ["CanonicalGroupLimited.Ubuntu"] = "Ubuntu (WSL)",
            ["PythonSoftwareFoundation.Python"] = "Python",
            ["Anthropic.Claude"] = "Claude Desktop",
            ["OpenAI.Codex"] = "OpenAI Codex",
            ["OpenAI.ChatGPT"] = "ChatGPT"
        };

        foreach (var entry in friendly)
        {
            if (packageName.Contains(entry.Key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }

        var lastSegment = packageName.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? packageName;
        if (Regex.IsMatch(lastSegment, @"^\{|^[a-fA-F0-9]{8}-|^[0-9]+$|^WindowsWorkload", RegexOptions.IgnoreCase))
        {
            return "";
        }

        return Regex.Replace(lastSegment, "([a-z])([A-Z])", "$1 $2");
    }

    private static List<AppEntry> GroupM365(List<AppEntry> apps)
    {
        var m365 = apps.Where(app => M365Patterns.Any(pattern => pattern.IsMatch(app.Name))).OrderBy(app => app.Name).ToList();
        var other = apps.Where(app => !M365Patterns.Any(pattern => pattern.IsMatch(app.Name))).OrderBy(app => app.Name).ToList();
        if (m365.Count == 0)
        {
            return other;
        }

        other.Insert(0, new AppEntry
        {
            Name = "Microsoft 365 (Office)",
            Version = $"{m365.Count} detected apps",
            Publisher = "Microsoft Corporation",
            Source = "Group"
        });
        other.InsertRange(1, m365.Select(app => new AppEntry
        {
            Name = $"  - {app.Name}",
            Version = app.Version,
            Publisher = app.Publisher,
            Source = app.Source,
            UserInstalled = app.UserInstalled
        }));
        return other;
    }

    private static bool IsRuntime(string name) => RuntimePatterns.Any(pattern => pattern.IsMatch(name));

    private static bool IsExcluded(string name) => ExcludePatterns.Any(pattern => pattern.IsMatch(name));

    private static string ReadString(RegistryKey key, string name) => key.GetValue(name)?.ToString() ?? "";

    private static int ReadInt(RegistryKey key, string name) => key.GetValue(name) is int value ? value : 0;
}
