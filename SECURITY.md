# Security

Report sensitive findings through GitHub private vulnerability reporting when
available. Otherwise request a private contact in an issue without including
reports, credentials, usernames, machine names, or private paths.

Scans are local and read-only. Reports are redacted by default and shared only
after user review. Removal uses confirmed Windows/vendor mechanisms and preserves
UAC and policy restrictions. Action approvals are recorded before execution.

The desktop client uses Windows current-user protection for new history.
Legacy records and user-exported reports have different protection; see
[data handling](PRIVACY.md). AppLens-Tune is separate from the core client and
requires explicit approval for system-changing actions.
