using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Plugins;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>A point of a row Line Tool is about to place, on the ground: where, and its prop's angle.</summary>
    public struct LinePoint
    {
        public Vector3 Position;
        public float Angle;
    }

    /// <summary>
    /// Rows of flags by the Line Tool mod (algernon's, Workshop 2956939040), when it is enabled (the
    /// author: "Let's add the buttons in the New flag popup ... only show it of course if the user
    /// has Line Tool"). The New flag window's Place row hands the game's current tool to Line Tool
    /// with the flag prefab selected and Line Tool's spacing, rotation and mode set from our rows.
    /// Its own panels are not used: they open only for the game's prop, tree and building tools.
    /// </summary>
    /// <remarks>
    /// Read by reflection, nothing referenced (Invariant 11), as WeatherModWind reads Volumetric
    /// Weather: without Line Tool, or if anything there differs, the rows stay hidden and flags are
    /// placed one at a time. Line Tool 0.7.1, read from its IL (2026-11-06):
    /// - LineToolMod.LineTool (public, a DefaultTool on the ToolController's GameObject once a city
    ///   has loaded) has public SelectedPrefab, CurrentMode (LineToolMod.Modes.*), Spacing, Rotation,
    ///   RelativeRotation, FenceMode, StepMode, Flip90, Flip180. Those are what this drives.
    /// - Its ToolController.SetTool prefix clears SelectedPrefab when the tool before it is not the
    ///   game's, so the prefab is set AFTER the tool.
    /// - It places by PropManager.CreateProp: FlagRenderer.Add gives each flag the new flag's design
    ///   and stands it on the ground, as for any tool that is not ours.
    /// - It draws a prop through the game, and ours has no renderer: the preview is FlagRenderer's,
    ///   from its private _propPoints (locked, as the simulation thread rewrites it) and the
    ///   protected DefaultTool.m_accuratePosition. Those are the one fragile part: gone, a row is
    ///   placed all the same, with only Line Tool's guide line to show for it.
    /// - Its points lie on the terrain (TerrainManager.SampleDetailHeight): never a roof or a bridge.
    ///   Page Up and Page Down still lift the whole row (FlagTool.RaiseOverLine).
    /// Whatever spacing and so on the player had in Line Tool goes back when the flags let it go.
    /// Main thread.
    /// </remarks>
    public static class LineFlags
    {
        public const int Single = 0, Line = 1, Curve = 2, Freeform = 3, Circle = 4;

        public static readonly int[] Values = { Single, Line, Curve, Freeform, Circle };

        public static readonly string[] NameKeys = { "Placing.Single", "Placing.Line", "Placing.Curve", "Placing.Freeform", "Placing.Circle" };

        /// <summary>Lucide icons, Resources\PanelIcons.lucide.txt, as the atlas names them.</summary>
        public static readonly string[] Icons = { "icon-flag", "icon-slash", "icon-spline", "icon-route", "icon-circle" };

        private static readonly string[] ModeTypes = { null, "LineMode", "CurveMode", "FreeformMode", "CircleMode" };

        private const string AssemblyName = "LineTool";
        private const string ToolTypeName = "LineToolMod.LineTool";
        private const string ModesNamespace = "LineToolMod.Modes.";
        private const float MinSpacing = 1f;

        /// <summary>The preview draws no more than this many flags of a row; a row may be longer.</summary>
        private const int MaxPreviewPoints = 256;

        private static bool _looked, _bound, _failed, _previewFailed;
        private static Type _toolType;
        private static Type[] _modes;
        private static PropertyInfo _selected, _mode, _spacing, _rotation, _along;
        private static PropertyInfo[] _kept;
        private static FieldInfo _points, _pointPosition, _pointRotation, _pointColliding, _pointer;

        private static ToolBase _tool;
        private static bool _driving;
        private static object[] _was;
        private static float _pushedSpacing, _pushedRotation;
        private static bool _pushedAlong;

        /// <summary>True while the tool goes over to Line Tool: FlagTool keeps its lift.</summary>
        public static bool Handing { get; private set; }

        /// <summary>Line Tool is enabled, working in this city, and as this mod knows it: the rows are shown.</summary>
        public static bool IsAvailable()
        {
            return Bind() && Tool() != null;
        }

        /// <summary>Line Tool is the current tool with the flag prefab, however it got it (Find It too).</summary>
        public static bool IsOut
        {
            get
            {
                if (!Bind())
                    return false;

                ToolBase tool = Tool();
                ToolController controller = ToolsModifierControl.toolController;
                if (tool == null || controller == null || controller.CurrentTool != tool)
                    return false;

                try
                {
                    return FlagPrefab.IsFlag(_selected.GetValue(tool, null) as PropInfo);
                }
                catch (Exception e)
                {
                    Fail("reading its prefab failed: " + e.Message);
                    return false;
                }
            }
        }

        /// <summary>Line Tool is out because the New flag window sent it: the window stays open.</summary>
        public static bool Driving
        {
            get { return _driving && IsOut; }
        }

        /// <summary>The Place row's choice: a row in that shape, or back to one flag at a time.</summary>
        public static void Choose(int mode)
        {
            ToolController controller = ToolsModifierControl.toolController;
            if (mode == Single)
            {
                if (Driving)
                    FlagTool.Open(FlagTool.Mode.Place);
                return;
            }

            ToolBase tool = Bind() ? Tool() : null;
            if (tool == null || controller == null || FlagPrefab.Info == null)
            {
                BackToSingle();
                return;
            }

            try
            {
                if (!_driving)
                    Remember(tool);

                Handing = true;
                controller.CurrentTool = tool;
                Handing = false;

                foreach (PropertyInfo p in _kept)
                {
                    if (p.PropertyType == typeof(bool))
                        p.SetValue(tool, false, null);
                }

                _selected.SetValue(tool, FlagPrefab.Info, null);
                _mode.SetValue(tool, Activator.CreateInstance(_modes[mode]), null);
                _driving = true;
                Push(tool, true);
                Log.Msg("flags: placing rows with Line Tool: " + ModeTypes[mode] + ", every " +
                        Settings.NewLineSpacing.value.ToString("0.#") + " m");
            }
            catch (Exception e)
            {
                Handing = false;
                _driving = false;
                Restore(tool);
                Fail("handing it the flag failed: " + e.Message);
                BackToSingle();
            }
        }

        /// <summary>Puts Line Tool away, if the window sent it. Says what it did, for the log.</summary>
        public static string PutAway()
        {
            if (Driving)
                ToolsModifierControl.SetTool<DefaultTool>();
            return "the rows put away";
        }

        /// <summary>Every frame: while the window drives Line Tool, our rows reach it and the raise keys work; once it has gone, what was there goes back.</summary>
        public static void Tick()
        {
            if (!_driving)
                return;

            ToolBase tool = Tool();
            ToolController controller = ToolsModifierControl.toolController;
            if (tool == null || controller == null || controller.CurrentTool != tool)
            {
                _driving = false;
                Restore(tool);
                if (!FlagTool.IsPlacing)
                    FlagTool.ForgetLift();
                return;
            }

            try
            {
                Push(tool, false);
            }
            catch (Exception e)
            {
                // A setter of Line Tool's that throws would throw here every frame, inside the
                // renderer's frame, leaving every flag undrawn while Line Tool is out: the rows
                // given up instead, said once (Fail), Line Tool's own settings put back.
                _driving = false;
                Restore(tool);
                Fail("our rows could not reach it: " + e.Message);
                BackToSingle();
                return;
            }

            if (FlagTool.Instance != null)
                FlagTool.Instance.RaiseOverLine();
        }

        /// <summary>
        /// The row as Line Tool has it now, its points on the ground that are not blocked, and where
        /// the pointer is. False when there is nothing to read (no preview).
        /// </summary>
        public static bool ReadPoints(List<LinePoint> into, out Vector3 pointer)
        {
            into.Clear();
            pointer = Vector3.zero;
            ToolBase tool = Tool();
            if (tool == null || _points == null || _previewFailed)
                return false;

            try
            {
                pointer = (Vector3)_pointer.GetValue(tool);
                var list = _points.GetValue(tool) as IList;
                if (list == null)
                    return false;

                lock (list)
                {
                    int count = Math.Min(list.Count, MaxPreviewPoints);
                    for (int i = 0; i < count; i++)
                    {
                        object point = list[i];
                        if ((bool)_pointColliding.GetValue(point))
                            continue;

                        into.Add(new LinePoint
                        {
                            Position = (Vector3)_pointPosition.GetValue(point),
                            Angle = (float)_pointRotation.GetValue(point),
                        });
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                _previewFailed = true;
                Log.Warn("flags: Line Tool's points could not be read: no preview of a row, it is placed all the same (" + e.Message + ")");
                return false;
            }
        }

        /// <summary>The city is unloading: its tool goes with it, and Line Tool is looked for again in the next.</summary>
        public static void Forget()
        {
            // Line Tool's own settings back first: the city can go with our rows still out, before
            // a Tick has seen Line Tool put away (it kept the flag window's spacing for the session).
            if (_was != null)
                Restore(_tool);

            _driving = false;
            _tool = null;
            _was = null;
            _looked = false;
            _bound = false;
            _failed = false;
            _previewFailed = false;
            Handing = false;
        }

        /// <summary>
        /// What of Line Tool this mod uses and <paramref name="assembly"/> lacks, joined; empty if
        /// nothing is missing. Public for the offline check against the real DLL (tools\test-linetool.ps1).
        /// </summary>
        public static string Missing(Assembly assembly)
        {
            var missing = new List<string>();
            Type tool = assembly.GetType(ToolTypeName, false);
            if (tool == null)
                return ToolTypeName;

            foreach (string name in new[] { "SelectedPrefab", "CurrentMode", "Spacing", "Rotation", "RelativeRotation", "FenceMode", "StepMode", "Flip90", "Flip180" })
            {
                PropertyInfo p = tool.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (p == null || !p.CanRead || !p.CanWrite)
                    missing.Add(name);
            }

            PropertyInfo mode = tool.GetProperty("CurrentMode", BindingFlags.Public | BindingFlags.Instance);
            for (int i = 1; i < ModeTypes.Length; i++)
            {
                Type type = assembly.GetType(ModesNamespace + ModeTypes[i], false);
                if (type == null || mode == null || !mode.PropertyType.IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) == null)
                    missing.Add(ModeTypes[i]);
            }

            if (!typeof(ToolBase).IsAssignableFrom(tool))
                missing.Add("a ToolBase");

            return string.Join(", ", missing.ToArray());
        }

        /// <summary>What the preview reads and <paramref name="assembly"/> lacks, joined; empty if nothing is missing.</summary>
        public static string MissingForPreview(Assembly assembly)
        {
            Type tool = assembly.GetType(ToolTypeName, false);
            if (tool == null)
                return ToolTypeName;

            string found = BindPreview(tool);
            return found ?? string.Empty;
        }

        private static ToolBase Tool()
        {
            if (_tool == null && _toolType != null)
            {
                ToolController controller = ToolsModifierControl.toolController;
                _tool = controller != null ? controller.GetComponent(_toolType) as ToolBase : null;
            }

            return _tool;
        }

        private static bool Bind()
        {
            if (_looked)
                return _bound && !_failed;

            // Once a city: the mods enabled cannot change inside one.
            _looked = true;
            Assembly assembly = Find();
            if (assembly == null)
                return false;

            try
            {
                string missing = Missing(assembly);
                if (missing.Length > 0)
                {
                    Fail("this Line Tool lacks " + missing);
                    return false;
                }

                _toolType = assembly.GetType(ToolTypeName, false);
                _selected = Property("SelectedPrefab");
                _mode = Property("CurrentMode");
                _spacing = Property("Spacing");
                _rotation = Property("Rotation");
                _along = Property("RelativeRotation");
                _kept = new[] { _spacing, _rotation, _along, Property("FenceMode"), Property("StepMode"), Property("Flip90"), Property("Flip180") };
                _modes = new Type[ModeTypes.Length];
                for (int i = 1; i < ModeTypes.Length; i++)
                    _modes[i] = assembly.GetType(ModesNamespace + ModeTypes[i], false);

                string preview = BindPreview(_toolType);
                if (preview != null)
                {
                    _points = null;
                    Log.Warn("flags: this Line Tool lacks " + preview + ": no preview of a row, it is placed all the same");
                }

                _bound = true;
                Log.Msg("flags: Line Tool is on: the New flag window places rows of flags with it");
                return true;
            }
            catch (Exception e)
            {
                Fail("binding failed: " + e.Message);
                return false;
            }
        }

        private static PropertyInfo Property(string name)
        {
            return _toolType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        }

        /// <summary>The preview's fields; null, or what was not found.</summary>
        private static string BindPreview(Type tool)
        {
            _pointer = typeof(DefaultTool).GetField("m_accuratePosition", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            _points = tool.GetField("_propPoints", BindingFlags.NonPublic | BindingFlags.Instance);
            Type point = tool.GetNestedType("PointData", BindingFlags.Public | BindingFlags.NonPublic);
            if (point == null)
                return "PointData";

            _pointPosition = point.GetField("Position");
            _pointRotation = point.GetField("Rotation");
            _pointColliding = point.GetField("Colliding");
            if (_pointer == null)
                return "m_accuratePosition";
            if (_points == null)
                return "_propPoints";
            if (_pointPosition == null || _pointPosition.FieldType != typeof(Vector3) ||
                _pointRotation == null || _pointRotation.FieldType != typeof(float) ||
                _pointColliding == null || _pointColliding.FieldType != typeof(bool))
                return "PointData's fields";

            return null;
        }

        private static Assembly Find()
        {
            try
            {
                if (!Singleton<PluginManager>.exists)
                    return null;

                foreach (PluginManager.PluginInfo plugin in Singleton<PluginManager>.instance.GetPluginsInfo())
                {
                    if (!plugin.isEnabled)
                        continue;

                    foreach (Assembly assembly in plugin.GetAssemblies())
                    {
                        if (string.Equals(assembly.GetName().Name, AssemblyName, StringComparison.OrdinalIgnoreCase))
                            return assembly;
                    }
                }
            }
            catch (Exception e)
            {
                Fail("looking for it failed: " + e.Message);
            }

            return null;
        }

        private static void Remember(ToolBase tool)
        {
            _was = new object[_kept.Length];
            for (int i = 0; i < _kept.Length; i++)
                _was[i] = _kept[i].GetValue(tool, null);
        }

        private static void Restore(ToolBase tool)
        {
            object[] was = _was;
            _was = null;
            if (tool == null || was == null)
                return;

            try
            {
                for (int i = 0; i < _kept.Length; i++)
                    _kept[i].SetValue(tool, was[i], null);
            }
            catch (Exception e)
            {
                Log.Warn("flags: Line Tool's own settings could not be put back: " + e.Message);
            }
        }

        /// <summary>Our rows into Line Tool, when they changed (the Rotation row's too, however it was set).</summary>
        private static void Push(ToolBase tool, bool always)
        {
            float spacing = Mathf.Max(Settings.NewLineSpacing.value, MinSpacing);
            float rotation = Settings.NewRotation.value * Mathf.Deg2Rad;
            bool along = Settings.NewLineAlong.value;
            if (!always && spacing == _pushedSpacing && rotation == _pushedRotation && along == _pushedAlong)
                return;

            _pushedSpacing = spacing;
            _pushedRotation = rotation;
            _pushedAlong = along;
            _spacing.SetValue(tool, spacing, null);
            _rotation.SetValue(tool, rotation, null);
            _along.SetValue(tool, along, null);
        }

        private static void BackToSingle()
        {
            Settings.NewPlacing.value = Single;
            FlagTool.Open(FlagTool.Mode.Place);
        }

        private static void Fail(string why)
        {
            if (_failed)
                return;

            _failed = true;
            Log.Warn("flags: Line Tool is not used (" + why + "); flags are placed one at a time");
        }
    }
}
