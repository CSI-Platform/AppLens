# Installed data handling

Installed scans PC specifications and installed applications locally. It collects
OS/device details, CPU/GPU names, memory/disk readings, app identities and locations,
computer name, and Windows username. Optional workflow notes are user-supplied.
Running diagnostics also collects local process and startup names, system status,
and hardware and runtime readings.
There are no accounts, telemetry, automatic uploads, or background monitoring.

Reports contain the full capture, notes, and action history, including rows hidden
by filters. Known computer/user identifiers and profile paths are redacted by
default, with explicit raw-detail opt-in. App names, diagnostic readings, and
notes may still be sensitive.

Download report saves a readable file to the chosen location. Send report reviews
a frozen file before handing it to Windows sharing; the user selects the app and
recipient. Installed does not verify delivery. It supplies a stream without an
extra export file; Windows or the sharing app may cache the content.

Uninstalls require confirmation and record the target, mechanism, approval, outcome,
and verification. New history uses Windows current-user DPAPI protection under
the AppLens local-data directory, without a plaintext index. Recovery depends on
the original Windows profile. Software running as that user can read the history.
Unreadable history prevents actions that require a durable approval record.

Legacy JSONL/SQLite records are read without rewriting or deleting them; they
retain their original plaintext format. Exported files are separate, readable,
and not encrypted by AppLens. App-data retention depends on Windows handling;
no automatic deletion of history or exported reports is assumed.
