using System;
using System.Collections.Generic;
using System.Diagnostics;
using ColossalFramework;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>One flag as the renderer keeps it: its prop, its design, its cloth and mesh.</summary>
    public sealed class FlagInstance
    {
        /// <summary>The prop's index; 0 for the placement ghost.</summary>
        public uint Prop;
        public FlagSpec Spec;
        public FlagCloth Cloth;
        public FlagMesh Mesh;

        /// <summary>The cloth's detail (cells along the longer side): as fine as the flag's size on the screen needs, up to the option's.</summary>
        public int Detail = FlagCloth.CellsAlong;
        public readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

        /// <summary>World: the top hoist corner, the prop's angle, the ground under it.</summary>
        public Vector3 Anchor;
        public float Angle;
        public float Ground;
        public bool HasAnchor;

        public bool Visible;
        public float Distance;
        public Bounds World;

        /// <summary>Time not yet stepped, seconds, and the length of this flag's steps (1/60 s, 1/30 s far out).</summary>
        public float Pending;
        public float StepSize = FlagRenderer.Step;

        /// <summary>Shown once its first seconds have been run (SettleLeft of them are still to run, on SettleClock).</summary>
        public bool Settled;
        public float SettleLeft;
        public float SettleClock;

        /// <summary>The cloth's thread refreshed the mesh's arrays (at ShownAlpha): they go to the GPU when it is next drawn.</summary>
        public bool Fresh;
        public float ShownAlpha = 1f;

        /// <summary>The main thread moved the cloth since the arrays were refreshed (its anchor moved): refreshed again when drawn.</summary>
        public bool MeshDirty = true;

        /// <summary>Over the cloth thread's budget this frame: left as it is until the nearer flags leave room.</summary>
        public bool Waiting;

        /// <summary>The design went into the block (it changes rarely; the glow each frame while the flag is hovered).</summary>
        public bool BlockReady;
        public bool Highlighted;

        public readonly PickShape[] Picks = { new PickShape(), new PickShape() };
        public int PickNext;
        public float PickAge = 1e6f;

        /// <summary>The hoist's line and the fly at rest (FlagOrientation), from the prop's angle and the spec's tilt and lean.</summary>
        public Vector3 Hoist = Vector3.down;
        public Vector3 Fly = Vector3.right;

        /// <summary>Drawn cells a cloth cell (FlagMesh.Refine), as the flag's size on the screen asks.</summary>
        public int Refine = 1;

        /// <summary>
        /// Real time until which a paused game still steps this flag (it is being handled:
        /// moved, turned, redesigned), and how far its own clock has run ahead of the city's while
        /// the city was paused.
        /// </summary>
        public float AwakeUntil;
        public float OwnTime;

        /// <summary>The pole the mod draws under it (Spec.Pole): its numbers and mesh, its colours.</summary>
        public FlagPoleShape PoleShape;
        public Mesh PoleMesh;
        public readonly MaterialPropertyBlock PoleBlock = new MaterialPropertyBlock();
        public bool PoleBlockReady;
        public bool PoleVisible;

        /// <summary>
        /// The pole's length along its axis to what it stands on: cast on the simulation thread
        /// (FlagTool.CastAlong) from where the axis crosses the anchor's level, again whenever
        /// that point or the axis moves. Negative until known.
        /// </summary>
        public float PoleLength = -1f;
        public Vector3 PoleCastFrom;
        public Vector3 PoleCastAlong;
        public int PoleCastAsked;
        public volatile int PoleCastDone;
        public float PoleCastResult;

        /// <summary>The cloth keeps off the current pole (it has been told: FlagPoleShape.ApplyTo).</summary>
        public bool PoleApplied;
    }

    /// <summary>A flag as the panel lists it.</summary>
    public struct FlagEntry
    {
        public uint Prop;
        public FlagSpec Spec;
        public Vector3 Anchor;
        public float Angle;
        public float Ground;
    }

    /// <summary>
    /// Finds the city's flag props, moves their cloth, and draws them, every frame. One per
    /// loaded city, on the controller.
    /// </summary>
    /// <remarks>
    /// The props are the game's: Move It, the bulldozer, Find It and the mod's own tool make,
    /// move, turn and delete them, so the renderer re-reads every flag's prop every frame and
    /// follows. New flag props are found by a scan of the prop buffer spread over frames (a
    /// sixteenth a frame); a prop with no design gets the Create new tab's.
    ///
    /// The cloth steps at 60 Hz on each flag's own clock (FlagCloth.SubStepsFor splits the step
    /// in a strong wind), drawn between its last two steps (FlagMesh.Refresh); a flag steps only
    /// while it is in view and near enough to see move, and at half the rate further out. The
    /// clock runs in real time -- the wind does not blow four times harder at the game's
    /// fastest speed -- and stops while the game is paused if the player wants a still.
    ///
    /// The stepping is done on threads of its own (ClothWorker) while the game draws the frame:
    /// each frame waits for the last batch, takes its meshes, then plans the next -- the flags
    /// nearest first, within <see cref="BatchBudget"/> of each thread's time, measured as it goes
    /// (the farthest wait, frozen, when there is not room for all). A new flag is first run for
    /// <see cref="SettleTime"/>, a little each frame, so it appears hanging or flying.
    ///
    /// Each flag's cloth is as fine as its size on the screen needs (a Normal cell over
    /// <see cref="PixelsPerCell"/> pixels asks for the next finer detail), up to the option's
    /// finest: the far flags of a city cost what they always did. A flag changing detail takes
    /// its old cloth's shape and motion (FlagCloth.TakeShape), so nothing jumps.
    ///
    /// Drawn with Graphics.DrawMesh for the main camera, on the layer the game's props are
    /// drawn on, casting and receiving shadows, through FlagCloth.shader's legacy deferred
    /// passes (the game's lights, its fog and depth of field see it).
    /// </remarks>
    public sealed class FlagRenderer : MonoBehaviour
    {
        public static FlagRenderer Instance { get; private set; }

        /// <summary>One cloth step, seconds (FlagMesh draws between steps).</summary>
        public const float Step = 1f / 60f;

        /// <summary>Beyond this, not drawn (a 3 m flag is under two pixels at 2 km).</summary>
        public const float DrawDistance = 2500f;

        /// <summary>Beyond this, still drawn, not moved.</summary>
        public const float SimDistance = 1500f;

        /// <summary>Beyond this, moved at half the rate (1/30 s steps), with a tenth either side before a flag changes.</summary>
        public const float HalfRateDistance = 400f;

        /// <summary>At most this many flags move in one frame, the nearest first.</summary>
        public const int MaxMoving = 48;

        /// <summary>
        /// The cloth thread's work in one frame, milliseconds: under half a frame at 60 fps, so
        /// the main thread does not wait for it, and under half a core. Planned from the measured
        /// cost of a particle step: in the GPU preview's Unity 5.6 Mono, room for 21 flags of
        /// 3 x 2 m in a light wind, 11 at 12 m/s (two sub-steps), 5 at 30 (four).
        /// </summary>
        public const double BatchBudget = 8.0;

        /// <summary>The frame's budget of thread time: <see cref="BatchBudget"/> on each of the cloth's threads.</summary>
        private double Budget
        {
            get { return BatchBudget * Math.Max(1, _worker.Threads); }
        }

        /// <summary>
        /// A particle step's cost until one is measured, milliseconds: the GPU preview's Unity 5.6
        /// editor (Mono) steps a 3 x 2 m flag's 247 particles and refreshes its mesh in 0.37 ms
        /// (gpu-render.ps1 -Bench).
        /// </summary>
        private const double FirstGuessCost = 0.0015;

        /// <summary>At most this many cloth steps a flag catches up in one frame (a hitch is not worth a burst of physics).</summary>
        private const int MaxStepsPerFrame = 3;

        /// <summary>
        /// A new cloth is run this long before it is first shown, to settle, in 1/30 s steps:
        /// <see cref="SettleSteps"/> of them a frame (fewer sub-steps' worth in a strong wind), on
        /// at most <see cref="MaxSettlesPerFrame"/> flags a frame.
        /// </summary>
        private const float SettleTime = 1.5f;
        private const int SettleSteps = 6;
        private const int MaxSettlesPerFrame = 2;

        /// <summary>How often a flag's pick sheet is refreshed, seconds.</summary>
        private const float PickInterval = 0.1f;

        /// <summary>
        /// A flag is given the coarsest detail whose cells are at most this many pixels on the
        /// screen (never coarser than Normal, never finer than the option): with a 45 deg camera on
        /// a 2160-pixel screen, a 3 m flag gets High within 87 m, Very high within 65 m, Ultra
        /// within 49 m -- a 15 m flag five times as far, a 1080-pixel screen half. The first frame
        /// logs the camera's own figures. With a band of 15% either way before it changes back,
        /// and a few changes a frame at most (a new cloth and mesh each).
        /// </summary>
        public const float PixelsPerCell = 5f;
        private const float DetailBand = 1.15f;
        private const int MaxDetailChanges = 2;

        /// <summary>
        /// A drawn cell is kept to about this many pixels (FlagMesh.Refine: up to four drawn cells a
        /// cloth cell each way, the smooth surface through the particles), with a band of 15%
        /// before it changes, a few changes a frame. A 30 m flag at Very high 105 m off, 684 px
        /// long, drawn a vertex a particle, was a polygon of 21-pixel sides; drawn four to a cell,
        /// 5 px.
        /// </summary>
        public const float RefinePixels = 5f;
        private const float RefineBand = 1.15f;
        private const int MaxRefineChanges = 4;

        /// <summary>
        /// A paused game's handled flag (moved, turned, redesigned) is stepped this long after the
        /// last change, its time slowing to nothing over the last <see cref="WakeFade"/> of it
        /// (ClothPlan.PausedTime): the first version stepped nothing while paused, so a flag
        /// dragged by Move It left its cloth behind its hoist.
        /// </summary>
        public const float WakeTime = 2.0f;
        public const float WakeFade = 0.6f;

        /// <summary>A pole's foot is looked for this far down its axis; nothing within it: FlagPoleShape.Fallback.</summary>
        private const float PoleCastLength = 1000f;

        /// <summary>Props scanned per frame: the whole buffer every sixteen frames.</summary>
        private const int ScanPerFrame = 4096;

        private readonly Dictionary<uint, FlagInstance> _byProp = new Dictionary<uint, FlagInstance>();
        private readonly List<FlagInstance> _flags = new List<FlagInstance>();
        private readonly List<uint> _gone = new List<uint>();
        private readonly HashSet<uint> _heightFixed = new HashSet<uint>();

        /// <summary>The placement ghost, and the tool's request for it (made in its Update, applied in LateUpdate between batches).</summary>
        private FlagInstance _ghost;
        private bool _ghostWanted;
        private Vector3 _ghostAnchor;
        private float _ghostAngle;
        private FlagSpec _ghostSpec;

        private Material _material;
        private Material _poleMaterial;
        private Camera _camera;
        private int _layer;
        private bool _ready;
        private int _scanCursor = 1;
        private int _refineChanges;
        private int _castSerial;

        /// <summary>The cloth's thread, the flags of its batch (in the batch's order), and the flags in view nearest first.</summary>
        private readonly ClothWorker _worker = new ClothWorker();
        private readonly List<FlagInstance> _batch = new List<FlagInstance>();
        private FlagInstance[] _order = new FlagInstance[64];
        private int _orderUsed;

        /// <summary>
        /// A particle step's cost on the cloth's threads, milliseconds: averaged as batches come
        /// back, a sample per 2000 particle steps (one flag's batch is a tenth of that), and in
        /// total for the log's line.
        /// </summary>
        private double _unitCost = FirstGuessCost;
        private long _sampleWork, _measuredWork;
        private double _sampleMs, _measuredMs;
        private bool _loggedCost;
        private int _failures;

        /// <summary>The options changed (FlagRenderer.ClothOptionsChanged): applied after the next Collect.</summary>
        private bool _optionsChanged;
        private bool _selfCollision = true;
        private int _finest = FlagCloth.CellsAlong;
        private float _pixelsPerRadian = 1000f;
        private int _detailChanges;

        private float _clock;
        private readonly Stopwatch _cost = new Stopwatch();
        private readonly Stopwatch _wait = new Stopwatch();
        private double _costMs, _waitMs, _batchMs, _batchWallMs;
        private int _costFrames, _moving, _waiting;
        private float _nextReport;
        private int _stepsThisReport;
        private bool _loggedFirstDraw;
        private int _loggedCamera = -1;

        // ---- lifetime -------------------------------------------------------------------------

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            Shader shader = ShaderBundle.Get(ShaderBundle.Cloth);
            if (shader == null)
            {
                Log.Error("flags: the cloth shader is not available here; flags are not drawn (see 'shader bundle' above)");
                return;
            }

            _material = new Material(shader) { name = "RealFlag cloth" };
            Shader pole = ShaderBundle.Get(ShaderBundle.Pole);
            if (pole != null)
                _poleMaterial = new Material(pole) { name = "RealFlag pole" };
            else
                Log.Warn("flags: the pole's shader is not in the bundle; poles are not drawn");
            FlagImages.Warn = Log.Warn;
            _layer = PropLayer();
            _ready = true;

            ReadOptions();

            string problem;
            int threads = ClothWorker.ThreadsFor(SystemInfo.processorCount);
            string stepping = _worker.Start(threads, out problem)
                ? "cloth stepped on " + (_worker.Threads == 1 ? "a thread of its own" : _worker.Threads + " threads of its own") +
                  " (" + SystemInfo.processorCount + " processors here)" + (problem != null ? " -- " + (threads - _worker.Threads) + " did not start: " + problem : "")
                : "cloth stepped on the main thread (its own threads did not start: " + problem + ")";

            Log.Msg("flags: renderer ready -- shader '" + shader.name + "' (" + _material.passCount + " passes), layer " + _layer +
                    " (" + LayerMask.LayerToName(_layer) + "), " + FlagRegistry.Count + " saved designs, prefab " +
                    (FlagPrefab.Info != null ? "index " + FlagPrefab.Info.m_prefabDataIndex : "MISSING") + ", " + stepping +
                    "; cloth detail up to " + UI.SettingsCatalog.DetailLogName(_finest) + ", self-collision " + (_selfCollision ? "on" : "off"));
        }

        /// <summary>The cloth's options changed (the panel, the options page, a reset): the flags follow from the next frame.</summary>
        public static void ClothOptionsChanged()
        {
            if (Instance != null)
                Instance._optionsChanged = true;
        }

        /// <summary>The options as they are now: the finest detail and self-collision.</summary>
        private void ReadOptions()
        {
            _finest = FlagCloth.ClampDetail(Settings.ClothDetail != null ? Settings.ClothDetail.value : Settings.Defaults.ClothDetail);
            _selfCollision = Settings.SelfCollision == null || Settings.SelfCollision.value;
        }

        /// <summary>After a change of options, between batches: every cloth takes the new self-collision (the details follow in Refresh).</summary>
        private void ApplyOptions()
        {
            if (!_optionsChanged)
                return;

            _optionsChanged = false;
            ReadOptions();
            foreach (FlagInstance f in _flags)
            {
                if (f.Cloth != null)
                    f.Cloth.SelfCollision = _selfCollision;
            }

            if (_ghost != null && _ghost.Cloth != null)
                _ghost.Cloth.SelfCollision = _selfCollision;
        }

        private void OnDestroy()
        {
            // First: nothing may still be stepping a cloth whose mesh is about to go.
            if (!_worker.Stop())
                Log.Warn("flags: the cloth's thread did not stop within 2 s; it ends with the game (a background thread)");

            foreach (FlagInstance f in _flags)
                Release(f);
            _flags.Clear();
            _byProp.Clear();

            if (_ghost != null)
                Release(_ghost);
            _ghost = null;

            FlagPicking.Clear();

            if (Log.Detailed)
                Log.Detail("flags: " + FlagImages.Describe() + " let go with the city");
            FlagImages.Clear();

            if (_material != null)
                Destroy(_material);
            if (_poleMaterial != null)
                Destroy(_poleMaterial);

            if (Instance == this)
                Instance = null;
        }

        private static void Release(FlagInstance f)
        {
            if (f.Mesh != null)
                f.Mesh.Destroy();
            f.Mesh = null;
            f.Cloth = null;
            DropPole(f);
            if (f.Prop != 0)
                FlagPicking.Publish(f.Prop, null);
        }

        private static void DropPole(FlagInstance f)
        {
            if (f.PoleMesh != null)
                Destroy(f.PoleMesh);
            f.PoleMesh = null;
            f.PoleApplied = false;
            if (f.Cloth != null)
            {
                // No pole, no ropes: the hoist held along its line again.
                f.Cloth.ClearPole();
                f.Cloth.SetHalyard(false);
            }
        }

        /// <summary>
        /// The Unity layer the game draws its props on (PrefabInfo.m_prefabDataLayer is the prefab's
        /// GameObject's layer): the main camera renders it and the sun's shadow sees it.
        /// </summary>
        private static int PropLayer()
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
                Log.Warn("flags: could not read the props' layer: " + e.Message);
            }

            return 0;
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

            // The batch planned last frame: done, as a rule, while the game drew.
            if (!Collect())
                return;

            float dt = Mathf.Clamp(Time.deltaTime, 0f, 0.25f);
            float realDt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.25f);
            bool frozen = Paused && (Settings.FreezeWhenPaused == null || Settings.FreezeWhenPaused.value);
            if (frozen)
                dt = 0f;
            _clock += dt;

            try
            {
                ApplyOptions();
                Scan();
                SetSun();

                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_camera);
                Vector3 eye = _camera.transform.position;
                Wind wind = Wind.Current();
                _pixelsPerRadian = _camera.pixelHeight / (2f * Mathf.Tan(0.5f * Mathf.Clamp(_camera.fieldOfView, 1f, 170f) * Mathf.Deg2Rad));
                Shader.SetGlobalFloat(FlagPoleMesh.IdPixelRadius, 0.6f / Mathf.Max(_pixelsPerRadian, 1f));
                _detailChanges = 0;
                _refineChanges = 0;
                LogCamera();

                ApplyGhost(eye, planes);
                Refresh(eye, planes);

                foreach (FlagInstance f in _flags)
                    Draw(f);

                if (_ghost != null)
                    Draw(_ghost);

                Plan(dt, frozen, realDt, wind);
            }
            catch (Exception e)
            {
                Log.Error("flags: the frame threw; flags are paused until the next frame.", e);
            }

            _worker.Run();
            _cost.Stop();
            Report();
        }

        /// <summary>
        /// Waits for the cloth's thread, then takes its batch: the meshes it refreshed, the cloths
        /// that went wrong, the time it took (the cost of a particle step, for the next plans).
        /// False if the thread is stuck (the renderer then stops).
        /// </summary>
        private bool Collect()
        {
            _wait.Reset();
            _wait.Start();
            bool done = _worker.Wait();
            _wait.Stop();
            _waitMs += _wait.Elapsed.TotalMilliseconds;

            if (!done)
            {
                // Its cloths are still its own: the flags stop here, and say why.
                Log.Error("flags: the cloth's thread has not finished a batch in " + (ClothWorker.WaitLimit / 1000) +
                          " s; the flags are no longer drawn or moved in this city");
                _ready = false;
                return false;
            }

            int count = Math.Min(_batch.Count, _worker.Count);
            ClothJob[] jobs = _worker.Jobs;
            for (int i = 0; i < count; i++)
            {
                FlagInstance f = _batch[i];
                if (jobs[i].Cloth != f.Cloth)
                    continue;

                if (jobs[i].Failure != null)
                {
                    // A job that throws is a bug: said once with its stack, then counted.
                    if (_failures++ == 0)
                        Log.Error("flags: stepping flag #" + f.Prop + "'s cloth threw; laid out again (further ones are counted in the detailed log)",
                                  jobs[i].Failure);
                    f.Cloth.Reset(jobs[i].DirX, jobs[i].DirZ);
                    f.MeshDirty = true;
                    continue;
                }

                if (jobs[i].WentWrong)
                    Log.Warn("flags: flag #" + f.Prop + "'s cloth went wrong (not a number); laid out again");

                if (jobs[i].Mesh != null)
                {
                    f.Fresh = true;
                    f.ShownAlpha = jobs[i].Alpha;
                }

                _stepsThisReport += jobs[i].Steps;
            }

            if (_worker.Count > 0)
            {
                long work = _worker.LastParticleSteps;
                double ms = _worker.LastMilliseconds;
                _batchMs += ms;
                _batchWallMs += _worker.LastWallMilliseconds;

                // The cost of a particle step, a sample per 2000 particle steps however many
                // batches that takes (a slow average: one batch the OS held up must not halve
                // the flags that move). The first version sampled only batches of 2000 or more,
                // which one or two flags never make: the plans kept the first guess.
                _sampleWork += work;
                _sampleMs += ms;
                if (_sampleWork >= 2000)
                {
                    double sample = Math.Max(1e-5, Math.Min(_sampleMs / _sampleWork, 0.05));
                    _unitCost = 0.9 * _unitCost + 0.1 * sample;
                    _measuredWork += _sampleWork;
                    _measuredMs += _sampleMs;
                    _sampleWork = 0;
                    _sampleMs = 0.0;
                }

                if (!_loggedCost && _measuredWork >= 300000)
                {
                    _loggedCost = true;
                    double us = 1000.0 * _measuredMs / _measuredWork;
                    Log.Msg("flags: the cloth costs " + us.ToString("F2") + " us a particle step here (" + (us * 247 / 1000.0).ToString("F2") +
                            " ms for a 3 x 2 m flag's step at Normal detail) on " +
                            (_worker.Threaded ? _worker.Threads + " thread(s) of its own" : "the main thread") + "; room for " +
                            (Budget / (us * 247 / 1000.0)).ToString("F0") + " such steps a frame");
                }
            }

            _worker.Clear();
            _batch.Clear();
            return true;
        }

        private static bool Paused
        {
            get
            {
                SimulationManager sim = Singleton<SimulationManager>.instance;
                return sim != null && (sim.SimulationPaused || sim.ForcedSimulationPaused);
            }
        }

        /// <summary>
        /// A sixteenth of the prop buffer: flag props the renderer does not know yet join it. The
        /// ones it knows are checked every frame in <see cref="Refresh"/>.
        /// </summary>
        private void Scan()
        {
            if (FlagPrefab.Info == null)
                return;

            PropInstance[] buffer = Singleton<PropManager>.instance.m_props.m_buffer;
            int end = Math.Min(buffer.Length, _scanCursor + ScanPerFrame);

            for (int i = _scanCursor; i < end; i++)
            {
                if ((buffer[i].m_flags & (ushort)PropInstance.Flags.Created) == 0 || !FlagPrefab.IsFlagIndex(buffer[i].m_infoIndex))
                    continue;

                uint prop = (uint)i;
                if (_byProp.ContainsKey(prop))
                    continue;

                Add(prop);
            }

            _scanCursor = end >= buffer.Length ? 1 : end;
        }

        private FlagInstance Add(uint prop)
        {
            FlagSpec spec;
            if (!FlagRegistry.TryGet(prop, out spec))
            {
                // Placed without the mod's tool (Find It, the prop tool, a Move It paste of a
                // flag copied before the mod was there): the Create new design.
                spec = Settings.NewSpec();
                FlagRegistry.Set(prop, spec);
                Log.Msg("flags: flag prop #" + prop + " had no design; given the Create new tab's (" + spec.Describe() + ")");
            }

            var f = new FlagInstance { Prop = prop, Spec = spec };
            _byProp[prop] = f;
            _flags.Add(f);

            if (Log.Detailed)
                Log.Detail("flags: flag #" + prop + " found: " + spec.Describe());
            return f;
        }

        /// <summary>Every known flag against its prop: gone, moved, turned, redesigned.</summary>
        private void Refresh(Vector3 eye, Plane[] planes)
        {
            _gone.Clear();

            foreach (FlagInstance f in _flags)
            {
                Vector3 anchor;
                float angle;
                bool fixedHeight;
                if (!FlagProps.TryRead(f.Prop, out anchor, out angle, out fixedHeight))
                {
                    _gone.Add(f.Prop);
                    continue;
                }

                // Placed by a tool that puts props on the ground: raise it to the Create new height,
                // once, or the flag hangs underground.
                if (!fixedHeight && _heightFixed.Add(f.Prop))
                {
                    float height = Settings.NewHeight != null ? Settings.NewHeight.value : Settings.Defaults.Height;
                    FlagProps.FixHeight(f.Prop, height);
                    Log.Msg("flags: flag #" + f.Prop + " stood on the ground; raised to " + height.ToString("F1") + " m and fixed there");
                }

                FlagSpec spec;
                if (FlagRegistry.TryGet(f.Prop, out spec) && !Same(spec, f.Spec))
                    SetSpec(f, spec);

                Place(f, anchor, angle);
                Cull(f, eye, planes);
                ChooseDetail(f);
                ChooseRefine(f);
                UpdatePole(f, planes);
            }

            foreach (uint prop in _gone)
                Remove(prop);
        }

        /// <summary>A paused game steps this flag for a moment: it is being handled (ClothPlan.PausedTime).</summary>
        private static void Wake(FlagInstance f)
        {
            f.AwakeUntil = Time.realtimeSinceStartup + WakeTime;
        }

        /// <summary>
        /// How finely the flag is drawn: drawn cells of about <see cref="RefinePixels"/> on the
        /// screen (FlagMesh.Refine), with <see cref="RefineBand"/> either way before it changes, a
        /// few changes a frame (a new grid each).
        /// </summary>
        private void ChooseRefine(FlagInstance f)
        {
            if (!f.Visible || f.Cloth == null || f.Mesh == null)
                return;

            float cell = Mathf.Max(f.Cloth.CellU, f.Cloth.CellV);
            float want = cell / Mathf.Max(f.Distance, 1f) * _pixelsPerRadian / RefinePixels;
            int current = f.Mesh.Refine;
            int wanted = current;
            if (want > current * RefineBand)
                wanted = Mathf.CeilToInt(want);
            else if (current > 1 && want < (current - 1) / RefineBand)
                wanted = Mathf.Max(1, Mathf.CeilToInt(want));

            wanted = FlagMesh.RefineFor(f.Cloth, wanted);
            if (wanted == current || _refineChanges >= MaxRefineChanges)
                return;

            _refineChanges++;
            f.Mesh.Destroy();
            f.Mesh = new FlagMesh(f.Cloth, wanted);
            f.Refine = wanted;
            f.Fresh = false;
            f.MeshDirty = true;
        }

        /// <summary>
        /// The pole under a flag that has one: its foot found (cast down its axis on the
        /// simulation thread when the axis moved), its mesh made again when its size changed, the
        /// cloth told to keep off it; and whether it is in view (it can be when the flag is not).
        /// A flag without one has its mesh and collider dropped.
        /// </summary>
        private void UpdatePole(FlagInstance f, Plane[] planes)
        {
            if (!f.Spec.HasPole || _poleMaterial == null)
            {
                if (f.PoleMesh != null || f.PoleApplied)
                    DropPole(f);
                f.PoleVisible = false;
                return;
            }

            // Where the axis crosses the anchor's level, from the last shape's offset (a
            // centimetre or two either way until the first cast is back).
            float offset = f.PoleMesh != null ? f.PoleShape.Offset : FlagPoleShape.For(f.Spec, Mathf.Max(f.Anchor.y - f.Ground, 1f)).Offset;
            Vector3 from = f.Anchor - f.Fly * offset;
            if (f.PoleCastAsked == 0 || (from - f.PoleCastFrom).sqrMagnitude > 1e-4f || (f.Hoist - f.PoleCastAlong).sqrMagnitude > 1e-6f)
                CastPole(f, from);

            int done = f.PoleCastDone;
            if (done != 0 && done == f.PoleCastAsked)
                f.PoleLength = f.PoleCastResult;
            else if (f.PoleLength < 0f)
                f.PoleLength = f.Hoist.y < -0.05f ? Mathf.Max(f.Anchor.y - f.Ground, 0f) / -f.Hoist.y : FlagPoleShape.Fallback(f.Spec);

            FlagPoleShape shape = FlagPoleShape.For(f.Spec, f.PoleLength);
            if (f.PoleMesh == null || !shape.Same(f.PoleShape))
            {
                f.PoleMesh = FlagPoleMesh.Build(shape, f.PoleMesh);
                f.PoleShape = shape;
                f.PoleApplied = false;
            }

            if (f.Cloth != null && !f.PoleApplied)
            {
                f.PoleShape.ApplyTo(f.Cloth, f.Fly.x, f.Fly.y, f.Fly.z);
                f.PoleApplied = true;
            }

            // The pole's box: its axis from the foot to the tip, and a metre round it.
            Vector3 foot = f.Anchor - f.Fly * shape.Offset + f.Hoist * shape.Length;
            Vector3 tip = foot - f.Hoist * shape.Tip;
            var box = new Bounds((foot + tip) * 0.5f, Vector3.zero);
            box.Encapsulate(foot);
            box.Encapsulate(tip);
            box.Expand(2f * shape.CollarRadius + 1f);
            f.PoleVisible = Vector3.Distance(_camera.transform.position, box.ClosestPoint(_camera.transform.position)) < DrawDistance &&
                            GeometryUtility.TestPlanesAABB(planes, box);
        }

        /// <summary>
        /// Asks the simulation thread how far down the pole's axis, from <paramref name="from"/>, the
        /// first ground, roof or road is: the pole's length. The answer lands in the flag
        /// (PoleCastResult, then PoleCastDone: read in that order by the main thread).
        /// </summary>
        private void CastPole(FlagInstance f, Vector3 from)
        {
            int serial = ++_castSerial;
            if (serial == 0)
                serial = ++_castSerial;
            f.PoleCastAsked = serial;
            f.PoleCastFrom = from;
            f.PoleCastAlong = f.Hoist;
            Vector3 along = f.Hoist;
            FlagSpec spec = f.Spec;

            try
            {
                Singleton<SimulationManager>.instance.AddAction(() =>
                {
                    float distance;
                    bool hit;
                    try
                    {
                        hit = FlagTool.CastAlong(from, along, PoleCastLength, out distance);
                    }
                    catch (Exception)
                    {
                        hit = false;
                        distance = 0f;
                    }

                    f.PoleCastResult = hit ? distance : FlagPoleShape.Fallback(spec);
                    f.PoleCastDone = serial;
                });
            }
            catch (Exception e)
            {
                Log.Warn("flags: could not ask where flag #" + f.Prop + "'s pole stands: " + e.Message);
            }
        }

        /// <summary>
        /// The detail the flag's size on the screen asks for (<see cref="PixelsPerCell"/>), with
        /// <see cref="DetailBand"/> either way before it changes; a cloth already made is made
        /// again at it, in its old one's shape and motion, <see cref="MaxDetailChanges"/> a frame.
        /// Flags out of view keep theirs (they are not stepped).
        /// </summary>
        private void ChooseDetail(FlagInstance f)
        {
            if (!f.Visible || (f.Cloth != null && !f.Settled))
                return;

            float size = Mathf.Max(f.Spec.Length, f.Spec.Height);
            float pixels = size / Mathf.Max(f.Distance, 1f) * _pixelsPerRadian;
            int finer = DetailFor(pixels / DetailBand), coarser = DetailFor(pixels * DetailBand);
            int wanted = f.Detail < finer ? finer : f.Detail > coarser ? coarser : f.Detail;
            if (wanted == f.Detail)
                return;

            // The same grid at both (a small flag's cells are at their smallest already): nothing to remake.
            int columns, rows;
            FlagCloth.GridFor(f.Spec.Length, f.Spec.Height, wanted, out columns, out rows);
            if (f.Cloth == null || (columns == f.Cloth.Columns && rows == f.Cloth.Rows))
            {
                f.Detail = wanted;
                return;
            }

            if (_detailChanges >= MaxDetailChanges)
                return;

            _detailChanges++;
            FlagCloth old = f.Cloth;
            var cloth = new FlagCloth(old.Length, old.Height, old.Fabric, 1f, 0f, wanted);
            cloth.TakeShape(old, true);
            cloth.GroundY = old.GroundY;
            cloth.SelfCollision = old.SelfCollision;

            if (f.Mesh != null)
                f.Mesh.Destroy();
            f.Cloth = cloth;
            f.Mesh = new FlagMesh(cloth, f.Refine);
            f.Refine = f.Mesh.Refine;
            f.Detail = wanted;
            f.Fresh = false;
            f.MeshDirty = true;
            f.PoleApplied = false;

            if (Log.Detailed)
                Log.Detail("flags: flag #" + f.Prop + " now at " + UI.SettingsCatalog.DetailLogName(wanted) + " (" + cloth.Columns + "x" +
                           cloth.Rows + "), " + pixels.ToString("F0") + " px long at " + f.Distance.ToString("F0") + " m");
        }

        /// <summary>Once, and again when the screen or the camera's angle changes: where a 3 m flag changes detail here.</summary>
        private void LogCamera()
        {
            int key = _camera.pixelHeight * 1000 + Mathf.RoundToInt(_camera.fieldOfView * 10f);
            if (key == _loggedCamera)
                return;

            _loggedCamera = key;
            string text = string.Empty;
            int[] details = FlagCloth.Details;
            for (int i = 1; i < details.Length; i++)
            {
                // A detail is taken from where the cells of the one below it pass PixelsPerCell.
                float within = 3f * _pixelsPerRadian / (details[i - 1] * PixelsPerCell);
                text += (text.Length > 0 ? ", " : "") + UI.SettingsCatalog.DetailLogName(details[i]) + " within " + within.ToString("F0") + " m";
            }

            Log.Msg("flags: camera " + _camera.pixelHeight + " px high, " + _camera.fieldOfView.ToString("F1") + " deg: a 3 m flag gets " + text +
                    " (the option allows up to " + UI.SettingsCatalog.DetailLogName(_finest) + ")");
        }

        /// <summary>The coarsest detail whose cells are at most <see cref="PixelsPerCell"/> for a flag this many pixels long, within the option's finest.</summary>
        private int DetailFor(float pixels)
        {
            float cells = pixels / PixelsPerCell;
            int[] details = FlagCloth.Details;
            for (int i = 0; i < details.Length; i++)
            {
                if (details[i] >= cells || details[i] >= _finest)
                    return Math.Min(details[i], _finest);
            }

            return _finest;
        }

        private void Remove(uint prop)
        {
            FlagInstance f;
            if (!_byProp.TryGetValue(prop, out f))
                return;

            Release(f);
            _byProp.Remove(prop);
            _flags.Remove(f);
            _heightFixed.Remove(prop);
            FlagRegistry.Remove(prop);
            FlagEditor.Forget(prop);

            if (Log.Detailed)
                Log.Detail("flags: flag #" + prop + " is gone (deleted, or no longer a flag)");
        }

        private static bool Same(FlagSpec a, FlagSpec b)
        {
            return a.Pattern == b.Pattern && a.Colour1 == b.Colour1 && a.Colour2 == b.Colour2 && a.Colour3 == b.Colour3 &&
                   a.SameShape(b) && Mathf.Approximately(a.Waviness, b.Waviness) && a.Name == b.Name &&
                   (a.Country ?? string.Empty) == (b.Country ?? string.Empty) && a.SamePole(b) && a.SamePoleColours(b) && a.SameHoist(b) &&
                   Mathf.Approximately(a.Darkness, b.Darkness);
        }

        private static void SetSpec(FlagInstance f, FlagSpec spec)
        {
            bool reshape = !spec.SameShape(f.Spec);
            bool refabric = !Mathf.Approximately(spec.Waviness, f.Spec.Waviness);
            f.Spec = spec;
            f.BlockReady = false;
            f.PoleBlockReady = false;
            f.PoleApplied = false;
            Wake(f);

            if (reshape && f.Cloth != null)
            {
                // A new size: a new cloth in the old one's shape, so a flag being resized in the
                // panel keeps flying (dropped and settled again, it vanished while the slider moved).
                FlagCloth old = f.Cloth;
                var cloth = new FlagCloth(spec.Length, spec.Height, FlagFabric.Standard(spec.Waviness), 1f, 0f, f.Detail);
                cloth.TakeShape(old);
                cloth.GroundY = old.GroundY;
                cloth.SelfCollision = old.SelfCollision;

                if (f.Mesh != null)
                    f.Mesh.Destroy();
                f.Cloth = cloth;
                f.Mesh = new FlagMesh(cloth, f.Refine);
                f.Refine = f.Mesh.Refine;
                f.Pending = 0f;
                f.Fresh = false;
                f.ShownAlpha = 1f;
                f.MeshDirty = true;
            }
            else if (refabric && f.Cloth != null)
            {
                f.Cloth.SetFabric(FlagFabric.Standard(spec.Waviness));
            }
        }

        /// <summary>
        /// The flag's prop moved or turned, or its hoist leans another way: the cloth follows
        /// (dragged if near, carried if a jump; turned whole onto a new hoist), and a paused game
        /// steps it for a moment.
        /// </summary>
        private static void Place(FlagInstance f, Vector3 anchor, float angle)
        {
            if (f.HasAnchor && f.Cloth != null)
            {
                Vector3 d = anchor - f.Anchor;
                if (d.sqrMagnitude > 1e-8f)
                {
                    f.Cloth.MoveAnchor(d.x, d.y, d.z);
                    f.MeshDirty = true;
                    Wake(f);
                }
            }

            if (f.HasAnchor && Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, f.Angle * Mathf.Rad2Deg)) > 1e-3f)
            {
                Wake(f);
                f.PoleApplied = false;
            }

            if (!f.HasAnchor || (anchor - f.Anchor).sqrMagnitude > 1e-4f)
                f.Ground = FlagProps.GroundAt(anchor);

            f.Anchor = anchor;
            f.Angle = angle;
            f.HasAnchor = true;

            float hx, hy, hz, fx, fy, fz;
            FlagOrientation.Frame(angle, f.Spec.Tilt, f.Spec.Lean, out hx, out hy, out hz, out fx, out fy, out fz);
            f.Hoist = new Vector3(hx, hy, hz);
            f.Fly = new Vector3(fx, fy, fz);

            FlagCloth c = f.Cloth;
            if (c != null && (Mathf.Abs(c.HoistX - hx) > 1e-5f || Mathf.Abs(c.HoistY - hy) > 1e-5f || Mathf.Abs(c.HoistZ - hz) > 1e-5f))
            {
                c.SetHoist(hx, hy, hz);
                f.MeshDirty = true;
                f.PoleApplied = false;
                Wake(f);
            }
        }

        private void Cull(FlagInstance f, Vector3 eye, Plane[] planes)
        {
            f.Distance = Vector3.Distance(eye, f.Anchor);

            // The box of the cloth as last drawn, or, before it is, of any shape it could take.
            Bounds box;
            if (f.Mesh != null)
            {
                box = f.Mesh.Bounds;
                box.center += f.Anchor;
            }
            else
            {
                float reach = f.Spec.Length + f.Spec.Height;
                box = new Bounds(f.Anchor + new Vector3(0f, -0.5f * f.Spec.Height, 0f), new Vector3(2f * reach, reach, 2f * reach));
            }

            f.World = box;
            f.Visible = f.Distance < DrawDistance && GeometryUtility.TestPlanesAABB(planes, box);
        }

        /// <summary>
        /// The next batch: the placement ghost first, then the flags in view nearest first --
        /// the nearest <see cref="MaxMoving"/> within <see cref="SimDistance"/> move -- as long as
        /// the planned work fits <see cref="BatchBudget"/> (the nearest always moves).
        /// </summary>
        private void Plan(float dt, bool frozen, float realDt, Wind wind)
        {
            double spent = 0.0;
            int settles = 0;
            _moving = 0;
            _waiting = 0;
            float now = Time.realtimeSinceStartup;

            if (_ghost != null)
            {
                // The flag being placed is always in the player's hand: it moves while paused too.
                float ghostDt = frozen ? realDt : dt;
                if (frozen)
                    _ghost.OwnTime += ghostDt;
                PlanFlag(_ghost, ghostDt, wind, true, true, ref spent, ref settles);
            }

            int n = InView();
            for (int i = 0; i < n; i++)
            {
                FlagInstance f = _order[i];
                bool moving = i < MaxMoving && f.Distance <= SimDistance;

                // Paused, frozen: only the flags being handled step, for a moment, slowing to a still.
                float flagDt = dt;
                if (frozen)
                {
                    flagDt = ClothPlan.PausedTime(realDt, f.AwakeUntil - now, WakeFade);
                    f.OwnTime += flagDt;
                }

                PlanFlag(f, flagDt, wind, moving, false, ref spent, ref settles);
            }
        }

        /// <summary>
        /// The flags in view into <see cref="_order"/>, nearest first; the others lose their
        /// untaken time (a flag coming back into view does not catch up). An insertion sort, as
        /// Volumetric Explosions sorts its volumes: no allocation (List.Sort with a comparison
        /// allocates on Mono), and the order changes little from one frame to the next.
        /// </summary>
        private int InView()
        {
            int n = 0;
            foreach (FlagInstance f in _flags)
            {
                if (!f.HasAnchor)
                    continue;

                if (!f.Visible)
                {
                    f.Pending = 0f;
                    continue;
                }

                if (n == _order.Length)
                    Array.Resize(ref _order, n * 2);
                _order[n++] = f;
            }

            for (int i = 1; i < n; i++)
            {
                FlagInstance x = _order[i];
                int j = i - 1;
                while (j >= 0 && _order[j].Distance > x.Distance)
                {
                    _order[j + 1] = _order[j];
                    j--;
                }

                _order[j + 1] = x;
            }

            for (int i = n; i < _orderUsed; i++)
                _order[i] = null;
            _orderUsed = n;
            return n;
        }

        /// <summary>One flag into the batch: its cloth made if needed, then a moment of settling, or this frame's steps.</summary>
        private void PlanFlag(FlagInstance f, float dt, Wind wind, bool moving, bool ghost, ref double spent, ref int settles)
        {
            f.Waiting = false;

            if (f.Cloth == null)
            {
                // Made and settled at Normal (six 1/30 s steps a frame at Ultra would be 35 ms of
                // one thread), then made finer as its size on the screen asks (ChooseDetail),
                // hanging from its hoist's line (FlagOrientation, set by Place).
                Vector3 start = wind.DirectionFor(f.Angle);
                f.Detail = FlagCloth.CellsAlong;
                f.Cloth = new FlagCloth(f.Spec.Length, f.Spec.Height, FlagFabric.Standard(f.Spec.Waviness), start.x, start.z, f.Detail,
                                        f.Hoist.x, f.Hoist.y, f.Hoist.z);
                f.Cloth.SelfCollision = _selfCollision;
                f.Mesh = new FlagMesh(f.Cloth, f.Refine);
                f.Refine = f.Mesh.Refine;
                f.Settled = false;
                f.SettleLeft = SettleTime;
                f.SettleClock = _clock - SettleTime;
                f.Pending = 0f;
                f.Fresh = false;
                f.BlockReady = false;

                // Its pole, if it has one and it is known yet (else the next Refresh sees to it).
                f.PoleApplied = false;
                if (f.Spec.HasPole && f.PoleMesh != null)
                {
                    f.PoleShape.ApplyTo(f.Cloth, f.Fly.x, f.Fly.y, f.Fly.z);
                    f.PoleApplied = true;
                }
            }

            f.Cloth.GroundY = f.Ground - f.Anchor.y;

            Vector3 dir = wind.DirectionFor(f.Angle);
            float height = Mathf.Max(f.Anchor.y - f.Ground, 0.5f);
            float at10 = wind.SpeedFor(f.Anchor, f.Ground);
            float speed = FlagWind.SpeedAt(at10, height);
            var job = new ClothJob
            {
                Cloth = f.Cloth,
                Speed = at10,
                Gusts = wind.Gusts,
                DirX = dir.x,
                DirZ = dir.z,
                Height = height,
                Waviness = f.Spec.Waviness,
                Length = f.Spec.Length,
                AnchorX = f.Anchor.x,
                AnchorZ = f.Anchor.z,
            };

            if (!f.Settled)
            {
                // Run it a moment first, a little each frame, so a flag appears hanging or flying,
                // not flat as a board. The ghost always gets its moment (the player is watching it).
                if (settles >= MaxSettlesPerFrame && !ghost)
                    return;

                settles++;
                int settleSub = f.Cloth.SubStepsFor(speed, 2f * Step);
                int steps = Math.Max(1, SettleSteps / settleSub);
                job.Steps = Math.Min(steps, Math.Max(1, Mathf.CeilToInt(f.SettleLeft / (2f * Step))));
                job.Size = 2f * Step;
                job.Time = f.SettleClock;
                f.SettleClock += job.Steps * job.Size;
                f.SettleLeft -= job.Steps * job.Size;

                if (f.SettleLeft <= 1e-4f)
                {
                    // Its last moment: shown from the next frame, when this batch is back.
                    f.Settled = true;
                    f.Pending = 0f;
                    job.Mesh = f.Mesh;
                    job.Alpha = 1f;
                }

                spent += job.Steps * settleSub * f.Cloth.Count * _unitCost;
                Queue(f, ref job);
                return;
            }

            if (!moving)
            {
                f.Pending = 0f;
                return;
            }

            _moving++;

            // Full rate near, half beyond HalfRateDistance, with a tenth's band either side (a
            // half-rate step takes twice the sub-steps once the wind carries the air a cell in
            // one: it saves in light winds only).
            if (f.StepSize <= Step && f.Distance > HalfRateDistance * 1.1f)
                f.StepSize = 2f * Step;
            else if (f.StepSize > Step && f.Distance < HalfRateDistance * 0.9f)
                f.StepSize = Step;
            float size = f.StepSize;
            int sub = f.Cloth.SubStepsFor(speed, size);

            // No room left: frozen at its last step until the nearer flags leave some.
            float behind;
            int due = ClothPlan.Due(ref f.Pending, dt, size, MaxStepsPerFrame, sub * f.Cloth.Count * _unitCost, Budget, ref spent,
                                    ghost, out behind, out f.Waiting);
            if (f.Waiting)
                _waiting++;

            job.Steps = due;
            job.Size = size;
            job.Time = _clock + f.OwnTime - behind;

            float alpha = Mathf.Clamp01(f.Pending / size);
            if (due == 0 && Mathf.Approximately(alpha, f.ShownAlpha))
                return;

            job.Mesh = f.Mesh;
            job.Alpha = alpha;
            Queue(f, ref job);
        }

        private void Queue(FlagInstance f, ref ClothJob job)
        {
            _worker.Add(ref job);
            _batch.Add(f);
        }

        private void Draw(FlagInstance f)
        {
            DrawPole(f);

            if (!f.Visible || f.Mesh == null || !f.Settled)
                return;

            if (f.MeshDirty)
            {
                // Moved here since the thread refreshed it: refreshed again here.
                f.Mesh.Upload(f.ShownAlpha);
                f.MeshDirty = false;
                f.Fresh = false;
            }
            else if (f.Fresh)
            {
                f.Mesh.Apply();
                f.Fresh = false;
            }

            // The flag the picking pointer is on glows gold; the selected one is not tinted (the
            // gizmo shows it -- the author: "remove the highlight on the flag when an existing flag
            // is selected. No need for it").
            bool hovered = IsHovered(f);
            if (!f.BlockReady || hovered || f.Highlighted)
            {
                FlagLook.Apply(f.Block, f.Spec, hovered ? HoverGlow : Vector4.zero);
                f.BlockReady = true;
                f.Highlighted = hovered;
            }

            // The particles' texture: the shape the vertex program lays the drawn grid on.
            f.Mesh.Bind(f.Block);
            Graphics.DrawMesh(f.Mesh.Mesh, Matrix4x4.TRS(f.Anchor, Quaternion.identity, Vector3.one), _material, _layer, _camera, 0,
                              f.Block, true, true);

            if (!_loggedFirstDraw)
            {
                _loggedFirstDraw = true;
                Log.Msg("flags: first flag drawn (#" + f.Prop + ", " + f.Spec.Describe() + ", " + f.Cloth.Columns + "x" + f.Cloth.Rows +
                        " cloth, at " + f.Distance.ToString("F0") + " m)");
            }

            f.PickAge += Time.unscaledDeltaTime;
            if (f.Prop != 0 && f.PickAge >= PickInterval)
            {
                f.PickAge = 0f;
                PublishPick(f);
            }
        }

        /// <summary>The flag under the picking pointer, if it is not the selected one (which carries the gizmo, untinted).</summary>
        private static bool IsHovered(FlagInstance f)
        {
            return f.Prop != 0 && f.Prop == FlagTool.Hovered && f.Prop != FlagEditor.Selected;
        }

        /// <summary>The hovered flag's glow (rgb x a, added): gold.</summary>
        private static readonly Vector4 HoverGlow = new Vector4(1f, 0.85f, 0.35f, 0.18f);

        /// <summary>The flag's pole, if it has one and it is in view: lit and shadowed like the cloth, glowing with it when hovered.</summary>
        private void DrawPole(FlagInstance f)
        {
            if (!f.PoleVisible || f.PoleMesh == null || _poleMaterial == null || !f.Spec.HasPole)
                return;

            // The cloth's glow this frame (Draw sets f.Highlighted after this).
            bool hovered = IsHovered(f);
            if (!f.PoleBlockReady || hovered || f.Highlighted)
            {
                FlagPoleMesh.Look(f.PoleBlock, f.Spec, hovered ? HoverGlow : Vector4.zero);
                f.PoleBlockReady = !hovered;
            }

            Graphics.DrawMesh(f.PoleMesh, FlagPoleMesh.Placement(f.Anchor, f.PoleShape, f.Hoist, f.Fly), _poleMaterial, _layer, _camera, 0,
                              f.PoleBlock, true, true);
        }

        /// <summary>A coarse sheet over the drawn cloth (every third particle and the edges), published for raycasts.</summary>
        private static void PublishPick(FlagInstance f)
        {
            FlagCloth c = f.Cloth;
            PickShape shape = f.Picks[f.PickNext];
            f.PickNext ^= 1;

            int cols = Samples(c.Columns), rows = Samples(c.Rows);
            int triangles = (cols - 1) * (rows - 1) * 2;
            if (shape.Triangles.Length < triangles * 9)
                shape.Triangles = new float[triangles * 9];

            float[] v = shape.Triangles;
            int o = 0;
            for (int b = 0; b < rows - 1; b++)
            {
                for (int a = 0; a < cols - 1; a++)
                {
                    int k00 = Index(c, a, b, cols, rows), k10 = Index(c, a + 1, b, cols, rows);
                    int k01 = Index(c, a, b + 1, cols, rows), k11 = Index(c, a + 1, b + 1, cols, rows);
                    o = Put(v, o, c, f.Anchor, k00);
                    o = Put(v, o, c, f.Anchor, k10);
                    o = Put(v, o, c, f.Anchor, k01);
                    o = Put(v, o, c, f.Anchor, k10);
                    o = Put(v, o, c, f.Anchor, k11);
                    o = Put(v, o, c, f.Anchor, k01);
                }
            }

            shape.Count = triangles;
            shape.Anchor = f.Anchor;
            PickVolumes(f, shape);
            FlagPicking.Publish(f.Prop, shape);
        }

        /// <summary>
        /// The easier targets round the cloth (PickShape): the slab -- the cloth's box in its own
        /// axes, out along its fly, down its hoist and across, a margin wider across -- and
        /// capsules round the hoist edge and the drawn pole; the box of it all for the raycast's
        /// first test.
        /// </summary>
        private static void PickVolumes(FlagInstance f, PickShape shape)
        {
            FlagCloth c = f.Cloth;
            float size = Mathf.Max(f.Spec.Length, f.Spec.Height);
            Vector3 h = f.Hoist;

            // Out along the fly as the cloth now lies: the fly edge's mean, square to the hoist.
            Vector3 m = Vector3.zero;
            for (int j = 0; j < c.Rows; j++)
            {
                int k = j * c.Columns + c.Columns - 1;
                m += new Vector3(c.X[k], c.Y[k], c.Z[k]);
            }

            m /= c.Rows;
            Vector3 u = m - h * Vector3.Dot(m, h);
            u = u.sqrMagnitude > 1e-6f ? u.normalized : f.Fly;
            Vector3 n = Vector3.Cross(h, u).normalized;

            float uMin = float.MaxValue, uMax = float.MinValue, hMin = float.MaxValue, hMax = float.MinValue;
            float nMin = float.MaxValue, nMax = float.MinValue;
            for (int k = 0; k < c.Count; k++)
            {
                var p = new Vector3(c.X[k], c.Y[k], c.Z[k]);
                float a = Vector3.Dot(p, u), b = Vector3.Dot(p, h), d = Vector3.Dot(p, n);
                if (a < uMin) uMin = a;
                if (a > uMax) uMax = a;
                if (b < hMin) hMin = b;
                if (b > hMax) hMax = b;
                if (d < nMin) nMin = d;
                if (d > nMax) nMax = d;
            }

            float across = Mathf.Max(PickMarginAcross, PickMarginShare * size);
            float inPlane = Mathf.Max(0.15f, 0.02f * size);
            shape.SlabU = u;
            shape.SlabV = h;
            shape.SlabN = n;
            shape.SlabCentre = f.Anchor + u * (0.5f * (uMin + uMax)) + h * (0.5f * (hMin + hMax)) + n * (0.5f * (nMin + nMax));
            shape.HalfU = 0.5f * (uMax - uMin) + inPlane;
            shape.HalfV = 0.5f * (hMax - hMin) + inPlane;
            shape.HalfN = 0.5f * (nMax - nMin) + across;
            shape.HasSlab = true;

            shape.HoistA = f.Anchor;
            shape.HoistB = f.Anchor + h * c.Height;
            shape.HoistRadius = Mathf.Max(PickMarginAcross, PickMarginShare * size);

            var box = new Bounds(shape.SlabCentre, Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                box.Encapsulate(shape.SlabCentre + u * ((corner & 1) != 0 ? shape.HalfU : -shape.HalfU) +
                                h * ((corner & 2) != 0 ? shape.HalfV : -shape.HalfV) + n * ((corner & 4) != 0 ? shape.HalfN : -shape.HalfN));
            }

            box.Encapsulate(new Bounds(shape.HoistA, Vector3.one * (2f * shape.HoistRadius)));
            box.Encapsulate(new Bounds(shape.HoistB, Vector3.one * (2f * shape.HoistRadius)));

            // The pole the mod draws, from its foot to its tip.
            if (f.Spec.HasPole && f.PoleMesh != null)
            {
                FlagPoleShape pole = f.PoleShape;
                shape.PoleA = f.Anchor - f.Fly * pole.Offset + h * pole.Length;
                shape.PoleB = shape.PoleA - h * pole.Tip;
                shape.PoleRadius = Mathf.Max(PickMarginAcross, 3f * pole.RadiusFoot);
                box.Encapsulate(new Bounds(shape.PoleA, Vector3.one * (2f * shape.PoleRadius)));
                box.Encapsulate(new Bounds(shape.PoleB, Vector3.one * (2f * shape.PoleRadius)));
            }
            else
            {
                shape.PoleRadius = 0f;
            }

            shape.Box = box;
        }

        /// <summary>
        /// The pick targets' margin round the cloth across its face and round its hoist and pole,
        /// metres: at least this, or this share of the flag's longer side (a 16 m flag: 1 m).
        /// </summary>
        private const float PickMarginAcross = 0.4f;
        private const float PickMarginShare = 0.06f;

        private static int Samples(int n)
        {
            return Math.Max(2, (n - 1) / 3 + 1 + ((n - 1) % 3 != 0 ? 1 : 0));
        }

        private static int Index(FlagCloth c, int a, int b, int cols, int rows)
        {
            int i = Math.Min(a * 3, c.Columns - 1);
            int j = Math.Min(b * 3, c.Rows - 1);
            return j * c.Columns + i;
        }

        private static int Put(float[] v, int o, FlagCloth c, Vector3 anchor, int k)
        {
            v[o] = anchor.x + c.X[k];
            v[o + 1] = anchor.y + c.Y[k];
            v[o + 2] = anchor.z + c.Z[k];
            return o + 3;
        }

        /// <summary>The sun (or the moon) for the light through the cloth: _RFSunDir and _RFSunColor, globally.</summary>
        private static void SetSun()
        {
            Vector3 dir = Vector3.up;
            Color light = Color.black;

            try
            {
                DayNightProperties properties = DayNightProperties.instance;
                if (properties != null)
                {
                    Light sun = properties.m_SunLight != null ? properties.m_SunLight.GetComponent<Light>() : null;
                    if (sun != null && sun.isActiveAndEnabled)
                    {
                        dir = -sun.transform.forward;
                        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
                        light = (linear ? sun.color.linear : sun.color) * sun.intensity;
                    }
                }
            }
            catch (Exception)
            {
                // The light through the cloth is a nicety; without the sun it is simply off.
            }

            Shader.SetGlobalVector(FlagLook.IdSunDir, new Vector4(dir.x, dir.y, dir.z, 0f));
            Shader.SetGlobalVector(FlagLook.IdSunColor, new Vector4(light.r, light.g, light.b, 0f));
        }

        private void Report()
        {
            _costMs += _cost.Elapsed.TotalMilliseconds;
            _costFrames++;

            float now = Time.realtimeSinceStartup;
            if (now < _nextReport)
                return;

            if (Log.Detailed && _nextReport > 0f)
            {
                int frames = Math.Max(1, _costFrames);
                float seconds = now - _nextReport + 5f;
                Wind w = Wind.Current();
                Log.Detail("flags: " + _flags.Count + " in the city, " + VisibleCount + " in view, " + _moving + " moving, " + _waiting +
                           " waiting for room | " + (frames / Math.Max(seconds, 0.1f)).ToString("F0") + " fps | main thread " +
                           (_costMs / frames).ToString("F3") + " ms a frame (" + (_waitMs / frames).ToString("F3") +
                           " of it waiting for the cloth), cloth " + (_batchMs / frames).ToString("F3") + " ms a frame on " +
                           (_worker.Threaded ? _worker.Threads + " thread(s) (" + (_batchWallMs / frames).ToString("F3") + " ms out)" : "the main thread") +
                           ", " + (1000.0 * _unitCost).ToString("F2") + " us a particle step | " + _stepsThisReport + " cloth steps in " +
                           seconds.ToString("F0") + " s" + (_failures > 0 ? ", " + _failures + " failed" : "") + " | in view by detail " +
                           DetailsInView() + " | wind " + w.Speed.ToString("F1") + " m/s, gusts " + (w.Gusts * 100f).ToString("F0") + "%, " +
                           w.Describe());
            }

            _costMs = 0.0;
            _waitMs = 0.0;
            _batchMs = 0.0;
            _batchWallMs = 0.0;
            _costFrames = 0;
            _stepsThisReport = 0;
            _nextReport = now + 5f;
        }

        /// <summary>For the log: the flags in view at each detail, "18:3 32:1".</summary>
        private string DetailsInView()
        {
            int[] details = FlagCloth.Details;
            var counts = new int[details.Length + 1];
            foreach (FlagInstance f in _flags)
            {
                if (!f.Visible || f.Cloth == null)
                    continue;

                int i = Array.IndexOf(details, f.Detail);
                counts[i < 0 ? details.Length : i]++;
            }

            string text = string.Empty;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > 0)
                    text += (text.Length > 0 ? " " : "") + (i < details.Length ? details[i].ToString() : "other") + ":" + counts[i];
            }

            return text.Length > 0 ? text : "none";
        }

        // ---- the placement ghost --------------------------------------------------------------

        /// <summary>
        /// The flag the tool is about to place, at the cursor: call every frame while wanted
        /// (the tool does, from its Update). Only noted here; applied in LateUpdate, when no
        /// cloth is being stepped.
        /// </summary>
        public void SetGhost(Vector3 anchor, float angle, FlagSpec spec)
        {
            _ghostAnchor = anchor;
            _ghostAngle = angle;
            _ghostSpec = spec;
            _ghostWanted = true;
        }

        private void ApplyGhost(Vector3 eye, Plane[] planes)
        {
            if (!_ghostWanted)
            {
                DropGhost();
                return;
            }

            // Wanted again next frame if the tool is still placing.
            _ghostWanted = false;

            if (_ghost == null)
                _ghost = new FlagInstance { Prop = 0, Spec = _ghostSpec };

            if (!Same(_ghostSpec, _ghost.Spec))
                SetSpec(_ghost, _ghostSpec);

            Place(_ghost, _ghostAnchor, _ghostAngle);
            _ghost.Distance = Vector3.Distance(eye, _ghostAnchor);
            _ghost.Visible = true;
            _ghost.World = new Bounds(_ghostAnchor, Vector3.one * (_ghostSpec.Length * 2f));
            ChooseDetail(_ghost);
            ChooseRefine(_ghost);
            UpdatePole(_ghost, planes);
        }

        private void DropGhost()
        {
            if (_ghost == null)
                return;

            Release(_ghost);
            _ghost = null;
        }

        // ---- for the panel and the tool -------------------------------------------------------

        public int Count
        {
            get { return _flags.Count; }
        }

        public int VisibleCount
        {
            get
            {
                int n = 0;
                foreach (FlagInstance f in _flags)
                    if (f.Visible)
                        n++;
                return n;
            }
        }

        /// <summary>Every flag, in prop order, for the Existing flags list.</summary>
        public void List(List<FlagEntry> into)
        {
            into.Clear();
            foreach (FlagInstance f in _flags)
            {
                if (f.HasAnchor)
                    into.Add(new FlagEntry { Prop = f.Prop, Spec = f.Spec, Anchor = f.Anchor, Angle = f.Angle, Ground = f.Ground });
            }

            into.Sort((a, b) => a.Prop.CompareTo(b.Prop));
        }

        public bool TryGet(uint prop, out FlagEntry entry)
        {
            FlagInstance f;
            if (prop != 0 && _byProp.TryGetValue(prop, out f) && f.HasAnchor)
            {
                entry = new FlagEntry { Prop = f.Prop, Spec = f.Spec, Anchor = f.Anchor, Angle = f.Angle, Ground = f.Ground };
                return true;
            }

            entry = default(FlagEntry);
            return false;
        }

        /// <summary>The nearest flag the ray meets, 0 for none (the tool's picking, on the main thread).</summary>
        public uint PickAt(ColossalFramework.Math.Segment3 ray)
        {
            uint best = 0;
            float bestT = 2f;
            foreach (FlagInstance f in _flags)
            {
                PickShape shape = FlagPicking.Get(f.Prop);
                float t;
                if (shape != null && shape.Intersect(ray, out t) && t < bestT)
                {
                    bestT = t;
                    best = f.Prop;
                }
            }

            return best;
        }

        /// <summary>The flag whose anchor is at <paramref name="position"/> (Move It's overlay passes a position, not a prop).</summary>
        public bool TryFindAt(Vector3 position, out Bounds world)
        {
            foreach (FlagInstance f in _flags)
            {
                if (f.HasAnchor && (f.Anchor - position).sqrMagnitude < 0.25f)
                {
                    world = OutlineBox(f);
                    return true;
                }
            }

            world = default(Bounds);
            return false;
        }

        /// <summary>From FlagLoadingHook.EndOverlay: the tool's hovered flag outlined on the city (the selected one has the gizmo instead).</summary>
        public void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            FlagInstance f;
            if (FlagTool.Hovered != 0 && FlagTool.Hovered != FlagEditor.Selected && _byProp.TryGetValue(FlagTool.Hovered, out f) && f.Visible)
                Outline(cameraInfo, OutlineBox(f), new Color(1f, 0.85f, 0.35f, 0.55f));
        }

        /// <summary>What a flag's outline covers: its cloth as drawn, and its pole from foot to tip when the mod draws one.</summary>
        private static Bounds OutlineBox(FlagInstance f)
        {
            Bounds box = f.World;
            if (f.Spec.HasPole && f.PoleMesh != null)
            {
                FlagPoleShape pole = f.PoleShape;
                Vector3 foot = f.Anchor - f.Fly * pole.Offset + f.Hoist * pole.Length;
                box.Encapsulate(foot);
                box.Encapsulate(foot - f.Hoist * pole.Tip);
            }

            return box;
        }

        /// <summary>
        /// The game's overlay over a flag (Move It's and the bulldozer's highlight, our tool's
        /// hover): a disc over the whole of it, painted on whatever lies within its height --
        /// the cloth, the pole, what the pole stands on. The disc spans the box's diagonal, so a
        /// flag flying across the axes is covered to its corners (the author: "the selector is
        /// too small"; the first disc spanned the box's wider side only).
        /// </summary>
        public static void Outline(RenderManager.CameraInfo cameraInfo, Bounds world, Color colour)
        {
            Vector3 c = world.center;
            float size = Mathf.Sqrt(world.size.x * world.size.x + world.size.z * world.size.z) + 1f;
            Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(cameraInfo, colour, c, size, world.min.y - 0.5f, world.max.y + 0.5f,
                                                                      false, true);
            Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
        }

        /// <summary>One line for the panel: how many flags, how many in view.</summary>
        public static string Describe()
        {
            FlagRenderer r = Instance;
            if (r == null)
                return Localization.Get("Status.NoCity");
            if (!r._ready)
                return Localization.Get("Status.Failed");
            if (FlagPrefab.Info == null)
                return Localization.Get("Status.NoPrefab");

            return Localization.Get("Status.Flags", r.Count, r.VisibleCount);
        }
    }

    /// <summary>The wind this frame, as the panel sets it: speed at 10 m, gustiness, and where it blows.</summary>
    public struct Wind
    {
        /// <summary>The game's wind (GameWind), not the player's own.</summary>
        public bool FromGame;

        /// <summary>
        /// At 10 m, m/s: the player's own, or the game's over open level ground (GameWind.BaseSpeed
        /// times its weather) -- each flag gets it times the game's wind map where it stands.
        /// </summary>
        public float Speed;

        public float Gusts;
        public int Mode;
        public Vector3 Fixed;

        /// <summary>The game's weather in it (its GetWindSpeedFactor), and the rain and fog behind that: for the panel's line.</summary>
        public float Weather, Rain, Fog;

        public static Wind Current()
        {
            bool fromGame = Settings.WindFromGame != null ? Settings.WindFromGame.value : Settings.Defaults.WindFromGame;
            if (fromGame)
            {
                // The game's: its direction, its weather; gusts as a town's (EN 1991-1-4).
                var g = new Wind { FromGame = true, Gusts = 1f, Mode = Settings.WindModes.Game };
                GameWind.Read(out g.Fixed, out g.Weather, out g.Rain, out g.Fog);
                g.Speed = GameWind.BaseSpeed * g.Weather;
                return g;
            }

            // The player's own, exactly as set.
            var w = new Wind
            {
                Speed = Settings.WindSpeed != null ? Mathf.Clamp(Settings.WindSpeed.value, 0f, 50f) : Settings.Defaults.WindSpeed,
                Gusts = Settings.WindGusts != null ? Mathf.Clamp(Settings.WindGusts.value, 0f, 2f) : Settings.Defaults.WindGusts,
                Mode = Settings.WindMode != null ? Settings.WindMode.value : Settings.Defaults.WindMode,
                Weather = 1f,
            };

            if (w.Mode == Settings.WindModes.Fixed)
            {
                float deg = Settings.WindDirection != null ? Settings.WindDirection.value : Settings.Defaults.WindDirection;
                w.Fixed = FlagProps.Direction(deg * Mathf.Deg2Rad);
            }
            else if (w.Mode == Settings.WindModes.Game)
            {
                float weather, rain, fog;
                GameWind.Read(out w.Fixed, out weather, out rain, out fog);
            }

            return w;
        }

        /// <summary>The way the wind blows at a flag of this prop angle (the way the flag points, in the first mode).</summary>
        public Vector3 DirectionFor(float angle)
        {
            return Mode == Settings.WindModes.EachFlag ? FlagProps.Direction(angle) : Fixed;
        }

        /// <summary>The wind at 10 m for a flag standing here over this ground: the game's wind map's share of it, or the player's own.</summary>
        public float SpeedFor(Vector3 place, float ground)
        {
            return FromGame ? Speed * GameWind.Exposure(place, ground) : Speed;
        }

        /// <summary>For the log: "the game's (5.8 m/s over open ground, weather x1.15)" or "fixed".</summary>
        public string Describe()
        {
            return FromGame
                ? "the game's (" + Speed.ToString("F1") + " m/s over open ground, weather x" + Weather.ToString("F2") + ")"
                : Settings.WindModes.LogName(Mode);
        }
    }
}
