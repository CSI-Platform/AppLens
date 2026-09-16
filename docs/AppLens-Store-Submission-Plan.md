# AppLens Store submission preparation

Prepared 2026-09-16 for COP-241. This is a local review packet, not a submitted
Store draft. The [release validation](AppLens-Release-Validation.md) retains the
completed tests. The owner approved Windows 11 x64 first during this session.
Final identity, distribution packaging and publication still need approval.

## Decisions and materials needed from the owner

| Input | Prepared position / exact value needed |
| --- | --- |
| Launch platforms — decided | Windows 11 x64 first, approved 2026-09-16. Desktop project, manifest, bundle settings, configuration check and listing now require build 22000+ and x64. Windows 10/native ARM64 runtime checks are deferred; their old cross-builds remain historical evidence. |
| Partner Center identity | From AppLens → Product management → Product identity: Package/Identity/Name, Package/Identity/Publisher, Package/Properties/PublisherDisplayName, plus reserved display name and Store ID. These are identifiers, not account credentials. Current CSI.AppLensDesktop / CN=CSI / CSI are placeholders. |
| Publisher account | Confirm the publishing entity and account verification. For the CSI business release, review the company-account requirement in policy 10.14; do not invent legal details or assume the account is verified. |
| Version and commercial settings | Approve initial version (source is 0.1.0.0), free/paid price, markets and release timing. Utilities & tools is the proposed category; en-US is the current package language. Complete the actual IARC questionnaire; do not invent an age rating. |
| Privacy and support | Supply the public publisher/contact details, effective date, and approved destinations for the existing [privacy](AppLens-Privacy-Draft.md) and [support](AppLens-Support-Draft.md) drafts. Resolve the storage-protection review below before approving privacy claims. |
| Artwork and screenshots | Review the original [AppLens artwork](../src/AppLens.Desktop/Assets/AppLens-Artwork.md), and capture the views below on a test machine with no private data. No existing internal screenshot is approved for publication. |
| Distribution preparation | Explicitly authorize building the final candidate after the preceding inputs are resolved. Separately approve any test signing/trust setup needed on the chosen test PC. The development probe installer is not a final-package installer. |
| External actions | Hosting, Partner Center changes/uploads, certification submission and publication each remain outside this preparation. If later authorized to submit but not publish, use the manual publishing hold described below. |

Microsoft documents the three manifest identity values in
[Product identity](https://learn.microsoft.com/windows/apps/publish/view-app-identity-details).
MSIX Store submissions do not require buying a CA signing certificate; Microsoft
re-signs them for publication. Local test installation has separate signing/trust
requirements. See [package requirements](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/app-package-requirements).

## Submission fields already drafted

Use [Store-Listing-Draft.md](Store-Listing-Draft.md) for the description, features,
limitations, runFullTrust explanation and reviewer steps. No account or server is
needed to use AppLens. Reports may be shared by the user; the app does not perform
AI analysis, transmit a report or generate AI content.

Proposed search phrases: app inventory; installed apps; disk usage; uninstall;
local report. Review them with the final listing. Do not advertise universal scan
timings, recovered-space guarantees, optimization, or untested platform support.

Partner Center normally publishes after certification. To preserve a separate
publication decision, select **Don't publish this submission until I select
Publish now** in a later authorized submission. Verify the saved setting before
submitting. No Partner Center setting has been changed by this preparation.
See [submission options](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/manage-submission-options).

## Artwork and screenshot capture sheet

The five current AppLens PNGs have verified dimensions: Store 50×50, small icon
88×88, tile 300×300, wide tile 620×300 and splash 1240×600. The existing
`AppLensSquare150.scale-200.png` is a candidate for the recommended 300×300 listing
icon, subject to owner review. Package asset dimensions are not WACK approval.

Capture real app views with an owned disposable fixture and a non-private test
account. Use PNG, at least 1366×768, under 50 MB each. One Desktop screenshot is
the submission minimum; the proposed four below explain this app's flow. Captions
are under 200 characters. See [Microsoft's image guidance](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/screenshots-and-images).

| View | Proposed caption |
| --- | --- |
| Completed scan and main table | Review installed apps and local disk usage in one table. |
| Largest-first table with a filter | Find apps by name or publisher and compare reported sizes. |
| Confirmation for the owned fixture | Review the exact installation before approving a supported uninstall. |
| Verified fixture result and report controls | Check the recorded result and save a complete local report. |

Before accepting each capture, inspect device/user labels, paths, app names,
history, account details and notifications. Do not fabricate app results or reuse
developer captures merely because report redaction passed. Keep rejected/internal
captures local; record the approved asset filenames and hashes separately.

## One coordinated final-candidate test session

Supply a clean PC or persistent VM with a standard-user account, no AppLens data,
and no development SDK or manually installed .NET runtime. Record pre-existing
Windows App SDK/VC runtime frameworks and Windows build/architecture. Have an
administrator available for native prompts. Use a separate managed test system
for genuine policy denial; do not modify this workstation's security policies.
Windows 10/native ARM64 test systems are no longer first-release prerequisites.
An x64 package can run under emulation on Windows 11 ARM64; this does not establish
AppLens compatibility. Review Partner Center's actual device availability before
submission, and do not make an ARM support claim. See Microsoft's
[Windows on Arm guidance](https://learn.microsoft.com/windows/arm/faq).

Native desktop control is not exposed by this Codex session's computer-use tool.
The owner must operate the Windows/Narrator steps, or resume them in a session
with native control. Secure-desktop approvals always require the owner.

1. **Freeze the candidate.** After final identity approval and distribution-build
   authorization, use `tools/Build-StoreCandidate.ps1`. Record Git commit, package
   version, architectures, SHA-256, dependency manifests and build logs in a new
   `artifacts/` directory. Inspect actual bundle contents; project properties alone
   do not prove that only x64 was packaged. Never upload a development
   ClientTest identity or raw machine reports.
2. **Install and launch normally.** Use the approved test installation method;
   record any native approval and every prerequisite obtained. Confirm launch
   without installing .NET manually. Record installation method honestly: sideload
   delivery does not establish Store dependency acquisition. Keep the developer
   PC's working client and historical evidence untouched.
3. **Complete the customer flow.** Scan → search/filter → cancel a fixture removal
   → approve the exact owned fixture → independently check its absence → inspect
   refreshed table/history → save and read a redacted JSON report with matching
   full inventory/actions → close/reopen and check history. Download Markdown and
   HTML once for final-package readability. Use unique filenames throughout.
4. **Go offline in the test environment.** Disconnect its network after installation
   and repeat scan/export. Record coverage, errors and results. Restore its network
   afterward. Do not disconnect the user's working workstation remotely.
5. **Check Narrator and focus.** Hear scan/completion status, a row and its action,
   confirmation/cancel, and report controls. Escape from the picker, verify visible
   focus, then reopen with keyboard navigation and Alt+D. The earlier automated
   Alt+D failure remains unresolved until observed here; UI names alone are not a
   spoken-output result. Reuse COP-239's full text/contrast matrix unless changed.
6. **Check remaining permission behavior.** Reuse COP-240 UAC denial/approval
   evidence; add a standard-user administrator credential handoff in this clean
   session. On the managed system, identify the actual enforcing policy, attempt
   only an owned fixture, and record Windows denial, retained installation and
   honest app/report outcome. NoRemove and simulated failure are not this test.
7. **Check AppLens removal and retention.** Uninstall AppLens through Windows,
   verify package absence, inspect which local history remains, confirm exported
   reports remain, then reinstall and record history behavior. Use only test data.
   Resolve the support/privacy retention wording from these observations.
8. **Run WACK on the exact candidate.** The local kit exists at
   `C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe`
   (observed version 10.0.26100.7705). Use the appropriate installed kit on the test
   system. Run from an approved elevated active user session; preserve any previous
   WACK reports before resetting kit state. Save fresh XML/HTML and inspect every
   failure/skipped test. Do not equate an exit code or a local pass with certification.

After the test package is installed, the documented WACK command sequence is:

```powershell
# Prepared instructions only; not executed in this review.
# Replace placeholders with the exact tested identity and a new report path.
& 'C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe' reset
& 'C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe' test -packagefullname '<exact package full name>' -reportoutputpath '<new absolute report.xml path>'
```

Microsoft's [WACK command-line instructions](https://learn.microsoft.com/windows/uwp/debug-test-perf/windows-app-certification-kit)
require administrator rights and an active user session. WACK may launch the app.
The owner approves native installation, administrator credential/UAC and WACK
elevation prompts; the agent must not approve them or retry a cancelled prompt.

For each platform, record launch-to-usable time, first-results/completion time,
inventory count/coverage, package bytes, separately downloaded prerequisites,
installed app payload and local data bytes. Label logical versus allocated disk
size and whether framework sizes are shared. Measure actual Store download and
installation only after authorized publication. Keep the developer package's
86.89 MiB figure labeled as historical development evidence.

## Privacy/storage review before submission

Current source writes action records to a JSONL log and SQLite index through
`RuntimeStorage.cs`, `RemovalService.cs` and `BlackboardStore.cs`. There is no
AppLens encryption layer on that history or exported files. Default export
redaction does not redact the underlying history. Windows permissions/device
protection have not been validated on the final clean test environment.

Policy 10.5.4 addresses secure storage of personal information using modern
cryptography. Treat alignment of this storage design and actual device protections
with that requirement as unresolved; this review does not establish compliance
or predict rejection. Before submission, resolve the protection approach and
accurate privacy disclosure. Any change to privacy posture needs owner approval
under AGENTS.md; do not silently encrypt/migrate or overwrite existing evidence.

Proposed follow-up for approval: protect app-managed history using Windows
[current-user data protection](https://learn.microsoft.com/dotnet/api/system.security.cryptography.dataprotectionscope?view=windowsdesktop-10.0),
and ensure the SQLite index, summaries and backups do not retain duplicate private
plaintext. Keep explicitly exported reports readable, with the existing redaction
and disclosure. Define migration/recovery and preservation of existing evidence
before writing files; test same-user recovery, damaged records and cross-user
access. This is a proposed protection approach, not an implemented feature or a
promise of Store acceptance; it does not protect against software running as the
same Windows user.

## Review date and release gate

Policy 7.19 became effective 2025-10-14. Version 7.20 was published 2026-09-15
but becomes effective 2026-10-22. This review checked both; use the version in
effect on submission day and recheck changes. The upcoming revision also makes
ongoing age-rating updates explicit. Sources: [7.19](https://learn.microsoft.com/windows/apps/publish/store-policy-archive/store-policy-7-19),
[7.20](https://learn.microsoft.com/windows/apps/publish/store-policies), and
[change history](https://learn.microsoft.com/windows/apps/publish/store-policies-change-history).

COP-241 stays In Progress until the unresolved acceptance work and release
materials are resolved. This packet is not permission to build for distribution,
modify hosting, submit, publish or merge. Store customer-delivery verification is
a later gate, after explicit publication approval.
