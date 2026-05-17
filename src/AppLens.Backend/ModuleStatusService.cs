namespace AppLens.Backend;

public sealed class ModuleStatusPaths
{
    public string AppLensLlmRoot { get; init; } = ConfiguredPath("APPLENS_LLM_ROOT", DefaultProjectPath("AppLens-LLM"));
    public string OracleRoot { get; init; } = DefaultOraclePath();
    public string MailboxRoot { get; init; } = ConfiguredPath("APPLENS_MAILBOX_ROOT", DefaultProjectPath("Mailbox"));
    public string AppLensZeroRoot { get; init; } = ConfiguredPath("APPLENS_ZERO_ROOT", DefaultProjectPath("AppLens-Zero"));
    public string SshClientPath { get; init; } = ConfiguredPath("APPLENS_SSH_CLIENT_PATH", "ssh");
    public string SshDescriptorPath { get; init; } = ConfiguredPath(
        "APPLENS_SSH_DESCRIPTOR_PATH",
        Path.Combine(DefaultProjectPath("AppLens-SSH"), "ssh-targets.json"));
    public string RemoteLlmDescriptorPath { get; init; } = ConfiguredPath(
        "APPLENS_REMOTE_LLM_DESCRIPTOR_PATH",
        Path.Combine(DefaultProjectPath("AppLens-SSH"), "remote-llm.json"));

    private static string DefaultProjectPath(string projectName)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Desktop",
            "csiOS",
            "Projects",
            projectName);
    }

    private static string DefaultOraclePath()
    {
        return ConfiguredPath(
            "APPLENS_ORACLE_ROOT",
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Desktop",
                "csiOS",
                "Oracle"));
    }

    private static string ConfiguredPath(string environmentVariable, string fallback)
    {
        var configured = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return fallback;
    }
}

public sealed class ModuleStatusService
{
    private readonly ModuleStatusPaths _paths;

    public ModuleStatusService(ModuleStatusPaths? paths = null)
    {
        _paths = paths ?? new ModuleStatusPaths();
    }

    public List<ModuleStatus> GetStatuses() =>
    [
        LlmStatus(),
        OracleStatus(),
        MailboxStatus(),
        ZeroStatus(),
        SshStatus()
    ];

    public List<ModuleManifest> GetManifests() =>
    [
        new ModuleManifest
        {
            AppId = "applens-llm",
            DisplayName = "AppLens-LLM",
            ModuleId = "llm",
            ModuleKind = "local-llm-adapter",
            RiskLevel = "medium",
            Capabilities = ["lane-status", "scorecard-import", "fit-report-import"],
            Entrypoints = ["status_command", "run_job", "import_reports"],
            DataContracts = ["runtime-lanes", "blackboard-record", "model-fit-scorecard", "fit-report", "benchmark-record"],
            ActionContracts = ["bounded-local-command"],
            Privacy = ["raw_private", "sanitized", "exportable"],
            StatusContract = "file-only",
            ReportRoots = [Path.Combine(_paths.AppLensLlmRoot, "out")],
            StorageRoots =
            [
                StorageRoot("repository", _paths.AppLensLlmRoot, "raw_private", "Local AppLens-LLM repository root."),
                StorageRoot("runtime-output", Path.Combine(_paths.AppLensLlmRoot, "out"), "raw_private", "Local LLM lane reports and blackboard output.")
            ],
            HealthChecks =
            [
                HealthCheck("package", "file", Path.Combine(_paths.AppLensLlmRoot, "pyproject.toml")),
                HealthCheck("cli", "file", Path.Combine(_paths.AppLensLlmRoot, "src", "applens_llm", "cli.py"))
            ],
            Actions =
            [
                Action("read-status", "read", "", requiresApproval: false, systemChanging: false, "Read local lane and scorecard status."),
                Action("runtime-health", "read", "", requiresApproval: false, systemChanging: false, "Read local LLM runtime health from configured local status artifacts."),
                Action("runtime-start", "execute-local", "module-llm-runtime", requiresApproval: true, systemChanging: true, "Start a configured local AppLens-LLM runtime lane."),
                Action("runtime-stop", "execute-local", "module-llm-runtime", requiresApproval: true, systemChanging: true, "Stop a configured local AppLens-LLM runtime lane."),
                Action("import-reports", "write-ledger", "module-report-import", requiresApproval: true, systemChanging: false, "Import local LLM reports into the AppLens ledger."),
                Action("run-bounded-job", "execute-local", "module-local-job", requiresApproval: true, systemChanging: false, "Run a bounded local AppLens-LLM job.")
            ]
        },
        new ModuleManifest
        {
            AppId = "oracle-workbench",
            DisplayName = "Oracle",
            ModuleId = "oracle",
            ModuleKind = "research-workload",
            RiskLevel = "medium",
            Capabilities = ["research-status", "read-only-job", "report-import"],
            Entrypoints = ["status_command", "run_job", "import_reports"],
            DataContracts = ["research-report", "run-record", "data-provenance"],
            ActionContracts = ["read-only-research-job"],
            Privacy = ["raw_private", "exportable", "invalidated"],
            StatusContract = "file-only",
            ReportRoots = [Path.Combine(_paths.OracleRoot, "out")],
            StorageRoots =
            [
                StorageRoot("repository", _paths.OracleRoot, "raw_private", "Local Oracle repository root."),
                StorageRoot("reports", Path.Combine(_paths.OracleRoot, "out"), "raw_private", "Read-only Oracle run reports.")
            ],
            HealthChecks =
            [
                HealthCheck("package", "file", Path.Combine(_paths.OracleRoot, "pyproject.toml"))
            ],
            Actions =
            [
                Action("read-status", "read", "", requiresApproval: false, systemChanging: false, "Read Oracle run status."),
                Action("import-reports", "write-ledger", "module-report-import", requiresApproval: true, systemChanging: false, "Import Oracle reports into the AppLens ledger."),
                Action("run-read-only-job", "execute-local", "module-local-job", requiresApproval: true, systemChanging: false, "Run a bounded read-only Oracle research job.")
            ]
        },
        new ModuleManifest
        {
            AppId = "mailbox",
            DisplayName = "Mailbox",
            ModuleId = "mailbox",
            ModuleKind = "folder-ui-app",
            RiskLevel = "medium",
            Capabilities = ["open_ui", "status_http", "folder-status"],
            Entrypoints = ["open_ui", "status_http"],
            DataContracts = ["filesystem-mailbox", "markdown-message", "attachment-artifact"],
            ActionContracts = ["open-only-v1"],
            Privacy = ["raw_private", "sanitized"],
            StatusContract = "file-or-http-status",
            ReportRoots = [Path.Combine(_paths.MailboxRoot, "data")],
            StorageRoots =
            [
                StorageRoot("repository", _paths.MailboxRoot, "raw_private", "Local Mailbox repository root."),
                StorageRoot("mailbox-data", Path.Combine(_paths.MailboxRoot, "data"), "raw_private", "Folder-backed mailbox data root.")
            ],
            HealthChecks =
            [
                HealthCheck("server", "file", Path.Combine(_paths.MailboxRoot, "mailbox", "server.py")),
                HealthCheck("config", "file", Path.Combine(_paths.MailboxRoot, "config.toml"))
            ],
            Actions =
            [
                Action("read-status", "read", "", requiresApproval: false, systemChanging: false, "Read Mailbox folder and server status."),
                Action("open-ui", "open-local-ui", "module-open-ui", requiresApproval: true, systemChanging: false, "Open the local Mailbox UI without changing mailbox data.")
            ]
        },
        new ModuleManifest
        {
            AppId = "applens-zero",
            DisplayName = "AppLens-Zero",
            ModuleId = "zero",
            ModuleKind = "hardware-lab-adapter",
            RiskLevel = "high",
            Capabilities = ["lab-readiness", "passive-import", "authorized-session"],
            Entrypoints = ["import_reports"],
            DataContracts = ["lab-authorization", "edge-device-profile", "capture-readiness"],
            ActionContracts = ["benign-check-only"],
            Privacy = ["raw_private", "sanitized"],
            StatusContract = "file-only",
            ReportRoots = [Path.Combine(_paths.AppLensZeroRoot, "raw")],
            StorageRoots =
            [
                StorageRoot("repository", _paths.AppLensZeroRoot, "raw_private", "Local AppLens-Zero repository root."),
                StorageRoot("raw-imports", Path.Combine(_paths.AppLensZeroRoot, "raw"), "raw_private", "Authorized passive import folder.")
            ],
            HealthChecks =
            [
                HealthCheck("platform-design", "file", Path.Combine(_paths.AppLensZeroRoot, "docs", "AppLens-Platform-Host-Design.md")),
                HealthCheck("raw-import-folder", "directory", Path.Combine(_paths.AppLensZeroRoot, "raw"))
            ],
            Actions =
            [
                Action("read-status", "read", "", requiresApproval: false, systemChanging: false, "Read AppLens-Zero lab readiness status."),
                Action("import-authorized-artifacts", "write-ledger", "module-report-import", requiresApproval: true, systemChanging: false, "Import authorized passive artifacts into the AppLens ledger.")
            ]
        },
        new ModuleManifest
        {
            AppId = "applens-ssh",
            DisplayName = "AppLens-SSH",
            ModuleId = "ssh",
            ModuleKind = "remote-ssh-adapter",
            RiskLevel = "medium",
            Capabilities = ["ssh-readiness", "connection-test", "remote-llm-health"],
            Entrypoints = ["ssh_descriptor", "remote_llm_descriptor"],
            DataContracts = ["ssh-target-alias", "remote-llm-health-summary", "sanitized-command-record"],
            ActionContracts = ["approval-gated-ssh-command"],
            Privacy = ["raw_private", "sanitized"],
            StatusContract = "file-only",
            ReportRoots = [Path.GetDirectoryName(_paths.SshDescriptorPath) ?? ""],
            StorageRoots =
            [
                StorageRoot("ssh-target-descriptor", _paths.SshDescriptorPath, "raw_private", "Local SSH target alias descriptor."),
                StorageRoot("remote-llm-descriptor", _paths.RemoteLlmDescriptorPath, "raw_private", "Remote LLM health descriptor without secret material.")
            ],
            HealthChecks =
            [
                HealthCheck("openssh-client", "file-or-path-command", _paths.SshClientPath),
                HealthCheck("ssh-target-descriptor", "file", _paths.SshDescriptorPath),
                HealthCheck("remote-llm-descriptor", "file", _paths.RemoteLlmDescriptorPath)
            ],
            Actions =
            [
                Action("read-status", "read", "", requiresApproval: false, systemChanging: false, "Read sanitized SSH readiness state."),
                Action("test-connection", "execute-ssh", "module-ssh-command", requiresApproval: true, systemChanging: false, "Run a bounded SSH connection smoke test against a configured alias."),
                Action("check-remote-llm", "execute-ssh", "module-ssh-command", requiresApproval: true, systemChanging: false, "Run a read-only remote LLM health check against a configured alias.")
            ]
        }
    ];

    private ModuleStatus LlmStatus()
    {
        if (!Directory.Exists(_paths.AppLensLlmRoot))
        {
            return NotConfigured("llm", "applens-llm", "AppLens-LLM", _paths.AppLensLlmRoot);
        }

        var pyproject = Path.Combine(_paths.AppLensLlmRoot, "pyproject.toml");
        var cli = Path.Combine(_paths.AppLensLlmRoot, "src", "applens_llm", "cli.py");
        if (!File.Exists(pyproject))
        {
            return Blocked("llm", "applens-llm", "AppLens-LLM", "AppLens-LLM pyproject.toml is unavailable.", _paths.AppLensLlmRoot);
        }

        if (!File.Exists(cli))
        {
            return Blocked("llm", "applens-llm", "AppLens-LLM", "AppLens-LLM CLI source is unavailable.", _paths.AppLensLlmRoot);
        }

        return Available("llm", "applens-llm", "AppLens-LLM", "Package and CLI source detected.");
    }

    private ModuleStatus OracleStatus()
    {
        if (!Directory.Exists(_paths.OracleRoot))
        {
            return NotConfigured("oracle", "oracle-workbench", "Oracle", _paths.OracleRoot);
        }

        var pyproject = Path.Combine(_paths.OracleRoot, "pyproject.toml");
        if (!File.Exists(pyproject))
        {
            return Blocked("oracle", "oracle-workbench", "Oracle", "Oracle pyproject.toml is unavailable.", _paths.OracleRoot);
        }

        return Available("oracle", "oracle-workbench", "Oracle", "Repo and CLI project detected.");
    }

    private ModuleStatus MailboxStatus()
    {
        if (!Directory.Exists(_paths.MailboxRoot))
        {
            return NotConfigured("mailbox", "mailbox", "Mailbox", _paths.MailboxRoot);
        }

        var config = Path.Combine(_paths.MailboxRoot, "config.toml");
        var server = Path.Combine(_paths.MailboxRoot, "mailbox", "server.py");
        if (!File.Exists(server))
        {
            return Blocked("mailbox", "mailbox", "Mailbox", "Mailbox server source is unavailable.", _paths.MailboxRoot);
        }

        if (!File.Exists(config))
        {
            return Blocked("mailbox", "mailbox", "Mailbox", "Mailbox config.toml is unavailable.", _paths.MailboxRoot);
        }

        return Available("mailbox", "mailbox", "Mailbox", "Config and server source detected.");
    }

    private ModuleStatus ZeroStatus()
    {
        if (!Directory.Exists(_paths.AppLensZeroRoot))
        {
            return NotConfigured("zero", "applens-zero", "AppLens-Zero", _paths.AppLensZeroRoot);
        }

        var docs = Path.Combine(_paths.AppLensZeroRoot, "docs", "AppLens-Platform-Host-Design.md");
        var raw = Path.Combine(_paths.AppLensZeroRoot, "raw");
        if (!File.Exists(docs) || !Directory.Exists(raw))
        {
            return Blocked("zero", "applens-zero", "AppLens-Zero", "Zero docs or import raw folder is unavailable.", _paths.AppLensZeroRoot);
        }

        return Available("zero", "applens-zero", "AppLens-Zero", "Docs and raw import folder detected.");
    }

    private ModuleStatus SshStatus()
    {
        if (!CommandOrFileExists(_paths.SshClientPath))
        {
            return NotConfigured("ssh", "applens-ssh", "AppLens-SSH", _paths.SshClientPath);
        }

        if (!File.Exists(_paths.SshDescriptorPath))
        {
            return NotConfigured("ssh", "applens-ssh", "AppLens-SSH", _paths.SshDescriptorPath);
        }

        if (!File.Exists(_paths.RemoteLlmDescriptorPath))
        {
            return Blocked("ssh", "applens-ssh", "AppLens-SSH", "Remote LLM descriptor is unavailable.", _paths.RemoteLlmDescriptorPath);
        }

        return Available(
            "ssh",
            "applens-ssh",
            "AppLens-SSH",
            "OpenSSH client and sanitized target descriptor detected; secret material is not read.");
    }

    private static ModuleStatus Available(string moduleId, string appId, string displayName, string reason) =>
        new()
        {
            ModuleId = moduleId,
            AppId = appId,
            DisplayName = displayName,
            Availability = ModuleAvailability.Available,
            Reason = reason,
            ExpectedSource = "",
            NextAction = "Review module status in AppLens."
        };

    private static ModuleStatus NotConfigured(string moduleId, string appId, string displayName, string expectedSource) =>
        new()
        {
            ModuleId = moduleId,
            AppId = appId,
            DisplayName = displayName,
            Availability = ModuleAvailability.NotConfigured,
            Reason = $"{displayName} module path is not configured.",
            ExpectedSource = expectedSource,
            NextAction = "Configure the local app path to enable this module."
        };

    private static ModuleStatus Blocked(string moduleId, string appId, string displayName, string reason, string expectedSource) =>
        new()
        {
            ModuleId = moduleId,
            AppId = appId,
            DisplayName = displayName,
            Availability = ModuleAvailability.Blocked,
            Reason = reason,
            ExpectedSource = expectedSource,
            NextAction = "Connect or configure the local app path before running jobs."
        };

    private static ModuleStorageRoot StorageRoot(string name, string path, string privacyState, string description) =>
        new()
        {
            Name = name,
            Path = path,
            PrivacyState = privacyState,
            Description = description
        };

    private static ModuleHealthCheck HealthCheck(string name, string kind, string target) =>
        new()
        {
            Name = name,
            Kind = kind,
            Target = target,
            Required = true
        };

    private static ModuleActionContract Action(
        string name,
        string permission,
        string executorKey,
        bool requiresApproval,
        bool systemChanging,
        string description) =>
        new()
        {
            Name = name,
            Permission = permission,
            ExecutorKey = executorKey,
            RequiresApproval = requiresApproval,
            SystemChanging = systemChanging,
            Description = description
        };

    private static bool CommandOrFileExists(string commandOrPath)
    {
        if (string.IsNullOrWhiteSpace(commandOrPath))
        {
            return false;
        }

        if (Path.IsPathFullyQualified(commandOrPath))
        {
            return CandidateCommandPaths(commandOrPath).Any(File.Exists);
        }

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(directory => CandidateCommandPaths(Path.Combine(directory, commandOrPath)))
            .Any(File.Exists);
    }

    private static IEnumerable<string> CandidateCommandPaths(string commandPath)
    {
        yield return commandPath;

        if (Path.HasExtension(commandPath))
        {
            yield break;
        }

        foreach (var extension in ExecutableExtensions())
        {
            yield return commandPath + extension;
        }
    }

    private static IEnumerable<string> ExecutableExtensions()
    {
        var configured = Environment.GetEnvironmentVariable("PATHEXT") ?? "";
        var extensions = configured
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(extension => extension.StartsWith('.') ? extension : "." + extension)
            .Where(extension => extension.Length > 1)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return extensions.Length > 0
            ? extensions
            : [".exe", ".cmd", ".bat", ".com"];
    }
}
