using System;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The mod's tool on the map, in two modes. PLACE (while the New flag window is open): the new
    /// flag follows the cursor -- the real cloth, blowing -- at the chosen height over the ground
    /// or roof under it; left click places it, again and again; the right button turns it as the
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
    /// The game's raise and lower keys (Page Up, Page Down) move a flag up and down WHOLE, its pole
    /// with it, as Move It moves its selection (ElevationKeys: 0.125 m a step, Shift x8, Alt /8,
    /// repeating after a third of a second held). Placing, they raise the new flag over its
    /// elevation (<see cref="NewLift"/>, shown after the elevation in the New flag window, let go
    /// with the window): its pole keeps the length it has at the elevation, its foot off the
    /// ground (or in it), and the flag placed keeps that pole -- the first version changed the
    /// elevation itself, and a pole always reaches down to what is under it: "PgUp / down just
    /// increases the height of the flagpole". Picking, they move the selected flag as the gizmo's
    /// arrows do (FlagEditor.ApplyTransform: its pole's length kept).
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

        private readonly FlagGizmo _gizmo = new FlagGizmo();

        // Placing: the right button's turn of the new flag (main thread). _turnTo is the drag's
        // own sum, unrounded (the setting keeps whole degrees); _turnHit is where the flag holds.
        private bool _rightDown;
        private bool _turned;
        private float _turnFrom, _turnTo;
        private bool _turnHitValid;
        private Vector3 _turnHit;

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

        /// <summary>Placing: the new flag raised (or, less than 0, lowered) whole over its elevation, metres.</summary>
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

        /// <summary>The new flag is raised or lowered at most this far over its elevation, metres.</summary>
        private const float MaxLift = 1000f;

        /// <summary>Placing: the new flag raised (or, less than 0, lowered) whole over its elevation by Page Up and Page Down, metres; 0 when not placing.</summary>
        public static float NewLift
        {
            get { return Instance != null && Instance.enabled && Instance.CurrentMode == Mode.Place ? Instance._lift : 0f; }
        }

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
            _lift = 0f;

            // Put away by any means: a drag stays where it got to, the selection goes.
            _gizmo.End();
            _gizmo.ClearHover();
            FlagEditor.Deselect();
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
                lock (_hitLock)
                    _hit = output.m_hitPos;
                _hitValid = true;
            }
            else
            {
                _hitValid = false;
            }
        }

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
            lock (_hitLock)
                hit = _hit;
            return _hitValid && _mouseRayValid;
        }

        /// <summary>
        /// Where the flag would stand: its anchor (the top of the hoist) the chosen height over the
        /// surface under the cursor, and raised or lowered whole by the keys (<see cref="NewLift"/>).
        /// </summary>
        private Vector3 AnchorOver(Vector3 hit)
        {
            float height = Settings.NewHeight != null ? Settings.NewHeight.value : Settings.Defaults.Height;
            return new Vector3(hit.x, hit.y + Mathf.Max(0f, height) + _lift, hit.z);
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
            if (_rightDown && _turned)
            {
                hit = _turnHit;
                return _turnHitValid;
            }

            return TryHit(out hit);
        }

        /// <summary>Placing: the right button went down over the city. What it does is known when it moves or comes up (<see cref="UpdateTurn"/>).</summary>
        private void BeginTurn()
        {
            _rightDown = true;
            _turned = false;
            _turnFrom = _turnTo = NewDegrees();
            _turnHitValid = TryHit(out _turnHit);
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
        /// keys move a flag up and down whole (ElevationKeys) -- placing, the new one over its
        /// elevation; picking, the selected one. Not while a text field has the keyboard, as Move
        /// It does not.
        /// </summary>
        private void UpdateRaise(Event e)
        {
            if (_raiseKey == null || _lowerKey == null ||
                ColossalFramework.UI.UIView.HasModalInput() || ColossalFramework.UI.UIView.HasInputFocus())
                return;

            bool up = Held(_raiseKey, e), down = Held(_lowerKey, e);
            float step = _elevation.Event(up, down, e.shift, e.alt, System.Diagnostics.Stopwatch.GetTimestamp(),
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

        /// <summary>Placing: the new flag, pole and all, a step higher or lower over its elevation.</summary>
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
                Log.Msg("flags: the new flag " + moved + ": " + _lift.ToString("0.###") + " m over its elevation now (" + keys + ")");
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
        private static bool Held(SavedInputKey key, Event e)
        {
            KeyCode code = key.Key;
            return code != KeyCode.None && Input.GetKey(code) && key.Control == e.control;
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

            Vector3 hit;
            bool valid;

            if (CurrentMode == Mode.Place)
            {
                Hovered = 0;
                bool turning = UpdateTurn();
                valid = TryGhostHit(out hit);
                if (valid && FlagRenderer.Instance != null)
                    FlagRenderer.Instance.SetGhost(AnchorOver(hit), NewAngle(), Settings.NewSpec(), _lift);

                string text = FlagPrefab.Info == null ? Localization.Get("Tool.NoPrefab")
                            : turning ? Localization.Get("Gizmo.Heading", NewDegrees().ToString("0", System.Globalization.CultureInfo.InvariantCulture))
                            : ShowingRaise && _raisedFlag == 0 ? RaiseText(_lift)
                            : Localization.Get("Tool.PlaceHint", RaiseKeys());
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
                    _gizmo.End();
                }
                else
                {
                    bool snap = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    bool fine = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    if (_gizmo.Update(camera, mouse, snap, fine))
                        FlagEditor.ApplyTransform(_gizmo.Anchor, _gizmo.Angle, _gizmo.Tilt, _gizmo.Lean);
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
                _gizmo.End();
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
            if (!TryGhostHit(out hit))
                return;

            Vector3 anchor = AnchorOver(hit);
            float angle = NewAngle();
            FlagSpec spec = Settings.NewSpec();

            // Raised or lowered whole by the keys: its pole as long as the flag being placed shows it
            // (as it stands at the elevation), kept -- not found down to the ground again from where
            // the flag now is.
            float lift = _lift, pole;
            if (spec.HasPole && Mathf.Abs(lift) > 1e-4f && FlagRenderer.Instance != null && FlagRenderer.Instance.TryGhostPole(out pole))
                spec.PoleLength = Mathf.Clamp(pole, FlagPoleShape.MinLength, FlagSpec.MaxPoleLength);

            FlagProps.Create(anchor, angle, spec, prop =>
            {
                if (prop == 0)
                    Log.Warn("flags: the flag was not placed (the city has as many props as it can hold)");
                else
                    Log.Msg("flags: flag #" + prop + " placed at (" + anchor.x.ToString("F0") + ", " + anchor.z.ToString("F0") + "), top of the hoist " +
                            anchor.y.ToString("F1") + " m (" + (anchor.y - hit.y).ToString("F1") + " m over what is under it" +
                            (Mathf.Abs(lift) > 1e-4f ? ", " + (lift > 0f ? "raised " : "lowered ") + Mathf.Abs(lift).ToString("0.###") + " m whole" +
                             (spec.PoleLength > 0f ? ", its pole " + spec.PoleLength.ToString("F1") + " m" : "") : "") + "), " +
                            (angle * Mathf.Rad2Deg).ToString("F0") + " deg: " + spec.Describe());
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
                if (!TryGhostHit(out hit))
                    return;

                // Where the pole would stand, on the ground or roof.
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
