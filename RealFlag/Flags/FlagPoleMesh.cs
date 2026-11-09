using System.Collections.Generic;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The mesh of the pole the mod draws under a flag (FlagPoleShape's numbers), in the pole's
    /// own frame: y up its axis from the foot, x towards the flag's hoist (where the halyard
    /// runs), z = x cross y. FlagRenderer turns it into place: the foot on whatever the pole
    /// stands on, the axis along the hoist. Engine code, shared with the GPU preview.
    /// </summary>
    /// <remarks>
    /// A lathe of <see cref="Sides"/> sides (the author: "at least 32-64 faces and be smooth"),
    /// its normals from the profile, so it shades round: the flash collar at the foot (or an
    /// ornate pole's cast base), the shaft -- tapering, straight, in sections with a collar at
    /// each joint, or fluted -- the truck (the cap whose sheave the halyard runs over), and a ball
    /// on a neck, a flat cap, or a spear. The halyard: the line the flag is clipped to, from the
    /// sheave down to the flag's top grommet and from its bottom grommet down to the cleat, and
    /// with two ropes the loop's return line beside it.
    ///
    /// Each vertex's first uv (four floats) holds its part (0 the pole, 1 the top, 2 a rope:
    /// FlagPole.shader colours them), for the tubes -- the shaft and the ropes -- their radius
    /// (those vertices sit ON the axis with the radial direction as their normal, and the shader
    /// puts them out by the radius, or by half a pixel's worth when that is more, so a rope a
    /// centimetre thick stays a steady thin line far away instead of flickering in and out), and a
    /// fluted shaft's flutes and their depth. The second uv is where its textures lie: how far
    /// along (in texture tiles), the direction round the axis as a cosine and sine -- the shader
    /// takes its angle per pixel, so a texture wraps the pole without a seam (Tarini 2012) -- and
    /// how many tiles go round. The tangents run round (the pole) or along (a rope's twist).
    /// </remarks>
    public static class FlagPoleMesh
    {
        /// <summary>Sides of the pole's lathe.</summary>
        public const int Sides = 64;

        /// <summary>Sides of a fluted shaft: eight a flute.</summary>
        public const int FlutedSides = 128;

        /// <summary>Sides of a rope.</summary>
        public const int RopeSides = 6;

        public const float PartPole = 0f;
        public const float PartTop = 1f;
        public const float PartRope = 2f;

        /// <summary>About how wide a texture tile is round a pole, metres: a thick pole has more than one round it.</summary>
        public const float TileAround = 0.6f;

        private sealed class Builder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector4> Tangents = new List<Vector4>();
            public readonly List<Vector4> Uv = new List<Vector4>();
            public readonly List<Vector4> Uv2 = new List<Vector4>();
            public readonly List<int> Triangles = new List<int>();

            /// <summary>Texture tiles round the pole, and per metre up it.</summary>
            public float Around = 1f, PerMetre = 1f;

            private void Add(Vector3 v, Vector3 n, float part, float radius, float flutes, float depth, float y, float c, float s)
            {
                Vertices.Add(v);
                Normals.Add(n);
                Uv.Add(new Vector4(part, radius, flutes, depth));
                Uv2.Add(new Vector4(y * PerMetre, c, s, Around));
                Tangents.Add(new Vector4(-s, 0f, c, -1f));
            }

            /// <summary>A ring of the lathe at <paramref name="y"/>: radius, normal (radial and up parts), part; returns its first vertex.</summary>
            public int Ring(float y, float radius, float nr, float ny, float part)
            {
                int first = Vertices.Count;
                for (int i = 0; i < Sides; i++)
                {
                    float a = i * 2f * Mathf.PI / Sides;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    Add(new Vector3(radius * c, y, radius * s), new Vector3(nr * c, ny, nr * s).normalized, part, 0f, 0f, 0f, y, c, s);
                }

                return first;
            }

            /// <summary>
            /// A ring of the upright shaft at <paramref name="y"/>, on the axis, put out by the shader:
            /// <paramref name="radius"/> (less a fluted one's grooves, <paramref name="flutes"/> of them
            /// <paramref name="depth"/> deep), facing out.
            /// </summary>
            public int ShaftRing(float y, int sides, float radius, int flutes, float depth)
            {
                int first = Vertices.Count;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * 2f * Mathf.PI / sides;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    float r = flutes > 0 ? radius * (1f - depth * (0.5f - 0.5f * Mathf.Cos(flutes * a))) : radius;
                    Add(new Vector3(0f, y, 0f), new Vector3(c, 0f, s), PartPole, r, flutes, depth, y, c, s);
                }

                return first;
            }

            /// <summary>A ring of a rope round the axis point <paramref name="centre"/>, <paramref name="along"/> metres from its start: on the axis, its normals the radial directions square to <paramref name="direction"/>, its radius for the shader.</summary>
            public int RopeRing(Vector3 centre, Vector3 direction, float along, float radius)
            {
                Vector3 a = direction.normalized;
                Vector3 side = Mathf.Abs(a.y) < 0.9f ? Vector3.Cross(a, Vector3.up).normalized : Vector3.Cross(a, Vector3.right).normalized;
                Vector3 other = Vector3.Cross(a, side);
                float perMetre = 1f / (2f * Mathf.PI * Mathf.Max(radius, 1e-4f));
                int first = Vertices.Count;
                for (int i = 0; i < RopeSides; i++)
                {
                    float t = i * 2f * Mathf.PI / RopeSides;
                    float c = Mathf.Cos(t), s = Mathf.Sin(t);
                    Vertices.Add(centre);
                    Normals.Add(side * c + other * s);
                    Uv.Add(new Vector4(PartRope, radius, 0f, 0f));
                    Uv2.Add(new Vector4(along * perMetre, c, s, 1f));

                    // Round the rope, its binormal (normal x tangent) along it: the twist's frame.
                    Vector3 round = -side * s + other * c;
                    Tangents.Add(new Vector4(round.x, round.y, round.z, 1f));
                }

                return first;
            }

            /// <summary>
            /// Two tube rings joined facing out, whichever way round their rings run: the first
            /// quad's front (Unity's: the cross of its first two edges) is compared with the
            /// outward normal, the vertices taken out along their normals as the shader will.
            /// </summary>
            public void Tube(int a, int b, int sides)
            {
                Vector3 a0 = Vertices[a] + Normals[a], b0 = Vertices[b] + Normals[b], a1 = Vertices[a + 1] + Normals[a + 1];
                if (Vector3.Dot(Vector3.Cross(b0 - a0, a1 - a0), Normals[a]) >= 0f)
                    Band(a, b, sides);
                else
                    Band(b, a, sides);
            }

            /// <summary>
            /// Quads between two rings of the same size (the lathe's run counter-clockwise seen
            /// from +y): with the lower ring first they face out, Unity's front being clockwise.
            /// In general: the two in the order the profile runs (up, or in across a top, or out
            /// across an underside) face the way its normal (dy, -dr) points.
            /// </summary>
            public void Band(int below, int above, int sides)
            {
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    Triangles.Add(below + i);
                    Triangles.Add(above + i);
                    Triangles.Add(below + j);
                    Triangles.Add(below + j);
                    Triangles.Add(above + i);
                    Triangles.Add(above + j);
                }
            }

            /// <summary>A flat disc over a ring, facing up (or down), round a centre vertex.</summary>
            public void Cap(int ring, float y, bool up, float part)
            {
                int centre = Vertices.Count;
                Add(new Vector3(0f, y, 0f), up ? Vector3.up : Vector3.down, part, 0f, 0f, 0f, y, 1f, 0f);
                for (int i = 0; i < Sides; i++)
                {
                    int j = (i + 1) % Sides;
                    Triangles.Add(centre);
                    Triangles.Add(up ? ring + j : ring + i);
                    Triangles.Add(up ? ring + i : ring + j);
                }
            }

            /// <summary>
            /// A smooth run of the lathe's profile through (r, y) points, in the order it runs: a
            /// ring at each, its normal (dy, -dr) from the points either side. Corners between runs
            /// are sharp (each run its own rings).
            /// </summary>
            public void Profile(List<Vector2> points, float part)
            {
                int previous = -1;
                for (int k = 0; k < points.Count; k++)
                {
                    Vector2 before = points[Mathf.Max(0, k - 1)], after = points[Mathf.Min(points.Count - 1, k + 1)];
                    Vector2 d = after - before;
                    int ring = Ring(points[k].y, Mathf.Max(points[k].x, 1e-4f), d.y, -d.x, part);
                    if (previous >= 0)
                        Band(previous, ring, Sides);
                    previous = ring;
                }
            }

            public Mesh ToMesh(Mesh mesh, Bounds bounds)
            {
                if (mesh == null)
                    mesh = new Mesh { name = "RealFlag pole" };
                else
                    mesh.Clear();

                mesh.SetVertices(Vertices);
                mesh.SetNormals(Normals);
                mesh.SetTangents(Tangents);
                mesh.SetUVs(0, Uv);
                mesh.SetUVs(1, Uv2);
                mesh.SetTriangles(Triangles, 0);
                mesh.bounds = bounds;
                return mesh;
            }
        }

        /// <summary>
        /// Where the pole's mesh goes for a flag whose anchor is at <paramref name="anchor"/>, its
        /// hoist running along <paramref name="hoist"/> and its fly at rest along
        /// <paramref name="fly"/> (FlagOrientation): the foot <see cref="FlagPoleShape.Length"/>
        /// down the axis, which is <see cref="FlagPoleShape.Offset"/> behind the hoist; the mesh's
        /// y up the pole, its x towards the flag.
        /// </summary>
        public static Matrix4x4 Placement(Vector3 anchor, FlagPoleShape s, Vector3 hoist, Vector3 fly)
        {
            Vector3 up = -hoist;
            Vector3 side = Vector3.Cross(fly, up);
            Vector3 foot = anchor - fly * s.Offset + hoist * s.Length;
            var m = new Matrix4x4();
            m.SetColumn(0, new Vector4(fly.x, fly.y, fly.z, 0f));
            m.SetColumn(1, new Vector4(up.x, up.y, up.z, 0f));
            m.SetColumn(2, new Vector4(side.x, side.y, side.z, 0f));
            m.SetColumn(3, new Vector4(foot.x, foot.y, foot.z, 1f));
            return m;
        }

        /// <summary>
        /// The pole's look into its property block: its colours (LINEAR), material, wear, rust and
        /// dirt and which scatter of them (<paramref name="seed"/>, FlagWear.SeedFor its prop), the
        /// material's textures (PoleTextures), and the highlight.
        /// </summary>
        public static void Look(MaterialPropertyBlock block, FlagSpec spec, Vector4 highlight, float seed)
        {
            block.SetVector(IdPoleColour, FlagLook.Linear(spec.PoleColour));
            block.SetVector(IdTopColour, FlagLook.Linear(spec.TopColour));
            block.SetVector(IdRopeColour, FlagLook.Linear(spec.RopeColour));
            block.SetVector(FlagLook.IdHighlight, highlight);
            block.SetVector(IdPoleWear, new Vector4(spec.PoleWear, FlagPoleMaterial.Rusts(spec.PoleMaterial) ? spec.PoleRust : 0f, spec.PoleDirt, spec.PoleMaterial));
            block.SetFloat(IdPoleSeed, seed);
            PoleTextures.Bind(block);
        }

        public static readonly int IdPoleColour = Shader.PropertyToID("_RFPoleColor");
        public static readonly int IdTopColour = Shader.PropertyToID("_RFTopColor");
        public static readonly int IdRopeColour = Shader.PropertyToID("_RFRopeColor");

        /// <summary>x worn, y rust (0 unless the material rusts), z dirt, w the FlagPoleMaterial.</summary>
        public static readonly int IdPoleWear = Shader.PropertyToID("_RFPoleWear");

        /// <summary>The scatter of its chips, rust and grime: 0..255.</summary>
        public static readonly int IdPoleSeed = Shader.PropertyToID("_RFPoleSeed");

        /// <summary>Half a pixel's angle, radians, for the camera: tubes are drawn no thinner than about a pixel (FlagPole.shader). Set globally, every frame.</summary>
        public static readonly int IdPixelRadius = Shader.PropertyToID("_RFPixelRadius");

        /// <summary>The pole's mesh, into <paramref name="reuse"/> if given (its old contents cleared).</summary>
        public static Mesh Build(FlagPoleShape s, Mesh reuse)
        {
            var b = new Builder();

            // The textures' tiles: whole ones round it (no seam), as square along.
            float round = 2f * Mathf.PI * Mathf.Max(s.RadiusAt(s.Length), 0.005f);
            b.Around = Mathf.Max(1f, Mathf.Round(round / TileAround));
            b.PerMetre = b.Around / round;

            if (s.Profile == FlagPoleProfile.Ornate)
                OrnateBase(b, s);
            else
                FlashCollar(b, s);

            Shaft(b, s);

            // The truck: a short drum on the shaft's top, the halyard's sheave in it; its
            // underside a flat ring (inner first: facing down).
            float y0 = s.ShaftTop, y1 = s.ShaftTop + s.TruckHeight;
            float bevel = 0.12f * s.TruckRadius;
            int t0 = b.Ring(y0, s.RadiusTop * 0.95f, 0f, -1f, PartPole);
            int t1 = b.Ring(y0, s.TruckRadius, 0f, -1f, PartPole);
            b.Band(t0, t1, Sides);
            int t2 = b.Ring(y0, s.TruckRadius, 1f, 0f, PartPole);
            int t3 = b.Ring(y1 - bevel, s.TruckRadius, 1f, 0f, PartPole);
            b.Band(t2, t3, Sides);
            int t4 = b.Ring(y1, s.TruckRadius - bevel, 1f, 1f, PartPole);
            b.Band(t3, t4, Sides);

            float tip;
            if (s.Style == FlagPoleStyle.Ball)
                tip = Ball(b, s, y1);
            else if (s.Style == FlagPoleStyle.Spear)
                tip = Spear(b, s, y1);
            else
                tip = Dome(b, s, y1);

            Ropes(b, s);

            float wide = Mathf.Max(Mathf.Max(s.CollarRadius, s.BaseRadius), s.Offset + s.RopeRadius + s.RopeGap) + 1f;
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(-wide, -0.1f, -wide), new Vector3(wide, tip + 0.1f, wide));
            return b.ToMesh(reuse, bounds);
        }

        /// <summary>The flash collar: a low cone round the foot, hugging the shaft at its top.</summary>
        private static void FlashCollar(Builder b, FlagPoleShape s)
        {
            float collarTop = s.RadiusAt(s.CollarHeight) * 1.05f;
            float slope = s.CollarRadius - collarTop;
            int c0 = b.Ring(0f, s.CollarRadius, s.CollarHeight, slope, PartPole);
            int c1 = b.Ring(s.CollarHeight, collarTop, s.CollarHeight, slope, PartPole);
            b.Band(c0, c1, Sides);
            // Its top, a narrow flat ring (outer first: facing up).
            int c2 = b.Ring(s.CollarHeight, collarTop, 0f, 1f, PartPole);
            int c3 = b.Ring(s.CollarHeight, collarTop * 0.9f, 0f, 1f, PartPole);
            b.Band(c2, c3, Sides);
        }

        /// <summary>
        /// An ornate pole's cast base, as the lathe turns one: a plinth, a torus on it, a bell
        /// narrowing in an ogee, a ring, and a neck to the shaft. Runs of the profile in units of
        /// its widest radius and its height; each run's corners sharp.
        /// </summary>
        private static void OrnateBase(Builder b, FlagPoleShape s)
        {
            float R = s.BaseRadius, H = s.BaseHeight;
            float neck = s.RadiusAt(H) / R;

            Run(b, R, H, 1.00f, 0.00f, 1.00f, 0.10f);              // the plinth's side
            Run(b, R, H, 1.00f, 0.10f, 0.86f, 0.10f);              // its top
            Arc(b, R, H, 0.86f, 0.17f, 0.07f, 0.07f);               // the torus
            Run(b, R, H, 0.86f, 0.24f, 0.70f, 0.24f);              // a step in

            var bell = new List<Vector2>();
            const int bellSteps = 10;
            for (int i = 0; i <= bellSteps; i++)
            {
                float t = i / (float)bellSteps;
                float r = 0.38f + 0.32f * (0.5f + 0.5f * Mathf.Cos(Mathf.PI * t));
                bell.Add(new Vector2(r * R, (0.24f + 0.48f * t) * H));
            }

            b.Profile(bell, PartPole);                              // the bell, an ogee
            Run(b, R, H, 0.38f, 0.72f, 0.46f, 0.72f);              // the ring's underside
            Arc(b, R, H, 0.46f, 0.76f, 0.04f, 0.04f);               // the ring
            Run(b, R, H, 0.46f, 0.80f, 0.40f, 0.80f);              // its top
            Run(b, R, H, 0.40f, 0.80f, neck, 1.00f);               // the neck to the shaft
        }

        /// <summary>A straight run of the base's profile, (r0, y0) to (r1, y1) in its units.</summary>
        private static void Run(Builder b, float R, float H, float r0, float y0, float r1, float y1)
        {
            b.Profile(new List<Vector2> { new Vector2(r0 * R, y0 * H), new Vector2(r1 * R, y1 * H) }, PartPole);
        }

        /// <summary>A half-round bulging out from the profile, from below its centre (r, y) to above it.</summary>
        private static void Arc(Builder b, float R, float H, float r, float y, float rr, float ry)
        {
            var points = new List<Vector2>();
            const int steps = 8;
            for (int i = 0; i <= steps; i++)
            {
                float a = -0.5f * Mathf.PI + i * Mathf.PI / steps;
                points.Add(new Vector2((r + rr * Mathf.Cos(a)) * R, (y + ry * Mathf.Sin(a)) * H));
            }

            b.Profile(points, PartPole);
        }

        /// <summary>The shaft: one tube, a fluted one, or a sectional pole's sections with a collar at each joint; drawn at least a pixel wide far off.</summary>
        private static void Shaft(Builder b, FlagPoleShape s)
        {
            if (s.Profile == FlagPoleProfile.Sectional && s.Sections > 1)
            {
                for (int k = 0; k < s.Sections; k++)
                {
                    float bottom = s.SectionBottom(k), top = k == s.Sections - 1 ? s.ShaftTop : s.SectionBottom(k + 1);
                    float r = s.SectionRadius(k);
                    int r0 = b.ShaftRing(bottom, Sides, r, 0, 0f);
                    int r1 = b.ShaftRing(top, Sides, r, 0, 0f);
                    b.Tube(r0, r1, Sides);
                    if (k > 0)
                        Joint(b, bottom, s.SectionRadius(k - 1), r);
                }

                return;
            }

            bool fluted = s.Profile == FlagPoleProfile.Fluted;
            int sides = fluted ? FlutedSides : Sides;
            int flutes = fluted ? FlagPoleShape.Flutes : 0;
            float depth = fluted ? FlagPoleShape.FluteDepth : 0f;
            int s0 = b.ShaftRing(0f, sides, s.RadiusFoot, flutes, depth);
            int s1 = b.ShaftRing(s.ShaftTop, sides, s.RadiusTop, flutes, depth);
            b.Tube(s0, s1, sides);
        }

        /// <summary>A sectional pole's joint at <paramref name="y"/>: a collar round the top of the section below, its top stepping in to the one above.</summary>
        private static void Joint(Builder b, float y, float below, float above)
        {
            float rc = below * 1.08f;
            float height = Mathf.Max(0.03f, 1.4f * below);
            float y0 = y - height;
            int u0 = b.Ring(y0, below, 0f, -1f, PartPole);
            int u1 = b.Ring(y0, rc, 0f, -1f, PartPole);
            b.Band(u0, u1, Sides);
            int side0 = b.Ring(y0, rc, 1f, 0f, PartPole);
            int side1 = b.Ring(y, rc, 1f, 0f, PartPole);
            b.Band(side0, side1, Sides);
            int top0 = b.Ring(y, rc, 0f, 1f, PartPole);
            int top1 = b.Ring(y, above * 0.98f, 0f, 1f, PartPole);
            b.Band(top0, top1, Sides);
        }

        /// <summary>The flat cap: a low dome on the truck. Returns the top.</summary>
        private static float Dome(Builder b, FlagPoleShape s, float y1)
        {
            float r = s.TruckRadius - 0.12f * s.TruckRadius;
            float rise = 0.25f * s.TruckRadius;
            int previous = b.Ring(y1, r, 0.6f, 1f, PartPole);
            const int steps = 4;
            for (int i = 1; i <= steps; i++)
            {
                float a = i / (float)steps * 0.5f * Mathf.PI;
                float rr = r * Mathf.Cos(a), yy = y1 + rise * Mathf.Sin(a);
                int ring = b.Ring(yy, Mathf.Max(rr, 1e-4f), Mathf.Cos(a) * rise, Mathf.Sin(a) * r, PartPole);
                b.Band(previous, ring, Sides);
                previous = ring;
            }

            b.Cap(previous, y1 + rise, true, PartPole);
            return y1 + rise;
        }

        /// <summary>A gilt ball on a short neck. Returns the top.</summary>
        private static float Ball(Builder b, FlagPoleShape s, float y1)
        {
            // The truck's top, flat.
            int top = b.Ring(y1, s.TruckRadius - 0.12f * s.TruckRadius, 0f, 1f, PartPole);
            b.Cap(top, y1, true, PartPole);

            int n0 = b.Ring(y1, s.NeckRadius, 1f, 0f, PartTop);
            int n1 = b.Ring(y1 + s.NeckHeight + 0.2f * s.BallRadius, s.NeckRadius, 1f, 0f, PartTop);
            b.Band(n0, n1, Sides);

            float centre = y1 + s.NeckHeight + 0.95f * s.BallRadius;
            const int latitudes = 20;
            int previous = -1;
            for (int i = 0; i <= latitudes; i++)
            {
                float a = -0.5f * Mathf.PI + i * Mathf.PI / latitudes;
                float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                int ring = b.Ring(centre + s.BallRadius * sn, Mathf.Max(s.BallRadius * c, 1e-4f), c, sn, PartTop);
                if (previous >= 0)
                    b.Band(previous, ring, Sides);
                previous = ring;
            }

            return centre + s.BallRadius;
        }

        /// <summary>A spear point on a small collar. Returns the top.</summary>
        private static float Spear(Builder b, FlagPoleShape s, float y1)
        {
            int top = b.Ring(y1, s.TruckRadius - 0.12f * s.TruckRadius, 0f, 1f, PartPole);
            b.Cap(top, y1, true, PartPole);

            float collar = 0.15f * s.SpearHeight;
            int k0 = b.Ring(y1, s.SpearRadius * 1.1f, 1f, 0f, PartTop);
            int k1 = b.Ring(y1 + collar, s.SpearRadius * 1.1f, 1f, 0f, PartTop);
            b.Band(k0, k1, Sides);
            int k2 = b.Ring(y1 + collar, s.SpearRadius * 1.1f, 0f, 1f, PartTop);
            int k3 = b.Ring(y1 + collar, s.SpearRadius, 0f, 1f, PartTop);
            b.Band(k2, k3, Sides);

            float height = s.SpearHeight - collar;
            int p0 = b.Ring(y1 + collar, s.SpearRadius, height, s.SpearRadius, PartTop);
            int p1 = b.Ring(y1 + s.SpearHeight, 1e-4f, height, s.SpearRadius, PartTop);
            b.Band(p0, p1, Sides);
            return y1 + s.SpearHeight;
        }

        /// <summary>The halyard: the flag's line in two runs (above its top clip, below its bottom one), and the return line.</summary>
        private static void Ropes(Builder b, FlagPoleShape s)
        {
            if (s.Ropes <= 0)
                return;

            float sheave = s.ShaftTop + 0.5f * s.TruckHeight;
            float grommetTop = s.Length, grommetBottom = s.Length - s.BottomClip;
            float cleatOut = s.RadiusAt(s.Cleat) + s.RopeRadius;
            var cleat = new Vector3(cleatOut, s.Cleat, 0f);

            Rope(b, s, new Vector3(s.Offset, sheave, 0f), new Vector3(s.Offset, grommetTop, 0f));
            if (grommetBottom > s.Cleat + 0.05f)
                Rope(b, s, new Vector3(s.Offset, grommetBottom, 0f), cleat);

            if (s.Ropes >= 2)
                Rope(b, s, new Vector3(s.Offset, sheave, -s.RopeGap), new Vector3(cleatOut, s.Cleat, -0.4f * s.RopeGap));
        }

        private static void Rope(Builder b, FlagPoleShape s, Vector3 from, Vector3 to)
        {
            Vector3 along = to - from;
            if (along.sqrMagnitude < 1e-6f)
                return;

            int r0 = b.RopeRing(from, along, 0f, s.RopeRadius);
            int r1 = b.RopeRing(to, along, along.magnitude, s.RopeRadius);
            b.Tube(r0, r1, RopeSides);
        }
    }
}
