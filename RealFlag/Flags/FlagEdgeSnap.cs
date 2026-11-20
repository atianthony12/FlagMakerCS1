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
            Place(ex, roofY, ez, nx, nz, angle, hoist, false, out ax, out ay, out az, out heading);
        }

        /// <summary>
        /// The same; with <paramref name="frontOut"/> (a banner with a back plate: FlagSpec.HasBackPlate)
        /// a banner along the edge always has its front -- the side its design reads from, in front
        /// of its plate -- out from the wall: the plate between the cloth and the wall, whichever
        /// way along the edge the player turned it.
        /// </summary>
        public static void Place(float ex, float roofY, float ez, float nx, float nz, float angle, float hoist, bool frontOut,
                                 out float ax, out float ay, out float az, out float heading)
        {
            float hx = (float)Math.Cos(angle), hz = (float)Math.Sin(angle);
            float tx = -nz, tz = nx;
            float along = hx * tx + hz * tz, across = hx * nx + hz * nz;
            float dx, dz;
            float fx = ex + Gap * nx, fz = ez + Gap * nz;
            if (Math.Abs(along) >= Math.Abs(across))
            {
                // Heading along (tx, tz), the front (FlagRenderer.ApplyPlate: (sin, -cos) of the heading) is (nx, nz): out.
                float s = along >= 0f || frontOut ? 1f : -1f;
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

        /// <summary>
        /// The same, turned by the player's right button while snapped (the author, 2026-11-19:
        /// "allow the user to right click to rotate even then"): <paramref name="turn"/> radians
        /// on top of the squared heading <see cref="Place(float, float, float, float, float, float, float, bool, out float, out float, out float, out float)"/>
        /// gives. The hoist keeps its middle where the edge's point is along the edge and is put
        /// out until its end nearer the wall is <see cref="Gap"/> off the face: never into the
        /// roof. A turn of 0 is Place's own answer, bit for bit; a quarter turn lands on Place's
        /// other squared ways (out from the face, along it).
        /// </summary>
        public static void Place(float ex, float roofY, float ez, float nx, float nz, float angle, float hoist, bool frontOut, float turn,
                                 out float ax, out float ay, out float az, out float heading)
        {
            Place(ex, roofY, ez, nx, nz, angle, hoist, frontOut, out ax, out ay, out az, out heading);
            if (turn == 0f)
                return;

            heading += turn;
            float hx = (float)Math.Cos(heading), hz = (float)Math.Sin(heading);
            float push = 0.5f * hoist * Math.Abs(hx * nx + hz * nz);
            float fx = ex + Gap * nx, fz = ez + Gap * nz;
            ax = fx - 0.5f * hoist * hx + push * nx;
            az = fz - 0.5f * hoist * hz + push * nz;
            ay = roofY;
        }

        // ---- walls (2026-11-19) ---------------------------------------------------------------
        //
        // The author: "Presets for flag orientation, like flags on buildings ... Wall Flag ...
        // easier for users to make flags that go on building walls. So flagpole should be very
        // small, rotation maybe 45deg". A new flag on a short pole tilted out (its top towards its
        // fly), the pointer on a building's wall: the pole's foot goes on the wall's face where the
        // pointer is, the flag heading straight out of it. The face is found on the surface the
        // cloth keeps off (FlagSurroundings.Surface, the buildings as drawn: a height above each
        // point), as the roof's edge is: where, along the way the pointer looks, the city rises
        // from below the pointer to above it. A height field holds no overhang: under a cornice or
        // a balcony the face found is the overhang's outer line, not the wall's.

        /// <summary>
        /// The least tilt (degrees, towards the fly) a pole snaps to a wall at: a pole near upright
        /// is a flag standing in front of a building, not one on it. The most is where FlagStand
        /// stops calling the pole upright (60 degrees with no lean: a crossbar past it).
        /// </summary>
        public const float WallTiltMin = 10f;

        /// <summary>The face is looked for from this far in front of where the pointer's ray met the wall, metres (the drawn face can stand out of the game's simple one).</summary>
        public const float WallBack = 1f;

        /// <summary>And to this far into it, metres.</summary>
        public const float WallInto = 1f;

        /// <summary>The steps of that look, metres: half the surface grid's node (its MinSpacing 0.1 m).</summary>
        public const float WallStep = 0.05f;

        /// <summary>The heights in front of the face and inside it are read this far either side of where the city first rises over the pointer, metres.</summary>
        public const float WallProbe = 0.3f;

        /// <summary>A wall: the city in front of it at least this far under the pointer, metres (a kerb or a step is not one) ...</summary>
        public const float WallBelow = 0.3f;

        /// <summary>... and the building at least this far over it (the pointer in its wall's top few centimetres is on the roof's edge).</summary>
        public const float WallAbove = 0.25f;

        /// <summary>The pole's foot this far off the face, metres, beyond its widest (FlagPoleShape.FootReach).</summary>
        public const float WallGap = 0.01f;

        /// <summary>
        /// A face found standing more than this in front of where the pointer's ray met the wall,
        /// metres, is no wall to snap to: a height field holds no overhang, so under a cornice, a
        /// balcony or an awning the face found is the overhang's outer line, and a pole's foot
        /// there would stand in the air. Placed as off a wall then. (The drawn face may stand out
        /// of the game's simple one the ray met by as much: such a wall is let go too.)
        /// </summary>
        public const float WallAgree = 0.3f;

        /// <summary>A face found turned more than this from square to the pointer's way (its cosine) is taken square to it: the two points beside fell on another face.</summary>
        private const float WallMostTurn = 0.17f;

        /// <summary>
        /// A flag that snaps to walls (with Settings.NewSnapWalls): a drawn pole (FlagPoleStyle
        /// not None) by its side hoist, tilted towards its fly by <see cref="WallTiltMin"/> or more,
        /// and still standing on its foot (FlagStand.Upright: under 60 degrees with no lean).
        /// </summary>
        public static bool WallFlag(int pole, int hoistedFrom, float tilt, float lean)
        {
            if (pole == FlagPoleStyle.None || hoistedFrom == FlagHoist.Top || !(tilt >= WallTiltMin))
                return false;

            float hx, hy, hz, fx, fy, fz;
            FlagOrientation.Frame(0f, tilt, lean, hoistedFrom, out hx, out hy, out hz, out fx, out fy, out fz);
            return FlagStand.Upright(hy);
        }

        public static bool WallFlag(FlagSpec spec)
        {
            return WallFlag(spec.Pole, spec.HoistedFrom, spec.Tilt, spec.Lean);
        }

        /// <summary>
        /// The wall the pointer's ray met at (x, y, z), going (dx, dz) level (into the wall):
        /// <paramref name="top"/>(x, z) is the city's height there (float.NaN off what is known).
        /// Out: (fx, fz) the face where the pointer looks, (nx, nz) level and out of it. False: no
        /// wall there -- the city in front not <see cref="WallBelow"/> under the pointer, or the
        /// building not <see cref="WallAbove"/> over it, or nothing rising within reach, or the
        /// face found more than <see cref="WallAgree"/> in front of (x, z) (an overhang).
        /// </summary>
        /// <remarks>
        /// The face is where the height crosses halfway between the city in front and the building
        /// (a crossing at the pointer's own height would slide along the grid's slope with it): a
        /// grid grows a wall up to half a node out into a one-node slope (ClothColliders.Raise), so
        /// the face found is 0 to one node (0.1 m) outside the drawn one, never inside it. Its way
        /// from two more crossings half a metre either side, as the roof's edge's.
        /// </remarks>
        public static bool FindWall(Func<float, float, float> top, float x, float y, float z, float dx, float dz,
                                    out float fx, out float fz, out float nx, out float nz)
        {
            fx = x; fz = z; nx = 0f; nz = 0f;
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (top == null || len < 1e-3f)
                return false;

            dx /= len;
            dz /= len;
            nx = -dx;
            nz = -dz;

            // Along the pointer's way: the first place the city stands at the pointer's height.
            float lo, hi;
            if (!FirstRise(top, x, z, dx, dz, y, -WallBack, WallInto, out lo, out hi))
                return false;

            float before = top(x + (lo - WallProbe) * dx, z + (lo - WallProbe) * dz);
            float inside = top(x + (hi + WallProbe) * dx, z + (hi + WallProbe) * dz);
            if (float.IsNaN(before) || float.IsNaN(inside) || before > y - WallBelow || inside < y + WallAbove)
                return false;

            float level = 0.5f * (before + inside);
            float d0;
            if (!Crossing(top, x, z, dx, dz, level, -WallBack - WallProbe, WallInto + WallProbe, out d0))
                return false;

            fx = x + d0 * dx;
            fz = z + d0 * dz;

            // The face's line through two crossings beside, out of the wall.
            float sx = -dz, sz = dx, d1, d2;
            float reach = WallBack + WallProbe + Side;
            if (Crossing(top, x + Side * sx, z + Side * sz, dx, dz, level, -reach, WallInto + WallProbe + Side, out d1) &&
                Crossing(top, x - Side * sx, z - Side * sz, dx, dz, level, -reach, WallInto + WallProbe + Side, out d2))
            {
                float ax = x + Side * sx + d1 * dx, az = z + Side * sz + d1 * dz;
                float cx = x - Side * sx + d2 * dx, cz = z - Side * sz + d2 * dz;
                float tx = ax - cx, tz = az - cz;
                float tl = (float)Math.Sqrt(tx * tx + tz * tz);
                if (tl > 1e-4f)
                {
                    float ux = tz / tl, uz = -tx / tl;
                    if (ux * dx + uz * dz > 0f)
                    {
                        ux = -ux;
                        uz = -uz;
                    }

                    if (-(ux * dx + uz * dz) >= WallMostTurn)
                    {
                        nx = ux;
                        nz = uz;
                    }
                }
            }

            // An overhang over the pointer: its outer line, not the wall (WallAgree).
            return (fx - x) * nx + (fz - z) * nz <= WallAgree;
        }

        /// <summary>
        /// Where a wall flag's pole stands on a face found by <see cref="FindWall"/>: its foot
        /// <paramref name="footReach"/> (FlagPoleShape.FootReach) and <see cref="WallGap"/> out of
        /// the face, at <paramref name="y"/>; its heading (radians, the prop's angle) straight out.
        /// </summary>
        public static void OnWall(float fx, float y, float fz, float nx, float nz, float footReach,
                                  out float footX, out float footY, out float footZ, out float heading)
        {
            float off = Math.Max(0f, footReach) + WallGap;
            footX = fx + off * nx;
            footY = y;
            footZ = fz + off * nz;
            heading = (float)Math.Atan2(nz, nx);
        }

        /// <summary>
        /// The same, the flag turned <paramref name="turn"/> radians from straight out by the
        /// player's right button (2026-11-19): the foot stays (it is round: FootReach is its
        /// widest every way), the pole leans and the flag flies the turned way. Not held short
        /// of the wall: turned past a quarter, the pole leans into it, as the player chose.
        /// </summary>
        public static void OnWall(float fx, float y, float fz, float nx, float nz, float footReach, float turn,
                                  out float footX, out float footY, out float footZ, out float heading)
        {
            OnWall(fx, y, fz, nx, nz, footReach, out footX, out footY, out footZ, out heading);
            heading += turn;
        }

        /// <summary>
        /// Stepping (dx, dz) from (x, z) between <paramref name="from"/> and <paramref name="to"/>
        /// metres: the step before and the step where the height first reaches
        /// <paramref name="level"/>. False if it starts there already, or never does, or leaves
        /// what is known.
        /// </summary>
        private static bool FirstRise(Func<float, float, float> top, float x, float z, float dx, float dz, float level, float from, float to,
                                      out float lo, out float hi)
        {
            lo = hi = 0f;
            float h = top(x + from * dx, z + from * dz);
            if (float.IsNaN(h) || h >= level)
                return false;

            for (float s = from + WallStep; s <= to + 1e-4f; s += WallStep)
            {
                h = top(x + s * dx, z + s * dz);
                if (float.IsNaN(h))
                    return false;

                if (h >= level)
                {
                    lo = s - WallStep;
                    hi = s;
                    return true;
                }
            }

            return false;
        }

        /// <summary>The first crossing of <paramref name="level"/> stepping along (dx, dz), found to a millimetre or so by bisection.</summary>
        private static bool Crossing(Func<float, float, float> top, float x, float z, float dx, float dz, float level, float from, float to, out float distance)
        {
            distance = 0f;
            float lo, hi;
            if (!FirstRise(top, x, z, dx, dz, level, from, to, out lo, out hi))
                return false;

            for (int i = 0; i < 6; i++)
            {
                float m = 0.5f * (lo + hi);
                float h = top(x + m * dx, z + m * dz);
                if (!float.IsNaN(h) && h >= level)
                    hi = m;
                else
                    lo = m;
            }

            distance = 0.5f * (lo + hi);
            return true;
        }
    }
}
