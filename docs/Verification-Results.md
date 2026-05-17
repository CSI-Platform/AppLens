# Verification Results

Last verified: 2026-05-17

## Commands

```powershell
dotnet restore AppLensDesktop.sln
dotnet build AppLensDesktop.sln --no-restore
dotnet test AppLensDesktop.sln --no-restore --no-build
dotnet build AppLensDesktop.sln --configuration Release --no-restore
dotnet test AppLensDesktop.sln --configuration Release --no-restore
git diff --check
.\tools\Build-StoreCandidate.ps1
```

## Results

- Debug solution build passed with backend, desktop, backend tests, and desktop presentation tests included.
- Debug no-build solution tests passed: backend 90 passed, desktop 8 passed.
- Release solution tests passed locally: backend 90 passed, desktop 8 passed.
- `git diff --check` passed.
- `Build-StoreCandidate.ps1` passed. It runs restore, Release solution tests, unsigned MSIX package generation, artifact listing, and Windows App Certification Kit detection.
- Latest unsigned package smoke output: `artifacts\install\AppLens.Desktop_0.1.0.0_x64_ARM64.msixbundle`.

## Known Local Tooling Gap

The package build emitted a warning that `mspdbcmf.exe` was not found, so a symbols package was not generated. Windows App Certification Kit `appcert.exe` is also still a submission prerequisite. Install Visual Studio / Windows SDK app certification tooling before final Store certification testing.

## CI Alignment

GitHub Actions now uses the same solution-level test command:

```powershell
dotnet test AppLensDesktop.sln --configuration Release --no-restore
```

`AppLensDesktop.slnx` includes both `AppLens.Backend.Tests` and `AppLens.Desktop.Tests` so lightweight solution consumers do not accidentally exclude desktop presentation coverage.
