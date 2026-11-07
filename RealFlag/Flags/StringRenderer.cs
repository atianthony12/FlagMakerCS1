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

        /// <summary>A source not wanted for this long is let go, seconds.</summary>
        public const float SourceIdle = 20f;

        /// <summary>Most steps a source takes a frame (catching up after a slow frame), and while it warms up.</summary>
        public const int MaxSourceSteps = 4, WarmSteps = 12;

        /// <summary>The buffer's segments looked at a frame for spans the renderer does not know yet.</summary>
        public const int ScanPerFrame = 2048;

        private Material _flagMaterial, _cordMaterial;
        private int _layer;
        private Camera _camera;
        private bool _ready;
        private readonly ClothWorker _worker = new ClothWorker();
        private readonly List<StringMotion.Source> _planned = new List<StringMotion.Source>();
        private readonly List<ClothJob> _plannedJobs = new List<ClothJob>();
        private readonly Dictionary<ushort, Span> _spans = new Dictionary<ushort, Span>();
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
        private readonly Stopwatch _cost = new Stopwatch();
        private double _costMs;
        private int _costFrames, _drawnSpans, _drawnFlags, _stillFlags, _stepped;
        private float _nextReport;
        private bool _loggedFirst;

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

            string problem;
            string stepping = _worker.Start(1, out problem) ? "its cloth on a thread of its own" : "its cloth on the main thread (" + problem + ")";
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

            if (!_worker.Wait())
            {
                Log.Warn("strings: the cloth's thread did not come back; strings stop moving");
                _ready = false;
                return;
            }

            // The batch's jobs as the thread left them (in the order they were added: the planned sources').
            ClothJob[] done = _worker.Jobs;
            for (int i = 0; i < _planned.Count && i < _worker.Count; i++)
            {
                ClothJob job = done[i];
                if (job.Failure != null)
                    Log.Warn("strings: a source's step threw: " + job.Failure.Message);
                _planned[i].Took(ref job);
            }

            _planned.Clear();
            _plannedJobs.Clear();
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

                PlanSources(dt);
            }
            catch (Exception e)
            {
                Log.Error("strings: a frame threw.", e);
            }

            _worker.Run();
            _cost.Stop();
            _costMs += _cost.Elapsed.TotalMilliseconds;
            _costFrames++;
            Report();
        }

        /// <summary>The network as it is: spans gone, moved or new; designs for spans made by others.</summary>
        private void Sync()
        {
            // The registry's spans (the tool's, Move It's pastes, the save's): known at once.
            int version = StringRegistry.Version;
            if (version != _registryVersion)
            {
                _registryVersion = version;
                StringRegistry.KeysTo(_keys);
                foreach (ushort k in _keys)
                {
                    if (!_spans.ContainsKey(k) && StringNet.IsSpan(k))
                        Join(k);
                }

                foreach (Span s in _spans.Values)
                {
                    StringSpec spec;
                    if (StringRegistry.TryGet(s.Segment, out spec) && !spec.Same(s.Spec))
                    {
                        bool shape = spec.Along != s.Spec.Along || spec.Hang != s.Spec.Hang || spec.Spacing != s.Spec.Spacing || spec.Gap != s.Spec.Gap ||
                                     spec.Count != s.Spec.Count || spec.Margin != s.Spec.Margin || spec.Sag != s.Spec.Sag;
                        s.Spec = spec;
                        s.DesignsDirty = true;
                        if (shape)
                        {
                            Lay(s);
                            _chainsDirty = true;
                        }
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

            DrawCord(s, highlight);

            if (flagPixels < DrawPixels || s.Layout.Count == 0)
                return;

            bool still = spec.Motion != FlagMotion.Waves || flagPixels < WavePixels;
            DrawFlags(s, speed, angle, still, highlight);
        }

        private void DrawCord(Span s, Vector4 highlight)
        {
            s.BuildCord();
            if (s.CordBlock == null)
                s.CordBlock = new MaterialPropertyBlock();
            Vector4 colour = FlagLook.Linear(s.Spec.Colour);
            s.CordBlock.SetVector(IdRopeColor, colour);
            s.CordBlock.SetVector(IdPoleColor, colour);
            s.CordBlock.SetVector(IdHighlight, highlight);
            Graphics.DrawMesh(s.Cord.Mesh, Matrix4x4.TRS(s.A, Quaternion.identity, Vector3.one), _cordMaterial, _layer, _camera, 0, s.CordBlock, true, true);
        }

        private void DrawFlags(Span s, float speed, float angle, bool still, Vector4 highlight)
        {
            int posed;
            if (!s.PoseFlags(SourceFor, _atlas, CountryNames.Order, speed, angle, still, out posed))
                return;

            if (s.Block == null)
                s.Block = new MaterialPropertyBlock();
            s.Block.SetVector(IdHighlight, highlight);
            Graphics.DrawMesh(s.Flags.Mesh, Matrix4x4.TRS(s.A, Quaternion.identity, Vector3.one), _flagMaterial, _layer, _camera, 0, s.Block, true, true);
            _drawnSpans++;
            _drawnFlags += posed;
            if (still)
                _stillFlags += posed;
            if (!_loggedFirst)
            {
                _loggedFirst = true;
                Log.Msg("strings: first string drawn (" + posed + " flags of " + s.Columns + "x" + s.Rows + " particles, " + s.Spec.Describe() + ")");
            }
        }

        // ---- the sources ----------------------------------------------------------------------

        private StringMotion.Source SourceFor(StringMotion.SourceKey key)
        {
            StringMotion.Source source;
            if (!_sources.TryGetValue(key, out source))
            {
                source = new StringMotion.Source(key, _now);
                source.Cloth.SelfCollision = Settings.SelfCollision == null || Settings.SelfCollision.value;
                _sources[key] = source;
                if (Log.Detailed)
                    Log.Detail("strings: a new shared cloth (" + key + "), " + _sources.Count + " in all");
            }

            source.LastWanted = _now;
            return source;
        }

        private void PlanSources(float dt)
        {
            _drop.Clear();
            foreach (KeyValuePair<StringMotion.SourceKey, StringMotion.Source> kv in _sources)
            {
                StringMotion.Source source = kv.Value;
                if (_now - source.LastWanted > SourceIdle)
                {
                    _drop.Add(kv.Key);
                    continue;
                }

                ClothJob job;
                if (!source.Plan(dt, MaxSourceSteps, WarmSteps, _wind.Gusts, _wind.FromWeatherMod, out job))
                    continue;

                _planned.Add(source);
                _plannedJobs.Add(job);
                _stepped += job.Steps;
            }

            foreach (StringMotion.SourceKey k in _drop)
                _sources.Remove(k);

            // The worker keeps its own copies: the planned jobs come back through it.
            for (int i = 0; i < _plannedJobs.Count; i++)
            {
                ClothJob job = _plannedJobs[i];
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
                _costMs = 0;
                _costFrames = 0;
                _drawnSpans = _drawnFlags = _stillFlags = _stepped = 0;
                return;
            }

            Log.Detail("strings: " + _spans.Count + " span(s), drawn " + (_drawnSpans / Math.Max(1, _costFrames)) + " a frame with " +
                       (_drawnFlags / Math.Max(1, _costFrames)) + " flags (" + (_stillFlags / Math.Max(1, _costFrames)) + " still: far or not waving); " +
                       _sources.Count + " shared cloth(s), " + (_stepped / 5) + " steps/s on the thread (" + _worker.LastMilliseconds.ToString("F2") +
                       " ms the last batch); main thread " + (_costMs / _costFrames).ToString("F2") + " ms a frame; atlas " + _atlas.Count + " design(s)");
            _costMs = 0;
            _costFrames = 0;
            _drawnSpans = _drawnFlags = _stillFlags = _stepped = 0;
        }
    }
}
