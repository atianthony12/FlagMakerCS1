using System;
using System.Collections.Generic;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The designs with an image that strings fly -- a country's flag, a picture of the player's --
    /// each baked into a cell of one texture, so that every flag of a span, whatever its design,
    /// is in one mesh drawn once (FlagString.shader reads the cell from the flag's vertices).
    /// Only the designs in use are baked, a few a frame; until its cell is there a flag shows its
    /// design's pattern. A texture atlas, as games have long packed many small textures into one
    /// to draw them in one call (NVIDIA, "Improve Batching Using Texture Atlases", 2004).
    /// </summary>
    /// <remarks>
    /// Each cell holds its design stretched to the cell, as its flag is to the string's size --
    /// a string's flags are all one size, as real strings of national flags are printed -- with
    /// its own mipmaps, so a cell never bleeds into its neighbours until it is a few pixels across.
    /// A picture is cropped as the flag's own design crops it (FlagCrop.Map, never stretched on
    /// the flag's shape), the pattern round it where it is smaller, its see-through parts kept in
    /// the alpha (cut, or the pattern through them: the flag's vertex says which).
    /// </remarks>
    public sealed class StringAtlas
    {
        public const int CellWidth = 128, CellHeight = 96;
        public const int Columns = 16, Rows = 21;
        public const int Width = CellWidth * Columns, Height = 2048;

        /// <summary>Cells' mip levels written (to 4 x 3 texels); the atlas's deeper ones are each cell's average.</summary>
        private const int CellMips = 6;

        /// <summary>The share of a cell the shader keeps clear of its edge: about a texel of its full size (the far levels bleed a little: a flag a few pixels across).</summary>
        public const float Inset = 0.006f;

        /// <summary>Most cells baked a frame (a picture's decoding and its 2 x 2 taps a texel: a few ms in the game's Mono), and how often the texture goes to the GPU, seconds.</summary>
        public const int BakesPerFrame = 1;
        public const float UploadInterval = 0.25f;

        private Texture2D _texture;
        private readonly Dictionary<string, int> _cells = new Dictionary<string, int>();
        private readonly Queue<KeyValuePair<string, FlagSpec>> _wanted = new Queue<KeyValuePair<string, FlagSpec>>();
        private readonly HashSet<string> _queued = new HashSet<string>();
        private readonly HashSet<string> _failed = new HashSet<string>();
        private int _next;
        private bool _dirty;
        private float _nextUpload;

        /// <summary>Moves on when a cell is added: the spans' meshes take their cells again.</summary>
        public int Version { get; private set; }

        public Texture2D Texture
        {
            get { return _texture; }
        }

        public Vector4 Grid
        {
            get { return new Vector4(Columns, Rows, 1f / Columns, (float)CellHeight / Height); }
        }

        public int Count
        {
            get { return _cells.Count; }
        }

        /// <summary>A design's key: what makes its cell (empty: it has no image).</summary>
        public static string KeyOf(FlagSpec d, float aspect)
        {
            FlagCountry country = FlagCountries.Find(d.Country);
            if (country != null)
                return "c:" + country.Image;
            if (!d.HasImage)
                return string.Empty;
            return "p:" + d.Image + "|" + d.ImageX.ToString("F3") + "," + d.ImageY.ToString("F3") + "," + d.ImageZoom.ToString("F3") + "," + d.ImageTurn +
                   "," + d.ImageAlpha + "|" + d.Pattern + "," + d.Colour1 + "," + d.Colour2 + "," + d.Colour3 + "|" + aspect.ToString("F3") + "|" + FlagImages.UserVersion;
        }

        /// <summary>The design's cell + 1 (0: none yet -- asked for, it is baked soon -- or it has no image).</summary>
        public int CellFor(FlagSpec design, float aspect)
        {
            string key = KeyOf(design, aspect);
            if (key.Length == 0)
                return 0;

            int cell;
            if (_cells.TryGetValue(key, out cell))
                return cell + 1;
            if (!_failed.Contains(key) && _queued.Add(key))
            {
                FlagSpec d = design;
                d.Proportion = FlagSize.ClampProportion((int)Math.Round(aspect * 1000f));
                d.HoistedFrom = FlagHoist.Side;
                _wanted.Enqueue(new KeyValuePair<string, FlagSpec>(key, d));
            }

            return 0;
        }

        /// <summary>The frame's bakes, and the texture to the GPU when there were some (at most every UploadInterval).</summary>
        public void Tick(float now)
        {
            int baked = 0;
            while (_wanted.Count > 0 && baked < BakesPerFrame)
            {
                KeyValuePair<string, FlagSpec> w = _wanted.Dequeue();
                _queued.Remove(w.Key);
                if (_cells.ContainsKey(w.Key))
                    continue;

                if (_next >= Columns * Rows)
                {
                    // Full: the oldest let go is not tracked; start again (every span takes its cells afresh).
                    if (FlagImages.Note != null)
                        FlagImages.Note("strings: the designs' atlas is full (" + (Columns * Rows) + " cells): made again from what is in use");
                    _cells.Clear();
                    _next = 0;
                    Version++;
                }

                if (Bake(_next, w.Value))
                {
                    _cells[w.Key] = _next++;
                    baked++;
                }
                else
                {
                    _failed.Add(w.Key);
                }
            }

            if (baked > 0)
                Version++;

            if (_dirty && now >= _nextUpload && _texture != null)
            {
                _texture.Apply(false, false);
                _dirty = false;
                _nextUpload = now + UploadInterval;
            }
        }

        public void Clear()
        {
            if (_texture != null)
                UnityEngine.Object.Destroy(_texture);
            _texture = null;
            _cells.Clear();
            _wanted.Clear();
            _queued.Clear();
            _failed.Clear();
            _next = 0;
            Version++;
        }

        private void Ensure()
        {
            if (_texture != null)
                return;

            _texture = new Texture2D(Width, Height, TextureFormat.RGBA32, true, false)
            {
                name = "RealFlag string designs",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };

            // A neutral start, every level.
            for (int m = 0; m < _texture.mipmapCount; m++)
            {
                int w = Math.Max(1, Width >> m), h = Math.Max(1, Height >> m);
                var grey = new Color32[w * h];
                for (int i = 0; i < grey.Length; i++)
                    grey[i] = new Color32(200, 200, 200, 255);
                _texture.SetPixels32(grey, m);
            }

            _dirty = true;
        }

        /// <summary>The design into cell <paramref name="cell"/>: its pixels, area-averaged from its source, every level.</summary>
        private bool Bake(int cell, FlagSpec d)
        {
            Texture2D source = null;
            try
            {
                FlagCountry country = FlagCountries.Find(d.Country);
                byte[] bytes = country != null
                    ? (FlagImages.Source != null ? FlagImages.Source(country.Image, false) : null)
                    : (FlagImages.UserSource != null ? FlagImages.UserSource(d.Image) : null);
                if (bytes == null)
                    return false;

                source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                if (!source.LoadImage(bytes, false))
                    return false;

                Ensure();
                Color32[] src = source.GetPixels32();
                int sw = source.width, sh = source.height;
                CropMap map = country != null ? CropMap.Whole : FlagCrop.Map(d, sw, sh);
                bool picture = country == null;

                // The pattern round a picture smaller than the flag.
                Color32[] pattern = null;
                if (picture)
                {
                    pattern = new Color32[CellWidth * CellHeight];
                    FlagPatternView.Render(pattern, CellWidth, CellHeight, 0f, 0f, CellWidth, CellHeight, d.Pattern, FlagProportion.Ratio(d.Proportion), false,
                                           d.Colour1, d.Colour2, d.Colour3, new Color32(0, 0, 0, 0));
                }

                var pixels = new Color32[CellWidth * CellHeight];
                const int taps = 2;
                for (int y = 0; y < CellHeight; y++)
                {
                    for (int x = 0; x < CellWidth; x++)
                    {
                        float r = 0f, g = 0f, b = 0f, a = 0f, inside = 0f;
                        for (int ty = 0; ty < taps; ty++)
                        {
                            for (int tx = 0; tx < taps; tx++)
                            {
                                // The flag's u (hoist to fly) and v (top to bottom) at this tap; the texture's rows from the bottom.
                                float u = (x + (tx + 0.5f) / taps) / CellWidth;
                                float v = 1f - (y + (ty + 0.5f) / taps) / CellHeight;
                                float s = map.Ox + u * map.Ax + v * map.Bx;
                                float t = map.Oy + u * map.Ay + v * map.By;
                                if (s < 0f || s > 1f || t < 0f || t > 1f)
                                    continue;
                                inside += 1f;
                                Color32 c = Bilinear(src, sw, sh, s, t);
                                r += c.r; g += c.g; b += c.b; a += c.a;
                            }
                        }

                        int n = taps * taps, k = y * CellWidth + x;
                        if (inside > 0f)
                        {
                            r /= inside; g /= inside; b /= inside; a /= inside;
                        }

                        float share = inside / n;
                        if (picture && share < 1f)
                        {
                            // Past the picture's edge: the pattern, opaque.
                            Color32 p = pattern[k];
                            r = r * share + p.r * (1f - share);
                            g = g * share + p.g * (1f - share);
                            b = b * share + p.b * (1f - share);
                            a = a * share + 255f * (1f - share);
                        }

                        pixels[k] = new Color32((byte)Math.Min(255f, r + 0.5f), (byte)Math.Min(255f, g + 0.5f), (byte)Math.Min(255f, b + 0.5f), (byte)Math.Min(255f, a + 0.5f));
                    }
                }

                Write(cell, pixels);
                return true;
            }
            catch (Exception e)
            {
                // FlagImages' own reporting (the mod's log, the editor's in the preview): this file is shared with the preview.
                if (FlagImages.Warn != null)
                    FlagImages.Warn("strings: could not bake a design into the atlas (" + (string.IsNullOrEmpty(d.Country) ? d.Image : d.Country) + "): " + e.Message);
                return false;
            }
            finally
            {
                if (source != null)
                    UnityEngine.Object.Destroy(source);
            }
        }

        private static Color32 Bilinear(Color32[] src, int w, int h, float s, float t)
        {
            float fx = s * w - 0.5f, fy = t * h - 0.5f;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            float ax = fx - x0, ay = fy - y0;
            int x1 = Math.Min(w - 1, Math.Max(0, x0 + 1)), y1 = Math.Min(h - 1, Math.Max(0, y0 + 1));
            x0 = Math.Min(w - 1, Math.Max(0, x0));
            y0 = Math.Min(h - 1, Math.Max(0, y0));
            Color32 a = src[y0 * w + x0], b = src[y0 * w + x1], c = src[y1 * w + x0], d = src[y1 * w + x1];
            return new Color32(
                (byte)Lerp2(a.r, b.r, c.r, d.r, ax, ay), (byte)Lerp2(a.g, b.g, c.g, d.g, ax, ay),
                (byte)Lerp2(a.b, b.b, c.b, d.b, ax, ay), (byte)Lerp2(a.a, b.a, c.a, d.a, ax, ay));
        }

        private static float Lerp2(float a, float b, float c, float d, float ax, float ay)
        {
            float top = a + (b - a) * ax, bottom = c + (d - c) * ax;
            return top + (bottom - top) * ay + 0.5f;
        }

        /// <summary>The cell's pixels into its place on every level: its own mipmaps (2 x 2 box) to CellMips, its average below.</summary>
        private void Write(int cell, Color32[] pixels)
        {
            int cx = (cell % Columns) * CellWidth;
            // Rows from the TOP of the atlas, as the shader counts them; the texture's rows from the bottom.
            int cy = Height - (cell / Columns + 1) * CellHeight;
            int w = CellWidth, h = CellHeight;
            Color32[] level = pixels;
            for (int m = 0; m < _texture.mipmapCount; m++)
            {
                if (m < CellMips)
                {
                    _texture.SetPixels32(cx >> m, cy >> m, w, h, level, m);
                    if (m + 1 < CellMips)
                    {
                        level = Halve(level, w, h);
                        w /= 2;
                        h /= 2;
                    }
                }
                else
                {
                    // Below the cell's own levels: one texel of its average where it lands.
                    Color32 avg = Average(level);
                    int lw = Math.Max(1, Width >> m), lh = Math.Max(1, Height >> m);
                    int x = Math.Min(lw - 1, cx >> m), y = Math.Min(lh - 1, cy >> m);
                    _texture.SetPixels32(x, y, 1, 1, new[] { avg }, m);
                }
            }

            _dirty = true;
        }

        private static Color32[] Halve(Color32[] p, int w, int h)
        {
            int hw = w / 2, hh = h / 2;
            var r = new Color32[hw * hh];
            for (int y = 0; y < hh; y++)
            {
                for (int x = 0; x < hw; x++)
                {
                    Color32 a = p[2 * y * w + 2 * x], b = p[2 * y * w + 2 * x + 1], c = p[(2 * y + 1) * w + 2 * x], d = p[(2 * y + 1) * w + 2 * x + 1];
                    r[y * hw + x] = new Color32((byte)((a.r + b.r + c.r + d.r + 2) / 4), (byte)((a.g + b.g + c.g + d.g + 2) / 4),
                                                (byte)((a.b + b.b + c.b + d.b + 2) / 4), (byte)((a.a + b.a + c.a + d.a + 2) / 4));
                }
            }

            return r;
        }

        private static Color32 Average(Color32[] p)
        {
            long r = 0, g = 0, b = 0, a = 0;
            foreach (Color32 c in p)
            {
                r += c.r; g += c.g; b += c.b; a += c.a;
            }

            int n = Math.Max(1, p.Length);
            return new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), (byte)(a / n));
        }
    }
}
