using System;
using ColossalFramework;
using ColossalFramework.Math;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The mod's tool on the map, in two modes. PLACE (the Create new tab's button): the Create
    /// new flag follows the cursor -- the real cloth, blowing -- at the chosen height over the
    /// ground or roof under it; left click places it, again and again; right click or Esc puts
    /// the tool away. PICK (the Existing flags tab's): the flag under the cursor lights up, a
    /// click selects it; the selected flag carries the gizmo (FlagGizmo) -- drag its arrows to
    /// move it, its rings to turn it -- and a click on nothing, a right click or Esc lets it go.
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
    /// Esc and the right button step back one thing at a time: a drag (the flag goes back where
    /// it was), then the selection, then the tool. The selection lives while the tool is out:
    /// the tool put away by any means lets it go (<see cref="OnDisable"/>).
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
                    tool._gizmo.Cancel();
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
        /// Esc or the right button: one step back -- a drag given up (the flag back where it was),
        /// else the selection let go, else the tool put away.
        /// </summary>
        public static void StepBack()
        {
            FlagTool tool = Instance;
            if (tool != null && tool._gizmo.IsDragging)
            {
                tool._gizmo.Cancel();
                FlagEditor.ApplyTransform(tool._gizmo.Anchor, tool._gizmo.Angle, tool._gizmo.Tilt, tool._gizmo.Lean);
                Log.Msg("flags: the drag was given up; the flag is back where it was");
                return;
            }

            if (tool != null && tool.CurrentMode == Mode.Pick && FlagEditor.Selected != 0)
            {
                FlagEditor.Deselect();
                return;
            }

            PutAway();
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
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _mouseRayValid = false;
            _hitValid = false;
            Hovered = 0;

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

        /// <summary>Where the flag would stand: its anchor (the top of the hoist) the chosen height over the surface under the cursor.</summary>
        private static Vector3 AnchorOver(Vector3 hit)
        {
            float height = Settings.NewHeight != null ? Settings.NewHeight.value : Settings.Defaults.Height;
            return new Vector3(hit.x, hit.y + Mathf.Max(0f, height), hit.z);
        }

        private static float NewAngle()
        {
            float degrees = Settings.NewRotation != null ? Settings.NewRotation.value : Settings.Defaults.Rotation;
            return degrees * Mathf.Deg2Rad;
        }

        protected override void OnToolUpdate()
        {
            base.OnToolUpdate();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (!UI.FlagsPanel.CatchEscape())
                    StepBack();
                return;
            }

            Vector3 hit;
            bool valid = TryHit(out hit);

            if (CurrentMode == Mode.Place)
            {
                Hovered = 0;
                if (valid && FlagRenderer.Instance != null)
                    FlagRenderer.Instance.SetGhost(AnchorOver(hit), NewAngle(), Settings.NewSpec());

                ShowToolInfo(valid, valid ? Localization.Get(FlagPrefab.Info != null ? "Tool.PlaceHint" : "Tool.NoPrefab") : null, hit);
                return;
            }

            UpdatePick(valid, hit);
        }

        /// <summary>Pick mode: the gizmo on the selected flag (a drag under way, or a handle under the pointer), else the flag under it.</summary>
        private void UpdatePick(bool valid, Vector3 hit)
        {
            Camera camera = Camera.main;
            Vector2 mouse = Input.mousePosition;
            FlagEntry selected = default(FlagEntry);
            bool haveSelected = camera != null && FlagEditor.TryGetSelected(out selected);

            if (haveSelected)
                _gizmo.Place(selected.Anchor, selected.Angle, selected.Spec.Tilt, selected.Spec.Lean, camera.transform.position);

            if (_gizmo.IsDragging)
            {
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

            string text = Hovered != 0 && Hovered != FlagEditor.Selected ? Localization.Get("Tool.PickHint")
                        : haveSelected ? Localization.Get("Tool.SelectedHint")
                        : Localization.Get("Tool.PickNone");
            ShowToolInfo(valid || Hovered != 0, text, Hovered != 0 || !haveSelected ? hit : selected.Anchor);
        }

        protected override void OnToolGUI(Event e)
        {
            if (e.type == EventType.MouseUp && e.button == 0 && _gizmo.IsDragging)
            {
                _gizmo.End();
                return;
            }

            if (m_toolController.IsInsideUI || e.type != EventType.MouseDown)
                return;

            if (e.button == 1)
            {
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
            if (!TryHit(out hit))
                return;

            Vector3 anchor = AnchorOver(hit);
            float angle = NewAngle();
            FlagSpec spec = Settings.NewSpec();
            FlagProps.Create(anchor, angle, spec, prop =>
            {
                if (prop == 0)
                    Log.Warn("flags: the flag was not placed (the city has as many props as it can hold)");
                else
                    Log.Msg("flags: flag #" + prop + " placed at (" + anchor.x.ToString("F0") + ", " + anchor.z.ToString("F0") + "), top of the hoist " +
                            anchor.y.ToString("F1") + " m (" + (anchor.y - hit.y).ToString("F1") + " m over what is under it), " +
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
                if (!TryHit(out hit))
                    return;

                // Where the pole would stand, on the ground or roof.
                Singleton<RenderManager>.instance.OverlayEffect.DrawCircle(cameraInfo, PlaceColour, hit, 1.2f, hit.y - 20f, hit.y + 20f,
                                                                          false, true);
                Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
            }
            catch (Exception e)
            {
                Log.Warn("flags: drawing the tool's overlay threw: " + e.Message);
            }
        }
    }
}
