using System;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The mod's tool on the map, in two modes. PLACE (while the New flag window is open): the new
    /// flag follows the cursor -- the real cloth, blowing -- standing on the ground or roof under
    /// it; left click places it, again and again; the right button turns it as the
    /// game's props turn (a click a step, a drag freely); Esc puts the tool away, and the window
    /// with it. PICK (the Flags tab's): the flag under the cursor lights up, a click selects it;
    /// the selected flag carries the gizmo (FlagGizmo) -- drag its arrows to move it, its rings to
    /// turn it -- and a click on nothing, a right click or Esc lets it go.
    /// </summary>
    /// <remarks>
    /// Volumetric Explosions' MissileTool pattern, from PropTool's IL: the mouse ray taken in
    /// OnToolLateUpdate on the main thread, cast into the terrain (and buildings and roads, so a
    /// flag can stand on a roof or a bridge) in SimulationStep on the simulation thread, clicks
    /// taken in OnToolGUI unless the mouse is over the UI. Added to the ToolController's own
    /// GameObject when first wanted (enabling a tool makes it the current one), destroyed with
    /// the city (<see cref="Remove"/>). Esc: the game's own Esc only puts away its toolbar's
    /// tools and otherwise opens the pause menu, so the panel takes the key first
    /// (FlagsPanel.CatchEscape), as in Volumetric Explosions. The right mouse button is free:
    /// the game turns the camera with the middle one (DefaultSettings.cameraMouseRotate, Mouse2).
    ///
    /// Placing, the right button is PropTool's (its IL, mode Single): pressed over the city, a
    /// drag turns the flag by the pointer's sideways travel ("Mouse X" x 10 degrees,
    /// DeltaAngle), and let go without one it turns to the next 45 degrees down (ToggleAngle:
    /// round(a / 45 - 1) x 45). The turn is the new flag's rotation itself, so the New flag
    /// window shows it. Unlike PropTool's prop, the flag holds where it stood while a drag turns it.
    ///
    /// Placing, the flag STANDS on the surface under the pointer (FlagStand): a standing pole's foot
    /// there, the flag at its top as tall as the Pole height makes it; with no pole, its hoist (a
    /// banner's cloth) there. The surface is the top of what is under the pointer, looked for
    /// straight down from just above where the pointer's ray met the city: a roof, a road, the
    /// ground -- and for a ray that met a wall's face, the ground in front of it, not a point part
    /// way up the wall. The game's raise and lower keys (Page Up, Page Down) move a flag up and down
    /// WHOLE, its pole with it, as Move It moves its selection (ElevationKeys: 0.125 m a step,
    /// Shift x8, Alt /8, repeating after a third of a second held): placing, off that surface
    /// (let go with the window); picking, the selected flag as the gizmo's arrows move it
    /// (FlagEditor.ApplyTransform: its pole's length kept). Nothing under the pointer changes the
    /// pole: the first rule put the flag's top an Elevation over the surface and cast the pole down
    /// from there, and over a building the pole grew with the height the pointer found on a wall
    /// ("the flagpole's height is being affected by the altitude").
    ///
    /// Esc (and, picking, the right button) steps back one thing at a time: a drag (the flag
    /// goes back where it was, or the new flag the way it pointed), then the selection, then the
    /// tool. The selection lives while the tool is out: the tool put away by any means lets it
    /// go (<see cref="OnDisable"/>).
    /// </remarks>
    public sealed class FlagTool : ToolBase
    {
        public enum Mode { Place, Pick }

        public static FlagTool Instance { get; private set; }

        /// <summary>The flag under the cursor in pick mode (0: none), for the highlight.</summary>
        public static uint Hovered { get; private set; }

        public Mode CurrentMode { get; private set; }

        private Ray _mouseRay;
        private float _mouseRayLength;
        private volatile bool _mouseRayValid;

        // Written by SimulationStep (the simulation thread), read on the main thread.
        private volatile bool _hitValid;
        private Vector3 _hit;
        private readonly object _hitLock = new object();

        /// <summary>
        /// A roof's edge near where the pointer is (FlagEdgeSnap.Find), found on the MAIN thread
        /// for the hit it belongs to: the edge's point (y the roof's), the way out from it.
        /// </summary>
        private struct EdgeSnap
        {
            public bool Valid;
            public Vector3 Edge;
            public float Nx, Nz;
        }

        // The main thread's own: the last edge looked for, and from where (looked for again only
        // when the pointer moves: a surface gathered round it, as the cloth's is).
        private Vector3 _snapFrom;
        private bool _snapFromSet;
        private EdgeSnap _snapLast;

        /// <summary>The pointer moved at least this far, metres, before the edge is looked for again.</summary>
        private const float SnapMove = 0.02f;

        /// <summary>The edge is looked for on a surface this far either side of the pointer, metres (FlagEdgeSnap's reach, a step and its points beside).</summary>
        private const float SnapHalf = FlagEdgeSnap.Reach + FlagEdgeSnap.Step + 0.6f;

        /// <summary>The edge found so far is forgotten (the buildings' shapes option changed).</summary>
        public static void ForgetSnap()
        {
            if (Instance != null)
                Instance._snapFromSet = false;
        }

        private readonly FlagGizmo _gizmo = new FlagGizmo();

        // Placing: the right button's turn of the new flag (main thread). _turnTo is the drag's
        // own sum, unrounded (the setting keeps whole degrees); _turnHit is where the flag holds.
        private bool _rightDown;
        private bool _turned;
        private float _turnFrom, _turnTo;
        private bool _turnHitValid;
        private Vector3 _turnHit;
        private EdgeSnap _turnSnap;

        /// <summary>Degrees a drag turns the flag per unit of the "Mouse X" axis: PropTool.OnToolUpdate's DeltaAngle(axis x 10).</summary>
        private const float TurnPerMouseX = 10f;

        // The raise and lower keys (the game's, as its road and building tools read them:
        // NetTool.Awake's IL) and Move It's stepping; whether they are moving a flag, how far since
        // they went down (logged when they are let go) and when they last stepped (real time), for
        // the tooltip.
        private SavedInputKey _raiseKey, _lowerKey;
        private ElevationKeys _elevation;
        private bool _raising;
        private float _raisedBy;
        private float _raisedAt = -1f;

        /// <summary>Placing: the new flag raised (or, less than 0, lowered) whole off the surface it stands on, metres.</summary>
        private float _lift;

        // Picking: the selected flag the keys move, and where they have sent it -- its own while
        // the keys work and a moment after: the prop moves on the simulation thread, a frame or
        // two behind, and the keys step on every GUI event, several a frame.
        private uint _raisedFlag;
        private Vector3 _raiseAnchor;
        private float _raiseAngle, _raiseTilt, _raiseLean, _raiseOwnUntil;

        /// <summary>The flag's move stays in the tooltip this long after the keys last moved it, seconds.</summary>
        private const float RaiseShown = 1f;

        /// <summary>The selected flag's place the keys sent it to is trusted over its prop's this long after their last step, seconds (FlagEditor's own quiet time).</summary>
        private const float RaiseOwnTime = 0.6f;

        /// <summary>The new flag is raised or lowered at most this far off the surface, metres.</summary>
        private const float MaxLift = 1000f;

        /// <summary>
        /// Where the pointer's ray met the city, the surface under it is looked for straight down
        /// from this high above that point, and this much nearer the camera (level): a ray that met
        /// a wall's face finds the ground in front of it, one that met a roof finds the roof.
        /// </summary>
        private const float SurfaceProbeUp = 0.5f;
        private const float SurfaceProbeBack = 0.1f;

        private static readonly Color PlaceColour = new Color(0.95f, 0.85f, 0.4f, 0.6f);

        /// <summary>Shows the tool in <paramref name="mode"/>; the same mode again puts it away.</summary>
        public static void Toggle(Mode mode)
        {
            FlagTool tool = Instance;
            ToolController controller = ToolsModifierControl.toolController;
            if (tool != null && controller != null && controller.CurrentTool == tool && tool.CurrentMode == mode)
            {
                PutAway();
                return;
            }

            Open(mode);
        }

        /// <summary>Takes the tool out in <paramref name="mode"/> (if it is out already, only its mode changes).</summary>
        public static void Open(Mode mode)
        {
            try
            {
                ToolController controller = ToolsModifierControl.toolController;
                if (controller == null)
                    return;

                FlagTool tool = Instance;
                bool was = tool != null && controller.CurrentTool == tool && tool.CurrentMode == mode;
                if (tool == null)
                    tool = controller.gameObject.AddComponent<FlagTool>();

                if (tool.CurrentMode != mode)
                {
                    tool._gizmo.Cancel();
                    tool._rightDown = false;
                    tool.EndRaise();
                    tool._lift = 0f;
                }
                tool.CurrentMode = mode;
                controller.CurrentTool = tool;
                if (!was)
                    Log.Msg("flags: tool " + (mode == Mode.Place ? "placing flags" : "picking a flag"));
            }
            catch (Exception e)
            {
                Log.Error("flags: could not open the tool.", e);
            }
        }

        public static bool IsOut
        {
            get { return Instance != null && Instance.enabled; }
        }

        public static bool IsPlacing
        {
            get { return IsOut && Instance.CurrentMode == Mode.Place; }
        }

        public static bool IsPicking
        {
            get { return IsOut && Instance.CurrentMode == Mode.Pick; }
        }

        /// <summary>Back to the game's default tool, if ours is out.</summary>
        public static void PutAway()
        {
            try
            {
                if (Instance != null && ToolsModifierControl.toolController != null &&
                    ToolsModifierControl.toolController.CurrentTool == Instance)
                {
                    ToolsModifierControl.SetTool<DefaultTool>();
                }
            }
            catch (Exception e)
            {
                Log.Warn("flags: could not put the tool away: " + e.Message);
            }
        }

        /// <summary>
        /// Esc (or, picking, the right button): one step back -- a drag given up (the flag back
        /// where it was, the new flag the way it pointed), else the selection let go, else the
        /// tool put away. Says which, for the log.
        /// </summary>
        public static string StepBack()
        {
            FlagTool tool = Instance;
            if (tool != null && tool._gizmo.IsDragging)
            {
                tool._gizmo.Cancel();
                FlagEditor.ApplyTransform(tool._gizmo.Anchor, tool._gizmo.Angle, tool._gizmo.Tilt, tool._gizmo.Lean);
                Log.Msg("flags: the drag was given up; the flag is back where it was");
                return "the drag given up";
            }

            if (tool != null && tool.CurrentMode == Mode.Place && tool._rightDown && tool._turned)
            {
                // The button is still down: nothing more turns until it is pressed again.
                tool._rightDown = false;
                SetNewDegrees(tool._turnFrom);
                Log.Msg("flags: the turn was given up; the new flag points " + NewDegrees().ToString("F0") + " deg again");
                return "the turn given up";
            }

            if (tool != null && tool.CurrentMode == Mode.Pick && FlagEditor.Selected != 0)
            {
                FlagEditor.Deselect();
                return "the selection let go";
            }

            PutAway();
            return "the tool put away";
        }

        /// <summary>Destroys the tool (the city is unloading): the default tool first, if ours is out.</summary>
        public static void Remove()
        {
            FlagTool tool = Instance;
            if (tool == null)
                return;

            PutAway();
            tool._gizmo.Destroy();
            Destroy(tool);
            Instance = null;
        }

        protected override void Awake()
        {
            base.Awake();
            Instance = this;

            try
            {
                _raiseKey = new SavedInputKey(global::Settings.buildElevationUp, global::Settings.gameSettingsFile, DefaultSettings.buildElevationUp, true);
                _lowerKey = new SavedInputKey(global::Settings.buildElevationDown, global::Settings.gameSettingsFile, DefaultSettings.buildElevationDown, true);
            }
            catch (Exception e)
            {
                _raiseKey = null;
                _lowerKey = null;
                Log.Warn("flags: the game's raise and lower keys could not be read; flags are not raised or lowered by them: " + e.Message);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _mouseRayValid = false;
            _hitValid = false;
            _rightDown = false;
            Hovered = 0;
            EndRaise();

            // Over to Line Tool, the row keeps the lift.
            if (!LineFlags.Handing)
                _lift = 0f;

            // Put away by any means: a drag stays where it got to, the selection goes.
            EndDrag();
            _gizmo.ClearHover();
            FlagEditor.Deselect();
        }

        /// <summary>
        /// A drag of the gizmo let go: the flag stays where it was taken, and the log says so, as
        /// for every other way a flag moves (the drags were silent: a tilt that re-found a pole's
        /// length showed in the log only as the pole's new length).
        /// </summary>
        private void EndDrag()
        {
            FlagGizmo.Handle handle = _gizmo.Dragging;
            _gizmo.End();
            uint flag = FlagEditor.Selected;
            if (handle == FlagGizmo.Handle.None || flag == 0)
                return;

            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Vector3 anchor = _gizmo.Anchor;
            string what;
            if (handle == FlagGizmo.Handle.TurnHeading)
            {
                what = "turned with the gizmo to " + Mathf.Repeat(_gizmo.Angle * Mathf.Rad2Deg, 360f).ToString("F0", inv) + " deg";
            }
            else if (handle == FlagGizmo.Handle.TurnTilt || handle == FlagGizmo.Handle.TurnLean)
            {
                what = "tilted with the gizmo: " + _gizmo.Tilt.ToString("0.#", inv) + " deg, leaning " + _gizmo.Lean.ToString("0.#", inv) + " deg";
                FlagSpec spec;
                if (FlagRegistry.TryGet(flag, out spec) && spec.HasPole)
                    what += !FlagStand.PoleStands(spec) ? ", on a crossbar found along it"
                          : spec.PoleLength > 0f ? ", its pole " + spec.PoleLength.ToString("0.#", inv) + " m tall on its foot"
                          : ", its pole found again down it";
            }
            else
            {
                what = "moved with the gizmo";
            }

            Log.Msg("flags: flag #" + flag + " " + what + "; top of the hoist at (" + anchor.x.ToString("F0", inv) + ", " +
                    anchor.z.ToString("F0", inv) + "), " + anchor.y.ToString("F1", inv) + " m");
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
                Instance = null;
            Hovered = 0;
        }

        protected override void OnToolLateUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                _mouseRayValid = false;
                return;
            }

            _mouseRay = camera.ScreenPointToRay(Input.mousePosition);
            _mouseRayLength = camera.farClipPlane;
            _mouseRayValid = !m_toolController.IsInsideUI && Cursor.visible;
        }

        public override void SimulationStep()
        {
            if (!_mouseRayValid)
            {
                _hitValid = false;
                return;
            }

            RaycastOutput output;
            if (Cast(_mouseRay, _mouseRayLength, out output))
            {
                Vector3 hit = output.m_hitPos;
                if (CurrentMode == Mode.Place)
                    hit = SurfaceUnder(hit, _mouseRay.direction);

                lock (_hitLock)
                    _hit = hit;

                _hitValid = true;
            }
            else
            {
                _hitValid = false;
            }
        }

        /// <summary>
        /// On the simulation thread: the top of what is under a point the pointer's ray met -- the
        /// first ground, roof or road straight down from just above it, a little towards the camera
        /// (<see cref="SurfaceProbeUp"/>, <see cref="SurfaceProbeBack"/>). A roof, a road or the
        /// ground finds itself again; a wall's face, the ground (or a lower roof) in front of it.
        /// </summary>
        private Vector3 SurfaceUnder(Vector3 hit, Vector3 rayDirection)
        {
            Vector3 back = new Vector3(-rayDirection.x, 0f, -rayDirection.z);
            float level = back.magnitude;
            back = level > 1e-4f ? back * (SurfaceProbeBack / level) : Vector3.zero;
            Vector3 from = hit + back + Vector3.up * SurfaceProbeUp;

            RaycastOutput down;
            if (Cast(new Ray(from, Vector3.down), _mouseRayLength, out down) && down.m_hitPos.y <= from.y)
                return down.m_hitPos;
            return hit;
        }

        /// <summary>
        /// The new flag snaps to a roof's edge: a banner hoisted from its top (the author: "usually
        /// placed on the edges of buildings or balconies"), the option on (Settings.NewSnapEdges).
        /// Read on either thread (two settings' values).
        /// </summary>
        private static bool SnapsToEdges
        {
            get
            {
                return Settings.NewSnapEdges != null && Settings.NewSnapEdges.value && Settings.NewHoistedFrom != null &&
                       Settings.NewHoistedFrom.value == FlagHoist.Top;
            }
        }

        /// <summary>
        /// On the MAIN thread: the edge of the surface the pointer is on (FlagEdgeSnap.Find), on
        /// the surface the cloth itself keeps off (FlagSurroundings.Surface, the buildings as drawn
        /// when that option is on): where the banner hangs is where its cloth finds the wall. The
        /// game's raycasts met only the LOD shapes (the 2026-11-07 run: a banner snapped to a
        /// 28-triangle box, its cloth behind the drawn cornice and frames). Looked for again only
        /// once the pointer moves.
        /// </summary>
        private EdgeSnap EdgeNear(Vector3 hit)
        {
            if (_snapFromSet && (hit - _snapFrom).sqrMagnitude < SnapMove * SnapMove)
                return _snapLast;

            var snap = new EdgeSnap();
            _snapFrom = hit;
            _snapFromSet = true;
            _snapLast = snap;

            SurfaceGrid grid;
            try
            {
                var found = new FlagSurroundings.Gathered();
                grid = FlagSurroundings.Surface(Vector3.zero, hit.x, hit.z, SnapHalf, ref found);

                // Drawn shapes not read yet: looked for again next frame, not kept.
                if (found.Pending > 0)
                    _snapFromSet = false;
            }
            catch (Exception e)
            {
                if (!_loggedSnapFailure)
                {
                    _loggedSnapFailure = true;
                    Log.Error("flags: could not read the city round the pointer for a roof's edge (no snapping)", e);
                }

                return snap;
            }

            // Off the surface read: no fall there (never an edge at the square's border).
            float y = hit.y;
            Func<float, float, float> top = (x, z) =>
            {
                float h, sx, sz;
                return grid.Sample(x, z, out h, out sx, out sz) ? h : y;
            };

            float ex, ez, nx, nz, roofY;
            if (FlagEdgeSnap.Find(top, hit.x, y, hit.z, out ex, out ez, out nx, out nz, out roofY))
            {
                snap.Valid = true;
                snap.Edge = new Vector3(ex, roofY, ez);
                snap.Nx = nx;
                snap.Nz = nz;
            }

            _snapLast = snap;
            return snap;
        }

        private bool _loggedSnapFailure;

        /// <summary>The ground, the roofs and the roads (the game's ToolBase.RayCast, on the simulation thread).</summary>
        private static bool Cast(Ray ray, float length, out RaycastOutput output)
        {
            var input = new RaycastInput(ray, length);
            input.m_buildingService = new RaycastService(ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Layer.Default);
            input.m_netService = new RaycastService(ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Layer.Default);
            input.m_ignoreBuildingFlags = Building.Flags.None;
            input.m_ignoreSegmentFlags = NetSegment.Flags.None;
            input.m_ignoreNodeFlags = NetNode.Flags.None;
            return RayCast(input, out output);
        }

        /// <summary>
        /// On the SIMULATION thread (an AddAction): from a point along a direction, how far to the
        /// first ground, roof or road -- where a drawn pole's foot stands (FlagRenderer). False if
        /// nothing is met within <paramref name="length"/>.
        /// </summary>
        public static bool CastAlong(Vector3 from, Vector3 direction, float length, out float distance)
        {
            distance = 0f;
            RaycastOutput output;
            if (!Cast(new Ray(from, direction), length, out output))
                return false;

            distance = Vector3.Dot(output.m_hitPos - from, direction.normalized);
            return distance > 0f;
        }

        private bool TryHit(out Vector3 hit)
        {
            EdgeSnap snap;
            return TryHit(out hit, out snap);
        }

        /// <summary>The MAIN thread's: the hit, and the roof's edge near it (looked for here, once per move: EdgeNear).</summary>
        private bool TryHit(out Vector3 hit, out EdgeSnap snap)
        {
            lock (_hitLock)
                hit = _hit;

            bool valid = _hitValid && _mouseRayValid;
            snap = valid && CurrentMode == Mode.Place && SnapsToEdges ? EdgeNear(hit) : default(EdgeSnap);
            return valid;
        }

        /// <summary>
        /// Where the new flag goes and which way it heads: on a roof's edge the pointer is near
        /// (FlagEdgeSnap.Place: along it or out from it, as its rotation is nearer, raised by the
        /// keys), else standing on the surface under the pointer (<see cref="AnchorOver"/>). True: on
        /// the edge.
        /// </summary>
        private bool GhostPlace(Vector3 hit, EdgeSnap snap, FlagSpec spec, out Vector3 anchor, out float angle)
        {
            if (snap.Valid && spec.HoistedFrom == FlagHoist.Top && SnapsToEdges)
            {
                float ax, ay, az;
                FlagEdgeSnap.Place(snap.Edge.x, snap.Edge.y + _lift, snap.Edge.z, snap.Nx, snap.Nz, NewAngle(), spec.Height,
                                   spec.HasBackPlate && FlagMotion.Moves(spec.Motion), out ax, out ay, out az, out angle);
                anchor = new Vector3(ax, ay, az);
                return true;
            }

            anchor = AnchorOver(hit, spec);
            angle = NewAngle();
            return false;
        }

        /// <summary>
        /// Where the new flag's anchor (the top of its hoist) goes: the flag of <paramref name="spec"/>
        /// standing on the surface under the cursor (FlagStand), raised or lowered whole by the keys.
        /// </summary>
        private Vector3 AnchorOver(Vector3 surface, FlagSpec spec)
        {
            float x, y, z;
            FlagStand.Anchor(spec, NewAngle(), spec.PoleLength, surface.x, surface.y + _lift, surface.z, out x, out y, out z);
            return new Vector3(x, y, z);
        }

        /// <summary>The new flag's rotation, degrees: the way the next flag points.</summary>
        private static float NewDegrees()
        {
            return Settings.NewRotation != null ? Settings.NewRotation.value : Settings.Defaults.Rotation;
        }

        private static float NewAngle()
        {
            return NewDegrees() * Mathf.Deg2Rad;
        }

        /// <summary>Turns the next flag: its rotation in whole degrees, 0..359, as its slider has them.</summary>
        private static void SetNewDegrees(float degrees)
        {
            if (Settings.NewRotation != null)
                Settings.NewRotation.value = FlagOrientation.WholeDegrees(degrees);
        }

        /// <summary>Where the flag being placed stands: under the cursor, or where it held while a right drag turns it.</summary>
        private bool TryGhostHit(out Vector3 hit)
        {
            EdgeSnap snap;
            return TryGhostHit(out hit, out snap);
        }

        private bool TryGhostHit(out Vector3 hit, out EdgeSnap snap)
        {
            if (_rightDown && _turned)
            {
                hit = _turnHit;
                snap = _turnSnap;
                return _turnHitValid;
            }

            return TryHit(out hit, out snap);
        }

        /// <summary>Placing: the right button went down over the city. What it does is known when it moves or comes up (<see cref="UpdateTurn"/>).</summary>
        private void BeginTurn()
        {
            _rightDown = true;
            _turned = false;
            _turnFrom = _turnTo = NewDegrees();
            _turnHitValid = TryHit(out _turnHit, out _turnSnap);
        }

        /// <summary>
        /// Placing, every frame: while the right button is down, the pointer's sideways travel
        /// turns the new flag; let go without that, it turns a step. True while a drag holds it.
        /// </summary>
        /// <remarks>
        /// Read from Input here, not from OnToolGUI's MouseUp: Unity calls Update before OnGUI, so
        /// a let-go is seen here first -- and here even off the window, where no event may come.
        /// </remarks>
        private bool UpdateTurn()
        {
            if (!_rightDown)
                return false;

            if (!Input.GetMouseButton(1))
            {
                _rightDown = false;
                if (!_turned)
                    SetNewDegrees(FlagOrientation.StepTurn(NewDegrees()));
                Log.Msg("flags: the new flag turned to " + NewDegrees().ToString("F0") + " deg (" + (_turned ? "a right drag" : "a right click") + ")");
                return false;
            }

            float travel = Input.GetAxis("Mouse X");
            if (travel != 0f)
            {
                _turned = true;
                _turnTo = Mathf.Repeat(_turnTo + travel * TurnPerMouseX, 360f);
                SetNewDegrees(_turnTo);
            }

            return _turned;
        }

        /// <summary>
        /// Every GUI event (Move It's OnToolGUI reads its keys on every one): the raise and lower
        /// keys move a flag up and down whole (ElevationKeys) -- placing, the new one off the
        /// surface it stands on; picking, the selected one. Not while a text field has the
        /// keyboard, as Move It does not.
        /// </summary>
        private void UpdateRaise(Event e)
        {
            UpdateRaise(e.control, e.shift, e.alt);
        }

        /// <summary>
        /// Every frame while Line Tool places a row: the raise and lower keys lift the whole row, as
        /// they lift the new flag. This tool is not out then and gets no GUI events: read from Input.
        /// </summary>
        public void RaiseOverLine()
        {
            UpdateRaise(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
                        Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                        Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));
        }

        /// <summary>The new flag's (or row's) lift off what it stands on, metres.</summary>
        public static float Lift
        {
            get { return Instance != null ? Instance._lift : 0f; }
        }

        /// <summary>The row is over: the next flag starts on the surface again.</summary>
        public static void ForgetLift()
        {
            if (Instance != null)
            {
                Instance.EndRaise();
                Instance._lift = 0f;
            }
        }

        private void UpdateRaise(bool control, bool shift, bool alt)
        {
            if (_raiseKey == null || _lowerKey == null ||
                ColossalFramework.UI.UIView.HasModalInput() || ColossalFramework.UI.UIView.HasInputFocus())
                return;

            bool up = Held(_raiseKey, control), down = Held(_lowerKey, control);
            float step = _elevation.Event(up, down, shift, alt, System.Diagnostics.Stopwatch.GetTimestamp(),
                                          System.Diagnostics.Stopwatch.Frequency);
            if (step != 0f)
            {
                if (CurrentMode == Mode.Place)
                    RaiseNew(step);
                else
                    RaiseSelected(step);
            }
            else if (!up && !down)
            {
                EndRaise();
            }
        }

        /// <summary>Placing: the new flag, pole and all, a step higher or lower off the surface it stands on.</summary>
        private void RaiseNew(float step)
        {
            _lift = Mathf.Clamp(_lift + step, -MaxLift, MaxLift);
            Stepped(step, 0);
        }

        /// <summary>
        /// Picking: the selected flag, pole and all, a step higher or lower -- as the gizmo's arrows
        /// move it (its pole's length kept), from where the keys have sent it while they work.
        /// </summary>
        private void RaiseSelected(float step)
        {
            if (_gizmo.IsDragging || FlagEditor.Selected == 0)
                return;

            float now = Time.realtimeSinceStartup;
            if (_raisedFlag != FlagEditor.Selected || now > _raiseOwnUntil)
            {
                FlagEntry entry;
                if (!FlagEditor.TryGetSelected(out entry))
                    return;

                _raiseAnchor = entry.Anchor;
                _raiseAngle = entry.Angle;
                _raiseTilt = entry.Spec.Tilt;
                _raiseLean = entry.Spec.Lean;
            }

            _raiseAnchor.y += step;
            FlagEditor.ApplyTransform(_raiseAnchor, _raiseAngle, _raiseTilt, _raiseLean);
            _raiseOwnUntil = now + RaiseOwnTime;
            Stepped(step, FlagEditor.Selected);
        }

        /// <summary>A step taken: counted for the tooltip and the log line when the keys are let go.</summary>
        private void Stepped(float step, uint flag)
        {
            if (!_raising || _raisedFlag != flag)
            {
                EndRaise();
                _raising = true;
                _raisedBy = 0f;
                _raisedFlag = flag;
            }

            _raisedBy += step;
            _raisedAt = Time.realtimeSinceStartup;
        }

        /// <summary>The keys let go (or the tool put away, or turned to the other mode): how far they took the flag, for the log.</summary>
        private void EndRaise()
        {
            if (!_raising)
                return;

            _raising = false;
            string keys = (_raiseKey != null ? _raiseKey.ToString() : "?") + " / " + (_lowerKey != null ? _lowerKey.ToString() : "?");
            string moved = (_raisedBy >= 0f ? "raised " : "lowered ") + Mathf.Abs(_raisedBy).ToString("0.###") + " m, pole and all";
            if (_raisedFlag != 0)
                Log.Msg("flags: flag #" + _raisedFlag + " " + moved + " (" + keys + ")");
            else
                Log.Msg("flags: the new flag " + moved + ": " + _lift.ToString("0.###") + " m off the surface under it now (" + keys + ")");
        }

        /// <summary>The keys moved a flag a moment ago (or are held): its move goes in the tooltip.</summary>
        private bool ShowingRaise
        {
            get { return _raising || (_raisedAt >= 0f && Time.realtimeSinceStartup - _raisedAt < RaiseShown); }
        }

        /// <summary>"Raised 3.125 m" or "Lowered 2 m".</summary>
        private static string RaiseText(float metres)
        {
            string abs = Mathf.Abs(metres).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            return Localization.Get(metres >= 0f ? "Tool.Raised" : "Tool.Lowered", abs);
        }

        /// <summary>
        /// A raise or lower key held as Move It reads its own (MoveItTool.IsKeyDown, its IL): its key
        /// down, and Ctrl as its binding has it -- Shift and Alt only scale the step.
        /// </summary>
        private static bool Held(SavedInputKey key, bool control)
        {
            KeyCode code = key.Key;
            return code != KeyCode.None && Input.GetKey(code) && key.Control == control;
        }

        /// <summary>The raise and lower keys' names, as the game's key mapping shows them.</summary>
        private string RaiseKeys()
        {
            if (_raiseKey == null || _lowerKey == null)
                return "Page Up / Page Down";

            return _raiseKey.ToLocalizedString("KEYNAME") + " / " + _lowerKey.ToLocalizedString("KEYNAME");
        }

        protected override void OnToolUpdate()
        {
            base.OnToolUpdate();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Typing in a text field (a colour's code, the country search): Esc is the
                // field's -- it gives the edit up -- and the game's shortcuts leave it alone too.
                if (ColossalFramework.UI.UIView.HasInputFocus())
                    return;

                if (!UI.FlagsPanel.CatchEscape())
                    StepBack();
                return;
            }

            // Delete: the selected flag, after the panel's own question (not while typing or asked something).
            if (Input.GetKeyDown(KeyCode.Delete) && FlagEditor.HasSelection &&
                !ColossalFramework.UI.UIView.HasInputFocus() && !ColossalFramework.UI.UIView.HasModalInput())
            {
                FlagEditor.ConfirmDelete();
                return;
            }

            Vector3 hit;
            bool valid;

            if (CurrentMode == Mode.Place)
            {
                Hovered = 0;
                bool turning = UpdateTurn();
                EdgeSnap snap;
                valid = TryGhostHit(out hit, out snap);
                bool onEdge = false;
                if (valid && FlagRenderer.Instance != null)
                {
                    FlagSpec spec = Settings.NewSpec();
                    Vector3 anchor;
                    float angle;
                    onEdge = GhostPlace(hit, snap, spec, out anchor, out angle);
                    FlagRenderer.Instance.SetGhost(anchor, angle, spec);
                }

                // Raised or lowered off the surface: said while the keys work, and over the hint after.
                string text = FlagPrefab.Info == null ? Localization.Get("Tool.NoPrefab")
                            : turning ? Localization.Get("Gizmo.Heading", NewDegrees().ToString("0", System.Globalization.CultureInfo.InvariantCulture))
                            : ShowingRaise && _raisedFlag == 0 ? RaiseText(_lift)
                            : Mathf.Abs(_lift) > 1e-4f ? RaiseText(_lift) + "\n" + Localization.Get("Tool.PlaceHint", RaiseKeys())
                            : Localization.Get("Tool.PlaceHint", RaiseKeys());
                if (onEdge && FlagPrefab.Info != null)
                    text = Localization.Get("Tool.OnEdge") + "\n" + text;
                ShowToolInfo(valid, valid ? text : null, hit);
                return;
            }

            valid = TryHit(out hit);
            UpdatePick(valid, hit);
        }

        /// <summary>Pick mode: the gizmo on the selected flag (a drag under way, or a handle under the pointer), else the flag under it.</summary>
        private void UpdatePick(bool valid, Vector3 hit)
        {
            Camera camera = Camera.main;
            Vector2 mouse = Input.mousePosition;
            FlagEntry selected = default(FlagEntry);
            bool haveSelected = camera != null && FlagEditor.TryGetSelected(out selected);

            // Raised or lowered by the keys a moment ago: where they sent it, which its prop reaches a frame or two later.
            if (haveSelected && _raisedFlag == selected.Prop && Time.realtimeSinceStartup <= _raiseOwnUntil)
                selected.Anchor = _raiseAnchor;

            if (haveSelected)
                _gizmo.Place(selected.Anchor, selected.Angle, selected.Spec.Tilt, selected.Spec.Lean, camera.transform.position);

            if (_gizmo.IsDragging)
            {
                // The gizmo moves it now: where the keys sent it is past.
                _raiseOwnUntil = 0f;
                Hovered = 0;
                if (!haveSelected || !Input.GetMouseButton(0))
                {
                    EndDrag();
                }
                else
                {
                    bool snap = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    bool fine = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    if (_gizmo.Update(camera, mouse, snap, fine))
                        _gizmo.Follow(FlagEditor.ApplyTransform(_gizmo.Anchor, _gizmo.Angle, _gizmo.Tilt, _gizmo.Lean));
                    ShowToolInfo(true, _gizmo.Describe(), _gizmo.Anchor);
                    return;
                }
            }

            // A handle under the pointer comes before the flags behind it.
            FlagGizmo.Handle handle = FlagGizmo.Handle.None;
            if (haveSelected && _mouseRayValid)
                handle = _gizmo.Pick(camera, mouse);
            else
                _gizmo.ClearHover();

            if (handle != FlagGizmo.Handle.None)
            {
                Hovered = 0;
                ShowToolInfo(true, FlagGizmo.Hint(handle), selected.Anchor);
                return;
            }

            Hovered = 0;
            if (_mouseRayValid && FlagRenderer.Instance != null)
            {
                var segment = new Segment3(_mouseRay.origin, _mouseRay.origin + _mouseRay.direction * _mouseRayLength);
                Hovered = FlagRenderer.Instance.PickAt(segment);
            }

            bool raised = haveSelected && ShowingRaise && _raisedFlag == selected.Prop;
            string text = raised ? RaiseText(_raisedBy)
                        : Hovered != 0 && Hovered != FlagEditor.Selected ? Localization.Get("Tool.PickHint")
                        : haveSelected ? Localization.Get("Tool.SelectedHint", RaiseKeys())
                        : Localization.Get("Tool.PickNone");
            ShowToolInfo(valid || Hovered != 0 || raised, text, !raised && (Hovered != 0 || !haveSelected) ? hit : selected.Anchor);
        }

        protected override void OnToolGUI(Event e)
        {
            // Every event, as Move It reads its keys: held, they repeat at the GUI's pace.
            UpdateRaise(e);

            if (e.type == EventType.MouseUp && e.button == 0 && _gizmo.IsDragging)
            {
                EndDrag();
                return;
            }

            if (m_toolController.IsInsideUI || e.type != EventType.MouseDown)
                return;

            if (e.button == 1)
            {
                // Placing, it turns the new flag, as the game's props turn; picking, it steps back.
                if (CurrentMode == Mode.Place)
                    BeginTurn();
                else
                    StepBack();
                return;
            }

            if (e.button != 0)
                return;

            if (CurrentMode == Mode.Pick)
            {
                Camera camera = Camera.main;
                if (camera != null && _gizmo.Hovered != FlagGizmo.Handle.None && _gizmo.Begin(camera, Input.mousePosition))
                    return;

                if (Hovered != 0)
                {
                    if (Hovered != FlagEditor.Selected)
                    {
                        FlagEditor.Select(Hovered);
                        UI.FlagsPanel.ShowExisting();
                    }
                }
                else
                {
                    // A click on nothing lets the selection go.
                    FlagEditor.Deselect();
                }

                return;
            }

            Vector3 hit;
            EdgeSnap snap;
            if (!TryGhostHit(out hit, out snap))
                return;

            // Its pole (if it stands) as tall as the Pole height says: the placed flag keeps it.
            FlagSpec spec = Settings.NewSpec();
            Vector3 anchor;
            float angle;
            bool onEdge = GhostPlace(hit, snap, spec, out anchor, out angle);
            float lift = _lift;

            FlagProps.Create(anchor, angle, spec, prop =>
            {
                if (prop == 0)
                    Log.Warn("flags: the flag was not placed (the city has as many props as it can hold)");
                else
                    Log.Msg("flags: flag #" + prop + " placed at (" + anchor.x.ToString("F0") + ", " + anchor.z.ToString("F0") + "), top of the hoist " +
                            anchor.y.ToString("F1") + " m (" + (anchor.y - hit.y).ToString("F1") + " m over the surface under it" +
                            (Mathf.Abs(lift) > 1e-4f ? ", " + (lift > 0f ? "raised " : "lowered ") + Mathf.Abs(lift).ToString("0.###") + " m off it" : "") +
                            (onEdge ? ", on a roof's edge" : "") + "), " + (angle * Mathf.Rad2Deg).ToString("F0") + " deg: " + spec.Describe());
            });
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            base.RenderOverlay(cameraInfo);

            try
            {
                if (CurrentMode == Mode.Pick)
                {
                    if (FlagEditor.Selected != 0)
                        _gizmo.Draw();
                    return;
                }

                Vector3 hit;
                EdgeSnap snap;
                if (!TryGhostHit(out hit, out snap))
                    return;

                // On a roof's edge: the ring on the edge it hangs from.
                if (snap.Valid && SnapsToEdges)
                    hit = snap.Edge;

                // Where the flag stands (its pole's foot, or its hoist), on the ground or roof.
                Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(cameraInfo, PlaceColour, hit, 1.2f, hit.y - 20f, hit.y + 20f,
                                                                          false, true);
                Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            }
            catch (Exception e)
            {
                // Once per different error: the overlay is drawn every frame.
                if (e.Message != _overlayFailure)
                {
                    _overlayFailure = e.Message;
                    Log.Warn("flags: drawing the tool's overlay threw (said once): " + e.Message);
                }
            }
        }

        private string _overlayFailure;
    }
}
