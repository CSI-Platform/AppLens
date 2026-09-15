# AppLens release validation — COP-241

Reviewed 2026-09-14. This document separates completed source checks from the
remaining installed-app checks. The initial source review used no desktop control.
The owner subsequently authorized resuming AppLens computer-use tests, with
native approval prompts reserved for their return.

## Completed without desktop control

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
properties without building a package. It runs in CI and before the separately
authorized Store-candidate builder. The builder explicitly starts from x64;
the source still lists both architectures until launch scope is decided.

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
| Windows 10 and ARM64 | Conditional on launch support | Test on those systems if they remain supported at launch. A Windows 11 x64-only launch could defer them, but requires an explicit scope decision and matching package/listing changes. Source currently retains both. |
| Windows App Certification Kit | Keep for final candidate | Although command-line driven, WACK needs an active user session and administrator context and may launch the app. Schedule it with the hands-on work. No final-package WACK result exists yet. |
| Store screenshots | Owner review; combine with final client session | Capture only the needed listing views using non-private data. Existing internal screenshots should not be published automatically. |
| Store download/install | After authorized publication | Confirm the real customer delivery path. This cannot be honestly marked complete before the app is published. |

The lowest-effort plan is one coordinated final-package session on a clean test
environment, plus any additional architecture the owner elects to support.
Do not change launch support or waive a recorded gap without the owner's decision.

## Microsoft requirements reviewed

Official sources checked on 2026-09-14; certification remains Microsoft's decision.

| Source | AppLens implication |
| --- | --- |
| [Store policies](https://learn.microsoft.com/windows/apps/publish/store-policies), 10.1, 10.2.7–8, 10.4 | Describe capabilities and limitations accurately, use supported mechanisms with consent, remain responsive, and support the advertised devices. AppLens itself must be removable. |
| Same policies, 10.5.1 and 10.6 | A Win32/Desktop Bridge app needs a privacy policy even with local-only processing. Declare capabilities tied to real functionality and preserve Windows permission checks. |
| [Capability declarations](https://learn.microsoft.com/windows/apps/package-and-deploy/app-capability-declarations) | Medium-integrity packaged desktop apps declare runFullTrust. It does not turn the process into an administrator or waive removal restrictions. |
| [Self-contained deployment](https://learn.microsoft.com/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps) and [deployment overview](https://learn.microsoft.com/windows/apps/package-and-deploy/deploy-overview) | .NET and Windows App SDK deployment settings are separate. Bundling .NET avoids a manual .NET prerequisite; Store-managed Windows App SDK frameworks remain a dependency whose installation must be tested. Ship .NET servicing fixes through app updates. |
| [Windows App Certification Kit](https://learn.microsoft.com/windows/uwp/debug-test-perf/windows-app-certification-kit) | Command-line execution still needs an active user session and admin context. A development build or local test pass is not certification. |

## Prepared materials and remaining owner inputs

Listing, privacy and support drafts already exist. The listing now includes a
reviewer walkthrough and the supported removal mechanisms; the privacy draft
describes local inventory/history, redaction and voluntary sharing. Before release,
the owner must supply/approve:

- Reserved AppLens name and exact Partner Center package/publisher identity.
- Final launch platforms, pricing/category/markets and questionnaire answers.
- Publisher/contact details and the destination for public privacy/support pages.
- Artwork and screenshots, followed by distribution/submission authorization.

These are product/account decisions, not additional app feature development.
No hosting changes, distribution package, public report upload, final identity,
certification result or Store publication is implied by the GitHub source push.
