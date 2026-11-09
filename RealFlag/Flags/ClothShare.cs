using System;

namespace RealFlag.Flags
{
    /// <summary>
    /// SHARED MOTION (2026-10-31; the author: "add Shared motion as an option, it should be on by
    /// default"; chosen: only when the cloth threads are out of budget). Flags of one cloth --
    /// the same size, shape, waviness, hoist, ropes, tilt, lean, pole and detail, in about the
    /// same wind -- move alike, so when there is not time for each to be simulated, one hidden
    /// cloth (the group's SOURCE) is, its recent steps are kept here, and each flag that FOLLOWS
    /// it plays them back a fixed delay behind (its own, 0.3-2.7 s from its prop), turned to its
    /// heading: a row of fifty like flags costs about one, and no two wave in step. The idea is
    /// crowd animation's -- one animation played by many instances at their own time offsets
    /// (GPU Gems 3, ch. 2, "Animated Crowd Rendering", Dudash 2007) -- with the clip a ring of the
    /// source's live steps instead of a loop, so it has no seam. PURE: tested by test-cloth.
    /// </summary>
    /// <remarks>
    /// Threads: the source's job writes the newest slots while the followers' jobs of the same
    /// batch read older ones -- the slots each reads are chosen on the main thread between
    /// batches (<see cref="Find"/>), at least <see cref="MinDelay"/> back, and the ring holds
    /// <see cref="Capacity"/> steps, more than the longest delay and a batch's writes together, so
    /// no slot is written and read in one batch. The head and the times are read on the main
    /// thread only between batches.
    /// </remarks>
    public sealed class MotionRecord
    {
        /// <summary>Steps kept: 3.2 s at 60 Hz, past the longest delay with a frame's catching up on top.</summary>
        public const int Capacity = 192;

        /// <summary>A follower plays the source this far behind, seconds, at least and at most (by its prop: <see cref="DelayFor"/>).</summary>
        public const float MinDelay = 0.3f, MaxDelay = 2.7f;

        /// <summary>The record is ready for followers once it spans this long (the source runs ahead to fill it: its warm-up).</summary>
        public const float ReadySpan = MaxDelay + 0.1f;

        public readonly int Columns, Rows, Count;

        private readonly float[] _x, _y, _z;
        private readonly float[] _time = new float[Capacity];
        private int _head = -1, _filled;

        public MotionRecord(int columns, int rows)
        {
            Columns = columns;
            Rows = rows;
            Count = columns * rows;
            _x = new float[Capacity * Count];
            _y = new float[Capacity * Count];
            _z = new float[Capacity * Count];
        }

        /// <summary>Steps kept.</summary>
        public int Filled
        {
            get { return _filled; }
        }

        /// <summary>The newest step's time, and the oldest's (the source's own clock).</summary>
        public float Newest
        {
            get { return _filled > 0 ? _time[_head] : 0f; }
        }

        public float Oldest
        {
            get { return _filled > 0 ? _time[(_head - _filled + 1 + Capacity) % Capacity] : 0f; }
        }

        /// <summary>The time the record spans, seconds.</summary>
        public float Span
        {
            get { return _filled > 1 ? Newest - Oldest : 0f; }
        }

        /// <summary>The cloth's places after a step ending at <paramref name="time"/> (its own frame: anchor-relative, world axes).</summary>
        public void Add(FlagCloth cloth, float time)
        {
            int slot = (_head + 1) % Capacity;
            int o = slot * Count;
            Array.Copy(cloth.X, 0, _x, o, Count);
            Array.Copy(cloth.Y, 0, _y, o, Count);
            Array.Copy(cloth.Z, 0, _z, o, Count);
            _time[slot] = time;
            _head = slot;
            if (_filled < Capacity)
                _filled++;
        }

        /// <summary>Forgets every step (the source went wrong and was laid out again): its followers wait until it is ready.</summary>
        public void Clear()
        {
            _head = -1;
            _filled = 0;
        }

        /// <summary>
        /// The two steps round <paramref name="time"/> (slots <paramref name="a"/> and
        /// <paramref name="b"/>, b the later) and how far between them, 0..1; held to the steps kept
        /// (none older than the oldest, none newer than the newest). The seconds between them in
        /// <paramref name="span"/>. False with fewer than two kept.
        /// </summary>
        public bool Find(float time, out int a, out int b, out float w, out float span)
        {
            a = b = -1;
            w = 0f;
            span = 0f;
            if (_filled < 2)
                return false;

            // Back from the newest: delays are short against the ring, so this walks few.
            int newer = _head;
            for (int n = 1; n < _filled; n++)
            {
                int older = (_head - n + Capacity) % Capacity;
                if (_time[older] <= time || n == _filled - 1)
                {
                    a = older;
                    b = newer;
                    span = _time[b] - _time[a];
                    w = span > 1e-6f ? (time - _time[a]) / span : 1f;
                    w = w < 0f ? 0f : w > 1f ? 1f : w;
                    return true;
                }

                newer = older;
            }

            return false;
        }

        /// <summary>
        /// The follower's cloth takes steps <paramref name="a"/> (its places before) and
        /// <paramref name="b"/> (its places), turned (cos, sin) about the vertical through the
        /// anchor, and the velocity between them -- its mesh then drawn between the two at the
        /// time's share (FlagMesh.Refresh). Blended from <paramref name="fromX"/>.. by
        /// <paramref name="blend"/> &lt; 1 (the first half second of following: from the shape it
        /// had). The held particles come with the source's, on the follower's own hoist line (the
        /// same line turned: the key holds the hoist, the turn is the heading's).
        /// </summary>
        public void Play(FlagCloth to, int a, int b, float span, float cos, float sin, float[] fromX, float[] fromY, float[] fromZ, float blend)
        {
            if (to.Columns != Columns || to.Rows != Rows)
            {
                PlayResampled(to, a, b, span, cos, sin, fromX, fromY, fromZ, blend);
                return;
            }

            int n = Count, oa = a * n, ob = b * n;
            float inv = span > 1e-6f ? 1f / span : 0f;
            bool blending = fromX != null && blend < 1f;
            float keep = 1f - blend;
            float[] X = to.X, Y = to.Y, Z = to.Z, PX = to.PrevX, PY = to.PrevY, PZ = to.PrevZ;
            for (int k = 0; k < n; k++)
            {
                float ax = _x[oa + k], ay = _y[oa + k], az = _z[oa + k];
                float bx = _x[ob + k], by = _y[ob + k], bz = _z[ob + k];
                float pax = ax * cos - az * sin, paz = ax * sin + az * cos;
                float pbx = bx * cos - bz * sin, pbz = bx * sin + bz * cos;

                to.Vx[k] = (pbx - pax) * inv;
                to.Vy[k] = (by - ay) * inv;
                to.Vz[k] = (pbz - paz) * inv;

                if (blending)
                {
                    pax = fromX[k] * keep + pax * blend; ay = fromY[k] * keep + ay * blend; paz = fromZ[k] * keep + paz * blend;
                    pbx = fromX[k] * keep + pbx * blend; by = fromY[k] * keep + by * blend; pbz = fromZ[k] * keep + pbz * blend;
                }

                PX[k] = pax; PY[k] = ay; PZ[k] = paz;
                X[k] = pbx; Y[k] = by; Z[k] = pbz;
            }
        }

        /// <summary>
        /// <see cref="Play"/> for a follower on another grid (another detail: since 2026-11-07 the
        /// detail is not in the key, so a far flag and a near one of one design share a source):
        /// each of its particles takes the source's places bilinearly at the same share along and
        /// down the cloth (both grids span the whole flag evenly: FlagCloth's CellU, CellV), the
        /// hoist column from the source's own. The shape is the source's; a finer follower's mesh
        /// is smooth through it (FlagMesh's refine), a coarser one drops the finest folds.
        /// </summary>
        private void PlayResampled(FlagCloth to, int a, int b, float span, float cos, float sin, float[] fromX, float[] fromY, float[] fromZ, float blend)
        {
            int cs = Columns, rs = Rows, ct = to.Columns, rt = to.Rows;
            int oa = a * Count, ob = b * Count;
            float su = ct > 1 ? (cs - 1f) / (ct - 1f) : 0f, sv = rt > 1 ? (rs - 1f) / (rt - 1f) : 0f;
            float inv = span > 1e-6f ? 1f / span : 0f;
            bool blending = fromX != null && blend < 1f;
            float keep = 1f - blend;
            float[] X = to.X, Y = to.Y, Z = to.Z, PX = to.PrevX, PY = to.PrevY, PZ = to.PrevZ;
            float[] sx = _x, sy = _y, sz = _z;
            for (int j = 0; j < rt; j++)
            {
                float v = j * sv;
                int j0 = Math.Min((int)v, rs - 2);
                float tv = v - j0;
                for (int i = 0; i < ct; i++)
                {
                    float u = i * su;
                    int i0 = Math.Min((int)u, cs - 2);
                    float tu = u - i0;
                    float w00 = (1f - tu) * (1f - tv), w10 = tu * (1f - tv), w01 = (1f - tu) * tv, w11 = tu * tv;
                    int s = j0 * cs + i0;
                    int a00 = oa + s, a10 = a00 + 1, a01 = a00 + cs, a11 = a01 + 1;
                    int b00 = ob + s, b10 = b00 + 1, b01 = b00 + cs, b11 = b01 + 1;

                    float ax = sx[a00] * w00 + sx[a10] * w10 + sx[a01] * w01 + sx[a11] * w11;
                    float ay = sy[a00] * w00 + sy[a10] * w10 + sy[a01] * w01 + sy[a11] * w11;
                    float az = sz[a00] * w00 + sz[a10] * w10 + sz[a01] * w01 + sz[a11] * w11;
                    float bx = sx[b00] * w00 + sx[b10] * w10 + sx[b01] * w01 + sx[b11] * w11;
                    float by = sy[b00] * w00 + sy[b10] * w10 + sy[b01] * w01 + sy[b11] * w11;
                    float bz = sz[b00] * w00 + sz[b10] * w10 + sz[b01] * w01 + sz[b11] * w11;

                    float pax = ax * cos - az * sin, paz = ax * sin + az * cos;
                    float pbx = bx * cos - bz * sin, pbz = bx * sin + bz * cos;
                    int k = j * ct + i;
                    to.Vx[k] = (pbx - pax) * inv;
                    to.Vy[k] = (by - ay) * inv;
                    to.Vz[k] = (pbz - paz) * inv;

                    if (blending)
                    {
                        pax = fromX[k] * keep + pax * blend; ay = fromY[k] * keep + ay * blend; paz = fromZ[k] * keep + paz * blend;
                        pbx = fromX[k] * keep + pbx * blend; by = fromY[k] * keep + by * blend; pbz = fromZ[k] * keep + pbz * blend;
                    }

                    PX[k] = pax; PY[k] = ay; PZ[k] = paz;
                    X[k] = pbx; Y[k] = by; Z[k] = pbz;
                }
            }
        }

        /// <summary>
        /// A follower's delay, seconds, from its prop: spread over MinDelay..MaxDelay by the golden
        /// ratio's sequence (Weyl), so props made one after another are far apart in it.
        /// </summary>
        public static float DelayFor(uint prop)
        {
            double f = prop * 0.6180339887498949;
            f -= Math.Floor(f);
            return MinDelay + (float)f * (MaxDelay - MinDelay);
        }
    }

    /// <summary>
    /// What makes two flags' cloth move alike: one group, one source. Lengths in millimetres,
    /// angles in tenths of a degree, the pole's numbers in 5 mm, the wind's speed in steps of
    /// <see cref="SpeedStep"/> and its angle against the flag's heading in steps of
    /// <see cref="UprightPoleAngleStep"/> (an upright hoist on a pole: a turned cloth is the same
    /// flag but for where the pole stands, a few centimetres behind the hoist),
    /// <see cref="TurnedAngleStep"/> (a tilted or top hoist, whose line the turn must keep), and
    /// none at all for an upright hoist with no pole. Colours, designs and pictures are not in it,
    /// nor the DETAIL (2026-11-07: a follower takes the source's shape on its own grid,
    /// MotionRecord.PlayResampled) -- the key once held it, and every change of detail (the load
    /// making the small flags coarser, a zoom) broke every group.
    /// </summary>
    /// <remarks>
    /// The steps only sort flags for a NEW group (a source is made for a key with two or more);
    /// which group a flag follows is decided by <see cref="Near"/>, within
    /// <see cref="SpeedTolerance"/> and the angle's tolerance of the group's own, so a flag on a
    /// step's edge, or a gust, does not change source. Until 2026-11-07 the key alone decided, in
    /// 10% and 15 deg steps of the wind of the moment: in the author's run (65 like flags in a
    /// roundabout, Volumetric Weather's gusts) the keys changed every few frames, 24 sources were
    /// made and dropped over and over, each running ahead 2.8 s before anyone could follow, and
    /// none was followed ("0 following 24 source(s)") while the flags froze waiting for room.
    /// </remarks>
    public struct ShareKey : IEquatable<ShareKey>
    {
        public const float UprightPoleAngleStep = 30f, TurnedAngleStep = 10f;

        /// <summary>A flag joins (or stays with) a group whose angle is within these of its own, degrees.</summary>
        public const float UprightPoleAngleTolerance = 25f, TurnedAngleTolerance = 7f;

        /// <summary>The wind's speed step: 25% (a bucket of ln 1.25); a flag joins a group within <see cref="SpeedTolerance"/> of its speed.</summary>
        public const double SpeedStep = 1.25;
        public const float SpeedTolerance = 0.3f;

        public int Length, Proportion, HoistedFrom, Tilt, Lean, Waviness, Pole, Speed, Angle;
        public bool Halyard, SelfCollision;

        /// <summary>Kept in front of a back plate (FlagSpec.HasBackPlate): it cannot fly the other way, so it is another cloth.</summary>
        public bool Plate;

        /// <summary>The cut (FlagCut) and its depth in thousandths where it has one: another cut is another cloth.</summary>
        public int Cut;

        /// <summary>
        /// The key of a flag of <paramref name="spec"/> in a wind of <paramref name="speed"/> m/s at
        /// the flag blowing <paramref name="relativeDegrees"/> from its heading; its pole's offset
        /// and radius at the hoist (0 for none).
        /// </summary>
        public static ShareKey For(FlagSpec spec, float speed, float relativeDegrees, float poleOffset, float poleRadius, bool selfCollision)
        {
            bool pole = poleRadius > 0f;
            float step = AngleStep(Upright(spec), pole);
            float rel = Wrap(relativeDegrees);

            return new ShareKey
            {
                Length = (int)Math.Round(spec.Length * 1000f),
                Proportion = spec.Proportion,
                HoistedFrom = spec.HoistedFrom,
                Tilt = (int)Math.Round(spec.Tilt * 10f),
                Lean = (int)Math.Round(spec.Lean * 10f),
                Waviness = (int)Math.Round(spec.Waviness * 1000f),
                Pole = pole ? 1 + (int)Math.Round(poleOffset * 200f) * 4096 + (int)Math.Round(poleRadius * 200f) : 0,
                Speed = (int)Math.Round(Math.Log(Math.Max(speed, 0.3f)) / Math.Log(SpeedStep)),
                Angle = step > 0f ? (int)Math.Round(rel / step) % (int)Math.Round(360f / step) : 0,
                Halyard = spec.OnHalyard,
                SelfCollision = selfCollision,
                Plate = spec.HasBackPlate,
                Cut =spec.Cut * 4096 + (FlagCut.UsesDepth(spec.Cut) ? (int)Math.Round(spec.CutDepth * 1000f) : 0),
            };
        }

        /// <summary>The angle's step, degrees: 0 (none) for an upright hoist with no pole, whose cloth turned with the wind is the same.</summary>
        public static float AngleStep(bool upright, bool pole)
        {
            return upright ? (pole ? UprightPoleAngleStep : 0f) : TurnedAngleStep;
        }

        /// <summary>The angle's tolerance, degrees (180: any).</summary>
        public static float AngleTolerance(bool upright, bool pole)
        {
            return upright ? (pole ? UprightPoleAngleTolerance : 180f) : TurnedAngleTolerance;
        }

        /// <summary>Degrees in 0..360.</summary>
        public static float Wrap(float degrees)
        {
            float d = degrees % 360f;
            return d < 0f ? d + 360f : d;
        }

        /// <summary>
        /// A flag in a wind of <paramref name="speed"/> blowing <paramref name="degrees"/> from its
        /// heading may follow a group of its cloth (<see cref="SameCloth"/>) made at
        /// <paramref name="groupSpeed"/> and <paramref name="groupDegrees"/>.
        /// </summary>
        public bool Near(float speed, float degrees, float groupSpeed, float groupDegrees, bool upright)
        {
            float r = Math.Max(speed, 0.3f) / Math.Max(groupSpeed, 0.3f);
            if (r > 1f + SpeedTolerance || r < 1f / (1f + SpeedTolerance))
                return false;

            float d = Math.Abs(Wrap(degrees - groupDegrees + 180f) - 180f);
            return d <= AngleTolerance(upright, Pole != 0);
        }

        /// <summary>A hoist straight down the pole from the flag's side: turning the cloth about the vertical turns nothing it hangs from.</summary>
        public static bool Upright(FlagSpec spec)
        {
            return spec.HoistedFrom == FlagHoist.Side && Math.Abs(spec.Tilt) < 0.05f && Math.Abs(spec.Lean) < 0.05f;
        }

        /// <summary>The same cloth, pole and hoist, whatever the wind (<see cref="Speed"/>, <see cref="Angle"/>).</summary>
        public bool SameCloth(ShareKey o)
        {
            return Length == o.Length && Proportion == o.Proportion && HoistedFrom == o.HoistedFrom && Tilt == o.Tilt && Lean == o.Lean &&
                   Waviness == o.Waviness && Pole == o.Pole && Halyard == o.Halyard && SelfCollision == o.SelfCollision && Cut == o.Cut &&
                   Plate == o.Plate;
        }

        /// <summary>This key with the wind left out: what a crowd of like flags is counted by.</summary>
        public ShareKey Cloth
        {
            get
            {
                ShareKey k = this;
                k.Speed = 0;
                k.Angle = 0;
                return k;
            }
        }

        public bool Equals(ShareKey o)
        {
            return SameCloth(o) && Speed == o.Speed && Angle == o.Angle;
        }

        public override bool Equals(object obj)
        {
            return obj is ShareKey && Equals((ShareKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Length;
                h = h * 31 + Proportion;
                h = h * 31 + HoistedFrom;
                h = h * 31 + Tilt;
                h = h * 31 + Lean;
                h = h * 31 + Waviness;
                h = h * 31 + Pole;
                h = h * 31 + Speed;
                h = h * 31 + Angle;
                h = h * 31 + (Halyard ? 1 : 0) + (SelfCollision ? 2 : 0) + (Plate ? 4 : 0);
                h = h * 31 + Cut;
                return h;
            }
        }
    }
}
