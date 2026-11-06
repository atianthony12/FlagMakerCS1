# Offline check of what LineFlags binds against the REAL Line Tool DLL from the Workshop, no game
# needed: every public member the New flag window's Place row drives, and the private list and
# fields the preview reads. Nothing references Line Tool (Invariant 11), so the compiler cannot
# tell if an update renames one: in the game the rows would just stay hidden, or the preview go.
#
#   dotnet build RealFlag/RealFlag.csproj -p:Deploy=false
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-linetool.ps1
#
# Without the Workshop item (not subscribed) it says so and passes: Line Tool is optional.
# Not covered, because it needs the game: the tool actually switching, the rows' look, the preview.

param(
    [string]$Dll = (Join-Path $PSScriptRoot "..\RealFlag\obj\Debug\RealFlag.dll"),
    [string]$LineTool = "C:\Program Files (x86)\Steam\steamapps\workshop\content\255710\2956939040\LineTool.dll"
)

$managed = "C:\Program Files (x86)\Steam\steamapps\common\Cities_Skylines\Cities_Data\Managed"

if (-not (Test-Path $LineTool)) {
    Write-Host "skip  Line Tool is not subscribed ($LineTool)"
    exit 0
}

# Loaded from bytes: Smart App Control can block a fresh unsigned DLL by path.
Add-Type -TypeDefinition @"
using System;
using System.IO;
using System.Reflection;
public static class LineToolResolver
{
    public static string[] Dirs;
    public static void Install() { AppDomain.CurrentDomain.AssemblyResolve += Resolve; }
    static Assembly Resolve(object sender, ResolveEventArgs e)
    {
        string name = new AssemblyName(e.Name).Name;
        foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
            if (loaded.GetName().Name == name) return loaded;
        foreach (string dir in Dirs)
        {
            string path = Path.Combine(dir, name + ".dll");
            if (File.Exists(path)) return Assembly.Load(File.ReadAllBytes(path));
        }
        return null;
    }
}
"@

[LineToolResolver]::Dirs = @($managed, (Split-Path -Parent (Resolve-Path $Dll)), (Split-Path -Parent $LineTool))
[LineToolResolver]::Install()

$failed = 0
function Check([string]$name, $actual, $expected) {
    if ("$actual" -ceq "$expected") { Write-Host "ok    $name" }
    else { Write-Host "FAIL  $name  ->  [$actual]  expected [$expected]"; $script:failed++ }
}

$mod = [System.Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Resolve-Path $Dll)))
$line = [System.Reflection.Assembly]::Load([IO.File]::ReadAllBytes($LineTool))
$flags = $mod.GetType("RealFlag.Flags.LineFlags", $true)

Check "Line Tool's version the code was read from" $line.GetName().Version.ToString() "0.7.1.0"
Check "the public members the Place row drives" $flags.GetMethod("Missing").Invoke($null, @(, $line)) ""
Check "the fields the preview reads" $flags.GetMethod("MissingForPreview").Invoke($null, @(, $line)) ""
Check "an assembly without Line Tool: the rows stay hidden" $flags.GetMethod("Missing").Invoke($null, @(, $mod)) "LineToolMod.LineTool"

$count = $flags.GetField("Values").GetValue($null).Length
Check "a key for each choice" $flags.GetField("NameKeys").GetValue($null).Length $count
Check "an icon for each choice" $flags.GetField("Icons").GetValue($null).Length $count

$sheet = Get-Content (Join-Path $PSScriptRoot "..\RealFlag\Resources\PanelIcons.txt")
foreach ($icon in $flags.GetField("Icons").GetValue($null)) {
    Check "$icon is on the sheet" (@($sheet | Where-Object { $_ -like "$icon *" }).Count) 1
}

if ($failed -gt 0) { Write-Host "$failed FAILED"; exit 1 }
Write-Host "all passed"
