using System;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.Math;
using ModsLibCS1;
using UnityEngine;

namespace RealFlag
{
    /// <summary>
    /// The game-update safeguard (the mods' folder's GAME-UPDATE-SAFEGUARD-PLAN.md; 2026-11-24, the
    /// author: "if a game update comes in, our mod checks the available methods and turns itself
    /// off if necessary rather than ruin the player's game"), on ModsLibCS1's GameCheck: at
    /// Mod.OnEnabled, before anything of the mod starts, every game member it patches or reads by
    /// name is looked for; Patcher applies each patch class through the same check.
    /// </summary>
    /// <remarks>
    /// OFF IS A KEEPER here, not nothing (the author: "we need to find a way to keep the flag props
    /// in their save because if they disable our mod and save, their flags will be gone"): the
    /// game lets a prop go whose prefab it cannot bind (PropManager.UpdateData, its IL), so the
    /// loading hook and the prefab stay -- the city's flags load as props, hidden (the prefab has
    /// no renderer) -- and FlagSaveData writes both keys back as they were loaded (Keep,
    /// Invariant 18's path). Nothing else runs: no patch, no controller, button, panel or tool,
    /// settings never written, the options page one line. A fixed version finds every flag, its
    /// design and its height as they were. Only if the keeper's own pieces broke (the prefab, the
    /// loading hook) would flags be lost: nothing of the mod could keep them then.
    /// </remarks>
    public static class GameUpdate
    {
        private static GameCheck _check;

        /// <summary>The mod has switched itself off (the game changed under it): only the keeper runs.</summary>
        public static bool Off { get; private set; }

        /// <summary>The check, for Patcher (null before <see cref="Check"/>).</summary>
        public static GameCheck Current
        {
            get { return _check; }
        }

        /// <summary>
        /// Mod.OnEnabled, first: the members the mod reads by name and the two Harmony targets,
        /// looked for now, whatever Harmony does later. A throw (a type gone: the JIT of
        /// <see cref="Require"/> fails) is a failure too.
        /// </summary>
        public static void Check()
        {
            if (_check != null)
                return;

            _check = new GameCheck(Mod.DisplayName, line => Log.Error(line));
            try
            {
                Require(_check);
            }
            catch (Exception e)
            {
                _check.Problem("the check itself threw: " + e.GetType().Name + ": " + e.Message);
            }

            if (_check.Failed)
                SwitchOff();
            else
                Log.Msg("game check: the game (" + GameCheck.GameVersion + ") has everything Flag Maker patches and reads by name");
        }

        /// <summary>
        /// What Flag Maker needs of the game by name. The plan's rule: a Harmony target (its
        /// signature, and the parameter names the prefix binds by) and every game member read by
        /// name. Other mods' members (Move It, Procedural Objects, Line Tool, Volumetric Weather)
        /// are not counted: each has its own fallback. Public for the offline test, which runs it
        /// against the game's DLLs.
        /// </summary>
        public static void Require(GameCheck check)
        {
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            Type byRefFloat = typeof(float).MakeByRefType();

            // Flags/FlagPatches.cs: FlagRayCastPatch's prefix takes propID, ray, t, targetSqr.
            check.RequireMethod(typeof(PropInstance), "RayCast", instance,
                                new[] { typeof(ushort), typeof(Segment3), byRefFloat, byRefFloat },
                                "propID", "ray", "t", "targetSqr");

            // FlagOverlayPatch's: cameraInfo, info, position, scale, angle, color.
            check.RequireMethod(typeof(PropTool), "RenderOverlay", statics,
                                new[] { typeof(RenderManager.CameraInfo), typeof(PropInfo), typeof(Vector3), typeof(float), typeof(float), typeof(Color) },
                                "cameraInfo", "info", "position", "scale", "angle", "color");

            // By name, as the code reads them: FlagLoadingHook.Unregister, FlagPrefab's prop title,
            // LineFlags' pointer.
            check.RequireField(typeof(RenderManager), "m_renderables", statics, typeof(FastList<IRenderableManager>));
            check.RequireField(typeof(LocaleManager), "m_Locale", instance, typeof(Locale));
            check.RequireField(typeof(DefaultTool), "m_accuratePosition", instance, typeof(Vector3));

            check.ForcedFailure();
        }

        /// <summary>
        /// A patch class that would not apply (Patcher, when Harmony is ready): the check has failed
        /// late, the patches are undone, and the mod switches off as at start-up.
        /// </summary>
        public static void PatchFailed()
        {
            if (Off)
                return;

            SwitchOff();
            Announce();
        }

        private static void SwitchOff()
        {
            Off = true;
            _check.Summary(Mod.Version);
            Log.Error("Flag Maker is off until it is updated: nothing of it runs but what keeps the cities' flags " +
                      "(their prop, their saved designs and heights, written back as they were loaded)");

            // Nothing written over the player's settings: they wait for the fixed version.
            SettingsXml.ReadOnly = true;
        }

        /// <summary>
        /// The pop-up, once a launch (shown at the main menu). Mod.OnEnabled calls it after
        /// Settings.Init, so the text is in the player's chosen language.
        /// </summary>
        public static void Announce()
        {
            if (!Off || _check == null)
                return;

            string title = Localization.Get("GameUpdate.Title", Mod.DisplayName);
            string text = Localization.Get("GameUpdate.Text", GameCheck.GameVersion, Log.Path);
            _check.ShowOnce(title, text);
        }
    }
}
