using Microsoft.Win32;

namespace AppLens.Backend;

// Values from one registration. Kept separate from I/O so metadata rules can be verified.
public sealed class RegistrationData
{
    public RegistryHive Hive { get; init; } = RegistryHive.CurrentUser;
    public RegistryView View { get; init; } = RegistryView.Default;
    public string KeyName { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string InstallLocation { get; init; } = "";
    public string InstallDate { get; init; } = "";
    public string ReleaseType { get; init; } = "";
    public string UninstallCommand { get; init; } = "";
    public long? EstimatedSizeKiB { get; init; }
    public bool WindowsInstaller { get; init; }
    public uint MsiContext { get; init; }
    public bool NoRemove { get; init; }
    public bool SystemComponent { get; init; }
}
