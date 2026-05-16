# AppLens Scanner

## Responsibility

Scanner captures workstation evidence for CSI reviews. It answers what is installed, what is running, what starts automatically, what tooling is present, where storage is concentrated, and where local development work lives.

## Boundary

Scanner is evidence-only. It does not tune the machine, uninstall software, change services, delete files, upload data, or run background monitoring.

## Current Status

- Cross-platform scripts exist for Windows, macOS, and Linux.
- The desktop shell includes native C# collectors.
- Reports export to JSON, Markdown, local HTML, and script text output.
- Default exports redact user, machine, and profile-path details.
- Scan completion can be represented as blackboard evidence.

## Next Moves

- Keep Scanner as the low-friction intake path.
- Normalize Scanner output around blackboard event contracts.
- Separate raw private evidence from exportable summaries.
- Keep collection fast, explainable, and operator-triggered.
