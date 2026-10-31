using System;
using System.Collections.Generic;
using System.Diagnostics;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// What stands round a flag, read from the game for its cloth to keep off (FlagCloth.SetSurface,
    /// SetCapsules). On the main thread, reading the managers' buffers as the game's own renderers
    /// do; what it makes (SurfaceGrid, CapsuleSet) is plain data the cloth's thread may read.
    /// </summary>
    /// <remarks>
    /// THE SURFACE: the terrain and the water (TerrainManager.SampleRawHeightSmoothWithWater's
    /// Vector3 overload, as the flag's ground always was) at least every metre, bilinear between
    /// -- the heightmap is 16 m -- raised to the buildings' LOD meshes: the triangles the game picks
    /// a building by (Building.RayCast's IL: the ray taken into the building's frame -- less its
    /// position, turned by -m_angle, CalculateLocalMeshOffset added along z -- and tested against
    /// every triangle of m_lodMeshData), found through the building grid as BuildingManager.RayCast
    /// finds them (cells of 64 m, a building up to 72 m from the cell it stands in), on the
    /// Default layer as the pole's cast asks; collapsed buildings left out (their rubble is
    /// another mesh). Gathered a quarter wider than the flag can reach (FlagOutline), so it moves
    /// a little without a new one. Not yet: roads and bridges (a network is a Bezier strip the
    /// game casts by its own maths, not a mesh).
    /// THE CAPSULES: the props and trees within reach, as their own RayCast picks them (their IL):
    /// scaled by a Randomizer seeded with the object's index, m_minScale + Int32(10000) x
    /// (m_maxScale - m_minScale) x 0.0001; the flags' own props, markers, decals and props with no
    /// renderer left out, and blocked props, and trees not grown.
    /// CHANGES: the buildings' own events (created, moved, released; raised on the simulation
    /// thread) are queued here and mark the surfaces near them stale (FlagRenderer); a surface in
    /// use is gathered again after a while anyway (terrain edits, a building moved away from a
    /// flag: its event has only where it went).
    /// </remarks>
    public static class FlagSurroundings
    {
        /// <summary>Gathered this much wider than the flag can reach: it moves a little without a new one.</summary>
        public const float Slack = 1.25f;

        /// <summary>The terrain is sampled at least this often, metres (its heightmap is 16 m; the grid is bilinear between).</summary>
        private const float TerrainStep = 1f;

        /// <summary>A building is found up to this far from the grid cell it stands in (BuildingManager.RayCast's 72 m, its IL).</summary>
        private const float BuildingReach = 72f;

        /// <summary>The same for props (cells of 64 m) and trees (32 m): their capsule's radius is an eighth of their width and depth.</summary>
        private const float PropReach = 32f;
        private const float TreeReach = 16f;

        /// <summary>What a gathering found, and how long it took.</summary>
        public struct Gathered
        {
            public int Buildings, Triangles, Props, Trees;
            public double Milliseconds;
        }

        /// <summary>Each building's LOD mesh's box in its own frame (x0, z0, x1, z1), read once: a building the flag's square misses is passed over.</summary>
        private static readonly Dictionary<BuildingInfo, Vector4> MeshBoxes = new Dictionary<BuildingInfo, Vector4>();

        /// <summary>Buildings changed (created, moved, released): (x, z, radius), queued by the simulation thread.</summary>
        private static readonly object ChangeLock = new object();
        private static readonly List<Vector3> Changes = new List<Vector3>();
        private static bool _overflow;
        private static BuildingManager _listened;
        private const int MaxChanges = 4096;

        /// <summary>
        /// The surface round the square <paramref name="half"/> metres either side of (centreX,
        /// centreZ), heights relative to <paramref name="origin"/> (the flag's anchor).
        /// </summary>
        public static SurfaceGrid Surface(Vector3 origin, float centreX, float centreZ, float half, ref Gathered found)
        {
            Stopwatch watch = Stopwatch.StartNew();
            float spacing;
            int nodes;
            SurfaceGrid.Layout(half, out spacing, out nodes);
            float span = (nodes - 1) * spacing;
            float x0 = centreX - 0.5f * span, z0 = centreZ - 0.5f * span;

            var grid = new SurfaceGrid(x0 - origin.x, z0 - origin.z, spacing, nodes, nodes, 0f);
            Terrain(grid, origin, x0, z0);
            Buildings(grid, origin, x0, z0, x0 + span, z0 + span, ref found);
            grid.Finish();

            found.Milliseconds += watch.Elapsed.TotalMilliseconds;
            return grid;
        }

        /// <summary>The terrain and water under every node: sampled every <see cref="TerrainStep"/> at least, the rest bilinear.</summary>
        private static void Terrain(SurfaceGrid grid, Vector3 origin, float x0, float z0)
        {
            TerrainManager terrain = Singleton<TerrainManager>.instance;
            int n = grid.Columns;
            float s = grid.Spacing;
            float[] h = grid.Heights;
            int every = Math.Max(1, (int)(TerrainStep / s));

            for (int r = 0; r < n; r = Next(r, every, n))
            {
                for (int c = 0; c < n; c = Next(c, every, n))
                    h[r * n + c] = terrain.SampleRawHeightSmoothWithWater(new Vector3(x0 + c * s, 0f, z0 + r * s), false, 0f) - origin.y;
            }

            if (every == 1)
                return;

            // Along the rows sampled, then down every column.
            for (int r = 0; r < n; r = Next(r, every, n))
            {
                for (int a = 0; a < n - 1;)
                {
                    int b = Next(a, every, n);
                    for (int c = a + 1; c < b; c++)
                        h[r * n + c] = Mathf.Lerp(h[r * n + a], h[r * n + b], (c - a) / (float)(b - a));
                    a = b;
                }
            }

            for (int c = 0; c < n; c++)
            {
                for (int a = 0; a < n - 1;)
                {
                    int b = Next(a, every, n);
                    for (int r = a + 1; r < b; r++)
                        h[r * n + c] = Mathf.Lerp(h[a * n + c], h[b * n + c], (r - a) / (float)(b - a));
                    a = b;
                }
            }
        }

        /// <summary>The next node sampled after <paramref name="i"/>: every <paramref name="every"/>, and the last; past it, <paramref name="n"/>.</summary>
        private static int Next(int i, int every, int n)
        {
            return i >= n - 1 ? n : Math.Min(i + every, n - 1);
        }

        /// <summary>Every building near the square raises the grid to its LOD mesh.</summary>
        private static void Buildings(SurfaceGrid grid, Vector3 origin, float x0, float z0, float x1, float z1, ref Gathered found)
        {
            BuildingManager manager = Singleton<BuildingManager>.instance;
            Building[] buffer = manager.m_buildings.m_buffer;
            ushort[] cells = manager.m_buildingGrid;
            const int res = BuildingManager.BUILDINGGRID_RESOLUTION;
            const float size = BuildingManager.BUILDINGGRID_CELL_SIZE;

            int gx0 = Mathf.Max((int)((x0 - BuildingReach) / size + res * 0.5f), 0);
            int gz0 = Mathf.Max((int)((z0 - BuildingReach) / size + res * 0.5f), 0);
            int gx1 = Mathf.Min((int)((x1 + BuildingReach) / size + res * 0.5f), res - 1);
            int gz1 = Mathf.Min((int)((z1 + BuildingReach) / size + res * 0.5f), res - 1);

            for (int gz = gz0; gz <= gz1; gz++)
            {
                for (int gx = gx0; gx <= gx1; gx++)
                {
                    ushort id = cells[gz * res + gx];
                    int guard = 0;
                    while (id != 0)
                    {
                        int triangles = Raise(grid, origin, ref buffer[id], x0, z0, x1, z1);
                        if (triangles > 0)
                        {
                            found.Buildings++;
                            found.Triangles += triangles;
                        }

                        id = buffer[id].m_nextGridBuilding;
                        if (++guard >= buffer.Length)
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// One building's LOD mesh into the grid, as Building.RayCast places it: a vertex v of the
        /// mesh is at m_position + turned by m_angle (v.x, v.z - offset), v.y up. The triangles
        /// raised (0: none reach the square, or it is not one the pole's cast would meet).
        /// </summary>
        private static int Raise(SurfaceGrid grid, Vector3 origin, ref Building building, float x0, float z0, float x1, float z1)
        {
            const Building.Flags wanted = Building.Flags.Created;
            const Building.Flags mask = Building.Flags.Created | Building.Flags.Deleted | Building.Flags.Hidden | Building.Flags.Collapsed;
            if ((building.m_flags & mask) != wanted)
                return 0;

            BuildingInfo info = building.Info;
            if (info == null || info.m_class == null || (info.m_class.m_layer & ItemClass.Layer.Default) == 0)
                return 0;

            RenderGroup.MeshData data = info.m_lodMeshData;
            if (data == null || data.m_vertices == null || data.m_triangles == null)
                return 0;

            Vector3[] v = data.m_vertices;
            int[] t = data.m_triangles;
            float a = building.m_angle, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            float offset = Building.CalculateLocalMeshOffset(info, building.Length);
            Vector3 p = building.m_position;

            // The square in the mesh's own frame (its corners turned by -angle, the offset added along z).
            float lx0 = float.MaxValue, lz0 = float.MaxValue, lx1 = float.MinValue, lz1 = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                float dx = ((i & 1) == 0 ? x0 : x1) - p.x, dz = ((i & 2) == 0 ? z0 : z1) - p.z;
                float lx = cos * dx + sin * dz, lz = cos * dz - sin * dx + offset;
                lx0 = Mathf.Min(lx0, lx); lx1 = Mathf.Max(lx1, lx);
                lz0 = Mathf.Min(lz0, lz); lz1 = Mathf.Max(lz1, lz);
            }

            Vector4 box = MeshBox(info, v);
            if (box.z < lx0 || box.x > lx1 || box.w < lz0 || box.y > lz1)
                return 0;

            float gx = p.x - origin.x, gy = p.y - origin.y, gz = p.z - origin.z;
            int raised = 0;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                int ia = t[i], ib = t[i + 1], ic = t[i + 2];
                if (ia >= v.Length || ib >= v.Length || ic >= v.Length)
                    continue;

                Vector3 va = v[ia], vb = v[ib], vc = v[ic];
                if ((va.x < lx0 && vb.x < lx0 && vc.x < lx0) || (va.x > lx1 && vb.x > lx1 && vc.x > lx1) ||
                    (va.z < lz0 && vb.z < lz0 && vc.z < lz0) || (va.z > lz1 && vb.z > lz1 && vc.z > lz1))
                    continue;

                float az = va.z - offset, bz = vb.z - offset, cz = vc.z - offset;
                grid.Raise(gx + cos * va.x - sin * az, gy + va.y, gz + sin * va.x + cos * az,
                           gx + cos * vb.x - sin * bz, gy + vb.y, gz + sin * vb.x + cos * bz,
                           gx + cos * vc.x - sin * cz, gy + vc.y, gz + sin * vc.x + cos * cz);
                raised++;
            }

            return raised;
        }

        /// <summary>
        /// The buildings standing within CityRoughness.Reach of (x, z), for the wind's terrain: each
        /// by its prefab's size (BuildingInfo.m_size, which InitializePrefab takes from its generated
        /// mesh info: width, height, length), found through the building grid as
        /// <see cref="Buildings"/> finds them, the same ones counted (Created, not Deleted, Hidden or
        /// Collapsed, on the Default layer).
        /// </summary>
        public static CityRoughness Roughness(float x, float z, ref Gathered found)
        {
            Stopwatch watch = Stopwatch.StartNew();
            var city = new CityRoughness();
            BuildingManager manager = Singleton<BuildingManager>.instance;
            Building[] buffer = manager.m_buildings.m_buffer;
            ushort[] cells = manager.m_buildingGrid;
            const int res = BuildingManager.BUILDINGGRID_RESOLUTION;
            const float size = BuildingManager.BUILDINGGRID_CELL_SIZE;
            const Building.Flags wanted = Building.Flags.Created;
            const Building.Flags mask = Building.Flags.Created | Building.Flags.Deleted | Building.Flags.Hidden | Building.Flags.Collapsed;
            const float reach = CityRoughness.Reach;

            int gx0 = Mathf.Max((int)((x - reach) / size + res * 0.5f), 0);
            int gz0 = Mathf.Max((int)((z - reach) / size + res * 0.5f), 0);
            int gx1 = Mathf.Min((int)((x + reach) / size + res * 0.5f), res - 1);
            int gz1 = Mathf.Min((int)((z + reach) / size + res * 0.5f), res - 1);

            for (int gz = gz0; gz <= gz1; gz++)
            {
                for (int gx = gx0; gx <= gx1; gx++)
                {
                    ushort id = cells[gz * res + gx];
                    int guard = 0;
                    while (id != 0)
                    {
                        ref Building b = ref buffer[id];
                        float dx = b.m_position.x - x, dz = b.m_position.z - z;
                        if ((b.m_flags & mask) == wanted && dx * dx + dz * dz <= reach * reach)
                        {
                            BuildingInfo info = b.Info;
                            if (info != null && info.m_class != null && (info.m_class.m_layer & ItemClass.Layer.Default) != 0)
                                city.Add(info.m_size.x, info.m_size.z, info.m_size.y);
                        }

                        id = b.m_nextGridBuilding;
                        if (++guard >= buffer.Length)
                            break;
                    }
                }
            }

            found.Buildings += city.Buildings;
            found.Milliseconds += watch.Elapsed.TotalMilliseconds;
            return city;
        }

        /// <summary>A building's LOD mesh's box in its own frame (x0, z0, x1, z1), read from its vertices once.</summary>
        private static Vector4 MeshBox(BuildingInfo info, Vector3[] v)
        {
            Vector4 box;
            if (MeshBoxes.TryGetValue(info, out box))
                return box;

            box = new Vector4(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
            foreach (Vector3 p in v)
            {
                box.x = Mathf.Min(box.x, p.x); box.y = Mathf.Min(box.y, p.z);
                box.z = Mathf.Max(box.z, p.x); box.w = Mathf.Max(box.w, p.z);
            }

            MeshBoxes[info] = box;
            return box;
        }

        /// <summary>
        /// The props' and trees' capsules reaching the square <paramref name="half"/> metres either
        /// side of (centreX, centreZ), relative to <paramref name="origin"/>.
        /// </summary>
        public static CapsuleSet Capsules(Vector3 origin, float centreX, float centreZ, float half, ref Gathered found)
        {
            Stopwatch watch = Stopwatch.StartNew();
            var set = new CapsuleSet();
            float x0 = centreX - half, z0 = centreZ - half, x1 = centreX + half, z1 = centreZ + half;
            Props(set, origin, x0, z0, x1, z1, ref found);
            Trees(set, origin, x0, z0, x1, z1, ref found);
            found.Milliseconds += watch.Elapsed.TotalMilliseconds;
            return set;
        }

        private static void Props(CapsuleSet set, Vector3 origin, float x0, float z0, float x1, float z1, ref Gathered found)
        {
            PropManager manager = Singleton<PropManager>.instance;
            PropInstance[] buffer = manager.m_props.m_buffer;
            ushort[] cells = manager.m_propGrid;
            const int res = PropManager.PROPGRID_RESOLUTION;
            const float size = PropManager.PROPGRID_CELL_SIZE;
            PropInfo flag = FlagPrefab.Info;
            const ushort mask = (ushort)(PropInstance.Flags.Created | PropInstance.Flags.Deleted | PropInstance.Flags.Hidden | PropInstance.Flags.Blocked);
            const ushort wanted = (ushort)PropInstance.Flags.Created;

            int gx0 = Mathf.Max((int)((x0 - PropReach) / size + res * 0.5f), 0);
            int gz0 = Mathf.Max((int)((z0 - PropReach) / size + res * 0.5f), 0);
            int gx1 = Mathf.Min((int)((x1 + PropReach) / size + res * 0.5f), res - 1);
            int gz1 = Mathf.Min((int)((z1 + PropReach) / size + res * 0.5f), res - 1);

            for (int gz = gz0; gz <= gz1; gz++)
            {
                for (int gx = gx0; gx <= gx1; gx++)
                {
                    ushort id = cells[gz * res + gx];
                    int guard = 0;
                    while (id != 0)
                    {
                        if ((buffer[id].m_flags & mask) == wanted)
                        {
                            PropInfo info = buffer[id].Info;
                            if (info != null && info != flag && info.m_hasRenderer && !info.m_isMarker && !info.m_isDecal && info.m_generatedInfo != null)
                            {
                                var random = new Randomizer(id);
                                float scale = info.m_minScale + random.Int32(10000u) * (info.m_maxScale - info.m_minScale) * 0.0001f;
                                if (Add(set, origin, buffer[id].Position, info.m_generatedInfo.m_size * scale, x0, z0, x1, z1))
                                    found.Props++;
                            }
                        }

                        id = buffer[id].m_nextGridProp;
                        if (++guard >= buffer.Length)
                            break;
                    }
                }
            }
        }

        private static void Trees(CapsuleSet set, Vector3 origin, float x0, float z0, float x1, float z1, ref Gathered found)
        {
            TreeManager manager = Singleton<TreeManager>.instance;
            TreeInstance[] buffer = manager.m_trees.m_buffer;
            uint[] cells = manager.m_treeGrid;
            const int res = TreeManager.TREEGRID_RESOLUTION;
            const float size = TreeManager.TREEGRID_CELL_SIZE;
            const TreeInstance.Flags mask = TreeInstance.Flags.Created | TreeInstance.Flags.Deleted | TreeInstance.Flags.Hidden;

            int gx0 = Mathf.Max((int)((x0 - TreeReach) / size + res * 0.5f), 0);
            int gz0 = Mathf.Max((int)((z0 - TreeReach) / size + res * 0.5f), 0);
            int gx1 = Mathf.Min((int)((x1 + TreeReach) / size + res * 0.5f), res - 1);
            int gz1 = Mathf.Min((int)((z1 + TreeReach) / size + res * 0.5f), res - 1);

            for (int gz = gz0; gz <= gz1; gz++)
            {
                for (int gx = gx0; gx <= gx1; gx++)
                {
                    uint id = cells[gz * res + gx];
                    int guard = 0;
                    while (id != 0)
                    {
                        if (((TreeInstance.Flags)buffer[id].m_flags & mask) == TreeInstance.Flags.Created && buffer[id].GrowState != 0)
                        {
                            TreeInfo info = buffer[id].Info;
                            if (info != null && info.m_generatedInfo != null)
                            {
                                var random = new Randomizer(id);
                                float scale = info.m_minScale + random.Int32(10000u) * (info.m_maxScale - info.m_minScale) * 0.0001f;
                                if (Add(set, origin, buffer[id].Position, info.m_generatedInfo.m_size * scale, x0, z0, x1, z1))
                                    found.Trees++;
                            }
                        }

                        id = buffer[id].m_nextGridTree;
                        if (++guard >= buffer.Length)
                            break;
                    }
                }
            }
        }

        /// <summary>An object of this size (as scaled) at this place, if its capsule reaches the square.</summary>
        private static bool Add(CapsuleSet set, Vector3 origin, Vector3 at, Vector3 size, float x0, float z0, float x1, float z1)
        {
            float radius = (size.x + size.z) * 0.125f;

            // Each test is an ACCEPT, which a NaN fails (every comparison with NaN is false): an
            // object whose size or place is not a number, or infinite, would otherwise turn every
            // cloth near it to NaN at each step.
            bool reaches = radius > 0f && radius < 1e4f && size.y > 0f && size.y < 1e4f && at.y > -1e5f && at.y < 1e5f &&
                           at.x + radius >= x0 && at.x - radius <= x1 && at.z + radius >= z0 && at.z - radius <= z1;
            if (!reaches)
                return false;

            set.AddObject(at.x - origin.x, at.y - origin.y, at.z - origin.z, size.x, size.z, size.y);
            return true;
        }

        // ---- changes ------------------------------------------------------------------------------

        /// <summary>Listens to the buildings' events (the renderer's life).</summary>
        public static void Listen()
        {
            if (_listened != null)
                return;

            try
            {
                BuildingManager manager = Singleton<BuildingManager>.instance;
                manager.EventBuildingCreated += OnBuilding;
                manager.EventBuildingRelocated += OnBuilding;
                manager.EventBuildingReleased += OnBuilding;
                _listened = manager;
            }
            catch (Exception e)
            {
                Log.Warn("flags: could not listen to the buildings' changes (the surfaces are gathered again every so often instead): " + e.Message);
            }
        }

        /// <summary>Stops listening, and forgets what it had (the city unloads).</summary>
        public static void Stop()
        {
            if (_listened != null)
            {
                try
                {
                    _listened.EventBuildingCreated -= OnBuilding;
                    _listened.EventBuildingRelocated -= OnBuilding;
                    _listened.EventBuildingReleased -= OnBuilding;
                }
                catch (Exception)
                {
                    // The manager is going with the city.
                }

                _listened = null;
            }

            lock (ChangeLock)
            {
                Changes.Clear();
                _overflow = false;
            }

            MeshBoxes.Clear();
        }

        /// <summary>On the simulation thread: where a building was made, moved to or let go, and how far it reaches.</summary>
        private static void OnBuilding(ushort id)
        {
            try
            {
                Building[] buffer = Singleton<BuildingManager>.instance.m_buildings.m_buffer;
                if (id == 0 || id >= buffer.Length)
                    return;

                Vector3 p = buffer[id].m_position;
                float reach = Mathf.Max(buffer[id].Width, buffer[id].Length) * 4f * 1.415f + 16f;
                lock (ChangeLock)
                {
                    if (Changes.Count < MaxChanges)
                        Changes.Add(new Vector3(p.x, p.z, reach));
                    else
                        _overflow = true;
                }
            }
            catch (Exception)
            {
                // Never into the game's own code.
            }
        }

        /// <summary>The buildings changed since the last call, as (x, z, radius), into <paramref name="into"/>; true if more changed than were kept (then all is stale).</summary>
        public static bool TakeChanges(List<Vector3> into)
        {
            lock (ChangeLock)
            {
                into.AddRange(Changes);
                Changes.Clear();
                bool overflow = _overflow;
                _overflow = false;
                return overflow;
            }
        }
    }
}
