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
            RiskLevel = "medium",
            Capabilities = ["lane-status", "scorecard-import", "fit-report-import"],
            Entrypoints = ["status_command", "run_job", "import_reports"],
            DataContracts = ["runtime-lanes", "blackboard-record", "model-fit-scorecard", "fit-report", "benchmark-record"],
            ActionContracts = ["bounded-local-command"],
            Privacy = ["raw_private", "sanitized", "exportable"],
            StatusContract = "file-only"
        },
        new ModuleManifest
        {
            AppId = "oracle-workbench",
            DisplayName = "Oracle",
            ModuleId = "oracle",
            RiskLevel = "medium",
            Capabilities = ["research-status", "read-only-job", "report-import"],
            Entrypoints = ["status_command", "run_job", "import_reports"],
            DataContracts = ["research-report", "run-record", "data-provenance"],
            ActionContracts = ["read-only-research-job"],
            Privacy = ["raw_private", "exportable", "invalidated"],
            StatusContract = "file-only"
        },
        new ModuleManifest
        {
            AppId = "mailbox",
            DisplayName = "Mailbox",
            ModuleId = "mailbox",
            RiskLevel = "medium",
            Capabilities = ["open_ui", "status_http", "folder-status"],
            Entrypoints = ["open_ui", "status_http"],
            DataContracts = ["filesystem-mailbox", "markdown-message", "attachment-artifact"],
            ActionContracts = ["open-only-v1"],
            Privacy = ["raw_private", "sanitized"],
            StatusContract = "file-or-http-status"
        },
        new ModuleManifest
        {
            AppId = "applens-zero",
            DisplayName = "AppLens-Zero",
            ModuleId = "zero",
            RiskLevel = "high",
            Capabilities = ["lab-readiness", "passive-import", "authorized-session"],
            Entrypoints = ["import_reports"],
            DataContracts = ["lab-authorization", "edge-device-profile", "capture-readiness"],
            ActionContracts = ["benign-check-only"],
            Privacy = ["raw_private", "sanitized"],
            StatusContract = "file-only"
        }
    ];

    private ModuleStatus LlmStatus()
    {
        var pyproject = Path.Combine(_paths.AppLensLlmRoot, "pyproject.toml");
        var cli = Path.Combine(_paths.AppLensLlmRoot, "src", "applens_llm", "cli.py");
        if (!File.Exists(pyproject) || !File.Exists(cli))
        {
            return Blocked("llm", "applens-llm", "AppLens-LLM", "AppLens-LLM package path is unavailable.", _paths.AppLensLlmRoot);
        }

        return Available("llm", "applens-llm", "AppLens-LLM", "Package and CLI source detected.");
    }

    private ModuleStatus OracleStatus()
    {
        var pyproject = Path.Combine(_paths.OracleRoot, "pyproject.toml");
        if (!File.Exists(pyproject))
        {
            return Blocked("oracle", "oracle-workbench", "Oracle", "Oracle repo or pyproject.toml is unavailable.", _paths.OracleRoot);
        }

        return Available("oracle", "oracle-workbench", "Oracle", "Repo and CLI project detected.");
    }

    private ModuleStatus MailboxStatus()
    {
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
}
