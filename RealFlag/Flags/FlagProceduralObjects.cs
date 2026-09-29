using System;
using System.Reflection;
using ColossalFramework.Threading;
using HarmonyLib;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// A flag never becomes a Procedural Object. Procedural Objects makes its object from a prop's
    /// PREFAB (ProceduralObject.ConstructObject: its mesh and material), and a flag's prefab is only
    /// the flat 3 x 2 m rest mesh under the cloth shader with none of the flag's design: the author
    /// saw "a flag rectangle with ... a placeholder flag" (2026-09-29) -- and Move It deletes the
    /// flag it converted. So every way in is refused, the flag left as it is, and the player told:
    /// - PO's own button and key: ProceduralObjectsLogic.CallConvertToPO(tool) with the current
    ///   tool -- the game's PropTool holding the flag's prefab (Find It, Picker) -- a prefix skips it;
    /// - Move It's "Convert to PO": ConvertToPOAction.Do asks PO_Manager.ConvertToPO(instance) for
    ///   each selected object, and a null answer leaves that object where it is, still selected,
    ///   not deleted -- a prefix answers null for a flag, the rest convert;
    /// - PO's own door for Move It: PO_MoveIt.RequestConversion(request), queued and converted in
    ///   PO's Update unless request.failed -- marked failed (Move It 2.10.9 does not call it).
    /// Read from their IL: Procedural Objects (1094334744) and Move It 2.10.9, 2026-09-29.
    /// </summary>
    /// <remarks>
    /// Patched by hand (Patcher), each part only when its mod is there, found by name with its
    /// parameters checked, and left off (logged) if anything differs: neither mod is referenced
    /// (Invariant 11). Move It's part is in Move It's own assembly and patched with or without PO.
    /// Checked on Unity 5.6's Mono with the game's Harmony 2.0.4 (the scratchpad's miniatures of
    /// the three methods): a flag refused, the others through, `ref object __result` null for
    /// Move It's PO_Object. No __args (Invariant 15).
    /// </remarks>
    public static class FlagProceduralObjects
    {
        private static bool _moveItPatched, _poPatched;
        private static PropertyInfo _instanceId;
        private static FieldInfo _requestProp, _requestFailed;
        private static int _shownFrame = -1;

        /// <summary>Harmony's patches were all removed (Patcher.Unpatch): patch again when the mod is enabled again.</summary>
        public static void Forget()
        {
            _moveItPatched = false;
            _poPatched = false;
        }

        /// <summary>Patches whichever of Move It's and PO's ways in is loaded and not patched yet.</summary>
        public static void TryPatch(Harmony harmony)
        {
            if (!_moveItPatched)
                _moveItPatched = PatchMoveIt(harmony);
            if (!_poPatched)
                _poPatched = PatchProceduralObjects(harmony);
        }

        /// <summary>Move It's "Convert to PO", per selected object: MoveIt.PO_Manager.ConvertToPO(Instance instance).</summary>
        private static bool PatchMoveIt(Harmony harmony)
        {
            try
            {
                Type manager = AccessTools.TypeByName("MoveIt.PO_Manager");
                Type instance = AccessTools.TypeByName("MoveIt.Instance");
                if (manager == null || instance == null)
                    return false; // no Move It: FlagMoveItPick says so

                MethodInfo convert = AccessTools.Method(manager, "ConvertToPO", new[] { instance });
                _instanceId = AccessTools.Property(instance, "id");
                ParameterInfo[] p = convert != null ? convert.GetParameters() : null;
                if (p == null || p.Length != 1 || p[0].Name != "instance" || convert.ReturnType.IsValueType ||
                    _instanceId == null || _instanceId.PropertyType != typeof(InstanceID))
                {
                    Log.Warn("Move It's PO_Manager.ConvertToPO is not the one this was written for: its Convert to PO may turn a flag into a Procedural Object.");
                    return false;
                }

                harmony.Patch(convert, prefix: new HarmonyMethod(typeof(FlagProceduralObjects), "ConvertPrefix"));
                Log.Msg("Move It: its Convert to PO leaves flags as they are (PO_Manager.ConvertToPO)");
                return true;
            }
            catch (Exception e)
            {
                Log.Error("Could not patch Move It's Convert to PO; it may turn a flag into a Procedural Object.", e);
                return false;
            }
        }

        /// <summary>PO's own button and key, and its door for Move It.</summary>
        private static bool PatchProceduralObjects(Harmony harmony)
        {
            try
            {
                Type logic = AccessTools.TypeByName("ProceduralObjects.ProceduralObjectsLogic");
                if (logic == null)
                {
                    Log.Msg("Procedural Objects is not loaded");
                    return false;
                }

                MethodInfo call = AccessTools.Method(logic, "CallConvertToPO", new[] { typeof(ToolBase) });
                ParameterInfo[] p = call != null ? call.GetParameters() : null;
                if (p == null || p[0].Name != "tool")
                {
                    Log.Warn("Procedural Objects' CallConvertToPO is not the one this was written for: it may turn a flag into a Procedural Object.");
                    return false;
                }

                harmony.Patch(call, prefix: new HarmonyMethod(typeof(FlagProceduralObjects), "ToolPrefix"));

                // Its door for Move It, if it has the one read from its IL.
                Type moveIt = AccessTools.TypeByName("ProceduralObjects.Classes.PO_MoveIt");
                Type request = AccessTools.TypeByName("ProceduralObjects.Classes.POConversionRequest");
                MethodInfo ask = moveIt != null && request != null ? AccessTools.Method(moveIt, "RequestConversion", new[] { request }) : null;
                _requestProp = request != null ? AccessTools.Field(request, "propInfo") : null;
                _requestFailed = request != null ? AccessTools.Field(request, "failed") : null;
                p = ask != null ? ask.GetParameters() : null;
                bool door = p != null && p[0].Name == "request" && _requestProp != null && _requestProp.FieldType == typeof(PropInfo) &&
                            _requestFailed != null && _requestFailed.FieldType == typeof(bool);
                if (door)
                    harmony.Patch(ask, prefix: new HarmonyMethod(typeof(FlagProceduralObjects), "RequestPrefix"));

                Log.Msg("Procedural Objects: its Convert to PO leaves flags as they are (CallConvertToPO" +
                        (door ? ", PO_MoveIt.RequestConversion" : "; its PO_MoveIt.RequestConversion not found") + ")");
                return true;
            }
            catch (Exception e)
            {
                Log.Error("Could not patch Procedural Objects' Convert to PO; it may turn a flag into a Procedural Object.", e);
                return false;
            }
        }

        /// <summary>Move It asks for one selected object: null for a flag (Move It keeps it, selected, and converts the rest).</summary>
        public static bool ConvertPrefix(object instance, ref object __result)
        {
            try
            {
                InstanceID id = instance != null ? (InstanceID)_instanceId.GetValue(instance, null) : InstanceID.Empty;
                if (id.Prop == 0 || !FlagProps.IsFlagProp(id.Prop))
                    return true;

                __result = null;
                Refuse("flag #" + id.Prop, "Move It's Convert to PO");
                return false;
            }
            catch (Exception e)
            {
                Log.Error("Move It's Convert to PO: testing for a flag threw.", e);
                return true;
            }
        }

        /// <summary>PO's button or key with the game's prop tool out: skipped when its prop is the flag.</summary>
        public static bool ToolPrefix(ToolBase tool)
        {
            var props = tool as PropTool;
            if (props == null || !FlagPrefab.IsFlag(props.m_prefab))
                return true;

            Refuse("the flag in the prop tool", "Procedural Objects' Convert to PO");
            return false;
        }

        /// <summary>A request to PO for a flag: failed, never queued (PO's Update skips a failed one; TryRetrieveConverted answers 2).</summary>
        public static bool RequestPrefix(object request)
        {
            try
            {
                if (request == null || !FlagPrefab.IsFlag(_requestProp.GetValue(request) as PropInfo))
                    return true;

                _requestFailed.SetValue(request, true);
                Refuse("a flag", "a request to Procedural Objects (PO_MoveIt.RequestConversion)");
                return false;
            }
            catch (Exception e)
            {
                Log.Error("Procedural Objects' conversion request: testing for a flag threw.", e);
                return true;
            }
        }

        /// <summary>Logged, and the player told once a frame (Move It converts a whole selection in one) -- on the main thread only.</summary>
        private static void Refuse(string what, string way)
        {
            Log.Msg("flags: " + what + " not turned into a Procedural Object (" + way + "): its cloth is RealFlag's to draw");

            if (!ReferenceEquals(Dispatcher.currentSafe, Dispatcher.mainSafe) || Time.frameCount == _shownFrame)
                return;

            _shownFrame = Time.frameCount;
            Loader.Show(Localization.Get("Mod.NoProcedural"));
        }
    }
}
