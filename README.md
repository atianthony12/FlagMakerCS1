<div align="center">

# Flag Maker

**Cloth flags that wave in the wind, for Cities: Skylines.**

Place them anywhere in your city and move them with Move It. Use any country's flag, your own
colors or your own pictures, on a flagpole or hanging on its own.

<!-- Hero image: a GIF of a few flags waving over a street, e.g.
<img src="Workshop/Screenshots/hero.gif" width="720" alt="Flags waving over a city street"> -->

<!-- Link the Workshop badge once the item is live. -->
[![Cities: Skylines](https://img.shields.io/badge/Cities%3A%20Skylines-mod-01ABF8)](https://store.steampowered.com/app/255710/Cities_Skylines/)
![Steam Workshop](https://img.shields.io/badge/Steam%20Workshop-coming%20soon-lightgrey?logo=steam)
[![Requires Harmony](https://img.shields.io/badge/requires-Harmony-blue)](https://steamcommunity.com/sharedfiles/filedetails/?id=2040656402)
![Languages](https://img.shields.io/badge/languages-EN%20%C2%B7%20ES%20%C2%B7%20FR%20%C2%B7%20ZH-informational)
[![License: MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE)

[Features](#features) · [Getting started](#getting-started) · [Controls](#controls) · [Guide](#guide) · [Compatibility](#compatibility) · [How it works](#how-it-works) · [Building](#building)

</div>

## Features

### Designs

- 250+ country flags
- 15 patterns in your own colors, and you can save your favorites
- Your own pictures (PNG or JPG), never stretched
- 7 shapes: rectangle, swallowtail, swallowtail and tongue, pennant, burgee, pointed and rounded
- Any size up to 30 m, hanging from the side or from the top
- Wear and tear: worn, torn, dirty and holed flags

### Cloth and wind

- Real cloth simulation that hangs, flutters and folds without going through itself
- Lit by the game's sun, shadows and street lights, and wet in the rain
- Waving, still or flat (still and flat flags cost nothing to run)
- Flies in the game's wind, or set your own speed, gusts and direction
- Stronger wind up high and on rooftops, calmer between buildings
- Follows [Volumetric Weather](https://steamcommunity.com/sharedfiles/filedetails/?id=3805863507)'s
  wind and gusts
- Drapes over roofs, walls, props and trees instead of passing through them
- Banners snap to roof edges and hang down the facade

### Flagpoles

- Optional: a flag comes without a pole, so you can put it on any pole asset, mast, roof or facade
- Or let the mod draw one, with a ball, flat or spear top
- Four shapes (tapered, straight, sectional, fluted) on a collar, a cast base or none, matte to
  glossy
- Wear, rust and dirt on the pole
- Halyard ropes, as thick as you like
- Your own colors, height and thickness
- Tilt and lean, for poles on walls

### Works with

- [Move It](https://steamcommunity.com/sharedfiles/filedetails/?id=1619685021): move, copy and
  paste flags with their designs, with undo
- Line Tool: place rows of flags in a line, a curve or a circle
- Unified UI
- 4 panel looks
- English, Spanish, French and Simplified Chinese

## Getting started

1. **Subscribe** to [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2040656402)
   and Flag Maker, and enable both (in Skyve too, if you use it).
2. **Load a city** (new or saved). The mod doesn't run in the editors.
3. **Press F6** (or click the mod's button) to open the panel. You can change the key in the mod's
   options.
4. **Click + New flag**, choose a design, then click on the map to place it. Keep clicking to place
   more, and press Esc when you're done.
5. **To change a placed flag**, click it in the list on the Flags tab, or click **Pick on the map**.

> [!TIP]
> To use your own pictures, put `.png` or `.jpg` files in the `FlagMaker\Images` folder. The
> folder button next to **Your image** opens it for you.

## Controls

**While placing a flag**

| Input | What it does |
| --- | --- |
| Left click | Places the flag. Click again to place another |
| Right click | Rotates it by 45° |
| Right-click and drag | Rotates it freely |
| Page Up / Page Down | Raises or lowers it, pole included: 12.5 cm per press, hold to keep going. Hold Shift to go 8× faster, Alt to go 8× finer |
| Home | Puts it back down after Page Up / Page Down |
| Ctrl+Z | Takes away the flag you just placed. Ctrl+Y (or Ctrl+Shift+Z) puts it back |
| Esc | Stops placing |

**With a flag selected**

| Input | What it does |
| --- | --- |
| Drag an arrow (red, green or blue) | Moves it along that axis |
| Drag the white knob | Moves it anywhere, keeping its height |
| Drag a ring | Rotates, tilts or leans it |
| Hold Ctrl / Shift while dragging | Snaps / moves in finer steps |
| Page Up / Page Down | Raises or lowers it, pole included |
| Delete | Deletes it (asks first) |
| Ctrl+Z / Ctrl+Y (or Ctrl+Shift+Z) | Undoes / redoes what you did to a flag: moves, turns, every design and flagpole change, even a delete. One step per drag |
| Esc, right click, or click somewhere else | Deselects it. Esc in the middle of a drag puts it back where it was |
| Double-click it in the list | Moves the camera to it |

On a Mac, the panel's key is Fn+F6 on most keyboards, and Page Up / Page Down are Fn+Up / Fn+Down
on a laptop. Undo and redo use Ctrl there too, not Cmd.

## Guide

### The New flag window

Click **+ New flag** at the top of the Flags tab. The flag follows your mouse, already waving, and
stands on whatever is under it: the ground, a roof or a road. If you point at a wall, it stands on
the ground in front of it. With a pole, the pole's foot goes there. Without one, the flag's top
corner on the pole side goes there, where a rope would clip on.

The window remembers your last flag's settings, and the small icon at the end of each row resets
that row.

| Setting | What it does |
| --- | --- |
| **Country** | Any country's or territory's flag. It keeps your flag's size and stretches to fit, turned on a tall shape the way a flag hung upright is shown; the Shape row's reset icon gives it its real proportions. Search by name (in your language or in English) or by country code. |
| **Your image** | A picture of your own, see [Your own pictures](#your-own-pictures). |
| **Pattern** | Opens the Pattern window, with 15 patterns in up to 3 colors and a preview. Pick colors from the palette, with the sliders, or type or paste a hex code. You can save a pattern and its colors under a name, and use it in any city. |
| **Proportions** | The flag's shape, from a tall banner to a long flag. |
| **Width**, **Height** | From 0.5 to 30 m each. Turn on **Lock width and height** to keep the proportions while you resize. |
| **Shape**, **Cut depth** | The outline of the flag's free end: rectangle, swallowtail, swallowtail and tongue, pennant, burgee, pointed or rounded. Cut depth sets how deep the notch or the point goes. The cloth itself is cut, not just the picture. |
| **Hangs from** | **Side**, like a flag on a pole, or **Top**, like a banner from a crossbar or a balcony. Hanging from the top, the design turns the way a real flag hung vertically does, with the corner box at the top left.[^flagcode] |
| **Motion** | **Waving** in the wind, **Loop**, **Still** or **Flat**, see [below](#loop-still-and-flat-flags). |
| **How wavy** | How much the cloth ripples, from 0 to 200%. |
| **Wind on it** | How much of the wind reaches the flag. At 0% a banner just hangs, and around 10 to 25% it sways gently. |
| **Darkness** | Tones a flag's colors down so its white and yellow don't glare in the sun: 30% to begin with, up to 80%. |
| **Worn**, **Torn**, **Torn edges**, **Dirt**, **Holes** | Wear and tear: faded colors and a frayed end, an end ripped to tatters like a pirate flag, torn top and bottom edges, grime and rain streaks, bullet holes, burns and tears. Every flag gets its own. |
| **Flagpole** | Opens the Flagpole window, see [Flagpoles](#flagpoles). |
| **Rotation** | Which way the flag points. |
| **Snap to edges** | A banner hanging from its top snaps to the edge of a roof near your mouse, so it hangs down the facade. |

### Loop, still and flat flags

A **loop** flag plays a few seconds of its own waving over and over. It flies in a steady breeze of
its own, the way it points, so it waves even when the game's wind is too weak to lift a flag. It
doesn't react to the wind, the weather or buildings, and your graphics card plays it back, so it
costs almost nothing however many you place. Flags with the same shape share one loop, each
starting at its own moment so a row is never in step. **Wind on it** sets how strong its breeze is.
The first flag of a shape takes a few seconds to appear while its loop is made.

A **still** flag is frozen in one shape, as if it was caught in a steady breeze blowing the way it
points. Rotate it to aim it (a banner hanging from its top just hangs). How wavy sets how rippled
it is, and flags with the same design get the same shape.

A **flat** flag stays completely flat, like on a wall or in a banner frame.

Neither of them moves with the wind or the weather, so once they're made they cost nothing to run.
They're good for long rows of flags, flags indoors or on walls, or if you just want your flags to
hold a pose. Switch one back to Waving and it starts flying again from where it was.

### Your own pictures

- Put `.png` or `.jpg` files in the `FlagMaker\Images` folder, in the game's data folder (see
  [Files and logs](#files-and-logs)). The folder button next to **Your image** opens it.
- Click the picture's name to open the Image window. It lists your pictures with thumbnails (new
  ones show up on their own while it's open), and shows the flag with your picture on it, the
  cropped parts dimmed.
- Pictures are never stretched. Picking one gives the flag the picture's shape, so all of it shows.
  While **Lock width and height** is on, the flag keeps its shape as you resize it. Pick another
  **Proportions**, or turn the lock off, to give the flag any shape, and the picture gets cropped to fit.
- Drag the picture to move it, and use the mouse wheel or the **Zoom** slider to zoom (100% just
  fills the flag, lower shows the whole picture with the flag's pattern around it). There's also
  **Fill**, **Whole picture**, **Turn 90°** and **Match its shape**.
- A PNG's transparent parts can be **Cut out**, so a pennant or swallowtail PNG makes a shaped flag,
  or **Show the flag** through them, which is good for a logo on a colored flag.
- On a banner hanging from its top, the picture stays upright. Pictures bigger than 2048 pixels are
  scaled down.
- Flags remember their picture by its file name. If the file is missing, the flag shows its pattern
  until the file is back. If you edit a picture, its flags update the next time you open the Image
  window.

### Flagpoles

- By default a flag comes with its own pole: tapered and sized like a real pole for that flag and
  height, with a collar at the bottom and a gold ball, a flat cap, a spear or a plain cylinder cap
  at the top. Set **Flagpole** to none to use a pole asset of your own.
- **Pole height** goes from 1 to 50 m, or up to 200 m with **Allow taller poles**.
- **Position on pole** lowers the flag down its pole: 100% at the top, 50% at half-mast. The pole
  stays as tall, on the same spot.
- With one or two halyard **Ropes**, the flag is only held at its top and bottom clips, so its edge
  bows out a little in the wind, like on a real pole. Without ropes, the whole edge is held.
- **Shape**: tapered (the default), straight, sectional (narrower steps up it, like telescoping
  poles) or fluted (grooved, like a ceremonial pole).
- **Base**: a flash collar (the default), a decorative cast base, a concrete block, or none. Any
  shape goes on any base.
- **Block**: the pole stands in a concrete block, square with chamfered edges, rounded or round.
  Set its width, height and color, and **Block wear** for stains, grime from the ground, streaks
  and chipped edges. The block always stands upright, even under a tilted pole. Only a pole that
  stands on its foot gets one.
- **Finish**: painted in the pole's color, or galvanized: bare zinc with its own grey and a faint
  pattern of zinc crystals. Worn galvanizing gets pale dull patches instead of chips.
- **Shine**: from matte to a glossy coat that reflects the sky, Real Glass's live sky and clouds
  when you have it. On a galvanized pole, from dull old zinc to bright new.[^poles]
- **Worn**, **Rust** and **Dirt**, under Wear and tear: paint chipped down to the primer (more at
  the foot and where the rope rubs); rust in the chips and running down from them; grime rising
  from the foot. Every pole wears in its own way.
- You can pick the colors of the pole, its top and its ropes, its **Thickness** and the
  **Rope thickness**.
- **Tilt** and **Lean** are for poles on walls: a wall-mounted pole usually leans 30 to 45°, a
  horizontal one 90°. The flag follows the tilt even without a drawn pole.
- The pole is part of the flag. Move, raise or lower the flag and its pole comes with it. Changing
  a placed flag's **Pole height** keeps the foot where it is, and the flag moves with the top.
- The cloth doesn't go through the pole: if the wind blows a flag back over its pole, it wraps
  around it.
- A banner hanging from its top has no pole (your pole comes back when you switch to Side). For a
  pole sticking straight out of a wall, tilt a flag that hangs from its side.

### The Flags tab

- It lists every flag in your city. Click one in the list (or use **Pick on the map**) to select
  it. Its design, size, pole and rotation show up in the tab, and any change applies right away.
- A selected flag gets a gizmo[^gizmo] with arrows, a knob and rings to move and rotate it (see
  [Controls](#controls)).
- **Go to** (or double-click it in the list) moves the camera to it, **Delete** removes it, and
  **Copy** opens the New flag window with its design, so you can place more like it.

### Settings

These are on the panel's Settings tab, and also in the mod's options (Pause menu → Options → Flag
Maker).

| Setting | What it does |
| --- | --- |
| **Use the game's wind** | On by default. Flags fly in the game's wind: a gentle breeze that gets stronger in the rain and calmer in fog, stronger on hills and higher up, and weaker in valleys and near forests. With Volumetric Weather on, flags use its wind instead. |
| **Wind speed**, **Gusts** | Your own wind, when the game's is off. The speed is at 10 m height (the panel shows its Beaufort force). |
| **Direction** | Which way flags fly, with the game's wind or your own: along each flag's rotation (the default), one fixed direction, or the game's (which slowly changes over time). |
| **Quality** | Sets Cloth detail, Cloth collides with itself, Detailed building shapes and Shared motion in one go, from Low to Ultra. Changing any of them yourself switches it to Custom. |
| **Cloth detail** | Low, Medium, High (default), Very high, Ultra or Extreme. Higher means finer cloth on flags close to the camera, and more CPU. |
| **Cloth collides with itself** | Folds lie on top of each other instead of going through. |
| **Cloth avoids buildings, props and trees** | On by default. **Detailed building shapes** uses the buildings' actual models instead of their simpler shapes. |
| **Shared motion** | On by default. When lots of similar flags are on screen, the smaller ones share one cloth's movement, each with its own timing, to save CPU. |
| **CPU threads for cloth** | Auto, or 1 to 16. More threads let more flags move at full detail. |
| **Freeze flags when paused** | Flags stop moving while the game is paused. A flag you move, rotate or edit still comes to life, then settles again. |

The mod's options also have **Where to show mod icon** (Unified UI, a button of its own on the
Toolbar that you can drag anywhere, or Never, since the key still opens the panel), **Panel look**
(Frosted glass, See-through, Blue or Vanilla), **Language** (the game's by default) and **Detailed
logging**.

### With Move It

- Flags are regular props, so Move It can select, move, rotate, raise, copy, mirror and delete
  them like anything else, with undo. Copies keep their design, and a raised or lowered flag keeps
  its pole.
- The cloth follows however fast you move it, leaning into the move a little like a real flag
  would, without ever going through itself. Lower one into the ground and it stays attached at the
  top instead of being torn off.
- Move It's highlight is a steady circle around everywhere the flag can reach, plus its pole.
- Move It picks a flag by the area its cloth covered in the last few seconds, plus its pole. That
  way the target doesn't jump around while the flag flutters, and it's never too small to click,
  even far away or edge-on. If a pole asset is right next to a flag, it can end up behind that
  target: use Move It's step-over or its filters to pick it. (The game's own tools, like the
  bulldozer, only pick the cloth itself.)

### With Line Tool

- If you have the Line Tool mod, the New flag window gets a **Place** row: **One flag**, **Straight
  line**, **Curve**, **Freeform** or **Circle**, with **Spacing** and **Point along the line**.
  Without Line Tool, you won't see any of this.
- Every flag in the row gets the window's design and its own pole, and you see them all before you
  click.
- Rows are placed on the ground, not on roofs or bridges. Use Page Up and Page Down to lift the
  whole row before placing it, or Move It afterwards.
- Line Tool skips points blocked by a road or a building (shown with a red ring), unless Prop
  Control's anarchy is on.

## Compatibility

Made and tested on Windows. **Mac and Linux are new and experimental** (their shaders are checked
but haven't been run on either yet), so please let me know how they run.

Harmony is required, and the game offers to subscribe to it if it's missing. Move It, Line Tool,
Unified UI and Volumetric Weather are all optional.

### Known issues / limitations

- **Extended Managers Library** (used by Prop Anarchy's larger prop limit) isn't supported yet. It
  stores a city's props where Flag Maker can't find them, so the mod does nothing in that city and
  tells you so. Your flags' designs are kept when you save.
- **Flags placed by other tools** (Find It, Picker, the game's own prop tool) get the New flag
  window's design. Place flags with the mod's own tool, or copy them with Move It, which keeps
  their designs.
- **Procedural Objects can't turn a flag into one.** It would only copy a flat placeholder, not the
  cloth, so the mod blocks it and the flag stays a flag. In a Move It selection, everything else
  still converts.
- **The cloth doesn't avoid roads and bridges yet**, or anything above a flag, like an arcade or a
  canopy. Still and flat flags don't avoid the city either, they keep the shape they were made with.
- **Only enable one copy of the mod.** A copy in your local Mods folder and a subscribed one would
  both run.

### Saves

Your flags are regular props in the game, and their designs are saved with the city.

> [!WARNING]
> If you load a city without the mod, its flags won't be there, like with any missing prop asset
> (nothing else changes). As long as you don't save, they'll all come back with the mod. **If you
> save the city without the mod, its flags are gone.**

If a city was saved with a newer version of the mod, or uses Extended Managers Library, the mod
leaves it alone and saves the flags' designs back exactly as they were. And if a city ever loses
its flags while loading (this has never happened in testing), the mod tells you and asks you not
to save.

### Files and logs

Everything the mod saves is in the game's data folder:

| Windows | Mac | Linux |
| --- | --- | --- |
| `%LOCALAPPDATA%\Colossal Order\Cities_Skylines` | `~/Library/Application Support/Colossal Order/Cities_Skylines` | `~/.local/share/Colossal Order/Cities_Skylines` |

| File | |
| --- | --- |
| `FlagMaker.log` | The mod's log, started fresh at every launch. |
| `FlagMaker.xml` | Your settings. Delete it (with the game closed) to reset everything. |
| `FlagMaker\Images` | Your pictures. |
| `FlagMaker\Patterns.xml` | Your saved patterns. |

### Reporting a bug

Please [open an issue](../../issues) and include the following:

1. **The game's log**, `output_log.txt`. In Steam, right-click Cities: Skylines → Manage → Browse
   local files, then open the `Cities_Data` folder. On a Mac it's `~/Library/Logs/Unity/Player.log`,
   on Linux `~/.config/unity3d/Colossal Order/Cities_Skylines/Player.log`.
2. **The mod's own log**, `FlagMaker.log`. Tick **Detailed logging** (Options → Flag Maker) first,
   then make the problem happen again.
3. **What happened**: what you did and expected, screenshots, your graphics card and your other
   mods.

Both logs start fresh at every launch, so copy them before restarting the game.

## How it works

Flag Maker is written from scratch, but it builds on a lot of published research. The sources are
credited in the notes at the bottom of the page. Click a topic to read more.

<details>
<summary><b>The cloth</b>: a grid of particles</summary>

<br>

- Each flag is a grid of particles held together by distance constraints, using position-based
  dynamics. Long range attachments to the pole side keep it from stretching like rubber, and the
  simulation takes smaller steps in strong wind.[^cloth] The edge along the pole is pinned where
  the pole would be.
- A new flag doesn't drop flat and whip through itself. It's lowered into the air, damped just
  enough to settle without swinging, the way engineers bring a membrane model to rest.[^relax]
- When you move a flag, its cloth is dragged through the air only as fast as it can follow, and the
  rest of the move just carries it along.[^drag]
- If a cloth ever gets knotted anyway (its most stretched link 30% too long for two seconds), it's
  laid out again.

</details>

<details>
<summary><b>It doesn't go through itself</b>: every particle is a small sphere</summary>

<br>

Each particle is a sphere about as wide as the grid's spacing, and no other particle can get
inside it. The mod finds nearby particles with a spatial hash and a neighbor list, and skips the
check while the cloth is too flat to fold onto itself.[^self] So a flag hanging in calm air lays
its folds on top of each other.

</details>

<details>
<summary><b>It keeps off the city</b>: roofs, walls, props and trees</summary>

<br>

- Around each flag, the mod maps the height of everything there (the ground, the water, the roofs)
  on a fine grid, and keeps the cloth above it.[^cloth] So a flag hangs onto a roof, a banner lies
  against the facade it hangs from, and a flag blown into a wall stops at it.
- With **Detailed building shapes** on, buildings are read from their actual models and their fixed
  props. Otherwise (or if a model can't be read), from the simpler shape the game uses for clicking
  on buildings.
- The cloth stops a few centimeters short of a wall rather than going into it.
- Props and trees are upright capsules, the same shapes the game uses for clicking on them, and the
  cloth keeps off them like it keeps off its pole. If a flag sits on a pole asset, that pole's
  capsule is narrowed so it doesn't push the flag away, and a wall right behind a pole is pushed
  back a little from the flag.
- A banner hanging from the top at a roof's edge is laid over the edge so it hangs down the facade,
  instead of lying on the roof.
- If you drag a flag through a building, it isn't snapped out all at once. The parts stuck inside
  are let out slowly, no faster than a dragged flag moves.[^physx] A new flag is laid out in the
  first direction that's clear of the walls around it.
- A flag high above everything costs nothing extra.

</details>

<details>
<summary><b>The air</b>: why a flag flutters</summary>

<br>

The air acts on the cloth the way the physics of fluttering flags describes it. As the cloth moves
sideways it carries some air with it (its "added mass"), and the flow pushes that air along, which
is what makes a flag flutter on its own in a steady wind. Drag across the cloth limits how far it
goes.[^air] Along the cloth there's only skin friction, so a flag streams out from the drag of its
own waves, and big flags fold and flap in a strong wind. The waves run from the pole to the free
end, slower than the wind, and grow towards the end, just like on real flags. The lower How wavy
is, the stiffer and rougher the cloth.

</details>

<details>
<summary><b>The wind</b>: stronger downtown and up high</summary>

<br>

- The wind follows the Eurocode, and knows the city around each flag.[^wind] Buildings make the
  terrain rougher, so downtown the wind gets stronger faster with height, and it's gustier. A flag
  on a tall pole or above the roofs downtown flies in a lot more wind than one down in the street.
- Gusts and eddies are the right size and strength, and they drift past with the wind, so flags a
  few meters apart feel the same gust one after the other. Flags follow changes in wind speed the
  way weather stations measure a 3-second gust.
- The game's wind has a direction but no speed. The mod takes its direction, and uses its weather
  and wind map (the same ones its wind turbines use) to make the breeze stronger or calmer. With
  Volumetric Weather on, flags use its wind and the same gusts that bend its trees.

</details>

<details>
<summary><b>The shader</b>: fabric, and wet in the rain</summary>

<br>

- The cloth has the soft sheen real fabric has at grazing angles, and lets light through from
  behind.[^shader]
- The design is drawn from its colors, a country's flag image (cut out where the image is, like
  Nepal's two pennants), or your picture, with a canvas strip along the pole side, two grommets and
  hems. Cut shapes are cut exactly, shadow included.
- Dry, it's matte. In the rain it gets wet like the city's ground (darker, with glints on the
  folds), and dries along with it.

</details>

<details>
<summary><b>Wear and tear</b>: drawn on the cloth</summary>

<br>

Worn, torn, dirty and holed flags are drawn by the shader at real sizes on the cloth, so a bullet
hole is about a centimeter wide on any flag. Fractal noise makes the fading, the frayed end and the
grime. Holes are scattered across the cloth (bullet holes with the dark ring a bullet leaves in
cloth, burns and tears), and the tatters are shaped with signed distance functions.[^wear] Every
flag gets its own, and the holes show in its shadow too. It's only the look though: a torn flag
still moves like a whole one.

</details>

<details>
<summary><b>The drawn surface</b>: smooth curves</summary>

<br>

A flag that's big on screen is drawn up to four times finer than its particles, on a smooth
Catmull-Rom surface through them, computed on the GPU. Its edges are smoothed first too, so the
outline and the folds are curves instead of polygons, and the physics doesn't cost any more.[^surface]

</details>

<details>
<summary><b>The pole</b>: sized like a real one</summary>

<br>

- The pole is sized the way real flagpoles are, for how hard the wind pulls on its flag at that
  height. The cloth keeps off it.
- A tilted pole turns the whole cloth with it (Rodrigues' rotation).
- On halyard ropes, the flag is held at two clips, with the lower one pulled up 3% (a real halyard
  is never perfectly tight). The canvas strip along the edge is stiffer than the flag, like on a
  real one, and free to move between the clips.

</details>

<details>
<summary><b>Still flags</b>: one shape, picked carefully</summary>

<br>

A still flag is simulated once: laid out flat, then flown for a few seconds in a steady breeze.
That's Beaufort force 3 ("light flags extended") for a flag up to 3 m, and stronger for bigger
flags so they hold out against their weight the same way.[^still] Over the last two seconds the
shape is checked every eighth of a second, and the mod keeps the smoothest one that shows the most
of the design (just taking one moment often froze the corner mid-flick). The same design always
gets the same shape, rotated to where each flag points, and it's never simulated again.

</details>

<details>
<summary><b>Loop flags</b>: simulated once, played back by the graphics card</summary>

<br>

A loop flag's cloth is simulated once, in the same steady breeze a still flag is caught in, and
recorded for about 17 seconds. A cloth never repeats on its own, so the mod looks for the two
moments in the recording, 4 to 6 seconds apart, where the cloth looks most alike over three
quarters of a second, cuts the loop there and cross-fades the end into the moments before its
start.[^loop] The frames go into a texture, and the flag's shader reads the cloth from it, so a
loop flag has no cloth of its own and is a single draw. Flags far away are drawn with a few dozen
points on the same surface. Bigger flags get longer loops, up to 12 seconds.

</details>

<details>
<summary><b>Your pictures</b>: cropped, never stretched</summary>

<br>

The flag shows a window into the picture with the flag's own shape, as large as it fits at 100%.
The hidden colors under a PNG's transparent pixels are filled in from the visible ones, so cut-out
edges don't get a dark fringe.[^pictures]

</details>

<details>
<summary><b>Detail and many flags</b>: scaled to the CPU you have</summary>

<br>

- Cloth detail goes from 18 to 64 cells along a flag. Only flags that are big on screen get the
  finest detail, the rest only as fine as their size needs. Ultra and Extreme only refine flags
  over about 3 m (smaller ones are already at their finest), and cost two to four times as much as
  Very high.
- When there are more flags on screen than the CPU has time for, similar flags share their motion
  first. One hidden cloth is simulated, and the smallest flags on screen play back its last few
  seconds, each with its own delay and turned its own way, the way games animate crowds.[^crowds]
  A group of six or more similar flags shares from the start.
- Only then do the smallest flags get coarser and update half as often, scaled to the budget like
  dynamic resolution. Never the flag you're holding, or one close to a building or the ground.

</details>

<details>
<summary><b>Threads and timing</b>: off the game's main thread</summary>

<br>

- The cloth runs on its own threads (one for every six CPU threads, up to four, or set **CPU
  threads for cloth** yourself), 60 times a second, while the game draws the frame, and it's drawn
  smoothly between steps.[^timestep] The biggest flags on screen go first, within a time budget,
  and flags that had to wait go first next time.
- How far away a flag is drawn and moves depends on how big it is on screen, not on a fixed
  distance, so zooming in keeps a far flag moving. With the game's usual camera, a 3 m flag is drawn
  up to about 4 km away and waves up to about 1.3 km. A 20 m flag goes several times further.
- Move It's pick target holds steady while the flag flutters, and is never smaller than a
  fingertip on screen.[^pick]

</details>

## Building

See [BUILDING.md](BUILDING.md) for the details. In short, clone with `--recurse-submodules`, then:

```powershell
.\build-bundle.ps1 -Only win            # only after changing a shader: compiles it with Unity 5.6.6
dotnet build RealFlag/RealFlag.csproj   # builds the mod and deploys it to the game's Mods folder
```

The compiled shaders are in the repo, so a normal build only needs the second line. Without
`-Only`, `build-bundle.ps1` compiles them for Windows, Mac and Linux.

## Credits

<sub>Flag Maker is written from scratch, but it builds on a lot of research and on a few ideas from
other mods, all credited in the notes below (no code from them is included). Country flags are from
[Flagpedia.net](https://flagpedia.net) (flagcdn.com), based on Wikimedia Commons' files, public
domain. Country names in Spanish, French and Chinese are from the [Unicode CLDR](https://cldr.unicode.org)
(Unicode License V3). The panel's icons are based on [Lucide](https://lucide.dev)'s (ISC). The
flagpoles' rust, rope and concrete textures are from [Poly Haven](https://polyhaven.com) and
[ambientCG](https://ambientcg.com) (CC0). Includes
UnifiedUILib by kianzarrin, CitiesHarmony.API by boformer and MoveItIntegration by Quboid, thanks
to all three. Full license info is in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).</sub>

<sub>This mod was made with substantial help from AI. I set the goals, made the design decisions,
and did all of the in-game testing and tuning.</sub>

## License

MIT, see [LICENSE](LICENSE).

[^flagcode]: The United States Flag Code, 4 U.S.C. § 7(i): a flag hung vertically shows its union (the corner box) at the top, to the flag's own right.
[^gizmo]: Inspired by the 3-axis gizmos of *Procedural Objects* (a Cities: Skylines mod) and of 3D editors. The idea only, no code.
[^cloth]: **The cloth.** T. Jakobsen, *Advanced Character Physics* (GDC 2001); M. Müller, B. Heidelberger, M. Hennix and J. Ratcliff, *Position Based Dynamics* (2007); T.-Y. Kim, N. Chentanez and M. Müller, *Long Range Attachments* (SCA 2012); M. Macklin et al., *Small Steps in Physics Simulation* (SCA 2019).
[^relax]: **A new flag lowered, not dropped.** A. S. Day, *An introduction to dynamic relaxation* (The Engineer, 1965); M. R. Barnes, *Form-finding and analysis of prestressed nets and membranes* (Computers & Structures, 1988).
[^drag]: **A moved flag.** The world velocity and inertia scales of NVIDIA PhysX's and Unity's cloth: the idea of dragging a moved flag's cloth only partly.
[^self]: **The cloth against itself.** M. Macklin, M. Müller, N. Chentanez and T.-Y. Kim, *Unified Particle Physics for Real-Time Applications* (SIGGRAPH 2014); M. Müller, *Ten Minute Physics* 15, *Self-collisions* (2022); M. Teschner, B. Heidelberger, M. Müller, D. Pomeranets and M. Gross, *Optimized Spatial Hashing for Collision Detection of Deformable Objects* (VMV 2003); L. Verlet, *Computer "Experiments" on Classical Fluids* (Phys. Rev., 1967); P. Volino and N. Magnenat-Thalmann, *Efficient Self-Collision Detection on Smoothly Discretised Surface Animations using Geometrical Shape Regularity* (Eurographics 1994).
[^physx]: **Cloth stuck in a building.** NVIDIA PhysX's maximum depenetration velocity (Unity's Rigidbody.maxDepenetrationVelocity): the idea of letting it out slowly.
[^air]: **The air on the cloth.** M. J. Lighthill, *Note on the swimming of slender fish* (J. Fluid Mech., 1960); C. Eloy, C. Souilliez and L. Schouveiler, *Flutter of a rectangular plate* (J. Fluids Struct., 2007); M. Argentina and L. Mahadevan, *Fluid-flow-induced flutter of a flag* (PNAS, 2005); M. J. Shelley and J. Zhang, *Flapping and bending bodies interacting with fluid flows* (Annu. Rev. Fluid Mech., 2011); S. Taneda, *Waving motions of flags* (J. Phys. Soc. Japan, 1968); P. Causin, J.-F. Gerbeau and F. Nobile on the added-mass effect (2005); S. F. Hoerner, *Fluid-Dynamic Drag* (1965); L. Prandtl's turbulent flat-plate friction, in H. Schlichting, *Boundary-Layer Theory*.
[^wind]: **The wind.** EN 1991-1-4, Eurocode 1: wind actions; A. G. Davenport, *Rationale for determining design wind velocities* (1960) and ASCE 7-95's exposures: how the wind grows with height over each terrain; H. Lettau (1969) and C. S. B. Grimmond and T. R. Oke, *Aerodynamic properties of urban areas derived from analysis of surface form* (1999): how rough the city is, from its buildings; H. A. Panofsky and J. A. Dutton, *Atmospheric Turbulence* (1984); G. I. Taylor (1938) and A. N. Kolmogorov (1941); J. C. Kaimal, J. C. Wyngaard, Y. Izumi and O. R. Cote, *Spectral characteristics of surface-layer turbulence* (1972), and K. Perlin, *An Image Synthesizer* (1985): gusts and eddies of several sizes; WMO-No. 8, *Guide to Instruments and Methods of Observation*, ch. 5 (the 3 s gust): how quickly flags follow the wind's speed; K. Perlin, *Improving Noise* (2002), and C. Wellons' lowbias32 hash: the noise.
[^shader]: **The fabric.** A. C. Estevez and C. Kulla, *Production Friendly Microfacet Sheen BRDF* (2017); D. Neubelt and M. Pettineo, *Crafting a Next-Gen Material Pipeline for The Order: 1886* (2013); S. Green, *Real-Time Approximations to Subsurface Scattering* (GPU Gems, 2004). **Wet in the rain:** S. Lagarde, *Water drop 3b, Physically based wet surfaces* (2013); D. Lazarov, *Physically-based lighting in Call of Duty: Black Ops* (SIGGRAPH 2011).
[^wear]: **Wear and tear.** D. Hoskins, *Hash without Sine* (Shadertoy, 2014, MIT): the shader's hashes; S. Worley, *A Cellular Texture Basis Function* (SIGGRAPH 1996): scattering the holes; F. K. Musgrave et al., *Texturing and Modeling: A Procedural Approach* (1994): fractal noise for the frayed end and the grime; I. Quilez's 2D signed distance functions: the shapes of the holes and tears; forensic science's "bullet wipe": the dark ring around a bullet hole in cloth.
[^poles]: **The poles.** M. Tarini, *Cylindrical and Toroidal Parameterizations Without Vertex Seams* (Journal of Graphics Tools, 2012): wrapping a texture around the pole with no seam; [Poly Haven](https://polyhaven.com)'s *Rust Coarse 01* and [ambientCG](https://ambientcg.com)'s *Rope 001* and *Concrete 016* (all CC0): the rust, the rope's braid and the concrete block; S. Worley, *A Cellular Texture Basis Function* (SIGGRAPH 1996): the zinc crystals of a galvanized pole; C. Schlick, *An Inexpensive BRDF Model for Physically-based Rendering* (1994) and N. Hoffman, *Physics and Math of Shading* (SIGGRAPH 2013): the shine's gloss coat, 4% head-on, as Real Glass's; D. Hoskins, *Hash without Sine* (Shadertoy, 2014, MIT) and F. K. Musgrave et al. (1994), as for the cloth's wear: the chips, the rust patches and the grime.
[^surface]: **The drawn surface.** E. Catmull and R. Rom, *A class of local interpolating splines* (Computer Aided Geometric Design, 1974); G. Taubin, *A Signal Processing Approach to Fair Surface Design* (SIGGRAPH 1995).
[^still]: **Still flags.** The Met Office's Beaufort wind force scale (force 3, "light flags extended"): the breeze a still flag is caught in; W. Froude's similarity (the Froude number, U²/gL): a stronger breeze for a bigger flag; W. Welch and A. Witkin, *Variational Surface Modeling* (SIGGRAPH 1992), the thin-plate energy: picking the smoothest moment.
[^pictures]: **Your pictures.** The images folders of *Procedural Objects* and *Image Overlay* (Cities: Skylines mods): a folder button and a list of the files, the idea only, no code; S. J. Gortler, R. Grzeszczuk, R. Szeliski and M. F. Cohen, *The Lumigraph* (SIGGRAPH 1996): the "pull-push" fill under transparent pixels; the PNG specification (W3C / ISO/IEC 15948) and JPEG (ITU-T T.81): reading a picture's size from its header.
[^loop]: **Loop flags.** A. Schödl, R. Szeliski, D. Salesin and I. Essa, *Video Textures* (SIGGRAPH 2000): finding where a recording can repeat and cross-fading the seam; the vertex animation texture games bake cloth and crowds into: the idea of reading the motion from a texture in the shader; G. Fowler, L. C. Noll and K.-P. Vo's FNV-1a hash: the checksum of the loops kept on disk.
[^crowds]: **Many flags.** B. Dudash, *Animated Crowd Rendering* (GPU Gems 3, ch. 2, 2007): similar flags playing back one cloth's motion, each with its own delay; D. Binks, *Dynamic Resolution Rendering* (Intel, 2011): scaling the work to a measured budget, smallest flags first; H. Weyl's equidistribution (1916): the golden ratio sequence that spreads the delays.
[^timestep]: G. Fiedler, *Fix Your Timestep!* (2004).
[^pick]: **Picking a flag.** C. Ericson, *Real-Time Collision Detection* (2005); the OpenGL Programming Guide's picking region a few pixels around the cursor (gluPickMatrix): the smallest a flag's pick target gets on screen; U. Zölzer, *Digital Audio Signal Processing* (2008), dynamic range control's peak measurement (an instant attack, a slow release): holding the pick target steady while the flag flutters. Ericson's moving box against a plane, with M. Cyrus and J. Beck, "Generalized Two- and Three-Dimensional Clipping", *Computers and Graphics* 3 (1978): keeping a flag that is off the screen while its shadow is on it.
