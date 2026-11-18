# Changelog

Notable changes to Flag Maker. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Each Workshop upload is one version
below; the mods list shows it after the mod's name, and the first line of `FlagMaker.log` gives
it with the exact build. The version is `Mod.Version` in `RealFlag/Mod.cs` (0.x while in
development).

## 1.0.0 — 2026-11-19

The first release.

### Flags

- **Cloth that waves in the wind**: every flag is a piece of cloth simulated as it hangs and
  flutters, drawn with a fabric shader in the game's own lighting (the sun and its shadows, street
  lights at night, fog), wet-looking in rain (**Wet in the rain**, on the Settings tab and the
  options page, sets how much).
- **Motion**, each flag its own: **Waving** (the default: the cloth simulated in the wind, keeping
  off the city), **Loop** (a few seconds of waving recorded once and played back by the graphics
  card, at almost no cost: for hundreds of flags; **Recapture** records another take), **Still**
  (one shape, held) or **Flat**. A loop flag flies along its own rotation, whatever the wind.
- **The game's wind** by default (stronger in rain and higher up), or your own speed and gusts on
  the Settings tab. With Volumetric Weather on, its wind: the strength of its weather or of its Now
  tab, and the gusts that bend its trees passing each flag. Either way the **Direction** is yours:
  along each flag's rotation (the default), one fixed direction, or the game's.
- **Gusts and eddies of many sizes** in that wind, from a street's width down to the flag's own: a
  flag changes its posture every few seconds instead of holding one, as calm or as wild as before.
- **Designs**: every country's and territory's flag, fifteen patterns in your colours (previewed,
  and saved under a name for any city), or your own pictures (a folder of PNG and JPEG files, cropped and zoomed in the Image window, never
  stretched); width and height from 0.5 to 30 m, any proportions from 1:5 to 5:1 (a country's flag
  too: stretched to fit, turned on a tall shape as a flag hung upright is shown); hoisted from its
  side or its top; how wavy, how much of the wind reaches it (**Wind effect**: a banner can hang
  and sway instead of standing out), how dark (30% to begin with).
- **Anchor points**: besides its hoist, a flag can be held by its top edge, its bottom edge, its
  far edge or its far corners, as a banner tied on more than one side.
- **Seven shapes**: rectangle, swallowtail, swallowtail and tongue, pennant, burgee, pointed or
  rounded, with a Cut depth. The cloth itself is cut: a swallowtail's tails flap apart, a pennant's
  point flies free, and the shadow is cut too.
- **Wear and tear**: any flag worn (faded, its fly frayed), torn (its fly ripped to tatters like a
  pirate's flag, pieces missing; **Torn edges** rips its top and bottom too), dirty (grime, rain
  streaks) or holed (bullet holes, burns, tears; through its shadow too), each flag its own.
- **A flagpole of the mod's own** if you want one: ball, flat or spear top, one or two halyard
  ropes, your colours, thickness, tilt and lean, a Pole height (1 to 50 m), and a **Position on
  pole** to fly the flag lower, at half-mast say. The cloth keeps off its pole. A banner hoisted
  from its top has no pole.
- **Four pole shapes on three bases, and Shine**: tapered, straight, sectional or fluted, on a
  flash collar, a decorative cast base or none; Shine from matte to a glossy coat that reflects the
  sky (Real Glass's live sky when it's on). **Worn**, **Rust**
  and **Dirt** on the pole (chipped paint, rust running down, grime up from the foot), every pole
  its own; the ropes braided, with a **Rope thickness**.
- **Keeps off the city**: roofs, walls, props and trees (Settings tab, on by default), with
  **Detailed building shapes** reading buildings as they are drawn -- cornices, frames, columns --
  not only their simple shapes. A banner hoisted from its top near a roof's edge hangs down the
  facade instead of lying on the roof, and its **Back plate** keeps it in front of the wall behind
  it.
- **Quality** (Settings tab): Low, Medium, High, Very high or Ultra sets the cloth's options at
  once, or Custom. **Cloth detail** goes from Low to Extreme, finer near the camera. With more flags
  in view than the CPU has time for, the smallest on the screen go coarser and slower first, so
  every flag keeps moving.
- **CPU threads for cloth** (Settings tab): Auto, or more threads for more flags at full detail.
- **Shared motion** (Settings tab, on by default): with many like flags in view, the smallest
  share one cloth's motion, each at its own delay, before any goes coarser. A crowd of six or more
  like flags always does, so a street or a roundabout full of them stays smooth.
- **Loop in big crowds** (Settings tab, on by default): with more waving flags in view than can
  move at once, those left over play a loop instead of standing frozen, and take their cloth up
  again when there is room.
- **Place crowds as Loop** (Settings tab, on by default): past twenty waving flags close together,
  new flags there are placed as Loop, with a note in the New flag window. Flags already placed are
  never changed.
- **Hundreds of flags**: a flag far out of view lets its cloth go after two minutes, and takes it
  up again from a like flag's shape when it comes back into view.
- **Loops kept on disk**: a loop is made once and kept in `FlagMaker\Loops` (up to 200 MB), so a
  city with loop flags shows them sooner the next time it loads. Delete the folder at any time.

### Placing and editing

- **+ New flag**: the flag follows the pointer and stands on what is under it; the right button
  turns it, Page Up and Page Down raise it. It starts from the last flag's settings. With **Snap to
  edges**, a banner hoisted from its top snaps to the roof's edge under the pointer, along it or out
  from it.
- **The Flags tab** lists the city's flags (a double click takes the camera to one); a selected
  flag carries handles to move, turn, tilt and lean it, and every design setting applies to it live.
  **Go to**, **Copy** (its design into the New flag window) and **Delete** (or the Del key; it asks
  first).
- **Undo and redo**: Ctrl+Z undoes and Ctrl+Y (or Ctrl+Shift+Z) redoes what you did to a flag
  with Flag Maker: moving, turning and raising it, every design and flagpole change, placing a
  flag and deleting one. One step per drag; up to 100 steps; not saved with the city.
- **A reset icon** on every setting puts it back to its default.
- **Line Tool**: with that mod on, the New flag window places rows of flags in a line, a curve,
  freeform or a circle, each flag previewed before the click.
- **Move It** moves, copies, pastes and undoes flags with their designs. Procedural Objects'
  conversion leaves flags as they are.

### The panel

- **Groups that fold**: a flag's settings sit under Design, Size and shape, Pole and placement,
  Movement and Wear and tear, each folding away; with no flag selected the panel is only the list.
- **Panel look**: Frosted glass, See-through, Blue or Vanilla.
- **The mod's icon** sits in Unified UI, on the Toolbar as a button of its own, or nowhere (the key
  still opens the panel): **Where to show mod icon** in the mod's options.
- **In four languages**: English, Spanish, French and Simplified Chinese, following the game's
  language or your choice in the options. The countries' names and their order are translated too,
  from the Unicode standard's list, and the search finds a country in your language or in English.

### Saved with the city

- Every flag's design and height is saved with the city. A city whose flags this copy cannot read
  is left untouched, and the mod stands down there with a message.
- A city saved while the mod was off has lost its flags (the game lets go of props it has no
  prefab for). With the mod back on, it runs in that city and lets the lost flags' designs go.

### Known at release

- **Extended Managers Library** (and Prop Anarchy's larger prop limit) is not supported yet: the
  mod stands down in such a city and keeps its flags' designs.
- **Mac and Linux** are new and experimental.
- Flags placed by other tools (Find It, Picker) get the New flag window's design.
