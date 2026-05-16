# AppLens-desktop Build Notes

AppLens-desktop is the Windows platform shell for AppLens. It is a packaged WinUI 3 / Windows App SDK desktop app with a native C# backend, local Scanner/Tune collectors, module status, blackboard records, and export surfaces.

## Requirements

- Windows 10/11 build 19041 or later.
- .NET SDK 10.
- Visual Studio 2022 with Windows App SDK / MSIX packaging tooling for Store packaging workflows.

The backend and tests can build with the .NET SDK alone. Store packaging may require Visual Studio or Windows SDK tooling on the build machine.

## Build

```powershell
dotnet restore AppLensDesktop.sln
dotnet build AppLensDesktop.sln
dotnet test AppLensDesktop.sln
```

## Package Smoke Build

```powershell
dotnet publish src\AppLens.Desktop\AppLens.Desktop.csproj `
  -c Release `
  -p:GenerateAppxPackageOnBuild=true `
  -p:AppxPackageSigningEnabled=false
```

The project is configured for MSIX tooling, Windows 10/11 19041+, and app version `0.1.0.0`. Store submission should use Partner Center-managed signing for the final package.

## Packaging Guardrails

- Local-first evidence collection.
- No telemetry or cloud upload by default.
- Exports are user-triggered.
- System-changing Tune actions must be explicit, approved, and blackboard-recorded.
- Admin-required actions must stay visibly separate from standard-user actions.
- The baseline scan should not shell out to PowerShell.
- No telemetry or cloud upload.
- Exports are user-triggered.
- Baseline scan does not shell out to PowerShell.
