using System;
using System.IO;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The drawn poles' textures (FlagPole.shader): rust, rope and concrete (the block a pole may
    /// stand in, 2026-11-19), each a colour map (sRGB colour, its roughness in alpha) and a normal
    /// map (OpenGL's, green up, LINEAR), made by tools\pole-textures.ps1 from CC0 scans (Poly
    /// Haven's rust, ambientCG's rope and concrete; THIRD-PARTY-NOTICES.md) and embedded in the DLL
    /// as RealFlag.Poles.&lt;key&gt;_&lt;c|n&gt;.png. (The wood went with the materials, 2026-11-09.)
    /// Loaded once on the main thread when a pole first needs them, kept until <see cref="Clear"/>.
    /// Engine code, shared with the GPU preview (which reads the files instead: <see cref="Source"/>).
    /// </summary>
    public static class PoleTextures
    {
        /// <summary>The textures' keys, as the files are named.</summary>
        public static readonly string[] Keys = { "rust", "rope", "concrete" };

        private static readonly int[] ColourIds =
        {
            Shader.PropertyToID("_RFRustTex"), Shader.PropertyToID("_RFRopeTex"), Shader.PropertyToID("_RFConcreteTex"),
        };

        private static readonly int[] NormalIds =
        {
            Shader.PropertyToID("_RFRustNormal"), Shader.PropertyToID("_RFRopeNormal"), Shader.PropertyToID("_RFConcreteNormal"),
        };

        /// <summary>A file's bytes by its name ("rust_c.png"), null if there is none: the DLL's resources unless the preview says otherwise.</summary>
        public static Func<string, byte[]> Source = FromResources;

        /// <summary>Told when a texture cannot be had (once each).</summary>
        public static Action<string> Warn;

        private static Texture2D[] _colour;
        private static Texture2D[] _normal;
        private static bool _loaded;

        /// <summary>The textures into a pole's property block (loaded the first time).</summary>
        public static void Bind(MaterialPropertyBlock block)
        {
            if (!_loaded)
                Load();

            for (int i = 0; i < Keys.Length; i++)
            {
                if (_colour[i] != null)
                    block.SetTexture(ColourIds[i], _colour[i]);
                if (_normal[i] != null)
                    block.SetTexture(NormalIds[i], _normal[i]);
            }
        }

        /// <summary>Every texture let go (the renderer goes, or the preview ends).</summary>
        public static void Clear()
        {
            if (_loaded)
            {
                for (int i = 0; i < Keys.Length; i++)
                {
                    if (_colour[i] != null)
                        UnityEngine.Object.Destroy(_colour[i]);
                    if (_normal[i] != null)
                        UnityEngine.Object.Destroy(_normal[i]);
                }
            }

            _colour = null;
            _normal = null;
            _loaded = false;
        }

        private static void Load()
        {
            _colour = new Texture2D[Keys.Length];
            _normal = new Texture2D[Keys.Length];
            _loaded = true;
            for (int i = 0; i < Keys.Length; i++)
            {
                _colour[i] = Make(Keys[i] + "_c.png", false);
                _normal[i] = Make(Keys[i] + "_n.png", true);
            }
        }

        /// <summary>
        /// A PNG as a mipmapped, tiling texture. A normal map is LINEAR: LoadImage's own texture
        /// is read back and its pixels put into one made linear, so no sRGB decode ever bends the
        /// normals whatever LoadImage does with the flag.
        /// </summary>
        private static Texture2D Make(string name, bool linear)
        {
            byte[] png = null;
            try
            {
                png = Source != null ? Source(name) : null;
            }
            catch (Exception e)
            {
                if (Warn != null)
                    Warn("poles: the texture " + name + " could not be read: " + e.Message);
            }

            if (png == null)
            {
                if (Warn != null)
                    Warn("poles: the texture " + name + " is missing; poles are drawn without it");
                return null;
            }

            var read = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
            if (!read.LoadImage(png))
            {
                UnityEngine.Object.Destroy(read);
                if (Warn != null)
                    Warn("poles: the texture " + name + " is not a picture Unity reads");
                return null;
            }

            var texture = new Texture2D(read.width, read.height, TextureFormat.RGBA32, true, linear)
            {
                name = "RealFlag pole " + name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            texture.SetPixels32(read.GetPixels32());
            texture.Apply(true, true);
            UnityEngine.Object.Destroy(read);
            return texture;
        }

        private static byte[] FromResources(string name)
        {
            using (Stream stream = typeof(PoleTextures).Assembly.GetManifestResourceStream("RealFlag.Poles." + name))
            {
                if (stream == null)
                    return null;
                var bytes = new byte[stream.Length];
                int got = 0;
                while (got < bytes.Length)
                {
                    int n = stream.Read(bytes, got, bytes.Length - got);
                    if (n <= 0)
                        break;
                    got += n;
                }

                return bytes;
            }
        }
    }
}
