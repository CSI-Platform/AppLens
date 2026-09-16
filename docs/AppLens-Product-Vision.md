# AppLens v1: executable task list

Build a lightweight Microsoft Store app for a guided screen-share session:
**scan -> review apps and storage -> approve removals -> download results for AI review.**

## AppLens v1 implementation boundary

- Ship the first Store release for Windows 11 x64; defer Windows 10 and native ARM64.
- Keep C#/.NET and WinUI 3. Reuse the existing scanner, reports, and shared services.
- Use one scan record for the table and exports. Do not parse the PowerShell text
  report. Preserve distinct installation identities, including duplicate app names.
- Aim for about one second to first useful results; measure it. Show partial results
  and unavailable readings honestly. No fixed delay before enabling actions.
- Use a silver, restrained metallic client with crisp edges, matte-black type,
  one main app table, and secondary device details/action history.
- Keep scans read-only and local. No accounts, telemetry, automatic uploads,
  automatic remediation, forced deletion, or automatic restarts.
- Preserve existing AppLens-Tune in `future/AppLens-Tune/`; core v1 must have no
  runtime or build dependency on it. Keep shared infrastructure in the backend.
  Defer startup/service optimization, top-process rankings, deeper diagnostics,
  and recursive app-folder sizing. Do not redesign Tune or move the inventory
  scripts into a legacy folder.

### Data and download requirements

| Area | Required content |
| --- | --- |
| Summary | Disk total/used/free and percentages, app count, capture time, scan coverage |
| App table | Name, publisher/version, reported size, scope, app type, removal status, action |
| App details | Stable identity, size source, relevant install location, restriction evidence, reliable install/servicing date |
| Device details | Windows version, CPU/GPU, RAM capacity/utilization, uptime |
| Action history | Confirmed target, mechanism, timestamps, outcome, verification, observed disk change |

Missing sizes display as **Unknown**, sort after known sizes, and never imply zero.
Reported size is not guaranteed recoverable space. Scope labels are **This user**,
**All users**, or **Unknown**; scope does not prove who installed an app.
Classify runtimes, updates, drivers, and protected components separately from
ordinary apps. Display groups are not uninstall targets; servicing dates are not
last-used dates.

Keep a visible download icon labeled **Download report**. Save locally through the
Windows picker: Markdown by default, with existing JSON/HTML options retained.
Export the complete captured inventory despite display filters, including unknown
values and action outcomes. Redact personal identifiers/profile paths by default,
with explicit raw-detail opt-in. Export a consistent snapshot and protect existing
reports from silent overwrite. The person sends the file to the consultant.

## Uninstall behavior and Microsoft requirements

Support ordinary MSI, vendor-uninstaller, and eligible Store/MSIX apps, including
admin-required apps. Revalidate the exact local target, confirm the action, use
supported Windows/vendor mechanisms, and preserve UAC and organization controls.
Validate executable paths/arguments; never run commands from imported reports or
interpret arbitrary registry text through a shell.

Show confirmed or possible administrator requirements, managed/protected status,
and unsupported routes truthfully. Enable a row only after its identity and route
are resolved and implemented. Explain relevant data-loss/restart requirements.
Offer cancellation only when supported; opening Windows Installed Apps is a
handoff, not successful removal. Verify removal and show observed disk change.

Use these references during task 3 and recheck requirements for the release:
[Store policies](https://learn.microsoft.com/en-us/windows/apps/publish/store-policies),
[package removal](https://learn.microsoft.com/en-us/uwp/api/windows.management.deployment.packagemanager.removepackageasync?view=winrt-26100),
[capabilities](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations),
[packaging/process model](https://learn.microsoft.com/en-us/windows/apps/get-started/intro-pack-dep-proc),
[installer metadata](https://learn.microsoft.com/en-us/windows/win32/msi/uninstall-registry-key),
[Windows Settings](https://learn.microsoft.com/en-us/windows/apps/develop/launch/launch-settings).
Do not assume changing language grants permissions, add restricted capabilities
without establishing a need, or claim Store approval from local tests.

## Ordered tasks for Linear

Create **one issue per task below only when the user requests the Linear upload**.
Execute in order, with each issue depending on the previous one. One agent owns
the work; **no subagents**. Shared requirements above apply to every issue.

### 1. Separate and preserve AppLens-Tune

- [ ] Map core/shared/Tune dependencies before moving files.
- [ ] Move Tune-specific code, tests, and supporting docs into
      `future/AppLens-Tune/`; update references and preserve shared services.
- [ ] Add a README with entry points, dependencies, verified/unfinished state,
      reason for deferral, and resumption instructions.

**Done when:** the core builds, launches, scans, and exports with Tune unavailable;
no background Tune work runs, and all relocated work is accounted for.

**Start in:** `AuditService.cs`, `TuneCollector.cs`, `Models.cs`, desktop bindings,
reports, and project references.

### 2. Connect the quick scan to the table and reports

- [ ] Extend the shared scan record with the data requirements above.
- [ ] Collect app metadata and disk/device readings without deep diagnostics;
      keep useful results if an optional reading fails.
- [ ] Bind a basic table and existing report writers to that record; keep the
      download control, redaction, and capture state working.

**Done when:** a native scan produces matching rows and saved reports; distinct
identities and unknown values survive; cancellation/partial results work; actual
first-results and completion timings are recorded.

**Start in:** `Models.cs`, `InventoryCollector.cs`, `AuditService.cs`,
`ReportWriter.cs`, `RedactionService.cs`, and `MainWindow.xaml.cs`.

### 3. Verify the Windows uninstall routes

- [ ] Check the current manifest and Microsoft requirements for MSI, vendor-EXE,
      and current-user MSIX removal.
- [ ] Use a small development-package test with disposable apps to establish
      supported routes and administrator/UAC behavior before building the full UI.
- [ ] Record the chosen routes, evidence, restrictions, and required capabilities
      for task 5. Resolve unsupported requirements before dependent work proceeds.

**Done when:** task 5 has a concrete mechanism and evidence for each proposed route;
untested cases and any required external approval are explicit.

**Start in:** `Package.appxmanifest`, `AppLens.Desktop.csproj`, existing action
services, and the Microsoft references above. Use an authorized test environment;
do not remove the user's working apps.

### 4. Finish the client and results table

- [ ] Apply the silver/metallic design, compact disk summary, main table, and
      secondary device/action panels.
- [ ] Add search by app/publisher, name/size/publisher sorting, scope/type/removal
      filters, supporting details, and clear scan/action states.
- [ ] Finish the download interaction and prepare the confirmation/status UI
      using task 3 findings; keep unfinished removal actions disabled.

**Done when:** duplicate names, suites, unknown sizes, and large inventories display
correctly; filtering leaves full exports intact; save/cancel, resizing, keyboard
navigation, text scaling, and high contrast work.

**Start in:** `MainWindow.xaml`, `MainWindow.xaml.cs`, and presentation tests.

### 5. Implement removals and update their results

- [ ] Connect the verified routes to exact-target confirmation, appropriate
      Windows approval, and local action recording.
- [ ] Handle denied approval, cancellation, restrictions, missing uninstallers,
      failure, and restart-required results without breaking the scan.
- [ ] Verify removal, refresh disk readings, and update the row, action history,
      and downloadable report from the same outcome.

**Done when:** real disposable-app removals are verified; admin cases are exercised;
the UI and report agree on outcomes. Distinguish live tests from simulated cases.

**Start in:** the shared action services/`BlackboardStore.cs`, desktop handlers,
and `ReportWriter.cs`; keep Tune dependencies excluded.

### 6. Validate the complete app and prepare the Store release

- [ ] Run the full relevant tests and the installed flow on a clean standard-user
      Windows PC: scan -> filter -> uninstall -> verify -> download/read report.
      Include failure states and confirm Tune independence.
- [ ] Measure launch/scan speed, package and installed size, prerequisites, and
      supported architectures; fix failures and retest affected behavior.
- [ ] Prepare final identity/assets, screenshots, privacy/support URLs, capability
      justification, and certification evidence using the Store readiness checklist.

**Done when:** the customer flow has recorded evidence and release materials are
ready; unresolved requirements are listed. Distribution packaging, external
publication, and Store submission need their explicit authorization. Local tests
do not establish certification; Store installation is verified after publication.

**Start in:** solution tests, desktop run/build guidance, and
[Store readiness checklist](Store-Readiness-Checklist.md).

## Working rules

- Run focused checks with each change; perform the comprehensive review in task 6.
  Do not repeat the full release review after every task.
- When Linear work begins, record progress, decisions, and test evidence there;
  keep this document as scope and acceptance criteria, not a duplicate status log.
- Resolve consequential uncertainty before relying on it. Do not expand scope,
  silently drop required functionality, or request approval for every routine step.
