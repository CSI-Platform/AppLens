# AppLens Tune

## Release Boundary

AppLens-Tune refers to the existing scripts and native Tune functionality. It is
intentionally deferred until the core AppLens v1 flow is complete. The agreed
implementation plan separates Tune-specific code into `future/AppLens-Tune/`,
preserves relevant tests/docs and resumption instructions, and removes its
dependencies from core launch, scan, uninstall, and export. Shared infrastructure
needed by v1 stays in the normal backend. See
[Product Vision](AppLens-Product-Vision.md#applens-v1-implementation-boundary).

The implementation has been relocated. See the
[preservation and resumption README](../future/AppLens-Tune/README.md).
Existing behavior below describes that deferred work, not the active v1 client.

## Responsibility

Tune turns workstation evidence into a prioritized plan. It identifies startup load, service noise, storage hotspots, repo placement risk, local AI readiness, and developer tooling issues.

## Boundary

Tune can propose work. Execution must remain approval-gated, recorded, and reversible where practical. Tune should never hide changes behind a score or one-click optimization claim.

## Current Status

- Readiness scoring and tune-plan guidance exist.
- Proposed actions include startup, service, cache, uninstall, repo, and manual-review categories.
- The backend includes proposal, approval, execution, and action event models.
- The action executor remains the low-level runtime.
- The platform loop is the intended path for approval-gated execution.

## When AppLens-Tune Development Resumes

Resume only after core v1 is complete, using the deferred implementation's README
and verification evidence to establish its actual state. Do not treat these
items as first-release AppLens requirements.

- Route desktop Tune execution through the platform loop.
- Record every proposed, approved, executed, blocked, failed, or verified action in the blackboard.
- Expand rules only when evidence, risk, backup, and verification are clear.
- Keep admin-required actions separate from standard-user actions.
