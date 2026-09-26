# Offline checks of the language files against the code, no game needed (Volumetric Weather's
# check, for this mod):
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\test-localization.ps1
#
# LocalizationFile.cs has no Unity dependency, so its SOURCE is compiled here and reads the
# real Localization\*.xml. Then:
#   - every file parses, with no key written twice and every {n} well-formed;
#   - English has every key the code asks for: the rows of SettingsCatalog.cs (Label, Tooltip,
#     Note, Confirm, Group, a paired button's Id2 -- read from the source, as the in-game check
#     reads them from the built rows; the design rows AddDesign makes for both tabs are read
#     from its calls), the tab names, and every key-shaped string literal in *.cs (the patterns'
#     names are literals in FlagDesign.cs);
#   - English has no key nothing asks for (dead text a translator would translate for nothing);
#   - a translation has no key English lacks, and says which English keys it has not done yet;
#   - wrapped texts come out as one line, \n as a real break.
# The same key check runs in the game at startup (SettingsCatalog.LogLayout), from the real rows.

$root = Join-Path $PSScriptRoot "..\RealFlag"
$cp = New-Object System.CodeDom.Compiler.CompilerParameters
[void]$cp.ReferencedAssemblies.Add("System.dll")
[void]$cp.ReferencedAssemblies.Add("System.Xml.dll")
# In memory: Windows' application control can block a freshly compiled DLL loaded by path.
$cp.GenerateInMemory = $true
Add-Type -TypeDefinition (Get-Content (Join-Path $root "LocalizationFile.cs") -Raw) -CompilerParameters $cp

$failed = 0
function Check([string]$name, $actual, $expected) {
    if ("$actual" -ceq "$expected") { Write-Host "ok    $name" }
    else { Write-Host "FAIL  $name  ->  [$actual]  expected [$expected]"; $script:failed++ }
}

function Load([string]$path) {
    $code = ''; $name = ''
    $dups = New-Object 'System.Collections.Generic.List[string]'
    $table = [RealFlag.LocalizationFile]::Parse([IO.File]::ReadAllText($path), [ref]$code, [ref]$name, $dups)
    return @{ Table = $table; Code = $code; Name = $name; Duplicates = $dups }
}

$en = Load (Join-Path $root "Localization\en.xml")
Check "en.xml: code" $en.Code "en"
Check "en.xml: no key twice" ($en.Duplicates -join ", ") ""

# ---- what the code needs ------------------------------------------------------------------

$needed = New-Object 'System.Collections.Generic.HashSet[string]'

# Setting property -> its name, from Settings.cs.
$settingsSrc = Get-Content (Join-Path $root "Settings.cs") -Raw
$nameOf = @{}
foreach ($m in [regex]::Matches($settingsSrc, '(\w+) = new (?:Float|Bool|Int)Setting\("(\w+)"')) { $nameOf[$m.Groups[1].Value] = $m.Groups[2].Value }
foreach ($m in [regex]::Matches($settingsSrc, '(\w+) = new SavedInputKey\("(\w+)"')) { $nameOf[$m.Groups[1].Value] = $m.Groups[2].Value }

# The rows, one block per "Add(new Row". AddDesign's own rows take their settings as parameters:
# those are skipped here and read from its calls below.
$catalog = Get-Content (Join-Path $root "UI\SettingsCatalog.cs") -Raw
$blocks = [regex]::Split($catalog, 'Add\(new Row') | Select-Object -Skip 1
$rows = 0
foreach ($b in $blocks) {
    $b = $b.Substring(0, $b.IndexOf('});'))
    $rows++
    $kind = if ($b -match 'Kind = RowKind\.(\w+)') { $Matches[1] } else { '?' }
    $name = $null
    if ($b -match '(?:Float|Bool|Int|Key) = Settings\.(\w+)') { $name = $nameOf[$Matches[1]] }
    if ($b -match 'Id = "(\w+)"') { $name = $Matches[1] }
    if (-not $name) {
        # AddDesign's and AddPole's rows, and AddPoleButton's: read from their calls below.
        if ($b -match '(?:Float|Int) = (country|pattern|c1|c2|c3|length|proportion|waviness|pole|ropes|poleColour|topColour|ropeColour|thickness|tilt|lean)\b') { continue }
        if ($b -match 'Id = id,') { continue }
        Write-Host "FAIL  a row with neither a setting nor an Id:`n$b"; $failed++; continue
    }
    $shown = ($b -match 'Panel = (PanelPage\.|page\b)') -or ($b -match 'Options = OptionsPage\.')

    if ($kind -ne 'Status' -and $kind -ne 'FlagList') {
        [void]$needed.Add("$name.Label")
        if ($shown) { [void]$needed.Add("$name.Tooltip") }
    }
    if ($b -match 'Id2 = "(\w+)"') { [void]$needed.Add($Matches[1] + ".Label"); [void]$needed.Add($Matches[1] + ".Tooltip") }
    if ($b -match 'HasNote = true') { [void]$needed.Add("$name.Note") }
    if ($b -match 'Confirm = true') { [void]$needed.Add("$name.Confirm") }
    if ($b -match 'Group = "(\w+)"') { [void]$needed.Add("Group." + $Matches[1]) }
}

# The design rows, from each AddDesign(PanelPage.X, Settings.A, ...) call: every setting passed is a row.
$designCalls = [regex]::Matches($catalog, 'AddDesign\(PanelPage\.\w+,([^;]*?)\);')
foreach ($call in $designCalls) {
    foreach ($m in [regex]::Matches($call.Groups[1].Value, 'Settings\.(\w+)')) {
        $name = $nameOf[$m.Groups[1].Value]
        if ($name) { [void]$needed.Add("$name.Label"); [void]$needed.Add("$name.Tooltip") }
    }
}
Check "the design rows are read from both tabs' AddDesign calls" $designCalls.Count 2

# The Flagpole popup's rows, from each AddPole(PanelPage.X, Settings.A, ...) call; each tab's
# Flagpole button, from its AddPoleButton(..., "Id", ...) call.
$poleCalls = [regex]::Matches($catalog, 'AddPole\(PanelPage\.\w+,([^;]*?)\);')
foreach ($call in $poleCalls) {
    foreach ($m in [regex]::Matches($call.Groups[1].Value, 'Settings\.(\w+)')) {
        $name = $nameOf[$m.Groups[1].Value]
        if ($name) { [void]$needed.Add("$name.Label"); [void]$needed.Add("$name.Tooltip") }
    }
}
Check "the pole rows are read from both tabs' AddPole calls" $poleCalls.Count 2
$buttonCalls = [regex]::Matches($catalog, 'AddPoleButton\(PanelPage\.\w+, PanelPage\.\w+, "(\w+)"')
foreach ($call in $buttonCalls) { [void]$needed.Add($call.Groups[1].Value + ".Label"); [void]$needed.Add($call.Groups[1].Value + ".Tooltip") }
Check "the Flagpole buttons are read from both tabs' AddPoleButton calls" $buttonCalls.Count 2
Write-Host "      ($rows rows in the catalog, 8 of them made twice by AddDesign, 8 by AddPole, 1 by AddPoleButton)"

foreach ($enum in @('PanelPage')) {
    if ($catalog -match "public enum $enum \{([^}]*)\}") {
        foreach ($v in ($Matches[1] -split ',')) { $v = $v.Trim(); if ($v -and $v -ne 'None') { [void]$needed.Add("Tab.$v") } }
    }
}

# Key-shaped literals anywhere in the code ("Status.Flags", "Pattern.Disc"...), including the
# ones picked by a ?: that a Localization.Get("...") search would miss.
$prefixes = 'Mod|Tab|Key|Status|Unit|File|Language|Group|ResetAll|Pattern|WindMode|Existing|Flag|Tool|Picker|Prop|Colours|DeleteFlag|ClothDetail|Country|Gizmo|PoleButton|Pole|Ropes'
foreach ($file in Get-ChildItem $root -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }) {
    foreach ($m in [regex]::Matches((Get-Content $file.FullName -Raw), "`"((?:$prefixes)\.[A-Z][A-Za-z.]*[A-Za-z])`"")) {
        [void]$needed.Add($m.Groups[1].Value)
    }
}

$missing = @($needed | Where-Object { -not $en.Table.ContainsKey($_) } | Sort-Object)
Check "English has every key the code needs ($($needed.Count))" ($missing -join ", ") ""

$unused = @($en.Table.Keys | Where-Object { -not $needed.Contains($_) } | Sort-Object)
Check "English has no key nothing uses" ($unused -join ", ") ""

# ---- every text ---------------------------------------------------------------------------

$bad = @()
$dummy = [object[]]@(0..9 | ForEach-Object { "#$_" })
foreach ($key in $en.Table.Keys) {
    $text = $en.Table[$key]
    if ([string]::IsNullOrEmpty($text)) { $bad += "$key (empty)"; continue }
    try { [void][string]::Format($text, $dummy) } catch { $bad += "$key (bad {n}: $text)" }
    if ($text -match '  \S') { $bad += "$key (double space)" }
}
Check "every English text is non-empty with well-formed {n}" ($bad -join "; ") ""

Check "a wrapped text is one line" ($en.Table["Mod.Unsupported"] -match "shows none in this city\. Your flags stay") "True"
Check "\n is a real break, no stray spaces" ($en.Table["Mod.Unsupported"] -match "in its saves\.`n`nThe mod needs") "True"
$header = $en.Table["File.Header"] -split "`n"
Check "the settings file header: 12 lines, blank second line" ("" + $header.Count + "," + ($header[1] -eq "")) "12,True"

# ---- translations -------------------------------------------------------------------------

foreach ($file in Get-ChildItem (Join-Path $root "Localization") -Filter *.xml | Where-Object { $_.Name -ne 'en.xml' }) {
    $t = Load $file.FullName
    $code = [IO.Path]::GetFileNameWithoutExtension($file.Name)
    Check "$($file.Name): code matches the file name" $t.Code $code
    Check "$($file.Name): no key twice" ($t.Duplicates -join ", ") ""
    $extra = @($t.Table.Keys | Where-Object { -not $en.Table.ContainsKey($_) } | Sort-Object)
    Check "$($file.Name): no key English lacks" ($extra -join ", ") ""
    $todo = @($en.Table.Keys | Where-Object { -not $t.Table.ContainsKey($_) }).Count
    Write-Host "      $($file.Name): $($t.Table.Count) texts, $todo still in English"
}

Write-Host ""
if ($failed -eq 0) { Write-Host "all passed" } else { Write-Host "$failed FAILED"; exit 1 }
