# AppLens-Tune — deferred, preserved

This is the existing AppLens-Tune implementation, set aside while AppLens v1
delivers fast inventory, storage review, uninstall, and report download.
Do not reconnect these diagnostics to core launch or scan.

## Contents and entry points

- `AppLens-Tune.ps1` and `Run-AppLens-Tune.bat`: Windows audit script.
- `AppLens-Tune.py` and `Run-AppLens-Tune.sh`: macOS/Linux audit script.
- `src/AppLens.Tune.csproj`: native diagnostics, rules, readiness, Tune plan,
  approval-gated executor, `TuneAuditService`, and `TuneReportWriter`.
- `tests/AppLens.Tune.Tests.csproj`: preserved native tests; run explicitly:
  `dotnet test future/AppLens-Tune/tests/AppLens.Tune.Tests.csproj -c Release`.
- `desktop/`: former dashboard window source. It is preserved as reference;
  there is no active Tune desktop project. Reintegrating it requires a deliberate
  separate desktop project and review after v1, not copying it into the core UI.
- `docs/`: original product/LLM/thesis material. Treat its roadmap as deferred.

## Shared dependencies

The native project references the normal core backend; core never references this
project. Machine collection, formatting/redaction, approval-loop interfaces, ledger,
module infrastructure, and existing serialized evidence contracts remain shared.
Historical `AuditSnapshot`/Tune-shaped evidence models stay in the backend for
existing platform/ledger compatibility. V1 uses `InventorySnapshot` and exports no
Tune fields. `ITuneActionExecutor` breaks the old concrete executor dependency.
Service-control implementation and its package dependency are confined here.

## Preservation and verification

`preservation-manifest.json` records source/destination paths and SHA-256 before
relocation. Files were hash-checked immediately after moving. Subsequent edits are
limited to renaming the legacy audit/report classes, adapting references/tests,
implementing the shared executor interface, and delegating machine reads to the
shared collector. Diagnostic/optimization logic was not redesigned.

The separated native project currently passes all 58 preserved tests. This does
not prove every real Tune action or OS probe works. The scripts and old dashboard
have not been live-validated after separation. An earlier Windows script audit
stalled; its cause remains unidentified. Do not run it as a v1 readiness check.

Resume only after v1: read the preserved docs, run the explicit tests, investigate
the stalled script independently, and establish a dedicated UI entry point.
