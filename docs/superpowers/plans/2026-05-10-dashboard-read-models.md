# Dashboard Read Models Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add backend-only dashboard read models so frontend work can bind to stable module cards, pending Tune approvals, recent ledger events, and summary state.

**Architecture:** Build a read-only `DashboardReadModelService` on top of `ModuleStatusService` and `IBlackboardStore`. The service must not execute Tune actions or run module jobs; it only maps existing module manifests/statuses and ledger events into UI-ready objects.

**Tech Stack:** C#/.NET 10, xUnit, existing AppLens backend module status and blackboard ledger services.

---

### Task 1: Dashboard Read Model Tests

**Files:**
- Create: `tests/AppLens.Backend.Tests/DashboardReadModelServiceTests.cs`

- [x] Add tests for module cards built from manifests and module statuses.
- [x] Add tests for pending Tune actions from `ActionProposed` events without matching `ActionApproved` events.
- [x] Add tests for recent ledger event ordering and dashboard summary counts.

### Task 2: Dashboard Read Model Service

**Files:**
- Create: `src/AppLens.Backend/DashboardReadModelService.cs`

- [x] Add `AppLensDashboardState`, `DashboardSummaryReadModel`, `ModuleCardReadModel`, `PendingTuneActionReadModel`, and `LedgerEventReadModel`.
- [x] Add `DashboardReadModelService.GetDashboardStateAsync`.
- [x] Add `DashboardReadModelService.GetModuleCards`.
- [x] Add `DashboardReadModelService.GetPendingActionsAsync`.
- [x] Add `DashboardReadModelService.GetRecentLedgerEventsAsync`.

### Task 3: Verification and PR

**Files:**
- Commit only backend read-model files and tests.

- [x] Run focused read-model tests.
- [x] Run `dotnet test .\AppLensDesktop.sln`.
- [x] Run `dotnet build .\AppLensDesktop.sln`.
- [ ] Commit, push `codex/dashboard-read-models`, and open a draft PR against `main`.
