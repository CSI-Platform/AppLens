# AppLens Platform Shell

## Responsibility

The platform shell is the Windows desktop control board. It shows local workstation state, module readiness, blackboard activity, pending work, exports, and operator decisions.

## Boundary

The shell should orchestrate. It should not own every module's domain logic, run hidden jobs, or bypass module manifests, approval rules, or blackboard records.

## Current Status

- Built with WinUI 3, .NET, and Windows App SDK.
- Hosts the dashboard, Scanner results, Tune plan, exports, module status, and blackboard-backed read models.
- Builds locally and has MSIX smoke packaging.
- Store submission assets and certification work remain incomplete.

## Next Moves

- Make module cards and pending approvals the primary desktop experience.
- Keep Scanner and Tune as first-party apps inside the shell.
- Add Planner once plan records and approval handoffs are stable.
- Treat Fleet, RAG, MCP, and Gov as future shell apps, not hard-coded features.
