# Evaluates release settings only. Does not build, package, install or launch an app.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\..\src\AppLens.Desktop\AppLens.Desktop.csproj'
foreach ($platform in @('x64', 'ARM64')) {
    $json = & dotnet msbuild $project '-p:Configuration=Release' "-p:Platform=$platform" '-p:GenerateAppxPackageOnBuild=true' '-getProperty:PlatformTarget,RuntimeIdentifier,SelfContained,WindowsAppSDKSelfContained,WindowsPackageType,AppxPackageSigningEnabled'
    if ($LASTEXITCODE -ne 0) { throw "Cannot evaluate $platform Store settings." }
    $properties = (($json -join "`n") | ConvertFrom-Json).Properties
    if ($properties.PlatformTarget -ine $platform -or $properties.RuntimeIdentifier -ine "win-$platform") {
        throw "$platform Store build targets the wrong CPU/runtime."
    }
    if ($properties.SelfContained -ne 'true') {
        throw "$platform Store build requires a separately installed .NET runtime."
    }
    if ($properties.WindowsPackageType -ne 'MSIX' -or $properties.AppxPackageSigningEnabled -ne 'false') {
        throw "$platform Store build is not configured for an unsigned MSIX submission candidate."
    }
    # Store-managed Windows App SDK frameworks are separate from the bundled .NET runtime.
    if ($properties.WindowsAppSDKSelfContained -eq 'true') {
        throw "$platform Store build unexpectedly embeds the Windows App SDK framework."
    }
    Write-Output "PASS: $platform Store settings bundle .NET and use Store-managed Windows App SDK dependencies."
}
