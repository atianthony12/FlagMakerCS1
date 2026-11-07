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
    /// angles in tenths of a degree, the pole's numbers in 5 mm, the wind's speed in steps of 10%
    /// and its angle against the flag's heading in steps of <see cref="UprightAngleStep"/> (an
    /// upright hoist: a turned cloth is the same flag) or <see cref="TurnedAngleStep"/> (a tilted
    /// or top hoist, whose line the turn must keep). Colours, designs and pictures are not in it.
    /// </summary>
    public struct ShareKey : IEquatable<ShareKey>
    {
        public const float UprightAngleStep = 15f, TurnedAngleStep = 5f;

        /// <summary>The wind's speed step: 10% (a bucket of ln 1.1).</summary>
        public const double SpeedStep = 1.1;

        public int Length, Proportion, HoistedFrom, Tilt, Lean, Waviness, Detail, Pole, Speed, Angle;
        public bool Halyard, SelfCollision;

        /// <summary>The cut (FlagCut) and its depth in thousandths where it has one: another cut is another cloth.</summary>
        public int Cut;

        /// <summary>
        /// The key of a flag of <paramref name="spec"/> at <paramref name="detail"/> in a wind of
        /// <paramref name="speed"/> m/s at the flag blowing <paramref name="relativeDegrees"/> from
        /// its heading; its pole's offset and radius at the hoist (0 for none).
        /// </summary>
        public static ShareKey For(FlagSpec spec, int detail, float speed, float relativeDegrees, float poleOffset, float poleRadius, bool selfCollision)
        {
            bool upright = Upright(spec);
            float step = upright ? UprightAngleStep : TurnedAngleStep;
            float rel = relativeDegrees % 360f;
            if (rel < 0f)
                rel += 360f;

            return new ShareKey
            {
                Length = (int)Math.Round(spec.Length * 1000f),
                Proportion = spec.Proportion,
                HoistedFrom = spec.HoistedFrom,
                Tilt = (int)Math.Round(spec.Tilt * 10f),
                Lean = (int)Math.Round(spec.Lean * 10f),
                Waviness = (int)Math.Round(spec.Waviness * 1000f),
                Detail = detail,
                Pole = poleRadius > 0f ? 1 + (int)Math.Round(poleOffset * 200f) * 4096 + (int)Math.Round(poleRadius * 200f) : 0,
                Speed = (int)Math.Round(Math.Log(Math.Max(speed, 0.3f)) / Math.Log(SpeedStep)),
                Angle = (int)Math.Round(rel / step) % (int)Math.Round(360f / step),
                Halyard = spec.OnHalyard,
                SelfCollision = selfCollision,
                Cut = spec.Cut * 4096 + (FlagCut.UsesDepth(spec.Cut) ? (int)Math.Round(spec.CutDepth * 1000f) : 0),
            };
        }

        /// <summary>A hoist straight down the pole from the flag's side: turning the cloth about the vertical turns nothing it hangs from.</summary>
        public static bool Upright(FlagSpec spec)
        {
            return spec.HoistedFrom == FlagHoist.Side && Math.Abs(spec.Tilt) < 0.05f && Math.Abs(spec.Lean) < 0.05f;
        }

        public bool Equals(ShareKey o)
        {
            return Length == o.Length && Proportion == o.Proportion && HoistedFrom == o.HoistedFrom && Tilt == o.Tilt && Lean == o.Lean &&
                   Waviness == o.Waviness && Detail == o.Detail && Pole == o.Pole && Speed == o.Speed && Angle == o.Angle &&
                   Halyard == o.Halyard && SelfCollision == o.SelfCollision && Cut == o.Cut;
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
                h = h * 31 + Detail;
                h = h * 31 + Pole;
                h = h * 31 + Speed;
                h = h * 31 + Angle;
                h = h * 31 + (Halyard ? 1 : 0) + (SelfCollision ? 2 : 0);
                h = h * 31 + Cut;
                return h;
            }
        }
    }
}
