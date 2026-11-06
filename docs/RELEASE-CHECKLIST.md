# Release checklist — Steam Workshop (Flag Maker: not published yet)

Everything that has to be true before the first upload, in the order the steps block each other.
Made 2026-10-22 from Volumetric Weather's and Real Glass's checklists (`..\VolumetricClouds\docs\
RELEASE-CHECKLIST.md`, `..\RealGlass\docs\RELEASE-CHECKLIST.md`): the facts about the game's
upload flow were read from its IL for Volumetric Weather (`WorkshopModUploadPanel`, `PluginInfo`,
`WorkshopHelper`) and hold for every mod of the game.

**One command checks almost all of it:**

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\release.ps1 -DryRun
```

It changes nothing and lists every problem (a real run stops at the first): the game closed, the
tree committed, ModsLibCS1 pinned and pushed, `Mod.Version` 1.0.0 or later and not a tag yet,
`CHANGELOG.md` dated, no personal name in the repo's files, every platform's bundle built from the
shader sources as they stand, the Workshop texts and image there, then the build, what the DLL
embeds, the five shipped files, `bundle-apis.ps1` on all three bundles (real code for **Direct3D 11,
OpenGL Core and Metal**) and the tests -- settings, localization, cloth, **Metal** (Apple's compiler
on every shipped Metal program) and **AMD** (AMD's compiler on every shipped Direct3D 11 program,
five chip generations). **All of it runs for every release, not only the first**: a release is never
shipped without them (Real Glass's 2026-10-22 release broke on every Mac on a shader `test-metal`
would have caught). About half an hour, most of it the AMD check (23
minutes here: the older chips take one program at a time). It never builds under a running game,
dry run or not: the build deploys. Without
`-DryRun`, for the first upload it stops after the checks (the game's Share button uploads
`Addons\Mods\RealFlag` as it stands); for every later one it copies the five files into the
subscribed Workshop folder and parks the local copy (see "Updating later").

## Where each piece lives

| Piece | Defined where | Notes |
| --- | --- | --- |
| Mod name and version in Content Manager | `Mod.DisplayName`, `Mod.Version` in `RealFlag/Mod.cs` | "Flag Maker 1.0.0". 0.x is the development series: release.ps1 refuses it |
| Mod description in Content Manager | `Mod.Description` in `RealFlag/Localization/en.xml` | "Cloth flags that wave in the wind" |
| Button icon | `RealFlag/Resources/FlagIcon.png` | Embedded; 72 × 72, white on clear, drawn from `FlagIcon.lucide.txt` by `ModsLibCS1\tools\make-button-icon.ps1` |
| Workshop title | Typed into the game's Share panel | Pre-filled from the folder name `RealFlag`: type **Flag Maker** over it |
| Workshop description | `Workshop/Description.bbcode.txt`, pasted into the panel | BBCode, not Markdown. Steam caps it at 8000 characters, counting each line break twice and each apostrophe as `&#39;` (5): `tools\bbcode-preview.html` previews and counts it Steam's way, release.ps1 checks it |
| Change note | `Workshop/ChangeNotes/<version>.bbcode.txt` | Pasted into the same panel. The author's fixed shape: `[h1]Version X.Y.Z[/h1]`, then New / Fixed / Changed / Removed lists, only those that apply |
| Preview image | `Workshop/PreviewImage.png`, dropped into the STAGING folder at upload | 512 × 512 PNG, under 1 MB. A copy in the mod folder is never used as the preview |
| Screenshots | The item's Steam page, *Add/Edit images* (masters in `Workshop/Screenshots/`) | 5–8, 1920 × 1080 or larger, 16:9, each under 1 MB (JPEG ~90) |
| Required items, visibility, tags | The item's Steam page | Harmony `2040656402`. The game sends one tag, `Mod` |

## Specifications (Volumetric Weather's, verified there)

- **PreviewImage.png**: exactly that name (`Path.Combine(stagingPath, "PreviewImage.png")`), at the
  root of `WorkshopStagingArea\<GUID>\` over the default the game wrote; square, read at a ~270 px
  thumbnail first: one clear image, the name large enough to survive it, no fine text. sRGB,
  8-bit; transparency is pointless (a dark page).
- **Screenshots**: in-game at the normal resolution, UI hidden where the shot is about the flags;
  the first one shows beside the description. Worth having: big flags waving over a skyline, a
  row of country flags, a flag on the mod's pole with its halyard, a banner hoisted from its top,
  the panel open (Flags tab and the New flag window), a flag with your own picture, flags at night
  under street lights. Video is a linked YouTube URL, not an upload.
- **Text**: the description and change notes are BBCode -- `[h1]`, `[b]`, `[i]`, `[url=…]`,
  `[list]`/`[*]`, `[img]`. One line per paragraph: Steam keeps every line break.

## 1. Code and content

- [ ] `Mod.Version` 1.0.0 (`RealFlag/Mod.cs`: "0.x: not on the Workshop yet").
- [ ] `CHANGELOG.md`: `## 1.0.0 — unreleased` dated (`## 1.0.0 — 2026-..-..`) when the build is
      final; the next change opens `## 1.0.1 — unreleased`.
- [ ] **All three bundles** built after the last shader edit (`.\build-bundle.ps1`, no `-Only`):
      `-Only win` while iterating leaves the Mac and Linux ones stale. build-bundle.ps1 records
      what each was built from in `RealFlag\Resources\bundles-built-from.txt`; release.ps1 refuses
      any line that does not match `tools\bundle-sources.ps1`'s hash of every `.shader`/`.cginc`
      under `UnityProject\Assets\RealFlag` and `BundleBuilder.cs`. (2026-11-06: no line yet, and the
      Mac and Linux bundles are older than wear and tear and Torn: the release's full build fixes both.)
- [ ] `tools\bundle-apis.ps1` on each bundle (release.ps1 runs it): every shader with real code for
      every API (win D3D11 + OpenGL Core, linux OpenGL Core, mac Metal + OpenGL Core), no "not"
      that HLSLcc made never false, no Metal sampler past slot 15. (2026-10-22: all three pass.)
- [ ] **Metal**: `tools\test-metal.ps1` passes. Unity 5.6 ships Metal as source and the Mac compiles
      it when the shader loads; one program it refuses and the whole shader is unsupported, and the
      mod stands down there (Real Glass 1.1.2 did, on every Mac). Needs Apple's Metal Developer
      Tools for Windows (installed in `Program Files\Metal Developer Tools`; `-Metal` / `RF_METAL`).
      (2026-10-22: 55 programs of 3 shaders compile at Metal 1.1 and 4.0; at most 4 samplers a
      program, of Metal's 16.)
- [ ] **AMD**: `tools\test-amd.ps1` passes -- `docs\AMD-COMPILE-CHECK.md` (the baseline, what to
      do when it fails). Needs Radeon GPU Analyzer (`Documents\Projects\RGA`; `-Rga` / `RF_RGA`).
      (2026-10-22: 275 programs on RX 7000, RX 9000, RX 6000, RX 5000 and Vega, no spills; the
      cloth's largest 692-1,250 instructions, at most 84 VGPRs.)
- [ ] `tools\test-cloth.ps1`, `test-settings.ps1`, `test-localization.ps1` pass (release.ps1 runs
      them on the deployed DLL).
- [ ] Defaults reviewed: no pole, the game's wind, Cloth detail High (32), self-collision and
      "keeps off buildings" on, freeze when paused on, detailed logging OFF, Panel look Frosted,
      the mod icon in Unified UI.
- [ ] Languages: en, es, fr, zh complete (`test-localization.ps1` lists any text still in English).
- [ ] No personal name in the repo's files but README.md and LICENSE (release.ps1 checks it).
- [ ] CLAUDE.md and the plans are never committed (`.gitignore` has `/CLAUDE.md`); no token
      patterns or e-mail addresses in the history before the repository goes public.

## 2. Verified in the game

- [ ] The release build itself (release.ps1 builds and deploys it) loaded in a city on Windows,
      the log read by Claude: the first line's version, the shader bundle loaded, no ERROR/WARN
      but the expected, the "other mods patching" line, Move It's and Procedural Objects' patch
      lines.
- [ ] The rounds since the last in-game run, seen (CLAUDE.md's "To see" lists): the pre-release
      round (the Pole height's Allow taller poles; no pole for a top hoist) and, before it, the ModsLibCS1
      panel (every look, every window), the Ultra/Extreme details and the flags dragged past
      buildings.
- [ ] **OpenGL, every release that changed a shader**: the game started with `-force-glcore` (Render
      It! off: its post-processing is D3D11-only) runs the Linux code path. Nothing offline compiles
      GLSL as a driver would: `bundle-apis.ps1` only proves the code is there.
- [ ] The Mac, if one is at hand (the first player's log: "shader bundle loaded: mac ... on Metal").

## 3. The folder that gets uploaded

The Workshop uploads `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\RealFlag` as it
stands.

- [ ] Exactly five files (release.ps1 checks it, the four besides the DLL byte-identical to the
      repo's): `RealFlag.dll`, `UnifiedUILib.dll`, `CitiesHarmony.API.dll`, `MoveItIntegration.dll`
      and `THIRD-PARTY-NOTICES.md` (deployed since 2026-10-22: MIT asks for its notice "in all
      copies", and a GitHub link is not with the distribution). No PDB, no subfolder.
      UnifiedUILib.dll and MoveItIntegration.dll must ship: our code references their types, and the
      game resolves a reference by NAME among the assemblies already loaded; Unified UI the mod and
      Move It the mod stay optional.
- [ ] The deployed build is the one tested in step 2: any `dotnet build` after it replaces it with
      another build number.

## 4. Images

- [ ] `Workshop/PreviewImage.png`, 512 × 512, under 1 MB (release.ps1 checks it).
- [ ] 5–8 screenshots in `Workshop/Screenshots/` (`Workshop/Screenshots/` is empty now).

## 5. Text

- [ ] `Workshop/Description.bbcode.txt`: Volumetric Weather's format (a version banner on top,
      then Requirements / How to use / Features / Performance / Compatibility / Source code /
      Reporting a bug), within 8000 characters Steam's way. The compatibility lines CLAUDE.md
      keeps: Extended Managers Library / Prop Anarchy's larger limit not supported yet (Flag Maker
      stands down, designs kept); Mac and Linux new and experimental; flags placed by other tools
      (Find It, Picker) get the new flag's design; removing the mod loses a city's flags only if
      it is saved without it.
- [ ] `Workshop/ChangeNotes/1.0.0.bbcode.txt`.

## 6. Publish (the first upload)

`tools\release.ps1` (no `-DryRun`) passes, then by hand:

1. Launch the game and load a city on that build; Claude reads the log.
2. Content Manager → **Mods** → Flag Maker → **Share**.
3. The game makes `WorkshopStagingArea\<GUID>\` with its own `PreviewImage.png` and the mod folder
   under `Content\`. Click the folder button and overwrite that `PreviewImage.png` with ours;
   check the thumbnail changed on screen.
4. The title (**Flag Maker**: the panel fills in the folder's name, `RealFlag`), the description,
   the change note. **Share**. Assume the item is live the moment
   the upload finishes (the panel has no visibility control).

## 7. On the Steam page, straight away

- [ ] **The GitHub repository public** (the description links it): `atianthony12/FlagMakerCS1`
      is PRIVATE on 2026-11-06. ModsLibCS1 is public; its pinned commit must be pushed
      (release.ps1 checks it) or a clone cannot build and GitHub shows the submodule as a 404.
- [ ] *Add/Remove Required Items* → Harmony, `2040656402`.
- [ ] Screenshots; tags beyond `Mod`; the description renders (lists, links).
- [ ] Visibility **Public**. (A fetched Workshop page's "This item has been removed from the
      community" sentence is a hidden template on every page: nothing to act on.)

## 8. After publishing

- [ ] Park the local `Addons\Mods\RealFlag` (moved aside to `Cities_Skylines\RealFlag-dev-parked`,
      never deleted) so it cannot load beside the subscribed copy: both would run.
- [ ] Subscribe and load a city on the published copy; the files in
      `steamapps\workshop\content\255710\<id>` byte-identical to the uploaded ones.
- [ ] The item's id into `tools\release.ps1` (`$workshopId`), README (its install line) and
      CHANGELOG.
- [ ] Tag `v1.0.0` (annotated) on the uploaded build's commit, pushed; then the **GitHub release**:
      a zip of exactly the five uploaded files in a `RealFlag/` folder, notes = what the version
      brings + a link to `CHANGELOG.md` at the tag + how to install by hand (the folder into
      `Addons\Mods`, Harmony `2040656402` subscribed), latest.
      `gh release create v1.0.0 FlagMaker-1.0.0.zip --title "Flag Maker 1.0.0" --notes-file ... --latest`
      The tag must be on GitHub first: with no remote tag, `gh` makes one at main's tip.
- [ ] Open `## 1.0.1 — unreleased` in CHANGELOG.md.

## Updating later — read this before the first update

A mod's published file id comes from **its folder name**: `PluginInfo`'s constructor takes
`Path.GetFileNameWithoutExtension` of the mod path, so a subscribed item in
`…\content\255710\<id>` knows its id, while a local folder called `RealFlag` has
`PublishedFileId.invalid`. With an invalid id the panel calls `CreateItem` -- **a brand-new
Workshop item** (`WorkshopModUploadPanel.OnShare`). Only a folder that knows its id goes to
`UpdateItem`, and the panel then pre-fills the title and description from Steam.

So every update is published from the *subscribed* copy, whose button reads **Update**:
`tools\release.ps1` (with `$workshopId` set) copies the five files into that folder, reads them
back, and parks the local copy so no Share button can appear. Then: Skyve -- the item enabled;
load a city (Claude reads the log); Content Manager → the button must read UPDATE (Share = STOP);
the description and the new change note; Update; the Steam page still shows ONE item. The preview
image stays unless a new one is dropped into the new staging folder. Every release also updates
the description's version banner and gets its change note file.
