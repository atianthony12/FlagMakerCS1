# Changelog

Notable changes to RealFlag. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Each Workshop upload is one version
below; the mods list shows it after the mod's name, and the first line of `RealFlag.log` gives
it with the exact build. The version is `Mod.Version` in `RealFlag/Mod.cs` (0.x while in
development).

## 1.0.0 — unreleased

The first release.

### Flags

- **Cloth that waves in the wind**: every flag is a piece of cloth simulated as it hangs and
  flutters, drawn with a fabric shader in the game's own lighting (the sun and its shadows, street
  lights at night, fog), wet-looking in rain.
- **The game's wind** by default (its direction, stronger in rain and higher up), or your own speed,
  gusts and direction on the Settings tab. With Volumetric Weather on, its wind: the strength of
  its weather or of its Now tab, its direction, and the gusts that bend its trees passing each flag.
- **Designs**: every country's and territory's flag, fifteen patterns in your colours, or your own
  pictures (a folder of PNG and JPEG files, cropped and zoomed in the Image window, never
  stretched); width and height from 0.5 to 30 m, any shape from 1:5 to 5:1; hoisted from its side
  or its top; waving, still or flat; how wavy, how dark.
- **A flagpole of the mod's own** if you want one: ball, flat or spear top, one or two halyard
  ropes, your colours, thickness, tilt and lean, and a Pole height (1 to 50 m, up to 200 m with
  Unlock range). The cloth keeps off its pole. A banner hoisted from its top has no pole.
- **Keeps off the city**: roofs, walls, props and trees (Settings tab, on by default).
- **Cloth detail** from Low to Extreme, finer near the camera. With more flags in view than the
  CPU has time for, the smallest on the screen go coarser and slower first, so every flag keeps
  moving.
- **Cloth threads** (Settings tab): Auto, or more threads for more flags at full detail.

### Placing and editing

- **+ New flag**: the flag follows the pointer and stands on what is under it; the right button
  turns it, Page Up and Page Down raise it.
- **The Flags tab** lists the city's flags; a selected flag carries handles to move, turn, tilt and
  lean it, and every design setting applies to it live.
- **Move It** moves, copies, pastes and undoes flags with their designs. Procedural Objects'
  conversion leaves flags as they are.

### Saved with the city

- Every flag's design is saved with the city. A city whose flags this copy cannot read is left
  untouched, and the mod stands down there with a message.

### Known at release

- **Extended Managers Library** (and Prop Anarchy's larger prop limit) is not supported yet: the
  mod stands down in such a city and keeps its flags' designs.
- **Mac and Linux** are new and experimental.
- Flags placed by other tools (Find It, Picker) get the New flag window's design.
