# AppLens Scanner

## Responsibility

The AppLens v1 scanner captures installed-app inventory, reported sizes, disk
utilization, removal information, and secondary device details for CSI reviews.
Startup, services, performance analysis, local AI/tooling, and repo diagnostics
belong to the existing AppLens-Tune, which is intentionally deferred. The core
scanner must operate independently of that feature; see the
[v1 implementation boundary](AppLens-Product-Vision.md#applens-v1-implementation-boundary).

## Boundary

Scanner is evidence-only. It does not tune the machine, uninstall software, change services, delete files, upload data, or run background monitoring.

## Current Status

- Cross-platform scripts exist for Windows, macOS, and Linux.
- The Windows initial script includes hardware/OS, uptime, RAM utilization,
  capacity/used/free space for local fixed disks and app counts.
  Attention flags use below 10% disk free and at least 85% RAM used; these are
  snapshot thresholds, not a hardware health diagnosis.
- Each Windows script run writes a timestamped report and refuses to overwrite
  an existing output path. Failed/empty workstation probes are labeled unavailable.
  The initial scan does not recurse through folders or invoke Tune runtime probes.
- The initial Windows script does not enumerate or rank running processes. The
  proposed client scan/table contract is in [Product Vision](AppLens-Product-Vision.md).
- The desktop shell includes native C# collectors.
- Reports export to JSON, Markdown, local HTML, and script text output.
- Default desktop-app exports redact user, machine, and profile-path details;
  standalone script text reports retain local identifiers.
- Scan completion can be represented as blackboard evidence.

## Next Moves

- Keep Scanner as the low-friction intake path.
- Normalize Scanner output around blackboard event contracts.
- Separate raw private evidence from exportable summaries.
- Keep collection fast, explainable, and operator-triggered.
