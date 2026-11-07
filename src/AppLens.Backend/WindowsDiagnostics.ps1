# AppLens runs one probe per process. Emit only selected observations.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$state = 'Succeeded'

function Find-LocalApplication($name) {
    $fixed = @([IO.DriveInfo]::GetDrives() | Where-Object DriveType -eq 'Fixed' | ForEach-Object Name)
    foreach ($entry in ($env:PATH -split ';')) {
        $root = $entry.Trim().Trim('"')
        if ($root -match '^[A-Za-z]:\\' -and $fixed -contains [IO.Path]::GetPathRoot($root)) {
            $file = Join-Path $root ($name + '.exe')
            if ([IO.File]::Exists($file)) { return $file }
        }
    }
}

function Invoke-LocalApplication($name, [string[]]$arguments) {
    $app = Find-LocalApplication $name
    if (!$app) { throw [NotSupportedException]::new('Executable unavailable') }
    # Windows PowerShell wraps native stderr; only return successful output.
    $ErrorActionPreference = 'Continue'
    $output = & $app @arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        if (($output -join ' ') -match 'permission denied|access is denied|operation not permitted') {
            throw [UnauthorizedAccessException]::new()
        }
        throw [InvalidOperationException]::new()
    }
    @($output | ForEach-Object ToString)
}

function Measure-SelectedFolder($label, $path, [switch]$repositories, [string]$modelPattern) {
    if (!(Test-Path -LiteralPath $path -PathType Container)) { return }
    $root = Get-Item -LiteralPath $path -Force
    if ($root.Attributes -band [IO.FileAttributes]::ReparsePoint) { return }
    $pending = New-Object 'System.Collections.Generic.Stack[object]'
    $pending.Push(@{ Directory = $root; Depth = 0 })
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $visited = 0
    $total = 0L
    $rowState = 'Succeeded'
    :walk while ($pending.Count -gt 0) {
        $item = $pending.Pop()
        try {
            if ($repositories -and (Test-Path -LiteralPath (Join-Path $item.Directory.FullName '.git'))) {
                $total++
                continue
            }
            foreach ($entry in $item.Directory.EnumerateFileSystemInfos()) {
                $visited++
                if ($visited -gt 50000 -or $clock.Elapsed.TotalSeconds -gt 1.2) {
                    $rowState = 'Partial'
                    break walk
                }
                if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
                if ($entry -is [IO.DirectoryInfo]) {
                    if (!$repositories -or $item.Depth -lt 4) {
                        $pending.Push(@{ Directory = $entry; Depth = $item.Depth + 1 })
                    }
                } elseif (!$repositories -and (!$modelPattern -or $entry.Name -like $modelPattern)) {
                    $total += $(if ($modelPattern) { 1 } else { $entry.Length })
                }
            }
        } catch [UnauthorizedAccessException] { $rowState = 'PermissionLimited' }
          catch [IO.IOException] { $rowState = 'Partial' }
    }
    $key = if ($repositories) { 'RepositoryCount' } elseif ($modelPattern) { 'ModelCount' } else { 'Bytes' }
    @{ Location = $label; $key = $total; State = $rowState }
}

function Read-Startup {
    $run = 'Software\Microsoft\Windows\CurrentVersion\Run'
    $approval = 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved'
    foreach ($hive in 'CurrentUser', 'LocalMachine') {
        foreach ($view in 'Registry64', 'Registry32') {
            # The current user's Run key is shared between registry views.
            if ($hive -eq 'CurrentUser' -and $view -eq 'Registry32') { continue }
            $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey($hive, $view)
            try {
                $key = $base.OpenSubKey($run)
                if (!$key) { continue }
                $approvalView = if ($view -eq 'Registry32') { 'Run32' } else { 'Run' }
                $approved = $(if ($hive -eq 'CurrentUser') { [Microsoft.Win32.Registry]::CurrentUser }
                              else { [Microsoft.Win32.Registry]::LocalMachine }).OpenSubKey("$approval\$approvalView")
                try {
                    foreach ($name in $key.GetValueNames()) {
                        $bytes = if ($approved) { $approved.GetValue($name) }
                        $value = if ($bytes) { $bytes[0] }
                        $enabled = if ($value -in 2,6) { 'Enabled' } elseif ($value -in 3,7) { 'Disabled' } else { 'Unknown' }
                        @{ Name = $name; Scope = $hive; Location = $approvalView; Approval = $enabled }
                    }
                } finally {
                    if ($approved) { $approved.Dispose() }
                    $key.Dispose()
                }
            } finally { $base.Dispose() }
        }
        $scope = if ($hive -eq 'CurrentUser') { 'Startup' } else { 'CommonStartup' }
        $folder = [Environment]::GetFolderPath($scope)
        if (!$folder -or !(Test-Path -LiteralPath $folder)) { continue }
        $base = if ($hive -eq 'CurrentUser') { [Microsoft.Win32.Registry]::CurrentUser } else { [Microsoft.Win32.Registry]::LocalMachine }
        $key = $base.OpenSubKey("$approval\StartupFolder")
        try {
            foreach ($entry in Get-ChildItem -LiteralPath $folder -File) {
                $bytes = if ($key) { $key.GetValue($entry.Name) }
                $value = if ($bytes) { $bytes[0] }
                $enabled = if ($value -in 2,6) { 'Enabled' } elseif ($value -in 3,7) { 'Disabled' } else { 'Unknown' }
                @{ Name = $entry.Name; Scope = $hive; Location = 'StartupFolder'; Approval = $enabled }
            }
        } finally { if ($key) { $key.Dispose() } }
    }
}

function Read-Battery {
    $batteries = @(Get-CimInstance -ClassName Win32_Battery)
    if (!$batteries.Count) { throw [NotSupportedException]::new('Inapplicable') }
    $static = @(Get-CimInstance -Namespace root/wmi -ClassName BatteryStaticData -ErrorAction SilentlyContinue)
    $full = @(Get-CimInstance -Namespace root/wmi -ClassName BatteryFullChargedCapacity -ErrorAction SilentlyContinue)
    $cycles = @(Get-CimInstance -Namespace root/wmi -ClassName BatteryCycleCount -ErrorAction SilentlyContinue)
    $devices = @($static + $full + $cycles | Group-Object InstanceName | ForEach-Object { $_.Group[0] })
    if (!$devices.Count) {
        foreach ($device in $batteries) {
            @{ DesignCapacity = $device.DesignCapacity; FullCapacity = $device.FullChargeCapacity
               Cycles = $null; CapacityUnit = 'mWh'; State = 'Partial' }
        }
    }
    foreach ($device in $devices) {
        $design = $static | Where-Object InstanceName -eq $device.InstanceName | Select-Object -First 1
        $capacity = $full | Where-Object InstanceName -eq $device.InstanceName | Select-Object -First 1
        $cycle = $cycles | Where-Object InstanceName -eq $device.InstanceName | Select-Object -First 1
        $designCapacity = $design.DesignedCapacity
        if (!$designCapacity -and $devices.Count -eq 1 -and $batteries.Count -eq 1) {
            $designCapacity = $batteries[0].DesignCapacity
        }
        @{ DesignCapacity = $designCapacity; FullCapacity = $capacity.FullChargedCapacity
           Cycles = $cycle.CycleCount; CapacityUnit = 'mWh'; CycleCountState = $(if ($cycle) { 'Succeeded' } else { 'Unavailable' })
           State = $(if ($designCapacity -and $capacity.FullChargedCapacity) { 'Succeeded' } else { 'Partial' }) }
    }
}

function Read-Probe {
    switch ($Probe) {
        'Processes' {
            Get-Process | Sort-Object WorkingSet64 -Descending | Select-Object -First 15 | ForEach-Object {
                @{ Name = $_.ProcessName; Pid = $_.Id; WorkingSetKiB = [long]($_.WorkingSet64 / 1KB); CpuSeconds = $_.CPU }
            }
        }
        'Startup' { Read-Startup }
        'Services' {
            Get-CimInstance Win32_Service -Property Name,State,StartMode |
                Group-Object State,StartMode | ForEach-Object {
                    @{ State = $_.Group[0].State; StartMode = $_.Group[0].StartMode; Count = $_.Count }
                }
        }
        'Storage' {
            Measure-SelectedFolder 'Temporary files' (Join-Path $env:LOCALAPPDATA 'Temp')
            Measure-SelectedFolder 'Ollama' (Join-Path $env:USERPROFILE '.ollama')
            Measure-SelectedFolder 'Package cache' (Join-Path $env:LOCALAPPDATA 'pip\Cache')
            Measure-SelectedFolder 'Docker Desktop' (Join-Path $env:LOCALAPPDATA 'Docker')
        }
        'Git' {
            foreach ($folder in 'Desktop', 'Documents', 'Projects', 'src') {
                Measure-SelectedFolder $folder (Join-Path $env:USERPROFILE $folder) -repositories
            }
        }
        'Docker' {
            Invoke-LocalApplication 'docker' @('--host','npipe:////./pipe/docker_engine','system','df','--format',
                '{{.Type}}|{{.TotalCount}}|{{.Active}}|{{.Size}}') | ForEach-Object {
                $parts = $_ -split '\|'
                if ($parts.Count -eq 4) {
                    @{ Type = $parts[0]; Total = $parts[1]; Active = $parts[2]; Size = $parts[3] }
                }
            }
        }
        'Wsl' {
            if (!(Find-LocalApplication 'wsl')) { throw [NotSupportedException]::new() }
            $root = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Lxss'
            if (Test-Path -LiteralPath $root) {
                Get-ChildItem -LiteralPath $root | ForEach-Object {
                    $values = Get-ItemProperty -LiteralPath $_.PSPath
                    if ($values.DistributionName) { @{ Version = $values.Version } }
                }
            }
        }
        'Tools' {
            foreach ($tool in 'ollama','llama-cli','llama-server','lms','nvcc','nvidia-smi') {
                if (Find-LocalApplication $tool) { @{ Tool = $tool } }
            }
        }
        'Models' {
            Measure-SelectedFolder 'Ollama manifests' (Join-Path $env:USERPROFILE '.ollama\models\manifests') -modelPattern '*'
            Measure-SelectedFolder 'LM Studio GGUF' (Join-Path $env:USERPROFILE '.lmstudio\models') -modelPattern '*.gguf'
        }
        'Graphics' {
            foreach ($device in Get-CimInstance Win32_VideoController -Property Name,AdapterRAM) {
                @{ Device = $device.Name; Backend = 'Display driver'; CimAdapterRamBytes32Bit = $device.AdapterRAM
                   SharedOrReservedBytes = $null; VerifiedUsableInferenceBytes = $null }
            }
            $index = 0
            foreach ($counter in Get-CimInstance Win32_PerfFormattedData_GPUPerformanceCounters_GPUAdapterMemory -ErrorAction SilentlyContinue) {
                $index++
                @{ Device = "GPU counter $index"; Backend = 'Windows GPU performance counter'
                   DedicatedUsageBytes = $counter.DedicatedUsage; SharedUsageBytes = $counter.SharedUsage
                   VerifiedUsableInferenceBytes = $null }
            }
            if (Find-LocalApplication 'nvidia-smi') {
                Invoke-LocalApplication 'nvidia-smi' @('--query-gpu=name,memory.total,memory.free','--format=csv,noheader,nounits') |
                    ForEach-Object {
                        $parts = $_ -split ',\s*'
                        if ($parts.Count -eq 3) {
                            @{ Device = $parts[0]; Backend = 'NVIDIA driver'; ReportedVramMiB = $parts[1]
                               ReportedFreeVramMiB = $parts[2]; VerifiedUsableInferenceBytes = $null }
                        }
                    }
            }
        }
        'Llama' {
            $tool = if (Find-LocalApplication 'llama-cli') { 'llama-cli' } else { 'llama-server' }
            Invoke-LocalApplication $tool @('--list-devices') | ForEach-Object {
                if ($_ -match '^\s*((CUDA|Vulkan|ROCm|HIP|SYCL|Metal|CPU)[\w.-]*):\s*(.+)$') {
                    @{ Device = $Matches[1]; Backend = $Matches[2]; Description = $Matches[3]; VerifiedUsableInferenceBytes = $null }
                }
            }
        }
        'Cuda' {
            Invoke-LocalApplication 'nvcc' @('--version') | ForEach-Object {
                if ($_ -match 'release ([\d.]+)') { @{ Release = $Matches[1] } }
            }
        }
        'Torch' {
            $env:PYTHONDONTWRITEBYTECODE = '1'
            $script = "import importlib.util,json; s=importlib.util.find_spec('torch'); exec('import torch') if s else None; print(json.dumps([dict(Version=torch.__version__,Backend='ROCm' if torch.version.hip else 'CUDA' if torch.version.cuda else 'CPU',DeviceCount=torch.cuda.device_count(),VerifiedUsableInferenceBytes=None)] if s else []))"
            $python = Find-LocalApplication 'python'
            if (!$python -or $python -like '*\Microsoft\WindowsApps\*') { throw [NotSupportedException]::new() }
            $items = ((Invoke-LocalApplication 'python' @('-I','-B','-c',$script)) -join '') | ConvertFrom-Json
            foreach ($item in $items) { $item }
        }
        'Disk' {
            $index = 0
            foreach ($disk in Get-PhysicalDisk) {
                $index++
                $status = 'Succeeded'
                try { $reading = $disk | Get-StorageReliabilityCounter }
                catch { $reading = $null; $status = 'Partial' }
                @{ Device = "Disk $index"; BusType = $disk.BusType.ToString(); Health = $disk.HealthStatus.ToString()
                   TemperatureC = $reading.Temperature; WearPercent = $reading.Wear; PowerOnHours = $reading.PowerOnHours
                   ReadErrors = $reading.ReadErrorsTotal; WriteErrors = $reading.WriteErrorsTotal; State = $status }
            }
        }
        'Battery' { Read-Battery }
        'Encryption' {
            $index = 0
            Get-CimInstance -Namespace root/cimv2/security/microsoftvolumeencryption -ClassName Win32_EncryptableVolume |
                ForEach-Object {
                    $index++
                    $protection = switch ($_.ProtectionStatus) {
                        0 { 'Off' }
                        1 { 'On' }
                        default { 'Unknown' }
                    }
                    @{ Volume = "Volume $index"; ProtectionStatus = $protection }
                }
        }
        'Network' {
            $adapters = @(Get-NetAdapter)
            @{ InterfaceCount = $adapters.Count; UpInterfaceCount = @($adapters | Where-Object Status -eq 'Up').Count }
        }
        'Dns' {
            $addresses = @(Get-DnsClientServerAddress | ForEach-Object ServerAddresses | Where-Object { $_ } | Sort-Object -Unique)
            @{ ResolverCount = $addresses.Count }
        }
        'Listeners' {
            @{ Transport = 'TCP'; Count = @(Get-NetTCPConnection -ErrorAction Stop | Where-Object State -eq 'Listen').Count }
            @{ Transport = 'UDP'; Count = @(Get-NetUDPEndpoint).Count }
        }
        'Firewall' {
            Get-NetFirewallProfile -PolicyStore ActiveStore | ForEach-Object {
                @{ Profile = $_.Name; Enabled = $_.Enabled.ToString(); DefaultInboundAction = $_.DefaultInboundAction.ToString() }
            }
        }
        'Backup' {
            try {
                Get-WinEvent -FilterHashtable @{ LogName = 'Microsoft-Windows-Backup'; Id = 4,5,9 } -MaxEvents 12 |
                    ForEach-Object {
                        @{ EventId = $_.Id; Outcome = $(if ($_.Id -eq 4) { 'Succeeded' } else { 'Failed' })
                           ObservedAt = $_.TimeCreated.ToUniversalTime().ToString('o') }
                    }
            } catch {
                if ($_.FullyQualifiedErrorId -notlike 'NoMatchingEventsFound*') { throw }
            }
        }
        'Jobs' {
            Get-ScheduledTask | Group-Object State | ForEach-Object { @{ State = $_.Name; Count = $_.Count } }
        }
        'Updates' {
            $session = New-Object -ComObject Microsoft.Update.Session
            $searcher = $session.CreateUpdateSearcher()
            $searcher.Online = $false
            $result = $searcher.Search('IsInstalled=0 and IsHidden=0 and Type=''Software''')
            $security = @($result.Updates | Where-Object {
                @($_.Categories | Where-Object CategoryID -eq '0fa1201d-4330-4fa8-8ae9-b877473b6441').Count -gt 0
            })
            @{ CachedCandidateCount = $result.Updates.Count; SecurityCandidateCount = $security.Count
               MetadataRefreshed = $false; ResultCode = $result.ResultCode; State = $(if ($result.ResultCode -eq 2) { 'Succeeded' } else { 'Partial' }) }
        }
        'Reboot' {
            $cbs = Test-Path -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending'
            $wu = Test-Path -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired'
            $rename = Get-ItemProperty -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager' -ErrorAction Stop
            @{ RebootRequired = [bool]($cbs -or $wu -or $rename.PendingFileRenameOperations)
               Servicing = $cbs; WindowsUpdate = $wu; PendingFileRename = [bool]$rename.PendingFileRenameOperations }
        }
    }
}

$rows = New-Object 'System.Collections.Generic.List[object]'
try {
    Read-Probe | ForEach-Object { if ($null -ne $_) { $rows.Add($_) } }
    if (!$rows.Count) { $state = 'Empty' }
    elseif ($Probe -in 'Disk','Battery','Storage','Git','Models','Updates' -and
            @($rows | Where-Object { $_.State -in 'Partial','PermissionLimited' }).Count) { $state = 'Partial' }
} catch {
    if ($rows.Count) { $state = 'Partial' }
    elseif ($_.Exception -is [UnauthorizedAccessException] -or $_.Exception -is [Security.SecurityException] -or
            $_.Exception.Message -match 'access.+denied|0x80070005') { $state = 'PermissionLimited' }
    elseif ($_.Exception.Message -eq 'Inapplicable') { $state = 'Inapplicable' }
    elseif ($_.Exception -is [NotSupportedException] -or $_.CategoryInfo.Category -in 'ObjectNotFound','NotInstalled' -or
            $_.Exception.Message -match 'invalid namespace|invalid class|not registered') { $state = 'Unavailable' }
    else { $state = 'Failed' }
}
[Console]::WriteLine((ConvertTo-Json -InputObject @{ State = $state; Rows = $rows.ToArray() } -Depth 6 -Compress))
