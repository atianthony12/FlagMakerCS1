using System;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The handles on the selected flag (the author: "follow ... Procedural Object's way of
    /// allowing the user to rotate/move things on the X, Y, Z axis"): three arrows that move it
    /// along its own axes, a knob that slides it over the level, and three rings that turn it
    /// -- its heading, its tilt, its lean -- all shown together, as Unity's own combined
    /// transform tool does. Owned by FlagTool, which feeds it the mouse and draws it in the
    /// game's overlay stage.
    /// </summary>
    /// <remarks>
    /// The idea of Procedural Objects' 3-axis gizmos and of every 3-D editor's (no code of
    /// either): red, green and blue for X, Y and Z; a handle lights up yellow under the pointer
    /// and while dragged. The axes are the flag's own -- X along its heading (where it points),
    /// Y up, Z across (X x Y) -- so "along" and "sideways" mean the flag's. The rings turn about
    /// those axes: round Y the heading (the prop's angle, as Move It turns it), round Z the tilt
    /// (the pole's top towards the fly), round X the lean (to the side).
    ///
    /// Picking is in screen space: the pointer against each arrow's segment and each ring's
    /// outline, projected, within <see cref="PickPixels"/>. Dragging an arrow follows the point of
    /// its axis nearest the pointer's ray (the closest points of two lines); the knob, where the
    /// ray meets the level through the anchor; a ring, the pointer's travel along the ring's
    /// on-screen tangent where it was grabbed, divided by how far a radian moves that point on
    /// the screen -- a grabbed point follows the pointer, and no view is edge-on to it. Ctrl
    /// snaps (a metre, 15 degrees for the heading, 5 for tilt and lean); Shift is fine (a fifth).
    /// </remarks>
    public sealed class FlagGizmo
    {
        public enum Handle { None, MoveX, MoveY, MoveZ, MovePlane, TurnHeading, TurnTilt, TurnLean }

        /// <summary>A handle is picked within this many pixels of the pointer, at 1080 lines (more on a bigger screen).</summary>
        public const float PickPixels = 11f;

        /// <summary>The gizmo's size against its distance from the camera: constant on the screen.</summary>
        private const float SizePerMetre = 0.11f;

        private const float ArrowFrom = 0.2f, ArrowTo = 1.3f, RingRadius = 0.95f;
        private const int RingSamples = 48;

        private static readonly Color ColourX = new Color(0.95f, 0.22f, 0.2f, 1f);
        private static readonly Color ColourY = new Color(0.35f, 0.88f, 0.3f, 1f);
        private static readonly Color ColourZ = new Color(0.25f, 0.5f, 1f, 1f);
        private static readonly Color ColourKnob = new Color(0.92f, 0.92f, 0.9f, 1f);
        private static readonly Color ColourHot = new Color(1f, 0.84f, 0.2f, 1f);

        private Material _material;
        private Mesh _arrow, _ring, _knob;
        private bool _failed;

        public Handle Hovered { get; private set; }
        public Handle Dragging { get; private set; }

        public bool IsDragging
        {
            get { return Dragging != Handle.None; }
        }

        // The frame drawn: the anchor, the flag's axes, the size.
        private Vector3 _origin, _x, _y, _z;
        private float _size;

        // The flag as the drag found it, and as it now is.
        private Vector3 _startAnchor;
        private float _startAngle, _startTilt, _startLean;
        public Vector3 Anchor { get; private set; }
        public float Angle { get; private set; }
        public float Tilt { get; private set; }
        public float Lean { get; private set; }

        // The drag's own: where it started on the screen and on its axis or plane, and for a
        // ring the screen's travel per radian along its tangent.
        private Vector2 _startMouse;
        private float _startAlong;
        private Vector3 _startHit;
        private Vector2 _tangent;
        private float _perRadian;

        /// <summary>
        /// The flag the gizmo sits on this frame: its anchor, heading and lean -- at rest, the
        /// prop's; during a drag and a moment after, the drag's own (the prop takes the last move
        /// a frame or two later, on the simulation thread: read back at once, the gizmo jumped
        /// back for a frame).
        /// </summary>
        public void Place(Vector3 anchor, float angle, float tilt, float lean, Vector3 eye)
        {
            if (!IsDragging && Time.realtimeSinceStartup >= _holdUntil)
            {
                Anchor = anchor;
                Angle = angle;
                Tilt = tilt;
                Lean = lean;
            }

            _origin = Anchor;
            _x = FlagProps.Direction(Angle);
            _y = Vector3.up;
            _z = Vector3.Cross(_x, _y);
            _size = Mathf.Max(0.2f, Vector3.Distance(eye, _origin) * SizePerMetre);
        }

        // ---- picking ----------------------------------------------------------------------

        /// <summary>The handle under the pointer (None: none within reach); remembered as Hovered.</summary>
        public Handle Pick(Camera camera, Vector2 mouse)
        {
            float reach = PickPixels * Mathf.Max(1f, camera.pixelHeight / 1080f);
            Handle best = Handle.None;
            float bestDistance = reach;

            // The knob first: it sits where every other handle meets.
            Vector2 centre;
            if (Screen(camera, _origin, out centre) && (centre - mouse).magnitude < 1.4f * reach)
            {
                Hovered = Handle.MovePlane;
                return Hovered;
            }

            Consider(camera, mouse, Handle.MoveX, _x, ref best, ref bestDistance);
            Consider(camera, mouse, Handle.MoveY, _y, ref best, ref bestDistance);
            Consider(camera, mouse, Handle.MoveZ, _z, ref best, ref bestDistance);

            // The arrows win within their reach; the rings are thin lines round them.
            if (best == Handle.None)
            {
                ConsiderRing(camera, mouse, Handle.TurnHeading, _y, ref best, ref bestDistance);
                ConsiderRing(camera, mouse, Handle.TurnTilt, _z, ref best, ref bestDistance);
                ConsiderRing(camera, mouse, Handle.TurnLean, _x, ref best, ref bestDistance);
            }

            Hovered = best;
            return best;
        }

        public void ClearHover()
        {
            Hovered = Handle.None;
        }

        private void Consider(Camera camera, Vector2 mouse, Handle handle, Vector3 axis, ref Handle best, ref float bestDistance)
        {
            Vector2 a, b;
            if (!Screen(camera, _origin + axis * (ArrowFrom * _size), out a) || !Screen(camera, _origin + axis * (ArrowTo * _size), out b))
                return;

            float d = DistanceToSegment(mouse, a, b);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = handle;
            }
        }

        private void ConsiderRing(Camera camera, Vector2 mouse, Handle handle, Vector3 axis, ref Handle best, ref float bestDistance)
        {
            Vector3 u, v;
            RingBasis(axis, out u, out v);
            Vector2 previous = Vector2.zero;
            bool havePrevious = false;
            for (int i = 0; i <= RingSamples; i++)
            {
                float t = i * 2f * Mathf.PI / RingSamples;
                Vector3 p = _origin + (u * Mathf.Cos(t) + v * Mathf.Sin(t)) * (RingRadius * _size);
                Vector2 s;
                if (!Screen(camera, p, out s))
                {
                    havePrevious = false;
                    continue;
                }

                if (havePrevious)
                {
                    float d = DistanceToSegment(mouse, previous, s);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = handle;
                    }
                }

                previous = s;
                havePrevious = true;
            }
        }

        // ---- dragging ---------------------------------------------------------------------

        /// <summary>Starts dragging the hovered handle from the pointer's position; false if none is hovered.</summary>
        public bool Begin(Camera camera, Vector2 mouse)
        {
            Handle handle = Hovered;
            if (handle == Handle.None)
                return false;

            _startAnchor = Anchor;
            _startAngle = Angle;
            _startTilt = Tilt;
            _startLean = Lean;
            _startMouse = mouse;

            Ray ray = camera.ScreenPointToRay(new Vector3(mouse.x, mouse.y, 0f));
            switch (handle)
            {
                case Handle.MoveX:
                case Handle.MoveY:
                case Handle.MoveZ:
                    _startAlong = Along(ray, AxisOf(handle));
                    break;

                case Handle.MovePlane:
                    if (!Level(ray, _origin.y, out _startHit))
                        return false;
                    break;

                default:
                {
                    // The ring's point nearest the pointer, the way it runs there, and how far a
                    // radian carries it on the screen.
                    Vector3 axis = AxisOf(handle);
                    Vector3 grab = NearestOnRing(camera, mouse, axis);
                    Vector3 offset = grab - _origin;
                    Vector3 tangent = handle == Handle.TurnHeading ? Vector3.Cross(offset, _y)
                                    : handle == Handle.TurnTilt ? Vector3.Cross(offset, _z)
                                    : Vector3.Cross(_x, offset);
                    Vector2 a, b;
                    const float step = 0.01f;
                    if (!Screen(camera, grab, out a) || !Screen(camera, grab + tangent * step, out b))
                        return false;

                    Vector2 travel = (b - a) / step;
                    _perRadian = travel.magnitude;
                    if (_perRadian < 1e-3f)
                        return false;
                    _tangent = travel / _perRadian;
                    break;
                }
            }

            Dragging = handle;
            return true;
        }

        /// <summary>The drag to where the pointer is now; true if the flag's place or turn changed.</summary>
        public bool Update(Camera camera, Vector2 mouse, bool snap, bool fine)
        {
            if (!IsDragging)
                return false;

            Ray ray = camera.ScreenPointToRay(new Vector3(mouse.x, mouse.y, 0f));
            float scale = fine ? 0.2f : 1f;
            Vector3 anchor = Anchor;
            float angle = Angle, tilt = Tilt, lean = Lean;

            switch (Dragging)
            {
                case Handle.MoveX:
                case Handle.MoveY:
                case Handle.MoveZ:
                {
                    Vector3 axis = AxisOf(Dragging);
                    float along = Along(ray, axis);
                    if (float.IsNaN(along))
                        return false;

                    float delta = Mathf.Clamp((along - _startAlong) * scale, -2000f, 2000f);
                    if (snap)
                        delta = Mathf.Round(delta);
                    anchor = _startAnchor + axis * delta;
                    break;
                }

                case Handle.MovePlane:
                {
                    Vector3 hit;
                    if (!Level(ray, _startAnchor.y, out hit))
                        return false;

                    Vector3 delta = (hit - _startHit) * scale;
                    delta.y = 0f;
                    if (delta.magnitude > 2000f)
                        return false;
                    if (snap)
                        delta = new Vector3(Mathf.Round(delta.x), 0f, Mathf.Round(delta.z));
                    anchor = _startAnchor + delta;
                    break;
                }

                default:
                {
                    float radians = Vector2.Dot(mouse - _startMouse, _tangent) / _perRadian * scale;
                    float degrees = radians * Mathf.Rad2Deg;
                    if (Dragging == Handle.TurnHeading)
                    {
                        float heading = _startAngle * Mathf.Rad2Deg + degrees;
                        if (snap)
                            heading = Mathf.Round(heading / 15f) * 15f;
                        angle = Mathf.Repeat(heading, 360f) * Mathf.Deg2Rad;
                    }
                    else if (Dragging == Handle.TurnTilt)
                    {
                        tilt = Mathf.Clamp(_startTilt + degrees, -FlagSpec.MaxTilt, FlagSpec.MaxTilt);
                        if (snap)
                            tilt = Mathf.Round(tilt / 5f) * 5f;
                    }
                    else
                    {
                        lean = Mathf.Clamp(_startLean + degrees, -FlagSpec.MaxTilt, FlagSpec.MaxTilt);
                        if (snap)
                            lean = Mathf.Round(lean / 5f) * 5f;
                    }

                    break;
                }
            }

            bool changed = (anchor - Anchor).sqrMagnitude > 1e-8f || Mathf.Abs(angle - Angle) > 1e-6f ||
                           Mathf.Abs(tilt - Tilt) > 1e-4f || Mathf.Abs(lean - Lean) > 1e-4f;
            Anchor = anchor;
            Angle = angle;
            Tilt = tilt;
            Lean = lean;
            return changed;
        }

        /// <summary>
        /// The flag went elsewhere than the drag put it -- tilted or leaning on a pole that stands,
        /// it pivots on the pole's foot (FlagEditor.ApplyTransform): the handles go with it. The
        /// drag's own measure (the pointer's travel since it began) is unchanged.
        /// </summary>
        public void Follow(Vector3 anchor)
        {
            if (IsDragging)
                Anchor = anchor;
        }

        /// <summary>The drag done: the flag stays where it was taken.</summary>
        public void End()
        {
            if (IsDragging)
                _holdUntil = Time.realtimeSinceStartup + 0.4f;
            Dragging = Handle.None;
        }

        private float _holdUntil;

        /// <summary>The drag given up (right-click, Esc): the flag's place and turn as the drag found them.</summary>
        public void Cancel()
        {
            if (!IsDragging)
                return;

            Anchor = _startAnchor;
            Angle = _startAngle;
            Tilt = _startTilt;
            Lean = _startLean;
            Dragging = Handle.None;
        }

        /// <summary>A line for the pointer's tooltip while dragging: what changes, and by how much.</summary>
        public string Describe()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            switch (Dragging)
            {
                case Handle.MoveX:
                case Handle.MoveY:
                case Handle.MoveZ:
                case Handle.MovePlane:
                    return Localization.Get("Gizmo.Moved", (Anchor - _startAnchor).magnitude.ToString("0.0", inv),
                                            (Anchor.y - _startAnchor.y).ToString("+0.0;-0.0;0.0", inv));
                case Handle.TurnHeading:
                    return Localization.Get("Gizmo.Heading", Mathf.Repeat(Angle * Mathf.Rad2Deg, 360f).ToString("0", inv));
                case Handle.TurnTilt:
                    return Localization.Get("Gizmo.Tilt", Tilt.ToString("0", inv));
                case Handle.TurnLean:
                    return Localization.Get("Gizmo.Lean", Lean.ToString("0", inv));
                default:
                    return null;
            }
        }

        /// <summary>What a handle does, for the pointer's tooltip while it is hovered.</summary>
        public static string Hint(Handle handle)
        {
            switch (handle)
            {
                case Handle.MoveX: return Localization.Get("Gizmo.HintX");
                case Handle.MoveY: return Localization.Get("Gizmo.HintY");
                case Handle.MoveZ: return Localization.Get("Gizmo.HintZ");
                case Handle.MovePlane: return Localization.Get("Gizmo.HintPlane");
                case Handle.TurnHeading: return Localization.Get("Gizmo.HintHeading");
                case Handle.TurnTilt: return Localization.Get("Gizmo.HintTilt");
                case Handle.TurnLean: return Localization.Get("Gizmo.HintLean");
                default: return null;
            }
        }

        // ---- drawing ----------------------------------------------------------------------

        /// <summary>
        /// The handles, in the overlay stage (the tool's RenderOverlay): rings, then arrows, then the
        /// knob, each in its colour or lit up, faint behind the city. Needs the gizmo shader; without
        /// it nothing is drawn (said once).
        /// </summary>
        public void Draw()
        {
            if (!Ready())
                return;

            DrawRing(_y, Handle.TurnHeading, ColourY);
            DrawRing(_z, Handle.TurnTilt, ColourZ);
            DrawRing(_x, Handle.TurnLean, ColourX);
            DrawArrow(_x, Handle.MoveX, ColourX);
            DrawArrow(_y, Handle.MoveY, ColourY);
            DrawArrow(_z, Handle.MoveZ, ColourZ);

            Color knob = Hovered == Handle.MovePlane || Dragging == Handle.MovePlane ? ColourHot : ColourKnob;
            DrawMesh(_knob, Matrix4x4.TRS(_origin, Quaternion.LookRotation(_x, _y), Vector3.one * _size), knob);
        }

        private void DrawArrow(Vector3 axis, Handle handle, Color colour)
        {
            Quaternion turn = Quaternion.FromToRotation(Vector3.up, axis);
            DrawMesh(_arrow, Matrix4x4.TRS(_origin, turn, Vector3.one * _size), Hot(handle) ? ColourHot : colour);
        }

        private void DrawRing(Vector3 axis, Handle handle, Color colour)
        {
            Quaternion turn = Quaternion.FromToRotation(Vector3.up, axis);
            DrawMesh(_ring, Matrix4x4.TRS(_origin, turn, Vector3.one * _size), Hot(handle) ? ColourHot : colour);
        }

        private bool Hot(Handle handle)
        {
            return Dragging == handle || (Dragging == Handle.None && Hovered == handle);
        }

        private void DrawMesh(Mesh mesh, Matrix4x4 matrix, Color colour)
        {
            _material.SetColor(IdColor, colour.linear);
            for (int pass = 0; pass < 2; pass++)
            {
                if (_material.SetPass(pass))
                    Graphics.DrawMeshNow(mesh, matrix);
            }
        }

        private static readonly int IdColor = Shader.PropertyToID("_Color");

        private bool Ready()
        {
            if (_material != null)
                return true;
            if (_failed)
                return false;

            Shader shader = ShaderBundle.Get(ShaderBundle.Gizmo);
            if (shader == null)
            {
                _failed = true;
                Log.Warn("flags: the gizmo's shader is not in the bundle; the selected flag is moved with the panel's sliders only");
                return false;
            }

            _material = new Material(shader) { name = "RealFlag gizmo" };
            _arrow = ArrowMesh();
            _ring = RingMesh();
            _knob = KnobMesh();
            return true;
        }

        public void Destroy()
        {
            FlagMesh.Release(_material);
            FlagMesh.Release(_arrow);
            FlagMesh.Release(_ring);
            FlagMesh.Release(_knob);
            _material = null;
        }

        // ---- geometry ---------------------------------------------------------------------

        private Vector3 AxisOf(Handle handle)
        {
            switch (handle)
            {
                case Handle.MoveX:
                case Handle.TurnLean:
                    return _x;
                case Handle.MoveZ:
                case Handle.TurnTilt:
                    return _z;
                default:
                    return _y;
            }
        }

        /// <summary>Two unit vectors square to each other and to the axis: a ring's plane.</summary>
        private static void RingBasis(Vector3 axis, out Vector3 u, out Vector3 v)
        {
            Quaternion turn = Quaternion.FromToRotation(Vector3.up, axis);
            u = turn * Vector3.right;
            v = turn * Vector3.forward;
        }

        private Vector3 NearestOnRing(Camera camera, Vector2 mouse, Vector3 axis)
        {
            Vector3 u, v;
            RingBasis(axis, out u, out v);
            Vector3 best = _origin + u * (RingRadius * _size);
            float bestDistance = float.MaxValue;
            for (int i = 0; i < RingSamples; i++)
            {
                float t = i * 2f * Mathf.PI / RingSamples;
                Vector3 p = _origin + (u * Mathf.Cos(t) + v * Mathf.Sin(t)) * (RingRadius * _size);
                Vector2 s;
                if (!Screen(camera, p, out s))
                    continue;

                float d = (s - mouse).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }

            return best;
        }

        /// <summary>
        /// Where along the axis through the origin (metres) the point nearest the ray is: the
        /// closest points of two lines. NaN when the axis points (nearly) at the eye.
        /// </summary>
        private float Along(Ray ray, Vector3 axis)
        {
            Vector3 d = ray.direction.normalized;
            float b = Vector3.Dot(axis, d);
            float denominator = 1f - b * b;
            if (denominator < 0.002f)
                return float.NaN;

            Vector3 w = _startAnchor - ray.origin;
            float along = (b * Vector3.Dot(d, w) - Vector3.Dot(axis, w)) / denominator;
            return along;
        }

        /// <summary>Where the ray meets the level at <paramref name="height"/>; false if it runs along it or away.</summary>
        private static bool Level(Ray ray, float height, out Vector3 hit)
        {
            hit = Vector3.zero;
            float dy = ray.direction.y;
            if (Mathf.Abs(dy) < 1e-3f)
                return false;

            float t = (height - ray.origin.y) / dy;
            if (t <= 0f)
                return false;

            hit = ray.origin + ray.direction * t;
            return true;
        }

        private static bool Screen(Camera camera, Vector3 world, out Vector2 screen)
        {
            Vector3 s = camera.WorldToScreenPoint(world);
            screen = new Vector2(s.x, s.y);
            return s.z > 0.01f;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float length2 = ab.sqrMagnitude;
            float t = length2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / length2) : 0f;
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>An arrow along +y, unit length to its tip: a thin shaft and a cone.</summary>
        private static Mesh ArrowMesh()
        {
            var b = new MeshBuilder();
            b.Tube(ArrowFrom, ArrowTo - 0.22f, 0.018f, 0.018f, 12, false);
            b.Tube(ArrowTo - 0.22f, ArrowTo, 0.065f, 0.0005f, 16, true);
            return b.ToMesh("RealFlag gizmo arrow");
        }

        /// <summary>A ring round +y, radius RingRadius: a thin torus.</summary>
        private static Mesh RingMesh()
        {
            var b = new MeshBuilder();
            b.Torus(RingRadius, 0.014f, 96, 8);
            return b.ToMesh("RealFlag gizmo ring");
        }

        /// <summary>The knob: a small cube.</summary>
        private static Mesh KnobMesh()
        {
            var b = new MeshBuilder();
            b.Cube(0.075f);
            return b.ToMesh("RealFlag gizmo knob");
        }

        /// <summary>The little meshes of the gizmo, with their normals (for the shader's headlamp shade).</summary>
        private sealed class MeshBuilder
        {
            private readonly System.Collections.Generic.List<Vector3> _v = new System.Collections.Generic.List<Vector3>();
            private readonly System.Collections.Generic.List<Vector3> _n = new System.Collections.Generic.List<Vector3>();
            private readonly System.Collections.Generic.List<int> _t = new System.Collections.Generic.List<int>();

            /// <summary>A tube (or cone) along +y from y0 to y1, radius r0 to r1, capped at the bottom.</summary>
            public void Tube(float y0, float y1, float r0, float r1, int sides, bool cone)
            {
                int first = _v.Count;
                float slope = r0 - r1, height = y1 - y0;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * 2f * Mathf.PI / sides;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    Vector3 n = new Vector3(c * height, slope, s * height).normalized;
                    _v.Add(new Vector3(r0 * c, y0, r0 * s)); _n.Add(n);
                    _v.Add(new Vector3(r1 * c, y1, r1 * s)); _n.Add(n);
                }

                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    int a0 = first + 2 * i, a1 = a0 + 1, b0 = first + 2 * j, b1 = b0 + 1;
                    _t.Add(a0); _t.Add(a1); _t.Add(b0);
                    _t.Add(b0); _t.Add(a1); _t.Add(b1);
                }

                // The bottom, facing down.
                int centre = _v.Count;
                _v.Add(new Vector3(0f, y0, 0f)); _n.Add(Vector3.down);
                int ring = _v.Count;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * 2f * Mathf.PI / sides;
                    _v.Add(new Vector3(r0 * Mathf.Cos(a), y0, r0 * Mathf.Sin(a)));
                    _n.Add(Vector3.down);
                }

                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    _t.Add(centre); _t.Add(ring + i); _t.Add(ring + j);
                }
            }

            /// <summary>A torus round +y: its big radius R, its tube's r.</summary>
            public void Torus(float big, float small, int around, int sides)
            {
                int first = _v.Count;
                for (int i = 0; i < around; i++)
                {
                    float a = i * 2f * Mathf.PI / around;
                    Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    for (int k = 0; k < sides; k++)
                    {
                        float b = k * 2f * Mathf.PI / sides;
                        Vector3 n = radial * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                        _v.Add(radial * big + n * small);
                        _n.Add(n);
                    }
                }

                for (int i = 0; i < around; i++)
                {
                    int i2 = (i + 1) % around;
                    for (int k = 0; k < sides; k++)
                    {
                        int k2 = (k + 1) % sides;
                        int a = first + i * sides + k, b = first + i2 * sides + k, c = first + i * sides + k2, d = first + i2 * sides + k2;
                        _t.Add(a); _t.Add(c); _t.Add(b);
                        _t.Add(b); _t.Add(c); _t.Add(d);
                    }
                }
            }

            /// <summary>A cube of this half size round the origin, flat-shaded.</summary>
            public void Cube(float half)
            {
                Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
                foreach (Vector3 n in normals)
                {
                    Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                    Vector3 v = Vector3.Cross(n, u);
                    int first = _v.Count;
                    _v.Add((n - u - v) * half); _v.Add((n + u - v) * half); _v.Add((n + u + v) * half); _v.Add((n - u + v) * half);
                    for (int i = 0; i < 4; i++)
                        _n.Add(n);
                    _t.Add(first); _t.Add(first + 1); _t.Add(first + 2);
                    _t.Add(first); _t.Add(first + 2); _t.Add(first + 3);
                }
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_v);
                mesh.SetNormals(_n);
                mesh.SetTriangles(_t, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
