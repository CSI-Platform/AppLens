# Microsoft Store Readiness Checklist

Store packaging is a distribution track for the AppLens platform shell. It should validate installability, privacy posture, export control, and user trust. It is not the product scope.

## App Package

- App name: `AppLens-desktop`
- Package identity placeholder: `CSI.AppLensDesktop`
- Version: `0.1.0.0`
- Target: Windows Desktop, minimum version `10.0.19041.0`
- Package type: MSIX / packaged WinUI 3 desktop app
- Trust level: medium integrity full-trust desktop app, using `runFullTrust`

## Implemented In The Current Shell

- WinUI 3 packaged app scaffold.
- Native C# Scanner and Tune collectors.
- App inventory, diagnostics, readiness score, and tune plan.
- Blackboard event and store primitives.
- Module status and dashboard read models.
- JSON, Markdown, and local HTML exports.
- Default redaction with explicit raw-detail export option.
- Unit tests for backend behavior and dashboard presentation.
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

The Store build should remain local-first and operator-controlled. It collects workstation inventory and diagnostics only after the user runs a scan. It does not upload data, create accounts, run background services, or automatically share reports. Reports are exported only when the user chooses export.

System-changing actions must remain explicit, approval-gated, and blackboard-recorded. If any action path is not ready for Store submission, disable it in the Store build and keep the read-only plan visible.

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

- Keep any Store-submitted action path explicit, approved, and documented.
- Avoid admin prompts.
- Avoid driver/service installation.
- Avoid automatic upload or telemetry.
- Do not claim Microsoft certification or affiliation.
- Note in certification comments that probes are local and user-triggered.
