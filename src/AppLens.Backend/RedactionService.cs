using System.Text.RegularExpressions;

namespace AppLens.Backend;

public sealed class RedactionService
{
    public string Redact(string text, AuditSnapshot snapshot) => Redact(text, snapshot.Machine);

    public string Redact(string text, DeviceSummary machine) => Redact(text, machine.UserName, machine.ComputerName);

    public string Redact(string text, MachineSummary machine) => Redact(text, machine.UserName, machine.ComputerName);

    private string Redact(string text, string userName, string computerName)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var patterns = new List<string>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            patterns.Add($"(?<profile>{Regex.Escape(userProfile)})");
            patterns.Add($"(?<profile>{Regex.Escape(userProfile.Replace(@"\", @"\\"))})");
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            patterns.Add($@"(?<profile>C:\\Users\\{Regex.Escape(userName)}(?=\\|""|\s|$))");
        }

        AddIdentifier(patterns, computerName, "computer");
        AddIdentifier(patterns, userName, "user");

        // Replace once so a name cannot alter a marker inserted for another identifier.
        return patterns.Count == 0 ? text : Regex.Replace(text, string.Join("|", patterns),
            match => match.Groups["profile"].Success ? "%USERPROFILE%"
                : match.Groups["computer"].Success ? "[computer]" : "[user]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static void AddIdentifier(List<string> patterns, string value, string group)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        // Match names in paths, domains and prose without replacing parts of app/spec names.
        const string identifierCharacters = @"\p{L}\p{N}\p{M}_-";
        patterns.Add($@"(?<![{identifierCharacters}])(?<{group}>{Regex.Escape(value)})(?![{identifierCharacters}])");
    }
}
