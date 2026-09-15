# Microsoft Store readiness

The active v1 flow is inventory -> storage/app table -> approved removal ->
verification -> local report. AppLens-Tune is preserved outside the core build.
The implementation plan is [Product Vision](AppLens-Product-Vision.md); changing
work status is tracked in Linear, not duplicated here.
For completed source checks and a condensed keep/defer list of hands-on work,
see [release validation](AppLens-Release-Validation.md).

## Package and runtime
- Native C#/.NET 10, WinUI 3 / Windows App SDK.
- Medium-integrity desktop app; only runFullTrust is declared.
- Source manifest minimum Windows build 19041; x64 and ARM64 project targets.
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
Actual screen-reader audio and the latest picker change in a newly installed
package remain to be tested. Desktop control is paused at the user's request;
continue those hands-on checks later, as listed in the [handoff](AppLens-v1-Handoff.md).

- [ ] Complete the installed customer flow on a clean standard-user Windows PC,
      including native/package dependencies and offline scan/export.
- [ ] Exercise all-user administrator removal, denied UAC, protected/managed
      cases, cancellation, failed/slow uninstallers, and app closure mid-action.
      Reuse recorded COP-240 approval/denial successes; focus on remaining gaps.
- [ ] Verify keyboard navigation, screen-reader naming, text scaling and Windows
      high contrast, including small screens and large inventories.
- [ ] Verify both claimed architectures on appropriate hardware. A cross-build
      does not prove ARM64 runtime behavior.
- [ ] Measure launch, first results and completion, package download, installed
      footprint, and first-install prerequisites for the actual distribution.
- [ ] Check current Microsoft Store policies, capabilities and privacy disclosures.
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

Distribution packaging, hosting changes and Store submission require explicit
authorization. No publication or final identity is inferred from this checklist.
