using System;

namespace RealFlag.Flags
{
    /// <summary>
    /// STRING FLAGS' SHARED MOTION. A string's flags all hang alike from one string in one wind,
    /// and how a cloth moves depends on its size, how wavy it is, its detail and the air -- never
    /// on its colours or its country. So a few hidden cloths, SOURCES, are stepped for the whole
    /// city, and every flag on every span plays one of them back a delay behind, its own: one
    /// animation played by many instances at their own time offsets (crowd animation's way, GPU
    /// Gems 3 ch. 2, Dudash 2007; the mod's ClothShare does the same for like flags on poles).
    /// A source is found by its <see cref="SourceKey"/>: flags of the same size, waviness and
    /// detail, in the same wind across their span (speed in 10% steps, its angle to the span in 15
    /// degree steps), on a string sloping the same (10 degree steps), and which of the span's
    /// motions it is (<see cref="SourceKey.Slot"/>: each slot's air is its own patch of the wind's
    /// field, so slots move differently). PURE.
    /// </summary>
    /// <remarks>
    /// A source's frame: its anchor (the top corner on the string) at the origin, the span's level
    /// direction along +X, up +Y, +Z square to the span (X x Y, the side the wind's +Z blows
    /// to). Its hoist runs along the string's slope in the X-Y plane and the cloth hangs from it;
    /// the wind blows along its angle in X-Z. A flag on a span takes the sample with the span's
    /// own axes: world = anchor + x S + y up + z P, P the span's level square.
    /// </remarks>
    public static class StringMotion
    {
        /// <summary>The longest delay a flag plays its source behind, and the shortest, seconds.</summary>
        public const float MinDelay = 0.2f, MaxDelay = 2.6f;

        /// <summary>The source's step, seconds (real time, as the flags').</summary>
        public const float Step = 1f / 60f;

        /// <summary>A flag's delay from its place along the chain and its span: spread by the golden ratio's sequence (Weyl), so neighbours are far apart in it.</summary>
        public static float DelayFor(int index, int span)
        {
            double f = (index + span * 0.3819660112501051) * 0.6180339887498949;
            f -= Math.Floor(f);
            return MinDelay + (float)f * (MaxDelay - MinDelay);
        }

        /// <summary>
        /// Which of <paramref name="motions"/> a flag plays, from its place along the chain: spread by
        /// the plastic number's sequence (R1, Roberts 2018), not in turn -- a string of three
        /// designs in turn with three motions in turn would give every red flag the same motion.
        /// </summary>
        public static int SlotFor(int index, int motions)
        {
            if (motions <= 1)
                return 0;
            double f = (index + 0.5) * 0.7548776662466927;
            f -= Math.Floor(f);
            return Math.Min(motions - 1, (int)(f * motions));
        }

        /// <summary>Auto: the motions a span of <paramref name="flags"/> flags shares -- more for longer spans, so no two near flags play one motion at nearly one delay.</summary>
        public static int AutoMotions(int flags)
        {
            return flags <= 6 ? 1 : flags <= 20 ? 2 : flags <= 60 ? 3 : 4;
        }

        /// <summary>
        /// A source's recent steps: a ring of <see cref="Capacity"/> places of every particle with
        /// the time each was taken. Written by the source's job (on the cloth's thread), read on the
        /// main thread only while no batch runs.
        /// </summary>
        public sealed class Ring
        {
            /// <summary>3.2 s of 1/60 s steps: past the longest delay.</summary>
            public const int Capacity = 192;

            public readonly int Count;
            private readonly float[] _x, _y, _z;
            private readonly float[] _time = new float[Capacity];
            private int _head = -1, _filled;

            public Ring(int count)
            {
                Count = count;
                _x = new float[Capacity * count];
                _y = new float[Capacity * count];
                _z = new float[Capacity * count];
            }

            public int Filled
            {
                get { return _filled; }
            }

            public float Newest
            {
                get { return _filled > 0 ? _time[_head] : 0f; }
            }

            public float Oldest
            {
                get { return _filled > 0 ? _time[(_head - _filled + 1 + Capacity) % Capacity] : 0f; }
            }

            public float Span
            {
                get { return _filled > 1 ? Newest - Oldest : 0f; }
            }

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

            public void Clear()
            {
                _head = -1;
                _filled = 0;
            }

            /// <summary>The places at <paramref name="time"/>, between the two steps round it (held to the oldest and newest kept). False when none is kept.</summary>
            public bool Sample(float time, float[] x, float[] y, float[] z)
            {
                if (_filled == 0)
                    return false;

                int a = _head, b = _head;
                float w = 0f;
                if (_filled > 1 && time < _time[_head])
                {
                    int newer = _head;
                    a = -1;
                    for (int n = 1; n < _filled; n++)
                    {
                        int older = (_head - n + Capacity) % Capacity;
                        if (_time[older] <= time)
                        {
                            a = older;
                            b = newer;
                            float span = _time[b] - _time[a];
                            w = span > 1e-6f ? (time - _time[a]) / span : 1f;
                            break;
                        }

                        newer = older;
                    }

                    if (a < 0)
                    {
                        a = b = newer;
                        w = 0f;
                    }
                }

                int oa = a * Count, ob = b * Count;
                float keep = 1f - w;
                for (int k = 0; k < Count; k++)
                {
                    x[k] = _x[oa + k] * keep + _x[ob + k] * w;
                    y[k] = _y[oa + k] * keep + _y[ob + k] * w;
                    z[k] = _z[oa + k] * keep + _z[ob + k] * w;
                }

                return true;
            }
        }

        /// <summary>What makes two flags' cloth move alike (see the class).</summary>
        public struct SourceKey : IEquatable<SourceKey>
        {
            public const float AngleStep = 15f, SlopeStep = 10f;
            public const double SpeedStep = 1.1;

            /// <summary>Millimetres along the string and down from it; waviness x 1000; the detail; the wind's speed step and angle step; the slope's step; the motion slot; still (1) or waving (0).</summary>
            public int Along, Hang, Waviness, Detail, Speed, Angle, Slope, Slot, Still;

            public static SourceKey For(float along, float hang, float waviness, int detail, float speed, float angleDegrees, float slopeDegrees, int slot, bool still)
            {
                float a = angleDegrees % 360f;
                if (a < 0f)
                    a += 360f;
                int steps = (int)Math.Round(360f / AngleStep);
                return new SourceKey
                {
                    Along = (int)Math.Round(along * 1000f),
                    Hang = (int)Math.Round(hang * 1000f),
                    Waviness = (int)Math.Round(waviness * 1000f),
                    Detail = detail,
                    Speed = still ? 0 : (int)Math.Round(Math.Log(Math.Max(speed, 0.3f)) / Math.Log(SpeedStep)),
                    Angle = still ? 0 : (int)Math.Round(a / AngleStep) % steps,
                    Slope = (int)Math.Round(Math.Max(-80f, Math.Min(80f, slopeDegrees)) / SlopeStep),
                    Slot = still ? 0 : slot,
                    Still = still ? 1 : 0,
                };
            }

            /// <summary>The wind this key stands for, at the flag: its speed (m/s) and its direction in the source's frame.</summary>
            public float SpeedOf
            {
                get { return Still == 1 ? 0f : (float)Math.Pow(SpeedStep, Speed); }
            }

            public void Direction(out float x, out float z)
            {
                double a = Angle * AngleStep * Math.PI / 180.0;
                x = (float)Math.Cos(a);
                z = (float)Math.Sin(a);
            }

            public float SlopeRadians
            {
                get { return (float)(Slope * SlopeStep * Math.PI / 180.0); }
            }

            public bool Equals(SourceKey o)
            {
                return Along == o.Along && Hang == o.Hang && Waviness == o.Waviness && Detail == o.Detail && Speed == o.Speed &&
                       Angle == o.Angle && Slope == o.Slope && Slot == o.Slot && Still == o.Still;
            }

            public override bool Equals(object obj)
            {
                return obj is SourceKey && Equals((SourceKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = Along;
                    h = h * 31 + Hang;
                    h = h * 31 + Waviness;
                    h = h * 31 + Detail;
                    h = h * 31 + Speed;
                    h = h * 31 + Angle;
                    h = h * 31 + Slope;
                    h = h * 31 + Slot;
                    h = h * 31 + Still;
                    return h;
                }
            }

            public override string ToString()
            {
                return Along + "x" + Hang + " mm, wavy " + Waviness + ", detail " + Detail + ", " + SpeedOf.ToString("F1") + " m/s at " +
                       (Angle * AngleStep).ToString("F0") + " deg, slope " + (Slope * SlopeStep).ToString("F0") + ", slot " + Slot + (Still == 1 ? ", still" : "");
            }
        }

        /// <summary>
        /// One source: its cloth, its record, its clock. A waving one steps every frame it is wanted
        /// (on the cloth's thread, as a ClothJob with its <see cref="Ring"/> written after each step);
        /// a still one is made once (StillPose) and holds that one shape.
        /// </summary>
        public sealed class Source
        {
            public readonly SourceKey Key;
            public readonly FlagCloth Cloth;
            public readonly Ring Record;

            /// <summary>Its clock (the time of its newest step) and the real time it was last wanted.</summary>
            public float Clock;
            public float LastWanted;

            /// <summary>The time left to step, and whether it is ready for followers (its record spans the longest delay).</summary>
            public float Pending;

            /// <summary>Where in the wind's field it flies: each slot its own patch, so slots do not move alike.</summary>
            public readonly float FieldX, FieldZ;

            public Source(SourceKey key, float now)
            {
                Key = key;
                float slope = key.SlopeRadians;
                float hx = (float)Math.Cos(slope), hy = (float)Math.Sin(slope);
                float dx, dz;
                key.Direction(out dx, out dz);
                // The fly hangs down from the string; the cloth's length is its fly, its height its hoist.
                Cloth = new FlagCloth(key.Hang / 1000f, key.Along / 1000f, FlagFabric.Standard(key.Waviness / 1000f), dx, dz, key.Detail, hx, hy, 0f);
                Cloth.LayOut(0f, -1f, 0f, key.Still == 0);
                Record = new Ring(Cloth.Count);
                FieldX = 137.1f * key.Slot + 11.3f * key.Angle;
                FieldZ = 91.7f * key.Slot - 7.9f * key.Speed;
                Clock = 0f;
                LastWanted = now;

                if (key.Still == 1)
                {
                    StillPose.Make(Cloth, FlagMotion.Still, false, 0f, -1f, 0f, key.Waviness / 1000f, key.Hang / 1000f);
                    Record.Add(Cloth, 0f);
                }
                else
                {
                    Cloth.Calm(FlagCloth.CalmTime);
                    // Its first shape at once (hanging, laid out): its flags show before its first steps come back.
                    Record.Add(Cloth, 0f);
                }
            }

            /// <summary>Its record spans the longest delay: flags may play it.</summary>
            public bool Ready
            {
                get { return Key.Still == 1 || Record.Span >= MaxDelay + 0.1f; }
            }

            /// <summary>
            /// This frame's job: the steps due in real time (at most <paramref name="maxSteps"/>, the
            /// rest let go), more while it warms up. Null when nothing is due (or it is still).
            /// </summary>
            public bool Plan(float dt, int maxSteps, int warmSteps, float gusts, bool gustInSpeed, out ClothJob job)
            {
                job = default(ClothJob);
                if (Key.Still == 1)
                    return false;

                Pending += dt;
                int steps = (int)Math.Floor(Pending / Step + 1e-4);
                int most = Ready ? maxSteps : Math.Max(maxSteps, warmSteps);
                if (!Ready)
                    steps = Math.Max(steps, warmSteps);
                if (steps > most)
                    steps = most;
                Pending = Math.Max(0f, Pending - steps * Step);
                if (Pending > 4f * Step)
                    Pending = 4f * Step;
                if (steps <= 0)
                    return false;

                float dx, dz;
                Key.Direction(out dx, out dz);
                job = new ClothJob
                {
                    Cloth = Cloth,
                    Steps = steps,
                    Size = Step,
                    Time = Clock,
                    Speed = Key.SpeedOf,
                    Gusts = gusts,
                    GustInSpeed = gustInSpeed,
                    DirX = dx,
                    DirZ = dz,
                    Height = 10f,
                    Waviness = Key.Waviness / 1000f,
                    Length = Key.Hang / 1000f,
                    AnchorX = FieldX,
                    AnchorZ = FieldZ,
                    Roughness = 0f,
                    Record = null,
                };
                Clock += steps * Step;
                return true;
            }

            /// <summary>After the batch: the job's steps are on the cloth; recorded at the clock (the job recorded none -- see <see cref="Plan"/>).</summary>
            public void Took(ref ClothJob job)
            {
                if (job.WentWrong)
                {
                    Record.Clear();
                    return;
                }

                Record.Add(Cloth, Clock);
            }
        }
    }
}
