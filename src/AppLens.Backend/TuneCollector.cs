using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using Microsoft.Win32;

namespace AppLens.Backend;

public sealed class TuneCollector
{
    private static readonly string[] InterestingServices =
    [
        "WSAIFabricSvc",
        "com.docker.service",
        "vmcompute",
        "HvHost",
        "OneDrive",
        "Ollama",
        "AsusAppService",
        "ASUSOptimization",
        "ASUSSoftwareManager",
        "ASUSSwitch",
        "GlideXService",
        "StoryCubeService"
    ];

    private readonly ProbeRunner _probeRunner;

    public TuneCollector(ProbeRunner probeRunner)
    {
        _probeRunner = probeRunner;
    }

    public Task<MachineSummary> CollectMachineAsync(CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (manufacturer, model, totalMemory) = ReadComputerSystem();
            var systemDrive = DriveInfo.GetDrives().FirstOrDefault(drive =>
                drive.IsReady &&
                string.Equals(Path.GetPathRoot(Environment.SystemDirectory), drive.RootDirectory.FullName, StringComparison.OrdinalIgnoreCase));

            return new MachineSummary
            {
                ComputerName = Environment.MachineName,
                UserName = Environment.UserName,
                OSDescription = RuntimeInformation.OSDescription,
                OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                Manufacturer = manufacturer,
                Model = model,
                TotalMemoryBytes = totalMemory,
                SystemDriveFreeBytes = systemDrive?.AvailableFreeSpace ?? 0
            };
        }, cancellationToken);

    public Task<TuneSummary> CollectTuneAsync(CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var approvalMap = GetStartupApprovalMap();

            return new TuneSummary
            {
                TopProcesses = GetTopProcesses(),
                StartupEntries = GetStartupEntries(approvalMap, cancellationToken),
                Services = GetServices(),
                StorageHotspots = GetStorageHotspots(cancellationToken),
                RepoPlacements = GetRepoPlacements(cancellationToken),
                ToolProbes = GetToolProbes()
            };
        }, cancellationToken);

    private static (string Manufacturer, string Model, long TotalMemory) ReadComputerSystem()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Model, TotalPhysicalMemory FROM Win32_ComputerSystem");
            using var results = searcher.Get();
            foreach (var item in results.Cast<ManagementObject>())
            {
                return (
                    item["Manufacturer"]?.ToString() ?? "",
                    item["Model"]?.ToString() ?? "",
                    long.TryParse(item["TotalPhysicalMemory"]?.ToString(), out var memory) ? memory : 0);
            }
        }
        catch
        {
            // Fall through to runtime defaults.
        }

        return ("", "", 0);
    }

    private static List<ProcessSnapshot> GetTopProcesses()
    {
        return Process.GetProcesses()
            .Select(process =>
            {
                try
                {
                    return new ProcessSnapshot
                    {
                        Name = process.ProcessName,
                        Id = process.Id,
                        WorkingSetBytes = process.WorkingSet64,
                        CpuSeconds = process.TotalProcessorTime.TotalSeconds
                    };
                }
                catch
                {
                    return null;
                }
            })
            .Where(process => process is not null)
            .Cast<ProcessSnapshot>()
            .OrderByDescending(process => process.WorkingSetBytes)
            .Take(15)
            .ToList();
    }

    private static List<StartupEntry> GetStartupEntries(Dictionary<string, string> approvalMap, CancellationToken cancellationToken)
    {
        var entries = new List<StartupEntry>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, Command, Location FROM Win32_StartupCommand");
            using var results = searcher.Get();
            foreach (var item in results.Cast<ManagementObject>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = item["Name"]?.ToString() ?? "";
                var location = item["Location"]?.ToString() ?? "";
                entries.Add(new StartupEntry
                {
                    Name = name,
                    State = GetStartupState(name, location, approvalMap),
                    Location = location,
                    Command = Formatting.OneLine(item["Command"]?.ToString() ?? "", 96)
                });
            }
        }
        catch
        {
            return [];
        }

        return entries.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static Dictionary<string, string> GetStartupApprovalMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in new[]
        {
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32"
        })
        {
            ReadApprovalKey(map, Registry.CurrentUser, $"HKCU:{path}", path);
            ReadApprovalKey(map, Registry.LocalMachine, $"HKLM:{path}", path);
        }

        return map;
    }

    private static void ReadApprovalKey(Dictionary<string, string> map, RegistryKey root, string label, string path)
    {
        using var key = root.OpenSubKey(path);
        if (key is null)
        {
            return;
        }

        foreach (var valueName in key.GetValueNames())
        {
            if (key.GetValue(valueName) is not byte[] bytes || bytes.Length == 0)
            {
                continue;
            }

            map[$"{label}|{valueName}"] = bytes[0] is 0x02 or 0x06 ? "Enabled" : "Disabled";
        }
    }

    private static string GetStartupState(string name, string location, Dictionary<string, string> approvalMap)
    {
        var hive = location.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) ? "HKLM" : "HKCU";
        foreach (var suffix in new[]
        {
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32"
        })
        {
            if (approvalMap.TryGetValue($"{hive}:{suffix}|{name}", out var state))
            {
                return state;
            }
        }

        return "Unknown";
    }

    private static List<ServiceSnapshot> GetServices()
    {
        try
        {
            return ServiceController.GetServices()
                .Where(service => InterestingServices.Contains(service.ServiceName, StringComparer.OrdinalIgnoreCase) ||
                                  InterestingServices.Contains(service.DisplayName, StringComparer.OrdinalIgnoreCase))
                .OrderBy(service => service.ServiceName, StringComparer.OrdinalIgnoreCase)
                .Select(service => new ServiceSnapshot
                {
                    Name = service.ServiceName,
                    DisplayName = service.DisplayName,
                    Status = service.Status.ToString(),
                    StartType = service.StartType.ToString()
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static List<StorageHotspot> GetStorageHotspots(CancellationToken cancellationToken)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var targets = new (string Label, string Path)[]
        {
            (".ollama", Path.Combine(userProfile, ".ollama")),
            (".codex", Path.Combine(userProfile, ".codex")),
            (".claude", Path.Combine(userProfile, ".claude")),
            (@"LocalAppData\Temp", Path.Combine(localAppData, "Temp")),
            (@"LocalAppData\Docker", Path.Combine(localAppData, "Docker")),
            (@"LocalAppData\Packages", Path.Combine(localAppData, "Packages")),
            (@"LocalAppData\pip\Cache", Path.Combine(localAppData, "pip", "Cache")),
            (@"LocalAppData\NuGet\Cache", Path.Combine(localAppData, "NuGet", "Cache")),
            (@"LocalAppData\uv\cache", Path.Combine(localAppData, "uv", "cache")),
            (@"LocalAppData\Yarn\Cache", Path.Combine(localAppData, "Yarn", "Cache")),
            (@"Roaming\npm-cache", Path.Combine(appData, "npm-cache")),
            (@"Roaming\Code\Cache", Path.Combine(appData, "Code", "Cache")),
            (@"ProgramData\chocolatey\lib-bad", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "chocolatey", "lib-bad"))
        };

        return targets
            .Where(target => Directory.Exists(target.Path))
            .Select(target => new StorageHotspot
            {
                Location = target.Label,
                Path = target.Path,
                Bytes = DirectorySize(target.Path, cancellationToken)
            })
            .OrderByDescending(target => target.Bytes ?? 0)
            .ToList();
    }

    private static long DirectorySize(string root, CancellationToken cancellationToken)
    {
        long total = 0;
        var visited = 0;
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0 && visited < 50_000)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            visited++;
            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        total += new FileInfo(file).Length;
                    }
                    catch
                    {
                        // Ignore inaccessible files.
                    }
                }

                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    pending.Push(directory);
                }
            }
            catch
            {
                // Ignore inaccessible directories.
            }
        }

        return total;
    }

    private static List<RepoPlacement> GetRepoPlacements(CancellationToken cancellationToken)
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new[]
        {
            Path.Combine(userProfile, "OneDrive", "Documents"),
            Path.Combine(userProfile, "source"),
            Path.Combine(userProfile, "Projects"),
            @"C:\Dev"
        };

        return roots
            .Where(Directory.Exists)
            .Select(root => CountRepos(root, cancellationToken))
            .ToList();
    }

    private static RepoPlacement CountRepos(string root, CancellationToken cancellationToken)
    {
        var count = 0;
        var sample = "";
        var truncated = false;
        var rootDepth = root.TrimEnd(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar).Length;
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            var depth = current.TrimEnd(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar).Length - rootDepth;
            if (depth > 5)
            {
                continue;
            }

            try
            {
                if (Directory.Exists(Path.Combine(current, ".git")))
                {
                    count++;
                    sample = sample.Length == 0 ? current : sample;
                    if (count >= 20)
                    {
                        truncated = true;
                        break;
                    }
                }

                foreach (var directory in Directory.EnumerateDirectories(current))
                {
                    var name = Path.GetFileName(directory);
                    if (name is ".git" or "node_modules" or ".venv" or "venv")
                    {
                        continue;
                    }

                    pending.Push(directory);
                }
            }
            catch
            {
                // Ignore inaccessible paths.
            }
        }

        return new RepoPlacement
        {
            Root = root,
            RepoCount = count,
            Sample = sample,
            Truncated = truncated
        };
    }

    private List<ToolProbe> GetToolProbes() =>
    [
        _probeRunner.RunTool("WSL Status", "wsl.exe", "--status", TimeSpan.FromSeconds(8)),
        _probeRunner.RunTool("WSL Distros", "wsl.exe", "-l -v", TimeSpan.FromSeconds(8)),
        _probeRunner.RunTool("Docker Summary", "docker", "system df", TimeSpan.FromSeconds(8)),
        _probeRunner.RunTool("Ollama Summary", "ollama", "list", TimeSpan.FromSeconds(8))
    ];
}
