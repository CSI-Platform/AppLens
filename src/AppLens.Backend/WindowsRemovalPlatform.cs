using System.ComponentModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Management.Deployment;

namespace AppLens.Backend;

public sealed class WindowsRemovalPlatform : IRemovalPlatform
{
    private readonly ConcurrentDictionary<string, Task<RemovalExecution>> _operations = new();
    private readonly ConcurrentDictionary<string, IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress>> _deployments = new();

    public async Task<RemovalExecution> StartAsync(AppEntry app, RemovalCommand command, bool administrator, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (command.Route == RemovalRoute.WindowsSettings) return await ExecuteAsync(app, command, administrator);
        if (command.Route == RemovalRoute.Msix)
        {
            try
            {
                if (IsOwnPackage(app.PackageFullName)) return new("Blocked", "Installed cannot remove its own running package.");
                var manager = new PackageManager();
                var package = manager.FindPackagesForUser("").SingleOrDefault(p => p.Id.FullName == app.PackageFullName);
                if (package is null) return new("Blocked", "The exact package is no longer registered.");
                if (package.IsFramework || package.IsResourcePackage || package.SignatureKind == PackageSignatureKind.System)
                    return new("Restricted", "Windows framework, resource, or system package.");
                var operation = Guid.NewGuid().ToString("N");
                _deployments[operation] = manager.RemovePackageAsync(app.PackageFullName);
                return new("Running", "Windows package removal is in progress.", OperationId: operation, ProcessState: RemovalProcessState.Running);
            }
            catch (UnauthorizedAccessException ex) { return new("Restricted", "Windows denied access: " + ex.Message); }
            catch (Exception ex) { return new("Failed", ex.Message + $" (0x{ex.HResult:X8})"); }
        }
        if (command.Route is not (RemovalRoute.Msi or RemovalRoute.Vendor)) return new("Blocked", "No supported removal route.");
        if (command.Route == RemovalRoute.Msi && (MsiQueryProductState(app.ProductCode) != 5 || app.MsiContext == 0 || MsiInstallation.ReadUniqueContext(app.ProductCode) != app.MsiContext))
            return new("Blocked", "Windows Installer did not confirm this product in the current installation context.");
        try
        {
            var start = new ProcessStartInfo(command.Executable) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(command.Executable)! };
            if (administrator) start.Verb = "runas";
            foreach (var argument in command.Arguments) start.ArgumentList.Add(argument);
            var process = Process.Start(start);
            if (process is null) return new("Outcome unknown", "Windows accepted the launch but provided no process to monitor.");
            RemovalProcessIdentity? identity = null;
            try { identity = new(process.Id, process.StartTime.ToUniversalTime()); } catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
            var operation = Guid.NewGuid().ToString("N");
            _operations[operation] = TrackProcessAsync(process, command.Route);
            return new("Running", "Complete any Windows or vendor approval dialog. You can continue reviewing this scan.",
                Process: identity, OperationId: operation, ProcessState: RemovalProcessState.Running);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return new("Cancelled", "Windows approval was declined or cancelled."); }
        catch (UnauthorizedAccessException ex) { return new("Restricted", "Windows denied access: " + ex.Message); }
        catch (Exception ex) { return new("Failed", ex.Message); }
    }

    private static async Task<RemovalExecution> TrackProcessAsync(Process process, RemovalRoute route)
    {
        using (process)
        {
            try
            {
                await process.WaitForExitAsync().ConfigureAwait(false);
                return InterpretExitCode(route, process.ExitCode) with { ProcessState = RemovalProcessState.Exited };
            }
            catch (Exception ex) { return new("Outcome unknown", "Process observation unavailable: " + ex.Message); }
        }
    }

    public Task<RemovalExecution?> ObserveExecutionAsync(RemovalRecord record, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (record.OperationId is { } deploymentId && _deployments.TryGetValue(deploymentId, out var deployment))
        {
            // Read native completion directly. A managed await continuation can lag
            // behind a completed deployment; absence alone never proves completion.
            if (deployment.Status == AsyncStatus.Started)
                return Task.FromResult<RemovalExecution?>(new("Running", "Windows package removal is still running.", ProcessState: RemovalProcessState.Running));
            RemovalExecution result;
            try
            {
                if (deployment.Status == AsyncStatus.Canceled) result = new("Cancelled", "Windows cancelled package removal.");
                else if (deployment.Status == AsyncStatus.Error) result = new("Failed", "Windows package removal failed: " + deployment.ErrorCode?.Message);
                else
                {
                    var completed = deployment.GetResults();
                    result = completed.ExtendedErrorCode is { } error && error.HResult < 0 ?
                        new("Failed", completed.ErrorText + $" (0x{error.HResult:X8})") :
                        new("Completed", "Windows completed current-user package removal.");
                }
            }
            catch (Exception ex) { result = new("Failed", ex.Message + $" (0x{ex.HResult:X8})"); }
            result = result with { ProcessState = RemovalProcessState.Exited };
            _operations[deploymentId] = Task.FromResult(result);
            _deployments.TryRemove(deploymentId, out _);
            deployment.Close();
            return Task.FromResult<RemovalExecution?>(result);
        }
        if (record.OperationId is { } operation && _operations.TryGetValue(operation, out var task))
        {
            if (!task.IsCompleted) return Task.FromResult<RemovalExecution?>(new("Running", "The Windows or vendor operation is still running.", ProcessState: RemovalProcessState.Running));
            // Retain the completed result for this session: a retry preflight may
            // observe it before reconciliation durably records the exit evidence.
            return CompletedAsync(task);
        }
        return Task.Run<RemovalExecution?>(() => ObserveProcess(record), token);
    }

    private static async Task<RemovalExecution?> CompletedAsync(Task<RemovalExecution> task)
    {
        var result = await task.ConfigureAwait(false);
        return result with { ProcessState = result.Outcome == "Outcome unknown" ? RemovalProcessState.Unknown : RemovalProcessState.Exited };
    }

    private static RemovalExecution? ObserveProcess(RemovalRecord record)
    {
        if (record.Process is not { } identity) return null;
        Process process;
        try { process = Process.GetProcessById(identity.Id); }
        catch (ArgumentException) { return new("Execution ended", "The recorded process no longer exists; its exit code is unavailable.", ProcessState: RemovalProcessState.Exited); }
        using (process)
        {
            try
            {
                if (process.StartTime.ToUniversalTime() != identity.StartedAt.UtcDateTime)
                    return new("Execution ended", "The recorded process ended; Windows has reused its process ID.", ProcessState: RemovalProcessState.Exited);
                return process.HasExited ? InterpretExitCode(record.Route, process.ExitCode) with { ProcessState = RemovalProcessState.Exited } :
                    new("Running", "The recorded uninstaller is still running.", ProcessState: RemovalProcessState.Running);
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or UnauthorizedAccessException)
            { return new("Outcome unknown", "The recorded process cannot be inspected. Review it in Windows."); }
        }
    }
    public async Task<AppEntry?> ReadAppAsync(string id)
    {
        var inventory = await new InventoryCollector().CollectAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(15));
        var app = inventory.DesktopApplications.Concat(inventory.StoreApplications).Concat(inventory.RuntimesAndFrameworks).Concat(inventory.SystemComponents)
            .SingleOrDefault(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (app?.CandidateRoute == RemovalRoute.Msix && IsOwnPackage(app.PackageFullName))
            throw new InvalidOperationException("Installed cannot remove itself while running. Use Windows Installed Apps.");
        if (app is not null && RemovalCommand.Resolve(app).Executable.Equals(Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Installed cannot use itself as an uninstaller.");
        return app;
    }

    public async Task<List<DiskSnapshot>> ReadDisksAsync() =>
        (await new MachineCollector().CollectAsync(CancellationToken.None)).Disks;

    public async Task<RemovalExecution> ExecuteAsync(AppEntry app, RemovalCommand command, bool administrator)
    {
        try
        {
            if (command.Route == RemovalRoute.Msix)
            {
                if (IsOwnPackage(app.PackageFullName)) return new("Blocked", "Installed cannot remove its own running package.");
                var manager = new PackageManager();
                var package = manager.FindPackagesForUser("").SingleOrDefault(p => p.Id.FullName == app.PackageFullName);
                if (package is null) return new("Blocked", "The exact package is no longer registered.");
                if (package.IsFramework || package.IsResourcePackage || package.SignatureKind == PackageSignatureKind.System)
                    return new("Restricted", "Windows framework, resource, or system package.");
                var result = await manager.RemovePackageAsync(app.PackageFullName);
                if (result.ExtendedErrorCode is { } error) return new("Failed", result.ErrorText + $" (0x{error.HResult:X8})");
                return new("Completed", "Windows completed current-user package removal.");
            }
            if (command.Route == RemovalRoute.WindowsSettings)
            {
                var launched = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures"));
                return new(launched ? "Handed off to Windows" : "Failed", launched ? "Windows Installed Apps opened. No removal was performed by Installed." : "Windows Installed Apps could not be opened.");
            }
            if (command.Route is not (RemovalRoute.Msi or RemovalRoute.Vendor)) return new("Blocked", "No supported removal route.");
            if (command.Route == RemovalRoute.Msi && (MsiQueryProductState(app.ProductCode) != 5 || app.MsiContext == 0 || MsiInstallation.ReadUniqueContext(app.ProductCode) != app.MsiContext))
                return new("Blocked", "Windows Installer did not confirm this product in the current installation context.");
            var start = new ProcessStartInfo(command.Executable) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(command.Executable)! };
            if (administrator) start.Verb = "runas";
            foreach (var argument in command.Arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start);
            if (process is null) return new("Not verified", "Windows accepted the launch but did not provide a process to monitor.");
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3)); }
            catch (TimeoutException) { return new("Still running", "The uninstaller has not exited. Review its Windows dialog; AppLens did not stop it."); }
            return InterpretExitCode(command.Route, process.ExitCode);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return new("Cancelled", "Windows approval was declined or cancelled."); }
        catch (UnauthorizedAccessException ex) { return new("Restricted", "Windows denied access: " + ex.Message); }
        catch (Exception ex) { return new("Failed", ex.Message + $" (0x{ex.HResult:X8})"); }
    }

    public static RemovalExecution InterpretExitCode(RemovalRoute route, int code) => code switch
    {
        0 => new("Completed", "The uninstaller exited with code 0.", ExitCode: code),
        1602 when route == RemovalRoute.Msi => new("Cancelled", "Windows Installer reported user cancellation (1602).", ExitCode: code),
        3010 when route == RemovalRoute.Msi => new("Completed", "Windows Installer requires a restart to finish. Installed will not restart this PC.", true, ExitCode: code),
        1625 when route == RemovalRoute.Msi => new("Restricted", "Windows Installer reports removal is forbidden by system policy (1625).", ExitCode: code),
        _ => new("Failed", $"The uninstaller exited with code {code}.", ExitCode: code)
    };

    public Task<bool?> IsInstalledAsync(AppEntry app) => Task.Run<bool?>(() =>
    {
        if (app.CandidateRoute == RemovalRoute.Msix)
            return new PackageManager().FindPackagesForUser("").Any(p => p.Id.FullName == app.PackageFullName);
        if (!Enum.TryParse<RegistryHive>(app.RegistryHive, out var hive) || !Enum.TryParse<RegistryView>(app.RegistryView, out var view)) return null;
        using var root = RegistryKey.OpenBaseKey(hive, view);
        using var key = root.OpenSubKey(InventoryCollector.UninstallKey + "\\" + app.RegistryKey);
        if (key is not null) return true;
        if (app.CandidateRoute != RemovalRoute.Msi) return false;
        return MsiQueryProductState(app.ProductCode) switch { -1 => false, 5 => true, _ => null };
    });

    public static bool IsOwnPackage(string fullName)
    {
        try { return Package.Current.Id.FullName.Equals(fullName, StringComparison.OrdinalIgnoreCase); }
        catch (InvalidOperationException) { return false; }
    }

    [DllImport("msi.dll", CharSet = CharSet.Unicode)] private static extern int MsiQueryProductState(string product);
}
