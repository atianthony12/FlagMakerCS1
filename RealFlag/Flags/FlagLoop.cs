using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// One loop as the graphics card plays it (FlagMotion.Loop): the frames LoopBake made, in a
    /// float texture -- a tile of particles a frame -- and the grids its flags are drawn with.
    /// Every flag of its shape shares it: a flag is one draw of one of its grids with the flag's
    /// own design and its own place in the loop, and nothing else a frame -- no cloth, no mesh of
    /// its own, no upload. Engine code, shared with the GPU preview (tools/sync-sim.ps1).
    /// </summary>
    /// <remarks>
    /// The idea is the vertex animation texture games bake cloth and crowds into (a texel a vertex
    /// a frame, read in the vertex program); here a texel a PARTICLE, read as FlagMesh's own
    /// texture path reads one moment's (FlagCloth.shader lays each vertex on the Catmull-Rom
    /// surface through the particles), so one texture serves every grid: a flag small on the
    /// screen is drawn with a few dozen vertices on the same surface, a near one with four to a
    /// cloth cell. The texture is RGBAFloat, point-sampled, as FlagMesh's (half floats would put
    /// 2 mm of noise on a 3 m flag's particles, a degree of its normals).
    /// </remarks>
    public sealed class FlagLoop
    {
        public readonly int Columns, Rows, Frames, TilesX;
        public readonly float FrameTime;

        /// <summary>The loop's length, seconds.</summary>
        public readonly float Seconds;

        /// <summary>The box of every frame, in the flag's own frame (heading 0), relative to the anchor.</summary>
        public readonly Bounds Bounds;

        /// <summary>
        /// The cloth's extents over the whole loop for the pick volume's slab, in the flag's own
        /// frame: out along its breeze's level way (or the fly at rest), down its hoist, across.
        /// </summary>
        public readonly float MinU, MaxU, MinV, MaxV, MinN, MaxN;

        /// <summary>The texture's size in bytes (the graphics card's copy; ours is let go once it is there).</summary>
        public readonly int Bytes;

        /// <summary>For the log: what it is (LoopBake.Describe).</summary>
        public readonly string Text;

        private readonly Texture2D _texture;
        private readonly Vector4 _gridSize;
        private readonly int _cells;
        private readonly float _aspect;
        private readonly Dictionary<int, Mesh> _meshes = new Dictionary<int, Mesh>();

        /// <param name="bake">Finished, not failed. Its frames are let go here (the texture has them).</param>
        /// <param name="keepData">Keep the bake's frames (the preview and the tests read them back).</param>
        public FlagLoop(LoopBake bake, bool keepData = false)
        {
            Columns = bake.Columns;
            Rows = bake.Rows;
            Frames = bake.Frames;
            TilesX = bake.TilesX;
            FrameTime = bake.FrameTime;
            Seconds = bake.Seconds;
            Text = bake.Describe();
            Bytes = bake.Raw.Length;

            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(bake.MinX, bake.MinY, bake.MinZ), new Vector3(bake.MaxX, bake.MaxY, bake.MaxZ));
            Bounds = bounds;

            // The slab's axes at heading 0, as FlagRenderer.PickVolumes takes them for a flag whose
            // "wind" is its fly at rest (PlanLoop): out along it, square to the hoist; down the hoist.
            FlagCloth c = bake.Cloth;
            var h = new Vector3(c.HoistX, c.HoistY, c.HoistZ);
            var fly = new Vector3(bake.FlyX, bake.FlyY, bake.FlyZ);
            Vector3 u = fly - h * Vector3.Dot(fly, h);
            u = u.sqrMagnitude > 0.09f ? u.normalized : fly;
            Vector3 n = Vector3.Cross(h, u).normalized;
            bake.Extents(u.x, u.y, u.z, h.x, h.y, h.z, n.x, n.y, n.z, out MinU, out MaxU, out MinV, out MaxV, out MinN, out MaxN);
            Out = u;

            _cells = Math.Max(Columns, Rows) - 1;
            _aspect = (Math.Min(Columns, Rows) - 1) / (float)_cells;
            _gridSize = new Vector4(Columns, Rows, 1f / bake.Width, 1f / bake.Height);
            _texture = new Texture2D(bake.Width, bake.Height, TextureFormat.RGBAFloat, false, true)
            {
                name = "RealFlag loop",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
            };
            _texture.LoadRawTextureData(bake.Raw);

            // No copy of it kept on the engine's side (the texture is never read back). Ours of
            // the places is kept (2026-11-11, 1-2 MB a loop): a loop standing in for a waving flag
            // begins at the frame nearest its cloth's pose, and hands its pose back to a new cloth
            // (Nearest, Motion). The bytes go.
            _texture.Apply(false, true);
            Places = new LoopFrames(bake);
            if (!keepData)
            {
                bake.Data = null;
                bake.Raw = null;
            }
        }

        /// <summary>The frames on our side, for the swaps to and from a cloth (LoopFrames).</summary>
        public readonly LoopFrames Places;

        /// <summary>The slab's axis out (see <see cref="MinU"/>), in the flag's own frame.</summary>
        public readonly Vector3 Out;

        /// <summary>The finest grid drawn: four drawn cells a cloth cell (FlagMesh.MaxRefine), as a waving flag's.</summary>
        public int FinestCells
        {
            get { return _cells * FlagMesh.MaxRefine; }
        }

        /// <summary>
        /// The grid with <paramref name="cells"/> drawn cells along the cloth's longer side (made on
        /// first use, kept): the flag's uv alone -- the vertex program puts every vertex on the
        /// loop's surface -- and the loop's box, for the camera's culling.
        /// </summary>
        public Mesh MeshFor(int cells)
        {
            cells = Mathf.Clamp(cells, 2, FinestCells);
            Mesh mesh;
            if (_meshes.TryGetValue(cells, out mesh) && mesh != null)
                return mesh;

            int other = Mathf.Max(2, Mathf.RoundToInt(cells * _aspect));
            int dc = (Columns >= Rows ? cells : other) + 1, dr = (Columns >= Rows ? other : cells) + 1;
            while (dc * dr > FlagMesh.MaxVertices)
            {
                dc = (dc - 1) / 2 + 1;
                dr = (dr - 1) / 2 + 1;
            }

            var vertices = new Vector3[dc * dr];
            var normals = new Vector3[dc * dr];
            var tangents = new Vector4[dc * dr];
            var uv = new Vector2[dc * dr];
            Vector3 centre = Bounds.center;
            for (int j = 0; j < dr; j++)
            {
                float v = j / (float)(dr - 1);
                for (int i = 0; i < dc; i++)
                {
                    int k = j * dc + i;
                    vertices[k] = centre;
                    normals[k] = Vector3.back;
                    tangents[k] = new Vector4(1f, 0f, 0f, 1f);
                    uv[k] = new Vector2(i / (float)(dc - 1), v);
                }
            }

            // FlagMesh's winding: the front is along the fly x down the hoist.
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

            mesh = new Mesh { name = "RealFlag loop grid" };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.bounds = Bounds;
            mesh.UploadMeshData(true);
            _meshes[cells] = mesh;
            return mesh;
        }

        /// <summary>
        /// The loop at <paramref name="clock"/> seconds for the flag of <paramref name="prop"/> into
        /// its property block, before its draw: the texture, and the two frames it is between
        /// (LoopMotion.FrameAt: each flag begins at its own place in the loop).
        /// </summary>
        public void Bind(MaterialPropertyBlock block, double clock, uint prop)
        {
            int a, b;
            float blend;
            LoopMotion.FrameAt(clock, prop, Frames, FrameTime, out a, out b, out blend);
            block.SetFloat(FlagMesh.IdUseGrid, 2f);
            block.SetTexture(FlagMesh.IdGrid, _texture);
            block.SetVector(FlagMesh.IdGridSize, _gridSize);
            block.SetVector(FlagMesh.IdGridAt, new Vector4((a % TilesX) * Columns, (a / TilesX) * Rows, (b % TilesX) * Columns, (b / TilesX) * Rows));
            block.SetFloat(FlagMesh.IdGridBlend, blend);
        }

        public void Destroy()
        {
            foreach (KeyValuePair<int, Mesh> kv in _meshes)
                FlagMesh.Release(kv.Value);
            _meshes.Clear();
            FlagMesh.Release(_texture);
        }
    }
}
