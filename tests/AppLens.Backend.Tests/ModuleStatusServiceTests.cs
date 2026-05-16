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
    public void Oracle_is_not_configured_when_repo_path_is_absent()
    {
        var service = new ModuleStatusService(new ModuleStatusPaths { OracleRoot = Path.Combine(_root, "missing-oracle") });

        var status = service.GetStatuses().Single(item => item.ModuleId == "oracle");

        Assert.Equal(ModuleAvailability.NotConfigured, status.Availability);
        Assert.Contains("not configured", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Oracle_is_blocked_when_configured_repo_is_missing_pyproject()
    {
        var oracleRoot = Path.Combine(_root, "Oracle");
        Directory.CreateDirectory(oracleRoot);
        var service = new ModuleStatusService(new ModuleStatusPaths { OracleRoot = oracleRoot });

        var status = service.GetStatuses().Single(item => item.ModuleId == "oracle");

        Assert.Equal(ModuleAvailability.Blocked, status.Availability);
        Assert.Contains("pyproject", status.Reason, StringComparison.OrdinalIgnoreCase);
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
    public void Llm_is_not_configured_when_repo_path_is_absent()
    {
        var service = new ModuleStatusService(new ModuleStatusPaths { AppLensLlmRoot = Path.Combine(_root, "missing-llm") });

        var status = service.GetStatuses().Single(item => item.ModuleId == "llm");

        Assert.Equal(ModuleAvailability.NotConfigured, status.Availability);
        Assert.Contains("not configured", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Llm_is_blocked_when_configured_repo_is_missing_cli()
    {
        var llmRoot = Path.Combine(_root, "AppLens-LLM");
        Directory.CreateDirectory(llmRoot);
        File.WriteAllText(Path.Combine(llmRoot, "pyproject.toml"), "[project]\nname='applens-llm'\n");
        var service = new ModuleStatusService(new ModuleStatusPaths { AppLensLlmRoot = llmRoot });

        var status = service.GetStatuses().Single(item => item.ModuleId == "llm");

        Assert.Equal(ModuleAvailability.Blocked, status.Availability);
        Assert.Contains("CLI", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Zero_is_not_configured_when_repo_path_is_absent()
    {
        var service = new ModuleStatusService(new ModuleStatusPaths { AppLensZeroRoot = Path.Combine(_root, "missing-zero") });

        var status = service.GetStatuses().Single(item => item.ModuleId == "zero");

        Assert.Equal(ModuleAvailability.NotConfigured, status.Availability);
        Assert.Contains("not configured", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Zero_is_blocked_when_configured_repo_is_missing_import_source()
    {
        var zeroRoot = Path.Combine(_root, "AppLens-Zero");
        Directory.CreateDirectory(zeroRoot);
        var service = new ModuleStatusService(new ModuleStatusPaths { AppLensZeroRoot = zeroRoot });

        var status = service.GetStatuses().Single(item => item.ModuleId == "zero");

        Assert.Equal(ModuleAvailability.Blocked, status.Availability);
        Assert.Contains("raw", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Every_manifest_uses_the_standard_platform_shape()
    {
        var service = new ModuleStatusService(new ModuleStatusPaths
        {
            AppLensLlmRoot = Path.Combine(_root, "AppLens-LLM"),
            OracleRoot = Path.Combine(_root, "Oracle"),
            MailboxRoot = Path.Combine(_root, "Mailbox"),
            AppLensZeroRoot = Path.Combine(_root, "AppLens-Zero")
        });

        var manifests = service.GetManifests();

        Assert.Equal(["llm", "oracle", "mailbox", "zero"], manifests.Select(manifest => manifest.ModuleId).ToArray());
        Assert.All(manifests, manifest =>
        {
            Assert.Equal(ModuleManifest.PlatformSchemaVersion, manifest.SchemaVersion);
            Assert.False(string.IsNullOrWhiteSpace(manifest.ModuleKind));
            Assert.NotEmpty(manifest.Capabilities);
            Assert.NotEmpty(manifest.StorageRoots);
            Assert.NotEmpty(manifest.HealthChecks);
            Assert.NotEmpty(manifest.Actions);
            Assert.All(manifest.StorageRoots, root =>
            {
                Assert.False(string.IsNullOrWhiteSpace(root.Name));
                Assert.False(string.IsNullOrWhiteSpace(root.Path));
                Assert.False(string.IsNullOrWhiteSpace(root.PrivacyState));
            });
            Assert.All(manifest.Actions, action =>
            {
                Assert.False(string.IsNullOrWhiteSpace(action.Name));
                Assert.False(string.IsNullOrWhiteSpace(action.Permission));
                Assert.False(string.IsNullOrWhiteSpace(action.Description));
            });
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
