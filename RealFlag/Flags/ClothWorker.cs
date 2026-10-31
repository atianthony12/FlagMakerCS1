using System;
using System.Diagnostics;
using System.Threading;

namespace RealFlag.Flags
{
    /// <summary>
    /// One flag's work for one frame: its cloth's steps in its air, then its mesh's vertices
    /// between the last two steps. Filled on the main thread, done on the cloth's thread.
    /// </summary>
    public struct ClothJob
    {
        public FlagCloth Cloth;

        /// <summary>Refreshed after the steps at <see cref="Alpha"/>; null leaves the mesh as it is.</summary>
        public FlagMesh Mesh;

        /// <summary>How many steps of <see cref="Size"/> seconds, and the flag's clock at the first.</summary>
        public int Steps;
        public float Size;
        public float Time;
        public float Alpha;

        /// <summary>The air, as FlagWind.Air takes it.</summary>
        public float Speed, Gusts, DirX, DirZ, Height, Waviness, Length, AnchorX, AnchorZ;

        /// <summary>The terrain's roughness length round the flag, metres (CityRoughness); 0 = a town's (FlagWind.RoughnessLength).</summary>
        public float Roughness;

        /// <summary>Set by the thread: the cloth stopped being numbers and was laid out again.</summary>
        public bool WentWrong;

        /// <summary>Set by the thread: what a job threw (its cloth is laid out again by the owner).</summary>
        public Exception Failure;
    }

    /// <summary>
    /// The one shape a flag that does not move keeps (FlagMotion Still or Flat): made once, then
    /// drawn as it is and never stepped again. PURE: FlagRenderer runs a still flag's steps a few
    /// a frame as the cloth's jobs; the preview and the tests run them in one go -- the same steps
    /// on a clock of 1/32 s steps (exact in a float however the steps are split into jobs), so
    /// the same shape everywhere, every time the city loads.
    /// </summary>
    /// <remarks>
    /// Still: the cloth laid out flat along its fly at rest, then flown in a steady breeze along
    /// that fly's level part -- the flag's heading, which the player turns -- with no big gusts,
    /// only the eddies of its own size (FlagWind, x its waviness), at 10 m where the breeze is
    /// the speed at the flag, for <see cref="StepsFor"/>; a flag whose fly at rest points down
    /// (hoisted from its top) hangs in calm air. It is made in the flag's own frame -- heading 0,
    /// the anchor at the origin -- so it is the design's alone, and turned and carried to where the
    /// flag stands (FlagCloth.SetShape; turned again with its prop: FlagCloth.Turn). The ground and
    /// the pole's collider are left out (a flag's shape does not change with its height, or with
    /// its pole's length).
    /// Flat: laid out flat along its fly at rest, exactly, no steps.
    /// </remarks>
    public static class StillPose
    {
        /// <summary>
        /// The breeze a still flag of up to <see cref="BreezeLength"/> is caught in, m/s at the flag:
        /// Beaufort force 3, a gentle breeze, whose sign on land is "leaves and small twigs in
        /// constant motion; light flags extended" (the Met Office's Beaufort scale) -- the flag
        /// stands out from its hoist with a few waves down it.
        /// </summary>
        public const float Breeze = 5f;

        /// <summary>A pose's step, seconds: 1/32, so every step's clock is exact in a float.</summary>
        public const float StepSize = 1f / 32f;

        /// <summary>A pose runs at least this long, seconds, and this many times the time the breeze takes to cross the flag more.</summary>
        public const float MinTime = 3.5f;
        public const float CrossTimes = 1.5f;

        /// <summary>
        /// Over its last <see cref="Window"/> seconds, the shape is weighed every
        /// <see cref="CandidateEvery"/> steps (1/8 s) and the smoothest kept (<see cref="Roughness"/>):
        /// a moment taken blindly often caught the fly's corner in the middle of a flick, a crumpled
        /// tatter frozen for good, where a quarter of a second either side the same wave was clean.
        /// </summary>
        public const float Window = 2f;
        public const int CandidateEvery = 4;

        /// <summary>Its fly at rest nearer upright than this (its level part shorter): the flag hangs in calm air.</summary>
        public const float MinLevel = 0.3f;

        /// <summary>The air's height over the ground, metres: the breeze is the speed there (FlagWind.ReferenceHeight).</summary>
        public const float Height = FlagWind.ReferenceHeight;

        /// <summary>How many steps a still flag's shape is run for.</summary>
        public static int StepsFor(float length, float speed)
        {
            float time = MinTime + CrossTimes * Math.Max(length, 0.5f) / Math.Max(speed, 1f);
            return (int)Math.Ceiling(time / StepSize);
        }

        /// <summary>
        /// The breeze for a flag this long, m/s: <see cref="Breeze"/> up to <see cref="BreezeLength"/>,
        /// then growing as the square root of the length -- the same U^2 / (g L), the Froude number,
        /// so a big flag holds out against its weight as a small one does. In one breeze a big flag
        /// droops more (preview, the shape kept: 3 m 18 deg at 5 m/s; 8 m 32 deg at 5, 16-18 at 7-9;
        /// 20 m 41 deg at 5, 8-16 at 9-12): 8 m gets 8.2 m/s, 20 m 12.9, 30 m 15.8.
        /// </summary>
        public static float BreezeFor(float length)
        {
            return Breeze * (float)Math.Sqrt(Math.Max(length, BreezeLength) / BreezeLength);
        }

        /// <summary>Flags up to this long, metres, are caught in <see cref="Breeze"/> itself.</summary>
        public const float BreezeLength = 3f;

        /// <summary>The breeze for a flag whose fly at rest is (flyX, flyY, flyZ): along its level part, or none.</summary>
        public static void Wind(float flyX, float flyY, float flyZ, float length, out float dirX, out float dirZ, out float speed)
        {
            float level = (float)Math.Sqrt(flyX * flyX + flyZ * flyZ);
            if (level < MinLevel)
            {
                dirX = 1f;
                dirZ = 0f;
                speed = 0f;
                return;
            }

            dirX = flyX / level;
            dirZ = flyZ / level;
            speed = BreezeFor(length);
        }

        /// <summary>
        /// A still or flat flag's cloth before its steps: a still one clipped to its halyard if it
        /// has one (its heading bows between the clips), a flat one held along its whole hoist (a
        /// halyard's slack would only shear its first column); laid out flat along its fly at rest
        /// (a still one with a hair of noise, to be flown; a flat one exactly).
        /// </summary>
        public static void Lay(FlagCloth cloth, int motion, bool halyard, float flyX, float flyY, float flyZ)
        {
            cloth.SetHalyard(halyard && motion == FlagMotion.Still);
            cloth.LayOut(flyX, flyY, flyZ, motion != FlagMotion.Flat);
        }

        /// <summary>The sub-steps of one of its steps (FlagCloth.SubStepsFor: ClothWorker.Execute splits them so).</summary>
        public static int SubSteps(FlagCloth cloth, float flyX, float flyY, float flyZ)
        {
            float dirX, dirZ, speed;
            Wind(flyX, flyY, flyZ, cloth.Length, out dirX, out dirZ, out speed);
            return cloth.SubStepsFor(FlagWind.SpeedAt(speed, Height), StepSize);
        }

        /// <summary>A step after which the shape is weighed: one of every <see cref="CandidateEvery"/> in the last <see cref="Window"/> of <paramref name="total"/>.</summary>
        public static bool IsCandidate(int step, int total)
        {
            return step > 0 && step <= total && step % CandidateEvery == 0 && step >= total - (int)(Window / StepSize);
        }

        /// <summary>
        /// How crumpled the cloth is: the squared second differences of its particles' places along
        /// its rows and down its columns, summed -- the thin-plate energy (Welch and Witkin,
        /// Variational Surface Modeling, SIGGRAPH 1992) on the grid, up to a constant: a smooth wave
        /// has little, a sharp fold much. Compared within one cloth only.
        /// </summary>
        public static float Roughness(FlagCloth cloth)
        {
            int cols = cloth.Columns, rows = cloth.Rows;
            float[] x = cloth.X, y = cloth.Y, z = cloth.Z;
            double sum = 0.0;
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    int k = j * cols + i;
                    if (i > 0 && i < cols - 1)
                    {
                        float dx = x[k - 1] - 2f * x[k] + x[k + 1], dy = y[k - 1] - 2f * y[k] + y[k + 1], dz = z[k - 1] - 2f * z[k] + z[k + 1];
                        sum += dx * dx + dy * dy + dz * dz;
                    }

                    if (j > 0 && j < rows - 1)
                    {
                        float dx = x[k - cols] - 2f * x[k] + x[k + cols], dy = y[k - cols] - 2f * y[k] + y[k + cols],
                              dz = z[k - cols] - 2f * z[k] + z[k + cols];
                        sum += dx * dx + dy * dy + dz * dz;
                    }
                }
            }

            return (float)sum;
        }

        /// <summary>
        /// How much of the design faces out, 0..1: the cloth's area projected on its plane at rest
        /// (through its hoist and its fly at rest), signed -- a fold turned back counts against it --
        /// over the flat flag's.
        /// </summary>
        public static float Shown(FlagCloth cloth, float flyX, float flyY, float flyZ)
        {
            // The plane's normal: hoist x fly at rest.
            float hx = cloth.HoistX, hy = cloth.HoistY, hz = cloth.HoistZ;
            float nx = hy * flyZ - hz * flyY, ny = hz * flyX - hx * flyZ, nz = hx * flyY - hy * flyX;
            int cols = cloth.Columns, rows = cloth.Rows;
            float[] x = cloth.X, y = cloth.Y, z = cloth.Z;
            double sum = 0.0;
            for (int j = 0; j < rows - 1; j++)
            {
                for (int i = 0; i < cols - 1; i++)
                {
                    int a = j * cols + i, b = a + 1, c = a + cols, d = c + 1;

                    // The quad's area vector: half the cross product of its diagonals.
                    float ux = x[d] - x[a], uy = y[d] - y[a], uz = z[d] - z[a];
                    float vx = x[c] - x[b], vy = y[c] - y[b], vz = z[c] - z[b];
                    float cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
                    sum += 0.5 * (cx * nx + cy * ny + cz * nz);
                }
            }

            return (float)(Math.Abs(sum) / Math.Max(cloth.Length * cloth.Height, 1e-6f));
        }

        /// <summary>
        /// A candidate shape's weight, the least kept: its <see cref="Roughness"/> over the square of
        /// how much of it faces out (<see cref="Shown"/>, at least a fifth) -- smooth, but not a
        /// smooth curl that hides the design.
        /// </summary>
        public static float Score(FlagCloth cloth, float flyX, float flyY, float flyZ)
        {
            float shown = Math.Max(Shown(cloth, flyX, flyY, flyZ), 0.2f);
            return Roughness(cloth) / (shown * shown);
        }

        /// <summary>Its steps <paramref name="first"/> to <paramref name="first"/> + <paramref name="steps"/> - 1, as a job.</summary>
        public static ClothJob Job(FlagCloth cloth, float flyX, float flyY, float flyZ, float waviness, float length, int first, int steps)
        {
            float dirX, dirZ, speed;
            Wind(flyX, flyY, flyZ, length, out dirX, out dirZ, out speed);
            return new ClothJob
            {
                Cloth = cloth,
                Steps = steps,
                Size = StepSize,
                Time = first * StepSize,
                Alpha = 1f,
                Speed = speed,
                Gusts = 0f,
                DirX = dirX,
                DirZ = dirZ,
                Height = Height,
                Waviness = waviness,
                Length = length,
            };
        }

        /// <summary>The whole shape, here and now (the preview, the tests): laid (<see cref="Lay"/>) and, still, run and chosen as the game does it (the run returned; null for a flat one).</summary>
        public static StillPoseRun Make(FlagCloth cloth, int motion, bool halyard, float flyX, float flyY, float flyZ, float waviness, float length)
        {
            Lay(cloth, motion, halyard, flyX, flyY, flyZ);
            if (motion != FlagMotion.Still)
                return null;

            var run = new StillPoseRun(cloth, flyX, flyY, flyZ, waviness, length);
            while (!run.Finished)
            {
                ClothJob job = run.Next(int.MaxValue);
                ClothWorker.Execute(ref job);
                run.Took();
            }

            run.Finish();
            return run;
        }
    }

    /// <summary>
    /// The making of one still flag's shape (StillPose): its cloth, laid out, run a job at a time
    /// -- each ending at the next step the shape is weighed at, so the same steps are weighed
    /// however many a frame the renderer gives it -- the smoothest shape kept, and put back on the
    /// cloth at the end. PURE. The renderer calls <see cref="Next"/> when it plans a frame and
    /// <see cref="Took"/> once that batch is back; the cloth and the kept shape are the thread's
    /// between the two.
    /// </summary>
    public sealed class StillPoseRun
    {
        public readonly FlagCloth Cloth;

        /// <summary>The steps it runs for, the steps done, and those handed out and not yet taken back.</summary>
        public readonly int Total;
        public int Done;
        public int Out;

        private readonly float _flyX, _flyY, _flyZ, _waviness, _length;
        private readonly float[] _bestX, _bestY, _bestZ;
        private float _best = float.MaxValue;

        /// <summary>The step whose shape is kept so far (-1: none weighed yet).</summary>
        public int BestStep = -1;

        /// <param name="cloth">Laid out already (StillPose.Lay).</param>
        public StillPoseRun(FlagCloth cloth, float flyX, float flyY, float flyZ, float waviness, float length)
        {
            Cloth = cloth;
            _flyX = flyX; _flyY = flyY; _flyZ = flyZ;
            _waviness = waviness;
            _length = length;
            float dirX, dirZ, speed;
            StillPose.Wind(flyX, flyY, flyZ, length, out dirX, out dirZ, out speed);
            Total = StillPose.StepsFor(length, speed);
            _bestX = new float[cloth.Count];
            _bestY = new float[cloth.Count];
            _bestZ = new float[cloth.Count];
        }

        public bool Finished
        {
            get { return Done >= Total && Out == 0; }
        }

        /// <summary>The sub-steps each of its steps takes (the cost of a step, for the renderer's plan).</summary>
        public int SubSteps
        {
            get { return StillPose.SubSteps(Cloth, _flyX, _flyY, _flyZ); }
        }

        /// <summary>The next job: at most <paramref name="maxSteps"/>, never past the next step to weigh.</summary>
        public ClothJob Next(int maxSteps)
        {
            int end = Done + Math.Max(0, Math.Min(Total - Done, Math.Max(1, maxSteps)));
            for (int s = Done + 1; s < end; s++)
            {
                if (StillPose.IsCandidate(s, Total))
                {
                    end = s;
                    break;
                }
            }

            Out = end - Done;
            return StillPose.Job(Cloth, _flyX, _flyY, _flyZ, _waviness, _length, Done, Out);
        }

        /// <summary>The last job is done: its steps counted, and the shape weighed if this is a step to.</summary>
        public void Took()
        {
            Done += Out;
            Out = 0;
            if (!StillPose.IsCandidate(Done, Total))
                return;

            float rough = StillPose.Score(Cloth, _flyX, _flyY, _flyZ);
            if (rough < _best && !float.IsNaN(rough))
            {
                _best = rough;
                BestStep = Done;
                Cloth.CopyShape(_bestX, _bestY, _bestZ, 0f);
            }
        }

        /// <summary>Done: the cloth takes the smoothest shape weighed, still.</summary>
        public void Finish()
        {
            if (BestStep >= 0)
                Cloth.SetShape(_bestX, _bestY, _bestZ, 0f);
        }
    }

    /// <summary>
    /// How many steps a flag takes this frame, within the cloth thread's budget. PURE: the
    /// arithmetic of FlagRenderer's planning, tested by tools/test-cloth.ps1.
    /// </summary>
    public static class ClothPlan
    {
        /// <summary>
        /// A frame's steps for one flag.
        /// </summary>
        /// <param name="pending">Time not yet stepped, seconds: the frame's time is added and the steps' taken off.</param>
        /// <param name="dt">The frame's time, seconds.</param>
        /// <param name="size">The flag's step, seconds.</param>
        /// <param name="maxSteps">The most it catches up in one frame; time beyond that is let go (a hitch is not worth a burst of physics).</param>
        /// <param name="stepCost">What one of its steps costs the thread, milliseconds.</param>
        /// <param name="budget">The thread's work for the frame, milliseconds.</param>
        /// <param name="spent">The work planned so far this frame (nearer flags first), milliseconds: its steps are added.</param>
        /// <param name="always">Steps whatever the budget (the placement ghost).</param>
        /// <param name="behind">How far behind the frame its first step starts, seconds (its clock: now - behind).</param>
        /// <param name="waiting">No room for it: it takes no step, and keeps one step's time for when there is.</param>
        /// <returns>The steps due now.</returns>
        public static int Due(ref float pending, float dt, float size, int maxSteps, double stepCost, double budget, ref double spent,
                              bool always, out float behind, out bool waiting)
        {
            waiting = false;
            pending += dt;
            behind = pending;

            // A hair of tolerance: 0.05 s is three steps of 1/60 s, not two and a rounding error.
            int due = Math.Min((int)((pending + 1e-5f) / size), maxSteps);
            if (due <= 0)
                return 0;

            double cost = due * stepCost;
            if (!always && spent > 0.0 && spent + cost > budget)
            {
                waiting = true;
                if (pending > size)
                    pending = size;
                return 0;
            }

            spent += cost;
            pending = Math.Max(0f, pending - due * size);
            if (pending >= size - 1e-5f)
                pending = 0f;
            return due;
        }

        /// <summary>
        /// A thread's time for one frame's batch, milliseconds: <paramref name="perStep"/> for a
        /// frame of one step or shorter, as much more as the frame is longer, up to the most steps
        /// a flag takes in one frame -- so the same flags move at any frame rate, and the batch
        /// stays under half the frame. The first rule, a fixed 8 ms (a 60 fps frame's), let a
        /// 10 fps frame, where every flag takes three steps, move a third of the flags while the
        /// threads idled nine tenths of it: the author's five flags at 6-16 fps, "the 3 smaller
        /// ones refused to play ... they were frozen" (the log: "3 waiting for room").
        /// </summary>
        public static double BudgetFor(float frame, float step, int maxSteps, double perStep)
        {
            double steps = frame / Math.Max(step, 1e-6f);
            return perStep * Math.Max(1.0, Math.Min((double)maxSteps, steps));
        }

        /// <summary>
        /// The frame time the budget is planned on: down to a shorter frame at once, up a tenth of
        /// the way a frame to a longer one -- one slow frame (a save, a hitch) does not plan the
        /// next batch as if the frames that follow were as long.
        /// </summary>
        public static float SmoothFrame(float smoothed, float frame)
        {
            if (!(smoothed > 0f) || frame <= smoothed)
                return frame;
            return smoothed + 0.1f * (frame - smoothed);
        }

        /// <summary>
        /// The time a flag steps this frame while the game is paused and the flags are frozen:
        /// none while it is left alone; while it is handled -- dragged, turned, redesigned -- and
        /// for a moment after (<paramref name="awake"/> seconds still to go), the frame's own,
        /// slowing to nothing over the last <paramref name="fade"/> seconds: a still that comes to
        /// life under the player's hand and settles back into a still, not one that freezes
        /// mid-swing or lets its hoist be dragged away from its cloth.
        /// </summary>
        public static float PausedTime(float frame, float awake, float fade)
        {
            if (awake <= 0f || frame <= 0f)
                return 0f;

            return frame * Math.Min(1f, awake / Math.Max(fade, 1e-4f));
        }
    }

    /// <summary>
    /// The cloth's detail under load (2026-10-31; the author's 93 flags in view: 48 moved, the rest
    /// frozen, and the moving ones took turns -- choppy, half speed). When the flags in view want
    /// more of the threads' time than the budget, <see cref="Scale"/> grows the on-screen sizes the
    /// renderer goes by (pixels a cloth cell, the full rate's): the SMALLEST flags on the screen go
    /// coarser and to half rate first, the biggest last -- dynamic resolution scaling's feedback
    /// loop (measure the frame's cost, scale the work to the budget), applied to the cloth. Up
    /// fast, down slowly, and more slowly each time a lowering has to be undone (an exponential
    /// back-off: a detail is a step of 1.7-3x in cost, so one notch down can tip the load over
    /// again). Only while even the coarsest is too much does <see cref="TimeScale"/> slow every
    /// flag evenly -- smooth slow motion, never turns taken (ClothPlan.Due's wait stays only as the
    /// last guard). PURE: tested by tools/test-cloth.ps1.
    /// </summary>
    public sealed class ClothLoad
    {
        /// <summary>The most the sizes are scaled: 18 cells and half rate for a flag up to 8 x 30 = 240 px on the screen.</summary>
        public const float MaxScale = 8f;

        /// <summary>How often it decides, seconds of real time.</summary>
        public const float Interval = 0.5f;

        /// <summary>
        /// A raise is the square root of the overload (a cloth's cost goes with its cells along
        /// squared), within these: never so small that it crawls, never more than this a step.
        /// </summary>
        public const float MinRaise = 1.15f, MaxRaise = 1.6f;

        /// <summary>A lowering's step.</summary>
        public const float Lower = 1.1f;

        /// <summary>Raised over this share of the budget wanted; lowered under <see cref="Low"/> for <see cref="CalmIntervals"/> decisions.</summary>
        public const double High = 1.0, Low = 0.55;

        /// <summary>Decisions under <see cref="Low"/> before a lowering (doubled each time one is undone within <see cref="BackOffTime"/>, to <see cref="MaxCalmIntervals"/>).</summary>
        public const int CalmIntervals = 4, MaxCalmIntervals = 60;
        public const float BackOffTime = 10f;

        /// <summary>
        /// Under this share no notch of detail can tip the load over (18 -> 32 cells is 3.2x the
        /// cost): lowered at the first pace, the back-off forgotten -- the flags gone from view
        /// come back to full detail in seconds, a load on a notch's edge stays backed off.
        /// </summary>
        public const double SafeLow = 0.3;

        /// <summary>The slowest the flags go when even the coarsest is too much (ClothPlan.Due's turns below it).</summary>
        public const float MinTimeScale = 0.25f;

        /// <summary>The on-screen sizes' factor (1: none).</summary>
        public float Scale = 1f;

        /// <summary>The flags' time against real time (1: real time).</summary>
        public float TimeScale = 1f;

        /// <summary>The last decision's share of the budget wanted (at real time).</summary>
        public double Ratio;

        /// <summary>The raises and lowerings made so far (for the log and the tests).</summary>
        public int Raises, Lowerings;

        private double _want, _have;
        private float _elapsed, _sinceLower = float.MaxValue;
        private int _calm, _calmNeeded = CalmIntervals;

        /// <summary>
        /// A frame: the cloth the flags in view want at real time and what the threads have, both
        /// milliseconds of thread time a second, over <paramref name="seconds"/> of real time. True
        /// when <see cref="Scale"/> or <see cref="TimeScale"/> changed.
        /// </summary>
        public bool Add(double wanted, double budget, float seconds)
        {
            if (!(seconds > 0f))
                return false;

            _want += Math.Max(0.0, wanted) * seconds;
            _have += Math.Max(1e-6, budget) * seconds;
            _elapsed += seconds;
            _sinceLower += seconds;
            if (_elapsed < Interval)
                return false;

            double r = _want / _have;
            _want = 0.0;
            _have = 0.0;
            _elapsed = 0f;
            Ratio = r;

            float scale = Scale;
            if (r > High)
            {
                _calm = 0;
                if (Scale < MaxScale)
                {
                    float step = (float)Math.Min(MaxRaise, Math.Max(MinRaise, Math.Sqrt(r)));
                    Scale = Math.Min(MaxScale, Scale * step);
                    Raises++;
                    if (_sinceLower < BackOffTime)
                        _calmNeeded = Math.Min(MaxCalmIntervals, _calmNeeded * 2);
                }
            }
            else if (r < Low && Scale > 1f)
            {
                if (r < SafeLow)
                    _calmNeeded = CalmIntervals;
                if (++_calm >= _calmNeeded)
                {
                    Scale = Math.Max(1f, Scale / Lower);
                    if (Scale < 1.001f)
                        Scale = 1f;
                    Lowerings++;
                    _sinceLower = 0f;
                    _calm = _calmNeeded / 2;
                }
            }
            else
            {
                _calm = 0;
            }

            // Slow motion only for what the coarsest cannot take: while the scale still has room it
            // is coming down by detail within a second or two.
            float time = r > High && Scale >= MaxScale ? (float)Math.Max(MinTimeScale, 0.95 / r) : 1f;
            bool changed = scale != Scale || time != TimeScale;
            TimeScale = time;
            return changed;
        }

        /// <summary>Back to none (a new city).</summary>
        public void Reset()
        {
            Scale = 1f;
            TimeScale = 1f;
            Ratio = 0.0;
            Raises = 0;
            Lowerings = 0;
            _want = 0.0;
            _have = 0.0;
            _elapsed = 0f;
            _sinceLower = float.MaxValue;
            _calm = 0;
            _calmNeeded = CalmIntervals;
        }
    }

    /// <summary>
    /// Steps the flags' cloth on threads of its own, while the game draws the frame. PURE of
    /// the engine: FlagMesh.Refresh is managed arithmetic on Unity's structs (Vector3.Min, Max,
    /// magnitude, Bounds.SetMinMax are IL, not internal calls -- read from the game's
    /// UnityEngine.dll), and nothing else it does touches the engine.
    /// </summary>
    /// <remarks>
    /// The frame: the renderer waits for the last batch (<see cref="Wait"/>) -- long done, as a
    /// rule -- then reads the props, moves and redesigns cloths, hands the refreshed vertices
    /// to their meshes and draws, then fills the next batch and <see cref="Run"/>s it. So a cloth
    /// is touched by the main thread only between Wait and Run, and by the cloth's threads only
    /// between Run and Wait; every flag is drawn one frame after it was stepped, which nobody
    /// can see in a flag. Unity's own job system came with 2018.1; this is the plain pattern of
    /// workers and events (the game itself runs its simulation on a thread of its own).
    ///
    /// With several threads (<see cref="ThreadsFor"/>: one per six of the machine's, at most
    /// four), each takes the batch's next job until none is left -- a flag's job is its own
    /// cloth and mesh, so they need nothing from each other -- and the last one done says so.
    /// A cloth is stepped by one thread per frame, whichever: the result is the same, bit for
    /// bit.
    ///
    /// If no thread can start, <see cref="Run"/> does the batch at once, on the caller's
    /// thread: slower, the same result.
    /// </remarks>
    public sealed class ClothWorker
    {
        /// <summary>
        /// The most threads the cloth takes, whatever the machine (the "Cloth threads" option's
        /// top, 2026-10-31); Auto (<see cref="ThreadsFor(int)"/>) takes at most <see cref="AutoThreads"/>.
        /// </summary>
        public const int MaxThreads = 16;
        public const int AutoThreads = 4;

        /// <summary>The "Cloth threads" option's choices: 0 = Auto, then thread counts.</summary>
        public static readonly int[] ThreadChoices = { 0, 1, 2, 3, 4, 6, 8, 12, 16 };

        private ClothJob[] _jobs = new ClothJob[32];
        private int _count;

        private Thread[] _threads = new Thread[0];
        private AutoResetEvent[] _go = new AutoResetEvent[0];
        private readonly ManualResetEvent _idle = new ManualResetEvent(true);
        private volatile bool _stop;
        private bool _running, _stuck;

        /// <summary>The next job to take, the threads still working, and the batch's totals (Interlocked).</summary>
        private int _next, _busy;
        private long _work, _ticks, _started;

        /// <summary>
        /// The last batch: the time its jobs took on the cloth's threads (added up), how long it
        /// was out (from Run to the last thread done), and its work in particle steps (sub-steps x
        /// particles).
        /// </summary>
        public double LastMilliseconds { get; private set; }
        public double LastWallMilliseconds { get; private set; }
        public long LastParticleSteps { get; private set; }

        /// <summary>True when the batches run on threads of their own (false: on the caller's).</summary>
        public bool Threaded
        {
            get { return _threads.Length > 0; }
        }

        /// <summary>How many threads step the cloth (0: the caller's).</summary>
        public int Threads
        {
            get { return _threads.Length; }
        }

        /// <summary>
        /// The cloth's threads for a machine of this many (logical) processors: one per six, one
        /// to <see cref="MaxThreads"/>. The game keeps its main, simulation and render threads
        /// busy and other mods have theirs; a 4-core machine gets one, an 8-core, 16-thread one
        /// two.
        /// </summary>
        public static int ThreadsFor(int processors)
        {
            return Math.Max(1, Math.Min(AutoThreads, processors / 6));
        }

        /// <summary>
        /// The threads for the "Cloth threads" option: 0 (or less) = Auto (<see cref="ThreadsFor(int)"/>),
        /// else the player's number, held to 1..<see cref="MaxThreads"/> and to the machine's processors.
        /// </summary>
        public static int ThreadsFor(int processors, int chosen)
        {
            if (chosen <= 0)
                return ThreadsFor(processors);
            return Math.Max(1, Math.Min(chosen, Math.Min(MaxThreads, Math.Max(1, processors))));
        }

        /// <summary>The option's choices on a machine of this many processors: Auto, and the counts it has the processors for.</summary>
        public static int[] ThreadChoicesFor(int processors)
        {
            var list = new System.Collections.Generic.List<int>();
            foreach (int c in ThreadChoices)
            {
                if (c <= 1 || c <= processors)
                    list.Add(c);
            }

            return list.ToArray();
        }

        public int Count
        {
            get { return _count; }
        }

        /// <summary>The batch's jobs (after <see cref="Wait"/>, with the thread's results in them).</summary>
        public ClothJob[] Jobs
        {
            get { return _jobs; }
        }

        /// <summary>Starts one thread; false, with the reason, if it could not (the batches then run inline).</summary>
        public bool Start(out string problem)
        {
            return Start(1, out problem);
        }

        /// <summary>
        /// Starts up to <paramref name="threads"/> threads (<see cref="MaxThreads"/> at most); false,
        /// with the reason, if none could start (the batches then run inline). If only some
        /// start, those do the work.
        /// </summary>
        public bool Start(int threads, out string problem)
        {
            problem = null;
            if (_threads.Length > 0)
                return true;

            threads = Math.Max(1, Math.Min(threads, MaxThreads));
            _stop = false;
            var started = new Thread[threads];
            var go = new AutoResetEvent[threads];
            int n = 0;
            for (int i = 0; i < threads; i++)
            {
                try
                {
                    var signal = new AutoResetEvent(false);
                    var thread = new Thread(() => Loop(signal)) { IsBackground = true, Name = "RealFlag cloth " + (n + 1) };
                    thread.Start();
                    go[n] = signal;
                    started[n++] = thread;
                }
                catch (Exception e)
                {
                    problem = e.GetType().Name + ": " + e.Message;
                    break;
                }
            }

            if (n < threads)
            {
                Array.Resize(ref started, n);
                Array.Resize(ref go, n);
            }

            _go = go;
            _threads = started;
            return n > 0;
        }

        /// <summary>
        /// Waits for the threads to finish the batch, then stops them (the city is unloading).
        /// False if they did not end within 2 s: they are background threads, so they end with
        /// the game.
        /// </summary>
        public bool Stop()
        {
            Thread[] threads = _threads;
            if (threads.Length == 0)
                return true;

            bool idle = Wait();
            _stop = true;
            foreach (AutoResetEvent signal in _go)
                signal.Set();

            bool ended = true;
            var clock = Stopwatch.StartNew();
            foreach (Thread thread in threads)
                ended &= thread.Join(Math.Max(0, 2000 - (int)clock.ElapsedMilliseconds));

            _threads = new Thread[0];
            _go = new AutoResetEvent[0];
            if (idle)
                Clear();
            return idle && ended;
        }

        /// <summary>
        /// How long <see cref="Wait"/> waits, milliseconds: a batch takes a few. Past it the thread
        /// is taken to be stuck, and the game must not hang with it.
        /// </summary>
        public const int WaitLimit = 5000;

        /// <summary>
        /// Blocks until the last <see cref="Run"/> is done; at once when nothing is running. False
        /// when the thread has not finished within <see cref="WaitLimit"/>: its jobs are then still
        /// its own, and the caller must leave their cloths alone for good.
        /// </summary>
        public bool Wait()
        {
            if (!_running)
                return true;

            if (_stuck || !_idle.WaitOne(WaitLimit))
            {
                _stuck = true;
                return false;
            }

            _running = false;
            return true;
        }

        /// <summary>A <see cref="Wait"/> ran out: the thread still has its batch, and is never waited for again.</summary>
        public bool Stuck
        {
            get { return _stuck; }
        }

        /// <summary>True while a batch is out on the thread (between <see cref="Run"/> and <see cref="Wait"/>).</summary>
        public bool Running
        {
            get { return _running; }
        }

        /// <summary>Empties the batch (only while nothing is running: after <see cref="Wait"/>).</summary>
        public void Clear()
        {
            if (_running)
                throw new InvalidOperationException("the cloth's batch is still running: Wait first");

            for (int i = 0; i < _count; i++)
                _jobs[i] = default(ClothJob);
            _count = 0;
        }

        public void Add(ref ClothJob job)
        {
            if (_running)
                throw new InvalidOperationException("the cloth's batch is still running: Wait first");

            if (_count == _jobs.Length)
                Array.Resize(ref _jobs, _count * 2);
            _jobs[_count++] = job;
        }

        /// <summary>Starts the batch on the threads (or does it now, without them).</summary>
        public void Run()
        {
            if (_count == 0 || _running)
                return;

            _next = 0;
            _work = 0;
            _ticks = 0;
            _started = Stopwatch.GetTimestamp();

            if (_threads.Length == 0)
            {
                TakeJobs();
                Finish();
                return;
            }

            // As many threads as there are jobs, up to all of them.
            int wake = Math.Min(_threads.Length, _count);
            _busy = wake;
            _idle.Reset();
            _running = true;
            for (int i = 0; i < wake; i++)
                _go[i].Set();
        }

        private void Loop(AutoResetEvent go)
        {
            while (true)
            {
                go.WaitOne();
                if (_stop)
                    break;

                TakeJobs();

                // The last one done ends the batch.
                if (Interlocked.Decrement(ref _busy) == 0)
                {
                    Finish();
                    _idle.Set();
                }
            }
        }

        /// <summary>The batch's next job, until none is left: each is timed, and what it throws is kept in it.</summary>
        private void TakeJobs()
        {
            long work = 0, ticks = 0;
            while (true)
            {
                int i = Interlocked.Increment(ref _next) - 1;
                if (i >= _count)
                    break;

                long start = Stopwatch.GetTimestamp();
                try
                {
                    work += Execute(ref _jobs[i]);
                }
                catch (Exception e)
                {
                    _jobs[i].Failure = e;
                }

                ticks += Stopwatch.GetTimestamp() - start;
            }

            Interlocked.Add(ref _work, work);
            Interlocked.Add(ref _ticks, ticks);
        }

        private void Finish()
        {
            double perTick = 1000.0 / Stopwatch.Frequency;
            LastMilliseconds = Interlocked.Read(ref _ticks) * perTick;
            LastWallMilliseconds = (Stopwatch.GetTimestamp() - _started) * perTick;
            LastParticleSteps = Interlocked.Read(ref _work);
        }

        /// <summary>
        /// One job, on whatever thread calls it: the steps (each split by FlagCloth.SubStepsFor
        /// for the wind at the flag), a cloth that went wrong laid out again, then the mesh.
        /// Returns the work done, in particle steps.
        /// </summary>
        public static long Execute(ref ClothJob job)
        {
            FlagCloth cloth = job.Cloth;
            long work = 0;

            if (job.Steps > 0)
            {
                int sub = cloth.SubStepsFor(FlagWind.SpeedAt(job.Speed, job.Height, job.Roughness), job.Size);
                float dt = job.Size / sub;

                // The anchor's moves since the last steps: as much as the cloth follows in these, the rest carried.
                cloth.PlanDrag(job.Steps * job.Size, job.Steps * sub);

                for (int s = 0; s < job.Steps; s++)
                {
                    for (int i = 0; i < sub; i++)
                    {
                        FlagAir air = FlagWind.Air(job.Speed, job.Gusts, job.DirX, job.DirZ, job.Height, job.Waviness, job.Length,
                                                   job.Time + s * job.Size + i * dt, job.AnchorX, job.AnchorZ, job.Roughness);
                        cloth.Step(dt, ref air);
                    }
                }

                cloth.EndDrag();
                work = (long)job.Steps * sub * cloth.Count;

                if (!cloth.IsFinite())
                {
                    // Laid out again and let down, as every new cloth is (Invariant 9): dropped flat
                    // into calm air it would whip through itself.
                    cloth.Reset(job.DirX, job.DirZ);
                    cloth.Calm(FlagCloth.CalmTime);
                    job.WentWrong = true;
                }
            }

            if (job.Mesh != null)
                job.Mesh.Refresh(job.Alpha);

            return work;
        }
    }
}
