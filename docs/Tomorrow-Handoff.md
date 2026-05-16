# Tomorrow Handoff

## Current State

AppLens-desktop is usable locally as the AppLens platform shell.

Implemented:

- installed app inventory
- AppLens-Tune diagnostics
- readiness score and highlights
- tune plan guidance
- approval-gated Tune action execution through the platform loop
- blackboard-backed scan and action records
- module status cards for configured capabilities
- JSON, Markdown, HTML, and bundle export
- default redaction with explicit raw-detail option
- MSIX smoke package generation
- GitHub CI

## Use It Locally

```powershell
.\tools\Run-AppLensDesktop.ps1
```

Or run the executable directly:

```powershell
.\src\AppLens.Desktop\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\AppLens.Desktop.exe
```

## Rebuild The Store Candidate

```powershell
.\tools\Build-StoreCandidate.ps1
```

This runs restore, tests, package smoke build, lists generated MSIX artifacts, and checks whether Windows App Certification Kit is available.

## High-Confidence Next Work

- make Tune evidence/backup/verification detail more legible in the desktop UI
- add blackboard verification records after follow-up scans
- move module definitions toward manifest/config files
- add schema artifacts for machine profiles, tune plans, and ledger events
- replace placeholder CSI assets
- capture screenshots from a clean scan
- reserve final Store app name
- create hosted privacy/support URLs
- run Windows App Certification Kit
- generate final Partner Center upload package
- submit for certification

## Keep Out Of The Store Build Until Proven

- hidden or automatic remediation
- broad filesystem cleanup
- unsupported rollback claims
- silent admin elevation
- network upload or account requirements

