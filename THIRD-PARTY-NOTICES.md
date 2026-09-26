# Third-Party Notices

RealFlag is an unofficial mod for Cities: Skylines 1. This file records what in the repository
is not original work, and what is credited rather than incorporated.

The repository contains no game code and no game assets. The game's DLLs are referenced from
your own installation at build time and are never copied into the repository or the
distribution.

## Shipped with the mod

These are redistributed alongside `RealFlag.dll`, which is the standard pattern in this
ecosystem.

| Component | Author | Licence | Use |
| --- | --- | --- | --- |
| [`UnifiedUILib.dll`](https://github.com/kianzarrin/UnifiedUI) | kianzarrin | MIT | Registers the mod's button with Unified UI, and hosts the button panel itself when the Unified UI mod is not enabled |
| [`CitiesHarmony.API.dll`](https://github.com/boformer/CitiesHarmony) | boformer | MIT | Bootstrap shim: waits for the Harmony mod and prompts the player to install it if it is missing |
| [`MoveItIntegration.dll`](https://github.com/Quboid/CS-MoveIt) | Quboid (Move It) | MIT | The interfaces Move It looks for in other mods, so a copied, pasted or restored flag keeps its design. Taken from Move It's own Workshop folder; with Move It installed, the game uses the copy it loaded first |

## Artwork

| Asset | Source | Licence | Use |
| --- | --- | --- | --- |
| `RealFlag/Resources/FlagIcon.png` | Drawn by `tools/make-icon.ps1` (System.Drawing) for this mod | MIT, as the rest | The mod's button icon, embedded in the DLL |
| `RealFlag/Resources/Flags/*.png`, `Flags/Small/*.png` | The world's flags from [Flagpedia.net](https://flagpedia.net) (its CDN, flagcdn.com), drawn from [Wikimedia Commons](https://commons.wikimedia.org)' vector files; fetched by `tools/fetch-flags.ps1` | Public domain: "The flag images provided on our website are in the public domain and can be used freely without restriction" ([flagpedia.net/terms](https://flagpedia.net/terms)) | The countries' flags on the cloth (640 px) and in the country picker (80 px), embedded in the DLL. 256 codes, 252 images |

National flags are state symbols. Showing them flown, as a city-builder does, is their ordinary
use; some countries have laws against their flag's misuse or its registration as a trademark
(the Paris Convention's article 6ter), which a free mod that flies them does neither of. The
list is Flagpedia's, ISO 3166-1's countries and territories, without judgement on any of them.

## Build-time only

| Component | Author | Licence | Use |
| --- | --- | --- | --- |
| [`CitiesHarmony.Harmony.dll`](https://github.com/boformer/CitiesHarmony) | boformer; a repackaging of [Harmony](https://github.com/pardeike/Harmony) by Andreas Pardeike | MIT | Compile-time reference only. The real assembly is provided at runtime by the Harmony mod (Workshop 2040656402) and is never shipped by us. |
| [`Microsoft.NETFramework.ReferenceAssemblies.net35`](https://www.nuget.org/packages/Microsoft.NETFramework.ReferenceAssemblies.net35) | Microsoft | MIT | net35 reference assemblies, so the project builds without an old framework installed |

## Techniques credited, not copied

The cloth, the air acting on it, the wind and the fabric shader follow published work --
position-based dynamics, long range attachments, Lighthill's slender-body theory of the
fluttering flag, the Eurocode's wind profile and turbulence, the "Charlie" sheen -- listed with
full references in `LICENSE` and `README.md`. The implementations were written for this mod.

Other mods were read to understand the game's behaviour and to stay compatible with them --
Move It (how it copies, pastes and restores props, and how it picks and outlines them) and
Volumetric Weather and Volumetric Explosions (this mod's foundations are theirs). Where another
mod's behaviour mattered, it was read from its IL; no code from any mod but those two, by the
same author, is included.

## Trademarks

Cities: Skylines is © Colossal Order Ltd. and published by Paradox Interactive AB; the names
are trademarks of their respective owners. Unity is a trademark of Unity Technologies. This
project is unofficial, fan-made, and not affiliated with or endorsed by any of them.
