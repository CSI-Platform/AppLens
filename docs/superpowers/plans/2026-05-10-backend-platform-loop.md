# Backend Platform Loop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the backend-only AppLens platform loop that proves module detection, Tune action proposal, approval-gated execution, and ledger recording work together without frontend changes or live system-changing jobs.

**Architecture:** Extend the existing backend primitives instead of adding a parallel harness. `ModuleStatusService` owns standardized module manifest/status shape, `BlackboardStore` owns append-only events plus query helpers, and a new platform loop service coordinates detect -> propose -> approve -> execute -> record through `TuneActionExecutor`.

**Tech Stack:** C#/.NET 10, xUnit, JSONL ledger, SQLite index, existing AppLens backend project.

---

### Task 1: Ledger Query Contract

**Files:**
- Modify: `src/AppLens.Backend/BlackboardStore.cs`
- Modify: `src/AppLens.Backend/BlackboardEvent.cs`
- Test: `tests/AppLens.Backend.Tests/BlackboardStoreTests.cs`

- [x] Add `BlackboardEventQuery` with optional filters for event type, module id, app id, correlation id, data state, and result limit.
- [x] Add `IBlackboardStore.QueryAsync(BlackboardEventQuery query, CancellationToken cancellationToken = default)`.
- [x] Test that querying by correlation id and event type returns only matching events in newest-first order.

### Task 2: Standardized Module Manifest Shape

**Files:**
- Modify: `src/AppLens.Backend/ModuleManifest.cs`
- Modify: `src/AppLens.Backend/ModuleStatusService.cs`
- Test: `tests/AppLens.Backend.Tests/ModuleStatusServiceTests.cs`

- [x] Add stable manifest metadata: schema version, module kind, storage roots, health checks, and typed action contracts.
- [x] Populate AppLens-LLM, Oracle, Mailbox, and AppLens-Zero with the same manifest shape.
- [x] Test that every manifest has the platform schema version, at least one storage root, at least one health check, and typed action contracts.

### Task 3: Tune Approval Lifecycle

**Files:**
- Create: `src/AppLens.Backend/PlatformLoopService.cs`
- Test: `tests/AppLens.Backend.Tests/PlatformLoopServiceTests.cs`

- [x] Add proposal and approval records for Tune actions.
- [x] Propose actions by writing `ActionProposed` ledger events.
- [x] Approve actions by writing `ActionApproved` ledger events with an approval grant id.
- [x] Execute only when an approval record is approved and references the proposal.
- [x] Record execution through `ActionExecuted` ledger events.

### Task 4: Integration Proof

**Files:**
- Test: `tests/AppLens.Backend.Tests/PlatformLoopServiceTests.cs`

- [x] Use fake module paths and a fake Tune runtime.
- [x] Prove detect -> propose -> approve -> execute -> record produces ledger events in one shared correlation id.
- [x] Prove rejected approvals do not call the runtime and record a blocked action.

### Task 5: Verification and PR

**Files:**
- Commit only backend feature files and tests.

- [x] Run `dotnet test .\AppLensDesktop.sln`.
- [x] Run `dotnet build .\AppLensDesktop.sln`.
- [ ] Commit the scoped backend platform loop work.
- [ ] Push `codex/backend-platform-next`.
- [ ] Open a draft PR against `main`.
