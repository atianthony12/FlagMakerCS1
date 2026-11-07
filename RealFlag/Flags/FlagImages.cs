using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The flags' images as textures: the countries' (FlagCountries), and the player's own
    /// pictures (FlagMaker\Images: FlagImageFolder), each made the first time a flag or the panel
    /// shows it and kept until the city unloads -- a picture of the player's also until its file
    /// changes (<see cref="ForgetUser"/>). Main thread only. Engine code, shared with the GPU
    /// preview (tools/sync-sim.ps1), which reads the countries' PNGs from the repository's folder
    /// instead of the mod's embedded copies, and the pictures from a folder of its own.
    /// </summary>
    /// <remarks>
    /// The cloth's image is mipmapped, trilinear and anisotropic (a flag seen edge-on is still a
    /// flag), sRGB (the game's linear colour space turns it to linear light as it samples it),
    /// clamped at its edges, and left uncompressed: Texture2D.Compress makes DXT5 of a PNG
    /// (LoadImage gives ARGB32) and its 4 x 4 blocks smear a flag's stars and emblems, while a
    /// 640-pixel flag is 1.5 MB with its mipmaps -- 30 MB for twenty countries in one city. The
    /// picker's thumbnails are the small images, 80 pixels wide, bilinear.
    ///
    /// A picture of the player's is whatever size its file is: past <see cref="MaxUserSize"/> on
    /// its longer side it is scaled down on the GPU (its mipmaps made first, so the blit reads
    /// the level its size needs), its shape kept -- a 2048 x 1365 picture is 15 MB with its
    /// mipmaps. Its fully see-through pixels take their visible neighbours' colours (AlphaFill:
    /// no dark fringe from the filtering). The panel has a smaller copy, <see cref="PreviewSize"/>,
    /// kept readable: its crop view is drawn from its pixels (FlagCropView).
    /// </remarks>
    public static class FlagImages
    {
        /// <summary>
        /// A flag's PNG: (image, small) -> its bytes, or null. The mod's embedded resources
        /// ("RealFlag.Flags.&lt;image&gt;.png", "RealFlag.Flags.Small.&lt;image&gt;.png") unless set.
        /// </summary>
        public static Func<string, bool, byte[]> Source = FromResources;

        /// <summary>A picture of the player's by its file name -> its bytes, or null (the mod: FlagImageFolder.Bytes; the preview sets its own).</summary>
        public static Func<string, byte[]> UserSource;

        /// <summary>Where a problem is said: the mod's log (set by the renderer), the editor's in the preview.</summary>
        public static Action<string> Warn;

        /// <summary>Where what was made is said (the mod: the log's ordinary lines).</summary>
        public static Action<string> Note;

        /// <summary>The largest side a player's picture is kept at on the cloth, pixels.</summary>
        public const int MaxUserSize = 2048;

        /// <summary>
        /// The most pixels a picture file may hold (its header's width x height) before it is
        /// refused unread: 8192 x 8192, the largest texture Unity 5.6 makes anyway. Decoding one
        /// bigger would take hundreds of MB of the engine's memory in one go (MakeUser).
        /// </summary>
        public const long MaxUserPixels = 8192L * 8192L;

        /// <summary>The panel's copy's largest side, pixels: its list's thumbnails and its crop view.</summary>
        public const int PreviewSize = 512;

        private static readonly Dictionary<string, Texture2D> Big = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> Small = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> UserBig = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Texture2D> UserSmall = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Moves on whenever a player's picture is let go (its file changed, went or came): the flags put their looks again.</summary>
        public static int UserVersion { get; private set; }

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

        /// <summary>The cloth's texture for a picture of the player's (by its file name); null if its file is not there or cannot be read (said once).</summary>
        public static Texture2D User(string name)
        {
            return LoadUser(name, false);
        }

        /// <summary>The panel's copy of a picture of the player's, readable; null if it cannot be had.</summary>
        public static Texture2D UserPreview(string name)
        {
            return LoadUser(name, true);
        }

        /// <summary>The panel's copy has been looked for already (made, or found missing): asking again costs nothing.</summary>
        public static bool HasUserPreview(string name)
        {
            return !string.IsNullOrEmpty(name) && UserSmall.ContainsKey(name);
        }

        /// <summary>A picture's file changed, went or came: its textures are made again the next time they are asked for.</summary>
        public static void ForgetUser(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;

            bool any = Drop(UserBig, name) | Drop(UserSmall, name);
            if (any)
                UserVersion++;
        }

        private static bool Drop(Dictionary<string, Texture2D> cache, string name)
        {
            Texture2D texture;
            if (!cache.TryGetValue(name, out texture))
                return false;

            cache.Remove(name);
            if (texture != null)
                FlagMesh.Release(texture);
            return true;
        }

        /// <summary>How many textures are made, and their memory, for the log.</summary>
        public static string Describe()
        {
            long bytes = 0, userBytes = 0;
            int count = 0, users = 0;
            foreach (Texture2D t in Big.Values)
            {
                if (t == null)
                    continue;
                count++;
                bytes += (long)t.width * t.height * 4 * 4 / 3;
            }

            foreach (Texture2D t in UserBig.Values)
            {
                if (t == null)
                    continue;
                users++;
                userBytes += (long)t.width * t.height * 4 * 4 / 3;
            }

            return count + " flag image(s), " + (bytes / (1024.0 * 1024.0)).ToString("F1") + " MB, " + Small.Count + " thumbnail(s); " +
                   users + " picture(s) of the player's, " + (userBytes / (1024.0 * 1024.0)).ToString("F1") + " MB, " + UserSmall.Count + " panel cop(ies)";
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

        private static void Tell(string text)
        {
            if (Note != null)
                Note(text);
        }

        private static Texture2D Make(byte[] png, bool forCloth, string name)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, forCloth, false);
            if (!texture.LoadImage(png))
            {
                FlagMesh.Release(texture);
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

        // ---- the player's pictures -------------------------------------------------------------

        private static Texture2D LoadUser(string name, bool preview)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            Dictionary<string, Texture2D> cache = preview ? UserSmall : UserBig;
            Texture2D texture;
            if (cache.TryGetValue(name, out texture))
                return texture;

            // Asked before the folder is known (the panel built before the renderer started):
            // nothing, and nothing kept -- the next ask, once it is, makes it.
            if (UserSource == null)
                return null;

            texture = null;
            string how = null;
            var watch = Stopwatch.StartNew();
            try
            {
                // The panel's copy from the cloth's when that is made: no second decoding.
                Texture2D big;
                if (preview && UserBig.TryGetValue(name, out big) && big != null)
                {
                    texture = Shrink(big, PreviewSize);
                    Finish(texture, false);
                    how = "from the cloth's";
                }
                else
                {
                    byte[] bytes = UserSource != null ? UserSource(name) : null;
                    if (bytes != null)
                        texture = MakeUser(bytes, preview ? PreviewSize : MaxUserSize, !preview, out how);
                    else
                        how = "no such file";
                }
            }
            catch (Exception e)
            {
                how = e.GetType().Name + ": " + e.Message;
                if (texture != null)
                    FlagMesh.Release(texture);
                texture = null;
            }

            if (texture == null)
            {
                // Said once for the cloth; the panel shows it as missing.
                if (!preview)
                    Say("flags: your image '" + name + "' cannot be had (" + how + "); the flag shows its pattern instead, until it can");
            }
            else
            {
                texture.name = "RealFlag picture " + name + (preview ? " (panel)" : "");
                if (!preview)
                    Tell("flags: your image '" + name + "' made for the cloth: " + how + ", " +
                         ((long)texture.width * texture.height * 4 * 4 / 3 / (1024.0 * 1024.0)).ToString("F1") + " MB, " +
                         watch.ElapsedMilliseconds + " ms");
            }

            // Kept even when null: a missing picture is looked for again only when its folder changes.
            cache[name] = texture;
            return texture;
        }

        /// <summary>A picture's texture from its file's bytes: decoded, scaled down past <paramref name="max"/>, its see-through pixels' colours filled, mipmapped.</summary>
        private static Texture2D MakeUser(byte[] bytes, int max, bool forCloth, out string how)
        {
            int headerWidth, headerHeight;
            bool alpha;
            bool known = ImageHeader.TryRead(bytes, out headerWidth, out headerHeight, out alpha);

            // LoadImage decodes the whole picture into the engine's memory before anything here
            // can shrink it: one past MaxUserPixels (a poster scan, a decompression bomb) is refused
            // by its header, never decoded.
            if (known && (long)headerWidth * headerHeight > MaxUserPixels)
            {
                how = headerWidth + " x " + headerHeight + " is too big to decode (over " + (MaxUserPixels / 1000000) +
                      " megapixels); make it smaller";
                return null;
            }

            var source = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
            if (!source.LoadImage(bytes, false))
            {
                FlagMesh.Release(source);
                how = "not a PNG or JPEG it can read";
                return null;
            }

            how = source.width + " x " + source.height;
            Texture2D texture = source;
            if (source.width > max || source.height > max)
            {
                source.Apply(true, false);
                texture = Shrink(source, max);
                FlagMesh.Release(source);
                how += " scaled down to " + texture.width + " x " + texture.height;
            }

            // A PNG that can have see-through pixels: theirs made their neighbours' colours.
            if (!known || alpha)
            {
                Color32[] pixels = texture.GetPixels32();
                if (AlphaFill.Fill(pixels, texture.width, texture.height))
                {
                    texture.SetPixels32(pixels);
                    how += ", see-through in parts";
                }
            }

            Finish(texture, forCloth);
            return texture;
        }

        /// <summary>Clamped, trilinear, mipmapped; the cloth's copy let go of its memory (the GPU has it), the panel's kept readable.</summary>
        private static void Finish(Texture2D texture, bool forCloth)
        {
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = forCloth ? 4 : 1;
            texture.Apply(true, forCloth);
        }

        /// <summary>
        /// A readable copy of a texture at most <paramref name="max"/> pixels a side, its shape
        /// kept: drawn on the GPU into an sRGB render texture and read back (the source's mipmaps,
        /// with trilinear filtering, give each pixel the mean of the pixels it covers).
        /// </summary>
        private static Texture2D Shrink(Texture2D source, int max)
        {
            float scale = Mathf.Min(1f, max / (float)Mathf.Max(source.width, source.height));
            int w = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int h = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

            FilterMode filter = source.filterMode;
            source.filterMode = FilterMode.Trilinear;
            RenderTexture target = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                var result = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
                result.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
                return result;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                source.filterMode = filter;
            }
        }

        /// <summary>Every texture made, destroyed (the city unloads).</summary>
        public static void Clear()
        {
            foreach (Texture2D t in Big.Values)
                if (t != null)
                    FlagMesh.Release(t);
            foreach (Texture2D t in Small.Values)
                if (t != null)
                    FlagMesh.Release(t);
            foreach (Texture2D t in UserBig.Values)
                if (t != null)
                    FlagMesh.Release(t);
            foreach (Texture2D t in UserSmall.Values)
                if (t != null)
                    FlagMesh.Release(t);

            Big.Clear();
            Small.Clear();
            UserBig.Clear();
            UserSmall.Clear();
            UserVersion++;
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
