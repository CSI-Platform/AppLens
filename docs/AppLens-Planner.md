# AppLens Planner

## Responsibility

Planner is the operator-facing planning app for multi-step local work. It should turn evidence and module status into a clear sequence of recommended actions, approvals, dependencies, and verification gates.

## Boundary

Planner should not execute work directly. It should create plans, request approvals, track state, and hand work to the relevant module through the platform loop.

## Current Status

- Planner is a product module, not a full implemented app.
- Current planning behavior is represented through docs, tune plans, dashboard summaries, and blackboard events.
- The platform loop provides the execution pattern Planner should use.

## Next Moves

- Define a plan event schema for steps, dependencies, owners, approvals, and completion state.
- Read Scanner, Tune, and module status from the blackboard.
- Generate operator-readable plans without hiding risk.
- Keep plan execution mediated by approvals and module contracts.
