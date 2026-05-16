# Security Policy

## Supported Versions

AppLens is currently in preview. Security and privacy fixes are accepted against the `main` branch.

## Reporting a Vulnerability

Please do not open public issues for sensitive findings that include usernames, machine names, private file paths, client details, logs, or report exports.

Use GitHub private vulnerability reporting when available. If private reporting is not available, open a minimal public issue that says a private security contact is needed and do not include sensitive details.

## Security Boundaries

AppLens Scanner and default diagnostics are local-first observation paths:

- no telemetry
- no account system
- no cloud upload
- no background service
- no automatic remediation
- no admin requirement for observation

AppLens Tune is the action layer. Tune actions must be explicit, approval-gated, narrowly allowlisted, and recorded through the blackboard. Admin-required work must be blocked unless the operator deliberately runs an elevated session.

