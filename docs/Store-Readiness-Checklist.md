# Microsoft Store Readiness Checklist

## App Package

- App name: `AppLens-desktop`
- Package identity placeholder: `CSI.AppLensDesktop`
- Version: `0.1.0.0`
- Target: Windows Desktop, minimum version `10.0.19041.0`
- Package type: MSIX / packaged WinUI 3 desktop app
- Trust level: medium integrity full trust desktop app, using `runFullTrust`

## Implemented For Store V1 Candidate

- WinUI 3 packaged app scaffold.
- Native C# read-only collectors.
- App inventory, tune diagnostics, readiness score, and tune plan.
- JSON, Markdown, and local HTML exports.
- Default redaction with explicit raw-detail export option.
- Unit tests for rules, reports, readiness, and tune-plan behavior.
- MSIX package smoke build.
- Local run script and Store candidate build script under `tools/`.

## Required Before Submission

- Reserve final app name in Partner Center.
- Replace placeholder CSI logo assets with final owned/licensed assets.
- Replace placeholder publisher identity with Partner Center identity.
- Add production privacy policy URL.
- Add support/contact URL.
- Capture Store screenshots from the final UI.
- Complete age rating questionnaire.
- Run Windows App Certification Kit on the final package.

## Drafted

- Store listing draft: [Store-Listing-Draft.md](Store-Listing-Draft.md)
- Tomorrow handoff: [Tomorrow-Handoff.md](Tomorrow-Handoff.md)

## Privacy Position

V1 is local-first and read-only. It collects workstation inventory and diagnostics only after the user runs a scan. It does not upload data, create accounts, run background services, change startup entries, change services, or perform remediation. Reports are exported only when the user chooses export.

Collected data may include:

- computer name and Windows username
- OS/device summary
- installed applications
- startup entries
- top processes
- selected service states
- storage hotspot paths and sizes
- repo placement paths
- WSL, Docker, and Ollama command summaries

Default exports redact user, machine, and profile path details. The UI has an explicit raw-detail export option.

## Certification Notes

- Keep Tune remediation out of V1.
- Avoid admin prompts.
- Avoid driver/service installation.
- Avoid automatic upload or telemetry.
- Do not claim Microsoft certification or affiliation.
- Note in certification comments that all probes are read-only and user-triggered.
