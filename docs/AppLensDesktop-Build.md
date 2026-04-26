# AppLens-desktop Build Notes

AppLens-desktop is the Microsoft Store-oriented Windows app for AppLens. It is a packaged WinUI 3 / Windows App SDK desktop app with a native C# backend.

## Requirements

- Windows 10/11 build 19041 or later.
- .NET SDK 10.
- Visual Studio 2022 with Windows App SDK / MSIX packaging tooling for Store packaging workflows.

The backend and tests can build with the .NET SDK alone. Store packaging may require Visual Studio or Windows SDK tooling on the build machine.

## Build

```powershell
dotnet restore AppLensDesktop.sln
dotnet build AppLensDesktop.sln
dotnet test tests\AppLens.Backend.Tests\AppLens.Backend.Tests.csproj
```

## Package Smoke Build

```powershell
dotnet publish src\AppLens.Desktop\AppLens.Desktop.csproj `
  -c Release `
  -p:GenerateAppxPackageOnBuild=true `
  -p:AppxPackageSigningEnabled=false
```

The project is configured for MSIX tooling, Windows 10/11 19041+, and app version `0.1.0.0`. Store submission should use Partner Center-managed signing for the final package.

## V1 Guardrails

- Read-only scan only.
- No admin elevation.
- No remediation.
- No telemetry or cloud upload.
- Exports are user-triggered.
- Baseline scan does not shell out to PowerShell.
