[CmdletBinding()]
param([switch]$DevelopmentClient)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$runId = [guid]::NewGuid().ToString('N')
$outputRoot = Join-Path $repoRoot ('artifacts\removal-preflight-' + $runId)
if (Test-Path -LiteralPath $outputRoot) { throw 'Evidence directory already exists.' }
New-Item -ItemType Directory -Path $outputRoot | Out-Null
$fixtureDirectory = Join-Path $outputRoot 'fixtures'
New-Item -ItemType Directory -Path $fixtureDirectory | Out-Null
$plan = [ordered]@{
    RunId=$runId; CallerName=('CSI.AppLens.RemovalProbe.'+$runId.Substring(0,16))
    FixtureName=('CSI.AppLens.Disposable.'+$runId.Substring(0,16)); FixtureDirectory=$fixtureDirectory
    ProductCode=[guid]::NewGuid().ToString('B').ToUpperInvariant()
}
if ($DevelopmentClient) { $plan.CallerName = 'CSI.AppLens.ClientTest.' + $runId.Substring(0,16) }
[IO.File]::WriteAllText((Join-Path $fixtureDirectory ($runId+'.fixture')), $runId)
& (Join-Path $PSScriptRoot 'New-FixtureMsi.ps1') -Path (Join-Path $fixtureDirectory 'fixture.msi') -RunId $runId -ProductCode $plan.ProductCode
$payload = Join-Path $outputRoot 'payload'
dotnet publish (Join-Path $PSScriptRoot 'AppLens.RemovalProbe.csproj') -c Release -r win-x64 --self-contained true -o $payload --verbosity quiet
if ($LASTEXITCODE -ne 0) { throw 'Probe publish failed.' }
$clientPayload = Join-Path $outputRoot 'client-payload'
if ($DevelopmentClient) {
    dotnet publish (Join-Path $repoRoot 'src\AppLens.Desktop\AppLens.Desktop.csproj') -c Release -p:Platform=x64 -r win-x64 --self-contained true -p:WindowsAppSDKSelfContained=true -o $clientPayload --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'Development client publish failed.' }
}
$makeAppx = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe'
if (!(Test-Path -LiteralPath $makeAppx)) { throw 'Windows SDK makeappx was not found.' }
$packages = @()
foreach ($kind in @('Caller','Fixture')) {
    $name = if ($kind -eq 'Caller') { $plan.CallerName } else { $plan.FixtureName }
    $publisher = if ($kind -eq 'Caller') { 'CN=AppLens Development Probe' } else { 'CN=AppLens Disposable Vendor' }
    $publisher += ', OID.2.25.311729368913984317654407730594956997722=1'
    $packageFolder = Join-Path $outputRoot $kind
    New-Item -ItemType Directory -Path $packageFolder | Out-Null
    $sourcePayload = if ($DevelopmentClient -and $kind -eq 'Caller') { $clientPayload } else { $payload }
    $executable = if ($DevelopmentClient -and $kind -eq 'Caller') { 'AppLens.Desktop.exe' } else { 'AppLens.RemovalProbe.exe' }
    foreach($item in Get-ChildItem -LiteralPath $sourcePayload) { Copy-Item -LiteralPath $item.FullName -Destination $packageFolder -Recurse }
    $assets = Join-Path $packageFolder 'Assets'
    if (!(Test-Path -LiteralPath $assets)) { New-Item -ItemType Directory -Path $assets | Out-Null }
    foreach($asset in @('AppLensStore.png','AppLensSquare44.scale-200.png','AppLensSquare150.scale-200.png')) {
        Copy-Item -LiteralPath (Join-Path $repoRoot ('src\AppLens.Desktop\Assets\'+$asset)) -Destination $assets
    }
    $plan | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $packageFolder 'probe-plan.json') -Encoding UTF8
    $manifest = @"
<?xml version="1.0" encoding="utf-8"?>
<Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
 xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
 xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
 xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
 IgnorableNamespaces="uap uap10 rescap">
 <Identity Name="$name" Publisher="$publisher" Version="0.0.0.1" ProcessorArchitecture="x64"/>
 <Properties><DisplayName>AppLens $kind test fixture</DisplayName><PublisherDisplayName>CSI development tests</PublisherDisplayName><Logo>Assets\AppLensStore.png</Logo></Properties>
 <Dependencies><TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.22000.0" MaxVersionTested="10.0.26100.0"/></Dependencies>
 <Resources><Resource Language="en-us"/></Resources>
 <Applications><Application Id="App" Executable="$executable" EntryPoint="Windows.FullTrustApplication"
   uap10:RuntimeBehavior="packagedClassicApp" uap10:TrustLevel="mediumIL">
  <uap:VisualElements DisplayName="AppLens $kind test fixture" Description="Disposable development test only"
   Square150x150Logo="Assets\AppLensSquare150.scale-200.png" Square44x44Logo="Assets\AppLensSquare44.scale-200.png" BackgroundColor="#E7E9EC"/>
 </Application></Applications>
 <Capabilities><rescap:Capability Name="runFullTrust"/></Capabilities>
</Package>
"@
    [IO.File]::WriteAllText((Join-Path $packageFolder 'AppxManifest.xml'),$manifest,[Text.UTF8Encoding]::new($false))
    if ($DevelopmentClient -and $kind -eq 'Caller') {
        # Unpackaged publish emits AppLens.Desktop.pri. An installed package needs
        # resources.pri with its own identity, including the compiled XAML.
        $priConfig = Join-Path $outputRoot 'client-priconfig.xml'
        @'
<?xml version="1.0" encoding="utf-8"?>
<resources targetOsVersion="10.0.0" majorVersion="1">
 <index root="\" startIndexAt="AppLens.Desktop.pri">
  <default><qualifier name="Language" value="en-US"/></default>
  <indexer-config type="PRI" />
 </index>
</resources>
'@ | Set-Content -LiteralPath $priConfig -Encoding UTF8
        $makePri = Join-Path (Split-Path -Parent $makeAppx) 'makepri.exe'
        & $makePri new /pr $packageFolder /cf $priConfig /in $name /of (Join-Path $packageFolder 'resources.pri') | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Packaged client resource generation failed.' }
        & (Join-Path $repoRoot 'tests\Script.Tests\Test-DevelopmentClientResources.ps1') -PackageDirectory $packageFolder
    }
    $packagePath=Join-Path $outputRoot ($kind+'.msix')
    $packLog=Join-Path $outputRoot ("makeappx-"+$kind+".log")
    & $makeAppx pack /d $packageFolder /p $packagePath | Tee-Object -FilePath $packLog | Out-Null
    if ($LASTEXITCODE -ne 0) { Get-Content -LiteralPath $packLog -Tail 8; throw "MSIX generation failed: $kind" }
    $packages += [pscustomobject]@{Name=$name;Path=$packagePath;SHA256=(Get-FileHash -LiteralPath $packagePath).Hash}
}
$installation = [ordered]@{ RunId=$runId; FixtureDirectory=$fixtureDirectory; CallerName=$plan.CallerName; FixtureName=$plan.FixtureName; ProductCode=$plan.ProductCode; Packages=$packages }
$installation | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $outputRoot 'installation-plan.json') -Encoding UTF8
Write-Output (Join-Path $outputRoot 'installation-plan.json')
