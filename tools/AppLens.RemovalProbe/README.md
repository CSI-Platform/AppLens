# Removal fixture probe

Development tool outside the core build/package. It targets generated disposable
fixtures, not existing user applications.

1. Run `tools/Build-DevelopmentProbe.ps1` to create a unique evidence folder,
   marker-only per-user MSI, and two unsigned Windows 11 test packages.
2. Inspect `installation-plan.json` and hashes. `Install-DevelopmentProbe.ps1`
   installs those packages with native Windows administrator approval; it does
   not enable Developer Mode or change certificate trust.
3. Launch the caller under its package identity as the ordinary user. It exercises
   a different-publisher MSIX, generated vendor registration, and generated MSI.
   An additional MSI cycle tests explicit administrator handoff.
4. Read `fixtures/results.json` and `probe-complete.txt`. Verify target absence;
   process launch or exit code alone does not establish removal.
5. Remove only the generated caller and fixture registrations; retain evidence.

`-DevelopmentClient` builds the actual WinUI client under a unique ClientTest
identity instead of automatically running the console probe. Select only owned
fixtures in its UI. `New-FixtureMsi.ps1 -AllUsersFixture` creates a distinct machine
fixture; verify its ProductCode and ownership before testing.

Do not repeat cancelled Windows approval automatically. These unsigned identities
are test packages. They include runtimes for isolation.

The client wrapper generates `resources.pri` for its package identity;
`Test-DevelopmentClientResources.ps1` checks identity and compiled XAML before
packing. Copying the unpackaged PRI alone is insufficient. Read generated MSI
hashes after the generating PowerShell process exits.
