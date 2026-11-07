# String flags in pictures, on this machine's GPU (2026-11-07): Unity 5.6 hangs a span between
# two posts with the mod's own StringSpan / StringMotion / StringAtlas and draws it with the REAL
# FlagString and FlagPole shaders (UnityProject\Assets\Editor\StringPreview.cs); this script lays
# the frames out as a labelled sheet in tools\preview\out\<Name>.png. Each scene is one frame:
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\preview\string-render.ps1 -Scenes "shape=pennant;wind=6|seq=countries;mount=top;w=0.6;h=0.4"
#
# A scene's keys (all optional): span, rise, height (m: the level span, the far end's rise, the
# near end's height), sag (0.03), shape (rectangle, swallowtail, swallowtailandtongue, pennant,
# burgee, pointed, rounded), depth, mount (hoist / top), w, h, gap, margin (m), seq (same / alt /
# countries), colours ("#c8341f,#f2f2f2,#2a4f8f"), pattern (n), which (all / region / list),
# region (0..5), list ("fr,de,jp"), order (az / region / random / picked), lead ("ca"), every,
# kind (cord / tape / rope / wire), thick (mm), colour ("#hex"), wind (m/s), windyaw (deg from
# the span towards the camera's far side: 90 across), t (s), motion (waves / still / flat),
# motions, swing, wavy, view (side / three / close), dist.
param(
    [string]$Unity = "C:\Program Files\Unity\Editor\Unity.exe",
    [string]$Scenes = "",
    [int]$Width = 480,
    [int]$Height = 300,
    [int]$Columns = 2,
    [string]$Name = "strings"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$here = $PSScriptRoot
$root = Resolve-Path (Join-Path $here "..\..")
$project = Join-Path $root "UnityProject"
$out = Join-Path $here "out"
$frames = Join-Path $out "string-frames"
$log = Join-Path $project "string.log"
New-Item -ItemType Directory -Force $out, $frames | Out-Null
Get-ChildItem $frames -Filter "*.png" | Remove-Item -Force
if (-not (Test-Path $Unity)) { throw "Unity not found at $Unity" }

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "tools\sync-sim.ps1")

$env:RF_OUT = $frames
$env:RF_W = "$Width"
$env:RF_H = "$Height"
$env:RF_SCENES = $Scenes

if (Test-Path $log) { Remove-Item $log -Force }
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $Unity -WindowStyle Hidden -Wait -PassThru -ArgumentList @(
    "-batchmode", "-quit",
    "-projectPath", "`"$project`"",
    "-executeMethod", "StringPreview.Render",
    "-logFile", "`"$log`"")
"Unity ran for $([int]$sw.Elapsed.TotalSeconds) s, exit code $($p.ExitCode)"

$lines = Get-Content $log
$lines | Where-Object { $_ -match "RF PREVIEW|RF FRAME|Shader error|Shader warning|error CS" } | ForEach-Object { $_ }
if ($p.ExitCode -ne 0 -or -not ($lines | Where-Object { $_ -eq "RF PREVIEW OK" })) {
    $lines | Select-Object -Last 40
    throw "the string preview did not finish; see $log"
}

$cells = @($lines | Where-Object { $_ -match "^RF FRAME " } | ForEach-Object {
    $m = [regex]::Match($_, "^RF FRAME row=(\d+) col=(\d+) t=([\d.]+) wind=([\d.]+) pattern=(\d+) file=(.+?) \| (.*)$")
    @{ Row = [int]$m.Groups[1].Value; File = $m.Groups[6].Value; Info = $m.Groups[7].Value }
} | Sort-Object { $_.Row })
if ($cells.Count -eq 0) { throw "no frames were rendered" }

$perRow = [Math]::Max(1, [Math]::Min($Columns, $cells.Count))
$rows = [int][Math]::Ceiling($cells.Count / [double]$perRow)
$labelH = 28
$sheet = New-Object System.Drawing.Bitmap ([int]($Width * $perRow)), ([int](($Height + $labelH) * $rows))
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::Black)
$font = New-Object System.Drawing.Font "Segoe UI", 7
$brush = [System.Drawing.Brushes]::White
for ($i = 0; $i -lt $cells.Count; $i++) {
    $f = $cells[$i]
    $r = [int][Math]::Floor($i / $perRow); $c = $i % $perRow
    $img = [System.Drawing.Image]::FromFile($f.File)
    $g.DrawImage($img, $c * $Width, $r * ($Height + $labelH) + $labelH, $Width, $Height)
    $img.Dispose()
    $rect = New-Object System.Drawing.RectangleF ([float]($c * $Width + 2)), ([float]($r * ($Height + $labelH) + 1)), ([float]($Width - 4)), ([float]$labelH)
    $g.DrawString($f.Info, $font, $brush, $rect)
}
$g.Dispose()
$file = Join-Path $out ("{0}.png" -f $Name)
$sheet.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()
"saved $file"
