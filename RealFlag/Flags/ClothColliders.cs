using System;

namespace RealFlag.Flags
{
    /// <summary>
    /// The top of whatever stands round a flag -- the ground, the water, the roofs -- on a square
    /// grid of heights, for its cloth to stay above (FlagCloth.SetSurface). PURE: gathered from the
    /// game on the main thread (FlagSurroundings), read by the cloth's thread, never changed once
    /// handed over.
    /// </summary>
    /// <remarks>
    /// Coordinates are metres relative to a point of the world (the flag's anchor when the grid was
    /// gathered), on the world's axes, y up. Node (c, r) stands at (MinX + c Spacing, MinZ + r
    /// Spacing). A node's height is what a ray cast straight down from the sky would meet first
    /// there: the terrain or the water, raised to every triangle of a building whose shadow
    /// straight down covers the node (<see cref="Raise"/>).
    /// Between the nodes the surface is bilinear: a wall is a steep slope one node wide, a roof a
    /// plateau. What the grid cannot hold -- an overhang, a roof over the flag -- is solid down to
    /// the ground: a cloth whose own hoist stands under the surface does not use it at all (FlagCloth).
    /// </remarks>
    public sealed class SurfaceGrid
    {
        /// <summary>
        /// The finest spacing, metres: a 1 m flag's grid is no finer (its cells are 8 cm).
        /// </summary>
        public const float MinSpacing = 0.1f;

        /// <summary>
        /// At most this many nodes along a side: a 3 m flag's grid is 81 x 81 at 11 cm, an 8 m
        /// flag's at 27 cm, a 30 m flag's at 95 cm. A wall is a slope one spacing wide
        /// (<see cref="Raise"/>): at 41 a side a 3 m flag blown at a wall went 4-7 cm into it, the
        /// cloth drawn torn where the wall hid it (the GPU preview's city-wall picture).
        /// </summary>
        public const int MaxNodes = 81;

        /// <summary>The coarse maxima's blocks, cells a side: the early out's lookup (<see cref="TopWithin"/>).</summary>
        private const int Block = 8;

        public readonly int Columns, Rows;
        public readonly float MinX, MinZ, Spacing;

        /// <summary>Node (c, r) at [r * Columns + c], metres relative to the grid's point.</summary>
        public readonly float[] Heights;

        private readonly int _blockColumns, _blockRows;
        private readonly float[] _blockTop;

        /// <summary>The highest node (after <see cref="Finish"/>).</summary>
        public float Top { get; private set; }

        public SurfaceGrid(float minX, float minZ, float spacing, int columns, int rows, float fill)
        {
            Columns = Math.Max(2, columns);
            Rows = Math.Max(2, rows);
            MinX = minX;
            MinZ = minZ;
            Spacing = Math.Max(spacing, 1e-3f);
            Heights = new float[Columns * Rows];
            for (int k = 0; k < Heights.Length; k++)
                Heights[k] = fill;

            _blockColumns = (Columns - 2) / Block + 1;
            _blockRows = (Rows - 2) / Block + 1;
            _blockTop = new float[_blockColumns * _blockRows];
            Top = fill;
        }

        /// <summary>
        /// The layout for a square <paramref name="half"/> metres either side of its middle: the
        /// spacing (at least <see cref="MinSpacing"/>) and the nodes a side (at most <see cref="MaxNodes"/>).
        /// </summary>
        public static void Layout(float half, out float spacing, out int nodes)
        {
            half = Math.Max(half, MinSpacing);
            spacing = Math.Max(MinSpacing, 2f * half / (MaxNodes - 1));
            nodes = Math.Min(MaxNodes, (int)Math.Ceiling(2f * half / spacing - 1e-4f) + 1);
        }

        public float MaxX
        {
            get { return MinX + (Columns - 1) * Spacing; }
        }

        public float MaxZ
        {
            get { return MinZ + (Rows - 1) * Spacing; }
        }

        /// <summary>
        /// Raises the nodes under a triangle to it: each node whose (x, z) lies in the triangle's
        /// shadow straight down takes the triangle's height there, if that is higher -- the first
        /// surface a ray from the sky meets (Building.RayCast tests the same triangles, its IL) --
        /// and so does each node within half a spacing of that shadow, at the height of the
        /// shadow's nearest point. A wall, seen from above, has no area and raises nothing: the
        /// roofs above it do, and a wall is the slope between the last node off its roof and the
        /// first on it. Grown by half a spacing, that slope lies outside the wall's face up to half
        /// the wall's height, and at most half a spacing inside it at the top: a cloth blown at a
        /// wall stops a few centimetres short of it rather than going into it (a stand-off reads
        /// as cloth billowing; cloth the wall hides reads as torn).
        /// </summary>
        public void Raise(float ax, float ay, float az, float bx, float by, float bz, float cx, float cy, float cz)
        {
            float grow = 0.5f * Spacing;
            float minX = Math.Min(ax, Math.Min(bx, cx)) - grow, maxX = Math.Max(ax, Math.Max(bx, cx)) + grow;
            float minZ = Math.Min(az, Math.Min(bz, cz)) - grow, maxZ = Math.Max(az, Math.Max(bz, cz)) + grow;
            float inv = 1f / Spacing;

            // A hair of slack, so a node on an edge shared by two triangles is not missed by both.
            int c0 = Math.Max(0, (int)Math.Ceiling((minX - MinX) * inv - 1e-4f));
            int c1 = Math.Min(Columns - 1, (int)Math.Floor((maxX - MinX) * inv + 1e-4f));
            int r0 = Math.Max(0, (int)Math.Ceiling((minZ - MinZ) * inv - 1e-4f));
            int r1 = Math.Min(Rows - 1, (int)Math.Floor((maxZ - MinZ) * inv + 1e-4f));
            if (c0 > c1 || r0 > r1)
                return;

            float grow2 = grow * grow;
            float det = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
            if (Math.Abs(det) < 1e-8f)
            {
                // A wall: no area seen from above, its shadow a segment -- its two corners farthest
                // apart. The nodes within half a spacing of it take its top.
                float ab = (bx - ax) * (bx - ax) + (bz - az) * (bz - az);
                float bc = (cx - bx) * (cx - bx) + (cz - bz) * (cz - bz);
                float ca = (ax - cx) * (ax - cx) + (az - cz) * (az - cz);
                float px = ax, pz = az, qx = bx, qz = bz;
                if (bc >= ab && bc >= ca) { px = bx; pz = bz; qx = cx; qz = cz; }
                else if (ca >= ab && ca >= bc) { px = cx; pz = cz; qx = ax; qz = az; }
                float top = Math.Max(ay, Math.Max(by, cy));
                for (int r = r0; r <= r1; r++)
                {
                    float z = MinZ + r * Spacing;
                    for (int c = c0; c <= c1; c++)
                    {
                        float y = float.MinValue;
                        Edge(MinX + c * Spacing, z, px, top, pz, qx, top, qz, grow2, ref y);
                        int k = r * Columns + c;
                        if (y > Heights[k])
                            Heights[k] = y;
                    }
                }

                return;
            }

            // Each barycentric times its corner's height over the opposite edge is the distance to
            // that edge's line: past half a spacing beyond any, no edge is within reach.
            float invDet = 1f / det, area = Math.Abs(det);
            float ha = area / Math.Max((float)Math.Sqrt((cx - bx) * (cx - bx) + (cz - bz) * (cz - bz)), 1e-6f);
            float hb = area / Math.Max((float)Math.Sqrt((ax - cx) * (ax - cx) + (az - cz) * (az - cz)), 1e-6f);
            float hc = area / Math.Max((float)Math.Sqrt((bx - ax) * (bx - ax) + (bz - az) * (bz - az)), 1e-6f);
            const float Slack = 1e-4f;
            for (int r = r0; r <= r1; r++)
            {
                float z = MinZ + r * Spacing;
                for (int c = c0; c <= c1; c++)
                {
                    float x = MinX + c * Spacing;
                    float l1 = ((bz - cz) * (x - cx) + (cx - bx) * (z - cz)) * invDet;
                    float l2 = ((cz - az) * (x - cx) + (ax - cx) * (z - cz)) * invDet;
                    float l3 = 1f - l1 - l2;
                    float y;
                    if (l1 >= -Slack && l2 >= -Slack && l3 >= -Slack)
                    {
                        y = l1 * ay + l2 * by + l3 * cy;
                    }
                    else
                    {
                        if (l1 * ha < -grow || l2 * hb < -grow || l3 * hc < -grow)
                            continue;

                        // Just outside the shadow: the highest edge within half a spacing, there.
                        y = float.MinValue;
                        Edge(x, z, ax, ay, az, bx, by, bz, grow2, ref y);
                        Edge(x, z, bx, by, bz, cx, cy, cz, grow2, ref y);
                        Edge(x, z, cx, cy, cz, ax, ay, az, grow2, ref y);
                        if (y == float.MinValue)
                            continue;
                    }

                    int k = r * Columns + c;
                    if (y > Heights[k])
                        Heights[k] = y;
                }
            }
        }

        /// <summary>
        /// The edge from a to b, seen from above: if (x, z) is within reach of it (squared, level),
        /// its height at the point nearest (x, z) -- or, along an upright edge, its top -- raises
        /// <paramref name="height"/>.
        /// </summary>
        private static void Edge(float x, float z, float ax, float ay, float az, float bx, float by, float bz, float reach2, ref float height)
        {
            float ex = bx - ax, ez = bz - az;
            float len2 = ex * ex + ez * ez;
            float t = len2 > 1e-12f ? Math.Max(0f, Math.Min(1f, ((x - ax) * ex + (z - az) * ez) / len2)) : 0f;
            float dx = ax + ex * t - x, dz = az + ez * t - z;
            if (dx * dx + dz * dz > reach2)
                return;

            float y = len2 > 1e-12f ? ay + (by - ay) * t : Math.Max(ay, by);
            if (y > height)
                height = y;
        }

        /// <summary>Every node set: the coarse maxima for <see cref="TopWithin"/> and <see cref="Top"/>.</summary>
        public void Finish()
        {
            float top = float.MinValue;
            for (int br = 0; br < _blockRows; br++)
            {
                for (int bc = 0; bc < _blockColumns; bc++)
                {
                    int c0 = bc * Block, c1 = Math.Min(Columns - 1, c0 + Block);
                    int r0 = br * Block, r1 = Math.Min(Rows - 1, r0 + Block);
                    float m = float.MinValue;
                    for (int r = r0; r <= r1; r++)
                    {
                        for (int c = c0; c <= c1; c++)
                        {
                            float h = Heights[r * Columns + c];
                            if (h > m)
                                m = h;
                        }
                    }

                    _blockTop[br * _blockColumns + bc] = m;
                    if (m > top)
                        top = m;
                }
            }

            Top = top;
        }

        /// <summary>The highest node near the square [x0, x1] x [z0, z1] (no lower than any in it); float.MinValue if the grid is nowhere under it.</summary>
        public float TopWithin(float x0, float z0, float x1, float z1)
        {
            float inv = 1f / Spacing;
            int c0 = (int)Math.Floor((x0 - MinX) * inv), c1 = (int)Math.Floor((x1 - MinX) * inv);
            int r0 = (int)Math.Floor((z0 - MinZ) * inv), r1 = (int)Math.Floor((z1 - MinZ) * inv);
            if (c1 < 0 || r1 < 0 || c0 > Columns - 2 || r0 > Rows - 2)
                return float.MinValue;

            int bc0 = Math.Max(0, c0) / Block, bc1 = Math.Min(Columns - 2, c1) / Block;
            int br0 = Math.Max(0, r0) / Block, br1 = Math.Min(Rows - 2, r1) / Block;
            float m = float.MinValue;
            for (int br = br0; br <= br1; br++)
            {
                for (int bc = bc0; bc <= bc1; bc++)
                {
                    float h = _blockTop[br * _blockColumns + bc];
                    if (h > m)
                        m = h;
                }
            }

            return m;
        }

        /// <summary>The surface's height at (x, z) and its slopes along x and z (bilinear between the nodes); false outside the grid.</summary>
        public bool Sample(float x, float z, out float height, out float slopeX, out float slopeZ)
        {
            float inv = 1f / Spacing;
            float fx = (x - MinX) * inv, fz = (z - MinZ) * inv;
            if (fx < 0f || fz < 0f || fx > Columns - 1 || fz > Rows - 1)
            {
                height = 0f;
                slopeX = 0f;
                slopeZ = 0f;
                return false;
            }

            int c = Math.Min((int)fx, Columns - 2), r = Math.Min((int)fz, Rows - 2);
            float tx = fx - c, tz = fz - r;
            int k = r * Columns + c;
            float h00 = Heights[k], h10 = Heights[k + 1], h01 = Heights[k + Columns], h11 = Heights[k + Columns + 1];
            height = (h00 + (h10 - h00) * tx) * (1f - tz) + (h01 + (h11 - h01) * tx) * tz;
            slopeX = ((h10 - h00) * (1f - tz) + (h11 - h01) * tz) * inv;
            slopeZ = ((h01 - h00) * (1f - tx) + (h11 - h10) * tx) * inv;
            return true;
        }
    }

    /// <summary>
    /// Upright capsules the cloth keeps off (FlagCloth.SetCapsules): the city's props and trees as
    /// the game itself picks them (PropInstance.RayCast, TreeInstance.RayCast, their IL) -- a
    /// segment up the object's axis, its radius an eighth of its width plus its depth. PURE; never
    /// changed once handed over. Metres relative to a point of the world, as <see cref="SurfaceGrid"/>.
    /// </summary>
    public sealed class CapsuleSet
    {
        /// <summary>Each capsule's axis at (X, Z) from Bottom to Top (the segment's ends), and its radius round it.</summary>
        public float[] X, Z, Bottom, Top, Radius;
        public int Count;

        public CapsuleSet(int capacity = 16)
        {
            capacity = Math.Max(capacity, 4);
            X = new float[capacity];
            Z = new float[capacity];
            Bottom = new float[capacity];
            Top = new float[capacity];
            Radius = new float[capacity];
        }

        public void Add(float x, float z, float bottom, float top, float radius)
        {
            if (radius <= 0f)
                return;

            if (Count == X.Length)
            {
                int n = Count * 2;
                Array.Resize(ref X, n);
                Array.Resize(ref Z, n);
                Array.Resize(ref Bottom, n);
                Array.Resize(ref Top, n);
                Array.Resize(ref Radius, n);
            }

            X[Count] = x;
            Z[Count] = z;
            Bottom[Count] = Math.Min(bottom, top);
            Top[Count] = Math.Max(bottom, top);
            Radius[Count] = radius;
            Count++;
        }

        /// <summary>
        /// An object as the game picks it (PropInstance.RayCast; TreeInstance.RayCast the same):
        /// standing at (x, y, z), <paramref name="height"/> tall, its radius (width + depth) / 8 of
        /// its size as scaled, the segment inset by the radius at both ends (by 0.45 of its height at
        /// most, for a flat one) -- so the capsule spans its whole height.
        /// </summary>
        public void AddObject(float x, float y, float z, float width, float depth, float height)
        {
            float radius = (width + depth) * 0.125f;
            float inset = Math.Min(radius, 0.45f * height);
            Add(x, z, y + inset, y + height - inset, radius);
        }
    }
}
