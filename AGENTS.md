# AppLens Agent Instructions

Installed (formerly AppLens) v1 is a lightweight Windows client for guided screen-share sessions:
scan -> review/filter apps and storage -> approve supported uninstalls -> verify
results -> save a local report. Keep communication concise and plain-English.

The Store product is Installed (9PBPFLL6JVVV). Keep AppLens repository, namespaces,
legacy storage paths and DPAPI purpose identifiers stable; renaming those can
break history compatibility. AppLens-Tune remains a separate deferred component.

## Product scope

- Target Windows 11 x64 and Microsoft Store distribution. Users should install
  from the AppLens website/Store and open the installed app from an icon, including
  a desktop shortcut, without developer tools or manual runtime/certificate setup.
- Follow [Product Vision](docs/AppLens-Product-Vision.md). Keep the silver/metallic
  interface focused on the v1 flow; scan, table, history and reports share one model.
- Preserve deferred Tune in `future/AppLens-Tune/`. Core v1 must have no build,
  runtime or background-probe dependency on it; keep needed shared backend services.
- Scans are read-only. System changes require explicit confirmation, supported
  mechanisms and recorded outcomes. Preserve Windows approval/policy controls;
  do not add automatic remediation, forced deletion or automatic restarts.
- Keep the approved local-only privacy design: no accounts, telemetry or uploads;
  Windows current-user protection for new history without plaintext duplicates;
  readable reports redacted by default. Preserve legacy evidence unchanged.

## Working rules

- One agent; no subagents. Keep changes within the agreed v1 scope.
- Default to under 120 words, lead with the result, and use plain English.
  Summarize verification once. Create reports, plans, handoffs or documentation
  files only when explicitly requested.
- Active working copy: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens-clean`,
  branch `codex/applens-clean`. The original checkout and worktrees are preserved.
- The [handoff](docs/AppLens-v1-Handoff.md) contains historical release evidence;
  its worktree path and issue status are historical. Verify current state before
  release work. Remote:
  `https://github.com/CSI-Platform/AppLens.git` (main product, not SSH or LLM).
- Honor recorded approvals; do not request them again. Keep native Windows
  approvals with the owner. Local builds do not authorize hosting changes, merge,
  Store submission or publication; consult the handoff for each action's scope.
- Reuse recorded validation; rerun checks for changed behavior/builds, changed
  environments or unresolved failures. Preserve raw evidence and saved reports.
- Keep current status and evidence in Linear and the handoff. This file holds
  durable rules. Distinguish local tests from installed, clean-PC and Store results.

## Shared engineering standards

Read the relevant shared standards before acting; keep project-specific scope and
approval rules above intact. Flag inaccessible references rather than guessing.

- [Testing](C:/Users/codyl/Desktop/csiOS/STANDARDS/rules/testing.md): behavioral coverage and evidence.
- [Safety](C:/Users/codyl/Desktop/csiOS/STANDARDS/rules/safety.md): data, access, secrets and recovery.
- [Quality](C:/Users/codyl/Desktop/csiOS/STANDARDS/rules/quality.md): acceptance, builds, usability and review.
- [Release](C:/Users/codyl/Desktop/csiOS/STANDARDS/rules/release.md): CI and authorized delivery.

## Development and release references

```powershell
dotnet restore AppLensDesktop.sln
dotnet build AppLensDesktop.sln -c Release
dotnet test AppLensDesktop.sln -c Release --no-build
```

Use [desktop build guidance](docs/AppLensDesktop-Build.md) for launch commands and
focused checks. `tools/Build-StoreCandidate.ps1` builds a local unsigned candidate;
use the handoff's recorded packaging authorization. A candidate is not a release.

Before release work, read [validation](docs/AppLens-Release-Validation.md),
[Store readiness](docs/Store-Readiness-Checklist.md) and the
[submission plan](docs/AppLens-Store-Submission-Plan.md).
