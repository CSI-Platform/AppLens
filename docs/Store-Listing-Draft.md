# Microsoft Store listing draft

Publication draft; no Store submission has been made.
Use the [submission preparation packet](AppLens-Store-Submission-Plan.md) for exact
Partner Center inputs, screenshot dimensions/captions and the final test session.

## App name
AppLens. Final name reservation and publisher identity must come from Partner Center.

## Short description
Review installed apps and disk usage, approve supported uninstalls, and download a local report.

## Description
Installed puts your Windows 11 app inventory and storage usage in one simple table.
Run a local scan, search apps or publishers, and sort by name or reported size.
See installation scope, removal restrictions, disk capacity and free space, plus
secondary Windows, processor, graphics and memory details.

Uninstall eligible desktop and current-user Store apps after confirming the exact
installation. Windows or the vendor may request administrator approval. Installed
respects restrictions, checks the result, and records observed storage changes.
App data can be deleted by an uninstall; save your work and review the confirmation.

Download the full inventory and action history as Markdown, JSON or HTML for
review or sharing. Personal identifiers and profile paths are redacted by
default, with an explicit option to include them. Installed saves locally; you
choose whether and how to send the report.

## Features
- Local app inventory and disk total/used/free readings.
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
The finished app table, largest-first storage review, exact-target confirmation,
and download/history controls. Use reviewed screenshots without personal data;
internal development captures are not automatically Store listing assets.

## Certification notes
The app runs as an ordinary medium-integrity WinUI desktop process. runFullTrust
supports local registry/WMI inventory and supported Windows/vendor uninstall
mechanisms. No packageManagement or allowElevation capability is requested.
Installed itself remains unelevated; Windows handles explicit administrator
handoffs. Explain and demonstrate each supported removal route in certification.

### Reviewer walkthrough (final identity/build to be supplied)
No login, account or server is required. Launch Installed and select Scan this PC.
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

