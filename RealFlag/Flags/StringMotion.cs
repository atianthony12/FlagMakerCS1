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
    /// detail) and which of the span's motions it is (<see cref="SourceKey.Slot"/>: each slot's
    /// air is its own patch of the wind's field). NOTHING OF THE WIND: every source flies across
    /// its string (<see cref="OnString.Angle"/>), in a speed fed to it as it steps, eased
    /// (<see cref="Source.Want"/>) -- the wind's share across the string (<see cref="OnString.Share"/>,
    /// the independence principle of yawed flow: the load goes with the normal component) -- and a
    /// wind from the other side swings the flags round to it (<see cref="OnString.Ease"/>). So a gust
    /// or a turning wind changes how a cloth flies, never which cloth a flag plays.
    /// The first version keyed the speed (10% steps), the angle (15 degrees) and the slope (10
    /// degrees): 1446 sources for two spans in the author's run (2026-11-07 15:32-15:39), each
    /// starting from rest -- "the cloth animation seems to be resetting every few seconds". The
    /// second kept three wind classes (along / oblique / across, mirrored): Volumetric Weather's
    /// turning wind still crossed them every 10-40 s (16:09-16:13, one span: 'wind oblique',
    /// 'along', 'across', 'oblique' ...), each a fresh cloth from rest -- "the animation keeps
    /// resetting" (the author: "I don't mind turning down the quality / collision /
    /// interactiveness of string flags much to make it work"). A source no flag plays this frame
    /// (its spans out of view or far) is NOT stepped and kept as it is for <see cref="KeepSeconds"/>,
    /// so looking back finds it where it was, not new. PURE.
    /// </summary>
    /// <remarks>
    /// A source's frame: its anchor (the top corner on the string) at the origin, the span's level
    /// direction along +X, up +Y, +Z square to the span (X x Y). Its hoist runs level along X and
    /// the cloth hangs from it; the wind blows <see cref="OnString.Angle"/> from X towards +Z. A flag
    /// takes the sample in its span's axes, z times its span's side (+1 the wind from -Z's side
    /// blowing to +Z, -1 the other, eased between), sheared to its string's slope (the hoist follows
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

        /// <summary>A source no flag plays is kept (not stepped) this long, seconds, then let go.</summary>
        public const float KeepSeconds = 300f;

        /// <summary>
        /// A source whose biggest flag on the screen is at least this many pixels steps at the full
        /// rate (<see cref="Step"/>); smaller, at half (steps twice as long, half as many), as the
        /// flags on poles do under FlagRenderer.FullRatePixels. A span's flags are posed at the
        /// same two rates (StringSpan).
        /// </summary>
        public const float FullRatePixels = 30f;

        /// <summary>
        /// The wind on a string's flags, from where it blows: <c>degrees</c> from the span's X
        /// towards its Z (90 = square to the string, towards +Z). Every source flies across;
        /// the angle only scales the speed fed to it and picks the side the flags fly out to.
        /// </summary>
        public static class OnString
        {
            /// <summary>The direction every source flies in, degrees from the string (X) towards +Z (the old "across" class's).</summary>
            public const float Angle = 75f;

            /// <summary>The share of the wind's speed a string's flags fly in when it blows along the string: they still stir.</summary>
            public const float AlongShare = 0.3f;

            /// <summary>Degrees past the string's line the wind must come from before the flags swing round to the other side.</summary>
            public const float SideHold = 15f;

            /// <summary>Seconds the flags take to swing round from one side to the other.</summary>
            public const float SideSeconds = 2f;

            /// <summary>The share of the wind's speed the flags fly in: AlongShare along the string, all of it square to it.</summary>
            public static float Share(float degrees)
            {
                float s = Math.Abs((float)Math.Sin(degrees * Math.PI / 180.0));
                return AlongShare + (1f - AlongShare) * s;
            }

            /// <summary>The side the flags fly out to (+1: +Z, -1: -Z), <paramref name="held"/> kept until the wind is SideHold past the string's line (0: none yet).</summary>
            public static float SideFor(float degrees, float held)
            {
                float s = (float)Math.Sin(degrees * Math.PI / 180.0);
                float hold = (float)Math.Sin(SideHold * Math.PI / 180.0);
                if (held > 0f)
                    return s < -hold ? -1f : 1f;
                if (held < 0f)
                    return s > hold ? 1f : -1f;
                return s >= 0f ? 1f : -1f;
            }

            /// <summary>The side a step on, towards <paramref name="target"/>, over SideSeconds from one side to the other.</summary>
            public static float Ease(float side, float target, float dt)
            {
                float step = 2f * Math.Max(0f, dt) / SideSeconds;
                if (side < target)
                    return Math.Min(target, side + step);
                return Math.Max(target, side - step);
            }

            /// <summary>The z scale a side gives the sample: smooth at both ends (the odd smoothstep), 0 halfway round.</summary>
            public static float ZScale(float side)
            {
                float s = Math.Max(-1f, Math.Min(1f, side));
                return s * (1.5f - 0.5f * s * s);
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
            private readonly int _slots;
            private readonly float[] _x, _y, _z;
            private readonly float[] _time;
            private int _head = -1, _filled;

            public Ring(int count) : this(count, Capacity)
            {
            }

            /// <summary>A ring of <paramref name="slots"/> entries: one for a still source, which holds its one shape (2.3 KB a particle saved).</summary>
            public Ring(int count, int slots)
            {
                Count = count;
                _slots = Math.Max(1, slots);
                _time = new float[_slots];
                _x = new float[_slots * count];
                _y = new float[_slots * count];
                _z = new float[_slots * count];
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
                get { return _filled > 0 ? _time[(_head - _filled + 1 + _slots) % _slots] : 0f; }
            }

            public float Span
            {
                get { return _filled > 1 ? Newest - Oldest : 0f; }
            }

            public void Add(FlagCloth cloth, float time)
            {
                int slot = (_head + 1) % _slots;
                int o = slot * Count;
                Array.Copy(cloth.X, 0, _x, o, Count);
                Array.Copy(cloth.Y, 0, _y, o, Count);
                Array.Copy(cloth.Z, 0, _z, o, Count);
                _time[slot] = time;
                _head = slot;
                if (_filled < _slots)
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
                        int older = (_head - n + _slots) % _slots;
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

        /// <summary>What makes two flags' cloth move alike (see the class): the flag and its slot -- nothing of the wind.</summary>
        public struct SourceKey : IEquatable<SourceKey>
        {
            /// <summary>Millimetres along the string and down from it; the cut (FlagCut) and its depth x 1000; waviness x 1000; the detail; the motion slot; still (1) or waving (0).</summary>
            public int Along, Hang, Cut, CutDepth, Waviness, Detail, Slot, Still;

            public static SourceKey For(float along, float hang, int cut, float cutDepth, float waviness, int detail, int slot, bool still)
            {
                return new SourceKey
                {
                    Along = (int)Math.Round(along * 1000f),
                    Hang = (int)Math.Round(hang * 1000f),
                    Cut = FlagCut.Sanitise(cut),
                    CutDepth = FlagCut.UsesDepth(cut) ? (int)Math.Round(FlagCut.SanitiseDepth(cutDepth) * 1000f) : 0,
                    Waviness = (int)Math.Round(waviness * 1000f),
                    Detail = detail,
                    Slot = still ? 0 : slot,
                    Still = still ? 1 : 0,
                };
            }

            /// <summary>The direction its source flies in, in the source's frame: OnString.Angle, every source alike.</summary>
            public void Direction(out float x, out float z)
            {
                double a = OnString.Angle * Math.PI / 180.0;
                x = (float)Math.Cos(a);
                z = (float)Math.Sin(a);
            }

            public bool Equals(SourceKey o)
            {
                return Along == o.Along && Hang == o.Hang && Cut == o.Cut && CutDepth == o.CutDepth && Waviness == o.Waviness && Detail == o.Detail &&
                       Slot == o.Slot && Still == o.Still;
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
                    h = h * 31 + Slot;
                    h = h * 31 + Still;
                    return h;
                }
            }

            public override string ToString()
            {
                return Along + "x" + Hang + " mm" + (Cut != FlagCut.Rectangle ? " " + FlagCut.LogName(Cut) : "") + ", wavy " + Waviness + ", detail " + Detail +
                       (Still == 1 ? ", still" : ", slot " + Slot);
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

            /// <summary>This frame's wanted speeds (its spans'), summed and counted; the biggest flag playing it, pixels.</summary>
            private float _wantSum, _wantPixels;
            private int _wantCount;

            /// <summary>The biggest flag that played it in the frame <see cref="Due"/> last took (pixels): the renderer's order when the batch is short of room.</summary>
            public float WantedPixels { get; private set; }

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
                // Small flags in a crowd: no self-collision (the author let strings go cheaper to stay steady).
                Cloth.SelfCollision = false;
                Cloth.LayOut(0f, -1f, 0f, key.Still == 0);
                // A still source holds one shape: one slot (a full ring is 192 x 12 bytes a particle).
                Record = new Ring(Cloth.Count, key.Still == 1 ? 1 : Ring.Capacity);
                FieldX = 137.1f * key.Slot + 22.6f;
                FieldZ = 91.7f * key.Slot - 15.8f;
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

            /// <summary>A flag plays it this frame, in a wind of <paramref name="speed"/> m/s (the finder marks LastWanted); the flag is big on the screen.</summary>
            public void Want(float speed)
            {
                Want(speed, float.MaxValue);
            }

            /// <summary>A flag <paramref name="pixels"/> big on the screen plays it this frame, in a wind of <paramref name="speed"/> m/s.</summary>
            public void Want(float speed, float pixels)
            {
                if (float.IsNaN(speed) || speed < 0f)
                    return;
                _wantSum += speed;
                _wantCount++;
                if (pixels > _wantPixels)
                    _wantPixels = pixels;
            }

            /// <summary>
            /// The frame's wants taken (the speed followed over FlagWind.FollowSeconds), and the steps
            /// due in real time: at most <paramref name="maxSteps"/>, more while it warms up, each of
            /// <paramref name="size"/> seconds (the full rate, or half while no big flag plays it:
            /// <see cref="FullRatePixels"/>). 0 when nothing is due, no flag plays it (held as it is,
            /// not stepped: it carries on from there when looked at again, never from rest), or it
            /// is still. Nothing is committed: <see cref="Commit"/> when the batch has room for it;
            /// otherwise the time waits (up to four steps' worth) and the flags playing it hold.
            /// </summary>
            public int Due(float dt, int maxSteps, int warmSteps, out float size)
            {
                size = Step;
                bool wanted = _wantCount > 0;
                if (wanted)
                {
                    float mean = _wantSum / _wantCount;
                    Speed = Speed < 0f || !Ready ? mean : FlagWind.Follow(Speed, mean, dt);
                }

                WantedPixels = wanted ? _wantPixels : 0f;
                _wantSum = 0f;
                _wantCount = 0;
                _wantPixels = 0f;
                if (Key.Still == 1)
                    return 0;

                if (!wanted)
                {
                    Pending = 0f;
                    return 0;
                }

                if (WantedPixels < FullRatePixels)
                    size = 2f * Step;
                Pending += dt;
                if (Pending > 4f * size)
                    Pending = 4f * size;
                int steps = (int)Math.Floor(Pending / size + 1e-4);
                int most = Ready ? maxSteps : Math.Max(maxSteps, warmSteps);
                if (!Ready)
                    steps = Math.Max(steps, warmSteps);
                if (steps > most)
                    steps = most;
                return Math.Max(0, steps);
            }

            /// <summary>The work <paramref name="steps"/> steps of <paramref name="size"/> seconds would be, in particle steps (the sub-steps for its speed), for the batch's budget.</summary>
            public long WorkFor(int steps, float size)
            {
                int sub = Cloth.SubStepsFor(FlagWind.ClothSpeed(Math.Max(0f, Speed), 10f, 0f), size);
                return (long)steps * sub * Cloth.Count;
            }

            /// <summary>The job for steps <see cref="Due"/> found due: the time taken off, the clock moved on.</summary>
            public ClothJob Commit(int steps, float size, float gusts, bool gustInSpeed)
            {
                Pending = Math.Max(0f, Pending - steps * size);
                float dx, dz;
                Key.Direction(out dx, out dz);
                var job = new ClothJob
                {
                    Cloth = Cloth,
                    Steps = steps,
                    Size = size,
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
                Clock += steps * size;
                return job;
            }

            /// <summary>
            /// This frame's job in one (the preview and the tests): the steps due, committed. False
            /// when nothing is due (or it is still).
            /// </summary>
            public bool Plan(float dt, int maxSteps, int warmSteps, float gusts, bool gustInSpeed, out ClothJob job)
            {
                job = default(ClothJob);
                float size;
                int steps = Due(dt, maxSteps, warmSteps, out size);
                if (steps <= 0)
                    return false;

                job = Commit(steps, size, gusts, gustInSpeed);
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
