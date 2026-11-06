# Microsoft Store listing draft

Publication draft; the Partner Center draft has not been submitted for certification.
Use the [submission preparation packet](AppLens-Store-Submission-Plan.md) for exact
Partner Center inputs, screenshot dimensions/captions and the final test session.

## App name
Installed. Reserved Store ID: `9PBPFLL6JVVV`; publisher: Copper State Intelligence.
The repository, assemblies and legacy storage paths retain the AppLens name.

## Short description
Share your PC specifications and installed tools to plan a workflow with your consultant.

## Description
Installed prepares a local snapshot of your Windows 11 PC for a workflow
consultation. Open the app to collect PC specifications, storage and installed
tools. Add optional goals, online services or requirements, review the report,
then choose how to share it with your consultant through Windows sharing.
Installed software does not prove a license, sign-in or working service access;
unknown readings and scan coverage remain visible.

Uninstall eligible desktop and current-user Store apps after confirming the exact
installation. Windows or the vendor may request administrator approval. Installed
respects restrictions, checks the result, and records observed storage changes.
App data can be deleted by an uninstall; save your work and review the confirmation.

Download the full inventory and action history as Markdown, JSON or HTML for
review or sharing. Personal identifiers and profile paths are redacted by
default, with an explicit option to include them. You choose the sharing app and
recipient. If your email app is unavailable, save the report and attach it manually.

## Features
- Local app inventory and disk total/used/free readings.
- PC specification summary and optional workflow context.
- Review before sharing, with Markdown, JSON and HTML report downloads.
- Search and size/name/publisher sorting; scope/type/removal filters.
- Explicit supported uninstall actions, Windows approval, and result verification.
- Local action history and downloadable reports.
- No accounts, telemetry, automatic uploads, or background optimization.

## Limits to describe accurately
Reported app sizes can be missing or differ from recoverable space. System and
shared components are separated from ordinary apps. Some uninstaller commands
and restricted apps require review in Windows Installed Apps. That handoff is
not a verified removal. Current-user MSIX removal does not remove other users'
registrations. Scan speed varies; do not advertise a universal one-second
completion time. AppLens-Tune is deferred and is not included in this release.

## First-release system requirements
Windows 11 on an x64 PC (minimum Windows build 22000). Windows 10 and native ARM64
are deferred. ARM64 emulation has not been validated; confirm actual Store device
availability before submission rather than implying the x64 package blocks it.

## Listing fields requiring owner/Store input
- Reserved name: Installed; publisher: Copper State Intelligence; Store ID: 9PBPFLL6JVVV.
- Category choice: Utilities & tools or Productivity, according to available Store categories.
- Age-rating questionnaire and supported markets.
- Verified, published privacy policy and support URLs.
- Review final screenshots and artwork before submission.

## Screenshot subjects
The PC summary, optional workflow context, reviewed report and sharing controls,
plus the app table and secondary removal/history flow. Use reviewed screenshots without personal data;
internal development captures are not automatically Store listing assets.

## Certification notes
The app runs as an ordinary medium-integrity WinUI desktop process. runFullTrust
supports local registry/WMI inventory and supported Windows/vendor uninstall
mechanisms. No packageManagement or allowElevation capability is requested.
Installed itself remains unelevated; Windows handles explicit administrator
handoffs. Explain and demonstrate each supported removal route in certification.

### Reviewer walkthrough (final identity/build to be supplied)
No login, account or server is required. Launch Installed; a read-only scan starts.
Inspect the PC summary, add disposable sample workflow notes, then review and
save a redacted report. Send report opens review before Windows sharing. Cancel
without sending, or use a controlled test target to inspect the received file.
Opening the sharing chooser is not proof of delivery.
Search by app/publisher, choose Largest first, and inspect an app's scope/details.
Unknown sizes mean unavailable metadata, not zero disk usage.

Use a disposable app in the certification test environment for removal. Installed
revalidates the selected installation before showing its confirmation. Cancel
leaves it installed. Supported routes are Windows Installer for an exact product
GUID, a validated registered vendor EXE, or current-user package removal through
Windows PackageManager. All-user desktop routes can request a Windows admin
handoff. Installed stays unelevated and does not bypass denied approval or policy.
Restricted components stay disabled; Windows Settings handoffs are labeled as
handoffs and do not claim removal. No arbitrary commands are read from a report.

After an approved fixture removal, inspect refreshed inventory and action history,
then download JSON, Markdown or HTML. The report includes the complete capture
even if the table is filtered. Personal identifiers are off by default. Close and
reopen to verify history. Uninstall Installed itself through Windows Installed Apps.

Final package identity, supported platforms, delivery size and prerequisite
behavior must be filled from the tested submission candidate. The package is
configured to include .NET and use Store-managed Windows App SDK dependencies;
the clean-PC installation has not yet verified their delivery.

