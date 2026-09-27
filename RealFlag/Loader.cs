using ColossalFramework.UI;
using ICities;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag
{
    /// <summary>
    /// Discovered automatically by the game alongside <see cref="Mod"/>. Owns the controller
    /// GameObject for the lifetime of a loaded city.
    /// </summary>
    public class Loader : LoadingExtensionBase
    {
        private static GameObject _root;
        private static bool _unsupportedShown;

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
            Patcher.EnsureMoveIt();

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

        /// <summary>No controller means no panel, no button and no drawing in this city (the flags' props and designs are kept).</summary>
        private static void StandDown()
        {
            Log.Msg("NOT SUPPORTED here: the cloth shader cannot run (" + SystemInfo.operatingSystem +
                    ", graphics " + SystemInfo.graphicsDeviceType + ", GPU '" + SystemInfo.graphicsDeviceName +
                    "'); flags are not drawn in this city, but they stay in it and in its saves");

            if (_unsupportedShown)
                return;

            _unsupportedShown = true;

            try
            {
                string message = Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor
                    ? Localization.Get("Mod.UnsupportedWindows", Log.Path)
                    : Localization.Get("Mod.Unsupported");

                ExceptionPanel panel = UIView.library.ShowModal<ExceptionPanel>("ExceptionPanel");
                panel.SetMessage(Mod.DisplayName, message, false);
            }
            catch (System.Exception e)
            {
                Log.Error("Could not show the 'not supported' message.", e);
            }
        }
    }
}
