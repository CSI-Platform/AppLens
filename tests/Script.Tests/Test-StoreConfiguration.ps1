# Evaluates release settings only. Does not build, package, install or launch an app.
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\..\src\AppLens.Desktop\AppLens.Desktop.csproj'
foreach ($platform in @('x64')) {
    $json = & dotnet msbuild $project '-p:Configuration=Release' "-p:Platform=$platform" '-p:GenerateAppxPackageOnBuild=true' '-getProperty:PlatformTarget,RuntimeIdentifier,SelfContained,WindowsAppSDKSelfContained,WindowsPackageType,AppxPackageSigningEnabled,Platforms,RuntimeIdentifiers,AppxBundlePlatforms,TargetPlatformMinVersion,TargetPlatformVersion'
    if ($LASTEXITCODE -ne 0) { throw "Cannot evaluate $platform Store settings." }
    $properties = (($json -join "`n") | ConvertFrom-Json).Properties
    # Catch a release bundle that advertises an unvalidated architecture or Windows 10.
    if ($properties.Platforms -ne 'x64' -or $properties.RuntimeIdentifiers -ne 'win-x64' -or
        $properties.AppxBundlePlatforms -ne 'x64') {
        throw 'The first Store release must produce only the approved x64 architecture.'
    }
    [xml]$manifest = Get-Content -LiteralPath (Join-Path (Split-Path $project) 'Package.appxmanifest') -Raw
    if ($properties.TargetPlatformMinVersion -ne '10.0.22000.0' -or
        $manifest.Package.Dependencies.TargetDeviceFamily.MinVersion -ne '10.0.22000.0' -or
        [version]$properties.TargetPlatformVersion -lt [version]'10.0.22000.0') {
        throw 'The first Store release must require Windows 11 in both project and manifest.'
    }
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
    Write-Output "PASS: Windows 11 $platform Store settings bundle .NET and use Store-managed Windows App SDK dependencies."
}
