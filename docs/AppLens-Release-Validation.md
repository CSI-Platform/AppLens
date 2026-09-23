# AppLens release validation — COP-241

## Installed name and Store identity — 2026-09-23

The owner reserved Installed and approved applying the name and official identity.
Partner Center showed the MSIX/PWA product In draft; nothing was submitted.

- Store ID: 9PBPFLL6JVVV.
- Package name: CopperStateIntelligence.Installed.
- Publisher: CN=EF287C88-DDD2-4A49-8AC9-E4AE3A998DED.
- Publisher display name: Copper State Intelligence.
- Package family: CopperStateIntelligence.Installed_s3n4tkq635xf8.

Updated the window/header, removal messages, report titles/default filenames,
manifest and publication drafts. Repository/project/assembly names, storage paths,
DPAPI purpose and legacy event identifiers remain AppLens for compatibility.
No existing history or reports were rewritten. Tune remains deferred.

Build-StoreCandidate.ps1 passed: 104 core tests (92 backend, 12 presentation)
and Windows 11 x64 configuration check. Actual nested package manifest matches
all three Partner Center identity values, Installed display name, x64, minimum
Windows build 22000 and runFullTrust. WACK was found but not run.

Unsigned candidate: artifacts/store-candidate-20260923-141404-0177375/install/
AppLens.Desktop_0.1.0.0_x64.msixbundle; 48,568,595 bytes; SHA-256
07C8B0B93F2C7F35223C7C668CD3977B04C93AA3A27D4FD0580E323E3C6E2738.
The internal artifact filename does not determine the Store display name.
Test TRXs are in that candidate directory. Earlier lifecycle evidence is retained;
no install/uninstall test was repeated and no Store upload occurred.

Remaining: replace/review the old angular-A artwork; clean-PC installed checks,
shortcut/reopen/update, accessibility, protected-history retention/cross-user,
managed-policy denial and final WACK; pricing/markets/IARC, approved screenshots,
and published privacy/support destinations. The new Store identity is not an
in-place update of the earlier placeholder/test packages; test history/launch
behavior explicitly. Submission, publication, hosting and merge remain held.


Updated 2026-09-18. This document separates completed source checks from the
remaining installed-app checks. The initial source review used no desktop control.
The owner subsequently authorized resuming AppLens computer-use tests, with
native approval prompts reserved for their return.

## Distribution decision and added acceptance detail — 2026-09-18

The owner confirmed continuing with Microsoft Store distribution, with the website
as an installation entry point. The final customer experience must include simple
installation, a desktop shortcut and double-click launch. These are requirements,
not new test results. Add shortcut/reopening and controlled same-identity upgrade
checks to the final-candidate session, including preservation of history and launch
access. Verify actual website/Store delivery after authorized publication and
Store updates when an approved update is available. The
[submission plan](AppLens-Store-Submission-Plan.md) covers the conditional website
installer and undecided pricing; no separate direct-download channel is planned.

This was a documentation-only pass; retained 2026-09-16 checks below were not rerun.
No release approval, installed result or certification result changed.

## Latest privacy and packaging verification — 2026-09-16

The owner delegated privacy and local authorization/preparation decisions after
the Windows 11 x64 change was pushed as `425c934`. Implemented Windows current-user
DPAPI for new removal-history records, no plaintext index, read-only compatibility
with preserved legacy logs, and explicit unavailable-history UI/report coverage.
Accounts, telemetry, uploads and removal-approval behavior remain unchanged.
See the [privacy/storage decision](AppLens-Store-Submission-Plan.md#privacy-and-authorization-decision--implemented-2026-09-16).

| Check | Observed result |
| --- | --- |
| Privacy regression | Failed against the previous plaintext store, then passed against protected storage. New tests cover round-trip/no plaintext copies, legacy log/index preservation, latest completed outcome/querying, unknown/damaged/tampered data, missing terminal newline, approval-persistence failure preventing execution and reopened action/report consistency. |
| Full core suite | 104 passed: 92 backend, 12 presentation. Ten new privacy cases plus the retained 94 tests. |
| Preserved Tune suite | 58 passed after the shared query helper extraction; Tune remains deferred and outside the v1 package. |
| Separate-process recovery | A synthetic record written by one process reopened under the same Windows user in another process. No plaintext fixture string or secondary file was present. This is not a cross-user or packaged-profile test. |
| Existing history | Read-only default `RemovalService` recovered eleven actions. Hashes of every existing runtime file before/after matched; no protected file was created by reading. |
| Dependencies | After adding ProtectedData 10.0.12, NuGet reports no known vulnerabilities for the four core projects from the configured source. |
| Build/tooling | Store configuration check and candidate script passed. Fixed discovery of installed `mspdbcmf.exe` and WACK; symbol conversion now runs without the prior warning. WACK was found but not run. Test results are retained per candidate. |
| Final compilation | Publish after the reviewed history-warning UI adjustment passed without warnings/errors. The UI warning is source/build verified; native rendering/speech remains part of the final installed check. |
| Actual bundle inspection | One x64 application package, minimum Windows 10.0.22000.0, .NET runtime and ProtectedData assembly included, compiled resources present, only runFullTrust capability. Windows App Runtime 1.8 remains a framework dependency (minimum 8000.836.2153.0). No deferred Tune, probe, ledger, database evidence or raw report paths. |

The final local unsigned candidate uses placeholder identity `CSI.AppLensDesktop`
/ `CN=CSI`, version `0.1.0.0`. It is **not ready for Store upload**:

- Directory: `artifacts/store-candidate-20260916-011555-3476646/install/`.
- Bundle: `AppLens.Desktop_0.1.0.0_x64.msixbundle`, 48,761,419 bytes.
- SHA-256: `B65A7A56C6B03DC2552B71BBBFB10944D4B7E1E3351E12D285BC16D3C75591B6`.
- Inner package: 257 entries, 124,283,361 uncompressed entry bytes. This is archive
  content size, not measured installed footprint or Store download size; shared
  framework prerequisites are separate.

Evidence: `artifacts/cop241-privacy-20260916-010513/` contains pre/post privacy logs,
Tune TRX, separate-process and legacy checks, dependency audit, packaging logs,
`candidate-inspection.json` and `package-entries.json`. All 104 core-test TRXs are
under `artifacts/store-candidate-20260916-011102-4248971/test-results/`. Failed and
superseded attempts are retained. The final UI adjustment only changes warning
propagation when history loading finishes after a scan; the final publish compiled
it without rerunning unrelated lifecycle tests.

Existing plaintext development history was not retroactively encrypted; no raw
evidence or report was rewritten. The previously successful installed/uninstall
lifecycle evidence below is reused. No native prompt, live removal, installation,
certificate trust, policy change, WACK, hosting or Store action was performed.
Final installed-profile recovery/cross-user denial and retention remain open,
alongside clean-PC/Narrator/focus/managed-policy and Partner Center gates.

## Earlier unattended scope preparation — 2026-09-16

- Verified the existing worktree is `Projects\.worktrees\AppLens\codex-applens-v1-quick-client`
  (the continuation prompt omitted the separator before `.worktrees`). It started
  clean on the requested branch; local and GitHub both pointed to
  `9be420b50b3ce42d3196eada91834b24773427bd` in `CSI-Platform/AppLens`.
- Linear COP-241 is In Progress; COP-240 is Done. GitHub reported no PR and no
  workflow runs for this branch. The workflow currently triggers on main pushes
  or PRs to main, so the retained tests are local evidence, not a CI pass.
- Source/tools/tests have not changed since `5230228`; `9be420b` changed three
  evidence documents only. Reused the installed/uninstall lifecycle results.
  Both the original installed JSON export and its local evidence copy still match
  the recorded SHA-256. Retained TRXs contain 82 backend + 12 presentation passes,
  zero failures. Initially reused without rerunning; the later approved target
  change below justified a new build/test pass.
- Refreshed the time-sensitive NuGet advisory check with
  `dotnet list AppLensDesktop.sln package --vulnerable --include-transitive --no-restore`:
  no vulnerable packages reported for the four solution projects by the configured
  NuGet source. No dependency version change or package generation was performed.
- Verified dimensions of the five original AppLens PNG assets and that WACK
  10.0.26100.7705 is installed. Neither result establishes final package acceptance.
- Prepared the [submission packet](AppLens-Store-Submission-Plan.md), reconciled
  stale paused-control/picker/test checklist items, and added factual storage
  disclosure to the privacy draft. Tune remains deferred.

### Owner-approved Windows 11 x64 launch scope

The owner selected **Windows 11 x64 first** on 2026-09-16 and then requested all
remaining unattended work while they sleep. Updated the desktop target framework
and minimum OS to build 22000, the manifest minimum to match, and project/runtime/
bundle architectures to x64 only. Updated the launcher path and current build/
listing documentation. Existing Windows App SDK and build-tool package versions
are unchanged; shared backend/test API targets remain at 19041.

The Store configuration check first failed for the old multi-architecture bundle,
then separately for the old Windows minimum. It now passes for the evaluated
Windows 11 x64 configuration, including bundled .NET and Store-managed Windows App
SDK frameworks. Restore and x64 Release build passed with zero warnings/errors;
all 94 core tests passed again (82 backend, 12 presentation). This rerun was needed
because the desktop target changed. See `windows11-*-before.log`,
`windows11-scope-after.log`, `windows11-restore.log`, `windows11-build.log` and
`windows11-tests/` under the evidence directory below.
The post-restore advisory query again reports no known vulnerable packages.
Evaluated package content/project references contain no deferred Tune, removal
probe or raw report paths. The launcher parses successfully; it was not launched.

Windows 10 and native ARM64 runtime acceptance are now deferred by the owner,
not passed. x64 emulation on ARM64 is not validated; check final Store device
availability before submission. The new target has not been installed or tested
interactively. Retain the prior lifecycle evidence and perform the planned final-
candidate spot check. No native prompt, installed-client replacement, distribution
package, hosting action or Store action was started while the owner was away.

Evidence: `artifacts/cop241-submission-prep-20260916-003337/`, including
`evidence-reuse.json`, `artwork-dimensions.json` and `dependency-audit.log`.
Existing reports, raw evidence, installed apps and system settings were untouched.
The current tool session does not expose native desktop control. Narrator/focus
tests need owner operation or a later native-control session; native approvals
remain the owner's responsibility.

## Retained source verification — 2026-09-14

| Evidence | Result |
| --- | --- |
| Release builds | x64 and ARM64 compile with zero warnings/errors. Compilation alone does not validate ARM64 hardware. |
| Core tests | 94 pass: 82 backend, 12 presentation. Covers partial/cancelled scans, filters/identity, protected routes, literal uninstaller arguments, approval recording, changed-target rejection, independent removal verification, failure outcomes and history recovery. |
| Preserved Tune | 58 tests pass. All 24 preservation-manifest destinations exist; 18 hashes are unchanged and six have the documented separation adaptations. |
| Windows snapshot | All five script check groups pass, including unknown readings and refusal to overwrite evidence. |
| Dependencies | NuGet reports no known vulnerabilities for the four core solution projects at review time. |
| Read-only backend smoke | 433 entries, 198 reported sizes, one disk, complete coverage; first results 0.523 s and completion 0.524 s on the developer PC. JSON/Markdown/HTML written locally. This is not an offline or clean-PC result and does not reload desktop action history. |
| Package input review | Evaluated desktop content/project references exclude deferred Tune, removal probes and local reports. Only runFullTrust is declared. |
| Runtime configuration regression | Initially failed because the Store settings required a separate .NET installation. Packaged builds now bundle .NET while using Store-managed Windows App SDK framework dependencies. The evaluated x64/ARM64 checks pass; actual installed delivery remains unverified. |
| Git destination | Main product repository `https://github.com/CSI-Platform/AppLens.git`; existing branch `codex/applens-v1-quick-client`. No AppLens-SSH/AppLens-LLM repository is involved. |

Local evidence is under `artifacts/cop241-source-20260914-1e406cb1/`.
Logs, scan reports, screenshots, runtime payloads and other machine evidence stay
out of Git. See [client verification](AppLens-Client-Verification.md) for the
earlier live UI evidence and the [handoff](AppLens-v1-Handoff.md) for continuation.

The new `tests/Script.Tests/Test-StoreConfiguration.ps1` evaluates actual MSBuild
properties without building a package. It is configured to run in CI and before
the separately authorized Store-candidate builder. The builder starts from x64;
the 2026-09-16 scope change above now restricts the project/bundle to x64.

## Remaining hands-on checks for owner review

### Live lifecycle checks completed on 2026-09-14

The current x64 Release client was exercised on the developer PC using four
owned, per-user registry fixtures and a bounded executable that only logged its
start/exit and returned code 13. No working application was removed.

| Check | Observed result |
| --- | --- |
| Declared protection | NoRemove=1 appeared as Removal restricted with a disabled action. This is not a managed-PC policy-denial test. |
| Real failed process | The fixture exited with code 13; AppLens showed Failed, retained the entry and recorded StillInstalled=true. |
| Real slow process | After the production three-minute timeout, AppLens showed Still running, retained the entry and blocked a retry. The helper exited naturally after 200 seconds; its log confirms only one launch. Scan/export stayed disabled during execution, including Alt+S. |
| Closure during execution | AppLens was closed while the 90-second helper remained running. Reopening restored Outcome unknown with rescan/review guidance; it did not claim removal. The helper subsequently exited naturally. |
| Download after reopening | The client saved JSON to Downloads with all 437 entries (433 baseline plus four fixtures) and all ten actions. Failed, Still running and Outcome unknown matched the UI and ledger; personal identifiers were redacted. |
| Cleanup | All four exact registry keys were ownership-checked and removed; no helper remained running. Text size 100%, Custom theme and high contrast off were unchanged. Evidence and ledger history were preserved. |

Local evidence: `artifacts/cop241-live-63a8a7fdb352457da74b8b70569b5fb3/`.
See `live-verification.json`, process logs, `reopened-history.png`,
`fixture-ledger-final.json` and `reopened-client-export.json`.
The original report is `C:\Users\codyl\Downloads\AppLens-20260914-190036-670.json`,
SHA-256 `8AE94CA15B171A6FAD909FA19CCC5CCE00B3E6980242EB4D469353D1B83D1EDC`.

A fresh disposable development client and package fixture were built and hash-
checked under `artifacts/removal-preflight-d51377dddcd24574bab23311c5b823dd/`.
The compiled-resource identity check passed. The owner approved the native
Windows prompt and both packages installed. The client launched from WindowsApps,
scanned 435 entries in 0.60 s, filtered to its two packages, disabled its own
uninstall action, and removed the exact different-publisher disposable package.
Independent package enumeration confirmed absence; the table refreshed to 434 entries.

The installed picker opened Downloads; Escape retained results, and saving JSON
retained all 434 entries and eleven actions despite the table showing one row.
The new Removed record matched independent absence and the history; personal
identifiers were redacted. See `installed-verification.json`, `installed-*.png`
and `installed-client-export.json`. Original Downloads report:
`AppLens-20260914-190622-706.json`, SHA-256
`097C83A99C831671BA8209A24167436334A4CACE0C8CFB1232080740638FDE1D`.

After the usage interruption, a fresh check confirmed that hash, all eleven
ledger correlations, fixture absence and unchanged display settings. No client
or fixture process was running. The development client remains installed for
later review; the disposable package is removed. See `post-reset-verification.json`.
This is a development identity with bundled runtimes, not final Store framework
delivery, clean-PC or certification evidence. Spoken screen-reader behavior was
not tested. The automation tool returned no accessibility tree for this installed
identity; this alone does not establish whether Narrator can read it. A packaged
keyboard/focus check should include reopening the picker after cancellation:
one automated Alt+D attempt did not open it, while clicking Download worked.

### Reduced remaining checks

These are recommendations for launch confidence, not claims that Microsoft
mandates this exact internal test list. Do not silently mark a skipped test passed.

| Check | Recommendation | Smallest useful pass / what can be reduced |
| --- | --- | --- |
| Clean-PC installed customer flow | Keep before launch | One clean standard-user Windows PC or VM: install, launch, scan, filter, remove an owned fixture, verify, save/read a report, close/reopen. Confirm no manual .NET setup. Include AppLens's own Windows uninstall and reinstall behavior. |
| Offline operation and final picker | Combine with the clean-PC pass | Disconnect networking in the test environment after installation; scan/export. Check Downloads and cancel/save in the final package. No separate repeated desktop session needed. |
| Managed-PC policy denial | Keep in an appropriate test environment | Failed/slow processes, NoRemove protection and close/reopen now have live evidence above. A genuine Windows policy denial still needs a managed test environment; do not change the working PC's security policy just to manufacture it. |
| Administrator prompts | Reuse completed COP-240 evidence unless package behavior changes | Native UAC denial and approval already passed on the developer PC. A fresh standard-user credential handoff can be folded into the clean-PC test. The user must handle any prompt. |
| Actual screen reader | Keep a short pass | Scan/status announcements, one row and its action, confirmation/cancel and focus recovery. UI Automation names were checked; spoken behavior was not. |
| Full keyboard/text/contrast matrix again | Defer the full repeat | The 100%/225%, Night sky/Desert, resize and keyboard checks passed in COP-239. Recheck only affected behavior or one final-package spot check if unchanged. |
| Windows 10 and native ARM64 | Deferred by owner on 2026-09-16 | Windows 11 x64-first scope is applied to project, manifest and listing. Do not claim these deferred runtimes passed. Confirm Store availability because Windows 11 ARM64 can emulate x64. |
| Windows App Certification Kit | Keep for final candidate | Although command-line driven, WACK needs an active user session and administrator context and may launch the app. Schedule it with the hands-on work. No final-package WACK result exists yet. |
| Store screenshots | Owner review; combine with final client session | Capture only the needed listing views using non-private data. Existing internal screenshots should not be published automatically. |
| Store download/install | After authorized publication | Confirm the real customer delivery path. This cannot be honestly marked complete before the app is published. |

The remaining plan is one coordinated final-package session on a clean Windows 11
x64 test environment, plus the managed-policy denial environment. Do not widen
launch support or waive another recorded gap without the owner's decision.

## Microsoft requirements reviewed

Policy/submission guidance refreshed on 2026-09-16; runtime references below retain
the 2026-09-14 review. Certification remains Microsoft's decision.
The [7.19 archive](https://learn.microsoft.com/windows/apps/publish/store-policy-archive/store-policy-7-19)
is effective from 2025-10-14. The main policy page now shows 7.20, published
2026-09-15 and effective 2026-10-22. Both were reviewed; recheck the applicable
version at submission. See the [change history](https://learn.microsoft.com/windows/apps/publish/store-policies-change-history).

| Source | AppLens implication |
| --- | --- |
| [Store policies](https://learn.microsoft.com/windows/apps/publish/store-policies), 10.1, 10.2.7–8, 10.4 | Describe capabilities and limitations accurately, use supported mechanisms with consent, remain responsive, and support the advertised devices. AppLens itself must be removable. |
| Same policies, 10.5.1 and 10.6 | A Win32/Desktop Bridge app needs a privacy policy even with local-only processing. Declare capabilities tied to real functionality and preserve Windows permission checks. |
| Same policies, 10.5.4 and 10.14 | Resolve protection of personal information in local storage; do not infer compliance from default export redaction. Confirm the appropriate publisher account and business/support details. |
| [Capability declarations](https://learn.microsoft.com/windows/apps/package-and-deploy/app-capability-declarations) | Medium-integrity packaged desktop apps declare runFullTrust. It does not turn the process into an administrator or waive removal restrictions. |
| [Self-contained deployment](https://learn.microsoft.com/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps) and [deployment overview](https://learn.microsoft.com/windows/apps/package-and-deploy/deploy-overview) | .NET and Windows App SDK deployment settings are separate. Bundling .NET avoids a manual .NET prerequisite; Store-managed Windows App SDK frameworks remain a dependency whose installation must be tested. Ship .NET servicing fixes through app updates. |
| [Windows App Certification Kit](https://learn.microsoft.com/windows/uwp/debug-test-perf/windows-app-certification-kit) | Command-line execution still needs an active user session and admin context. A development build or local test pass is not certification. |

The initial review found plaintext JSONL/SQLite action history. The owner's later
privacy delegation authorized the current-user protection implemented and tested
above. New history is encrypted without a plaintext index; legacy development
evidence is preserved and explicitly disclosed. Reports remain ordinary
user-selected files. Complete final-profile and retention checks and assess the
accurate final disclosures against policy 10.5.4 before submission. Local tests
and this policy review do not establish legal compliance or certification.

## Prepared materials and remaining owner inputs

Listing, privacy and support drafts already exist. The listing now includes a
reviewer walkthrough and the supported removal mechanisms; the privacy draft
describes local inventory/history, redaction and voluntary sharing. Before release,
the owner must supply/approve:

- Reserved AppLens name and exact Partner Center package/publisher identity.
- Pricing/category/markets and questionnaire answers. Launch scope is decided:
  Windows 11 x64 first.
- Publisher/contact details and the destination for public privacy/support pages.
- Artwork and screenshots, followed by hosting/submission/publication authorization.

The [submission packet](AppLens-Store-Submission-Plan.md) names the exact identity
fields, proposed listing choices, test environments, native approvals and manual
publication-hold setting. It is ready for owner review, not for Store upload.

The privacy design and local candidate preparation are delegated and completed.
Remaining account/contact facts cannot be invented; remaining native tests need
the environments and approvals listed in the packet. No hosting change, public
report upload, final identity, certification result or Store publication is
implied by the source push or the unsigned local candidate.
