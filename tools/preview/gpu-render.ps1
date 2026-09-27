# Renders flags with the REAL cloth shader on this machine's GPU: Unity 5.6 (the editor that
# builds the bundles) steps the mod's own cloth and draws it through the same Graphics.DrawMesh +
# property block path the mod uses, in the game's legacy deferred lighting with a shadowing sun
# (UnityProject\Assets\Editor\FlagPreview.cs), and this script lays the frames out as a labelled
# contact sheet in tools\preview\out\<Name>.png.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\preview\gpu-render.ps1
#   ... -Winds "0,2,4,8,16"        one row per wind speed (m/s at 10 m, the panel's)
#   ... -Patterns "0,1,2,3"        times one row per pattern value (FlagDesign.cs)
#   ... -Times "4,8,12"            one column per moment (seconds after the flag appears)
#   ... -Seq "12,0.05"             12 frames 0.05 s apart from the first -Times: the motion
#   ... -Night                     moonlight and a street lamp
#   ... -SunYaw 180                the sun behind the flag (the light through the cloth)
#   ... -Path forward              the forward path instead of the game's legacy deferred
#   ... -Bench 600                 no pictures: the cloth's cost per step in Unity's Mono
#   ... -Details "18,32"           one row per cloth detail (cells along; times the winds and patterns)
#   ... -Refines "1,4"             one row per drawn refinement (FlagMesh.Refine: drawn cells a cloth cell)
#   ... -NoSmooth                  the cloth's edges drawn as they are (ClothSurface.SmoothEdges off)
#   ... -NoCollide                 self-collision off (on in the game by default)
#   ... -Countries "fr,us,np"      countries' flags (their images) instead of -Patterns
#   ... -Darkness "0,20,40"        each design once per cloth darkness (percent, the panel's)
#   ... -Sun 2.7                   the sun's intensity by day (1.3; Render It!'s "Sunny Default" has 2.7)
#   ... -Wet 1                     the game's ground wetness (_WeatherParams.w, 0..1): the cloth wet in rain
#   ... -Pole 1 -Ropes 2 -Thick 1 -PoleColours "#hex,#hex,#hex"   the mod's pole (1 ball, 2 flat, 3 spear)
#   ... -Tilt 45 -Lean 0           the hoist's lean, degrees (a wall-mounted pole: 30-45)
#   ... -Hoist top                 hoisted from its top: a banner hanging from a crossbar
#   ... -Images "a.png,b.jpg" -ImageDir <folder>   pictures of the player's, laid on -Patterns' first pattern
#   ... -Crops "0.5,0.5,1,0|0.3,0.5,2,1"           each picture once per crop: the middle x,y on it, zoom, quarter turns
#   ... -Through                   their see-through parts show the pattern (else cut out)
# The cloth and the sheet: -Length (m) -Proportion (height/length x 1000) -Waviness -Gusts
# -Elevation (m) -Colours "#hex,#hex,#hex"; the camera: -Yaw -Pitch -Dist (flag lengths away).

param(
    [int]$Width = 320,
    [int]$Height = 240,
    [string]$Winds = "6",
    [string]$Patterns = "5",
    [string]$Times = "8",
    [string]$Seq = "",
    [double]$Length = 3.0,
    [int]$Proportion = 667,
    [double]$Waviness = 1.0,
    [double]$Gusts = 1.0,
    [double]$Elevation = 10.0,
    [string]$Colours = "",
    [switch]$Night,
    [double]$SunYaw = 35,
    [ValidateSet("deferred", "forward")][string]$Path = "deferred",
    [double]$Yaw = 0,
    [double]$Pitch = 8,
    [double]$Dist = 2.6,
    [int]$Bench = 0,
    [string]$Details = "18",
    [string]$Refines = "1",
    [switch]$NoSmooth,
    [switch]$NoCollide,
    [string]$Countries = "",
    [string]$Darkness = "0",
    [double]$Sun = 1.3,
    [double]$Wet = 0,
    [int]$Pole = 0,
    [int]$Ropes = 2,
    [double]$Thick = 1.0,
    [string]$PoleColours = "",
    [double]$Tilt = 0,
    [double]$Lean = 0,
    [ValidateSet("side", "top")][string]$Hoist = "side",
    [string]$Images = "",
    [string]$ImageDir = (Join-Path $env:LOCALAPPDATA "Colossal Order\Cities_Skylines\RealFlag\Images"),
    [string]$Crops = "0.5,0.5,1,0",
    [switch]$Through,
    # Metres added to the point looked at (the flag's middle), "x,y,z": "-1.5,1,0" is a 3 x 2 m flag's anchor.
    [string]$Look = "0,0,0",
    # The way the wind blows, degrees from the flag's heading: 180 blows it back over its pole.
    [double]$WindYaw = 0,
    [int]$Columns = 0,
    # Fabric overrides per row, cycling: "A=0.5|A=0.8" (FlagPreview.Fabric lists the keys).
    [string]$Fabric = "",
    [string]$Name = "gpu",
    [string]$Unity = "C:\Program Files\Unity\Editor\Unity.exe"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$here = $PSScriptRoot
$root = Resolve-Path (Join-Path $here "..\..")
$project = Join-Path $root "UnityProject"
$out = Join-Path $here "out"
$frames = Join-Path $out "gpu-frames"
$log = Join-Path $project "gpu.log"
New-Item -ItemType Directory -Force $out, $frames | Out-Null
Get-ChildItem $frames -Filter "*.png" | Remove-Item -Force

if (-not (Test-Path $Unity)) { throw "Unity not found at $Unity" }

& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "tools\sync-sim.ps1")

$inv = [Globalization.CultureInfo]::InvariantCulture
$env:RF_OUT = $frames
$env:RF_W = "$Width"
$env:RF_H = "$Height"
$env:RF_WINDS = $Winds
$env:RF_PATTERNS = $Patterns
$env:RF_TIMES = $Times
$env:RF_SEQ = $Seq
$env:RF_LENGTH = $Length.ToString($inv)
$env:RF_PROPORTION = "$Proportion"
$env:RF_WAVINESS = $Waviness.ToString($inv)
$env:RF_GUSTS = $Gusts.ToString($inv)
$env:RF_ELEVATION = $Elevation.ToString($inv)
$env:RF_COLOURS = $Colours
$env:RF_NIGHT = if ($Night) { "1" } else { "0" }
$env:RF_SUNYAW = $SunYaw.ToString($inv)
$env:RF_PATH = $Path
$env:RF_YAW = $Yaw.ToString($inv)
$env:RF_PITCH = $Pitch.ToString($inv)
$env:RF_DIST = $Dist.ToString($inv)
$env:RF_BENCH = "$Bench"
$env:RF_DETAILS = $Details
$env:RF_REFINES = $Refines
$env:RF_SMOOTH = if ($NoSmooth) { "0" } else { "1" }
$env:RF_COLLIDE = if ($NoCollide) { "0" } else { "1" }
$env:RF_COUNTRIES = $Countries
$env:RF_DARKNESS = $Darkness
$env:RF_SUN = $Sun.ToString($inv)
$env:RF_WET = $Wet.ToString($inv)
$env:RF_POLE = "$Pole"
$env:RF_ROPES = "$Ropes"
$env:RF_THICK = $Thick.ToString($inv)
$env:RF_POLECOLOURS = $PoleColours
$env:RF_TILT = $Tilt.ToString($inv)
$env:RF_LEAN = $Lean.ToString($inv)
$env:RF_HOIST = $Hoist
$env:RF_IMAGES = $Images
$env:RF_IMAGEDIR = $ImageDir
$env:RF_CROPS = $Crops
$env:RF_THROUGH = if ($Through) { "1" } else { "0" }
$env:RF_LOOK = $Look
$env:RF_WINDYAW = $WindYaw.ToString($inv)
$env:RF_FABRIC = $Fabric

if (Test-Path $log) { Remove-Item $log -Force }
$sw = [Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $Unity -WindowStyle Hidden -Wait -PassThru -ArgumentList @(
    "-batchmode", "-quit",
    "-projectPath", "`"$project`"",
    "-executeMethod", "FlagPreview.Render",
    "-logFile", "`"$log`"")
"Unity ran for $([int]$sw.Elapsed.TotalSeconds) s, exit code $($p.ExitCode)"

$lines = Get-Content $log
$lines | Where-Object { $_ -match "RF PREVIEW|RF FRAME|RF BENCH|Shader error|Shader warning|error CS|RF PREVIEW FAILED" } | ForEach-Object { $_ }
if ($p.ExitCode -ne 0 -or -not ($lines | Where-Object { $_ -eq "RF PREVIEW OK" })) {
    $lines | Select-Object -Last 40
    throw "the GPU preview did not finish; see $log"
}
if ($Bench -gt 0) { return }

# The sheet: the frames in the order rendered, row by row; -Columns wraps a long row (a -Seq).
$frameLines = @($lines | Where-Object { $_ -match "^RF FRAME " } | ForEach-Object {
    $m = [regex]::Match($_, "^RF FRAME row=(\d+) col=(\d+) t=([\d.]+) wind=([\d.]+) pattern=(\d+) file=(.+?) \| (.*)$")
    @{ Row = [int]$m.Groups[1].Value; Col = [int]$m.Groups[2].Value; T = $m.Groups[3].Value; Wind = $m.Groups[4].Value;
       Pattern = $m.Groups[5].Value; File = $m.Groups[6].Value; Info = $m.Groups[7].Value }
})
if ($frameLines.Count -eq 0) { throw "no frames were rendered" }

$cells = @()
foreach ($f in ($frameLines | Sort-Object { $_.Row }, { $_.Col })) { $cells += $f }
$perRow = ($frameLines | ForEach-Object { $_.Col } | Measure-Object -Maximum).Maximum + 1
if ($Columns -gt 0) { $perRow = $Columns }
$rows = [int][Math]::Ceiling($cells.Count / [double]$perRow)
if ($Columns -le 0) { $rows = ($frameLines | ForEach-Object { $_.Row } | Sort-Object -Unique).Count }
$labelH = 16
$sheet = New-Object System.Drawing.Bitmap ([int]($Width * $perRow)), ([int](($Height + $labelH) * $rows))
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.Clear([System.Drawing.Color]::Black)
$font = New-Object System.Drawing.Font "Segoe UI", 8
$brush = [System.Drawing.Brushes]::White

for ($i = 0; $i -lt $cells.Count; $i++) {
    $f = $cells[$i]
    if ($Columns -gt 0) { $r = [int][Math]::Floor($i / $perRow); $c = $i % $perRow } else { $r = $f.Row; $c = $f.Col }
    $img = [System.Drawing.Image]::FromFile($f.File)
    $g.DrawImage($img, $c * $Width, $r * ($Height + $labelH) + $labelH, $Width, $Height)
    $img.Dispose()
    $droop = ($f.Info -replace ' \|.*$', '')
    $g.DrawString(("wind {0}  p{1}  t={2}s  {3}" -f $f.Wind, $f.Pattern, $f.T, $droop), $font, $brush, ($c * $Width + 2), ($r * ($Height + $labelH) + 1))
}
$g.Dispose()

$file = Join-Path $out ("{0}{1}.png" -f $Name, $(if ($Night) { "-night" } else { "" }))
$sheet.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
$sheet.Dispose()
"saved $file"
