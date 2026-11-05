# AppLens v1 uninstall routes

The core remains a packaged WinUI 3 desktop application at medium integrity with
`runFullTrust`. It does not elevate itself or add `packageManagement` or
`allowElevation`. A language rewrite would not change Windows permissions.

## Mechanisms

| Target | Mechanism | Boundary |
| --- | --- | --- |
| MSI | Windows System32 msiexec.exe /x with a validated product GUID, interactive UI and /norestart | Revalidate registry identity and product registration; preserve Windows approval. Explicit administrator handoff launches Windows Installer, not an elevated AppLens. |
| Vendor app | Registered, validated absolute local EXE path and arguments | No shell interpretation, PATH search, script hosts, arbitrary DLL execution, quiet-string substitution, or inferred switches. Ambiguous/missing paths use Windows Installed Apps. |
| Current-user MSIX | PackageManager.RemovePackageAsync(exact PackageFullName) | Revalidate current-user identity and exclude frameworks/resources/system packages and AppLens itself. No all-user removal. Cannot cancel once started; Windows can close the target app and remove its local data. |
| Unsupported/restricted | Explain the evidence; offer Windows Installed Apps where appropriate | A Settings handoff is not a successful uninstall. Never override NoRemove, management policy, protected components, or UAC. |

Registry machine scope alone does not establish an administrator requirement.
MSI registrations can appear under HKLM even for a current-user installation.
Use MsiEnumProductsEx with the current-user/machine contexts to resolve MSI scope;
ambiguous or unavailable contexts hand off to Windows. Multiple contexts for one
product GUID are not safe to select through a bare msiexec /x GUID command.
Show “May require administrator approval” unless stronger evidence exists.
NoRemove is installer restriction evidence, not proof of organization management.
MSI exit 1602 and UAC error 1223 are cancellation/denial; 3010 means restart
required, not permission to restart. Verify installation identity after execution;
a process exit alone does not prove removal. Record observed disk change with
capture times; other activity and shared data prevent exact attribution.

## Development evidence, 2026-09-13

Generated, disposable fixtures only. Windows 11 Home build 26200; ordinary
unelevated caller (`Administrator=false`). Both test MSIX packages declared only
runFullTrust and used different publishers. Windows approved installation of the
unsigned development packages; no certificate trust or Developer Mode change.

Evidence directory: `artifacts/removal-preflight-203d4975e60841d4a10834aeb0e0ee35/`.
The installation plan records package hashes and exact identities. Results are in
`fixtures/results.json`; setup and MSI logs preserve observed outcomes.

- Current-user different-publisher MSIX: API completed, exact package absent.
- Vendor EXE child: exit 0, disposable registration absent.
- Per-user MSI: installation state 5, removal exit 0, state -1 afterward.
- Explicit runas handoff to Windows Installer: exit 0, MSI state -1 afterward.
- Separate unpackaged fixture validation: install/remove exit 0, marker absent.

This establishes route feasibility, not arbitrary vendor compatibility, managed
device permission, all-user MSI behavior, denied-UAC behavior, Windows 10
compatibility, or Store approval. Exercise these cases in task 5/6 and distinguish
live evidence from simulations. The test package is not a distribution artifact.

## Microsoft references checked

- [Package removal requirements and current-user behavior](https://learn.microsoft.com/en-us/uwp/api/windows.management.deployment.packagemanager.removepackageasync?view=winrt-26100)
- [Capabilities, including runFullTrust and allowElevation](https://learn.microsoft.com/windows/apps/package-and-deploy/app-capability-declarations)
- [ShellExecute runas and Windows approval](https://learn.microsoft.com/windows/win32/shell/launch)
- [Windows Installer command line](https://learn.microsoft.com/windows/win32/msi/command-line-options)
- [Microsoft Store policies](https://learn.microsoft.com/windows/apps/publish/store-policies)
- [Unsigned Windows 11 development packages](https://learn.microsoft.com/windows/msix/package/unsigned-package)

Store review must accurately describe removal, data loss, administrator approval,
local reports and their privacy controls, and justify runFullTrust. Local API
success does not grant certification or permission to bypass restrictions.
