# AppLens-Tune

Separate diagnostics and approval-gated action code, outside `AppLensDesktop.sln`.
The core client does not build or start Tune.

- `AppLens-Tune.ps1` / `Run-AppLens-Tune.bat`: Windows script.
- `AppLens-Tune.py` / `Run-AppLens-Tune.sh`: macOS/Linux script.
- `src/AppLens.Tune.csproj`: collectors, rules, plans, execution, and reports.
- `desktop/`: former window source; no active desktop project.
- `preservation-manifest.json`: original relocation paths and hashes.

The native project references the shared backend; the dependency is one-way.
`ITuneActionExecutor` connects Tune execution to the shared approval loop.
Diagnostic scripts perform runtime probes beyond the core inventory scan.

```powershell
dotnet test future/AppLens-Tune/tests/AppLens.Tune.Tests.csproj -c Release
```
