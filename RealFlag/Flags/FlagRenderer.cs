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

        /// <summary>Its longer side on the screen, pixels, this frame (FlagRenderer.DrawPixels and the rest go by it).</summary>
        public float Pixels;

        /// <summary>In the renderer's in-view order (FlagRenderer.InView keeps last frame's: the sort then has little to do).</summary>
        public bool InOrder;

        /// <summary>Real time since when it has been out of view and small (FlagRenderer.KeepPixels); 0 while not. Its cloth is let go after FlagRenderer.ReleaseSeconds.</summary>
        public float UnseenSince;

        /// <summary>Time not yet stepped, seconds, and the length of this flag's steps (1/60 s, 1/30 s when small on the screen).</summary>
        public float Pending;
        public float StepSize = FlagRenderer.Step;

        /// <summary>
        /// A still flag's shape being made (FlagMotion Still: StillPose), on a cloth of its own in the
        /// flag's own frame (heading 0): the flag shows the shape it had meanwhile (none, if it is
        /// new), and takes this one, turned to its heading, when done.
        /// </summary>
        public StillPoseRun Pose;

        /// <summary>A still or flat flag's shape is to be made (again): it is new, or what shapes it changed.</summary>
        public bool PoseWanted;

        /// <summary>What makes the shape being made (FlagRenderer's shapes kept, by it); null: not to be kept.</summary>
        public string PoseKey;

        /// <summary>Shown once its first seconds have been run (SettleLeft of them are still to run, on SettleClock).</summary>
        public bool Settled;
        public float SettleLeft;
        public float SettleClock;

        /// <summary>The cloth's thread refreshed the mesh's arrays (at ShownAlpha): they go to the GPU when it is next drawn.</summary>
        public bool Fresh;
        public float ShownAlpha = 1f;

        /// <summary>The main thread moved the cloth since the arrays were refreshed (its anchor moved): refreshed again when drawn.</summary>
        public bool MeshDirty = true;

        /// <summary>
        /// Over the cloth thread's budget this frame: left as it is, and planned before the flags
        /// that had room (WaitedLast, its value when the next plan began).
        /// </summary>
        public bool Waiting;
        public bool WaitedLast;

        /// <summary>The design went into the block (it changes rarely; the glow each frame while the flag is hovered).</summary>
        public bool BlockReady;
        public bool Highlighted;

        /// <summary>FlagImages.UserVersion when the block was last put: a picture of the player's made again (its file changed) is taken up by every flag.</summary>
        public int ImageVersion = -1;

        public readonly PickShape[] Picks = { new PickShape(), new PickShape() };
        public int PickNext;
        public float PickAge = 1e6f;

        /// <summary>Where the cloth has been lately, for its pick volume (PickHold), in the axes it was taken in: down the hoist, out along <see cref="HeldU"/>, across.</summary>
        public PickHold Hold;
        public Vector3 HeldU;

        /// <summary>The way the wind blows at the flag (as its last job had it): the pick volume's axis out along the fly.</summary>
        public Vector3 WindDir = Vector3.right;

        /// <summary>
        /// The wind at 10 m the flag flies in, following the speed of the moment where it stands
        /// (FlagWind.Follow: Volumetric Weather's gusts come and go smoothly), and the clock it was
        /// last followed at; negative until its first.
        /// </summary>
        public float WindAt10 = -1f;
        public float WindClock;

        /// <summary>The hoist's line and the fly at rest (FlagOrientation), from the prop's angle and the spec's hoisted edge, tilt and lean.</summary>
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

        /// <summary>The pole's plain copy for Real Glass's Reflective finish (made only while it is used; FlagPoleMesh.BuildPlain).</summary>
        public Mesh PoleFinishMesh;
        public readonly MaterialPropertyBlock PoleBlock = new MaterialPropertyBlock();
        public bool PoleBlockReady;
        public bool PoleVisible;

        /// <summary>
        /// The pole's length along its axis from the anchor to its foot: the spec's own, or, while
        /// that is 0 (a banner's crossbar, an older save's pole, one sent down to the ground again),
        /// cast on the simulation thread (FlagTool.CastAlong) from where the axis crosses the
        /// anchor's level, again whenever that point or the axis moves. Negative until known.
        /// </summary>
        public float PoleLength = -1f;
        public Vector3 PoleCastFrom;
        public Vector3 PoleCastAlong;
        public int PoleCastAsked;
        public volatile int PoleCastDone;
        public float PoleCastResult;

        /// <summary>The cloth keeps off the current pole (it has been told: FlagPoleShape.ApplyTo).</summary>
        public bool PoleApplied;

        /// <summary>
        /// The city round it that its cloth keeps off (FlagSurroundings): the surface under it and
        /// the props' and trees' capsules, each gathered round a point of the world (the anchor
        /// then) over a square (its middle x, z and half its side); when (real time); the surface
        /// marked stale by a building changed near it. Plain data, shared with the thread.
        /// </summary>
        public SurfaceGrid Surface;
        public Vector3 SurfaceOrigin;
        public Vector3 SurfaceSquare;
        public float SurfaceAt;
        public bool SurfaceStale;
        public CapsuleSet Capsules;
        public Vector3 CapsulesOrigin;
        public Vector3 CapsulesSquare;
        public float CapsulesAt;

        /// <summary>Said once: its hoist stands under the surface (a roof over it, inside a building), which its cloth leaves out.</summary>
        public bool LoggedBlocked;

        /// <summary>Said once: its hoist stands in a wall's slope, which its cloth keeps back from it (FlagCloth.WallShift).</summary>
        public bool LoggedWall;

        /// <summary>Its cloth's FlagCloth.Relaid and FlagCloth.Unknotted when last looked at (a change is logged).</summary>
        public int Relaid;
        public int Unknotted;

        /// <summary>Its new cloth has been laid out clear of the city (FlagCloth.LayOutClear), before it first settles.</summary>
        public bool LaidOut;

        /// <summary>
        /// The wind's terrain round it (FlagSurroundings.Roughness): the buildings' roughness length,
        /// metres (0 until read: a town's), where (x, z) and when (real time) it was read, marked stale
        /// by a building changed within its reach, and the value last logged.
        /// </summary>
        public float Roughness;
        public Vector2 RoughnessPlace;
        public float RoughnessAt;
        public bool RoughnessStale;
        public float RoughnessLogged;

        /// <summary>
        /// Shared motion (MotionRecord): the group whose source it follows, on which cloth of its
        /// own (a cloth made since -- a new detail, a redesign -- has let it go), since when (real
        /// time: the first half second blends from the shape it had, kept in From), and the time
        /// of the source it last showed.
        /// </summary>
        public ShareGroup Follow;
        public FlagCloth FollowCloth;
        public float FollowSince;
        public float FollowShown = float.NaN;
        public float[] FromX, FromY, FromZ;

        /// <summary>
        /// Its wind for shared motion's key and group, eased over FlagRenderer.ShareSpeedSeconds
        /// (a gust is no reason to change source), updated once a plan frame; -1 not yet. The
        /// wind's angle from its heading, degrees, that frame.
        /// </summary>
        public float ShareSpeed = -1f, ShareDegrees;
        public int ShareFrame = -1;

        /// <summary>The props' and trees' capsules' highest top over the world (for CapsulesTopFor), read once a gathering.</summary>
        public CapsuleSet CapsulesTopFor;
        public float CapsulesTop;

        /// <summary>
        /// A flag that plays a loop (FlagMotion.Loop, FlagRenderer.Loop.cs): the loop it is DRAWN
        /// with -- it has no cloth or mesh of its own then -- and the one its design asks for now
        /// (null: to be looked up). They differ while a new one is being made (a redesign, an
        /// option): the old one shows meanwhile, or the flag's cloth if it has one. The grid it is
        /// drawn with: its cells along the longer side (LoopMotion.CellsFor), 0 not chosen yet.
        /// </summary>
        public LoopEntry Loop;
        public LoopEntry LoopWanted;
        public int LoopCells;
    }

    /// <summary>
    /// Shared motion's group (MotionRecord): like flags (one cloth: ShareKey.SameCloth, their wind
    /// Near its own) and the hidden cloth they follow -- its SOURCE, made from the first that
    /// followed (its founder) at the finest detail its like flags had, stepped every frame at the
    /// full rate in its first follower's wind, each step kept in its record. A new source runs
    /// ahead of time (WarmSteps a frame) until its record spans MotionRecord.ReadySpan; none
    /// follows it before. Dropped after FlagRenderer.GroupIdle seconds with no follower.
    /// </summary>
    public sealed class ShareGroup
    {
        public ShareKey Key;
        public FlagCloth Source;
        public MotionRecord Record;

        /// <summary>The source's detail, and the wind (eased, FlagInstance.ShareSpeed) and its angle from the heading, degrees, it was made for: who may join (ShareKey.Near).</summary>
        public int Detail;
        public float KeySpeed, KeyDegrees;

        /// <summary>The source's heading (its founder's prop angle, radians) and its wind's direction (radians, as a heading); upright: turned by the wind, else by the heading.</summary>
        public float Angle, WindAngle;
        public bool Upright;

        /// <summary>The source's wind as its founder had it this frame (the job's air): its speed at 10 m and at the flag, height, roughness, place, direction.</summary>
        public float At10, Speed, Height, Roughness, AnchorX, AnchorZ, DirX, DirZ, Waviness, Length;
        public uint Founder;
        public int WindFrame = -1;

        /// <summary>The source's clock (the time followers play back from), its time not yet stepped, and its last step's end.</summary>
        public float Now, Pending, Stepped;
        public bool Warm;

        /// <summary>Followers this frame, seconds with none, and dropped (its followers let it go).</summary>
        public int Members;
        public float Idle;
        public bool Dead;
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
    /// sixteenth a frame); a prop with no design gets the new flag's (the New flag window's).
    ///
    /// The cloth steps at 60 Hz on each flag's own clock (FlagCloth.SubStepsFor splits the step
    /// in a strong wind), drawn between its last two steps (FlagMesh.Refresh); a flag steps only
    /// while it is in view and big enough on the screen to see move, and at half the rate when
    /// smaller -- by its size there, not its distance (<see cref="DrawPixels"/>). The clock runs
    /// in real time -- the wind does not blow four times harder at the game's fastest speed --
    /// and stops while the game is paused if the player wants a still.
    ///
    /// The stepping is done on threads of its own (ClothWorker) while the game draws the frame:
    /// each frame waits for the last batch, takes its meshes, then plans the next -- the flags
    /// biggest on the screen first, within <see cref="BatchBudget"/> of each thread's time,
    /// measured as it goes (the smallest wait, frozen, when there is not room for all). A new flag
    /// is first run for <see cref="SettleTime"/>, a little each frame, so it appears hanging or flying.
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
    public sealed partial class FlagRenderer : MonoBehaviour
    {
        public static FlagRenderer Instance { get; private set; }

        /// <summary>
        /// Drawing and stepping (the shader loaded, the thread not stuck). While false the flags
        /// are not drawn, so a pick by their cloth would find nothing: the game's own test then
        /// (FlagPatches), so the bulldozer and Move It still reach them.
        /// </summary>
        public bool Ready
        {
            get { return _ready; }
        }

        /// <summary>One cloth step, seconds (FlagMesh draws between steps).</summary>
        public const float Step = 1f / 60f;

        /// <summary>
        /// A flag is drawn while its longer side is at least this many pixels on the screen, it
        /// moves while at least <see cref="MovePixels"/>, and at the full rate (1/60 s steps; 1/30 s
        /// below, with a tenth's band either side before it changes) while at least
        /// <see cref="FullRatePixels"/> -- by its size on the screen, not its distance (the camera's
        /// pixels per radian: its screen's height and its field of view), so a zoomed camera keeps
        /// a far flag it shows large. The first version cut at fixed distances: the author's
        /// 21.7 m flag, 104 px long through a 10 degree view 2.4 km off, stood still from 1.5 km
        /// and vanished at 2.5 km. With the game's camera (45 degrees, 2160 px high) a 3 m flag is
        /// drawn to 3.9 km, moves to 1.3 km, at the full rate within 260 m (the first version's
        /// 2.5 km, 1.5 km, 400 m); a 20 m flag to 26 km, 8.7 km, 1.7 km. The pole the mod draws
        /// is drawn while its length is at least DrawPixels.
        /// </summary>
        public const float DrawPixels = 2f;
        public const float MovePixels = 6f;
        public const float FullRatePixels = 30f;

        /// <summary>
        /// A flag casts a shadow while at least this big on the screen: a 3 m flag within about
        /// 325 m of the game's camera. Below it the shadow is a speck no one sees, and every
        /// shadowed draw costs the shadow passes (the 2026-11-07 sweep: a city of specks drawn
        /// with shadows to 3.9 km).
        /// </summary>
        public const float ShadowPixels = 24f;

        /// <summary>
        /// A flag out of view keeps its cloth (settled, ready for a turn of the camera) while it is
        /// at least this big on the screen were the camera turned to it; smaller than that for
        /// <see cref="ReleaseSeconds"/>, its cloth, mesh and city are let go (ReleaseUnseen) and
        /// made again when it is next in view -- from a like flag's shape in 0.3 s, else in 1.5 s.
        /// Before, every flag within MovePixels (about 1.3 km for a 3 m flag) kept all of it for
        /// the session: 0.3-0.9 GB for 2000 flags (the sweep). PlanUnseen settles only flags this
        /// big too, so nothing is let go and settled by turns. ReleaseSeconds was 30: the author's
        /// run (2026-11-07 19:00-19:17, 22 flags) let 136 cloths go in 15 minutes of panning, each
        /// flag settling again (1.5 s) about six times as the camera came and went; 120 s covers
        /// a look elsewhere and back, and still bounds a city of thousands within two minutes.
        /// </summary>
        public const float KeepPixels = 24f;
        public const float ReleaseSeconds = 120f;

        /// <summary>
        /// At most this many flags move in one frame, the biggest on the screen first. Was 48 until
        /// 2026-10-31: the author's 93 flags in view, 45 of them frozen whatever the threads had
        /// room for. The budget decides now (ClothLoad makes the smaller ones coarser first); this
        /// only bounds the main thread's share (a mesh handed over a flag, 2-9 us).
        /// </summary>
        public const int MaxMoving = 160;

        /// <summary>
        /// The cloth thread's work in a frame of 1/60 s, milliseconds: under half the frame, so
        /// the main thread does not wait for it, and under half a core; a longer frame gives it
        /// as much more, up to <see cref="MaxStepsPerFrame"/> times (ClothPlan.BudgetFor).
        /// Planned from the measured cost of a particle step: in the GPU preview's Unity 5.6
        /// Mono, room for 21 flags of 3 x 2 m in a light wind, 11 at 12 m/s (two sub-steps), 5 at
        /// 30 (four).
        /// </summary>
        public const double BatchBudget = 8.0;

        /// <summary>The frame's budget of thread time: <see cref="BatchBudget"/> for the frame's length, on each of the cloth's threads.</summary>
        private double Budget
        {
            get { return ClothPlan.BudgetFor(_frameTime, Step, MaxStepsPerFrame, BatchBudget) * Math.Max(1, _worker.Threads); }
        }

        /// <summary>
        /// A particle step's cost until one is measured, milliseconds: the GPU preview's Unity 5.6
        /// editor (Mono) steps a 3 x 2 m flag's 247 particles and refreshes its mesh in 0.37 ms
        /// (gpu-render.ps1 -Bench).
        /// </summary>
        private const double FirstGuessCost = 0.0015;

        /// <summary>
        /// At most this many cloth steps a flag catches up in one frame (a hitch is not worth a burst
        /// of physics): the flags keep real time down to 60 / this fps, and slow below it. Was 3
        /// until 2026-10-30 -- real time to 20 fps only: the author plays at 7-11 fps in a big city,
        /// where every flag ran at 0.35-0.55 of real time, and a 16.9 m flag on a 37 m pole (a big
        /// flag's waves are slow for its size already) "wasn't waving at all".
        /// </summary>
        private const int MaxStepsPerFrame = 8;

        /// <summary>
        /// A new cloth is run this long before it is first shown, to settle, in 1/30 s steps: as
        /// many a frame as one thread's share of the budget holds (ClothPlan.SettleSteps), on as
        /// many flags as the budget has room for -- at least <see cref="MinSettlesPerFrame"/>
        /// whatever it holds, at most <see cref="MaxSettlesPerFrame"/>. Until 2026-11-01 six steps
        /// a frame on two flags a frame: the author's 58 flags in view at a load (and again after a
        /// Move It paste) were hidden 10-15 s, popping in two by two, while the threads ran at 0-6%
        /// of their budget. A settle costs ~31 ms of a thread (a 2.6 m flag at 18 cells, 45 steps
        /// of two sub-steps at 1.8 us a particle step): the 58 together, about half a second on four.
        /// </summary>
        private const float SettleTime = 1.5f;
        private const int MinSettlesPerFrame = 2;
        private const int MaxSettlesPerFrame = 32;

        /// <summary>
        /// A flag that settles from a like one's shape (the same ShareKey, settled at 18 cells, with
        /// nothing of the city in reach: <see cref="TryCopySettled"/>) runs only this long of its
        /// own first -- its own wind at its own place -- before it is shown.
        /// </summary>
        private const float CopiedSettleTime = 0.3f;

        /// <summary>
        /// Settled flags whose shapes a new like flag may take, by key; dropped whole at
        /// <see cref="MaxSettledLike"/> and when the city unloads. A donor is used only while its
        /// cloth is still the one it settled (not made finer since) and nothing of the city is in
        /// its reach.
        /// </summary>
        private readonly Dictionary<ShareKey, SettledLike> _settledLike = new Dictionary<ShareKey, SettledLike>();
        private const int MaxSettledLike = 256;

        private struct SettledLike
        {
            public FlagInstance Flag;
            public FlagCloth Cloth;
        }

        /// <summary>
        /// The surfaces gathered at once for flags about to settle (FlagSurroundings.Surface: a few
        /// tenths of a millisecond in open ground, a few ms beside a detailed building) stop for the
        /// frame once they have taken this much of the main thread, milliseconds -- the first
        /// <see cref="MinSettlesPerFrame"/> whatever they take.
        /// </summary>
        private const double SettleGatherMs = 2.0;
        private double _settleGatherMs;

        /// <summary>
        /// Flags out of view that settle with the budget left over, nearest first, at most this
        /// many a frame: turned to, they are there already (until 2026-11-01 only flags in view
        /// settled, so a turn of the camera made more pop in).
        /// </summary>
        private const int UnseenPerFrame = 8;
        private readonly FlagInstance[] _unseen = new FlagInstance[UnseenPerFrame];
        private bool _unseenPass;

        /// <summary>For the log: settles finished in view, out of view, and from a like flag's shape, since the last report; a burst of them timed.</summary>
        private int _settledSeen, _settledUnseen, _settleCopies;
        private float _burstSince = -1f, _burstLast;
        private int _burstSettled, _burstCopies, _frameSettled, _frameCopies;

        /// <summary>A burst ends this long after its last flag waiting or settled, seconds (a paste's flags are found over several frames: Scan).</summary>
        private const float BurstQuiet = 0.5f;

        /// <summary>
        /// A still flag's shape (StillPose, 2.5-7 s of its breeze) is made this many 1/32 s steps a
        /// frame (fewer sub-steps' worth, more in a longer frame: ClothPlan.BudgetFor's one to
        /// three), sharing the settles' room (<see cref="SettleRoom"/>) with the new cloths: a 3 x 2 m flag's in
        /// about 24 frames at 60 fps, 8 at 10 fps. Its steps cost ~7 ms of a thread a frame.
        /// </summary>
        private const int PoseStepsPerFrame = 12;

        /// <summary>
        /// Still flags' shapes made, by what makes them (StillKey), in the flag's own frame (heading
        /// 0): another flag of the same design takes its shape at once -- a flag placed takes the
        /// ghost's, a city's row of like flags is made once. Dropped whole when it holds
        /// <see cref="MaxStillShapes"/> (each a few kilobytes) and when the city unloads.
        /// </summary>
        private readonly Dictionary<string, StillShape> _stillShapes = new Dictionary<string, StillShape>();
        private const int MaxStillShapes = 64;

        private sealed class StillShape
        {
            public int Columns, Rows;
            public float[] X, Y, Z;
        }

        /// <summary>How often a flag's pick volume is refreshed, seconds.</summary>
        private const float PickInterval = 0.1f;

        /// <summary>
        /// The pick volume gives back the cloth's extents with this time constant, seconds
        /// (PickHold): long against a flutter's second or so, so it holds the whole sweep; short
        /// enough that a flag the wind has dropped is picked where it now hangs within a few
        /// seconds.
        /// </summary>
        private const float PickRelease = 4f;

        /// <summary>
        /// Every pick volume is at least this wide on the screen, pixels of a 1080-line view
        /// (scaled to the screen's own): a far or small flag is found without pointing at its
        /// few pixels exactly -- a picking region round the cursor, as OpenGL's gluPickMatrix
        /// has one (the OpenGL Programming Guide, "Selection and Feedback"). The gizmo's handles
        /// are picked within 11. Move It's (and the mod's tool's) wider pick: at least
        /// <see cref="WidePickPixels"/>.
        /// </summary>
        private const float PickPixels = 10f;
        private const float WidePickPixels = 16f;

        /// <summary>
        /// A flag is given the coarsest detail whose cells are at most this many pixels on the
        /// screen (never coarser than 18 cells, never finer than the option): with a 45 deg camera on
        /// a 2160-pixel screen, a 3 m flag gets 24 cells within 87 m, 32 within 65 m, 40 within 49
        /// m (and no more: its cells are FlagCloth.MinCell there) -- a 15 m flag five times as far
        /// (and 52 within 196 m, 64 within 150 m), a 1080-pixel screen half. The first frame
        /// logs the camera's own figures. With a band of 15% either way before it changes back,
        /// and a few changes a frame at most (a new cloth and mesh each).
        /// </summary>
        public const float PixelsPerCell = 5f;
        private const float DetailBand = 1.15f;
        private const int MaxDetailChanges = 2;

        /// <summary>Coarser cloths a frame besides (a load being shed: a coarser cloth is cheaper to make and to step).</summary>
        private const int MaxCoarserChanges = 4;

        /// <summary>
        /// The cloth under load (2026-10-31): the on-screen sizes PixelsPerCell and FullRatePixels
        /// are taken x its Scale, so the smallest flags go coarser and to half rate first; its
        /// TimeScale slows the flags evenly when even that is too much. _demand: this frame's
        /// moving flags' cloth at real time, milliseconds of thread time a second.
        /// </summary>
        private readonly ClothLoad _load = new ClothLoad();
        private double _demand;
        private bool _loadSaid;

        /// <summary>
        /// Shared motion (MotionRecord, ShareGroup; the option, on by default): the groups by key;
        /// this frame's flags that could share (_sh*: size on the screen, what following saves, its
        /// key's number this frame) and the keys with two or more of them last frame (a group is
        /// made only for those: one flag alone saves nothing by a source of its own).
        /// </summary>
        private readonly List<ShareGroup> _groupList = new List<ShareGroup>();
        private readonly Dictionary<ShareKey, int> _frameKeys = new Dictionary<ShareKey, int>();
        private readonly List<int> _frameKeyCount = new List<int>();
        private readonly List<int> _frameKeyDetail = new List<int>();
        private readonly List<double> _frameKeyCost = new List<double>();
        private readonly List<ShareKey> _frameKeyList = new List<ShareKey>();
        private readonly Dictionary<ShareKey, int> _pairedLast = new Dictionary<ShareKey, int>();

        /// <summary>Like flags that may share, by cloth (ShareKey.Cloth), this frame; the cloths a crowd of them had last frame (<see cref="CrowdCount"/>).</summary>
        private readonly Dictionary<ShareKey, int> _crowdCount = new Dictionary<ShareKey, int>();
        private readonly Dictionary<ShareKey, bool> _crowdLast = new Dictionary<ShareKey, bool>();
        private float[] _shPixels = new float[64], _outPixels = new float[64];
        private double[] _shSaving = new double[64], _outSaving = new double[64], _outExtra = new double[16];
        private int[] _shKey = new int[64], _outGroup = new int[64], _keyRemap = new int[16];
        private int _shCount, _followers, _crowdFollowers, _planFrame;
        private bool _sharing = true, _groupsSaid;

        /// <summary>
        /// A CROWD (2026-11-07; the author, of 65 like flags round a roundabout freezing on 16
        /// threads: "We need to increase the Shared motion a LOT, especially if the flags are near
        /// each other and there's a lot of them. This isn't an edge case -- a lot of users will want
        /// flags in a circle very close together as part of a park or government building"): this
        /// many like flags in view that may share (one cloth, pole and hoist, whatever their wind)
        /// share motion from the start, whatever the load -- not only once the threads are out of
        /// budget, which came too late (each new source runs ahead ~1 s first, the flags frozen
        /// meanwhile). Each still plays the source at its own delay, turned to its own wind.
        /// </summary>
        private const int CrowdCount = 6;

        /// <summary>
        /// A crowd's flag bigger than this share of the screen's height steps on its own while
        /// the threads have room (the one the camera is close to stays its own); over budget the
        /// load shares it as any other (ClothLoad.Share).
        /// </summary>
        private const float CrowdOwnScreen = 0.25f;

        /// <summary>
        /// A flag's wind for its key and group is eased over this long, seconds (FlagInstance.
        /// ShareSpeed): Volumetric Weather's gusts pass each flag in seconds, and the key followed
        /// them.
        /// </summary>
        private const float ShareSpeedSeconds = 6f;

        /// <summary>A group with no follower this long, seconds, is dropped (its source with it).</summary>
        private const float GroupIdle = 3f;

        /// <summary>A flag that begins to follow blends from its own shape into the source's over this long, seconds.</summary>
        private const float BlendTime = 0.5f;

        /// <summary>At most this many groups (sources) at once.</summary>
        private const int MaxGroups = 24;

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

        /// <summary>
        /// A pole lying nearer level than upright (a banner's crossbar, a flag on a horizontal
        /// pole) is looked for only this many hoists along, and a couple of metres more: an arm
        /// from a wall the flag hangs near, not a beam to the next hill. Nothing that near: the
        /// arm is FlagPoleShape.Fallback long.
        /// </summary>
        private const float LevelPoleCastHoists = 2f;
        private const float LevelPoleCastMore = 2f;

        /// <summary>Props scanned per frame: the whole buffer every sixteen frames.</summary>
        private const int ScanPerFrame = 4096;

        /// <summary>
        /// The city a flag keeps off (FlagSurroundings) is gathered for at most this many flags a
        /// frame -- a surface is a few milliseconds of the main thread (the terrain every metre, a
        /// building's LOD triangles), capsules a few tenths -- the flag being placed first; a flag
        /// about to settle takes its own at once (it must not settle through a roof). Gathered
        /// again when the flag leaves the square it was gathered over, a building changes near it,
        /// or it is this old (seconds): a terrain edit, a building moved away from beside it, a
        /// tree planted.
        /// </summary>
        private const int SurfacesPerFrame = 1;
        private const int CapsulesPerFrame = 4;
        private const float SurfaceMaxAge = 30f;
        private const float CapsulesMaxAge = 3f;

        /// <summary>
        /// The wind's terrain (the buildings within CityRoughness.Reach: a few dozen grid cells, a
        /// tenth of a millisecond) is read for at most this many flags a frame, the flag being placed
        /// first; again when the flag has moved this far (metres) from where it was read, a building
        /// changes within its reach, or it is this old (seconds).
        /// </summary>
        private const int RoughnessPerFrame = 4;
        private const float RoughnessMove = 20f;
        private const float RoughnessMaxAge = 60f;

        /// <summary>
        /// The flag being placed, sliding with the pointer, gathers a new surface at most this often,
        /// seconds: a detailed building's LOD triangles in an 81 x 81 grid are a few milliseconds
        /// (2000 small triangles: 2 ms in .NET, ~10 in Unity's Mono). Meanwhile the old one holds
        /// where it reaches (it is the world's), the flat ground beyond it.
        /// </summary>
        private const float GhostSurfaceInterval = 0.2f;

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

        /// <summary>Line Tool's row this frame, and how many props there were when it last looked (a change: flags to find).</summary>
        private readonly List<LinePoint> _linePoints = new List<LinePoint>();
        private uint _propsCounted;

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

        /// <summary>
        /// Frames that threw, and cloths that went wrong (not a number), in this city: the first few
        /// are said (a frame's error with its stack), the rest counted -- a fault that comes back
        /// every frame would otherwise write sixty stacks a second into both logs.
        /// </summary>
        private int _frameFailures, _frameFailuresSaid, _wentWrong;
        private string _frameFailureLast;
        private const int MaxFrameFailuresSaid = 5;
        private const int MaxWentWrongSaid = 10;

        /// <summary>The options changed (FlagRenderer.ClothOptionsChanged): applied after the next Collect.</summary>
        private bool _optionsChanged;
        private bool _selfCollision = true;
        private bool _cityCollision = true;

        /// <summary>This frame's gatherings of the city (<see cref="SurfacesPerFrame"/>), the buildings changed, and the report's count.</summary>
        private int _surfacesThisFrame, _capsulesThisFrame, _roughnessThisFrame;
        private readonly List<Vector3> _changes = new List<Vector3>();
        private int _surfaceGathers, _capsuleGathers;
        private double _gatherMs;
        private bool _loggedGatherFailure;

        /// <summary>The flags planned this frame whose last step touched the city, and whose hoist stands under its surface.</summary>
        private int _touching, _blocked;

        /// <summary>Cloths found knotted and laid out afresh in this city (FlagCloth.Unknotted).</summary>
        private int _unknots;
        private int _finest = FlagCloth.CellsAlong;
        private float _pixelsPerRadian = 1000f;
        private int _detailChanges;

        private float _clock;

        /// <summary>
        /// The wind's direction as the flags follow it (TurnWind), degrees in the game's convention,
        /// and where it is turning to; this frame's wind (for the report); a turn said once.
        /// </summary>
        private float _windAngle, _windTarget;
        private bool _windTurnSet, _windTurnSaid;
        private Wind _wind;

        /// <summary>The real frame time the budget is planned on (ClothPlan.SmoothFrame), seconds.</summary>
        private float _frameTime;

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
            PoleTextures.Warn = Log.Warn;
            FlagImages.Note = Log.Msg;
            FlagImages.UserSource = FlagImageFolder.Bytes;
            _layer = PropLayer();
            _ready = true;

            ReadOptions();

            string problem;
            int threads = ClothWorker.ThreadsFor(SystemInfo.processorCount, _threadsChosen);
            _threadsStarted = _threadsChosen;
            string stepping = _worker.Start(threads, out problem)
                ? "cloth stepped on " + (_worker.Threads == 1 ? "a thread of its own" : _worker.Threads + " threads of its own") +
                  " (" + SystemInfo.processorCount + " processors here; " + (_threadsChosen > 0 ? "the option's" : "Auto") + ")" + (problem != null ? " -- " + (threads - _worker.Threads) + " did not start: " + problem : "")
                : "cloth stepped on the main thread (its own threads did not start: " + problem + ")";

            FlagSurroundings.Listen();

            Log.Msg("flags: renderer ready -- shader '" + shader.name + "' (" + _material.passCount + " passes), layer " + _layer +
                    " (" + LayerMask.LayerToName(_layer) + "), " + FlagRegistry.Count + " saved designs, prefab " +
                    (FlagPrefab.Info != null ? "index " + FlagPrefab.Info.m_prefabDataIndex : "MISSING") + ", " + stepping +
                    "; cloth detail up to " + UI.SettingsCatalog.DetailLogName(_finest) + ", self-collision " + (_selfCollision ? "on" : "off") +
                    ", keeps off the city (ground, water, roofs, walls, props, trees) " + (_cityCollision ? "on" : "off"));
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
            _cityCollision = Settings.CityCollision == null || Settings.CityCollision.value;
            _threadsChosen = Settings.ClothThreads != null ? Settings.ClothThreads.value : Settings.Defaults.ClothThreads;
            _sharing = Settings.SharedMotion == null || Settings.SharedMotion.value;
            _load.Sharing = _sharing;
        }

        /// <summary>The "Cloth threads" option as read, and as the threads running were started with.</summary>
        private int _threadsChosen, _threadsStarted;

        /// <summary>
        /// After a change of options, between batches (Collect has taken the last one, nothing is
        /// out): every cloth takes the new self-collision (the details follow in Refresh), and the
        /// threads are started again if their number changed.
        /// </summary>
        private void ApplyOptions()
        {
            if (!_optionsChanged)
                return;

            _optionsChanged = false;
            bool wasColliding = _selfCollision;
            ReadOptions();
            if (_selfCollision && !wasColliding)
                _layOutForCollision = true;
            if (_threadsChosen != _threadsStarted && _worker.Threaded)
            {
                _threadsStarted = _threadsChosen;
                int threads = ClothWorker.ThreadsFor(SystemInfo.processorCount, _threadsChosen);
                if (threads != _worker.Threads)
                {
                    string problem;
                    int was = _worker.Threads;
                    bool stopped = _worker.Stop();
                    bool started = _worker.Start(threads, out problem);
                    Log.Msg("flags: the cloth now steps on " + (started ? _worker.Threads + " thread(s)" : "the main thread") + " (was " + was + ")" +
                            (!stopped ? "; the old ones did not stop within 2 s (background threads: they end with the game)" : "") +
                            (problem != null ? "; " + problem : ""));
                    _load.Reset();
                }
            }
            foreach (FlagInstance f in _flags)
            {
                if (f.Cloth != null)
                    f.Cloth.SelfCollision = _selfCollision;
            }

            if (_ghost != null && _ghost.Cloth != null)
                _ghost.Cloth.SelfCollision = _selfCollision;

            // Sources keep their key's self-collision: under the new option no flag finds their
            // key, and they go (GroupIdle); with shared motion off, none follows and they all go.
        }

        /// <summary>
        /// Self-collision just switched on (the option, or a Quality level that has it): every
        /// waving cloth is to be laid out afresh once the flags move (<see cref="LayOutForCollision"/>).
        /// </summary>
        private bool _layOutForCollision;

        /// <summary>
        /// Lays every waving cloth out afresh, let down as a new one is (CalmTime), after
        /// self-collision came back on. While it was off a cloth may have folded through itself
        /// (a limp flag in a light wind does so within seconds); switched on, the collision keeps
        /// each crossed layer on the side it is on -- the wrong one -- and locks the fold, its links
        /// stretched until the knot check lays it out 2 s later. The author's run 2026-11-08
        /// 21:12:44: Quality to Ultra after 17 s without, 1 m/s, 46 of 75 flags knotted within 4 s
        /// (links 32-136% long), each seen tangled before it fell; and on his Mac (20:49:48) one
        /// 9.1 m flag. Laid out at once, they fall once, clean. Waits while the game is paused
        /// with the flags frozen, so no flag stands laid out in mid-air until it is unpaused.
        /// Between batches (after Collect): the cloths are the main thread's (Invariant 2); a
        /// layout is one of the moves Invariant 9 allows outside a step.
        /// </summary>
        private void LayOutForCollision()
        {
            _layOutForCollision = false;
            int laid = 0;
            foreach (FlagInstance f in _flags)
            {
                if (LayOutAfresh(f))
                    laid++;
            }

            if (_ghost != null)
                LayOutAfresh(_ghost);

            if (laid > 0)
                Log.Msg("flags: the cloth collides with itself again -- " + laid + " waving flag(s) laid out afresh, so no fold made while it was off is locked in");
        }

        /// <summary>
        /// A banner hoisted from its top in front of its back plate (FlagSpec.HasBackPlate): the
        /// plate through its hoist, its front the side the design reads from -- the flag's -Z,
        /// (sin a, 0, -cos a) for the heading a along which the hoist runs (FlagOrientation: a
        /// viewer there sees the hoist across the top from the left, as a hung flag is shown).
        /// A cloth that had none and lies behind it now (new, laid along a wind blowing at the
        /// plate; the option ticked or the hoist moved to the top while it flew back) is laid out
        /// afresh in front of it rather than pressed flat onto it in one step.
        /// </summary>
        private static void ApplyPlate(FlagCloth cloth, FlagInstance f)
        {
            if (!f.Spec.HasBackPlate)
            {
                cloth.ClearPlate();
                return;
            }

            bool had = cloth.HasPlate;
            cloth.SetPlate(f.Hoist.z, -f.Hoist.x);
            if (!had && cloth.HasPlate && cloth.BehindPlate > 0)
            {
                Vector3 dir = f.WindDir.sqrMagnitude > 1e-6f ? f.WindDir : f.Fly;
                cloth.LayOutClear(dir.x, dir.z);
                cloth.Calm(FlagCloth.CalmTime);
            }
        }

        private static bool LayOutAfresh(FlagInstance f)
        {
            if (f.Cloth == null || FlagMotion.KeepsShape(f.Spec.Motion))
                return false;

            Vector3 dir = f.WindDir;
            f.Cloth.LayOutClear(dir.x, dir.z);
            f.Cloth.Calm(FlagCloth.CalmTime);
            return true;
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
            _stillShapes.Clear();
            _settledLike.Clear();
            _groupList.Clear();
            DestroyLoops();

            FlagSurroundings.Stop();
            FlagPicking.Clear();

            // Whatever was only counted (see FrameFailed), said once with the city.
            if (_frameFailures > _frameFailuresSaid || _wentWrong > MaxWentWrongSaid)
                Log.Warn("flags: in this city " + _frameFailures + " frame(s) threw and " + _wentWrong + " cloth(s) went wrong (not a number)");

            if (Log.Detailed)
                Log.Detail("flags: " + FlagImages.Describe() + " let go with the city");
            FlagImages.Clear();
            PoleTextures.Clear();

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
            f.Pose = null;
            f.Surface = null;
            f.Capsules = null;
            f.Loop = null;
            f.LoopWanted = null;
            DropPole(f);
            if (f.Prop != 0)
                FlagPicking.Publish(f.Prop, null);
        }

        private static void DropPole(FlagInstance f)
        {
            if (f.PoleMesh != null)
                Destroy(f.PoleMesh);
            f.PoleMesh = null;
            if (f.PoleFinishMesh != null)
                Destroy(f.PoleFinishMesh);
            f.PoleFinishMesh = null;
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

            // The batch planned last frame: done, as a rule, while the game drew. Inside a try as
            // the frame is: a throw here escaped LateUpdate every frame, Unity logging each one.
            bool collected;
            try
            {
                collected = Collect();
            }
            catch (Exception e)
            {
                FrameFailed(e);

                // The batch's results let go, so the next frame plans afresh: left in place, the
                // same throw came back on the same jobs every frame and every flag stood still for
                // good (the 2026-11-07 sweep). Never while the thread still has them.
                if (_worker != null && !_worker.Running)
                {
                    _worker.Clear();
                    _batch.Clear();
                }

                return;
            }

            if (!collected)
                return;

            float dt = Mathf.Clamp(Time.deltaTime, 0f, 0.25f);
            float realDt = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.25f);
            _frameTime = ClothPlan.SmoothFrame(_frameTime, realDt);
            bool frozen =Paused && (Settings.FreezeWhenPaused == null || Settings.FreezeWhenPaused.value);
            if (frozen)
                dt = 0f;
            _clock += dt;
            _loopClock += dt;

            try
            {
                ApplyOptions();
                if (_layOutForCollision && !frozen)
                    LayOutForCollision();
                Scan();
                SetSun();

                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_camera);
                Vector3 eye = _camera.transform.position;
                Wind wind = TurnWind(Wind.Current(), dt);
                _wind = wind;
                _pixelsPerRadian = _camera.pixelHeight / (2f * Mathf.Tan(0.5f * Mathf.Clamp(_camera.fieldOfView, 1f, 170f) * Mathf.Deg2Rad));
                Shader.SetGlobalFloat(FlagPoleMesh.IdPixelRadius, 0.6f / Mathf.Max(_pixelsPerRadian, 1f));
                _detailChanges = 0;
                _refineChanges = 0;
                LogCamera();

                LineFlags.Tick();
                AskLine();
                ApplyGhost(eye, planes);
                Refresh(eye, planes);

                foreach (FlagInstance f in _flags)
                    Draw(f);

                if (_ghost != null)
                {
                    Draw(_ghost);
                    DrawLine();
                }

                _surfacesThisFrame = 0;
                _capsulesThisFrame = 0;
                _roughnessThisFrame = 0;
                _settleGatherMs = 0.0;
                _touching = 0;
                _blocked = 0;
                MarkChanges();
                Plan(dt, frozen, realDt, wind);
            }
            catch (Exception e)
            {
                FrameFailed(e);
            }

            _worker.Run();
            _cost.Stop();
            Report();
        }

        /// <summary>
        /// The frame threw (the flags skip the rest of it): said with its stack the first time, and
        /// again for each different error up to <see cref="MaxFrameFailuresSaid"/>; every one counted.
        /// </summary>
        private void FrameFailed(Exception e)
        {
            _frameFailures++;
            string what = e.GetType().Name + ": " + e.Message;
            if (what == _frameFailureLast || _frameFailuresSaid >= MaxFrameFailuresSaid)
                return;

            _frameFailureLast = what;
            _frameFailuresSaid++;
            Log.Error("flags: the frame threw; flags are paused until the next frame" +
                      (_frameFailuresSaid == MaxFrameFailuresSaid ? " (further errors are only counted)" : " (the same error again is only counted)"), e);
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
                if (jobs[i].Work != null)
                {
                    // A loop being made (LoopBake, no flag of its own): one that threw is not made.
                    var bake = jobs[i].Work as LoopBake;
                    if (bake != null && jobs[i].Failure != null)
                        bake.Threw(jobs[i].Failure);
                    continue;
                }

                if (f == null)
                {
                    // A shared motion's source (no flag of its own): a job that threw is dropped
                    // with its group, whose followers take its last steps and step on their own.
                    _stepsThisReport += jobs[i].Steps;
                    if (jobs[i].Failure != null)
                    {
                        if (_failures++ == 0)
                            Log.Error("flags: stepping a shared motion's source threw; dropped (further ones are counted in the detailed log)", jobs[i].Failure);
                        DropGroup(jobs[i].Cloth);
                    }

                    continue;
                }

                bool pose = f.Pose != null && jobs[i].Cloth == f.Pose.Cloth;
                if (jobs[i].Cloth != f.Cloth && !pose)
                    continue;

                if (jobs[i].Failure != null)
                {
                    // A job that throws is a bug: said once with its stack, then counted.
                    if (_failures++ == 0)
                        Log.Error("flags: stepping flag #" + f.Prop + "'s cloth threw; laid out again (further ones are counted in the detailed log)",
                                  jobs[i].Failure);
                    if (pose)
                    {
                        // A still flag whose shape could not be made: flat, rather than none (not kept for others).
                        f.Pose = null;
                        Adopt(f, StillCloth(f, FlagMotion.Flat));
                        continue;
                    }

                    f.Cloth.Reset(jobs[i].DirX, jobs[i].DirZ);
                    f.Cloth.Calm(FlagCloth.CalmTime);
                    f.MeshDirty = true;
                    continue;
                }

                if (pose)
                {
                    if (jobs[i].WentWrong && _wentWrong++ < MaxWentWrongSaid)
                        Log.Warn("flags: flag #" + f.Prop + "'s still shape went wrong (not a number); laid out again and run on");
                    f.Pose.Took();
                    _stepsThisReport += jobs[i].Steps;
                    continue;
                }

                // Said for the first few; a cloth that keeps going wrong (something in reach of it
                // that is not a number) would otherwise say it every frame.
                if (jobs[i].WentWrong && _wentWrong++ < MaxWentWrongSaid)
                    Log.Warn("flags: flag #" + f.Prop + "'s cloth went wrong (not a number); laid out again" +
                             (_wentWrong == MaxWentWrongSaid ? " (further ones are counted in the detailed log)" : ""));

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

            // Line Tool's row, as soon as it is placed: all of it found now, not over 16 frames
            // (seconds at the author's frame rates, the preview gone and the flags not there yet).
            if (LineFlags.IsOut)
            {
                uint count = Singleton<PropManager>.instance.m_props.ItemCount();
                if (count != _propsCounted)
                {
                    _propsCounted = count;
                    _scanCursor = 1;
                    end = buffer.Length;
                }
            }

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
                // flag copied before the mod was there): the new flag's design.
                spec = Settings.NewSpec();
                FlagRegistry.Set(prop, spec);
                Log.Msg("flags: flag prop #" + prop + " had no design; given the new flag's (" + spec.Describe() + ")");
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
            float now = Time.realtimeSinceStartup;

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

                // Placed by a tool that puts props on the ground: stood there as the mod's own tool
                // stands a flag (FlagStand: a standing pole's foot on it, else the hoist), once, or
                // the flag hangs underground.
                if (!fixedHeight && _heightFixed.Add(f.Prop))
                {
                    float rise = StandRise(f.Spec, angle);
                    if (LineFlags.IsOut)
                        rise += FlagTool.Lift;
                    FlagProps.FixHeight(f.Prop, rise);
                    Log.Msg("flags: flag #" + f.Prop + " stood on the ground; raised " + rise.ToString("F1") + " m to stand there, and fixed");
                }

                FlagSpec spec;
                if (FlagRegistry.TryGet(f.Prop, out spec) && !Same(spec, f.Spec))
                    SetSpec(f, spec);

                Place(f, anchor, angle);
                Cull(f, eye, planes);
                WantLoop(f, now);
                KeepOrRelease(f, now);
                ChooseDetail(f);
                ChooseRefine(f);
                UpdatePole(f, planes);
            }

            foreach (uint prop in _gone)
                Remove(prop);
        }

        /// <summary>
        /// A flag out of view and under <see cref="KeepPixels"/> for <see cref="ReleaseSeconds"/>
        /// lets its cloth go (<see cref="ReleaseUnseen"/>); in view, or big enough, it keeps it.
        /// Never the selected flag, one being handled, or one whose still shape is being made.
        /// </summary>
        private void KeepOrRelease(FlagInstance f, float now)
        {
            if (f.Visible || f.Pixels >= KeepPixels)
            {
                f.UnseenSince = 0f;
                return;
            }

            if (f.Cloth == null && f.Pose == null)
                return;

            if (f.UnseenSince == 0f)
            {
                f.UnseenSince = now;
                return;
            }

            if (now - f.UnseenSince < ReleaseSeconds || f.Prop == FlagEditor.Selected || f.AwakeUntil > now || f.Pose != null)
                return;

            ReleaseUnseen(f);
        }

        /// <summary>
        /// The flag's cloth, mesh, city and pole let go, and everything that would hold a reference
        /// to them (a kept shape for like flags, a shared motion), the flag put back to how a new
        /// one stands before its cloth is made: PlanFlag makes it again when the flag is next in
        /// view (its pick volume gone meanwhile, the game's own test finds it: FlagPatches).
        /// </summary>
        private void ReleaseUnseen(FlagInstance f)
        {
            Release(f);
            ForgetSettled(f);
            f.Follow = null;
            f.FollowCloth = null;
            f.FollowShown = float.NaN;
            f.FromX = f.FromY = f.FromZ = null;
            f.Settled = false;
            f.SettleLeft = SettleTime;
            f.SurfaceAt = 0f;
            f.SurfaceStale = false;
            f.CapsulesAt = 0f;
            f.CapsulesTopFor = null;
            f.LaidOut = false;
            f.PoseWanted = true;
            f.WindAt10 = -1f;
            f.Waiting = false;
            f.WaitedLast = false;
            f.Pending = 0f;
            f.Fresh = false;
            f.MeshDirty = true;
            f.BlockReady = false;
            f.UnseenSince = 0f;
            _released++;
        }

        private readonly List<ShareKey> _pruneKeys = new List<ShareKey>();
        private int _released;

        /// <summary>The shapes kept for like flags no longer name this flag (its cloth is going).</summary>
        private void ForgetSettled(FlagInstance f)
        {
            if (_settledLike.Count == 0)
                return;

            _pruneKeys.Clear();
            foreach (KeyValuePair<ShareKey, SettledLike> kv in _settledLike)
            {
                if (kv.Value.Flag == f)
                    _pruneKeys.Add(kv.Key);
            }

            foreach (ShareKey k in _pruneKeys)
                _settledLike.Remove(k);
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
        /// The pole under a flag that has one: its length the flag's own (Spec.PoleLength: it goes
        /// where the flag goes, Move It raising it raises the pole too -- a standing pole's is the
        /// Pole height it was placed with) or, while it has none (a banner's crossbar, an older
        /// save's pole, one sent down to the ground again), found along its axis on the simulation
        /// thread and then kept -- the flag being placed looks again wherever it goes; its mesh made
        /// again when its size changed, the cloth told to keep off it; and whether it is in view (it
        /// can be when the flag is not). A flag without one has its mesh and collider dropped.
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

            if (f.Spec.PoleLength > 0f)
            {
                // Its own: never found again as the flag moves (the first version's pole reached
                // down to the ground again after every move, so Move It raising a flag stretched
                // its pole instead of lifting it).
                f.PoleLength = f.Spec.PoleLength;
            }
            else
            {
                // Where the axis crosses the anchor's level, from the last shape's offset (a
                // centimetre or two either way until the first cast is back).
                float offset = f.PoleMesh != null ? f.PoleShape.Offset : FlagPoleShape.For(f.Spec, Mathf.Max(f.Anchor.y - f.Ground, 1f)).Offset;
                Vector3 from = f.Anchor - f.Fly * offset;
                if (f.PoleCastAsked == 0 || (from - f.PoleCastFrom).sqrMagnitude > 1e-4f || (f.Hoist - f.PoleCastAlong).sqrMagnitude > 1e-6f)
                    CastPole(f, from);

                int done = f.PoleCastDone;
                if (done != 0 && done == f.PoleCastAsked)
                {
                    f.PoleLength = f.PoleCastResult;

                    // Found: the flag's own from now on (the ghost keeps looking as it moves) --
                    // unless its prop has moved or turned since this frame read it (a move queued
                    // before the cast has run with it): then it is looked for again from where
                    // the flag stands now, never kept for where it stood.
                    if (f.Prop != 0)
                    {
                        Vector3 now;
                        float angleNow;
                        bool fixedNow;
                        if (FlagProps.TryRead(f.Prop, out now, out angleNow, out fixedNow) && (now - f.Anchor).sqrMagnitude < 1e-6f &&
                            Mathf.Abs(Mathf.DeltaAngle(angleNow * Mathf.Rad2Deg, f.Angle * Mathf.Rad2Deg)) < 1e-3f)
                            KeepPoleLength(f, f.PoleLength);
                        else
                            f.PoleCastAsked = 0;
                    }
                }
                else if (f.PoleLength < 0f)
                {
                    // Until the cast is back: down to the ground, or a crossbar's arm.
                    f.PoleLength = FlagStand.Upright(f.Hoist.y) ? Mathf.Max(f.Anchor.y - f.Ground, 0f) / -f.Hoist.y : FlagPoleShape.Fallback(f.Spec);
                }
            }

            FlagPoleShape shape = FlagPoleShape.For(f.Spec, f.PoleLength);
            if (f.PoleMesh == null || !shape.Same(f.PoleShape))
            {
                f.PoleMesh = FlagPoleMesh.Build(shape, f.PoleMesh);
                if (f.PoleFinishMesh != null)
                    f.PoleFinishMesh = FlagPoleMesh.BuildPlain(shape, f.PoleFinishMesh);
                f.PoleShape = shape;
                f.PoleApplied = false;
            }

            // A still or flat flag's cloth is never stepped: its shape was made without the pole (StillPose), and keeps it.
            if (f.Cloth != null && !f.PoleApplied && !FlagMotion.KeepsShape(f.Spec.Motion))
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

            // Drawn while its length is DrawPixels on the screen (its tubes keep 0.6 px wide).
            Vector3 eye = _camera.transform.position;
            float pixels = shape.Tip / Mathf.Max(Vector3.Distance(eye, box.ClosestPoint(eye)), 1f) * _pixelsPerRadian;
            f.PoleVisible = pixels >= DrawPixels && GeometryUtility.TestPlanesAABB(planes, box);
        }

        /// <summary>
        /// A flag's pole has found its length: the flag's own from now on -- in its spec, so the
        /// save and Move It's copies keep it, and it moves with the flag. The registry's spec is
        /// written as it stands (a redesign on its way still comes through Refresh).
        /// </summary>
        private static void KeepPoleLength(FlagInstance f, float length)
        {
            float kept = Mathf.Clamp(length, FlagPoleShape.MinLength, FlagSpec.MaxPoleLength);
            FlagSpec own = f.Spec;
            own.PoleLength = kept;
            f.Spec = own;

            FlagSpec stored;
            if (FlagRegistry.TryGet(f.Prop, out stored))
            {
                stored.PoleLength = kept;
                FlagRegistry.Set(f.Prop, stored);
            }

            if (Log.Detailed)
                Log.Detail("flags: flag #" + f.Prop + "'s pole reaches " + kept.ToString("F1") + " m down its axis; kept as the flag's own");
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

            // Upright, down to the ground however far; nearer level, an arm's length at most.
            float reach = FlagStand.Upright(along.y) ? PoleCastLength : LevelPoleCastHoists * spec.Height + LevelPoleCastMore;

            try
            {
                Singleton<SimulationManager>.instance.AddAction(() =>
                {
                    float distance;
                    bool hit;
                    try
                    {
                        hit = FlagTool.CastAlong(from, along, reach, out distance);
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
            // A still flag keeps the Normal cloth its shape was made on: a finer grid would only
            // spread the same shape over more particles (the drawn surface is refined anyway). A
            // cloth still being let down into the air falls at the detail it was made at: made finer
            // mid-fall, its fine cells whipped through one another (FlagCloth.CalmTime).
            if (!f.Visible || (f.Cloth != null && (!f.Settled || f.Cloth.Calming)) || FlagMotion.KeepsShape(f.Spec.Motion))
                return;

            // A follower too (since 2026-11-07): it plays its source on whatever grid it has
            // (MotionRecord.PlayResampled), so its detail follows its size on the screen as any
            // flag's -- its mesh costs what its size asks. A remade cloth rejoins, blended.

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

            // A few a frame; a few more coarser ones (a load being shed: ClothLoad).
            if (_detailChanges >= (wanted < f.Detail ? MaxDetailChanges + MaxCoarserChanges : MaxDetailChanges))
                return;

            _detailChanges++;
            FlagCloth old = f.Cloth;
            var cloth = new FlagCloth(old.Length, old.Height, old.Fabric, 1f, 0f, wanted, 0f, -1f, 0f, old.Cut, old.CutDepth);
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

            // At INFO the first time; after that in the detailed log only, at most once a second: a
            // camera mod that zooms by the field of view changes it every frame of a zoom.
            float now = Time.realtimeSinceStartup;
            if (_cameraLines > 0 && (!Log.Detailed || now < _nextCameraLine))
                return;

            _loggedCamera = key;
            _nextCameraLine = now + 1f;
            string text = string.Empty, big = string.Empty;
            int[] details = FlagCloth.Details;
            for (int i = 1; i < details.Length; i++)
            {
                // A detail is taken from where the cells of the one below it pass PixelsPerCell. The
                // finest refine only flags whose cells are not at FlagCloth.MinCell already: told
                // for a 10 m flag.
                int c0, r0, c1, r1;
                FlagCloth.GridFor(3f, 2f, details[i - 1], out c0, out r0);
                FlagCloth.GridFor(3f, 2f, details[i], out c1, out r1);
                bool small = c1 != c0 || r1 != r0;
                float within = (small ? 3f : 10f) * _pixelsPerRadian / (details[i - 1] * PixelsPerCell);
                string part = UI.SettingsCatalog.DetailLogName(details[i]) + " within " + within.ToString("F0") + " m";
                if (small)
                    text += (text.Length > 0 ? ", " : "") + part;
                else
                    big += (big.Length > 0 ? ", " : "") + part;
            }

            if (big.Length > 0)
                text += "; a 10 m flag " + big;

            string line = "flags: camera " + _camera.pixelHeight + " px high, " + _camera.fieldOfView.ToString("F1") + " deg: a 3 m flag gets " + text +
                          " (the option allows up to " + UI.SettingsCatalog.DetailLogName(_finest) + "); it is drawn to " +
                          (3f * _pixelsPerRadian / DrawPixels).ToString("F0") + " m, moves to " + (3f * _pixelsPerRadian / MovePixels).ToString("F0") +
                          " m, at the full rate within " + (3f * _pixelsPerRadian / FullRatePixels).ToString("F0") + " m";
            if (_cameraLines++ == 0)
                Log.Msg(line);
            else
                Log.Detail(line);
        }

        private int _cameraLines;
        private float _nextCameraLine;

        /// <summary>
        /// The coarsest detail whose cells are at most <see cref="PixelsPerCell"/> (x the load's
        /// scale, ClothLoad) for a flag this many pixels long, within the option's finest.
        /// </summary>
        private int DetailFor(float pixels)
        {
            float cells = pixels / (PixelsPerCell * _load.Scale);
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
            ForgetSettled(f);
            // Last frame's in-view order may still hold it (InView): nothing of it is planned again.
            f.HasAnchor = false;
            f.Visible = false;
            f.InOrder = false;
            f.Follow = null;
            f.FollowCloth = null;
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
                   a.SameShape(b) && Mathf.Approximately(a.Waviness, b.Waviness) && Mathf.Approximately(a.WindShare, b.WindShare) &&
                   a.Motion == b.Motion && a.Name == b.Name && a.BackPlate == b.BackPlate &&
                   (a.Country ?? string.Empty) == (b.Country ?? string.Empty) && a.SamePole(b) && a.SamePoleColours(b) && a.SameHoist(b) &&
                   Mathf.Approximately(a.Darkness, b.Darkness) && a.SameImage(b) && a.SameWeather(b);
        }

        private static void SetSpec(FlagInstance f, FlagSpec spec)
        {
            bool reshape = !spec.SameShape(f.Spec);
            bool refabric = !Mathf.Approximately(spec.Waviness, f.Spec.Waviness);

            // A still or flat flag whose shape changes has it made again -- showing its old one,
            // resized if need be, meanwhile; one that waves again flies on from the shape it kept.
            // A loop's is looked up again (PlanLoop): the design may ask for another.
            f.LoopWanted = null;
            if (!FlagMotion.KeepsShape(spec.Motion))
            {
                f.Pose = null;
                f.PoseWanted = false;
            }
            else if (spec.Motion != f.Spec.Motion || !spec.SamePose(f.Spec))
            {
                f.Pose = null;
                f.PoseWanted = true;
            }

            // Its pole's length let go (a crossbar turned, "Pole down to the ground"): found again.
            if (spec.PoleLength <= 0f && f.Spec.PoleLength > 0f)
                f.PoleCastAsked = 0;

            // A pole that stands turned to a crossbar or back: nothing of the old length is drawn
            // along the new line meanwhile (a 25 m crossbar until its wall is found).
            if (FlagStand.PoleStands(spec) != FlagStand.PoleStands(f.Spec))
                f.PoleLength = -1f;
            f.Spec = spec;
            f.BlockReady = false;
            f.PoleBlockReady = false;
            f.PoleApplied = false;
            if (reshape)
                f.Hold.Clear();
            Wake(f);

            if (reshape && f.Cloth != null)
            {
                // A new size: a new cloth in the old one's shape, so a flag being resized in the
                // panel keeps flying (dropped and settled again, it vanished while the slider moved).
                FlagCloth old = f.Cloth;
                var cloth = new FlagCloth(spec.Length, spec.Height, FlagFabric.Standard(spec.Waviness), 1f, 0f, f.Detail, 0f, -1f, 0f,
                                          spec.Cut, spec.CutDepth);
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
        /// (carried, and dragged by as much as it can follow as it steps: FlagCloth.MoveAnchor;
        /// turned whole onto a new hoist), and a paused game steps it for a moment.
        /// </summary>
        private static void Place(FlagInstance f, Vector3 anchor, float angle)
        {
            // A still or flat flag goes with its prop as it is (its cloth is the anchor's, and never stepped).
            // A waving one is carried too until it steps (its particles are the anchor's): its mesh holds.
            bool still = FlagMotion.KeepsShape(f.Spec.Motion);
            if (f.HasAnchor && f.Cloth != null && !still)
            {
                Vector3 d = anchor - f.Anchor;
                if (d.sqrMagnitude > 1e-8f)
                {
                    f.Cloth.MoveAnchor(d.x, d.y, d.z);
                    Wake(f);
                }
            }

            if (f.HasAnchor && Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, f.Angle * Mathf.Rad2Deg)) > 1e-3f)
            {
                if (still && f.Cloth != null)
                {
                    // Turned whole with its prop (a shape being made is made in the flag's own frame, and turned when taken).
                    f.Cloth.Turn(angle - f.Angle);
                    f.MeshDirty = true;
                    f.Hold.Clear();
                }

                Wake(f);
                f.PoleApplied = false;
            }

            if (!f.HasAnchor || (anchor - f.Anchor).sqrMagnitude > 1e-4f)
                f.Ground = FlagProps.GroundAt(anchor);

            f.Anchor = anchor;
            f.Angle = angle;
            f.HasAnchor = true;

            float hx, hy, hz, fx, fy, fz;
            FlagOrientation.Frame(angle, f.Spec.Tilt, f.Spec.Lean, f.Spec.HoistedFrom, out hx, out hy, out hz, out fx, out fy, out fz);
            f.Hoist = new Vector3(hx, hy, hz);
            f.Fly = new Vector3(fx, fy, fz);

            FlagCloth c = f.Cloth;
            if (c != null && (Mathf.Abs(c.HoistX - hx) > 1e-5f || Mathf.Abs(c.HoistY - hy) > 1e-5f || Mathf.Abs(c.HoistZ - hz) > 1e-5f))
            {
                c.SetHoist(hx, hy, hz);
                f.MeshDirty = true;
                f.PoleApplied = false;
                f.Hold.Clear();
                Wake(f);
            }
        }

        private void Cull(FlagInstance f, Vector3 eye, Plane[] planes)
        {
            f.Distance = Vector3.Distance(eye, f.Anchor);

            // The box of the cloth as last drawn (a loop's: of all its frames), or, before it is, of any shape it could take.
            Bounds box;
            if (ShowsLoop(f))
            {
                box = LoopBox(f);
            }
            else if (f.Mesh != null)
            {
                box = f.Mesh.Bounds;
                box.center += f.Anchor;
            }
            else
            {
                // Round the anchor, as far as the cloth reaches any way it hangs (from its top too).
                float reach = f.Spec.Length + f.Spec.Height;
                box = new Bounds(f.Anchor, new Vector3(2f * reach, 2f * reach, 2f * reach));
            }

            // Its longer side on the screen: what it is drawn and moved by (DrawPixels).
            f.Pixels = Mathf.Max(f.Spec.Length, f.Spec.Height) / Mathf.Max(f.Distance, 1f) * _pixelsPerRadian;

            f.World = box;
            f.Visible = f.Pixels >= DrawPixels && GeometryUtility.TestPlanesAABB(planes, box);
        }

        /// <summary>
        /// The next batch: the placement ghost first, then the flags in view -- the
        /// <see cref="MaxMoving"/> biggest on the screen, at least <see cref="MovePixels"/> there,
        /// move -- as long as the planned work fits the <see cref="Budget"/> (the first planned
        /// always moves). Those that had no room last frame go before the rest, biggest first
        /// among each: with more cloth than the threads have time for, the flags take turns, all
        /// of them slower, where biggest first alone gave the same flags every frame's room and
        /// left the others standing still for as long as it lasted.
        /// </summary>
        private void Plan(float dt, bool frozen, float realDt, Wind wind)
        {
            double spent = 0.0;
            int settles = 0;
            _moving = 0;
            _waiting = 0;
            _demand = 0.0;
            float now = Time.realtimeSinceStartup;

            // Shared motion's sources first (they always step: each stands for several flags), on
            // their followers' wind of last frame; then this frame's followers counted afresh.
            _planFrame++;
            _followers = 0;
            _crowdFollowers = 0;
            _shCount = 0;
            _frameKeys.Clear();
            _frameKeyList.Clear();
            _frameKeyCount.Clear();
            _frameKeyDetail.Clear();
            _frameKeyCost.Clear();
            _crowdCount.Clear();
            PlanSources(dt * _load.TimeScale, realDt, wind, ref spent);

            // The loops being made (FlagMotion.Loop): a job each, before the flags that wait for them.
            PlanLoops(now, ref spent);

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
                _order[i].WaitedLast = _order[i].Waiting;

            for (int pass = 0; pass < 2; pass++)
            {
                bool waited = pass == 0;
                for (int i = 0; i < n; i++)
                {
                    FlagInstance f = _order[i];
                    if (f.WaitedLast != waited)
                        continue;

                    bool moving = i < MaxMoving && f.Pixels >= MovePixels;

                    // Paused, frozen: only the flags being handled step, for a moment, slowing to a still.
                    // Under a load even the coarsest cannot take, the rest slow evenly (ClothLoad);
                    // a flag in the player's hand keeps real time.
                    float flagDt = f.AwakeUntil > now ? dt : dt * _load.TimeScale;
                    if (frozen)
                    {
                        flagDt = ClothPlan.PausedTime(realDt, f.AwakeUntil - now, WakeFade);
                        f.OwnTime += flagDt;
                    }

                    PlanFlag(f, flagDt, wind, moving, false, ref spent, ref settles);
                }
            }

            // With the budget the flags in view left: the nearest out of view settle, so a turn
            // of the camera finds them flying.
            PlanUnseen(wind, ref spent, ref settles);
            TimeBurst(n);

            ShareableToLoad();

            // What the flags in view wanted against what the threads have, both at real time (not
            // while frozen: the few flags handled then say nothing of the city's load).
            if (!frozen && _load.Add(_demand, Budget / Math.Max(_frameTime, 1e-3f), realDt))
                SayLoad();
        }

        /// <summary>
        /// The flags out of view still to settle (no cloth yet, or settling; a still flag's shape
        /// not made), at least <see cref="MovePixels"/> on the screen were they turned to: the
        /// <see cref="UnseenPerFrame"/> nearest planned with the budget left -- never past it, and
        /// none of the settles' minimum (that is the flags in view's).
        /// </summary>
        private void PlanUnseen(Wind wind, ref double spent, ref int settles)
        {
            if (spent >= Budget || settles >= MaxSettlesPerFrame)
                return;

            int n = 0;
            foreach (FlagInstance f in _flags)
            {
                // Only the flags big enough to keep their cloth out of view (KeepPixels): smaller
                // ones would be settled here and let go again by turns.
                if (!f.HasAnchor || f.Visible || f.Pixels < KeepPixels)
                    continue;

                // (A flag that plays a loop has nothing to settle: it takes its loop the frame it is seen.)
                bool due = FlagMotion.KeepsShape(f.Spec.Motion) ? f.Cloth == null || f.Pose != null
                         : f.Spec.Motion == FlagMotion.Loop ? f.Cloth != null && !f.Settled
                         : f.Cloth == null || !f.Settled;
                if (!due)
                    continue;

                // The nearest few, kept in order (no sort of them all: a city may hold thousands).
                if (n == UnseenPerFrame && f.Distance >= _unseen[n - 1].Distance)
                    continue;

                int i = n < UnseenPerFrame ? n++ : n - 1;
                while (i > 0 && _unseen[i - 1].Distance > f.Distance)
                {
                    _unseen[i] = _unseen[i - 1];
                    i--;
                }

                _unseen[i] = f;
            }

            _unseenPass = true;
            try
            {
                for (int i = 0; i < n; i++)
                {
                    if (spent >= Budget || settles >= MaxSettlesPerFrame)
                        break;
                    PlanFlag(_unseen[i], 0f, wind, false, false, ref spent, ref settles);
                }
            }
            finally
            {
                _unseenPass = false;
                for (int i = 0; i < n; i++)
                    _unseen[i] = null;
            }
        }

        /// <summary>
        /// The settles' room for one more flag this frame at <paramref name="cost"/> ms: the ghost
        /// always; out of view (<see cref="PlanUnseen"/>) only within the budget; in view the first
        /// <see cref="MinSettlesPerFrame"/> whatever it holds, then as the budget has room.
        /// </summary>
        private bool SettleRoom(bool ghost, int settles, double cost, double spent)
        {
            if (ghost)
                return true;
            if (settles >= MaxSettlesPerFrame)
                return false;
            if (!_unseenPass && settles < MinSettlesPerFrame)
                return true;
            return spent + cost <= Budget;
        }

        /// <summary>
        /// A flag about to settle may have the city round it read at once: the ghost always; the
        /// rest while the settles have room and the frame's reads for them are under
        /// <see cref="SettleGatherMs"/> (the first <see cref="MinSettlesPerFrame"/> in view whatever).
        /// </summary>
        private bool SettleGatherRoom(bool ghost, int settles, double spent)
        {
            if (ghost)
                return true;
            if (settles >= MaxSettlesPerFrame || (_unseenPass && spent >= Budget))
                return false;
            return (!_unseenPass && settles < MinSettlesPerFrame) || _settleGatherMs < SettleGatherMs;
        }

        /// <summary>
        /// A burst of settling in view (a city loaded, a paste of many flags) timed from its first
        /// frame -- a flag waiting to settle, or one settled -- to the last such frame, once
        /// <see cref="BurstQuiet"/> has passed with none: said once at INFO when it held 8 or more,
        /// in the detailed log otherwise. (The first version began a burst only on a flag seen
        /// waiting: a flag settled from a like one's shape settles in the frame it is first planned,
        /// so the author's 88 flags pasted back, 73 of them copied, were never timed.)
        /// </summary>
        private void TimeBurst(int inView)
        {
            int waiting = 0;
            for (int i = 0; i < inView; i++)
            {
                FlagInstance f = _order[i];
                if (f.Loop == null && (FlagMotion.KeepsShape(f.Spec.Motion) ? f.Cloth == null : f.Cloth == null || !f.Settled))
                    waiting++;
            }

            // This frame's settles in view (PlanFlag counts them), into the burst if there is one.
            int settled = _frameSettled, copies = _frameCopies;
            _frameSettled = 0;
            _frameCopies = 0;
            float now = Time.realtimeSinceStartup;
            if (waiting > 0 || settled > 0)
            {
                if (_burstSince < 0f)
                {
                    _burstSince = now - _frameTime;
                    _burstSettled = 0;
                    _burstCopies = 0;
                }

                _burstSettled += settled;
                _burstCopies += copies;
                _burstLast = now;
                return;
            }

            if (_burstSince < 0f || now - _burstLast < BurstQuiet)
                return;

            string line = "flags: " + _burstSettled + " flag(s) settled and shown in " + (_burstLast - _burstSince).ToString("F1") + " s (" +
                          _burstCopies + " of them from a like flag's shape)";
            if (_burstSettled >= 8 && !_burstSaid)
            {
                _burstSaid = true;
                Log.Msg(line);
            }
            else if (Log.Detailed && _burstSettled > 0)
            {
                Log.Detail(line);
            }

            _burstSince = -1f;
        }

        private bool _burstSaid;

        /// <summary>The load's scale changed: said once at INFO the first time it is not none, then in the detailed log.</summary>
        private void SayLoad()
        {
            string line = "flags: the flags in view want " + (_load.Ratio * 100.0).ToString("F0") + "% of the " +
                          (_worker.Threaded ? _worker.Threads + " cloth thread(s)'" : "main thread's") + " budget -- " + LoadText();
            if (!_loadSaid && _load.Scale > 1f)
            {
                _loadSaid = true;
                Log.Msg(line + " (the smallest flags on the screen go coarser and to half rate first; the option 'Cloth threads' gives the cloth more room)");
            }
            else if (Log.Detailed)
            {
                Log.Detail(line);
            }
        }

        /// <summary>For the log: what the load does now.</summary>
        private string LoadText()
        {
            string shared = _load.Share > 0f
                ? "like flags under " + _load.Share.ToString("F0") + " px on the screen share motion (" + _followers + " following " + _groupList.Count + " source(s)" +
                  (_crowdFollowers > 0 ? ", " + _crowdFollowers + " of them in crowds" : "") + ")"
                : _crowdFollowers > 0 || _groupList.Count > 0
                    ? "crowds of like flags share motion (" + _followers + " following " + _groupList.Count + " source(s))"
                    : "";
            if (shared.Length > 0 && (_load.Scale > 1f || _load.TimeScale < 1f))
                shared += "; ";
            if (_load.Scale <= 1f && _load.TimeScale >= 1f)
                return shared.Length > 0 ? shared : "every flag as fine as its size on the screen asks";
            return shared + "a cloth cell up to " + (PixelsPerCell * _load.Scale).ToString("F0") + " px, the full rate from " +
                   (FullRatePixels * _load.Scale).ToString("F0") + " px on the screen" +
                   (_load.TimeScale < 1f ? ", the flags at " + (_load.TimeScale * 100f).ToString("F0") + "% of real time" : "");
        }

        /// <summary>
        /// The flags in view into <see cref="_order"/>, the biggest on the screen first (of flags
        /// of one size, the nearest); the others lose their untaken time (a flag coming back into
        /// view does not catch up). An insertion sort, as Volumetric Explosions sorts its volumes:
        /// no allocation (List.Sort with a comparison allocates on Mono), and the order changes
        /// little from one frame to the next.
        /// </summary>
        private int InView()
        {
            // Last frame's order first, less the flags no longer in view (or gone): the ones still
            // there keep their places, so the insertion sort below moves only what the frame moved.
            // The first version refilled it from the flags' list each frame (prop order: random in
            // size), so the sort was the count squared every frame: a million moves at 2000 in
            // view (the 2026-11-07 sweep).
            int n = 0;
            for (int i = 0; i < _orderUsed; i++)
            {
                FlagInstance f = _order[i];
                if (f.HasAnchor && f.Visible)
                    _order[n++] = f;
                else
                    f.InOrder = false;
            }

            foreach (FlagInstance f in _flags)
            {
                if (!f.HasAnchor)
                    continue;

                if (!f.Visible)
                {
                    f.Pending = 0f;
                    f.Waiting = false;
                    f.InOrder = false;
                    continue;
                }

                if (f.InOrder)
                    continue;

                f.InOrder = true;
                if (n == _order.Length)
                    Array.Resize(ref _order, n * 2);
                _order[n++] = f;
            }

            for (int i = 1; i < n; i++)
            {
                FlagInstance x = _order[i];
                int j = i - 1;
                while (j >= 0 && _order[j].Pixels < x.Pixels)
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

        /// <summary>
        /// A still or flat flag: its shape made when it is wanted -- flat at once; still, from the
        /// shapes kept, or run a few steps a frame -- then taken, and never stepped again.
        /// </summary>
        private void PlanStill(FlagInstance f, bool ghost, ref double spent, ref int settles)
        {
            f.Pending = 0f;
            f.WindDir = f.Fly;

            // Its shape's last steps came back with this frame's batch: the smoothest moment kept,
            // and shown -- turned from the flag's own frame to where it heads.
            if (f.Pose != null && f.Pose.Finished)
            {
                StillPoseRun done = f.Pose;
                f.Pose = null;
                done.Finish();
                StillShape shape = KeepStillShape(f, done.Cloth);
                FlagCloth cloth = StillCloth(f, FlagMotion.Still);
                if (shape.Columns == cloth.Columns && shape.Rows == cloth.Rows)
                    cloth.SetShape(shape.X, shape.Y, shape.Z, f.Angle);
                Adopt(f, cloth);
                if (Log.Detailed)
                    Log.Detail("flags: flag #" + f.Prop + "'s still shape made (" + done.Total + " steps of 1/32 s, the moment at " +
                               (done.BestStep * StillPose.StepSize).ToString("F2") + " s kept)");
            }

            if (f.Cloth == null && f.Pose == null)
                f.PoseWanted = true;

            if (f.PoseWanted)
            {
                f.PoseWanted = false;
                StartStill(f);
            }

            if (f.Pose == null)
                return;

            // The flag being placed always gets its steps (the player is watching for it).
            int sub = f.Pose.SubSteps;
            int steps = (int)Math.Max(1.0, ClothPlan.BudgetFor(_frameTime, Step, MaxStepsPerFrame, PoseStepsPerFrame) / sub);
            if (!SettleRoom(ghost, settles, steps * sub * f.Pose.Cloth.Count * _unitCost, spent))
                return;

            settles++;
            ClothJob job = f.Pose.Next(steps);
            spent += job.Steps * sub * f.Pose.Cloth.Count * _unitCost;
            Queue(f, ref job);
        }

        /// <summary>
        /// A still or flat flag's shape begins: flat, taken at once; still, taken from the shapes
        /// kept, or its run begun -- in the flag's own frame (heading 0), whatever way the flag
        /// heads, so the shape is its design's alone (made at another heading, the float rounding of
        /// the turned frame would grow, as a cloth's motion does, into another shape).
        /// </summary>
        private void StartStill(FlagInstance f)
        {
            int motion = f.Spec.Motion;
            FlagCloth cloth = StillCloth(f, motion);
            if (motion != FlagMotion.Still)
            {
                Adopt(f, cloth);
                return;
            }

            string key = StillKey(f.Spec);
            StillShape shape;
            if (_stillShapes.TryGetValue(key, out shape) && shape.Columns == cloth.Columns && shape.Rows == cloth.Rows)
            {
                cloth.SetShape(shape.X, shape.Y, shape.Z, f.Angle);
                Adopt(f, cloth);
                return;
            }

            float hx, hy, hz, fx, fy, fz;
            FlagOrientation.Frame(0f, f.Spec.Tilt, f.Spec.Lean, f.Spec.HoistedFrom, out hx, out hy, out hz, out fx, out fy, out fz);
            FlagCloth own = StillCloth(f.Spec, motion, hx, hy, hz, fx, fy, fz);
            f.Pose = new StillPoseRun(own, fx, fy, fz, f.Spec.Waviness, f.Spec.Length);
            f.PoseKey = key;
        }

        /// <summary>A Normal cloth for a still or flat flag where it heads, laid out along its fly at rest (StillPose.Lay).</summary>
        private FlagCloth StillCloth(FlagInstance f, int motion)
        {
            return StillCloth(f.Spec, motion, f.Hoist.x, f.Hoist.y, f.Hoist.z, f.Fly.x, f.Fly.y, f.Fly.z);
        }

        private FlagCloth StillCloth(FlagSpec spec, int motion, float hx, float hy, float hz, float fx, float fy, float fz)
        {
            var cloth = new FlagCloth(spec.Length, spec.Height, FlagFabric.Standard(spec.Waviness), fx, fz, FlagCloth.CellsAlong, hx, hy, hz,
                                      spec.Cut, spec.CutDepth);
            cloth.SelfCollision = _selfCollision;
            StillPose.Lay(cloth, motion, spec.OnHalyard, fx, fy, fz);
            return cloth;
        }

        /// <summary>What makes a still flag's shape (FlagSpec.SamePose, and the self-collision option): the key its shape is kept by.</summary>
        private string StillKey(FlagSpec spec)
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            return spec.Length.ToString("R", inv) + "|" + spec.Proportion + "|" + spec.HoistedFrom + "|" + spec.Tilt.ToString("R", inv) + "|" +
                   spec.Lean.ToString("R", inv) + "|" + spec.Waviness.ToString("R", inv) + "|" + (spec.OnHalyard ? 1 : 0) + "|" +
                   (_selfCollision ? 1 : 0) + "|" + spec.Cut + "|" + (FlagCut.UsesDepth(spec.Cut) ? spec.CutDepth.ToString("R", inv) : "");
        }

        /// <summary>A still shape made in the flag's own frame (heading 0): kept for the next flag like it.</summary>
        private StillShape KeepStillShape(FlagInstance f, FlagCloth own)
        {
            var shape = new StillShape
            {
                Columns = own.Columns,
                Rows = own.Rows,
                X = new float[own.Count],
                Y = new float[own.Count],
                Z = new float[own.Count],
            };
            own.CopyShape(shape.X, shape.Y, shape.Z, 0f);
            if (f.PoseKey != null)
            {
                if (_stillShapes.Count >= MaxStillShapes)
                    _stillShapes.Clear();
                _stillShapes[f.PoseKey] = shape;
                f.PoseKey = null;
            }

            return shape;
        }

        /// <summary>The flag shows this cloth from now on (a still or flat flag's shape made), at Normal, in a new mesh.</summary>
        private void Adopt(FlagInstance f, FlagCloth cloth)
        {
            if (f.Mesh != null)
                f.Mesh.Destroy();
            f.Cloth = cloth;
            f.Detail = cloth.Detail;
            f.Mesh = new FlagMesh(cloth, f.Refine);
            f.Refine = f.Mesh.Refine;
            f.Settled = true;
            f.Pending = 0f;
            f.Fresh = false;
            f.ShownAlpha = 1f;
            f.MeshDirty = true;
            f.PoleApplied = false;
            f.Hold.Clear();
        }

        /// <summary>One flag into the batch: its cloth made if needed, then a moment of settling, or this frame's steps.</summary>
        /// <summary>
        /// The wind at 10 m the flag flies in: the speed of the moment where it stands, followed
        /// over FlagWind.FollowSeconds of its own clock -- taken at once the first time, and after a
        /// long while unplanned (out of view, it was never followed).
        /// </summary>
        private float FollowWind(FlagInstance f, float now10)
        {
            float clock = _clock + f.OwnTime;
            float gap = clock - f.WindClock;
            f.WindClock = clock;
            if (f.WindAt10 < 0f || gap < 0f || gap > 4f * FlagWind.FollowSeconds)
                f.WindAt10 = now10;
            else
                f.WindAt10 = FlagWind.Follow(f.WindAt10, now10, gap);
            return f.WindAt10;
        }

        /// <summary>
        /// The city's wind turned as the flags follow it (FlagWind.Turn, real time on the flags'
        /// clock): Volumetric Weather's or the game's direction, or the player's fixed one. Along
        /// each flag (the first mode) is each flag's own heading, never turned here. Taken at once
        /// in a new city.
        /// </summary>
        private Wind TurnWind(Wind wind, float dt)
        {
            if (wind.Mode == Settings.WindModes.EachFlag)
            {
                _windTurnSet = false;
                return wind;
            }

            float target = Mathf.Repeat(Mathf.Atan2(wind.Fixed.x, wind.Fixed.z) * Mathf.Rad2Deg, 360f);
            if (!_windTurnSet)
            {
                _windTurnSet = true;
                _windAngle = target;
            }
            else
            {
                _windAngle = FlagWind.Turn(_windAngle, target, dt);
            }

            // A turn of the wind is said once as it begins (the detailed log), not while it goes on.
            float left = Mathf.Abs(Mathf.DeltaAngle(_windAngle, target));
            if (left > 20f && !_windTurnSaid)
            {
                _windTurnSaid = true;
                Log.Detail("wind: turning from " + _windAngle.ToString("F0") + " to " + target.ToString("F0") + " deg (" + left.ToString("F0") +
                           " deg): the flags follow most of the way in " + FlagWind.TurnSeconds.ToString("F0") + " s, at most " +
                           FlagWind.TurnMost.ToString("F0") + " deg a second");
            }
            else if (left < 2f)
            {
                _windTurnSaid = false;
            }

            _windTarget = target;
            float a = _windAngle * Mathf.Deg2Rad;
            wind.Fixed = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            return wind;
        }

        /// <summary>For the 5 s line: ", toward 274 deg" or ", toward 274 deg (turning to 300)"; nothing along each flag.</summary>
        private string WindTurnText()
        {
            if (!_windTurnSet)
                return "";
            bool turning = Mathf.Abs(Mathf.DeltaAngle(_windAngle, _windTarget)) >= 1f;
            return ", toward " + _windAngle.ToString("F0") + " deg" + (turning ? " (turning to " + _windTarget.ToString("F0") + ")" : "");
        }

        private void PlanFlag(FlagInstance f, float dt, Wind wind, bool moving, bool ghost, ref double spent, ref int settles)
        {
            f.Waiting = false;

            if (FlagMotion.KeepsShape(f.Spec.Motion))
            {
                PlanStill(f, ghost, ref spent, ref settles);
                return;
            }

            // A loop: nothing to step once it has it. Until its loop is made a flag in hand, or
            // one that has a cloth, waves as below; so does one whose loop could not be made.
            if (f.Spec.Motion == FlagMotion.Loop && PlanLoop(f, ghost))
                return;

            Vector3 start = wind.DirectionFor(f.Angle);
            if (f.Cloth == null)
            {
                // Made and settled at 18 cells (six 1/30 s steps a frame at 40 would be 35 ms of
                // one thread), then made finer as its size on the screen asks (ChooseDetail),
                // hanging from its hoist's line (FlagOrientation, set by Place).
                f.Detail = FlagCloth.CellsAlong;
                f.Cloth = new FlagCloth(f.Spec.Length, f.Spec.Height, FlagFabric.Standard(f.Spec.Waviness), start.x, start.z, f.Detail,
                                        f.Hoist.x, f.Hoist.y, f.Hoist.z, f.Spec.Cut, f.Spec.CutDepth);
                f.Cloth.SelfCollision = _selfCollision;

                // A banner in front of its back plate is laid out in front of it, whatever the wind.
                ApplyPlate(f.Cloth, f);

                // Let down into the air while it settles and a moment after, not dropped flat (FlagCloth.CalmDamping).
                f.Cloth.Calm(FlagCloth.CalmTime);
                f.Mesh = new FlagMesh(f.Cloth, f.Refine);
                f.Refine = f.Mesh.Refine;
                f.Settled = false;
                f.SettleLeft = SettleTime;
                f.SettleClock = _clock - SettleTime;
                f.Pending = 0f;
                f.Fresh = false;
                f.BlockReady = false;
                f.LaidOut = false;
                f.Hold.Clear();

                // Its pole, if it has one and it is known yet (else the next Refresh sees to it).
                f.PoleApplied = false;
                if (f.Spec.HasPole && f.PoleMesh != null)
                {
                    f.PoleShape.ApplyTo(f.Cloth, f.Fly.x, f.Fly.y, f.Fly.z);
                    f.PoleApplied = true;
                }
            }

            f.Cloth.GroundY = f.Ground - f.Anchor.y;

            // Its back plate, every frame: the cloth may be new (a detail, a size), the hoist turned, the option changed.
            ApplyPlate(f.Cloth, f);

            // Settled and not moving this frame (small on the screen, or past MaxMoving): nothing
            // of the city, the wind or the air is worth reading for it -- its city is read again
            // when it next moves (KeepOffCity: old or stale by then), its wind taken afresh
            // (FollowWind's gap). The pick volume's axis alone. Every flag in view paid for all of
            // it each frame before, the thousands past the 160 that move included (the sweep).
            if (f.Settled && !moving)
            {
                f.WindDir = wind.DirectionFor(f.Angle);
                f.Pending = 0f;
                return;
            }

            // The city round it: at once for a flag about to settle (it must not settle through a
            // roof), while the frame has room for those reads; a new cloth laid out the first way
            // free of it, not into a wall.
            bool gatherNow = !f.Settled && SettleGatherRoom(ghost, settles, spent);
            double gathered = _gatherMs;
            KeepOffCity(f, ghost, gatherNow);
            if (gatherNow)
                _settleGatherMs += _gatherMs - gathered;
            if (!f.Settled && !f.LaidOut && f.Surface != null && _cityCollision)
            {
                f.Cloth.LayOutClear(start.x, start.z);
                f.LaidOut = true;
            }
            if (f.Cloth.Relaid > f.Relaid && Log.Detailed)
                Log.Detail("flags: " + (f.Prop != 0 ? "flag #" + f.Prop : "the new flag") + "'s cloth was buried in a building (moved into it): laid out afresh where it is free");
            f.Relaid = f.Cloth.Relaid;

            Vector3 dir = wind.DirectionFor(f.Angle);
            f.WindDir = dir;
            float height = Mathf.Max(f.Anchor.y - f.Ground, 0.5f);
            // Its share of the wind where it stands (FlagSpec.WindShare, "Wind on it"): followed as
            // the wind is, so a change on the slider eases in over FlagWind.FollowSeconds.
            float at10 = FollowWind(f, wind.SpeedFor(f.Anchor, f.Ground) * f.Spec.WindShare);
            WindTerrain(f, ghost, height);
            float speed = FlagWind.ClothSpeed(at10, height, f.Roughness);

            // Knotted (FlagCloth.KnotStretch): said while there are few, with where its worst link
            // was and the wind at the flag, counted in the report after.
            if (f.Cloth.Unknotted > f.Unknotted)
            {
                _unknots++;
                if (_unknots <= 20)
                    Log.Msg("flags: " + (f.Prop != 0 ? "flag #" + f.Prop : "the new flag") + "'s cloth was knotted (a link " +
                            ((FlagCloth.KnotStretch - 1f) * 100f).ToString("F0") + "% long or more for " + FlagCloth.KnotTime.ToString("F0") +
                            " s; at last " + ((f.Cloth.KnotLink - 1f) * 100f).ToString("F0") + "%, column " + f.Cloth.KnotColumn + " and row " +
                            f.Cloth.KnotRow + " of " + f.Cloth.Columns + "x" + f.Cloth.Rows + ", in " + speed.ToString("F1") +
                            " m/s): laid out afresh where it is free" + (_unknots == 20 ? " (further ones are counted in the detailed log)" : ""));
            }

            f.Unknotted = f.Cloth.Unknotted;
            var job = new ClothJob
            {
                Cloth = f.Cloth,
                Speed = at10,
                Gusts = wind.Gusts,
                GustInSpeed = wind.FromWeatherMod,
                DirX = dir.x,
                DirZ = dir.z,
                Height = height,
                Waviness = f.Spec.Waviness,
                Length = f.Spec.Length,
                AnchorX = f.Anchor.x,
                AnchorZ = f.Anchor.z,
                Roughness = f.Roughness,
            };

            if (!f.Settled)
            {
                // Run it a moment first, a little each frame, so a flag appears hanging or flying,
                // not flat as a board. The ghost always gets its moment (the player is watching it).
                // Its city not read yet (no room this frame for the read): it waits, rather than
                // settle through a roof.
                if (!ghost && _cityCollision && f.SurfaceAt == 0f)
                    return;

                int settleSub = f.Cloth.SubStepsFor(speed, 2f * Step);
                double stepCost = settleSub * f.Cloth.Count * _unitCost;
                if (!SettleRoom(ghost, settles, stepCost, spent))
                    return;

                // Not begun yet: from a like flag's shape, if one has settled, and only a moment of its own.
                if (!ghost && f.SettleLeft >= SettleTime - 1e-4f && TryCopySettled(f, dir, speed))
                {
                    f.SettleLeft = CopiedSettleTime;
                    f.SettleClock = _clock - CopiedSettleTime;
                    _settleCopies++;
                    if (!_unseenPass)
                        _frameCopies++;
                }

                settles++;
                int left = Math.Max(1, Mathf.CeilToInt(f.SettleLeft / (2f * Step) - 1e-3f));
                job.Steps = ghost
                    ? Math.Min(left, Math.Max(1, 6 / settleSub))
                    : ClothPlan.SettleSteps(stepCost, Budget / Math.Max(1, _worker.Threads), Budget - spent, left);
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
                    if (!ghost && _unseenPass)
                    {
                        _settledUnseen++;
                    }
                    else if (!ghost)
                    {
                        _settledSeen++;
                        _frameSettled++;
                    }

                    if (!ghost)
                        KeepSettled(f, dir, speed);
                }

                spent += job.Steps * stepCost;
                Queue(f, ref job);
                return;
            }

            if (!moving)
            {
                f.Pending = 0f;
                return;
            }

            // Shared motion: one of the smallest of like flags while the threads are out of budget
            // plays its group's source back (Follows); one that stops following steps on from there.
            if (Follows(f, ghost, dir, at10, speed, height))
                return;

            _moving++;

            // Full rate while FullRatePixels on the screen, half below, with a tenth's band either
            // side (a half-rate step takes twice the sub-steps once the wind carries the air a
            // cell in one: it saves in light winds only). The ghost is always in the player's hand;
            // and a flag whose cloth can reach the ground, or the top of what stands round it, is
            // always at the full rate: heaped on it in 1/30 s steps of a single sub-step, the cloth
            // went through itself (the knots harness, 2026-09-29: a 2.8 m flag on a 3.7 m pole, 14
            // knots in 15 min at half rate, none at full, at 3, 5 and 8 fps).
            // Under load the full rate's size grows with the rest (ClothLoad): the small go to half first.
            bool low = CanReachGround(f);
            float fullRate = FullRatePixels * _load.Scale;
            if (f.StepSize <= Step && !ghost && !low && f.Pixels < fullRate / 1.1f)
                f.StepSize = 2f * Step;
            else if (f.StepSize > Step && (ghost || low || f.Pixels > fullRate * 1.1f))
                f.StepSize = Step;
            float size = f.StepSize;
            int sub = f.Cloth.SubStepsFor(speed, size);

            // Its cloth at real time, ms of thread time a second: the load's measure (ClothLoad).
            _demand += sub * f.Cloth.Count * _unitCost / size;

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

        // ---- shared motion ----------------------------------------------------------------------

        /// <summary>
        /// A waving flag in view, settled and moving: if it can share (CanShare: in no one's hand,
        /// nothing of the city or the ground in its reach), it is counted for the load, and if it
        /// is one of a CROWD of like flags (<see cref="CrowdCount"/>, under
        /// <see cref="CrowdOwnScreen"/> of the screen) or the load shares flags its size
        /// (ClothLoad.Share), and a group of its cloth in about its wind is ready
        /// (<see cref="FindGroup"/>), it plays that source back this frame (true). Otherwise it
        /// steps on its own -- from where it was shown, if it was following (Promote).
        /// </summary>
        private bool Follows(FlagInstance f, bool ghost, Vector3 dir, float at10, float speed, float height)
        {
            float now = Time.realtimeSinceStartup;
            ShareGroup g = null;
            if (CanShare(f, ghost, now))
            {
                ShareKey key = KeyFor(f, dir, speed);
                bool crowd = _crowdLast.ContainsKey(key.Cloth) && f.Pixels < CrowdOwnScreen * Screen.height;
                Shareable(f, key, speed, crowd);
                if (_sharing && (crowd || f.Pixels < _load.Share))
                {
                    // A ready group for it, and one for it at its detail (perhaps still running
                    // ahead) -- made if there is none and its key has two or more.
                    ShareGroup fits;
                    g = FindGroup(f, key, out fits);
                    if (fits == null && _pairedLast.ContainsKey(key) && _groupList.Count < MaxGroups)
                        fits = NewGroup(key, f, dir);
                    if (fits != null)
                    {
                        fits.Members++;
                        SourceWind(fits, f, dir, at10, speed, height);
                    }

                    if (g != null && g != fits)
                    {
                        g.Members++;
                        SourceWind(g, f, dir, at10, speed, height);
                    }

                    if (g != null && crowd)
                        _crowdFollowers++;
                }
            }

            if (g == null || !Play(f, g, dir, now))
            {
                if (f.Follow != null)
                    Promote(f);
                return false;
            }

            return true;
        }

        /// <summary>
        /// The group <paramref name="f"/> plays this frame, or null: its own while it is ready and
        /// still Near (a flag changes source only when it must), else the nearest ready one of its
        /// cloth whose source is at most a notch coarser than the flag. In
        /// <paramref name="fits"/> a group of its cloth Near it at such a detail, ready or not (none:
        /// the caller may make one) -- while a finer one runs ahead, the flag keeps the one it
        /// follows.
        /// </summary>
        private ShareGroup FindGroup(FlagInstance f, ShareKey key, out ShareGroup fits)
        {
            fits = null;
            bool upright = ShareKey.Upright(f.Spec);
            int notch = DetailNotch(f.Detail);
            ShareGroup own = f.Follow != null && !f.Follow.Dead ? f.Follow : null;
            ShareGroup ready = null;
            float best = float.MaxValue;
            for (int i = 0; i < _groupList.Count; i++)
            {
                ShareGroup g = _groupList[i];
                if (!g.Key.SameCloth(key) || !key.Near(f.ShareSpeed, f.ShareDegrees, g.KeySpeed, g.KeyDegrees, upright))
                    continue;

                bool fine = DetailNotch(g.Detail) >= notch - 1;
                if (fine && (fits == null || g == own || (fits != own && g.Warm && !fits.Warm)))
                    fits = g;
                if (!g.Warm || !fine)
                    continue;

                float off = Mathf.Abs(Mathf.Log(Mathf.Max(f.ShareSpeed, 0.3f) / Mathf.Max(g.KeySpeed, 0.3f)));
                if (g == own)
                    off = -1f;
                if (off < best)
                {
                    best = off;
                    ready = g;
                }
            }

            // A finer source still running ahead: the one it follows meanwhile, if still Near.
            if (ready == null && own != null && own.Warm && own.Key.SameCloth(key) &&
                key.Near(f.ShareSpeed, f.ShareDegrees, own.KeySpeed, own.KeyDegrees, upright))
                ready = own;
            return ready;
        }

        /// <summary>A detail's place in FlagCloth.Details (0 = 18 cells).</summary>
        private static int DetailNotch(int detail)
        {
            int[] d = FlagCloth.Details;
            for (int i = d.Length - 1; i >= 0; i--)
            {
                if (detail >= d[i])
                    return i;
            }

            return 0;
        }

        /// <summary>
        /// The flags that may follow: waving, settled and let down, on a cloth of known pole, not
        /// the ghost, not handled (awake) or selected, and with nothing to keep off -- the ground,
        /// the surface's top (CanReachGround) and the props' and trees' capsules out of its reach,
        /// the surface read (a source keeps off nothing).
        /// </summary>
        private bool CanShare(FlagInstance f, bool ghost, float now)
        {
            if (ghost || f.Prop == 0 || !f.Settled || f.Cloth == null || f.Cloth.Calming || f.AwakeUntil > now || f.Prop == FlagEditor.Selected)
                return false;
            if (f.Spec.HasPole && !f.PoleApplied)
                return false;
            if (_cityCollision && f.Surface == null)
                return false;
            if (CanReachGround(f))
                return false;
            return !CapsulesInReach(f);
        }

        // ---- settling from a like flag ----------------------------------------------------------

        /// <summary>
        /// A flag whose shape a like one may take, or that may take one (2026-11-01): a placed flag,
        /// waving, at the detail it settles at (18 cells: the copy is particle for particle), its
        /// pole's collider on, and nothing of the city in its reach -- the ground, the surface's top,
        /// the props' and trees' capsules (CanShare's test: a shape made against a wall is that
        /// wall's alone). Neither handled nor selected.
        /// </summary>
        private bool CopyFit(FlagInstance f)
        {
            if (f.Prop == 0 || f.Cloth == null || FlagMotion.KeepsShape(f.Spec.Motion) || f.Cloth.Detail != FlagCloth.CellsAlong)
                return false;
            if (f.Prop == FlagEditor.Selected || f.AwakeUntil > Time.realtimeSinceStartup)
                return false;
            if (f.Spec.HasPole && !f.PoleApplied)
                return false;
            if (_cityCollision && f.Surface == null)
                return false;
            if (CanReachGround(f))
                return false;
            return !CapsulesInReach(f);
        }

        /// <summary>A flag has settled: its shape is the one a new like flag takes, while it keeps that cloth.</summary>
        private void KeepSettled(FlagInstance f, Vector3 dir, float speed)
        {
            if (!CopyFit(f))
                return;

            if (_settledLike.Count >= MaxSettledLike)
                _settledLike.Clear();
            _settledLike[KeyFor(f, dir, speed)] = new SettledLike { Flag = f, Cloth = f.Cloth };
        }

        /// <summary>
        /// A new flag takes the shape and motion of a like flag that has settled (the same
        /// ShareKey: its design's cloth, pole, wind at it against its heading), turned to it as a
        /// follower is turned to its source -- an upright hoist by the wind, else by the heading
        /// (FlagCloth.CopyMotion). A row of like flags settles once, not once each. False (it
        /// settles on its own) when none is kept, the one kept has gone or been made finer, or
        /// either has the city in reach.
        /// </summary>
        private bool TryCopySettled(FlagInstance f, Vector3 dir, float speed)
        {
            if (_settledLike.Count == 0 || !CopyFit(f))
                return false;

            ShareKey key = KeyFor(f, dir, speed);
            SettledLike like;
            if (!_settledLike.TryGetValue(key, out like))
                return false;

            FlagInstance d = like.Flag;
            FlagInstance alive;
            bool usable = d != f && d.Cloth == like.Cloth && d.Settled && _byProp.TryGetValue(d.Prop, out alive) && alive == d && CopyFit(d);
            if (!usable)
            {
                _settledLike.Remove(key);
                return false;
            }

            float turn = ShareKey.Upright(f.Spec) ? WindAngleOf(dir) - WindAngleOf(d.WindDir) : f.Angle - d.Angle;
            if (!f.Cloth.CopyMotion(d.Cloth, turn))
                return false;

            f.MeshDirty = true;
            f.Hold.Clear();
            return true;
        }

        private bool CapsulesInReach(FlagInstance f)
        {
            CapsuleSet set = f.Capsules;
            if (!_cityCollision || set == null || set.Count == 0)
                return false;

            if (f.CapsulesTopFor != set)
            {
                float top = float.MinValue;
                for (int i = 0; i < set.Count; i++)
                    top = Mathf.Max(top, set.Top[i] + set.Radius[i]);
                f.CapsulesTop = top;
                f.CapsulesTopFor = set;
            }

            return f.CapsulesTop + f.CapsulesOrigin.y > f.Anchor.y - (1.02f * f.Spec.Length + f.Spec.Height + 0.5f);
        }

        /// <summary>
        /// Its key (ShareKey.For): its design's cloth, its pole's collider, and its wind against
        /// its heading -- the wind eased over <see cref="ShareSpeedSeconds"/> (kept in
        /// f.ShareSpeed / ShareDegrees for FindGroup), once a plan frame.
        /// </summary>
        private ShareKey KeyFor(FlagInstance f, Vector3 dir, float speed)
        {
            float off = 0f, radius = 0f;
            if (f.Spec.HasPole)
            {
                off = f.PoleShape.Offset;
                radius = f.PoleShape.RadiusAt(f.PoleShape.Length);
            }

            if (f.ShareFrame != _planFrame)
            {
                f.ShareFrame = _planFrame;
                f.ShareDegrees = ShareKey.Wrap((WindAngleOf(dir) - f.Angle) * Mathf.Rad2Deg);
                f.ShareSpeed = f.ShareSpeed < 0f
                    ? speed
                    : f.ShareSpeed + (speed - f.ShareSpeed) * (1f - Mathf.Exp(-Mathf.Max(_frameTime, 0f) / ShareSpeedSeconds));
            }

            return ShareKey.For(f.Spec, f.ShareSpeed, f.ShareDegrees, off, radius, _selfCollision);
        }

        private static float WindAngleOf(Vector3 dir)
        {
            return Mathf.Atan2(dir.z, dir.x);
        }

        /// <summary>
        /// A flag that may follow: counted by key (a new group's key needs two; its finest detail
        /// is the source's) and by cloth (a crowd); for the load (ClothLoad.SetShareable) its size,
        /// what following saves, its key this frame -- not a crowd's flag, which shares whatever the
        /// load (counting it would have the load raise its share for nothing).
        /// </summary>
        private void Shareable(FlagInstance f, ShareKey key, float speed, bool crowd)
        {
            int id;
            if (!_frameKeys.TryGetValue(key, out id))
            {
                id = _frameKeyList.Count;
                _frameKeys[key] = id;
                _frameKeyList.Add(key);
                _frameKeyCount.Add(0);
                _frameKeyDetail.Add(0);

                // Its source, were one started: a flag like this one at the full rate.
                _frameKeyCost.Add(f.Cloth.SubStepsFor(speed, Step) * f.Cloth.Count * _unitCost / Step);
            }

            _frameKeyCount[id]++;
            _frameKeyDetail[id] = Math.Max(_frameKeyDetail[id], f.Detail);

            ShareKey cloth = key.Cloth;
            int alike;
            _crowdCount.TryGetValue(cloth, out alike);
            _crowdCount[cloth] = alike + 1;
            if (crowd)
                return;

            if (_shCount == _shPixels.Length)
            {
                Array.Resize(ref _shPixels, _shCount * 2);
                Array.Resize(ref _shSaving, _shCount * 2);
                Array.Resize(ref _shKey, _shCount * 2);
            }

            double own = f.Cloth.SubStepsFor(speed, f.StepSize) * f.Cloth.Count * _unitCost / f.StepSize;
            _shPixels[_shCount] = f.Pixels;
            _shSaving[_shCount] = own - PlayCost(f);
            _shKey[_shCount] = id;
            _shCount++;
        }

        /// <summary>A follower's playback, ms of thread time a second (one a frame).</summary>
        private double PlayCost(FlagInstance f)
        {
            return (double)f.Cloth.Count / ClothWorker.PlayShare * _unitCost / Math.Max(_frameTime, 1e-3f);
        }

        /// <summary>This frame's flags that may follow, in keys of two or more, to the load; the keys kept for the next frame's new groups.</summary>
        private void ShareableToLoad()
        {
            int keys = _frameKeyList.Count;
            if (_keyRemap.Length < keys)
            {
                _keyRemap = new int[keys * 2];
                _outExtra = new double[keys * 2];
            }

            // Read by the next frame's Follows (a new group only for a key with two or more, at
            // the finest detail its flags had; a crowd's flags share whatever the load).
            _pairedLast.Clear();
            _crowdLast.Clear();
            foreach (KeyValuePair<ShareKey, int> c in _crowdCount)
            {
                if (c.Value >= CrowdCount)
                    _crowdLast[c.Key] = true;
            }

            int groups = 0;
            for (int k = 0; k < keys; k++)
            {
                _keyRemap[k] = -1;
                if (_frameKeyCount[k] < 2)
                    continue;

                ShareKey key = _frameKeyList[k];
                _pairedLast[key] = _frameKeyDetail[k];
                _keyRemap[k] = groups;
                _outExtra[groups] = HasGroupFor(key) ? 0.0 : _frameKeyCost[k];
                groups++;
            }

            if (_outPixels.Length < _shCount)
            {
                _outPixels = new float[_shCount * 2];
                _outSaving = new double[_shCount * 2];
                _outGroup = new int[_shCount * 2];
            }

            int m = 0;
            for (int i = 0; i < _shCount; i++)
            {
                int g = _keyRemap[_shKey[i]];
                if (g < 0)
                    continue;
                _outPixels[m] = _shPixels[i];
                _outSaving[m] = _shSaving[i];
                _outGroup[m] = g;
                m++;
            }

            _load.SetShareable(_outPixels, _outSaving, _outGroup, m, _outExtra, groups);
        }

        /// <summary>A group runs for flags of this key: one of its cloth whose wind is in the key's steps or one either side.</summary>
        private bool HasGroupFor(ShareKey key)
        {
            for (int i = 0; i < _groupList.Count; i++)
            {
                ShareKey g = _groupList[i].Key;
                if (g.SameCloth(key) && Math.Abs(g.Speed - key.Speed) <= 1 && (g.Angle == key.Angle || Math.Abs(g.Angle - key.Angle) == 1))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// A new group for <paramref name="key"/>: its source a copy of this flag's cloth (TakeShape,
        /// motion and all) where it heads, at the finest detail the key's flags had last frame
        /// (never coarser than this flag's), its pole's collider and halyard as the flag's.
        /// </summary>
        private ShareGroup NewGroup(ShareKey key, FlagInstance f, Vector3 dir)
        {
            int detail;
            if (!_pairedLast.TryGetValue(key, out detail) || detail < f.Detail)
                detail = f.Detail;
            var source = new FlagCloth(f.Spec.Length, f.Spec.Height, FlagFabric.Standard(f.Spec.Waviness), dir.x, dir.z, detail,
                                       f.Hoist.x, f.Hoist.y, f.Hoist.z, f.Cloth.Cut, f.Cloth.CutDepth);
            source.SelfCollision = key.SelfCollision;
            if (f.Spec.HasPole)
                f.PoleShape.ApplyTo(source, f.Fly.x, f.Fly.y, f.Fly.z);
            if (f.Spec.HasBackPlate)
                source.SetPlate(f.Hoist.z, -f.Hoist.x);
            source.TakeShape(f.Cloth, true);

            var g = new ShareGroup
            {
                Key = key,
                Detail = detail,
                KeySpeed = f.ShareSpeed,
                KeyDegrees = f.ShareDegrees,
                Source = source,
                Record = new MotionRecord(source.Columns, source.Rows),
                Angle = f.Angle,
                WindAngle = f.Angle + f.ShareDegrees * Mathf.Deg2Rad,
                Upright = ShareKey.Upright(f.Spec),
                Founder = f.Prop,
                Waviness = f.Spec.Waviness,
                Length = f.Spec.Length,
            };
            g.DirX = Mathf.Cos(g.WindAngle);
            g.DirZ = Mathf.Sin(g.WindAngle);

            _groupList.Add(g);
            int alike;
            _crowdCount.TryGetValue(key.Cloth, out alike);
            if (!_groupsSaid)
            {
                _groupsSaid = true;
                Log.Msg("flags: shared motion begins -- like flags (" + f.Spec.Length.ToString("F1") + " m, " + UI.SettingsCatalog.DetailLogName(detail) +
                        ", " + alike + " of them in view) play one cloth back, each at its own delay -- " +
                        (_crowdLast.ContainsKey(key.Cloth) ? "a crowd of " + CrowdCount + " or more shares always" : "the cloth threads are out of budget"));
            }
            else if (Log.Detailed)
            {
                Log.Detail("flags: a shared motion source made from flag #" + f.Prop + " (" + f.Spec.Length.ToString("F1") + " m, " +
                           UI.SettingsCatalog.DetailLogName(detail) + ", " + source.Columns + "x" + source.Rows + ", wind " + f.ShareSpeed.ToString("F1") +
                           " m/s at " + f.ShareDegrees.ToString("F0") + " deg from its heading; " + alike + " like flag(s) in view; " + _groupList.Count + " source(s))");
            }

            return g;
        }

        /// <summary>
        /// The source's air this frame: the first follower's (its speed, height, roughness and place
        /// for the gusts), its speed eased over FlagWind.FollowSeconds (the first follower may be
        /// another flag next frame, in a wind up to ShareKey.SpeedTolerance off). Its direction
        /// stays the one it was made in, KeyDegrees from the source's heading (NewGroup): its
        /// followers are within the angle's tolerance of it, and the first of them changing
        /// must not swing the source's wind (until 2026-11-07 it was turned each frame to the
        /// first follower's, which the old 15 deg steps allowed).
        /// </summary>
        private void SourceWind(ShareGroup g, FlagInstance f, Vector3 dir, float at10, float speed, float height)
        {
            if (g.WindFrame == _planFrame)
                return;

            bool first = g.WindFrame < 0;
            g.WindFrame = _planFrame;
            g.At10 = first ? at10 : FlagWind.Follow(g.At10, at10, Mathf.Max(_frameTime, 0f));
            g.Speed = speed;
            g.Height = height;
            g.Roughness = f.Roughness;
            g.AnchorX = f.Anchor.x;
            g.AnchorZ = f.Anchor.z;
        }

        /// <summary>
        /// The flag shows its group's source <see cref="MotionRecord.DelayFor"/> behind, turned to
        /// it -- the first half second blended from the shape it had. False if the record has
        /// nothing to show yet.
        /// </summary>
        private bool Play(FlagInstance f, ShareGroup g, Vector3 dir, float now)
        {
            float t = g.Now - MotionRecord.DelayFor(f.Prop);
            int a, b;
            float w, span;
            if (!g.Record.Find(t, out a, out b, out w, out span))
                return false;

            if (f.Follow != g || f.FollowCloth != f.Cloth)
            {
                if (f.Follow != null)
                    Promote(f);
                Join(f, g, now);
            }

            float blend = Mathf.Clamp01((now - f.FollowSince) / BlendTime);
            blend = blend * blend * (3f - 2f * blend);
            f.Pending = 0f;
            f.Waiting = false;
            f.WindDir = dir;
            _followers++;
            _demand += PlayCost(f);

            // Nothing new (the city paused): the same frame stays drawn.
            if (blend >= 1f && t == f.FollowShown)
                return true;

            f.FollowShown = t;
            float turn = Turn(f, g, dir);
            var job = new ClothJob
            {
                Cloth = f.Cloth,
                Mesh = f.Mesh,
                Alpha = w,
                Size = Step,
                DirX = dir.x,
                DirZ = dir.z,
                Play = g.Record,
                PlayA = a,
                PlayB = b,
                PlaySpan = span,
                PlayCos = Mathf.Cos(turn),
                PlaySin = Mathf.Sin(turn),
                PlayBlend = blend,
                FromX = blend < 1f ? f.FromX : null,
                FromY = f.FromY,
                FromZ = f.FromZ,
            };
            Queue(f, ref job);
            return true;
        }

        /// <summary>The turn from the source to the flag: an upright hoist by the wind (the cloth flies with it), else by the heading (the hoist's line).</summary>
        private static float Turn(FlagInstance f, ShareGroup g, Vector3 dir)
        {
            return g.Upright ? WindAngleOf(dir) - g.WindAngle : f.Angle - g.Angle;
        }

        /// <summary>The flag begins to follow: the shape it shows now kept, to blend from.</summary>
        private void Join(FlagInstance f, ShareGroup g, float now)
        {
            FlagCloth c = f.Cloth;
            int n = c.Count;
            if (f.FromX == null || f.FromX.Length != n)
            {
                f.FromX = new float[n];
                f.FromY = new float[n];
                f.FromZ = new float[n];
            }

            float a = Mathf.Clamp01(f.ShownAlpha), b = 1f - a;
            for (int k = 0; k < n; k++)
            {
                f.FromX[k] = c.PrevX[k] * b + c.X[k] * a;
                f.FromY[k] = c.PrevY[k] * b + c.Y[k] * a;
                f.FromZ[k] = c.PrevZ[k] * b + c.Z[k] * a;
            }

            f.Follow = g;
            f.FollowCloth = c;
            f.FollowSince = now;
            f.FollowShown = float.NaN;
        }

        /// <summary>
        /// The flag stops following and steps on its own from the source's steps it last showed
        /// (their places and the velocity between them: FlagCloth.Resume) -- none of it if its
        /// cloth was made anew since (a new detail, a redesign).
        /// </summary>
        private void Promote(FlagInstance f)
        {
            ShareGroup g = f.Follow;
            f.Follow = null;
            if (f.Cloth == null || f.FollowCloth != f.Cloth)
            {
                f.FollowCloth = null;
                return;
            }

            int a, b;
            float w, span;
            float t = float.IsNaN(f.FollowShown) ? g.Now - MotionRecord.DelayFor(f.Prop) : f.FollowShown;
            if (g.Record.Find(t, out a, out b, out w, out span))
            {
                float turn = Turn(f, g, f.WindDir);
                g.Record.Play(f.Cloth, a, b, span, Mathf.Cos(turn), Mathf.Sin(turn), null, null, null, 1f);
            }

            f.Cloth.Resume();
            f.FollowCloth = null;
            f.FollowShown = float.NaN;
            f.Pending = 0f;
            f.MeshDirty = true;
        }

        /// <summary>
        /// The sources: each group's clock run on (a warm one by the frame's time, a new one ahead
        /// of time until its record is ready), its steps queued (they always step) on its
        /// followers' air of last frame; a group with none for <see cref="GroupIdle"/> s, or every
        /// one with shared motion off, dropped.
        /// </summary>
        private void PlanSources(float dt, float realDt, Wind wind, ref double spent)
        {
            for (int i = _groupList.Count - 1; i >= 0; i--)
            {
                ShareGroup g = _groupList[i];
                int members = g.Members;
                g.Members = 0;
                if (members == 0 || !_sharing)
                {
                    g.Idle += realDt;
                    if (g.Idle >= GroupIdle || !_sharing)
                        RemoveGroupAt(i);
                    continue;
                }

                g.Idle = 0f;
                int steps;
                if (!g.Warm && g.Record.Span >= MotionRecord.ReadySpan)
                {
                    g.Warm = true;
                    g.Stepped = g.Record.Newest;
                    g.Now = g.Stepped;
                    g.Pending = 0f;
                }

                if (!g.Warm)
                {
                    // Ahead of time: a record's span in about a second at any frame rate.
                    steps = Mathf.Clamp(2 * Mathf.CeilToInt(_frameTime / Step), 4, 16);
                }
                else
                {
                    g.Pending += dt;
                    steps = Math.Min((int)((g.Pending + 1e-5f) / Step), MaxStepsPerFrame);
                    g.Pending = Mathf.Clamp(g.Pending - steps * Step, 0f, Step);
                }

                if (steps == 0)
                {
                    g.Now = g.Stepped + g.Pending;
                    continue;
                }

                FlagCloth source = g.Source;
                int sub = source.SubStepsFor(g.Speed, Step);
                var job = new ClothJob
                {
                    Cloth = source,
                    Steps = steps,
                    Size = Step,
                    Time = g.Stepped,
                    Speed = g.At10,
                    Gusts = wind.Gusts,
                    GustInSpeed = wind.FromWeatherMod,
                    DirX = g.DirX,
                    DirZ = g.DirZ,
                    Height = Mathf.Max(g.Height, 0.5f),
                    Waviness = g.Waviness,
                    Length = g.Length,
                    AnchorX = g.AnchorX,
                    AnchorZ = g.AnchorZ,
                    Roughness = g.Roughness,
                    Record = g.Record,
                };

                g.Stepped += steps * Step;
                if (g.Warm)
                    g.Now = g.Stepped + g.Pending;
                spent += steps * sub * source.Count * _unitCost;
                _demand += sub * source.Count * _unitCost / Step;
                Queue(null, ref job);
            }
        }

        private void RemoveGroupAt(int i)
        {
            ShareGroup g = _groupList[i];
            g.Dead = true;
            _groupList.RemoveAt(i);
            if (Log.Detailed)
                Log.Detail("flags: a shared motion source dropped (" + g.Source.Columns + "x" + g.Source.Rows + ", no follower for " + GroupIdle.ToString("F0") +
                           " s or shared motion off); " + _groupList.Count + " left");
        }

        /// <summary>The group whose source is this cloth, dropped (its job threw).</summary>
        private void DropGroup(FlagCloth source)
        {
            for (int i = _groupList.Count - 1; i >= 0; i--)
            {
                if (_groupList[i].Source == source)
                    RemoveGroupAt(i);
            }
        }

        private void Queue(FlagInstance f, ref ClothJob job)
        {
            _worker.Add(ref job);
            _batch.Add(f);
        }

        /// <summary>
        /// The flag's cloth can reach the ground under it, or the top of the city round it: hanging
        /// straight down from the foot of its hoist, its fly (FlagOutline's 1.02 of its length)
        /// would go below that.
        /// </summary>
        private static bool CanReachGround(FlagInstance f)
        {
            float floor = f.Ground;
            if (f.Surface != null)
                floor = Mathf.Max(floor, f.Surface.Top + f.SurfaceOrigin.y);
            return f.Anchor.y - floor < 1.02f * f.Spec.Length + f.Spec.Height;
        }

        /// <summary>
        /// The city round a waving flag, for its cloth to keep off (FlagSurroundings): the surface
        /// under it and the capsules of the props and trees near it, gathered when it has none over
        /// where it can reach now, a building changed near it, or they are old -- a few flags a
        /// frame (<see cref="SurfacesPerFrame"/>), the flag being placed first; <paramref name="now"/>
        /// (a flag about to settle) whatever the frame's count. A flag placed where the ghost stood
        /// takes the ghost's. Set on its cloth relative to the anchor, every frame (the anchor moves;
        /// the city does not).
        /// </summary>
        private void KeepOffCity(FlagInstance f, bool ghost, bool now)
        {
            FlagCloth cloth = f.Cloth;
            if (!_cityCollision)
            {
                cloth.SetSurface(null, 0f, 0f, 0f);
                cloth.SetCapsules(null, 0f, 0f, 0f);
                return;
            }

            FlagOutline reach = FlagOutline.For(f.Anchor, f.Hoist, f.Spec.Height, f.Spec.Length);
            float time = Time.realtimeSinceStartup;

            // Never gathered, stale, left behind or old (none after a failure: tried again when old).
            bool surface = f.SurfaceAt == 0f || f.SurfaceStale || (f.Surface != null && !Holds(f.SurfaceSquare, reach)) || time - f.SurfaceAt > SurfaceMaxAge;
            if (surface && ghost && f.Surface != null && !f.SurfaceStale && time - f.SurfaceAt < GhostSurfaceInterval)
                surface = false;
            if (surface && !ghost && _ghost != null && _ghost.Surface != null && !_ghost.SurfaceStale && Holds(_ghost.SurfaceSquare, reach))
            {
                // Placed where the ghost stood: its city is this one's.
                f.Surface = _ghost.Surface;
                f.SurfaceOrigin = _ghost.SurfaceOrigin;
                f.SurfaceSquare = _ghost.SurfaceSquare;
                f.SurfaceAt = _ghost.SurfaceAt;
                f.SurfaceStale = false;
                surface = false;
            }

            if (surface && (now || _surfacesThisFrame < SurfacesPerFrame))
            {
                _surfacesThisFrame++;
                float half = reach.Radius * FlagSurroundings.Slack;
                var found = new FlagSurroundings.Gathered();
                try
                {
                    f.Surface = FlagSurroundings.Surface(f.Anchor, reach.X, reach.Z, half, ref found);
                }
                catch (Exception e)
                {
                    f.Surface = null;
                    if (!_loggedGatherFailure)
                    {
                        _loggedGatherFailure = true;
                        Log.Error("flags: could not read the city round " + Who(f) + " (its cloth keeps off the flat ground alone)", e);
                    }
                }

                f.SurfaceOrigin = f.Anchor;
                f.SurfaceSquare = new Vector3(reach.X, reach.Z, half);
                f.SurfaceAt = time;
                // Drawn shapes not read yet (their LOD shapes stood in): gathered again, a frame on.
                f.SurfaceStale = found.Pending > 0;
                _surfaceGathers++;
                _gatherMs += found.Milliseconds;
                if (Log.Detailed && f.Surface != null && !ghost && found.Pending == 0)
                    Log.Detail("flags: the surface round " + Who(f) + ": " + f.Surface.Columns + "x" + f.Surface.Rows + " every " +
                               f.Surface.Spacing.ToString("F2") + " m, " + found.Buildings + " building(s) (" + found.Triangles +
                               " triangles; " + found.Detailed + " as drawn" + (found.BuildingProps > 0 ? " with " + found.BuildingProps + " prop(s)" : "") +
                               ", " + found.Simple + " by their simple shape" + (FlagSurroundings.DetailedShapes ? "" : ": the option off") +
                               "; " + BuildingShapes.MeshesRead + " mesh(es) read so far in " + BuildingShapes.ReadMilliseconds.ToString("F0") +
                               " ms (the longest " + BuildingShapes.LongestRead.ToString("F1") + ", " + BuildingShapes.PendingReads + " put off a frame), " +
                               BuildingShapes.TrianglesHeld + " triangles kept), its top " + (f.Surface.Top + f.Anchor.y - f.Ground).ToString("F1") + " m over the ground here, in " +
                               found.Milliseconds.ToString("F2") + " ms");
            }

            bool capsules = f.CapsulesAt == 0f || (f.Capsules != null && !Holds(f.CapsulesSquare, reach)) || time - f.CapsulesAt > CapsulesMaxAge;
            if (capsules && (now || _capsulesThisFrame < CapsulesPerFrame))
            {
                _capsulesThisFrame++;
                float half = reach.Radius * FlagSurroundings.Slack;
                var found = new FlagSurroundings.Gathered();
                int before = f.Capsules != null ? f.Capsules.Count : -1;
                try
                {
                    f.Capsules = FlagSurroundings.Capsules(f.Anchor, reach.X, reach.Z, half, ref found);
                }
                catch (Exception e)
                {
                    f.Capsules = null;
                    if (!_loggedGatherFailure)
                    {
                        _loggedGatherFailure = true;
                        Log.Error("flags: could not read the props and trees round " + Who(f), e);
                    }
                }

                f.CapsulesOrigin = f.Anchor;
                f.CapsulesSquare = new Vector3(reach.X, reach.Z, half);
                f.CapsulesAt = time;
                _capsuleGathers++;
                _gatherMs += found.Milliseconds;
                if (Log.Detailed && f.Capsules != null && f.Capsules.Count != before && !ghost)
                    Log.Detail("flags: " + Who(f) + " keeps off " + found.Props + " prop(s) and " + found.Trees + " tree(s) round it (" +
                               found.Milliseconds.ToString("F2") + " ms)");
            }

            Vector3 s = f.SurfaceOrigin - f.Anchor, c = f.CapsulesOrigin - f.Anchor;
            cloth.SetSurface(f.Surface, s.x, s.y, s.z);
            cloth.SetCapsules(f.Capsules, c.x, c.y, c.z);

            // What the last step found (the report's): touching the city, or standing under the surface it was given.
            if (cloth.SurfaceInUse || cloth.CapsulesInUse > 0)
                _touching++;
            if (cloth.SurfaceBlocked)
                _blocked++;
            if (cloth.SurfaceBlocked && !f.LoggedBlocked && !ghost)
            {
                f.LoggedBlocked = true;
                Log.Msg("flags: " + Who(f) + "'s hoist stands under a roof or inside a building: its cloth leaves the " +
                        "surface there out (the flat ground, props and trees still count)");
            }

            if (cloth.DrapeDistance > 0f && !f.LoggedWall && !ghost)
            {
                f.LoggedWall = true;
                Log.Msg("flags: " + Who(f) + " stands on a roof " + cloth.DrapeDistance.ToString("F2") + " m from its edge: its cloth is laid " +
                        "over the edge, down the wall");
            }

            if (cloth.WallShift > 0f && !f.LoggedWall && !ghost)
            {
                f.LoggedWall = true;
                Log.Msg(cloth.HoistOnEdge
                            ? "flags: " + Who(f) + " hangs from a roof's edge: its cloth hangs down the wall, the building kept " +
                              cloth.WallShift.ToString("F2") + " m back from the hoist"
                            : "flags: " + Who(f) + "'s hoist stands against a wall: its cloth keeps the building, the wall kept " +
                              cloth.WallShift.ToString("F2") + " m back from the hoist");
            }
        }

        /// <summary>
        /// The wind's terrain round a waving flag (FlagWind, CityRoughness): read from the buildings
        /// within CityRoughness.Reach when it has none, it has moved <see cref="RoughnessMove"/> from
        /// where it was read, a building changed in reach, or it is old -- a few flags a frame
        /// (<see cref="RoughnessPerFrame"/>), the flag being placed first. Logged when it first
        /// reads, and when it changes by a twentieth.
        /// </summary>
        private void WindTerrain(FlagInstance f, bool ghost, float height)
        {
            float time = Time.realtimeSinceStartup;
            Vector2 place = new Vector2(f.Anchor.x, f.Anchor.z);
            bool read = f.RoughnessAt == 0f || f.RoughnessStale || (place - f.RoughnessPlace).sqrMagnitude > RoughnessMove * RoughnessMove ||
                        time - f.RoughnessAt > RoughnessMaxAge;
            if (!read || (!ghost && _roughnessThisFrame >= RoughnessPerFrame))
                return;

            _roughnessThisFrame++;
            var found = new FlagSurroundings.Gathered();
            CityRoughness city;
            try
            {
                city = FlagSurroundings.Roughness(f.Anchor.x, f.Anchor.z, ref found);
            }
            catch (Exception e)
            {
                city = new CityRoughness();
                if (!_loggedGatherFailure)
                {
                    _loggedGatherFailure = true;
                    Log.Error("flags: could not read the buildings round " + Who(f) + " (its wind is a town's)", e);
                }
            }

            f.Roughness = city.Roughness;
            f.RoughnessPlace = place;
            f.RoughnessAt = time;
            f.RoughnessStale = false;
            _gatherMs += found.Milliseconds;

            if (ghost || !Log.Detailed || Mathf.Abs(f.Roughness - f.RoughnessLogged) <= 0.05f * f.Roughness)
                return;

            f.RoughnessLogged = f.Roughness;
            float z0 = f.Roughness;
            string terrain = z0 >= FlagWind.CityCentre - 1e-4f ? "a city centre's" : z0 <= FlagWind.RoughnessLength + 1e-4f ? "a town's" : "between a town's and a city centre's";
            Log.Detail("flags: the wind round " + Who(f) + ": " + city.Buildings + " building(s) within " + CityRoughness.Reach.ToString("F0") +
                       " m, " + city.MeanHeight.ToString("F1") + " m tall on average over " + (city.PlanShare * 100f).ToString("F0") +
                       "% of the ground (frontal " + (city.FrontalShare * 100f).ToString("F0") + "%): roughness " + z0.ToString("F2") + " m, " + terrain +
                       " -- at its " + height.ToString("F0") + " m the wind is " + FlagWind.SpeedAt(1f, height, z0).ToString("F2") +
                       "x the 10 m speed (a town's " + FlagWind.SpeedAt(1f, height).ToString("F2") + "x), gusts " +
                       (FlagWind.Intensity(height, z0) * 100f).ToString("F0") + "% of it (" + (FlagWind.Intensity(height) * 100f).ToString("F0") +
                       "%), in " + found.Milliseconds.ToString("F2") + " ms");
        }

        /// <summary>A flag's name in the log, made only when a line is written (this runs every frame for every waving flag in view).</summary>
        private static string Who(FlagInstance f)
        {
            return f.Prop != 0 ? "flag #" + f.Prop : "the new flag";
        }

        /// <summary>A square (middle x, z, half its side) holds all a flag can reach.</summary>
        private static bool Holds(Vector3 square, FlagOutline reach)
        {
            return Mathf.Abs(reach.X - square.x) + reach.Radius <= square.z && Mathf.Abs(reach.Z - square.y) + reach.Radius <= square.z;
        }

        /// <summary>The buildings changed since the last frame (FlagSurroundings' queue): the surfaces they reach are stale.</summary>
        private void MarkChanges()
        {
            _changes.Clear();
            bool all = FlagSurroundings.TakeChanges(_changes);
            if (_changes.Count == 0 && !all)
                return;

            foreach (FlagInstance f in _flags)
                MarkChanged(f, all);
            if (_ghost != null)
                MarkChanged(_ghost, all);
        }

        private void MarkChanged(FlagInstance f, bool all)
        {
            // The wind's terrain: any building changed within its reach.
            if (f.RoughnessAt != 0f && !f.RoughnessStale)
            {
                if (all)
                    f.RoughnessStale = true;
                else
                {
                    foreach (Vector3 change in _changes)
                    {
                        float dx = change.x - f.RoughnessPlace.x, dz = change.y - f.RoughnessPlace.y, r = CityRoughness.Reach + change.z;
                        if (dx * dx + dz * dz <= r * r)
                        {
                            f.RoughnessStale = true;
                            break;
                        }
                    }
                }
            }

            if (f.Surface == null || f.SurfaceStale)
                return;

            if (all)
            {
                f.SurfaceStale = true;
                return;
            }

            Vector3 square = f.SurfaceSquare;
            foreach (Vector3 change in _changes)
            {
                if (Mathf.Abs(change.x - square.x) <= square.z + change.z && Mathf.Abs(change.y - square.y) <= square.z + change.z)
                {
                    f.SurfaceStale = true;
                    return;
                }
            }
        }

        private void Draw(FlagInstance f)
        {
            DrawPole(f);

            // A loop (FlagMotion.Loop): its own draw, no mesh of the flag's. One the design no
            // longer asks for shows until the cloth that follows it is ready, then is let go.
            if (f.Loop != null)
            {
                if (ShowsLoop(f))
                {
                    if (f.Visible)
                        DrawLoop(f);
                    return;
                }

                f.Loop = null;
            }

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

            PutBlock(f);

            // The particles' texture: the shape the vertex program lays the drawn grid on. A shadow
            // only while the flag is big enough on the screen for one to be seen (ShadowPixels).
            f.Mesh.Bind(f.Block);
            Graphics.DrawMesh(f.Mesh.Mesh, Matrix4x4.TRS(f.Anchor, Quaternion.identity, Vector3.one), _material, _layer, _camera, 0,
                              f.Block, f.Pixels >= ShadowPixels, true);

            if (!_loggedFirstDraw)
            {
                _loggedFirstDraw = true;
                Log.Msg("flags: first flag drawn (#" + f.Prop + ", " + f.Spec.Describe() + ", " + f.Cloth.Columns + "x" + f.Cloth.Rows +
                        " cloth, at " + f.Distance.ToString("F0") + " m)");
            }

            f.PickAge += Time.unscaledDeltaTime;
            if (f.Prop != 0 && f.PickAge >= PickInterval)
            {
                float since = f.PickAge;
                f.PickAge = 0f;
                PublishPick(f, since);
            }
        }

        /// <summary>
        /// The flag's design into its block, when it changed. The flag the picking pointer is on
        /// glows gold; the selected one is not tinted (the gizmo shows it -- the author: "remove
        /// the highlight on the flag when an existing flag is selected. No need for it").
        /// </summary>
        private static void PutBlock(FlagInstance f)
        {
            bool hovered = IsHovered(f);
            if (!f.BlockReady || hovered || f.Highlighted || (f.Spec.HasImage && f.ImageVersion != FlagImages.UserVersion))
            {
                FlagLook.Apply(f.Block, f.Spec, hovered ? HoverGlow : Vector4.zero, FlagWear.SeedFor(f.Prop));
                f.BlockReady = true;
                f.Highlighted = hovered;
                f.ImageVersion = FlagImages.UserVersion;
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
                FlagPoleMesh.Look(f.PoleBlock, f.Spec, hovered ? HoverGlow : Vector4.zero, FlagWear.SeedFor(f.Prop));
                f.PoleBlockReady = !hovered;
            }

            Matrix4x4 placement = FlagPoleMesh.Placement(f.Anchor, f.PoleShape, f.Hoist, f.Fly);

            // Real Glass's Reflective finish over a glossy pole big enough to show it: the city in
            // the paint. Drawn: our own sky reflection of the paint is left out (_RFPoleFinish).
            bool finished = false;
            if (f.Spec.PoleShine > 0f && f.Pixels >= FinishPixels && RealGlassFinish.Available)
            {
                if (f.PoleFinishMesh == null)
                    f.PoleFinishMesh = FlagPoleMesh.BuildPlain(f.PoleShape, null);
                float shine = f.Spec.PoleShine;
                finished = RealGlassFinish.Draw(f.PoleFinishMesh, placement, _layer, Color.white,
                                                FinishReflectivity * (1f - 0.5f * f.Spec.PoleWear) * (1f - 0.6f * f.Spec.PoleDirt), shine, shine);
            }

            f.PoleBlock.SetFloat(FlagPoleMesh.IdPoleFinish, finished ? 1f : 0f);
            Graphics.DrawMesh(f.PoleMesh, placement, _poleMaterial, _layer, _camera, 0,
                              f.PoleBlock, f.Pixels >= ShadowPixels, true);
        }

        /// <summary>A pole gets Real Glass's finish from this many pixels on the screen (as its shadow): below, its own sky reflection.</summary>
        private const float FinishPixels = ShadowPixels;

        /// <summary>A gloss coat's reflectance head-on (Hoffman 2013's clear coat; Real Glass's Gloss coat), as FlagPole.shader's.</summary>
        private const float FinishReflectivity = 0.04f;

        /// <summary>
        /// The flag's pick volume, published for raycasts (PickShape): where its cloth has been
        /// lately, held (<see cref="PickRelease"/>), and its hoist and pole; <paramref name="since"/>
        /// seconds after the last.
        /// </summary>
        private void PublishPick(FlagInstance f, float since)
        {
            PickShape shape = f.Picks[f.PickNext];
            f.PickNext ^= 1;
            shape.Anchor = f.Anchor;
            PickVolumes(f, shape, since, PickAperture(f.Distance, PickPixels), PickAperture(f.Distance, WidePickPixels));
            FlagPicking.Publish(f.Prop, shape);
        }

        /// <summary>The least radius of a pick volume this far off, metres: <paramref name="pixels"/> of a 1080-line view there.</summary>
        private float PickAperture(float distance, float pixels)
        {
            float pixelHeight = _camera != null ? _camera.pixelHeight : 1080f;
            return Mathf.Max(distance, 1f) * pixels * pixelHeight / (1080f * Mathf.Max(_pixelsPerRadian, 1f));
        }

        /// <summary>
        /// The targets round the cloth (PickShape): the slab -- the cloth's box in steady axes,
        /// down its hoist, out along the wind (square to the hoist; the fly at rest while that is
        /// not defined) and across, held over its recent flutter (PickHold) and widened by a
        /// margin -- and capsules round the hoist edge and the drawn pole; the box of it all for
        /// the raycast's first test. Two sizes: tight, every volume at least
        /// <paramref name="aperture"/> wide; wider (Move It's), at least <paramref name="wideAperture"/>.
        /// </summary>
        private static void PickVolumes(FlagInstance f, PickShape shape, float since, float aperture, float wideAperture)
        {
            FlagCloth c = f.Cloth;
            float size = Mathf.Max(f.Spec.Length, f.Spec.Height);
            Vector3 h = f.Hoist;

            // Steady axes: the wind's way does not follow the flutter (the cloth's own fly edge did).
            Vector3 w = f.WindDir;
            Vector3 u = w - h * Vector3.Dot(w, h);
            u = u.sqrMagnitude > 0.09f ? u.normalized : f.Fly;
            Vector3 n = Vector3.Cross(h, u).normalized;

            // Axes turned far since the extents were held (the wind set another way): held afresh.
            if (f.Hold.Held && Vector3.Dot(u, f.HeldU) < 0.87f)
                f.Hold.Clear();
            f.HeldU = u;

            float uMin = float.MaxValue, uMax = float.MinValue, hMin = float.MaxValue, hMax = float.MinValue;
            float nMin = float.MaxValue, nMax = float.MinValue;

            // A flag that plays a loop has no cloth: its loop's extents over all its frames, in
            // these axes (FlagLoop: taken at heading 0, where the flag's own axes are these).
            if (c == null)
            {
                FlagLoop loop = f.Loop.Loop;
                uMin = loop.MinU; uMax = loop.MaxU;
                hMin = loop.MinV; hMax = loop.MaxV;
                nMin = loop.MinN; nMax = loop.MaxN;
            }

            for (int k = 0; c != null && k < c.Count; k++)
            {
                // A cut cloth's out particles are never drawn: not picked either.
                if (!c.IsCloth(k))
                    continue;

                var p = new Vector3(c.X[k], c.Y[k], c.Z[k]);
                float a = Vector3.Dot(p, u), b = Vector3.Dot(p, h), d = Vector3.Dot(p, n);
                if (a < uMin) uMin = a;
                if (a > uMax) uMax = a;
                if (b < hMin) hMin = b;
                if (b > hMax) hMax = b;
                if (d < nMin) nMin = d;
                if (d > nMax) nMax = d;
            }

            f.Hold.Take(uMin, uMax, hMin, hMax, nMin, nMax, since, PickRelease);
            PickHold held = f.Hold;

            shape.SlabU = u;
            shape.SlabV = h;
            shape.SlabN = n;
            shape.SlabCentre = f.Anchor + u * (0.5f * (held.MinU + held.MaxU)) + h * (0.5f * (held.MinV + held.MaxV)) +
                               n * (0.5f * (held.MinN + held.MaxN));
            shape.CoreU = 0.5f * (held.MaxU - held.MinU);
            shape.CoreV = 0.5f * (held.MaxV - held.MinV);
            shape.CoreN = 0.5f * (held.MaxN - held.MinN);
            shape.HasSlab = true;

            // Tight, for the game's own tools; wider, for Move It and the mod's tool.
            shape.Across = Mathf.Max(Mathf.Max(PickMarginAcross, PickMarginShare * size), aperture);
            shape.InPlane = Mathf.Max(Mathf.Max(0.15f, 0.02f * size), aperture);
            shape.HoistRadius = shape.Across;
            shape.WideAcross = Mathf.Max(Mathf.Max(WideMarginMin, WideMarginAcross * size), wideAperture);
            shape.WideInPlane = Mathf.Max(Mathf.Max(WideMarginMin * 0.66f, WideMarginInPlane * size), wideAperture);
            shape.WideHoistRadius = Mathf.Max(Mathf.Max(WideMarginMin, WideMarginHoist * size), wideAperture);

            shape.HoistA = f.Anchor;
            shape.HoistB = f.Anchor + h * f.Spec.Height;

            // The box of the wider volumes (the first test of either size).
            float halfU = shape.CoreU + Mathf.Max(shape.InPlane, shape.WideInPlane);
            float halfV = shape.CoreV + Mathf.Max(shape.InPlane, shape.WideInPlane);
            float halfN = shape.CoreN + Mathf.Max(shape.Across, shape.WideAcross);
            var box = new Bounds(shape.SlabCentre, Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                box.Encapsulate(shape.SlabCentre + u * ((corner & 1) != 0 ? halfU : -halfU) +
                                h * ((corner & 2) != 0 ? halfV : -halfV) + n * ((corner & 4) != 0 ? halfN : -halfN));
            }

            float hoistReach = Mathf.Max(shape.HoistRadius, shape.WideHoistRadius);
            box.Encapsulate(new Bounds(shape.HoistA, Vector3.one * (2f * hoistReach)));
            box.Encapsulate(new Bounds(shape.HoistB, Vector3.one * (2f * hoistReach)));

            // The pole the mod draws, from its foot to its tip.
            if (f.Spec.HasPole && f.PoleMesh != null)
            {
                FlagPoleShape pole = f.PoleShape;
                shape.PoleA = f.Anchor - f.Fly * pole.Offset + h * pole.Length;
                shape.PoleB = shape.PoleA - h * pole.Tip;
                shape.PoleRadius = Mathf.Max(Mathf.Max(PickMarginAcross, 3f * pole.RadiusFoot), aperture);
                shape.WidePoleRadius = Mathf.Max(Mathf.Max(WideMarginMin, 4f * pole.RadiusFoot), wideAperture);
                float poleReach = Mathf.Max(shape.PoleRadius, shape.WidePoleRadius);
                box.Encapsulate(new Bounds(shape.PoleA, Vector3.one * (2f * poleReach)));
                box.Encapsulate(new Bounds(shape.PoleB, Vector3.one * (2f * poleReach)));
            }
            else
            {
                shape.PoleRadius = 0f;
                shape.WidePoleRadius = 0f;
            }

            shape.Box = box;
        }

        /// <summary>
        /// The pick targets' margin round the cloth across its face and round its hoist and pole,
        /// metres: at least this, or this share of the flag's longer side (a 16 m flag: 1 m).
        /// </summary>
        private const float PickMarginAcross = 0.4f;
        private const float PickMarginShare = 0.06f;

        /// <summary>
        /// The wider pick's margins (Move It's, the mod's tool's), shares of the flag's longer side,
        /// at least <see cref="WideMarginMin"/> metres and <see cref="WidePickPixels"/> on the screen:
        /// across its face 15% (a 21.7 m flag: 3.3 m), in its plane 8%, round its hoist 12%; round
        /// the pole four of its foot's radii. Wider than the tight pick, far less than the whole
        /// reach the highlight paints (tried: the author found it "too big").
        /// </summary>
        private const float WideMarginAcross = 0.15f;
        private const float WideMarginInPlane = 0.08f;
        private const float WideMarginHoist = 0.12f;
        private const float WideMarginMin = 0.6f;

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

            // The sky a glossy pole reflects: Real Glass's, else the game's (PoleSky).
            try
            {
                PoleSky.Bind(dir.y);
            }
            catch (Exception)
            {
                // A pole without its reflection still draws.
            }
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
                Wind w = _wind;
                int still = 0, unshown = 0, looping = 0;
                foreach (FlagInstance f in _flags)
                {
                    if (FlagMotion.KeepsShape(f.Spec.Motion))
                        still++;
                    if (f.Loop != null)
                        looping++;

                    // In view but not drawn yet: no cloth or mesh, or still settling (Draw skips them); a loop not made yet.
                    if (f.Visible && f.Loop == null && (f.Mesh == null || !f.Settled))
                        unshown++;
                }

                Log.Detail("flags: " + _flags.Count + " in the city (" + still + " still or flat, " + _stillShapes.Count + " still shape(s) kept; " +
                           looping + " playing a loop, " + LoopsText() + "), " +
                           VisibleCount + " in view (" + unshown + " not drawn yet: settling" +
                           (_released > 0 ? "; " + _released + " cloth(s) let go out of view so far" : "") + "), settled in " + seconds.ToString("F0") + " s: " +
                           _settledSeen + " in view, " + _settledUnseen + " out of view, " + _settleCopies + " from a like flag's shape; " + _moving + " moving, " + _waiting +
                           " waiting for room | " + (frames / Math.Max(seconds, 0.1f)).ToString("F0") + " fps | main thread " +
                           (_costMs / frames).ToString("F3") + " ms a frame (" + (_waitMs / frames).ToString("F3") +
                           " of it waiting for the cloth), cloth " + (_batchMs / frames).ToString("F3") + " ms a frame (room for " +
                           Budget.ToString("F0") + ") on " +
                           (_worker.Threaded ? _worker.Threads + " thread(s) (" + (_batchWallMs / frames).ToString("F3") + " ms out)" : "the main thread") +
                           ", " + (1000.0 * _unitCost).ToString("F2") + " us a particle step | " + _stepsThisReport + " cloth steps in " +
                           seconds.ToString("F0") + " s" + (_failures > 0 ? ", " + _failures + " failed" : "") +
                           (_wentWrong > 0 ? ", " + _wentWrong + " went wrong (not a number)" : "") +
                           (_frameFailures > 0 ? ", " + _frameFailures + " frame(s) threw" : "") +
                           (_unknots > 0 ? ", " + _unknots + " knotted and laid out afresh in this city" : "") + " | in view by detail " +
                           DetailsInView() + " | load " + (_load.Ratio * 100.0).ToString("F0") + "% of the budget: " + LoadText() + " | wind " + w.Speed.ToString("F1") + " m/s, gusts " + (w.Gusts * 100f).ToString("F0") + "%, " +
                           w.Describe() + WindTurnText() + " | the city: " +
                           (_cityCollision
                                ? _touching + " touching it, " + _blocked + " under a roof; " + _surfaceGathers + " surface(s) and " +
                                  _capsuleGathers + " capsule set(s) read in " + _gatherMs.ToString("F1") + " ms"
                                : "off"));
            }

            _surfaceGathers = 0;
            _capsuleGathers = 0;
            _gatherMs = 0.0;
            _costMs = 0.0;
            _waitMs = 0.0;
            _batchMs = 0.0;
            _batchWallMs = 0.0;
            _costFrames = 0;
            _stepsThisReport = 0;
            _settledSeen = 0;
            _settledUnseen = 0;
            _settleCopies = 0;
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
        /// The flag the tool is about to place, standing at the cursor (FlagStand): call every
        /// frame while wanted (the tool does, from its Update). Only noted here; applied in
        /// LateUpdate, when no cloth is being stepped.
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
            _ghost.Pixels = Mathf.Max(_ghostSpec.Length, _ghostSpec.Height) / Mathf.Max(_ghost.Distance, 1f) * _pixelsPerRadian;
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

        /// <summary>How far above the ground a flag put there by another tool goes to stand on it (FlagStand: a standing pole's foot on it, else its hoist).</summary>
        private static float StandRise(FlagSpec spec, float angle)
        {
            float pole = spec.PoleLength > 0f ? spec.PoleLength : Settings.NewHeight != null ? Settings.NewHeight.value : Settings.Defaults.Height;
            float x, rise, z;
            FlagStand.Anchor(spec, angle, pole, 0f, 0f, 0f, out x, out rise, out z);
            return rise;
        }

        /// <summary>
        /// Line Tool has the flag: the ghost stands at the row's first point, or under the pointer
        /// until there is a row, and <see cref="DrawLine"/> shows it at the others. The game cannot
        /// draw the row (our prop has no renderer), and a live cloth at each of a long row's points
        /// would be a settle per point per move of the mouse: one cloth, drawn many times.
        /// </summary>
        private void AskLine()
        {
            _linePoints.Clear();
            if (!LineFlags.IsOut)
                return;

            Vector3 pointer;
            if (!LineFlags.ReadPoints(_linePoints, out pointer))
                return;

            FlagSpec spec = Settings.NewSpec();
            float lift = FlagTool.Lift;
            Vector3 at;
            float angle;
            if (_linePoints.Count > 0)
            {
                at = _linePoints[0].Position;
                angle = _linePoints[0].Angle;
            }
            else
            {
                ToolController tools = ToolsModifierControl.toolController;
                if (tools == null || tools.IsInsideUI)
                    return;

                at = new Vector3(pointer.x, FlagProps.GroundAt(pointer), pointer.z);
                angle = Settings.NewRotation.value * Mathf.Deg2Rad;
            }

            at.y += StandRise(spec, angle) + lift;
            SetGhost(at, angle, spec);
        }

        /// <summary>
        /// The ghost again at the row's other points, stood as those flags will stand. A waving
        /// flag upright is only carried there, flying as it does in this wind; one whose shape
        /// goes by the way it heads (still, flat, tilted, hoisted from its top) turns with it.
        /// </summary>
        private void DrawLine()
        {
            FlagInstance g = _ghost;
            bool loop = ShowsLoop(g);
            if (_linePoints.Count < 2 || !g.BlockReady || (!loop && (g.Mesh == null || !g.Settled)))
                return;

            FlagSpec spec = g.Spec;
            bool pole = g.PoleMesh != null && spec.HasPole && _poleMaterial != null && g.PoleBlockReady;
            if (pole)
                g.PoleBlock.SetFloat(FlagPoleMesh.IdPoleFinish, 0f);   // the row's poles get no Real Glass finish: their own sky
            bool turns = FlagMotion.KeepsShape(spec.Motion) || spec.HoistedFrom != FlagHoist.Side || spec.Tilt != 0f || spec.Lean != 0f;
            float lift = FlagTool.Lift;

            for (int i = 1; i < _linePoints.Count; i++)
            {
                LinePoint p = _linePoints[i];
                Vector3 anchor = p.Position;
                anchor.y += StandRise(spec, p.Angle) + lift;

                // The prop's angle runs from x towards z; Unity's turn about y the other way.
                // A loop is in the flag's own frame (heading 0): turned whole to each point's heading.
                Quaternion turn = loop ? Quaternion.AngleAxis(-p.Angle * Mathf.Rad2Deg, Vector3.up)
                                : turns ? Quaternion.AngleAxis((g.Angle - p.Angle) * Mathf.Rad2Deg, Vector3.up) : Quaternion.identity;
                Graphics.DrawMesh(loop ? g.Loop.Loop.MeshFor(g.LoopCells) : g.Mesh.Mesh, Matrix4x4.TRS(anchor, turn, Vector3.one), _material, _layer, _camera,
                                  0, g.Block, true, true);

                if (pole)
                {
                    float hx, hy, hz, fx, fy, fz;
                    FlagOrientation.Frame(p.Angle, spec.Tilt, spec.Lean, spec.HoistedFrom, out hx, out hy, out hz, out fx, out fy, out fz);
                    Graphics.DrawMesh(g.PoleMesh, FlagPoleMesh.Placement(anchor, g.PoleShape, new Vector3(hx, hy, hz), new Vector3(fx, fy, fz)),
                                      _poleMaterial, _layer, _camera, 0, g.PoleBlock, true, true);
                }
            }
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

        /// <summary>The nearest flag the ray meets anywhere in its reach, 0 for none (the tool's picking, on the main thread; as Move It's).</summary>
        public uint PickAt(ColossalFramework.Math.Segment3 ray)
        {
            uint best = 0;
            float bestT = 2f;
            foreach (FlagInstance f in _flags)
            {
                PickShape shape = FlagPicking.Get(f.Prop);
                float t;
                if (shape != null && shape.IntersectWide(ray, out t) && t < bestT)
                {
                    bestT = t;
                    best = f.Prop;
                }
            }

            return best;
        }

        /// <summary>
        /// The flag whose prop is at <paramref name="position"/> (Move It's overlay passes a
        /// position, not a prop), and its outline there: the nearest anchor within
        /// <see cref="FindReach"/> -- a prop Move It drags fast has moved on since this frame read
        /// it -- and the outline drawn where Move It has the prop, not where the renderer last saw it.
        /// </summary>
        public bool TryFindAt(Vector3 position, out FlagOutline outline)
        {
            FlagInstance best = null;
            float bestSqr = FindReach * FindReach;
            foreach (FlagInstance f in _flags)
            {
                float sqr = f.HasAnchor ? (f.Anchor - position).sqrMagnitude : float.MaxValue;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = f;
                }
            }

            if (best == null)
            {
                outline = default(FlagOutline);
                return false;
            }

            outline = OutlineOf(best);
            outline.Shift(position - best.Anchor);
            return true;
        }

        /// <summary>How far from a flag's anchor Move It's overlay may be and still be that flag's, metres.</summary>
        private const float FindReach = 3f;

        /// <summary>From FlagLoadingHook.EndOverlay: the tool's hovered flag outlined on the city (the selected one has the gizmo instead).</summary>
        public void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            FlagInstance f;
            if (FlagTool.Hovered != 0 && FlagTool.Hovered != FlagEditor.Selected && _byProp.TryGetValue(FlagTool.Hovered, out f) && f.Visible)
                Outline(cameraInfo, OutlineOf(f), new Color(1f, 0.85f, 0.35f, 0.55f));
        }

        /// <summary>
        /// What a flag's outline covers (FlagOutline): everywhere its cloth can reach from its
        /// hoist, and its pole from foot to tip when the mod draws one -- from its place and size,
        /// not its cloth, so it stands still while the flag waves.
        /// </summary>
        private static FlagOutline OutlineOf(FlagInstance f)
        {
            FlagOutline outline = FlagOutline.For(f.Anchor, f.Hoist, f.Spec.Height, f.Spec.Length);
            if (f.Spec.HasPole && f.PoleMesh != null)
            {
                FlagPoleShape pole = f.PoleShape;
                Vector3 foot = f.Anchor - f.Fly * pole.Offset + f.Hoist * pole.Length;
                outline.Include(foot);
                outline.Include(foot - f.Hoist * pole.Tip);
            }

            return outline;
        }

        /// <summary>
        /// The game's overlay over a flag (Move It's and the bulldozer's highlight, our tool's
        /// hover): a disc round all of it, painted on whatever lies within its heights -- the
        /// cloth, the pole, what the pole stands on -- as the game paints a prop's circle, and
        /// faded as the game fades a big prop's (FlagOutline.Alpha). The first ones followed the
        /// cloth's box every frame, and so swayed with it: "the selector goes all over the place".
        /// </summary>
        public static void Outline(RenderManager.CameraInfo cameraInfo, FlagOutline outline, Color colour)
        {
            colour.a = outline.Alpha(colour.a);
            var centre = new Vector3(outline.X, 0.5f * (outline.MinY + outline.MaxY), outline.Z);
            Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(cameraInfo, colour, centre, 2f * outline.Radius, outline.MinY, outline.MaxY,
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

        /// <summary>
        /// Volumetric Weather's wind in place of the game's (WeatherModWind, 2026-10-30): its strength,
        /// its direction, and the gust passing each flag -- the same that bends its trees.
        /// </summary>
        public bool FromWeatherMod;

        /// <summary>Volumetric Weather's wind is set by hand on its Now tab.</summary>
        public bool Overridden;

        public static Wind Current()
        {
            bool fromGame = Settings.WindFromGame != null ? Settings.WindFromGame.value : Settings.Defaults.WindFromGame;
            if (fromGame)
            {
                // The game's: its direction, its weather; gusts as a town's (EN 1991-1-4). With Volumetric
                // Weather on, its wind instead: it already has the weather in it (cloud, rain, storms).
                // Its strength and gusts; the way it blows as the Direction row says, here too (the
                // author, 2026-11-10: "this option should be enabled even if Use the game's wind is
                // on", along each flag's rotation by default).
                var g = new Wind { FromGame = true, Gusts = 1f, Mode = ModeSet() };
                GameWind.Read(out g.Fixed, out g.Weather, out g.Rain, out g.Fog);
                g.Speed = GameWind.BaseSpeed * g.Weather;
                Vector3 direction;
                float speed;
                if (WeatherModWind.TryRead(out direction, out speed, out g.Overridden))
                {
                    g.FromWeatherMod = true;
                    g.Fixed = direction;
                    g.Speed = speed;
                }

                if (g.Mode == Settings.WindModes.Fixed)
                    g.Fixed = FixedSet();
                return g;
            }

            // The player's own, exactly as set.
            var w = new Wind
            {
                Speed = Settings.WindSpeed != null ? Mathf.Clamp(Settings.WindSpeed.value, 0f, 50f) : Settings.Defaults.WindSpeed,
                Gusts = Settings.WindGusts != null ? Mathf.Clamp(Settings.WindGusts.value, 0f, 2f) : Settings.Defaults.WindGusts,
                Mode = ModeSet(),
                Weather = 1f,
            };

            if (w.Mode == Settings.WindModes.Fixed)
            {
                w.Fixed = FixedSet();
            }
            else if (w.Mode == Settings.WindModes.Game)
            {
                // The game's direction -- Volumetric Weather's when it is on (it may be turning slowly or fixed there).
                float weather, rain, fog, speed;
                bool overridden;
                Vector3 direction;
                GameWind.Read(out w.Fixed, out weather, out rain, out fog);
                if (WeatherModWind.TryRead(out direction, out speed, out overridden))
                    w.Fixed = direction;
            }

            return w;
        }

        /// <summary>The Direction row: along each flag's rotation, fixed, or the game's (Volumetric Weather's when it is on).</summary>
        private static int ModeSet()
        {
            int mode = Settings.WindMode != null ? Settings.WindMode.value : Settings.Defaults.WindMode;
            return mode == Settings.WindModes.Fixed || mode == Settings.WindModes.Game ? mode : Settings.WindModes.EachFlag;
        }

        /// <summary>The fixed direction set, as a flag's rotation reads it.</summary>
        private static Vector3 FixedSet()
        {
            float deg = Settings.WindDirection != null ? Settings.WindDirection.value : Settings.Defaults.WindDirection;
            return FlagProps.Direction(deg * Mathf.Deg2Rad);
        }

        /// <summary>The way the wind blows at a flag of this prop angle (the way the flag points, in the first mode).</summary>
        public Vector3 DirectionFor(float angle)
        {
            return Mode == Settings.WindModes.EachFlag ? FlagProps.Direction(angle) : Fixed;
        }

        /// <summary>The wind at 10 m for a flag standing here over this ground: the game's wind map's share of it, or the player's own.</summary>
        public float SpeedFor(Vector3 place, float ground)
        {
            if (!FromGame)
                return Speed;
            float here = FromWeatherMod ? WeatherModWind.SpeedAt(place.x, place.z, Speed) : Speed;
            return here * GameWind.Exposure(place, ground);
        }

        /// <summary>For the log: "the game's (5.8 m/s over open ground, weather x1.15)", "Volumetric Weather's (...)" or "fixed".</summary>
        public string Describe()
        {
            if (FromWeatherMod)
                return "Volumetric Weather's (" + Speed.ToString("F1") + " m/s over open ground" + (Overridden ? ", set on its Now tab" : ", from its weather") +
                       ", its gusts passing each flag), direction " + Settings.WindModes.LogName(Mode);
            return FromGame
                ? "the game's (" + Speed.ToString("F1") + " m/s over open ground, weather x" + Weather.ToString("F2") + "), direction " +
                  Settings.WindModes.LogName(Mode)
                : Settings.WindModes.LogName(Mode);
        }
    }
}
