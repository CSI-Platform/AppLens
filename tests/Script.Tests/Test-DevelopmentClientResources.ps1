param([Parameter(Mandatory)][string]$PackageDirectory)
$ErrorActionPreference = 'Stop'
$directory = (Resolve-Path -LiteralPath $PackageDirectory).Path
[xml]$manifest = Get-Content -Raw -LiteralPath (Join-Path $directory 'AppxManifest.xml')
$pri = Join-Path $directory 'resources.pri'
if (!(Test-Path -LiteralPath $pri)) { throw 'Packaged client is missing resources.pri.' }
$dump = Join-Path ([IO.Path]::GetTempPath()) ('AppLens-pri-check-' + [guid]::NewGuid().ToString('N') + '.xml')
& 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makepri.exe' dump /if $pri /of $dump | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect packaged client resources.' }
[xml]$resources = Get-Content -Raw -LiteralPath $dump
$primary = @($resources.PriInfo.ResourceMap | Where-Object primary -eq 'true')
if ($primary.Count -ne 1 -or $primary[0].name -ne $manifest.Package.Identity.Name) {
    throw 'Resource identity does not match the installed package identity.'
}
foreach ($name in @('App.xbf','MainWindow.xbf')) {
    if (!($resources.SelectNodes('//NamedResource') | Where-Object name -eq $name)) {
        throw "Packaged client is missing compiled UI resource $name."
    }
}
Write-Output 'PASS: package identity and compiled client resources agree.'
