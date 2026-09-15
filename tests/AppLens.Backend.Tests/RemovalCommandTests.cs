namespace AppLens.Backend.Tests;

public class RemovalCommandTests
{
    [Theory]
    [InlineData("cmd.exe /c del x")]
    [InlineData("powershell.exe -Command something")]
    [InlineData("C:\\Program Files\\Vendor\\remove.exe /uninstall")]
    [InlineData("\\\\server\\share\\remove.exe /u")]
    [InlineData("C:\\tools\\remove.cmd")]
    [InlineData("\"C:\\Windows\\System32\\rundll32.exe\" a.dll,Remove")]
    public void AmbiguousOrInterpretedCommandsHandOffToWindows(string command)
    {
        var app = new AppEntry { Kind = ApplicationKind.Desktop, CandidateRoute = RemovalRoute.Vendor, UninstallCommand = command };
        Assert.Equal(RemovalRoute.WindowsSettings, RemovalCommand.Resolve(app).Route);
    }

    [Fact]
    public void ValidVendorExecutableKeepsLiteralArguments()
    {
        var exe = Environment.ProcessPath!;
        var app = new AppEntry { Kind = ApplicationKind.Desktop, CandidateRoute = RemovalRoute.Vendor,
            UninstallCommand = "\"" + exe + "\" /uninstall \"literal & argument\"" };
        var command = RemovalCommand.Resolve(app);
        Assert.Equal(RemovalRoute.Vendor, command.Route);
        Assert.Equal(exe, command.Executable);
        Assert.Equal(["/uninstall", "literal & argument"], command.Arguments);
    }

    [Fact]
    public void MsiUsesOnlyTheValidatedProductCodeAndSuppressesRestart()
    {
        var code = Guid.NewGuid();
        var app = new AppEntry { Kind = ApplicationKind.Desktop, CandidateRoute = RemovalRoute.Msi,
            ProductCode = code.ToString(), UninstallCommand = "untrusted extra text" };
        var command = RemovalCommand.Resolve(app);
        Assert.Equal(["/x", code.ToString("B").ToUpperInvariant(), "/qb", "/norestart"], command.Arguments);
    }

    [Fact]
    public void ProtectedComponentsNeverBecomeRemovalCommands()
    {
        Assert.Equal(RemovalRoute.None, RemovalCommand.Resolve(new AppEntry { Kind = ApplicationKind.Runtime, CandidateRoute = RemovalRoute.Msi, ProductCode = Guid.NewGuid().ToString() }).Route);
    }
}
