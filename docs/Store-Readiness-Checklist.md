# Microsoft Store readiness

The active v1 flow is inventory -> storage/app table -> approved removal ->
verification -> local report. AppLens-Tune is preserved outside the core build.
The implementation plan is [Product Vision](AppLens-Product-Vision.md); changing
work status is tracked in Linear, not duplicated here.
For completed source checks and a condensed keep/defer list of hands-on work,
see [release validation](AppLens-Release-Validation.md).
The [submission preparation packet](AppLens-Store-Submission-Plan.md) lists exact
owner inputs, screenshot requirements and the coordinated final-candidate test.

## Package and runtime
- Native C#/.NET 10, WinUI 3 / Windows App SDK.
- Medium-integrity desktop app; only runFullTrust is declared.
- Owner-approved first release: Windows 11 x64, minimum build 22000. Project,
  manifest and bundle settings match. Windows 10 and native ARM64 are deferred.
- Source identity CSI.AppLensDesktop / CN=CSI remains a placeholder until
  Partner Center supplies the final identity.
- Store configuration bundles .NET and uses Store-managed Windows App SDK
  frameworks; actual clean-PC prerequisite delivery is still unverified.
- Display name AppLens, original silver geometric artwork and local-first copy.
- Developer executable builds and unsigned development test packages are not a
  Store installation, distribution approval or certification result.

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
- [ ] Resolve local-history storage protection against policy 10.5.4 and approve
      accurate final privacy/retention disclosures. A policy review is not compliance.
- [ ] Run Windows App Certification Kit against the final package.
- [ ] Verify Store download/install only after authorized publication.

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
- [ ] Verify manual publishing hold before any later authorized certification
      submission when public release has not separately been approved.

Distribution packaging, hosting changes and Store submission require explicit
authorization. No publication or final identity is inferred from this checklist.
