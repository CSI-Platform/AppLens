# AppLens v1 handoff to COP-241 — updated 2026-09-18

## Start here

Continue in this existing worktree:
`C:\Users\codyl\Desktop\csiOS\Projects\.worktrees\AppLens\codex-applens-v1-quick-client`

Branch: `codex/applens-v1-quick-client`, based on `33be555`. The user authorized
commit and push on 2026-09-14, specifically to `CSI-Platform/AppLens`. Check the
branch log/upstream for the saved revision and `git status` for any later edits.
That push authorization does not authorize merge or Store publication. The later
2026-09-16 delegation below separately authorizes privacy work and local packaging.
The original AppLens checkout and its 13 pre-existing changed/untracked
files were preserved and carried into this worktree before implementation.
Do not switch to the original checkout or discard its work. Use one agent;
no subagents. Read this worktree's AGENTS.md before continuing.

[Linear project](https://linear.app/csi-platform/project/applens-platform-final-push-bda8c2d44ddf)
· [Approved six-task plan](https://linear.app/csi-platform/document/applens-v1-approved-six-task-delivery-plan-fafa6b12136a).
COP-236/237/238/239/240 are Done; COP-241 remains In Progress.

## Latest: Store direction confirmed — 2026-09-18

The owner chose to continue with Microsoft Store distribution after considering
a separate website download. The website should be the installation entry point;
customers must be able to install and then open AppLens from an icon, including
double-clicking a desktop shortcut, without developer tools or manual setup.
The [product scope](AppLens-Product-Vision.md), [Store checklist](Store-Readiness-Checklist.md)
and [submission plan](AppLens-Store-Submission-Plan.md) now make that acceptance
requirement explicit. The approved six-task plan in Linear is updated to match.

Keep Microsoft Store as the release channel. If pricing is free and the app is
eligible, prefer Microsoft's website installer through the official Direct-mode
badge; otherwise link the website button to the Store listing. Pricing is still
undecided. No separate direct-download installer, signing service or update
infrastructure is planned. Website installation, desktop-shortcut behavior and
updates are not yet verified. Do not confuse the website install button with
the client's Download report control.

Next work remains under COP-241:

1. Obtain exact Partner Center app/publisher identity and verified contact/listing
   facts; replace placeholder values before freezing a final candidate.
2. Review/complete desktop-shortcut creation and test install -> open -> close ->
   double-click icon, plus a same-identity update preserving history/launch access.
3. Complete the existing clean Windows 11 x64, offline, accessibility, history
   retention/cross-user and managed-policy checks; run WACK on the final candidate.
4. Finalize privacy/support pages, screenshots and website installation integration.
   Verify actual Store installation and updates after authorized publication.

Reviewed and shortened AGENTS.md: removed broad legacy platform context, the
obsolete main-branch inventory note, completed Tune-separation instructions,
duplicated restrictions and unrelated starting points. It now holds durable v1
scope, customer simplicity, privacy/evidence rules, one-agent workflow and focused
references; active branch, approvals and evidence stay here and in Linear.

This pass changes documentation only. It started clean at `ff5cf0d`, matching the
remote; COP-241 was verified In Progress. Existing source/package/lifecycle
evidence below is reused; no build or installed test was repeated. The Store
decision continues preparation, not submission/publication. Local builds and
branch commit/push remain authorized; hosting changes, merge, Store submission
and publication remain held. Owner-operated native approvals are still required.

## Retained privacy and local packaging verification — 2026-09-16

After Windows 11 x64 preparation was pushed as `425c934`, the owner delegated
privacy and authorization decisions: "for privacy and auth can you do those for
me? ... i'll let you decide what's best". Proceed without asking again about this
privacy design or local candidate builds. Keep merge, hosting, Store submission
and publication held; native Windows prompts still require the owner. AppLens
remains account-free, local-only and explicit about uninstall approval.

Implemented current-user Windows DPAPI protection for every new v1 history
record, with a separate versioned encrypted log and no plaintext SQLite index.
The shared/deferred Blackboard implementation stays available; legacy evidence
is read-only, combined in memory and never migrated or deleted. Existing plaintext
development logs and reports are not retroactively encrypted. Corrupt protected
history fails explicitly and prevents the approval write/uninstaller start.
The client carries unavailable-history warnings into history and report coverage,
including when history loading finishes after a scan.

Verification: all **104 core tests** passed (92 backend, 12 presentation), including
ten new privacy cases; **58 preserved Tune tests** passed. The privacy regression
first failed against plaintext storage. Separate processes wrote/recovered a
synthetic protected record under the same Windows account. Read-only default
history loading recovered all eleven real legacy actions; hashes of every existing
runtime file remained unchanged. The dependency audit reports no known vulnerable
packages. No installed lifecycle test was repeated.

`Build-StoreCandidate.ps1` passed and now finds the installed symbol conversion
tool and WACK correctly; it saves test TRXs in each candidate directory. A final
publish compiled the reviewed UI change without warnings/errors. The inspected
candidate is `artifacts/store-candidate-20260916-011555-3476646/install/`:
`AppLens.Desktop_0.1.0.0_x64.msixbundle`, **48,761,419 bytes**, SHA-256
`B65A7A56C6B03DC2552B71BBBFB10944D4B7E1E3351E12D285BC16D3C75591B6`.
Actual bundle contents are x64 only, Windows minimum 22000, bundled .NET and
Windows protection assembly, compiled resources, and Windows App Runtime 1.8
framework dependency. No deferred Tune, probe or raw evidence paths are packaged.
Identity remains `CSI.AppLensDesktop` / `CN=CSI`, version `0.1.0.0`; unsigned,
not installed or submitted. Do not upload it as the final Store package.

Evidence: `artifacts/cop241-privacy-20260916-010513/`; final core TRXs are in
`artifacts/store-candidate-20260916-011102-4248971/test-results/`. Earlier attempts
and evidence remain intact. Privacy/support/checklist and
[submission packet](AppLens-Store-Submission-Plan.md) reflect the implementation.

Remaining: exact Partner Center identity/account and listing facts, verified
contact/hosting destinations, screenshots/artwork review, a clean standard-user
Windows 11 x64 environment for final install/offline/Narrator/focus/retention and
cross-user protection checks, and a managed-policy denial environment. Owner
operates native installation/UAC/WACK approvals. No prompt, certificate trust,
system-policy change or installed-client replacement was attempted while away.
COP-241 remains In Progress. The earlier privacy/build-approval gaps below are
historical and superseded; final installed evidence and external release approval
remain open.

## Earlier scope preparation — 2026-09-16

The owner again authorized local completion, Linear/handoff updates, commit and
push to this existing branch; no merge, publication or Store submission. The path
in that prompt was missing the separator before `.worktrees`; Git verified the
existing path above, branch and remote. Starting local/remote SHA was `9be420b`,
working tree clean. COP-241 was verified In Progress and COP-240 Done. No GitHub PR
or branch CI run exists; source tests remain local evidence.

Unattended preparation is complete for this pass. Read the new
[Store submission packet](AppLens-Store-Submission-Plan.md) for exact Partner
Center inputs, screenshot plan and a coordinated final-candidate test sequence.
The checklist now reuses successful installed/lifecycle checks instead of listing
them as wholly pending. Current advisory query reports no known vulnerable NuGet
packages for the four solution projects. The original installed report and local
copy match the recorded hash; retained core TRXs confirm 94 passes. No completed
installed/uninstall lifecycle test was repeated. WACK and existing artwork
dimensions were inspected only.
Evidence: `artifacts/cop241-submission-prep-20260916-003337/`.

Microsoft policy 7.19 remains effective until the published 7.20 takes effect on
2026-10-22; both were reviewed. This phase found plaintext local history and
proposed current-user protection; the later delegation and implementation above
resolve that design/build-approval gap. Final-package retention remains open.

The owner then chose **Windows 11 x64 first**, followed by "I'm going to sleep;
do as much as you can without me." Applied build 22000 minimum to the desktop
project/manifest, x64-only runtime/bundle targets, matching launcher/build paths
and listing text. The existing configuration check rejected old architecture and
Windows minimum settings separately, then passed. Fresh restore/x64 Release build
passed with zero warnings/errors; 94 core tests passed. The new target justified
this rerun. Windows App SDK/SQLite versions, core behavior and deferred Tune are unchanged.
The newer configuration has not had an installed/live pass; retain prior lifecycle
evidence and the final-candidate spot-check requirement.

Native Windows control is not exposed in this session; the owner must operate
Narrator/focus tests or resume with native control. Do not trigger secure-desktop
prompts while the owner is away. Remaining inputs: clean Windows 11 x64 and managed
test systems, final identity/account details, commercial/listing choices, privacy/
support contact and hosting destinations, and artwork/screenshot review. The
later delegation resolves privacy-design and candidate-build authorization. Windows 10/native
ARM64 runtime tests are deferred, not passed. x64 emulation on ARM64 is untested;
verify actual Store device availability before submission. Reuse completed failure/
timeout/closure/UAC results unless behavior changes or a new failure requires more.

## Retained 2026-09-14 evidence

The 2026-09-14 source/release review is complete without desktop control:
[release validation and remaining hands-on decisions](AppLens-Release-Validation.md).
Fresh core/Tune tests, x64/ARM64 builds, dependency and snapshot checks passed.
Store settings now bundle .NET; a regression checks the evaluated release
properties without generating a package. Store reviewer instructions are drafted.
Evidence: `artifacts/cop241-source-20260914-1e406cb1/` (local, not pushed).
The fresh backend smoke had 433 entries and complete coverage at ~0.52 seconds.
It writes a new scan and does not reload desktop action history; retain the
earlier seven-action UI export as that evidence.

At the time of the retained 2026-09-14 evidence, launch scope still included Windows
10/ARM64. The 2026-09-16 Windows 11 x64-first decision above supersedes that scope;
the older cross-build and installed-package records remain valid historical evidence.

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
   Include final-profile history recovery, cross-user protection and AppLens
   uninstall/reinstall retention, using test data only.
3. Validate the approved Windows 11 x64 configuration on the clean test system.
   Windows 10/native ARM64 are deferred. Measure launch/first-results/completion,
   download size, installed
   footprint and prerequisites for the actual distribution. Earlier 86.89 MiB
   compressed / ~217.49 MiB payload figures describe a development package only.
4. Finish owner inputs: Partner Center name/publisher/identity, artwork and private-
   data-free screenshots, contact and real privacy/support URLs. Drafts exist;
   `CSI.AppLensDesktop` / `CN=CSI` is still a placeholder. SQLite bundle is 2.1.13
   (observed native SQLite 3.53.3). Recheck current Microsoft policies when preparing
   release. Local candidate builds are already authorized. Hosting/submission/
   publication still need explicit approval; run WACK on the final candidate with
   the owner operating required native approvals and record its result.
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
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Script.Tests/Test-StoreConfiguration.ps1
```

To review that exact x64 Release build when desktop control resumes:

```powershell
& .\src\AppLens.Desktop\bin\x64\Release\net10.0-windows10.0.22000.0\win-x64\AppLens.Desktop.exe
```

`Run-AppLensDesktop.ps1` launches Debug, which can be stale unless rebuilt.
Use a unique test-results directory/logger filename; do not overwrite earlier TRXs.
Builds do not scan the PC; `tools/AppLens.Smoke` explicitly scans and writes evidence.
Development-package instructions: [RemovalProbe README](../tools/AppLens.RemovalProbe/README.md).
`Build-StoreCandidate.ps1` is authorized for local preparation by the owner's
2026-09-16 delegation; final identity and native test trust/approvals are still needed.

[Build and keyboard guidance](AppLensDesktop-Build.md) ·
[Uninstall routes](AppLens-Uninstall-Routes.md) ·
[Store checklist](Store-Readiness-Checklist.md) ·
[Product scope](AppLens-Product-Vision.md).
