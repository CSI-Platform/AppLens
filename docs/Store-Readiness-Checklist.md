# Microsoft Store readiness

The active v1 flow is inventory -> storage/app table -> approved removal ->
verification -> local report. AppLens-Tune is preserved outside the core build.
The implementation plan is [Product Vision](AppLens-Product-Vision.md); changing
work status is tracked in Linear, not duplicated here.
For completed source checks and a condensed keep/defer list of hands-on work,
see [release validation](AppLens-Release-Validation.md).
The [submission preparation packet](AppLens-Store-Submission-Plan.md) lists exact
owner inputs, screenshot requirements and the coordinated final-candidate test.

## Confirmed delivery direction

Owner decision, 2026-09-18: continue with Microsoft Store distribution and a website
installation entry point. Customers need a normal installed app they can open by
double-clicking a desktop icon. A separate direct-download installer is outside
this release plan. See the submission packet for the conditional Store Web
Installer option; choosing the Store does not decide free/paid pricing or authorize
submission/publication.

## Package and runtime
- Native C#/.NET 10, WinUI 3 / Windows App SDK.
- Medium-integrity desktop app; only runFullTrust is declared.
- Owner-approved first release: Windows 11 x64, minimum build 22000. Project,
  manifest and bundle settings match. Windows 10 and native ARM64 are deferred.
- Source identity now matches Installed, Store ID 9PBPFLL6JVVV; see the
  submission packet for exact Partner Center values and candidate evidence.
- Store configuration bundles .NET and uses Store-managed Windows App SDK
  frameworks; actual clean-PC prerequisite delivery is still unverified.
- Display name Installed; existing angular-A artwork needs replacement/review.
- Developer executable builds and unsigned development test packages are not a
  Store installation, distribution approval or certification result.
- Owner delegated privacy and local packaging decisions on 2026-09-16. A new
  unsigned x64 candidate is built and inspected with the official Installed identity;
  exact artifact/hash evidence is in [release validation](AppLens-Release-Validation.md).
- New v1 action history uses Windows current-user data protection, with no
  plaintext index. Legacy evidence stays unchanged and reports stay readable.

## Release acceptance
Local evidence already covers keyboard navigation, row/action accessible names,
100%/225% text, normal and two high-contrast themes, resizing and save/cancel:
[COP-239 client verification](AppLens-Client-Verification.md). COP-240 also covers
packaged Store/MSI/vendor fixtures and native UAC denial/approval. These results
inform the final installed review; they do not check off clean-PC certification.
The 2026-09-14 development-package pass also covers Downloads picker cancel/save,
failed/slow processes, NoRemove protection, and closure/reopening during an action.
Actual Narrator speech, keyboard focus after picker cancellation, genuine managed
policy denial and final-candidate/clean-PC behavior remain open. See the
[handoff](AppLens-v1-Handoff.md). The current session has no native desktop control;
this is a tooling limit, not a renewed owner pause.

- [ ] Complete the installed customer flow on a clean standard-user Windows PC,
      including native/package dependencies and offline scan/export.
- [ ] Complete and verify desktop-shortcut creation and double-click launch,
      reopening without developer tools, and a controlled same-identity update
      preserving launch access and readable history.
- [x] Retain developer-PC evidence for all-user removal, denied/approved UAC,
      cancellation, NoRemove protection, failed/slow uninstallers and app closure.
- [ ] Verify genuine managed-policy denial and the clean-PC administrator handoff.
- [x] Retain COP-239 keyboard, accessible-name, text scaling, high-contrast,
      small-window and large-inventory evidence.
- [ ] Verify actual Narrator speech and installed picker focus recovery; perform
      the final-package spot check without repeating unchanged full matrices.
- [ ] Verify the approved Windows 11 x64 target on the clean test system. Windows
      10/native ARM64 tests are deferred by the 2026-09-16 scope decision, not passed.
      Review actual Store device availability; x64 can be emulated on ARM64.
- [ ] Measure launch, first results and completion, package download, installed
      footprint, and first-install prerequisites for the actual distribution.
- [x] Refresh Microsoft policy/capability/submission guidance on 2026-09-16;
      distinguish effective 7.19 from 7.20 effective 2026-10-22.
- [x] Implement the delegated local-history protection decision, including
      same-user recovery, corrupted-history refusal and legacy preservation tests.
- [ ] Complete installed-profile/cross-user protection and uninstall/reinstall
      retention checks; verify final privacy/contact disclosures against policy
      10.5.4. Local encryption tests do not establish certification or compliance.
- [ ] Run Windows App Certification Kit against the final package.
- [ ] Verify Store download/install only after authorized publication.
- [ ] Verify the website's Store installation entry point after authorized
      publication; verify actual Store update delivery when an approved update
      is available. Local package upgrades do not establish Store delivery.

Use [Uninstall routes](AppLens-Uninstall-Routes.md) for the chosen APIs, evidence
and limitations. Application identifiers, unavailable values, action outcomes and
scope evidence must remain consistent in the table and all report formats.
No automatic restart, forced deletion or bypass of administrator/policy controls.

## Materials to finalize
- [ ] Reserve the final AppLens name and use Partner Center's package/publisher identity.
- [ ] Owner review of original AppLens artwork and final screenshots without private data.
- [ ] Publish and verify the [privacy draft](AppLens-Privacy-Draft.md) at a real URL.
- [ ] Publish and verify the [support draft](AppLens-Support-Draft.md) with a real contact.
- [ ] Complete category, age-rating, market and certification fields using the
      [listing draft](Store-Listing-Draft.md).
- [ ] Produce the final upload package using the authorized signing/submission process.
- [ ] Prepare the website installation button using the final Store ID. Prefer
      the official Direct-mode badge when eligible; otherwise link the Store
      listing. Website implementation/hosting approval remains separate.
- [ ] Verify manual publishing hold before any later authorized certification
      submission when public release has not separately been approved.

Local candidate preparation is authorized by the 2026-09-16 delegation. Hosting
changes, Store submission and publication still require explicit authorization.
No final identity or approval of native Windows prompts is inferred from this
checklist.
