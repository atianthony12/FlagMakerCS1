# RealFlag

Cloth flags that wave in the wind, for **Cities: Skylines** (the first game). Every flag is a
real piece of cloth, simulated as it hangs and flutters, and drawn with a fabric shader in the
game's own lighting: the sun and its shadows, the street lights at night, the fog.

A flag comes **without a pole** unless you ask for one. Its reference point is the top corner of
its hoist edge, where a pole's halyard would clip on, and the hoist edge hangs down from it --
straight down, or along a tilted pole. Put it on a pole of your choosing, a mast, a roof or a
facade; or let the mod draw a flagpole under it.

## Using it

Open the panel with **F6** (or its Unified UI button).

**Create new**: pick a country's flag -- every country and territory, England, Scotland,
Wales, Northern Ireland, the EU and the UN, flown as it is at its own proportions (search by
name or code) -- or a pattern (plain, bands, crosses, a saltire, a canton, stripes and
canton, a disc, a hoist triangle, a diagonal band, a border) in up to three colours (a palette,
hue, saturation and brightness, or a hex code typed, pasted or copied); then the size (hoist to
fly, 0.5 to 30 m), the proportions
(1:1 to 1:2, a pattern's), how wavy the cloth is, the way it points and its height over the
ground. Press
**Place flags** and click where the pole would stand: the flag appears, blowing, at the chosen
height over whatever is under the cursor (ground, roof, road). Click again for another; right
click or Esc when done.

**Flagpole** (a button in each tab, which opens beside the panel): none by default -- the flag
is for a pole of your own -- or a pole the mod draws from whatever the flag stands over up to
the flag: 64-sided, tapered like a real one and as thick as one for that flag and height, with
a flash collar at its foot, a truck at its top and a gilt ball, a flat cap or a spear on it,
one or two halyard lines, your colours for the pole, its top and its ropes, and a thickness to
taste. With ropes, the flag hangs from its top and bottom clips only, as a hoisted flag does,
and its edge bows out between them in the wind; without, it is held along its whole edge. The
cloth keeps off the pole: a flag the wind turns back over its pole wraps round it. Here
too the pole's **tilt** and **lean** (a wall-mounted pole leans 30 to 45 degrees, a horizontal
one 90) -- the flag hangs from the tilted line whether a pole is drawn or not.

**Existing flags**: every flag in the city. Click one in the list (or **Pick on the map**) to
select it: its design, pole, rotation and height are in the tab, and it carries a **gizmo** --
drag its red, green and blue arrows to move it along its own axes (the white knob slides it
anywhere at its height), its rings to turn it, tilt it or lean it; hold Ctrl to snap, Shift to
go finely. Click elsewhere on the map, right-click or press Esc to let it go (Esc during a drag
puts the flag back). **Go to** flies the camera there, **Copy to Create new** takes its design
for the next ones, **Delete** removes it.

**Settings**: the **wind** is the game's own by default -- its direction, which turns slowly
over time; a gentle breeze made stronger by rain and calmer by fog, as the game's weather has
it; more wind on hills and less in valleys and by forests, as on the game's wind map; and more
the higher a flag flies. Or switch it off and set your own: the speed at 10 m (the panel shows
its Beaufort force), how gusty it is, and where it blows -- along each flag (every flag flies
the way it was placed), one fixed direction for all, or the game's direction at your speed.
Here too the **cloth detail** and whether the cloth **collides with itself**, and whether flags
stop while the game is paused.

**While the game is paused** the flags stand still (an option), but a flag you move, turn or
redesign comes to life under your hand and settles back into a still a moment after.

### With Move It

Flags are the game's props, so **Move It** selects, moves, turns, raises, copies, mirrors and
deletes them like any other, with undo. A copied flag keeps its design. A flag is picked
anywhere on or near its cloth, along its hoist edge or on its pole -- a wide target even seen
edge on, however high it flies -- and Move It's highlight covers the whole flag and its pole:
these need the Harmony mod, which the game offers to subscribe to when it is missing.

## How it works

- **The cloth** is a grid of particles held together by distance constraints: position-based
  dynamics (Jakobsen 2001; Müller et al. 2007), with long range attachments to the hoist so it
  never stretches like rubber (Kim, Chentanez and Müller 2012) and shorter steps in strong
  wind (Macklin et al. 2019). The hoist edge is pinned where the pole would be.
- **It does not go through itself**: every particle is a sphere as wide as the grid's spacing
  that no other may enter (Macklin et al. 2014; Müller's *Ten Minute Physics*), found through a
  spatial hash (Teschner et al. 2003) and a neighbour list (Verlet 1967), and skipped while the
  cloth is too flat to fold onto itself (Volino and Magnenat-Thalmann 1994). A flag hanging in
  calm air lays its folds on one another.
- **Cloth detail** (the options page, or the foot of the panel): Normal, High, Very high or
  Ultra -- 18 to 40 cells along a flag. Only flags big on the screen get the finest; the others
  as fine as their size there needs.
- **The drawn surface** is smooth: a flag big on the screen is drawn up to four times finer
  than its particles, on the Catmull-Rom surface through them (Catmull and Rom 1974), worked
  out on the GPU from the particles in a small texture, with its normals from the surface; its
  edges are smoothed as curves first (Taubin 1995), so a flying flag's outline and folds are
  curves, not polygons -- and the physics costs what it did.
- **The pole** is sized as flagpoles are, for the wind's bending moment on its flag at its
  height (its foot grows as the cube root of flag area times height), and is a cylinder the
  cloth's particles keep off. A tilted hoist turns the whole cloth with it (Rodrigues' rotation).
  On its halyard a flag is held at its two clips, the lower drawn up 3% of the hoist (a halyard
  is never bar-tight), and its heading -- stiffer than the flag, as canvas is -- is free between.
- **The air** acts on it as the theory of the fluttering flag has it: the added mass of the air
  the cloth carries sideways, pushed along by the flow (Lighthill 1960; Eloy, Souilliez and
  Schouveiler 2007), which is what makes a flag in a steady wind flutter by itself, and a flat
  plate's drag across the cloth (Hoerner 1965), which limits how far. Along the cloth, only
  the air's skin friction (Prandtl's law): a flag streams out on the drag of its own waves, and
  big flags fold and flap in a strong wind. Waves run from the hoist to the fly, slower than
  the wind, and grow towards the fly, as real flags' do. The less wavy a flag is set, the
  stiffer and rougher its cloth.
- **The wind** follows the Eurocode for towns (EN 1991-1-4): more wind higher up, gusts of the
  right size and strength, and eddies the size of a flag carried past it with the wind, so
  flags a few metres apart feel the same gust one after the other. The game's own wind, which
  has a direction but no speed, gives it its direction, and its weather and wind map (the ones
  its wind turbines use) make a gentle breeze stronger or calmer.
- **The shader** is a two-sided fabric: the "Charlie" sheen of cloth at grazing angles
  (Estevez and Kulla 2017; Neubelt and Pettineo 2013), light shining through the cloth from
  behind (GPU Gems ch. 16), and the design drawn from its colours -- or a country's flag from
  its image, cut out where the image is (Nepal's two pennants) -- with a canvas heading, two
  grommets and hems.
- The cloth is stepped on threads of its own (one per six of the machine's, up to four), 60
  times a second, while the game draws the frame; the nearest flags first, within a time
  budget.

The designs are saved with the city. Without the mod, a city simply has no flags.

## Building

See [BUILDING.md](BUILDING.md). In short: `.\build-bundle.ps1 -Only win` compiles the cloth
shader with Unity 5.6.6, then `dotnet build RealFlag/RealFlag.csproj` builds the mod and deploys
it to the game's Mods folder.

## Credits

Written from scratch after these, with thanks (no code from them is included):

- T. Jakobsen, *Advanced Character Physics* (GDC 2001); M. Müller, B. Heidelberger, M. Hennix
  and J. Ratcliff, *Position Based Dynamics* (2007); T.-Y. Kim, N. Chentanez and M. Müller,
  *Long Range Attachments* (SCA 2012); M. Macklin et al., *Small Steps in Physics Simulation*
  (SCA 2019) -- the cloth; the world velocity and inertia scales of NVIDIA PhysX's and Unity's
  cloth -- the idea of a moved flag dragging its cloth only in part.
- M. Macklin, M. Müller, N. Chentanez and T.-Y. Kim, *Unified Particle Physics for Real-Time
  Applications* (SIGGRAPH 2014); M. Müller, *Ten Minute Physics* 15, *Self-collisions* (2022);
  M. Teschner, B. Heidelberger, M. Müller, D. Pomeranets and M. Gross, *Optimized Spatial
  Hashing for Collision Detection of Deformable Objects* (VMV 2003); L. Verlet, *Computer
  "Experiments" on Classical Fluids* (Phys. Rev., 1967); P. Volino and N. Magnenat-Thalmann,
  *Efficient Self-Collision Detection on Smoothly Discretised Surface Animations using
  Geometrical Shape Regularity* (Eurographics 1994) -- the cloth against itself.
- M. J. Lighthill, *Note on the swimming of slender fish* (J. Fluid Mech., 1960); C. Eloy,
  C. Souilliez and L. Schouveiler, *Flutter of a rectangular plate* (J. Fluids Struct., 2007);
  M. Argentina and L. Mahadevan, *Fluid-flow-induced flutter of a flag* (PNAS, 2005);
  M. J. Shelley and J. Zhang, *Flapping and bending bodies interacting with fluid flows*
  (Annu. Rev. Fluid Mech., 2011); S. Taneda, *Waving motions of flags* (J. Phys. Soc. Japan,
  1968); P. Causin, J.-F. Gerbeau and F. Nobile on the added-mass effect (2005);
  S. F. Hoerner, *Fluid-Dynamic Drag* (1965); L. Prandtl's turbulent flat-plate friction, in
  H. Schlichting, *Boundary-Layer Theory* -- the air on the cloth.
- EN 1991-1-4, Eurocode 1: wind actions; H. A. Panofsky and J. A. Dutton, *Atmospheric
  Turbulence* (1984); G. I. Taylor (1938) and A. N. Kolmogorov (1941) -- the wind.
- A. C. Estevez and C. Kulla, *Production Friendly Microfacet Sheen BRDF* (2017); D. Neubelt
  and M. Pettineo, *Crafting a Next-Gen Material Pipeline for The Order: 1886* (2013);
  S. Green, *Real-Time Approximations to Subsurface Scattering* (GPU Gems, 2004) -- the fabric.
- E. Catmull and R. Rom, *A class of local interpolating splines* (Computer Aided Geometric
  Design, 1974); G. Taubin, *A Signal Processing Approach to Fair Surface Design* (SIGGRAPH
  1995) -- the drawn surface.
- The gizmo follows the idea of the 3-axis gizmos of *Procedural Objects* (a Cities: Skylines
  mod) and of 3-D editors' transform handles -- the idea only, no code.
- K. Perlin, *Improving Noise* (2002); C. Wellons' lowbias32 hash; T. Möller and B. Trumbore,
  *Fast, Minimum Storage Ray/Triangle Intersection* (1997); C. Ericson, *Real-Time Collision
  Detection* (2005) -- picking a flag; G. Fiedler, *Fix Your Timestep!* (2004).
- The countries' flags: [Flagpedia.net](https://flagpedia.net) (flagcdn.com), after
  Wikimedia Commons' vector files -- public domain, with thanks.

## License

MIT -- see [LICENSE](LICENSE). The libraries shipped with it are listed in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
