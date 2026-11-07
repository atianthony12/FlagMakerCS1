using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// One span's flags as ONE mesh (FlagString.shader): a grid of the cloth's particles per flag,
    /// each flag's design in its vertices. The topology and the designs are made when the span
    /// changes; the places and normals every frame it is drawn, from its flags' shared motions
    /// (StringMotion), turned onto the string. Coordinates relative to the span's first node.
    /// </summary>
    public sealed class StringFlagsMesh
    {
        /// <summary>The most vertices a mesh takes (16-bit indices).</summary>
        public const int MaxVertices = 65000;

        public readonly Mesh Mesh;
        public int Flags { get; private set; }
        public int Columns { get; private set; }
        public int Rows { get; private set; }

        private int _count;
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector4> _uv0 = new List<Vector4>();
        private readonly List<Vector4> _uv1 = new List<Vector4>();
        private readonly List<Vector4> _uv2 = new List<Vector4>();
        private readonly List<Vector4> _uv3 = new List<Vector4>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly List<int> _triangles = new List<int>();

        private float[] _x = new float[0], _y = new float[0], _z = new float[0];

        /// <summary>A source's sample, before it is mirrored and sheared into _x, _y, _z.</summary>
        private float[] _sx = new float[0], _sy = new float[0], _sz = new float[0];

        public StringFlagsMesh()
        {
            Mesh = new Mesh { name = "RealFlag string flags" };
            Mesh.MarkDynamic();
        }

        /// <summary>How many flags of a grid this size one mesh can hold.</summary>
        public static int MostFlags(int columns, int rows)
        {
            return Math.Max(0, MaxVertices / Math.Max(1, columns * rows));
        }

        /// <summary>A new topology: <paramref name="flags"/> grids of columns x rows (fly x hoist).</summary>
        public void Shape(int flags, int columns, int rows)
        {
            flags = Math.Min(flags, MostFlags(columns, rows));
            Flags = flags;
            Columns = columns;
            Rows = rows;
            _count = columns * rows;
            int n = flags * _count;
            if (_x.Length < _count)
            {
                _x = new float[_count];
                _y = new float[_count];
                _z = new float[_count];
            }

            Resize(_vertices, n, Vector3.zero);
            Resize(_normals, n, Vector3.up);
            Resize(_uv0, n, Vector4.zero);
            Resize(_uv1, n, Vector4.zero);
            Resize(_uv2, n, Vector4.zero);
            Resize(_uv3, n, Vector4.zero);
            Resize(_colors, n, new Color32(255, 0, 0, 0));

            _triangles.Clear();
            for (int f = 0; f < flags; f++)
            {
                int o = f * _count;
                for (int j = 0; j + 1 < rows; j++)
                {
                    for (int i = 0; i + 1 < columns; i++)
                    {
                        int a = o + j * columns + i, b = a + 1, c = a + columns, d = c + 1;
                        _triangles.Add(a); _triangles.Add(c); _triangles.Add(b);
                        _triangles.Add(b); _triangles.Add(c); _triangles.Add(d);
                    }
                }
            }

            Mesh.Clear();
            Mesh.SetVertices(_vertices);
            Mesh.SetNormals(_normals);
            Mesh.SetTriangles(_triangles, 0, false);
        }

        private static void Resize<T>(List<T> list, int n, T fill)
        {
            if (list.Count > n)
                list.RemoveRange(n, list.Count - n);
            while (list.Count < n)
                list.Add(fill);
        }

        /// <summary>Flag <paramref name="f"/>'s design into its vertices: its colours (linear), pattern, cell, brightness, see-through parts, the shape.</summary>
        public void Design(int f, StringSpec spec, Vector4 c1, Vector4 c2, Vector4 c3, int pattern, int cell, float brightness, bool through)
        {
            if (f >= Flags)
                return;

            float hang = spec.Hang, along = spec.Along;
            bool top = spec.Mount == StringMount.Top;
            byte bright = (byte)Mathf.Clamp(Mathf.RoundToInt(brightness * 255f), 0, 255);
            byte shape = (byte)Mathf.Clamp(Mathf.RoundToInt(spec.Shape / 8f * 255f), 0, 255);
            byte depth = (byte)Mathf.Clamp(Mathf.RoundToInt(spec.Depth * 255f), 0, 255);
            var look = new Color32(bright, (byte)(through ? 255 : 0), shape, depth);
            float code = pattern + 16f * cell;

            int o = f * _count;
            for (int j = 0; j < Rows; j++)
            {
                float t = Rows > 1 ? j / (float)(Rows - 1) : 0f;
                for (int i = 0; i < Columns; i++)
                {
                    float s = Columns > 1 ? i / (float)(Columns - 1) : 0f;
                    int k = o + j * Columns + i;
                    // The design: hung by its hoist it is the cloth turned (x along the fly), by its top upright (x along the string).
                    _uv0[k] = top ? new Vector4(s, t, t, s) : new Vector4(s, t, s, t);
                    _uv1[k] = new Vector4(c1.x, c1.y, c1.z, code);
                    _uv2[k] = new Vector4(c2.x, c2.y, c2.z, top ? -hang : hang);
                    _uv3[k] = new Vector4(c3.x, c3.y, c3.z, along);
                    _colors[k] = look;
                }
            }
        }

        /// <summary>The designs to the GPU, after Design for every flag.</summary>
        public void ApplyDesigns()
        {
            Mesh.SetUVs(0, _uv0);
            Mesh.SetUVs(1, _uv1);
            Mesh.SetUVs(2, _uv2);
            Mesh.SetUVs(3, _uv3);
            Mesh.SetColors(_colors);
        }

        /// <summary>
        /// Flag <paramref name="f"/> where it hangs: its source's places at <paramref name="time"/>
        /// (the source's frame: a level hoist along X, the fly down, Z square), z times
        /// <paramref name="zScale"/> (the span's side, StringMotion.OnString.ZScale: -1 the wind from
        /// the other side, between while the flags swing round), SHEARED to its string's slope
        /// (<paramref name="tanSlope"/>: the hoist follows the string, the fly stays plumb, as cloth
        /// hangs), set in the frame (ex, ey, ez) at <paramref name="anchor"/> (relative to the mesh's origin).
        /// </summary>
        public bool Pose(int f, StringMotion.Source source, float time, float tanSlope, float zScale, Vector3 anchor, Vector3 ex, Vector3 ey, Vector3 ez)
        {
            if (f >= Flags || source == null || source.Cloth.Count != _count)
                return false;
            if (_sx.Length < _count)
            {
                _sx = new float[_count];
                _sy = new float[_count];
                _sz = new float[_count];
            }

            if (!source.Record.Sample(time, _sx, _sy, _sz))
                return false;

            int cols = Columns, rows = Rows;
            int o = f * _count;
            for (int j = 0; j < rows; j++)
            {
                int from = j * cols, to = j * cols;
                for (int i = 0; i < cols; i++)
                {
                    float x = _sx[from + i];
                    float y = _sy[from + i] + x * tanSlope, z = _sz[from + i] * zScale;
                    int k = to + i;
                    _x[k] = x;
                    _y[k] = y;
                    _z[k] = z;
                    _vertices[o + k] = anchor + ex * x + ey * y + ez * z;
                }
            }

            // Normals from the grid (central differences; one-sided at the edges), in the same frame.
            for (int j = 0; j < rows; j++)
            {
                int j0 = Math.Max(0, j - 1), j1 = Math.Min(rows - 1, j + 1);
                for (int i = 0; i < cols; i++)
                {
                    int i0 = Math.Max(0, i - 1), i1 = Math.Min(cols - 1, i + 1);
                    int a = j * cols + i0, b = j * cols + i1, c = j0 * cols + i, d = j1 * cols + i;
                    float ux = _x[b] - _x[a], uy = _y[b] - _y[a], uz = _z[b] - _z[a];
                    float vx = _x[d] - _x[c], vy = _y[d] - _y[c], vz = _z[d] - _z[c];
                    float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                    float len = Mathf.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (len < 1e-9f)
                    {
                        nx = 0f; ny = 0f; nz = 1f; len = 1f;
                    }

                    _normals[o + j * cols + i] = (ex * nx + ey * ny + ez * nz) / len;
                }
            }

            return true;
        }

        /// <summary>The places and normals to the GPU, with the bounds given (relative to the origin).</summary>
        public void ApplyPose(Bounds bounds)
        {
            Mesh.SetVertices(_vertices);
            Mesh.SetNormals(_normals);
            Mesh.bounds = bounds;
        }

        public void Release()
        {
            FlagMesh.Release(Mesh);
        }
    }

    /// <summary>
    /// The string itself: a tube along the span (FlagPole.shader's rope: its vertices ON the axis
    /// with the radial direction as their normal, put out by the radius or half a pixel, whichever
    /// is more -- a thin cord stays a steady line far off), or a tape's flat band hanging in the
    /// span's plane (two faces). Coordinates relative to the span's first node.
    /// </summary>
    public sealed class StringCordMesh
    {
        public const int Sides = 6;

        public readonly Mesh Mesh;
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uv = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();
        private int _points = -1, _kind = -1;

        public StringCordMesh()
        {
            Mesh = new Mesh { name = "RealFlag string" };
            Mesh.MarkDynamic();
        }

        /// <summary>
        /// The cord through <paramref name="points"/> (relative to the origin), each with the
        /// direction it hangs (<paramref name="downs"/>, unit, square to the string), of this kind,
        /// thickness (a tape's width) and part (FlagPole.shader: 0 painted, 2 rope).
        /// </summary>
        public void Build(List<Vector3> points, List<Vector3> downs, int kind, float thickness, Bounds bounds)
        {
            int n = points.Count;
            if (n < 2)
                return;

            bool tape = kind == StringKind.Tape;
            int ring = tape ? 4 : Sides;
            bool topology = n != _points || kind != _kind;
            _points = n;
            _kind = kind;

            _vertices.Clear();
            _normals.Clear();
            _uv.Clear();
            float part = kind == StringKind.Wire ? 0f : 2f;
            for (int p = 0; p < n; p++)
            {
                Vector3 t = points[Math.Min(n - 1, p + 1)] - points[Math.Max(0, p - 1)];
                if (t.sqrMagnitude < 1e-10f)
                    t = Vector3.right;
                t.Normalize();
                Vector3 down = downs[p];
                Vector3 side = Vector3.Cross(t, down);
                if (side.sqrMagnitude < 1e-10f)
                    side = Vector3.Cross(t, Vector3.up);
                side.Normalize();
                down = Vector3.Cross(side, t).normalized;

                if (tape)
                {
                    // A band from the string down by its width, both faces: put out by half a millimetre each way.
                    Vector3 lower = points[p] + down * thickness;
                    _vertices.Add(points[p]); _normals.Add(side); _uv.Add(new Vector2(part, 0.0005f));
                    _vertices.Add(lower); _normals.Add(side); _uv.Add(new Vector2(part, 0.0005f));
                    _vertices.Add(points[p]); _normals.Add(-side); _uv.Add(new Vector2(part, 0.0005f));
                    _vertices.Add(lower); _normals.Add(-side); _uv.Add(new Vector2(part, 0.0005f));
                }
                else
                {
                    float radius = 0.5f * thickness;
                    for (int s = 0; s < Sides; s++)
                    {
                        float a = s * 2f * Mathf.PI / Sides;
                        _vertices.Add(points[p]);
                        _normals.Add(side * Mathf.Cos(a) + down * Mathf.Sin(a));
                        _uv.Add(new Vector2(part, radius));
                    }
                }
            }

            if (topology)
            {
                _triangles.Clear();
                for (int p = 0; p + 1 < n; p++)
                {
                    int o = p * ring, q = o + ring;
                    if (tape)
                    {
                        // Face one (0, 1), face two (2, 3) wound the other way.
                        _triangles.Add(o); _triangles.Add(q); _triangles.Add(o + 1);
                        _triangles.Add(o + 1); _triangles.Add(q); _triangles.Add(q + 1);
                        _triangles.Add(o + 2); _triangles.Add(o + 3); _triangles.Add(q + 2);
                        _triangles.Add(o + 3); _triangles.Add(q + 3); _triangles.Add(q + 2);
                        continue;
                    }

                    // Wound to face out (the shader culls back faces): the other way showed the tube's inside, dark and thin (the preview, 2026-11-07).
                    for (int s = 0; s < Sides; s++)
                    {
                        int s1 = (s + 1) % Sides;
                        _triangles.Add(o + s); _triangles.Add(q + s); _triangles.Add(o + s1);
                        _triangles.Add(o + s1); _triangles.Add(q + s); _triangles.Add(q + s1);
                    }
                }

                Mesh.Clear();
                Mesh.SetVertices(_vertices);
                Mesh.SetNormals(_normals);
                Mesh.SetUVs(0, _uv);
                Mesh.SetTriangles(_triangles, 0, false);
            }
            else
            {
                Mesh.SetVertices(_vertices);
                Mesh.SetNormals(_normals);
                Mesh.SetUVs(0, _uv);
            }

            Mesh.bounds = bounds;
        }

        public void Release()
        {
            FlagMesh.Release(Mesh);
        }
    }
}
