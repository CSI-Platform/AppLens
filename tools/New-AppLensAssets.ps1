[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$assets=Join-Path $PSScriptRoot '..\src\AppLens.Desktop\Assets'
$assets=[IO.Path]::GetFullPath($assets)
# Original AppLens geometric mark. Existing CSI artwork is preserved.
function New-Mark([int]$width,[int]$height,[string]$name) {
    $bitmap=[Drawing.Bitmap]::new($width,$height)
    $graphics=[Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([Drawing.Color]::FromArgb(241,242,244))
        $size=[Math]::Min($width,$height)*0.72
        $left=($width-$size)/2
        $top=($height-$size)/2
        $rect=[Drawing.RectangleF]::new($left,$top,$size,$size)
        $silver=[Drawing.Drawing2D.LinearGradientBrush]::new($rect,[Drawing.Color]::FromArgb(249,250,251),[Drawing.Color]::FromArgb(164,174,186),90)
        $ink=[Drawing.Pen]::new([Drawing.Color]::FromArgb(25,27,30),[single]($size*0.08))
        $edge=[Drawing.Pen]::new([Drawing.Color]::FromArgb(110,120,132),[single]($size*0.025))
        try {
            $graphics.FillRectangle($silver,$rect)
            $graphics.DrawRectangle($edge,[single]$left,[single]$top,[single]$size,[single]$size)
            $points=[Drawing.PointF[]]@(
                [Drawing.PointF]::new($left+$size*0.22,$top+$size*0.77),
                [Drawing.PointF]::new($left+$size*0.5,$top+$size*0.22),
                [Drawing.PointF]::new($left+$size*0.78,$top+$size*0.77)
            )
            $graphics.DrawLines($ink,$points)
            $graphics.DrawLine($ink,[single]($left+$size*0.35),[single]($top+$size*0.58),[single]($left+$size*0.65),[single]($top+$size*0.58))
        } finally { $silver.Dispose(); $ink.Dispose(); $edge.Dispose() }
        $path=Join-Path $assets $name
        if(Test-Path -LiteralPath $path) { throw "Asset already exists: $name" }
        $bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose(); $bitmap.Dispose() }
}
New-Mark 88 88 'AppLensSquare44.scale-200.png'
New-Mark 300 300 'AppLensSquare150.scale-200.png'
New-Mark 620 300 'AppLensWide.scale-200.png'
New-Mark 1240 600 'AppLensSplash.scale-200.png'
New-Mark 50 50 'AppLensStore.png'
$iconPath=Join-Path $assets 'AppLens.ico'
if(Test-Path -LiteralPath $iconPath) { throw 'Icon already exists.' }
$png=[IO.File]::ReadAllBytes((Join-Path $assets 'AppLensSquare44.scale-200.png'))
$writer=[IO.BinaryWriter]::new([IO.File]::Create($iconPath))
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
    $writer.Write([byte]88); $writer.Write([byte]88); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$png.Length); $writer.Write([uint32]22)
    $writer.Write($png)
} finally { $writer.Dispose() }
