# Installed privacy policy — publication draft

Installed collects information locally when you start a scan. This includes installed
app names, versions, publishers, installation identities and locations, reported
sizes, removal information, local disk capacity and usage, Windows/device details,
processor and graphics names, memory usage, uptime, computer name and Windows username.

Installed v1 does not collect top-process rankings or run the deferred AppLens-Tune
diagnostics. It does not include accounts, telemetry, automatic uploads, cloud
analysis, background monitoring, or automatic report sharing.

Uninstall actions require your confirmation. Windows or the app vendor may request
additional approval. Installed records the selected installation, mechanism,
timestamps, outcome, verification and observed disk readings in local action
history under the current user's local application data (the internal AppLens folder name is retained). Closing Installed before
completion can leave an unknown outcome; review it in Windows and run a fresh scan.
Uninstalling an app can delete that app's data. Installed does not provide an undo
facility for third-party uninstalls.

Reports are saved only when you choose a local destination. They contain the full
captured inventory and recorded actions, including entries hidden by display
filters. Computer/user identifiers and profile paths are redacted by default.
You can explicitly include personal identifiers. Redaction is not a guarantee
that a report contains no sensitive information; review it before sharing.
Installed does not send the report to a consultant or AI service. Any sharing you
perform afterward is your choice and is subject to the recipient's practices.

Local history and saved reports remain on your device until removed by you or by
Windows' applicable app-data handling. Exported files are separate from the app.
Windows, Microsoft Store and third-party uninstallers have their own policies.

Installed encrypts new action-history records using Windows data protection tied
to your current Windows user. It does not create a separate plaintext history
index. Recovery depends on the Windows profile that protected the records; copying
the file to another account or losing that profile may make it unreadable. This
protection does not prevent software running as your Windows user from reading
the history. If history cannot be read or safely extended, Installed reports the
problem and cannot start a new uninstall that requires recording approval.

Reports you explicitly save are ordinary readable files, not encrypted by
AppLens. Default report redaction applies to exported content, not to the
underlying history while it is being processed. Review reports before sharing
and use appropriate Windows access permissions and device protection.

If you used a pre-release development build, its older JSON Lines history and
SQLite index remain unchanged and unencrypted. The new client can read that
history but does not automatically migrate, rewrite or delete it. New records
are written separately with Windows protection. Existing reports also remain
unchanged.

Before publication, the owner must add the verified publisher/legal identity,
contact channel, effective date, and any applicable jurisdiction-specific text.
This draft has not been published and is not a substitute for legal review.
The owner delegated the privacy design and local release preparation on 2026-09-16;
the protection described above is implemented and locally tested. Complete the
final installed-profile and retention checks in the
[submission preparation packet](AppLens-Store-Submission-Plan.md) before publication.
Do not promise automatic history deletion on uninstall without supporting evidence.
