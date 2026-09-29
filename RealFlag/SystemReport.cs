using System;
using UnityEngine;

namespace RealFlag
{
    /// <summary>
    /// The always-on lines about the machine and the settings, written when a city loads: a
    /// report must say what the screen, the card and the wind were.
    /// </summary>
    public static class SystemReport
    {
        private static bool _written;

        public static void Write()
        {
            if (_written)
                return;

            _written = true;

            try
            {
                Log.Msg("system: " + Screen.width + "x" + Screen.height +
                        (Screen.fullScreen ? " fullscreen" : " windowed") +
                        " | RAM " + SystemInfo.systemMemorySize + " MB" +
                        " | GPU '" + SystemInfo.graphicsDeviceName + "' " + SystemInfo.graphicsMemorySize + " MB, " +
                        SystemInfo.graphicsDeviceVersion +
                        " | Unity " + Application.unityVersion +
                        ", colour space " + QualitySettings.activeColorSpace +
                        " | Harmony patches " + (Patcher.IsPatched ? "on" : "OFF (flags are picked only just above their top corner)"));

                bool gameWind = Settings.WindFromGame == null || Settings.WindFromGame.value;
                Log.Msg("features: wind=" + (gameWind ? "the game's" : "own") +
                        (gameWind ? "" : " " + Value(Settings.WindSpeed, Settings.Defaults.WindSpeed).ToString("F1") + "m/s at 10 m" +
                                         " gusts=" + Percent(Settings.WindGusts, Settings.Defaults.WindGusts) +
                                         " direction=" + Settings.WindModes.LogName(Settings.WindMode != null ? Settings.WindMode.value : Settings.Defaults.WindMode) +
                                         (Settings.WindMode != null && Settings.WindMode.value == Settings.WindModes.Fixed
                                             ? " " + Value(Settings.WindDirection, 0f).ToString("F0") + "deg" : "")) +
                        " freeze when paused=" + (Settings.FreezeWhenPaused == null || Settings.FreezeWhenPaused.value ? "on" : "off") +
                        " | next flag: " + Settings.NewSpec().Describe() + ", " +
                        Value(Settings.NewRotation, Settings.Defaults.Rotation).ToString("F0") + " deg");
            }
            catch (Exception e)
            {
                Log.Warn("Could not write the system report: " + e.Message);
            }
        }

        private static float Value(FloatSetting setting, float fallback)
        {
            return setting != null ? setting.value : fallback;
        }

        private static string Percent(FloatSetting setting, float fallback)
        {
            return (Value(setting, fallback) * 100f).ToString("F0") + "%";
        }
    }
}
