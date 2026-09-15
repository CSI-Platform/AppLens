#Requires -Version 5.1
<#
.SYNOPSIS
    AppLens — Pre-Audit App Scanner for CSI AI Workflow Audits
.DESCRIPTION
    Captures hardware, RAM usage, local disk utilization,
    installed desktop apps (Win32), and Microsoft Store apps without admin rights.
    Read-only: writes a new local text report without changing system settings.
#>

[CmdletBinding()]
param([string]$OutputPath)

# ── Configuration ──────────────────────────────────────────────────────────────

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

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Get-DesktopFilePath -FileName "AppLens_Results_$($env:COMPUTERNAME)_$(Get-Date -Format 'yyyyMMdd-HHmmss-fff').txt"
}
if (Test-Path -LiteralPath $OutputPath) {
    throw 'The report path already exists. Choose a new OutputPath to preserve previous evidence.'
}

# Patterns for apps that should be filtered into the "Runtimes & Frameworks" section
$RuntimePatterns = @(
    '\.NET'
    'Microsoft \.NET'
    'Visual C\+\+'
    'Microsoft Visual C\+\+'
    'Java\(TM\)'
    'Java SE'
    'Oracle Java'
    'Python \d'
    'Node\.js'
    'PowerShell \d'
    'Windows Software Development Kit'
    'Windows SDK'
    'MSVC'
)

# Patterns for entries to exclude entirely (system junk)
$ExcludePatterns = @(
    '^Update for '
    '^Security Update'
    '^Hotfix for'
    '^Service Pack'
    'KB\d{6,}'
    '^\{[0-9A-F-]+\}$'
    '^Windows Driver'
    'Driver Update'
    'NVIDIA Graphics Driver'
    'NVIDIA GeForce'
    'NVIDIA FrameView'
    'NVIDIA HD Audio'
    'NVIDIA PhysX'
    'NVIDIA Update'
    'NVIDIA nView'
    'NVIDIA Install Application'
    'AMD Software'
    'AMD Chipset'
    'Realtek '
    'AI ?Framework ?Service'
    'AI ?Image ?Agent'
    'GlideX Service'
    'StoryCube Service'
    'ProArt Creator Hub Service'
    'ASUS .*(Service|Toolkit|Control Panel)'
    'Python Launcher'
    'Intel\(R\) (Chipset|Management|Network|Graphics|Serial|USB|Rapid|Optane|Wireless|Wi-Fi|Bluetooth|HID|Trusted)'
    'Broadcom '
    'Synaptics '
    'Conexant '
    'Qualcomm '
    'Microsoft Update Health Tools'
    'Update for Windows'
    'Microsoft Security Client'
    'Windows Malicious Software Removal Tool'
)

# Store app name prefixes to exclude (system/framework packages)
$ExcludeStorePatterns = @(
    'Microsoft\.VCLibs'
    'Microsoft\.UI\.Xaml'
    'Microsoft\.NET'
    'Microsoft\.Services\.Store'
    'Microsoft\.DirectX'
    'Microsoft\.Windows\.'
    'Microsoft\.DesktopAppInstaller'
    'Microsoft\.StorePurchaseApp'
    'Microsoft\.WindowsStore'
    'Microsoft\.VP9VideoExtensions'
    'Microsoft\.HEVCVideoExtension'
    'Microsoft\.HEIFImageExtension'
    'Microsoft\.WebMediaExtensions'
    'Microsoft\.RawImageExtension'
    'Microsoft\.AV1VideoExtension'
    'Microsoft\.WebpImageExtension'
    'Microsoft\.LanguageExperiencePack'
    'Microsoft\.MicrosoftEdge'
    'Microsoft\.Advertising'
    'Microsoft\.549981C3F5F10'
    'MicrosoftWindows\.'
    'windows\.'
    'Microsoft\.XboxGameCallableUI'
    'InputApp'
    'NcsiUwpApp'
    'RealtekSemiconductorCorp'
    'AppUp\.IntelGraphicsExperience'
    'E2ESurfaceApp'
    'Microsoft\.ECApp'
    'Microsoft\.LockApp'
    'Microsoft\.AAD\.BrokerPlugin'
    'Microsoft\.AccountsControl'
    'Microsoft\.AsyncTextService'
    'Microsoft\.BioEnrollment'
    'Microsoft\.CredDialogHost'
    'Microsoft\.MicrosoftEdgeDevToolsClient'
    'Microsoft\.Win32WebViewHost'
    'c5e2524a-ea46-4f67-841f-6a9465d9d515'
    'E046963F\.LenovoCompanion'
    'E046963F\.LenovoSettings'
    'DellInc\.'
    'AppUp\.'
    'NVIDIA\.'
    'NVIDIACorp\.'
    'AD2F1837\.'
    'Microsoft\.Getstarted'
    'Microsoft\.MixedReality'
    'Microsoft\.3DBuilder'
    'Microsoft\.Print3D'
    'Microsoft\.OneConnect'
    'Microsoft\.MSPaint'
    'Microsoft\.Microsoft3DViewer'
    'Microsoft\.BingFinance'
    'Microsoft\.BingSports'
    'Microsoft\.BingTravel'
    'Microsoft\.BingFoodAndDrink'
    'Microsoft\.BingHealthAndFitness'
    'Microsoft\.Messaging'
    'Microsoft\.ConnectivityStore'
    'Microsoft\.PPIProjection'
    'Microsoft\.People'
    'Microsoft\.WindowsMaps'
    'Microsoft\.WindowsCamera'
    'Microsoft\.WindowsAlarms'
    'Microsoft\.WindowsFeedback'
    'Microsoft\.WindowsSoundRecorder'
    'Microsoft\.ZuneVideo'
    'Microsoft\.ZuneMusic'
    'Microsoft\.Wallet'
    'Microsoft\.SkypeApp'
    'microsoft\.windowscommunicationsapps'
    'Microsoft\.WindowsCalculator'
    'Microsoft\.XboxIdentityProvider'
    'Microsoft\.XboxSpeechToTextOverlay'
    'Microsoft\.XboxGamingOverlay'
    'Microsoft\.Xbox\.TCUI'
    'Microsoft\.GamingApp'
    'Microsoft\.GamingServices'
    'Microsoft\.YourPhone'
    'Microsoft\.GetHelp'
    'Microsoft\.Cortana'
    'Microsoft\.549981C3F5F10'
    'Microsoft\.ScreenSketch'
    'Microsoft\.MicrosoftSolitaireCollection'
    'Microsoft\.MicrosoftStickyNotes'
    'Microsoft\.WindowsFeedbackHub'
    'Microsoft\.PowerAutomateDesktop'
    'Microsoft\.WindowsTerminal'
    'Microsoft\.Paint'
    'Microsoft\.SecHealthUI'
    'MicrosoftCorporationII\.QuickAssist'
    'Clipchamp\.Clipchamp'
    'Microsoft\.BingNews'
    'Microsoft\.BingWeather'
    'Microsoft\.Todos'
    'Microsoft\.Office\.OneNote'
    'Microsoft\.MicrosoftOfficeHub'
    'Microsoft\.OutlookForWindows'
    'Microsoft\.OneDriveSync'
    'Microsoft\.Office\.Desktop'
    'Microsoft\.MicrosoftPCManager'
    'Microsoft\.Copilot'
    'Microsoft\.WidgetsPlatformRuntime'
    'Microsoft\.StartExperiencesApp'
    'Microsoft\.CrossDevice'
    'MSTeams'
    'Microsoft\.Bing'
    'Microsoft\.DevHome'
    'Microsoft\.WindowsNotepad'
    'Microsoft\.Photos'
    'Microsoft\.Engagement'
    'Microsoft\.Family'
    'MicrosoftCorporationII\.'
    'Microsoft\.ApplicationCompatibilityEnhancements'
    'AsusTek\.'
    'ASUS'
    'B9ECED6F\.'
    'AMDRadeon'
    'DolbyLaboratories'
    'Dolby\.'
    'RealTek'
    'AVCLabs'
    'Microsoft\.OfficePushNotificationUtility'
    'Microsoft\.MicrosoftJournal'
    'Microsoft\.XboxApp'
    'ActionsServer'
    'aimgr'
    '^\d+$'
    'Microsoft\.Winget'
    'Microsoft\.VisualStudioCode'
    'Microsoft\.AVCEncoder'
    'WindowsWorkload\.'
    '^[0-9a-fA-F]{8}-'
    '^[0-9a-fA-F]{4,}'
    'Clipchamp'
)

# Friendly names for Store apps worth showing
$StoreAppFriendlyNames = @{
    'Microsoft.MicrosoftTo-Do'        = 'Microsoft To Do'
    'Microsoft.Whiteboard'            = 'Microsoft Whiteboard'
    'Microsoft.ScreenSketch'          = 'Snipping Tool'
    'Microsoft.WindowsNotepad'        = 'Notepad'
    'Microsoft.Photos'                = 'Microsoft Photos'
    'Microsoft.MicrosoftStickyNotes'  = 'Microsoft Sticky Notes'
    'Microsoft.WindowsTerminal'       = 'Windows Terminal'
    'Microsoft.PowerAutomateDesktop'  = 'Power Automate Desktop'
    'SpotifyAB.SpotifyMusic'          = 'Spotify'
    'CanonicalGroupLimited.Ubuntu'    = 'Ubuntu (WSL)'
    'PythonSoftwareFoundation.Python' = 'Python'
    'WhatsApp'                        = 'WhatsApp'
    'Facebook.Instagram'              = 'Instagram'
    '9426MICRO-STARINTERNATION.MSICenter' = 'MSI Center'
    'Anthropic.Claude'                    = 'Claude Desktop'
    'OpenAI.Codex'                        = 'OpenAI Codex'
    'OpenAI.ChatGPT'                      = 'ChatGPT'
}

# Microsoft 365 component publishers
$M365Publishers = @(
    'Microsoft Corporation'
)

# Microsoft 365 app name patterns (to group under "Microsoft 365")
$M365AppPatterns = @(
    '^Microsoft (Excel|Word|Outlook|PowerPoint|Access|Publisher|OneNote|OneDrive|Teams|Lync|Visio|Project)(\s|$)'
    '^Microsoft Office'
    '^Microsoft 365'
)

# ── Functions ──────────────────────────────────────────────────────────────────

function Get-Win32Apps {
    $regPaths = @(
        @{ Path = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'; UserInstalled = $false }
        @{ Path = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'; UserInstalled = $false }
        @{ Path = 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'; UserInstalled = $true }
    )

    $allApps = @()

    foreach ($reg in $regPaths) {
        try {
            $items = Get-ItemProperty -Path $reg.Path -ErrorAction SilentlyContinue
        } catch {
            continue
        }
        foreach ($item in $items) {
            $name = $item.DisplayName
            if ([string]::IsNullOrWhiteSpace($name)) { continue }
            $name = $name.Trim()

            # Skip if it's a system component flagged in registry
            if ($item.SystemComponent -eq 1) { continue }
            if ($item.ReleaseType -match 'Update|Hotfix|Security Update|Service Pack') { continue }
            if ([string]::IsNullOrWhiteSpace($item.UninstallString) -and
                [string]::IsNullOrWhiteSpace($item.InstallLocation)) { continue }

            $allApps += [PSCustomObject]@{
                Name          = $name
                Version       = if ($item.DisplayVersion) { $item.DisplayVersion.Trim() } else { '' }
                Publisher     = if ($item.Publisher) { $item.Publisher.Trim() } else { '' }
                UserInstalled = $reg.UserInstalled
                InstallPath   = if ($item.InstallLocation) { $item.InstallLocation.Trim() } else { '' }
            }
        }
    }

    return $allApps
}

function Get-StoreApps {
    try {
        $packages = Get-AppxPackage -ErrorAction SilentlyContinue
    } catch {
        return @()
    }

    $apps = @()
    foreach ($pkg in $packages) {
        $name = $pkg.Name
        $excluded = $false
        foreach ($pattern in $ExcludeStorePatterns) {
            if ($name -match $pattern) { $excluded = $true; break }
        }
        if ($excluded) { continue }

        # Skip framework packages
        if ($pkg.IsFramework) { continue }
        if ($pkg.SignatureKind -eq 'System') { continue }

        # Try to get a friendly name
        $displayName = $null
        foreach ($key in $StoreAppFriendlyNames.Keys) {
            if ($name -match [regex]::Escape($key)) {
                $displayName = $StoreAppFriendlyNames[$key]
                break
            }
        }

        if (-not $displayName) {
            # Use the last segment of the package name as a fallback
            $parts = $name -split '\.'
            if ($parts.Count -gt 1) {
                $displayName = $parts[-1] -creplace '([a-z])([A-Z])', '$1 $2'
            } else {
                $displayName = $name
            }
            # Skip if it still looks like a system package ID or GUID
            if ($displayName -match '^\{|^[a-fA-F0-9]{8}-|^[0-9]+$|^WindowsWorkload') { continue }
        }

        $apps += [PSCustomObject]@{
            Name    = $displayName
            Version = $pkg.Version.ToString()
        }
    }

    return $apps | Sort-Object Name -Unique
}

function Test-IsRuntime {
    param([string]$Name)
    foreach ($pattern in $RuntimePatterns) {
        if ($Name -match $pattern) { return $true }
    }
    return $false
}

function Test-IsExcluded {
    param([string]$Name)
    foreach ($pattern in $ExcludePatterns) {
        if ($Name -match $pattern) { return $true }
    }
    return $false
}

function Test-IsM365App {
    param([string]$Name)
    foreach ($pattern in $M365AppPatterns) {
        if ($Name -match $pattern) { return $true }
    }
    return $false
}

function Format-AppLine {
    param(
        [string]$Name,
        [string]$Version,
        [bool]$UserInstalled = $false,
        [string]$Indent = ''
    )

    $line = "$Indent$Name"
    if ($Version) { $line += " (Version $Version)" }
    if ($UserInstalled) { $line += "     [User-installed]" }
    return $line
}

function Get-WorkstationSnapshot {
    $snapshot = @{ Issues = @() }
    $queries = [ordered]@{
        Win32_ComputerSystem = @('Manufacturer', 'Model')
        Win32_OperatingSystem = @('Caption', 'Version', 'BuildNumber', 'OSArchitecture', 'TotalVisibleMemorySize', 'FreePhysicalMemory', 'LastBootUpTime')
        Win32_Processor = @('Name', 'NumberOfCores', 'NumberOfLogicalProcessors')
        Win32_VideoController = @('Name', 'DriverVersion')
        Win32_LogicalDisk = @('DeviceID', 'FileSystem', 'Size', 'FreeSpace')
    }
    foreach ($className in $queries.Keys) {
        $parameters = @{
            ClassName = $className
            Property = $queries[$className]
            OperationTimeoutSec = 5
            ErrorAction = 'Stop'
        }
        if ($className -eq 'Win32_LogicalDisk') { $parameters.Filter = 'DriveType = 3' }
        try {
            $snapshot[$className] = @(Get-CimInstance @parameters)
            if ($snapshot[$className].Count -eq 0) { $snapshot.Issues += "${className}: Unavailable (no data returned)." }
        } catch {
            $snapshot[$className] = @()
            $snapshot.Issues += "${className}: Unavailable (query failed or timed out)."
        }
    }
    return $snapshot
}

function Format-WorkstationSnapshot {
    param([Parameter(Mandatory = $true)]$Snapshot)

    $lines = @('', '--- Workstation Summary ---')
    $attention = @()
    $system = $Snapshot.Win32_ComputerSystem | Select-Object -First 1
    $os = $Snapshot.Win32_OperatingSystem | Select-Object -First 1
    if ($system) { $lines += "Machine: $($system.Manufacturer) $($system.Model)" }
    else { $lines += 'Machine: Unavailable' }
    if ($os) {
        $lines += "OS: $($os.Caption) $($os.OSArchitecture) (version $($os.Version), build $($os.BuildNumber))"
        if ($os.LastBootUpTime) {
            $uptime = (Get-Date) - $os.LastBootUpTime
            $lines += "Uptime: $($uptime.Days)d $($uptime.Hours)h $($uptime.Minutes)m (since last Windows boot)"
        } else { $lines += 'Uptime: Unavailable' }
    } else { $lines += 'OS: Unavailable'; $lines += 'Uptime: Unavailable' }
    $processors = @($Snapshot.Win32_Processor)
    if ($processors.Count -gt 0) {
        foreach ($cpu in $processors) {
            $lines += "CPU: $($cpu.Name) | $($cpu.NumberOfCores) cores / $($cpu.NumberOfLogicalProcessors) logical processors"
        }
    } else { $lines += 'CPU: Unavailable' }
    $gpus = @($Snapshot.Win32_VideoController)
    if ($gpus.Count -gt 0) {
        foreach ($gpu in $gpus) { $lines += "GPU: $($gpu.Name) | Driver: $($gpu.DriverVersion)" }
    } else { $lines += 'GPU: Unavailable' }

    if ($os -and $os.TotalVisibleMemorySize -gt 0 -and $null -ne $os.FreePhysicalMemory -and
        $os.FreePhysicalMemory -ge 0 -and $os.FreePhysicalMemory -le $os.TotalVisibleMemorySize) {
        $usedMemory = $os.TotalVisibleMemorySize - $os.FreePhysicalMemory
        $memoryPercent = 100.0 * $usedMemory / $os.TotalVisibleMemorySize
        $lines += 'RAM: {0:N1} / {1:N1} GiB used ({2:N1}%); {3:N1} GiB available to Windows' -f
            ($usedMemory / 1MB), ($os.TotalVisibleMemorySize / 1MB), $memoryPercent, ($os.FreePhysicalMemory / 1MB)
        if ($memoryPercent -ge 85) { $attention += '- High RAM usage: {0:N1}% used at capture time (threshold: 85%).' -f $memoryPercent }
    } else { $lines += 'RAM: Unavailable' }

    $lines += '', '--- Local Disk Utilization ---'
    $lines += 'Drive | File system | Total GiB | Used GiB | Free GiB | Used % | Free %'
    $disks = @($Snapshot.Win32_LogicalDisk | Sort-Object DeviceID)
    if ($disks.Count -eq 0) { $lines += 'Unavailable (no local fixed-disk readings).' }
    foreach ($disk in $disks) {
        if ($disk.Size -le 0 -or $null -eq $disk.FreeSpace -or $disk.FreeSpace -lt 0 -or $disk.FreeSpace -gt $disk.Size) {
            $lines += "$($disk.DeviceID) | Unavailable (capacity or free space not reported)."
            continue
        }
        $used = $disk.Size - $disk.FreeSpace
        $freePercent = 100.0 * $disk.FreeSpace / $disk.Size
        $lines += '{0} | {1} | {2:N1} | {3:N1} | {4:N1} | {5:N1}% | {6:N1}%' -f
            $disk.DeviceID, $disk.FileSystem, ($disk.Size / 1GB), ($used / 1GB), ($disk.FreeSpace / 1GB), (100 - $freePercent), $freePercent
        if ($freePercent -lt 10) {
            $attention += '- Low disk space on {0} - {1:N1} GiB free ({2:N1}%; threshold: below 10%).' -f
                $disk.DeviceID, ($disk.FreeSpace / 1GB), $freePercent
        }
    }
    $lines += 'Scope: mounted local fixed disks with drive letters. GiB = 1,073,741,824 bytes.'

    $lines += '', '--- Attention Items ---'
    if ($attention.Count) { $lines += $attention }
    else { $lines += 'No low-disk/high-RAM thresholds triggered in the available readings.' }
    $lines += '', '--- Snapshot Collection Notes ---'
    if ($Snapshot.Issues.Count) { $lines += $Snapshot.Issues }
    else { $lines += 'All workstation probes returned data.' }
    $lines += 'Point-in-time resource snapshot; no folder-size traversal or Tune runtime probes.'
    return $lines
}

function Save-ScanReport {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][string]$Text)
    $Text | Out-File -LiteralPath $Path -Encoding UTF8 -NoClobber -ErrorAction Stop
}

# ── Main ───────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "AppLens - Pre-Audit App Scanner" -ForegroundColor Cyan
Write-Host "Capturing workstation resources and installed applications..." -ForegroundColor Gray
Write-Host ""

$scanStarted = Get-Date
$scanTimer = [Diagnostics.Stopwatch]::StartNew()
$workstation = Get-WorkstationSnapshot

# Collect Win32 apps
$win32Apps = Get-Win32Apps

# Separate into categories
$runtimes = @()
$m365Apps = @()
$desktopApps = @()
$seenNames = @{}

foreach ($app in $win32Apps) {
    # Deduplicate by name (keep the first occurrence)
    $key = $app.Name.ToLower()
    if ($seenNames.ContainsKey($key)) { continue }
    $seenNames[$key] = $true

    if (Test-IsExcluded $app.Name) { continue }

    if (Test-IsRuntime $app.Name) {
        $runtimes += $app
    } elseif (Test-IsM365App $app.Name) {
        $m365Apps += $app
    } else {
        $desktopApps += $app
    }
}

# Collect Store apps
$storeApps = @(Get-StoreApps)
$scanTimer.Stop()

# Build output
$output = @()
$output += "=== AppLens Scan Results ==="
$output += "Computer: $env:COMPUTERNAME"
$output += "User: $env:USERNAME"
$output += "Scan Date: $($scanStarted.ToString('yyyy-MM-dd HH:mm:ss zzz'))"
$output += 'Mode: Audit (read-only)'
$output += 'Collection duration: {0:N1} seconds' -f $scanTimer.Elapsed.TotalSeconds
$output += Format-WorkstationSnapshot -Snapshot $workstation
$output += ''
$output += '--- App Inventory Summary ---'
$output += "Desktop entries: $($desktopApps.Count + $m365Apps.Count) | Store entries: $($storeApps.Count) | Runtimes/frameworks: $($runtimes.Count)"
$output += 'Scope: registered desktop apps and current-user Store apps after filtering; portable/unregistered apps may be absent.'
$output += ""
$output += "--- Desktop Applications ---"

# Microsoft 365 group
if ($m365Apps.Count -gt 0) {
    $m365Apps = $m365Apps | Sort-Object Name
    $output += "Microsoft 365 (Office)"
    foreach ($app in $m365Apps) {
        $shortName = $app.Name
        $output += Format-AppLine -Name $shortName -Version $app.Version -UserInstalled $app.UserInstalled -Indent '  - '
    }
}

# Other desktop apps
$desktopApps = $desktopApps | Sort-Object Name
foreach ($app in $desktopApps) {
    $output += Format-AppLine -Name $app.Name -Version $app.Version -UserInstalled $app.UserInstalled
}

# Store apps
$output += ""
$output += "--- Microsoft Store Apps ---"
if ($storeApps.Count -eq 0) {
    $output += "(none detected)"
} else {
    foreach ($app in $storeApps) {
        $output += $app.Name
    }
}

# Runtimes
$output += ""
$output += "--- Runtimes & Frameworks (for reference) ---"
if ($runtimes.Count -eq 0) {
    $output += "(none detected)"
} else {
    $runtimes = $runtimes | Sort-Object Name
    foreach ($app in $runtimes) {
        $output += Format-AppLine -Name $app.Name -Version $app.Version
    }
}

# Write to file
$outputText = $output -join "`r`n"
Save-ScanReport -Path $OutputPath -Text $outputText

# Display results
Write-Host $outputText
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  Scan complete!" -ForegroundColor Green
Write-Host "  Results saved to:" -ForegroundColor Green
Write-Host "  $OutputPath" -ForegroundColor Yellow
Write-Host "" -ForegroundColor Green
Write-Host "  Please send this file to your IT contact." -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""

# Keep window open if launched via double-click (.bat wrapper sets this env var)
if ($env:APPLENS_INTERACTIVE -eq '1') {
    Write-Host "Press any key to close..." -ForegroundColor Gray
    $null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')
}
