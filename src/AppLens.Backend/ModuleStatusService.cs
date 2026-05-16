namespace AppLens.Backend;

public sealed class ModuleStatusPaths
{
    public string AppLensLlmRoot { get; init; } = ConfiguredPath("APPLENS_LLM_ROOT", DefaultProjectPath("AppLens-LLM"));
    public string OracleRoot { get; init; } = DefaultOraclePath();
    public string MailboxRoot { get; init; } = ConfiguredPath("APPLENS_MAILBOX_ROOT", DefaultProjectPath("Mailbox"));
    public string AppLensZeroRoot { get; init; } = ConfiguredPath("APPLENS_ZERO_ROOT", DefaultProjectPath("AppLens-Zero"));

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
        ZeroStatus()
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
}
