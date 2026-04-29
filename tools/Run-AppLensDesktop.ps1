$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$exePath = Join-Path $repoRoot 'src\AppLens.Desktop\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\AppLens.Desktop.exe'

if (-not (Test-Path -LiteralPath $exePath)) {
    dotnet build (Join-Path $repoRoot 'AppLensDesktop.sln')
}

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "AppLens-desktop executable was not found at $exePath"
}

Start-Process -FilePath $exePath
Write-Host "Started AppLens-desktop: $exePath"
