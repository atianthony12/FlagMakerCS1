using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// A mesh's triangles by where they lie, level (BuildingShapes: a gathering visits the
    /// triangles near the flag only, not every one of a building whose box reaches it -- the
    /// 2026-11-07 run: 1-7 ms a gathering, most of it that cull; the LOD shapes 0.3-2.5 ms). PURE
    /// (Vector3 and Mathf's managed maths only): test-cloth checks it against every triangle.
    /// A grid of Cols x Rows cells of Cell metres from (OX, OZ), the mesh's longer side over
    /// <see cref="Cells"/> (at least <see cref="MinCell"/>); each triangle listed in every cell its
    /// box touches (CellTris from CellStart[cell] to CellStart[cell + 1]). A triangle whose
    /// indices fall outside the vertices is left out.
    /// </summary>
    public sealed class TriangleIndex
    {
        public const int Cells = 32;
        public const float MinCell = 1f;

        public float OX, OZ, Cell;
        public int Cols, Rows;
        public int[] CellStart, CellTris;

        public static TriangleIndex Build(Vector3[] v, int[] t, Vector3 min, Vector3 max)
        {
            var x = new TriangleIndex();
            float sizeX = max.x - min.x, sizeZ = max.z - min.z;
            float cell = Mathf.Max(MinCell, Mathf.Max(sizeX, sizeZ) / Cells);
            int cols = Mathf.Max(1, Mathf.CeilToInt(sizeX / cell + 1e-4f)), rows = Mathf.Max(1, Mathf.CeilToInt(sizeZ / cell + 1e-4f));
            x.OX = min.x;
            x.OZ = min.z;
            x.Cell = cell;
            x.Cols = cols;
            x.Rows = rows;

            int[] start = new int[cols * rows + 1];
            int[] fill = null;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    int ia = t[i], ib = t[i + 1], ic = t[i + 2];
                    if ((uint)ia >= (uint)v.Length || (uint)ib >= (uint)v.Length || (uint)ic >= (uint)v.Length)
                        continue;

                    Vector3 va = v[ia], vb = v[ib], vc = v[ic];
                    int cA = x.ColumnOf(Mathf.Min(va.x, Mathf.Min(vb.x, vc.x))), cB = x.ColumnOf(Mathf.Max(va.x, Mathf.Max(vb.x, vc.x)));
                    int rA = x.RowOf(Mathf.Min(va.z, Mathf.Min(vb.z, vc.z))), rB = x.RowOf(Mathf.Max(va.z, Mathf.Max(vb.z, vc.z)));
                    for (int r = rA; r <= rB; r++)
                    {
                        for (int c = cA; c <= cB; c++)
                        {
                            int k = r * cols + c;
                            if (pass == 0)
                                start[k + 1]++;
                            else
                                x.CellTris[start[k] + fill[k]++] = i;
                        }
                    }
                }

                if (pass == 0)
                {
                    for (int k = 0; k < cols * rows; k++)
                        start[k + 1] += start[k];
                    x.CellTris = new int[start[cols * rows]];
                    fill = new int[cols * rows];
                }
            }

            x.CellStart = start;
            return x;
        }

        public int ColumnOf(float px)
        {
            int i = (int)((px - OX) / Cell);
            return i < 0 ? 0 : i >= Cols ? Cols - 1 : i;
        }

        public int RowOf(float pz)
        {
            int i = (int)((pz - OZ) / Cell);
            return i < 0 ? 0 : i >= Rows ? Rows - 1 : i;
        }

        /// <summary>
        /// Into <paramref name="into"/>, once each: the first index (into t) of every triangle
        /// reaching the rectangle (x0, z0)..(x1, z1) of the mesh's frame -- its box not wholly
        /// to one side of it. A triangle in several cells is taken in the first one that is in
        /// the rectangle (the cell of its box's low corner, held to the rectangle's).
        /// </summary>
        public void Near(Vector3[] v, int[] t, float x0, float z0, float x1, float z1, List<int> into)
        {
            if (x1 < OX || z1 < OZ || x0 > OX + Cols * Cell || z0 > OZ + Rows * Cell)
                return;

            int c0 = ColumnOf(x0), c1 = ColumnOf(x1), r0 = RowOf(z0), r1 = RowOf(z1);
            for (int r = r0; r <= r1; r++)
            {
                for (int c = c0; c <= c1; c++)
                {
                    int cell = r * Cols + c;
                    for (int n = CellStart[cell]; n < CellStart[cell + 1]; n++)
                    {
                        int i = CellTris[n];
                        Vector3 va = v[t[i]], vb = v[t[i + 1]], vc = v[t[i + 2]];
                        int lc = Math.Max(c0, ColumnOf(Mathf.Min(va.x, Mathf.Min(vb.x, vc.x))));
                        int lr = Math.Max(r0, RowOf(Mathf.Min(va.z, Mathf.Min(vb.z, vc.z))));
                        if (lc != c || lr != r || !Reaches(va, vb, vc, x0, z0, x1, z1))
                            continue;

                        into.Add(i);
                    }
                }
            }
        }

        /// <summary>The triangle's box is not wholly to one side of the rectangle.</summary>
        public static bool Reaches(Vector3 va, Vector3 vb, Vector3 vc, float x0, float z0, float x1, float z1)
        {
            return !((va.x < x0 && vb.x < x0 && vc.x < x0) || (va.x > x1 && vb.x > x1 && vc.x > x1) ||
                     (va.z < z0 && vb.z < z0 && vc.z < z0) || (va.z > z1 && vb.z > z1 && vc.z > z1));
        }
    }
}
