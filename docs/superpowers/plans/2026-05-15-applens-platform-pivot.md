# AppLens Platform Pivot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Reframe AppLens from a legacy Store-scope readiness utility into CSI's local control board for scanner, Tune, agent, planner, and blackboard workflows.

**Architecture:** Keep the current WinUI desktop shell and C# backend, but rename the product envelope around platform modules and the blackboard. Tune remains executable, but desktop execution must use the platform proposal/approval/execution path. External modules become configured capabilities rather than hard-coded missing dependencies.

**Tech Stack:** .NET 10, WinUI 3, xUnit, local JSONL/SQLite blackboard, Markdown docs.

---

### Task 1: Replace Legacy Store Scope With Platform Scope

**Files:**
- Delete: `docs/Store-V1-Scope.md`
- Create: `docs/AppLens-Platform-Scope.md`
- Modify: `docs/ROADMAP.md`
- Modify: `docs/Store-Readiness-Checklist.md`
- Modify: `docs/Store-Listing-Draft.md`

- [x] Remove the legacy Store scope document.
- [x] Create a platform scope document that defines AppLens as the CSI local control board.
- [x] State that Scanner, Tune, Planner, and future modules are separate platform apps.
- [x] Move Store-readiness language into legacy/packaging notes.

### Task 2: Rewrite README Product Envelope

**Files:**
- Modify: `README.md`

- [x] Rewrite the overview around AppLens as a mobile-OS-inspired control board.
- [x] List Scanner, Tune, Blackboard, Planner, and future Fleet/RAG/MCP/Gov modules.
- [x] Keep build/test/run instructions accurate.
- [x] Avoid claiming Store packaging as the governing product promise.

### Task 3: Split Platform Docs

**Files:**
- Create: `docs/AppLens-Scanner.md`
- Create: `docs/AppLens-Tune-App.md`
- Create: `docs/AppLens-Blackboard.md`
- Create: `docs/AppLens-Planner.md`
- Create: `docs/AppLens-Platform-Shell.md`

- [x] Give each app/module a concise responsibility, boundary, current status, and next moves.
- [x] Keep docs aligned with the platform scope and README.

### Task 4: Route Tune Execution Through PlatformLoopService

**Files:**
- Modify: `src/AppLens.Desktop/MainWindow.xaml.cs`
- Test: `tests/AppLens.Backend.Tests/PlatformLoopServiceTests.cs`

- [x] Write a failing backend test proving execution records proposal, approval, and execution events under one correlation.
- [x] Update desktop Tune execution to use `PlatformLoopService` instead of direct `TuneActionExecutor`.
- [x] Keep direct executor available as the low-level action runtime.

### Task 5: Make External Modules Configured Capabilities

**Files:**
- Modify: `src/AppLens.Backend/ModuleManifest.cs`
- Modify: `src/AppLens.Backend/ModuleStatusService.cs`
- Modify: `src/AppLens.Backend/DashboardReadModelService.cs`
- Test: `tests/AppLens.Backend.Tests/ModuleStatusServiceTests.cs`
- Test: `tests/AppLens.Backend.Tests/DashboardReadModelServiceTests.cs`

- [x] Add `NotConfigured` module availability.
- [x] Treat missing optional module roots as not configured, not blocked.
- [x] Ensure not-configured modules do not force the dashboard into action-required state.
- [x] Keep available/blocked states for configured modules with failing required checks.

### Verification

- [x] Run `dotnet test AppLensDesktop.sln --no-restore`.
- [x] Run `dotnet build AppLensDesktop.sln --no-restore`.
- [x] Run `git status --short`.
