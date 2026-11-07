# Local storage

`AppLensRuntimeStorage` defaults to `%LOCALAPPDATA%/AppLens`.
`FromRoot` supplies an isolated root for tests and tools.

- `history/events.dpapi-v1.jsonl`: desktop removal history, protected with current-user
  Windows DPAPI and purpose `AppLens.RemovalHistory.v1`.
- `ledger/events.jsonl` and `ledger/index.sqlite`: legacy/shared blackboard storage.

`ProtectedRemovalHistoryStore` implements `IBlackboardStore`, reads legacy removal
events, and writes new removal records only to protected history. It validates
existing protected records before appending and does not create a plaintext index.
Unreadable history blocks writes; `RemovalService` persists approval before
starting an uninstaller. Legacy records are not migrated or rewritten.

`BlackboardStore` supports append-only JSONL with a SQLite index. Events carry
schema, participant/module/app, correlation, lifecycle, privacy, payload, artifact,
provenance, and policy fields. Queries filter events by type, module, app,
correlation, and data state. Shared module and Tune code use these contracts.

Preserve serialized identifiers, storage paths, and DPAPI purpose when changing
this code. Reports are readable exports, not copies of encrypted history.
