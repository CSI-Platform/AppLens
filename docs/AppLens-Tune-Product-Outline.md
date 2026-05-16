# AppLens Tune Product Outline

This document supersedes the old read-only Tune outline. The active module spec is [AppLens Tune](AppLens-Tune-App.md).

## Product Shape

Tune is the workstation operations app inside the AppLens platform shell. Scanner tells the platform what exists and what is happening. Tune decides what deserves review, proposes operator actions, records approvals, executes allowed changes through narrow runtimes, and verifies the result.

The product is not a generic PC cleaner. It is a controlled operations workflow for consulting, AI-readiness, developer environment stabilization, and local-agent preparation.

## Users

- CSI operators preparing a client workstation or local AI node.
- Technical users who want evidence before changing startup, service, cache, repo, or runtime state.
- Future Planner and agent workflows that need a policy-gated action surface.

## Platform Role

Tune is one app on the control board. It depends on:

- **Scanner** for evidence.
- **Blackboard** for proposal, approval, execution, and verification records.
- **Platform Shell** for operator visibility and consent.
- **Planner** for future multi-step workflows.

Tune should not own global platform policy. It should expose proposed actions and evidence; the shell and blackboard enforce visibility, consent, and auditability.

## Workflow

1. Collect Scanner and Tune evidence.
2. Classify findings by category and risk.
3. Generate tune-plan items with evidence, guidance, backup concept, and verification step.
4. Propose eligible actions into the blackboard.
5. Capture explicit approval.
6. Execute through the Tune runtime only when policy and consent allow it.
7. Record the result and verify with a follow-up scan.

## Current Implementation

- `TunePlanItem`, `ProposedAction`, and `TuneActionRecord` models exist.
- `TunePlanBuilder` produces startup, service, storage, repo, local-AI, and manual-review guidance.
- `TuneActionExecutor` is the low-level runtime for selected startup, service, and cache actions.
- `PlatformLoopService` is the intended orchestration path for propose, approve, execute, and record.
- Reports include Tune plan detail.

## Non-Negotiables

- No hidden one-click optimization.
- No action without explicit approval.
- No silent admin escalation.
- No broad filesystem cleanup outside allowlisted cache roots.
- No service or startup change without evidence and verification guidance.
- Every action path must create blackboard records.

## Next Moves

- Make the desktop Tune UI show evidence, backup concept, and verification detail more clearly.
- Add verification records after post-action scans.
- Add rollback records for supported actions.
- Keep expanding action coverage only where the risk model is narrow and testable.
