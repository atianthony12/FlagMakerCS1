# Rebuilds the cloth shader AssetBundles with Unity 5.6 -- one per platform -- checks what each
# one holds, and drops them where the mod embeds them. Run this after editing anything under
# UnityProject\Assets, then rebuild the mod.
#
#   .\build-bundle.ps1
#   .\build-bundle.ps1 -Only win       # just the Windows bundle; linux/mac left as they are
#
# What it checks, and why (Volumetric Weather's and Volumetric Explosions' lessons):
# - Unity's log for "Shader error" / "Shader warning": a shader that fails to compile still
#   produces a bundle (containing a broken shader) and exit code 0.
# - tools\bundle-apis.ps1 on EACH BUNDLE THAT SHIPS (they are built uncompressed for that
#   reason): every shader named in BundleBuilder.cs must be in it, with real code for every
#   graphics API of its platform. Unity leaves an EMPTY 4-byte blob, no message, exit 0, for
#   an API it cannot compile a shader for (Metal at `#pragma target 4.0`), and a bundle
#   without the API the game runs is a stand-down in every city.
# - Each bundle's hash before and after, printed: UNCHANGED after a shader edit is a problem,
#   UNCHANGED without one is expected (bundle builds are deterministic).
# - What each bundle was built from (tools\bundle-sources.ps1's hash), in
#   RealFlag\Resources\bundles-built-from.txt: release.ps1 refuses a bundle built from older sources.

param(
    [string]$Unity = "C:\Program Files\Unity\Editor\Unity.exe",
    # Build only these bundles, e.g. -Only win (the others in Resources are left as they are).
    [string[]]$Only = @()
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "UnityProject"
$log = Join-Path $project "build.log"
$resources = Join-Path $root "RealFlag\Resources"
$apisTool = Join-Path $root "tools\bundle-apis.ps1"
$builder = Join-Path $project "Assets\Editor\BundleBuilder.cs"

# One row per bundle: its name, and the platform ids every shader in it must carry
# (1 D3D9, 4 D3D11, 14 Metal, 15 OpenGLCore, 18 Vulkan). Must match BundleBuilder.Targets.
$allTargets = @(
    @{ Name = "win";   Platforms = "4,15";  Needs = "Direct3D 11 + OpenGL Core" },
    @{ Name = "linux"; Platforms = "15";    Needs = "OpenGL Core" },
    @{ Name = "mac";   Platforms = "14,15"; Needs = "Metal + OpenGL Core" }
)
$targets = $allTargets
if ($Only.Count -gt 0) {
    $Only = @($Only | ForEach-Object { $_ -split "," } | Where-Object { $_ })
    foreach ($o in $Only) {
        if (-not ($targets | Where-Object { $_.Name -eq $o })) { throw "Unknown bundle '$o' (win, linux, mac)" }
    }
    $targets = @($targets | Where-Object { $Only -contains $_.Name })
}

if (-not (Test-Path $Unity)) { throw "Unity not found at $Unity" }
if (-not (Test-Path $apisTool)) { throw "Missing $apisTool" }
if (-not (Test-Path $builder)) { throw "Missing $builder" }
$stampFile = Join-Path $resources "bundles-built-from.txt"
$sourcesTool = Join-Path $root "tools\bundle-sources.ps1"
if (-not (Test-Path $sourcesTool)) { throw "Missing $sourcesTool" }

# The sources' hash BEFORE the build: what the bundles are built from (an edit saved during the build
# would make this stamp older than the sources, which the next release check would catch).
$sourcesHash = & powershell -NoProfile -ExecutionPolicy Bypass -File $sourcesTool
if ($sourcesHash -notmatch '^[0-9a-f]{64}$') { throw "tools\bundle-sources.ps1 gave no hash: $sourcesHash" }

# The shaders to expect: read out of BundleBuilder.cs, the one list.
$shaders = @([regex]::Matches((Get-Content $builder -Raw), '"Assets/RealFlag/(\w+)\.shader"') |
    ForEach-Object { "RealFlag/" + $_.Groups[1].Value })
if ($shaders.Count -eq 0) { throw "No shader names found in $builder" }
Write-Host ("Expecting {0} shaders: {1}" -f $shaders.Count, ($shaders -join ", "))

New-Item -ItemType Directory -Force $resources | Out-Null
$before = @{}
foreach ($t in $targets) {
    $f = Join-Path $resources ("realflag-" + $t.Name + ".bundle")
    $before[$t.Name] = if (Test-Path $f) { (Get-FileHash $f -Algorithm SHA256).Hash } else { $null }
}

if (Test-Path $log) { Remove-Item $log -Force }

# The editor compiles every script under Assets first, the GPU preview's copies of the cloth
# included: refresh them, or a stale copy fails the build.
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $root "tools\sync-sim.ps1")

# A surface shader that does not compile ships as a silent one-pass stand-in (no "Shader error"
# in the log): Assets\Editor\ShaderCheck.cs reads the errors Unity keeps on the asset.
$checkLog = Join-Path $project "check.log"
if (Test-Path $checkLog) { Remove-Item $checkLog -Force }
Write-Host "Checking the shaders compile (ShaderCheck)..."
$c = Start-Process -FilePath $Unity -WindowStyle Hidden -Wait -PassThru -ArgumentList @(
    "-batchmode", "-nographics", "-quit",
    "-projectPath", "`"$project`"",
    "-executeMethod", "ShaderCheck.Run",
    "-logFile", "`"$checkLog`"")
Select-String -Path $checkLog -Pattern "SHADER CHECK" -SimpleMatch | ForEach-Object { Write-Host $_.Line }
if ($c.ExitCode -ne 0 -or -not (Select-String -Path $checkLog -Pattern "SHADER CHECK OK" -SimpleMatch -Quiet)) {
    throw "A shader does not compile (see above and $checkLog); nothing built."
}

$names = ($targets | ForEach-Object { $_.Name }) -join ","
Write-Host "Building the bundles ($names) with Unity (headless)..."
$unityArgs = @(
    "-batchmode", "-nographics", "-quit",
    "-projectPath", "`"$project`"",
    "-executeMethod", "BundleBuilder.Build",
    "-logFile", "`"$log`"")
if ($Only.Count -gt 0) { $unityArgs += @("-bundleTargets", $names) }
$p = Start-Process -FilePath $Unity -WindowStyle Hidden -Wait -PassThru -ArgumentList $unityArgs

if ($p.ExitCode -ne 0) {
    Get-Content $log -Tail 25
    throw "Unity exited with code $($p.ExitCode). See $log"
}

$shaderProblems = Select-String -Path $log -Pattern "Shader error", "Shader warning" -SimpleMatch
if ($shaderProblems) {
    $shaderProblems | ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
    throw "A shader compiled with errors or warnings; nothing copied."
}

foreach ($t in $targets) {
    $marker = "BUNDLE BUILD OK: " + $t.Name
    if (-not (Select-String -Path $log -Pattern $marker -SimpleMatch -Quiet)) {
        throw "Marker '$marker' missing from the log. See $log"
    }
}

Write-Host "Checking what each bundle holds..."
foreach ($t in $targets) {
    $built = Join-Path $project ("Bundles\" + $t.Name + "\realflag")
    & powershell -NoProfile -ExecutionPolicy Bypass -File $apisTool -Bundle $built -Expect $t.Platforms -Shaders ($shaders -join ",")
    if ($LASTEXITCODE -ne 0) {
        throw ("The " + $t.Name + " bundle does not hold " + $t.Needs + " for every shader; nothing copied.")
    }
}

foreach ($t in $targets) {
    $src = Join-Path $project ("Bundles\" + $t.Name + "\realflag")
    $dst = Join-Path $resources ("realflag-" + $t.Name + ".bundle")
    Copy-Item $src $dst -Force
    $hash = (Get-FileHash $dst -Algorithm SHA256).Hash
    $state = if ($null -eq $before[$t.Name]) { "new" } elseif ($before[$t.Name] -eq $hash) { "UNCHANGED" } else { "changed" }
    Write-Host ("OK: {0} ({1} bytes, {2}) sha256 {3}" -f $dst, (Get-Item $dst).Length, $state, $hash) -ForegroundColor Green
}

# What each bundle was built from: this build's platforms get the sources' hash, the others keep theirs.
$stamps = [ordered]@{}
foreach ($t in $allTargets) { $stamps[$t.Name] = "(never recorded)" }
if (Test-Path $stampFile) {
    foreach ($line in Get-Content $stampFile) {
        if ($line -match '^(win|linux|mac)\s+(\S+)') { $stamps[$Matches[1]] = $Matches[2] }
    }
}
foreach ($t in $targets) { $stamps[$t.Name] = $sourcesHash }
$lines = @("# Written by build-bundle.ps1: per bundle, tools\bundle-sources.ps1's hash of the sources it was built from.",
           "# release.ps1 refuses a release unless every line matches the sources (build-bundle.ps1, no -Only).")
foreach ($k in $stamps.Keys) { $lines += "$k $($stamps[$k])" }
Set-Content -Path $stampFile -Value $lines -Encoding ASCII

foreach ($t in $allTargets) {
    if ($stamps[$t.Name] -ne $sourcesHash) {
        Write-Host ("NOTE: the {0} bundle was not built from these sources (left as it was): build-bundle.ps1 with no -Only before a release." -f $t.Name) -ForegroundColor Yellow
    }
}

Write-Host "Now rebuild the mod to embed them."
