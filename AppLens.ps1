#Requires -Version 5.1
<#
.SYNOPSIS
    AppLens — Pre-Audit App Scanner for CSI AI Workflow Audits
.DESCRIPTION
    Scans installed desktop apps (Win32) and Microsoft Store apps without
    requiring admin rights. Outputs a clean, categorized Markdown file suitable
    for pasting into an AI prompt alongside a workflow audit transcript.
#>

# ── Configuration ──────────────────────────────────────────────────────────────

function Get-DesktopFilePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FileName
    )

    $desktopPath = $env:APPLENS_OUTPUT_DIR
    if ([string]::IsNullOrWhiteSpace($desktopPath)) {
        $desktopPath = [Environment]::GetFolderPath('Desktop')
    }
    if ([string]::IsNullOrWhiteSpace($desktopPath)) {
        $desktopPath = Join-Path $env:USERPROFILE 'Desktop'
    }

    if (-not (Test-Path -LiteralPath $desktopPath)) {
        New-Item -ItemType Directory -Path $desktopPath -Force | Out-Null
    }

    return Join-Path $desktopPath $FileName
}

$OutputPath = Get-DesktopFilePath -FileName "AppLens_Results_$env:COMPUTERNAME.md"

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

# ── Main ───────────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "AppLens - Pre-Audit App Scanner" -ForegroundColor Cyan
Write-Host "Scanning installed applications..." -ForegroundColor Gray
Write-Host ""

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
$storeApps = Get-StoreApps

# Build output
$output = @()
$output += "# AppLens Scan Results"
$output += "- **Computer:** $env:COMPUTERNAME"
$output += "- **User:** $env:USERNAME"
$output += "- **Scan Date:** $(Get-Date -Format 'yyyy-MM-dd')"
$output += ""
$output += "## Desktop Applications"

# Microsoft 365 group
if ($m365Apps.Count -gt 0) {
    $m365Apps = $m365Apps | Sort-Object Name
    $output += "### Microsoft 365 (Office)"
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
$output += "## Microsoft Store Apps"
if ($storeApps.Count -eq 0) {
    $output += "(none detected)"
} else {
    foreach ($app in $storeApps) {
        $output += $app.Name
    }
}

# Runtimes
$output += ""
$output += "## Runtimes & Frameworks (for reference)"
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
$outputText | Out-File -FilePath $OutputPath -Encoding UTF8

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
