# Draws the mod's button icon, RealFlag\Resources\FlagIcon.png (128 x 128, transparent): a pole
# and a waving flag with a Nordic cross in the default colours. Run it again after changing it;
# the csproj embeds the file.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\make-icon.ps1

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot "..\RealFlag\Resources\FlagIcon.png"

$size = 128
$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# The cloth: a strip from the hoist (x = 30) to the fly (x = 116), its top and bottom edges
# waving, as a set of quads between wave samples.
function Edge([double]$x, [double]$y0) {
    $u = ($x - 30.0) / 86.0
    return $y0 + 7.0 * $u * [Math]::Sin(2.0 * [Math]::PI * (1.3 * $u - 0.1))
}

$navy = [System.Drawing.Color]::FromArgb(255, 0, 38, 100)
$white = [System.Drawing.Color]::FromArgb(255, 245, 245, 240)
$cloth = New-Object System.Drawing.Drawing2D.GraphicsPath
$top = @(); $bottom = @()
for ($x = 30; $x -le 116; $x += 2) {
    $top += New-Object System.Drawing.PointF ([single]$x), ([single](Edge $x 22))
    $bottom += New-Object System.Drawing.PointF ([single]$x), ([single](Edge $x 80))
}
[array]::Reverse($bottom)
$cloth.AddPolygon([System.Drawing.PointF[]]($top + $bottom))
$g.FillPath((New-Object System.Drawing.SolidBrush $navy), $cloth)

# The cross, clipped to the cloth, following its wave.
$g.SetClip($cloth)
$crossBrush = New-Object System.Drawing.SolidBrush $white
for ($x = 30; $x -le 116; $x += 1) {
    $y = (Edge $x 51) - 6
    $g.FillRectangle($crossBrush, [single]$x, [single]$y, 1.6, 12)
}
$cx = 30 + 0.375 * 86
$g.FillRectangle($crossBrush, [single]($cx - 6), 10, 12, 90)
$g.ResetClip()

# Shading bands across the cloth, where it folds.
for ($x = 30; $x -le 116; $x += 1) {
    $u = ($x - 30.0) / 86.0
    $shade = [int](70 * [Math]::Max(0.0, [Math]::Cos(2.0 * [Math]::PI * (1.3 * $u - 0.1))))
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb($shade, 0, 0, 0)), 1.2
    $g.DrawLine($pen, [single]$x, [single](Edge $x 22), [single]$x, [single](Edge $x 80))
}

# The pole, with its ball.
$poleBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 200, 200, 205))
$g.FillRectangle($poleBrush, 24, 14, 6, 108)
$g.FillEllipse($poleBrush, 21, 6, 12, 12)
$g.DrawRectangle((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(160, 60, 60, 70)), 1), 24, 14, 6, 108)

$g.Dispose()
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
"saved $out"
