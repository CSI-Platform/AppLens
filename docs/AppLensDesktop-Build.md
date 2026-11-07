# Desktop build

Target: Windows 11 x64, minimum build 22000. Requires .NET 10 SDK and compatible
Windows SDK/WinUI build tooling. Native XAML compilation requires Windows.

```powershell
dotnet restore AppLensDesktop.sln
dotnet build AppLensDesktop.sln -c Release
dotnet test AppLensDesktop.sln -c Release --no-build
& .\src\AppLens.Desktop\bin\x64\Release\net10.0-windows10.0.22000.0\win-x64\AppLens.Desktop.exe
```

Direct desktop-project builds use `-p:Platform=x64`.
`tools/Run-AppLensDesktop.ps1` starts the Debug executable, building only if absent;
rebuild Debug before using it to inspect changed source.

Additional commands, from the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests\Script.Tests\Test-AppLensSnapshot.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests\Script.Tests\Test-StoreConfiguration.ps1
dotnet run --project tools\AppLens.Smoke\AppLens.Smoke.csproj -c Release
.\tools\Build-StoreCandidate.ps1
```

The smoke command scans the local machine and writes evidence; ordinary builds do
not scan. The packaging script writes unsigned candidates to a new `artifacts/`
directory. Package settings bundle .NET and use Windows App SDK framework
dependencies. Identity is defined in [Package.appxmanifest](../src/AppLens.Desktop/Package.appxmanifest);
an unsigned package is not a normal end-user installer.

[Removal probe](../tools/AppLens.RemovalProbe/README.md) uses disposable test
packages. [Tune](../future/AppLens-Tune/README.md) builds and tests separately.
