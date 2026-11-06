# Installed support — publication draft

Installed v1 is a local Windows app that prepares PC specifications and installed
tools for a workflow consultation. Supported removal and history features are
also available. It is not a background optimizer.

## Using Installed
1. Open Installed. A read-only scan starts automatically; Scan this PC refreshes it.
2. Review your PC summary and installed tools. Optionally add your workflow goal,
   online services and constraints in the panel below the app table.
3. Select Review report. Inspect the summary and expand Complete report if needed.
4. Select Send report, review it, then choose a sharing app and recipient in
   Windows. Finish sending in that app; Installed cannot confirm email delivery.
5. If your email app is unavailable in Windows sharing, select Download report
   and attach the file manually. Markdown, JSON and HTML all contain the complete
   capture even when table filters hide rows.

For an uninstall, review the exact app and scope before confirming. Complete any
Windows or vendor approval, then inspect action history and refreshed results.
A Settings handoff or process exit alone is not proof of removal.

Unknown size means the installer did not report a usable value. It does not mean
zero space. Observed disk changes can include other activity.

If a scan is partial, inspect the coverage details in the client/report and retry.
If Windows denies removal, contact the device's administrator. Installed does not
override management or protected-component restrictions. Do not manually delete
application directories as a substitute for the supported uninstaller.

Before sending a report for help, review it for private information. Leave
personal identifiers disabled unless they are specifically needed.

## Removing Installed
Close Installed, open Windows Settings → Apps → Installed apps, find Installed, and
choose Uninstall. Installed does not uninstall itself from its running table.
Reports you saved elsewhere are separate files; remove them yourself if no
longer needed. Final-package local-history retention must be confirmed before
publishing detailed retention/removal promises.

## Action history unavailable
New history is protected for the Windows user who recorded it. Use the original
Windows profile when reopening Installed. If the history is damaged or the profile
is unavailable, keep the file and any profile backup for recovery; copying only
the history file to a new account may not restore access. Do not delete or edit
history to bypass an error. Installed must save approval before it starts an
uninstaller. Scans and report export remain available, with missing history
reported in coverage.

Pre-release development logs and indexes are preserved in their old plaintext
format; the new client reads them without rewriting them. New history is separate
and encrypted. User-saved reports remain readable files with redaction enabled
by default; preserve anything you need before removing local data.

## Publication requirements
The owner must provide and verify a real support contact and hosted URL before
Store submission. Include the app version, Windows version, relevant error and
the steps that led to it; do not send passwords or other secrets.
