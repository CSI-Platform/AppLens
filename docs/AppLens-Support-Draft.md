# AppLens support — publication draft

AppLens v1 is a local Windows app for inventory, storage review, supported removal
actions and report download. It is not a background optimizer.

## Using AppLens
1. Select Scan this PC.
2. Review storage and the app table. Search or filter the rows as needed.
3. Review the exact app and scope before confirming an uninstall. Complete any
   Windows or vendor approval dialog.
4. Check action history and refresh results. A Settings handoff or process exit
   alone is not proof of removal.
5. Select Download report and choose Markdown, JSON or HTML. The complete capture
   is included even when table filters are active.

Unknown size means the installer did not report a usable value. It does not mean
zero space. Observed disk changes can include other activity.

If a scan is partial, inspect the coverage details in the client/report and retry.
If Windows denies removal, contact the device's administrator. AppLens does not
override management or protected-component restrictions. Do not manually delete
application directories as a substitute for the supported uninstaller.

Before sending a report for help, review it for private information. Leave
personal identifiers disabled unless they are specifically needed.

## Removing AppLens
Close AppLens, open Windows Settings → Apps → Installed apps, find AppLens, and
choose Uninstall. AppLens does not uninstall itself from its running table.
Reports you saved elsewhere are separate files; remove them yourself if no
longer needed. Final-package local-history retention must be confirmed before
publishing detailed retention/removal promises.

## Action history unavailable
New history is protected for the Windows user who recorded it. Use the original
Windows profile when reopening AppLens. If the history is damaged or the profile
is unavailable, keep the file and any profile backup for recovery; copying only
the history file to a new account may not restore access. Do not delete or edit
history to bypass an error. AppLens must save approval before it starts an
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
