using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// One span as it hangs and is drawn -- its curve, its swing, its cord and its flags posed
    /// from their shared cloths -- with no game type in it: StringRenderer drives one per segment
    /// in the game, the preview (StringPreview, the Unity editor) the same in its own scene. The
    /// game's side (the wind at the span, the network, the draws) stays in StringRenderer.
    /// </summary>
    public sealed class StringSpan
    {
        /// <summary>The cord's points: one every this many metres along the level, at least / at most.</summary>
        public const float CordStep = 0.4f;
        public const int MinCordPoints = 9, MaxCordPoints = 81;

        /// <summary>The cloth detail of the sources (FlagCloth.Details; small flags are coarser anyway: MinCell).</summary>
        public const int SourceDetail = 18;

        public ushort Segment;
        public StringSpec Spec;
        public Vector3 A, B;
        public ushort NodeA, NodeB;
        public StringCurve Curve;
        public StringLayout Layout;
        public StringSwing Swing;
        public float Seed;

        /// <summary>Its place in its chain: the flags before it, whether the chain runs along it from its start, how many spans the chain has.</summary>
        public int ChainOffset;
        public bool ChainForward = true;
        public int ChainLength = 1;

        public StringFlagsMesh Flags;
        public StringCordMesh Cord;
        public MaterialPropertyBlock Block, CordBlock;

        public bool DesignsDirty = true, ShapeDirty = true;
        public int AtlasVersion = -1;
        public int Columns, Rows;

        /// <summary>Its frame: the level along it (X), up, and square to it (Z = X x up); bounds relative to A.</summary>
        public Vector3 X = Vector3.right, Z = Vector3.forward;
        public Bounds Bounds;
        public bool Visible;

        public readonly List<Vector3> Points = new List<Vector3>();
        public readonly List<Vector3> Downs = new List<Vector3>();
        private Vector3 _swung = Vector3.down;

        /// <summary>
        /// The wind the flags' sources are found by, HELD: changed only when the wind at the span
        /// has moved well past it (a quarter in speed, 22.5 degrees in angle), so a gusty wind
        /// does not flick the flags from one shared cloth to another (each change a jump).
        /// </summary>
        private float _heldSpeed = -1f, _heldAngle;

        /// <summary>Each flag's slope step, held the same way (half a step past its edge before it changes).</summary>
        private int[] _slopeStep = new int[0];

        public const float HoldSpeed = 1.25f, HoldAngle = 22.5f;

        private void HoldWind(float speed, float angle)
        {
            if (_heldSpeed < 0f || speed > _heldSpeed * HoldSpeed || speed < _heldSpeed / HoldSpeed)
                _heldSpeed = speed;
            if (_heldSpeed == speed || Mathf.Abs(Mathf.DeltaAngle(angle, _heldAngle)) > HoldAngle)
                _heldAngle = angle;
        }

        /// <summary>Flag k's slope, in the sources' steps, held: a new step only half a step past the old one's edge.</summary>
        private float HeldSlope(int k, float degrees)
        {
            if (_slopeStep.Length <= k)
            {
                int was = _slopeStep.Length;
                Array.Resize(ref _slopeStep, Math.Max(k + 1, Layout.Count));
                for (int i = was; i < _slopeStep.Length; i++)
                    _slopeStep[i] = int.MinValue;
            }

            float step = StringMotion.SourceKey.SlopeStep;
            int now = Mathf.RoundToInt(degrees / step);
            int held = _slopeStep[k];
            if (held == int.MinValue || Mathf.Abs(degrees - held * step) > step)
                _slopeStep[k] = held = now;
            return held * step;
        }

        /// <summary>The curve and the flags' places along it, from the ends and the design.</summary>
        public void Lay()
        {
            Vector3 d = B - A;
            float level = new Vector2(d.x, d.z).magnitude;
            Curve = StringCurve.Through(level, d.y, Spec.Sag);
            Layout = StringLayout.For(Spec, Curve.Length);
            X = level > 1e-3f ? new Vector3(d.x, 0f, d.z) / level : Vector3.right;
            Z = Vector3.Cross(X, Vector3.up);
            ShapeDirty = true;
            DesignsDirty = true;
            _slopeStep = new int[0];
        }

        /// <summary>
        /// The swing a step on, the wind <paramref name="across"/> m/s square to the span (signed:
        /// towards +Z), the flags' area per metre of string from the design.
        /// </summary>
        public void StepSwing(float dt, float across)
        {
            float area = Layout.Count * Spec.Width * Spec.Height / Mathf.Max(Curve.Length, 0.1f);
            float held = Mathf.Sign(across) * StringSwing.Held(Mathf.Abs(across), area, Spec.Thickness, Spec.Kind) * Spec.Swing;
            Swing.Step(dt, held, StringSwing.Omega(Curve.Deepest));
        }

        /// <summary>The cord's points and the way it hangs at each, its sag plane swung about the chord; the bounds with the flags' reach.</summary>
        public void Hang()
        {
            Vector3 chord = B - A;
            Vector3 u = chord.sqrMagnitude > 1e-8f ? chord.normalized : Vector3.right;
            Vector3 down = Vector3.down - u * Vector3.Dot(Vector3.down, u);
            down = down.sqrMagnitude > 1e-8f ? down.normalized : Z;
            Vector3 side = Vector3.Cross(u, down);
            if (Vector3.Dot(side, Z) < 0f)
                side = -side;
            float phi = Swing.Angle;
            _swung = down * Mathf.Cos(phi) + side * Mathf.Sin(phi);

            float level = Curve.Level;
            int points = Mathf.Clamp(Mathf.CeilToInt(level / CordStep) + 1, MinCordPoints, MaxCordPoints);
            Points.Clear();
            Downs.Clear();
            Vector3 lo = Vector3.zero, hi = Vector3.zero;
            for (int i = 0; i < points; i++)
            {
                Vector3 p = Place(level * i / (points - 1));
                Points.Add(p);
                Downs.Add(_swung);
                lo = Vector3.Min(lo, p);
                hi = Vector3.Max(hi, p);
            }

            float reach = Spec.Hang + 0.2f;
            lo -= new Vector3(reach, reach, reach);
            hi += new Vector3(reach, reach * 0.25f, reach);
            Bounds = new Bounds((lo + hi) * 0.5f, hi - lo);
        }

        /// <summary>The string's point x metres along the level from the first end (relative to it).</summary>
        public Vector3 Place(float x)
        {
            float level = Curve.Level;
            float f = level > 1e-4f ? x / level : 0f;
            return (B - A) * f + _swung * Curve.DropAt(x);
        }

        public void BuildCord()
        {
            if (Cord == null)
                Cord = new StringCordMesh();
            Cord.Build(Points, Downs, Spec.Kind, Spec.Thickness, Bounds);
        }

        /// <summary>Flag k's place in its chain's sequence (the chain carrying on, or each span on its own).</summary>
        public int Index(int k)
        {
            int n = Layout.Count;
            int own = ChainForward ? k : n - 1 - k;
            return Spec.CarryOn ? ChainOffset + own : own;
        }

        /// <summary>
        /// The flags posed: each plays its source (<paramref name="sourceFor"/> finds or makes it by
        /// key) a delay behind, set on the string where it hangs; designs made first if they
        /// changed. False when none could be posed. The wind at the flags: <paramref name="speed"/>
        /// m/s, blowing <paramref name="angle"/> degrees from the span's X towards its Z.
        /// </summary>
        public bool PoseFlags(Func<StringMotion.SourceKey, StringMotion.Source> sourceFor, StringAtlas atlas, IList<FlagCountry> order,
                              float speed, float angle, bool still, out int posed)
        {
            posed = 0;
            int columns, rows;
            FlagCloth.GridFor(Spec.Hang, Spec.Along, SourceDetail, out columns, out rows);
            int n = Math.Min(Layout.Count, StringFlagsMesh.MostFlags(columns, rows));
            if (n <= 0)
                return false;

            if (Flags == null)
                Flags = new StringFlagsMesh();
            if (ShapeDirty || Flags.Flags != n || Columns != columns || Rows != rows)
            {
                Flags.Shape(n, columns, rows);
                Columns = columns;
                Rows = rows;
                ShapeDirty = false;
                DesignsDirty = true;
            }

            if (DesignsDirty || (atlas != null && AtlasVersion != atlas.Version))
                Designs(atlas, order, n);

            int motions = Spec.Motions > 0 ? Spec.Motions : StringMotion.AutoMotions(ChainLength > 1 ? n * 2 : n);
            HoldWind(speed, angle);
            for (int k = 0; k < n; k++)
            {
                float s0 = Layout.First + k * Layout.Pitch;
                Vector3 a = Place(Curve.XAt(s0));
                Vector3 e = Place(Curve.XAt(s0 + Layout.Along));
                Vector3 t = e - a;
                float slope = Mathf.Atan2(t.y, new Vector2(t.x, t.z).magnitude);
                int index = Index(k);

                StringMotion.SourceKey key = StringMotion.SourceKey.For(Spec.Along, Spec.Hang, Spec.Shape, Spec.Depth, Spec.Waviness, SourceDetail, _heldSpeed, _heldAngle,
                                                                        HeldSlope(k, slope * Mathf.Rad2Deg), StringMotion.SlotFor(index, motions), still);
                StringMotion.Source source = sourceFor(key);
                float time = source.Clock - (still ? 0f : StringMotion.DelayFor(index, Segment));
                if (Flags.Pose(k, source, time, slope - key.SlopeRadians, a, X, Vector3.up, Z))
                    posed++;
            }

            if (posed == 0)
                return false;
            Flags.ApplyPose(Bounds);
            return true;
        }

        /// <summary>Every flag's design into the mesh: its colours, pattern, atlas cell, brightness.</summary>
        private void Designs(StringAtlas atlas, IList<FlagCountry> order, int n)
        {
            var sequence = new StringFlags(Spec, order);
            float aspect = Spec.Height / Mathf.Max(Spec.Width, 0.01f);
            for (int k = 0; k < n; k++)
            {
                FlagSpec d = sequence.At(Index(k));
                int cell = atlas != null ? atlas.CellFor(d, aspect) : 0;
                bool through = d.HasImage && d.ImageAlpha == FlagImageAlpha.Through && FlagCountries.Find(d.Country) == null;
                Flags.Design(k, Spec, FlagLook.Linear(d.Colour1), FlagLook.Linear(d.Colour2), FlagLook.Linear(d.Colour3), d.Pattern, cell, d.Brightness, through);
            }

            Flags.ApplyDesigns();
            DesignsDirty = false;
            AtlasVersion = atlas != null ? atlas.Version : -1;
        }

        public void Release()
        {
            if (Flags != null)
                Flags.Release();
            if (Cord != null)
                Cord.Release();
            Flags = null;
            Cord = null;
        }
    }
}
