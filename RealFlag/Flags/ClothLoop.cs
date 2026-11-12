using System;
using System.Globalization;

namespace RealFlag.Flags
{
    /// <summary>
    /// A flag that plays a LOOP (FlagMotion.Loop): a few seconds of its own cloth flying in a
    /// steady breeze, made once for every flag of its shape and played back by the graphics card
    /// (FlagLoop, FlagCloth.shader) -- no cloth of its own, no steps, nothing a frame but its draw.
    /// PURE: the numbers of it, shared by the renderer, the preview and the tests.
    /// </summary>
    /// <remarks>
    /// The author (2026-11-10): "if a LOT of flags are placed, issues become obvious ... sometimes
    /// people just want a static animation ... In some games, especially older ones like
    /// Battlefield 4, the flags across the map are just an animation that repeats, they don't react
    /// to the environment"; of its name, "I like 'Loop'"; of the two ways to play it, "Let's go
    /// with the GPU loop". The many variables (size, cut, hoist, tilt, waviness) are no trouble:
    /// nothing is shipped baked, each PC makes each shape's loop when a flag first asks for it, as
    /// a still flag's shape is made (StillPose) -- a second or so of a cloth thread.
    ///
    /// What a looping flag gives up, as a still one does: the city's wind (it flies in its own
    /// breeze along its heading, StillPose.BreezeFor x its "Wind on it"), the buildings, props and
    /// its pole as things to keep off, and being dragged (it is carried whole). What it keeps: its
    /// halyard's bow, its cut, its back plate (a banner from its top stays in front of its wall).
    /// </remarks>
    public static class LoopMotion
    {
        /// <summary>The bake's step, seconds: 1/64, exact in a float (the game's own is 1/60).</summary>
        public const float StepSize = 1f / 64f;

        /// <summary>
        /// A loop's cloth is no finer than this (High), whatever the Cloth detail option: one
        /// texture serves its flags near and far, drawn on the smooth surface through its
        /// particles (the shader's Catmull-Rom patches), and its size goes with the square of it.
        /// </summary>
        public const int MaxDetail = 32;

        /// <summary>
        /// A frame is kept every 2 steps (1/32 s) for a flag under <see cref="BigLength"/>, every 4
        /// (1/16 s) from there: a big flag's waves are slow (Froude: their time goes with the
        /// square root of its length), and its loop is longer.
        /// </summary>
        public const float BigLength = 6f;

        /// <summary>
        /// A loop's length for a flag up to 3 m, seconds -- several of its waves, long enough that
        /// the eye does not catch the repeat on one flag -- growing with the square root of the
        /// length (as its breeze does, StillPose.BreezeFor, so the waves it holds stay as many), to
        /// <see cref="MaxSeconds"/>.
        /// </summary>
        public const float BaseSeconds = 5f;
        public const float MaxSeconds = 12f;

        /// <summary>The loop's end is blended into the moments before its start over this long, seconds (LoopBake's seam).</summary>
        public const float BlendSeconds = 0.75f;

        /// <summary>
        /// The seam is looked for among loops this much shorter and longer than
        /// <see cref="Seconds"/>, starting anywhere within <see cref="Choice"/> of it. Measured
        /// 2026-11-10 (.NET, a 3 x 2 m flag at 32 cells, the two blended moments' rms distance):
        /// 22 cm with a choice of half a loop, 17.5 cm with two, the same with four -- against
        /// 4.5 cm the cloth moves in a frame; an 8 m flag 57 / 38 / 38 cm against 14 cm.
        /// </summary>
        public const float Shortest = 0.8f, Longest = 1.25f, Choice = 2f;

        /// <summary>
        /// A banner hoisted from its top hangs: its breeze blows ALONG its hoist (the wall it
        /// hangs on), this share of a flag's (StillPose.BreezeFor of its width) -- it sways and
        /// ripples where it hangs. Across it the same breeze would stand it out from the wall
        /// (0.6 Pa lifts 1.1 N/m2 of cloth 40 degrees), or press it flat on its back plate.
        /// </summary>
        public const float HangShare = 0.5f;

        public static int FrameSteps(float length)
        {
            return length < BigLength ? 2 : 4;
        }

        public static float FrameTime(float length)
        {
            return FrameSteps(length) * StepSize;
        }

        /// <summary>The loop's length aimed at, seconds.</summary>
        public static float Seconds(float length)
        {
            return Math.Min(MaxSeconds, BaseSeconds * (float)Math.Sqrt(Math.Max(length, StillPose.BreezeLength) / StillPose.BreezeLength));
        }

        /// <summary>The detail a loop is made at under the Cloth detail option <paramref name="finest"/>.</summary>
        public static int DetailFor(int finest)
        {
            return Math.Min(FlagCloth.ClampDetail(finest), MaxDetail);
        }

        /// <summary>
        /// The breeze a loop is flown in, in the flag's own frame (heading 0): a flag's along its
        /// fly's level part, as a still flag's (StillPose.Wind); a hanging banner's along its
        /// hoist's (<see cref="HangShare"/>); times the flag's share of the wind
        /// (<paramref name="share"/>: FlagSpec.WindShare, "Wind on it").
        /// </summary>
        public static void Wind(float hoistX, float hoistZ, float flyX, float flyY, float flyZ, float length, float height, float share,
                                out float dirX, out float dirZ, out float speed)
        {
            share = share < 0f ? 0f : share > 1f ? 1f : share;
            float level = (float)Math.Sqrt(flyX * flyX + flyZ * flyZ);
            if (level >= StillPose.MinLevel)
            {
                dirX = flyX / level;
                dirZ = flyZ / level;
                speed = StillPose.BreezeFor(length) * share;
                return;
            }

            float across = (float)Math.Sqrt(hoistX * hoistX + hoistZ * hoistZ);
            if (across < 1e-3f)
            {
                dirX = 1f;
                dirZ = 0f;
                speed = 0f;
                return;
            }

            dirX = hoistX / across;
            dirZ = hoistZ / across;
            speed = StillPose.BreezeFor(height) * HangShare * share;
        }

        /// <summary>
        /// What makes a loop: every flag with the same key plays the same one. The cloth's size,
        /// cut, hoist line, fabric, halyard and back plate, its share of the breeze, the detail it
        /// is made at, whether the cloth collides with itself, and its take (FlagSpec.Take).
        /// </summary>
        public static string Key(FlagSpec spec, int detail, bool selfCollision)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            return spec.Length.ToString("R", inv) + "|" + spec.Proportion + "|" + spec.HoistedFrom + "|" + spec.Tilt.ToString("R", inv) + "|" +
                   spec.Lean.ToString("R", inv) + "|" + spec.Waviness.ToString("R", inv) + "|" + (spec.OnHalyard ? 1 : 0) + "|" +
                   (selfCollision ? 1 : 0) + "|" + spec.Cut + "|" + (FlagCut.UsesDepth(spec.Cut) ? spec.CutDepth.ToString("R", inv) : "") + "|" +
                   spec.WindShare.ToString("R", inv) + "|" + (spec.HasBackPlate ? 1 : 0) + "|" + detail + "|" + spec.Take + "|" + spec.HeldNow;
        }

        /// <summary>
        /// Where in its loop a flag starts, 0..1, by its prop: the golden ratio's multiples (Weyl
        /// 1916, as shared motion's delays: MotionRecord.DelayFor), so no two flags of a row are
        /// in step, nor any regular share of them.
        /// </summary>
        public static double Phase(uint prop)
        {
            double x = prop * 0.6180339887498949;
            return x - Math.Floor(x);
        }

        /// <summary>
        /// The seconds to add to <paramref name="clock"/> so that flag <paramref name="prop"/> is
        /// at frame <paramref name="frame"/> exactly (<see cref="FrameAt"/>: a, blend 0), 0 up to
        /// the loop's length: a loop standing in for a waving flag begins at the frame nearest the
        /// pose its cloth was in (FlagRenderer.PlanStandIn, 2026-11-11).
        /// </summary>
        public static float ShiftTo(int frame, double clock, uint prop, int frames, float frameTime)
        {
            if (frames < 2 || frameTime <= 0f)
                return 0f;

            double at = clock / frameTime + Phase(prop) * frames;
            double d = frame - at;
            d -= Math.Floor(d / frames) * frames;
            return (float)(d * frameTime);
        }

        /// <summary>The shares of its breeze a stand-in's loop is made at (<see cref="ShareFor"/>).</summary>
        public const int ShareSteps = 4;

        /// <summary>
        /// The share of its loop's breeze (FlagSpec.WindShare, the bake's) nearest the wind a waving
        /// flag flies in, in quarters, a quarter at least: a loop standing in for it is the one
        /// made in about its own wind (<paramref name="speed"/>, m/s on its cloth, against
        /// <paramref name="breeze"/>, the loop's at a full share), so the flag that takes it does
        /// not jump from drooping in a light air to flying in a fresh breeze. A loop is never made
        /// in more than its breeze: past it, the whole.
        /// </summary>
        public static float ShareFor(float speed, float breeze)
        {
            if (!(breeze > 1e-3f))
                return 1f;

            int step = (int)Math.Round(ShareSteps * speed / breeze);
            return Math.Max(1, Math.Min(ShareSteps, step)) / (float)ShareSteps;
        }

        /// <summary>
        /// How far past the halfway point between two quarters, in quarters, the wind must go
        /// before a flag gives up the share it holds (<see cref="ShareFor(float, float, float)"/>).
        /// A first guess: 0.3 of a quarter is 0.7 m/s of a 9.6 m/s breeze.
        /// </summary>
        public const float ShareHold = 0.3f;

        /// <summary>
        /// <see cref="ShareFor(float, float)"/> for a flag that already holds the share
        /// <paramref name="held"/> (2026-11-12): kept until the wind is <see cref="ShareHold"/>
        /// of a quarter past the halfway point to the next, then the nearest. Without it, a flag in
        /// Volumetric Weather's gusts near halfway changed loops by turns, each a new bake, the
        /// last one thrown away. A held share that is no quarter (0: none yet) holds nothing.
        /// </summary>
        public static float ShareFor(float speed, float breeze, float held)
        {
            if (!(breeze > 1e-3f))
                return 1f;

            float steps = held * ShareSteps;
            int h = (int)Math.Round(steps);
            if (h < 1 || h > ShareSteps || Math.Abs(steps - h) > 1e-3f)
                return ShareFor(speed, breeze);

            float ratio = ShareSteps * speed / breeze;
            float reach = 0.5f + ShareHold;
            bool below = h > 1 && ratio < h - reach;
            bool above = h < ShareSteps && ratio > h + reach;
            return below || above ? ShareFor(speed, breeze) : held;
        }

        /// <summary>
        /// The two frames a flag shows at <paramref name="clock"/> seconds and how far between
        /// them, 0..1: its loop of <paramref name="frames"/> frames <paramref name="frameTime"/>
        /// apart, begun at its own <see cref="Phase"/>, going round for ever (the last frame is
        /// followed by the first).
        /// </summary>
        public static void FrameAt(double clock, uint prop, int frames, float frameTime, out int a, out int b, out float blend)
        {
            if (frames < 2 || frameTime <= 0f)
            {
                a = b = 0;
                blend = 0f;
                return;
            }

            double at = clock / frameTime + Phase(prop) * frames;
            at -= Math.Floor(at / frames) * frames;
            int i = (int)Math.Floor(at);
            if (i >= frames)
                i = frames - 1;
            if (i < 0)
                i = 0;
            a = i;
            b = i + 1 < frames ? i + 1 : 0;
            blend = (float)(at - i);
            if (blend < 0f) blend = 0f;
            if (blend > 1f) blend = 1f;
        }

        /// <summary>
        /// Drawn cells along a looping flag's longer side for its size on the screen: cells of
        /// about <paramref name="pixelsPerCell"/>, one of <see cref="DrawnCells"/>, no finer than
        /// <paramref name="finest"/>. Every vertex is laid on the same smooth surface (the shader's
        /// patch), so a coarser grid is the same flag with fewer vertices: a city's far flags are a
        /// few dozen vertices each.
        /// </summary>
        public static int CellsFor(float pixels, float pixelsPerCell, int finest)
        {
            float want = pixels / Math.Max(pixelsPerCell, 0.5f);
            int[] steps = DrawnCells;
            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i] >= want || steps[i] >= finest || i == steps.Length - 1)
                    return Math.Min(steps[i], Math.Max(finest, steps[0]));
            }

            return steps[0];
        }

        public static readonly int[] DrawnCells = { 4, 6, 8, 12, 16, 24, 32, 48, 64, 96, 128 };
    }

    /// <summary>
    /// A made loop's frames on our side (2026-11-11; 1-2 MB a loop): each particle's place in each
    /// frame, as the texture holds them (LoopBake.Data, kept). A loop that stands in for a waving
    /// flag (FlagRenderer.PlanStandIn) begins at the frame nearest its cloth's pose
    /// (<see cref="Nearest"/>) and hands its pose and motion back to a new cloth
    /// (<see cref="Motion"/>, FlagCloth.SetMotion), so neither swap shows. PURE.
    /// </summary>
    public sealed class LoopFrames
    {
        public readonly int Columns, Rows, Frames, TilesX;
        public readonly float FrameTime;

        /// <param name="bake">Finished, not failed; its Data is kept here (not copied).</param>
        public LoopFrames(LoopBake bake)
        {
            Columns = bake.Columns;
            Rows = bake.Rows;
            Frames = bake.Frames;
            TilesX = bake.TilesX;
            FrameTime = bake.FrameTime;
            _data = bake.Data;
            _width = bake.Width;
        }

        private readonly float[] _data;
        private readonly int _width;

        private int Texel(int frame, int k)
        {
            int tx = frame % TilesX, ty = frame / TilesX;
            int i = k % Columns, j = k / Columns;
            return ((ty * Rows + j) * _width + tx * Columns + i) * 4;
        }

        /// <summary>The cloth has this loop's grid: a particle of it for each of the loop's.</summary>
        public bool Fits(FlagCloth cloth)
        {
            return cloth != null && _data != null && cloth.Columns == Columns && cloth.Rows == Rows;
        }

        /// <summary>
        /// The loop as it is drawn at <paramref name="clock"/> for the flag of <paramref name="prop"/>
        /// (<see cref="Bind"/>'s two frames and blend), in the flag's own frame (heading 0): each
        /// particle's place and its velocity there (the next frame less this one over a frame's
        /// time: the motion the shader shows), into arrays of Columns x Rows.
        /// </summary>
        public void Motion(double clock, uint prop, float[] x, float[] y, float[] z, float[] vx, float[] vy, float[] vz)
        {
            int a, b;
            float blend;
            LoopMotion.FrameAt(clock, prop, Frames, FrameTime, out a, out b, out blend);
            float per = FrameTime > 0f ? 1f / FrameTime : 0f;
            int count = Columns * Rows;
            for (int k = 0; k < count; k++)
            {
                int da = Texel(a, k), db = Texel(b, k);
                float ax = _data[da], ay = _data[da + 1], az = _data[da + 2];
                float bx = _data[db], by = _data[db + 1], bz = _data[db + 2];
                x[k] = ax + (bx - ax) * blend;
                y[k] = ay + (by - ay) * blend;
                z[k] = az + (bz - az) * blend;
                vx[k] = (bx - ax) * per;
                vy[k] = (by - ay) * per;
                vz[k] = (bz - az) * per;
            }
        }

        /// <summary>Points of the cloth weighed by <see cref="Nearest"/>: a few down the fly's end, the middle and a quarter along.</summary>
        private const int NearRows = 5;
        private static readonly float[] NearAlong = { 1f, 0.5f, 0.25f };

        /// <summary>A velocity's weight against a place's in <see cref="Nearest"/>, seconds: the frame that is where the cloth will be this much later counts as near as one where it is.</summary>
        private const float NearSeconds = 0.15f;

        /// <summary>
        /// The frame nearest a cloth's pose: <paramref name="cloth"/> (any grid of the same flag)
        /// turned back by its heading <paramref name="radians"/> into the flag's own frame, a few
        /// of its particles' places and velocities against each frame's (the frame's velocity:
        /// to the next one). A swap from the cloth to the loop begins there (LoopMotion.ShiftTo).
        /// </summary>
        public int Nearest(FlagCloth cloth, float radians)
        {
            if (_data == null || cloth == null || Frames < 2)
                return 0;

            float c = (float)Math.Cos(-radians), s = (float)Math.Sin(-radians);
            int n = NearRows * NearAlong.Length;
            var mine = new int[n];
            var px = new float[n]; var py = new float[n]; var pz = new float[n];
            var qx = new float[n]; var qy = new float[n]; var qz = new float[n];
            int m = 0;
            foreach (float along in NearAlong)
            {
                for (int r = 0; r < NearRows; r++)
                {
                    float down = r / (float)(NearRows - 1);
                    int k = (int)Math.Round(down * (cloth.Rows - 1)) * cloth.Columns + (int)Math.Round(along * (cloth.Columns - 1));
                    mine[m] = (int)Math.Round(down * (Rows - 1)) * Columns + (int)Math.Round(along * (Columns - 1));
                    float x = cloth.X[k], z = cloth.Z[k];
                    px[m] = x * c - z * s; py[m] = cloth.Y[k]; pz[m] = x * s + z * c;
                    x = cloth.Vx[k]; z = cloth.Vz[k];
                    qx[m] = (x * c - z * s) * NearSeconds; qy[m] = cloth.Vy[k] * NearSeconds; qz[m] = (x * s + z * c) * NearSeconds;
                    m++;
                }
            }

            int best = 0;
            float least = float.MaxValue;
            float lead = FrameTime > 0f ? NearSeconds / FrameTime : 0f;
            for (int f = 0; f < Frames; f++)
            {
                int next = f + 1 < Frames ? f + 1 : 0;
                float cost = 0f;
                for (int i = 0; i < n && cost < least; i++)
                {
                    int da = Texel(f, mine[i]), db = Texel(next, mine[i]);
                    float ax = _data[da], ay = _data[da + 1], az = _data[da + 2];
                    float dx = px[i] - ax, dy = py[i] - ay, dz = pz[i] - az;
                    float ex = qx[i] - (_data[db] - ax) * lead, ey = qy[i] - (_data[db + 1] - ay) * lead, ez = qz[i] - (_data[db + 2] - az) * lead;
                    cost += dx * dx + dy * dy + dz * dz + ex * ex + ey * ey + ez * ez;
                }

                if (cost < least)
                {
                    least = cost;
                    best = f;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// The making of one loop: a cloth of the flag's shape in the flag's own frame (heading 0, the
    /// anchor at the origin), laid out and flown as a still flag's is (StillPose) until it has
    /// settled, then recorded a frame every few steps; the two moments of the record that match
    /// best a loop's length apart are found, and the loop cut there, its end blended into the
    /// moments before its start. PURE, and the cloth thread's work (IClothWork): the renderer hands
    /// it some steps a frame (<see cref="StepsNext"/>) as a job; the preview and the tests make it
    /// in one go (<see cref="Make"/>) -- the same steps, the same loop.
    /// </summary>
    /// <remarks>
    /// THE SEAM, after Schoedl, Szeliski, Salesin and Essa, "Video Textures" (SIGGRAPH 2000): a
    /// cloth never repeats, so a loop needs a transition from a frame i to an earlier frame j that
    /// the eye takes for the next frame -- where frame i is like frame j - 1, not in one frame
    /// only but over a window of them (their filtered distance: the same shape moving the same
    /// way), and the residue cross-faded. Here the window is the cross-fade itself: for a loop of
    /// N frames starting at record frame a, its last B frames are blended with the B frames before
    /// a (which lead into a as the loop's end must), and the cost is the cloth's mean squared
    /// distance between the two strands weighted by how much of both is on screen at once. Every
    /// start within <see cref="LoopMotion.Choice"/> and every length within
    /// <see cref="LoopMotion.Shortest"/> .. <see cref="LoopMotion.Longest"/> is tried; the distances
    /// are taken on a sample of the particles.
    ///
    /// A blend of two cloth shapes is not a cloth shape (its links are a little short where the two
    /// differ); with the best match the two differ by centimetres, and the tests hold the loop's
    /// links to the record's.
    ///
    /// A cloth found knotted while it is recorded (FlagCloth lays it out afresh) is settled and
    /// recorded again, <see cref="MaxTries"/> times; one that is not numbers fails
    /// (<see cref="Failed"/>: the flag waves instead).
    /// </remarks>
    public sealed class LoopBake : IClothWork
    {
        public const int MaxTries = 3;

        /// <summary>At most this many particles each way are compared for the seam.</summary>
        private const int SampleColumns = 12, SampleRows = 8;

        public readonly FlagCloth Cloth;
        public readonly int Columns, Rows, Count;

        /// <summary>Seconds between frames, the steps between them, and the breeze (m/s at the flag, and its way).</summary>
        public readonly float FrameTime;
        public readonly int FrameSteps;
        public readonly float Speed, DirX, DirZ;

        /// <summary>The fly at rest in the flag's own frame (FlagOrientation at heading 0).</summary>
        public readonly float FlyX, FlyY, FlyZ;

        /// <summary>The steps the renderer gives its next job (set on the main thread before it is queued).</summary>
        public int StepsNext;

        /// <summary>Done: the loop is in <see cref="Data"/> (or it <see cref="Failed"/>, <see cref="Problem"/> saying why).</summary>
        public bool Finished { get; private set; }
        public bool Failed { get; private set; }
        public string Problem { get; private set; }

        /// <summary>Steps run so far, in all tries; how often it began again.</summary>
        public int StepsRun { get; private set; }
        public int Tries { get; private set; }

        // ---- the loop ----

        /// <summary>The loop's frames, and its length in seconds.</summary>
        public int Frames { get; private set; }

        public float Seconds
        {
            get { return Frames * FrameTime; }
        }

        /// <summary>
        /// The frames as the texture takes them: a tile of Columns x Rows texels a frame,
        /// <see cref="TilesX"/> tiles a row, rows from the texture's first (frame f at tile
        /// f % TilesX, f / TilesX); a texel a particle, its place relative to the anchor (x, y,
        /// z, 1) -- what FlagMesh's own texture holds for one moment. <see cref="Raw"/>: the same
        /// as bytes, for the texture.
        /// </summary>
        public float[] Data;
        public byte[] Raw;
        public int TilesX { get; private set; }
        public int TilesY { get; private set; }
        public int Width { get; private set; }
        public int Height { get; private set; }

        /// <summary>The box of every frame (the drawn sheet's, with the surface's bulge between particles), relative to the anchor.</summary>
        public float MinX, MinY, MinZ, MaxX, MaxY, MaxZ;

        /// <summary>
        /// The seam, for the log and the tests: the record frame the loop starts at, the rms
        /// distance of the two strands blended (metres, weighted as the blend shows them), the rms
        /// distance the cloth moves in a frame, and from the loop's last frame to its first.
        /// </summary>
        public int SeamStart { get; private set; }
        public float SeamGap { get; private set; }
        public float FrameMove { get; private set; }
        public float WrapMove { get; private set; }

        private readonly float _waviness, _length, _fieldX, _fieldZ;
        private readonly bool _smoothEdges;
        private readonly int _settleSteps, _blend, _minFrames, _maxFrames, _recordFrames;
        private readonly float[] _rx, _ry, _rz;
        private int _settleLeft, _recorded, _sinceFrame;
        private int _unknotted, _relaid;

        /// <summary>The most steps of one try run so far (<see cref="Progress"/>): one int, written by the job alone.</summary>
        private volatile int _shownSteps;

        /// <param name="spec">The flag: its size, cut, hoist line, fabric, halyard, back plate, share of the wind.</param>
        /// <param name="detail">The cloth's detail (LoopMotion.DetailFor).</param>
        /// <param name="selfCollision">The option, as every cloth has it.</param>
        /// <param name="smoothEdges">The frames' edges smoothed as a drawn flag's are (ClothSurface.SmoothEdges).</param>
        public LoopBake(FlagSpec spec, int detail, bool selfCollision, bool smoothEdges = true)
        {
            float hx, hy, hz, fx, fy, fz;
            FlagOrientation.Frame(0f, spec.Tilt, spec.Lean, spec.HoistedFrom, out hx, out hy, out hz, out fx, out fy, out fz);
            float dirX, dirZ, speed;
            LoopMotion.Wind(hx, hz, fx, fy, fz, spec.Length, spec.Height, spec.WindShare, out dirX, out dirZ, out speed);
            DirX = dirX;
            DirZ = dirZ;
            Speed = speed;
            FlyX = fx;
            FlyY = fy;
            FlyZ = fz;
            _waviness = spec.Waviness;
            _length = spec.Length;
            _smoothEdges = smoothEdges;

            // Its take (FlagSpec.Take): where in the breeze's eddies it is flown, as a still flag's.
            StillPose.FieldFor(spec.Take, out _fieldX, out _fieldZ);

            Cloth = new FlagCloth(spec.Length, spec.Height, FlagFabric.Standard(spec.Waviness), fx, fz, detail, hx, hy, hz, spec.Cut, spec.CutDepth);
            Cloth.SelfCollision = selfCollision;
            Cloth.SetHalyard(spec.OnHalyard);

            // Its back plate (FlagRenderer.ApplyPlate): the front is the flag's -Z.
            if (spec.HasBackPlate)
                Cloth.SetPlate(hz, -hx);

            // Its held edges, along its fly at rest (before it is laid out: laid where they hold it).
            Cloth.SetHolds(spec.HeldNow, fx, fy, fz);
            Cloth.LayOut(fx, fy, fz, true);

            Columns = Cloth.Columns;
            Rows = Cloth.Rows;
            Count = Cloth.Count;

            // The frames go by the flag's longer side: a banner's waves are its hanging length's.
            float size = Math.Max(spec.Length, spec.Height);
            FrameSteps = LoopMotion.FrameSteps(size);
            FrameTime = FrameSteps * LoopMotion.StepSize;
            int aim = Math.Max(8, (int)Math.Round(LoopMotion.Seconds(size) / FrameTime));
            _blend = Math.Max(2, (int)Math.Round(LoopMotion.BlendSeconds * LoopMotion.Seconds(size) / LoopMotion.BaseSeconds / FrameTime));
            _minFrames = Math.Max(_blend + 2, (int)Math.Round(aim * LoopMotion.Shortest));
            _maxFrames = Math.Max(_minFrames, (int)Math.Round(aim * LoopMotion.Longest));
            _recordFrames = _blend + _maxFrames + Math.Max(1, (int)Math.Round(aim * LoopMotion.Choice));

            // As long as a still flag's shape is run for before it is caught (StillPose.StepsFor), in this step.
            float settle = StillPose.StepsFor(size, Math.Max(speed, 1f)) * StillPose.StepSize;
            _settleSteps = (int)Math.Ceiling(settle / LoopMotion.StepSize);
            _settleLeft = _settleSteps;

            _rx = new float[_recordFrames * Count];
            _ry = new float[_recordFrames * Count];
            _rz = new float[_recordFrames * Count];
            _unknotted = Cloth.Unknotted;
            _relaid = Cloth.Relaid;
            Tries = 1;
        }

        /// <summary>The steps of one try: its settling and its record.</summary>
        public int StepsTotal
        {
            get { return _settleSteps + _recordFrames * FrameSteps; }
        }

        /// <summary>
        /// How far it is, 0..1, for the panel's Recapture button (the author, 2026-11-12: "show a
        /// 'loading' state on a flag when it's set to Loop motion as it generates the motion"): the
        /// steps of a try run so far over a try's, below 1 until the loop is made (its seam is cut
        /// after the last frame), 1 once it is (one that failed stays where it stopped: the renderer
        /// says so the next frame, LoopState.Failed). It never runs back: a try begun again after a knot
        /// holds where the last one had got to until the new one passes it. Read from the main
        /// thread while the job may be running: it reads one int the job writes, nothing else of it.
        /// </summary>
        public float Progress
        {
            get
            {
                if (Finished && !Failed)
                    return 1f;
                float p = _shownSteps / (float)Math.Max(1, StepsTotal);
                return p < 0f ? 0f : p > 0.99f ? 0.99f : p;
            }
        }

        /// <summary>The sub-steps each step takes (the cost of a step, for the renderer's plan).</summary>
        public int SubSteps
        {
            get { return Cloth.SubStepsFor(FlagWind.ClothSpeed(Speed, StillPose.Height), LoopMotion.StepSize); }
        }

        /// <summary>The whole loop, here and now (the preview, the tests).</summary>
        public static LoopBake Make(FlagSpec spec, int detail, bool selfCollision, bool smoothEdges = true)
        {
            var bake = new LoopBake(spec, detail, selfCollision, smoothEdges);
            while (!bake.Finished)
            {
                bake.StepsNext = 1 << 20;
                bake.Run();
            }

            return bake;
        }

        /// <summary>
        /// <see cref="StepsNext"/> steps of it (the cloth thread's job); once the record is whole,
        /// the seam and the loop. Returns the work done, in particle steps.
        /// </summary>
        public long Run()
        {
            if (Finished)
                return 0;

            long work = 0;
            int left = Math.Max(1, StepsNext);
            while (left > 0 && _recorded < _recordFrames)
            {
                // Never past the next frame to keep (or the settling's end).
                int n = _settleLeft > 0 ? Math.Min(left, _settleLeft) : Math.Min(left, FrameSteps - _sinceFrame);
                var job = new ClothJob
                {
                    Cloth = Cloth,
                    Steps = n,
                    Size = LoopMotion.StepSize,
                    Time = StepsRun * LoopMotion.StepSize,
                    Alpha = 1f,
                    Speed = Speed,
                    Gusts = 0f,
                    DirX = DirX,
                    DirZ = DirZ,
                    Height = StillPose.Height,
                    Waviness = _waviness,
                    Length = _length,
                    AnchorX = _fieldX,
                    AnchorZ = _fieldZ,
                };
                work += ClothWorker.Execute(ref job);
                StepsRun += n;
                left -= n;

                if (job.WentWrong)
                {
                    Fail("its cloth stopped being numbers");
                    return work;
                }

                // Knotted and laid out afresh (or buried: never here): what was recorded has a jump in it.
                if (Cloth.Unknotted != _unknotted || Cloth.Relaid != _relaid)
                {
                    _unknotted = Cloth.Unknotted;
                    _relaid = Cloth.Relaid;
                    if (Tries >= MaxTries)
                    {
                        Fail("its cloth knotted " + MaxTries + " times while it was recorded");
                        return work;
                    }

                    Tries++;
                    _settleLeft = _settleSteps;
                    _recorded = 0;
                    _sinceFrame = 0;
                    continue;
                }

                if (_settleLeft > 0)
                {
                    _settleLeft -= n;
                    continue;
                }

                _sinceFrame += n;
                if (_sinceFrame >= FrameSteps)
                {
                    _sinceFrame = 0;
                    int o = _recorded * Count;
                    Array.Copy(Cloth.X, 0, _rx, o, Count);
                    Array.Copy(Cloth.Y, 0, _ry, o, Count);
                    Array.Copy(Cloth.Z, 0, _rz, o, Count);
                    _recorded++;
                }
            }

            // This try's steps so far, for Progress: kept at the most any try reached.
            int done = (_settleSteps - Math.Max(0, _settleLeft)) + _recorded * FrameSteps + _sinceFrame;
            if (done > _shownSteps)
                _shownSteps = done;

            if (_recorded >= _recordFrames)
            {
                Cut();
                work += (long)_recordFrames * Count / ClothWorker.PlayShare;
            }

            return work;
        }

        private void Fail(string why)
        {
            Failed = true;
            Finished = true;
            Problem = why;
        }

        /// <summary>The job threw (the thread kept what): the loop is not made.</summary>
        public void Threw(Exception e)
        {
            Fail("making it threw " + (e != null ? e.GetType().Name + ": " + e.Message : "an error"));
        }

        /// <summary>The seam found, the loop cut and laid into <see cref="Data"/>.</summary>
        private void Cut()
        {
            int n = Count, B = _blend, R = _recordFrames;

            // The particles compared: a grid of them over the cloth, its cloth only.
            int si = Math.Max(1, (Columns - 1) / SampleColumns), sj = Math.Max(1, (Rows - 1) / SampleRows);
            int samples = 0;
            var sample = new int[((Columns - 1) / si + 1) * ((Rows - 1) / sj + 1)];
            for (int j = 0; j < Rows; j += sj)
            {
                for (int i = 0; i < Columns; i += si)
                {
                    int k = j * Columns + i;
                    if (Cloth.IsCloth(k))
                        sample[samples++] = k;
                }
            }

            if (samples == 0)
            {
                Fail("its cloth has no particle to compare");
                return;
            }

            // How much of both strands shows at once through the blend: s (1 - s) of the smooth step.
            var weight = new double[B + 1];
            double weights = 0.0;
            for (int k = 1; k <= B; k++)
            {
                weight[k] = Smooth((B + 1 - k) / (float)(B + 1));
                weight[k] *= 1.0 - weight[k];
                weights += weight[k];
            }

            int spans = _maxFrames - _minFrames + 1;
            var apart = new float[R];
            double best = double.MaxValue;
            int bestStart = B, bestFrames = _minFrames;
            for (int s = 0; s < spans; s++)
            {
                int N = _minFrames + s;

                // Frame i against frame i + N, the mean squared distance of the sample.
                int last = R - N - 1;
                for (int i = 0; i <= last; i++)
                    apart[i] = (float)Apart(i, i + N, sample, samples);

                for (int a = B; a <= R - N; a++)
                {
                    double cost = 0.0;
                    for (int k = 1; k <= B; k++)
                        cost += weight[k] * apart[a - k];
                    if (cost < best)
                    {
                        best = cost;
                        bestStart = a;
                        bestFrames = N;
                    }
                }
            }

            if (double.IsNaN(best) || best == double.MaxValue)
            {
                Fail("no seam could be found in its record");
                return;
            }

            Frames = bestFrames;
            SeamStart = bestStart;
            SeamGap = (float)Math.Sqrt(best / Math.Max(weights, 1e-12));

            // The texture's tiles: about square.
            TilesX = Math.Max(1, (int)Math.Ceiling(Math.Sqrt((double)Frames * Rows / Columns)));
            TilesY = (Frames + TilesX - 1) / TilesX;
            Width = TilesX * Columns;
            Height = TilesY * Rows;
            Data = new float[Width * Height * 4];

            var x = new float[n];
            var y = new float[n];
            var z = new float[n];
            var scratch = new float[3 * Math.Max(Columns, Rows)];
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            for (int f = 0; f < Frames; f++)
            {
                int o = (bestStart + f) * n;
                int into = f - (Frames - B);
                if (into < 0)
                {
                    Array.Copy(_rx, o, x, 0, n);
                    Array.Copy(_ry, o, y, 0, n);
                    Array.Copy(_rz, o, z, 0, n);
                }
                else
                {
                    // Its end, more and more the moments before its start: the last frame is all but the one before the first.
                    float t = Smooth((into + 1) / (float)(B + 1)), keep = 1f - t;
                    int e = (bestStart + f - Frames) * n;
                    for (int k = 0; k < n; k++)
                    {
                        x[k] = keep * _rx[o + k] + t * _rx[e + k];
                        y[k] = keep * _ry[o + k] + t * _ry[e + k];
                        z[k] = keep * _rz[o + k] + t * _rz[e + k];
                    }
                }

                if (_smoothEdges)
                    ClothSurface.SmoothEdges(x, y, z, Columns, Rows, scratch);

                int tx = f % TilesX, ty = f / TilesX;
                for (int j = 0; j < Rows; j++)
                {
                    int d = ((ty * Rows + j) * Width + tx * Columns) * 4;
                    for (int i = 0; i < Columns; i++, d += 4)
                    {
                        int k = j * Columns + i;
                        float px = x[k], py = y[k], pz = z[k];
                        Data[d] = px;
                        Data[d + 1] = py;
                        Data[d + 2] = pz;
                        Data[d + 3] = 1f;
                        if (px < minX) minX = px;
                        if (px > maxX) maxX = px;
                        if (py < minY) minY = py;
                        if (py > maxY) maxY = py;
                        if (pz < minZ) minZ = pz;
                        if (pz > maxZ) maxZ = pz;
                    }
                }
            }

            // As FlagMesh's texture path: the spline bulges past the particles between them.
            float margin = 0.05f + 0.25f * Math.Max(Cloth.CellU, Cloth.CellV);
            MinX = minX - margin; MinY = minY - margin; MinZ = minZ - margin;
            MaxX = maxX + margin; MaxY = maxY + margin; MaxZ = maxZ + margin;

            // For the log and the tests: a frame's move, and the move over the seam.
            double moved = 0.0;
            for (int f = 0; f + 1 < Frames; f++)
                moved += Math.Sqrt(Apart(bestStart + f, bestStart + f + 1, sample, samples));
            FrameMove = (float)(moved / Math.Max(1, Frames - 1));
            WrapMove = (float)Math.Sqrt(FramesApart(Frames - 1, 0, sample, samples));

            Raw = new byte[Data.Length * 4];
            Buffer.BlockCopy(Data, 0, Raw, 0, Raw.Length);
            Finished = true;
        }

        private static float Smooth(float t)
        {
            return t * t * (3f - 2f * t);
        }

        /// <summary>Record frames <paramref name="a"/> and <paramref name="b"/>: the sample's mean squared distance.</summary>
        private double Apart(int a, int b, int[] sample, int samples)
        {
            int oa = a * Count, ob = b * Count;
            double sum = 0.0;
            for (int s = 0; s < samples; s++)
            {
                int k = sample[s];
                float dx = _rx[oa + k] - _rx[ob + k], dy = _ry[oa + k] - _ry[ob + k], dz = _rz[oa + k] - _rz[ob + k];
                sum += dx * dx + dy * dy + dz * dz;
            }

            return sum / samples;
        }

        /// <summary>The same of two of the loop's own frames.</summary>
        private double FramesApart(int a, int b, int[] sample, int samples)
        {
            double sum = 0.0;
            for (int s = 0; s < samples; s++)
            {
                int k = sample[s];
                int da = Texel(a, k), db = Texel(b, k);
                float dx = Data[da] - Data[db], dy = Data[da + 1] - Data[db + 1], dz = Data[da + 2] - Data[db + 2];
                sum += dx * dx + dy * dy + dz * dz;
            }

            return sum / samples;
        }

        /// <summary>Where particle <paramref name="k"/> of loop frame <paramref name="frame"/> is in <see cref="Data"/>.</summary>
        public int Texel(int frame, int k)
        {
            int tx = frame % TilesX, ty = frame / TilesX;
            int i = k % Columns, j = k / Columns;
            return ((ty * Rows + j) * Width + tx * Columns + i) * 4;
        }

        /// <summary>One frame of the loop, the particles' places relative to the anchor.</summary>
        public void Frame(int frame, float[] x, float[] y, float[] z)
        {
            for (int k = 0; k < Count; k++)
            {
                int d = Texel(frame, k);
                x[k] = Data[d];
                y[k] = Data[d + 1];
                z[k] = Data[d + 2];
            }
        }

        /// <summary>
        /// The loop between two frames, as the shader shows it: frame <paramref name="a"/> towards
        /// <paramref name="b"/> by <paramref name="blend"/>.
        /// </summary>
        public void Between(int a, int b, float blend, float[] x, float[] y, float[] z)
        {
            float keep = 1f - blend;
            for (int k = 0; k < Count; k++)
            {
                int da = Texel(a, k), db = Texel(b, k);
                x[k] = keep * Data[da] + blend * Data[db];
                y[k] = keep * Data[da + 1] + blend * Data[db + 1];
                z[k] = keep * Data[da + 2] + blend * Data[db + 2];
            }
        }

        /// <summary>
        /// The loop's extents along three axes (unit, in the flag's own frame) over every frame,
        /// its cloth only: the pick volume's slab (FlagRenderer.PickVolumes), which a waving
        /// flag's cloth gives every tenth of a second.
        /// </summary>
        public void Extents(float ux, float uy, float uz, float vx, float vy, float vz, float nx, float ny, float nz,
                            out float uMin, out float uMax, out float vMin, out float vMax, out float nMin, out float nMax)
        {
            uMin = vMin = nMin = float.MaxValue;
            uMax = vMax = nMax = float.MinValue;
            for (int f = 0; f < Frames; f++)
            {
                for (int k = 0; k < Count; k++)
                {
                    if (!Cloth.IsCloth(k))
                        continue;

                    int d = Texel(f, k);
                    float x = Data[d], y = Data[d + 1], z = Data[d + 2];
                    float a = x * ux + y * uy + z * uz, b = x * vx + y * vy + z * vz, c = x * nx + y * ny + z * nz;
                    if (a < uMin) uMin = a;
                    if (a > uMax) uMax = a;
                    if (b < vMin) vMin = b;
                    if (b > vMax) vMax = b;
                    if (c < nMin) nMin = c;
                    if (c > nMax) nMax = c;
                }
            }
        }

        /// <summary>For the log.</summary>
        public string Describe()
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            if (Failed)
                return "not made: " + Problem;
            return Frames + " frames of 1/" + ((int)Math.Round(1f / FrameTime)).ToString(inv) + " s (" + Seconds.ToString("F2", inv) + " s), " + Columns + "x" + Rows +
                   " particles in a " + Width + "x" + Height + " texture (" + (Raw != null ? Raw.Length / 1048576f : 0f).ToString("F1", inv) + " MB); flown in " +
                   Speed.ToString("F1", inv) + " m/s, " + StepsRun + " steps of 1/64 s" + (Tries > 1 ? " in " + Tries + " tries" : "") + "; its seam: the two moments " +
                   (SeamGap * 100f).ToString("F1", inv) + " cm apart, the cloth moving " + (FrameMove * 100f).ToString("F1", inv) + " cm a frame (" +
                   (WrapMove * 100f).ToString("F1", inv) + " cm from the last frame to the first)";
        }
    }
}
