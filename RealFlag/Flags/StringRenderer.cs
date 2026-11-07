using System;
using System.Collections.Generic;
using System.Diagnostics;
using ColossalFramework;
using UnityEngine;
using Span = RealFlag.Flags.StringSpan;

namespace RealFlag.Flags
{
    /// <summary>
    /// Draws every string of flags (2026-11-07): each span of the string network (StringNet) hangs
    /// as its catenary (StringCurve), swings in the wind about the line between its ends
    /// (StringSwing), and carries its flags (StringLayout, StringFlags), which play the city's few
    /// shared cloths (StringMotion) a delay behind, each its own. One mesh and one draw for a
    /// span's flags (FlagString.shader, StringAtlas for the images), one for its cord (the pole
    /// shader's rope). Beside FlagRenderer on the controller; its own cloth thread.
    /// </summary>
    /// <remarks>
    /// The frame: the last batch collected (each source's steps recorded); the network read
    /// (every span checked, the buffer scanned a part a frame for spans made by others: Move It's
    /// pastes, the game's network tool); the wind; each span posed and drawn -- flags 6 px or more
    /// on the screen play waving sources, smaller ones the still shape (StillPose's, carried by
    /// the swing), under 2 px none; then the sources wanted this frame stepped on the thread.
    /// What it costs per flag is a playback and a share of a mesh upload, never a cloth step.
    /// </remarks>
    public sealed class StringRenderer : MonoBehaviour
    {
        public static StringRenderer Instance { get; private set; }

        /// <summary>On-screen size of a flag, pixels: below WavePixels its still shape, below DrawPixels none.</summary>
        public const float WavePixels = 6f, DrawPixels = 1.5f;

        /// <summary>A source not wanted for this long is let go, seconds (meanwhile kept as it was, not stepped: StringMotion.Source.Plan).</summary>
        public const float SourceIdle = StringMotion.KeepSeconds;

        /// <summary>Most steps a source takes a frame (catching up after a slow frame), and while it warms up.</summary>
        public const int MaxSourceSteps = 4, WarmSteps = 12;

        /// <summary>
        /// The batch's room: this many milliseconds of each thread per 1/60 s of frame (the flags'
        /// FlagRenderer.BatchBudget), more in a longer frame. The spans' posing is spent first
        /// (what is drawn), then the sources' steps, the biggest on the screen first; a source the
        /// room runs out before holds its time, up to four steps, and its flags hold with it.
        /// </summary>
        public const double BatchBudget = 8.0;

        /// <summary>The buffer's segments looked at a frame for spans the renderer does not know yet.</summary>
        public const int ScanPerFrame = 2048;

        private Material _flagMaterial, _cordMaterial;
        private int _layer;
        private Camera _camera;
        private bool _ready;
        private readonly ClothWorker _worker = new ClothWorker();
        private readonly List<StringMotion.Source> _planned = new List<StringMotion.Source>();
        private readonly List<Span> _works = new List<Span>();
        private readonly List<Due> _due = new List<Due>();
        private readonly List<ushort> _changed = new List<ushort>();
        private readonly Dictionary<ushort, Span> _spans = new Dictionary<ushort, Span>();

        /// <summary>A source with steps due this frame, before the room is shared out.</summary>
        private struct Due
        {
            public StringMotion.Source Source;
            public int Steps;
            public float Size, Pixels;
            public bool Warming;
        }

        /// <summary>The biggest on the screen first (one delegate, kept: List.Sort with a new lambda allocates on Mono).</summary>
        private static readonly Comparison<Due> ByPixels = (a, b) => b.Pixels.CompareTo(a.Pixels);

        /// <summary>The cost of a particle step on the thread, ms (the flags' FlagRenderer measures its own the same way): a slow average over the batches.</summary>
        private double _unitCost = 0.0015;
        private long _sampleWork;
        private double _sampleMs;
        private readonly Dictionary<StringMotion.SourceKey, StringMotion.Source> _sources = new Dictionary<StringMotion.SourceKey, StringMotion.Source>();
        private readonly List<StringMotion.SourceKey> _drop = new List<StringMotion.SourceKey>();
        private readonly StringAtlas _atlas = new StringAtlas();
        private readonly List<ushort> _found = new List<ushort>();
        private readonly List<ushort> _keys = new List<ushort>();
        private readonly List<ushort> _gone = new List<ushort>();
        private readonly List<ushort> _chain = new List<ushort>();
        private readonly List<bool> _chainForward = new List<bool>();
        private int _scanAt = 1, _registryVersion = -1;
        private bool _chainsDirty = true;
        private float _clock, _now, _windAngle = float.NaN;
        private Wind _wind;
        private float _pixelsPerRadian = 1000f;

        private readonly List<Span> _ghosts = new List<Span>();
        private int _ghostCount;

        /// <summary>The spans highlighted (the selected string) and hovered.</summary>
        private readonly HashSet<ushort> _selected = new HashSet<ushort>();
        private ushort _hovered;

        // The 5 s line.
        private readonly Stopwatch _cost = new Stopwatch(), _wait = new Stopwatch();
        private double _costMs, _waitMs;
        private int _costFrames, _drawnSpans, _drawnFlags, _stillFlags, _stepped, _posedSpans, _halfSpans, _held;
        private float _nextReport;
        private bool _loggedFirst;
        private int _stepFailures, _workFailures;

        // A frame that threw: counted, said with its stack for the first few (FrameFailed).
        private int _frameFailures, _frameFailuresSaid;
        private string _frameFailureLast;
        private const int MaxFrameFailuresSaid = 5;

        private static readonly int IdAtlas = Shader.PropertyToID("_RFAtlas");
        private static readonly int IdAtlasGrid = Shader.PropertyToID("_RFAtlasGrid");
        private static readonly int IdAtlasInset = Shader.PropertyToID("_RFAtlasInset");
        private static readonly int IdHighlight = Shader.PropertyToID("_RFHighlight");
        private static readonly int IdRopeColor = Shader.PropertyToID("_RFRopeColor");
        private static readonly int IdPoleColor = Shader.PropertyToID("_RFPoleColor");

        // ---- lifetime -------------------------------------------------------------------------

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            Shader flags = ShaderBundle.Get(ShaderBundle.String);
            Shader cord = ShaderBundle.Get(ShaderBundle.Pole);
            if (flags == null || cord == null)
            {
                Log.Warn("strings: the string shaders are not in this bundle (" + (flags == null ? ShaderBundle.String : ShaderBundle.Pole) +
                         "); strings are not drawn here (their spans stay in the city)");
                return;
            }

            _flagMaterial = new Material(flags) { name = "RealFlag string flags" };
            _cordMaterial = new Material(cord) { name = "RealFlag string" };
            _layer = PropLayerOf();

            // As many threads as the flags' (the "CPU threads for cloth" option, Auto = one per six
            // processors): they step the sources and pose every span in view.
            string problem;
            int threads = ClothWorker.ThreadsFor(SystemInfo.processorCount, Settings.ClothThreads != null ? Settings.ClothThreads.value : Settings.Defaults.ClothThreads);
            string stepping = _worker.Start(threads, out problem)
                ? "its cloth and its posing on " + _worker.Threads + " thread(s) of their own"
                : "its cloth on the main thread (" + problem + ")";
            _ready = true;
            Log.Msg("strings: renderer ready -- shader '" + flags.name + "' (" + _flagMaterial.passCount + " passes), " + StringRegistry.Count +
                    " saved span design(s), network " + (StringPrefab.Info != null ? "index " + StringPrefab.Info.m_prefabDataIndex : "MISSING") + ", " + stepping);
        }

        private static int PropLayerOf()
        {
            try
            {
                int count = PrefabCollection<PropInfo>.LoadedCount();
                for (int i = 0; i < count; i++)
                {
                    PropInfo info = PrefabCollection<PropInfo>.GetLoaded((uint)i);
                    if (info != null && info.m_hasRenderer && !info.m_isMarker && !info.m_isDecal)
                        return info.m_prefabDataLayer;
                }
            }
            catch (Exception e)
            {
                Log.Warn("strings: could not read the props' layer: " + e.Message);
            }

            return 0;
        }

        private void OnDestroy()
        {
            // Whatever was only counted (see FrameFailed), said once with the city.
            if (_frameFailures > _frameFailuresSaid)
                Log.Warn("strings: in this city " + _frameFailures + " frame(s) threw");

            _worker.Wait();
            _worker.Stop();
            foreach (Span s in _spans.Values)
                Release(s);
            foreach (Span s in _ghosts)
                Release(s);
            _spans.Clear();
            _ghosts.Clear();
            _sources.Clear();
            _atlas.Clear();
            if (_flagMaterial != null)
                Destroy(_flagMaterial);
            if (_cordMaterial != null)
                Destroy(_cordMaterial);
            if (Instance == this)
                Instance = null;
        }

        private static void Release(Span s)
        {
            s.Release();
        }

        // ---- what the tool, the panel and the list ask -----------------------------------------

        public int SpanCount
        {
            get { return _spans.Count; }
        }

        public int SourceCount
        {
            get { return _sources.Count; }
        }

        /// <summary>The spans known, in no order (for the list).</summary>
        public void SpansTo(List<ushort> into)
        {
            into.Clear();
            into.AddRange(_spans.Keys);
        }

        public bool TryGetSpan(ushort segment, out Span span)
        {
            return _spans.TryGetValue(segment, out span);
        }

        /// <summary>The placing tool's string, drawn as it would be: spans through these points (none: none).</summary>
        public void SetGhost(IList<Vector3> points, StringSpec spec)
        {
            int spans = points != null ? Math.Max(0, points.Count - 1) : 0;
            while (_ghosts.Count < spans)
                _ghosts.Add(new Span { Seed = 0.37f * _ghosts.Count });
            for (int i = 0; i < spans; i++)
            {
                Span g = _ghosts[i];
                if (!g.Spec.Same(spec))
                {
                    g.Spec = spec.Sanitised();
                    g.DesignsDirty = true;
                }

                if (g.A != points[i] || g.B != points[i + 1])
                {
                    g.A = points[i];
                    g.B = points[i + 1];
                    Lay(g);
                }

                g.ChainOffset = 0;
                for (int k = 0; k < i; k++)
                    g.ChainOffset += _ghosts[k].Layout.Count;
            }

            _ghostCount = spans;
        }

        public void ClearGhost()
        {
            _ghostCount = 0;
        }

        /// <summary>The selected string's spans glow; the hovered one too.</summary>
        public void SetHighlight(ICollection<ushort> selected, ushort hovered)
        {
            _selected.Clear();
            if (selected != null)
            {
                foreach (ushort s in selected)
                    _selected.Add(s);
            }

            _hovered = hovered;
        }

        /// <summary>The span nearest the ray within <paramref name="pixels"/> of its cord on the screen, or 0.</summary>
        public ushort Pick(Ray ray, Camera camera, float pixels)
        {
            ushort best = 0;
            float bestScore = float.MaxValue;
            if (camera == null)
                return 0;
            float ppr = camera.pixelHeight / (2f * Mathf.Tan(0.5f * Mathf.Clamp(camera.fieldOfView, 1f, 170f) * Mathf.Deg2Rad));
            foreach (Span s in _spans.Values)
            {
                if (s.Points.Count < 2)
                    continue;
                // Its box first, grown by the pick's reach at its distance (the flags' hang is in
                // it already): every cord point of every span was tested before.
                var box = new Bounds(s.Bounds.center + s.A, s.Bounds.size);
                float far = Mathf.Sqrt(box.SqrDistance(ray.origin)) + box.extents.magnitude;
                box.Expand(2f * pixels / ppr * far);
                if (!box.IntersectRay(ray))
                    continue;
                for (int i = 0; i + 1 < s.Points.Count; i++)
                {
                    Vector3 p = s.A + s.Points[i], q = s.A + s.Points[i + 1];
                    float along;
                    float d = RayToSegment(ray, p, q, out along);
                    if (along <= 0f)
                        continue;
                    // The flags hang below the cord: their hang counts as near.
                    float reach = pixels / ppr * along + 0.5f * s.Spec.Hang;
                    if (d < reach && along < bestScore)
                    {
                        bestScore = along;
                        best = s.Segment;
                    }
                }
            }

            return best;
        }

        /// <summary>The distance from a ray to a segment, and how far along the ray the nearest point is.</summary>
        private static float RayToSegment(Ray ray, Vector3 p, Vector3 q, out float along)
        {
            Vector3 u = ray.direction, v = q - p, w = ray.origin - p;
            float a = Vector3.Dot(u, u), b = Vector3.Dot(u, v), c = Vector3.Dot(v, v), d = Vector3.Dot(u, w), e = Vector3.Dot(v, w);
            float den = a * c - b * b;
            float sc, tc;
            if (den < 1e-8f)
            {
                sc = 0f;
                tc = c > 1e-8f ? e / c : 0f;
            }
            else
            {
                sc = (b * e - c * d) / den;
                tc = (a * e - b * d) / den;
            }

            tc = Mathf.Clamp01(tc);
            sc = Mathf.Max(0f, (Vector3.Dot(u, p + v * tc - ray.origin)) / a);
            along = sc;
            return Vector3.Distance(ray.origin + u * sc, p + v * tc);
        }

        /// <summary>The middle of a span (for Go to and the list's distance).</summary>
        public bool TryMiddle(ushort segment, out Vector3 middle)
        {
            Span s;
            middle = Vector3.zero;
            if (!_spans.TryGetValue(segment, out s) || s.Points.Count == 0)
                return false;
            middle = s.A + s.Points[s.Points.Count / 2];
            return true;
        }

        // ---- the frame ------------------------------------------------------------------------

        private void LateUpdate()
        {
            if (!_ready)
                return;

            _camera = Camera.main;
            if (_camera == null)
                return;

            _cost.Reset();
            _cost.Start();

            _wait.Reset();
            _wait.Start();
            bool back = _worker.Wait();
            _wait.Stop();
            _waitMs += _wait.Elapsed.TotalMilliseconds;
            if (!back)
            {
                Log.Warn("strings: the cloth's thread did not come back; strings stop moving");
                _ready = false;
                return;
            }

            // The batch's jobs as the thread left them, in the order they were added: the planned
            // sources' first, then the spans' work (StringSpan.SpanWork).
            ClothJob[] done = _worker.Jobs;
            int count = _worker.Count;
            for (int i = 0; i < _planned.Count && i < count; i++)
            {
                ClothJob job = done[i];
                if (job.Failure != null && _stepFailures++ == 0)
                    Log.Error("strings: a source's step threw (said once; the source is laid out again)", job.Failure);
                _planned[i].Took(ref job);
            }

            for (int i = _planned.Count; i < count; i++)
            {
                if (done[i].Failure != null && _workFailures++ == 0)
                    Log.Error("strings: posing a span on the thread threw (said once; the span keeps its last pose)", done[i].Failure);
            }

            foreach (Span s in _works)
            {
                if (s.WorkOut)
                    s.TakeWork();
                else
                    s.DropWork();
            }

            // The cost of a particle step (a posed vertex a share of one: StringSpan.PoseShare),
            // sampled every 2000 of them: the room a source's steps take (PlanBatch).
            if (count > 0 && _worker.LastParticleSteps > 0)
            {
                _sampleWork += _worker.LastParticleSteps;
                _sampleMs += _worker.LastMilliseconds;
                if (_sampleWork >= 2000)
                {
                    double sample = Math.Max(1e-5, Math.Min(_sampleMs / _sampleWork, 0.05));
                    _unitCost = 0.9 * _unitCost + 0.1 * sample;
                    _sampleWork = 0;
                    _sampleMs = 0.0;
                }
            }

            _works.Clear();
            _planned.Clear();
            _worker.Clear();

            try
            {
                float realDt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.25f);
                SimulationManager sim = Singleton<SimulationManager>.instance;
                bool paused = sim != null && (sim.SimulationPaused || sim.ForcedSimulationPaused);
                bool frozen = paused && (Settings.FreezeWhenPaused == null || Settings.FreezeWhenPaused.value);
                float dt = frozen ? 0f : realDt;
                _clock += dt;
                _now = Time.realtimeSinceStartup;

                _wind = Wind.Current();
                float target = Mathf.Atan2(_wind.Fixed.x, _wind.Fixed.z) * Mathf.Rad2Deg;
                _windAngle = float.IsNaN(_windAngle) ? target : FlagWind.Turn(_windAngle, target, dt);
                _pixelsPerRadian = _camera.pixelHeight / (2f * Mathf.Tan(0.5f * Mathf.Clamp(_camera.fieldOfView, 1f, 170f) * Mathf.Deg2Rad));

                Sync();
                if (_chainsDirty)
                    Chains();

                _atlas.Tick(_now);
                if (_atlas.Texture != null)
                {
                    _flagMaterial.SetTexture(IdAtlas, _atlas.Texture);
                    _flagMaterial.SetVector(IdAtlasGrid, _atlas.Grid);
                    _flagMaterial.SetFloat(IdAtlasInset, StringAtlas.Inset);
                }

                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_camera);
                Vector3 eye = _camera.transform.position;
                foreach (Span s in _spans.Values)
                    Frame(s, dt, planes, eye);
                for (int i = 0; i < _ghostCount; i++)
                    Frame(_ghosts[i], dt, planes, eye);

                PlanBatch(dt, realDt);
            }
            catch (Exception e)
            {
                FrameFailed(e);
            }

            _worker.Run();
            _cost.Stop();
            _costMs += _cost.Elapsed.TotalMilliseconds;
            _costFrames++;
            Report();
        }

        /// <summary>
        /// The frame threw (the strings skip the rest of it): said with its stack the first time,
        /// and again for each different error up to <see cref="MaxFrameFailuresSaid"/>; every one
        /// counted (as FlagRenderer.FrameFailed). Before, every frame wrote the stack to both logs.
        /// </summary>
        private void FrameFailed(Exception e)
        {
            _frameFailures++;
            string what = e.GetType().Name + ": " + e.Message;
            if (what == _frameFailureLast || _frameFailuresSaid >= MaxFrameFailuresSaid)
                return;

            _frameFailureLast = what;
            _frameFailuresSaid++;
            Log.Error("strings: the frame threw; the strings are paused until the next frame" +
                      (_frameFailuresSaid == MaxFrameFailuresSaid ? " (further errors are only counted)" : " (the same error again is only counted)"), e);
        }

        /// <summary>The network as it is: spans gone, moved or new; designs for spans made by others.</summary>
        private void Sync()
        {
            // The registry's spans (the tool's, Move It's pastes, the save's): known at once. Only
            // the spans Set since last time are compared (a slider on a long chain Sets every span
            // of it, four times a second: comparing them all against all, twice encoded each,
            // grew as the square).
            int version = StringRegistry.Version;
            if (version != _registryVersion)
            {
                _registryVersion = version;
                bool all = StringRegistry.TakeChanged(_changed);
                StringRegistry.KeysTo(_keys);
                foreach (ushort k in _keys)
                {
                    if (!_spans.ContainsKey(k) && StringNet.IsSpan(k))
                        Join(k);
                }

                if (all)
                {
                    foreach (Span s in _spans.Values)
                        Redesign(s);
                }
                else
                {
                    foreach (ushort k in _changed)
                    {
                        Span s;
                        if (_spans.TryGetValue(k, out s))
                            Redesign(s);
                    }
                }
            }

            // A part of the buffer: spans made by another tool (Move It's clones, the network tool).
            int total = StringNet.SegmentCount;
            if (total > 0)
            {
                _found.Clear();
                StringNet.Scan(_scanAt, ScanPerFrame, _found);
                _scanAt += ScanPerFrame;
                if (_scanAt >= total)
                    _scanAt = 1;
                foreach (ushort f in _found)
                {
                    if (!_spans.ContainsKey(f))
                        Join(f);
                }
            }

            // Every known span: still there, still where it was.
            _gone.Clear();
            foreach (Span s in _spans.Values)
            {
                Vector3 a, b;
                ushort na, nb;
                if (!StringNet.TryRead(s.Segment, out a, out b, out na, out nb))
                {
                    _gone.Add(s.Segment);
                    continue;
                }

                if (na != s.NodeA || nb != s.NodeB)
                    _chainsDirty = true;
                s.NodeA = na;
                s.NodeB = nb;
                if (a != s.A || b != s.B)
                {
                    s.A = a;
                    s.B = b;
                    Lay(s);
                    _chainsDirty = true;
                }
            }

            foreach (ushort g in _gone)
            {
                Span s = _spans[g];
                Release(s);
                _spans.Remove(g);
                StringRegistry.Remove(g);
                _chainsDirty = true;
            }

            if (_gone.Count > 0 && Log.Detailed)
                Log.Detail("strings: " + _gone.Count + " span(s) gone (bulldozed, moved away by another tool, or removed)");
        }

        /// <summary>A span whose design the registry may have changed: taken if it did, laid again if its shape did.</summary>
        private void Redesign(Span s)
        {
            StringSpec spec;
            if (!StringRegistry.TryGet(s.Segment, out spec) || spec.Same(s.Spec))
                return;

            bool shape = spec.Along != s.Spec.Along || spec.Hang != s.Spec.Hang || spec.Spacing != s.Spec.Spacing || spec.Gap != s.Spec.Gap ||
                         spec.Count != s.Spec.Count || spec.Margin != s.Spec.Margin || spec.Sag != s.Spec.Sag;
            s.Spec = spec;
            s.DesignsDirty = true;
            s.CordDirty = true;
            if (shape)
            {
                Lay(s);
                _chainsDirty = true;
            }
        }

        /// <summary>A span the renderer did not know: its design from the registry, or -- made by another tool -- the new string's.</summary>
        private void Join(ushort segment)
        {
            Vector3 a, b;
            ushort na, nb;
            if (!StringNet.TryRead(segment, out a, out b, out na, out nb))
                return;

            StringSpec spec;
            if (!StringRegistry.TryGet(segment, out spec))
            {
                spec = StringEditor.NewSpec();
                // A city whose designs could not be read keeps them as they were: nothing written over them (Invariant 18).
                if (!StringSaveData.Keeping)
                    StringRegistry.Set(segment, spec);
                Log.Msg("strings: span #" + segment + " came without a design (another tool made it): it takes the new string's");
            }

            var s = new Span { Segment = segment, Spec = spec, A = a, B = b, NodeA = na, NodeB = nb, Seed = (segment * 0.6180339887f) % 1f * 100f };
            Lay(s);
            _spans[segment] = s;
            _chainsDirty = true;
        }

        /// <summary>The span's curve and its flags' places along it, from its ends and design.</summary>
        private static void Lay(Span s)
        {
            s.Lay();
        }

        /// <summary>Each chain's spans' places in it: the sequence carries on along a chain (StringSpec.CarryOn).</summary>
        private void Chains()
        {
            _chainsDirty = false;
            var done = new HashSet<ushort>();
            foreach (Span s in _spans.Values)
            {
                if (done.Contains(s.Segment))
                    continue;

                StringNet.ChainOf(s.Segment, _chain, _chainForward);
                int offset = 0;
                for (int i = 0; i < _chain.Count; i++)
                {
                    Span c;
                    if (!_spans.TryGetValue(_chain[i], out c))
                        continue;
                    done.Add(c.Segment);
                    int was = c.ChainOffset;
                    bool fwd = c.ChainForward;
                    c.ChainOffset = offset;
                    c.ChainForward = _chainForward[i];
                    c.ChainLength = _chain.Count;
                    if (was != offset || fwd != c.ChainForward)
                        c.DesignsDirty = true;
                    offset += c.Layout.Count;
                }

                if (!done.Contains(s.Segment))
                    done.Add(s.Segment);
            }
        }

        // ---- one span -----------------------------------------------------------------------

        private void Frame(Span s, float dt, Plane[] planes, Vector3 eye)
        {
            // Out of view by its ends and its sag alone: nothing of it this frame (its swing waits where it is).
            if (!s.QuickVisible(planes))
            {
                s.Visible = false;
                return;
            }

            StringSpec spec = s.Spec;
            Vector3 chord = s.B - s.A;

            // The wind at the span: its speed at the flags' height, and where it blows, in the span's frame.
            Vector3 middle = s.A + chord * 0.5f;
            float ground = FlagProps.GroundAt(middle);
            float height = Mathf.Max(middle.y - ground - s.Curve.Deepest, 0.5f);
            float at10 = _wind.SpeedFor(middle, ground);
            float speed = FlagWind.ClothSpeed(at10, height, 0f);
            Vector3 dir = _wind.Mode == Settings.WindModes.EachFlag ? s.Z : new Vector3(Mathf.Sin(_windAngle * Mathf.Deg2Rad), 0f, Mathf.Cos(_windAngle * Mathf.Deg2Rad));
            float across = Vector3.Dot(dir, s.Z), alongWind = Vector3.Dot(dir, s.X);
            float angle = Mathf.Atan2(across, alongWind) * Mathf.Rad2Deg;

            // The swing: blown out across the span, its gusts slow and broad (the big ones of FlagWind).
            float gust = Mathf.Max(0f, 1f + 0.3f * _wind.Gusts * FlagNoise.Value1(_clock * 0.25f + s.Seed, 11));
            s.StepSwing(dt, across * speed * gust);
            s.Steer(dt, angle);
            s.Hang();

            var world = new Bounds(s.Bounds.center + s.A, s.Bounds.size);
            s.Visible = GeometryUtility.TestPlanesAABB(planes, world);
            if (!s.Visible)
                return;

            float distance = Mathf.Max(0.5f, Mathf.Sqrt(world.SqrDistance(eye)));
            float flagPixels = Mathf.Max(spec.Width, spec.Height) / distance * _pixelsPerRadian;
            float spanPixels = Mathf.Max(s.Curve.Level, 0.1f) / distance * _pixelsPerRadian;
            if (spanPixels < 2f)
                return;

            Vector4 highlight = s.Segment != 0 && _hovered == s.Segment ? new Vector4(0.45f, 0.35f, 0.05f, 1f)
                              : s.Segment != 0 && _selected.Contains(s.Segment) ? new Vector4(0.25f, 0.2f, 0.03f, 1f) : Vector4.zero;

            // The cord and the flags as the thread last filled them (the frame before); what they
            // need next is set up for this frame's batch (StringSpan: the thread's part of the frame).
            bool work = s.PrepareCord();
            if (s.CordReady)
                DrawCord(s, highlight);

            if (flagPixels >= DrawPixels && s.Layout.Count > 0)
            {
                bool still = spec.Motion != FlagMotion.Waves || flagPixels < WavePixels;
                int flags;
                if (s.PrepareFlags(SourceFor, _atlas, CountryNames.Order, speed, angle, still, flagPixels, out flags))
                {
                    work = true;
                    _posedSpans++;
                    if (!still && flagPixels < StringMotion.FullRatePixels)
                        _halfSpans++;
                }

                if (s.FlagsReady)
                    DrawFlags(s, flags, still, highlight);
            }

            if (work)
                _works.Add(s);
        }

        private void DrawCord(Span s, Vector4 highlight)
        {
            if (s.CordBlock == null)
                s.CordBlock = new MaterialPropertyBlock();
            Vector4 colour = FlagLook.Linear(s.Spec.Colour);
            s.CordBlock.SetVector(IdRopeColor, colour);
            s.CordBlock.SetVector(IdPoleColor, colour);
            s.CordBlock.SetVector(IdHighlight, highlight);
            Graphics.DrawMesh(s.Cord.Mesh, Matrix4x4.TRS(s.A, Quaternion.identity, Vector3.one), _cordMaterial, _layer, _camera, 0, s.CordBlock, true, true);
        }

        private void DrawFlags(Span s, int flags, bool still, Vector4 highlight)
        {
            if (s.Block == null)
                s.Block = new MaterialPropertyBlock();
            s.Block.SetVector(IdHighlight, highlight);
            Graphics.DrawMesh(s.Flags.Mesh, Matrix4x4.TRS(s.A, Quaternion.identity, Vector3.one), _flagMaterial, _layer, _camera, 0, s.Block, true, true);
            _drawnSpans++;
            _drawnFlags += flags;
            if (still)
                _stillFlags += flags;
            if (!_loggedFirst)
            {
                _loggedFirst = true;
                Log.Msg("strings: first string drawn (" + flags + " flags of " + s.Columns + "x" + s.Rows + " particles, " + s.Spec.Describe() + ")");
            }
        }

        // ---- the sources ----------------------------------------------------------------------

        private StringMotion.Source SourceFor(StringMotion.SourceKey key)
        {
            StringMotion.Source source;
            if (!_sources.TryGetValue(key, out source))
            {
                source = new StringMotion.Source(key, _now);
                _sources[key] = source;
                if (Log.Detailed)
                    Log.Detail("strings: a new shared cloth (" + key + "), " + _sources.Count + " in all");
            }

            source.LastWanted = _now;
            return source;
        }

        /// <summary>
        /// The frame's batch: the sources with steps due, the biggest on the screen first while the
        /// room lasts (<see cref="BatchBudget"/>; the spans' posing is spent first, being what is
        /// drawn), then the spans' work. The sources' jobs go in first: Took reads them back by index.
        /// </summary>
        private void PlanBatch(float dt, float realDt)
        {
            _drop.Clear();
            _due.Clear();
            foreach (KeyValuePair<StringMotion.SourceKey, StringMotion.Source> kv in _sources)
            {
                StringMotion.Source source = kv.Value;
                if (_now - source.LastWanted > SourceIdle)
                {
                    _drop.Add(kv.Key);
                    continue;
                }

                float size;
                int steps = source.Due(dt, MaxSourceSteps, WarmSteps, out size);
                if (steps <= 0)
                    continue;

                _due.Add(new Due { Source = source, Steps = steps, Size = size, Pixels = source.WantedPixels, Warming = !source.Ready });
            }

            foreach (StringMotion.SourceKey k in _drop)
                _sources.Remove(k);

            double budget = BatchBudget * Math.Max(1, _worker.Threads) * Mathf.Clamp(realDt * 60f, 1f, 4f);
            double spent = 0.0;
            foreach (Span s in _works)
                spent += s.PredictedWork * _unitCost;

            _due.Sort(ByPixels);
            foreach (Due d in _due)
            {
                double cost = d.Source.WorkFor(d.Steps, d.Size) * _unitCost;
                // A new source warms up whatever the room (its flags hang from rest until it has); the rest wait their turn.
                if (!d.Warming && spent > 0.0 && spent + cost > budget)
                {
                    _held++;
                    continue;
                }

                spent += cost;
                ClothJob job = d.Source.Commit(d.Steps, d.Size, _wind.Gusts, _wind.FromWeatherMod);
                _planned.Add(d.Source);
                _worker.Add(ref job);
                _stepped += d.Steps;
            }

            foreach (Span s in _works)
            {
                s.WorkOut = true;
                var job = new ClothJob { Work = s.Work };
                _worker.Add(ref job);
            }
        }

        // ---- the log ----------------------------------------------------------------------------

        private void Report()
        {
            if (_now < _nextReport)
                return;

            bool first = _nextReport == 0f;
            _nextReport = _now + 5f;
            if (first || !Log.Detailed || _costFrames == 0)
            {
                _costMs = _waitMs = 0;
                _costFrames = 0;
                _drawnSpans = _drawnFlags = _stillFlags = _stepped = _posedSpans = _halfSpans = _held = 0;
                return;
            }

            int frames = Math.Max(1, _costFrames);
            Log.Detail("strings: " + _spans.Count + " span(s), drawn " + (_drawnSpans / frames) + " a frame with " +
                       (_drawnFlags / frames) + " flags (" + (_stillFlags / frames) + " still: far or not waving), posed " + (_posedSpans / frames) +
                       " a frame (" + (_halfSpans / frames) + " at half rate); " +
                       _sources.Count + " shared cloth(s), " + (_stepped / 5) + " steps/s on " + Math.Max(1, _worker.Threads) + " thread(s) (" +
                       _worker.LastMilliseconds.ToString("F2") + " ms the last batch, " + (_unitCost * 1000.0).ToString("F2") + " us a particle step, " +
                       (_held / frames) + " source(s) a frame held for room); main thread " + (_costMs / frames).ToString("F2") + " ms a frame (" +
                       (_waitMs / frames).ToString("F2") + " of it waiting for the thread); atlas " + _atlas.Count + " design(s)");
            _costMs = _waitMs = 0;
            _costFrames = 0;
            _drawnSpans = _drawnFlags = _stillFlags = _stepped = _posedSpans = _halfSpans = _held = 0;
        }
    }
}
