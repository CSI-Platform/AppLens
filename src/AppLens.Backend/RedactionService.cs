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

        var redacted = text;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        redacted = ReplaceLiteral(redacted, userProfile, "%USERPROFILE%");
        redacted = ReplaceLiteral(redacted, userProfile.Replace(@"\", @"\\"), "%USERPROFILE%");

        if (!string.IsNullOrWhiteSpace(userName))
        {
            redacted = Regex.Replace(
                redacted,
                $@"C:\\Users\\{Regex.Escape(userName)}(?=\\|""|\s|$)",
                "%USERPROFILE%",
                RegexOptions.IgnoreCase);
        }

        redacted = ReplaceLiteral(redacted, computerName, "[computer]");
        redacted = ReplaceLiteral(redacted, userName, "[user]");

        return redacted;
    }

    private static string ReplaceLiteral(string text, string value, string replacement)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return text;
        }

        return text.Replace(value, replacement, StringComparison.OrdinalIgnoreCase);
    }
}
