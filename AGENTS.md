# AppLens Agent Instructions

Be concise. This file is durable project context, not a task log.

## Project Context

AppLens is CSI's local control board for managing workstation apps, agents, evidence, approvals, scans, diagnostics, and local reports.

First Store release direction: a lightweight, silver/metallic Windows client for
guided screen-share sessions: scan, review/filter an app table, approve supported
uninstalls, and verify results. Follow `docs/AppLens-Product-Vision.md`; distinguish
the user's requirements from proposed implementation choices. Keep existing
platform infrastructure from expanding the first-release interface by default.

AppLens-Tune means the existing Tune implementation. It is intentionally deferred:
separate Tune-specific code into `future/AppLens-Tune/`, preserve its work and
document how to resume it, and keep infrastructure needed by v1 in the shared
backend. Core v1 must operate without AppLens-Tune, including background probes.
Follow the ordered executable tasks in the product vision document. One agent
owns this work with no subagents. Scan, table, and report changes share one model.

Canonical local path: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens`.

GitHub remote: `https://github.com/CSI-Platform/AppLens.git`.

Current branch at csiOS inventory time: `main`.

## Product Boundaries

- AppLens is local-first and operator-controlled.
- Read-only scans are the default.
- System-changing behavior must remain explicit, approval-gated, reversible where practical, and recorded.
- No telemetry, accounts, or cloud upload should be added without explicit product approval.

## Development

Primary desktop commands from the README:

```powershell
dotnet restore AppLensDesktop.sln
dotnet build AppLensDesktop.sln
dotnet test AppLensDesktop.sln
.\tools\Run-AppLensDesktop.ps1
```

Package smoke build:

```powershell
.\tools\Build-StoreCandidate.ps1
```

## Useful Starting Points

- `README.md`
- `docs/AppLensDesktop-Build.md`
- `docs/AppLens-Platform-Scope.md`
- `docs/Store-Readiness-Checklist.md`
- `docs/AppLens-Scanner.md`
- `docs/AppLens-Tune-App.md`
- `docs/AppLens-Blackboard.md`

## No-Touch Boundaries

- Do not add automatic remediation.
- Do not change privacy/telemetry posture without explicit approval.
- Do not publish or package for distribution without explicit approval.
- Do not overwrite raw evidence, artifacts, or user-generated reports without approval.

## Project Management

Current work state belongs in Linear and handoff docs, not in this file.
