# AppLens V1 Platform Ledger Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the AppLens-local evidence ledger and platform-host foundation without merging external apps or building MCP/A2A in v1.

**Architecture:** AppLens remains snapshot-centric. A new backend ledger writes append-only JSONL events under `%LOCALAPPDATA%\AppLens\ledger\events.jsonl` and maintains a SQLite index under `%LOCALAPPDATA%\AppLens\ledger\index.sqlite`; the desktop orchestrator appends events after scans and Tune actions. Static module manifests and status checks describe LLM, Oracle, Mailbox, and Zero without running live jobs.

**Tech Stack:** .NET, WinUI, xUnit, `Microsoft.Data.Sqlite`, JSONL, `%LOCALAPPDATA%` runtime storage.

---

## Scope

Implement now:

- Local runtime storage root resolver with `%LOCALAPPDATA%\AppLens` default.
- Evidence ledger event model with future-proof identity/scope/privacy/policy fields.
- JSONL append log as truth.
- SQLite index as rebuildable local query layer.
- Backend tests for append, readback, index creation, and corrupt-line tolerance.
- Desktop orchestration hooks for scan completion and Tune action completion.
- Static app/module manifest model and status model for LLM, Oracle, Mailbox, and Zero.
- UI additions in the current single-page dashboard for ledger storage/status and hosted-app statuses.

Defer:

- MCP adapter.
- A2A adapter.
- Full participant registry.
- Double-handshake grants.
- Cryptographic signing.
- Live Oracle research, LLM lane start/stop, Mailbox sync/send.
- Mailbox, Oracle, AppLens-LLM, or Stigmergent code edits.
- Broad Tune action expansion unless it fits safely after the ledger is complete.

## File Structure

- Create `src/AppLens.Backend/RuntimeStorage.cs`
  - Resolves AppLens runtime root and ledger paths.
- Create `src/AppLens.Backend/BlackboardEvent.cs`
  - Defines the ledger event contract, enums, artifact refs, provenance, and policy result.
- Create `src/AppLens.Backend/BlackboardStore.cs`
  - Implements `IBlackboardStore`, append, read, SQLite index, and rebuild.
- Create `src/AppLens.Backend/ModuleManifest.cs`
  - Defines static hosted-module manifest and status contracts.
- Create `src/AppLens.Backend/ModuleStatusService.cs`
  - Computes blocked/available statuses without live jobs.
- Modify `src/AppLens.Backend/AppLens.Backend.csproj`
  - Add `Microsoft.Data.Sqlite`.
- Modify `src/AppLens.Desktop/MainWindow.xaml`
  - Add compact ledger/status section in the existing single-page dashboard.
- Modify `src/AppLens.Desktop/MainWindow.xaml.cs`
  - Instantiate ledger/status services and append events after scan and Tune action completion.
- Create `tests/AppLens.Backend.Tests/BlackboardStoreTests.cs`
  - TDD coverage for local ledger behavior.
- Create `tests/AppLens.Backend.Tests/ModuleStatusServiceTests.cs`
  - TDD coverage for static status checks using temp dirs.
- Modify `tests/AppLens.Backend.Tests/AppLens.Backend.Tests.csproj`
  - Add SQLite dependency if needed by test project restore.

## Task 1: Runtime Storage Root

**Files:**
- Create: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\src\AppLens.Backend\RuntimeStorage.cs`
- Test: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\tests\AppLens.Backend.Tests\RuntimeStorageTests.cs`

- [ ] Write a failing test proving explicit roots create expected ledger paths.
- [ ] Run `dotnet test .\tests\AppLens.Backend.Tests\AppLens.Backend.Tests.csproj --filter RuntimeStorageTests`.
- [ ] Implement `AppLensRuntimeStorage` with `Root`, `LedgerDirectory`, `EventsJsonl`, and `IndexSqlite`.
- [ ] Run the focused test until green.

## Task 2: Ledger Event Contract

**Files:**
- Create: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\src\AppLens.Backend\BlackboardEvent.cs`
- Test: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\tests\AppLens.Backend.Tests\BlackboardEventTests.cs`

- [ ] Write a failing test for creating a scan-completed event with schema version, participant identity, module id, app id, scope id, correlation id, data state, privacy state, and lifecycle state.
- [ ] Run the focused test and verify it fails because the model does not exist.
- [ ] Implement event records and enums.
- [ ] Run focused tests until green.

## Task 3: JSONL Append Store

**Files:**
- Create: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\src\AppLens.Backend\BlackboardStore.cs`
- Test: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\tests\AppLens.Backend.Tests\BlackboardStoreTests.cs`

- [ ] Write a failing test that appends an event and reads it back from JSONL.
- [ ] Write a failing test that a corrupt JSONL line is skipped while valid lines still load.
- [ ] Run focused tests and verify failures.
- [ ] Implement `IBlackboardStore.AppendAsync` and `ReadAllAsync`.
- [ ] Run focused tests until green.

## Task 4: SQLite Index

**Files:**
- Modify: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\src\AppLens.Backend\AppLens.Backend.csproj`
- Modify: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\src\AppLens.Backend\BlackboardStore.cs`
- Modify: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\tests\AppLens.Backend.Tests\BlackboardStoreTests.cs`

- [ ] Add `Microsoft.Data.Sqlite` to the backend project.
- [ ] Write a failing test proving appending an event creates `index.sqlite` and indexes event id, type, module id, app id, created time, data state, privacy state, and summary.
- [ ] Run focused tests and verify failure.
- [ ] Implement SQLite initialization and indexing.
- [ ] Run focused tests until green.

## Task 5: Module Manifests And Status

**Files:**
- Create: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\src\AppLens.Backend\ModuleManifest.cs`
- Create: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\src\AppLens.Backend\ModuleStatusService.cs`
- Test: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\tests\AppLens.Backend.Tests\ModuleStatusServiceTests.cs`

- [ ] Write failing tests for Oracle blocked when repo path is absent.
- [ ] Write failing tests for Mailbox blocked when config is absent.
- [ ] Write failing tests for LLM blocked when CLI/package path is absent.
- [ ] Write failing tests for Zero blocked when no raw/import source is configured.
- [ ] Implement static manifests and file-only status checks.
- [ ] Run focused tests until green.

## Task 6: Scan Completion Ledger Hook

**Files:**
- Modify: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\src\AppLens.Desktop\MainWindow.xaml.cs`
- Optional test seam: backend factory method in `BlackboardEvent.cs`

- [ ] Add a backend test for a scan-completed event factory using a small `AuditSnapshot`.
- [ ] Run focused test and verify failure.
- [ ] Implement the factory method.
- [ ] Wire desktop scan completion to append the event after `AuditService.RunAsync` returns.
- [ ] Run backend tests.

## Task 7: Tune Action Ledger Hook

**Files:**
- Modify: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\src\AppLens.Desktop\MainWindow.xaml.cs`
- Optional test seam: backend factory method in `BlackboardEvent.cs`

- [ ] Add a backend test for Tune action-completed event factory using a `TuneActionRecord`.
- [ ] Run focused test and verify failure.
- [ ] Implement the factory method.
- [ ] Wire desktop Tune action completion to append one event per returned action record.
- [ ] Run backend tests.

## Task 8: Dashboard Status Section

**Files:**
- Modify: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\src\AppLens.Desktop\MainWindow.xaml`
- Modify: `C:\Users\codyl\Desktop\csiOS\Projects\AppLens\src\AppLens.Desktop\MainWindow.xaml.cs`

- [ ] Add a compact storage/status section to the existing single-page dashboard.
- [ ] Show runtime root, ledger file path, indexed event count if available, and module statuses.
- [ ] Keep the current single-page shape; do not add navigation.
- [ ] Build the desktop project.

## Task 9: Verification

**Files:**
- No new files.

- [ ] Run `dotnet test C:\Users\codyl\Desktop\csiOS\Projects\AppLens\AppLensDesktop.sln`.
- [ ] Run `dotnet build C:\Users\codyl\Desktop\csiOS\Projects\AppLens\AppLensDesktop.sln`.
- [ ] Inspect `git diff --stat`.
- [ ] Confirm no files changed in AppLens-LLM, Oracle, Mailbox, or Stigmergent.

## Self-Review

This plan covers the approved v1 platform foundation: local runtime root, JSONL + SQLite ledger, future-proof schema hooks, manifest/status boundaries, scan/Tune persistence, and current UI shape. It intentionally defers MCP, A2A, full registry, double-handshake, live external jobs, and broader Tune side effects until the ledger and host contracts are stable.
