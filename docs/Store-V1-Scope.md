# AppLens-desktop Store V1 Scope

## Goal

Store V1 is a local Windows desktop app that helps a non-technical user generate a workstation readiness report and run selected AppLens-Tune actions with explicit approval.

## Included

- WinUI 3 packaged desktop app.
- Native C# backend collectors.
- Installed app inventory.
- AppLens-Tune diagnostics and selected action workflow.
- Readiness score and highlights.
- Tune plan guidance.
- Action log export.
- JSON, Markdown, and local HTML exports.
- Redaction by default for user, machine, and profile-path details.
- Explicit raw-detail export option.
- MSIX package smoke build.

## Consent-Based Tune Actions

The app may execute selected supported actions only after user consent. Unsupported, risky, or admin-bound items are blocked and recorded in the action log.

Modeled future action types:

- disable startup entry
- set service to manual
- stop service
- clear rebuildable cache
- uninstall application
- move repo
- manual review

V1 execution state:

- `ReadOnlyOnly`
- `RequiresUserConsent`
- `RequiresAdmin`
- `Completed`
- `Failed`
- `RolledBack`
- `Unsupported`

## Explicitly Out Of Scope For Store V1

- unapproved actions
- broad app uninstall/debloat behavior
- deleting user documents or project data
- driver, firmware, firewall, or security policy changes
- unattended admin elevation
- background monitoring
- telemetry
- cloud upload
- account sign-in
- automatic sharing

## Store Submission Gaps

- Replace placeholder CSI assets.
- Reserve final app name in Partner Center.
- Replace placeholder publisher identity.
- Add hosted privacy policy and support URLs.
- Capture final screenshots.
- Run Windows App Certification Kit.
- Generate final Store upload package through Visual Studio or Store tooling.

