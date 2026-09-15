#Requires -Version 5.1
<#
.SYNOPSIS
    AppLens-Tune Audit - workstation tuning snapshot for CSI engagements
.DESCRIPTION
    Captures a targeted workstation snapshot focused on startup load, background
    services, local AI tooling, storage hotspots, and repo placement. The
    standalone script is audit mode: it does not change the machine and only
    writes a plain-text report. The AppLens Tune app handles approval-gated
    actions through the platform shell and blackboard.
#>

function Get-DesktopFilePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FileName
    )

    $desktopPath = [Environment]::GetFolderPath('Desktop')
    if ([string]::IsNullOrWhiteSpace($desktopPath)) {
        $desktopPath = Join-Path $env:USERPROFILE 'Desktop'
    }

    if (-not (Test-Path -LiteralPath $desktopPath)) {
        New-Item -ItemType Directory -Path $desktopPath -Force | Out-Null
    }

    return Join-Path $desktopPath $FileName
}

function ConvertTo-Lines {
    param(
        [AllowNull()]
        [string]$Text
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return @('(none)')
    }

    return @(
        $Text.TrimEnd() -split '\r?\n' |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
}

function ConvertTo-TableLines {
    param(
        [AllowNull()]
        $Rows
    )

    if (-not $Rows) {
        return @('(none)')
    }

    return ConvertTo-Lines (($Rows | Format-Table -AutoSize | Out-String))
}

function Invoke-TextCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,

        [string[]]$Arguments = @()
    )

    if (-not (Get-Command $Command -ErrorAction SilentlyContinue)) {
        return @("$Command not found")
    }

    try {
        $output = & $Command @Arguments 2>&1 | ForEach-Object { "$_" -replace "`0", '' }
        if (-not $output) {
            return @('(no output)')
        }
        return @($output)
    } catch {
        return @("Unavailable: $($_.Exception.Message)")
    }
}

function Get-StartupApprovalMap {
    $map = @{}
    $approvalPaths = @(
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run',
        'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32'
    )

    foreach ($path in $approvalPaths) {
        if (-not (Test-Path -LiteralPath $path)) {
            continue
        }

        $item = Get-Item -LiteralPath $path -ErrorAction SilentlyContinue
        if (-not $item) {
            continue
        }

        foreach ($valueName in $item.GetValueNames()) {
            $value = $item.GetValue($valueName)
            if (-not ($value -is [byte[]]) -or $value.Length -eq 0) {
                continue
            }

            $status = switch ($value[0]) {
                0x02 { 'Enabled' }
                0x06 { 'Enabled' }
                default { 'Disabled' }
            }

            $map["$path|$valueName"] = [pscustomobject]@{
                Status = $status
                Code   = '0x{0:X2}' -f $value[0]
            }
        }
    }

    return $map
}

function Get-StartupCommandState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [AllowNull()]
        [string]$Location,

        [Parameter(Mandatory = $true)]
        [hashtable]$ApprovalMap
    )

    $candidatePaths = @()

    if ($Location -like 'HKLM*') {
        $candidatePaths += 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'
        $candidatePaths += 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32'
    } else {
        $candidatePaths += 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run'
        $candidatePaths += 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32'
    }

    foreach ($path in $candidatePaths) {
        $key = "$path|$Name"
        if ($ApprovalMap.ContainsKey($key)) {
            return $ApprovalMap[$key].Status
        }
    }

    return 'Unknown'
}

function Get-DirectorySizeBytes {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $sum = (Get-ChildItem -LiteralPath $Path -Force -Recurse -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum).Sum

    if ($null -eq $sum) {
        return [int64]0
    }

    return [int64]$sum
}

function Format-Size {
    param(
        [AllowNull()]
        [double]$Bytes
    )

    if ($null -eq $Bytes) {
        return '(missing)'
    }

    if ($Bytes -ge 1GB) {
        return '{0:N2} GB' -f ($Bytes / 1GB)
    }

    if ($Bytes -ge 1MB) {
        return '{0:N0} MB' -f ($Bytes / 1MB)
    }

    if ($Bytes -ge 1KB) {
        return '{0:N0} KB' -f ($Bytes / 1KB)
    }

    return "$Bytes B"
}

function New-Section {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Title,

        [AllowNull()]
        [string[]]$Lines
    )

    $section = @()
    $section += ''
    $section += $Title
    if (-not $Lines -or $Lines.Count -eq 0) {
        $section += '(none)'
    } else {
        $section += $Lines
    }
    return $section
}

$OutputPath = Get-DesktopFilePath -FileName "AppLens_Tune_Results_$env:COMPUTERNAME.txt"

$computerSystem = Get-CimInstance Win32_ComputerSystem
$operatingSystem = Get-CimInstance Win32_OperatingSystem
$cDrive = Get-PSDrive -Name C -PSProvider FileSystem -ErrorAction SilentlyContinue

$topProcesses = Get-Process |
    Sort-Object WS -Descending |
    Select-Object -First 15 ProcessName, Id,
        @{ Name = 'WS_GB'; Expression = { [math]::Round($_.WS / 1GB, 2) } },
        @{ Name = 'CPU_s'; Expression = { if ($_.CPU) { [math]::Round($_.CPU, 1) } else { 0 } } }

$startupApprovalMap = Get-StartupApprovalMap

$startupCommands = Get-CimInstance Win32_StartupCommand |
    Sort-Object Name |
    ForEach-Object {
        $command = $_.Command
        $shortCommand = if ([string]::IsNullOrWhiteSpace($command)) {
            ''
        } elseif ($command.Length -gt 92) {
            $command.Substring(0, 92) + '...'
        } else {
            $command
        }

        [pscustomobject]@{
            Name     = $_.Name
            State    = Get-StartupCommandState -Name $_.Name -Location $_.Location -ApprovalMap $startupApprovalMap
            Location = $_.Location
            Command  = $shortCommand
        }
    }

$serviceNames = @(
    'WSAIFabricSvc',
    'com.docker.service',
    'vmcompute',
    'HvHost',
    'OneDrive',
    'Ollama',
    'AsusAppService',
    'ASUSOptimization',
    'ASUSSoftwareManager',
    'ASUSSwitch',
    'GlideXService',
    'StoryCubeService'
)

$interestingServices = Get-Service -ErrorAction SilentlyContinue |
    Where-Object { $serviceNames -contains $_.Name -or $serviceNames -contains $_.DisplayName } |
    Sort-Object Name |
    Select-Object Name, DisplayName, Status, StartType

$hotspotTargets = @(
    [pscustomobject]@{ Label = '.ollama'; Path = Join-Path $env:USERPROFILE '.ollama' },
    [pscustomobject]@{ Label = '.codex'; Path = Join-Path $env:USERPROFILE '.codex' },
    [pscustomobject]@{ Label = '.claude'; Path = Join-Path $env:USERPROFILE '.claude' },
    [pscustomobject]@{ Label = 'LocalAppData\Temp'; Path = Join-Path $env:LOCALAPPDATA 'Temp' },
    [pscustomobject]@{ Label = 'LocalAppData\Docker'; Path = Join-Path $env:LOCALAPPDATA 'Docker' },
    [pscustomobject]@{ Label = 'LocalAppData\Packages'; Path = Join-Path $env:LOCALAPPDATA 'Packages' },
    [pscustomobject]@{ Label = 'LocalAppData\pip\Cache'; Path = Join-Path $env:LOCALAPPDATA 'pip\Cache' },
    [pscustomobject]@{ Label = 'Roaming\npm-cache'; Path = Join-Path $env:APPDATA 'npm-cache' }
)

$storageHotspots = foreach ($target in $hotspotTargets) {
    if (-not (Test-Path -LiteralPath $target.Path)) {
        continue
    }

    $bytes = Get-DirectorySizeBytes -Path $target.Path
    [pscustomobject]@{
        Location = $target.Label
        Bytes    = $bytes
        Size     = Format-Size $bytes
        Path     = $target.Path
    }
}

$storageHotspots = $storageHotspots | Sort-Object Bytes -Descending

$repoRoots = @(
    (Join-Path $env:USERPROFILE 'OneDrive\Documents'),
    (Join-Path $env:USERPROFILE 'source'),
    (Join-Path $env:USERPROFILE 'Projects'),
    'C:\Dev'
)

$repoPlacement = foreach ($root in $repoRoots) {
    if (-not (Test-Path -LiteralPath $root)) {
        continue
    }

    $repoCount = 0
    $sample = ''

    if (Test-Path -LiteralPath (Join-Path $root '.git')) {
        $repoCount = 1
        $sample = $root
    } else {
        $repos = Get-ChildItem -Path $root -Directory -Recurse -Force -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -eq '.git' } |
            Select-Object -First 5

        $repoCount = @($repos).Count
        if ($repoCount -gt 0) {
            $sample = Split-Path $repos[0].FullName -Parent
        }
    }

    [pscustomobject]@{
        Root     = $root
        RepoCount = $repoCount
        Sample   = $sample
    }
}

$wslStatus = Invoke-TextCommand -Command 'wsl.exe' -Arguments @('--status')
$wslList = Invoke-TextCommand -Command 'wsl.exe' -Arguments @('-l', '-v')
$dockerSummary = Invoke-TextCommand -Command 'docker' -Arguments @('system', 'df')
$ollamaSummary = Invoke-TextCommand -Command 'ollama' -Arguments @('list')

$asusProcesses = Get-Process -ErrorAction SilentlyContinue |
    Where-Object { $_.ProcessName -match 'Asus|ASUS' }

$oneDriveProcess = Get-Process -Name OneDrive -ErrorAction SilentlyContinue | Select-Object -First 1
$ollamaBytes = Get-DirectorySizeBytes -Path (Join-Path $env:USERPROFILE '.ollama')
$tempBytes = Get-DirectorySizeBytes -Path (Join-Path $env:LOCALAPPDATA 'Temp')
$oneDriveRepos = $repoPlacement | Where-Object { $_.Root -like '*OneDrive*' -and $_.RepoCount -gt 0 }
$browserAutolaunch = $startupCommands | Where-Object { $_.Name -match 'ChromeAutoLaunch|EdgeAutoLaunch' -and $_.State -eq 'Enabled' }
$dockerStartup = $startupCommands | Where-Object { $_.Name -eq 'Docker Desktop' -and $_.State -eq 'Enabled' }
$disabledStartupTargets = $startupCommands | Where-Object {
    $_.Name -in @(
        'Docker Desktop',
        'GoogleChromeAutoLaunch_73B6C066306F5C60640DD5CAA62D7F8C',
        'MicrosoftEdgeAutoLaunch_5DEF50B543B1C6FAE52926502D2A426A'
    ) -and $_.State -eq 'Disabled'
}
$wsaifabric = $interestingServices | Where-Object { $_.Name -eq 'WSAIFabricSvc' } | Select-Object -First 1
$dockerService = $interestingServices | Where-Object { $_.Name -eq 'com.docker.service' } | Select-Object -First 1

$stableFindings = @()
if ($wsaifabric -and $wsaifabric.StartType -eq 'Disabled' -and $wsaifabric.Status -eq 'Stopped') {
    $stableFindings += '- WSAIFabricSvc is still disabled and stopped.'
}
if ($dockerService -and $dockerService.StartType -eq 'Manual' -and $dockerService.Status -eq 'Stopped') {
    $stableFindings += '- Docker Desktop service is manual and idle.'
}
if ($interestingServices | Where-Object { $_.Name -eq 'GlideXService' -and $_.StartType -eq 'Manual' }) {
    $stableFindings += '- GlideX remains manual.'
}
if ($interestingServices | Where-Object { $_.Name -eq 'StoryCubeService' -and $_.StartType -eq 'Manual' }) {
    $stableFindings += '- StoryCube remains manual.'
}
if (-not ($topProcesses | Where-Object { $_.ProcessName -eq 'WorkloadsSessionHost' })) {
    $stableFindings += '- WorkloadsSessionHost is not present in the top working set snapshot.'
}
if (@($disabledStartupTargets).Count -eq 3) {
    $stableFindings += '- Docker Desktop, Chrome, and Edge login-startup entries are disabled.'
}

$reviewItems = @()
if ($browserAutolaunch) {
    $names = ($browserAutolaunch | Select-Object -ExpandProperty Name) -join ', '
    $reviewItems += "- Browser auto-launch entries are still enabled: $names."
}
if ($dockerStartup) {
    $reviewItems += '- Docker Desktop still has a login startup entry.'
}
if ($asusProcesses.Count -ge 8) {
    $reviewItems += "- ASUS background load is still high: $($asusProcesses.Count) live ASUS processes."
}
if ($oneDriveProcess) {
    $reviewItems += "- OneDrive is active at roughly $([math]::Round($oneDriveProcess.WS / 1MB)) MB working set."
}
if ($oneDriveRepos) {
    $reviewItems += '- Git repos were detected inside a OneDrive-synced root.'
}

$optionalImprovements = @()
if ($ollamaBytes -and $ollamaBytes -ge 15GB) {
    $optionalImprovements += "- Ollama models currently use $(Format-Size $ollamaBytes). Review whether both local models still need to stay resident."
}
if ($tempBytes -and $tempBytes -ge 2GB) {
    $optionalImprovements += "- Local temp storage is $(Format-Size $tempBytes). Clearing rebuildable temp/cache data could reclaim space."
}
if ($cDrive -and $cDrive.Free -lt 100GB) {
    $optionalImprovements += "- C: free space is down to $(Format-Size $cDrive.Free)."
}

$summaryLines = @(
    "Machine: $($computerSystem.Manufacturer) $($computerSystem.Model)",
    "OS: $($operatingSystem.Caption) ($($operatingSystem.Version))",
    "RAM: $('{0:N1} GB' -f ($computerSystem.TotalPhysicalMemory / 1GB))",
    "C: Free: $(Format-Size $cDrive.Free)"
)

$output = @()
$output += '=== AppLens-Tune Audit Results ==='
$output += "Computer: $env:COMPUTERNAME"
$output += "User: $env:USERNAME"
$output += "Scan Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$output += 'Mode: Audit (read-only)'
$output += ''
$output += $summaryLines
$output += New-Section -Title '--- Stability Checks ---' -Lines $stableFindings
$output += New-Section -Title '--- Review Items ---' -Lines $reviewItems
$output += New-Section -Title '--- Optional Improvements ---' -Lines $optionalImprovements
$output += New-Section -Title '--- Top Memory Processes ---' -Lines (ConvertTo-TableLines $topProcesses)
$output += New-Section -Title '--- Startup Commands ---' -Lines (ConvertTo-TableLines $startupCommands)
$output += New-Section -Title '--- Key Services ---' -Lines (ConvertTo-TableLines $interestingServices)
$output += New-Section -Title '--- Storage Hotspots ---' -Lines (ConvertTo-TableLines ($storageHotspots | Select-Object Location, Size, Path))
$output += New-Section -Title '--- Repo Placement ---' -Lines (ConvertTo-TableLines $repoPlacement)
$output += New-Section -Title '--- WSL Status ---' -Lines $wslStatus
$output += New-Section -Title '--- WSL Distros ---' -Lines $wslList
$output += New-Section -Title '--- Docker Summary ---' -Lines $dockerSummary
$output += New-Section -Title '--- Ollama Summary ---' -Lines $ollamaSummary

$outputText = $output -join "`r`n"
$outputText | Out-File -FilePath $OutputPath -Encoding UTF8

Write-Host ''
Write-Host 'AppLens-Tune - Workstation Audit' -ForegroundColor Cyan
Write-Host 'Collecting an audit-mode workstation snapshot...' -ForegroundColor Gray
Write-Host ''
Write-Host $outputText
Write-Host ''
Write-Host '============================================' -ForegroundColor Green
Write-Host '  Audit complete!' -ForegroundColor Green
Write-Host '  Results saved to:' -ForegroundColor Green
Write-Host "  $OutputPath" -ForegroundColor Yellow
Write-Host '============================================' -ForegroundColor Green
Write-Host ''

if ($env:APPLENS_INTERACTIVE -eq '1') {
    Write-Host 'Press any key to close...' -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
}
