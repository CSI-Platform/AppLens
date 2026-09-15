[CmdletBinding()]
param([Parameter(Mandatory)][string]$ManifestPath)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$manifest = [IO.Path]::GetFullPath($ManifestPath)
if (!$manifest.StartsWith((Join-Path $repoRoot 'artifacts\removal-preflight-'),[StringComparison]::OrdinalIgnoreCase)) {
    throw 'Only a generated AppLens development plan is accepted.'
}
$plan = Get-Content -Raw -LiteralPath $manifest | ConvertFrom-Json
$runRoot = Split-Path -Parent $manifest
if($plan.Packages.Count -ne 2) { throw 'Expected exactly two disposable test packages.' }
foreach($package in $plan.Packages) {
    $path = [IO.Path]::GetFullPath($package.Path)
    if (!$path.StartsWith($runRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or
        $package.Name -notmatch '^CSI\.AppLens\.(RemovalProbe|ClientTest|Disposable)\.[a-f0-9]{16}$' -or
        (Get-FileHash -LiteralPath $path).Hash -ne $package.SHA256) { throw 'Test package validation failed.' }
}
$log=Join-Path $runRoot 'setup.jsonl'
try {
    foreach($package in $plan.Packages) {
        Add-AppxPackage -Path $package.Path -AllowUnsigned -ErrorAction Stop
        [IO.File]::AppendAllText($log,(@{Time=(Get-Date).ToString('O');Package=$package.Name;Status='Installed'}|ConvertTo-Json -Compress)+[Environment]::NewLine)
    }
} catch {
    [IO.File]::AppendAllText($log,(@{Time=(Get-Date).ToString('O');Status='Failed';Error=$_.Exception.Message}|ConvertTo-Json -Compress)+[Environment]::NewLine)
    throw
}
