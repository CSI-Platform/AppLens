# AppLens Tune

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

## Next Moves

- Route desktop Tune execution through the platform loop.
- Record every proposed, approved, executed, blocked, failed, or verified action in the blackboard.
- Expand rules only when evidence, risk, backup, and verification are clear.
- Keep admin-required actions separate from standard-user actions.
