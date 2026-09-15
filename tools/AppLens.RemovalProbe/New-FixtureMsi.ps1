param(
    [Parameter(Mandatory)][string]$Path,
    [Parameter(Mandatory)][guid]$RunId,
    [Parameter(Mandatory)][guid]$ProductCode,
    [switch]$AllUsersFixture
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $Path) { throw 'Refusing to overwrite an MSI fixture.' }
$installer = New-Object -ComObject WindowsInstaller.Installer
$database = $installer.OpenDatabase([IO.Path]::GetFullPath($Path), 3)
function Invoke-MsiSql([string]$sql) {
    $sql=$sql.Replace('{',[string][char]96).Replace('}',[string][char]96)
    $view = $database.OpenView($sql)
    try { $view.Execute() } finally { $view.Close() }
}
function Add-MsiRow([string]$table, [object[]]$values) {
    $record = $installer.CreateRecord($values.Count)
    for ($index = 0; $index -lt $values.Count; $index++) {
        if ($null -eq $values[$index]) { continue }
        if ($values[$index] -is [int]) { $record.IntegerData($index + 1) = $values[$index] }
        else { $record.StringData($index + 1) = [string]$values[$index] }
    }
    $view = $database.OpenView("SELECT * FROM $table")
    try { $view.Execute(); $view.Modify(1, $record) } finally { $view.Close() }
}
Invoke-MsiSql 'CREATE TABLE {Property} ({Property} CHAR(72) NOT NULL, {Value} CHAR(0) NOT NULL PRIMARY KEY {Property})'
Invoke-MsiSql 'CREATE TABLE {Directory} ({Directory} CHAR(72) NOT NULL, {Directory_Parent} CHAR(72), {DefaultDir} CHAR(255) NOT NULL PRIMARY KEY {Directory})'
Invoke-MsiSql 'CREATE TABLE {Component} ({Component} CHAR(72) NOT NULL, {ComponentId} CHAR(38), {Directory_} CHAR(72) NOT NULL, {Attributes} SHORT NOT NULL, {Condition} CHAR(255), {KeyPath} CHAR(72) PRIMARY KEY {Component})'
Invoke-MsiSql 'CREATE TABLE {Feature} ({Feature} CHAR(38) NOT NULL, {Feature_Parent} CHAR(38), {Title} CHAR(64), {Description} CHAR(255), {Display} SHORT, {Level} SHORT NOT NULL, {Directory_} CHAR(72), {Attributes} SHORT NOT NULL PRIMARY KEY {Feature})'
Invoke-MsiSql 'CREATE TABLE {FeatureComponents} ({Feature_} CHAR(38) NOT NULL, {Component_} CHAR(72) NOT NULL PRIMARY KEY {Feature_}, {Component_})'
Invoke-MsiSql 'CREATE TABLE {Registry} ({Registry} CHAR(72) NOT NULL, {Root} SHORT NOT NULL, {Key} CHAR(255) NOT NULL, {Name} CHAR(255), {Value} CHAR(0), {Component_} CHAR(72) NOT NULL PRIMARY KEY {Registry})'
Invoke-MsiSql 'CREATE TABLE {InstallExecuteSequence} ({Action} CHAR(72) NOT NULL, {Condition} CHAR(255), {Sequence} SHORT PRIMARY KEY {Action})'
Invoke-MsiSql 'CREATE TABLE {Media} ({DiskId} SHORT NOT NULL, {LastSequence} LONG NOT NULL, {DiskPrompt} CHAR(64), {Cabinet} CHAR(255), {VolumeLabel} CHAR(32), {Source} CHAR(72) PRIMARY KEY {DiskId})'
Invoke-MsiSql 'CREATE TABLE {File} ({File} CHAR(72) NOT NULL, {Component_} CHAR(72) NOT NULL, {FileName} CHAR(255) NOT NULL, {FileSize} LONG NOT NULL, {Version} CHAR(72), {Language} CHAR(20), {Attributes} SHORT, {Sequence} LONG NOT NULL PRIMARY KEY {File})'
Add-MsiRow 'Media' @(1,0,$null,$null,$null,$null)
$properties = @{
    ProductCode=$ProductCode.ToString('B').ToUpperInvariant(); ProductName='AppLens disposable MSI fixture'
    ProductVersion='1.0.0'; ProductLanguage='1033'; Manufacturer='CSI development tests'
    UpgradeCode=[guid]::NewGuid().ToString('B').ToUpperInvariant(); INSTALLLEVEL='1'; ARPNOMODIFY='1'
}
foreach ($entry in $properties.GetEnumerator()) { Add-MsiRow 'Property' @($entry.Key,$entry.Value) }
if ($AllUsersFixture) { Add-MsiRow 'Property' @('ALLUSERS','1') }
Add-MsiRow 'Directory' @('TARGETDIR',$null,'SourceDir')
Add-MsiRow 'Component' @('ProbeMarker',[guid]::NewGuid().ToString('B').ToUpperInvariant(),'TARGETDIR',4,$null,'Marker')
Add-MsiRow 'Feature' @('ProbeFeature',$null,'Disposable fixture',$null,1,1,$null,0)
Add-MsiRow 'FeatureComponents' @('ProbeFeature','ProbeMarker')
$registryRoot = if ($AllUsersFixture) { 2 } else { 1 }
Add-MsiRow 'Registry' @('Marker',$registryRoot,('Software\CSI\AppLens\Disposable\' + $RunId.ToString('N')),'Marker',$RunId.ToString('N'),'ProbeMarker')
$sequence = [ordered]@{
    CostInitialize=800; FileCost=900; CostFinalize=1000; InstallValidate=1400
    InstallInitialize=1500; ProcessComponents=1600; UnpublishFeatures=1800
    RemoveRegistryValues=2600; WriteRegistryValues=5000; RegisterUser=6000
    RegisterProduct=6100; PublishFeatures=6300; PublishProduct=6400; InstallFinalize=6600
}
foreach ($entry in $sequence.GetEnumerator()) { Add-MsiRow 'InstallExecuteSequence' @($entry.Key,$null,[int]$entry.Value) }
$summary=$database.SummaryInformation(10)
$summary.Property(2)='AppLens disposable MSI fixture'
$summary.Property(3)='Development test only'
$summary.Property(7)='Intel;1033'
$summary.Property(9)=[guid]::NewGuid().ToString('B').ToUpperInvariant()
$summary.Property(14)=200
$summary.Property(15)=0
$summary.Persist()
$database.Commit()
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($summary) | Out-Null
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($database) | Out-Null
[Runtime.InteropServices.Marshal]::FinalReleaseComObject($installer) | Out-Null
Write-Output ([IO.Path]::GetFullPath($Path))
