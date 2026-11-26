using System;

namespace RealFlag.Flags
{
    /// <summary>What a flag is made of: how heavy, how the air takes hold of it, how it folds.</summary>
    public struct FlagFabric
    {
        /// <summary>
        /// Mass per area, kg/m^2. 0.115: knitted polyester, the usual outdoor flag cloth
        /// (110-130 g/m^2; 200-denier nylon is about 80, two-ply polyester 200-300).
        /// </summary>
        public float Density;

        /// <summary>
        /// C_A, the share of Lighthill's added mass that acts: the air a flag of height H drags
        /// sideways with it is rho pi H^2 / 4 per metre of its length (Lighthill 1960, the
        /// slender-body value), less for a flag as tall as it is long (Eloy, Souilliez and
        /// Schouveiler 2007). It sets how hard the flow drives the flutter.
        /// </summary>
        public float AddedMass;

        /// <summary>
        /// C_D, the resistive drag across the cloth, ½ rho C_D |V_n| V_n per area: a flat plate
        /// broadside to the flow (1.17 for a square plate, Hoerner 1965, Fluid-Dynamic Drag,
        /// 3-16). It limits the flutter's amplitude (the "Taylor" resistive term of Eloy et al.).
        /// </summary>
        public float Drag;

        /// <summary>
        /// C_T, the drag along the cloth: skin friction, both faces. A flat plate's turbulent
        /// skin friction at a flag's Reynolds number (U L / nu 1e6 to 6e7: a 3 m flag in 5 m/s,
        /// a 30 m one in 30) is 0.004-0.009 for both faces together (Prandtl's 0.074 Re^-1/5 a
        /// face: Schlichting, Boundary-Layer Theory, the turbulent flat plate); woven cloth is
        /// rougher: 0.01. The drag of the flag's waves is NOT in it -- the air's normal forces on
        /// the waving cloth are that drag already. The first value, 0.08, took them in twice,
        /// and the friction it gave (its rate, rho C_T U / 2 sigma: 4.7 /s at 11 m/s) damped the
        /// cloth's motion along itself as fast as the flutter grew: in a steady 11 m/s a 3 m
        /// flag's fly moved 0.9% of its length, 18% without it; in the game's gusty air an 8 m
        /// flag stayed flat (1.8%, the author: "the cloth is mostly staying flat"), 4.4% at 0.01.
        /// </summary>
        public float Friction;

        /// <summary>Stiffness of the diagonal links over one step, 0..1: how much the weave resists shearing.</summary>
        public float Shear;

        /// <summary>Stiffness of the links two apart over one step, 0..1: how much the cloth resists folding.</summary>
        public float Bend;

        /// <summary>
        /// How much a link resists being SHORTENED, as a share of its stiffness, 0..1. Cloth is
        /// nearly inextensible but buckles at once when pushed together: resisting that as
        /// hard as stretching makes it a sheet of card.
        /// </summary>
        public float Compression;

        /// <summary>Constraint passes per step (Gauss-Seidel).</summary>
        public int Iterations;

        /// <summary>Velocity lost per second besides the air's own drag (the fabric's internal friction), 1/s.</summary>
        public float Damping;

        /// <summary>
        /// A flag of ordinary cloth at the given waviness (0..2, the panel's setting): wavier
        /// cloth folds more easily and is smoother along its surface (a rough, stiff weave holds
        /// the air along it more, and flaps less). Friction C_T 0.01 at 100%, x4 at 0% (0.04),
        /// /4 at 200% (0.0025).
        /// </summary>
        public static FlagFabric Standard(float waviness)
        {
            float w = Math.Max(0f, Math.Min(waviness, 2f));
            return new FlagFabric
            {
                Density = 0.115f,
                AddedMass = 0.5f,
                Drag = 1.17f,
                Friction = 0.01f * (float)Math.Pow(4.0, 1.0 - w),
                Shear = 0.7f,
                Bend = 0.1f / (0.5f + 0.5f * w),
                Compression = 0.15f,
                Iterations = 5,
                Damping = 0.08f,
            };
        }
    }

    /// <summary>
    /// The cloth of one flag: a grid of particles held along its hoist edge -- the line between
    /// the two hoist corners, where the pole would be -- moved by gravity and the air, and held
    /// together by distance constraints. PURE: no engine call; the tests, the GPU preview and the
    /// game all step this same code.
    /// </summary>
    /// <remarks>
    /// Position-based dynamics (Jakobsen 2001, "Advanced Character Physics" -- the Hitman cloth
    /// -- and Mueller, Heidelberger, Hennix and Ratcliff 2007, "Position Based Dynamics"):
    /// forces change the velocities, positions are predicted, then the constraints are
    /// projected on the predictions in a few Gauss-Seidel passes, and the velocities are what
    /// the projection left. Stiffnesses are per step: the per-pass value is 1 - (1 - k)^(1/n)
    /// (Mueller et al. section 3.3), so changing the pass count does not change the cloth.
    ///
    /// Long range attachments (Kim, Chentanez and Mueller 2012): every particle is tied to the
    /// hoist point of its own row by the flat distance between them, as an upper bound only. A
    /// few passes cannot stop a long chain of links stretching under its own weight; the
    /// attachment does it in one projection, so the cloth never goes elastic.
    ///
    /// The cloth does not go through itself: its particles are spheres a cell wide (wider at a
    /// finer detail) that the passes keep apart like the links (<see cref="CollisionWidth"/>), found through a
    /// spatial hash and a neighbour list, skipped while the cloth is too flat to fold onto
    /// itself. A hanging flag's folds, which went through one another in every frame of calm
    /// air, now lie against one another.
    ///
    /// The air, as the theory of the fluttering flag has it (Lighthill 1960, "Note on the
    /// swimming of slender fish", J. Fluid Mech. 9; as a flag's: Eloy, Souilliez and Schouveiler
    /// 2007, J. Fluids Struct. 23, and the review of Shelley and Zhang 2011, Annu. Rev. Fluid
    /// Mech. 43). With V_n the air's velocity relative to the cloth along its normal n (for a
    /// small wave w, V_n = -(dw/dt + U dw/ds): the flow sees the slope as well as the motion), the
    /// air pushes each area of cloth with
    ///   REACTIVE  m (d/dt + U d/ds) V_n,  m = C_A rho pi H / 4,
    /// the added mass of the air the flag carries sideways times its rate of change following
    /// the flow -- the term that makes a flag in a steady wind flutter by itself: it pushes a
    /// crest further out in proportion to U^2 times its curvature; and
    ///   RESISTIVE ½ rho C_D |V_n| V_n,
    /// a plate's drag across the flow, which lets small ripples grow and stops big ones.
    /// Along the cloth, ½ rho C_T |V| V_t: skin friction alone (C_T 0.01) -- what streams a
    /// flag out downwind is mostly the drag of its own waves, which the normal forces carry.
    ///
    /// Taken IMPLICITLY where it must be. The added mass is ~17 times the cloth's for a 2 m tall
    /// flag, and an added mass applied explicitly, from the last step's acceleration, blows up
    /// (Causin, Gerbeau and Nobile 2005, "Added-mass effect in the design of partitioned
    /// algorithms for fluid-structure problems"): so the cloth's own acceleration in the
    /// reactive term is moved to the left-hand side, and every normal force accelerates the
    /// cloth's mass plus the air's. The drags are relaxations of the velocity towards the air's,
    /// Delta v = V lambda dt / (1 + lambda dt) (backward Euler): at 20 m/s lambda is ~200 /s,
    /// and an explicit step of 1/60 s would overshoot.
    ///
    /// Frame: positions are relative to the ANCHOR, the top hoist corner (the prop's position),
    /// on the world's axes; y is up. The hoist edge is the line from (0, 0, 0) down along the
    /// hoist direction (<see cref="HoistX"/>: straight down, (0, -1, 0), unless the flag hangs
    /// from a tilted pole) for Height metres. Particle (i, j) is column i (0 at the hoist,
    /// Columns - 1 at the fly) of row j (0 at the top): index j * Columns + i.
    ///
    /// A pole the mod draws (<see cref="SetPole"/>) is a cylinder the particles keep off, as they
    /// keep off each other: a flag the wind turns back over its pole wraps round it.
    ///
    /// And the city round it (FlagSurroundings, gathered from the game): the top of what stands
    /// under the flag -- ground, water, roofs, a wall as a slope one node of the grid wide --
    /// which the cloth stays above (<see cref="SetSurface"/>), and the props and trees as the game
    /// itself picks them, upright capsules (<see cref="SetCapsules"/>). The same projection as the
    /// pole's, each pass, with a little friction; only what the cloth's box reaches this step is
    /// looked at.
    /// </remarks>
    public sealed class FlagCloth
    {
        public const float Gravity = 9.81f;

        /// <summary>Air at 15 C at sea level, kg/m^3 (ISA).</summary>
        public const float AirDensity = 1.225f;

        /// <summary>
        /// Cells along the longer side at the Normal detail: waves a sixth of the flag long get six
        /// particles each.
        /// </summary>
        public const int CellsAlong = 18;

        /// <summary>
        /// The details a cloth can have, cells along its longer side: Low, Medium, High, Very high,
        /// Ultra, Extreme (the option's choice is the finest; the renderer gives each flag what its
        /// size on the screen needs). Until 2026-10-01 there were four, named Normal 18, High 24,
        /// Very high 32, Ultra 40 -- the names the comments and docs/HISTORY.md use for those
        /// counts ("Normal" is 18 cells, <see cref="CellsAlong"/>, throughout). Measured on a 3 x 2 m
        /// flag against 18 (20 s each, the scratchpad's bench and tools/test-cloth.ps1): the same
        /// flight at 6 m/s -- the fly's reach, the waves' speed within a tenth, the fly's flapping
        /// within a fifth -- while in calm air the finer cloth hangs straighter (82 deg of droop at
        /// 18, 85 at 24, 87 at 32, 88 at 40), as real cloth does. The cost (the GPU preview's Unity
        /// 5.6 Mono, a 1/60 s step with its mesh): 0.4 ms at 18, x2 at 24, x4 at 32 in calm air and
        /// x8 at 6 m/s (its 9 cm cells take two sub-steps there), x7 and x13 at 40. Flags over 3 m
        /// keep one sub-step to stronger winds (their cells are longer), so they cost less for the
        /// same detail. 52 and 64 (2026-10-01) refine only flags over 3.2 m (a 3 m flag's cells are
        /// <see cref="MinCell"/> at 40 already): on the author's 8.9 m flag at 1:1.9 (41 x 22
        /// particles at 40, 53 x 28 at 52, 65 x 35 at 64) the cloth bends tighter at each -- the
        /// tightest 1% of its bends 18-21 cm in radius at 40, 14-16 at 52, 11-13 at 64; 14.5 and
        /// 21.7 m flags alike in proportion -- at the same flight (droop and reach), worst link
        /// 1.09-1.18, never through itself in free air (.NET, 20 s at 0-12 m/s, 23 m up). The cost
        /// (.NET, a 1/60 s step): x2 at 52 and x3-4 at 64 against 40 (1.3-2.9 ms at 64 on one thread).
        /// </summary>
        public static readonly int[] Details = { 18, 24, 32, 40, 52, 64 };

        /// <summary>
        /// The smallest cell, metres: flags under 1.44 m (at Normal) get fewer cells, not smaller
        /// ones. A cell half as long needs twice the sub-steps in the same wind
        /// (<see cref="MaxCourant"/>), and a small flag is small on the screen too. At every
        /// detail: a finer detail refines only flags big enough for it.
        /// </summary>
        public const float MinCell = 0.08f;

        /// <summary>
        /// Self-collision: every particle is a sphere this wide, in cells, that no other particle
        /// may enter, bar its eight neighbours (held by their links anyway) -- the cloth as the
        /// particles of Macklin, Mueller, Chentanez and Kim 2014 ("Unified Particle Physics for
        /// Real-Time Applications"; NVIDIA's PhysX cloth the same) with Mueller's rule of a
        /// thickness as wide as the spacing ("Ten Minute Physics" 15, 2022): narrower, a particle
        /// slips between four others, or an edge through a cell. Measured (3 x 2 m, 20 s, calm to
        /// 12 m/s, every detail): without it the cloth went through itself in every frame of calm
        /// air (52 crossing pairs of triangles on average, 1300 at 36 cells) and in 72% of the
        /// frames at 2 m/s; at 0.75 of a cell, in half the frames of calm air (1.7 pairs); at one
        /// cell, in none. A folded flag's layers stay a cell apart: 17 cm on a 3 m flag at Normal.
        /// A finer detail's cloth is thicker than its cells (<see cref="CollisionWidth"/>).
        /// </summary>
        public const float CollisionThickness = 1.0f;

        /// <summary>
        /// Self-collision's thickness, metres: <see cref="CollisionThickness"/> cells at Normal (18
        /// cells), and at a finer detail the geometric mean of its cell and the Normal detail's --
        /// 1.15 cells at 24, 1.33 at 32, 1.45-1.49 at 40 (a 3 m flag: 17, 14, 12.5, 11.5 cm) --
        /// held to <see cref="MaxCollisionCells"/> at 52 and 64. The
        /// cloth moves as far in a step at every detail -- the air and the step are the same, only
        /// the cells shrink -- so a cell-thick finer cloth let its particles through one another
        /// twice as often, and the cloth against itself then held the layers crossed: a 2.8 m flag
        /// low on its pole in light wind from behind, at Very high and Ultra, was found knotted and
        /// laid out afresh 41 times in 3 hours of cloth (twelve cases, the knots harness,
        /// 2026-09-29; in the game seven times in 32 s, the "resetting" the author saw by a
        /// building); at this thickness never, nor in the release sweep's 16 cases (draped on a
        /// pole, big flags, calm), whose crossings it lowered. 1.1 and 1.2 cells still knotted (18
        /// and 3 times). A thicker cloth holds the links round a pole long more often (a flag draped
        /// on its pole: 55-90% of the time against 10-37%): all of them on the pole, which the knot
        /// test leaves out and the pole hides. Under two cells no particle but the eight neighbours
        /// is within a thickness at rest.
        /// </summary>
        public readonly float CollisionWidth;

        /// <summary>
        /// The most cells <see cref="CollisionWidth"/> may be: 40 cells' 1.45-1.49, so the four
        /// first details are as they were. Grown on by the geometric mean, 64 cells' cloth would be
        /// 1.9 cells thick: a particle and the one two along it touch at a bend of a radius near the
        /// thickness, so it bent no tighter than 52's, and in calm air held its links 17% long
        /// (pushed apart in its own plane). Held here, the bends grow tighter with every detail and
        /// no knot was found: the author's 8.9 m flag on its 23 m, 200% thick pole, a 14.5 m and a 6
        /// m tilted low one, the wind from behind (draped), across and away, a wall behind, at 40, 52
        /// and 64 cells, 10 min each (the knots harness, 2026-10-01): none laid out afresh, the
        /// draped flag through itself in 1-4% of the samples at 52 and 64 against 2-22% at 40.
        /// </summary>
        public const float MaxCollisionCells = 1.5f;

        /// <summary>
        /// The neighbour list's reach, as a multiple of the thickness: pairs this near are listed,
        /// and the list is kept until some particle has moved half the difference (Verlet 1967,
        /// the neighbour list with a skin). 1.5: remade in about a third of the steps of calm air.
        /// </summary>
        private const float CollisionReach = 1.5f;

        /// <summary>
        /// Share of their sliding over one another that two touching particles lose each pass
        /// (the friction of cloth on cloth): folds that meet settle instead of skating.
        /// </summary>
        private const float CollisionFriction = 0.1f;

        /// <summary>
        /// The same for a particle against the city (a roof, a wall, a prop or a tree): cloth laid
        /// on a roof settles there instead of skating off it. Measured (3 m flags and banners blown
        /// at a wall straight and 45 degrees off, 2-12 m/s, 20 s, reaching it from free air): the
        /// worst link 1.07-1.23, typically 1.03-1.06, at 0.1 and at 0.03 alike. Coulomb's friction
        /// as position-based dynamics takes it (Macklin et al. 2014, 6.1: still below mu_s times
        /// the depth put out) was tried: as good at the walls, but a flag the wind pressed round a
        /// post stuck where it touched, and its links stretched to 2.4 times.
        /// </summary>
        private const float ContactFriction = 0.1f;

        /// <summary>
        /// The cloth keeps this far above a surface (SetSurface): 2 cm, and a quarter of a cell more
        /// (a link between two particles either side of a roof's edge cuts the corner by less
        /// than that). A 3 m flag at Normal lies 6 cm over a roof, a 30 m one 44 cm.
        /// </summary>
        private const float SurfaceGap = 0.02f;
        private const float SurfaceGapCells = 0.25f;

        /// <summary>
        /// A particle found deeper in a surface or a capsule than this many of its longer cells --
        /// half a metre at least -- did not get there by moving (the air crosses at most
        /// <see cref="MaxCourant"/> of a cell a sub-step): the flag was laid out through a wall,
        /// carried into a building by Move It. Put out along the surface's normal it would be
        /// thrown up the wall or across the building; it is CARRIED back instead, its step's start
        /// with it (no speed from the jump) -- towards its row's hoist point, level, to the first
        /// place free at its height; or, under a surface lower than the hoist's top (a roof under
        /// the flag), up onto it.
        /// </summary>
        private const float DepthCells = 2f;
        private const float MinDepth = 0.5f;

        /// <summary>
        /// The held hoist may lie this far under the surface before the surface is left out
        /// (<see cref="SurfaceBlocked"/>): a flag hoisted along a roof's edge is not under it.
        /// </summary>
        private const float HoistUnder = 0.05f;

        /// <summary>
        /// A held hoist under the surface where it slopes steeper than this (rise over run) stands
        /// in a WALL's slope -- the grid holds a wall as a slope a node wide, grown half a node
        /// out: the wall is kept back from it down the slope first (<see cref="HoistClear"/>), then
        /// any of eight ways.
        /// </summary>
        private const float WallSlope = 1f;

        /// <summary>
        /// The most a wall is kept back from a hoist standing in it, in the grid's nodes, and at
        /// least <see cref="MinWallShift"/> metres: a pole partly touching a building (2026-10-01:
        /// the author's 18.4 m flag, its hoist 0.3 m off a 36 m wall, found its hoist in the wall's
        /// slope and left the whole surface out -- the cloth blew through the building). Further in
        /// than that, the flag stands inside the building: the surface is left out, as before.
        /// </summary>
        private const float WallShiftNodes = 2f;
        private const float MinWallShift = 0.5f;

        /// <summary>
        /// A LEVEL hoist (a banner hoisted from its top, or from a crossbar: its line points down by
        /// less than this share) hangs its whole cloth under its line, and stands on a roof's EDGE
        /// when the surface under any of its held particles comes within the cloth's depth limit
        /// (<see cref="DepthCells"/>, <see cref="MinDepth"/>) under it: the grid holds a building
        /// as solid down to the ground, so the cloth under it would be carried up onto the roof
        /// (2026-11-07: the author's banners "hoisted from the top are usually placed on the edges
        /// of buildings or balconies ... flags sink into the building ... and don't appear anymore";
        /// the preview, a 7 m banner 0.3 m in from a 17 m roof's edge: flapping on the roof). The
        /// wall is kept back from it as from a pole against a wall (<see cref="HoistClear"/>), by as
        /// much as <see cref="EdgeShift"/> metres, so the cloth hangs down the facade; further in,
        /// the flag stands on the roof, and its cloth lies on it as before.
        /// </summary>
        private const float LevelHoist = 0.5f;
        private const float EdgeShift = 1f;

        /// <summary>
        /// A free particle that BEGINS a step inside the city -- deeper than this share of the
        /// surface's gap, or of a capsule's margin -- did not move there: a building or a tree
        /// swept into it (its flag dragged by Move It or the placing pointer: the cloth is carried
        /// with its anchor), it was laid or carried there, or the city changed round it. It is let
        /// out no faster than a moved flag is dragged (<see cref="DragSpeed"/>,
        /// <see cref="MaxDragCells"/> a sub-step), along the way the surface or capsule would put
        /// it out, and never deeper -- the speed PhysX caps an overlap's way out at (its
        /// "max depenetration velocity", Unity's Rigidbody.maxDepenetrationVelocity). Put out at
        /// once, as before, it jumped: the author's 18.4 m flag dragged 15 m along a 36 m wall in 2
        /// s had 900-1300 particles jump more than a cell in a sub-step (the knots harness,
        /// 2026-10-01), its links torn 170% long, and the flag laid out afresh for a knot after --
        /// in the game "the flag animation resets many times ... fighting for space". A particle
        /// that meets the city from free air (it began the step outside) is put out at once, as
        /// before: a flag flying into a wall steps as it did, bit for bit.
        /// </summary>
        private const float ExitTolerance = 0.5f;

        /// <summary>
        /// The free particles letting themselves out of the city this step (<see cref="ExitTolerance"/>):
        /// 0 none, 1 out of the surface or a capsule along its normal, 2 deep in a wall's block,
        /// retreating level towards its row's hoist point; each kept at least as far out as A
        /// along E.
        /// </summary>
        private byte[] _exit;
        private float[] _exitAX, _exitAY, _exitAZ, _exitEX, _exitEY, _exitEZ;
        private int _exiting;

        /// <summary>The surface's point of the world this step, from the anchor (SetSurface's, less the wall kept back from the hoist).</summary>
        private float _effX, _effZ;

        /// <summary>
        /// Self-collision is skipped while every normal is within 70 deg of the cloth's mean one:
        /// a sheet that does not turn back on itself cannot fold onto itself (the normal cone of
        /// Volino and Magnenat-Thalmann 1994, "Efficient self-collision detection on smoothly
        /// discretized surface animations using geometrical shape regularity"). A flag flying in
        /// a breeze passes it two steps in three.
        /// </summary>
        private const float FlatCone = 0.34f;

        /// <summary>
        /// The most cells the wind may carry the air along the flag in one sub-step (a Courant
        /// number): the reactive term's upwind difference is explicit. Measured (the worst link
        /// over 16 s, flags of 0.5 to 6 m, winds of 6 and 12 m/s, steps of 1/60 and 1/30 s): the
        /// stretch goes with this number alone -- 0.32: 3%, 0.63: 8%, 0.76: 9%, 1.26: 21%,
        /// 3.8: 66%.
        /// </summary>
        public const float MaxCourant = 0.9f;

        /// <summary>
        /// Sub-steps are capped here: <see cref="MaxCourant"/> at FlagWind.MaxSpeed in a 1/30 s
        /// step on the shortest cells a flag of FlagSpec.MinLength can have (0.075 m: GridFor
        /// rounds the count of cells up as well as down) needs 20.
        /// </summary>
        public const int MaxSubSteps = 20;

        /// <summary>The reactive term's ceiling, m/s^2 (see Step).</summary>
        private const float MaxReactive = 4000f;

        /// <summary>
        /// On a halyard (<see cref="SetHalyard"/>), how much nearer the top the bottom clip is held
        /// than the hoist is long: a share of it. A halyard is hauled taut by hand and cleated,
        /// never bar-tight, and it stretches under the flag's pull, so the heading between the
        /// clips is a little longer than the line between them and bows out -- as far as the
        /// middle of a string of that length can, H sqrt(3 s / 8) for a small share s: 0.03 lets
        /// a 2 m hoist bow about 20 cm.
        /// </summary>
        public const float HalyardSlack = 0.03f;

        public readonly int Columns;
        public readonly int Rows;
        public readonly int Count;

        /// <summary>The detail it was made at (cells along the longer side, one of <see cref="Details"/>).</summary>
        public readonly int Detail;

        /// <summary>
        /// The cut it was made to (FlagCut: the fly's outline) and the cut's depth. A cut cloth is
        /// the same grid of particles, but only the cells the outline touches are cloth
        /// (FlagCut.Cells): the particles of no such cell are OUT -- held still (inverse mass 0,
        /// so every loop over the free particles passes them by), in no link, no pair of the
        /// self-collision, with no air -- and after each step put where the cloth beside them
        /// would carry on to (<see cref="FillCut"/>), so the normals, the drawn mesh and its
        /// Catmull-Rom patches near the edge see a surface that goes on smoothly; the shader cuts
        /// the outline (FlagWear.cginc's RFCutEdge) and nothing of an out cell is ever drawn. So a
        /// swallowtail's tails, a cell apart past the notch's apex, fly apart; a pennant's point,
        /// with nothing past it, whips -- as the cloth, not a picture on a rectangle. A held
        /// particle is one of inverse mass 0 THAT IS CLOTH (Invariant 10): the hoist's column is
        /// always cloth (every cut keeps some of each row).
        /// </summary>
        public readonly int Cut;
        public readonly float CutDepth;

        /// <summary>FlagCut.Cells for the grid (null: a rectangle, every cell cloth), and whether there is a cut at all.</summary>
        private readonly bool[] _cells;
        private readonly bool _shaped;

        /// <summary>A cut cloth's: each particle out of the cloth; the span of cloth down its column through it (metres, the air's added mass); the out particles in the order they are filled, each with the cloth beside it to carry on from (<see cref="FillCut"/>).</summary>
        private bool[] _out;
        private float[] _span;
        private int[] _fillOrder, _fillStart, _fillA, _fillB;

        /// <summary>
        /// The air's added mass down the hoist (2026-11-26). 1, the game's (the author's choice,
        /// "Keep mode 1", after the preview's side-by-side): an ellipse, most in the middle of the
        /// span and none at its free edges (<see cref="PrepareSpanShares"/>), in the reactive term
        /// and gravity's share, the resistive drag against the even inertia as before. 0: the same
        /// on every row (before 2026-11-26). 2: the ellipse in the drag too -- tried: the light
        /// edges then held to the air more tightly and calmed. Measured (a 3 m flag, 32 cells,
        /// varied air, four places): mode 1 against 0, the fly +11% at 12 m/s, the top edge +23%
        /// and the twist +22% at 3 m/s, 6 m/s within the noise, reach and stretch unchanged.
        /// The preview's -Span compares them. Read when a cloth is made (the shares) and each
        /// step (the drag). A change here changes every loop: <see cref="LoopMotion.Key"/>'s air.
        /// </summary>
        public static int SpanwiseAir = 1;

        /// <summary>Each particle's share of its span's mean added mass (the ellipse; 1 everywhere with <see cref="SpanwiseAir"/> off).</summary>
        private float[] _spanShare;

        /// <summary>Cut the same way (the same cloth cells, on a grid of the same size).</summary>
        public bool SameCut(FlagCloth other)
        {
            return other != null && other.Cut == Cut && (!FlagCut.UsesDepth(Cut) || Math.Abs(other.CutDepth - CutDepth) < 1e-4f);
        }

        /// <summary>Whether particle k is cloth (always, uncut).</summary>
        public bool IsCloth(int k)
        {
            return _out == null || !_out[k];
        }

        /// <summary>Whether cell (i, j) -- between columns i, i + 1 and rows j, j + 1 -- is cloth (always, uncut).</summary>
        public bool IsClothCell(int i, int j)
        {
            return _cells == null || _cells[j * (Columns - 1) + i];
        }

        /// <summary>The particles that are cloth (<see cref="Count"/> uncut).</summary>
        public int ClothCount
        {
            get
            {
                if (_out == null)
                    return Count;

                int n = 0;
                for (int k = 0; k < Count; k++)
                {
                    if (!_out[k])
                        n++;
                }

                return n;
            }
        }

        /// <summary>
        /// Keeps the cloth out of itself (<see cref="CollisionThickness"/>). On by default; set
        /// between steps (the main thread, when the option changes).
        /// </summary>
        public bool SelfCollision = true;

        /// <summary>Hoist to fly, and the hoist, metres.</summary>
        public readonly float Length;
        public readonly float Height;

        /// <summary>Rest spacing of the particles along the fly and down the hoist, metres.</summary>
        public readonly float CellU;
        public readonly float CellV;

        /// <summary>Positions after the last step, and before it (for drawing between steps).</summary>
        public readonly float[] X, Y, Z;
        public readonly float[] PrevX, PrevY, PrevZ;

        /// <summary>Velocities, m/s.</summary>
        public readonly float[] Vx, Vy, Vz;

        /// <summary>Unit normals at the last step's positions (the air's; the drawing makes its own).</summary>
        public readonly float[] Nx, Ny, Nz;

        /// <summary>The ground under the flag, relative to the anchor (negative, metres): the cloth stays above it.</summary>
        public float GroundY = -1e6f;

        /// <summary>
        /// The hoist edge's direction from the top corner down (unit): (0, -1, 0) unless the flag
        /// hangs from a tilted pole (FlagOrientation). The hoist's particles are pinned on it,
        /// particle (0, j) at j * CellV along it -- on a halyard only the two clips (SetHalyard).
        /// </summary>
        private float _hx, _hy = -1f, _hz;

        public float HoistX
        {
            get { return _hx; }
        }

        public float HoistY
        {
            get { return _hy; }
        }

        public float HoistZ
        {
            get { return _hz; }
        }

        /// <summary>Held by its two grommets on a halyard, not along its whole hoist (<see cref="SetHalyard"/>).</summary>
        private bool _clipped;

        /// <summary>The pole the cloth keeps off (SetPole): its axis through a point, along the hoist; its radius there and its growth per metre down; its extent down the axis; the margin kept off it.</summary>
        private bool _poleOn;
        private float _poleX, _poleY, _poleZ, _poleRadius, _poleGrowth, _poleTop, _poleBottom, _poleMargin;

        /// <summary>The back plate the cloth keeps in front of (SetPlate): an upright plane through the anchor, its level way out (unit).</summary>
        private bool _plateOn;
        private float _plateX, _plateZ;

        /// <summary>
        /// The city round the flag (SetSurface, SetCapsules): the top of what stands under it and
        /// the props' and trees' capsules, each relative to its own point of the world, which lies
        /// at (x, y, z) from the anchor.
        /// </summary>
        private SurfaceGrid _surface;
        private float _surfaceX, _surfaceY, _surfaceZ;
        private CapsuleSet _capsules;
        private float _capsulesX, _capsulesY, _capsulesZ;

        /// <summary>
        /// This step's: the surface in reach of the cloth, and not over its own hoist; the capsules in
        /// reach (their axis relative to the anchor, their radius and margin as this cloth keeps off
        /// them); the margins, and how deep a particle may be found and still be put out.
        /// </summary>
        private bool _surfaceOn, _surfaceBlocked;
        private float _surfaceMargin, _depthLimit, _hoistTop;
        private float[] _capX = new float[8], _capZ = new float[8], _capBottom = new float[8], _capTop = new float[8], _capReach = new float[8];
        private int _capCount;

        private readonly float[] _px, _py, _pz;
        private readonly float[] _invMass;

        /// <summary>
        /// The air at each particle this step, the air's velocity along the normal (u . n), the
        /// same a step ago, and the air's velocity relative to the cloth along the normal (V_n).
        /// </summary>
        private readonly float[] _ux, _uy, _uz, _un, _unPrev, _vn;

        /// <summary>The reactive acceleration at each particle, before and after it is smoothed.</summary>
        private readonly float[] _react, _smooth;
        private bool _primed;

        /// <summary>Each particle's flat distance to its row's hoist point, to the top corner and to the bottom corner.</summary>
        private readonly float[] _tether, _tetherTop, _tetherBottom;

        /// <summary>
        /// The links: their two particles, rest length, kind, per-pass stiffness when stretched
        /// and when squashed, and each end's share of a correction (its inverse mass over the
        /// pair's). Flat arrays, the projection written out in the loop: Mono's JIT inlines
        /// nothing, and a call and a switch per link per pass was most of a step's cost.
        /// </summary>
        private readonly int[] _a, _b;
        private readonly float[] _rest, _kOut, _kIn, _wa, _wb;
        private readonly byte[] _kind;
        private int _links;

        /// <summary>The air at two heights of each column (a quarter and three quarters down), and each row's blend between them.</summary>
        private readonly float[] _colUx, _colUy, _colUz, _colLx, _colLy, _colLz, _rowBlend;
        private readonly int _rowUpper, _rowLower;

        private FlagFabric _fabric;

        private const byte Structural = 0, ShearLink = 1, BendLink = 2, HeadingLink = 3;

        /// <summary>
        /// The heading's stiffness in bending over one step, 0..1: the hoist's own two-apart links,
        /// which only a flag on a halyard has free (<see cref="SetHalyard"/>). A heading is a strip
        /// of canvas sewn double over the flag's edge, far stiffer than the flag. Measured (a 3 m
        /// flag at Normal on a halyard, calm, 12 s): at the flag's own 0.1 the slack heading
        /// folded into the cloth and self-collision locked the folds -- 26% stretch against the
        /// pole, 35% without one; at 0.6, 6.5% and 4%, the heading a smooth bow; at 1.0 too stiff
        /// to bow smoothly, 26% again.
        /// </summary>
        public const float HeadingBend = 0.6f;

        /// <summary>
        /// Self-collision's working arrays: each particle's column and row, the spatial hash (a
        /// counting sort of the particles by the hash of their cell), the cells, where the
        /// particles were when the neighbour list was made, and the list: pairs of particles.
        /// </summary>
        private readonly int[] _col, _row;
        private int[] _hashStart, _hashEntry, _hashOf, _cellX, _cellY, _cellZ, _pairA, _pairB;
        private float[] _listX, _listY, _listZ;
        private int _pairs;
        private bool _listed;

        /// <summary>
        /// A flag <paramref name="length"/> metres from hoist to fly and <paramref name="height"/>
        /// down the hoist, laid out flat towards (dirX, dirZ), with <paramref name="detail"/>
        /// cells along its longer side (<see cref="Details"/>).
        /// </summary>
        public FlagCloth(float length, float height, FlagFabric fabric, float dirX, float dirZ, int detail = CellsAlong)
            : this(length, height, fabric, dirX, dirZ, detail, 0f, -1f, 0f)
        {
        }

        /// <summary>
        /// The same, its hoist along (<paramref name="hoistX"/>, <paramref name="hoistY"/>,
        /// <paramref name="hoistZ"/>) from the anchor (FlagOrientation), and laid out square to it.
        /// </summary>
        public FlagCloth(float length, float height, FlagFabric fabric, float dirX, float dirZ, int detail,
                         float hoistX, float hoistY, float hoistZ)
            : this(length, height, fabric, dirX, dirZ, detail, hoistX, hoistY, hoistZ, FlagCut.Rectangle, FlagCut.DefaultDepth)
        {
        }

        /// <summary>
        /// The same, cut to <paramref name="cut"/> (FlagCut) at <paramref name="cutDepth"/>: only the
        /// grid's cells the outline touches are cloth (<see cref="Cut"/>).
        /// </summary>
        public FlagCloth(float length, float height, FlagFabric fabric, float dirX, float dirZ, int detail,
                         float hoistX, float hoistY, float hoistZ, int cut, float cutDepth)
        {
            SetHoistDirection(hoistX, hoistY, hoistZ);
            Length = Math.Max(length, 0.1f);
            Height = Math.Max(height, 0.1f);
            Detail = ClampDetail(detail);
            GridFor(Length, Height, Detail, out Columns, out Rows);
            Count = Columns * Rows;
            CellU = Length / (Columns - 1);
            CellV = Height / (Rows - 1);
            Cut = FlagCut.Sanitise(cut);
            CutDepth = FlagCut.SanitiseDepth(cutDepth);
            _cells = FlagCut.Cells(Cut, CutDepth, Columns, Rows);
            _shaped = _cells != null;

            // A cell at Normal; finer, the geometric mean of its cell and Normal's.
            int normalColumns, normalRows;
            GridFor(Length, Height, CellsAlong, out normalColumns, out normalRows);
            float cell = Math.Min(CellU, CellV);
            float normal = Math.Min(Length / (normalColumns - 1), Height / (normalRows - 1));
            CollisionWidth = CollisionThickness * (normal > cell ? Math.Min((float)Math.Sqrt(cell * normal), MaxCollisionCells * cell) : cell);

            X = new float[Count]; Y = new float[Count]; Z = new float[Count];
            PrevX = new float[Count]; PrevY = new float[Count]; PrevZ = new float[Count];
            Vx = new float[Count]; Vy = new float[Count]; Vz = new float[Count];
            Nx = new float[Count]; Ny = new float[Count]; Nz = new float[Count];
            _px = new float[Count]; _py = new float[Count]; _pz = new float[Count];
            _ux = new float[Count]; _uy = new float[Count]; _uz = new float[Count];
            _un = new float[Count]; _unPrev = new float[Count]; _vn = new float[Count];
            _react = new float[Count]; _smooth = new float[Count];
            _invMass = new float[Count];
            _tether = new float[Count];
            _tetherTop = new float[Count];
            _tetherBottom = new float[Count];
            _col = new int[Count];
            _row = new int[Count];

            // The most links BuildLinks makes: (C-1)R + (R-1)C structural, 2(C-1)(R-1) shear,
            // (C-2)R + (R-2)C bend = 6CR - 5(C+R) + 2, under six a particle; a cut cloth fewer. It
            // was sized at twice that (a third of a cloth's memory, with hundreds of flags).
            int max = 6 * Count;
            _a = new int[max];
            _b = new int[max];
            _rest = new float[max];
            _kOut = new float[max];
            _kIn = new float[max];
            _wa = new float[max];
            _wb = new float[max];
            _kind = new byte[max];

            _colUx = new float[Columns]; _colUy = new float[Columns]; _colUz = new float[Columns];
            _colLx = new float[Columns]; _colLy = new float[Columns]; _colLz = new float[Columns];
            _rowBlend = new float[Rows];
            _rowUpper = Math.Min(Rows - 1, Rows / 4);
            _rowLower = Math.Max(_rowUpper, (3 * (Rows - 1)) / 4);
            for (int j = 0; j < Rows; j++)
            {
                float span = Math.Max(1, _rowLower - _rowUpper);
                _rowBlend[j] = Math.Max(0f, Math.Min(1f, (j - _rowUpper) / span));
            }

            for (int j = 0; j < Rows; j++)
            {
                for (int i = 0; i < Columns; i++)
                {
                    int k = j * Columns + i;
                    _invMass[k] = i == 0 ? 0f : 1f;
                    _col[k] = i;
                    _row[k] = j;
                    float u = i * CellU, up = j * CellV, down = (Rows - 1 - j) * CellV;
                    _tether[k] = u;
                    _tetherTop[k] = (float)Math.Sqrt(u * u + up * up);
                    _tetherBottom[k] = (float)Math.Sqrt(u * u + down * down);
                }
            }

            if (_shaped)
                PrepareCut();

            PrepareSpanShares();
            BuildLinks();
            SetFabric(fabric);
            Reset(dirX, dirZ);
        }

        /// <summary>
        /// The grid for a flag of this size at this detail: <paramref name="detail"/> cells along
        /// the longer side (fewer on a flag too small for them to be <see cref="MinCell"/>), as
        /// square as the other side allows, at least 3 particles across.
        /// </summary>
        public static void GridFor(float length, float height, int detail, out int columns, out int rows)
        {
            detail = ClampDetail(detail);
            float cell = Math.Max(Math.Max(length, height) / detail, MinCell);
            columns = Math.Max(4, Math.Min(detail + 1, (int)Math.Round(length / cell) + 1));
            rows = Math.Max(3, Math.Min(detail + 1, (int)Math.Round(height / cell) + 1));
        }

        /// <summary>The grid at the Normal detail.</summary>
        public static void GridFor(float length, float height, out int columns, out int rows)
        {
            GridFor(length, height, CellsAlong, out columns, out rows);
        }

        /// <summary>A detail in range: between the coarsest and the finest of <see cref="Details"/>.</summary>
        public static int ClampDetail(int detail)
        {
            return Math.Max(Details[0], Math.Min(detail, Details[Details.Length - 1]));
        }

        public FlagFabric Fabric
        {
            get { return _fabric; }
        }

        /// <summary>
        /// The passes a step of this cloth takes: the fabric's, one more at 32 cells, two more at
        /// 40, three at 52, four at 64 -- a longer chain of links takes more passes to hold
        /// together. Measured (3 x 2 m, 20 s, the worst link at 6 and 12 m/s): 40 cells 9.8% and
        /// 16.3% with five, 8.5% and 12.3% with seven; 32 cells 7.8% and 11.2% with five; on an
        /// 8.9 m flag at 12 m/s, 52 cells 11% and 64 14% at worst. The stiffnesses are per step, so the
        /// cloth folds the same (Mueller et al.'s per-pass formula).
        /// </summary>
        public int Passes
        {
            get { return Math.Max(1, _fabric.Iterations) + Math.Max(0, (Detail - CellsAlong) / 10) + (_heldCount > 0 ? HeldPasses : 0); }
        }

        /// <summary>
        /// How many sub-steps a step of <paramref name="size"/> seconds takes in a wind of
        /// <paramref name="speed"/> m/s at the flag: enough that the air moves at most
        /// <see cref="MaxCourant"/> cells along this cloth in each. Short steps hold a cloth
        /// together better than more passes do (Macklin, Storey, Lu, Terdiman, Chentanez, Jeschke
        /// and Mueller 2019, "Small Steps in Physics Simulation"). A 3 m flag's 1/60 s step takes
        /// one to 9 m/s, two to 18, five at FlagWind.MaxSpeed; a 1/30 s step twice as many.
        /// </summary>
        public int SubStepsFor(float speed, float size)
        {
            return SubSteps(speed, size, CellU);
        }

        /// <summary>The same for a cell of <paramref name="cell"/> metres along the flag.</summary>
        public static int SubSteps(float speed, float size, float cell)
        {
            // A hair under the whole number: 9 m/s on a 3 m flag is one sub-step, not a rounding error's two.
            double n = Math.Ceiling(speed * size / (MaxCourant * cell) - 1e-4);
            return n < 1.0 ? 1 : n > MaxSubSteps ? MaxSubSteps : (int)n;
        }

        /// <summary>New stiffnesses (the waviness changed): per pass, from per step, onto every link.</summary>
        public void SetFabric(FlagFabric fabric)
        {
            _fabric = fabric;
            int n = Passes;
            float structural = PerPass(1f, n);
            float shear = PerPass(fabric.Shear, n);
            float bend = PerPass(fabric.Bend, n);
            float heading = PerPass(Math.Max(fabric.Bend, HeadingBend), n);

            for (int c = 0; c < _links; c++)
            {
                switch (_kind[c])
                {
                    case Structural:
                        _kOut[c] = structural;
                        _kIn[c] = structural * fabric.Compression;
                        break;
                    case ShearLink:
                        _kOut[c] = shear;
                        _kIn[c] = shear * fabric.Compression;
                        break;
                    case HeadingLink:
                        _kOut[c] = heading;
                        _kIn[c] = heading;
                        break;
                    default:
                        // A fold IS the two-apart link being shortened: that is the bending stiffness.
                        _kOut[c] = bend;
                        _kIn[c] = bend;
                        break;
                }
            }
        }

        /// <summary>Mueller et al. 2007, 3.3: the per-pass stiffness that makes k over n passes.</summary>
        private static float PerPass(float k, int passes)
        {
            k = Math.Max(0f, Math.Min(k, 1f));
            return k >= 1f ? 1f : 1f - (float)Math.Pow(1.0 - k, 1.0 / passes);
        }

        /// <summary>The hoist's direction (normalised; straight down if it has no length). Nothing moves.</summary>
        private void SetHoistDirection(float x, float y, float z)
        {
            float len = (float)Math.Sqrt(x * x + y * y + z * z);
            if (len < 1e-6f)
            {
                _hx = 0f; _hy = -1f; _hz = 0f;
                return;
            }

            _hx = x / len; _hy = y / len; _hz = z / len;
        }

        /// <summary>
        /// Lays the cloth out flat and still, extended towards (dirX, dirZ) -- as near that way as
        /// a sheet square to its hoist can point -- with a hair of noise to break its symmetry.
        /// </summary>
        public void Reset(float dirX, float dirZ)
        {
            // Never laid behind its back plate: a wind blowing at the plate lays it the mirrored way out.
            float back = dirX * _plateX + dirZ * _plateZ;
            if (_plateOn && back < 0f)
            {
                dirX -= 2f * back * _plateX;
                dirZ -= 2f * back * _plateZ;
            }

            LayOut(dirX, 0f, dirZ, true);
        }

        /// <summary>
        /// Lays the cloth out flat and still towards (dirX, dirY, dirZ) -- any way, not only level:
        /// a banner hoisted from its top is laid hanging down -- as near that way as a sheet square
        /// to its hoist can point; with <paramref name="wobble"/>, a hair of noise breaks its
        /// symmetry (a cloth to be stepped), without it the sheet is exactly flat (a flat flag).
        /// </summary>
        public void LayOut(float dirX, float dirY, float dirZ, bool wobble)
        {
            // Held edges (SetHolds): only along the fly they are held along, whatever was asked.
            if (_heldCount > 0)
            {
                dirX = _holdFx; dirY = _holdFy; dirZ = _holdFz;
            }

            float hx = _hx, hy = _hy, hz = _hz;

            // The given direction, less its part along the hoist.
            float along = dirX * hx + dirY * hy + dirZ * hz;
            float fx = dirX - along * hx, fy = dirY - along * hy, fz = dirZ - along * hz;
            float len = (float)Math.Sqrt(fx * fx + fy * fy + fz * fz);
            if (len < 1e-4f)
            {
                // Along the hoist (or none): any way square to it, level if it can be -- in front of a back plate.
                fx = -hz; fy = 0f; fz = hx;
                len = (float)Math.Sqrt(fx * fx + fz * fz);
                if (len < 1e-4f)
                {
                    fx = 1f; fz = 0f; len = 1f;
                }

                if (_plateOn && fx * _plateX + fz * _plateZ < 0f)
                {
                    fx = -fx;
                    fz = -fz;
                }
            }

            fx /= len; fy /= len; fz /= len;

            // Across the sheet: hoist x fly (for a straight-down hoist, (-dirZ, 0, dirX)).
            float wx = hy * fz - hz * fy, wy = hz * fx - hx * fz, wz = hx * fy - hy * fx;

            for (int j = 0; j < Rows; j++)
            {
                for (int i = 0; i < Columns; i++)
                {
                    int k = j * Columns + i;
                    float u = i * CellU;
                    // The hoist on its line: on a halyard, drawn up between its clips.
                    float down = i == 0 ? HoistDown(j) : j * CellV;
                    float off = i == 0 || !wobble ? 0f : 0.002f * Length * FlagNoise.Value1(k * 0.37f, 5);
                    X[k] = down * hx + u * fx + off * wx;
                    Y[k] = down * hy + u * fy + off * wy;
                    Z[k] = down * hz + u * fz + off * wz;
                    PrevX[k] = X[k]; PrevY[k] = Y[k]; PrevZ[k] = Z[k];
                    Vx[k] = 0f; Vy[k] = 0f; Vz[k] = 0f;
                }
            }

            if (_heldCount > 0)
                PlaceHeld(true);

            ComputeNormals(X, Y, Z, Nx, Ny, Nz);
            _primed = false;
            _listed = false;

            // Laid out afresh where its anchor is: it owes nothing to the anchor's moves, and is not knotted.
            _dragX = 0f; _dragY = 0f; _dragZ = 0f;
            _dragStepX = 0f; _dragStepY = 0f; _dragStepZ = 0f;
            _knotted = 0f;

            // Nothing on its way out of the city: laid into it, its particles count as newly buried,
            // and its next step puts them out at once (FindExits).
            if (_exit != null)
                Array.Clear(_exit, 0, _exit.Length);
            _exiting = 0;
            _laid = true;
        }

        /// <summary>
        /// A new cloth's first layout where it waves, before anything of the city is known: a level
        /// hoist (<see cref="LevelHoist"/>: a banner from its top, a crossbar) laid HANGING, as
        /// LayOutClear lays it first -- the noise only out in front of its back plate, if it has
        /// one; any other hoist as <see cref="Reset"/> (towards the wind), exactly as before. The
        /// author, 2026-11-11: banners from their top "start out horizontal, and then the cloth
        /// motion sets in and they get lowered" -- laid level along the wind, a 19 m banner let
        /// down by its calm (CalmDamping) had not fallen in its 1.5 s of settling and was shown
        /// level, falling; with the city off nothing laid it hanging.
        /// </summary>
        public void LayOutNew(float dirX, float dirZ)
        {
            if (HoistY <= -LevelHoist)
            {
                Reset(dirX, dirZ);
                return;
            }

            LayHanging();
        }

        /// <summary>
        /// Laid hanging straight down (a level hoist's way, LayOutClear's first), wobbled, the noise
        /// mirrored in front of its back plate: hung in the plate's plane, a big banner's noise
        /// (0.002 L, 3.8 cm at 19 m) behind it was past PlateSlack and the hanging way refused.
        /// </summary>
        private void LayHanging()
        {
            LayOut(0f, -1f, 0f, true);
            if (!_plateOn)
                return;

            // The layout's noise is across the sheet, which hangs in the plate's plane: mirrored
            // to the front where it went behind (KeepInFront would push it there in the first step).
            for (int k = 0; k < Count; k++)
            {
                float d = X[k] * _plateX + Z[k] * _plateZ;
                if (d >= 0f)
                    continue;

                X[k] -= 2f * d * _plateX;
                Z[k] -= 2f * d * _plateZ;
                PrevX[k] = X[k];
                PrevZ[k] = Z[k];
            }

            ComputeNormals(X, Y, Z, Nx, Ny, Nz);
        }

        /// <summary>
        /// Laid out since the last step (<see cref="LayOut"/>): the city round it is put out of it at
        /// once in the next step, as before (a banner laid hanging through the roof under it lies on
        /// the roof from its first step; let out at a dragged flag's speed it rose through the roof
        /// for 2 s) -- only what the city sweeps into later is let out slowly (<see cref="ExitTolerance"/>).
        /// </summary>
        private bool _laid;

        /// <summary>
        /// Lays the cloth out as <see cref="LayOut"/> does (still, wobbled) along the first way that
        /// leaves it out of the surface round it (<see cref="SetSurface"/>), its pole and the posts
        /// in reach: towards (dirX, dirZ), then turned from it 45, 90 and 135 degrees either side,
        /// then the other way; if none, the way with the fewest particles in them. Laid into a wall,
        /// its particles would be carried back out towards the hoist in a heap, in no order, and the
        /// cloth against itself would lock the heap's layers as they fell (3 m flags laid through a
        /// wall: links 30-60% long for seconds; reaching the wall from free air, 8-29% at worst).
        /// Laid through its own pole -- the wind's way, when it blows back over the pole -- it was
        /// found knotted again 2 s later, over and over (the knots harness: 199 relayouts in 400 s,
        /// 2 laid clear of the pole). A level hoist tries hanging straight down first. Returns the
        /// particles left in the way (0: clear).
        /// </summary>
        public int LayOutClear(float dirX, float dirZ)
        {
            // A level hoist (a banner from its top, a crossbar: LevelHoist) is laid HANGING first, as
            // it hangs: laid level towards a wind blowing at the building it hangs from, it lay out
            // over the roof and stayed there (the preview, 2026-11-07), the roof under it holding it.
            if (HoistY > -LevelHoist)
            {
                LayHanging();
                if (InTheWay() == 0)
                    return 0;

                // On a roof near its edge: along the roof to the edge, then down the wall.
                if (DrapeDistance > 0f)
                {
                    LayDraped(_clearUX, _clearUZ, DrapeDistance);
                    if (InTheWay() == 0)
                        return 0;
                }

                // Its hoist in a wall's band: leaning off the band in front of it (HangsClear), a
                // little more each time.
                if (HoistOnEdge && WallShift > 0f)
                {
                    for (int a = 1; a <= 3; a++)
                    {
                        double lean = a * (Math.PI / 12);
                        float s = (float)Math.Sin(lean), c = (float)Math.Cos(lean);
                        LayOut(s * _clearUX, -c, s * _clearUZ, true);
                        if (InTheWay() == 0)
                            return 0;
                    }
                }
            }

            int best = 0, fewest = int.MaxValue;
            for (int i = 0; i < 8; i++)
            {
                float x, z;
                Turned(i, dirX, dirZ, out x, out z);
                LayOut(x, 0f, z, true);
                int inside = InTheWay();
                if (inside == 0)
                    return 0;

                if (inside < fewest)
                {
                    fewest = inside;
                    best = i;
                }
            }

            float bx, bz;
            Turned(best, dirX, dirZ, out bx, out bz);
            LayOut(bx, 0f, bz, true);
            return fewest;
        }

        /// <summary>
        /// Laid as <see cref="LayOut"/> along (dirX, 0, dirZ) -- square to the hoist as near as it
        /// can be -- for <paramref name="distance"/> metres measured along (dirX, dirZ), the rest
        /// hanging straight down from there: a level hoist's cloth over a roof's edge.
        /// </summary>
        private const float DrapeClear = 0.02f;

        private void LayDraped(float dirX, float dirZ, float distance)
        {
            LayOut(dirX, 0f, dirZ, true);

            // Held edges keep it where they hold it: never draped.
            if (_heldCount > 0)
                return;

            // The way it went (LayOut took the part along the hoist away) and how far along it
            // the edge is: as far as the edge is along (dirX, dirZ), over its share of that way.
            float fx = X[1] - X[0], fz = Z[1] - Z[0];
            float len = (float)Math.Sqrt(fx * fx + fz * fz);
            if (len < 1e-6f)
                return;

            fx /= len;
            fz /= len;

            // A hair past the edge, and the layout's noise only outwards: a hanging particle laid 1
            // cm towards the wall was in the band's steep part (70 cm deep a centimetre in).
            float d = (distance + DrapeClear) / Math.Max(fx * dirX + fz * dirZ, 0.3f);
            for (int j = 0; j < Rows; j++)
            {
                for (int i = 1; i < Columns; i++)
                {
                    float u = i * CellU;
                    int k = j * Columns + i, h = j * Columns;
                    float off = Math.Abs(Y[k] - Y[h]);
                    if (u <= d)
                    {
                        // Along the roof: its noise up, off the roof, not into it.
                        Y[k] = Y[h] + off;
                        PrevY[k] = Y[k];
                        continue;
                    }

                    X[k] = X[h] + d * fx;
                    Y[k] = Y[h] - (u - d);
                    Z[k] = Z[h] + d * fz;
                    X[k] += off * fx;
                    Z[k] += off * fz;
                    PrevX[k] = X[k]; PrevY[k] = Y[k]; PrevZ[k] = Z[k];
                }
            }

            ComputeNormals(X, Y, Z, Nx, Ny, Nz);
        }

        /// <summary>The <paramref name="i"/>th way to try: 0, +45, -45, +90, -90, +135, -135, 180 degrees from (x, z).</summary>
        private static void Turned(int i, float x, float z, out float tx, out float tz)
        {
            double a = (i + 1) / 2 * (Math.PI / 4) * (i % 2 == 1 ? 1 : -1);
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            tx = x * c - z * s;
            tz = x * s + z * c;
        }

        /// <summary>
        /// The free particles, where they are, inside the pole or a capsule in use, and under the
        /// surface (not counted while it is not in use: none, or its hoist under it).
        /// </summary>
        private int InTheWay()
        {
            int inside = CountBehindPlate();
            if (_poleOn || _capCount > 0)
            {
                for (int k = 0; k < Count; k++)
                {
                    if (_invMass[k] != 0f && OnPost(k, 0f))
                        inside++;
                }
            }

            SurfaceGrid grid = _surface;
            if (grid == null)
                return inside;

            // Its hoist or a held edge under it: not in use, so not counted -- unless only a wall's slope, kept back.
            float ox, oz, oy = _surfaceY;
            if (!HoistClear(grid, out ox, out oz) || HoldsUnder(grid, ox, oz))
                return inside;

            float h, sx, sz;
            for (int k = 0; k < Count; k++)
            {
                if (_invMass[k] != 0f && grid.Sample(X[k] - ox, Z[k] - oz, out h, out sx, out sz) && Y[k] < h + oy)
                    inside++;
            }

            return inside;
        }

        /// <summary>
        /// Times the cloth was found mostly buried in the surface -- its flag moved into a building
        /// faster than the cloth could follow, carried along with it (MoveAnchor) -- and laid out
        /// afresh where it is free (<see cref="LayOutClear"/>): carried back out a particle at a
        /// time it would fall into a heap the cloth against itself locks.
        /// </summary>
        public int Relaid { get; private set; }

        /// <summary>
        /// Where the cloth's air has carried its gusts and eddies (FlagWind.Air moves it on each
        /// sub-step; a field, so it goes by ref). Taken on by <see cref="TakeShape"/> (the same flag).
        /// </summary>
        public AirDrift Air;

        /// <summary>A quarter of the particles carried back out of the surface in a step (<see cref="Retreat"/>): the cloth is laid out afresh.</summary>
        private const float BuriedShare = 0.25f;

        /// <summary>The particles carried back out of the surface this step (<see cref="Retreat"/>).</summary>
        private int _heaped;

        /// whole cloth turns about the anchor with it, as it is, moving as it moved -- the smallest
        /// turn that takes the old hoist onto the new one (Rodrigues' rotation) -- and the hoist
        /// is pinned exactly on the new line. The air and gravity do the rest.
        /// </summary>
        public void SetHoist(float x, float y, float z)
        {
            float len = (float)Math.Sqrt(x * x + y * y + z * z);
            if (len < 1e-6f)
                return;

            x /= len; y /= len; z /= len;
            float c = _hx * x + _hy * y + _hz * z;
            if (c < 0.9999999f)
            {
                // The axis: old x new; opposite hoists turn half round any axis square to the old.
                float ax = _hy * z - _hz * y, ay = _hz * x - _hx * z, az = _hx * y - _hy * x;
                float s = (float)Math.Sqrt(ax * ax + ay * ay + az * az);
                if (s < 1e-6f)
                {
                    ax = -_hz; ay = 0f; az = _hx;
                    float t = (float)Math.Sqrt(ax * ax + az * az);
                    if (t < 1e-6f)
                    {
                        ax = 1f; az = 0f; t = 1f;
                    }

                    ax /= t; az /= t;
                    s = 0f;
                    c = -1f;
                }
                else
                {
                    ax /= s; ay /= s; az /= s;
                }

                Rotate(X, Y, Z, ax, ay, az, c, s);
                Rotate(PrevX, PrevY, PrevZ, ax, ay, az, c, s);
                Rotate(Vx, Vy, Vz, ax, ay, az, c, s);

                // The held edges' fly turns with the cloth (SetHolds would turn it again otherwise).
                float d = 1f - c, fx = _holdFx, fy = _holdFy, fz = _holdFz;
                float dot = (ax * fx + ay * fy + az * fz) * d;
                _holdFx = fx * c + (ay * fz - az * fy) * s + ax * dot;
                _holdFy = fy * c + (az * fx - ax * fz) * s + ay * dot;
                _holdFz = fz * c + (ax * fy - ay * fx) * s + az * dot;
            }

            _hx = x; _hy = y; _hz = z;
            PinHoist();

            ComputeNormals(X, Y, Z, Nx, Ny, Nz);
            _primed = false;
            _listed = false;
        }

        /// <summary>
        /// Hoisted on a halyard (a pole the mod draws, with its ropes) or not. On one, the flag
        /// hangs from its two grommets alone -- the top clip at the anchor, the bottom one
        /// <see cref="HalyardSlack"/> of the hoist nearer it -- and the heading between them is
        /// free: it bows out in the wind and the flag works in the middle, as a flag on a pole's
        /// ropes does (the author: "In real life, flags that are hoisted on ropes are attached
        /// only by the top and bottom, creating a nice effect in the middle of the flag").
        /// Otherwise the whole hoist is held on its line: sewn to a pole, or fixed to one of the
        /// city's own, whose ropes the mod cannot know. Set between steps; the links are made
        /// again for the particles now held or free.
        /// </summary>
        public void SetHalyard(bool clipped)
        {
            if (clipped == _clipped)
                return;

            _clipped = clipped;
            for (int j = 0; j < Rows; j++)
                _invMass[j * Columns] = clipped && j > 0 && j < Rows - 1 ? 1f : 0f;

            BuildLinks();
            SetFabric(_fabric);
            PinHoist();

            // Held edges go by the hoist's places down its line, which a halyard draws up.
            if (_holds != 0)
                BuildHeld();
            ComputeNormals(X, Y, Z, Nx, Ny, Nz);
            _listed = false;
        }

        /// <summary>True on a halyard: held by its two grommets only.</summary>
        public bool Clipped
        {
            get { return _clipped; }
        }

        /// <summary>
        /// How far down the hoist's line particle (0, <paramref name="j"/>) is held -- or, free on
        /// a halyard, laid out: on one, the hoist is drawn up <see cref="HalyardSlack"/> between
        /// its clips.
        /// </summary>
        private float HoistDown(int j)
        {
            return _clipped ? j * CellV * (1f - HalyardSlack) : j * CellV;
        }

        /// <summary>Every held particle of the hoist onto its place on the hoist's line, still.</summary>
        private void PinHoist()
        {
            for (int j = 0; j < Rows; j++)
            {
                int k = j * Columns;
                if (_invMass[k] != 0f)
                    continue;

                float down = HoistDown(j);
                X[k] = down * _hx; Y[k] = down * _hy; Z[k] = down * _hz;
                PrevX[k] = X[k]; PrevY[k] = Y[k]; PrevZ[k] = Z[k];
                Vx[k] = 0f; Vy[k] = 0f; Vz[k] = 0f;
            }
        }

        /// <summary>
        /// The whole cloth turned <paramref name="radians"/> about the vertical through the anchor,
        /// as it is -- its hoist's line and its pole with it: a flag that keeps one shape (FlagMotion
        /// Still, Flat) turned with its prop. (x, z) goes to (x cos - z sin, x sin + z cos), which
        /// takes the heading (cos a, 0, sin a) to a + <paramref name="radians"/> and FlagOrientation's
        /// frame at a to its frame there.
        /// </summary>
        public void Turn(float radians)
        {
            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            TurnLevel(X, Z, c, s);
            TurnLevel(PrevX, PrevZ, c, s);
            TurnLevel(Vx, Vz, c, s);

            float hx = _hx * c - _hz * s, hz = _hx * s + _hz * c;
            _hx = hx; _hz = hz;
            float px = _poleX * c - _poleZ * s, pz = _poleX * s + _poleZ * c;
            _poleX = px; _poleZ = pz;
            float fx = _holdFx * c - _holdFz * s, fz = _holdFx * s + _holdFz * c;
            _holdFx = fx; _holdFz = fz;

            PinHoist();
            ComputeNormals(X, Y, Z, Nx, Ny, Nz);
            _primed = false;
            _listed = false;
        }

        private void TurnLevel(float[] vx, float[] vz, float c, float s)
        {
            for (int k = 0; k < Count; k++)
            {
                float x = vx[k], z = vz[k];
                vx[k] = x * c - z * s;
                vz[k] = x * s + z * c;
            }
        }

        /// <summary>The particles' places turned <paramref name="radians"/> about the vertical (<see cref="Turn"/>), into the given arrays of <see cref="Count"/>.</summary>
        public void CopyShape(float[] x, float[] y, float[] z, float radians)
        {
            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            for (int k = 0; k < Count; k++)
            {
                x[k] = X[k] * c - Z[k] * s;
                y[k] = Y[k];
                z[k] = X[k] * s + Z[k] * c;
            }
        }

        /// <summary>
        /// The cloth takes the places a cloth of the same grid had (<see cref="CopyShape"/>) turned
        /// <paramref name="radians"/> about the vertical, still; its held particles put exactly on
        /// its own hoist's line.
        /// </summary>
        public void SetShape(float[] x, float[] y, float[] z, float radians)
        {
            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            for (int k = 0; k < Count; k++)
            {
                X[k] = x[k] * c - z[k] * s;
                Y[k] = y[k];
                Z[k] = x[k] * s + z[k] * c;
                PrevX[k] = X[k]; PrevY[k] = Y[k]; PrevZ[k] = Z[k];
                Vx[k] = 0f; Vy[k] = 0f; Vz[k] = 0f;
            }

            PinHoist();
            ComputeNormals(X, Y, Z, Nx, Ny, Nz);
            _primed = false;
            _listed = false;
        }

        /// <summary>
        /// The cloth takes the places AND the motion a cloth of the same grid had in the flag's own
        /// frame, turned <paramref name="radians"/> about the vertical (as <see cref="SetShape"/>),
        /// and steps on from them: the velocities given, its places before a step of
        /// <paramref name="step"/> seconds back along them. A loop that stood in for a waving flag
        /// hands its pose back this way (FlagLoop.Motion, 2026-11-11): the new cloth flies on from
        /// where the loop was, not settled afresh from another shape. Not let down (no Calm): it
        /// is already flying.
        /// </summary>
        public void SetMotion(float[] x, float[] y, float[] z, float[] vx, float[] vy, float[] vz, float radians, float step)
        {
            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            for (int k = 0; k < Count; k++)
            {
                X[k] = x[k] * c - z[k] * s;
                Y[k] = y[k];
                Z[k] = x[k] * s + z[k] * c;
                Vx[k] = vx[k] * c - vz[k] * s;
                Vy[k] = vy[k];
                Vz[k] = vx[k] * s + vz[k] * c;
                PrevX[k] = X[k] - Vx[k] * step;
                PrevY[k] = Y[k] - Vy[k] * step;
                PrevZ[k] = Z[k] - Vz[k] * step;
            }

            _knotted = 0f;
            Resume();
        }

        /// <summary>
        /// Its places, places before and velocities were written from outside (shared motion:
        /// MotionRecord.Play) and it steps on from them: the held particles back on its own hoist's
        /// line, the normals made, the self-collision list and the prediction made afresh.
        /// </summary>
        public void Resume()
        {
            PinHoist();
            ComputeNormals(X, Y, Z, Nx, Ny, Nz);
            _primed = false;
            _listed = false;
            _laid = false;
        }

        /// <summary>
        /// The cloth takes the motion of <paramref name="from"/>, a like flag's cloth of the same grid
        /// and hoist (the same ShareKey: a new flag settled from one like it, FlagRenderer's settle
        /// by copy, 2026-11-01): its places, places before and velocities turned
        /// <paramref name="radians"/> about the vertical through the anchor, as MotionRecord.Play
        /// gives a follower its source's. Its own hoist's line, pole and halyard are kept (the turn
        /// takes the like flag's line onto this one's: the key holds the hoist), the held particles
        /// put back on that line, and it steps on from there (<see cref="Resume"/>). False, and
        /// nothing changed, for another grid or another way of hoisting.
        /// </summary>
        public bool CopyMotion(FlagCloth from, float radians)
        {
            if (from == null || from == this || from.Columns != Columns || from.Rows != Rows || from._clipped != _clipped || !SameCut(from))
                return false;

            float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
            for (int k = 0; k < Count; k++)
            {
                float x = from.X[k], z = from.Z[k];
                X[k] = x * c - z * s; Y[k] = from.Y[k]; Z[k] = x * s + z * c;
                x = from.PrevX[k]; z = from.PrevZ[k];
                PrevX[k] = x * c - z * s; PrevY[k] = from.PrevY[k]; PrevZ[k] = x * s + z * c;
                x = from.Vx[k]; z = from.Vz[k];
                Vx[k] = x * c - z * s; Vy[k] = from.Vy[k]; Vz[k] = x * s + z * c;
            }

            _knotted = 0f;
            Resume();
            return true;
        }

        /// <summary>v cos + (k x v) sin + k (k . v)(1 - cos), for every vector of the three arrays.</summary>
        private void Rotate(float[] vx, float[] vy, float[] vz, float kx, float ky, float kz, float c, float s)
        {
            float d = 1f - c;
            for (int k = 0; k < Count; k++)
            {
                float x = vx[k], y = vy[k], z = vz[k];
                float dot = (kx * x + ky * y + kz * z) * d;
                vx[k] = x * c + (ky * z - kz * y) * s + kx * dot;
                vy[k] = y * c + (kz * x - kx * z) * s + ky * dot;
                vz[k] = z * c + (kx * y - ky * x) * s + kz * dot;
            }
        }

        /// <summary>
        /// A pole for the cloth to keep off (FlagPoleShape): its axis along the hoist through
        /// (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) relative to the anchor,
        /// its <paramref name="radius"/> there and <paramref name="growth"/> per metre down the axis
        /// (a tapered pole thickens towards its foot), from <paramref name="top"/> to
        /// <paramref name="bottom"/> metres down the axis (negative: above the anchor). Set between
        /// steps; again whenever the hoist turns (the axis goes with it).
        /// </summary>
        public void SetPole(float x, float y, float z, float radius, float growth, float top, float bottom)
        {
            _poleOn = true;
            _poleX = x; _poleY = y; _poleZ = z;
            _poleRadius = Math.Max(radius, 0f);
            _poleGrowth = growth;
            _poleTop = top;
            _poleBottom = Math.Max(bottom, top);

            // The margin: half a cell, so no link between particles either side can cut the pole
            // -- but never past the hoist line, which hangs just off the pole on its halyard: a
            // pole wider than that would push the cloth by its hoist out while its links pulled
            // it in (a flag blown back onto its pole stretched 20% with half a cell of 8 cm
            // round a pole 3 cm from its hoist).
            float along = x * _hx + y * _hy + z * _hz;
            float ox = x - along * _hx, oy = y - along * _hy, oz = z - along * _hz;
            float gap = (float)Math.Sqrt(ox * ox + oy * oy + oz * oz) - (_poleRadius + Math.Max(growth, 0f) * Height) - 0.002f;
            _poleMargin = Math.Max(0f, Math.Min(0.5f * Math.Min(CellU, CellV), gap));
        }

        /// <summary>No pole to keep off (none is drawn).</summary>
        public void ClearPole()
        {
            _poleOn = false;
        }

        public bool HasPole
        {
            get { return _poleOn; }
        }

        /// <summary>
        /// A back plate (the author, 2026-11-09, of a banner hoisted from its top against a facade
        /// whose cloth swung back into the wall: "a collision mesh right behind the flag cloth ...
        /// So that these flags don't go backwards into the building walls, only forward with the
        /// wind"): an upright plane through the anchor, the free particles kept on its side
        /// (<paramref name="x"/>, <paramref name="z"/>) -- the level way out in front of it,
        /// relative to the anchor's axes. A cloth pressed back onto it by the wind lies flat on it,
        /// as a banner on a wall does. Infinite: everything the cloth can reach hangs from the
        /// hoist on it. Set between steps; (0, 0) clears it.
        /// </summary>
        public void SetPlate(float x, float z)
        {
            float len = (float)Math.Sqrt(x * x + z * z);
            if (len < 1e-6f)
            {
                _plateOn = false;
                return;
            }

            _plateOn = true;
            _plateX = x / len;
            _plateZ = z / len;
        }

        // ---- held edges (FlagHold) ----------------------------------------------------------------

        /// <summary>
        /// The share of a held span left slack, so the cloth between two holds can bow and ripple
        /// (held at its full flat length it is a board): along the fly when the far edge or the far
        /// corners are held (against the hoist), down the hoist when the top and the bottom edges
        /// are (against each other).
        /// </summary>
        public const float HoldSlack = 0.04f;

        /// <summary>
        /// Passes added to a cloth with held edges: a membrane held round carries the wind's
        /// pressure as tension (offline, 3 x 2 m held at its corners, the wind across: worst link
        /// 1.47 with 5 passes and knotted twice in 20 s, 1.29 with 7 and none).
        /// </summary>
        public const int HeldPasses = 2;

        /// <summary>The slack along the fly (the far edge or corners held against the hoist), its own: see <see cref="HoldSlack"/>.</summary>
        public const float HoldSlackAlong = 0.08f;

        /// <summary>
        /// The slack down the hoist grows from none at the hoist (held there whole) to all of it
        /// this share of the length along: a held bottom edge drawn up a little, not kinked at the pole.
        /// </summary>
        public const float HoldRamp = 0.25f;

        /// <summary>
        /// A held corner (FlagHold.FlyCorners) is held along both its edges this share of the
        /// flag's shorter side (at least a cell each way), as a grommet on a bracket is: one
        /// particle held alone took the whole free edge's pull (offline, 3 x 2 m at 6 m/s: its
        /// links 2.1-2.7 times their length, knotted 5-9 times in 20 s).
        /// </summary>
        public const float CornerShare = 0.15f;

        private int _holds;
        private float _holdFx = 1f, _holdFy, _holdFz;
        private int[] _heldK;
        private float[] _heldAlong, _heldDown;
        private bool[] _heldMark;
        private int _heldCount;

        /// <summary>The edges held besides the hoist (FlagHold), 0 for none.</summary>
        public int Holds
        {
            get { return _holds; }
        }

        /// <summary>
        /// Holds more of the cloth than its hoist (the author, 2026-11-11: banners "hoisted from the
        /// side, but are steady at the top and bottom so the whole advertisement on the banner is
        /// visible but the flag itself is cloth and ripples"): its first row (the top edge of a
        /// flag from its side), its last row, the fly's end of every row, or of the first and last
        /// rows only (FlagHold bits), each held where the flat flag along (flyX, flyY, flyZ) would
        /// have it -- the fly at rest, square to the hoist, the flag's heading -- less
        /// <see cref="HoldSlack"/> between opposing holds. A held particle stays FREE (its inverse
        /// mass, its links, everything that reads a pinned particle as the hoist unchanged) and is
        /// put on its hold every pass after the attachments, as the pole and the back plate are
        /// kept off (KeepHeld): the surface still wins over it. The fly at rest turned (the flag
        /// turned with its prop) turns the cloth rigidly with it. Set between steps; 0 lets go.
        /// </summary>
        public void SetHolds(int mask, float flyX, float flyY, float flyZ)
        {
            mask &= FlagHold.All;

            // The fly square to the hoist.
            float along = flyX * _hx + flyY * _hy + flyZ * _hz;
            float fx = flyX - along * _hx, fy = flyY - along * _hy, fz = flyZ - along * _hz;
            float len = (float)Math.Sqrt(fx * fx + fy * fy + fz * fz);
            if (len < 1e-4f)
            {
                fx = _holdFx; fy = _holdFy; fz = _holdFz;
            }
            else
            {
                fx /= len; fy /= len; fz /= len;
            }

            bool turned = false;
            if (_holds != 0 && mask != 0)
            {
                float c = _holdFx * fx + _holdFy * fy + _holdFz * fz;
                if (c < 0.99999f)
                {
                    // Old x new; opposite ways turn half round the hoist.
                    float ax = _holdFy * fz - _holdFz * fy, ay = _holdFz * fx - _holdFx * fz, az = _holdFx * fy - _holdFy * fx;
                    float s = (float)Math.Sqrt(ax * ax + ay * ay + az * az);
                    if (s < 1e-6f)
                    {
                        ax = _hx; ay = _hy; az = _hz;
                        s = 0f;
                        c = -1f;
                    }
                    else
                    {
                        ax /= s; ay /= s; az /= s;
                    }

                    Rotate(X, Y, Z, ax, ay, az, c, s);
                    Rotate(PrevX, PrevY, PrevZ, ax, ay, az, c, s);
                    Rotate(Vx, Vy, Vz, ax, ay, az, c, s);
                    turned = true;
                }
            }

            _holdFx = fx; _holdFy = fy; _holdFz = fz;
            if (mask != _holds)
            {
                _holds = mask;
                BuildHeld();
            }

            if (turned)
            {
                PinHoist();
                PlaceHeld(true);
                ComputeNormals(X, Y, Z, Nx, Ny, Nz);
                _primed = false;
                _listed = false;
            }
        }

        /// <summary>
        /// The held particles of <see cref="Holds"/> and where each is held, in the hoist's and the
        /// fly's terms; then the links made again, a held particle weighed in them as a pinned one
        /// is (it gives nothing back: snapped onto its hold each pass, half of every link's pull
        /// on it was lost, its neighbours stretched).
        /// </summary>
        private void BuildHeld()
        {
            _heldVersion++;
            _heldCount = 0;
            if (_heldMark != null)
                Array.Clear(_heldMark, 0, _heldMark.Length);

            if (_holds != 0)
                FindHeld();

            BuildLinks();
            SetFabric(_fabric);
        }

        private void FindHeld()
        {
            if (_heldK == null || _heldK.Length < Count)
            {
                _heldK = new int[Count];
                _heldAlong = new float[Count];
                _heldDown = new float[Count];
                _heldMark = new bool[Count];
            }

            bool top = (_holds & FlagHold.TopEdge) != 0, bottom = (_holds & FlagHold.BottomEdge) != 0;
            bool fly = (_holds & FlagHold.FlyEdge) != 0, corners = (_holds & FlagHold.FlyCorners) != 0;
            float slackU = fly || corners ? HoldSlackAlong : 0f;
            float slackV = top && bottom ? HoldSlack : 0f;
            float ramp = Math.Max(HoldRamp * Length, 1e-3f);
            int last = Rows - 1;
            float corner = CornerShare * Math.Min(Length, Height);
            int cornerCols = Math.Max(1, (int)Math.Round(corner / CellU)), cornerRows = Math.Max(1, (int)Math.Round(corner / CellV));

            for (int j = 0; j < Rows; j++)
            {
                // The fly's end of this row: its last cloth particle (a cut's outline).
                int end = 0;
                for (int i = Columns - 1; i > 0; i--)
                {
                    if (IsCloth(j * Columns + i))
                    {
                        end = i;
                        break;
                    }
                }

                for (int i = 1; i <= end; i++)
                {
                    int k = j * Columns + i;
                    if (!IsCloth(k))
                        continue;

                    // A corner: along its edge and down (or up) the fly a little way from it.
                    bool nearCorner = corners && i == end && (j <= cornerRows || j >= last - cornerRows) ||
                                      corners && (j == 0 || j == last) && i >= end - cornerCols;
                    bool held = (top && j == 0) || (bottom && j == last) || (fly && i == end) || nearCorner;
                    if (!held)
                        continue;

                    float u = i * CellU;
                    _heldK[_heldCount] = k;
                    _heldAlong[_heldCount] = u * (1f - slackU);
                    _heldDown[_heldCount] = HoistDown(j) * (1f - slackV * Math.Min(1f, u / ramp));
                    _heldMark[k] = true;
                    _heldCount++;
                }
            }
        }

        /// <summary>Every held particle on its hold (predicted places, in a pass); with <paramref name="still"/>, its place, last place and velocity too (a layout).</summary>
        private void PlaceHeld(bool still)
        {
            float[] x = still ? X : _px, y = still ? Y : _py, z = still ? Z : _pz;
            for (int n = 0; n < _heldCount; n++)
            {
                int k = _heldK[n];
                float a = _heldAlong[n], d = _heldDown[n];
                x[k] = d * _hx + a * _holdFx;
                y[k] = d * _hy + a * _holdFy;
                z[k] = d * _hz + a * _holdFz;
                if (still)
                {
                    PrevX[k] = X[k]; PrevY[k] = Y[k]; PrevZ[k] = Z[k];
                    Vx[k] = 0f; Vy[k] = 0f; Vz[k] = 0f;
                }
            }
        }

        /// <summary>No back plate (the cloth free to swing either way, as it always was).</summary>
        public void ClearPlate()
        {
            _plateOn = false;
        }

        public bool HasPlate
        {
            get { return _plateOn; }
        }

        /// <summary>
        /// A free particle behind the plate (<see cref="SetPlate"/>) goes square to it, onto it,
        /// less <see cref="ContactFriction"/> of its sliding along it since the step began, as
        /// against a wall of the city (<see cref="Slide"/>): pressed onto the plate by a wind of 12
        /// m/s -- 80 times the cloth's weight -- with none, a banner rode up the plate on its own
        /// wrinkles and folded over its hoist (the preview, 2026-11-09). Inside every pass, with
        /// the pole, before the attachments (a tether to a point on the plate or in front of it
        /// keeps a particle in front: the half-space is convex).
        /// </summary>
        private void KeepInFront()
        {
            float[] px = _px, py = _py, pz = _pz;
            float nx = _plateX, nz = _plateZ;
            for (int k = 0; k < Count; k++)
            {
                if (_invMass[k] == 0f)
                    continue;

                float d = px[k] * nx + pz[k] * nz;
                if (d < 0f)
                    Slide(k, px[k] - d * nx, py[k], pz[k] - d * nz, nx, 0f, nz);
            }
        }

        /// <summary>The free particles behind the plate by more than a layout's noise, where they are (a layout to be tried another way: <see cref="InTheWay"/>).</summary>
        public int BehindPlate
        {
            get { return CountBehindPlate(); }
        }

        private int CountBehindPlate()
        {
            if (!_plateOn)
                return 0;

            int behind = 0;
            for (int k = 0; k < Count; k++)
            {
                if (_invMass[k] != 0f && X[k] * _plateX + Z[k] * _plateZ < -PlateSlack)
                    behind++;
            }

            return behind;
        }

        /// <summary>A layout's noise behind the plate by less than this, metres, is not in the way (the first step puts it on the plate).</summary>
        private const float PlateSlack = 0.02f;

        /// <summary>
        /// A surface to stay above (FlagSurroundings: the ground, the water and the roofs round the
        /// flag), its point of the world at (<paramref name="x"/>, <paramref name="y"/>,
        /// <paramref name="z"/>) from the anchor; null: none (the flat ground, <see cref="GroundY"/>,
        /// as before). Set between steps -- every frame, as the anchor moves.
        /// </summary>
        public void SetSurface(SurfaceGrid grid, float x, float y, float z)
        {
            _surface = grid;
            _surfaceX = x; _surfaceY = y; _surfaceZ = z;
            if (grid == null)
            {
                _surfaceOn = false;
                _surfaceBlocked = false;
                _blockedByHold = false;
                _heaped = 0;
            }
        }

        /// <summary>Upright capsules to keep off (the props' and trees' round the flag), their point of the world at (x, y, z) from the anchor; null: none.</summary>
        public void SetCapsules(CapsuleSet set, float x, float y, float z)
        {
            _capsules = set;
            _capsulesX = x; _capsulesY = y; _capsulesZ = z;
            if (set == null)
                _capCount = 0;
        }

        /// <summary>
        /// The last step found the surface over the cloth's own hoist (the flag under a roof, inside
        /// a building, or its hoist deeper in a wall than <see cref="WallShiftNodes"/> keeps back): it
        /// left the surface out -- the flat ground alone, as before -- rather than pull the cloth
        /// against its hoist.
        /// </summary>
        public bool SurfaceBlocked
        {
            get { return _surfaceBlocked; }
        }

        /// <summary>The last step kept the cloth above the surface (it was in reach).</summary>
        public bool SurfaceInUse
        {
            get { return _surfaceOn; }
        }

        /// <summary>The capsules the last step kept the cloth off (those in reach).</summary>
        public int CapsulesInUse
        {
            get { return _capCount; }
        }

        /// <summary>
        /// Takes the shape of <paramref name="old"/>, a cloth of another size, proportion or
        /// detail (the flag resized in the panel, or seen nearer or further off): each particle
        /// goes where the same point of the old cloth was (bilinear between its particles),
        /// relative to its row's hoist point and scaled by the change in length, and stands still
        /// there -- or, with <paramref name="keepMotion"/> (the same flag at another detail), goes
        /// on moving as that point moved. What the change of proportion leaves uneven, the
        /// constraints put right within a few steps. So a flag being resized keeps flying instead
        /// of starting over from flat.
        /// </summary>
        public void TakeShape(FlagCloth old, bool keepMotion = false)
        {
            float scale = Length / old.Length;
            int oc = old.Columns, orows = old.Rows;

            // The same flag: it hangs from the same line, the same way (sewn on, or clipped to a halyard).
            float hx = old._hx, hy = old._hy, hz = old._hz;
            _hx = hx; _hy = hy; _hz = hz;
            SetHalyard(old._clipped);

            // The same air goes on blowing on it: its gusts and eddies where they were.
            Air = old.Air;

            // Its held edges as the old cloth's (on this grid): a new detail is not a new hold.
            _holdFx = old._holdFx; _holdFy = old._holdFy; _holdFz = old._holdFz;
            if (old._holds != _holds)
            {
                _holds = old._holds;
                BuildHeld();
            }

            for (int j = 0; j < Rows; j++)
            {
                float v = j / (float)(Rows - 1);
                float fj = v * (orows - 1);
                int j0 = Math.Min((int)fj, orows - 2);
                float tj = fj - j0;

                // This row's hoist point, and the old cloth's at the same share of its hoist.
                float down = j * CellV, oldDown = v * old.Height;
                float hoistX = down * hx, hoistY = down * hy, hoistZ = down * hz;
                float oldX = oldDown * hx, oldY = oldDown * hy, oldZ = oldDown * hz;

                for (int i = 0; i < Columns; i++)
                {
                    int k = j * Columns + i;

                    // Held (the hoist, or on a halyard its two clips): on the line, where PinHoist puts it.
                    if (_invMass[k] == 0f && IsCloth(k))
                    {
                        float held = HoistDown(j);
                        X[k] = held * hx; Y[k] = held * hy; Z[k] = held * hz;
                        PrevX[k] = X[k]; PrevY[k] = Y[k]; PrevZ[k] = Z[k];
                        Vx[k] = 0f; Vy[k] = 0f; Vz[k] = 0f;
                        continue;
                    }

                    float fi = i / (float)(Columns - 1) * (oc - 1);
                    int i0 = Math.Min((int)fi, oc - 2);
                    float ti = fi - i0;

                    int a = j0 * oc + i0, b = a + 1, c = a + oc, d = c + 1;
                    float wa = (1f - ti) * (1f - tj), wb = ti * (1f - tj), wc = (1f - ti) * tj, wd = ti * tj;

                    // Relative to the old row's hoist point, scaled, onto the new one's.
                    X[k] = hoistX + (old.X[a] * wa + old.X[b] * wb + old.X[c] * wc + old.X[d] * wd - oldX) * scale;
                    Y[k] = hoistY + (old.Y[a] * wa + old.Y[b] * wb + old.Y[c] * wc + old.Y[d] * wd - oldY) * scale;
                    Z[k] = hoistZ + (old.Z[a] * wa + old.Z[b] * wb + old.Z[c] * wc + old.Z[d] * wd - oldZ) * scale;

                    if (keepMotion)
                    {
                        PrevX[k] = hoistX + (old.PrevX[a] * wa + old.PrevX[b] * wb + old.PrevX[c] * wc + old.PrevX[d] * wd - oldX) * scale;
                        PrevY[k] = hoistY + (old.PrevY[a] * wa + old.PrevY[b] * wb + old.PrevY[c] * wc + old.PrevY[d] * wd - oldY) * scale;
                        PrevZ[k] = hoistZ + (old.PrevZ[a] * wa + old.PrevZ[b] * wb + old.PrevZ[c] * wc + old.PrevZ[d] * wd - oldZ) * scale;
                        Vx[k] = (old.Vx[a] * wa + old.Vx[b] * wb + old.Vx[c] * wc + old.Vx[d] * wd) * scale;
                        Vy[k] = (old.Vy[a] * wa + old.Vy[b] * wb + old.Vy[c] * wc + old.Vy[d] * wd) * scale;
                        Vz[k] = (old.Vz[a] * wa + old.Vz[b] * wb + old.Vz[c] * wc + old.Vz[d] * wd) * scale;
                    }
                    else
                    {
                        PrevX[k] = X[k]; PrevY[k] = Y[k]; PrevZ[k] = Z[k];
                        Vx[k] = 0f; Vy[k] = 0f; Vz[k] = 0f;
                    }
                }
            }

            // Cut: what is not cloth carries on from the cloth (the old cloth's shape there was another cut's, or none).
            FillCutStill();

            ComputeNormals(X, Y, Z, Nx, Ny, Nz);
            _primed = false;
            _listed = false;

            // Still being let down into the air (a flag made finer as it settles): so is this one.
            _calm = old._calm;

            // Not laid out: the old cloth's shape, which the city may have swept into (FindExits).
            _laid = false;
        }

        /// <summary>
        /// How fast, at most, the anchor's move drags the cloth through the air -- leaves its free
        /// particles behind in the world for the held hoist to pull after it -- as a share of
        /// sqrt(g L), the speed a flag swings at as a pendulum of its length, so a flag of any size
        /// sways alike when it is moved: 1.1 m/s for a 3 m flag, 1.9 m/s for 8.9 m, 3.4 m/s for
        /// 30 m. The rest of the move carries it as it is: the attachment's motion only partly given
        /// to the cloth, as PhysX's and Unity's cloth scale it (their world velocity and inertia
        /// scales).
        /// The first rule dragged it up to half a cell a FRAME however long a move went on (a
        /// height changed 10 m in ten frames, the case it was measured on, stays 5 cells): Move
        /// It's Page Up/Down (0.125 m every GUI event once held 333 ms: 15-22 m/s) and its drags
        /// pushed the hoist through its own cloth at 5-10 m/s for as long as they lasted. A 3 m flag
        /// dragged to and fro at 10 m/s in calm air, and the author's 8.9 m flag on a halyard moved
        /// to and fro over kerbs as the placing pointer does, were still tangled 6 s after -- 9-73
        /// pairs of triangles crossing, 46-47% stretch -- and every fast move crumpled the cloth to
        /// a third of its spread (the scratchpad's move harness, 2026-09-28). Capped at this speed
        /// and by <see cref="MaxDragCells"/>, none crossed itself at any time in 58 of 59 such
        /// moves -- Page Up and Down, drags straight, to and fro and round, the pointer over kerbs
        /// and up walls, at 20, 60 and 144 fps, in calm air and the game's wind, 1-30 m flags at
        /// Normal to Ultra -- the worst link 1.30; the 59th, a flag lowered until its hoist sank
        /// into its own cloth heaped on the ground, was untied by <see cref="KnotStretch"/>.
        /// </summary>
        public const float DragFroude = 0.2f;

        /// <summary>
        /// And at most this share of a cell in one sub-step: a cloth's fine cells cross one another
        /// at a speed its coarse ones take. A 3 m flag at Ultra (8 cm cells) dragged to and fro in
        /// calm air crossed itself for moments at 0.15 and 0.1 of a cell a 1/60 s step (0.7 and
        /// 0.47 m/s), never at 0.05 (0.24 m/s), at 20, 60 or 144 fps. The drag this leaves: 0.5 m/s
        /// for a 3 m flag at Normal, 1.1 m/s for the author's 8.9 m one at High, the Froude limit
        /// for big flags at Normal.
        /// </summary>
        public const float MaxDragCells = 0.05f;

        /// <summary>
        /// A move of the anchor longer than this in one go, metres, is a jump -- a paste, an undo,
        /// the placing pointer crossing a roof's edge: it carries the cloth whole.
        /// </summary>
        public const float JumpDistance = 2f;

        /// <summary>The anchor's moves since the cloth was last stepped, as the drag they would give its free particles (against the move, relative to the anchor).</summary>
        private float _dragX, _dragY, _dragZ;

        /// <summary>The share of it each sub-step of the job under way takes (<see cref="PlanDrag"/>).</summary>
        private float _dragStepX, _dragStepY, _dragStepZ;

        /// <summary>The most the anchor's move drags the cloth, m/s (<see cref="DragFroude"/>).</summary>
        public float DragSpeed
        {
            get { return DragFroude * (float)Math.Sqrt(Gravity * Length); }
        }

        /// <summary>
        /// The anchor moved by (dx, dy, dz) in the world. Nothing moves yet: the next steps drag
        /// the cloth by as much of it as the cloth can follow (<see cref="PlanDrag"/>), and the
        /// rest carries it -- a jump (<see cref="JumpDistance"/>) whole. Between steps (the main
        /// thread, like every change from outside).
        /// </summary>
        public void MoveAnchor(float dx, float dy, float dz)
        {
            if (dx * dx + dy * dy + dz * dz > JumpDistance * JumpDistance)
            {
                _dragX = 0f; _dragY = 0f; _dragZ = 0f;
                return;
            }

            _dragX -= dx; _dragY -= dy; _dragZ -= dz;
        }

        /// <summary>
        /// Before a job's steps (ClothWorker.Execute): of the anchor's moves since the last steps,
        /// the drag the cloth takes in the job's <paramref name="seconds"/> -- at most
        /// <see cref="DragSpeed"/>, and <see cref="MaxDragCells"/> a sub-step -- spread evenly
        /// over its <paramref name="subSteps"/>. The rest has carried it, and is let go: a cloth
        /// not stepped while its flag moved (out of view, waiting its turn) is carried, never
        /// dragged all at once when it steps again.
        /// </summary>
        public void PlanDrag(float seconds, int subSteps)
        {
            float x = _dragX, y = _dragY, z = _dragZ;
            _dragX = 0f; _dragY = 0f; _dragZ = 0f;
            _dragStepX = 0f; _dragStepY = 0f; _dragStepZ = 0f;

            float d = (float)Math.Sqrt(x * x + y * y + z * z);
            if (subSteps <= 0 || seconds <= 0f || d < 1e-7f)
                return;

            float allowed = Math.Min(DragSpeed * seconds, MaxDragCells * Math.Min(CellU, CellV) * subSteps);
            float s = (d > allowed ? allowed / d : 1f) / subSteps;
            _dragStepX = x * s; _dragStepY = y * s; _dragStepZ = z * s;
        }

        /// <summary>After a job's steps: the drag planned for them is done.</summary>
        public void EndDrag()
        {
            _dragStepX = 0f; _dragStepY = 0f; _dragStepZ = 0f;
        }

        /// <summary>
        /// A cloth laid out flat and let go into the air (a new flag, one laid out afresh) is
        /// damped this much more, times sqrt(g / L) per second, for its first moments
        /// (<see cref="Calm"/>): critically, as a pendulum of its length, so it sinks into the
        /// way it hangs or flies instead of falling and whipping -- dynamic relaxation, the damped
        /// run that brings a laid-out net or membrane to rest (Day 1965; Barnes 1988, "Form-finding
        /// and analysis of prestressed nets and membranes"). Dropped flat into calm air undamped,
        /// the fly fell and whipped through the rest of the cloth, and self-collision locked what
        /// crossed: of 12 flags of 1-30 m settled as the renderer settles them (1/30 s steps), 4
        /// stayed tangled for good, 14-24 pairs of triangles crossing, 28-81% stretch (2-5 m flags
        /// at the finer details; a 1 m flag too, in 1/60 s steps); damped so for the settle's
        /// 1.5 s, none (the scratchpad's settle2 harness, 2026-09-28). It is gravity's fall in still
        /// air that whips: the damping fades out with the wind at the flag (none from
        /// <see cref="CalmWind"/>), which carries a cloth out instead of dropping it -- a flag in wind
        /// flies out and flutters as it always did.
        /// </summary>
        public const float CalmDamping = 2f;

        /// <summary>The wind at the flag from which a cloth is not calmed at all, m/s (<see cref="CalmDamping"/>).</summary>
        public const float CalmWind = 3f;

        /// <summary>
        /// How long a new cloth is calmed, seconds: the renderer's hidden settle (1.5 s) and as long
        /// again once it shows, while its fly is still falling -- and not made finer meanwhile
        /// (<see cref="Calming"/>). Flags of 2-4 m settled at Normal and made finer (to Ultra's 7-8
        /// cm cells) 0-1.5 s after they showed, as the renderer makes near flags finer, knotted in 8
        /// cases of 21 calmed for 1.5 s; calmed for 3 s and made finer only after, in none of 7
        /// (the scratchpad's knot2 harness, calm air). The same for a cloth laid out afresh while
        /// it flies (<see cref="Relaid"/>, <see cref="Unknotted"/>).
        /// </summary>
        public const float CalmTime = 3f;

        /// <summary>The cloth is still being let down into the air (<see cref="Calm"/>): the renderer does not make it finer yet.</summary>
        public bool Calming
        {
            get { return _calm > 0f; }
        }

        /// <summary>
        /// A cloth whose worst link has stayed this much over its length (1.3: 30%) for
        /// <see cref="KnotTime"/> seconds straight is knotted -- layers of it locked through one
        /// another, which self-collision holds on the wrong sides for good -- and is laid out
        /// afresh where it is free, calmed (<see cref="Unknotted"/>): a flag is never left knotted,
        /// whatever knotted it. A healthy cloth passes 30% only for moments (a fly's corner
        /// whipping, a flag slapping a wall: 0-2% of the time, test-cloth's city checks); every
        /// knot measured held 34-81% for as long as it was watched (the move harness: a flag
        /// lowered until its hoist met the cloth heaped on the ground under it, the settle and
        /// drag cases before their fixes).
        /// </summary>
        public const float KnotStretch = 1.3f;
        public const float KnotTime = 2f;

        /// <summary>The worst link is looked at every this many seconds of the cloth's steps.</summary>
        private const float KnotCheck = 0.1f;

        /// <summary>Cloth time since the worst link was last looked at, and how long it has been over <see cref="KnotStretch"/>.</summary>
        private float _knotClock, _knotted;

        /// <summary>Times the cloth was found knotted and laid out afresh (<see cref="KnotStretch"/>).</summary>
        public int Unknotted { get; private set; }

        /// <summary>The last knot found: its worst link's stretch (a ratio to its length), and the column and row of its first particle -- for the log.</summary>
        public float KnotLink { get; private set; }
        public int KnotColumn { get; private set; }
        public int KnotRow { get; private set; }

        /// <summary>The worst link <see cref="WorstFreeStretch"/> last found (-1: none over its length).</summary>
        private int _worstFree = -1;

        private static int _unknottedAll;

        /// <summary>Every cloth's knots undone in this process: the tests hold a healthy flag's to none, whatever it is doing.</summary>
        public static int UnknottedAll
        {
            get { return _unknottedAll; }
        }

        /// <summary>The share of the anchor's move each sub-step of the job under way drags the cloth by, metres (for the tests).</summary>
        public float PlannedDrag
        {
            get { return (float)Math.Sqrt(_dragStepX * _dragStepX + _dragStepY * _dragStepY + _dragStepZ * _dragStepZ); }
        }

        /// <summary>Seconds of <see cref="CalmDamping"/> left.</summary>
        private float _calm;

        /// <summary>The cloth is let down into the air, not dropped: <see cref="CalmDamping"/> for its next <paramref name="seconds"/> of steps. Between steps.</summary>
        public void Calm(float seconds)
        {
            _calm = Math.Max(_calm, seconds);
        }

        /// <summary>
        /// The flat ground this step (<see cref="GroundY"/>, and 2 cm), but never over the held
        /// hoist: a flag lowered into the ground hangs from its buried hoist into it, as the
        /// surface is left out over a hoist under it (<see cref="HoistUnder"/>). Held up by the
        /// ground, its cloth was torn from its hoist -- a hoist sunk 3 m under stretched its links
        /// ten times over (the move harness's sinkrise).
        /// </summary>
        private float _ground;

        /// <summary>One step of <paramref name="dt"/> seconds in the given air.</summary>
        public void Step(float dt, ref FlagAir air)
        {
            if (dt <= 0f)
                return;

            // Mostly buried last step (its flag carried into a building), or knotted for seconds: laid out afresh where it is free.
            bool buried = _heaped > BuriedShare * Count, knotted = _knotted >= KnotTime;
            if (buried || knotted)
            {
                float wx = air.MeanX, wz = air.MeanZ;
                if (wx * wx + wz * wz < 1e-6f)
                {
                    wx = X[Count - 1] - X[0];
                    wz = Z[Count - 1] - Z[0];
                }

                if (knotted && !buried && _worstFree >= 0)
                {
                    int a = _a[_worstFree], b = _b[_worstFree];
                    float dx = X[a] - X[b], dy = Y[a] - Y[b], dz = Z[a] - Z[b];
                    KnotLink = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz) / _rest[_worstFree];
                    KnotColumn = a % Columns;
                    KnotRow = a / Columns;
                }

                int left = LayOutClear(wx, wz);
                Calm(CalmTime);
                _knotted = 0f;
                if (buried)
                {
                    Relaid++;

                    // Held, it lays one way only: still in the way, the surface is left out.
                    if (_heldCount > 0 && left > 0 && _surface != null)
                        HoldsInTheWay();
                }
                else
                {
                    Unknotted++;
                    System.Threading.Interlocked.Increment(ref _unknottedAll);
                }
            }

            _heaped = 0;
            int n = Count;
            float sigma = Math.Max(_fabric.Density, 0.01f);

            // This sub-step's share of the anchor's move (PlanDrag): the free particles left behind in the world by it.
            if (_dragStepX != 0f || _dragStepY != 0f || _dragStepZ != 0f)
            {
                for (int k = 0; k < n; k++)
                {
                    if (_invMass[k] == 0f)
                        continue;

                    X[k] += _dragStepX; Y[k] += _dragStepY; Z[k] += _dragStepZ;
                }
            }

            // The air carried sideways with each square metre of cloth, and the inertia a normal
            // force has to move: the cloth's own and that air's.
            float added = _fabric.AddedMass * AirDensity * (float)Math.PI * Height * 0.25f;
            float inertia = sigma + added;
            float shareAll = added / inertia;

            float dragAll = _fabric.Drag * AirDensity / (2f * inertia) * dt;
            float friction = _fabric.Friction * AirDensity / (2f * sigma) * dt;
            float damping = _fabric.Damping;
            if (_calm > 0f)
            {
                // Let down, not dropped: critically damped as a pendulum of its length, less as the wind carries it.
                float wind = (float)Math.Sqrt(air.MeanX * air.MeanX + air.MeanY * air.MeanY + air.MeanZ * air.MeanZ);
                damping += CalmDamping * (float)Math.Sqrt(Gravity / Length) * Math.Max(0f, 1f - wind / CalmWind);
                _calm -= dt;
            }

            float keep = Math.Max(0f, 1f - damping * dt);
            float gravity = Gravity * dt;
            float invDt = 1f / dt;
            float invCell = 1f / CellU;

            // ---- the air: sampled at two heights of each column and blended down it (the eddies
            // vary along the wind far more than up a flag, and the slender-body force acts on the
            // whole span at once) -- a tenth of the noise a sample per particle took ----
            int cols = Columns;
            int upper = _rowUpper * cols, lower = _rowLower * cols;
            for (int i = 0; i < cols; i++)
            {
                air.At(X[upper + i], Y[upper + i], Z[upper + i], out _colUx[i], out _colUy[i], out _colUz[i]);
                air.At(X[lower + i], Y[lower + i], Z[lower + i], out _colLx[i], out _colLy[i], out _colLz[i]);
            }

            // ---- the air at every particle, and its normal parts ----
            for (int j = 0, k = 0; j < Rows; j++)
            {
                float t = _rowBlend[j], s = 1f - t;
                for (int i = 0; i < cols; i++, k++)
                {
                    PrevX[k] = X[k]; PrevY[k] = Y[k]; PrevZ[k] = Z[k];

                    float ux = _colUx[i] * s + _colLx[i] * t;
                    float uy = _colUy[i] * s + _colLy[i] * t;
                    float uz = _colUz[i] * s + _colLz[i] * t;
                    _ux[k] = ux; _uy[k] = uy; _uz[k] = uz;

                    float nx = Nx[k], ny = Ny[k], nz = Nz[k];
                    float un = ux * nx + uy * ny + uz * nz;
                    _un[k] = un;
                    _vn[k] = un - (Vx[k] * nx + Vy[k] * ny + Vz[k] * nz);
                }
            }

            if (!_primed)
            {
                Array.Copy(_un, _unPrev, n);
                _primed = true;
            }

            // ---- the reactive term, m (d/dt + U d/ds) V_n, at every free particle: the flow U
            // along the cloth towards the fly, so the upstream neighbour is k - 1 ----
            float meanX = air.MeanX, meanY = air.MeanY, meanZ = air.MeanZ;
            for (int j = 0; j < Rows; j++)
            {
                int row = j * cols;
                _react[row] = 0f;

                for (int i = 1; i < cols; i++)
                {
                    int k = row + i;
                    int up = k - 1;
                    int down = i < cols - 1 ? k + 1 : k;
                    if (_shaped)
                    {
                        // Out of the cloth: no air. The row's last cloth particle is its fly end.
                        if (_out[k])
                        {
                            _react[k] = 0f;
                            continue;
                        }

                        if (_out[down])
                            down = k;
                    }

                    float tx = X[down] - X[up], ty = Y[down] - Y[up], tz = Z[down] - Z[up];
                    float tl = (float)Math.Sqrt(tx * tx + ty * ty + tz * tz);
                    float us = tl > 1e-6f ? (meanX * tx + meanY * ty + meanZ * tz) / tl : 0f;
                    if (us < 0f)
                        us = 0f;

                    float reactive = (_un[k] - _unPrev[k]) * invDt + us * (_vn[k] - _vn[up]) * invCell;
                    // A fold turning its normal over in one step is not the flow: a ceiling well
                    // above what a 30 m/s wind asks (U^2 k^2 A ~ 1300 m/s^2 for a 20 cm ripple a
                    // metre long).
                    _react[k] = reactive > MaxReactive ? MaxReactive : reactive < -MaxReactive ? -MaxReactive : reactive;
                }
            }

            SmoothReactive();

            // ---- forces: gravity, the air, the fabric's own damping ----
            for (int k = 0; k < n; k++)
            {
                if (_invMass[k] == 0f)
                {
                    Vx[k] = 0f; Vy[k] = 0f; Vz[k] = 0f;
                    _px[k] = X[k]; _py[k] = Y[k]; _pz[k] = Z[k];
                    continue;
                }

                float nx = Nx[k], ny = Ny[k], nz = Nz[k];
                float vx = Vx[k], vy = Vy[k], vz = Vz[k];

                // Its share of the added mass down its span (the ellipse, PrepareSpanShares) -- of
                // its own span down the column on a cut cloth (a tail's, a point's).
                float share = shareAll, drag = dragAll;
                float w = _spanShare[k];
                if (_shaped || w != 1f)
                {
                    float own = (_shaped ? _fabric.AddedMass * AirDensity * (float)Math.PI * _span[k] * 0.25f : added) * w;
                    float mass = sigma + own;
                    share = own / mass;
                    if (_shaped || SpanwiseAir >= 2)
                    {
                        float dragMass = SpanwiseAir >= 2 || !_shaped ? mass : sigma + own / w;
                        drag = _fabric.Drag * AirDensity / (2f * dragMass) * dt;
                    }
                }

                // Gravity: its normal part moves the cloth AND the air it carries.
                float gn = -gravity * ny;
                vy -= gravity;
                float cut = gn * share;
                vx -= nx * cut; vy -= ny * cut; vz -= nz * cut;

                float an = _react[k] * share * dt;
                vx += nx * an; vy += ny * an; vz += nz * an;

                // Resistive, across the cloth: implicit, against the whole inertia.
                float rx = _ux[k] - vx, ry = _uy[k] - vy, rz = _uz[k] - vz;
                float wn = rx * nx + ry * ny + rz * nz;
                float a = drag * Math.Abs(wn);
                float f = wn * a / (1f + a);
                vx += nx * f; vy += ny * f; vz += nz * f;

                // Friction along it: implicit, against the cloth alone.
                float s = (float)Math.Sqrt(rx * rx + ry * ry + rz * rz);
                float b = friction * s;
                float g = b / (1f + b);
                vx += (rx - nx * wn) * g;
                vy += (ry - ny * wn) * g;
                vz += (rz - nz * wn) * g;

                vx *= keep; vy *= keep; vz *= keep;
                Vx[k] = vx; Vy[k] = vy; Vz[k] = vz;

                _px[k] = X[k] + vx * dt;
                _py[k] = Y[k] + vy * dt;
                _pz[k] = Z[k] + vz * dt;
            }

            Array.Copy(_un, _unPrev, n);

            // ---- the flat ground, lowered under a buried hoist ----
            float ground = GroundY + 0.02f;
            for (int j = 0; j < Rows; j++)
            {
                int k = j * Columns;
                if (_invMass[k] == 0f && Y[k] - 0.02f < ground)
                    ground = Y[k] - 0.02f;
            }

            // ... and under a buried held edge's place (2026-11-11): held below the ground, a 19 m
            // banner 12 m up, held at its bottom, had its last free column held up by the ground
            // 5.5 m from its hold -- links 11.6 times their length, "knotted" every 2 s.
            for (int m = 0; m < _heldCount; m++)
            {
                float y = _heldDown[m] * _hy + _heldAlong[m] * _holdFy - 0.02f;
                if (y < ground)
                    ground = y;
            }

            _ground = ground;

            // ---- the city in reach this step: the surface under the cloth, the capsules near it ----
            PrepareColliders(dt);

            // ---- the pairs of particles near enough to collide this step (none while it is flat) ----
            float thick = 0f;
            if (SelfCollision && !Flat())
            {
                thick = CollisionWidth;
                ListPairs(thick);
            }
            else
            {
                _listed = false;
            }

            // ---- constraints: links, the cloth against itself, then the attachments and the ground, each pass ----
            int passes = Passes;
            float[] px = _px, py = _py, pz = _pz, rest = _rest, kOut = _kOut, kIn = _kIn, wa = _wa, wb = _wb;
            int[] la = _a, lb = _b;
            int links = _links;
            for (int pass = 0; pass < passes; pass++)
            {
                // Alternate the order, so no side of the cloth is always last to be corrected.
                bool forward = (pass & 1) == 0;
                for (int m = 0; m < links; m++)
                {
                    int c = forward ? m : links - 1 - m;
                    int a = la[c], b = lb[c];
                    float dx = px[a] - px[b], dy = py[a] - py[b], dz = pz[a] - pz[b];
                    float d2 = dx * dx + dy * dy + dz * dz;
                    if (d2 < 1e-12f)
                        continue;

                    float d = (float)Math.Sqrt(d2);
                    float r = rest[c];
                    float k = d > r ? kOut[c] : kIn[c];
                    float s = k * (d - r) / d;
                    float sa = s * wa[c], sb = s * wb[c];
                    px[a] -= dx * sa; py[a] -= dy * sa; pz[a] -= dz * sa;
                    px[b] += dx * sb; py[b] += dy * sb; pz[b] += dz * sb;
                }

                if (thick > 0f)
                    Collide(thick);

                if (_poleOn)
                    KeepOffPole();

                if (_plateOn)
                    KeepInFront();

                if (_capCount > 0)
                    KeepOffCapsules();

                Attach();

                // Held edges on their holds (SetHolds): after the attachments, before the city.
                if (_heldCount > 0)
                    PlaceHeld(false);

                // Last: a roof or a wall is never gone through, whatever pulled the cloth there --
                // and what began the step inside the city goes no deeper, on its way out.
                if (_surfaceOn)
                    KeepAboveSurface();

                if (_exiting > 0)
                    KeepExiting();
            }

            _laid = false;

            // ---- velocities are what the projection left ----
            float inv = 1f / dt;
            for (int k = 0; k < n; k++)
            {
                if (_invMass[k] == 0f)
                    continue;

                Vx[k] = (_px[k] - X[k]) * inv;
                Vy[k] = (_py[k] - Y[k]) * inv;
                Vz[k] = (_pz[k] - Z[k]) * inv;
                X[k] = _px[k]; Y[k] = _py[k]; Z[k] = _pz[k];
            }

            // A cut cloth: what is not cloth goes where the cloth beside it would carry on to.
            if (_shaped)
                FillCut(X, Y, Z);

            ComputeNormals(X, Y, Z, Nx, Ny, Nz);

            // Knotted? Its worst link off the pole and the city's posts, every KnotCheck: laid out
            // afresh after KnotTime over KnotStretch.
            _knotClock += dt;
            if (_knotClock >= KnotCheck)
            {
                _knotted = WorstFreeStretch() > KnotStretch ? _knotted + _knotClock : 0f;
                _knotClock = 0f;
            }
        }

        /// <summary>A particle this close to the pole's or a capsule's collider, metres, lies on it (<see cref="WorstFreeStretch"/>).</summary>
        private const float ContactSlack = 0.01f;

        /// <summary>
        /// <see cref="MaxStretch"/> less the links with an end on the pole or a capsule in use (within
        /// its collider and <see cref="ContactSlack"/>): the knot test's. A link can only go round a
        /// post as a chord, and the collider holds it there: a flag draped over its pole at Very high
        /// or Ultra (8-9 cm links round a pole ~15 cm across) kept its links by the pole 33-90% long
        /// for good, the rest of it whole (no two triangles crossing), and was laid out afresh every
        /// few seconds -- 0.1.0.42392 in the game; offline a 3 m flag on a halyard, the wind from
        /// behind, 174 times a cloth-hour, and none with these links left out, every genuine knot
        /// still found (the knots harness, 2026-09-29). Only a link that would be the worst so far
        /// is looked at more closely. A cloth with held edges leaves out the links at and beside
        /// its holds too (<see cref="MarkNearHold"/>).
        /// </summary>
        private float WorstFreeStretch()
        {
            float worst = 1f;
            int worstLink = -1;
            bool posts = _poleOn || _capCount > 0;
            bool held = _heldCount > 0;
            for (int c = 0; c < _links; c++)
            {
                if (_kind[c] != Structural || (held && _nearHold[c]))
                    continue;

                int a = _a[c], b = _b[c];
                float dx = X[a] - X[b], dy = Y[a] - Y[b], dz = Z[a] - Z[b];
                float r = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz) / _rest[c];
                if (r <= worst)
                    continue;

                if (posts && (OnPost(a, ContactSlack) || OnPost(b, ContactSlack)))
                    continue;

                worst = r;
                worstLink = c;
            }

            _worstFree = worstLink;
            return worst;
        }

        /// <summary>
        /// Particle <paramref name="k"/> lies on or in the pole or a capsule in use: within its
        /// collider (the radius the cloth is kept to) and <paramref name="slack"/> more.
        /// </summary>
        private bool OnPost(int k, float slack)
        {
            if (_poleOn)
            {
                float dx = X[k] - _poleX, dy = Y[k] - _poleY, dz = Z[k] - _poleZ;
                float s = dx * _hx + dy * _hy + dz * _hz;
                if (s >= _poleTop - slack && s <= _poleBottom + slack)
                {
                    float rx = dx - s * _hx, ry = dy - s * _hy, rz = dz - s * _hz;
                    float reach = _poleRadius + _poleMargin + _poleGrowth * s + slack;
                    if (rx * rx + ry * ry + rz * rz < reach * reach)
                        return true;
                }
            }

            for (int c = 0; c < _capCount; c++)
            {
                float y = Y[k];
                float ay = y < _capBottom[c] ? _capBottom[c] : y > _capTop[c] ? _capTop[c] : y;
                float dx = X[k] - _capX[c], dy = y - ay, dz = Z[k] - _capZ[c];
                float reach = _capReach[c] + slack;
                if (dx * dx + dy * dy + dz * dz < reach * reach)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Band-limits the reactive term: 1-2-1 twice down the span, three times along the flag.
        /// Lighthill's added mass rho pi H / 4 per area holds for waves longer than the flag is
        /// tall; for shorter ones it falls with the wavelength (towards rho / k, the 2-D value --
        /// Eloy et al. 2007), so the grid's own wiggles, which the U^2 k^2 of the term would
        /// drive hardest, must not be driven at all: unsmoothed, the fly edge frayed into a
        /// zigzag. And the slender-body force acts on the whole span at once, which is why real
        /// flags' crests run nearly straight from top to bottom.
        /// Along the flag, the upwind difference's own dispersion drives a wave some seven cells
        /// long at a frequency set by the step (5 Hz at 1/60 s, 12 Hz at 1/120), strongest where
        /// the air crosses 0.6 cells a sub-step: the fly edge shivered 1.25 cm (6 m/s) and 2.35 cm
        /// (12 m/s) rms above 5 Hz with one pass, against 0.5 and 0.8 cm with the steps halved
        /// (converged). Three passes: 0.56 and 1.15 cm, the flutter itself unchanged (10-13 cm).
        /// </summary>
        private void SmoothReactive()
        {
            if (_shaped)
            {
                SmoothCutReactive();
                return;
            }

            int cols = Columns, rows = Rows;

            for (int pass = 0; pass < 2; pass++)
            {
                for (int j = 0; j < rows; j++)
                {
                    for (int i = 1; i < cols; i++)
                    {
                        int k = j * cols + i;
                        float c = _react[k];
                        float a = j > 0 ? _react[k - cols] : c;
                        float b = j < rows - 1 ? _react[k + cols] : c;
                        _smooth[k] = 0.25f * a + 0.5f * c + 0.25f * b;
                    }
                }

                for (int j = 0; j < rows; j++)
                    Array.Copy(_smooth, j * cols + 1, _react, j * cols + 1, cols - 1);
            }

            for (int pass = 0; pass < 3; pass++)
            {
                for (int j = 0; j < rows; j++)
                {
                    for (int i = 1; i < cols; i++)
                    {
                        int k = j * cols + i;
                        float c = _react[k];
                        float a = i > 1 ? _react[k - 1] : c;
                        float b = i < cols - 1 ? _react[k + 1] : c;
                        _smooth[k] = 0.25f * a + 0.5f * c + 0.25f * b;
                    }
                }

                for (int j = 0; j < rows; j++)
                    Array.Copy(_smooth, j * cols + 1, _react, j * cols + 1, cols - 1);
            }
        }

        /// <summary>
        /// <see cref="SmoothReactive"/> on a cut cloth: the same passes, an out neighbour taken as
        /// the particle itself (as past the grid's edge), so no air leaks across a notch.
        /// </summary>
        private void SmoothCutReactive()
        {
            int cols = Columns, rows = Rows;
            bool[] gone = _out;

            for (int pass = 0; pass < 2; pass++)
            {
                for (int j = 0; j < rows; j++)
                {
                    for (int i = 1; i < cols; i++)
                    {
                        int k = j * cols + i;
                        float c = _react[k];
                        float a = j > 0 && !gone[k - cols] ? _react[k - cols] : c;
                        float b = j < rows - 1 && !gone[k + cols] ? _react[k + cols] : c;
                        _smooth[k] = gone[k] ? 0f : 0.25f * a + 0.5f * c + 0.25f * b;
                    }
                }

                for (int j = 0; j < rows; j++)
                    Array.Copy(_smooth, j * cols + 1, _react, j * cols + 1, cols - 1);
            }

            for (int pass = 0; pass < 3; pass++)
            {
                for (int j = 0; j < rows; j++)
                {
                    for (int i = 1; i < cols; i++)
                    {
                        int k = j * cols + i;
                        float c = _react[k];
                        float a = i > 1 ? _react[k - 1] : c;
                        float b = i < cols - 1 && !gone[k + 1] ? _react[k + 1] : c;
                        _smooth[k] = gone[k] ? 0f : 0.25f * a + 0.5f * c + 0.25f * b;
                    }
                }

                for (int j = 0; j < rows; j++)
                    Array.Copy(_smooth, j * cols + 1, _react, j * cols + 1, cols - 1);
            }
        }

        /// <summary>
        /// The long range attachments, and the ground. Each particle is held within its flat
        /// distance of three hoist points: its own row's (which stops the rows stretching) and
        /// the two corners (which stop the cloth sagging or rising away from the hoist -- the
        /// vertical stretch the row alone lets through in a strong wind). The hoist is pinned,
        /// so its points are where the pole would be -- on a halyard only its two clips are, and
        /// the rows between hang from the corners alone. Written out (Mono inlines nothing).
        /// </summary>
        private void Attach()
        {
            float[] px = _px, py = _py, pz = _pz;

            // Under a surface in reach, that is the ground (KeepAboveSurface); else the flat ground under the anchor.
            float ground = _surfaceOn ? float.MinValue : _ground;
            int cols = Columns;
            int bottom = (Rows - 1) * cols;
            float tx0 = px[0], ty0 = py[0], tz0 = pz[0];
            float bx = px[bottom], by = py[bottom], bz = pz[bottom];

            for (int j = 0; j < Rows; j++)
            {
                int row = j * cols;
                float hx = px[row], hy = py[row], hz = pz[row];

                // A row held at the hoist is tethered to its point there. On a halyard the rows
                // between the clips hang from the free heading: only the clips are fixed points,
                // and the heading's own particles are tethered to them like the rest.
                bool held = _invMass[row] == 0f;
                for (int i = held ? 1 : 0; i < cols; i++)
                {
                    int k = row + i;
                    float x = px[k], y = py[k], z = pz[k];
                    float dx, dy, dz, d2, reach;

                    if (held)
                    {
                        dx = x - hx; dy = y - hy; dz = z - hz;
                        d2 = dx * dx + dy * dy + dz * dz;
                        reach = _tether[k];
                        if (d2 > reach * reach)
                        {
                            float s = reach / (float)Math.Sqrt(d2);
                            x = hx + dx * s; y = hy + dy * s; z = hz + dz * s;
                        }
                    }

                    dx = x - tx0; dy = y - ty0; dz = z - tz0;
                    d2 = dx * dx + dy * dy + dz * dz;
                    reach = _tetherTop[k];
                    if (d2 > reach * reach)
                    {
                        float s = reach / (float)Math.Sqrt(d2);
                        x = tx0 + dx * s; y = ty0 + dy * s; z = tz0 + dz * s;
                    }

                    dx = x - bx; dy = y - by; dz = z - bz;
                    d2 = dx * dx + dy * dy + dz * dz;
                    reach = _tetherBottom[k];
                    if (d2 > reach * reach)
                    {
                        float s = reach / (float)Math.Sqrt(d2);
                        x = bx + dx * s; y = by + dy * s; z = bz + dz * s;
                    }

                    if (y < ground)
                        y = ground;

                    px[k] = x; py[k] = y; pz[k] = z;
                }
            }
        }

        /// <summary>
        /// The pole (<see cref="SetPole"/>): a free particle inside its cylinder -- widened by a
        /// margin, up to half a cell, so no link between two particles either side of it can cut
        /// through -- goes out square to the axis, to its surface. Inside every pass, before the
        /// attachments.
        /// </summary>
        private void KeepOffPole()
        {
            float[] px = _px, py = _py, pz = _pz;
            float hx = _hx, hy = _hy, hz = _hz;
            float cx = _poleX, cy = _poleY, cz = _poleZ;
            float top = _poleTop, bottom = _poleBottom, radius = _poleRadius + _poleMargin, growth = _poleGrowth;

            for (int k = 0; k < Count; k++)
            {
                if (_invMass[k] == 0f)
                    continue;

                float dx = px[k] - cx, dy = py[k] - cy, dz = pz[k] - cz;
                float s = dx * hx + dy * hy + dz * hz;
                if (s < top || s > bottom)
                    continue;

                float rx = dx - s * hx, ry = dy - s * hy, rz = dz - s * hz;
                float r2 = rx * rx + ry * ry + rz * rz;
                float reach = radius + growth * s;
                if (r2 >= reach * reach)
                    continue;

                float r = (float)Math.Sqrt(r2);
                if (r < 1e-6f)
                {
                    // On the axis itself: out towards the hoist line, which is off the pole.
                    rx = -cx + (cx * hx + cy * hy + cz * hz) * hx;
                    ry = -cy + (cx * hx + cy * hy + cz * hz) * hy;
                    rz = -cz + (cx * hx + cy * hy + cz * hz) * hz;
                    r = (float)Math.Sqrt(rx * rx + ry * ry + rz * rz);
                    if (r < 1e-6f)
                        continue;
                }

                float scale = reach / r;
                px[k] = cx + s * hx + rx * scale;
                py[k] = cy + s * hy + ry * scale;
                pz[k] = cz + s * hz + rz * scale;
            }
        }

        /// <summary>
        /// Once a step, before the passes: which of the city the cloth can touch this step. The
        /// cloth's box (where it was and where it is going, a cell round) against the surface's
        /// highest node under it and each capsule's box -- a flag high on its pole touches nothing,
        /// and costs nothing more.
        /// The surface is left out while any held particle of the hoist lies under it (the flag under
        /// a roof, inside a building: the grid holds all of that as solid down to the ground, and the
        /// cloth would be pulled against its own hoist) -- but a hoist in a wall's slope keeps the
        /// surface, the wall kept back from it (<see cref="HoistClear"/>). A capsule round the hoist
        /// is narrowed to leave the hoist outside, and its margin -- half a cell, so no link between
        /// particles either side of a post cuts it -- kept within the room left, as the pole's is
        /// (SetPole). Then what begins the step inside the city (<see cref="FindExits"/>).
        /// </summary>
        private void PrepareColliders(float dt)
        {
            _surfaceOn = false;
            _capCount = 0;
            _exiting = 0;
            if (_surface == null && _capsules == null)
                return;

            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            float[] px = _px, py = _py, pz = _pz;
            for (int k = 0; k < Count; k++)
            {
                float x = X[k], y = Y[k], z = Z[k];
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
                x = px[k]; y = py[k]; z = pz[k];
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }

            float cell = Math.Max(CellU, CellV), small = Math.Min(CellU, CellV);
            _surfaceMargin = SurfaceGap + SurfaceGapCells * small;
            _depthLimit = Math.Max(MinDepth, DepthCells * cell);
            float pad = cell + _surfaceMargin;

            _hoistTop = float.MinValue;
            for (int j = 0; j < Rows; j++)
            {
                int k = j * Columns;
                if (_invMass[k] == 0f && Y[k] > _hoistTop)
                    _hoistTop = Y[k];
            }

            SurfaceGrid grid = _surface;
            if (grid != null)
            {
                float ox, oy = _surfaceY, oz;
                bool blocked = !HoistClear(grid, out ox, out oz);
                _blockedByHold = false;
                if (!blocked && HoldsUnder(grid, ox, oz))
                {
                    // A held edge inside the city: left out as for the hoist, nothing kept back.
                    blocked = true;
                    _blockedByHold = true;
                    WallShift = 0f;
                    HoistOnEdge = false;
                    DrapeDistance = 0f;
                }

                _effX = ox;
                _effZ = oz;
                _surfaceBlocked = blocked;
                if (!blocked)
                {
                    float top = grid.TopWithin(minX - pad - ox, minZ - pad - oz, maxX + pad - ox, maxZ + pad - oz);
                    _surfaceOn = top > float.MinValue && top + oy + pad > minY;
                }
            }

            CapsuleSet set = _capsules;
            if (set == null)
            {
                FindExits(dt);
                return;
            }

            float half = 0.5f * small;
            float bx = _capsulesX, by = _capsulesY, bz = _capsulesZ;
            for (int c = 0; c < set.Count; c++)
            {
                float r = set.Radius[c];
                float cx = set.X[c] + bx, cz = set.Z[c] + bz, bottom = set.Bottom[c] + by, top = set.Top[c] + by;
                float reach = r + half + pad;
                if (cx + reach < minX || cx - reach > maxX || cz + reach < minZ || cz - reach > maxZ || top + reach < minY || bottom - reach > maxY)
                    continue;

                // The held particles' nearest approach to its axis: the hoist stays outside it, a
                // little more than its margin away.
                float nearest = float.MaxValue;
                for (int j = 0; j < Rows; j++)
                {
                    int k = j * Columns;
                    if (_invMass[k] != 0f)
                        continue;

                    float y = Y[k] < bottom ? bottom : Y[k] > top ? top : Y[k];
                    float dx = X[k] - cx, dy = Y[k] - y, dz = Z[k] - cz;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (d < nearest)
                        nearest = d;
                }

                // ... and the held edges' places, as the hoist's (2026-11-11): a 3 x 2 m flag
                // held at its far corners, a post of 25 cm at the top one, was pushed off the post
                // and put back on its hold every pass, and taken for knotted at 12 m/s.
                for (int m = 0; m < _heldCount; m++)
                {
                    float a = _heldAlong[m], dn = _heldDown[m];
                    float hx = dn * _hx + a * _holdFx, hy = dn * _hy + a * _holdFy, hz = dn * _hz + a * _holdFz;
                    float y = hy < bottom ? bottom : hy > top ? top : hy;
                    float dx = hx - cx, dy = hy - y, dz = hz - cz;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (d < nearest)
                        nearest = d;
                }

                float radius = Math.Min(r, nearest - 0.02f);
                if (radius < 0.01f)
                    continue;

                float margin = Math.Max(0f, Math.Min(half, nearest - radius - 0.002f));
                if (_capCount == _capX.Length)
                {
                    int m = _capCount * 2;
                    Array.Resize(ref _capX, m);
                    Array.Resize(ref _capZ, m);
                    Array.Resize(ref _capBottom, m);
                    Array.Resize(ref _capTop, m);
                    Array.Resize(ref _capReach, m);
                }

                _capX[_capCount] = cx;
                _capZ[_capCount] = cz;
                _capBottom[_capCount] = bottom;
                _capTop[_capCount] = top;
                _capReach[_capCount] = radius + margin;
                _capCount++;
            }

            FindExits(dt);
        }

        /// <summary>
        /// Whether the surface can be kept with the held hoist where it is, and from where: the
        /// surface's point of the world from the anchor (<paramref name="ox"/>, <paramref name="oz"/>)
        /// -- SetSurface's, or, with the hoist in a wall's slope (<see cref="WallSlope"/>), that
        /// point moved so the wall stands back from the hoist (<see cref="WallShift"/>): sampled
        /// that much further down the slope, as if the wall were built that much further off. A
        /// wall a node or two thick in the grid moves back whole, and so do the rest of the
        /// building and the ground round it, by no more than <see cref="WallShiftNodes"/> nodes.
        /// A level hoist (<see cref="LevelHoist"/>) whose cloth would hang into a wall's band of the
        /// grid is kept clear the same way (<see cref="HangsClear"/>), the wall stood back by as
        /// much as <see cref="EdgeShift"/> more. One standing ON a roof near its edge keeps the
        /// surface as it is, and its cloth is laid draped over the nearest edge
        /// (<see cref="DrapeDistance"/>, <see cref="LayOutClear"/>): along the roof to it, then down
        /// the wall, as a banner tied back from a parapet hangs; further in than EdgeShift, it lies
        /// on the roof as before. False: the hoist is under a roof or inside a building -- the
        /// surface is left out.
        /// </summary>
        private bool HoistClear(SurfaceGrid grid, out float ox, out float oz)
        {
            ox = _surfaceX;
            oz = _surfaceZ;
            float oy = _surfaceY;
            float gx = 0f, gz = 0f, ex = 0f, ez = 0f;
            bool under = false, band = false, roof = false;
            bool level = HoistY > -LevelHoist;
            float hang = level ? Math.Max(MinDepth, DepthCells * Math.Max(CellU, CellV)) : 0f;
            for (int j = 0; j < Rows; j++)
            {
                int k = j * Columns;
                float h, sx, sz;
                if (_invMass[k] != 0f || !grid.Sample(X[k] - ox, Z[k] - oz, out h, out sx, out sz))
                    continue;

                if (Y[k] < h + oy - HoistUnder)
                {
                    gx += sx;
                    gz += sz;
                    under = true;
                }
                else if (level && !HangsClear(k, h, sx, sz, oy, hang))
                {
                    ex += sx;
                    ez += sz;
                    if (sx * sx + sz * sz >= WallSlope * WallSlope)
                        band = true;
                    else
                        roof = true;
                }
            }

            DrapeDistance = 0f;
            if (!under && !band && !roof)
            {
                WallShift = 0f;
                HoistOnEdge = false;
                return true;
            }

            if (!under)
            {
                gx = ex;
                gz = ez;
            }

            // The same hoist on the same surface as last time: the same answer (the search below
            // samples the grid up to 9 x 16 times a held particle).
            int bottom = (Rows - 1) * Columns;
            if (grid == _clearGrid && ox == _clearX && oy == _clearY && oz == _clearZ && X[0] == _clearTopX && Y[0] == _clearTopY &&
                Z[0] == _clearTopZ && X[bottom] == _clearBottomX && Y[bottom] == _clearBottomY && Z[bottom] == _clearBottomZ)
            {
                ox = _clearOX;
                oz = _clearOZ;
                WallShift = _clearShift;
                HoistOnEdge = _clearEdge;
                DrapeDistance = _clearDrape;
                return _clearOk;
            }

            _clearGrid = grid; _clearX = ox; _clearY = oy; _clearZ = oz;
            _clearTopX = X[0]; _clearTopY = Y[0]; _clearTopZ = Z[0];
            _clearBottomX = X[bottom]; _clearBottomY = Y[bottom]; _clearBottomZ = Z[bottom];

            // The nearest way back that leaves the hoist the cloth's own gap clear of the surface
            // (KeepAboveSurface's: the particles beside the hoist are not pushed off it) -- down the
            // slope first (away from a wall), then the eight ways round: a hoist inside a wall's
            // band grown half a node out stands where the grid is level.
            // A level hoist wants to hang clear (HangsClear), and may have the wall kept back
            // further -- EdgeShift, and the grid's wall band besides (grown half a node out, a node
            // wide) -- in at most 32 steps, sixteen ways round (the way it drapes, too).
            float g = (float)Math.Sqrt(gx * gx + gz * gz);
            float gap = SurfaceGap + SurfaceGapCells * Math.Min(CellU, CellV);
            float step = 0.125f * grid.Spacing, most = Math.Max(WallShiftNodes * grid.Spacing, MinWallShift);
            int ways = 8;
            if (level)
            {
                most = Math.Max(most, EdgeShift + 1.5f * grid.Spacing);
                step = Math.Max(step, most / 32f);
                ways = 16;
            }

            for (float t = step; t <= most + 1e-4f; t += step)
            {
                for (int w = 0; w <= ways; w++)
                {
                    float ux, uz;
                    if (w == 0)
                    {
                        if (g < WallSlope)
                            continue;

                        ux = -gx / g;
                        uz = -gz / g;
                    }
                    else
                    {
                        double a = (w - 1) * (2.0 * Math.PI / ways);
                        ux = (float)Math.Cos(a);
                        uz = (float)Math.Sin(a);
                    }

                    float tx = ox - t * ux, tz = oz - t * uz;
                    bool clear = true;
                    for (int j = 0; j < Rows && clear; j++)
                    {
                        int k = j * Columns;
                        float h, sx, sz;
                        if (_invMass[k] == 0f && grid.Sample(X[k] - tx, Z[k] - tz, out h, out sx, out sz) &&
                            (level ? !HangsClear(k, h, sx, sz, oy, hang) : Y[k] < h + oy + gap))
                            clear = false;
                    }

                    if (!clear)
                        continue;

                    _clearOk = true;
                    _clearUX = ux;
                    _clearUZ = uz;
                    if (under || band)
                    {
                        // In a wall's band, or under the roof's edge: the wall kept back.
                        ox = tx;
                        oz = tz;
                        WallShift = t;
                        HoistOnEdge = level;
                        _clearOX = ox; _clearOZ = oz; _clearShift = t; _clearEdge = level; _clearDrape = 0f;
                    }
                    else
                    {
                        // On the roof, its edge t away: the surface as it is, the cloth draped over it.
                        WallShift = 0f;
                        HoistOnEdge = false;
                        DrapeDistance = t;
                        _clearOX = ox; _clearOZ = oz; _clearShift = 0f; _clearEdge = false; _clearDrape = t;
                    }

                    return true;
                }
            }

            // A level hoist over a roof, further in than that: it stands on the roof, the surface kept as it is.
            WallShift = 0f;
            HoistOnEdge = false;
            _clearEdge = false;
            _clearDrape = 0f;
            if (!under)
            {
                _clearOk = true; _clearOX = ox; _clearOZ = oz; _clearShift = 0f;
                return true;
            }

            // Further in than that: under a roof, or inside the building.
            _clearOk = false; _clearOX = ox; _clearOZ = oz; _clearShift = 0f;
            return false;
        }

        /// <summary>
        /// A held particle of a level hoist hangs clear of the surface sampled under it (h, slopes
        /// sx, sz): the cloth's depth limit (<paramref name="hang"/>) free under it, and not over a
        /// wall's band of the grid (a slope steeper than <see cref="WallSlope"/>) its cloth would
        /// hang down into above its fly's reach -- over level ground, a lower roof, or the band's
        /// foot. The wall kept back stops as soon as that holds: the band then stands IN FRONT of
        /// the hoist, and the wind presses the cloth onto it there, never behind the hoist's line
        /// (kept back past the whole band, the preview's banner 0.15 m off a facade was pressed 0.1
        /// m into the building's drawn wall). Its cloth is laid out leaning off the band
        /// (<see cref="LayOutClear"/>).
        /// </summary>
        private bool HangsClear(int k, float h, float sx, float sz, float oy, float hang)
        {
            if (Y[k] < h + oy + hang)
                return false;

            return sx * sx + sz * sz < WallSlope * WallSlope || Y[k] - Length >= h + oy;
        }

        /// <summary>
        /// A held edge's place (where <see cref="PlaceHeld"/> puts it) lies under the surface by more
        /// than <see cref="HoistUnder"/>, sampled from the surface's point (<paramref name="ox"/>,
        /// <paramref name="oz"/>) <see cref="HoistClear"/> gave: the surface is left out, as it is
        /// over a hoist under it (2026-11-11). The hold is put back on its place every pass and
        /// the surface, last, pushed it out again: a 3 x 2 m flag whose fly end was held a metre
        /// inside a wall was buried, laid out afresh along its hold (the only way it lays) and
        /// buried again on every sub-step -- 2399-3599 times in 20 s, its links 2.7-3.5 times
        /// their length (the held-edge stress harness, "Cloth avoids buildings" on). Its places,
        /// not its particles: the surface has already lifted those. Asked again only when the
        /// surface, its point, the hoist or the holds change (a flag standing still: compares
        /// alone). True too while a buried cloth laid out afresh along its holds was still in the
        /// way there (<see cref="HoldsInTheWay"/>).
        /// </summary>
        private bool HoldsUnder(SurfaceGrid grid, float ox, float oz)
        {
            if (_heldCount == 0)
                return false;

            float oy = _surfaceY;
            if (_inWay && grid.MinX == _inWayMinX && grid.MinZ == _inWayMinZ && grid.Spacing == _inWaySpacing && grid.Columns == _inWayCols &&
                grid.Rows == _inWayRows && grid.Top == _inWayTop && ox == _inWayX && oy == _inWayY && oz == _inWayZ && _heldVersion == _inWayVersion &&
                _hx == _inWayHx && _hy == _inWayHy && _hz == _inWayHz && _holdFx == _inWayFx && _holdFy == _inWayFy && _holdFz == _inWayFz)
                return true;

            if (grid == _underGrid && ox == _underX && oy == _underY && oz == _underZ && _heldVersion == _underVersion &&
                _hx == _underHx && _hy == _underHy && _hz == _underHz && _holdFx == _underFx && _holdFy == _underFy && _holdFz == _underFz)
                return _underAnswer;

            _underGrid = grid; _underX = ox; _underY = oy; _underZ = oz; _underVersion = _heldVersion;
            _underHx = _hx; _underHy = _hy; _underHz = _hz; _underFx = _holdFx; _underFy = _holdFy; _underFz = _holdFz;
            _underAnswer = true;

            float h, sx, sz;
            for (int n = 0; n < _heldCount; n++)
            {
                float a = _heldAlong[n], d = _heldDown[n];
                float x = d * _hx + a * _holdFx, y = d * _hy + a * _holdFy, z = d * _hz + a * _holdFz;
                if (grid.Sample(x - ox, z - oz, out h, out sx, out sz) && y < h + oy - HoistUnder)
                    return true;
            }

            _underAnswer = false;
            return false;
        }

        /// <summary>
        /// A buried held cloth laid out afresh (<see cref="Step"/>) is still in the way: its holds
        /// clear, the sheet between them is not -- a 3 x 2 m flag held at its fly end with a 30 cm
        /// wall between hoist and hold was relaid on every sub-step (2399-3599 times in 20 s, its
        /// links 5.8 times their length), as it lays along its holds alone. The surface is left out
        /// as for a hold inside it (<see cref="HoldsUnder"/>) while the surface's place, layout and
        /// highest point, its point, the hoist and the holds stay as they are -- not the grid
        /// itself, which FlagSurroundings gathers afresh every 30 s: a flag standing still is laid
        /// out once (a building changed under it may lay it out once more), a dragged one as often
        /// as any flag carried into a building.
        /// </summary>
        private void HoldsInTheWay()
        {
            SurfaceGrid grid = _surface;
            _inWay = true;
            _inWayMinX = grid.MinX; _inWayMinZ = grid.MinZ; _inWaySpacing = grid.Spacing; _inWayCols = grid.Columns; _inWayRows = grid.Rows; _inWayTop = grid.Top;
            _inWayX = _effX; _inWayY = _surfaceY; _inWayZ = _effZ; _inWayVersion = _heldVersion;
            _inWayHx = _hx; _inWayHy = _hy; _inWayHz = _hz; _inWayFx = _holdFx; _inWayFy = _holdFy; _inWayFz = _holdFz;
        }

        /// <summary>Where HoldsInTheWay last found a held cloth's only layout in the way.</summary>
        private bool _inWay;
        private float _inWayMinX, _inWayMinZ, _inWaySpacing, _inWayTop, _inWayX, _inWayY, _inWayZ;
        private float _inWayHx, _inWayHy, _inWayHz, _inWayFx, _inWayFy, _inWayFz;
        private int _inWayCols, _inWayRows, _inWayVersion;

        /// <summary>HoldsUnder's last question and answer; the held particles' version (<see cref="BuildHeld"/>).</summary>
        private SurfaceGrid _underGrid;
        private float _underX, _underY, _underZ, _underHx, _underHy, _underHz, _underFx, _underFy, _underFz;
        private int _underVersion = -1, _heldVersion;
        private bool _underAnswer;

        /// <summary>The last step left the surface out for a held edge inside it, not the hoist (<see cref="SurfaceBlocked"/>, <see cref="HoldsUnder"/>): for the log.</summary>
        public bool SurfaceBlockedByHold
        {
            get { return _blockedByHold; }
        }

        private bool _blockedByHold;

        /// <summary>HoistClear's last question and answer.</summary>
        private SurfaceGrid _clearGrid;
        private float _clearX, _clearY, _clearZ, _clearTopX, _clearTopY, _clearTopZ, _clearBottomX, _clearBottomY, _clearBottomZ;
        private bool _clearOk, _clearEdge;
        private float _clearOX, _clearOZ, _clearShift;

        /// <summary>The way the last wall kept back went, level: away from the building (what a level hoist's cloth is laid leaning or draped towards).</summary>
        private float _clearUX, _clearUZ, _clearDrape;

        /// <summary>
        /// A level hoist standing on a roof near its edge (<see cref="HoistClear"/>): how far the
        /// edge is, metres, along the way it drapes (0: not so). The surface is kept as it is; a
        /// new or relaid cloth is laid over it (<see cref="LayOutClear"/>).
        /// </summary>
        public float DrapeDistance { get; private set; }

        /// <summary>The last step's <see cref="WallShift"/> was a level hoist's on a roof's edge (<see cref="LevelHoist"/>), not a pole's against a wall.</summary>
        public bool HoistOnEdge { get; private set; }

        /// <summary>
        /// How far the last step kept a wall back from the hoist standing in its slope, metres
        /// (<see cref="HoistClear"/>); 0: none needed, or the surface left out (<see cref="SurfaceBlocked"/>).
        /// </summary>
        public float WallShift { get; private set; }

        /// <summary>
        /// The free particles that begin this step inside the surface or a capsule in use
        /// (<see cref="ExitTolerance"/>): each is let out at most a dragged flag's step
        /// (<see cref="DragSpeed"/>, <see cref="MaxDragCells"/>) along the way out it would be put
        /// -- the surface's normal; deep in a wall's block, level towards its row's hoist point
        /// (Retreat's way); deep under a roof lower than the hoist, up the normal (Carry's); a
        /// capsule's, square to its axis, or deep in it towards the hoist's side -- and kept at
        /// least that far out every pass (<see cref="KeepExiting"/>); its links may pull it out
        /// faster. One deep in a wall's block that was not on its way out last step counts as
        /// buried (<see cref="BuriedShare"/>): laid or carried there whole, not swept into.
        /// </summary>
        private void FindExits(float dt)
        {
            if ((!_surfaceOn && _capCount == 0) || _laid)
            {
                if (_exit != null)
                    Array.Clear(_exit, 0, _exit.Length);
                return;
            }

            if (_exit == null)
            {
                _exit = new byte[Count];
                _exitAX = new float[Count]; _exitAY = new float[Count]; _exitAZ = new float[Count];
                _exitEX = new float[Count]; _exitEY = new float[Count]; _exitEZ = new float[Count];
            }

            float most = Math.Min(DragSpeed * dt, MaxDragCells * Math.Min(CellU, CellV));
            float gap = _surfaceMargin, depth = _depthLimit, tolerance = ExitTolerance * gap;
            SurfaceGrid grid = _surfaceOn ? _surface : null;
            float ox = _effX, oy = _surfaceY, oz = _effZ;

            for (int k = 0; k < Count; k++)
            {
                byte was = _exit[k];
                _exit[k] = 0;
                if (_invMass[k] == 0f)
                    continue;

                float x = X[k], y = Y[k], z = Z[k];
                float ex = 0f, ey = 0f, ez = 0f, far = 0f;
                byte kind = 0;

                float h, sx, sz;
                if (grid != null && grid.Sample(x - ox, z - oz, out h, out sx, out sz))
                {
                    h += oy;
                    float len = 1f / (float)Math.Sqrt(sx * sx + 1f + sz * sz);
                    float nx = -sx * len, ny = len, nz = -sz * len;
                    float push = gap - (y - h) * ny;
                    if (push > tolerance)
                    {
                        if (push <= depth + gap || h <= _hoistTop)
                        {
                            ex = nx; ey = ny; ez = nz; far = push;
                            kind = 1;
                        }
                        else
                        {
                            // Deep in a wall's block: left free, the surface out of its way (no way
                            // out is known from here). Pushed level towards its row's hoist point
                            // (Retreat's way) while its shallower neighbours went out through the
                            // face, the two pulled apart -- links 3.8 times long -- and whipped back
                            // 3-4 cells in a sub-step; free, the cloth outside draws it out.
                            kind = 2;
                            if (was != 2)
                                _heaped++;
                        }
                    }
                }

                for (int c = 0; c < _capCount && kind == 0; c++)
                {
                    float cx = _capX[c], cz = _capZ[c], reach = _capReach[c];
                    float dx = x - cx, dz = z - cz;
                    if (dx > reach || dx < -reach || dz > reach || dz < -reach)
                        continue;

                    float ay = y < _capBottom[c] ? _capBottom[c] : y > _capTop[c] ? _capTop[c] : y;
                    float dy = y - ay;
                    float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (reach - d <= tolerance)
                        continue;

                    if (reach - d > depth || d < 1e-6f)
                    {
                        // Deep in it: out level, on the side of its row's hoist point (KeepOffCapsules' carry).
                        int r = k / Columns * Columns;
                        float hx = X[r] - cx, hz = Z[r] - cz;
                        float hl = (float)Math.Sqrt(hx * hx + hz * hz);
                        if (hl < 1e-4f)
                            continue;

                        float tx = cx + hx / hl * reach - x, tz = cz + hz / hl * reach - z;
                        float tl = (float)Math.Sqrt(tx * tx + tz * tz);
                        if (tl < 1e-6f)
                            continue;

                        ex = tx / tl; ez = tz / tl; far = tl;
                    }
                    else
                    {
                        ex = dx / d; ey = dy / d; ez = dz / d; far = reach - d;
                    }

                    kind = 1;
                }

                if (kind == 0)
                    continue;

                if (kind == 2)
                {
                    // Free inside the block: A behind it along nothing (KeepExiting leaves it be).
                    _exit[k] = kind;
                    _exitEX[k] = 0f; _exitEY[k] = 0f; _exitEZ[k] = 0f;
                    _exitAX[k] = x; _exitAY[k] = y; _exitAZ[k] = z;
                    _exiting++;
                    continue;
                }

                float s = far < most ? far : most;
                _exit[k] = kind;
                _exitAX[k] = x + ex * s; _exitAY[k] = y + ey * s; _exitAZ[k] = z + ez * s;
                _exitEX[k] = ex; _exitEY[k] = ey; _exitEZ[k] = ez;
                _exiting++;
            }
        }

        /// <summary>Every pass, last: a particle on its way out of the city (<see cref="FindExits"/>) at least as far out as this step lets it.</summary>
        private void KeepExiting()
        {
            float[] px = _px, py = _py, pz = _pz;
            for (int k = 0; k < Count; k++)
            {
                if (_exit[k] == 0)
                    continue;

                float ex = _exitEX[k], ey = _exitEY[k], ez = _exitEZ[k];
                float s = (px[k] - _exitAX[k]) * ex + (py[k] - _exitAY[k]) * ey + (pz[k] - _exitAZ[k]) * ez;
                if (s < 0f)
                {
                    px[k] -= ex * s;
                    py[k] -= ey * s;
                    pz[k] -= ez * s;
                }
            }
        }

        /// <summary>
        /// The props' and trees' capsules in reach (PrepareColliders): a free particle inside one
        /// -- its radius and margin round its axis -- goes out square to the axis (or off its end),
        /// to its surface, and loses a little of its sliding along it. Inside every pass, after the
        /// pole, before the attachments.
        /// </summary>
        private void KeepOffCapsules()
        {
            float[] px = _px, py = _py, pz = _pz;
            float depth = _depthLimit;
            bool exiting = _exiting > 0;
            for (int c = 0; c < _capCount; c++)
            {
                float cx = _capX[c], cz = _capZ[c], bottom = _capBottom[c], top = _capTop[c], reach = _capReach[c];
                float reach2 = reach * reach;
                for (int k = 0; k < Count; k++)
                {
                    // On its way out of the city this step (KeepExiting).
                    if (_invMass[k] == 0f || (exiting && _exit[k] != 0))
                        continue;

                    float dx = px[k] - cx, dz = pz[k] - cz;
                    if (dx > reach || dx < -reach || dz > reach || dz < -reach)
                        continue;

                    float y = py[k];
                    float ay = y < bottom ? bottom : y > top ? top : y;
                    float dy = y - ay;
                    float d2 = dx * dx + dy * dy + dz * dz;
                    if (d2 >= reach2)
                        continue;

                    float d = (float)Math.Sqrt(d2);
                    if (reach - d > depth)
                    {
                        // Deep in it (carried there): carried out, level, on the side of its row's hoist point.
                        int h = k / Columns * Columns;
                        float hx = px[h] - cx, hz = pz[h] - cz;
                        float hl = (float)Math.Sqrt(hx * hx + hz * hz);
                        if (hl > 1e-4f)
                            Carry(k, cx + hx / hl * reach - px[k], 0f, cz + hz / hl * reach - pz[k]);
                        continue;
                    }

                    if (d < 1e-6f)
                    {
                        // On the axis: out the way it came.
                        dx = X[k] - cx; dy = 0f; dz = Z[k] - cz;
                        d = (float)Math.Sqrt(dx * dx + dz * dz);
                        if (d < 1e-6f)
                            continue;
                    }

                    float nx = dx / d, ny = dy / d, nz = dz / d;
                    float x1 = cx + nx * reach, y1 = ay + ny * reach, z1 = cz + nz * reach;
                    Slide(k, x1, y1, z1, nx, ny, nz);
                }
            }
        }

        /// <summary>
        /// The surface in reach (PrepareColliders): a free particle nearer the surface than the gap
        /// -- the distance to the surface's tangent plane under it, so a roof pushes up and a wall
        /// (a steep slope a node wide) sideways -- goes out along the plane's normal to the gap,
        /// and loses a little of its sliding along it; one found deep in it is carried out
        /// (<see cref="DepthCells"/>). Beyond the grid, the flat ground under the anchor, as
        /// before. Inside every pass, last.
        /// </summary>
        private void KeepAboveSurface()
        {
            SurfaceGrid grid = _surface;
            float[] heights = grid.Heights;
            int columns = grid.Columns, rows = grid.Rows;
            float inv = 1f / grid.Spacing;
            // The grid's first node, relative to the anchor (a wall kept back from the hoist: HoistClear).
            float ox = _effX + grid.MinX, oy = _surfaceY, oz = _effZ + grid.MinZ;
            float gap = _surfaceMargin, depth = _depthLimit, ground = _ground;
            float lastX = columns - 1, lastZ = rows - 1;
            float[] px = _px, py = _py, pz = _pz;
            bool exiting = _exiting > 0;

            for (int k = 0; k < Count; k++)
            {
                // On its way out of the city this step (KeepExiting).
                if (_invMass[k] == 0f || (exiting && _exit[k] != 0))
                    continue;

                float fx = (px[k] - ox) * inv, fz = (pz[k] - oz) * inv;
                if (fx < 0f || fz < 0f || fx > lastX || fz > lastZ)
                {
                    if (py[k] < ground)
                        py[k] = ground;
                    continue;
                }

                // Bilinear between the four nodes round it, and its slopes (written out: Mono inlines nothing).
                int c = (int)fx, r = (int)fz;
                if (c > columns - 2) c = columns - 2;
                if (r > rows - 2) r = rows - 2;
                float tx = fx - c, tz = fz - r;
                int n = r * columns + c;
                float h00 = heights[n], h10 = heights[n + 1], h01 = heights[n + columns], h11 = heights[n + columns + 1];
                float h = (h00 + (h10 - h00) * tx) * (1f - tz) + (h01 + (h11 - h01) * tx) * tz + oy;
                if (py[k] - h >= gap)
                    continue;

                float sx = ((h10 - h00) * (1f - tz) + (h11 - h01) * tz) * inv;
                float sz = ((h01 - h00) * (1f - tx) + (h11 - h10) * tx) * inv;
                float len = 1f / (float)Math.Sqrt(sx * sx + 1f + sz * sz);
                float nx = -sx * len, ny = len, nz = -sz * len;

                // The distance to the tangent plane through the surface under it.
                float d = (py[k] - h) * ny;
                float push = gap - d;
                if (push <= 0f)
                    continue;

                if (push <= depth + gap)
                    Slide(k, px[k] + nx * push, py[k] + ny * push, pz[k] + nz * push, nx, ny, nz);
                else if (h <= _hoistTop)
                    Carry(k, nx * push, ny * push, nz * push);
                else
                    Retreat(k, grid, _effX, oy, _effZ);
            }
        }

        /// <summary>
        /// A particle found deep in the surface where it stands higher than the hoist's top (in a
        /// wall's block): carried back, level, towards its row's hoist point, to the first place
        /// the surface lets it be at its height. If none (the hoist's own place is under it too),
        /// it stays.
        /// </summary>
        private void Retreat(int k, SurfaceGrid grid, float ox, float oy, float oz)
        {
            _heaped++;
            int h = k / Columns * Columns;
            float x = _px[k], y = _py[k], z = _pz[k];
            float dx = _px[h] - x, dz = _pz[h] - z;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            if (dist < 1e-4f)
                return;

            int steps = Math.Min(64, (int)Math.Ceiling(dist / (0.5f * grid.Spacing)));
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                float qx = x + dx * t, qz = z + dz * t;
                float s, sx, sz;
                if (!grid.Sample(qx - ox, qz - oz, out s, out sx, out sz) || s + oy + _surfaceMargin <= y)
                {
                    Carry(k, qx - x, 0f, qz - z);
                    return;
                }
            }
        }

        /// <summary>A particle moved as a whole, where its step began too: out of where it cannot be, with no speed from the move.</summary>
        private void Carry(int k, float dx, float dy, float dz)
        {
            _px[k] += dx; _py[k] += dy; _pz[k] += dz;
            X[k] += dx; Y[k] += dy; Z[k] += dz;
        }

        /// <summary>
        /// A particle put out of the city at (x, y, z) against a surface of normal n: there, less
        /// <see cref="ContactFriction"/> of its sliding along the surface since the step began.
        /// </summary>
        private void Slide(int k, float x, float y, float z, float nx, float ny, float nz)
        {
            float mx = x - X[k], my = y - Y[k], mz = z - Z[k];
            float along = mx * nx + my * ny + mz * nz;
            _px[k] = x - (mx - nx * along) * ContactFriction;
            _py[k] = y - (my - ny * along) * ContactFriction;
            _pz[k] = z - (mz - nz * along) * ContactFriction;
        }

        /// <summary>
        /// True while every normal is within <see cref="FlatCone"/> of the cloth's mean normal: it
        /// cannot fold onto itself, and the collisions are skipped (Volino and Magnenat-Thalmann
        /// 1994).
        /// </summary>
        private bool Flat()
        {
            int n = Count;
            float mx = 0f, my = 0f, mz = 0f;
            for (int k = 0; k < n; k++)
            {
                mx += Nx[k]; my += Ny[k]; mz += Nz[k];
            }

            float len = (float)Math.Sqrt(mx * mx + my * my + mz * mz);
            if (len < 1e-6f)
                return false;

            float limit = FlatCone * len;
            for (int k = 0; k < n; k++)
            {
                if (Nx[k] * mx + Ny[k] * my + Nz[k] * mz < limit && !(_shaped && _out[k]))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// The neighbour list: every pair of particles within <see cref="CollisionReach"/>
        /// thicknesses of each other, bar grid neighbours (and two pinned ones), found through a
        /// spatial hash of the predicted positions -- cells as big as the reach, hashed with
        /// Teschner, Heidelberger, Mueller, Pomeranets and Gross's primes (2003, "Optimized Spatial
        /// Hashing for Collision Detection of Deformable Objects") into a table twice the
        /// particles, a counting sort -- and kept while no particle has moved half the reach's
        /// margin since (Verlet 1967): no pair missing from it can then have come within a
        /// thickness. Written out (Mono inlines nothing).
        /// </summary>
        private void ListPairs(float thick)
        {
            int n = Count;
            if (_hashStart == null)
            {
                int size0 = 1;
                while (size0 < 2 * n)
                    size0 <<= 1;
                _hashStart = new int[size0 + 1];
                _hashEntry = new int[n];
                _hashOf = new int[n];
                _cellX = new int[n]; _cellY = new int[n]; _cellZ = new int[n];
                _listX = new float[n]; _listY = new float[n]; _listZ = new float[n];
                _pairA = new int[2 * n];
                _pairB = new int[2 * n];
            }

            float[] px = _px, py = _py, pz = _pz;
            float reach = thick * CollisionReach;

            if (_listed)
            {
                float half = 0.5f * (reach - thick), half2 = half * half;
                bool fresh = true;
                for (int k = 0; k < n; k++)
                {
                    float ex = px[k] - _listX[k], ey = py[k] - _listY[k], ez = pz[k] - _listZ[k];
                    if (ex * ex + ey * ey + ez * ez > half2)
                    {
                        fresh = false;
                        break;
                    }
                }

                if (fresh)
                    return;
            }

            _listed = true;
            float inv = 1f / reach;
            int size = _hashStart.Length - 1, mask = size - 1;
            int[] start = _hashStart, entry = _hashEntry, cx = _cellX, cy = _cellY, cz = _cellZ;
            Array.Clear(start, 0, start.Length);

            for (int k = 0; k < n; k++)
            {
                // Math.Floor written out (a call to the runtime under Mono): the same double the
                // call was handed, truncated, one less below zero.
                double fx = px[k] * inv, fy = py[k] * inv, fz = pz[k] * inv;
                int x = (int)fx, y = (int)fy, z = (int)fz;
                if (fx < x) x--;
                if (fy < y) y--;
                if (fz < z) z--;
                cx[k] = x; cy[k] = y; cz[k] = z;
                int h = ((x * 73856093) ^ (y * 19349663) ^ (z * 83492791)) & mask;
                _hashOf[k] = h;
                start[h]++;
                _listX[k] = px[k]; _listY[k] = py[k]; _listZ[k] = pz[k];
            }

            int sum = 0;
            for (int h = 0; h < size; h++)
            {
                sum += start[h];
                start[h] = sum;
            }

            start[size] = sum;
            for (int k = 0; k < n; k++)
                entry[--start[_hashOf[k]]] = k;

            // The fields this loop reads, as locals, and the grid-neighbour test as two unsigned
            // compares: with the floor written out above, 3.5% of a waving cloth's step in the
            // game's Mono (measured together, 2026-11-14), the same pairs bit for bit.
            int[] col = _col, row = _row, pairA = _pairA, pairB = _pairB;
            float[] invMass = _invMass;
            bool[] gone = _out;
            bool shaped = _shaped;
            int pairs = 0;
            float reach2 = reach * reach;
            for (int a = 0; a < n; a++)
            {
                // Not cloth (a cut): nothing to collide.
                if (shaped && gone[a])
                    continue;

                int ax = cx[a], ay = cy[a], az = cz[a];
                int ia = col[a] - 1, ja = row[a] - 1;
                bool pinned = invMass[a] == 0f;
                float x = px[a], y = py[a], z = pz[a];

                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int hyz = ((ay + dy) * 19349663) ^ ((az + dz) * 83492791);
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int h = (((ax + dx) * 73856093) ^ hyz) & mask;
                            for (int e = start[h], end = start[h + 1]; e < end; e++)
                            {
                                int b = entry[e];
                                if (b <= a)
                                    continue;

                                // A grid neighbour (a column and a row within one of a's): one test each.
                                if ((uint)(col[b] - ia) <= 2u && (uint)(row[b] - ja) <= 2u)
                                    continue;
                                if (pinned && invMass[b] == 0f)
                                    continue;
                                if (shaped && gone[b])
                                    continue;

                                float ex = px[b] - x, ey = py[b] - y, ez = pz[b] - z;
                                if (ex * ex + ey * ey + ez * ez >= reach2)
                                    continue;

                                if (pairs == pairA.Length)
                                {
                                    Array.Resize(ref _pairA, pairs * 2);
                                    Array.Resize(ref _pairB, pairs * 2);
                                    pairA = _pairA;
                                    pairB = _pairB;
                                }

                                pairA[pairs] = a;
                                pairB[pairs] = b;
                                pairs++;
                            }
                        }
                    }
                }
            }

            _pairs = pairs;
        }

        /// <summary>
        /// One pass over the neighbour list: two particles nearer than a thickness are pushed
        /// apart along the line between them, each by its share (a pinned one does not move), and
        /// lose <see cref="CollisionFriction"/> of their sliding past each other since the step
        /// began. Inside every pass, with the links (Gauss-Seidel, as every constraint).
        /// </summary>
        private void Collide(float thick)
        {
            float[] px = _px, py = _py, pz = _pz, x = X, y = Y, z = Z;
            float thick2 = thick * thick;
            for (int c = 0; c < _pairs; c++)
            {
                int a = _pairA[c], b = _pairB[c];
                float dx = px[a] - px[b], dy = py[a] - py[b], dz = pz[a] - pz[b];
                float d2 = dx * dx + dy * dy + dz * dz;
                if (d2 >= thick2 || d2 < 1e-12f)
                    continue;

                float ma = _invMass[a], mb = _invMass[b], w = 1f / (ma + mb);
                ma *= w;
                mb *= w;
                float d = (float)Math.Sqrt(d2);
                float s = (thick - d) / d;
                px[a] += dx * s * ma; py[a] += dy * s * ma; pz[a] += dz * s * ma;
                px[b] -= dx * s * mb; py[b] -= dy * s * mb; pz[b] -= dz * s * mb;

                // Friction: the part of their motion this step that slides along the contact.
                float inv = 1f / d;
                float nx = dx * inv, ny = dy * inv, nz = dz * inv;
                float rx = (px[a] - x[a]) - (px[b] - x[b]);
                float ry = (py[a] - y[a]) - (py[b] - y[b]);
                float rz = (pz[a] - z[a]) - (pz[b] - z[b]);
                float rn = rx * nx + ry * ny + rz * nz;
                rx = (rx - rn * nx) * CollisionFriction;
                ry = (ry - rn * ny) * CollisionFriction;
                rz = (rz - rn * nz) * CollisionFriction;
                px[a] -= rx * ma; py[a] -= ry * ma; pz[a] -= rz * ma;
                px[b] += rx * mb; py[b] += ry * mb; pz[b] += rz * mb;
            }
        }

        /// <summary>
        /// Each particle's share of the added mass down its span. The slender-body force on a
        /// strip across the flow is the cross-flow of a flat plate of the span's width H: its
        /// potential on the plate is +-V sqrt((H/2)^2 - z^2) (Newman 1977, "Marine Hydrodynamics",
        /// 4.13), so the air's pressure on it, and the mass of air it carries, go as that ellipse --
        /// the most in the middle, none at a free edge -- and sum to the strip's rho pi H^2 / 4, the
        /// added mass the reactive term already uses (Lighthill 1960). Evened out, the edges were
        /// driven as hard as the middle and every row moved alike; on the ellipse the top and bottom
        /// edges are lighter and freer than the middle. Each particle takes the ellipse's mean over
        /// its own strip of the span (half a cell either way, clipped at the ends: an edge particle
        /// keeps a little), scaled so the run's particles average 1 -- the total is unchanged. A
        /// cut cloth's runs are its own spans down each column (<see cref="PrepareCut"/>).
        /// </summary>
        private void PrepareSpanShares()
        {
            int cols = Columns, rows = Rows;
            _spanShare = new float[Count];
            for (int k = 0; k < Count; k++)
                _spanShare[k] = 1f;

            if (SpanwiseAir <= 0)
                return;

            for (int i = 0; i < cols; i++)
            {
                int j = 0;
                while (j < rows)
                {
                    if (_shaped && _out[j * cols + i])
                    {
                        j++;
                        continue;
                    }

                    int start = j;
                    while (j < rows && !(_shaped && _out[j * cols + i]))
                        j++;

                    // The run start .. j - 1: its strips on x in [-1, 1] across the span.
                    int count = j - start;
                    if (count < 3)
                        continue;

                    float cells = count - 1;
                    double sum = 0.0;
                    for (int r = start; r < j; r++)
                    {
                        double a = Math.Max(-1.0, 2.0 * (r - start - 0.5) / cells - 1.0);
                        double b = Math.Min(1.0, 2.0 * (r - start + 0.5) / cells - 1.0);
                        double mean = (EllipseArea(b) - EllipseArea(a)) / (b - a);
                        _spanShare[r * cols + i] = (float)mean;
                        sum += mean;
                    }

                    float scale = (float)(count / sum);
                    for (int r = start; r < j; r++)
                        _spanShare[r * cols + i] *= scale;
                }
            }
        }

        /// <summary>The integral of sqrt(1 - x^2) from 0 to x.</summary>
        private static double EllipseArea(double x)
        {
            return 0.5 * (x * Math.Sqrt(Math.Max(0.0, 1.0 - x * x)) + Math.Asin(x));
        }

        /// <summary>A particle's share of its span's mean added mass (<see cref="PrepareSpanShares"/>), for the tests.</summary>
        public float SpanShare(int i, int j)
        {
            return _spanShare[j * Columns + i];
        }

        /// <summary>
        /// A cut cloth's particles (<see cref="Cut"/>): out where no cell round them is cloth; the
        /// span of cloth down each column through each particle (a tail of a swallowtail is its own
        /// span, a pennant's narrows to its point: Lighthill's added mass is the span's); and the
        /// order the out particles are filled in, outwards from the cloth a ring at a time, each from
        /// the neighbours already known: carried on in a straight line from two (2 a - b) where it
        /// can be, else beside the one there is.
        /// </summary>
        private void PrepareCut()
        {
            int cols = Columns, rows = Rows, cu = cols - 1;
            _out = new bool[Count];
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    bool cloth = (i > 0 && j > 0 && _cells[(j - 1) * cu + i - 1]) || (i < cu && j > 0 && _cells[(j - 1) * cu + i]) ||
                                 (i > 0 && j < rows - 1 && _cells[j * cu + i - 1]) || (i < cu && j < rows - 1 && _cells[j * cu + i]);
                    int k = j * cols + i;
                    _out[k] = !cloth;
                    if (!cloth)
                        _invMass[k] = 0f;
                }
            }

            // The span down each column: the run of cloth particles through each one.
            _span = new float[Count];
            for (int i = 0; i < cols; i++)
            {
                int j = 0;
                while (j < rows)
                {
                    if (_out[j * cols + i])
                    {
                        j++;
                        continue;
                    }

                    int start = j;
                    while (j < rows && !_out[j * cols + i])
                        j++;

                    float span = Math.Max(1, j - 1 - start) * CellV;
                    for (int r = start; r < j; r++)
                        _span[r * cols + i] = span;
                }
            }

            // The fill: rings out from the cloth.
            var known = new bool[Count];
            for (int k = 0; k < Count; k++)
                known[k] = !_out[k];

            var order = new System.Collections.Generic.List<int>();
            var starts = new System.Collections.Generic.List<int>();
            var froms = new System.Collections.Generic.List<int>();
            var beyonds = new System.Collections.Generic.List<int>();
            var ring = new System.Collections.Generic.List<int>();
            int[] di = { 1, -1, 0, 0 }, dj = { 0, 0, 1, -1 };
            while (true)
            {
                ring.Clear();
                for (int k = 0; k < Count; k++)
                {
                    if (known[k])
                        continue;

                    int i = k % cols, j = k / cols;
                    for (int d = 0; d < 4; d++)
                    {
                        int ni = i + di[d], nj = j + dj[d];
                        if (ni >= 0 && ni < cols && nj >= 0 && nj < rows && known[nj * cols + ni])
                        {
                            ring.Add(k);
                            break;
                        }
                    }
                }

                if (ring.Count == 0)
                    break;

                foreach (int k in ring)
                {
                    int i = k % cols, j = k / cols;
                    starts.Add(froms.Count);
                    order.Add(k);

                    // Carried on from two in a line where it can be.
                    int lines = 0;
                    for (int d = 0; d < 4; d++)
                    {
                        int ai = i + di[d], aj = j + dj[d], bi = i + 2 * di[d], bj = j + 2 * dj[d];
                        if (ai < 0 || ai >= cols || aj < 0 || aj >= rows || !known[aj * cols + ai])
                            continue;
                        if (bi < 0 || bi >= cols || bj < 0 || bj >= rows || !known[bj * cols + bi])
                            continue;

                        froms.Add(aj * cols + ai);
                        beyonds.Add(bj * cols + bi);
                        lines++;
                    }

                    if (lines > 0)
                        continue;

                    for (int d = 0; d < 4; d++)
                    {
                        int ai = i + di[d], aj = j + dj[d];
                        if (ai >= 0 && ai < cols && aj >= 0 && aj < rows && known[aj * cols + ai])
                        {
                            froms.Add(aj * cols + ai);
                            beyonds.Add(-1);
                        }
                    }
                }

                // A ring known only once all of it is placed: each is filled from the ring inside it.
                foreach (int k in ring)
                    known[k] = true;
            }

            starts.Add(froms.Count);
            _fillOrder = order.ToArray();
            _fillStart = starts.ToArray();
            _fillA = froms.ToArray();
            _fillB = beyonds.ToArray();
        }

        /// <summary>
        /// The out particles of a cut cloth put where the cloth beside them carries on to
        /// (<see cref="PrepareCut"/>): positions only, the given arrays; nothing uncut.
        /// </summary>
        public void FillCut(float[] x, float[] y, float[] z)
        {
            if (_fillOrder == null)
                return;

            int[] order = _fillOrder, start = _fillStart, fa = _fillA, fb = _fillB;
            for (int o = 0; o < order.Length; o++)
            {
                int k = order[o];
                float sx = 0f, sy = 0f, sz = 0f;
                int count = start[o + 1] - start[o];
                for (int e = start[o]; e < start[o + 1]; e++)
                {
                    int a = fa[e], b = fb[e];
                    if (b >= 0)
                    {
                        sx += 2f * x[a] - x[b]; sy += 2f * y[a] - y[b]; sz += 2f * z[a] - z[b];
                    }
                    else
                    {
                        sx += x[a]; sy += y[a]; sz += z[a];
                    }
                }

                if (count > 0)
                {
                    float inv = 1f / count;
                    x[k] = sx * inv; y[k] = sy * inv; z[k] = sz * inv;
                }
            }
        }

        /// <summary>A cut cloth's out particles filled (<see cref="FillCut"/>), where they were before too, and still.</summary>
        private void FillCutStill()
        {
            if (_fillOrder == null)
                return;

            FillCut(X, Y, Z);
            for (int o = 0; o < _fillOrder.Length; o++)
            {
                int k = _fillOrder[o];
                PrevX[k] = X[k]; PrevY[k] = Y[k]; PrevZ[k] = Z[k];
                Vx[k] = 0f; Vy[k] = 0f; Vz[k] = 0f;
            }
        }

        private void BuildLinks()
        {
            if (_shaped)
                BuildCutLinks();
            else
                BuildGridLinks();

            if (_heldCount > 0)
                MarkNearHold();
        }

        /// <summary>
        /// The links the knot test leaves out of a cloth with held edges (<see cref="WorstFreeStretch"/>):
        /// those with an end on a held particle or beside one (its eight neighbours).
        /// </summary>
        private bool[] _nearHold;

        /// <summary>
        /// Marks <see cref="_nearHold"/> (2026-11-11). Where a held edge stops beside a free one --
        /// a held corner's arm, a held top edge meeting the free fly -- a membrane in the wind
        /// carries its pressure into that point (the stress at the end of a held boundary is
        /// singular), and the links there stay over KnotStretch with no knot at all, worse the
        /// finer the cloth: at 32 cells, held at its far corners or top and bottom in 12-30 m/s,
        /// 1.4-1.7 for a third to a half of the time within a cell of a hold, the rest of the cloth
        /// over 1.3 at most 0.4 s straight (to 1.35) in 150 s (the held-edge stress harness,
        /// 2026-11-11); laid out afresh as knotted every 2-20 s, a visible reset. As links round a
        /// post are (OnPost), they are left out; a knot shows in the rest of the cloth (not shown
        /// offline: a knot made by hand in a held cloth was pulled out by its holds first). NOT
        /// enough for a tongued or swallowtail cut held at its corners: the notch-side edge of each
        /// tail pulls into the corner's last held particle, 1.4-1.7 further in than a cell (in 120 s
        /// at 32 cells: tongued 8 knots at 12 m/s and 15 at 18; swallowtail none and 4).
        /// </summary>
        private void MarkNearHold()
        {
            if (_nearHold == null || _nearHold.Length < _a.Length)
                _nearHold = new bool[_a.Length];

            bool[] near = new bool[Count];
            for (int n = 0; n < _heldCount; n++)
            {
                int k = _heldK[n], i = k % Columns, j = k / Columns;
                for (int dj = -1; dj <= 1; dj++)
                {
                    for (int di = -1; di <= 1; di++)
                    {
                        int ii = i + di, jj = j + dj;
                        if (ii >= 0 && ii < Columns && jj >= 0 && jj < Rows)
                            near[jj * Columns + ii] = true;
                    }
                }
            }

            for (int c = 0; c < _links; c++)
                _nearHold[c] = near[_a[c]] || near[_b[c]];
        }

        private void BuildGridLinks()
        {
            _links = 0;

            // Structural first, from the hoist outwards: tension reaches the fly in one pass.
            for (int i = 0; i < Columns - 1; i++)
                for (int j = 0; j < Rows; j++)
                    Link(j * Columns + i, j * Columns + i + 1, Structural);

            for (int j = 0; j < Rows - 1; j++)
                for (int i = 0; i < Columns; i++)
                    Link(j * Columns + i, (j + 1) * Columns + i, Structural);

            for (int j = 0; j < Rows - 1; j++)
            {
                for (int i = 0; i < Columns - 1; i++)
                {
                    Link(j * Columns + i, (j + 1) * Columns + i + 1, ShearLink);
                    Link(j * Columns + i + 1, (j + 1) * Columns + i, ShearLink);
                }
            }

            for (int j = 0; j < Rows; j++)
                for (int i = 0; i < Columns - 2; i++)
                    Link(j * Columns + i, j * Columns + i + 2, BendLink);

            for (int j = 0; j < Rows - 2; j++)
                for (int i = 0; i < Columns; i++)
                    Link(j * Columns + i, (j + 2) * Columns + i, i == 0 ? HeadingLink : BendLink);
        }

        /// <summary>
        /// A cut cloth's links (<see cref="Cut"/>), in <see cref="BuildLinks"/>'s order: the edges and
        /// diagonals of its cloth cells, and the two-apart links along a row or a column of them. No
        /// link reaches an out particle, so nothing joins two tails past the cells between them.
        /// </summary>
        private void BuildCutLinks()
        {
            _links = 0;
            int cols = Columns, rows = Rows;

            for (int i = 0; i < cols - 1; i++)
                for (int j = 0; j < rows; j++)
                    if (AlongEdge(i, j))
                        Link(j * cols + i, j * cols + i + 1, Structural);

            for (int j = 0; j < rows - 1; j++)
                for (int i = 0; i < cols; i++)
                    if (DownEdge(i, j))
                        Link(j * cols + i, (j + 1) * cols + i, Structural);

            for (int j = 0; j < rows - 1; j++)
            {
                for (int i = 0; i < cols - 1; i++)
                {
                    if (!IsClothCell(i, j))
                        continue;

                    Link(j * cols + i, (j + 1) * cols + i + 1, ShearLink);
                    Link(j * cols + i + 1, (j + 1) * cols + i, ShearLink);
                }
            }

            for (int j = 0; j < rows; j++)
                for (int i = 0; i < cols - 2; i++)
                    if (AlongEdge(i, j) && AlongEdge(i + 1, j))
                        Link(j * cols + i, j * cols + i + 2, BendLink);

            for (int j = 0; j < rows - 2; j++)
                for (int i = 0; i < cols; i++)
                    if (DownEdge(i, j) && DownEdge(i, j + 1))
                        Link(j * cols + i, (j + 2) * cols + i, i == 0 ? HeadingLink : BendLink);
        }

        /// <summary>The edge from (i, j) to (i + 1, j) is a cloth cell's.</summary>
        private bool AlongEdge(int i, int j)
        {
            return (j > 0 && IsClothCell(i, j - 1)) || (j < Rows - 1 && IsClothCell(i, j));
        }

        /// <summary>The edge from (i, j) to (i, j + 1) is a cloth cell's.</summary>
        private bool DownEdge(int i, int j)
        {
            return (i > 0 && IsClothCell(i - 1, j)) || (i < Columns - 1 && IsClothCell(i, j));
        }

        private void Link(int a, int b, byte kind)
        {
            // Two pinned particles: nothing to hold.
            if (_invMass[a] == 0f && _invMass[b] == 0f)
                return;

            // A held particle (SetHolds) weighs as a pinned one; two held, nothing to hold.
            float ma = _invMass[a], mb = _invMass[b];
            if (_heldCount > 0)
            {
                if (_heldMark[a]) ma = 0f;
                if (_heldMark[b]) mb = 0f;
                if (ma == 0f && mb == 0f)
                    return;
            }

            int ia = a % Columns, ja = a / Columns;
            int ib = b % Columns, jb = b / Columns;
            float du = (ib - ia) * CellU, dv = (jb - ja) * CellV;

            float w = ma + mb;
            _a[_links] = a;
            _b[_links] = b;
            _rest[_links] = (float)Math.Sqrt(du * du + dv * dv);
            _kind[_links] = kind;
            _wa[_links] = ma / w;
            _wb[_links] = mb / w;
            _links++;
        }

        /// <summary>How many links hold the cloth together (for the tests).</summary>
        public int LinkCount
        {
            get { return _links; }
        }

        /// <summary>
        /// The worst stretch of any structural link, as a ratio to its rest length (1 = none):
        /// what the tests hold the attachments to.
        /// </summary>
        public float MaxStretch()
        {
            float worst = 1f;
            for (int c = 0; c < _links; c++)
            {
                if (_kind[c] != Structural)
                    continue;

                int a = _a[c], b = _b[c];
                float dx = X[a] - X[b], dy = Y[a] - Y[b], dz = Z[a] - Z[b];
                float r = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz) / _rest[c];
                if (r > worst)
                    worst = r;
            }

            return worst;
        }

        /// <summary>False once any position is not a finite number (the owner lays it out again).</summary>
        public bool IsFinite()
        {
            for (int k = 0; k < Count; k++)
            {
                if (float.IsNaN(X[k]) || float.IsInfinity(X[k]) ||
                    float.IsNaN(Y[k]) || float.IsInfinity(Y[k]) ||
                    float.IsNaN(Z[k]) || float.IsInfinity(Z[k]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Unit normals of a grid of positions, from central differences (one-sided at the
        /// edges): n = (p[i+1] - p[i-1]) x (p[j+1] - p[j-1]). Unity's winding makes this the
        /// FRONT side of the mesh (FlagMesh's triangles), which the shader flips for the back.
        /// </summary>
        public void ComputeNormals(float[] x, float[] y, float[] z, float[] nx, float[] ny, float[] nz)
        {
            int cols = Columns, rows = Rows;
            for (int j = 0; j < rows; j++)
            {
                int j0 = j > 0 ? j - 1 : j, j1 = j < rows - 1 ? j + 1 : j;
                for (int i = 0; i < cols; i++)
                {
                    int i0 = i > 0 ? i - 1 : i, i1 = i < cols - 1 ? i + 1 : i;
                    int a = j * cols + i0, b = j * cols + i1;
                    int c = j0 * cols + i, d = j1 * cols + i;

                    float tx = x[b] - x[a], ty = y[b] - y[a], tz = z[b] - z[a];
                    float bx = x[d] - x[c], by = y[d] - y[c], bz = z[d] - z[c];

                    float cx = ty * bz - tz * by;
                    float cy = tz * bx - tx * bz;
                    float cz = tx * by - ty * bx;
                    float len = (float)Math.Sqrt(cx * cx + cy * cy + cz * cz);

                    int k = j * cols + i;
                    if (len > 1e-12f)
                    {
                        float inv = 1f / len;
                        nx[k] = cx * inv; ny[k] = cy * inv; nz[k] = cz * inv;
                    }
                    else
                    {
                        nx[k] = 0f; ny[k] = 0f; nz[k] = 1f;
                    }
                }
            }
        }
    }
}
