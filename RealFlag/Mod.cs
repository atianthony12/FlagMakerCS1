using System.Reflection;
using ColossalFramework;
using ColossalFramework.Plugins;
using ICities;
using RealFlag.Flags;
using RealFlag.UI;

namespace RealFlag
{
    public class Mod : IUserMod
    {
        /// <summary>
        /// The mod's name wherever a player reads it: the mods list, the options page, the panel's
        /// title, button tooltips, dialog titles, the log's first line. The player's files are
        /// FlagMaker too (FlagMaker.log, FlagMaker.xml, FlagMaker\Images), and so are the DLL, the
        /// Harmony id and the Unified UI button's name (2026-11-19). The namespace, the project's
        /// and the local mod's folder, the shaders and Move It's integration ID keep "RealFlag",
        /// its first name, and the save keys and the prefab's name always will.
        /// </summary>
        public const string DisplayName = "Flag Maker";

        /// <summary>
        /// The release a player has: raised once per Workshop upload (the third number for a
        /// fix, the second for new features). Also the first three parts of the assembly version
        /// (Properties/AssemblyInfo.cs). 0.x: not on the Workshop yet.
        /// </summary>
        public const string Version = "1.0.0";

        /// <summary>The mods list and the options page show the version after the name, so a report can say which build it was.</summary>
        public string Name => DisplayName + " " + Version;
        public string Description => Localization.Get("Mod.Description");

        /// <summary>Called by the game via reflection when the mod is enabled (at start-up, in the main menu).</summary>
        public void OnEnabled()
        {
            Log.Start();
            Log.Msg("Mod enabled. Log file: " + Log.Path);
            // Through Step: it names PluginManager's members, and one the game took away throws as
            // it is compiled, before its own try -- the game check below must still run.
            Step(WarnOfSecondCopy, "the second copy's check");

            // Before anything of the mod starts: has the game everything it patches and reads by
            // name? If not it switches itself off -- all but what keeps the cities' flags (GameUpdate).
            // Each step below caught HERE: a compiled reference the game took away throws as the
            // callee is compiled, before its own try is entered.
            Step(GameUpdate.Check, "the game check");

            // Reads FlagMaker.xml and starts the saver that writes changes back and picks up edits
            // made to the file by hand (read-only while off: GameUpdate).
            Step(Settings.Init, "the settings");
            if (!GameUpdate.Off)
                Step(SettingsXml.StartSaver, "the settings' saver"); // again after a disable/enable, when Init has nothing left to do
            else
                Step(GameUpdate.Announce, "the switched-off pop-up"); // in the player's language, read with the settings

            // After the settings, for the player's language. Never at start-up: no city is open then.
            Step(SayIfCityOpen, "the open city's check");

            // The flags' prop prefab has to be in the game's prop collection before a city's
            // props are bound to their prefabs, which is during loading: the hook is how (see
            // FlagLoadingHook). Registered now, before any city loads -- off or not: it is what
            // keeps the city's flags in it.
            Step(FlagLoadingHook.Register, "the loading hook");

            // Clicking the cloth (Move It, the bulldozer) and Move It's highlight are Harmony
            // patches; the rest of the mod does without them. None while off.
            if (!GameUpdate.Off)
            {
                Step(Patcher.EnsureHarmony, "Harmony's check");
                Step(Patcher.PatchOnReady, "the patches");
            }
        }

        private static void Step(System.Action step, string what)
        {
            try
            {
                step();
            }
            catch (System.Exception e)
            {
                Log.Error("Mod enabled: " + what + " threw; the rest goes on", e);
            }
        }

        /// <summary>
        /// Enabled with a city open: Steam installed an update of the mod while it was (the game
        /// swaps the assembly and calls the new copy's OnEnabled, IL: PluginManager.AddPlugins), or
        /// it was switched on in it. This copy never loads that city (FlagSaveData.LoadedHere): it
        /// stays off in it, its saves leave the city's flags as they were, and the player is told
        /// to load the city again. While off after a game update, GameUpdate's pop-up says enough.
        /// </summary>
        private static void SayIfCityOpen()
        {
            if (!Loader.CityOpen())
                return;

            Log.Warn("Flag Maker came in with a city open (a Workshop update, or switched on in it): it stays off in this city, " +
                     "and its saves leave the city's flags as they were. Load the city again to use them");
            if (!GameUpdate.Off)
                Loader.SayCameMidCity();
        }

        /// <summary>
        /// Two copies of the mod enabled at once (a local Addons\Mods folder beside the Workshop's)
        /// both run: both patch, both save, each keeps the designs it loaded. Nothing here can stop
        /// the other one; it is said, so the log explains what follows (each copy says it).
        /// </summary>
        /// <remarks>
        /// Counted among the ENABLED plugins, not the AppDomain's assemblies: an update Steam
        /// installs while the game runs leaves the old assembly loaded (no assembly is ever
        /// unloaded) but takes its plugin out of the list before the new copy's OnEnabled
        /// (PluginManager.AddPlugins replaces the entry in its first loop and calls OnEnabled in
        /// its second, IL), so a swap is one copy. At start-up both copies are in the list by then.
        /// </remarks>
        private static void WarnOfSecondCopy()
        {
            try
            {
                int copies = 0;
                foreach (PluginManager.PluginInfo plugin in Singleton<PluginManager>.instance.GetPluginsInfo())
                {
                    if (!plugin.isEnabled)
                        continue;

                    foreach (Assembly a in plugin.GetAssemblies())
                    {
                        if (a.GetName().Name == "FlagMaker")
                            copies++;
                    }
                }

                if (copies > 1)
                    Log.Warn(copies + " copies of Flag Maker are loaded (a local copy in Addons\\Mods beside the Workshop's?): " +
                             "both run; disable one, or a city's flags may be saved by the wrong copy");
            }
            catch (System.Exception e)
            {
                Log.Warn("could not count the mod's loaded copies: " + e.Message);
            }
        }

        /// <summary>Called by the game via reflection when the mod is disabled.</summary>
        public void OnDisabled()
        {
            // First, whatever this copy runs in an open city: replaced by Steam's update with the
            // city open, it never hears OnLevelUnloading, and its controller would go on drawing
            // and stepping the flags, with its button, into the next city. Nothing with no city
            // loaded. Each step caught inside; this catch for a throw as it is compiled.
            try
            {
                Loader.LetGo();
            }
            catch (System.Exception e)
            {
                Log.Error("Letting go of the city's flags threw.", e);
            }

            // Each step whatever the one before did: Unpatch names Harmony's types, which the JIT
            // cannot find without the Harmony mod -- only called once patched, and caught.
            try
            {
                if (Patcher.IsPatched)
                    Patcher.Unpatch();
            }
            catch (System.Exception e)
            {
                Log.Error("Removing the Harmony patches threw.", e);
            }

            FlagLoadingHook.Unregister();
            SettingsXml.Shutdown();
        }

        /// <summary>
        /// Called by the game via reflection when the options page is (re)built -- at least once
        /// per scene, and again whenever any mod is enabled or disabled. The mod itself is
        /// configured there; the flags are made and edited in the in-game panel.
        /// </summary>
        public void OnSettingsUI(UIHelperBase helper)
        {
            OptionsUI.Build(helper);
        }
    }
}
