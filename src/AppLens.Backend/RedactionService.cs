using System.Text.RegularExpressions;

namespace AppLens.Backend;

public sealed class RedactionService
{
    public string Redact(string text, AuditSnapshot snapshot)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var redacted = text;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        redacted = ReplaceLiteral(redacted, userProfile, "%USERPROFILE%");
        redacted = ReplaceLiteral(redacted, userProfile.Replace(@"\", @"\\"), "%USERPROFILE%");

        if (!string.IsNullOrWhiteSpace(snapshot.Machine.UserName))
        {
            redacted = Regex.Replace(
                redacted,
                $@"C:\\Users\\{Regex.Escape(snapshot.Machine.UserName)}(?=\\|""|\s|$)",
                "%USERPROFILE%",
                RegexOptions.IgnoreCase);
        }

        redacted = ReplaceLiteral(redacted, snapshot.Machine.ComputerName, "[computer]");
        redacted = ReplaceLiteral(redacted, snapshot.Machine.UserName, "[user]");

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
