# AppLens Platform Scope

## Position

AppLens is CSI's local control board for workstation apps and agents. It borrows from mobile operating systems: a shell hosts focused apps, each app declares capabilities, and shared services handle state, permissions, evidence, and handoffs.

The differentiator is CSI's proprietary blackboard technology. Scanner, Tune, Planner, and future modules do not pass around ad hoc files or hidden state. They publish evidence, status, proposed actions, approvals, and verification records into a local blackboard that can be indexed, audited, redacted, and exported.

## Core Product

- **Platform shell**: WinUI desktop control board for local modules, operator decisions, and exports.
- **Scanner**: local evidence collection for installed apps, runtime state, tooling, storage, and readiness.
- **Tune**: diagnostic plan, approval-gated action path, execution records, and verification.
- **Blackboard**: local append-only event layer backed by JSONL and SQLite indexing.
- **Planner**: planning surface for multi-step work across local modules.
- **Future modules**: Fleet, RAG, MCP, and Gov.

## Operating Rules

- Local-first by default.
- No telemetry, accounts, or cloud upload by default.
- Read-only evidence capture is the default posture.
- Actions require explicit operator approval.
- System-changing work must record proposal, approval, execution, and verification.
- Raw local detail stays private unless the operator exports it.

## Current Boundary

The current repo contains the desktop shell, Scanner/Tune collectors, blackboard event/store primitives, module manifests/status checks, dashboard read models, reports, tests, and Store packaging smoke tooling.

The platform is not yet a full app marketplace, fleet manager, remote agent runner, RAG control plane, MCP gateway, or governance console. Those are product modules that should attach through the same shell, manifest, policy, and blackboard contracts.

## Module Runtime Contract

Module detection and module execution are separate states. A module can be configured and available while its action executor is still not implemented in this shell build.

- Manifests declare action names, permissions, approval requirements, system-changing flags, and executor keys.
- The dashboard read model resolves those actions against the platform executor registry.
- Only actions with a registered executor are shown as runnable.
- Missing executors are shown as not implemented so module cards do not overpromise local jobs, UI launches, imports, or future SSH actions.

## Distribution

Microsoft Store packaging remains a distribution track for the Windows shell. It is not the product strategy. Store readiness should prove that the shell is installable, transparent, local-first, and operator-controlled.
