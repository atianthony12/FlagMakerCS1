using System;

namespace RealFlag.Flags
{
    /// <summary>
    /// STRING FLAGS' SHARED MOTION. A string's flags all hang alike from one string in one wind,
    /// and how a cloth moves depends on its size, its cut, how wavy it is and the air -- never on
    /// its colours or its country. So a few hidden cloths, SOURCES, are stepped for the whole city,
    /// and every flag on every span plays one of them back a delay behind, its own: one animation
    /// played by many instances at their own time offsets (crowd animation's way, GPU Gems 3 ch. 2,
    /// Dudash 2007; the mod's ClothShare does the same for like flags on poles).
    /// A source is found by its <see cref="SourceKey"/>: the flag itself (size, cut, waviness,
    /// detail), which of three ways the wind crosses its span (along, oblique, across: a
    /// <see cref="WindClass"/>), and which of the span's motions it is (<see cref="SourceKey.Slot"/>:
    /// each slot's air is its own patch of the wind's field). NOT the wind of the moment: its speed
    /// is fed to each source as it steps, eased (<see cref="Source.Want"/>), so a gust or a turning
    /// wind changes how a cloth flies, never which cloth a flag plays -- the first version keyed the
    /// speed (10% steps), the angle (15 degrees) and the string's slope (10 degrees), and the
    /// author's run (2026-11-07 15:32-15:39, Volumetric Weather's gusts) made 1446 sources for two
    /// spans, each starting from rest: "the cloth animation seems to be resetting every few
    /// seconds". PURE.
    /// </summary>
    /// <remarks>
    /// A source's frame: its anchor (the top corner on the string) at the origin, the span's level
    /// direction along +X, up +Y, +Z square to the span (X x Y). Its hoist runs level along X and
    /// the cloth hangs from it; the wind blows at its class's angle from X towards +Z. A flag takes
    /// the sample in its span's axes, mirrored when its wind comes from the other side (-Z: z
    /// negated; from -X: its hoist reversed), and sheared to its string's slope (the hoist follows
    /// the string, the fly stays plumb): StringFlagsMesh.Pose.
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
        /// How the wind crosses a span, folded by the cloth's two mirrors (from -Z it is the same
        /// cloth with z negated; from -X, with its hoist reversed) into 0..90 degrees from the
        /// string, in three classes -- along (0-30), oblique (30-60), across (60-90) -- each a
        /// source's own steady direction (<see cref="Angle"/>). A span keeps its class until the
        /// wind is <see cref="Hold"/> past the class's edge.
        /// </summary>
        public static class WindClass
        {
            public const int Along = 0, Oblique = 1, Across = 2;

            /// <summary>Degrees past a class's edge before a span takes another.</summary>
            public const float Hold = 7.5f;

            /// <summary>The direction a class's sources fly in, degrees from the string (X) towards +Z.</summary>
            public static float Angle(int windClass)
            {
                return windClass == Along ? 15f : windClass == Oblique ? 45f : 75f;
            }

            /// <summary>
            /// The wind blowing <paramref name="degrees"/> from the span's X towards its Z, folded:
            /// the angle in 0..90, and whether the sample is mirrored in z (wind from -Z) and in x (from -X).
            /// </summary>
            public static float Fold(float degrees, out bool mirrorZ, out bool mirrorX)
            {
                float a = degrees % 360f;
                if (a < 0f)
                    a += 360f;
                mirrorZ = a > 180f;
                if (mirrorZ)
                    a = 360f - a;
                mirrorX = a > 90f;
                if (mirrorX)
                    a = 180f - a;
                return a;
            }

            /// <summary>The class for a folded angle, held: <paramref name="held"/> kept until the angle is Hold past its edges (-1: none yet).</summary>
            public static int For(float folded, int held)
            {
                int now = folded < 30f ? Along : folded < 60f ? Oblique : Across;
                if (held < Along || held > Across || held == now)
                    return now;
                float lo = held * 30f - Hold, hi = (held + 1) * 30f + Hold;
                return folded >= lo && folded <= hi ? held : now;
            }
        }

        /// <summary>
        /// A source's recent steps: a ring of <see cref="Capacity"/> places of every particle with
        /// the time each was taken. Written after the source's job (on the main thread, between
        /// batches), read on the main thread.
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

        /// <summary>What makes two flags' cloth move alike (see the class): the flag, its wind's class, its slot.</summary>
        public struct SourceKey : IEquatable<SourceKey>
        {
            /// <summary>Millimetres along the string and down from it; the cut (FlagCut) and its depth x 1000; waviness x 1000; the detail; the wind's class; the motion slot; still (1) or waving (0).</summary>
            public int Along, Hang, Cut, CutDepth, Waviness, Detail, Wind, Slot, Still;

            public static SourceKey For(float along, float hang, int cut, float cutDepth, float waviness, int detail, int windClass, int slot, bool still)
            {
                return new SourceKey
                {
                    Along = (int)Math.Round(along * 1000f),
                    Hang = (int)Math.Round(hang * 1000f),
                    Cut = FlagCut.Sanitise(cut),
                    CutDepth = FlagCut.UsesDepth(cut) ? (int)Math.Round(FlagCut.SanitiseDepth(cutDepth) * 1000f) : 0,
                    Waviness = (int)Math.Round(waviness * 1000f),
                    Detail = detail,
                    Wind = still ? 0 : Math.Max(WindClass.Along, Math.Min(WindClass.Across, windClass)),
                    Slot = still ? 0 : slot,
                    Still = still ? 1 : 0,
                };
            }

            /// <summary>The direction its source flies in, in the source's frame.</summary>
            public void Direction(out float x, out float z)
            {
                double a = WindClass.Angle(Wind) * Math.PI / 180.0;
                x = (float)Math.Cos(a);
                z = (float)Math.Sin(a);
            }

            public bool Equals(SourceKey o)
            {
                return Along == o.Along && Hang == o.Hang && Cut == o.Cut && CutDepth == o.CutDepth && Waviness == o.Waviness && Detail == o.Detail &&
                       Wind == o.Wind && Slot == o.Slot && Still == o.Still;
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
                    h = h * 31 + Cut;
                    h = h * 31 + CutDepth;
                    h = h * 31 + Waviness;
                    h = h * 31 + Detail;
                    h = h * 31 + Wind;
                    h = h * 31 + Slot;
                    h = h * 31 + Still;
                    return h;
                }
            }

            public override string ToString()
            {
                string wind = Wind == WindClass.Along ? "along" : Wind == WindClass.Oblique ? "oblique" : "across";
                return Along + "x" + Hang + " mm" + (Cut != FlagCut.Rectangle ? " " + FlagCut.LogName(Cut) : "") + ", wavy " + Waviness + ", detail " + Detail +
                       (Still == 1 ? ", still" : ", wind " + wind + ", slot " + Slot);
            }
        }

        /// <summary>
        /// One source: its cloth, its record, its clock, the wind it flies in. A waving one steps
        /// every frame it is wanted (on the cloth's thread, as a ClothJob; recorded after) in the
        /// wind its flags want this frame (<see cref="Want"/>), eased; a still one is made once
        /// (StillPose) and holds that one shape.
        /// </summary>
        public sealed class Source
        {
            public readonly SourceKey Key;
            public readonly FlagCloth Cloth;
            public readonly Ring Record;

            /// <summary>Its clock (the time of its newest step) and the real time it was last wanted.</summary>
            public float Clock;
            public float LastWanted;

            /// <summary>The time left to step.</summary>
            public float Pending;

            /// <summary>The speed it flies in (m/s at the flags, eased); -1 before it is first wanted.</summary>
            public float Speed = -1f;

            /// <summary>This frame's wanted speeds (its spans'), summed and counted.</summary>
            private float _wantSum;
            private int _wantCount;

            /// <summary>Where in the wind's field it flies: each slot its own patch, so slots do not move alike.</summary>
            public readonly float FieldX, FieldZ;

            public Source(SourceKey key, float now)
            {
                Key = key;
                float dx, dz;
                key.Direction(out dx, out dz);
                // The fly hangs down from a level hoist; the cloth's length is its fly, its height its hoist;
                // cut to the flags' outline (FlagCut: only the cells it touches are cloth).
                float depth = key.CutDepth > 0 ? key.CutDepth / 1000f : FlagCut.DefaultDepth;
                Cloth = new FlagCloth(key.Hang / 1000f, key.Along / 1000f, FlagFabric.Standard(key.Waviness / 1000f), dx, dz, key.Detail, 1f, 0f, 0f,
                                      key.Cut, depth);
                Cloth.LayOut(0f, -1f, 0f, key.Still == 0);
                Record = new Ring(Cloth.Count);
                FieldX = 137.1f * key.Slot + 11.3f * key.Wind;
                FieldZ = 91.7f * key.Slot - 7.9f * key.Wind;
                Clock = 0f;
                LastWanted = now;

                if (key.Still == 1)
                    StillPose.Make(Cloth, FlagMotion.Still, false, 0f, -1f, 0f, key.Waviness / 1000f, key.Hang / 1000f);
                else
                    Cloth.Calm(FlagCloth.CalmTime);

                // Its first shape at once (hanging, laid out, or the still one): its flags show before its first steps come back.
                Record.Add(Cloth, 0f);
            }

            /// <summary>Its record spans the longest delay: flags may play it.</summary>
            public bool Ready
            {
                get { return Key.Still == 1 || Record.Span >= MaxDelay + 0.1f; }
            }

            /// <summary>A flag plays it this frame, in a wind of <paramref name="speed"/> m/s (the finder marks LastWanted).</summary>
            public void Want(float speed)
            {
                if (float.IsNaN(speed) || speed < 0f)
                    return;
                _wantSum += speed;
                _wantCount++;
            }

            /// <summary>
            /// This frame's job: the steps due in real time (at most <paramref name="maxSteps"/>, the
            /// rest let go), more while it warms up, in the wind its spans wanted (their mean, followed
            /// over FlagWind.FollowSeconds). False when nothing is due (or it is still).
            /// </summary>
            public bool Plan(float dt, int maxSteps, int warmSteps, float gusts, bool gustInSpeed, out ClothJob job)
            {
                job = default(ClothJob);
                if (_wantCount > 0)
                {
                    float wanted = _wantSum / _wantCount;
                    Speed = Speed < 0f || !Ready ? wanted : FlagWind.Follow(Speed, wanted, dt);
                }

                _wantSum = 0f;
                _wantCount = 0;
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
                    Speed = Math.Max(0f, Speed),
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

            /// <summary>After the batch: the job's steps are on the cloth; recorded at the clock.</summary>
            public void Took(ref ClothJob job)
            {
                if (job.WentWrong)
                {
                    Record.Clear();
                    Record.Add(Cloth, Clock);
                    return;
                }

                Record.Add(Cloth, Clock);
            }
        }
    }
}
