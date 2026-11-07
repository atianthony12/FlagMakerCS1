using System;
using System.Collections.Generic;
using ColossalFramework;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// Buildings' DRAWN shapes for the cloth to keep off (Settings.DetailedBuildings), in place of
    /// the LOD meshes the game picks a building by (the author, 2026-11-07, a banner hung down a
    /// facade whose cornice, frames and corner columns stand proud of its 28-triangle LOD box: "a
    /// lot of assets don't have proper collision"). Engine, MAIN THREAD (Mesh.vertices is the
    /// engine's), called by FlagSurroundings.
    /// </summary>
    /// <remarks>
    /// What is raised, as the game draws it (their IL): BuildingAI.RenderMeshes draws the info's
    /// m_mesh at the instance's place and turn (Building.CalculateMeshPosition: the place less the
    /// local mesh offset along z, turned by m_angle about DOWN -- the frame Building.RayCast takes
    /// the LOD mesh in), then each of m_subMeshes whose m_flagsRequired / m_flagsForbidden (and the
    /// 2s) the building's flags pass, by its m_matrix; a sub-mesh whose BuildingInfoSub has
    /// sub-meshes of its own draws those that pass instead, by the outer m_matrix. One that follows
    /// the terrain (m_followTerrain, bent by the shader) is left out. BuildingAI.RenderProps draws
    /// m_props: each by a Randomizer of the building's id against m_probability -- only the props
    /// that are ALWAYS there (100) are raised, as the random ones cannot be told without replaying
    /// the game's draws -- if the building is as long as m_requiredLength, at m_dataMatrix1 (the
    /// mesh's frame, BuildingAI.RefreshInstance) times m_position, turned by m_radAngle about down;
    /// a prop not of fixed height on the ground under it. Decals, markers and props with no
    /// renderer are left out; trees too (no mesh to stand on).
    /// A mesh is read once (vertices and triangles) and kept until the city unloads; one the
    /// engine keeps unreadable (Mesh.isReadable false) is kept as null: its building takes its LOD
    /// mesh, said once per asset.
    /// </remarks>
    internal static class BuildingShapes
    {
        /// <summary>
        /// A mesh's triangles in its own frame, their box (x, y, z min and max), and -- from
        /// <see cref="IndexFrom"/> triangles -- their index by where they lie (TriangleIndex): a
        /// gathering visits the triangles near the flag only.
        /// </summary>
        private sealed class Shape
        {
            public Vector3[] V;
            public int[] T;
            public Vector3 Min, Max;
            public TriangleIndex Index;
        }

        /// <summary>Meshes of fewer triangles are culled whole, not indexed.</summary>
        private const int IndexFrom = 256;

        /// <summary>The triangles near a gathering's square (the main thread's alone).</summary>
        private static readonly List<int> Near = new List<int>(1024);

        /// <summary>
        /// Meshes are read for the first time for at most this long a frame, milliseconds (at least
        /// one a frame): the run's first gatherings read a city's big assets at once, 30-47 ms in
        /// one frame. One not read yet is PENDING: its building keeps its LOD shape in that
        /// gathering, which says so (Gathered.Pending) and is made again soon.
        /// </summary>
        private const double ReadBudgetMs = 2.0;

        private static int _readFrame = -1;
        private static double _readSpent;

        /// <summary>Not read yet: this frame's reading time is spent.</summary>
        private static readonly Shape Pending = new Shape();

        private static readonly Dictionary<Mesh, Shape> Meshes = new Dictionary<Mesh, Shape>();
        private static readonly HashSet<string> SaidSimple = new HashSet<string>();

        /// <summary>Said once each: assets whose drawn shape could not be read (their LOD used).</summary>
        private const int MaxSaid = 40;

        /// <summary>The meshes read this session, and the triangles they hold (the cost of the memory).</summary>
        public static int MeshesRead { get; private set; }
        public static long TrianglesHeld { get; private set; }

        /// <summary>
        /// One building's drawn shape into the grid: its mesh, its sub-meshes, its props that are
        /// always there. The triangles raised; -1 if its own mesh cannot be read, or is not read
        /// yet (found.Pending counts that): the caller raises its LOD mesh instead.
        /// </summary>
        public static int Raise(SurfaceGrid grid, Vector3 origin, ref Building building, BuildingInfo info,
                                float x0, float z0, float x1, float z1, ref FlagSurroundings.Gathered found)
        {
            Shape main = Of(info.m_mesh);
            if (main == Pending)
            {
                found.Pending++;
                return -1;
            }

            if (main == null)
            {
                Simple(info);
                return -1;
            }

            Compare(info, main);

            // The mesh's frame in the world (Building.CalculateMeshPosition), less the grid's origin.
            float offset = Building.CalculateLocalMeshOffset(info, building.Length);
            Quaternion turn = Quaternion.AngleAxis(building.m_angle * Mathf.Rad2Deg, Vector3.down);
            Vector3 place = building.m_position + turn * new Vector3(0f, 0f, -offset) - origin;
            Matrix4x4 frame = Matrix4x4.TRS(place, turn, Vector3.one);

            int raised = RaisePart(grid, main, frame, x0 - origin.x, z0 - origin.z, x1 - origin.x, z1 - origin.z);

            BuildingInfo.MeshInfo[] subs = info.m_subMeshes;
            if (subs != null)
            {
                for (int i = 0; i < subs.Length; i++)
                {
                    BuildingInfo.MeshInfo sub = subs[i];
                    if (sub == null || sub.m_subInfo == null || sub.m_followTerrain || !Passes(sub, ref building, true))
                        continue;

                    Matrix4x4 at = frame * sub.m_matrix;
                    var nested = sub.m_subInfo as BuildingInfoSub;
                    if (nested != null && nested.m_subMeshes != null && nested.m_subMeshes.Length > 0)
                    {
                        // Its own variants, by the outer matrix, by m_flags alone (RenderMeshes' IL).
                        foreach (BuildingInfo.MeshInfo inner in nested.m_subMeshes)
                        {
                            if (inner != null && inner.m_subInfo != null && Passes(inner, ref building, false))
                                raised += RaiseMesh(grid, inner.m_subInfo.m_mesh, at, x0, z0, x1, z1, origin, ref found);
                        }
                    }
                    else
                    {
                        raised += RaiseMesh(grid, sub.m_subInfo.m_mesh, at, x0, z0, x1, z1, origin, ref found);
                    }
                }
            }

            BuildingInfo.Prop[] props = info.m_props;
            if (props != null)
            {
                int length = building.Length;
                TerrainManager terrain = Singleton<TerrainManager>.instance;
                for (int i = 0; i < props.Length; i++)
                {
                    BuildingInfo.Prop prop = props[i];
                    if (prop == null || prop.m_probability < 100 || length < prop.m_requiredLength)
                        continue;

                    PropInfo pi = prop.m_finalProp;
                    if (pi == null || pi.m_isDecal || pi.m_isMarker || !pi.m_hasRenderer)
                        continue;

                    Shape shape = Of(pi.m_mesh);
                    if (shape == Pending)
                        found.Pending++;
                    if (shape == null || shape == Pending)
                        continue;

                    // Its place in the world: the mesh's frame times its place; off the ground
                    // under it unless it keeps its height.
                    Vector3 at = frame.MultiplyPoint3x4(prop.m_position);
                    if (!prop.m_fixedHeight)
                        at.y = terrain.SampleRawHeightSmooth(at + origin) - origin.y;

                    float scale = pi.m_maxScale > pi.m_minScale ? 0.5f * (pi.m_minScale + pi.m_maxScale) : pi.m_minScale;
                    if (!(scale > 0f))
                        scale = 1f;
                    Quaternion propTurn = Quaternion.AngleAxis((building.m_angle + prop.m_radAngle) * Mathf.Rad2Deg, Vector3.down);
                    int n = RaisePart(grid, shape, Matrix4x4.TRS(at, propTurn, new Vector3(scale, scale, scale)),
                                      x0 - origin.x, z0 - origin.z, x1 - origin.x, z1 - origin.z);
                    if (n > 0)
                    {
                        raised += n;
                        found.BuildingProps++;
                    }
                }
            }

            return raised;
        }

        /// <summary>A sub-mesh drawn for this building's flags (RenderMeshes' test: (required | forbidden) &amp; flags == required).</summary>
        private static bool Passes(BuildingInfo.MeshInfo sub, ref Building building, bool second)
        {
            if (((sub.m_flagsRequired | sub.m_flagsForbidden) & building.m_flags) != sub.m_flagsRequired)
                return false;

            return !second || ((sub.m_flagsRequired2 | sub.m_flagsForbidden2) & building.m_flags2) == sub.m_flagsRequired2;
        }

        private static int RaiseMesh(SurfaceGrid grid, Mesh mesh, Matrix4x4 at, float x0, float z0, float x1, float z1, Vector3 origin,
                                     ref FlagSurroundings.Gathered found)
        {
            Shape shape = Of(mesh);
            if (shape == Pending)
            {
                found.Pending++;
                return 0;
            }

            return shape == null ? 0 : RaisePart(grid, shape, at, x0 - origin.x, z0 - origin.z, x1 - origin.x, z1 - origin.z);
        }

        /// <summary>
        /// A shape's triangles into the grid by <paramref name="at"/> (its frame in the grid's,
        /// turned about the vertical only, scaled evenly): those near the square (x0, z0)..(x1, z1),
        /// found in the shape's own frame, where the square's corners fall.
        /// </summary>
        private static int RaisePart(SurfaceGrid grid, Shape s, Matrix4x4 at, float x0, float z0, float x1, float z1)
        {
            // The square in the shape's frame: an upright turn and an even scale keep x and z apart from y.
            Matrix4x4 back = at.inverse;
            float lx0 = float.MaxValue, lz0 = float.MaxValue, lx1 = float.MinValue, lz1 = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = back.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? x0 : x1, 0f, (i & 2) == 0 ? z0 : z1));
                lx0 = Mathf.Min(lx0, c.x); lx1 = Mathf.Max(lx1, c.x);
                lz0 = Mathf.Min(lz0, c.z); lz1 = Mathf.Max(lz1, c.z);
            }

            if (s.Max.x < lx0 || s.Min.x > lx1 || s.Max.z < lz0 || s.Min.z > lz1)
                return 0;

            int[] t = s.T;
            if (s.Index == null)
            {
                int whole = 0;
                for (int i = 0; i + 2 < t.Length; i += 3)
                    whole += RaiseTriangle(grid, s, i, at, lx0, lz0, lx1, lz1);
                return whole;
            }

            Near.Clear();
            s.Index.Near(s.V, t, lx0, lz0, lx1, lz1, Near);
            int raised = 0;
            for (int n = 0; n < Near.Count; n++)
                raised += RaiseTriangle(grid, s, Near[n], at, lx0, lz0, lx1, lz1);
            return raised;
        }

        /// <summary>The triangle from T[i] into the grid by <paramref name="at"/>, if it reaches the square (its frame's): 1, else 0.</summary>
        private static int RaiseTriangle(SurfaceGrid grid, Shape s, int i, Matrix4x4 at, float lx0, float lz0, float lx1, float lz1)
        {
            Vector3[] v = s.V;
            int[] t = s.T;
            int ia = t[i], ib = t[i + 1], ic = t[i + 2];
            if ((uint)ia >= (uint)v.Length || (uint)ib >= (uint)v.Length || (uint)ic >= (uint)v.Length)
                return 0;

            Vector3 va = v[ia], vb = v[ib], vc = v[ic];
            if ((va.x < lx0 && vb.x < lx0 && vc.x < lx0) || (va.x > lx1 && vb.x > lx1 && vc.x > lx1) ||
                (va.z < lz0 && vb.z < lz0 && vc.z < lz0) || (va.z > lz1 && vb.z > lz1 && vc.z > lz1))
                return 0;

            Vector3 a = at.MultiplyPoint3x4(va), b = at.MultiplyPoint3x4(vb), c = at.MultiplyPoint3x4(vc);
            grid.Raise(a.x, a.y, a.z, b.x, b.y, b.z, c.x, c.y, c.z);
            return 1;
        }


        /// <summary>
        /// A mesh's triangles, read once (and indexed); null if it has none or the engine keeps it
        /// unreadable; <see cref="Pending"/> if this frame's reading time is spent
        /// (<see cref="ReadBudgetMs"/>: a mesh is read when the frame has some left, at least one a
        /// frame).
        /// </summary>
        private static Shape Of(Mesh mesh)
        {
            if (mesh == null)
                return null;

            Shape s;
            if (Meshes.TryGetValue(mesh, out s))
                return s;

            int frame = Time.frameCount;
            if (frame != _readFrame)
            {
                _readFrame = frame;
                _readSpent = 0.0;
            }
            else if (_readSpent >= ReadBudgetMs)
            {
                PendingReads++;
                return Pending;
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            s = null;
            try
            {
                if (mesh.isReadable)
                {
                    Vector3[] v = mesh.vertices;
                    int[] t = mesh.triangles;
                    if (v != null && t != null && v.Length > 0 && t.Length >= 3)
                    {
                        s = new Shape { V = v, T = t, Min = v[0], Max = v[0] };
                        for (int i = 1; i < v.Length; i++)
                        {
                            s.Min = Vector3.Min(s.Min, v[i]);
                            s.Max = Vector3.Max(s.Max, v[i]);
                        }

                        if (t.Length / 3 >= IndexFrom)
                            s.Index = TriangleIndex.Build(v, t, s.Min, s.Max);
                        MeshesRead++;
                        TrianglesHeld += t.Length / 3;
                    }
                }
            }
            catch (Exception)
            {
                s = null;
            }

            Meshes[mesh] = s;
            double ms = watch.Elapsed.TotalMilliseconds;
            _readSpent += ms;
            ReadMilliseconds += ms;
            if (ms > LongestRead)
                LongestRead = ms;
            return s;
        }

        /// <summary>The time spent reading (and indexing) meshes this session, the longest one, and the reads put off to a later frame.</summary>
        public static double ReadMilliseconds { get; private set; }
        public static double LongestRead { get; private set; }
        public static int PendingReads { get; private set; }

        /// <summary>Said once per asset (the detailed log): its drawn shape cannot be read, its LOD mesh is used.</summary>
        private static void Simple(BuildingInfo info)
        {
            string name = info.name ?? "?";
            if (SaidSimple.Count >= MaxSaid || !SaidSimple.Add(name))
                return;

            Log.Detail("flags: building '" + name + "': its drawn shape cannot be read (" +
                       (info.m_mesh == null ? "no mesh" : "the engine keeps it unreadable") + "): the cloth keeps off its simple LOD shape" +
                       (SaidSimple.Count == MaxSaid ? " (further ones are counted, not named)" : ""));
        }

        private static readonly HashSet<string> Compared = new HashSet<string>();
        private const int MaxCompared = 12;

        /// <summary>
        /// The first assets read (the detailed log): the drawn mesh's box against its LOD mesh's in
        /// the same frame -- near alike if the drawn one is placed where the game draws it (the
        /// placement is the engine's maths, not run offline).
        /// </summary>
        private static void Compare(BuildingInfo info, Shape main)
        {
            if (!Log.Detailed || Compared.Count >= MaxCompared)
                return;

            string name = info.name ?? "?";
            if (!Compared.Add(name))
                return;

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string lod = "none";
            RenderGroup.MeshData data = info.m_lodMeshData;
            if (data != null && data.m_vertices != null && data.m_vertices.Length > 0)
            {
                Vector3 lo = data.m_vertices[0], hi = lo;
                foreach (Vector3 v in data.m_vertices)
                {
                    lo = Vector3.Min(lo, v);
                    hi = Vector3.Max(hi, v);
                }

                lod = Box(lo, hi, inv) + " (" + (data.m_triangles != null ? data.m_triangles.Length / 3 : 0) + " triangles)";
            }

            Log.Detail("flags: building '" + name + "' as drawn: " + Box(main.Min, main.Max, inv) + " (" + main.T.Length / 3 + " triangles, " +
                       (info.m_subMeshes != null ? info.m_subMeshes.Length : 0) + " sub-mesh(es), " + (info.m_props != null ? info.m_props.Length : 0) +
                       " prop(s)); its LOD shape: " + lod);
        }

        private static string Box(Vector3 lo, Vector3 hi, IFormatProvider inv)
        {
            return "x " + lo.x.ToString("F1", inv) + ".." + hi.x.ToString("F1", inv) + ", y " + lo.y.ToString("F1", inv) + ".." + hi.y.ToString("F1", inv) +
                   ", z " + lo.z.ToString("F1", inv) + ".." + hi.z.ToString("F1", inv) + " m";
        }

        /// <summary>Forgets every mesh read (the city unloads).</summary>
        public static void Clear()
        {
            Meshes.Clear();
            SaidSimple.Clear();
            Compared.Clear();
            MeshesRead = 0;
            TrianglesHeld = 0;
            ReadMilliseconds = 0.0;
            LongestRead = 0.0;
            PendingReads = 0;
            _readFrame = -1;
        }
    }
}
