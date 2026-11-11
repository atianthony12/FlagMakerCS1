using System;
using ColossalFramework.Math;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// A flag as the pointer finds it, in world space: a slab round where its cloth has been
    /// lately, and capsules round its hoist and its drawn pole, with their box. Written on the
    /// main thread by the renderer, read by raycasts on any thread (the game's tools cast on the
    /// simulation thread): each flag has two, and the renderer fills the one not published, then
    /// publishes it with one reference store.
    /// </summary>
    /// <remarks>
    /// The first pick followed the cloth -- a coarse sheet of its triangles and a box round it,
    /// both as it lay a tenth of a second ago -- so a pointer held still over a fluttering flag
    /// found it one moment and lost it the next, and a flag seen edge on or far off was a sliver
    /// ("it's still difficult to select the flag with MoveIt because of how small the area to
    /// click is"). Then the slab became the cloth's box in steady axes (down its hoist, out along
    /// the wind, across) held over its recent flutter (<see cref="PickHold"/>): it covers
    /// everything the flag sweeps and stands still while it waves. Every volume is at least a
    /// few pixels wide on the screen, however far off (FlagRenderer's pick aperture).
    ///
    /// Two sizes of the same volumes: TIGHT for the game's own tools (the bulldozer: they must
    /// not catch a flag over what the player works on near it) and WIDER for Move It and the
    /// mod's tool (<see cref="IntersectWide"/>). Move It draws a selected object in its hover
    /// colour for as long as the pointer is on it, so a click shows only once the pointer leaves
    /// it: a flag picked in the column of its whole reach (tried, 2026-09-27) was never left --
    /// "the selector is now much bigger, but ... hovering my mouse on it, I can't select it ...
    /// The selector is too big now". A ray picks the flag within the slab or a radius of its
    /// hoist edge and of the pole the mod draws: the closest points of two segments (Ericson,
    /// Real-Time Collision Detection, 2005, 5.1.9).
    /// </remarks>
    public sealed class PickShape
    {
        /// <summary>Round the wider volumes: the raycasts' first test, for either size.</summary>
        public Bounds Box;
        public Vector3 Anchor;

        /// <summary>The slab: its centre, its three unit axes, and its half sizes along them without any margin (the cloth's held extents).</summary>
        public Vector3 SlabCentre, SlabU, SlabV, SlabN;
        public float CoreU, CoreV, CoreN;
        public bool HasSlab;

        /// <summary>Capsules round the hoist edge and the drawn pole: their segments (a pole radius of 0: none).</summary>
        public Vector3 HoistA, HoistB, PoleA, PoleB;

        /// <summary>The margins and radii: tight (the game's tools) and wide (Move It, the mod's tool).</summary>
        public float InPlane, Across, HoistRadius, PoleRadius;
        public float WideInPlane, WideAcross, WideHoistRadius, WidePoleRadius;

        /// <summary>The segment a..b against the flag, the tight volumes: where along it (0..1) it first meets the slab or a capsule.</summary>
        public bool Intersect(Segment3 ray, out float t)
        {
            return Intersect(ray, InPlane, Across, HoistRadius, PoleRadius, out t);
        }

        /// <summary>The same, the wider volumes: Move It's pick and the mod's own.</summary>
        public bool IntersectWide(Segment3 ray, out float t)
        {
            return Intersect(ray, WideInPlane, WideAcross, WideHoistRadius, WidePoleRadius, out t);
        }

        private bool Intersect(Segment3 ray, float inPlane, float across, float hoistRadius, float poleRadius, out float t)
        {
            t = 2f;
            Vector3 origin = ray.a;
            Vector3 dir = ray.b - ray.a;

            var box = Box;
            box.Expand(0.2f);
            if (!box.IntersectRay(new Ray(origin, dir)))
                return false;

            bool hit = false;
            float s;
            if (HasSlab && SlabEntry(origin, dir, SlabCentre, SlabU, SlabV, SlabN, CoreU + inPlane, CoreV + inPlane, CoreN + across, out s) && s < t)
            {
                t = s;
                hit = true;
            }

            if (hoistRadius > 0f && Capsule(origin, ray.b, HoistA, HoistB, hoistRadius, out s) && s < t)
            {
                t = s;
                hit = true;
            }

            if (poleRadius > 0f && Capsule(origin, ray.b, PoleA, PoleB, poleRadius, out s) && s < t)
            {
                t = s;
                hit = true;
            }

            return hit;
        }

        /// <summary>
        /// The segment origin + dir x [0, 1] against a box of these unit axes and half sizes round
        /// <paramref name="centre"/> (the slab method, in the box's axes): where it enters (0 if it
        /// starts inside).
        /// </summary>
        public static bool SlabEntry(Vector3 origin, Vector3 dir, Vector3 centre, Vector3 u, Vector3 v, Vector3 n,
                                     float halfU, float halfV, float halfN, out float t)
        {
            t = 2f;
            Vector3 o = origin - centre;
            float lo = 0f, hi = 1f;
            if (!Axis(Vector3.Dot(o, u), Vector3.Dot(dir, u), halfU, ref lo, ref hi) ||
                !Axis(Vector3.Dot(o, v), Vector3.Dot(dir, v), halfV, ref lo, ref hi) ||
                !Axis(Vector3.Dot(o, n), Vector3.Dot(dir, n), halfN, ref lo, ref hi))
                return false;

            t = lo;
            return true;
        }

        private static bool Axis(float o, float d, float half, ref float lo, ref float hi)
        {
            if (d > -1e-9f && d < 1e-9f)
                return o >= -half && o <= half;

            float a = (-half - o) / d, b = (half - o) / d;
            if (a > b)
            {
                float swap = a;
                a = b;
                b = swap;
            }

            if (a > lo) lo = a;
            if (b < hi) hi = b;
            return lo <= hi;
        }

        /// <summary>The ray segment p..q within <paramref name="radius"/> of the segment a..b: where along p..q it comes closest.</summary>
        private static bool Capsule(Vector3 p, Vector3 q, Vector3 a, Vector3 b, float radius, out float t)
        {
            float s, u;
            float d2 = ClosestPoints(p, q, a, b, out s, out u);
            t = s;
            return d2 <= radius * radius;
        }

        /// <summary>
        /// The squared distance between the segments p1..q1 and p2..q2, and where on each (0..1)
        /// the closest points are (Ericson 2005, 5.1.9).
        /// </summary>
        public static float ClosestPoints(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2, out float s, out float t)
        {
            Vector3 d1 = q1 - p1, d2 = q2 - p2, r = p1 - p2;
            float a = Vector3.Dot(d1, d1), e = Vector3.Dot(d2, d2), f = Vector3.Dot(d2, r);

            if (a <= 1e-12f && e <= 1e-12f)
            {
                s = t = 0f;
                return Vector3.Dot(r, r);
            }

            if (a <= 1e-12f)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= 1e-12f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = denom > 1e-12f ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Mathf.Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Mathf.Clamp01((b - c) / a);
                    }
                }
            }

            Vector3 c1 = p1 + d1 * s, c2 = p2 + d2 * t;
            return (c1 - c2).sqrMagnitude;
        }
    }

    /// <summary>
    /// Where a flag's cloth has been lately, along three axes: each extent grows at once and gives
    /// back slowly, exponentially -- the peak measurement of dynamic range control, an instant
    /// attack and an exponential release (U. Zoelzer, Digital Audio Signal Processing, 2008) --
    /// so the pick volume made from it covers the flag's whole flutter and stands still while the
    /// flag waves, and shrinks back within seconds when the wind drops. PURE.
    /// </summary>
    public struct PickHold
    {
        public float MinU, MaxU, MinV, MaxV, MinN, MaxN;

        /// <summary>False until the first extents are taken (and after <see cref="Clear"/>).</summary>
        public bool Held;

        /// <summary>
        /// The cloth's extents now, <paramref name="dt"/> seconds after the last: a wider one is
        /// taken at once, a narrower one given back to with the time constant
        /// <paramref name="release"/>, seconds.
        /// </summary>
        public void Take(float minU, float maxU, float minV, float maxV, float minN, float maxN, float dt, float release)
        {
            if (!Held)
            {
                MinU = minU; MaxU = maxU;
                MinV = minV; MaxV = maxV;
                MinN = minN; MaxN = maxN;
                Held = true;
                return;
            }

            float k = release > 0f ? 1f - (float)Math.Exp(-Math.Max(dt, 0f) / release) : 1f;
            MinU = Lower(MinU, minU, k); MaxU = Upper(MaxU, maxU, k);
            MinV = Lower(MinV, minV, k); MaxV = Upper(MaxV, maxV, k);
            MinN = Lower(MinN, minN, k); MaxN = Upper(MaxN, maxN, k);
        }

        public void Clear()
        {
            Held = false;
        }

        private static float Lower(float held, float now, float k)
        {
            return now < held ? now : held + (now - held) * k;
        }

        private static float Upper(float held, float now, float k)
        {
            return now > held ? now : held + (now - held) * k;
        }
    }

    /// <summary>
    /// A disc round a flag, from its place, hoist and size alone, never its cloth, over whatever
    /// lies between its heights. Two uses: <see cref="For"/> is everything the cloth can reach
    /// (the city gathered round a flag, FlagRenderer.KeepOffCity); <see cref="Selector"/> is the
    /// disc its highlight is painted with -- Move It's and the bulldozer's (the game's
    /// PropTool.RenderOverlay, patched: FlagOverlayPatch) and the mod's own hover -- which stands
    /// still while the flag waves (the author: "the selector goes all over the place, it's
    /// reacting to the flag animation"). PURE (Unity's Vector3 only).
    /// </summary>
    /// <remarks>
    /// <see cref="For"/> is round everything the cloth can reach, whatever the wind: each particle is held within
    /// its flat distance of its row's hoist point (FlagCloth's long range attachments, the last
    /// constraint of every pass), so the whole cloth lies within the flag's length of its hoist --
    /// the disc spans the hoist's own reach across and that length round it, and the heights that
    /// length below and above it: a limp flag's fly hangs down its length, and a banner whose
    /// crossbar the wind blows along flaps up over it (2 m over a 3 m banner's, in test-cloth).
    /// The pole the mod draws is added whole.
    /// </remarks>
    public struct FlagOutline
    {
        /// <summary>The disc's centre, and its radius, metres.</summary>
        public float X, Z, Radius;

        /// <summary>Painted on what lies between these heights.</summary>
        public float MinY, MaxY;

        /// <summary>Beyond the reach, metres.</summary>
        public const float Margin = 0.5f;

        /// <param name="anchor">The top of the hoist (the prop's position).</param>
        /// <param name="hoist">The hoist's direction from the anchor (unit, FlagOrientation).</param>
        /// <param name="hoistLength">The hoist's length (FlagSpec.Height).</param>
        /// <param name="length">From the hoist to the fly (FlagSpec.Length).</param>
        public static FlagOutline For(Vector3 anchor, Vector3 hoist, float hoistLength, float length)
        {
            Vector3 end = anchor + hoist * hoistLength;
            float dx = end.x - anchor.x, dz = end.z - anchor.z;

            // The length, and a fiftieth more: a halyard's free heading bows a little past it.
            float reach = 1.02f * length + Margin;

            var o = new FlagOutline
            {
                X = 0.5f * (anchor.x + end.x),
                Z = 0.5f * (anchor.z + end.z),
                Radius = 0.5f * Mathf.Sqrt(dx * dx + dz * dz) + reach,
                MinY = Mathf.Min(anchor.y, end.y) - reach,
                MaxY = Mathf.Max(anchor.y, end.y) + reach,
            };
            return o;
        }

        /// <summary>
        /// The flag length up to which <see cref="Selector"/> is <see cref="For"/>'s whole reach:
        /// a regular flag's (1.02 x 4 + 0.5 = 4.58 m round its hoist).
        /// </summary>
        public const float SelectorRegular = 4f;

        /// <summary>The share of the reach past <see cref="SelectorRegular"/>'s that <see cref="Selector"/> keeps.</summary>
        public const float SelectorShare = 0.25f;

        /// <summary>
        /// The highlight's disc (2026-11-11, the author: "The selector around flags, especially for
        /// big flags, is massive ... for regular sized flags it's fine but for bigger ones only"):
        /// <see cref="For"/>'s, its reach as it is up to a <see cref="SelectorRegular"/> m flag
        /// and only <see cref="SelectorShare"/> of the rest past it -- a 3 m flag's disc 3.56 m
        /// round its hoist as before, a 10 m flag's 6.1 m (was 10.7), a 20 m flag's 8.7 m (was
        /// 20.9), a 30 m flag's 11.2 m (was 31.1). It marks the flag; a big flag's fly may reach
        /// past it (the city round the flag is still gathered by <see cref="For"/>). Its heights are
        /// <see cref="For"/>'s: it still paints the cloth and the ground under the pole.
        /// </summary>
        public static FlagOutline Selector(Vector3 anchor, Vector3 hoist, float hoistLength, float length)
        {
            FlagOutline o = For(anchor, hoist, hoistLength, length);
            float reach = 1.02f * length + Margin;
            float regular = 1.02f * SelectorRegular + Margin;
            if (reach > regular)
                o.Radius -= (1f - SelectorShare) * (reach - regular);

            return o;
        }

        /// <summary>The same outline moved by <paramref name="d"/> (the flag drawn where Move It has just put it).</summary>
        public void Shift(Vector3 d)
        {
            X += d.x;
            Z += d.z;
            MinY += d.y;
            MaxY += d.y;
        }

        /// <summary>A point that is part of the flag (its pole's foot, its tip): within the disc and between its heights.</summary>
        public void Include(Vector3 p)
        {
            float dx = p.x - X, dz = p.z - Z;
            Radius = Mathf.Max(Radius, Mathf.Sqrt(dx * dx + dz * dz) + Margin);
            MinY = Mathf.Min(MinY, p.y - Margin);
            MaxY = Mathf.Max(MaxY, p.y + Margin);
        }

        /// <summary>
        /// A colour's alpha as the game fades a big prop's circle (PropTool.CheckOverlayAlpha, its
        /// IL): no more than 2 / sqrt(radius) past a radius of a metre.
        /// </summary>
        public float Alpha(float alpha)
        {
            return Mathf.Min(alpha, 2f / Mathf.Max(1f, Mathf.Sqrt(Radius)));
        }
    }

    /// <summary>The published pick shapes, by prop index (the vanilla game's 65536 props).</summary>
    public static class FlagPicking
    {
        private static readonly PickShape[] ByProp = new PickShape[65536];

        /// <summary>
        /// The props with a shape, for a search along a whole ray (FlagMoveItPick): an array
        /// replaced whole on the main thread, never changed in place, so another thread reads a
        /// consistent one.
        /// </summary>
        private static volatile uint[] _props = new uint[0];

        public static void Publish(uint prop, PickShape shape)
        {
            if (prop >= ByProp.Length)
                return;

            bool had = ByProp[prop] != null;
            ByProp[prop] = shape;
            if (had == (shape != null))
                return;

            uint[] old = _props;
            var now = new System.Collections.Generic.List<uint>(old.Length + 1);
            foreach (uint p in old)
            {
                if (p != prop)
                    now.Add(p);
            }

            if (shape != null)
                now.Add(prop);
            _props = now.ToArray();
        }

        public static PickShape Get(uint prop)
        {
            return prop < ByProp.Length ? ByProp[prop] : null;
        }

        /// <summary>Every prop with a published shape (a snapshot: do not change it).</summary>
        public static uint[] Props
        {
            get { return _props; }
        }

        public static void Clear()
        {
            for (int i = 0; i < ByProp.Length; i++)
                ByProp[i] = null;
            _props = new uint[0];
        }

        /// <summary>
        /// What PropInstance.RayCast answers for a flag (FlagPatches): the slab round its cloth,
        /// its hoist and pole -- not the game's thin cylinder above its anchor. False while the
        /// flag has not been drawn yet.
        /// </summary>
        public static bool RayCast(uint prop, Segment3 ray, out float t, out float targetSqr)
        {
            targetSqr = 0f;
            PickShape shape = Get(prop);
            if (shape == null)
            {
                t = 2f;
                return false;
            }

            return shape.Intersect(ray, out t);
        }

        /// <summary>The same with the wider volumes (PickShape.IntersectWide): what Move It's pointer finds it by (FlagMoveItPick).</summary>
        public static bool RayCastWide(uint prop, Segment3 ray, out float t)
        {
            PickShape shape = Get(prop);
            if (shape == null)
            {
                t = 2f;
                return false;
            }

            return shape.IntersectWide(ray, out t);
        }
    }
}
