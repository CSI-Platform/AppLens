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

        var policyBlockedReason = TuneActionPolicy.BlockedReasonFor(action);
        if (!string.IsNullOrWhiteSpace(policyBlockedReason))
        {
            return Record(item, TuneActionStatus.Blocked, startedAt, policyBlockedReason);
        }

        if (RequiresAdmin(item) && !_runtime.IsAdministrator)
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
                ProposedActionKind.CheckLocalLlmHealth => await CheckLocalLlmHealthAsync(item, startedAt, cancellationToken)
                    .ConfigureAwait(false),
                ProposedActionKind.StartLocalLlmServer => await StartLocalLlmServerAsync(item, startedAt, cancellationToken)
                    .ConfigureAwait(false),
                ProposedActionKind.StopLocalLlmServer => await StopLocalLlmServerAsync(item, startedAt, cancellationToken)
                    .ConfigureAwait(false),
                ProposedActionKind.TestSshConnection => await TestSshConnectionAsync(item, startedAt, cancellationToken)
                    .ConfigureAwait(false),
                ProposedActionKind.CheckRemoteLlmHealth => await CheckRemoteLlmHealthAsync(item, startedAt, cancellationToken)
                    .ConfigureAwait(false),
                _ => Record(item, TuneActionStatus.Blocked, startedAt, "This action kind is not executable in this build.")
            };
        }
        catch (NotSupportedException ex)
        {
            return Record(item, TuneActionStatus.Blocked, startedAt, ex.Message);
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
        if (!TuneActionPolicy.IsClearableCacheTarget(path))
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

    private async Task<TuneActionRecord> CheckLocalLlmHealthAsync(
        TunePlanItem item,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var message = await _runtime.CheckLocalLlmHealthAsync(item.ProposedAction.Target, cancellationToken)
            .ConfigureAwait(false);
        return Record(item, TuneActionStatus.Succeeded, startedAt, message);
    }

    private async Task<TuneActionRecord> StartLocalLlmServerAsync(
        TunePlanItem item,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var message = await _runtime.StartLocalLlmServerAsync(item.ProposedAction.Target, cancellationToken)
            .ConfigureAwait(false);
        return Record(item, TuneActionStatus.Succeeded, startedAt, message);
    }

    private async Task<TuneActionRecord> StopLocalLlmServerAsync(
        TunePlanItem item,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var message = await _runtime.StopLocalLlmServerAsync(item.ProposedAction.Target, cancellationToken)
            .ConfigureAwait(false);
        return Record(item, TuneActionStatus.Succeeded, startedAt, message);
    }

    private async Task<TuneActionRecord> TestSshConnectionAsync(
        TunePlanItem item,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var message = await _runtime.TestSshConnectionAsync(item.ProposedAction.Target, cancellationToken)
            .ConfigureAwait(false);
        return Record(item, TuneActionStatus.Succeeded, startedAt, message);
    }

    private async Task<TuneActionRecord> CheckRemoteLlmHealthAsync(
        TunePlanItem item,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        var message = await _runtime.CheckRemoteLlmHealthAsync(item.ProposedAction.Target, cancellationToken)
            .ConfigureAwait(false);
        return Record(item, TuneActionStatus.Succeeded, startedAt, message);
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
            BackupDetail = string.IsNullOrWhiteSpace(item.BackupPlan)
                ? TuneActionPolicy.RestoreGuidanceFor(item.ProposedAction.Kind)
                : item.BackupPlan,
            VerificationStep = item.VerificationStep,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.Now,
            RequiresAdmin = item.RequiresAdmin || RequiresAdmin(item.ProposedAction.ExecutionState)
        };
    }

    private static bool IsExecutable(TunePlanExecutionState state, ProposedActionKind kind) =>
        (kind is ProposedActionKind.ClearRebuildableCache
            or ProposedActionKind.SetServiceManual
            or ProposedActionKind.StopService
            or ProposedActionKind.DisableStartup
            or ProposedActionKind.EnableStartup
            or ProposedActionKind.CheckLocalLlmHealth
            or ProposedActionKind.StartLocalLlmServer
            or ProposedActionKind.StopLocalLlmServer
            or ProposedActionKind.TestSshConnection
            or ProposedActionKind.CheckRemoteLlmHealth) &&
        state is TunePlanExecutionState.ReadyToRun
            or TunePlanExecutionState.RequiresUserConsent
            or TunePlanExecutionState.RequiresAdmin;

    private static bool RequiresAdmin(TunePlanItem item) =>
        item.RequiresAdmin ||
        RequiresAdmin(item.ProposedAction.ExecutionState) ||
        TuneActionPolicy.RequiresAdmin(item.ProposedAction);

    private static bool RequiresAdmin(TunePlanExecutionState state) =>
        state is TunePlanExecutionState.RequiresAdmin or TunePlanExecutionState.FutureAdminRequired;
}

public static class TuneActionPolicy
{
    public static IReadOnlyList<string> StartupRegistryLocationAllowlist { get; } =
    [
        @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        @"HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
        @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce"
    ];

    public static IReadOnlyList<string> ProtectedStartupEntries { get; } =
    [
        "SecurityHealth",
        "OneDrive"
    ];

    public static IReadOnlyList<string> ServiceAllowlistTokens { get; } =
    [
        "ASUS",
        "GlideX",
        "StoryCube",
        "Docker",
        "Ollama"
    ];

    public static IReadOnlyList<string> ClearableCacheRoots => ClearableCacheRootsCore().ToArray();

    public static string BlockedReasonFor(ProposedAction action) =>
        action.Kind switch
        {
            ProposedActionKind.DisableStartup or ProposedActionKind.EnableStartup => StartupBlockedReason(action.Target, action.TargetContext),
            ProposedActionKind.SetServiceManual or ProposedActionKind.StopService => ServiceBlockedReason(action.Target, action.TargetContext),
            ProposedActionKind.ClearRebuildableCache => IsClearableCacheTarget(action.Target)
                ? ""
                : "Cache cleanup target is outside AppLens-Tune's allowlist.",
            ProposedActionKind.CheckLocalLlmHealth => LocalLlmHealthBlockedReason(action.Target, action.TargetContext),
            ProposedActionKind.StartLocalLlmServer => LocalLlmCommandBlockedReason(action.Target, action.TargetContext, "local-llm:start"),
            ProposedActionKind.StopLocalLlmServer => LocalLlmCommandBlockedReason(action.Target, action.TargetContext, "local-llm:stop"),
            ProposedActionKind.TestSshConnection => SshBlockedReason(action.Target, action.TargetContext, "ssh:test-connection"),
            ProposedActionKind.CheckRemoteLlmHealth => SshBlockedReason(action.Target, action.TargetContext, "ssh:remote-llm-health"),
            _ => ""
        };

    public static bool RequiresAdmin(ProposedAction action) =>
        action.Kind is ProposedActionKind.SetServiceManual or ProposedActionKind.StopService ||
        (action.Kind is ProposedActionKind.DisableStartup or ProposedActionKind.EnableStartup &&
         NormalizeRegistryLocation(action.TargetContext).StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase));

    public static bool IsClearableCacheTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            var fullPath = NormalizePath(path);
            return ClearableCacheRootsCore().Any(root => IsSameOrChild(fullPath, NormalizePath(root)));
        }
        catch
        {
            return false;
        }
    }

    public static string RestoreGuidanceFor(ProposedActionKind kind) =>
        kind switch
        {
            ProposedActionKind.DisableStartup => "Re-enable the startup entry from Windows Startup Apps or the app's own settings if needed.",
            ProposedActionKind.EnableStartup => "Disable the startup entry again from Windows Startup Apps or the app's own settings if needed.",
            ProposedActionKind.SetServiceManual => "Record the original service start mode before changing it; restore that start mode if the change causes issues.",
            ProposedActionKind.StopService => "Start the service again if the approved stop action causes issues.",
            ProposedActionKind.ClearRebuildableCache => "Cache cleanup deletes rebuildable contents only; affected apps should recreate cache files as needed.",
            ProposedActionKind.CheckLocalLlmHealth => "Health checks are read-only; no restore action should be needed.",
            ProposedActionKind.StartLocalLlmServer => "Stop the local LLM server if the approved start action causes issues.",
            ProposedActionKind.StopLocalLlmServer => "Restart the local LLM server if the approved stop action causes issues.",
            ProposedActionKind.TestSshConnection => "SSH connection tests are bounded to configured aliases; no restore action should be needed.",
            ProposedActionKind.CheckRemoteLlmHealth => "Remote LLM health checks are bounded to configured SSH aliases; no restore action should be needed.",
            _ => "Record the original state before making changes so it can be restored manually."
        };

    private static string StartupBlockedReason(string entryName, string location)
    {
        if (string.IsNullOrWhiteSpace(entryName))
        {
            return "Startup action target is missing.";
        }

        if (ProtectedStartupEntries.Contains(entryName, StringComparer.OrdinalIgnoreCase))
        {
            return $"Startup entry '{entryName}' is protected and cannot be changed by AppLens-Tune.";
        }

        if (string.IsNullOrWhiteSpace(location) ||
            !StartupRegistryLocationAllowlist.Contains(NormalizeRegistryLocation(location), StringComparer.OrdinalIgnoreCase))
        {
            return "Startup action target is outside AppLens-Tune's registry-location allowlist.";
        }

        return "";
    }

    private static string ServiceBlockedReason(string serviceName, string displayName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            return "Service action target is missing.";
        }

        var evidence = $"{serviceName} {displayName}";
        return ServiceAllowlistTokens.Any(token => evidence.Contains(token, StringComparison.OrdinalIgnoreCase))
            ? ""
            : "Service action target is outside AppLens-Tune's service allowlist.";
    }

    private static string LocalLlmHealthBlockedReason(string endpoint, string context)
    {
        if (!IsAllowedContext(context, "local-llm:health"))
        {
            return "Local LLM action target context is outside AppLens-Tune's allowlist.";
        }

        return IsLoopbackHttpEndpoint(endpoint)
            ? ""
            : "Local LLM health endpoint must be a loopback HTTP endpoint.";
    }

    private static string LocalLlmCommandBlockedReason(string commandSummary, string context, string expectedContext)
    {
        if (!IsAllowedContext(context, expectedContext))
        {
            return "Local LLM action target context is outside AppLens-Tune's allowlist.";
        }

        if (string.IsNullOrWhiteSpace(commandSummary))
        {
            return "Local LLM runtime command is missing.";
        }

        return commandSummary.IndexOfAny(['&', '|', ';', '`']) >= 0
            ? "Local LLM runtime command summary cannot contain shell chaining characters."
            : "";
    }

    private static string SshBlockedReason(string targetAlias, string context, string expectedContext)
    {
        if (!IsAllowedContext(context, expectedContext))
        {
            return "SSH action target context is outside AppLens-Tune's allowlist.";
        }

        return IsSanitizedAlias(targetAlias)
            ? ""
            : "SSH action target must be a configured alias, not a raw host or address.";
    }

    private static bool IsAllowedContext(string context, string expectedContext) =>
        string.Equals(context.Trim(), expectedContext, StringComparison.OrdinalIgnoreCase);

    private static bool IsLoopbackHttpEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme is "http" or "https" && uri.IsLoopback;
    }

    private static bool IsSanitizedAlias(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '-' or '_' or '.');

    private static IEnumerable<string> ClearableCacheRootsCore()
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

    private static string NormalizeRegistryLocation(string location) =>
        location.Trim().TrimEnd('\\').ToUpperInvariant();

    private static bool IsSameOrChild(string path, string root)
    {
        var normalizedPath = EnsureTrailingSeparator(path);
        var normalizedRoot = EnsureTrailingSeparator(root);
        return normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));

    private static string EnsureTrailingSeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
}

public interface ITuneActionRuntime
{
    bool IsAdministrator { get; }

    Task<long> ClearDirectoryContentsAsync(string path, CancellationToken cancellationToken = default);

    Task SetServiceStartModeManualAsync(string serviceName, CancellationToken cancellationToken = default);

    Task StopServiceAsync(string serviceName, CancellationToken cancellationToken = default);

    Task DisableStartupEntryAsync(string entryName, string location, CancellationToken cancellationToken = default);

    Task EnableStartupEntryAsync(string entryName, string location, CancellationToken cancellationToken = default);

    Task<string> CheckLocalLlmHealthAsync(string healthEndpoint, CancellationToken cancellationToken = default);

    Task<string> StartLocalLlmServerAsync(string commandSummary, CancellationToken cancellationToken = default);

    Task<string> StopLocalLlmServerAsync(string commandSummary, CancellationToken cancellationToken = default);

    Task<string> TestSshConnectionAsync(string targetAlias, CancellationToken cancellationToken = default);

    Task<string> CheckRemoteLlmHealthAsync(string targetAlias, CancellationToken cancellationToken = default);
}

public sealed class WindowsTuneActionRuntime : ITuneActionRuntime
{
    private readonly HttpMessageHandler? _httpMessageHandler;

    public WindowsTuneActionRuntime(HttpMessageHandler? httpMessageHandler = null)
    {
        _httpMessageHandler = httpMessageHandler;
    }

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

    public async Task<string> CheckLocalLlmHealthAsync(string healthEndpoint, CancellationToken cancellationToken = default)
    {
        using var client = _httpMessageHandler is null
            ? new HttpClient()
            : new HttpClient(_httpMessageHandler, disposeHandler: false);
        client.Timeout = TimeSpan.FromSeconds(3);

        using var response = await client.GetAsync(healthEndpoint, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return $"Local LLM health check returned {(int)response.StatusCode} {response.ReasonPhrase}.";
    }

    public Task<string> StartLocalLlmServerAsync(string commandSummary, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Local LLM start requires a registered module runtime executor.");

    public Task<string> StopLocalLlmServerAsync(string commandSummary, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Local LLM stop requires a registered module runtime executor.");

    public Task<string> TestSshConnectionAsync(string targetAlias, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("SSH connection testing requires a registered SSH module executor.");

    public Task<string> CheckRemoteLlmHealthAsync(string targetAlias, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Remote LLM health checks require a registered SSH module executor.");

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
    public static bool IsClearableCacheTarget(string path) =>
        TuneActionPolicy.IsClearableCacheTarget(path);
}
