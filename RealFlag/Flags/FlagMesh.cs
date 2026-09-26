using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealFlag.Flags
{
    /// <summary>
    /// One flag's drawable, refreshed every frame from the cloth, between its last two steps (the
    /// cloth steps 60 times a second whatever the frame rate; drawing the step it happens to be
    /// on would judder on a 144 Hz screen -- Fiedler, "Fix Your Timestep!", 2004). Engine code,
    /// shared with the GPU preview (tools/sync-sim.ps1).
    /// </summary>
    /// <remarks>
    /// Two ways, by <see cref="Refine"/> (FlagRenderer's choice, from the flag's size on the
    /// screen):
    ///
    /// - A vertex a particle (Refine 1, most flags: far off, small on the screen): the vertices,
    ///   normals (central differences) and tangents worked out on the cloth's thread and handed
    ///   to the mesh -- a few microseconds of the main thread a flag.
    ///
    /// - Finer (2 to <see cref="MaxRefine"/> drawn cells a cloth cell each way, the flags big on
    ///   the screen): the particles go to the GPU as a small float texture (a texel each: its
    ///   position relative to the anchor; 16 bytes a particle), and FlagCloth.shader's vertex
    ///   program lays each vertex of a finer static grid on the smooth surface through them
    ///   (ClothSurface: the Catmull-Rom patch of each cell), with the normal and tangent from the
    ///   surface's own derivatives. A 30 m flag drawn a vertex a particle was a polygon of
    ///   21-pixel sides 105 m off; four to a cell, 5. Refreshing that grid's vertices on the CPU
    ///   would be 40 bytes a VERTEX and a millisecond of Mono; the texture's upload is a fixed
    ///   20-100 microseconds in the editor, which the few near flags can afford and a city's
    ///   hundred far ones could not -- hence the two ways.
    ///
    /// Either way the fly, top and bottom edges are smoothed first (ClothSurface.SmoothEdges).
    /// Vertices are relative to the anchor (the top hoist corner), so the mesh is drawn with the
    /// anchor's translation. UV: x from the hoist (0) to the fly (1), y from the top (0) to the
    /// bottom (1) -- what FlagPattern.cginc reads, and where the shader samples the surface.
    /// Triangles (k, k+1, k+cols) and (k+1, k+cols+1, k+cols) face the side the normals
    /// (FlagCloth.ComputeNormals, or d/du x d/dv) point to: Unity's front face. Tangents run along
    /// the fly.
    /// </remarks>
    public sealed class FlagMesh
    {
        /// <summary>At most this many drawn cells a cloth cell, each way.</summary>
        public const int MaxRefine = 4;

        /// <summary>A drawn grid is kept under this many vertices (an Ultra 30 m flag at four: 17 500).</summary>
        public const int MaxVertices = 20000;

        /// <summary>The edges smoothed before drawing (ClothSurface.SmoothEdges); off for comparisons in the preview.</summary>
        public static bool SmoothEdges = true;

        public static readonly int IdGrid = Shader.PropertyToID("_RFGrid");
        public static readonly int IdGridSize = Shader.PropertyToID("_RFGridSize");
        public static readonly int IdUseGrid = Shader.PropertyToID("_RFUseGrid");

        public readonly Mesh Mesh;

        /// <summary>Drawn cells per cloth cell, each way (1: a vertex per particle, the CPU's way).</summary>
        public readonly int Refine;

        private readonly FlagCloth _cloth;
        private readonly float[] _x, _y, _z, _scratch;

        // A vertex a particle: the arrays the mesh takes.
        private readonly float[] _nx, _ny, _nz;
        private readonly Vector3[] _vertices, _normals;
        private readonly Vector4[] _tangents;

        // Finer: the particles' texture.
        private readonly Texture2D _grid;
        private readonly float[] _packed;
        private readonly byte[] _raw;
        private readonly Vector4 _gridSize;

        /// <summary>The drawn sheet's box, relative to the anchor, after the last <see cref="Refresh"/>.</summary>
        public Bounds Bounds { get; private set; }

        /// <summary>The finest this cloth can be drawn within <see cref="MaxVertices"/>, up to <paramref name="refine"/>.</summary>
        public static int RefineFor(FlagCloth cloth, int refine)
        {
            refine = Mathf.Clamp(refine, 1, MaxRefine);
            while (refine > 1 && ClothSurface.VerticesFor(cloth.Columns, cloth.Rows, refine) > MaxVertices)
                refine--;
            return refine;
        }

        public FlagMesh(FlagCloth cloth, int refine = 1)
        {
            _cloth = cloth;
            Refine = RefineFor(cloth, refine);

            int n = cloth.Count, cols = cloth.Columns, rows = cloth.Rows;
            _x = new float[n];
            _y = new float[n];
            _z = new float[n];
            _scratch = new float[3 * Math.Max(cols, rows)];

            if (Refine == 1)
            {
                _nx = new float[n];
                _ny = new float[n];
                _nz = new float[n];
                _vertices = new Vector3[n];
                _normals = new Vector3[n];
                _tangents = new Vector4[n];
            }
            else
            {
                _packed = new float[n * 4];
                _raw = new byte[n * 16];
                _gridSize = new Vector4(cols, rows, 1f / cols, 1f / rows);
                _grid = new Texture2D(cols, rows, TextureFormat.RGBAFloat, false, true)
                {
                    name = "RealFlag cloth grid",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    anisoLevel = 0,
                };
            }

            // The drawn grid: Refine vertices a cloth cell each way (at the rest pose, which the
            // shader replaces when finer; the arrays below when not).
            int dc = (cols - 1) * Refine + 1, dr = (rows - 1) * Refine + 1;
            var vertices = new Vector3[dc * dr];
            var normals = new Vector3[dc * dr];
            var tangents = new Vector4[dc * dr];
            var uv = new Vector2[dc * dr];
            for (int j = 0; j < dr; j++)
            {
                float v = j / (float)(dr - 1);
                for (int i = 0; i < dc; i++)
                {
                    float u = i / (float)(dc - 1);
                    int k = j * dc + i;
                    vertices[k] = new Vector3(u * cloth.Length, -v * cloth.Height, 0f);
                    normals[k] = Vector3.back;
                    tangents[k] = new Vector4(1f, 0f, 0f, 1f);
                    uv[k] = new Vector2(u, v);
                }
            }

            var triangles = new int[(dc - 1) * (dr - 1) * 6];
            int t = 0;
            for (int j = 0; j < dr - 1; j++)
            {
                for (int i = 0; i < dc - 1; i++)
                {
                    int k = j * dc + i;
                    triangles[t++] = k;
                    triangles[t++] = k + 1;
                    triangles[t++] = k + dc;
                    triangles[t++] = k + 1;
                    triangles[t++] = k + dc + 1;
                    triangles[t++] = k + dc;
                }
            }

            Mesh = new Mesh { name = "RealFlag cloth" };
            if (Refine == 1)
                Mesh.MarkDynamic();
            Mesh.vertices = vertices;
            Mesh.normals = normals;
            Mesh.tangents = tangents;
            Mesh.uv = uv;
            Mesh.triangles = triangles;
            Refresh(1f);
            Apply();
        }

        /// <summary>The drawn grid's vertex count.</summary>
        public int Vertices
        {
            get { return ClothSurface.VerticesFor(_cloth.Columns, _cloth.Rows, Refine); }
        }

        /// <summary>
        /// The cloth at <paramref name="alpha"/> of the way from its previous step to its last
        /// (0..1), its edges smoothed, into the mesh's arrays or the texture's bytes (not yet the
        /// mesh or the texture: <see cref="Apply"/>). Managed arithmetic only (Unity's structs are
        /// IL), so the cloth's thread does it (ClothWorker).
        /// </summary>
        public void Refresh(float alpha)
        {
            FlagCloth c = _cloth;
            int n = c.Count;
            float a = alpha < 0f ? 0f : alpha > 1f ? 1f : alpha;
            float b = 1f - a;

            for (int k = 0; k < n; k++)
            {
                _x[k] = c.PrevX[k] * b + c.X[k] * a;
                _y[k] = c.PrevY[k] * b + c.Y[k] * a;
                _z[k] = c.PrevZ[k] * b + c.Z[k] * a;
            }

            if (SmoothEdges)
                ClothSurface.SmoothEdges(_x, _y, _z, c.Columns, c.Rows, _scratch);

            if (Refine == 1)
                RefreshVertices();
            else
                RefreshTexture();
        }

        /// <summary>A vertex a particle: positions, normals from central differences, tangents along the fly, the box.</summary>
        private void RefreshVertices()
        {
            FlagCloth c = _cloth;
            c.ComputeNormals(_x, _y, _z, _nx, _ny, _nz);

            Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            int cols = c.Columns;

            for (int j = 0; j < c.Rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    int k = j * cols + i;
                    Vector3 p = new Vector3(_x[k], _y[k], _z[k]);
                    _vertices[k] = p;
                    _normals[k] = new Vector3(_nx[k], _ny[k], _nz[k]);

                    int k0 = i > 0 ? k - 1 : k, k1 = i < cols - 1 ? k + 1 : k;
                    Vector3 along = new Vector3(_x[k1] - _x[k0], _y[k1] - _y[k0], _z[k1] - _z[k0]);
                    float len = along.magnitude;
                    along = len > 1e-6f ? along / len : Vector3.right;
                    _tangents[k] = new Vector4(along.x, along.y, along.z, 1f);

                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }

            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            bounds.Expand(0.05f);
            Bounds = bounds;
        }

        /// <summary>Finer: the particles into the texture's bytes, and the box (a little wider: the spline bulges past them between).</summary>
        private void RefreshTexture()
        {
            FlagCloth c = _cloth;
            int n = c.Count;
            float minX = float.MaxValue, minY = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue, maxZ = float.MinValue;
            for (int k = 0, o = 0; k < n; k++, o += 4)
            {
                float x = _x[k], y = _y[k], z = _z[k];
                _packed[o] = x;
                _packed[o + 1] = y;
                _packed[o + 2] = z;
                _packed[o + 3] = 1f;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
                if (z < minZ) minZ = z;
                if (z > maxZ) maxZ = z;
            }

            Buffer.BlockCopy(_packed, 0, _raw, 0, _raw.Length);

            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ));
            bounds.Expand(0.05f + 0.25f * Math.Max(c.CellU, c.CellV));
            Bounds = bounds;
        }

        /// <summary>The last <see cref="Refresh"/> into the mesh or the texture, and the box into the mesh (main thread only).</summary>
        public void Apply()
        {
            if (Refine == 1)
            {
                Mesh.vertices = _vertices;
                Mesh.normals = _normals;
                Mesh.tangents = _tangents;
            }
            else
            {
                _grid.LoadRawTextureData(_raw);
                _grid.Apply(false);
            }

            Mesh.bounds = Bounds;
        }

        /// <summary>Refresh, and hand the result to the GPU (main thread only).</summary>
        public void Upload(float alpha)
        {
            Refresh(alpha);
            Apply();
        }

        /// <summary>Which way this mesh is drawn, and the particles' texture if finer, into the flag's property block, before each draw.</summary>
        public void Bind(MaterialPropertyBlock block)
        {
            block.SetFloat(IdUseGrid, Refine > 1 ? 1f : 0f);
            if (Refine > 1)
            {
                block.SetTexture(IdGrid, _grid);
                block.SetVector(IdGridSize, _gridSize);
            }
        }

        public void Destroy()
        {
            Release(Mesh);
            Release(_grid);
        }

        /// <summary>Destroy in the game, DestroyImmediate in the editor's preview (Destroy may not be called in edit mode).</summary>
        public static void Release(Object thing)
        {
            if (thing == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(thing);
            else
                Object.DestroyImmediate(thing);
        }
    }

    /// <summary>
    /// A flag's design into its material property block: the uniforms FlagCloth.shader reads.
    /// Engine code, shared with the GPU preview.
    /// </summary>
    public static class FlagLook
    {
        public static readonly int IdColour1 = Shader.PropertyToID("_RFColor1");
        public static readonly int IdColour2 = Shader.PropertyToID("_RFColor2");
        public static readonly int IdColour3 = Shader.PropertyToID("_RFColor3");
        public static readonly int IdPattern = Shader.PropertyToID("_RFPattern");
        public static readonly int IdAspect = Shader.PropertyToID("_RFAspect");
        public static readonly int IdLength = Shader.PropertyToID("_RFLength");
        public static readonly int IdHighlight = Shader.PropertyToID("_RFHighlight");
        public static readonly int IdSunDir = Shader.PropertyToID("_RFSunDir");
        public static readonly int IdSunColor = Shader.PropertyToID("_RFSunColor");
        public static readonly int IdImage = Shader.PropertyToID("_RFImage");
        public static readonly int IdUseImage = Shader.PropertyToID("_RFUseImage");

        /// <summary>
        /// The colours go in as LINEAR vectors, converted here: the game renders in linear
        /// colour space, and a vector (unlike a colour property) is passed as it is. A country's
        /// flag sets its image (FlagImages: made on first use) and _RFUseImage; without the image
        /// it falls back to its pattern.
        /// </summary>
        public static void Apply(MaterialPropertyBlock block, FlagSpec spec, Vector4 highlight)
        {
            block.SetVector(IdColour1, Linear(spec.Colour1));
            block.SetVector(IdColour2, Linear(spec.Colour2));
            block.SetVector(IdColour3, Linear(spec.Colour3));
            block.SetFloat(IdPattern, spec.Pattern);
            block.SetFloat(IdAspect, FlagProportion.Ratio(spec.Proportion));
            block.SetFloat(IdLength, spec.Length);
            block.SetVector(IdHighlight, highlight);

            FlagCountry country = FlagCountries.Find(spec.Country);
            Texture2D image = country != null ? FlagImages.Get(country.Image) : null;
            if (image != null)
                block.SetTexture(IdImage, image);
            block.SetFloat(IdUseImage, image != null ? 1f : 0f);
        }

        public static Vector4 Linear(uint rgb)
        {
            int r, g, b;
            FlagColours.Channels(rgb, out r, out g, out b);
            return new Vector4(FlagColours.ToLinear(r / 255f), FlagColours.ToLinear(g / 255f), FlagColours.ToLinear(b / 255f), 1f);
        }
    }
}
