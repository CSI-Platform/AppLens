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
    public void Llm_manifest_exposes_concrete_runtime_actions()
    {
        var manifest = new ModuleStatusService().GetManifests().Single(item => item.ModuleId == "llm");

        var health = Assert.Single(manifest.Actions, action => action.Name == "runtime-health");
        Assert.Equal("read", health.Permission);
        Assert.False(health.RequiresApproval);

        var start = Assert.Single(manifest.Actions, action => action.Name == "runtime-start");
        Assert.Equal("execute-local", start.Permission);
        Assert.Equal("module-llm-runtime", start.ExecutorKey);
        Assert.True(start.RequiresApproval);

        var stop = Assert.Single(manifest.Actions, action => action.Name == "runtime-stop");
        Assert.Equal("execute-local", stop.Permission);
        Assert.Equal("module-llm-runtime", stop.ExecutorKey);
        Assert.True(stop.RequiresApproval);
    }

    [Fact]
    public void Ssh_is_not_configured_when_descriptor_is_missing()
    {
        var client = Path.Combine(_root, "ssh.exe");
        File.WriteAllText(client, "");
        var service = new ModuleStatusService(new ModuleStatusPaths
        {
            SshClientPath = client,
            SshDescriptorPath = Path.Combine(_root, "missing-targets.json"),
            RemoteLlmDescriptorPath = Path.Combine(_root, "remote-llm.json")
        });

        var status = service.GetStatuses().Single(item => item.ModuleId == "ssh");

        Assert.Equal(ModuleAvailability.NotConfigured, status.Availability);
        Assert.DoesNotContain("@", status.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Ssh_is_available_when_client_and_descriptors_are_present()
    {
        var client = Path.Combine(_root, "ssh.exe");
        var descriptor = Path.Combine(_root, "ssh-targets.json");
        var remoteLlm = Path.Combine(_root, "remote-llm.json");
        File.WriteAllText(client, "");
        File.WriteAllText(descriptor, """{"targets":[{"alias":"local-gpu"}]}""");
        File.WriteAllText(remoteLlm, """{"alias":"local-gpu","health":"llama-health"}""");
        var service = new ModuleStatusService(new ModuleStatusPaths
        {
            SshClientPath = client,
            SshDescriptorPath = descriptor,
            RemoteLlmDescriptorPath = remoteLlm
        });

        var status = service.GetStatuses().Single(item => item.ModuleId == "ssh");

        Assert.Equal(ModuleAvailability.Available, status.Availability);
        Assert.Contains("target descriptor", status.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("192.168", status.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id_rsa", status.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ssh_client_command_resolves_windows_exe_from_path()
    {
        var clientDirectory = Path.Combine(_root, "bin");
        var descriptor = Path.Combine(_root, "ssh-targets.json");
        var remoteLlm = Path.Combine(_root, "remote-llm.json");
        Directory.CreateDirectory(clientDirectory);
        File.WriteAllText(Path.Combine(clientDirectory, "ssh.exe"), "");
        File.WriteAllText(descriptor, """{"targets":[{"alias":"local-gpu"}]}""");
        File.WriteAllText(remoteLlm, """{"alias":"local-gpu","health":"llama-health"}""");
        var originalPath = Environment.GetEnvironmentVariable("PATH");

        try
        {
            Environment.SetEnvironmentVariable("PATH", clientDirectory + Path.PathSeparator + originalPath);
            var service = new ModuleStatusService(new ModuleStatusPaths
            {
                SshClientPath = "ssh",
                SshDescriptorPath = descriptor,
                RemoteLlmDescriptorPath = remoteLlm
            });

            var status = service.GetStatuses().Single(item => item.ModuleId == "ssh");

            Assert.Equal(ModuleAvailability.Available, status.Availability);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void Ssh_manifest_exposes_approval_gated_remote_actions()
    {
        var manifest = new ModuleStatusService().GetManifests().Single(item => item.ModuleId == "ssh");

        Assert.Equal("applens-ssh", manifest.AppId);
        Assert.Equal("remote-ssh-adapter", manifest.ModuleKind);
        Assert.Contains(manifest.Actions, action =>
            action.Name == "test-connection" &&
            action.Permission == "execute-ssh" &&
            action.ExecutorKey == "module-ssh-command" &&
            action.RequiresApproval);
        Assert.Contains(manifest.Actions, action =>
            action.Name == "check-remote-llm" &&
            action.Permission == "execute-ssh" &&
            action.ExecutorKey == "module-ssh-command" &&
            action.RequiresApproval);
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

        Assert.Equal(["llm", "oracle", "mailbox", "zero", "ssh"], manifests.Select(manifest => manifest.ModuleId).ToArray());
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
                if (!action.Permission.Equals("read", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.False(string.IsNullOrWhiteSpace(action.ExecutorKey));
                }
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
