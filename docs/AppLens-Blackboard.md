# AppLens Blackboard

## Responsibility

Blackboard is CSI's proprietary local coordination layer. It gives AppLens modules a shared, auditable memory for evidence, status, policy, proposals, approvals, executions, verification, and imports.

## Boundary

Blackboard is not a remote sync service, telemetry channel, or generic database. It is the local operating record for one workstation unless a future Fleet module explicitly extends it.

## Current Status

- Events are modeled with schema version, participant, module, app, scope, correlation, lifecycle, data state, privacy state, payload, artifacts, provenance, and policy result.
- The store supports append-only JSONL and a SQLite index.
- Existing event types cover module detection, scan completion, Tune proposal, Tune approval, Tune execution, verification, model runs, and blocked state.
- Dashboard read models can consume blackboard state.

## Next Moves

- Treat the blackboard as the contract between modules.
- Keep raw private data separate from sanitized/exportable data.
- Add policy checks before higher-risk actions.
- Preserve correlation IDs across multi-step work.
