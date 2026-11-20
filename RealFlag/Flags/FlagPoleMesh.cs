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
    /// with two ropes the loop's return line beside it. A banner's arms (2026-11-19): a tube out
    /// along the fly from the axis at each held edge, painted as the pole (FlagPoleShape.ArmTop).
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

        /// <summary>Sides of a banner's arm: round enough near, a few centimetres thick.</summary>
        public const int ArmSides = 24;

        public const float PartPole = 0f;
        public const float PartTop = 1f;
        public const float PartRope = 2f;

        /// <summary>The concrete block's part (its own mesh, <see cref="BuildBlock"/>): out of the paint, the coat and the tubes.</summary>
        public const float PartBlock = 3f;

        /// <summary>How many metres of concrete a tile of its texture covers (the block's uv are metres over this).</summary>
        public const float ConcreteTile = 1f;

        /// <summary>A Rounded block's quarter corners in this many segments; a Round one's sides; its top edge's rounding in so many steps.</summary>
        public const int BlockCornerSteps = 4;
        public const int BlockRoundSides = 24;
        public const int BlockEdgeSteps = 3;

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

            /// <summary>
            /// The plain copy (BuildPlain): the shaft's rings at their radius, not on the axis -- no
            /// shader of ours puts them out there (Real Glass's finish draws it).
            /// </summary>
            public bool Plain;

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
                    if (Plain)
                        Add(new Vector3(r * c, y, r * s), new Vector3(c, 0f, s), PartPole, 0f, 0f, 0f, y, c, s);
                    else
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
            /// A ring of a banner's arm (FlagPoleShape.ArmTop) round <paramref name="centre"/>, the
            /// arm running along the pole frame's x (the fly at rest), <paramref name="along"/>
            /// metres out from the axis: painted as the pole, on the axis and put out by the shader
            /// as the shaft is (no thinner than about a pixel far off) -- in the plain copy at its
            /// radius. Round it the second uv's direction and the tangent, along it the texture.
            /// </summary>
            public int ArmRing(Vector3 centre, float along, float radius)
            {
                Vector3 side = new Vector3(0f, 0f, 1f), other = new Vector3(0f, -1f, 0f);
                int first = Vertices.Count;
                for (int i = 0; i < ArmSides; i++)
                {
                    float t = i * 2f * Mathf.PI / ArmSides;
                    float c = Mathf.Cos(t), s = Mathf.Sin(t);
                    Vector3 n = side * c + other * s;
                    Vertices.Add(Plain ? centre + n * radius : centre);
                    Normals.Add(n);
                    Uv.Add(new Vector4(PartPole, Plain ? 0f : radius, 0f, 0f));
                    Uv2.Add(new Vector4(along * PerMetre, c, s, Around));

                    // Round the arm, its binormal (normal x tangent) along it, as a rope's.
                    Vector3 round = -side * s + other * c;
                    Tangents.Add(new Vector4(round.x, round.y, round.z, 1f));
                }

                return first;
            }

            /// <summary>
            /// The arm's end: a flat disc at its true radius facing out along it (not put out by
            /// the shader: far off, where the tube is drawn wider, the end is under a pixel anyway).
            /// </summary>
            public void ArmCap(Vector3 centre, float along, float radius)
            {
                Vector3 side = new Vector3(0f, 0f, 1f), other = new Vector3(0f, -1f, 0f), outward = new Vector3(1f, 0f, 0f);
                Vector4 tangent = new Vector4(0f, 0f, 1f, 1f);
                int c0 = Vertices.Count;
                Vertices.Add(centre);
                Normals.Add(outward);
                Uv.Add(new Vector4(PartPole, 0f, 0f, 0f));
                Uv2.Add(new Vector4(along * PerMetre, 1f, 0f, Around));
                Tangents.Add(tangent);
                for (int i = 0; i < ArmSides; i++)
                {
                    float t = i * 2f * Mathf.PI / ArmSides;
                    float c = Mathf.Cos(t), s = Mathf.Sin(t);
                    Vertices.Add(centre + (side * c + other * s) * radius);
                    Normals.Add(outward);
                    Uv.Add(new Vector4(PartPole, 0f, 0f, 0f));
                    Uv2.Add(new Vector4(along * PerMetre, c, s, Around));
                    Tangents.Add(tangent);
                }

                // Facing out: a triangle (p0, p1, p2) faces the way the cross of its first two edges points (Tube's rule).
                bool forward = Vector3.Dot(Vector3.Cross(Vertices[c0 + 1] - centre, Vertices[c0 + 2] - centre), outward) >= 0f;
                for (int i = 0; i < ArmSides; i++)
                {
                    int a = c0 + 1 + i, b = c0 + 1 + (i + 1) % ArmSides;
                    Triangles.Add(c0);
                    Triangles.Add(forward ? a : b);
                    Triangles.Add(forward ? b : a);
                }
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
        /// The pole's look into its property block: its colours (LINEAR), shine, wear, rust and
        /// dirt and which scatter of them (<paramref name="seed"/>, FlagWear.SeedFor its prop), the
        /// material's textures (PoleTextures), and the highlight.
        /// </summary>
        public static void Look(MaterialPropertyBlock block, FlagSpec spec, Vector4 highlight, float seed)
        {
            block.SetVector(IdPoleColour, FlagLook.Linear(spec.PoleColour));
            block.SetVector(IdTopColour, FlagLook.Linear(spec.TopColour));
            block.SetVector(IdRopeColour, FlagLook.Linear(spec.RopeColour));
            block.SetVector(FlagLook.IdHighlight, highlight);
            block.SetVector(IdPoleWear, new Vector4(spec.PoleWear, spec.PoleRust, spec.PoleDirt, spec.PoleShine));
            block.SetFloat(IdPoleSeed, seed);
            block.SetFloat(IdGalvanized, spec.PoleFinish == FlagPoleFinish.Galvanized ? 1f : 0f);

            // The block's (drawn with this same block: BuildBlock's mesh, part 3): its half width,
            // height, shape and wear for its weathering, its colour (LINEAR) tinting the concrete.
            block.SetVector(IdBlock, new Vector4(0.5f * spec.BlockWidth, spec.BlockHeight, spec.BlockShape, spec.BlockWear));
            block.SetVector(IdBlockColour, FlagLook.Linear(spec.BlockColour));
            PoleTextures.Bind(block);
        }

        /// <summary>
        /// Galvanized zinc's colour, sRGB: a light bluish grey, chosen by eye (FlagPole.shader's
        /// zinc, in linear), for Real Glass's finish over a galvanized pole.
        /// </summary>
        public static readonly Color ZincColour = new Color(0.86f, 0.88f, 0.90f);

        public static readonly int IdPoleColour = Shader.PropertyToID("_RFPoleColor");

        /// <summary>1 for a galvanized pole (FlagPoleFinish), 0 painted.</summary>
        public static readonly int IdGalvanized = Shader.PropertyToID("_RFGalvanized");

        /// <summary>The block: x its half width, y its height (m), z its FlagBlockShape, w its wear 0..1.</summary>
        public static readonly int IdBlock = Shader.PropertyToID("_RFBlock");
        public static readonly int IdBlockColour = Shader.PropertyToID("_RFBlockColor");
        public static readonly int IdTopColour = Shader.PropertyToID("_RFTopColor");
        public static readonly int IdRopeColour = Shader.PropertyToID("_RFRopeColor");

        /// <summary>x worn, y rust, z dirt, w the shine (FlagSpec.PoleShine), each 0..1.</summary>
        public static readonly int IdPoleWear = Shader.PropertyToID("_RFPoleWear");

        /// <summary>1 while Real Glass's Reflective finish draws the paint's reflection this frame (RealGlassFinish): the shader leaves its own out.</summary>
        public static readonly int IdPoleFinish = Shader.PropertyToID("_RFPoleFinish");

        /// <summary>The scatter of its chips, rust and grime: 0..255.</summary>
        public static readonly int IdPoleSeed = Shader.PropertyToID("_RFPoleSeed");

        /// <summary>Half a pixel's angle, radians, for the camera: tubes are drawn no thinner than about a pixel (FlagPole.shader). Set globally, every frame.</summary>
        public static readonly int IdPixelRadius = Shader.PropertyToID("_RFPixelRadius");

        /// <summary>A camera's pixels per radian of view, from its height in pixels and its vertical field of view (degrees).</summary>
        public static float PixelsPerRadian(float pixelHeight, float fieldOfView)
        {
            return pixelHeight / (2f * Mathf.Tan(0.5f * Mathf.Clamp(fieldOfView, 1f, 170f) * Mathf.Deg2Rad));
        }

        /// <summary>What <see cref="IdPixelRadius"/> holds for a picture of this many pixels per radian.</summary>
        public static float PixelRadius(float pixelsPerRadian)
        {
            return 0.6f / Mathf.Max(pixelsPerRadian, 1f);
        }

        /// <summary>The pole's mesh, into <paramref name="reuse"/> if given (its old contents cleared).</summary>
        public static Mesh Build(FlagPoleShape s, Mesh reuse)
        {
            return Build(s, reuse, false);
        }

        /// <summary>
        /// The painted pole alone as plain geometry -- the shaft at its radius, no top, no ropes --
        /// for Real Glass's Reflective finish (RealGlassFinish), which draws a mesh as it is: a pole
        /// far off, drawn wider by our shader, keeps a thinner finish down its middle, hidden behind
        /// our own draw where that lies in front.
        /// </summary>
        public static Mesh BuildPlain(FlagPoleShape s, Mesh reuse)
        {
            return Build(s, reuse, true);
        }

        private static Mesh Build(FlagPoleShape s, Mesh reuse, bool plain)
        {
            var b = new Builder { Plain = plain };

            // The textures' tiles: whole ones round it (no seam), as square along.
            float round = 2f * Mathf.PI * Mathf.Max(s.RadiusAt(s.Length), 0.005f);
            b.Around = Mathf.Max(1f, Mathf.Round(round / TileAround));
            b.PerMetre = b.Around / round;

            // Any shaft on any base (the author, 2026-11-09: "an ornate base but with a sectional pole body").
            if (s.Base == FlagPoleBase.Cast)
                CastBase(b, s);
            else if (s.Base == FlagPoleBase.Collar)
                FlashCollar(b, s);

            Shaft(b, s);
            Arms(b, s);

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
            else if (s.Style == FlagPoleStyle.Cap)
                tip = Cap(b, s, y1);
            else
                tip = Dome(b, s, y1);

            if (plain)
            {
                // The painted parts only: the gilt top keeps its own look.
                var kept = new List<int>(b.Triangles.Count);
                for (int t = 0; t + 2 < b.Triangles.Count; t += 3)
                {
                    int i0 = b.Triangles[t], i1 = b.Triangles[t + 1], i2 = b.Triangles[t + 2];
                    if (b.Uv[i0].x < 0.5f && b.Uv[i1].x < 0.5f && b.Uv[i2].x < 0.5f)
                    {
                        kept.Add(i0);
                        kept.Add(i1);
                        kept.Add(i2);
                    }
                }

                b.Triangles.Clear();
                b.Triangles.AddRange(kept);
            }
            else
            {
                Ropes(b, s);
            }

            // Out to the arms' ends too (Unity culls the draw and its shadow by these bounds).
            float wide = Mathf.Max(Mathf.Max(s.CollarRadius, s.BaseRadius), s.Offset + s.RopeRadius + s.RopeGap) + 1f;
            wide = Mathf.Max(wide, s.Offset + Mathf.Max(s.ArmTop, s.ArmBottom) + s.ArmRadius + 0.1f);
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(-wide, -0.1f, -wide), new Vector3(wide, tip + 0.1f, wide));
            return b.ToMesh(reuse, bounds);
        }

        /// <summary>
        /// The flash collar, as one is spun from sheet: a thin upright lip on the ground, a skirt
        /// curving in (concave) to the shaft, and a narrow bead where it hugs it.
        /// </summary>
        private static void FlashCollar(Builder b, FlagPoleShape s)
        {
            float R = s.CollarRadius, H = s.CollarHeight;
            float shaft = s.RadiusAt(H) / R;
            float hug = Mathf.Min(shaft * 1.12f, 0.9f);

            Run(b, R, H, 1.00f, 0.00f, 1.00f, 0.10f);              // the lip
            var skirt = new List<Vector2>();
            const int steps = 10;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float r = hug + (1f - hug) * (1f - t) * (1f - t);   // a flare, concave seen from outside
                skirt.Add(new Vector2(r * R, (0.10f + 0.75f * t) * H));
            }

            b.Profile(skirt, PartPole);
            Run(b, R, H, hug, 0.85f, hug, 0.92f);                   // the band round the shaft
            Arc(b, R, H, hug, 0.95f, 0.06f * shaft, 0.03f);          // its bead
            Run(b, R, H, hug, 0.98f, shaft, 1.00f);                  // in to the shaft
        }

        /// <summary>
        /// A decorative cast base, as the lathe turns one after the classical column base (an Attic
        /// base under a baluster): a round plinth, a torus, a scotia (a cove), a smaller torus, a
        /// vase narrowing in an ogee, an astragal (a ring) and a neck into the shaft. Runs of the
        /// profile in units of its widest radius and its height; each run's corners sharp.
        /// </summary>
        private static void CastBase(Builder b, FlagPoleShape s)
        {
            float R = s.BaseRadius, H = s.BaseHeight;
            float neck = s.RadiusAt(H) / R;

            Run(b, R, H, 1.00f, 0.00f, 1.00f, 0.07f);              // the plinth's side
            Run(b, R, H, 1.00f, 0.07f, 0.90f, 0.07f);              // its top
            Arc(b, R, H, 0.90f, 0.11f, 0.08f, 0.04f);               // the lower torus
            Run(b, R, H, 0.90f, 0.15f, 0.84f, 0.15f);              // a fillet

            var scotia = new List<Vector2>();
            const int coveSteps = 8;
            for (int i = 0; i <= coveSteps; i++)
            {
                float t = i / (float)coveSteps;
                scotia.Add(new Vector2((0.84f - 0.08f * Mathf.Sin(Mathf.PI * t) - 0.04f * t) * R, (0.15f + 0.08f * t) * H));
            }

            b.Profile(scotia, PartPole);                            // the scotia, a cove
            Arc(b, R, H, 0.80f, 0.26f, 0.06f, 0.03f);               // the upper torus
            Run(b, R, H, 0.80f, 0.29f, 0.72f, 0.29f);              // a step in

            var vase = new List<Vector2>();
            const int vaseSteps = 14;
            for (int i = 0; i <= vaseSteps; i++)
            {
                float t = i / (float)vaseSteps;
                // Full low, an ogee in to the waist: a smoothstep's S, eased so the belly bulges.
                float e = t * t * (3f - 2f * t);
                float r = 0.40f + 0.32f * (1f - e) + 0.04f * Mathf.Sin(Mathf.PI * t);
                vase.Add(new Vector2(r * R, (0.29f + 0.49f * t) * H));
            }

            b.Profile(vase, PartPole);                              // the vase
            Run(b, R, H, 0.40f, 0.78f, 0.46f, 0.78f);              // the astragal's underside
            Arc(b, R, H, 0.46f, 0.81f, 0.05f, 0.03f);               // the astragal
            Run(b, R, H, 0.46f, 0.84f, 0.41f, 0.84f);              // its top

            var collar = new List<Vector2>();
            const int neckSteps = 6;
            for (int i = 0; i <= neckSteps; i++)
            {
                float t = i / (float)neckSteps;
                // A cove up into the shaft.
                float r = neck + (0.41f - neck) * (1f - t) * (1f - t);
                collar.Add(new Vector2(r * R, (0.84f + 0.16f * t) * H));
            }

            b.Profile(collar, PartPole);                            // the neck into the shaft
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

        /// <summary>
        /// A banner's arms (FlagPoleShape.ArmTop, ArmBottom): from the pole's axis out along the
        /// fly at rest (the mesh's x) to the held edge's far end -- the top one at the anchor's
        /// level (y = Length: the flag's top hoist corner, lowered with the flag on its pole), the
        /// bottom one ArmBottomDown under it -- so the cloth's held edge runs along the arm, which
        /// turns with the pole's tilt and lean as the edge does (both are in the pole's frame).
        /// </summary>
        private static void Arms(Builder b, FlagPoleShape s)
        {
            if (s.ArmTop > 0f)
                Arm(b, s, s.Length, s.Offset + s.ArmTop);
            if (s.ArmBottom > 0f)
                Arm(b, s, s.Length - s.ArmBottomDown, s.Offset + s.ArmBottom);
        }

        private static void Arm(Builder b, FlagPoleShape s, float y, float end)
        {
            float r = Mathf.Max(s.ArmRadius, 1e-4f);
            int r0 = b.ArmRing(new Vector3(0f, y, 0f), 0f, r);
            int r1 = b.ArmRing(new Vector3(end, y, 0f), end, r);
            b.Tube(r0, r1, ArmSides);
            b.ArmCap(new Vector3(end, y, 0f), end, r);
        }

        /// <summary>
        /// A sectional pole's joint at <paramref name="y"/>, as a swaged one is made: the lower
        /// section's top rounded over in a bead, then a shoulder stepping in at 45 degrees to the
        /// section above -- both catch the light, so the steps read from afar.
        /// </summary>
        private static void Joint(Builder b, float y, float below, float above)
        {
            float bead = Mathf.Max(0.004f, 0.1f * below);
            float height = Mathf.Max(0.02f, 0.6f * below);
            var points = new List<Vector2>();
            const int steps = 8;
            for (int i = 0; i <= steps; i++)
            {
                float a = -0.5f * Mathf.PI + i * Mathf.PI / steps;
                points.Add(new Vector2(below + bead * Mathf.Cos(a), y - 0.5f * height + 0.5f * height * Mathf.Sin(a)));
            }

            b.Profile(points, PartPole);
            float step = Mathf.Max(below - above, 0.002f);
            b.Profile(new List<Vector2> { new Vector2(below, y), new Vector2(above * 0.995f, y + step) }, PartPole);
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

        /// <summary>
        /// The Cap top (FlagPoleStyle.Cap; the author's Battlefield 4 pole, 2026-11-19): a short drum
        /// on the truck, a little wider than the shaft's top, its upper edge rounded, painted as the
        /// pole (part 0: galvanized with it, and in Real Glass's plain copy). Its underside a ring
        /// facing down from the truck's flat top out to its side. Returns the top.
        /// </summary>
        private static float Cap(Builder b, FlagPoleShape s, float y1)
        {
            float inner = s.TruckRadius - 0.12f * s.TruckRadius;
            float R = Mathf.Max(s.CapRadius, 1e-4f), H = Mathf.Max(s.CapHeight, 1e-4f);
            float e = Mathf.Clamp(s.CapEdge, 0f, Mathf.Min(0.5f * H, 0.9f * R));

            // The underside, where it stands out of the truck (inner first: facing down).
            if (R > inner + 1e-5f)
            {
                int u0 = b.Ring(y1, inner, 0f, -1f, PartPole);
                int u1 = b.Ring(y1, R, 0f, -1f, PartPole);
                b.Band(u0, u1, Sides);
            }

            int c0 = b.Ring(y1, R, 1f, 0f, PartPole);
            int previous = b.Ring(y1 + H - e, R, 1f, 0f, PartPole);
            b.Band(c0, previous, Sides);

            // The rounded edge: a quarter round of radius e, up and in.
            const int steps = 4;
            for (int i = 1; i <= steps; i++)
            {
                float a = i / (float)steps * 0.5f * Mathf.PI;
                float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                int ring = b.Ring(y1 + H - e + e * sn, Mathf.Max(R - e + e * c, 1e-4f), c, sn, PartPole);
                b.Band(previous, ring, Sides);
                previous = ring;
            }

            b.Cap(previous, y1 + H, true, PartPole);
            return y1 + H;
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
            int k3 = b.Ring(y1 + collar, 0.45f * s.SpearRadius, 0f, 1f, PartTop);
            b.Band(k2, k3, Sides);

            // A leaf-shaped head, as a cast spear finial: a narrow waist, widest a third up, then
            // curving in to its point.
            float height = s.SpearHeight - collar;
            var leaf = new List<Vector2>();
            const int steps = 14;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float r = t < 0.3f
                    ? 0.45f + 0.55f * Mathf.Sin(0.5f * Mathf.PI * t / 0.3f)
                    : Mathf.Pow(Mathf.Cos(0.5f * Mathf.PI * (t - 0.3f) / 0.7f), 1.2f);
                leaf.Add(new Vector2(Mathf.Max(r * s.SpearRadius, 1e-4f), y1 + collar + t * height));
            }

            b.Profile(leaf, PartTop);
            return y1 + s.SpearHeight;
        }

        // ---- the concrete block (FlagPoleBase.Block, 2026-11-19) -----------------------------------

        /// <summary>
        /// Where a pole's block goes: its middle on the pole's foot (<paramref name="foot"/>: the
        /// pole's placement's own, column 3 of <see cref="Placement"/>), UPRIGHT whatever the pole's
        /// tilt and lean, turned by the flag's heading alone (<paramref name="heading"/>, the prop's
        /// angle: its x along FlagProps.Direction).
        /// </summary>
        public static Matrix4x4 BlockPlacement(Vector3 foot, float heading)
        {
            var x = new Vector3(Mathf.Cos(heading), 0f, Mathf.Sin(heading));
            Vector3 z = Vector3.Cross(x, Vector3.up);
            var m = new Matrix4x4();
            m.SetColumn(0, new Vector4(x.x, x.y, x.z, 0f));
            m.SetColumn(1, new Vector4(0f, 1f, 0f, 0f));
            m.SetColumn(2, new Vector4(z.x, z.y, z.z, 0f));
            m.SetColumn(3, new Vector4(foot.x, foot.y, foot.z, 1f));
            return m;
        }

        /// <summary>
        /// The concrete block's mesh (FlagPoleShape's block: none -- null -- without one), into
        /// <paramref name="reuse"/> if given. In its own frame: y up from the pole's foot, its
        /// middle on the axis, from <see cref="FlagPoleShape.BlockSink"/> under the foot to its
        /// height over it (no bottom: it is in the ground). Its OWN mesh and draw, not the pole's:
        /// Real Glass's Reflective finish is drawn over the whole of the pole's plain copy
        /// (RealGlassFinish), and concrete is no mirror.
        /// </summary>
        /// <remarks>
        /// Low poly, flat where it is flat: Square -- four sides, four upright chamfers, eight top
        /// chamfers and an octagon on top (38 triangles); Rounded -- a rounded square ring of
        /// <see cref="BlockCornerSteps"/> segments a quarter corner, its sides and the top edge's
        /// <see cref="BlockEdgeSteps"/> rounding bands, and a fan on top (180); Round -- a ring of
        /// <see cref="BlockRoundSides"/> the same way (216). Each vertex's first uv: x the part
        /// (<see cref="PartBlock"/>), y 0 (no tube: the shader leaves it where it is); its second:
        /// where the concrete's texture lies, metres over <see cref="ConcreteTile"/> -- along each
        /// face (round a rounded one) and up it, the top across -- its tangents along u, so the
        /// concrete's normal map is in tangent space.
        /// </remarks>
        public static Mesh BuildBlock(FlagPoleShape s, Mesh reuse)
        {
            if (s.Base != FlagPoleBase.Block || s.BlockWidth <= 0f || s.BlockHeight <= 0f)
                return null;

            var b = new Builder();
            float h = 0.5f * s.BlockWidth, top = s.BlockHeight, sink = FlagPoleShape.BlockSink;
            if (s.BlockShape == FlagBlockShape.Square)
            {
                SquareBlock(b, h, top, sink, Mathf.Clamp(s.BlockChamfer, 0f, 0.45f * Mathf.Min(h, top)));
            }
            else
            {
                bool round = s.BlockShape == FlagBlockShape.Round;
                float corner = round ? h : Mathf.Clamp(s.BlockCorner, 1e-3f, h);
                float edge = Mathf.Clamp(s.BlockEdge, 0f, Mathf.Min(0.5f * top, 0.9f * corner));
                RoundBlock(b, h, top, sink, corner, edge, round);
            }

            float reach = s.BlockReach + 0.01f;
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(-reach, -sink - 0.01f, -reach), new Vector3(reach, top + 0.01f, reach));
            Mesh mesh = b.ToMesh(reuse, bounds);
            mesh.name = "RealFlag pole block";
            return mesh;
        }

        /// <summary>One vertex of the block: its place, normal, the texture's u direction (the tangent) and which way its v runs (the tangent's sign), the texture's place in metres.</summary>
        private static int BlockVertex(Builder b, Vector3 p, Vector3 n, Vector3 along, Vector3 up, float u, float v)
        {
            float sign = Vector3.Dot(Vector3.Cross(n, along), up) >= 0f ? 1f : -1f;
            b.Vertices.Add(p);
            b.Normals.Add(n);
            b.Tangents.Add(new Vector4(along.x, along.y, along.z, sign));
            b.Uv.Add(new Vector4(PartBlock, 0f, 0f, 0f));
            b.Uv2.Add(new Vector4(u / ConcreteTile, v / ConcreteTile, 0f, 0f));
            return b.Vertices.Count - 1;
        }

        /// <summary>A triangle of the block facing <paramref name="n"/> (Tube's rule: the cross of its first two edges is its front).</summary>
        private static void BlockTriangle(Builder b, int i0, int i1, int i2, Vector3 n)
        {
            Vector3 p0 = b.Vertices[i0];
            bool forward = Vector3.Dot(Vector3.Cross(b.Vertices[i1] - p0, b.Vertices[i2] - p0), n) >= 0f;
            b.Triangles.Add(i0);
            b.Triangles.Add(forward ? i1 : i2);
            b.Triangles.Add(forward ? i2 : i1);
        }

        /// <summary>A flat quad of the block: corners in order round it, one normal, u along <paramref name="along"/> from the origin, v up <paramref name="up"/> (heights given).</summary>
        private static void BlockQuad(Builder b, Vector3 a0, Vector3 a1, Vector3 a2, Vector3 a3, Vector3 n, Vector3 along, Vector3 up,
                                      float v0, float v1, float v2, float v3)
        {
            int i0 = BlockVertex(b, a0, n, along, up, Vector3.Dot(a0, along), v0);
            int i1 = BlockVertex(b, a1, n, along, up, Vector3.Dot(a1, along), v1);
            int i2 = BlockVertex(b, a2, n, along, up, Vector3.Dot(a2, along), v2);
            int i3 = BlockVertex(b, a3, n, along, up, Vector3.Dot(a3, along), v3);
            BlockTriangle(b, i0, i1, i2, n);
            BlockTriangle(b, i0, i2, i3, n);
        }

        /// <summary>
        /// A square block, half <paramref name="h"/> wide, <paramref name="top"/> high, its edges
        /// chamfered <paramref name="c"/>: the four sides, the four upright chamfers, the octagon round
        /// its top at <paramref name="top"/> - c, the eight chamfers up and in to the top's octagon
        /// (each of its corners moved in by c both ways), and the top.
        /// </summary>
        private static void SquareBlock(Builder b, float h, float top, float sink, float c)
        {
            Vector3 up = Vector3.up;
            Vector3[] n = { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
            float y0 = -sink, y1 = top - c;

            // The ring round the top's chamfers at y1, in order: each side's start and end.
            var ring = new List<Vector3>(8);
            for (int k = 0; k < 4; k++)
            {
                Vector3 nk = n[k], tk = n[(k + 1) % 4];
                Vector3 start = nk * h - tk * (h - c), end = nk * h + tk * (h - c);
                ring.Add(start);
                ring.Add(end);

                // The side, from the ground up (v its height).
                BlockQuad(b, start + up * y0, end + up * y0, end + up * y1, start + up * y1, nk, tk, up, y0, y0, y1, y1);

                // The upright chamfer to the next side.
                Vector3 next = n[(k + 1) % 4] * h - n[(k + 2) % 4] * (h - c);
                Vector3 cn = (nk + tk).normalized, ct = (next - end).normalized;
                BlockQuad(b, end + up * y0, next + up * y0, next + up * y1, end + up * y1, cn, ct, up, y0, y0, y1, y1);
            }

            // The top's octagon: the ring's corners moved in by c on both axes.
            var inner = new List<Vector3>(8);
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = ring[i];
                inner.Add(new Vector3(p.x - c * Mathf.Sign(p.x), top, p.z - c * Mathf.Sign(p.z)));
            }

            // The chamfers round the top, each up and in from the ring to the octagon.
            for (int i = 0; i < 8; i++)
            {
                Vector3 a0 = ring[i] + up * y1, a1 = ring[(i + 1) % 8] + up * y1, a2 = inner[(i + 1) % 8], a3 = inner[i];
                Vector3 along = (a1 - a0).normalized;
                Vector3 outward = new Vector3(0.5f * (a0.x + a1.x), 0f, 0.5f * (a0.z + a1.z)).normalized;
                Vector3 normal = Vector3.Cross(along, a3 - a0).normalized;
                if (Vector3.Dot(normal, outward + up) < 0f)
                    normal = -normal;
                Vector3 slope = (a3 - a0 - along * Vector3.Dot(a3 - a0, along)).normalized;
                float rise = y1 + (a3 - a0 - along * Vector3.Dot(a3 - a0, along)).magnitude;
                BlockQuad(b, a0, a1, a2, a3, normal, along, slope, y1, y1, rise, rise);
            }

            // The top: a fan over the octagon, the texture laid across it (u along x, v along z).
            int first = b.Vertices.Count;
            for (int i = 0; i < 8; i++)
                BlockVertex(b, inner[i], up, Vector3.right, Vector3.forward, inner[i].x, inner[i].z);
            for (int i = 1; i < 7; i++)
                BlockTriangle(b, first, first + i, first + i + 1, up);
        }

        /// <summary>
        /// A rounded or round block: a ring round the axis (a square of half <paramref name="h"/>
        /// whose corners are quarter rounds of <paramref name="corner"/>, or a circle), straight up
        /// from the ground to <paramref name="edge"/> under its top, the top edge a quarter round of
        /// <paramref name="edge"/> in <see cref="BlockEdgeSteps"/> bands, and a fan on top. Smooth
        /// round its corners; the texture's u the way round, a seam's vertices doubled.
        /// </summary>
        private static void RoundBlock(Builder b, float h, float top, float sink, float corner, float edge, bool round)
        {
            Vector3 up = Vector3.up;

            // The ring: each point's centre (its corner's) and its way out.
            var centres = new List<Vector3>();
            var outs = new List<Vector3>();
            if (round)
            {
                for (int i = 0; i < BlockRoundSides; i++)
                {
                    float a = i * 2f * Mathf.PI / BlockRoundSides;
                    centres.Add(Vector3.zero);
                    outs.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
                }
            }
            else
            {
                float k = h - corner;
                Vector3[] at = { new Vector3(k, 0f, k), new Vector3(-k, 0f, k), new Vector3(-k, 0f, -k), new Vector3(k, 0f, -k) };
                for (int q = 0; q < 4; q++)
                {
                    for (int i = 0; i <= BlockCornerSteps; i++)
                    {
                        float a = (q + i / (float)BlockCornerSteps) * 0.5f * Mathf.PI;
                        centres.Add(at[q]);
                        outs.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
                    }
                }
            }

            // The way round, along the outside: u at each point, and the seam's again at the end.
            int count = centres.Count;
            var u = new float[count + 1];
            for (int i = 1; i <= count; i++)
            {
                Vector3 p0 = centres[i - 1] + outs[i - 1] * corner, p1 = centres[i % count] + outs[i % count] * corner;
                u[i] = u[i - 1] + (p1 - p0).magnitude;
            }

            // The rings, from the ground up: the sides' two, then round the top edge to its top.
            int rings = 2 + BlockEdgeSteps;
            var firsts = new int[rings];
            for (int r = 0; r < rings; r++)
            {
                float angle = r < 2 ? 0f : (r - 1) / (float)BlockEdgeSteps * 0.5f * Mathf.PI;
                float y = r == 0 ? -sink : top - edge + edge * Mathf.Sin(angle);
                float inset = edge * (1f - Mathf.Cos(angle));
                float v = r == 0 ? -sink : top - edge + edge * angle;
                firsts[r] = b.Vertices.Count;
                for (int i = 0; i <= count; i++)
                {
                    Vector3 o = outs[i % count];
                    Vector3 p = centres[i % count] + o * (corner - inset) + up * y;
                    Vector3 normal = (o * Mathf.Cos(angle) + up * Mathf.Sin(angle)).normalized;
                    Vector3 along = new Vector3(-o.z, 0f, o.x);
                    Vector3 rising = (up * Mathf.Cos(angle) - o * Mathf.Sin(angle)).normalized;
                    BlockVertex(b, p, normal, along, rising, u[i], v);
                }
            }

            for (int r = 0; r + 1 < rings; r++)
            {
                for (int i = 0; i < count; i++)
                {
                    int a0 = firsts[r] + i, a1 = a0 + 1, c0 = firsts[r + 1] + i, c1 = c0 + 1;
                    Vector3 n = (b.Normals[a0] + b.Normals[a1] + b.Normals[c0] + b.Normals[c1]).normalized;
                    BlockTriangle(b, a0, a1, c1, n);
                    BlockTriangle(b, a0, c1, c0, n);
                }
            }

            // The top: a fan from its middle over the last ring (the texture across it).
            int centre = BlockVertex(b, up * top, up, Vector3.right, Vector3.forward, 0f, 0f);
            int rim = b.Vertices.Count;
            int last = firsts[rings - 1];
            for (int i = 0; i < count; i++)
            {
                Vector3 p = b.Vertices[last + i];
                BlockVertex(b, p, up, Vector3.right, Vector3.forward, p.x, p.z);
            }

            for (int i = 0; i < count; i++)
                BlockTriangle(b, centre, rim + i, rim + (i + 1) % count, up);
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
