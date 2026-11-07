namespace AppLens.Backend.Tests;

public sealed class RedactionServiceTests
{
    private readonly RedactionService _redaction = new();

    [Fact]
    public void Short_identifiers_do_not_corrupt_specs_or_similarly_named_tools()
    {
        var machine = new DeviceSummary { UserName = "a", ComputerName = "pc" };
        const string text = "AMD Radeon, Slack, grpc, a-tools, pc-tools, café; user A on PC.";

        Assert.Equal("AMD Radeon, Slack, grpc, a-tools, pc-tools, café; user [user] on [computer].",
            _redaction.Redact(text, machine));
    }

    [Fact]
    public void Identifiers_in_paths_domains_and_possessives_are_still_redacted()
    {
        var machine = new DeviceSummary { UserName = "client-person", ComputerName = "client-pc" };
        const string text = @"CLIENT-PC\client-person; \\client-pc\share; client-pc.example; client-person's notes; C:\Users\client-person\file; D:\Users\client-person\file";

        Assert.Equal(@"[computer]\[user]; \\[computer]\share; [computer].example; [user]'s notes; %USERPROFILE%\file; D:\Users\[user]\file",
            _redaction.Redact(text, machine));
    }

    [Fact]
    public void Replacement_markers_are_not_redacted_again()
    {
        var machine = new DeviceSummary { UserName = "computer", ComputerName = "USERPROFILE" };

        Assert.Equal(@"%USERPROFILE%\file; [computer]; [user]",
            _redaction.Redact(@"C:\Users\computer\file; USERPROFILE; computer", machine));
    }

    [Fact]
    public void Identifiers_are_literal_and_do_not_match_inside_longer_unicode_names()
    {
        var machine = new DeviceSummary { UserName = "Zoë", ComputerName = "PC[1]" };

        Assert.Equal("[user]'s [computer]; ZoëApp; appZoë; PC11",
            _redaction.Redact("Zoë's PC[1]; ZoëApp; appZoë; PC11", machine));
    }

    [Fact]
    public void Current_profile_path_is_redacted_in_plain_and_escaped_text()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.False(string.IsNullOrWhiteSpace(profile));
        var machine = new DeviceSummary { UserName = "user", ComputerName = "profile" };

        Assert.Equal("%USERPROFILE%/report; %USERPROFILE%/report",
            _redaction.Redact($"{profile}/report; {profile.Replace(@"\", @"\\")}/report", machine));
    }
}
