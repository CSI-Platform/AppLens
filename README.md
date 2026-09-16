<p align="center">
  <img src="src/AppLens.Desktop/Assets/AppLensSquare150.scale-200.png" alt="AppLens logo" width="120">
</p>

<h1 align="center">AppLens</h1>

<p align="center">
  CSI's local control board for managing workstation apps, agents, evidence, and approvals.
</p>

<p align="center">
  <a href="https://github.com/CSI-Platform/AppLens/actions/workflows/dotnet.yml"><img alt="Desktop CI" src="https://github.com/CSI-Platform/AppLens/actions/workflows/dotnet.yml/badge.svg"></a>
  <img alt="Status" src="https://img.shields.io/badge/status-preview-orange">
  <img alt="Store client platform" src="https://img.shields.io/badge/Store%20client-Windows%2011%20x64-blue">
</p>

## Overview

AppLens is CSI's mobile-OS-inspired local control board. It gives a workstation one place to see apps, agents, evidence, proposed actions, and operator approvals.

The first Store release is now centered on a quick client-session workflow:
install AppLens, scan, review a sortable app table, approve supported uninstalls,
and verify the result. Its approved launch target is **Windows 11 x64**; Windows 10
and native ARM64 support are deferred. The scripts/platform work below has a
broader scope than this Store client. See [Product Vision](docs/AppLens-Product-Vision.md) for the
current product direction and proposed client design. The broader platform below
describes existing infrastructure and future possibilities, not requirements for
the first-release interface.

The platform is organized around apps/modules that publish state into CSI's proprietary blackboard layer:

- **Scanner**: local workstation inventory and readiness evidence.
- **Tune**: diagnostics, proposed fixes, approvals, execution records, and verification.
- **Blackboard**: the local evidence, status, policy, and action ledger.
- **Planner**: operator planning for multi-step local work.
- **Future modules**: Fleet, RAG, MCP, and Gov.

The active desktop client runs the core inventory scan and report download.
Existing Tune diagnostics and the old window are preserved in
[future/AppLens-Tune](future/AppLens-Tune/README.md), outside the v1 build.

## Safety Model

AppLens is local-first and operator-controlled:

- read-only scans by default
- approval-gated actions
- no automatic remediation without an explicit grant
- no telemetry, accounts, or cloud upload
- user-controlled report export
- default report redaction for user, machine, and profile-path details

## AppLens-desktop

AppLens is the WinUI 3 inventory client. It provides storage and device readings,
a searchable/sortable app table, confirmed Windows/vendor uninstall routes,
verification, local action history, and full JSON/Markdown/HTML downloads.
See the [uninstall route contract](docs/AppLens-Uninstall-Routes.md) and
[Store readiness](docs/Store-Readiness-Checklist.md) for verification limits.

Tune actions are modeled as proposals, approvals, executions, and verification records. System-changing behavior must remain explicit, reversible where practical, and blackboard-recorded.

Build and test:

```powershell
dotnet restore AppLensDesktop.sln
dotnet build AppLensDesktop.sln
dotnet test AppLensDesktop.sln
```

Run locally:

```powershell
.\tools\Run-AppLensDesktop.ps1
```

Distribution candidate build (requires explicit authorization):

```powershell
.\tools\Build-StoreCandidate.ps1
```

More detail is in [docs/AppLensDesktop-Build.md](docs/AppLensDesktop-Build.md), [docs/AppLens-Platform-Scope.md](docs/AppLens-Platform-Scope.md), and [docs/Store-Readiness-Checklist.md](docs/Store-Readiness-Checklist.md).

Platform module docs:

- [Scanner](docs/AppLens-Scanner.md)
- [Tune](docs/AppLens-Tune-App.md)
- [Blackboard](docs/AppLens-Blackboard.md)
- [Planner](docs/AppLens-Planner.md)
- [Platform Shell](docs/AppLens-Platform-Shell.md)

## Script Usage

### Windows

Double-click:

```text
Run-AppLens.bat
future\AppLens-Tune\Run-AppLens-Tune.bat
```

PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File AppLens.ps1
powershell -ExecutionPolicy Bypass -File future\AppLens-Tune\AppLens-Tune.ps1
```

### macOS and Linux

```sh
chmod +x Run-AppLens.sh future/AppLens-Tune/Run-AppLens-Tune.sh
./Run-AppLens.sh
./future/AppLens-Tune/Run-AppLens-Tune.sh
```

Or run Python directly:

```sh
python3 AppLens.py
python3 future/AppLens-Tune/AppLens-Tune.py
```

## Outputs

Script reports are written to the user's Desktop:

- Windows Scanner: `AppLens_Results_<ComputerName>_<Timestamp>.txt`
- macOS/Linux Scanner: `AppLens_Results_<ComputerName>.txt`
- `AppLens_Tune_Results_<ComputerName>.txt`

The Windows initial scan includes machine/OS/CPU/GPU details, uptime, RAM usage,
total/used/free space and utilization for each local fixed disk,
low-disk/high-RAM flags, and the categorized app inventory.
Unavailable workstation readings are labeled explicitly. It uses lightweight
local queries without recursive folder scans or starting the Tune runtime probes.

Windows scans preserve previous reports. An optional `-OutputPath` selects a new
report path; existing files are rejected. Script text reports contain local
computer/user identifiers; desktop-app export redaction is a separate feature.

Run the Windows snapshot regression checks:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests\Script.Tests\Test-AppLensSnapshot.ps1
```

The desktop app exports:

- JSON
- Markdown
- local HTML

After a scan, **Download report** (the download icon beside Scan this PC) saves the
selected Markdown, JSON or HTML report through the Windows picker, starting in
Downloads. Exports contain the full scan even when table filters hide rows.
The person can send that file for review; AppLens does not upload it. Personal
identifiers are redacted by default unless explicitly included. Alt+S starts a
scan and Alt+D downloads. See [client verification](docs/AppLens-Client-Verification.md)
and the [continuation handoff](docs/AppLens-v1-Handoff.md).
The [release validation review](docs/AppLens-Release-Validation.md) separates
completed checks from final installation, platform and Store work.

## Repository Layout

```text
src/AppLens.Backend         Native C# collectors, rules, blackboard, module status, reports
src/AppLens.Desktop         WinUI 3 platform shell
tests/AppLens.Backend.Tests Backend unit and golden report tests
docs/                      Platform, module, roadmap, build, and Store readiness notes
assets/                    Placeholder branding
```

## Project Status

AppLens v1 is a local preview. The core client builds independently of the preserved Tune work. Store release still requires the remaining installed/clean-PC checks, owner-reviewed materials, Partner Center identity, published privacy/support URLs, and certification. Local builds do not establish Store availability.
