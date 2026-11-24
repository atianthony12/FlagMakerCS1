# Offline check of the game-update safeguard (RealFlag/GameUpdate.cs on ModsLibCS1's GameCheck),
# run against the BUILT mod DLL and the INSTALLED game's DLLs, no game needed:
#   - Flag Maker's requirement list (GameUpdate.Require) finds everything in this game: 0 problems
#     (after a game update, this is the first thing to run: it names what changed);
#   - GAMECHECK_FAIL=Flag Maker adds exactly one problem (the forced switch-off for testing);
#   - GameCheck itself reports a missing member, a field of another type, changed parameter
#     names and a patch that throws, and undoes once.
#
#   dotnet build RealFlag/RealFlag.csproj -p:Deploy=false
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-gamecheck.ps1
#
# Not covered, because it needs the game: the switch-off itself (the pop-up, the Loader standing
# down, the save keys written back) -- see the to-see list in CLAUDE.md.

param(
    [string]$Dll = (Join-Path $PSScriptRoot "..\RealFlag\bin\Debug\FlagMaker.dll")
)

$ErrorActionPreference = "Stop"
$managed = "C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed"
$libs = Join-Path $PSScriptRoot "..\libs"

# Every assembly from its bytes (Smart App Control blocks LoadFrom of a fresh unsigned DLL), found by name.
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
public static class GcResolver
{
    public static string[] Dirs;
    static readonly Dictionary<string, Assembly> Loaded = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
    public static void Install() { AppDomain.CurrentDomain.AssemblyResolve += Resolve; }
    public static Assembly Load(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        Assembly a;
        if (Loaded.TryGetValue(name, out a)) return a;
        a = Assembly.Load(File.ReadAllBytes(path));
        Loaded[name] = a;
        return a;
    }
    static Assembly Resolve(object sender, ResolveEventArgs e)
    {
        string name = new AssemblyName(e.Name).Name;
        Assembly a;
        if (Loaded.TryGetValue(name, out a)) return a;
        foreach (string dir in Dirs)
        {
            string path = Path.Combine(dir, name + ".dll");
            if (File.Exists(path)) return Load(path);
        }
        return null;
    }
}
"@

[GcResolver]::Dirs = @($managed, (Split-Path $Dll), $libs)
[GcResolver]::Install()
foreach ($n in "Assembly-CSharp", "ColossalManaged", "ICities", "UnityEngine") { [void][GcResolver]::Load((Join-Path $managed "$n.dll")) }
$mod = [GcResolver]::Load((Resolve-Path $Dll).Path)

$checkType = $mod.GetType("ModsLibCS1.GameCheck", $true)
$update = $mod.GetType("RealFlag.GameUpdate", $true)
$require = $update.GetMethod("Require")
$asm = [GcResolver]::Load((Join-Path $managed "Assembly-CSharp.dll"))

$failed = 0
function Check([string]$what, [bool]$ok, [string]$detail) {
    if ($ok) { "ok    $what" } else { "FAIL  $what -- $detail"; $script:failed++ }
}

$script:lines = New-Object System.Collections.Generic.List[string]
$sink = [Action[string]] { param($s) $script:lines.Add($s) }
function NewCheck { $script:lines.Clear(); return [Activator]::CreateInstance($checkType, @("Flag Maker", $sink)) }

# 1. This game has everything.
$env:GAMECHECK_FAIL = $null
$c = NewCheck
$require.Invoke($null, @($c))
Check "the installed game has everything Flag Maker needs by name" (-not $c.Failed) ($script:lines -join " | ")

# 2. The forced failure: exactly one problem.
$env:GAMECHECK_FAIL = "flag maker"
$c = NewCheck
$require.Invoke($null, @($c))
$env:GAMECHECK_FAIL = $null
Check "GAMECHECK_FAIL=Flag Maker: one problem" ($c.Failed -and $c.Problems.Count -eq 1) ("$($c.Problems.Count): " + ($script:lines -join " | "))
$env:GAMECHECK_FAIL = "Another Mod"
$c = NewCheck
$require.Invoke($null, @($c))
$env:GAMECHECK_FAIL = $null
Check "GAMECHECK_FAIL naming another mod: none" (-not $c.Failed) ($script:lines -join " | ")

# 3. GameCheck's own reports.
$inst = [Reflection.BindingFlags]"Instance, Public, NonPublic"
$stat = [Reflection.BindingFlags]"Static, Public, NonPublic"
$propTool = $asm.GetType("PropTool", $true)
$renderManager = $asm.GetType("RenderManager", $true)

$c = NewCheck
[void]$c.RequireField($renderManager, "m_noSuchField", $stat, $null)
Check "a missing field is a problem" ($c.Problems.Count -eq 1) ""

$c = NewCheck
[void]$c.RequireField($renderManager, "m_renderables", $stat, [int])
Check "a field of another type is a problem" ($c.Problems.Count -eq 1 -and $script:lines[0] -match "is a") ($script:lines -join " | ")

$overlay = $propTool.GetMethod("RenderOverlay", $stat, $null, [Type[]]@($asm.GetType("RenderManager+CameraInfo", $true), $asm.GetType("PropInfo", $true),
            [UnityEngine.Vector3], [float], [float], [UnityEngine.Color]), $null)
$types = [Type[]]($overlay.GetParameters() | ForEach-Object { $_.ParameterType })
$c = NewCheck
[void]$c.RequireMethod($propTool, "RenderOverlay", $stat, $types, [string[]]@("cameraInfo", "info", "position", "scale", "angle", "color"))
Check "PropTool.RenderOverlay as the patch binds it" (-not $c.Failed) ($script:lines -join " | ")

$c = NewCheck
[void]$c.RequireMethod($propTool, "RenderOverlay", $stat, $types, [string[]]@("cameraInfo", "info", "position", "size"))
Check "a renamed parameter is a problem" ($c.Problems.Count -eq 1 -and $script:lines[0] -match "'scale' now, not 'size'") ($script:lines -join " | ")

$c = NewCheck
[void]$c.RequireMethod($propTool, "RenderOverlay", $stat, [Type[]]@([float]), [string[]]@())
Check "other parameter types are a problem" ($c.Problems.Count -eq 1) ($script:lines -join " | ")

$c = NewCheck
$ok1 = $c.Patch("Good", [Action] { })
$ok2 = $c.Patch("Bad", [Action] { throw (New-Object System.MissingMethodException "PropTool", "Gone") })
$ok3 = $c.Patch("After", [Action] { })
$script:undone = 0
$u1 = $c.UndoIfFailed([Action] { $script:undone++ })
$u2 = $c.UndoIfFailed([Action] { $script:undone++ })
Check "a patch that throws is a problem; none applied after it; undone once" ($ok1 -and -not $ok2 -and -not $ok3 -and $u1 -and $u2 -and $script:undone -eq 1 -and $c.Problems.Count -eq 1) ("$ok1 $ok2 $ok3 $u1 $u2 $($script:undone) | " + ($script:lines -join " | "))

$c = NewCheck
$script:undone = 0
Check "nothing failed: nothing undone" (-not $c.UndoIfFailed([Action] { $script:undone++ }) -and $script:undone -eq 0) ""

""
if ($failed -gt 0) { "$failed FAILED"; exit 1 } else { "all passed" }
