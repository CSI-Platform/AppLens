# Scanner and reports

The desktop client uses `AuditService`, `MachineCollector`, and `InventoryCollector`.
`InventorySnapshot` combines device readings, full app inventory, coverage, action
history, and optional client context. **Run diagnostics** adds local diagnostic
readings and findings to the inventory. Windows desktop JSON and the Linux CLI
use schema 3.0. The `Mode` field identifies the scan.
Unavailable readings remain unknown; installed software does not establish access,
licensing, or workload performance.

`PreparedIntakeReport` freezes review/export content. `ReportWriter` emits Markdown,
JSON, and HTML; filters affect the table only. Default redaction protects known
identifier tokens and profile paths. Client notes are unverified, escaped text.
`ReportShareService` hands a streamed attachment to Windows sharing.

## Standalone scripts

- Windows: `Run-AppLens.bat` or `powershell -File AppLens.ps1`.
  Captures hardware/OS, uptime, RAM, fixed disks, and categorized apps.
  Disk free below 10% and RAM use at least 85% produce snapshot flags.
- Linux: `sh Run-AppLens.sh` or `python3 AppLens.py`. Prints redacted inventory
  JSON. Add `--diagnostics` for local diagnostics and `--output PATH` to save it.
- macOS: `sh Run-AppLens.sh` or `python3 AppLens.py`. Captures app and developer-tool inventory.

Windows writes `AppLens_Results_<ComputerName>_<Timestamp>.txt` to Desktop.
`-OutputPath` selects another path; existing files are rejected.
On macOS, Python writes `AppLens_Results_<ComputerName>.txt`, replacing it on rerun.
It uses `APPLENS_OUTPUT_DIR` or Desktop, falling back to the home directory if
the selected directory is absent. Windows and macOS text scripts retain local
identifiers.
