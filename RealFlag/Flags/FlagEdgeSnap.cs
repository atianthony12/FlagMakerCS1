using System;

namespace RealFlag.Flags
{
    /// <summary>
    /// A banner hoisted from its top, placed on a roof near its edge, snaps to the edge (the author,
    /// 2026-11-07: "flags that are hoisted from the top are usually placed on the edges of
    /// buildings or balconies"; the snap asked for with the collision's fix). PURE: the tool asks
    /// <see cref="Find"/> on the simulation thread with the game's own raycast as
    /// <c>top</c>, the tests with boxes of their own.
    ///
    /// Found: the edge's line nearest the pointer, the way out from the roof square to it, and the
    /// roof's height at it. <see cref="Place"/> then lays the hoist ALONG the edge, a hair off the
    /// wall's face (the banner hangs flat down the wall), or, the flag turned more across the edge
    /// than along it, OUT from the wall's face square to it (a banner on a bracket, hanging edge-on
    /// to the street) -- the player's own rotation, squared to the wall.
    /// </summary>
    public static class FlagEdgeSnap
    {
        /// <summary>How far from the pointer an edge is looked for, metres.</summary>
        public const float Reach = 2f;

        /// <summary>The steps the looking takes out from the pointer, metres (the bisection finds the edge to a few millimetres after).</summary>
        public const float Step = 0.4f;

        /// <summary>The fall past an edge, metres: at least this much lower than the roof the pointer is on (a parapet's lip is not an edge).</summary>
        public const float Drop = 1f;

        /// <summary>The hoist's gap from the wall's face, metres: off it, not in it.</summary>
        public const float Gap = 0.03f;

        /// <summary>The ways looked: eight round the pointer; the edge's own line is found after, from two points either side.</summary>
        private const int Ways = 8;

        /// <summary>The two points the edge's line is found from lie this far either side of the way it was found, metres.</summary>
        private const float Side = 0.5f;

        private const int Bisections = 7;

        /// <summary>
        /// The edge of the surface the pointer is on (x, y, z), within <see cref="Reach"/>: where it
        /// falls by <see cref="Drop"/> or more. <paramref name="top"/>(x, z) is the height of the
        /// first surface straight down from just above y at that place, or float.NegativeInfinity
        /// if none. Out: (ex, ez) on the edge's line nearest (x, z), (nx, nz) level and out from the
        /// roof, roofY the roof's height just inside the edge. False: no edge in reach.
        /// </summary>
        public static bool Find(Func<float, float, float> top, float x, float y, float z,
                                out float ex, out float ez, out float nx, out float nz, out float roofY)
        {
            ex = x; ez = z; nx = 1f; nz = 0f; roofY = y;
            if (top == null)
                return false;

            // The nearest of eight ways the surface falls away.
            float best = float.MaxValue, bx = 0f, bz = 0f;
            for (int w = 0; w < Ways; w++)
            {
                double a = w * (2.0 * Math.PI / Ways);
                float dx = (float)Math.Cos(a), dz = (float)Math.Sin(a);
                for (float r = Step; r <= Reach + 1e-4f && r < best; r += Step)
                {
                    float h = top(x + r * dx, z + r * dz);
                    if (h < y - Drop)
                    {
                        best = r;
                        bx = dx;
                        bz = dz;
                        break;
                    }

                    // A wall rising this way, not an edge.
                    if (h > y + Drop)
                        break;
                }
            }

            if (best == float.MaxValue)
                return false;

            // The edge's line: found along that way from two points either side of the pointer.
            float sx = -bz, sz = bx;
            float d0 = EdgeAlong(top, x, y, z, bx, bz, best), d1, d2;
            bool one = TryEdgeAlong(top, x + Side * sx, y, z + Side * sz, bx, bz, out d1);
            bool two = TryEdgeAlong(top, x - Side * sx, y, z - Side * sz, bx, bz, out d2);

            float px = x + d0 * bx, pz = z + d0 * bz;
            if (one && two)
            {
                // Its line through the two, out from the roof the way it was found.
                float ax = x + Side * sx + d1 * bx, az = z + Side * sz + d1 * bz;
                float cx = x - Side * sx + d2 * bx, cz = z - Side * sz + d2 * bz;
                float tx = ax - cx, tz = az - cz;
                float len = (float)Math.Sqrt(tx * tx + tz * tz);
                if (len > 1e-4f)
                {
                    tx /= len;
                    tz /= len;
                    nx = tz;
                    nz = -tx;
                    if (nx * bx + nz * bz < 0f)
                    {
                        nx = -nx;
                        nz = -nz;
                    }

                    // The edge's point nearest the pointer.
                    float along = (x - cx) * tx + (z - cz) * tz;
                    px = cx + along * tx;
                    pz = cz + along * tz;
                }
                else
                {
                    nx = bx;
                    nz = bz;
                }
            }
            else
            {
                // A corner or a short ledge: square to the way it was found.
                nx = bx;
                nz = bz;
            }

            ex = px;
            ez = pz;

            // The roof's height just inside the edge (a sloping roof's, not the pointer's).
            float inside = top(px - 0.1f * nx, pz - 0.1f * nz);
            roofY = inside >= y - Drop && inside <= y + Drop ? inside : y;
            return true;
        }

        /// <summary>The edge's distance along (dx, dz) from (x, z), between the step before <paramref name="found"/> and it, by bisection.</summary>
        private static float EdgeAlong(Func<float, float, float> top, float x, float y, float z, float dx, float dz, float found)
        {
            float lo = Math.Max(0f, found - Step), hi = found;
            for (int i = 0; i < Bisections; i++)
            {
                float m = 0.5f * (lo + hi);
                if (top(x + m * dx, z + m * dz) < y - Drop)
                    hi = m;
                else
                    lo = m;
            }

            return 0.5f * (lo + hi);
        }

        /// <summary>The same from a point beside the pointer: on the roof itself, and the edge within reach of it along the same way.</summary>
        private static bool TryEdgeAlong(Func<float, float, float> top, float x, float y, float z, float dx, float dz, out float distance)
        {
            distance = 0f;
            float here = top(x, z);
            if (here < y - Drop || here > y + Drop)
                return false;

            for (float r = Step; r <= Reach + Step + 1e-4f; r += Step)
            {
                float h = top(x + r * dx, z + r * dz);
                if (h > y + Drop)
                    return false;

                if (h < y - Drop)
                {
                    distance = EdgeAlong(top, x, y, z, dx, dz, r);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Where a banner hoisted from its top goes on an edge found by <see cref="Find"/>: its
        /// anchor (the hoist's first corner, the prop's place) at the roof's height, and its
        /// heading (radians, the prop's angle: the hoist runs from the anchor along (cos, sin)).
        /// Turned (<paramref name="angle"/>) nearer along the edge than across it, the hoist lies
        /// along the edge, its middle at the edge's point, <see cref="Gap"/> off the wall's face;
        /// else it runs out from the face, square to it. Whichever way of the two is nearer the
        /// player's is kept (the side of the banner that faces out).
        /// </summary>
        public static void Place(float ex, float roofY, float ez, float nx, float nz, float angle, float hoist,
                                 out float ax, out float ay, out float az, out float heading)
        {
            float hx = (float)Math.Cos(angle), hz = (float)Math.Sin(angle);
            float tx = -nz, tz = nx;
            float along = hx * tx + hz * tz, across = hx * nx + hz * nz;
            float dx, dz;
            float fx = ex + Gap * nx, fz = ez + Gap * nz;
            if (Math.Abs(along) >= Math.Abs(across))
            {
                float s = along >= 0f ? 1f : -1f;
                dx = s * tx;
                dz = s * tz;
                ax = fx - 0.5f * hoist * dx;
                az = fz - 0.5f * hoist * dz;
            }
            else
            {
                // Out from the face: the anchor at the face, or at the hoist's far end running back to it.
                float s = across >= 0f ? 1f : -1f;
                dx = s * nx;
                dz = s * nz;
                ax = s > 0f ? fx : fx + hoist * nx;
                az = s > 0f ? fz : fz + hoist * nz;
            }

            ay = roofY;
            heading = (float)Math.Atan2(dz, dx);
        }
    }
}
