using System.Runtime.InteropServices;

namespace AppLens.Backend;

public sealed record RemovalCommand(RemovalRoute Route, string Executable, IReadOnlyList<string> Arguments, string Reason)
{
    public static RemovalCommand Resolve(AppEntry app)
    {
        if (app.CandidateRoute == RemovalRoute.None || app.Kind is not (ApplicationKind.Desktop or ApplicationKind.Store))
            return new(RemovalRoute.None, "", [], app.RemovalReason);
        if (app.CandidateRoute == RemovalRoute.Msix && app.PackageFullName.Length > 0)
            return new(RemovalRoute.Msix, "", [], "Remove this user's Store package");
        if (app.CandidateRoute == RemovalRoute.Msi && Guid.TryParse(app.ProductCode, out var code))
            return new(RemovalRoute.Msi, Path.Combine(Environment.SystemDirectory, "msiexec.exe"),
                ["/x", code.ToString("B").ToUpperInvariant(), "/qb", "/norestart"], "Windows Installer; Windows approval may be required");
        if (app.CandidateRoute == RemovalRoute.Vendor && TryVendor(app.UninstallCommand, out var executable, out var arguments))
            return new(RemovalRoute.Vendor, executable, arguments, "Registered vendor uninstaller; Windows approval may be required");
        return new(RemovalRoute.WindowsSettings, "", [], "Review in Windows Installed Apps");
    }

    private static bool TryVendor(string text, out string executable, out string[] arguments)
    {
        executable = ""; arguments = [];
        if (string.IsNullOrWhiteSpace(text) || text.Length > 8192 || text.Any(char.IsControl)) return false;
        var expanded = Environment.ExpandEnvironmentVariables(text.Trim());
        var pointer = CommandLineToArgvW(expanded, out var count);
        if (pointer == IntPtr.Zero) return false;
        try
        {
            var parts = Enumerable.Range(0, count).Select(i => Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, i * IntPtr.Size)) ?? "").ToArray();
            if (parts.Length == 0) return false;
            var path = parts[0];
            if (path.Length < 4 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\' ||
                path[2..].Contains(':') || !Path.IsPathFullyQualified(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return false;
            string[] hosts = ["cmd", "powershell", "pwsh", "wscript", "cscript", "rundll32", "regsvr32", "mshta", "msiexec", "python", "pythonw", "node", "dotnet", "wsl", "reg", "sc", "wmic", "msbuild", "installutil", "control", "explorer"];
            if (hosts.Contains(Path.GetFileNameWithoutExtension(path), StringComparer.OrdinalIgnoreCase)) return false;
            if (parts.Skip(1).Any(a => a.Equals("/forcerestart", StringComparison.OrdinalIgnoreCase) || a.Equals("REBOOT=Force", StringComparison.OrdinalIgnoreCase))) return false;
            executable = Path.GetFullPath(path); arguments = parts.Skip(1).ToArray();
            return true;
        }
        finally { LocalFree(pointer); }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}
