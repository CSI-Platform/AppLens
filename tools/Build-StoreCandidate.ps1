$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$solution = Join-Path $repoRoot 'AppLensDesktop.sln'
$desktopProject = Join-Path $repoRoot 'src\AppLens.Desktop\AppLens.Desktop.csproj'
$candidateRoot = Join-Path $repoRoot ('artifacts\store-candidate-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fffffff'))
if(Test-Path -LiteralPath $candidateRoot) { throw 'Refusing to reuse a package output directory.' }
$installDir = Join-Path $candidateRoot 'install'
$packageDir = Join-Path $candidateRoot 'msix'
$programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
$vswhere = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
$symbolArguments = @()
if(Test-Path -LiteralPath $vswhere) {
    $symbolTool = & $vswhere -latest -products '*' -find 'VC\Tools\MSVC\**\bin\Hostx64\x64\mspdbcmf.exe' | Select-Object -First 1
    if($symbolTool -and (Test-Path -LiteralPath $symbolTool)) {
        $symbolArguments = @("-p:MsPdbCmfExeFullpath=$symbolTool")
    }
}

Push-Location $repoRoot
try {
    dotnet restore $solution
    if($LASTEXITCODE -ne 0) { throw 'Restore failed; no package generated.' }
    dotnet test $solution --configuration Release --no-restore --logger trx --results-directory (Join-Path $candidateRoot 'test-results')
    if($LASTEXITCODE -ne 0) { throw 'Tests failed; no package generated.' }
    & (Join-Path $repoRoot 'tests\Script.Tests\Test-StoreConfiguration.ps1')
    dotnet publish $desktopProject -c Release -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false "-p:AppxPackageDir=$packageDir\" "-p:AppxPackageTestDir=$installDir\" @symbolArguments
    if($LASTEXITCODE -ne 0) { throw 'Store candidate generation failed.' }

    Write-Host ''
    Write-Host 'Generated package artifacts:'
    Get-ChildItem -LiteralPath $installDir -Filter '*.msix*' | Sort-Object LastWriteTime -Descending | Select-Object Name, Length, LastWriteTime

    $appCertPath = (Get-Command appcert.exe -ErrorAction SilentlyContinue).Source
    if (!$appCertPath) {
        $defaultAppCert = Join-Path $programFilesX86 'Windows Kits\10\App Certification Kit\appcert.exe'
        if(Test-Path -LiteralPath $defaultAppCert) { $appCertPath = $defaultAppCert }
    }
    if (!$appCertPath) {
        Write-Warning 'Windows App Certification Kit appcert.exe was not found. Install Windows SDK / WACK before final Store submission.'
    }
    else {
        Write-Host "Windows App Certification Kit found: $appCertPath (not run)"
    }
}
finally {
    Pop-Location
}
