# Packaged uninstall preflight

Development-only probe for COP-238/240. Not part of the AppLens build or Store package.

1. Run Build-DevelopmentProbe.ps1. It creates a unique evidence folder, a
   marker-only per-user MSI, and two unsigned Windows 11 MSIX test packages.
2. Review installation-plan.json and package hashes. Install-DevelopmentProbe.ps1
   installs only those generated packages; Windows requires administrator approval.
   It does not enable Developer Mode or add a signing certificate.
3. Launch the caller through its package application identity as the ordinary user.
   It removes the different-publisher disposable MSIX, exercises a generated vendor
   uninstall registration, and installs/removes its generated MSI. A second MSI
   cycle explicitly tests Windows UAC handoff. Approve or deny that native prompt
   to capture the relevant result.
4. Read fixtures/results.json and probe-complete.txt. A process launch or zero exit
   code alone is not removal verification. The MSI test covers a per-user fixture,
   not organization policy or all-user MSI installations.
5. Remove only the generated caller/remaining fixture registrations afterward.
   Preserve evidence and note any denied approval or incomplete cleanup.

The probe accepts no application identities from exported inventories. The MSI
writes only its run-specific HKCU registry marker. Existing apps are not targets.
Unsigned test identities use Microsoft's dedicated OID and cannot impersonate
the future signed Store identity.

For the complete client test, pass -DevelopmentClient to Build-DevelopmentProbe.ps1.
The caller package then contains the actual WinUI client under a unique ClientTest
identity; its UI does not automatically run the console probe. Install the two
hashed packages only after native Windows approval, launch the caller normally,
and explicitly select the disposable Store fixture in its app table. Other apps
are not test targets. The generated MSI can be installed separately for a UI test;
New-FixtureMsi.ps1 -AllUsersFixture creates a distinct machine fixture requiring
administrator approval. Confirm fixture ProductCode/ownership before each test.

Never repeat a cancelled Windows approval automatically. Keep the prepared plan
and resume when the operator is ready. Development packages include their runtimes
for isolation; their size is not the final Store download or installed footprint.

The development-client wrapper must generate resources.pri for its unique package
identity; copying the unpackaged AppLens.Desktop.pri alone crashes at startup.
The builder runs Test-DevelopmentClientResources.ps1 before packing to verify the
identity and compiled XAML. This static check supplements an installed launch test.
After removal tests, relaunch and export again to verify persisted outcomes.
For independently generated MSI metadata, read its hash after the generating
PowerShell process exits so Windows Installer COM has released the file.

Microsoft references:
- [Unsigned development packages](https://learn.microsoft.com/windows/msix/package/unsigned-package)
- [Current-user removal API](https://learn.microsoft.com/uwp/api/windows.management.deployment.packagemanager.removepackageasync)
- [Package resource index naming and identity](https://learn.microsoft.com/windows/uwp/app-resources/resource-management-system)
