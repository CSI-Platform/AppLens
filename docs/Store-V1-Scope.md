# AppLens-desktop Store V1 Scope

## Goal

Store V1 is a read-only Windows desktop app that helps a non-technical user generate a workstation readiness report without using GitHub, PowerShell, or command-line tools.

## Included

- WinUI 3 packaged desktop app.
- Native C# backend collectors.
- Installed app inventory.
- AppLens-Tune diagnostics.
- Readiness score and highlights.
- Read-only tune plan guidance.
- JSON, Markdown, and local HTML exports.
- Redaction by default for user, machine, and profile-path details.
- Explicit raw-detail export option.
- MSIX package smoke build.

## Read-Only Tune Plan

The app may describe future actions, but it does not execute them in V1.

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
- `FutureUserConsent`
- `FutureAdminRequired`
- `Unsupported`

## Explicitly Out Of Scope For Store V1

- uninstalling apps
- changing startup entries
- changing or stopping services
- deleting files or caches
- admin elevation
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

