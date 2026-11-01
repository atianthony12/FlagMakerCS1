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

            // Made during loading by FlagLoadingHook; made (and bound) here only if the mod was
            // switched on after the city had loaded.
            FlagPrefab.EnsureCreated(true);
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
            FlagSaveData.StandUnplaced();

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

            Debug.Log("[RealFlag] Controller created.");
        }

        public override void OnLevelUnloading()
        {
            base.OnLevelUnloading();

            // This city's flags go with it; the next city's come with its save.
            FlagTool.Remove();
            FlagEditor.Clear();
            FlagRegistry.Clear();
            FlagSaveData.Forget();
            FlagPrefab.Forget();

            if (_root == null)
                return;

            Object.Destroy(_root);
            _root = null;
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
                Log.Warn("Extended Managers Library is enabled: RealFlag stands down in this city (its props are out of RealFlag's sight)");
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
