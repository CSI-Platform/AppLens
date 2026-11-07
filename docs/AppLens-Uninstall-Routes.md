# Uninstall routes

The desktop client uses `runFullTrust` at medium integrity. It does not elevate
itself. Removal requires confirmation of the exact installation and a durable
approval record before execution.

| Target | Mechanism | Boundary |
| --- | --- | --- |
| MSI | System32 `msiexec.exe /x` with a validated product GUID, interactive UI, and `/norestart` | Revalidate registration and installation context; administrator handoff launches Windows Installer. |
| Vendor app | Validated absolute local EXE path and registered arguments | No shell interpretation, PATH search, script hosts, arbitrary DLL execution, or inferred switches. |
| Current-user MSIX | `PackageManager.RemovePackageAsync` with exact PackageFullName | Excludes frameworks/resources/system packages and AppLens; no all-user removal or cancellation once started. |
| Unsupported/restricted | Explain evidence and offer Windows Installed Apps where appropriate | Preserve NoRemove, management policy, protected components, and UAC. |

HKLM registration alone does not prove machine scope or an administrator requirement.
MSI scope uses `MsiEnumProductsEx`; unavailable or ambiguous installation contexts
hand off to Windows rather than selecting an arbitrary instance.

MSI exit 1602 and UAC error 1223 represent cancellation/denial. Exit 3010 means a
restart is required, not permission to restart. A process exit or Settings handoff
does not establish removal; verification checks the installation identity.
Disk changes are timestamped observations and can include unrelated activity.

Implementation: `RemovalAssessment`, `RemovalCommand`, `RemovalService`,
`WindowsRemovalPlatform`, and `RemovalTracking` in the backend.
[Fixture probe](../tools/AppLens.RemovalProbe/README.md) exercises disposable targets.
