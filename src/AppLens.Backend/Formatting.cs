using System.Net;
using System.Text;
using System.Text.Encodings.Web;

namespace AppLens.Backend;

public static class Formatting
{
    public static string Size(long? bytes)
    {
        if (bytes is null)
        {
            return "(missing)";
        }

        var value = (double)bytes.Value;
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        var index = 0;
        while (value >= 1024 && index < suffixes.Length - 1)
        {
            value /= 1024;
            index++;
        }

        return index == 0 ? $"{value:N0} {suffixes[index]}" : $"{value:N2} {suffixes[index]}";
    }

    public static string OneLine(string value, int maxLength = 120)
    {
        var normalized = string.Join(' ', value.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : normalized[..Math.Max(0, maxLength - 3)] + "...";
    }

    public static string MarkdownEscape(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal);

    public static string Html(string value) => WebUtility.HtmlEncode(value);

    public static string HtmlBlock(IEnumerable<string> lines)
    {
        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            builder.Append(Html(line)).Append("<br>");
        }

        return builder.ToString();
    }
}
