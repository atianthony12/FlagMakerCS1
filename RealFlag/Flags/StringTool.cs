using System;
using System.Collections.Generic;
using ColossalFramework;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The strings' tool (2026-11-07). PLACE: each click puts a point where the pointer meets the
    /// city -- on a wall's face, a roof, the ground (GroundRise over the ground: a string is not
    /// laid on it), or a string's end, which it joins -- and from the second point on each click
    /// strings a span from the last, at once, as the game's network tools build: a chain of
    /// spans zig-zagging across a street. Page Up/Down raise the next point (the game's elevation
    /// keys, as FlagTool reads them). The right button ends the chain (the next click starts
    /// another); Esc, or the window closed, puts it away. The span being strung follows the
    /// pointer, drawn as it will be. PICK: the string under the pointer glows; a click selects it.
    /// </summary>
    public sealed class StringTool : ToolBase
    {
        public enum Mode { Place, Pick }

        public static StringTool Instance { get; private set; }

        public Mode CurrentMode { get; private set; }

        /// <summary>A click on the ground puts the point this far above it, metres (a click on a building, on it).</summary>
        public const float GroundRise = 4f;

        /// <summary>A click this near a string's end (metres, at least; more for a far camera) joins it.</summary>
        public const float JoinRadius = 1f;

        /// <summary>How far the keys may take the next point off what it is on, metres.</summary>
        public const float MaxLift = 200f;

        private Ray _mouseRay;
        private float _mouseRayLength;
        private volatile bool _mouseRayValid;
        private readonly object _hitLock = new object();
        private Vector3 _hit;
        private bool _hitOnGround;
        private volatile bool _hitValid;

        private bool _hasLast;
        private Vector3 _last;
        private ushort _lastNode;
        private int _spansThisChain;
        private float _lift;
        private SavedInputKey _raiseKey, _lowerKey;
        private ElevationKeys _elevation;
        private ushort _hovered;
        private readonly List<Vector3> _ghost = new List<Vector3>();
        private readonly List<ushort> _spans = new List<ushort>();
        private string _overlayFailure;

        private static readonly Color PointColour = new Color(0.95f, 0.85f, 0.4f, 0.7f);
        private static readonly Color JoinColour = new Color(0.4f, 0.95f, 0.5f, 0.8f);

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

        public static void Open(Mode mode)
        {
            try
            {
                ToolController controller = ToolsModifierControl.toolController;
                if (controller == null)
                    return;

                StringTool tool = Instance;
                bool was = tool != null && controller.CurrentTool == tool && tool.CurrentMode == mode;
                if (tool == null)
                    tool = controller.gameObject.AddComponent<StringTool>();
                if (tool.CurrentMode != mode)
                    tool.EndChain();
                tool.CurrentMode = mode;
                controller.CurrentTool = tool;
                if (!was)
                    Log.Msg("strings: tool " + (mode == Mode.Place ? "placing strings" : "picking a string"));
            }
            catch (Exception e)
            {
                Log.Error("strings: could not open the tool.", e);
            }
        }

        public static void PutAway()
        {
            try
            {
                if (Instance != null && ToolsModifierControl.toolController != null && ToolsModifierControl.toolController.CurrentTool == Instance)
                    ToolsModifierControl.SetTool<DefaultTool>();
            }
            catch (Exception e)
            {
                Log.Warn("strings: could not put the tool away: " + e.Message);
            }
        }

        /// <summary>Esc: a chain under way ended, else the selection let go, else the tool put away.</summary>
        public static string StepBack()
        {
            StringTool tool = Instance;
            if (tool != null && tool.CurrentMode == Mode.Place && tool._hasLast)
            {
                tool.EndChain();
                return "the string ended";
            }

            if (tool != null && tool.CurrentMode == Mode.Pick && StringEditor.HasSelection)
            {
                StringEditor.Deselect();
                return "the selection let go";
            }

            PutAway();
            return "the tool put away";
        }

        public static void Remove()
        {
            StringTool tool = Instance;
            if (tool == null)
                return;
            PutAway();
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
                Log.Warn("strings: the game's raise and lower keys could not be read: " + e.Message);
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _mouseRayValid = false;
            _hitValid = false;
            EndChain();
            _lift = 0f;
            _hovered = 0;
            if (StringRenderer.Instance != null)
            {
                StringRenderer.Instance.ClearGhost();
                StringRenderer.Instance.SetHighlight(StringEditor.SelectedChain, 0);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this)
                Instance = null;
        }

        private void EndChain()
        {
            if (_hasLast && _spansThisChain > 0)
                Log.Msg("strings: a string of " + _spansThisChain + " span(s) placed");
            _hasLast = false;
            _lastNode = 0;
            _spansThisChain = 0;
            if (StringRenderer.Instance != null)
                StringRenderer.Instance.ClearGhost();
        }

        protected override void OnToolLateUpdate()
        {
            TakePendingNode();
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

            var input = new RaycastInput(_mouseRay, _mouseRayLength);
            input.m_buildingService = new RaycastService(ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Layer.Default);
            input.m_netService = new RaycastService(ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Layer.Default);
            input.m_ignoreBuildingFlags = Building.Flags.None;
            input.m_ignoreSegmentFlags = NetSegment.Flags.None;
            input.m_ignoreNodeFlags = NetNode.Flags.None;
            RaycastOutput output;
            if (RayCast(input, out output))
            {
                // A building's face or roof is where the string ties; the ground or a road, a point above it.
                bool ground = output.m_building == 0;
                lock (_hitLock)
                {
                    _hit = output.m_hitPos;
                    _hitOnGround = ground;
                }

                _hitValid = true;
            }
            else
            {
                _hitValid = false;
            }
        }

        /// <summary>Where the next point goes, and the string end it joins (0: a new node).</summary>
        private bool TryPoint(out Vector3 point, out ushort join)
        {
            point = Vector3.zero;
            join = 0;
            Vector3 hit;
            bool ground;
            lock (_hitLock)
            {
                hit = _hit;
                ground = _hitOnGround;
            }

            if (!_hitValid || !_mouseRayValid)
                return false;

            point = hit + Vector3.up * ((ground ? GroundRise : 0f) + _lift);

            // A string's end near the point where it lands, or near the ray: joined.
            StringRenderer r = StringRenderer.Instance;
            if (r != null)
            {
                r.SpansTo(_spans);
                Camera camera = Camera.main;
                float radius = JoinRadius;
                if (camera != null)
                    radius = Mathf.Max(JoinRadius, 0.012f * Vector3.Distance(camera.transform.position, hit));
                ushort node = StringNet.NodeNear(point, radius, _spans);
                if (node == 0)
                    node = NodeNearRay(radius);
                if (node != 0 && node != _lastNode)
                {
                    join = node;
                    point = StringNet.NodePosition(node);
                }
            }

            return true;
        }

        private ushort NodeNearRay(float radius)
        {
            ushort best = 0;
            float bestDist = radius;
            NetManager net = Singleton<NetManager>.instance;
            foreach (ushort s in _spans)
            {
                NetSegment seg = net.m_segments.m_buffer[s];
                for (int k = 0; k < 2; k++)
                {
                    ushort node = k == 0 ? seg.m_startNode : seg.m_endNode;
                    Vector3 p = net.m_nodes.m_buffer[node].m_position;
                    float d = Vector3.Cross(_mouseRay.direction, p - _mouseRay.origin).magnitude;
                    if (d < bestDist && Vector3.Dot(p - _mouseRay.origin, _mouseRay.direction) > 0f)
                    {
                        bestDist = d;
                        best = node;
                    }
                }
            }

            return best;
        }

        protected override void OnToolUpdate()
        {
            base.OnToolUpdate();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (ColossalFramework.UI.UIView.HasInputFocus())
                    return;
                if (!UI.FlagsPanel.CatchEscape())
                    StepBack();
                return;
            }

            StringRenderer r = StringRenderer.Instance;
            Vector3 point;
            ushort join;
            bool valid = TryPoint(out point, out join);

            if (CurrentMode == Mode.Pick)
            {
                _hovered = 0;
                Camera camera = Camera.main;
                if (_mouseRayValid && r != null && camera != null)
                    _hovered = r.Pick(_mouseRay, camera, 14f);
                if (r != null)
                    r.SetHighlight(StringEditor.SelectedChain, _hovered);
                string text = _hovered != 0 ? Localization.Get("StringTool.PickHint")
                            : StringEditor.HasSelection ? Localization.Get("StringTool.Selected") : Localization.Get("StringTool.PickNone");
                ShowToolInfo(valid || _hovered != 0, text, point);
                return;
            }

            if (r != null)
            {
                if (_hasLast && valid)
                {
                    _ghost.Clear();
                    _ghost.Add(_last);
                    _ghost.Add(point);
                    r.SetGhost(_ghost, StringEditor.NewSpec());
                }
                else
                {
                    r.ClearGhost();
                }
            }

            string hint = StringPrefab.Info == null ? Localization.Get("StringTool.NoNetwork")
                        : !_hasLast ? Localization.Get("StringTool.Start", RaiseKeys())
                        : Localization.Get("StringTool.Next", RaiseKeys());
            if (Mathf.Abs(_lift) > 1e-4f)
                hint = Localization.Get(_lift >= 0f ? "Tool.Raised" : "Tool.Lowered", Mathf.Abs(_lift).ToString("0.###")) + "\n" + hint;
            if (_hasLast && valid)
            {
                float length = Vector3.Distance(_last, point);
                if (length > StringNet.MaxSpan)
                    hint = Localization.Get("StringTool.TooLong", StringNet.MaxSpan.ToString("F0")) + "\n" + hint;
                else
                    hint = Localization.Get("StringTool.Span", length.ToString("0.0")) + "\n" + hint;
            }

            if (join != 0)
                hint = Localization.Get("StringTool.Join") + "\n" + hint;
            ShowToolInfo(valid, hint, point);
        }

        private string RaiseKeys()
        {
            if (_raiseKey == null || _lowerKey == null)
                return "Page Up / Page Down";
            return _raiseKey.ToLocalizedString("KEYNAME") + " / " + _lowerKey.ToLocalizedString("KEYNAME");
        }

        protected override void OnToolGUI(Event e)
        {
            UpdateRaise(e);

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
                if (_hovered != 0)
                {
                    StringEditor.Select(_hovered);
                    UI.StringPanel.OpenSelected();
                }
                else
                {
                    StringEditor.Deselect();
                }

                return;
            }

            Vector3 point;
            ushort join;
            if (!TryPoint(out point, out join) || StringPrefab.Info == null)
                return;

            if (!_hasLast)
            {
                _hasLast = true;
                _last = point;
                _lastNode = join;
                _spansThisChain = 0;
                Log.Msg("strings: a string begins at (" + point.x.ToString("F0") + ", " + point.z.ToString("F0") + "), " + point.y.ToString("F1") + " m" +
                        (join != 0 ? ", on the end of a string (node #" + join + ")" : ""));
                return;
            }

            float length = Vector3.Distance(_last, point);
            float level = new Vector2(point.x - _last.x, point.z - _last.z).magnitude;
            if (length < StringNet.MinSpan || length > StringNet.MaxSpan || level < 0.2f)
            {
                Log.Msg("strings: no span there (" + length.ToString("0.0") + " m; spans are " + StringNet.MinSpan + " to " + StringNet.MaxSpan + " m and not upright)");
                return;
            }

            StringSpec spec = StringEditor.NewSpec();
            Vector3 from = _last;
            ushort fromNode = _lastNode;
            var points = new List<Vector3> { from, point };
            var nodes = new List<ushort> { fromNode, join };
            StringTool tool = this;
            StringNet.Create(points, nodes, spec, made =>
            {
                // On the simulation thread: the end node is the next span's start.
                if (made.Count > 0)
                {
                    ushort seg = made[made.Count - 1];
                    tool._pendingNode = Singleton<NetManager>.instance.m_segments.m_buffer[seg].m_endNode;
                    Log.Msg("strings: span #" + seg + " strung, " + length.ToString("0.0") + " m (" + spec.Describe() + ")");
                }
            });

            _spansThisChain++;
            _last = point;
            _lastNode = join;
            _pendingFrom = point;
            _awaitNode = join == 0;

            // A click on a string's end that was not where this chain began ends the chain there.
            if (join != 0 && join != fromNode)
                EndChain();
        }

        /// <summary>The node the simulation thread made for the last point: the next span starts from it.</summary>
        private volatile ushort _pendingNode;
        private Vector3 _pendingFrom;
        private bool _awaitNode;

        /// <summary>From OnToolLateUpdate (ToolBase's LateUpdate calls it; a LateUpdate here would hide that).</summary>
        private void TakePendingNode()
        {
            if (_awaitNode && _pendingNode != 0)
            {
                ushort node = _pendingNode;
                _pendingNode = 0;
                _awaitNode = false;
                if (_hasLast && (StringNet.NodePosition(node) - _pendingFrom).sqrMagnitude < 0.01f)
                    _lastNode = node;
            }
        }

        private void UpdateRaise(Event e)
        {
            if (_raiseKey == null || _lowerKey == null || ColossalFramework.UI.UIView.HasModalInput() || ColossalFramework.UI.UIView.HasInputFocus())
                return;

            bool up = Held(_raiseKey, e.control), down = Held(_lowerKey, e.control);
            float step = _elevation.Event(up, down, e.shift, e.alt, System.Diagnostics.Stopwatch.GetTimestamp(), System.Diagnostics.Stopwatch.Frequency);
            if (step != 0f && CurrentMode == Mode.Place)
                _lift = Mathf.Clamp(_lift + step, -MaxLift, MaxLift);
        }

        private static bool Held(SavedInputKey key, bool control)
        {
            KeyCode code = key.Key;
            return code != KeyCode.None && Input.GetKey(code) && key.Control == control;
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            base.RenderOverlay(cameraInfo);
            if (CurrentMode != Mode.Place)
                return;

            try
            {
                Vector3 point;
                ushort join;
                if (!TryPoint(out point, out join))
                    return;

                OverlayEffect overlay = Singleton<RenderManager>.instance.OverlayEffect;
                overlay.DrawCircle(cameraInfo, join != 0 ? JoinColour : PointColour, point, 0.8f, point.y - 30f, point.y + 2f, false, true);
                Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
                if (_hasLast)
                {
                    overlay.DrawCircle(cameraInfo, PointColour, _last, 0.6f, _last.y - 30f, _last.y + 2f, false, true);
                    Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
                }
            }
            catch (Exception e)
            {
                if (e.Message != _overlayFailure)
                {
                    _overlayFailure = e.Message;
                    Log.Warn("strings: drawing the tool's overlay threw (said once): " + e.Message);
                }
            }
        }
    }
}
