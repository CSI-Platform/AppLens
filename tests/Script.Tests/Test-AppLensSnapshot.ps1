#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
$scanPath = Join-Path $PSScriptRoot '..\..\AppLens.ps1'
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($scanPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors -join "`n") }
# Load the real functions without running a scan or writing to the Desktop.
foreach ($definition in $ast.FindAll({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
    . ([scriptblock]::Create($definition.Extent.Text))
}
foreach ($name in @('Get-WorkstationSnapshot', 'Format-WorkstationSnapshot', 'Save-ScanReport')) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) { throw "FAIL: initial scan is missing $name" }
}

function Assert-Match($Text, $Pattern, $Message) {
    if ($Text -notmatch $Pattern) { throw "FAIL: $Message" }
}

$snapshot = @{
    Win32_ComputerSystem = [pscustomobject]@{ Manufacturer = 'Example'; Model = 'Workstation' }
    Win32_OperatingSystem = [pscustomobject]@{
        Caption = 'Windows Test'; Version = '10.0'; BuildNumber = '123'; OSArchitecture = '64-bit'
        TotalVisibleMemorySize = 33554432; FreePhysicalMemory = 4194304
        LastBootUpTime = (Get-Date).AddHours(-25)
    }
    Win32_Processor = @([pscustomobject]@{ Name = 'Test CPU'; NumberOfCores = 8; NumberOfLogicalProcessors = 16 })
    Win32_VideoController = @([pscustomobject]@{ Name = 'Test GPU'; DriverVersion = '1.2' })
    Win32_LogicalDisk = @(
        [pscustomobject]@{ DeviceID = 'C:'; FileSystem = 'NTFS'; Size = 107374182400; FreeSpace = 5368709120 }
        [pscustomobject]@{ DeviceID = 'D:'; FileSystem = 'NTFS'; Size = 214748364800; FreeSpace = 107374182400 }
    )
    Processes = @(
        [pscustomobject]@{ ProcessName = 'small'; Id = 1; WorkingSet64 = 104857600 }
        [pscustomobject]@{ ProcessName = 'large'; Id = 2; WorkingSet64 = 1073741824 }
    )
    Issues = @()
}
$report = (Format-WorkstationSnapshot -Snapshot $snapshot) -join "`n"
Assert-Match $report 'RAM:.*28\.0.*32\.0.*87\.5%' 'RAM must convert KiB correctly and calculate used percentage.'
Assert-Match $report 'C:.*100\.0.*95\.0.*5\.0.*95\.0%.*5\.0%' 'Disk report must include total, used, free and both percentages.'
Assert-Match $report 'D:.*200\.0.*100\.0.*100\.0.*50\.0%' 'Every fixed disk must be reported independently.'
Assert-Match $report 'Low disk space.*C:' 'Low disk space must appear in the attention summary.'
Assert-Match $report 'High RAM usage' 'High RAM usage must appear in the attention summary.'
if ($report -match 'Top Memory Processes|Working set MiB|large|small') {
    throw 'FAIL: the initial scan must not present a process memory ranking, including legacy process data.'
}
Write-Host 'PASS: capacity arithmetic, attention flags, multiple drives, and no process ranking'

$snapshot.Win32_OperatingSystem.FreePhysicalMemory = $null
$snapshot.Win32_LogicalDisk = @([pscustomobject]@{ DeviceID = 'E:'; FileSystem = ''; Size = 0; FreeSpace = $null })
$report = (Format-WorkstationSnapshot -Snapshot $snapshot) -join "`n"
Assert-Match $report 'RAM: Unavailable' 'Missing memory readings must not be treated as zero free RAM.'
Assert-Match $report 'E:.*Unavailable' 'Unknown disk capacity must not divide by zero or claim a full disk.'
if ($report -match 'Low disk space|High RAM usage') { throw 'FAIL: unavailable readings generated a utilization alert.' }
Write-Host 'PASS: unavailable readings remain unknown'

$snapshot.Win32_LogicalDisk = @(
    [pscustomobject]@{ DeviceID = 'C:'; FileSystem = 'NTFS'; Size = 107374182400; FreeSpace = 0 }
    [pscustomobject]@{ DeviceID = 'D:'; FileSystem = 'NTFS'; Size = 107374182400; FreeSpace = $null }
)
$report = (Format-WorkstationSnapshot -Snapshot $snapshot) -join "`n"
Assert-Match $report 'C:.*100\.0.*100\.0.*0\.0.*100\.0%.*0\.0%' 'A full disk must be reported as 100% used.'
Assert-Match $report 'Low disk space.*C:' 'Zero free space must still trigger the low-disk flag.'
Assert-Match $report 'D:.*Unavailable' 'Missing free space on a sized disk must remain unknown.'
Write-Host 'PASS: zero free space and missing free space remain distinct'

$script:processProbeCalled = $false
$partial = & {
    function Get-CimInstance {
        [CmdletBinding()]
        param($ClassName, $Property, $Filter, $OperationTimeoutSec)
        if ($ClassName -eq 'Win32_OperatingSystem') { throw 'Provider unavailable' }
        if ($ClassName -eq 'Win32_ComputerSystem') { return [pscustomobject]@{ Manufacturer = 'Example'; Model = 'Survived' } }
        return @()
    }
    function Get-Process { [CmdletBinding()] param() $script:processProbeCalled = $true; throw 'Unexpected process probe' }
    Get-WorkstationSnapshot
}
$report = (Format-WorkstationSnapshot -Snapshot $partial) -join "`n"
Assert-Match $report 'Example Survived' 'A failed probe must not discard successful evidence.'
Assert-Match $report 'Win32_OperatingSystem.*Unavailable' 'CIM probe failures must be visible in the report.'
if ($script:processProbeCalled) { throw 'FAIL: the initial scan must not enumerate running processes.' }
Write-Host 'PASS: failed probes preserve partial evidence'

$testDirectory = Join-Path $PSScriptRoot ('..\..\artifacts\snapshot-tests-' + [guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $testDirectory
$testReport = Join-Path $testDirectory 'existing.txt'
Save-ScanReport -Path $testReport -Text 'Original evidence'
$rejected = $false
try { Save-ScanReport -Path $testReport -Text 'Replacement' } catch { $rejected = $true }
if (-not $rejected -or (Get-Content -LiteralPath $testReport -Raw).Trim() -ne 'Original evidence') {
    throw 'FAIL: writing a report must never overwrite existing evidence.'
}
Write-Host 'PASS: existing evidence cannot be overwritten'
Write-Host 'All snapshot checks passed.'
