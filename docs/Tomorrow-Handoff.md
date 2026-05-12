# Tomorrow Handoff

## Current State

AppLens-desktop is usable locally as a scan-and-tune candidate.

Implemented:

- installed app inventory
- AppLens-Tune diagnostics
- readiness score and highlights
- consent-based tune plan actions
- action log export
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

- replace placeholder CSI assets
- capture screenshots from a clean scan
- reserve final Store app name
- create hosted privacy/support URLs
- run Windows App Certification Kit
- generate final Partner Center upload package
- submit for certification

## Keep Out Of V1

- app uninstall
- unapproved startup, service, or cache changes
- unattended admin elevation
- automatic remediation without action logs

