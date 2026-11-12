using ColossalFramework;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// Crowds placed as Loop (2026-11-12; the author: when a player places many Waving flags close
    /// together, the mod switches the new flag's Motion to Loop and says so -- players "are lazy
    /// and won't bother changing the motion"). Three ways in, each before the new flag's spec is
    /// taken: our tool's click (<see cref="CrowdBeforePlacing"/>), a flag prop with no design
    /// (Line Tool's rows, Find It, the prop tool: <see cref="Add"/>), and Line Tool's row while it
    /// is previewed (<see cref="AskLine"/>), so no big row comes out half and half. Only NEW flags
    /// switch: no flag's spec is ever changed. The player choosing Waving again after a switch
    /// keeps it until placing ends. Main thread only.
    /// </summary>
    public sealed partial class FlagRenderer
    {
        /// <summary>The new flag's Motion went to Loop by itself while this placing lasts (the New flag window's note).</summary>
        private static bool _crowdSwitched;

        /// <summary>... and the player chose Waving again since: no switch until placing ends.</summary>
        private static bool _crowdHeld;

        /// <summary>The one-off message is due (shown from LateUpdate, never inside the prop scan).</summary>
        private static bool _crowdTell;

        /// <summary>The switch may happen: the option on, the new flag waving, not held.</summary>
        private static bool CrowdArmed
        {
            get
            {
                return Settings.IsInitialised && Settings.CrowdLoops.value && !_crowdHeld &&
                       Settings.NewMotion.value == FlagMotion.Waves;
            }
        }

        /// <summary>The New flag window's note under Motion: shown while a switch holds.</summary>
        public static bool CrowdNoteShown()
        {
            return _crowdSwitched && !_crowdHeld && Settings.IsInitialised && Settings.NewMotion.value == FlagMotion.Loop;
        }

        /// <summary>The New flag window's Motion row changed (its AfterChange): Waving after a switch is the player's say.</summary>
        public static void CrowdMotionChosen(int motion)
        {
            if (!_crowdSwitched || _crowdHeld || motion != FlagMotion.Waves)
                return;

            _crowdHeld = true;
            Log.Msg("flags: Waving chosen again after a crowd went to Loop -- new flags wave until placing ends");
        }

        /// <summary>Our tool's click, before the new flag's spec is taken: the waving flags round where it goes.</summary>
        public static void CrowdBeforePlacing(Vector3 at)
        {
            FlagRenderer renderer = Instance;
            if (renderer == null || !CrowdArmed)
                return;

            int near = renderer.WavingNear(at.x, at.z);
            if (ClothPlan.Crowded(near))
                CrowdSwitch(near + " waving flags within " + ClothPlan.CrowdRadius.ToString("F0") + " m of the flag placed by hand");
        }

        /// <summary>A flag prop with no design, before it is given the new flag's: the waving flags round it.</summary>
        private void CrowdBeforeAdding(uint prop)
        {
            // Never while a loaded city's flags are first found (Scan's first pass): a prop that
            // came with the city without its design is not a flag the player is placing, and
            // must not change the new flag's Motion or bring up the message at load.
            if (!_scannedAll || !CrowdArmed)
                return;

            Vector3 at = Singleton<PropManager>.instance.m_props.m_buffer[prop].Position;
            int near = WavingNear(at.x, at.z);
            if (ClothPlan.Crowded(near))
                CrowdSwitch(near + " waving flags within " + ClothPlan.CrowdRadius.ToString("F0") + " m of flag prop #" + prop + ", placed with no design");
        }

        /// <summary>Line Tool's row as it is previewed: one too long for waving flags goes to Loop before it is placed.</summary>
        private static void CrowdRow(int points)
        {
            if (CrowdArmed && ClothPlan.RowCrowded(points))
                CrowdSwitch("a Line Tool row of " + points + " flags");
        }

        /// <summary>
        /// The known waving flags within <see cref="ClothPlan.CrowdRadius"/> on the map, by their
        /// props' places in the buffer: a flag added this frame has no anchor yet, and a row added
        /// in one scan counts its own flags.
        /// </summary>
        private int WavingNear(float x, float z)
        {
            PropInstance[] buffer = Singleton<PropManager>.instance.m_props.m_buffer;
            int near = 0;
            foreach (FlagInstance f in _flags)
            {
                if (f.Spec.Motion != FlagMotion.Waves || f.Prop >= buffer.Length)
                    continue;

                Vector3 p = buffer[f.Prop].Position;
                if (ClothPlan.InCrowd(p.x - x, p.z - z))
                    near++;
            }

            return near;
        }

        private static void CrowdSwitch(string why)
        {
            Settings.NewMotion.value = FlagMotion.Loop;
            _crowdSwitched = true;
            Log.Msg("flags: " + why + " -- new flags play a loop from here (Motion set to Loop; \"Place crowds as Loop\" in Settings)");

            if (!Settings.CrowdLoopsTold.value)
                _crowdTell = true;

            UI.SettingsCatalog.RefreshAllUIs();
        }

        /// <summary>Each frame: placing over, the rule armed again; the one-off message, once.</summary>
        private static void CrowdTick()
        {
            if (!FlagTool.IsPlacing && !LineFlags.IsOut && (_crowdSwitched || _crowdHeld))
            {
                _crowdSwitched = false;
                _crowdHeld = false;
            }

            if (!_crowdTell)
                return;

            _crowdTell = false;
            if (!Settings.IsInitialised || Settings.CrowdLoopsTold.value)
                return;

            Settings.CrowdLoopsTold.value = true;
            Loader.Show(Localization.Get("Mod.CrowdLoop"));
        }
    }
}
