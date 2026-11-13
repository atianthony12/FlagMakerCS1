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
#   - a translation has no key English lacks, keeps English's {n} and \n in every text, and says
#     which English keys it has not done yet;
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
# A row's tooltip is optional (Row.Tooltip: none without its key, 2026-11-11): a tooltip key is
# allowed for every row, required for none.
$optional = New-Object 'System.Collections.Generic.HashSet[string]'

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
        # Their headings are still read here.
        if ($b -match '(?:Float|Int) = (country|image|pattern|c1|c2|c3|length|proportion|hoistedFrom|motion|waviness|windShare|darkness|wear|tear|tearEdges|dirt|holes|cut|cutDepth|pole|height|ropes|poleColour|topColour|ropeColour|thickness|tilt|lean|profile|poleBase|shine|ropeThickness|position|rust|held)\b') {
            if ($b -match 'Group = "(\w+)"') { [void]$needed.Add("Group." + $Matches[1]) }
            continue
        }
        if ($b -match 'Id = (id|shapeId|widthId|heightId|recaptureId),') {
            if ($b -match 'Group = "(\w+)"') { [void]$needed.Add("Group." + $Matches[1]) }
            continue
        }
        Write-Host "FAIL  a row with neither a setting nor an Id:`n$b"; $failed++; continue
    }
    $shown = ($b -match 'Panel = (PanelPage\.|page\b)') -or ($b -match 'Options = OptionsPage\.')

    if ($kind -ne 'Status' -and $kind -ne 'FlagList') {
        [void]$needed.Add("$name.Label")
        if ($shown) { [void]$optional.Add("$name.Tooltip") }
    }
    if ($b -match 'Id2 = "(\w+)"') { [void]$needed.Add($Matches[1] + ".Label"); [void]$optional.Add($Matches[1] + ".Tooltip") }
    if ($b -match 'HasNote = true') { [void]$needed.Add("$name.Note") }
    if ($b -match 'Confirm = true') { [void]$needed.Add("$name.Confirm") }
    if ($b -match 'Group = "(\w+)"') { [void]$needed.Add("Group." + $Matches[1]) }
}

# A group given to a row once it is made (AddDesign's placement rows: Rows[first].Group = "Place").
foreach ($m in [regex]::Matches($catalog, '\]\.Group = "(\w+)"')) { [void]$needed.Add("Group." + $m.Groups[1].Value) }

# The design rows, from each AddDesign(PanelPage.X, "Shape", "Width", "Height", Settings.A, ...) call: every
# setting passed is a row -- but the length and proportion, which the width and height work through --
# and so is every name (the rows over no setting of their own: the shapes, the width and the height).
$designCalls = [regex]::Matches($catalog, 'AddDesign\(PanelPage\.\w+,([^;]*?)\);')
foreach ($call in $designCalls) {
    foreach ($m in [regex]::Matches($call.Groups[1].Value, 'Settings\.(\w+)')) {
        $name = $nameOf[$m.Groups[1].Value]
        if ($name -match '^(New|Sel)(Length|Proportion)$') { continue }
        if ($name) { [void]$needed.Add("$name.Label"); [void]$optional.Add("$name.Tooltip") }
    }
    foreach ($m in [regex]::Matches($call.Groups[1].Value, '"(\w+)"')) {
        [void]$needed.Add($m.Groups[1].Value + ".Label"); [void]$optional.Add($m.Groups[1].Value + ".Tooltip")
    }
}
Check "the design rows are read from the New flag window's and the Flags tab's AddDesign calls" $designCalls.Count 2

# The Flagpole popup's rows, from each AddPole(PanelPage.X, Settings.A, ...) and AddPoleWear(...) call;
# each tab's Flagpole button, from its AddPoleButton(..., "Id", ...) call.
$poleCalls = [regex]::Matches($catalog, 'AddPole(?:Wear)?\(PanelPage\.\w+,([^;]*?)\);')
foreach ($call in $poleCalls) {
    foreach ($m in [regex]::Matches($call.Groups[1].Value, 'Settings\.(\w+)')) {
        $name = $nameOf[$m.Groups[1].Value]
        if ($name) { [void]$needed.Add("$name.Label"); [void]$optional.Add("$name.Tooltip") }
    }
}
Check "the pole rows are read from both tabs' AddPole and AddPoleWear calls" $poleCalls.Count 4
$buttonCalls = [regex]::Matches($catalog, 'AddPoleButton\(PanelPage\.\w+, PanelPage\.\w+, "(\w+)"')
foreach ($call in $buttonCalls) { [void]$needed.Add($call.Groups[1].Value + ".Label"); [void]$optional.Add($call.Groups[1].Value + ".Tooltip") }
Check "the Flagpole buttons are read from both tabs' AddPoleButton calls" $buttonCalls.Count 2
Write-Host "      ($rows rows in the catalog, 24 of them made twice by AddDesign, 15 by AddPole, 3 by AddPoleWear, 1 by AddPoleButton)"

foreach ($enum in @('PanelPage')) {
    if ($catalog -match "public enum $enum \{([^}]*)\}") {
        foreach ($v in ($Matches[1] -split ',')) { $v = $v.Trim(); if ($v -and $v -ne 'None') { [void]$needed.Add("Tab.$v") } }
    }
}

# Key-shaped literals anywhere in the code ("Status.Flags", "Pattern.Disc"...), including the
# ones picked by a ?: that a Localization.Get("...") search would miss.
$prefixes = 'Mod|Tab|Key|Status|Unit|File|Language|Group|ResetAll|Reset|Pattern|WindMode|GameWindNow|Existing|Flag|Tool|Picker|Prop|Colours|DeleteFlag|ClothDetail|ClothThreads|Country|Gizmo|PoleButton|Pole|Ropes|Profile|Base|Hoist|Held|Motion|Cut|Image|PanelSkin|IconPlace|Placing|Quality'
foreach ($file in Get-ChildItem $root -Recurse -Filter *.cs | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }) {
    foreach ($m in [regex]::Matches((Get-Content $file.FullName -Raw), "`"((?:$prefixes)\.[A-Z][A-Za-z.]*[A-Za-z])`"")) {
        [void]$needed.Add($m.Groups[1].Value)
    }
}

$missing = @($needed | Where-Object { -not $en.Table.ContainsKey($_) } | Sort-Object)
Check "English has every key the code needs ($($needed.Count))" ($missing -join ", ") ""

$unused = @($en.Table.Keys | Where-Object { -not $needed.Contains($_) -and -not $optional.Contains($_) } | Sort-Object)
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

Check "a wrapped text is one line" ($en.Table["Mod.UnsupportedWindows"] -match "Your flags stay saved in the city\.") "True"
Check "\n is a real break, no stray spaces" ($en.Table["Mod.UnsupportedWindows"] -match "in the city\.`n`nRemove any") "True"
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
    # A {n} left out formats without an error (the number just goes missing); a \n left out runs
    # two lines of the settings file's header into one. (Real Glass's check.)
    $differ = @()
    $holes = { param($s) (@([regex]::Matches($s, '\{\d+\}') | ForEach-Object { $_.Value }) | Sort-Object) -join "," }
    foreach ($key in @($t.Table.Keys | Where-Object { $en.Table.ContainsKey($_) } | Sort-Object)) {
        $mine = $t.Table[$key]; $english = $en.Table[$key]
        if ((& $holes $mine) -ne (& $holes $english)) { $differ += "$key ({n})" }
        if ($mine.Split("`n").Count -ne $english.Split("`n").Count) { $differ += "$key (\n)" }
        try { [void][string]::Format($mine, $dummy) } catch { $differ += "$key (bad {n})" }
    }
    Check "$($file.Name): the same {n} and \n as English" ($differ -join ", ") ""
    $todo = @($en.Table.Keys | Where-Object { -not $t.Table.ContainsKey($_) }).Count
    Write-Host "      $($file.Name): $($t.Table.Count) texts, $todo still in English"
}

# ---- the countries' names (tools\fetch-country-names.ps1) --------------------------------------

$tableText = [IO.File]::ReadAllText((Join-Path $root "Flags\FlagCountries.cs"))
$codes = @([regex]::Matches($tableText, 'new FlagCountry\("([a-z-]+)"') | ForEach-Object { $_.Groups[1].Value })
$translated = @(Get-ChildItem (Join-Path $root "Localization") -Filter *.xml | Where-Object { $_.Name -ne 'en.xml' } | ForEach-Object { $_.BaseName })
$nameFiles = @(Get-ChildItem (Join-Path $root "Localization\Countries") -Filter *.txt)
# By its codes: PowerShell 5.1 reads this file as ANSI, so a literal range arrives mangled.
$chinese = New-Object regex ("[" + [char]0x4E00 + "-" + [char]0x9FFF + "]")
Check "every translation has the countries' names" (@($translated | Where-Object { $nameFiles.BaseName -notcontains $_ }) -join ", ") ""

foreach ($file in $nameFiles) {
    $seen = @{}
    $wrong = @()
    foreach ($line in [IO.File]::ReadAllLines($file.FullName, [Text.Encoding]::UTF8)) {
        if (-not $line -or $line.StartsWith("#")) { continue }
        if ($line -notmatch '^([a-z-]+)=(\S.*)$') { $wrong += "'$line'"; continue }
        if ($codes -notcontains $Matches[1]) { $wrong += "$($Matches[1]) (no such flag)" }
        if ($seen.ContainsKey($Matches[1])) { $wrong += "$($Matches[1]) (twice)" }
        $seen[$Matches[1]] = $Matches[2]
        # CLDR's mark for "the same as its parent locale" is never a name.
        if ($Matches[2].IndexOf([char]0x2191) -ge 0) { $wrong += "$($Matches[1]) (an inherited mark)" }
        if ($file.BaseName -eq 'zh' -and -not $chinese.IsMatch($Matches[2])) { $wrong += "$($Matches[1]) (no Chinese)" }
    }
    Check "$($file.Name): every flag once, and nothing else" ($wrong -join ", ") ""
    Check "$($file.Name): $($codes.Count) names" $seen.Count $codes.Count
}

Write-Host ""
if ($failed -eq 0) { Write-Host "all passed" } else { Write-Host "$failed FAILED"; exit 1 }
