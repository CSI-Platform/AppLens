# AppLens desktop build

The active WinUI 3 client scans installed apps, disks and basic device information,
presents a sortable table, supports confirmed removal routes, and saves reports
locally. It has no build/runtime dependency on future/AppLens-Tune.

## Build and run
Windows build 19041+ and .NET 10 SDK are required for development. WinUI/MSIX
builds also require compatible Windows SDK tooling. The source project declares
x64 and ARM64; validate each runtime on its corresponding hardware before release.

```powershell
dotnet restore AppLensDesktop.sln
dotnet build AppLensDesktop.sln -c Release
dotnet test AppLensDesktop.sln -c Release --no-build
& .\src\AppLens.Desktop\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\AppLens.Desktop.exe
```

Build the solution, or specify -p:Platform=x64 when building the desktop project
directly. The solution's x64 executable is under
src/AppLens.Desktop/bin/x64/Release/net10.0-windows10.0.19041.0/win-x64/.
A direct project build without Platform can use a different output directory.
Run-AppLensDesktop.ps1 starts the Debug build, creating it only if absent; rebuild
Debug explicitly before using that launcher to review source changes.

## Using the client
Alt+S starts a scan; Alt+D opens Download report after completion. Tab/Shift+Tab
move through controls and row actions. Choose Markdown, JSON or HTML before
downloading. The Windows picker starts in Downloads; choose another folder if
needed. Cancelling keeps the results. Exports include the complete scan even when
filters hide rows, and personal identifiers are redacted by default.

At smaller window sizes or larger Windows text sizes, scroll the table
horizontally and the page vertically to reach all columns and expanded panels.
See [client verification](AppLens-Client-Verification.md) for observed coverage and
the [continuation handoff](AppLens-v1-Handoff.md) for remaining release work.

## Focused verification
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests\Script.Tests\Test-AppLensSnapshot.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests\Script.Tests\Test-StoreConfiguration.ps1
dotnet test future\AppLens-Tune\tests\AppLens.Tune.Tests.csproj -c Release
dotnet run --project tools\AppLens.Smoke\AppLens.Smoke.csproj -c Release
dotnet list AppLensDesktop.sln package --vulnerable --include-transitive
```

The smoke command explicitly runs a read-only scan and writes a new evidence
directory. Normal builds do not scan the workstation. Preserved Tune tests run
separately and are not part of the core solution.

## Development packages and release
Store build settings bundle the .NET runtime and use Store-managed Windows App
SDK framework dependencies. Development builds have separate settings; neither
their size nor this configuration check proves a clean-PC Store installation.
See [release validation](AppLens-Release-Validation.md) for the configuration
regression and remaining hands-on acceptance checks.

[Removal probe instructions](../tools/AppLens.RemovalProbe/README.md) describe
disposable, uniquely identified Windows 11 unsigned test packages. Their install
needs native Windows administrator approval. They neither change certificate
trust nor enable Developer Mode. They are not distribution packages.

The existing Build-StoreCandidate script is for an explicitly authorized
distribution preparation run. It is not proof of WACK/Store certification or a
clean-PC install. See [Store readiness](Store-Readiness-Checklist.md).
