$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$solution = Join-Path $repoRoot 'AppLensDesktop.sln'
$desktopProject = Join-Path $repoRoot 'src\AppLens.Desktop\AppLens.Desktop.csproj'
$candidateRoot = Join-Path $repoRoot ('artifacts\store-candidate-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fffffff'))
if(Test-Path -LiteralPath $candidateRoot) { throw 'Refusing to reuse a package output directory.' }
$installDir = Join-Path $candidateRoot 'install'
$packageDir = Join-Path $candidateRoot 'msix'

Push-Location $repoRoot
try {
    dotnet restore $solution
    if($LASTEXITCODE -ne 0) { throw 'Restore failed; no package generated.' }
    dotnet test $solution --configuration Release --no-restore
    if($LASTEXITCODE -ne 0) { throw 'Tests failed; no package generated.' }
    & (Join-Path $repoRoot 'tests\Script.Tests\Test-StoreConfiguration.ps1')
    dotnet publish $desktopProject -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false "-p:AppxPackageDir=$packageDir\" "-p:AppxPackageTestDir=$installDir\"
    if($LASTEXITCODE -ne 0) { throw 'Store candidate generation failed.' }

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
