using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;
using Microsoft.Win32;

namespace AppLens.Backend;

public sealed class TuneActionExecutor
{
    private readonly ITuneActionRuntime _runtime;

    public TuneActionExecutor()
        : this(new WindowsTuneActionRuntime())
    {
    }

    public TuneActionExecutor(ITuneActionRuntime runtime)
    {
        _runtime = runtime;
    }

    public async Task<TuneActionRecord> ExecuteAsync(
        TunePlanItem item,
        bool userApproved,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.Now;
        var action = item.ProposedAction;

        if (!IsExecutable(action.ExecutionState, action.Kind))
        {
            return Record(item, TuneActionStatus.Blocked, startedAt, "This tune-plan item is not executable in this build.");
        }

        if (!userApproved)
        {
            return Record(item, TuneActionStatus.Blocked, startedAt, "Action blocked because Tune consent was not granted.");
        }

        if ((item.RequiresAdmin || RequiresAdmin(action.ExecutionState)) && !_runtime.IsAdministrator)
        {
            return Record(item, TuneActionStatus.Blocked, startedAt, "Action requires an elevated AppLens-Tune session.");
        }

        try
        {
            return action.Kind switch
            {
                ProposedActionKind.ClearRebuildableCache => await ClearRebuildableCacheAsync(item, startedAt, cancellationToken)
                    .ConfigureAwait(false),
                ProposedActionKind.SetServiceManual => await SetServiceManualAsync(item, startedAt, cancellationToken)
                    .ConfigureAwait(false),
                ProposedActionKind.StopService => await StopServiceAsync(item, startedAt, cancellationToken)
                    .ConfigureAwait(false),
                ProposedActionKind.DisableStartup => await DisableStartupAsync(item, startedAt, cancellationToken)
                    .ConfigureAwait(false),
                ProposedActionKind.EnableStartup => await EnableStartupAsync(item, startedAt, cancellationToken)
                    .ConfigureAwait(false),
                _ => Record(item, TuneActionStatus.Blocked, startedAt, "This action kind is not executable in this build.")
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Record(item, TuneActionStatus.Failed, startedAt, ex.Message);
        }
    }

    private async Task<TuneActionRecord> ClearRebuildableCacheAsync(
        TunePlanItem item,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var path = item.ProposedAction.Target;
        if (!ActionTargetPolicy.IsClearableCacheTarget(path))
        {
            return Record(item, TuneActionStatus.Blocked, startedAt, "Cache cleanup target is outside AppLens-Tune's allowlist.");
        }

        var bytesDeleted = await _runtime.ClearDirectoryContentsAsync(path, cancellationToken).ConfigureAwait(false);
        return Record(item, TuneActionStatus.Succeeded, startedAt, $"Cleared {Formatting.Size(bytesDeleted)} from rebuildable cache contents.");
    }

    private async Task<TuneActionRecord> SetServiceManualAsync(
        TunePlanItem item,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        await _runtime.SetServiceStartModeManualAsync(item.ProposedAction.Target, cancellationToken).ConfigureAwait(false);
        return Record(item, TuneActionStatus.Succeeded, startedAt, "Service start mode was set to Manual.");
    }

    private async Task<TuneActionRecord> StopServiceAsync(
        TunePlanItem item,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        await _runtime.StopServiceAsync(item.ProposedAction.Target, cancellationToken).ConfigureAwait(false);
        return Record(item, TuneActionStatus.Succeeded, startedAt, "Service was stopped.");
    }

    private async Task<TuneActionRecord> DisableStartupAsync(
        TunePlanItem item,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        await _runtime.DisableStartupEntryAsync(
                item.ProposedAction.Target,
                item.ProposedAction.TargetContext,
                cancellationToken)
            .ConfigureAwait(false);
        return Record(item, TuneActionStatus.Succeeded, startedAt, "Startup entry was disabled.");
    }

    private async Task<TuneActionRecord> EnableStartupAsync(
        TunePlanItem item,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        await _runtime.EnableStartupEntryAsync(
                item.ProposedAction.Target,
                item.ProposedAction.TargetContext,
                cancellationToken)
            .ConfigureAwait(false);
        return Record(item, TuneActionStatus.Succeeded, startedAt, "Startup entry was enabled.");
    }

    private static TuneActionRecord Record(
        TunePlanItem item,
        TuneActionStatus status,
        DateTimeOffset startedAt,
        string message)
    {
        return new TuneActionRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            PlanItemId = item.Id,
            Kind = item.ProposedAction.Kind,
            Status = status,
            Target = item.ProposedAction.Target,
            Message = message,
            BackupDetail = item.BackupPlan,
            VerificationStep = item.VerificationStep,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.Now,
            RequiresAdmin = item.RequiresAdmin || RequiresAdmin(item.ProposedAction.ExecutionState)
        };
    }

    private static bool IsExecutable(TunePlanExecutionState state, ProposedActionKind kind) =>
        kind is not ProposedActionKind.None and not ProposedActionKind.ManualReview and not ProposedActionKind.MoveRepo and not ProposedActionKind.UninstallApplication &&
        state is TunePlanExecutionState.ReadyToRun
            or TunePlanExecutionState.RequiresUserConsent
            or TunePlanExecutionState.RequiresAdmin;

    private static bool RequiresAdmin(TunePlanExecutionState state) =>
        state is TunePlanExecutionState.RequiresAdmin or TunePlanExecutionState.FutureAdminRequired;
}

public interface ITuneActionRuntime
{
    bool IsAdministrator { get; }

    Task<long> ClearDirectoryContentsAsync(string path, CancellationToken cancellationToken = default);

    Task SetServiceStartModeManualAsync(string serviceName, CancellationToken cancellationToken = default);

    Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default);

    Task DisableStartupEntryAsync(string entryName, string location, CancellationToken cancellationToken = default);

    Task EnableStartupEntryAsync(string entryName, string location, CancellationToken cancellationToken = default);
}

public sealed class WindowsTuneActionRuntime : ITuneActionRuntime
{
    public bool IsAdministrator
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public Task<long> ClearDirectoryContentsAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            if (!Directory.Exists(path))
            {
                return 0L;
            }

            var bytes = 0L;
            foreach (var file in Directory.EnumerateFiles(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    bytes += new FileInfo(file).Length;
                    File.Delete(file);
                }
                catch
                {
                    // Keep cleanup best-effort; inaccessible files should not abort the whole tune run.
                }
            }

            foreach (var directory in Directory.EnumerateDirectories(path))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var info = new DirectoryInfo(directory);
                    if ((info.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                    {
                        continue;
                    }

                    bytes += DirectorySize(directory, cancellationToken);
                    Directory.Delete(directory, recursive: true);
                }
                catch
                {
                    // Keep cleanup best-effort for locked cache trees.
                }
            }

            return bytes;
        }, cancellationToken);

    public async Task SetServiceStartModeManualAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new InvalidOperationException("Service name is missing.");
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("sc.exe")
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("config");
        process.StartInfo.ArgumentList.Add(serviceName);
        process.StartInfo.ArgumentList.Add("start=");
        process.StartInfo.ArgumentList.Add("demand");

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim());
        }
    }

    public Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            using var service = new ServiceController(serviceName);
            if (service.Status == ServiceControllerStatus.Stopped)
            {
                return;
            }

            if (!service.CanStop)
            {
                throw new InvalidOperationException("Service does not report that it can be stopped.");
            }

            service.Stop(stopDependentServices: false);
            service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(20));
        }, cancellationToken);

    public Task DisableStartupEntryAsync(string entryName, string location, CancellationToken cancellationToken = default) =>
        SetStartupEntryStateAsync(entryName, location, enabled: false, cancellationToken);

    public Task EnableStartupEntryAsync(string entryName, string location, CancellationToken cancellationToken = default) =>
        SetStartupEntryStateAsync(entryName, location, enabled: true, cancellationToken);

    private static Task SetStartupEntryStateAsync(string entryName, string location, bool enabled, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(entryName))
            {
                throw new InvalidOperationException("Startup entry name is missing.");
            }

            var root = location.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase)
                ? Registry.LocalMachine
                : Registry.CurrentUser;

            foreach (var path in new[]
            {
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32"
            })
            {
                using var key = root.OpenSubKey(path, writable: true);
                if (key?.GetValue(entryName) is not byte[] bytes || bytes.Length == 0)
                {
                    continue;
                }

                bytes[0] = enabled ? (byte)0x02 : (byte)0x03;
                key.SetValue(entryName, bytes, RegistryValueKind.Binary);
                return;
            }

            throw new InvalidOperationException("Startup approval entry was not found.");
        }, cancellationToken);

    private static long DirectorySize(string root, CancellationToken cancellationToken)
    {
        var bytes = 0L;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                bytes += new FileInfo(file).Length;
            }
            catch
            {
                // Ignore inaccessible cache files while estimating cleanup.
            }
        }

        return bytes;
    }
}

public static class ActionTargetPolicy
{
    public static bool IsClearableCacheTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fullPath = Normalize(path);
        return ClearableCacheRoots().Any(root => IsSameOrChild(fullPath, Normalize(root)));
    }

    private static IEnumerable<string> ClearableCacheRoots()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        yield return Path.GetTempPath();
        yield return Path.Combine(localAppData, "Temp");
        yield return Path.Combine(localAppData, "pip", "Cache");
        yield return Path.Combine(localAppData, "NuGet", "Cache");
        yield return Path.Combine(localAppData, "uv", "cache");
        yield return Path.Combine(localAppData, "Yarn", "Cache");
        yield return Path.Combine(appData, "npm-cache");
        yield return Path.Combine(appData, "Code", "Cache");
        yield return Path.Combine(programData, "chocolatey", "lib-bad");
    }

    private static bool IsSameOrChild(string path, string root)
    {
        var normalizedPath = EnsureTrailingSeparator(path);
        var normalizedRoot = EnsureTrailingSeparator(root);
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
}
