using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace AppLens.Backend;

public sealed class WindowsRemovalPlatform : IRemovalPlatform
{
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
        0 => new("Completed", "The uninstaller exited with code 0."),
        1602 when route == RemovalRoute.Msi => new("Cancelled", "Windows Installer reported user cancellation (1602)."),
        3010 when route == RemovalRoute.Msi => new("Completed", "Windows Installer requires a restart to finish. Installed will not restart this PC.", true),
        1625 when route == RemovalRoute.Msi => new("Restricted", "Windows Installer reports removal is forbidden by system policy (1625)."),
        _ => new("Failed", $"The uninstaller exited with code {code}.")
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
