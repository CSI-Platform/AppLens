# AppLens

AppLens is a Windows 11 x64 PC inventory client, displayed as **Installed**.
A client opens it, reviews specifications and installed tools, and shares a report
that a consultant can use to design workflows.

Opening the app starts a read-only scan. Optional goals, services, and constraints
are recorded as client statements. Review and share use a frozen report;
Markdown, JSON, and HTML exports include the full inventory regardless of filters.
Reports redact known identifiers by default. Windows sharing leaves app and
recipient selection to the user. Confirmed uninstall actions and history are
also available.

## Development

Requires Windows, .NET 10 SDK, and compatible Windows SDK/WinUI build tooling.

```powershell
dotnet restore AppLensDesktop.sln
dotnet build AppLensDesktop.sln -c Release
dotnet test AppLensDesktop.sln -c Release --no-build
& .\src\AppLens.Desktop\bin\x64\Release\net10.0-windows10.0.22000.0\win-x64\AppLens.Desktop.exe
```

## Code and references

- `src/AppLens.Backend/`: collection, reports, removal, and local history.
- `src/AppLens.Desktop/`: WinUI client and Windows sharing.
- `tests/`: backend, presentation, and PowerShell checks.
- [Build commands](docs/AppLensDesktop-Build.md), [scanner and scripts](docs/AppLens-Scanner.md),
  [storage](docs/AppLens-Blackboard.md), [removal routes](docs/AppLens-Uninstall-Routes.md).
- [AppLens-Tune](future/AppLens-Tune/README.md): separate diagnostics and action code outside the core solution.
- [Privacy](PRIVACY.md), [security](SECURITY.md), [support](SUPPORT.md).
