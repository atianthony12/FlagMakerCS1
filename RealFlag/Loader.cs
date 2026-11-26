using System;
using System.Collections.Generic;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using ICities;
using RealFlag.Flags;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealFlag
{
    /// <summary>
    /// Discovered automatically by the game alongside <see cref="Mod"/>. Owns the controller
    /// GameObject for the lifetime of a loaded city.
    /// </summary>
    public class Loader : LoadingExtensionBase
    {
        private static GameObject _root;
        private static bool _unsupportedShown, _emlShown;

        public override void OnLevelLoaded(LoadMode mode)
        {
            base.OnLevelLoaded(mode);

            if (mode != LoadMode.NewGame && mode != LoadMode.LoadGame && mode != LoadMode.NewGameFromScenario)
                return;

            if (_root != null)
                return;

            // This copy came in while the city was loading, after the copy before it had read the
            // city's flags (FlagSaveData.LoadedHere: every load runs OnLoadData before this): it
            // has none of them, so it stands down as for a game update -- nothing drawn or placed,
            // and its saves leave the city's designs and heights as they are.
            if (!FlagSaveData.LoadedHere)
            {
                Log.Warn("flags: this copy of Flag Maker did not load the city (updated or switched on while it loaded): " +
                         "it stays off in this city; its flags stay in it and in its saves as they were");
                SayCameMidCity();
                return;
            }

            // Made during loading by FlagLoadingHook; made (and bound) here only if the mod was
            // switched on after the city had loaded.
            FlagPrefab.EnsureCreated(true);

            // Switched off after a game update (GameUpdate): the prefab keeps the city's flags and
            // FlagSaveData their designs and heights; nothing else runs -- no controller, panel,
            // button or tool. The pop-up said it at the main menu.
            if (GameUpdate.Off)
            {
                Log.Warn("flags: Flag Maker is off after a game update: this city's flags stay in it, hidden, and in its saves " +
                         "(designs and heights written back as they were loaded)" +
                         (FlagSaveData.Keeping ? "" : " -- it has none"));
                return;
            }

            Patcher.EnsureOtherMods();

            // Before anything is drawn or placed: the city's flags must be ones this copy of the
            // mod can keep. If not, it leaves them alone and their designs are saved as they came.
            string message = CannotKeep();
            if (message != null)
            {
                if (message.Length > 0)
                    Show(message);
                return;
            }

            // Flags of a city saved before their heights were kept loaded at 0, under the map: stood
            // on the ground before anything reads where they are (FlagSaveData).
            try
            {
                FlagSaveData.StandUnplaced();
            }
            catch (Exception e)
            {
                // Not worth the city's controller: the flags stay where they loaded.
                Log.Error("flags: standing the flags with no saved height on the ground threw; they stay where they loaded", e);
            }

            if (!CanDraw())
            {
                StandDown();
                return;
            }

            // The player's pictures come from their folder (before any panel asks for one).
            FlagImages.UserSource = FlagImageFolder.Bytes;
            FlagImages.Warn = Log.Warn;
            FlagImages.Note = Log.Msg;

            _root = new GameObject("RealFlagController");
            Object.DontDestroyOnLoad(_root);
            _root.AddComponent<ModController>();

            Debug.Log("[FlagMaker]Controller created.");
        }

        public override void OnLevelUnloading()
        {
            base.OnLevelUnloading();
            LetGo();
        }

        /// <summary>
        /// Everything this copy runs in a city, let go: the city unloading, or the mod disabled or
        /// replaced (Mod.OnDisabled -- Steam's update of the mod swaps the assembly with the city
        /// open, and the old copy never hears OnLevelUnloading: its controller would go on
        /// drawing, stepping and showing its button into the next city). With no city loaded there
        /// is nothing to let go and nothing throws. The flag prefab stays in the game's collection
        /// and the city's props stay bound to it (Forget drops only the reference).
        /// </summary>
        internal static void LetGo()
        {
            // This city's flags go with it; the next city's come with its save. Each step on its
            // own, and the controller destroyed whatever threw: one throw here would otherwise
            // leave _root set, so the next city never got a controller (the mod dead until a
            // restart) while this city's kept running in it (the 2026-11-07 sweep).
            try
            {
                // First: with the city open (Mod.OnDisabled), a save on the simulation thread must
                // not find the designs gone below and erase them from the save (FlagSaveData).
                Step(FlagSaveData.Forget, "the flags' kept data");
                Step(FlagTool.Remove, "the flag tool");
                Step(LineFlags.Forget, "Line Tool");
                Step(FlagEditor.Clear, "the selection");
                Step(FlagUndo.Clear, "the undo steps");
                Step(FlagRegistry.Clear, "the flags' designs");
                Step(FlagPrefab.Forget, "the flag prefab");
                Step(GameCamera.Forget, "the game's camera");
            }
            finally
            {
                if (_root != null)
                {
                    Object.Destroy(_root);
                    _root = null;
                }
            }
        }

        private static void Step(Action step, string what)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                Log.Error("unloading: letting go of " + what + " threw; the rest goes on", e);
            }
        }

        /// <summary>
        /// The flags are drawn by the cloth shader from the bundle built for this platform
        /// (ShaderBundle). Where the game runs on anything else, or the card cannot run shader
        /// model 3.5 / OpenGL 3.3, it is unsupported and ShaderBundle returns null.
        /// </summary>
        private static bool CanDraw()
        {
            return ShaderBundle.Get(ShaderBundle.Cloth) != null;
        }

        /// <summary>
        /// Why this copy of the mod must leave the city's flags alone -- the message for the player
        /// -- or null. Then it stands down in the city (no controller) and every save of it writes
        /// the flags' designs back exactly as they were loaded (FlagSaveData.Keep): what this copy
        /// cannot read or cannot see is never its to throw away.
        /// </summary>
        private static string CannotKeep()
        {
            // Designs it cannot read: a newer version's format, or damaged (FlagSaveData kept them).
            if (FlagSaveData.Keeping)
                return Localization.Get("Mod.KeptUnreadable", Log.Path);

            // Extended Managers Library keeps every prop in a buffer of its own (EPropManager, 32-bit
            // ids): the flags would be out of sight of this mod, which reads the game's, and a save
            // would drop every design as the flags of a city that has none.
            if (EmlEnabled())
            {
                Log.Warn("Extended Managers Library is enabled: Flag Maker stands down in this city (its props are out of Flag Maker's sight)");
                FlagSaveData.Keep("Extended Managers Library is enabled");
                if (_emlShown)
                    return string.Empty; // said once a session: the log says it for every city
                _emlShown = true;
                return Localization.Get("Mod.KeptEml");
            }

            // Designs with no flag left: the game let the flags' props go as the city loaded (it
            // releases a prop whose prefab it could not bind -- PropManager.UpdateData, its IL). A
            // save would make that loss for good; the player is told not to.
            var designs = new List<KeyValuePair<uint, FlagSpec>>();
            FlagRegistry.CopyTo(designs);
            int alive = 0;
            foreach (KeyValuePair<uint, FlagSpec> d in designs)
            {
                if (FlagProps.IsFlagProp(d.Key))
                    alive++;
            }

            // Unless the prefab was there in time for the save's props (FlagPrefab.InTime): then
            // every flag in the save came back a flag, and designs with none were saved without
            // their flags -- the city saved while the mod was off (the game let them go then).
            // Nothing is left to keep for them; kept, the mod stayed off in that city for good,
            // telling the player at every load not to save (the 2026-11-07 sweep; the author's
            // call: "Let orphans go"). Invariant 18 holds for a load that went wrong.
            if (designs.Count > 0 && alive == 0 && FlagPrefab.InTime)
            {
                Log.Warn("flags: the city was saved with " + designs.Count + " flag design(s) and none of their flags: it was saved " +
                         "while Flag Maker was off, and the game let those flags go then. The designs are let go; Flag Maker runs here");
                FlagRegistry.Clear();
                return null;
            }

            if (designs.Count > 0 && alive == 0)
            {
                Log.Error("flags: the city was saved with " + designs.Count + " flag(s), and none of their props is a flag now (the prefab " +
                          (FlagPrefab.Info != null ? "is index " + FlagPrefab.Info.m_prefabDataIndex : "is MISSING") +
                          "): they were let go while the city loaded -- do not save it");
                FlagSaveData.Keep("its " + designs.Count + " flag(s) were let go while the city loaded");
                return Localization.Get("Mod.KeptLost", designs.Count, Log.Path);
            }

            return null;
        }

        /// <summary>
        /// Extended Managers Library is enabled: found as its own API finds it (PropAPI.Initialize,
        /// its IL -- the same test Find It, BOB and Picker run): an enabled plugin with an assembly
        /// named "EManagersLib".
        /// </summary>
        internal static bool EmlEnabled()
        {
            try
            {
                foreach (PluginManager.PluginInfo plugin in Singleton<PluginManager>.instance.GetPluginsInfo())
                {
                    if (!plugin.isEnabled)
                        continue;

                    foreach (Assembly assembly in plugin.GetAssemblies())
                    {
                        if (string.Equals(assembly.GetName().Name, "EManagersLib", StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Warn("Could not look for Extended Managers Library: " + e.Message);
            }

            return false;
        }

        /// <summary>No controller means no panel, no button and no drawing in this city (the flags' props and designs are kept).</summary>
        private static void StandDown()
        {
            Log.Msg("NOT SUPPORTED here: the cloth shader cannot run (" + SystemInfo.operatingSystem +
                    ", graphics " + SystemInfo.graphicsDeviceType + ", GPU '" + SystemInfo.graphicsDeviceName +
                    "'); flags are not drawn in this city, but they stay in it and in its saves");

            if (_unsupportedShown)
                return;

            _unsupportedShown = true;

            RuntimePlatform platform = Application.platform;
            Show(platform == RuntimePlatform.WindowsPlayer || platform == RuntimePlatform.WindowsEditor
                     ? Localization.Get("Mod.UnsupportedWindows", Log.Path)
                     : platform == RuntimePlatform.LinuxPlayer || platform == RuntimePlatform.LinuxEditor
                         ? Localization.Get("Mod.UnsupportedLinux", Log.Path)
                         : Localization.Get("Mod.Unsupported"));
        }

        /// <summary>
        /// A city is open in the game (not an editor): the load has finished and not been undone.
        /// LoadingManager.m_loadingComplete is set true by LoadLevelComplete and false by LoadLevel,
        /// LoadIntro (the main menu) and UnloadLevel (IL); ToolController.m_mode is what
        /// LoadingWrapper.currentMode reads (IL), null-checked here, as it is not there, for the
        /// main menu. Compiled references to public fields: nothing for GameUpdate.Require.
        /// </summary>
        internal static bool CityOpen()
        {
            // .exists first: .instance would make the manager at start-up.
            if (!Singleton<LoadingManager>.exists || !Singleton<LoadingManager>.instance.m_loadingComplete)
                return false;

            ToolController properties = Singleton<ToolManager>.exists ? Singleton<ToolManager>.instance.m_properties : null;
            return properties != null && properties.m_mode == ItemClass.Availability.Game;
        }

        private static bool _midCityShown;

        /// <summary>
        /// The player told, once: this copy came in with the city open (Mod.OnEnabled) or loading
        /// (OnLevelLoaded), and stays off in it. Through the game's ThreadingWrapper.QueueMainThread,
        /// which runs it at once on the main thread and queues it there from any other (IL): Steam's
        /// "item installed" comes from the platform's native callbacks, whose thread the IL cannot show.
        /// </summary>
        internal static void SayCameMidCity()
        {
            if (_midCityShown)
                return;

            ThreadingWrapper threading = Singleton<SimulationManager>.exists ? Singleton<SimulationManager>.instance.m_ThreadingWrapper : null;
            if (threading != null)
            {
                // Marked only once it is on its way: without the wrapper, a later call may still show it.
                _midCityShown = true;
                threading.QueueMainThread(() => Show(Localization.Get("Mod.CameMidCity")));
            }
            else
                Log.Warn("Could not show the message that Flag Maker came in with the city open (no threading wrapper)");
        }

        /// <summary>The game's own message box, with the mod's name (main thread only).</summary>
        internal static void Show(string message)
        {
            try
            {
                ExceptionPanel panel = UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel");
                panel.SetMessage(Mod.DisplayName, message, false);
            }
            catch (Exception e)
            {
                Log.Error("Could not show the message: " + message, e);
            }
        }
    }
}
