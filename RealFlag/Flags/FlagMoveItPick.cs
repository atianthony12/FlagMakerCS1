using System;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Math;
using HarmonyLib;

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
    /// (IsPropValid), its filters (Filters.Filter).
    /// </summary>
    /// <remarks>
    /// Patched by hand (Patcher), only when Move It is there: an optional mod, its method found by
    /// name, every step checked, a failure logged and the patch left off. The per-cell method runs
    /// once for each of the one to four cells searched; the flags' boxes are tested first, and the
    /// reflection calls run only for a flag nearer than what the search found.
    /// </remarks>
    public static class FlagMoveItPick
    {
        private static bool _patched;
        private static MethodInfo _isPropValid, _filter, _isValidP;
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

                harmony.Patch(target, postfix: new HarmonyMethod(typeof(FlagMoveItPick), "Postfix"));
                _patched = true;
                Log.Msg("Move It: its pointer finds a flag anywhere along its ray (PropsManager.RaycastHoverInstance; its checks: step-over " +
                        (_isValidP != null) + ", layers " + (_isPropValid != null) + ", filters " + (_filter != null) + ")");
            }
            catch (Exception e)
            {
                Log.Error("Could not patch Move It's pointer search; flags are picked only where it reaches them.", e);
            }
        }

        public static void Postfix(object __instance, ref Segment3 ray, ref float smallestDist, ref InstanceID id, ItemClass.Layer itemLayers,
                                   object[] __args)
        {
            try
            {
                uint[] props = FlagPicking.Props;
                if (props.Length == 0)
                    return;

                PropInstance[] buffer = Singleton<PropManager>.instance.m_props.m_buffer;
                object stepOver = __args != null && __args.Length > 2 ? __args[2] : null;
                foreach (uint prop in props)
                {
                    float t, targetSqr;
                    if (!FlagPicking.RayCast(prop, ray, out t, out targetSqr) || t >= smallestDist)
                        continue;

                    if (!Valid(__instance, buffer, prop, itemLayers, stepOver))
                        continue;

                    id.Prop = (ushort)prop;
                    smallestDist = t;
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
