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
    /// The cloth does not go through itself: its particles are spheres a cell wide that the
    /// passes keep apart like the links (<see cref="CollisionThickness"/>), found through a
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
        /// The details a cloth can have, cells along its longer side: Normal, High, Very high,
        /// Ultra (the option's choice is the finest; the renderer gives each flag what its size on
        /// the screen needs). Measured on a 3 x 2 m flag against Normal (20 s each, the scratchpad's
        /// bench and tools/test-cloth.ps1): the same flight at 6 m/s -- the fly's reach, the
        /// waves' speed within a tenth, the fly's flapping within a fifth -- while in calm air the
        /// finer cloth hangs straighter (82 deg of droop at Normal, 85 at High, 87 at Very high, 88
        /// at Ultra), as real cloth does. The cost (the GPU preview's Unity 5.6 Mono, a 1/60 s step
        /// with its mesh): 0.4 ms at Normal, x2 at High, x4 at Very high in calm air and x8 at
        /// 6 m/s (its 9 cm cells take two sub-steps there), x7 and x13 at Ultra. Flags over 3 m
        /// keep one sub-step to stronger winds (their cells are longer), so they cost less for the
        /// same detail.
        /// </summary>
        public static readonly int[] Details = { 18, 24, 32, 40 };

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
        /// cell, in none. A folded flag's layers stay a cell apart: 17 cm on a 3 m flag at Normal,
        /// 9 cm at Very high.
        /// </summary>
        public const float CollisionThickness = 1.0f;

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

        public readonly int Columns;
        public readonly int Rows;
        public readonly int Count;

        /// <summary>The detail it was made at (cells along the longer side, one of <see cref="Details"/>).</summary>
        public readonly int Detail;

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
        /// particle (0, j) at j * CellV along it.
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

        /// <summary>The pole the cloth keeps off (SetPole): its axis through a point, along the hoist; its radius there and its growth per metre down; its extent down the axis; the margin kept off it.</summary>
        private bool _poleOn;
        private float _poleX, _poleY, _poleZ, _poleRadius, _poleGrowth, _poleTop, _poleBottom, _poleMargin;

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

        private const byte Structural = 0, ShearLink = 1, BendLink = 2;

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
        {
            SetHoistDirection(hoistX, hoistY, hoistZ);
            Length = Math.Max(length, 0.1f);
            Height = Math.Max(height, 0.1f);
            Detail = ClampDetail(detail);
            GridFor(Length, Height, Detail, out Columns, out Rows);
            Count = Columns * Rows;
            CellU = Length / (Columns - 1);
            CellV = Height / (Rows - 1);

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

            int max = 2 * Count * 6;
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
        /// The passes a step of this cloth takes: the fabric's, one more at Very high, two more at
        /// Ultra -- a longer chain of links takes more passes to hold together. Measured (3 x 2 m,
        /// 20 s, the worst link at 6 and 12 m/s): Ultra 9.8% and 16.3% with five, 8.5% and 12.3%
        /// with seven; Very high 7.8% and 11.2% with five. The stiffnesses are per step, so the
        /// cloth folds the same (Mueller et al.'s per-pass formula).
        /// </summary>
        public int Passes
        {
            get { return Math.Max(1, _fabric.Iterations) + Math.Max(0, (Detail - CellsAlong) / 10); }
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
            float hx = _hx, hy = _hy, hz = _hz;

            // The given direction, less its part along the hoist.
            float along = dirX * hx + dirZ * hz;
            float fx = dirX - along * hx, fy = -along * hy, fz = dirZ - along * hz;
            float len = (float)Math.Sqrt(fx * fx + fy * fy + fz * fz);
            if (len < 1e-4f)
            {
                // Along the hoist (or none): any way square to it, level if it can be.
                fx = -hz; fy = 0f; fz = hx;
                len = (float)Math.Sqrt(fx * fx + fz * fz);
                if (len < 1e-4f)
                {
                    fx = 1f; fz = 0f; len = 1f;
                }
            }

            fx /= len; fy /= len; fz /= len;

            // Across the sheet: hoist x fly (for a straight-down hoist, (-dirZ, 0, dirX)).
            float wx = hy * fz - hz * fy, wy = hz * fx - hx * fz, wz = hx * fy - hy * fx;

            for (int j = 0; j < Rows; j++)
            {
                float down = j * CellV;
                for (int i = 0; i < Columns; i++)
                {
                    int k = j * Columns + i;
                    float u = i * CellU;
                    float wobble = i == 0 ? 0f : 0.002f * Length * FlagNoise.Value1(k * 0.37f, 5);
                    X[k] = down * hx + u * fx + wobble * wx;
                    Y[k] = down * hy + u * fy + wobble * wy;
                    Z[k] = down * hz + u * fz + wobble * wz;
                    PrevX[k] = X[k]; PrevY[k] = Y[k]; PrevZ[k] = Z[k];
                    Vx[k] = 0f; Vy[k] = 0f; Vz[k] = 0f;
                }
            }

            ComputeNormals(X, Y, Z, Nx, Ny, Nz);
            _primed = false;
            _listed = false;
        }

        /// <summary>
        /// The hoist leans another way (the pole was tilted: the panel, the gizmo, a paste): the
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
            }

            _hx = x; _hy = y; _hz = z;
            for (int j = 0; j < Rows; j++)
            {
                int k = j * Columns;
                float down = j * CellV;
                X[k] = down * x; Y[k] = down * y; Z[k] = down * z;
                PrevX[k] = X[k]; PrevY[k] = Y[k]; PrevZ[k] = Z[k];
                Vx[k] = 0f; Vy[k] = 0f; Vz[k] = 0f;
            }

            ComputeNormals(X, Y, Z, Nx, Ny, Nz);
            _primed = false;
            _listed = false;
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

            // The same flag: it hangs from the same line.
            float hx = old._hx, hy = old._hy, hz = old._hz;
            _hx = hx; _hy = hy; _hz = hz;

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

                    if (i == 0)
                    {
                        X[k] = hoistX; Y[k] = hoistY; Z[k] = hoistZ;
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

            ComputeNormals(X, Y, Z, Nx, Ny, Nz);
            _primed = false;
            _listed = false;
        }

        /// <summary>
        /// The most of one move of the anchor that drags the cloth, in cells: the rest carries it
        /// along as it is. Measured (an 8.1 m flag at 41x28 hanging, its height changed 10 m in
        /// ten frames; a 3 m flag at Ultra, 10 m in twenty): dragged whole, the hoist went 1 m a
        /// frame through its own hanging cloth and self-collision locked the layers on the wrong
        /// sides -- 42-70 pairs of triangles still crossing 10 s later, 55-77% stretch, "the only
        /// way to untie it is to move it around"; half a cell dragged, none crossing at any time.
        /// The attachment's motion only partly given to the cloth, as PhysX's and Unity's cloth
        /// scale it (their world velocity and inertia scales).
        /// </summary>
        public const float MaxDragCells = 0.5f;

        /// <summary>
        /// The anchor moved by (dx, dy, dz) in the world since the last step. A small move drags
        /// the cloth (its free particles stay where they were in the world, and the pinned hoist
        /// pulls them after it); of a bigger one only <see cref="MaxDragCells"/> drags it, the
        /// rest carries it; a jump -- a paste, a teleport -- carries it along whole.
        /// </summary>
        public void MoveAnchor(float dx, float dy, float dz)
        {
            float d2 = dx * dx + dy * dy + dz * dz;
            if (d2 > 4f)
                return;

            // Only this much leaves the free particles behind in the world.
            float limit = MaxDragCells * Math.Min(CellU, CellV);
            if (d2 > limit * limit)
            {
                float s = limit / (float)Math.Sqrt(d2);
                dx *= s; dy *= s; dz *= s;
            }

            for (int k = 0; k < Count; k++)
            {
                if (_invMass[k] == 0f)
                    continue;

                X[k] -= dx; Y[k] -= dy; Z[k] -= dz;
                PrevX[k] -= dx; PrevY[k] -= dy; PrevZ[k] -= dz;
            }
        }

        /// <summary>One step of <paramref name="dt"/> seconds in the given air.</summary>
        public void Step(float dt, ref FlagAir air)
        {
            if (dt <= 0f)
                return;

            int n = Count;
            float sigma = Math.Max(_fabric.Density, 0.01f);

            // The air carried sideways with each square metre of cloth, and the inertia a normal
            // force has to move: the cloth's own and that air's.
            float added = _fabric.AddedMass * AirDensity * (float)Math.PI * Height * 0.25f;
            float inertia = sigma + added;
            float share = added / inertia;
            float ownShare = sigma / inertia;

            float drag = _fabric.Drag * AirDensity / (2f * inertia) * dt;
            float friction = _fabric.Friction * AirDensity / (2f * sigma) * dt;
            float keep = Math.Max(0f, 1f - _fabric.Damping * dt);
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

            // ---- the pairs of particles near enough to collide this step (none while it is flat) ----
            float thick = 0f;
            if (SelfCollision && !Flat())
            {
                thick = CollisionThickness * Math.Min(CellU, CellV);
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

                Attach();
            }

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

            ComputeNormals(X, Y, Z, Nx, Ny, Nz);
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
        /// The long range attachments, and the ground. Each particle is held within its flat
        /// distance of three hoist points: its own row's (which stops the rows stretching) and
        /// the two corners (which stop the cloth sagging or rising away from the hoist -- the
        /// vertical stretch the row alone lets through in a strong wind). The hoist is pinned,
        /// so its points are where the pole would be. Written out (Mono inlines nothing).
        /// </summary>
        private void Attach()
        {
            float[] px = _px, py = _py, pz = _pz;
            float ground = GroundY + 0.02f;
            int cols = Columns;
            int bottom = (Rows - 1) * cols;
            float tx0 = px[0], ty0 = py[0], tz0 = pz[0];
            float bx = px[bottom], by = py[bottom], bz = pz[bottom];

            for (int j = 0; j < Rows; j++)
            {
                int row = j * cols;
                float hx = px[row], hy = py[row], hz = pz[row];

                for (int i = 1; i < cols; i++)
                {
                    int k = row + i;
                    float x = px[k], y = py[k], z = pz[k];

                    float dx = x - hx, dy = y - hy, dz = z - hz;
                    float d2 = dx * dx + dy * dy + dz * dz;
                    float reach = _tether[k];
                    if (d2 > reach * reach)
                    {
                        float s = reach / (float)Math.Sqrt(d2);
                        x = hx + dx * s; y = hy + dy * s; z = hz + dz * s;
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
                if (Nx[k] * mx + Ny[k] * my + Nz[k] * mz < limit)
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
                int x = (int)Math.Floor(px[k] * inv), y = (int)Math.Floor(py[k] * inv), z = (int)Math.Floor(pz[k] * inv);
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

            _pairs = 0;
            float reach2 = reach * reach;
            for (int a = 0; a < n; a++)
            {
                int ax = cx[a], ay = cy[a], az = cz[a];
                int ia = _col[a], ja = _row[a];
                bool pinned = _invMass[a] == 0f;
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

                                int di = _col[b] - ia, dj = _row[b] - ja;
                                if (di <= 1 && di >= -1 && dj <= 1 && dj >= -1)
                                    continue;
                                if (pinned && _invMass[b] == 0f)
                                    continue;

                                float ex = px[b] - x, ey = py[b] - y, ez = pz[b] - z;
                                if (ex * ex + ey * ey + ez * ez >= reach2)
                                    continue;

                                if (_pairs == _pairA.Length)
                                {
                                    Array.Resize(ref _pairA, _pairs * 2);
                                    Array.Resize(ref _pairB, _pairs * 2);
                                }

                                _pairA[_pairs] = a;
                                _pairB[_pairs] = b;
                                _pairs++;
                            }
                        }
                    }
                }
            }
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

        private void BuildLinks()
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
                    Link(j * Columns + i, (j + 2) * Columns + i, BendLink);
        }

        private void Link(int a, int b, byte kind)
        {
            // Two pinned particles: nothing to hold.
            if (_invMass[a] == 0f && _invMass[b] == 0f)
                return;

            int ia = a % Columns, ja = a / Columns;
            int ib = b % Columns, jb = b / Columns;
            float du = (ib - ia) * CellU, dv = (jb - ja) * CellV;

            float w = _invMass[a] + _invMass[b];
            _a[_links] = a;
            _b[_links] = b;
            _rest[_links] = (float)Math.Sqrt(du * du + dv * dv);
            _kind[_links] = kind;
            _wa[_links] = _invMass[a] / w;
            _wb[_links] = _invMass[b] / w;
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
