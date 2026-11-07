using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The shaders compiled in Unity 5.6 and embedded in this assembly as AssetBundles: one per
    /// platform, because a bundle only holds code for the graphics APIs its build target allows
    /// (Direct3D 11 and OpenGL Core for Windows, Metal and OpenGL Core for the Mac, OpenGL Core
    /// for Linux; build-bundle.ps1). The one for <see cref="Application.platform"/> is loaded; the
    /// others stay unread inside the DLL. (Volumetric Explosions' loader, with this mod's names.)
    /// </summary>
    /// <remarks>
    /// Loaded once and the bundle released immediately (keeping the shaders alive): a bundle left
    /// loaded cannot be loaded a second time, which would break the mod's hot reload. A bundle
    /// built for another standalone platform still LOADS, with every shader isSupported false: a
    /// wrong pick is a stand-down (Loader), never a crash.
    /// </remarks>
    public static class ShaderBundle
    {
        /// <summary>The flag's cloth (FlagCloth.shader).</summary>
        public const string Cloth = "RealFlag/FlagCloth";

        /// <summary>The pole the mod can draw under a flag (FlagPole.shader).</summary>
        public const string Pole = "RealFlag/FlagPole";

        /// <summary>The selected flag's move and turn handles (FlagGizmo.shader).</summary>
        public const string Gizmo = "RealFlag/FlagGizmo";

        private const string ResourcePrefix = "RealFlag.Resources.realflag-";
        private const string ResourceSuffix = ".bundle";

        private static Dictionary<string, Shader> _shaders;

        /// <summary>Which bundle a platform gets: "win", "mac" or "linux"; null where none is built.</summary>
        public static string BundleFor(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return "win";
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return "mac";
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                    return "linux";
                default:
                    return null;
            }
        }

        /// <summary>Returns the named shader, or null if it is missing or unsupported here.</summary>
        public static Shader Get(string shaderName)
        {
            if (_shaders == null)
                _shaders = Load();

            Shader shader;
            if (!_shaders.TryGetValue(shaderName, out shader))
            {
                Log.Warn("Shader '" + shaderName + "' is not in the bundle.");
                return null;
            }

            if (!shader.isSupported)
            {
                Log.Error("Shader '" + shaderName + "' is not supported on this GPU/graphics API (" +
                          SystemInfo.graphicsDeviceType + ", '" + SystemInfo.graphicsDeviceName + "').");
                return null;
            }

            return shader;
        }

        private static Dictionary<string, Shader> Load()
        {
            var result = new Dictionary<string, Shader>();

            try
            {
                string name = BundleFor(Application.platform);
                if (name == null)
                {
                    Log.Warn("No shader bundle is built for the platform '" + Application.platform + "'; flags cannot be drawn.");
                    return result;
                }

                string resource = ResourcePrefix + name + ResourceSuffix;
                byte[] data = ReadResource(resource);
                if (data == null)
                {
                    Log.Warn("Shader bundle '" + resource + "' is not embedded in this build (embedded: " +
                             string.Join(", ", Assembly.GetExecutingAssembly().GetManifestResourceNames()) +
                             "); flags cannot be drawn. Run build-bundle.ps1, then rebuild the mod.");
                    return result;
                }

                AssetBundle bundle = AssetBundle.LoadFromMemory(data);
                if (bundle == null)
                {
                    Log.Error("Shader bundle '" + name + "' failed to load (built with a mismatched Unity version?).");
                    return result;
                }

                foreach (string asset in bundle.GetAllAssetNames())
                {
                    if (!asset.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                        continue;

                    Shader shader = bundle.LoadAsset<Shader>(asset);
                    if (shader != null)
                        result[shader.name] = shader;
                }

                bundle.Unload(false);

                string[] names = new string[result.Count];
                result.Keys.CopyTo(names, 0);
                Log.Msg("shader bundle loaded: " + name + " (" + data.Length + " bytes) for " + Application.platform +
                        " on " + SystemInfo.graphicsDeviceType + ": " + string.Join(", ", names));
            }
            catch (Exception e)
            {
                Log.Error("Loading the shader bundle threw.", e);
            }

            return result;
        }

        private static byte[] ReadResource(string resource)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (stream == null)
                    return null;

                byte[] data = new byte[stream.Length];
                int offset = 0;
                while (offset < data.Length)
                {
                    int read = stream.Read(data, offset, data.Length - offset);
                    if (read <= 0)
                        break;
                    offset += read;
                }

                return data;
            }
        }
    }
}
