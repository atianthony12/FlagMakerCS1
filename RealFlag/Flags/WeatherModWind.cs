using System;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Plugins;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// Volumetric Weather's wind, when it is enabled: the city's one wind (2026-10-30; the author:
    /// "the central place to control wind is Volumetric Weather"). Its VolumetricClouds.Sky.WeatherWind
    /// keeps how strong it blows (from its weather -- cloud, rain, a storm -- or its Now tab's
    /// override), which way (its Now tab's wind direction, eased) and the gusts rolling across the map
    /// that bend its trees; the flags then fly in the same wind. Read by reflection, nothing referenced:
    /// without Volumetric Weather (or an older one), or if anything there differs, the flags keep the
    /// game's wind (GameWind) as before. Main thread.
    /// </summary>
    public static class WeatherModWind
    {
        private const string AssemblyName = "VolumetricClouds";
        private const string TypeName = "VolumetricClouds.Sky.WeatherWind";

        private static bool _bound;
        private static bool _failed;
        private static float _nextLook;
        private static Func<bool> _ready;
        private static Func<bool> _overridden;
        private static Func<Vector3> _direction;
        private static Func<float> _speed;
        private static Func<float, float, float> _speedAt;

        /// <summary>Volumetric Weather's wind now: where it blows to (level, unit) and its mean at 10 m, m/s. False without it.</summary>
        public static bool TryRead(out Vector3 direction, out float speed, out bool overridden)
        {
            direction = Vector3.right;
            speed = 0f;
            overridden = false;
            if (!Bind())
                return false;

            try
            {
                if (!_ready())
                    return false;
                direction = _direction();
                speed = Mathf.Max(0f, _speed());
                overridden = _overridden();
                return !float.IsNaN(speed) && direction.sqrMagnitude > 0.5f;
            }
            catch (Exception e)
            {
                Fail("reading it failed: " + e.Message);
                return false;
            }
        }

        /// <summary>The wind at 10 m at (x, z) with Volumetric Weather's passing gust in it, m/s; <paramref name="mean"/> without it.</summary>
        public static float SpeedAt(float x, float z, float mean)
        {
            if (!_bound || _failed)
                return mean;

            try
            {
                float s = _speedAt(x, z);
                return float.IsNaN(s) ? mean : Mathf.Max(0f, s);
            }
            catch (Exception e)
            {
                Fail("its gusts failed: " + e.Message);
                return mean;
            }
        }

        private static bool Bind()
        {
            if (_bound)
                return !_failed;
            if (_failed)
                return false;

            // Not every frame: the plugin list is looked through every 10 s until it is found.
            if (Time.realtimeSinceStartup < _nextLook)
                return false;
            _nextLook = Time.realtimeSinceStartup + 10f;
            Type type = Find();
            if (type == null)
                return false;

            try
            {
                FieldInfo version = type.GetField("ApiVersion", BindingFlags.Public | BindingFlags.Static);
                int v = version != null ? (int)version.GetValue(null) : 0;
                if (v < 1)
                {
                    Fail("its WeatherWind has no ApiVersion 1 (" + v + ")");
                    return false;
                }

                _ready = Getter<bool>(type, "Ready");
                _overridden = Getter<bool>(type, "Overridden");
                _direction = Getter<Vector3>(type, "Direction");
                _speed = Getter<float>(type, "Speed");
                MethodInfo at = type.GetMethod("SpeedAt", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(float), typeof(float) }, null);
                if (at == null || at.ReturnType != typeof(float))
                {
                    Fail("its WeatherWind.SpeedAt(float, float) is missing");
                    return false;
                }

                _speedAt = (Func<float, float, float>)Delegate.CreateDelegate(typeof(Func<float, float, float>), at);
                _bound = true;
                Log.Msg("wind: Volumetric Weather is on -- the flags fly in its wind (its weather or its Now tab's, its direction and its gusts), " +
                        "times the game's wind map where each flag stands");
                return true;
            }
            catch (Exception e)
            {
                Fail("binding failed: " + e.Message);
                return false;
            }
        }

        private static Func<T> Getter<T>(Type type, string name)
        {
            PropertyInfo p = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            if (p == null || p.PropertyType != typeof(T) || p.GetGetMethod() == null)
                throw new MissingMemberException(TypeName, name);
            return (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), p.GetGetMethod());
        }

        /// <summary>The type in an enabled plugin's assembly named VolumetricClouds, or null.</summary>
        private static Type Find()
        {
            try
            {
                if (!Singleton<PluginManager>.exists)
                    return null;
                foreach (PluginManager.PluginInfo plugin in Singleton<PluginManager>.instance.GetPluginsInfo())
                {
                    if (!plugin.isEnabled)
                        continue;
                    foreach (Assembly assembly in plugin.GetAssemblies())
                    {
                        if (string.Equals(assembly.GetName().Name, AssemblyName, StringComparison.OrdinalIgnoreCase))
                            return assembly.GetType(TypeName, false);
                    }
                }
            }
            catch (Exception e)
            {
                Fail("looking for it failed: " + e.Message);
            }

            return null;
        }

        private static void Fail(string why)
        {
            if (_failed)
                return;
            _failed = true;
            Log.Warn("wind: Volumetric Weather's wind is not used (" + why + "); the flags keep the game's wind");
        }
    }
}
