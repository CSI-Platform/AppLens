# Verification Results

Last verified: 2026-04-28

## Commands

```powershell
dotnet build AppLensDesktop.sln
dotnet test tests\AppLens.Backend.Tests\AppLens.Backend.Tests.csproj
dotnet publish src\AppLens.Desktop\AppLens.Desktop.csproj -c Release -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false
.\tools\Build-StoreCandidate.ps1
```

## Results

- Solution build passed.
- Backend tests passed: 13 passed, 0 failed.
- MSIX package build passed.
- Generated unsigned package outputs under `artifacts\install`.
- Latest bundle smoke output: `AppLens.Desktop_0.1.0.0_x64_ARM64.msixbundle`.

## Known Local Tooling Gap

The package build emitted a warning that `mspdbcmf.exe` was not found, so a symbols package was not generated. This machine also does not appear to have Windows App Certification Kit `appcert.exe` installed. Install Visual Studio / Windows SDK app certification tooling before final Store certification testing.
