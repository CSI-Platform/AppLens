namespace AppLens.Backend.Tests;

public sealed class ModuleStatusServiceTests : IDisposable
{
    private readonly string _root;

    public ModuleStatusServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "AppLens-ModuleStatusServiceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Oracle_is_blocked_when_repo_path_is_absent()
    {
        var service = new ModuleStatusService(new ModuleStatusPaths { OracleRoot = Path.Combine(_root, "missing-oracle") });

        var status = service.GetStatuses().Single(item => item.ModuleId == "oracle");

        Assert.Equal(ModuleAvailability.Blocked, status.Availability);
        Assert.Contains("Oracle repo", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Oracle_is_available_when_pyproject_exists()
    {
        var oracleRoot = Path.Combine(_root, "Oracle");
        Directory.CreateDirectory(oracleRoot);
        File.WriteAllText(Path.Combine(oracleRoot, "pyproject.toml"), "[project]\nname='oracle-workbench'\n");
        var service = new ModuleStatusService(new ModuleStatusPaths { OracleRoot = oracleRoot });

        var status = service.GetStatuses().Single(item => item.ModuleId == "oracle");

        Assert.Equal(ModuleAvailability.Available, status.Availability);
    }

    [Fact]
    public void Mailbox_is_blocked_when_config_is_absent()
    {
        var mailboxRoot = Path.Combine(_root, "Mailbox");
        Directory.CreateDirectory(Path.Combine(mailboxRoot, "mailbox"));
        File.WriteAllText(Path.Combine(mailboxRoot, "mailbox", "server.py"), "# fake server");
        var service = new ModuleStatusService(new ModuleStatusPaths { MailboxRoot = mailboxRoot });

        var status = service.GetStatuses().Single(item => item.ModuleId == "mailbox");

        Assert.Equal(ModuleAvailability.Blocked, status.Availability);
        Assert.Contains("config", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Llm_is_blocked_when_package_path_is_absent()
    {
        var service = new ModuleStatusService(new ModuleStatusPaths { AppLensLlmRoot = Path.Combine(_root, "missing-llm") });

        var status = service.GetStatuses().Single(item => item.ModuleId == "llm");

        Assert.Equal(ModuleAvailability.Blocked, status.Availability);
        Assert.Contains("AppLens-LLM", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Zero_is_blocked_when_no_import_source_exists()
    {
        var service = new ModuleStatusService(new ModuleStatusPaths { AppLensZeroRoot = Path.Combine(_root, "missing-zero") });

        var status = service.GetStatuses().Single(item => item.ModuleId == "zero");

        Assert.Equal(ModuleAvailability.Blocked, status.Availability);
        Assert.Contains("Zero", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
