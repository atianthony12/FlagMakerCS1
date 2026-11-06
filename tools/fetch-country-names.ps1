# Writes the countries' names in each language from Unicode CLDR (unicode.org/cldr), the standard's
# names: never translated by hand or by machine, a country's name being the last place to guess.
#
#   RealFlag\Localization\Countries\<language>.txt    code=name, one a line, IN THAT LANGUAGE'S ORDER
#
# The picker lists a language's flags in its file's order. The sort is done HERE, by Windows' own
# collation for the language: the game's old Mono cannot sort Chinese by pinyin or Spanish's n-tilde,
# and a file in order needs no sorting there at all.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\fetch-country-names.ps1 [-Tag release-49] [-Source <folder>]
#
# -Source: a folder already holding CLDR's common\main\<lang>.xml and common\subdivisions\<lang>.xml.
#
# CLDR's plain name for a country, with the exceptions in $prefer below: never its other short forms
# ("EE. UU.", "R.-U."), and three up arrows (the same as its parent locale) are never a name. A correction goes in
# tools\country-names-fixes.txt, which outlives a run of this script. English stays Flagpedia's, in
# Flags\FlagCountries.cs. Licence: Unicode License V3, THIRD-PARTY-NOTICES.md.

param(
    [string]$Tag = "release-49",
    [string]$Source = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "RealFlag\Localization\Countries"
$fixes = Join-Path $PSScriptRoot "country-names-fixes.txt"

# The game's language code -> CLDR's file, and the culture that sorts it.
$languages = @(
    @{ Code = "es"; Cldr = "es"; Culture = "es-ES" },
    @{ Code = "fr"; Cldr = "fr"; Culture = "fr-FR" },
    @{ Code = "zh"; Cldr = "zh"; Culture = "zh-CN" }
)

# "Language code" -> CLDR's alt form to take over its plain name. * is every language.
#   hk, mo: its plain name is the formal "Hong Kong SAR China" (the table's English says Hong Kong, Macau).
#   ps: as the table's English says Palestine.
#   es ci: its plain name is the French Cote d'Ivoire; Spanish says Costa de Marfil.
$prefer = @{ "* hk" = "short"; "* mo" = "short"; "* ps" = "short"; "es ci" = "variant" }

# Every code the mod has, from the table.
$tableText = [IO.File]::ReadAllText((Join-Path $root "RealFlag\Flags\FlagCountries.cs"))
$codes = @([regex]::Matches($tableText, 'new FlagCountry\("([a-z-]+)"') | ForEach-Object { $_.Groups[1].Value })
if ($codes.Count -lt 250) { throw "Only $($codes.Count) codes found in FlagCountries.cs" }

if (-not $Source) {
    $Source = Join-Path ([IO.Path]::GetTempPath()) ("cldr-" + [guid]::NewGuid().ToString("N"))
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    foreach ($l in $languages) {
        foreach ($part in @("main", "subdivisions")) {
            $dir = Join-Path $Source "common\$part"
            New-Item -ItemType Directory -Force $dir | Out-Null
            $url = "https://raw.githubusercontent.com/unicode-org/cldr/$Tag/common/$part/$($l.Cldr).xml"
            Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile (Join-Path $dir "$($l.Cldr).xml")
        }
    }
}

# By regex: the files name a DTD that is not beside them, and only these one-line elements are wanted.
function ReadNames([string]$path, [string]$element) {
    $names = @{}
    $text = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
    $pattern = "<$element type=""([A-Za-z0-9]+)""((?: [a-z]+=""[^""]*"")*)>([^<]*)</$element>"
    foreach ($m in [regex]::Matches($text, $pattern)) {
        $alt = [regex]::Match($m.Groups[2].Value, 'alt="([a-z]+)"').Groups[1].Value
        $name = [Net.WebUtility]::HtmlDecode($m.Groups[3].Value).Trim()
        if ($name -eq [string][char]0x2191 * 3) { continue }   # the parent's: not a name
        $names[$m.Groups[1].Value.ToLowerInvariant() + "|" + $alt] = $name
    }
    return $names
}

# "fr cd=Name": a person's correction, which wins over CLDR.
$fixed = @{}
if (Test-Path $fixes) {
    foreach ($line in [IO.File]::ReadAllLines($fixes, [Text.Encoding]::UTF8)) {
        $line = $line.Trim()
        if (-not $line -or $line.StartsWith("#")) { continue }
        if ($line -notmatch '^([a-z]+) ([a-z-]+)=(.+)$') { throw "country-names-fixes.txt: cannot read '$line'" }
        if ($codes -notcontains $Matches[2]) { throw "country-names-fixes.txt: no flag has the code '$($Matches[2])'" }
        $fixed[$Matches[1] + " " + $Matches[2]] = $Matches[3].Trim()
    }
}

New-Item -ItemType Directory -Force $out | Out-Null
foreach ($l in $languages) {
    $territories = ReadNames (Join-Path $Source "common\main\$($l.Cldr).xml") "territory"
    $subdivisions = ReadNames (Join-Path $Source "common\subdivisions\$($l.Cldr).xml") "subdivision"

    $rows = @()
    foreach ($code in $codes) {
        $alt = $prefer["$($l.Code) $code"]
        if (-not $alt) { $alt = $prefer["* $code"] }

        # gb-sct is the subdivision "gbsct"; the rest are territories.
        $key = if ($code -like "*-*") { $code.Replace("-", "") } else { $code }
        $set = if ($code -like "*-*") { $subdivisions } else { $territories }

        $name = $fixed["$($l.Code) $code"]
        if (-not $name -and $alt) { $name = $set["$key|$alt"] }
        if (-not $name) { $name = $set["$key|"] }
        if (-not $name) { throw "CLDR has no $($l.Code) name for '$code'" }
        if ($name -match '[=\r\n]') { throw "A name the file cannot hold: $name" }
        $rows += New-Object PSObject -Property @{ Code = $code; Name = $name }
    }

    $sorted = @($rows | Sort-Object -Property Name -Culture $l.Culture)
    $text = New-Object Text.StringBuilder
    [void]$text.Append("# The countries' names, generated by tools\fetch-country-names.ps1 from Unicode CLDR $Tag`n")
    [void]$text.Append("# (Unicode License V3: THIRD-PARTY-NOTICES.md), in this language's order, which the picker keeps.`n")
    [void]$text.Append("# Do not edit: a correction goes in tools\country-names-fixes.txt, then run the script.`n")
    foreach ($r in $sorted) { [void]$text.Append($r.Code).Append("=").Append($r.Name).Append("`n") }

    $file = Join-Path $out "$($l.Code).txt"
    [IO.File]::WriteAllText($file, $text.ToString(), (New-Object Text.UTF8Encoding($false)))
    Write-Host ("{0}: {1} names, {2} bytes -> {3}" -f $l.Code, $sorted.Count, (Get-Item $file).Length, $file)
}
