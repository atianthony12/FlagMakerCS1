# Third-Party Notices

Flag Maker is an unofficial mod for Cities: Skylines 1. This file records what in the repository
is not original work, and what is credited rather than incorporated.

The repository contains no game code and no game assets. The game's DLLs are referenced from
your own installation at build time and are never copied into the repository or the
distribution.

## Shipped with the mod

These are redistributed alongside `FlagMaker.dll`, which is the standard pattern in this
ecosystem.

| Component | Author | Licence | Use |
| --- | --- | --- | --- |
| [`UnifiedUILib.dll`](https://github.com/kianzarrin/UnifiedUI) | kianzarrin | MIT | Registers the mod's button with Unified UI, and hosts the button panel itself when the Unified UI mod is not enabled |
| [`CitiesHarmony.API.dll`](https://github.com/boformer/CitiesHarmony) | boformer | MIT | Bootstrap shim: waits for the Harmony mod and prompts the player to install it if it is missing |
| [ModsLibCS1](https://github.com/anthonytoumaibrahim/ModsLibCS1) (git submodule `ModsLibCS1/`) | This mod's author's own UI library | MIT | The panel's controls and looks, compiled into `FlagMaker.dll` (not a DLL of its own) |
| [`MoveItIntegration.dll`](https://github.com/Quboid/CS-MoveIt) | Quboid (Move It) | MIT | The interfaces Move It looks for in other mods, so a copied, pasted or restored flag keeps its design. Taken from Move It's own Workshop folder; with Move It installed, the game uses the copy it loaded first |

## Artwork

| Asset | Source | Licence | Use |
| --- | --- | --- | --- |
| `RealFlag/Resources/FlagIcon.png` (listed in `FlagIcon.lucide.txt`) | Drawn by ModsLibCS1's `tools/make-button-icon.ps1` from the path data of the [Lucide](https://lucide.dev) icon flag (lucide-static 0.460.0) | ISC, as the panel's icons below | The mod's button icon, embedded in the DLL |
| `RealFlag/Resources/Poles/rust_*.png` | Made by `tools/pole-textures.ps1` (halved, its colour scaled) from [Poly Haven](https://polyhaven.com)'s [Rust Coarse 01](https://polyhaven.com/a/rust_coarse_01) by Dimitrios Savva and Rico Cilliers: its colour, roughness and normal maps | CC0 1.0 (public domain dedication; [polyhaven.com/license](https://polyhaven.com/license)) | A rusted flagpole's rust, embedded in the DLL |
| `RealFlag/Resources/Poles/rope_*.png` | Made by `tools/pole-textures.ps1` (halved, made grey) from [ambientCG](https://ambientcg.com)'s [Rope 001](https://ambientcg.com/a/Rope001): its colour, roughness and normal maps | CC0 1.0 ([ambientcg.com/license](https://ambientcg.com/license)) | The halyard's braid, embedded in the DLL |
| `RealFlag/Resources/Poles/concrete_*.png` | Made by `tools/pole-textures.ps1` (halved, made grey) from [ambientCG](https://ambientcg.com)'s [Concrete 016](https://ambientcg.com/a/Concrete016): its colour, roughness and normal maps | CC0 1.0 ([ambientcg.com/license](https://ambientcg.com/license)) | The concrete block a flagpole can stand in, embedded in the DLL |
| `RealFlag/Resources/Flags/*.png`, `Flags/Small/*.png` | The world's flags from [Flagpedia.net](https://flagpedia.net) (its CDN, flagcdn.com), drawn from [Wikimedia Commons](https://commons.wikimedia.org)' vector files; fetched by `tools/fetch-flags.ps1` | Public domain: "The flag images provided on our website are in the public domain and can be used freely without restriction" ([flagpedia.net/terms](https://flagpedia.net/terms)) | The countries' flags on the cloth (640 px) and in the country picker (80 px), embedded in the DLL. 256 codes, 252 images |

| The panel's icons: the tab and folder icons (`RealFlag/Resources/PanelIcons.png`, listed in `PanelIcons.lucide.txt`) and the library's glyphs (`ModsLibCS1/sprites/sheet.png`) | Drawn by the ModsLibCS1 UI library's tools (the library is compiled into the DLL) from the path data of [Lucide](https://lucide.dev) icons (lucide-static 0.460.0): flag, settings (the tabs), folder-open (the images folder's buttons), slash, spline, route, circle (the New flag window's Place row, with flag); brick-wall, bookmark (its Presets row, with flag); rotate-ccw, chevron-down, chevron-right, x (the library's reset, fold, dropdown, close) | ISC (copyright Lucide Contributors; portions Cole Bemis, as part of Feather, MIT); its notice follows | The panel's tab icons, the images folder's buttons, the close cross and the dropdown arrow, embedded in the DLL |

Lucide's licence, as published in its repository:

```
ISC License

Copyright (c) for portions of Lucide are held by Cole Bemis 2013-2022 as part of Feather (MIT). All other copyright (c) for Lucide are held by Lucide Contributors 2022.

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
```

| Asset | Source | Licence | Use |
| --- | --- | --- | --- |
| `RealFlag/Localization/Countries/*.txt` | The countries' names in Spanish, French and Simplified Chinese from the [Unicode Common Locale Data Repository](https://cldr.unicode.org) (CLDR, release 49: `common/main`, `common/subdivisions`); written by `tools/fetch-country-names.ps1`, which sorts them and takes CLDR's short form for Hong Kong, Macao and Palestine and its variant Costa de Marfil in Spanish; any correction is in `tools/country-names-fixes.txt` | Unicode License V3; its notice follows | The country picker and the flag list, embedded in the DLL |

Unicode's licence, as published in CLDR's repository:

```
Copyright © 2001–2026 Unicode, Inc.

NOTICE TO USER: Carefully read the following legal agreement. BY
DOWNLOADING, INSTALLING, COPYING OR OTHERWISE USING DATA FILES, AND/OR
SOFTWARE, YOU UNEQUIVOCALLY ACCEPT, AND AGREE TO BE BOUND BY, ALL OF THE
TERMS AND CONDITIONS OF THIS AGREEMENT. IF YOU DO NOT AGREE, DO NOT
DOWNLOAD, INSTALL, COPY, DISTRIBUTE OR USE THE DATA FILES OR SOFTWARE.

Permission is hereby granted, free of charge, to any person obtaining a
copy of data files and any associated documentation (the "Data Files") or
software and any associated documentation (the "Software") to deal in the
Data Files or Software without restriction, including without limitation
the rights to use, copy, modify, merge, publish, distribute, and/or sell
copies of the Data Files or Software, and to permit persons to whom the
Data Files or Software are furnished to do so, provided that either (a)
this copyright and permission notice appear with all copies of the Data
Files or Software, or (b) this copyright and permission notice appear in
associated Documentation.

THE DATA FILES AND SOFTWARE ARE PROVIDED "AS IS", WITHOUT WARRANTY OF ANY
KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT OF
THIRD PARTY RIGHTS.

IN NO EVENT SHALL THE COPYRIGHT HOLDER OR HOLDERS INCLUDED IN THIS NOTICE
BE LIABLE FOR ANY CLAIM, OR ANY SPECIAL INDIRECT OR CONSEQUENTIAL DAMAGES,
OR ANY DAMAGES WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS,
WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION,
ARISING OUT OF OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THE DATA
FILES OR SOFTWARE.

Except as contained in this notice, the name of a copyright holder shall
not be used in advertising or otherwise to promote the sale, use or other
dealings in these Data Files or Software without prior written
authorization of the copyright holder.

SPDX-License-Identifier: Unicode-3.0
```

National flags are state symbols. Showing them flown, as a city-builder does, is their ordinary
use; some countries have laws against their flag's misuse or its registration as a trademark
(the Paris Convention's article 6ter), which a free mod that flies them does neither of. The
list is Flagpedia's, ISO 3166-1's countries and territories, without judgement on any of them;
its translated names are the Unicode standard's, likewise.

## Build-time only

| Component | Author | Licence | Use |
| --- | --- | --- | --- |
| [`CitiesHarmony.Harmony.dll`](https://github.com/boformer/CitiesHarmony) | boformer; a repackaging of [Harmony](https://github.com/pardeike/Harmony) by Andreas Pardeike | MIT | Compile-time reference only. The real assembly is provided at runtime by the Harmony mod (Workshop 2040656402) and is never shipped by us. |
| [`Microsoft.NETFramework.ReferenceAssemblies.net35`](https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies.net35) | Microsoft | MIT | net35 reference assemblies, so the project builds without an old framework installed |

## Techniques credited, not copied

The cloth, the air acting on it, the wind and the fabric shader follow published work --
position-based dynamics, long range attachments, Lighthill's slender-body theory of the
fluttering flag, the Eurocode's wind profile and turbulence, the power law of the wind over a
city (Davenport, ASCE 7) and its roughness from the buildings (Lettau), the WMO's 3 s gust,
gusts and eddies of several sizes (Perlin's sum of octaves, Kaimal et al.'s surface-layer
spectra), the "Charlie" sheen, crowd
animation's time offsets (shared motion), dynamic resolution scaling (the load), video textures'
seam and the vertex animation texture (flags that play a loop), the FNV-1a hash (the loops kept
on disk), Worley's cellular texture (a cloth's holes, a galvanized pole's zinc crystals) -- listed with
full references in `LICENSE` and `README.md`. The implementations were written for this mod.

One exception, a few lines: the hash functions of `UnityProject/Assets/RealFlag/FlagWear.cginc`
(`RFHash11`, `RFHash12`, `RFHash42`) and `FlagPole.shader` (`RFPoleHash`), compiled into the
shipped shader bundles, which scatter a worn flag's holes, tatters and grime and a worn pole's
chips, rust and grime, are adapted from David Hoskins' "Hash without Sine" (Shadertoy 4djSRW,
2014), MIT:

```
Copyright (c) 2014 David Hoskins.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Other mods were read to understand the game's behaviour and to stay compatible with them --
Move It (how it copies, pastes and restores props, and how it picks and outlines them) and
Volumetric Weather and Volumetric Explosions (this mod's foundations are theirs). Where another
mod's behaviour mattered, it was read from its IL; no code from any mod but those two, by the
same author, is included.

## Trademarks

Cities: Skylines is © Colossal Order Ltd. and published by Paradox Interactive AB; the names
are trademarks of their respective owners. Unity is a trademark of Unity Technologies. This
project is unofficial, fan-made, and not affiliated with or endorsed by any of them.
