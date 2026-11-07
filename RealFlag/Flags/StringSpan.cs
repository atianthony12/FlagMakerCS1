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
    /// <remarks>
    /// THE FRAME'S WORK IS SPLIT (the 2026-11-07 sweep: one span of 36 flags cost 0.7 ms of the
    /// main thread every frame, all of it posing and uploading, and a city hangs hundreds). The
    /// main thread decides what the span needs this frame (<see cref="PrepareCord"/>,
    /// <see cref="PrepareFlags"/>: its swing, its hang, which flags play which source at what
    /// time); the cloth's thread fills the vertices (<see cref="SpanWork"/>, a ClothJob.Work: the
    /// cord's tube, each flag's sample, transform and normals -- managed maths only, Invariant 1);
    /// the main thread uploads what came back the frame after (<see cref="TakeWork"/>) and draws
    /// it. So a span is drawn one frame behind its state, cord and flags alike, as a flag on a pole
    /// is drawn a frame after it was stepped. A still span (its flags far, or not waving) is posed
    /// again only when its swing has moved; a waving one whose flags are under
    /// StringMotion.FullRatePixels every other frame; the cord only when its swing or its ends
    /// moved. The catenary's cosh is taken once per lay, not per frame. The preview drives the same
    /// code inline (<see cref="PoseFlags"/>, <see cref="BuildCord"/>).
    /// </remarks>
    public sealed class StringSpan
    {
        /// <summary>The cord's points: one every this many metres along the level, at least / at most.</summary>
        public const float CordStep = 0.4f;
        public const int MinCordPoints = 9, MaxCordPoints = 81;

        /// <summary>The cloth detail of the sources (FlagCloth.Details; small flags are coarser anyway: MinCell).</summary>
        public const int SourceDetail = 18;

        /// <summary>
        /// A still span's swing must have turned this far (radians, about 0.3 degrees) before its
        /// flags are posed again, the cord this far (about 0.1 degrees) before it is filled again:
        /// in a steady wind the swing settles and nothing of a still span costs anything.
        /// </summary>
        public const float StillAngleStep = 0.005f, CordAngleStep = 0.002f;

        /// <summary>
        /// A vertex posed (sampled, placed, its normal) counted as this share of a particle step,
        /// for the batch's budget: about a sixth of a step's time, measured against the thread's
        /// own unit cost in the log (ClothWorker.PlayShare is a playback's).
        /// </summary>
        public const int PoseShare = 8;

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

        /// <summary>The designs or the shape changed (the renderer sets them with a new Spec); the cord's kind or thickness did.</summary>
        public bool DesignsDirty = true, ShapeDirty = true, CordDirty = true;
        public int AtlasVersion = -1;
        public int Columns, Rows;

        /// <summary>Its frame: the level along it (X), up, and square to it (Z = X x up); bounds relative to A.</summary>
        public Vector3 X = Vector3.right, Z = Vector3.forward;
        public Bounds Bounds;
        public bool Visible;

        public readonly List<Vector3> Points = new List<Vector3>();
        public readonly List<Vector3> Downs = new List<Vector3>();
        private Vector3 _swung = Vector3.down;

        /// <summary>The side its flags fly out to (StringMotion.OnString): the target (+1 / -1, held; 0 before the first) and where they are now, eased between.</summary>
        private float _sideTarget, _side;

        /// <summary>The thread's part of the frame, and whether it is out on the thread (queued this frame, taken the next).</summary>
        public readonly SpanWork Work;
        public bool WorkOut;

        /// <summary>The cord's mesh / the flags' mesh hold something to draw (a pose taken since the last shape or lay).</summary>
        public bool CordReady, FlagsReady;

        // Per lay (LayVersion moves on in Lay): where along the chord (a fraction) and how far
        // below it (the drop) each cord point and each flag's two ends are -- the catenary's cosh
        // and log once, so Hang and the anchors are a multiply-add each.
        private int _layVersion, _cordLaidFor = -1, _flagsLaidFor = -1;
        private float _deepest;
        private float[] _cordF = new float[0], _cordDrop = new float[0];
        private float[] _flagF0 = new float[0], _flagDrop0 = new float[0], _flagF1 = new float[0], _flagDrop1 = new float[0];

        // The pose prepared for the thread: per flag its source, the time it samples, its anchor and its string's slope; the span's sources by slot.
        private StringMotion.Source[] _slotSources = new StringMotion.Source[0];
        private StringMotion.Source[] _poseSources = new StringMotion.Source[0];
        private float[] _poseTimes = new float[0], _poseSlopes = new float[0];
        private Vector3[] _poseAnchors = new Vector3[0];
        private int _poseCount;
        private float _poseZScale = 1f;

        // What the last pose and the last cord were made of, to skip the next while nothing changed.
        private float _posedAngle = float.NaN, _cordAngle = float.NaN;
        private int _posedLay = -1, _cordLay = -1;
        private bool _posedStill;
        private int _frame;

        /// <summary>The designs' sequence for the current Spec (made again when the designs change; kept when only the atlas moved on).</summary>
        private StringFlags _sequence;

        public StringSpan()
        {
            Work = new SpanWork(this);
        }

        /// <summary>Where its flags are between the two sides now (+1: out to +Z), for the log and the tests.</summary>
        public float Side
        {
            get { return _side; }
        }

        /// <summary>
        /// The side its flags fly out to, a step on, the wind blowing <paramref name="angle"/>
        /// degrees from X towards Z: swung round over OnString.SideSeconds when the wind has come
        /// round to the other side (and is SideHold past the string's line). The first call takes it at once.
        /// </summary>
        public void Steer(float dt, float angle)
        {
            bool first = _sideTarget == 0f;
            _sideTarget = StringMotion.OnString.SideFor(angle, _sideTarget);
            _side = first ? _sideTarget : StringMotion.OnString.Ease(_side, _sideTarget, dt);
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
            _deepest = Curve.Deepest;
            _layVersion++;
            ShapeDirty = true;
            DesignsDirty = true;
            CordDirty = true;
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

        /// <summary>
        /// In view by its ends, its sag and its flags' hang alone, whatever its swing (a box round
        /// all of that): false and nothing else of the span is worth doing this frame. Before this
        /// every span in the city was hung, and every one on the screen's edge posed, each frame.
        /// </summary>
        public bool QuickVisible(Plane[] planes)
        {
            Vector3 c = B - A;
            float reach = _deepest + Spec.Hang + 0.3f;
            Vector3 lo = Vector3.Min(Vector3.zero, c) - new Vector3(reach, reach, reach);
            Vector3 hi = Vector3.Max(Vector3.zero, c) + new Vector3(reach, reach, reach);
            return GeometryUtility.TestPlanesAABB(planes, new Bounds(A + (lo + hi) * 0.5f, hi - lo));
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
            if (_cordLaidFor != _layVersion || _cordF.Length != points)
            {
                if (_cordF.Length != points)
                {
                    _cordF = new float[points];
                    _cordDrop = new float[points];
                }

                for (int i = 0; i < points; i++)
                {
                    float x = level * i / (points - 1);
                    _cordF[i] = level > 1e-4f ? x / level : 0f;
                    _cordDrop[i] = Curve.DropAt(x);
                }

                _cordLaidFor = _layVersion;
            }

            Points.Clear();
            Downs.Clear();
            Vector3 lo = Vector3.zero, hi = Vector3.zero;
            for (int i = 0; i < points; i++)
            {
                Vector3 p = chord * _cordF[i] + _swung * _cordDrop[i];
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

        /// <summary>Flag k's place in its chain's sequence (the chain carrying on, or each span on its own).</summary>
        public int Index(int k)
        {
            int n = Layout.Count;
            int own = ChainForward ? k : n - 1 - k;
            return Spec.CarryOn ? ChainOffset + own : own;
        }

        // ---- the cord ---------------------------------------------------------------------------

        /// <summary>
        /// Whether the cord must be filled again this frame (its ends moved, its kind or thickness
        /// changed, its swing turned by CordAngleStep): true, and the thread's work is set up for it.
        /// </summary>
        public bool PrepareCord()
        {
            if (Points.Count < 2)
                return false;

            bool due = CordDirty || !CordReady || _cordLay != _layVersion || Math.Abs(Swing.Angle - _cordAngle) > CordAngleStep;
            if (!due)
                return false;

            if (Cord == null)
                Cord = new StringCordMesh();
            CordDirty = false;
            _cordLay = _layVersion;
            _cordAngle = Swing.Angle;
            Work.DoCord = true;
            Work.Kind = Spec.Kind;
            Work.Thickness = Spec.Thickness;
            Work.CordBounds = Bounds;
            return true;
        }

        /// <summary>The cord filled and sent in one (the preview).</summary>
        public void BuildCord()
        {
            if (Cord == null)
                Cord = new StringCordMesh();
            Cord.Build(Points, Downs, Spec.Kind, Spec.Thickness, Bounds);
            CordReady = true;
            CordDirty = false;
            _cordLay = _layVersion;
            _cordAngle = Swing.Angle;
        }

        // ---- the flags --------------------------------------------------------------------------

        /// <summary>
        /// The frame's part of posing done on the main thread: the mesh shaped and designed if it
        /// must be, the span's sources found and told they are wanted (<paramref name="speed"/>
        /// m/s at the flags, blowing <paramref name="angle"/> degrees from X towards Z, the flags
        /// <paramref name="pixels"/> big on the screen), and -- when a pose is due -- each flag's
        /// source, sample time, anchor and slope set out for the thread (<see cref="SpanWork"/>).
        /// True when a pose is due. <paramref name="flags"/>: how many the span draws.
        /// </summary>
        public bool PrepareFlags(Func<StringMotion.SourceKey, StringMotion.Source> sourceFor, StringAtlas atlas, IList<FlagCountry> order,
                                 float speed, float angle, bool still, float pixels, out int flags)
        {
            flags = 0;
            int columns, rows;
            FlagCloth.GridFor(Spec.Hang, Spec.Along, SourceDetail, out columns, out rows);
            int n = Math.Min(Layout.Count, StringFlagsMesh.MostFlags(columns, rows));
            if (n <= 0)
                return false;
            flags = n;

            if (Flags == null)
                Flags = new StringFlagsMesh();
            if (ShapeDirty || Flags.Flags != n || Columns != columns || Rows != rows)
            {
                Flags.Shape(n, columns, rows);
                Columns = columns;
                Rows = rows;
                ShapeDirty = false;
                DesignsDirty = true;
                // Its vertices are nothing until a pose comes back: not drawn meanwhile.
                FlagsReady = false;
            }

            if (DesignsDirty)
                Designs(atlas, order, n);
            else if (atlas != null && AtlasVersion != atlas.Version)
                Recell(atlas, order, n);

            int motions = Spec.Motions > 0 ? Spec.Motions : StringMotion.AutoMotions(ChainLength > 1 ? n * 2 : n);
            // Every source flies across: the wind's angle scales the speed it is fed and picks the
            // side the flags fly out to (never which cloth they play).
            if (_sideTarget == 0f)
                Steer(0f, angle);
            float zScale = still ? 1f : StringMotion.OnString.ZScale(_side);
            float fed = speed * StringMotion.OnString.Share(angle);

            // The span's sources -- one still, or one per motion slot -- each wanted this frame
            // whether or not a pose is due, so they keep stepping (and their LastWanted fresh).
            int slots = still ? 1 : motions;
            if (_slotSources.Length < slots)
                _slotSources = new StringMotion.Source[slots];
            for (int slot = 0; slot < slots; slot++)
            {
                StringMotion.SourceKey key = StringMotion.SourceKey.For(Spec.Along, Spec.Hang, Spec.Shape, Spec.Depth, Spec.Waviness, SourceDetail, slot, still);
                StringMotion.Source source = sourceFor(key);
                source.Want(fed, pixels);
                _slotSources[slot] = source;
            }

            // A pose this frame? A still span only when its swing has moved (or it has never been
            // posed as still); a waving one far off (its flags under FullRatePixels) every other frame.
            bool due;
            if (still)
            {
                due = !FlagsReady || !_posedStill || _posedLay != _layVersion || _poseZScale != 1f || Math.Abs(Swing.Angle - _posedAngle) > StillAngleStep;
            }
            else
            {
                _frame++;
                due = !FlagsReady || pixels >= StringMotion.FullRatePixels || (_frame & 1) == 0;
            }

            if (!due)
                return false;

            if (_flagsLaidFor != _layVersion || _flagF0.Length < n)
            {
                // Per lay: where along the chord each flag's two ends are and how far they drop.
                if (_flagF0.Length < n)
                {
                    _flagF0 = new float[n];
                    _flagDrop0 = new float[n];
                    _flagF1 = new float[n];
                    _flagDrop1 = new float[n];
                }

                float level = Curve.Level;
                for (int k = 0; k < n; k++)
                {
                    float s0 = Layout.First + k * Layout.Pitch;
                    float x0 = Curve.XAt(s0), x1 = Curve.XAt(s0 + Layout.Along);
                    _flagF0[k] = level > 1e-4f ? x0 / level : 0f;
                    _flagDrop0[k] = Curve.DropAt(x0);
                    _flagF1[k] = level > 1e-4f ? x1 / level : 0f;
                    _flagDrop1[k] = Curve.DropAt(x1);
                }

                _flagsLaidFor = _layVersion;
            }

            if (_poseSources.Length < n)
            {
                _poseSources = new StringMotion.Source[n];
                _poseTimes = new float[n];
                _poseSlopes = new float[n];
                _poseAnchors = new Vector3[n];
            }

            Vector3 chord = B - A;
            for (int k = 0; k < n; k++)
            {
                Vector3 a = chord * _flagF0[k] + _swung * _flagDrop0[k];
                Vector3 e = chord * _flagF1[k] + _swung * _flagDrop1[k];
                Vector3 t = e - a;
                float level = new Vector2(t.x, t.z).magnitude;
                int index = Index(k);
                StringMotion.Source source = _slotSources[still ? 0 : StringMotion.SlotFor(index, motions)];
                _poseSources[k] = source;
                _poseTimes[k] = source.Clock - (still ? 0f : StringMotion.DelayFor(index, Segment));
                _poseSlopes[k] = level > 1e-4f ? Mathf.Clamp(t.y / level, -3f, 3f) : 0f;
                _poseAnchors[k] = a;
            }

            _poseCount = n;
            _poseZScale = zScale;
            _posedAngle = Swing.Angle;
            _posedLay = _layVersion;
            _posedStill = still;
            Work.DoFlags = true;
            Work.FlagBounds = Bounds;
            return true;
        }

        /// <summary>The thread's posing: every flag set out by PrepareFlags sampled, placed and given its normals (StringFlagsMesh.Pose). Returns how many were.</summary>
        private int PoseAll()
        {
            int posed = 0;
            for (int k = 0; k < _poseCount; k++)
            {
                // The side in the sample, not the frame: each flag's rows (and so its design) stay where they are on the string.
                if (Flags.Pose(k, _poseSources[k], _poseTimes[k], _poseSlopes[k], _poseZScale, _poseAnchors[k], X, Vector3.up, Z))
                    posed++;
            }

            return posed;
        }

        /// <summary>
        /// What the thread filled last frame, to the GPU (the main thread, after the batch): the
        /// cord's and the flags' meshes, ready to draw. Nothing came (a throw): the last pose stays.
        /// </summary>
        public void TakeWork()
        {
            WorkOut = false;
            if (Work.CordDone && Cord != null)
            {
                Cord.Apply(Work.CordBounds);
                CordReady = true;
            }

            if (Work.FlagsDone && Flags != null && Work.Posed > 0)
            {
                Flags.ApplyPose(Work.FlagBounds);
                FlagsReady = true;
            }

            Work.Reset();
        }

        /// <summary>Work set up but never run (the frame threw before the batch): forgotten, so the next frame decides afresh.</summary>
        public void DropWork()
        {
            WorkOut = false;
            if (Work.DoCord)
                CordDirty = true;
            if (Work.DoFlags)
                _posedLay = -1;
            Work.Reset();
        }

        /// <summary>The work the thread would do for what is set up, in particle steps' worth (for the batch's budget).</summary>
        public long PredictedWork
        {
            get
            {
                long work = 0;
                if (Work.DoCord)
                    work += (long)Points.Count * StringCordMesh.Sides / PoseShare;
                if (Work.DoFlags && Flags != null)
                    work += (long)_poseCount * Flags.VerticesPerFlag / PoseShare;
                return Math.Max(1L, work);
            }
        }

        /// <summary>
        /// The flags posed and sent in one (the preview; the renderer splits it over PrepareFlags,
        /// the thread and TakeWork). False when none could be posed. <paramref name="posed"/>: how
        /// many were, or hold the last pose.
        /// </summary>
        public bool PoseFlags(Func<StringMotion.SourceKey, StringMotion.Source> sourceFor, StringAtlas atlas, IList<FlagCountry> order,
                              float speed, float angle, bool still, out int posed)
        {
            int flags;
            if (!PrepareFlags(sourceFor, atlas, order, speed, angle, still, float.MaxValue, out flags))
            {
                posed = FlagsReady ? _poseCount : 0;
                return FlagsReady;
            }

            posed = PoseAll();
            Work.Reset();
            if (posed == 0)
                return false;
            Flags.ApplyPose(Bounds);
            FlagsReady = true;
            return true;
        }

        /// <summary>Every flag's design into the mesh: its colours, pattern, atlas cell, brightness.</summary>
        private void Designs(StringAtlas atlas, IList<FlagCountry> order, int n)
        {
            _sequence = new StringFlags(Spec, order);
            float aspect = Spec.Height / Mathf.Max(Spec.Width, 0.01f);
            for (int k = 0; k < n; k++)
            {
                FlagSpec d = _sequence.At(Index(k));
                int cell = atlas != null ? atlas.CellFor(d, aspect) : 0;
                bool through = d.HasImage && d.ImageAlpha == FlagImageAlpha.Through && FlagCountries.Find(d.Country) == null;
                Flags.Design(k, Spec, FlagLook.Linear(d.Colour1), FlagLook.Linear(d.Colour2), FlagLook.Linear(d.Colour3), d.Pattern, cell, d.Brightness, through);
            }

            Flags.ApplyDesigns();
            DesignsDirty = false;
            AtlasVersion = atlas != null ? atlas.Version : -1;
        }

        /// <summary>The atlas moved on (cells baked and uploaded): each flag's cell again, that one channel sent -- the designs are as they were.</summary>
        private void Recell(StringAtlas atlas, IList<FlagCountry> order, int n)
        {
            if (_sequence == null)
            {
                Designs(atlas, order, n);
                return;
            }

            float aspect = Spec.Height / Mathf.Max(Spec.Width, 0.01f);
            for (int k = 0; k < n; k++)
            {
                FlagSpec d = _sequence.At(Index(k));
                Flags.Cell(k, d.Pattern, atlas.CellFor(d, aspect));
            }

            Flags.ApplyCells();
            AtlasVersion = atlas.Version;
        }

        public void Release()
        {
            if (Flags != null)
                Flags.Release();
            if (Cord != null)
                Cord.Release();
            Flags = null;
            Cord = null;
            CordReady = false;
            FlagsReady = false;
        }

        /// <summary>
        /// A span's share of the cloth's batch (ClothJob.Work): the cord's vertices and the flags'
        /// poses the main thread set up (PrepareCord, PrepareFlags), filled into the span's mesh
        /// lists on the thread, uploaded by TakeWork the frame after. Managed maths only.
        /// </summary>
        public sealed class SpanWork : IClothWork
        {
            private readonly StringSpan _span;

            /// <summary>What to do (set on the main thread), what was done (set by the thread).</summary>
            public bool DoCord, DoFlags, CordDone, FlagsDone;
            public int Posed;

            /// <summary>The cord's kind and thickness, and the bounds each mesh takes (copied here: the span's own fields are the main thread's).</summary>
            public int Kind;
            public float Thickness;
            public Bounds CordBounds, FlagBounds;

            public SpanWork(StringSpan span)
            {
                _span = span;
            }

            public long Run()
            {
                long work = 0;
                if (DoCord && _span.Cord != null)
                {
                    _span.Cord.Fill(_span.Points, _span.Downs, Kind, Thickness);
                    CordDone = true;
                    work += (long)_span.Points.Count * StringCordMesh.Sides / PoseShare;
                }

                if (DoFlags && _span.Flags != null)
                {
                    Posed = _span.PoseAll();
                    FlagsDone = true;
                    work += (long)Posed * _span.Flags.VerticesPerFlag / PoseShare;
                }

                return Math.Max(1L, work);
            }

            public void Reset()
            {
                DoCord = DoFlags = CordDone = FlagsDone = false;
                Posed = 0;
            }
        }
    }
}
