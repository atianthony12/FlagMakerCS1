using System;
using System.Reflection;
using CitiesHarmony.API;
using HarmonyLib;
using UnityEngine;

namespace RealFlag
{
    /// <summary>
    /// Harmony bootstrap (Volumetric Weather's). The CitiesHarmony mod provides the actual Harmony
    /// assembly at runtime, so patching is deferred until it reports ready rather than assumed.
    /// The patches are in Flags/FlagPatches.cs; without them flags are still made, drawn and saved,
    /// but Move It and the bulldozer pick a flag only by a small spot just above its anchor.
    /// </summary>
    public static class Patcher
    {
        private const string HarmonyId = "flagmaker.flags";

        private static bool _patched;

        public static bool IsPatched
        {
            get { return _patched; }
        }

        public static void PatchOnReady()
        {
            try
            {
                HarmonyHelper.DoOnHarmonyReady(Patch);
            }
            catch (Exception e)
            {
                Log.Error("Could not schedule Harmony patching.", e);
            }
        }

        /// <summary>Prompts the player to subscribe if the Harmony mod is missing.</summary>
        public static void EnsureHarmony()
        {
            try
            {
                if (!HarmonyHelper.IsHarmonyInstalled)
                {
                    Log.Warn("Harmony is NOT installed: Move It and the bulldozer can only pick a flag just above its top corner.");
                    HarmonyHelper.EnsureHarmonyInstalled();
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static void Patch()
        {
            // Switched off by the game check (GameUpdate): nothing of ours is patched.
            if (_patched || GameUpdate.Off)
                return;

            try
            {
                var harmony = new Harmony(HarmonyId);

                // Each class on its own, through the game check (GameUpdate): Harmony 2.0.4's
                // PatchAll throws out at the first target it cannot find. One that will not apply
                // is the game changed under the mod: every patch of ours is undone and the mod
                // switches itself off -- never left half-patched. The attribute patches are in
                // Flags/FlagPatches.cs; Move It's are made by hand (FlagMoveItPick), not counted
                // (another mod's members: each has its own fallback).
                Type[] patchClasses = { typeof(Flags.FlagRayCastPatch), typeof(Flags.FlagOverlayPatch) };
                ModsLibCS1.GameCheck check = GameUpdate.Current;
                int applied = 0;
                foreach (Type patch in patchClasses)
                {
                    Type captured = patch;
                    if (check != null ? check.Patch(patch.Name, () => harmony.CreateClassProcessor(captured).Patch()) : TryPatch(harmony, captured))
                        applied++;
                }

                if (check != null && check.UndoIfFailed(() => harmony.UnpatchAll(HarmonyId)))
                {
                    Log.Msg("Harmony patches removed (" + HarmonyId + "): the game check failed");
                    GameUpdate.PatchFailed();
                    return;
                }

                _patched = true;
                Log.Msg("Harmony patches applied (" + HarmonyId + ", " + applied + " of " + patchClasses.Length +
                        "): clicking a flag's cloth, Move It's outline of it");
                Flags.FlagMoveItPick.TryPatch(harmony);
                Flags.FlagProceduralObjects.TryPatch(harmony);
                LogSharedMethods();
            }
            catch (Exception e)
            {
                Log.Error("Harmony patching failed; flags are picked only just above their top corner.", e);
            }
        }

        /// <summary>One class without a game check (never: GameUpdate.Check runs first in OnEnabled).</summary>
        private static bool TryPatch(Harmony harmony, Type patch)
        {
            try
            {
                harmony.CreateClassProcessor(patch).Patch();
                return true;
            }
            catch (Exception e)
            {
                Log.Error("Harmony: " + patch.Name + " could not be applied; the rest go on without it.", e);
                return false;
            }
        }

        /// <summary>
        /// Move It's pointer search and Move It's and Procedural Objects' Convert to PO, once a city
        /// is loaded, if they could not be patched when the mod was enabled (their assemblies not
        /// loaded yet).
        /// </summary>
        public static void EnsureOtherMods()
        {
            if (!_patched || GameUpdate.Off)
                return;

            if (!Flags.FlagMoveItPick.IsPatched)
            {
                try
                {
                    Flags.FlagMoveItPick.TryPatch(new Harmony(HarmonyId));
                }
                catch (Exception e)
                {
                    Log.Warn("Could not patch Move It's pointer search: " + e.Message);
                }
            }

            try
            {
                Flags.FlagProceduralObjects.TryPatch(new Harmony(HarmonyId));
            }
            catch (Exception e)
            {
                Log.Warn("Could not patch Convert to PO: " + e.Message);
            }

            // Again with the city: a mod that patches as a city loads was not there to be seen at start.
            LogSharedMethods();
        }

        /// <summary>
        /// One line naming every other Harmony ID that patches a method we patch: a clash there is
        /// the likeliest way another mod and ours break each other.
        /// </summary>
        private static void LogSharedMethods()
        {
            try
            {
                string shared = "";
                foreach (MethodBase method in new Harmony(HarmonyId).GetPatchedMethods())
                {
                    Patches info = Harmony.GetPatchInfo(method);
                    if (info == null)
                        continue;

                    string others = "";
                    foreach (string owner in info.Owners)
                    {
                        if (owner != HarmonyId)
                            others += (others.Length > 0 ? ", " : "") + owner;
                    }

                    if (others.Length > 0)
                        shared += (shared.Length > 0 ? "; " : "") + method.DeclaringType.Name + "." + method.Name + " by " + others;
                }

                Log.Msg("harmony: other mods patching the methods we patch: " + (shared.Length > 0 ? shared : "none"));
            }
            catch (Exception e)
            {
                Log.Warn("Could not list other mods' patches: " + e.Message);
            }
        }

        public static void Unpatch()
        {
            if (!_patched)
                return;

            try
            {
                new Harmony(HarmonyId).UnpatchAll(HarmonyId);
                Log.Msg("Harmony patches removed.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            // UnpatchAll took Move It's and PO's too: patched again if the mod is enabled again.
            Flags.FlagMoveItPick.Forget();
            Flags.FlagProceduralObjects.Forget();
            _patched = false;
        }
    }
}
