# AppLens v1 handoff to COP-241 — updated 2026-09-14

## Start here

Continue in this existing worktree:
`C:\Users\codyl\Desktop\csiOS\Projects\.worktrees\AppLens\codex-applens-v1-quick-client`

Branch: `codex/applens-v1-quick-client`, based on `33be555`. The user authorized
commit and push on 2026-09-14, specifically to `CSI-Platform/AppLens`. Check the
branch log/upstream for the saved revision and `git status` for any later edits.
No merge, distribution package or Store publication is authorized by that push.
The original AppLens checkout and its 13 pre-existing changed/untracked
files were preserved and carried into this worktree before implementation.
Do not switch to the original checkout or discard its work. Use one agent;
no subagents. Read this worktree's AGENTS.md before continuing.

[Linear project](https://linear.app/csi-platform/project/applens-platform-final-push-bda8c2d44ddf)
· [Approved six-task plan](https://linear.app/csi-platform/document/applens-v1-approved-six-task-delivery-plan-fafa6b12136a).
COP-236/237/238/239/240 are Done; COP-241 remains In Progress.

The 2026-09-14 source/release review is complete without desktop control:
[release validation and remaining hands-on decisions](AppLens-Release-Validation.md).
Fresh core/Tune tests, x64/ARM64 builds, dependency and snapshot checks passed.
Store settings now bundle .NET; a regression checks the evaluated release
properties without generating a package. Store reviewer instructions are drafted.
Evidence: `artifacts/cop241-source-20260914-1e406cb1/` (local, not pushed).
The fresh backend smoke had 433 entries and complete coverage at ~0.52 seconds.
It writes a new scan and does not reload desktop action history; retain the
earlier seven-action UI export as that evidence.

No launch-scope reduction has been approved. Windows 11 x64-only was offered as
an option to defer Windows 10/ARM64 hardware checks; source currently retains both
architectures and its existing minimum OS. Ask/verify the owner's decision before
changing release targets. Do not silently waive any remaining checks.

**Computer use resumed with the owner's permission on 2026-09-14.** Run unattended
AppLens checks first and reserve native approval prompts for the owner's return.
Original Custom theme, 100% text size and high contrast off remain the baseline.
Native UAC must be approved by the user; broad administrator authorization does
not bypass Windows' secure-desktop approval. See release validation for new evidence.

Latest unattended checks passed: real exit-code failure, the production three-
minute timeout and retry guard, NoRemove protection, and closing/reopening the
client during an action. The downloaded JSON retains all ten history records,
including Failed, Still running and Outcome unknown; identifiers are redacted.
The four owned registrations were removed after verification; helper processes
exited naturally. No display settings changed. Evidence:
`artifacts/cop241-live-63a8a7fdb352457da74b8b70569b5fb3/`.
A fresh development package was installed after the owner's native approval:
`artifacts/removal-preflight-d51377dddcd24574bab23311c5b823dd/installation-plan.json`.
Installed scan, filtering, self-protection, disposable-package removal, Downloads
picker cancellation and JSON save passed. The latest report has 434 entries and
eleven actions; its hash and ledger survived the usage interruption. The client
remains installed but closed; its disposable package is absent. Do not confuse
this bundled-runtime test identity with final Store delivery. Actual Narrator
speech remains untested; include keyboard focus after picker cancellation in
that pass (an automated Alt+D attempt did not reopen it; the button worked).

## Product and completed implementation

AppLens v1 is the local C#/.NET 10 WinUI client: fast inventory → disk/app table
→ exact-target approved removal → verification → local report download for AI
review. Silver/metallic visual design, no accounts/telemetry/upload or automatic
remediation. Reported app size is an estimate; disk deltas are observations,
not guaranteed space recovered from an app.

Tune-specific code/scripts/tests/docs are preserved in `future/AppLens-Tune/`
with a move-time hash manifest and README. Core build/launch/scan/export were
exercised with that directory unavailable. Shared evidence contracts remain in
the backend; core `InventorySnapshot` and reports contain no Tune diagnostics.

The table preserves installation identities, searches names/publishers, sorts
names/publishers/numeric sizes, and filters scope/type/removal route. Unknown
sizes sort last. Ordinary apps are the default; components remain available.
MSI scope comes from Windows Installer context, not HKLM alone. Details, disk
readings, status, history and JSON/Markdown/HTML reports use the shared scan data.
Filters do not truncate exports; personal identifiers are redacted by default.

Removal uses conservative vendor-EXE parsing, typed MSI GUID commands with
/norestart, current-user MSIX, revalidation and single-use confirmation tokens,
Windows approval, independent presence checks and persistent action outcomes.
Ambiguous routes open Windows for review; protected components/self-removal are
blocked. Never execute commands imported from a report.

COP-239's final fixes keep the virtualized list visible when large text and open
panels need page scrolling, align wrapped table headers/rows, expose meaningful
row/action accessibility names, and start the save picker in Downloads using the
current user's known-folder path. No framework/language rewrite or SDK upgrade.

## Verification to retain

[Client verification](AppLens-Client-Verification.md) maps each COP-239 requirement
to local evidence and lists the later computer-use checks.

- Final x64 and ARM64 desktop builds succeeded with zero warnings/errors.
  ARM64 cross-compilation is not ARM64 runtime verification.
- Latest core run: **94 passed** (82 backend, 12 presentation). TRXs are under
  `artifacts/cop239-client-26922a8cb41d482db372f2590b9c386e/final-tests/`.
- Live Windows 11 x64 client: 433 inventory entries, 158 ordinary apps; latest
  complete scan 0.52 s on this developer PC. No clean-PC timing guarantee.
- Keyboard navigation, real duplicate names/suite entries, unknown-size ordering,
  1280×880/960×760 layouts, 100%/225% text, Night sky/Desert high contrast,
  UI Automation names, confirmation cancellation and native save/cancel passed.
- Latest zero-visible-row JSON export contains all 433 entries and seven actions
  (four Removed, three Cancelled), matching the ledger with identifiers redacted.
  See `final-report-verification.json` and `client-export-zero-visible.json` in
  that same artifact directory. Original report remains in Downloads as
  `AppLens-20260913-174908-731.json`; its hash is recorded in the verification file.
- Original display settings and owned registry-fixture cleanup are documented in
  `display-restored.json` and `ui-fixture-cleanup.json`. No working app was removed
  in COP-239. The extra fixture cancellation is intentionally kept in the ledger.
- Earlier validation: 58 preserved Tune tests, five Windows snapshot check groups,
  no known NuGet vulnerabilities reported, and physical Tune-independence checks.
  These are earlier evidence, not fresh release/certification results.

COP-240 evidence is preserved under
`artifacts/cop240-live-6e40495c2ab845f89db8f4373f6ef81b/`:

- The initially installed WinUI wrapper failed to launch because its resource
  index was absent/wrongly identified. `Build-DevelopmentProbe.ps1` now indexes
  compiled XAML for the test package identity. The resource regression failed
  against the original payload and passed for corrected packages.
- Corrected packaged client launched unelevated from WindowsApps with only
  runFullTrust. Real Store confirmation cancellation/approval and all-user MSI
  native UAC denial/approval were independently verified. Denial kept the fixture;
  approval removed it and refreshed table/disk/history. MSI/vendor tests also passed.
- Report verification found and fixed history reload choosing an approval from a
  newest-first query. RemovalService now prioritizes the recorded completion;
  the BlackboardStore regression failed before the fix. That run had 93 tests;
  the additional COP-239 accessible-name test brings the current total to 94.
- Final development plan:
  `artifacts/removal-preflight-644303be956d47be8208d88d1bdedecb/installation-plan.json`.
  The package was installed/tested and then removed. All six owned package
  identities and the all-user MSI were verified absent during cleanup. No owned
  client process remained. The six-action report in COP-240 is historical;
  COP-239's seven-action report is newer. Do not rewrite either or the ledger.
- Use `machine-fixture-plan-verified.json` for the generated MSI hash; its earlier
  incomplete record hit a COM file lock. Keep failed/superseded raw artifacts.

## COP-241: short execution order

1. Read this handoff, release/client verification and Store checklist; inspect
   the branch/status. Reuse completed evidence, and rerun checks only for a changed build,
   changed behavior or an unresolved failure. Review final source/package choices.
2. When desktop control is available, validate a final installed candidate on a
   clean standard-user PC: offline scan → filter → disposable uninstall → verify
   → download/read report. Downloads now passed in the latest development package;
   retain a final-candidate spot check. Check actual screen-reader audio
   and modal focus recovery. Real failed/slow, NoRemove protection and app closure
   now passed on the developer PC; a genuine managed-policy denial remains.
3. Verify claimed Windows 10 and ARM64 runtime configurations on appropriate
   systems. Measure launch/first-results/completion, download size, installed
   footprint and prerequisites for the actual distribution. Earlier 86.89 MiB
   compressed / ~217.49 MiB payload figures describe a development package only.
4. Finish owner inputs: Partner Center name/publisher/identity, artwork and private-
   data-free screenshots, contact and real privacy/support URLs. Drafts exist;
   `CSI.AppLensDesktop` / `CN=CSI` is still a placeholder. SQLite bundle is 2.1.13
   (observed native SQLite 3.53.3). Recheck current Microsoft policies when preparing
   release. Obtain explicit distribution/hosting/submission authorization before
   those actions, then run WACK on the final candidate and record its result.
   Store download/install is verified only after authorized publication.

Do not mark COP-241 Done based on local builds. WACK exists on this machine but
has not been run against a final package. No clean-PC, Windows 10 or ARM64 live
result, final prerequisite/footprint measurement or Store certification is claimed.

## Commands and references

Run from the worktree above:

```powershell
dotnet restore AppLensDesktop.sln
dotnet build AppLensDesktop.sln -c Release
dotnet test AppLensDesktop.sln -c Release --no-build
dotnet build src/AppLens.Desktop/AppLens.Desktop.csproj -c Release -p:Platform=ARM64
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Script.Tests/Test-StoreConfiguration.ps1
```

To review that exact x64 Release build when desktop control resumes:

```powershell
& .\src\AppLens.Desktop\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\AppLens.Desktop.exe
```

`Run-AppLensDesktop.ps1` launches Debug, which can be stale unless rebuilt.
Use a unique test-results directory/logger filename; do not overwrite earlier TRXs.
Builds do not scan the PC; `tools/AppLens.Smoke` explicitly scans and writes evidence.
Development-package instructions: [RemovalProbe README](../tools/AppLens.RemovalProbe/README.md).
`Build-StoreCandidate.ps1` requires separate distribution authorization.

[Build and keyboard guidance](AppLensDesktop-Build.md) ·
[Uninstall routes](AppLens-Uninstall-Routes.md) ·
[Store checklist](Store-Readiness-Checklist.md) ·
[Product scope](AppLens-Product-Vision.md).
