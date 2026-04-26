# AppLens-Tune Product Outline

## Product Shape

AppLens-Tune is the workstation-readiness layer that sits next to AppLens. AppLens answers what is installed. AppLens-Tune answers what is running, what is starting automatically, what is consuming resources, what deserves review, and what changed after an approved tune-up.

The product should feel like a professional client-audit tool, not a generic PC cleaner. Its value is evidence, judgment, reversibility, and clear client-facing reporting.

## Primary User

- CSI consultant preparing a client machine for workflow, AI-readiness, automation, or development work.
- Small-business owner or employee who needs a readable workstation health report.
- Technical operator who wants a safe baseline before making manual system changes.

## V1 Positioning

AppLens-Tune V1 should remain read-only inside AppLens-desktop.

It should:

- collect workstation performance and startup evidence
- classify findings by review category
- explain why each finding matters
- export redacted reports
- prepare a tune plan without changing the system

It should not:

- disable services
- remove startup entries
- delete caches
- uninstall packages
- require admin rights
- run background monitoring

## App Structure

### 1. Overview

Purpose: show the machine's current workstation readiness at a glance.

Content:

- machine summary
- scan completion state
- total review items
- startup load summary
- top memory pressure indicators
- storage hotspot summary
- high-confidence recommendations

### 2. Startup

Purpose: explain what launches at sign-in and what deserves review.

Content:

- registry startup entries
- startup folder shortcuts
- startup approval state
- packaged app startup tasks where available
- publisher and path redaction
- classification: keep, review, user choice, admin required, do not touch

### 3. Services

Purpose: highlight selected background services without pretending to audit all of Windows.

Content:

- selected OEM services
- local AI and developer tool services
- Microsoft 365 and Copilot-adjacent services where identifiable
- Docker, WSL, virtualization, and update helpers
- service state and start mode
- conservative recommendation category

### 4. Processes

Purpose: show current resource pressure in plain language.

Content:

- top memory processes
- top CPU processes when available
- process count summary
- known app family grouping
- repeated helper processes
- notes when a process is normal but worth understanding

### 5. Storage

Purpose: show rebuildable or reviewable storage hotspots.

Content:

- package caches
- local AI model caches
- developer caches
- Docker/WSL storage indicators
- repo location checks
- OneDrive or synced-folder placement warnings

### 6. Dev And AI Readiness

Purpose: summarize whether local tooling is likely to interfere with consulting work.

Content:

- WSL status
- Docker status
- Ollama status
- common CLI availability
- repo placement
- path and profile redaction

### 7. Tune Plan

Purpose: prepare a safe action plan without executing it in V1.

Content:

- finding
- evidence
- recommended action
- risk level
- admin requirement
- rollback concept
- verification step

### 8. Reports

Purpose: create useful artifacts for both CSI and the client.

Outputs:

- JSON for structured records
- Markdown for consultant notes
- local HTML for client-friendly delivery

Default reports should redact usernames, machine names, and private paths. Raw-detail export should remain explicit.

## Data Contract

The current `AuditSnapshot` should continue to be the top-level contract. AppLens-Tune should extend the `tune` and `findings` sections instead of creating a second disconnected data model.

Core objects:

- `TuneSnapshot`
- `StartupEntry`
- `StartupApprovalState`
- `ServiceSnapshot`
- `ProcessSnapshot`
- `StorageHotspot`
- `RepoPlacement`
- `ToolProbeStatus`
- `Finding`
- `TunePlanItem`

Future remediation objects:

- `ProposedAction`
- `ActionExecution`
- `RollbackRecord`
- `VerificationResult`

## Rule Categories

Use stable, readable categories:

- `Keep`
- `Review`
- `Optional`
- `UserChoice`
- `AdminRequired`
- `DoNotTouch`

Avoid aggressive labels such as "bad", "junk", or "bloat." The product should sound like an audit tool.

## V1 Build Plan

### Phase 1: Stronger Read-Only Backend

- Expand startup collection.
- Add startup approval state where available.
- Add selected scheduled task inspection.
- Improve service profiles with vendor, purpose, and safety notes.
- Add richer storage hotspot sizing with timeouts.
- Add rule tests for each finding category.

### Phase 2: Tune Plan Model

- Add `TunePlanItem`.
- Map findings to proposed actions.
- Include risk, admin requirement, backup concept, and verification step.
- Export tune plans in JSON, Markdown, and HTML.
- Keep execution disabled.

### Phase 3: Desktop UX

- Add AppLens-Tune navigation areas.
- Add filters by category and risk.
- Add client-friendly explanations.
- Add export controls for redacted and raw-detail reports.
- Keep the UI dense, calm, and operational.

### Phase 4: Verification Workflow

- Add before/after snapshot comparison.
- Add reboot-needed markers.
- Add manual verification checklist.
- Export comparison reports.

### Phase 5: Optional Remediation Research

This phase should not ship until V1 is stable.

Requirements before any remediation:

- explicit consent
- admin boundary detection
- backup record
- dry-run mode
- rollback notes
- post-change verification
- narrow allowlist of supported actions

## First Build Slice

The best first implementation slice is:

1. Add `TunePlanItem` to the backend model.
2. Generate tune plan items from existing read-only findings.
3. Export those plan items in JSON, Markdown, and HTML.
4. Add unit tests and golden report coverage.
5. Show the tune plan in AppLens-desktop as read-only guidance.

This slice is useful, low-risk, and directly prepares the product for a future approved remediation workflow.

