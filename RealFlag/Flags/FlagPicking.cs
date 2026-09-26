using ColossalFramework.Math;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// A flag as the pointer finds it, in world space: a coarse sheet of triangles over the drawn
    /// cloth (every few particles), a slab round the cloth, and capsules round its hoist and its
    /// drawn pole, with their box. Written on the main thread by the renderer, read by raycasts on
    /// any thread (the game's tools cast on the simulation thread): each flag has two, and the
    /// renderer fills the one not published, then publishes it with one reference store.
    /// </summary>
    /// <remarks>
    /// The sheet alone was a thin target (the author: "Move it: the selector is too small"): a
    /// flag seen edge on is a line, a fluttering one's coarse sheet lagged its cloth by up to a
    /// tenth of a second and cut the corners of its folds. So a ray also picks the flag within
    /// the SLAB -- the cloth's own box in its own axes (along its hoist, out along its fly, and
    /// across), widened across by a margin -- and within a radius of its hoist edge and of the
    /// pole the mod draws: the closest points of two segments (Ericson, Real-Time Collision
    /// Detection, 2005, 5.1.9).
    /// </remarks>
    public sealed class PickShape
    {
        public float[] Triangles = new float[0];
        public int Count;
        public Bounds Box;
        public Vector3 Anchor;

        /// <summary>The slab: its centre, its three unit axes, and its half sizes along them.</summary>
        public Vector3 SlabCentre, SlabU, SlabV, SlabN;
        public float HalfU, HalfV, HalfN;
        public bool HasSlab;

        /// <summary>Capsules round the hoist edge and the drawn pole: segments and radii (a radius of 0: none).</summary>
        public Vector3 HoistA, HoistB, PoleA, PoleB;
        public float HoistRadius, PoleRadius;

        /// <summary>
        /// The segment a..b against the flag: where along it (0..1) it first meets the cloth, the
        /// slab or a capsule.
        /// </summary>
        public bool Intersect(Segment3 ray, out float t)
        {
            t = 2f;
            Vector3 origin = ray.a;
            Vector3 dir = ray.b - ray.a;

            var box = Box;
            box.Expand(0.2f);
            if (!box.IntersectRay(new Ray(origin, dir)))
                return false;

            bool hit = false;
            float[] v = Triangles;
            for (int i = 0; i < Count; i++)
            {
                int o = i * 9;
                float ax = v[o], ay = v[o + 1], az = v[o + 2];
                float e1x = v[o + 3] - ax, e1y = v[o + 4] - ay, e1z = v[o + 5] - az;
                float e2x = v[o + 6] - ax, e2y = v[o + 7] - ay, e2z = v[o + 8] - az;

                float px = dir.y * e2z - dir.z * e2y;
                float py = dir.z * e2x - dir.x * e2z;
                float pz = dir.x * e2y - dir.y * e2x;
                float det = e1x * px + e1y * py + e1z * pz;
                if (det > -1e-9f && det < 1e-9f)
                    continue;

                float inv = 1f / det;
                float sx = origin.x - ax, sy = origin.y - ay, sz = origin.z - az;
                float u = (sx * px + sy * py + sz * pz) * inv;
                if (u < 0f || u > 1f)
                    continue;

                float qx = sy * e1z - sz * e1y;
                float qy = sz * e1x - sx * e1z;
                float qz = sx * e1y - sy * e1x;
                float w = (dir.x * qx + dir.y * qy + dir.z * qz) * inv;
                if (w < 0f || u + w > 1f)
                    continue;

                float along = (e2x * qx + e2y * qy + e2z * qz) * inv;
                if (along >= 0f && along <= 1f && along < t)
                {
                    t = along;
                    hit = true;
                }
            }

            float s;
            if (HasSlab && SlabEntry(origin, dir, SlabCentre, SlabU, SlabV, SlabN, HalfU, HalfV, HalfN, out s) && s < t)
            {
                t = s;
                hit = true;
            }

            if (HoistRadius > 0f && Capsule(origin, ray.b, HoistA, HoistB, HoistRadius, out s) && s < t)
            {
                t = s;
                hit = true;
            }

            if (PoleRadius > 0f && Capsule(origin, ray.b, PoleA, PoleB, PoleRadius, out s) && s < t)
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
        /// What PropInstance.RayCast answers for a flag (FlagPatches): its cloth, the slab round
        /// it, its hoist and pole -- not the game's thin cylinder above its anchor. False while the
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
    }
}
