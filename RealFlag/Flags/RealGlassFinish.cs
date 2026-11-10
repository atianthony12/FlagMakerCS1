using System;
using System.Reflection;
using ColossalFramework;
using ColossalFramework.Plugins;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// Real Glass's Reflective finish over a glossy pole, when Real Glass is enabled (2026-11-09; the
    /// author: "You can coordinate with RealGlass on that. We recently added a Reflectivity material
    /// for use on props and decals, maybe it can fit here"). Its RealGlass.Api.Reflective (ApiVersion
    /// 1) draws, over a mesh another mod draws, the city's reflection where the screen holds it, the
    /// ground, the sky and the sun's glint, and dims what lies under it by 1 - F. Read by reflection,
    /// nothing referenced (Invariant 11): without Real Glass, an older one, or anything there that
    /// differs, the poles keep their own sky reflection (FlagPole.shader, PoleSky). Main thread.
    /// </summary>
    public static class RealGlassFinish
    {
        private const string AssemblyName = "RealGlass";
        private const string TypeName = "RealGlass.Api.Reflective";

        /// <summary>Its Draw(mesh, matrix, layer, camera, colour, reflectivity, polish, glint): true when it drew.</summary>
        private delegate bool DrawFn(Mesh mesh, Matrix4x4 matrix, int layer, Camera camera, Color colour, float reflectivity, float polish, float glint);

        private static bool _bound;
        private static bool _failed;
        private static float _nextLook;
        private static Func<bool> _available;
        private static DrawFn _draw;

        /// <summary>Real Glass would draw a finish this frame (bound, and its glass on).</summary>
        public static bool Available
        {
            get
            {
                if (!Bind())
                    return false;
                try
                {
                    return _available();
                }
                catch (Exception e)
                {
                    Fail("asking it failed: " + e.Message);
                    return false;
                }
            }
        }

        /// <summary>The finish over <paramref name="mesh"/> this frame; true when Real Glass drew it (leave out our own reflection there).</summary>
        public static bool Draw(Mesh mesh, Matrix4x4 matrix, int layer, Color colour, float reflectivity, float polish, float glint)
        {
            if (!_bound || _failed || mesh == null)
                return false;
            try
            {
                return _draw(mesh, matrix, layer, null, colour, reflectivity, polish, glint);
            }
            catch (Exception e)
            {
                Fail("drawing failed: " + e.Message);
                return false;
            }
        }

        private static bool Bind()
        {
            if (_bound)
                return !_failed;
            if (_failed)
                return false;

            // The plugin list is looked through every 10 s until it is found, not every frame.
            if (Time.realtimeSinceStartup < _nextLook)
                return false;
            _nextLook = Time.realtimeSinceStartup + 10f;
            Type type = Find();
            if (type == null)
                return false;

            try
            {
                FieldInfo version = type.GetField("ApiVersion", BindingFlags.Public | BindingFlags.Static);
                int v = version != null && version.FieldType == typeof(int) ? (int)version.GetValue(null) : 0;
                if (v < 1)
                {
                    Fail("its Api.Reflective has no ApiVersion 1 (" + v + ")");
                    return false;
                }

                PropertyInfo available = type.GetProperty("Available", BindingFlags.Public | BindingFlags.Static);
                if (available == null || available.PropertyType != typeof(bool) || available.GetGetMethod() == null)
                {
                    Fail("its Api.Reflective.Available is missing");
                    return false;
                }

                MethodInfo draw = type.GetMethod("Draw", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(Mesh), typeof(Matrix4x4), typeof(int), typeof(Camera), typeof(Color), typeof(float), typeof(float), typeof(float) }, null);
                if (draw == null || draw.ReturnType != typeof(bool))
                {
                    Fail("its Api.Reflective.Draw(Mesh, Matrix4x4, int, Camera, Color, float, float, float) is missing");
                    return false;
                }

                _available = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), available.GetGetMethod());
                _draw = (DrawFn)Delegate.CreateDelegate(typeof(DrawFn), draw);
                _bound = true;
                Log.Msg("poles: Real Glass is on -- a glossy pole wears its Reflective finish (the city, the ground, the sky and the sun in it)");
                return true;
            }
            catch (Exception e)
            {
                Fail("binding failed: " + e.Message);
                return false;
            }
        }

        /// <summary>The type in an enabled plugin's assembly named RealGlass, or null.</summary>
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
            Log.Warn("poles: Real Glass's Reflective finish is not used (" + why + "); the poles keep their own sky reflection");
        }
    }
}
