# AppLens privacy policy — publication draft

AppLens collects information locally when you start a scan. This includes installed
app names, versions, publishers, installation identities and locations, reported
sizes, removal information, local disk capacity and usage, Windows/device details,
processor and graphics names, memory usage, uptime, computer name and Windows username.

AppLens v1 does not collect top-process rankings or run the deferred AppLens-Tune
diagnostics. It does not include accounts, telemetry, automatic uploads, cloud
analysis, background monitoring, or automatic report sharing.

Uninstall actions require your confirmation. Windows or the app vendor may request
additional approval. AppLens records the selected installation, mechanism,
timestamps, outcome, verification and observed disk readings in local action
history under the current user's AppLens application data. Closing AppLens before
completion can leave an unknown outcome; review it in Windows and run a fresh scan.
Uninstalling an app can delete that app's data. AppLens does not provide an undo
facility for third-party uninstalls.

Reports are saved only when you choose a local destination. They contain the full
captured inventory and recorded actions, including entries hidden by display
filters. Computer/user identifiers and profile paths are redacted by default.
You can explicitly include personal identifiers. Redaction is not a guarantee
that a report contains no sensitive information; review it before sharing.
AppLens does not send the report to a consultant or AI service. Any sharing you
perform afterward is your choice and is subject to the recipient's practices.

Local history and saved reports remain on your device until removed by you or by
Windows' applicable app-data handling. Exported files are separate from the app.
Windows, Microsoft Store and third-party uninstallers have their own policies.

AppLens stores action history as a local JSON Lines log and SQLite index. AppLens
does not itself encrypt those files or exported reports; protection depends on
Windows access permissions and any device/storage encryption you use. Default
report redaction applies to exported content, not the underlying action history.
Other software or people with access to those files may be able to read them.

Before publication, the owner must add the verified publisher/legal identity,
contact channel, effective date, and any applicable jurisdiction-specific text.
This draft has not been published and is not a substitute for legal review.
The owner must also resolve the storage-protection and final-package retention
questions recorded in the [submission preparation packet](AppLens-Store-Submission-Plan.md)
before approving this draft for publication. Do not describe storage as encrypted
or promise automatic history deletion on uninstall without supporting evidence.
