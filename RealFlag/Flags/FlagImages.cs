using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealFlag.Flags
{
    /// <summary>
    /// The countries' flags as textures (FlagCountries): each made from its PNG the first time a
    /// flag or the picker shows it, and kept until the city unloads. Main thread only. Engine
    /// code, shared with the GPU preview (tools/sync-sim.ps1), which reads the PNGs from the
    /// repository's folder instead of the mod's embedded copies.
    /// </summary>
    /// <remarks>
    /// The cloth's image is mipmapped, trilinear and anisotropic (a flag seen edge-on is still a
    /// flag), sRGB (the game's linear colour space turns it to linear light as it samples it),
    /// clamped at its edges, and left uncompressed: Texture2D.Compress makes DXT5 of a PNG
    /// (LoadImage gives ARGB32) and its 4 x 4 blocks smear a flag's stars and emblems, while a
    /// 640-pixel flag is 1.5 MB with its mipmaps -- 30 MB for twenty countries in one city. The
    /// picker's thumbnails are the small images, 80 pixels wide, bilinear.
    /// </remarks>
    public static class FlagImages
    {
        /// <summary>
        /// A flag's PNG: (image, small) -> its bytes, or null. The mod's embedded resources
        /// ("RealFlag.Flags.&lt;image&gt;.png", "RealFlag.Flags.Small.&lt;image&gt;.png") unless set.
        /// </summary>
        public static Func<string, bool, byte[]> Source = FromResources;

        /// <summary>Where a problem is said: the mod's log (set by the renderer), the editor's in the preview.</summary>
        public static Action<string> Warn;

        private static readonly Dictionary<string, Texture2D> Big = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> Small = new Dictionary<string, Texture2D>();

        /// <summary>The cloth's texture for this image; null if it cannot be had (said once in the log).</summary>
        public static Texture2D Get(string image)
        {
            return Load(image, false);
        }

        /// <summary>The picker's thumbnail for this image; null if it cannot be had.</summary>
        public static Texture2D Thumbnail(string image)
        {
            return Load(image, true);
        }

        /// <summary>How many textures are made, and their memory, for the log.</summary>
        public static string Describe()
        {
            long bytes = 0;
            int count = 0;
            foreach (Texture2D t in Big.Values)
            {
                if (t == null)
                    continue;
                count++;
                bytes += (long)t.width * t.height * 4 * 4 / 3;
            }

            return count + " flag image(s), " + (bytes / (1024.0 * 1024.0)).ToString("F1") + " MB, " + Small.Count + " thumbnail(s)";
        }

        private static Texture2D Load(string image, bool small)
        {
            if (string.IsNullOrEmpty(image))
                return null;

            Dictionary<string, Texture2D> cache = small ? Small : Big;
            Texture2D texture;
            if (cache.TryGetValue(image, out texture))
                return texture;

            texture = null;
            try
            {
                byte[] png = Source != null ? Source(image, small) : null;
                if (png != null)
                    texture = Make(png, !small, image);
            }
            catch (Exception e)
            {
                Say("flags: could not make the image '" + image + "'" + (small ? " (thumbnail)" : "") + ": " + e.Message);
            }

            if (texture == null)
                Say("flags: no image '" + image + "'" + (small ? " (thumbnail)" : "") + "; the flag shows its pattern instead");

            // Kept even when null: a missing image is looked for once.
            cache[image] = texture;
            return texture;
        }

        private static void Say(string text)
        {
            if (Warn != null)
                Warn(text);
        }

        private static Texture2D Make(byte[] png, bool forCloth, string name)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, forCloth, false);
            if (!texture.LoadImage(png))
            {
                Object.Destroy(texture);
                return null;
            }

            texture.name = "RealFlag " + name + (forCloth ? "" : " (small)");
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = forCloth ? FilterMode.Trilinear : FilterMode.Bilinear;
            texture.anisoLevel = forCloth ? 4 : 1;

            // The mipmaps made from the image (LoadImage does not promise them), then the copy in
            // memory let go: the GPU has it.
            texture.Apply(forCloth, true);
            return texture;
        }

        /// <summary>Every texture made, destroyed (the city unloads).</summary>
        public static void Clear()
        {
            foreach (Texture2D t in Big.Values)
                if (t != null)
                    Object.Destroy(t);
            foreach (Texture2D t in Small.Values)
                if (t != null)
                    Object.Destroy(t);

            Big.Clear();
            Small.Clear();
        }

        private static byte[] FromResources(string image, bool small)
        {
            string name = "RealFlag.Flags." + (small ? "Small." : "") + image + ".png";
            using (Stream stream = typeof(FlagImages).Assembly.GetManifestResourceStream(name))
            {
                if (stream == null)
                    return null;

                var bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int n = stream.Read(bytes, read, bytes.Length - read);
                    if (n <= 0)
                        break;
                    read += n;
                }

                return read == bytes.Length ? bytes : null;
            }
        }
    }
}
