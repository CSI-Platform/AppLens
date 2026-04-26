<p align="center">
  <img src="assets/applens-placeholder-logo.png" alt="AppLens logo" width="120">
</p>

<h1 align="center">AppLens</h1>

<p align="center">
  Read-only workstation inventory and desktop readiness reporting for client audits.
</p>

<p align="center">
  <a href="https://github.com/CSI-Platform/AppLens/actions/workflows/dotnet.yml"><img alt="Desktop CI" src="https://github.com/CSI-Platform/AppLens/actions/workflows/dotnet.yml/badge.svg"></a>
  <img alt="Status" src="https://img.shields.io/badge/status-preview-orange">
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20Linux-blue">
</p>

## Overview

AppLens is a local-first audit tool for understanding what is installed, running, and worth reviewing on a workstation before a consulting, workflow, or AI-readiness engagement.

The repository currently includes three surfaces:

- **AppLens**: cross-platform installed-app inventory scripts for Windows, macOS, and Linux.
- **AppLens-Tune**: read-only workstation diagnostics and tune-plan guidance for startup load, services, local dev tooling, storage hotspots, and repo placement.
- **AppLens-desktop**: a CSI-branded Windows desktop app built with WinUI 3, .NET, and Windows App SDK for eventual Microsoft Store packaging.

## Safety Model

AppLens is intentionally conservative:

- read-only scans by default
- no admin prompt required for V1
- no automatic remediation
- no telemetry, accounts, or cloud upload
- user-controlled report export
- default report redaction for user, machine, and profile-path details

## AppLens-desktop

AppLens-desktop is the Microsoft Store-oriented version of AppLens. It provides a local dashboard, machine summary, inventory review, tune diagnostics, and export options for JSON, Markdown, and local HTML reports.

AppLens-Tune guidance is included as a read-only tune plan with a readiness score, review categories, evidence, backup concepts, and verification steps. Proposed actions are modeled for future user-approved workflows, but AppLens-desktop V1 does not execute remediation.

Build and test:

```powershell
dotnet restore AppLensDesktop.sln
dotnet build AppLensDesktop.sln
dotnet test tests\AppLens.Backend.Tests\AppLens.Backend.Tests.csproj
```

Package smoke build:

```powershell
dotnet publish src\AppLens.Desktop\AppLens.Desktop.csproj `
  -c Release `
  -p:GenerateAppxPackageOnBuild=true `
  -p:AppxPackageSigningEnabled=false
```

More detail is in [docs/AppLensDesktop-Build.md](docs/AppLensDesktop-Build.md), [docs/Store-V1-Scope.md](docs/Store-V1-Scope.md), and [docs/Store-Readiness-Checklist.md](docs/Store-Readiness-Checklist.md).

## Script Usage

### Windows

Double-click:

```text
Run-AppLens.bat
Run-AppLens-Tune.bat
```

PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File AppLens.ps1
powershell -ExecutionPolicy Bypass -File AppLens-Tune.ps1
```

### macOS and Linux

```sh
chmod +x Run-AppLens.sh Run-AppLens-Tune.sh
./Run-AppLens.sh
./Run-AppLens-Tune.sh
```

Or run Python directly:

```sh
python3 AppLens.py
python3 AppLens-Tune.py
```

## Outputs

Script reports are written to the user's Desktop:

- `AppLens_Results_<ComputerName>.txt`
- `AppLens_Tune_Results_<ComputerName>.txt`

The desktop app exports:

- JSON
- Markdown
- local HTML

## Repository Layout

```text
src/AppLens.Backend        Native C# collectors, rules, redaction, reports
src/AppLens.Desktop        WinUI 3 packaged desktop app
tests/AppLens.Backend.Tests Unit and golden report tests
docs/                     Build, Store readiness, roadmap, verification notes
assets/                   Placeholder branding
```

## Project Status

AppLens is in preview. The command-line scripts are usable now. AppLens-desktop builds locally and has a package smoke build, but final Store submission still needs production branding, Partner Center identity, screenshots, a hosted privacy policy URL, and Windows App Certification Kit validation.
