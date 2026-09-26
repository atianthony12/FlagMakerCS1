# Copies the mod's shared sources -- the cloth, the wind, the designs, the mesh and its surface,
# the pole, the cloth's thread -- into UnityProject\Assets\Sim, where the editor compiles them for the GPU preview
# (Assets\Editor\FlagPreview.cs). Run by tools\preview\gpu-render.ps1 and by build-bundle.ps1:
# the editor compiles EVERY script under Assets before it does anything, so stale copies here
# break the bundle build with "Scripts have compiler errors". The folder is generated, and
# gitignored.

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$simDir = Join-Path $root "UnityProject\Assets\Sim"
New-Item -ItemType Directory -Force $simDir | Out-Null

$files = "Flags\FlagNoise.cs", "Flags\FlagWind.cs", "Flags\FlagCloth.cs", "Flags\FlagDesign.cs", "Flags\FlagMesh.cs", "Flags\ClothWorker.cs",
         "Flags\FlagCountry.cs", "Flags\FlagCountries.cs", "Flags\FlagImages.cs", "Flags\ClothSurface.cs", "Flags\FlagPole.cs",
         "Flags\FlagPoleMesh.cs"
$names = @()
foreach ($f in $files) {
    $name = Split-Path $f -Leaf
    $names += $name
    Copy-Item (Join-Path $root "RealFlag\$f") (Join-Path $simDir $name) -Force
}

# A source that was removed must not linger as a copy.
foreach ($stale in Get-ChildItem $simDir -Filter "*.cs") {
    if ($names -notcontains $stale.Name) { Remove-Item $stale.FullName -Force }
}
