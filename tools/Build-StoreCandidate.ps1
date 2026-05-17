$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$solution = Join-Path $repoRoot 'AppLensDesktop.sln'
$desktopProject = Join-Path $repoRoot 'src\AppLens.Desktop\AppLens.Desktop.csproj'
$installDir = Join-Path $repoRoot 'artifacts\install'

Push-Location $repoRoot
try {
    dotnet restore $solution
    dotnet test $solution --configuration Release --no-restore
    dotnet publish $desktopProject -c Release -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false

    Write-Host ''
    Write-Host 'Generated package artifacts:'
    Get-ChildItem -LiteralPath $installDir -Filter '*.msix*' | Sort-Object LastWriteTime -Descending | Select-Object Name, Length, LastWriteTime

    $appCert = Get-Command appcert.exe -ErrorAction SilentlyContinue
    if ($null -eq $appCert) {
        Write-Warning 'Windows App Certification Kit appcert.exe was not found. Install Windows SDK / WACK before final Store submission.'
    }
    else {
        Write-Host "Windows App Certification Kit found: $($appCert.Source)"
    }
}
finally {
    Pop-Location
}
