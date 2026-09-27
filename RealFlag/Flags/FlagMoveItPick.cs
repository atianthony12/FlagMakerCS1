using System;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Math;
using HarmonyLib;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// Move It's pointer finds a flag anywhere along its ray. Move It does not ask the game which
    /// prop the pointer is on: it finds where the ray meets the ground (RaycastMouseLocation) and
    /// tests the objects of the map cells (64 m) within 16 m of that point (MoveItTool
    /// .RaycastHoverInstance; PropsManager.RaycastHoverInstance per cell -- Move It's IL,
    /// 2026-09-26). A flag stands high: the ground behind it under the pointer can be tens of
    /// metres from its own cell, and then Move It never asked it -- "MoveIt!'s selector is still
    /// too small" even after the flag's own pick volumes grew. So a postfix on the per-cell search
    /// also tests every flag's pick shape (FlagPicking) against the whole ray, with Move It's own
    /// checks on whatever it would add: its step-over (isValidP), its layers and flags
    /// (IsPropValid), its filters (Filters.Filter). It finds a flag in its wider pick volumes
    /// (PickShape.IntersectWide: the cloth's held box, its hoist and its pole with generous
    /// margins). The column of the flag's whole reach was tried (the author: "The MoveIt!
    /// selector is still too small, it needs to be much bigger") and was "too big now". Neither
    /// size was ever the trouble: this postfix's own __args undid every find (see the remarks).
    /// Move It searches even when its ray meets no ground (RaycastMouseLocation hands back the hit
    /// position regardless, IL): a flag seen against the sky is found too. A click on a flag is
    /// logged, with whether Move It selected it (<see cref="ClickPostfix"/>).
    /// </summary>
    /// <remarks>
    /// Patched by hand (Patcher), only when Move It is there: an optional mod, its method found by
    /// name, every step checked, a failure logged and the patch left off. The per-cell method runs
    /// once for each of the one to four cells searched; the flags' boxes are tested first, and the
    /// reflection calls run only for a flag nearer than what the search found.
    /// <para>
    /// The postfix must NEVER take <c>object[] __args</c>. The game's Harmony (2.0.4, through
    /// CitiesHarmony) fills that array as the method is entered and, after a postfix that asks for
    /// it, writes it back into every ref argument (MethodPatcher.AddPostfixes calls
    /// RestoreArgumentArray, its IL): the id and distance this postfix set, AND what Move It's own
    /// search had found in that cell, were put back to what they were on entry. From 0.1.0.28676
    /// to 0.1.0.26324 Move It could not hover or click any prop while this mod was on (the author:
    /// "I can't click on the actual flag itself. I have to use the marquee tool to select it");
    /// the log showed a flag handed over 0.01 s before a click that found nothing. On Unity 5.6's
    /// Mono with that Harmony: a search finding id 42, a postfix setting 7 -- with __args id 0,
    /// without it 7. The step-over is read from MoveItTool.stepOver, the static the search is
    /// handed by reference (its IL: ldsflda).
    /// </para>
    /// </remarks>
    public static class FlagMoveItPick
    {
        private static bool _patched;
        private static MethodInfo _isPropValid, _filter, _isValidP;
        private static FieldInfo _stepOver;
        private static bool _loggedFailure;

        public static bool IsPatched
        {
            get { return _patched; }
        }

        /// <summary>Patches Move It's per-cell prop search, if Move It is loaded and its method is the one read from its IL.</summary>
        public static void TryPatch(Harmony harmony)
        {
            if (_patched)
                return;

            try
            {
                Type manager = AccessTools.TypeByName("MoveIt.PropsManager");
                if (manager == null)
                {
                    Log.Msg("Move It is not loaded: flags are picked by their shape where Move It's search reaches them");
                    return;
                }

                MethodInfo target = AccessTools.Method(manager, "RaycastHoverInstance");
                ParameterInfo[] p = target != null ? target.GetParameters() : null;
                if (p == null || p.Length != 7 || p[3].Name != "ray" || p[3].ParameterType != typeof(Segment3).MakeByRefType() ||
                    p[4].Name != "smallestDist" || p[5].Name != "id" || p[5].ParameterType != typeof(InstanceID).MakeByRefType() ||
                    p[6].Name != "itemLayers" || p[6].ParameterType != typeof(ItemClass.Layer))
                {
                    Log.Warn("Move It's PropsManager.RaycastHoverInstance is not the one this was written for; flags are picked only where Move It's own search reaches them.");
                    return;
                }

                _isPropValid = AccessTools.Method(manager, "IsPropValid");
                Type filters = AccessTools.TypeByName("MoveIt.Filters");
                _filter = filters != null ? AccessTools.Method(filters, "Filter", new[] { typeof(PropInfo) }) : null;
                Type stepOver = AccessTools.TypeByName("MoveIt.StepOver");
                _isValidP = stepOver != null ? AccessTools.Method(stepOver, "isValidP", new[] { typeof(uint) }) : null;

                // The step-over the search is handed (by reference) is this static of the tool's.
                Type tool = AccessTools.TypeByName("MoveIt.MoveItTool");
                _stepOver = tool != null ? AccessTools.Field(tool, "stepOver") : null;
                if (_stepOver != null && (!_stepOver.IsStatic || _stepOver.FieldType != stepOver))
                    _stepOver = null;
                if (_stepOver == null)
                    _isValidP = null;

                harmony.Patch(target, postfix: new HarmonyMethod(typeof(FlagMoveItPick), "Postfix"));
                _patched = true;
                Log.Msg("Move It: its pointer finds a flag anywhere along its ray (PropsManager.RaycastHoverInstance; its checks: step-over " +
                        (_isValidP != null) + ", layers " + (_isPropValid != null) + ", filters " + (_filter != null) + ")");

                PatchClick(harmony);
            }
            catch (Exception e)
            {
                Log.Error("Could not patch Move It's pointer search; flags are picked only where it reaches them.", e);
            }
        }

        // ---- a click on a flag, for the log -----------------------------------------------------

        private static FieldInfo _hoverField, _selectionField;
        private static PropertyInfo _instanceId;
        private static MethodInfo _selectionContains;
        private static PropertyInfo _selectionCount;

        /// <summary>The flag Move It's search was last handed by the postfix, and when (real time): for the click's log line.</summary>
        private static uint _offered;
        private static float _offeredAt = -1f;
        private static bool _loggedClickFailure;

        /// <summary>
        /// Whether a click on a flag selects it, in the log: a postfix on Move It's click
        /// (MoveItTool.OnLeftClick: selects its m_hoverInstance -- its IL, 2026-09-27), reading the
        /// hovered instance and Move It's selection (Action.selection) by reflection. Diagnostics
        /// only: left off, logged, when anything differs.
        /// </summary>
        private static void PatchClick(Harmony harmony)
        {
            try
            {
                Type tool = AccessTools.TypeByName("MoveIt.MoveItTool");
                Type instance = AccessTools.TypeByName("MoveIt.Instance");
                Type action = AccessTools.TypeByName("MoveIt.Action");
                MethodInfo click = tool != null ? AccessTools.Method(tool, "OnLeftClick", Type.EmptyTypes) : null;
                _hoverField = tool != null ? AccessTools.Field(tool, "m_hoverInstance") : null;
                _instanceId = instance != null ? AccessTools.Property(instance, "id") : null;
                _selectionField = action != null ? AccessTools.Field(action, "selection") : null;
                Type selectionType = _selectionField != null ? _selectionField.FieldType : null;
                _selectionContains = selectionType != null && instance != null ? selectionType.GetMethod("Contains", new[] { instance }) : null;
                _selectionCount = selectionType != null ? selectionType.GetProperty("Count") : null;

                if (click == null || _hoverField == null || _hoverField.FieldType != instance || _instanceId == null ||
                    _instanceId.PropertyType != typeof(InstanceID) || _selectionField == null || !_selectionField.IsStatic ||
                    _selectionContains == null || _selectionCount == null)
                {
                    Log.Msg("Move It: its click is not the one read from its IL; clicks on flags are not logged");
                    return;
                }

                harmony.Patch(click, postfix: new HarmonyMethod(typeof(FlagMoveItPick), "ClickPostfix"));
                Log.Msg("Move It: a click on a flag is logged, with whether Move It selected it (MoveItTool.OnLeftClick)");
            }
            catch (Exception e)
            {
                Log.Warn("Move It: could not watch its clicks (" + e.Message + "); clicks on flags are not logged");
            }
        }

        /// <summary>After Move It's click: what it had under the pointer, whether that is now selected, and whether a flag was offered to its search just before.</summary>
        public static void ClickPostfix(object __instance)
        {
            try
            {
                object hover = _hoverField.GetValue(__instance);
                InstanceID id = hover != null ? (InstanceID)_instanceId.GetValue(hover, null) : InstanceID.Empty;
                bool flag = id.Prop != 0 && FlagPicking.Get(id.Prop) != null;
                float since = Time.realtimeSinceStartup - _offeredAt;
                bool offered = _offeredAt >= 0f && since < 1f;
                if (!flag && !offered)
                    return;

                object selection = _selectionField.GetValue(null);
                bool selected = hover != null && (bool)_selectionContains.Invoke(selection, new[] { hover });
                int count = (int)_selectionCount.GetValue(selection, null);
                string what = flag ? "flag #" + id.Prop : hover == null ? "nothing" : id.Type + " " + id.Index;
                Log.Msg("Move It: a click on " + what + " -- selected: " + selected + " (" + count + " in its selection)" +
                        (!flag && offered ? "; flag #" + _offered + " was under the pointer " + since.ToString("F2") + " s before" : ""));
            }
            catch (Exception e)
            {
                if (!_loggedClickFailure)
                {
                    _loggedClickFailure = true;
                    Log.Warn("Move It: reading its click threw (logged once): " + e.Message);
                }
            }
        }

        /// <summary>After Move It's search of one map cell: every flag nearer along the ray than what it found. No __args (see the remarks).</summary>
        public static void Postfix(object __instance, ref Segment3 ray, ref float smallestDist, ref InstanceID id, ItemClass.Layer itemLayers)
        {
            try
            {
                uint[] props = FlagPicking.Props;
                if (props.Length == 0)
                    return;

                PropInstance[] buffer = Singleton<PropManager>.instance.m_props.m_buffer;
                object stepOver = _stepOver != null ? _stepOver.GetValue(null) : null;
                foreach (uint prop in props)
                {
                    // Anywhere in the wider pick volumes (PickShape.IntersectWide).
                    float t;
                    if (!FlagPicking.RayCastWide(prop, ray, out t) || t >= smallestDist)
                        continue;

                    if (!Valid(__instance, buffer, prop, itemLayers, stepOver))
                        continue;

                    id.Prop = (ushort)prop;
                    smallestDist = t;
                    _offered = prop;
                    _offeredAt = Time.realtimeSinceStartup;
                }
            }
            catch (Exception e)
            {
                if (!_loggedFailure)
                {
                    _loggedFailure = true;
                    Log.Error("Move It's pointer search: testing the flags threw (logged once).", e);
                }
            }
        }

        /// <summary>What Move It asks of a prop before it hovers it.</summary>
        private static bool Valid(object manager, PropInstance[] buffer, uint prop, ItemClass.Layer layers, object stepOver)
        {
            if (prop >= buffer.Length)
                return false;

            var flags = (PropInstance.Flags)buffer[prop].m_flags;
            if ((flags & (PropInstance.Flags.Created | PropInstance.Flags.Deleted)) != PropInstance.Flags.Created)
                return false;

            if (_isValidP != null && stepOver != null && !(bool)_isValidP.Invoke(stepOver, new object[] { prop }))
                return false;

            if (_isPropValid != null && !(bool)_isPropValid.Invoke(manager, new object[] { buffer[prop], layers }))
                return false;

            if (_filter != null && !(bool)_filter.Invoke(null, new object[] { buffer[prop].Info }))
                return false;

            return true;
        }
    }
}
