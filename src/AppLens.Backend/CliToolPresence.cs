namespace AppLens.Backend;

public sealed record CliToolPresence(string Name, string Command, string State)
{
    private static readonly string[] Commands =
        ["git", "python", "node", "npm", "docker", "ollama", "pwsh", "winget", "choco", "scoop", "wsl", "dotnet", "java", "go", "rustc", "uv"];
    private static readonly string[] Extensions = [".exe", ".cmd", ".bat", ".ps1", ".com"];

    internal static IReadOnlyList<CliToolPresence> Collect()
    {
        var roots = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator)
            .Select(path => path.Trim().Trim('"'))
            .Where(path => Path.IsPathFullyQualified(path) && !path.StartsWith(@"\\", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var fixedDrives = DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed)
            .Select(drive => drive.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var localRoots = roots.Where(root => fixedDrives.Contains(Path.GetPathRoot(root) ?? "")).ToArray();
        return Commands.Select(command => new CliToolPresence(command, command, localRoots.Any(root =>
            Extensions.Any(extension => File.Exists(Path.Combine(root, command + extension))))
            ? "Succeeded" : "Unavailable")).ToArray();
    }
}
