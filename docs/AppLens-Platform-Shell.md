# AppLens Platform Shell

## Responsibility

The platform shell is the Windows desktop control board. It shows local workstation state, module readiness, blackboard activity, pending work, exports, and operator decisions.

## Boundary

The shell should orchestrate. It should not own every module's domain logic, run hidden jobs, or bypass module manifests, approval rules, or blackboard records.

## Module Action Contract

Module manifests can declare actions, but the shell only labels an action as runnable when a matching executor key is registered in the platform runtime.

- `read` actions do not need an executor; they describe status/readiness already surfaced by the shell.
- Action permissions such as `open-local-ui`, `write-ledger`, `execute-local`, and future SSH-backed runners must include an executor key.
- If a module is detected but its executor key is not registered, the module card must show the action surface as `No runner`, not `Runnable`.
- If a module is missing required files or configuration, the action surface is blocked by module availability before executor lookup.

## Current Status

- Built with WinUI 3, .NET, and Windows App SDK.
- Hosts the dashboard, Scanner results, Tune plan, exports, module status, and blackboard-backed read models.
- Module cards distinguish available modules from not-yet-implemented action executors.
- Builds locally and has MSIX smoke packaging.
- Store submission assets and certification work remain incomplete.

## Next Moves

- Make module cards and pending approvals the primary desktop experience.
- Keep Scanner and Tune as first-party apps inside the shell.
- Add Planner once plan records and approval handoffs are stable.
- Treat Fleet, RAG, MCP, and Gov as future shell apps, not hard-coded features.
