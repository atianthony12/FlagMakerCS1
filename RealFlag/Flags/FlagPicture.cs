using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The names of the player's own images (the files in RealFlag\Images: FlagImageFolder): what a
    /// flag, the settings file and Move It keep of one -- its file name, nothing more -- and this
    /// session's numbers for them (a setting's value, as a country's is: 0 for none). PURE.
    /// </summary>
    /// <remarks>
    /// A number is only a handle for the session; the NAME is what is kept. Names are matched
    /// without regard to case, as Windows matches file names.
    /// </remarks>
    public static class FlagImageNames
    {
        /// <summary>The longest name kept, characters.</summary>
        public const int MaxLength = 128;

        private static readonly object Lock = new object();
        private static readonly List<string> Names = new List<string>();
        private static readonly Dictionary<string, int> Numbers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// A file name as a flag keeps it: trimmed, any folder in front of it dropped (only the
        /// images folder is ever read), without the characters no file name may have, at most
        /// <see cref="MaxLength"/>; empty for none (and for "." and "..").
        /// </summary>
        public static string Sanitise(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            string t = name.Trim();
            int slash = Math.Max(t.LastIndexOf('/'), t.LastIndexOf('\\'));
            if (slash >= 0)
                t = t.Substring(slash + 1);

            var sb = new StringBuilder(t.Length);
            foreach (char c in t)
            {
                if (c < ' ' || c == '"' || c == '<' || c == '>' || c == '|' || c == ':' || c == '*' || c == '?')
                    continue;
                sb.Append(c);
                if (sb.Length == MaxLength)
                    break;
            }

            string clean = sb.ToString().Trim();
            return clean == "." || clean == ".." ? string.Empty : clean;
        }

        /// <summary>This session's number for the name (from 1; a name new to the session is given the next one); 0 for none.</summary>
        public static int NumberOf(string name)
        {
            string clean = Sanitise(name);
            if (clean.Length == 0)
                return 0;

            lock (Lock)
            {
                int number;
                if (Numbers.TryGetValue(clean, out number))
                    return number;

                Names.Add(clean);
                number = Names.Count;
                Numbers[clean] = number;
                return number;
            }
        }

        /// <summary>The name with this number; empty for 0 or a number never given.</summary>
        public static string At(int number)
        {
            lock (Lock)
            {
                return number >= 1 && number <= Names.Count ? Names[number - 1] : string.Empty;
            }
        }
    }

    /// <summary>
    /// The shader's map from a cloth's uv to its image's texture coordinates, and where the image
    /// may end inside the flag: (s, t) = O + u A + v B, u along the fly, v down the hoist (the
    /// mesh's uv), t running UP from the texture's bottom row. PURE.
    /// </summary>
    public struct CropMap
    {
        public float Ox, Oy, Ax, Ay, Bx, By;

        /// <summary>1 where the image is narrower than the flag along the texture's s (or t): past its edge the flag shows its pattern.</summary>
        public float EdgeS, EdgeT;

        /// <summary>A country's flag: the whole image on the whole cloth, its top at the flag's top, its left at the hoist -- (u, 1 - v), as it always was.</summary>
        public static CropMap Whole
        {
            get { return new CropMap { Ox = 0f, Oy = 1f, Ax = 1f, Ay = 0f, Bx = 0f, By = -1f }; }
        }
    }

    /// <summary>
    /// Where a picture of the player's lies on a flag (FlagSpec.Image*): NEVER STRETCHED. The flag
    /// shows a window of the picture with the flag's own shape as it is SEEN (FlagSize: its width
    /// across, its height down, whichever edge is hoisted). At zoom 1 that window is the largest
    /// one inside the picture: the picture just fills the flag, and what is left over is cut off.
    /// At zoom z it is z times smaller -- below 1 wider than the picture, which then shows whole,
    /// with the flag's own pattern round it. The window's middle (<see cref="ClampCentre"/>) stays
    /// where the flag shows only picture (zoomed in) or where the picture stays whole on the flag
    /// (zoomed out), each way on its own. PURE.
    /// </summary>
    /// <remarks>
    /// Coordinates on a picture: x across from its left edge, y down from its top, 0..1, of the
    /// picture as it stands after its TURN (quarter turns clockwise, as seen); on the flag the same
    /// of the flag's face as seen from where its hoist is on the left. From its side the fly runs
    /// across and the hoist down; from its top the other way round (FlagOrientation) -- so a
    /// picture stays upright as seen on a banner too, where a country's flag turns as a flag hung
    /// upright does (FlagHoist). The shader takes it all as one affine map, <see cref="CropMap"/>.
    /// </remarks>
    public static class FlagCrop
    {
        /// <summary>The least zoom: a tenth of filling the flag (a small emblem).</summary>
        public const float MinZoom = 0.1f;

        /// <summary>The most: an eighth of the picture's side across the flag.</summary>
        public const float MaxZoom = 8f;

        public static float ClampZoom(float zoom)
        {
            if (float.IsNaN(zoom) || float.IsInfinity(zoom))
                return 1f;
            return zoom < MinZoom ? MinZoom : zoom > MaxZoom ? MaxZoom : zoom;
        }

        /// <summary>The flag's shape as seen: its height over its width.</summary>
        public static float Seen(int proportion, int hoisted)
        {
            float r = FlagSize.Ratio(proportion);
            return hoisted == FlagHoist.Top ? 1f / r : r;
        }

        /// <summary>The picture's size in pixels as it stands after its turn.</summary>
        public static void TurnedSize(int width, int height, int turn, out int tw, out int th)
        {
            if ((turn & 1) != 0)
            {
                tw = height;
                th = width;
            }
            else
            {
                tw = width;
                th = height;
            }
        }

        /// <summary>
        /// The window the flag shows of the (turned) picture, as fractions of its width and height:
        /// at zoom 1 one of them exactly 1, the other at most 1 -- the flag's shape (<paramref name="seen"/>,
        /// height over width) cut from the picture as large as it goes.
        /// </summary>
        public static void Window(float seen, int tw, int th, float zoom, out float ww, out float wh)
        {
            float aspect = Math.Max(1, th) / (float)Math.Max(1, tw);
            seen = Math.Max(seen, 1e-3f);
            float fw, fh;
            if (aspect > seen)
            {
                fw = 1f;
                fh = seen / aspect;
            }
            else
            {
                fw = aspect / seen;
                fh = 1f;
            }

            zoom = ClampZoom(zoom);
            ww = fw / zoom;
            wh = fh / zoom;
        }

        /// <summary>The zoom at which the whole picture shows on the flag (at most 1).</summary>
        public static float WholeZoom(float seen, int tw, int th)
        {
            float ww, wh;
            Window(seen, tw, th, 1f, out ww, out wh);
            return Math.Max(MinZoom, Math.Min(ww, wh));
        }

        /// <summary>
        /// The window's middle along one side (<paramref name="w"/> the window's size there, a
        /// fraction of the picture's): inside the picture while the window is smaller than it,
        /// keeping the whole picture on the flag while it is bigger.
        /// </summary>
        public static float ClampCentre(float c, float w)
        {
            if (float.IsNaN(c) || float.IsInfinity(c))
                c = 0.5f;
            float a = 0.5f * w, b = 1f - 0.5f * w;
            float lo = Math.Min(a, b), hi = Math.Max(a, b);
            return c < lo ? lo : c > hi ? hi : c;
        }

        /// <summary>A point of the picture as it stands after <paramref name="turn"/> quarter turns clockwise, on the picture as its file has it.</summary>
        public static void Unturn(int turn, float tx, float ty, out float fx, out float fy)
        {
            switch (turn & 3)
            {
                case 1:
                    fx = ty;
                    fy = 1f - tx;
                    break;
                case 2:
                    fx = 1f - tx;
                    fy = 1f - ty;
                    break;
                case 3:
                    fx = 1f - ty;
                    fy = tx;
                    break;
                default:
                    fx = tx;
                    fy = ty;
                    break;
            }
        }

        /// <summary>A point of the picture as its file has it, on the picture turned <paramref name="turn"/> quarter turns clockwise.</summary>
        public static void Turn(int turn, float fx, float fy, out float tx, out float ty)
        {
            switch (turn & 3)
            {
                case 1:
                    tx = 1f - fy;
                    ty = fx;
                    break;
                case 2:
                    tx = 1f - fx;
                    ty = 1f - fy;
                    break;
                case 3:
                    tx = fy;
                    ty = 1f - fx;
                    break;
                default:
                    tx = fx;
                    ty = fy;
                    break;
            }
        }

        /// <summary>The picture's shape as seen after its turn, height over width x 1000 (a flag's proportion's unit), held to 1:5..5:1.</summary>
        public static int ShapeOf(int width, int height, int turn)
        {
            int tw, th;
            TurnedSize(width, height, turn, out tw, out th);
            return FlagSize.ClampProportion((int)Math.Round(1000.0 * Math.Max(1, th) / Math.Max(1, tw)));
        }

        /// <summary>The shader's map for a flag's spec and its picture's size in pixels (as its file has it).</summary>
        public static CropMap Map(FlagSpec spec, int width, int height)
        {
            return Map(Seen(spec.Proportion, spec.HoistedFrom), spec.HoistedFrom == FlagHoist.Top, width, height, spec.ImageTurn, spec.ImageZoom,
                       spec.ImageX, spec.ImageY);
        }

        /// <summary>
        /// The shader's map: the window (<see cref="Window"/>) at its middle (<paramref name="cx"/>,
        /// <paramref name="cy"/>, held by <see cref="ClampCentre"/>) laid on the flag as seen --
        /// from its top (<paramref name="top"/>) the cloth's u runs down and its v across -- the
        /// turn undone, into the texture's coordinates. Affine, so three corners make it.
        /// </summary>
        public static CropMap Map(float seen, bool top, int width, int height, int turn, float zoom, float cx, float cy)
        {
            int tw, th;
            TurnedSize(width, height, turn, out tw, out th);
            float ww, wh;
            Window(seen, tw, th, zoom, out ww, out wh);
            cx = ClampCentre(cx, ww);
            cy = ClampCentre(cy, wh);

            float s0, t0, s1, t1, s2, t2;
            At(0f, 0f, top, cx, cy, ww, wh, turn, out s0, out t0);
            At(1f, 0f, top, cx, cy, ww, wh, turn, out s1, out t1);
            At(0f, 1f, top, cx, cy, ww, wh, turn, out s2, out t2);

            // Along the texture's s lies the turned picture's x when the turn is even, its y when odd.
            bool even = (turn & 1) == 0;
            return new CropMap
            {
                Ox = s0,
                Oy = t0,
                Ax = s1 - s0,
                Ay = t1 - t0,
                Bx = s2 - s0,
                By = t2 - t0,
                EdgeS = (even ? ww : wh) > 1.0001f ? 1f : 0f,
                EdgeT = (even ? wh : ww) > 1.0001f ? 1f : 0f,
            };
        }

        /// <summary>The texture's coordinates at the cloth's (u, v).</summary>
        public static void At(float u, float v, bool top, float cx, float cy, float ww, float wh, int turn, out float s, out float t)
        {
            // As seen: across from the flag's left edge, down from its top.
            float sx = top ? v : u, sy = top ? u : v;

            // On the picture as it stands, then as its file has it.
            float tx = cx + (sx - 0.5f) * ww, ty = cy + (sy - 0.5f) * wh;
            float fx, fy;
            Unturn(turn, tx, ty, out fx, out fy);

            s = fx;
            t = 1f - fy;
        }

        /// <summary>
        /// The window's middle after the pointer dragged the picture across the flag by
        /// (<paramref name="dx"/>, <paramref name="dy"/>) of the flag's width and height: the picture
        /// goes with the pointer, so the window the other way.
        /// </summary>
        public static void Pan(float seen, int tw, int th, float zoom, float cx, float cy, float dx, float dy, out float nx, out float ny)
        {
            float ww, wh;
            Window(seen, tw, th, zoom, out ww, out wh);
            nx = ClampCentre(cx - dx * ww, ww);
            ny = ClampCentre(cy - dy * wh, wh);
        }

        /// <summary>
        /// The window's middle for a new zoom that keeps the picture's point under the pointer
        /// (at <paramref name="px"/>, <paramref name="py"/> of the flag's width and height) where it is.
        /// </summary>
        public static void ZoomAt(float seen, int tw, int th, float zoom, float cx, float cy, float newZoom, float px, float py,
                                  out float nx, out float ny)
        {
            float ww, wh, nw, nh;
            Window(seen, tw, th, zoom, out ww, out wh);
            Window(seen, tw, th, newZoom, out nw, out nh);
            cx = ClampCentre(cx, ww);
            cy = ClampCentre(cy, wh);
            float x = cx + (px - 0.5f) * ww, y = cy + (py - 0.5f) * wh;
            nx = ClampCentre(x - (px - 0.5f) * nw, nw);
            ny = ClampCentre(y - (py - 0.5f) * nh, nh);
        }
    }

    /// <summary>
    /// A picture's size and whether it can have see-through pixels, read from its file's header
    /// alone -- the images folder's list shows it, and "the flag takes its shape" needs it, before
    /// anything is decoded. PNG (W3C / ISO 15948: the IHDR chunk; alpha by its colour type or a tRNS
    /// chunk before the image data) and JPEG (ITU-T T.81: the first start-of-frame marker; never
    /// see-through). PURE.
    /// </summary>
    public static class ImageHeader
    {
        public static bool TryRead(byte[] data, out int width, out int height, out bool alpha)
        {
            width = height = 0;
            alpha = false;
            if (data == null)
                return false;

            using (var stream = new MemoryStream(data, false))
                return TryRead(stream, out width, out height, out alpha);
        }

        /// <summary>From a stream that can seek (a file): only the headers are read, the rest stepped over.</summary>
        public static bool TryRead(Stream stream, out int width, out int height, out bool alpha)
        {
            width = height = 0;
            alpha = false;
            try
            {
                int b0 = stream.ReadByte(), b1 = stream.ReadByte();
                if (b0 == 0x89 && b1 == 0x50)
                    return Png(stream, out width, out height, out alpha);
                if (b0 == 0xFF && b1 == 0xD8)
                    return Jpeg(stream, out width, out height);
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static bool Png(Stream s, out int width, out int height, out bool alpha)
        {
            width = height = 0;
            alpha = false;

            // The rest of the signature: "NG\r\n\x1A\n".
            byte[] rest = { 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            foreach (byte b in rest)
            {
                if (s.ReadByte() != b)
                    return false;
            }

            bool header = false;
            for (int chunk = 0; chunk < 4096; chunk++)
            {
                long length = Big32(s);
                string type = Type(s);
                if (length < 0 || type == null)
                    return header;

                if (type == "IHDR")
                {
                    if (length < 13)
                        return false;
                    width = (int)Big32(s);
                    height = (int)Big32(s);
                    s.ReadByte(); // bit depth
                    int colourType = s.ReadByte();
                    alpha = colourType == 4 || colourType == 6;
                    header = width > 0 && height > 0;
                    if (!header)
                        return false;
                    Skip(s, length - 10 + 4);
                    continue;
                }

                if (type == "tRNS")
                {
                    alpha = true;
                    return header;
                }

                // The image data or the end: no tRNS can come after.
                if (type == "IDAT" || type == "IEND")
                    return header;

                Skip(s, length + 4);
            }

            return header;
        }

        private static bool Jpeg(Stream s, out int width, out int height)
        {
            width = height = 0;
            for (int segment = 0; segment < 4096; segment++)
            {
                int b = s.ReadByte();
                if (b < 0)
                    return false;
                if (b != 0xFF)
                    continue;

                int marker = s.ReadByte();
                while (marker == 0xFF)
                    marker = s.ReadByte();
                if (marker < 0)
                    return false;

                // Markers with no length: the start, the restarts, TEM.
                if (marker == 0xD8 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                    continue;

                // The end, or the scan's data: no frame header came first.
                if (marker == 0xD9 || marker == 0xDA)
                    return false;

                int length = (s.ReadByte() << 8) | s.ReadByte();
                if (length < 2)
                    return false;

                // A start of frame (C0-CF but DHT C4, JPG C8 and DAC CC): precision, height, width.
                if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                {
                    s.ReadByte();
                    height = (s.ReadByte() << 8) | s.ReadByte();
                    width = (s.ReadByte() << 8) | s.ReadByte();
                    return width > 0 && height > 0;
                }

                Skip(s, length - 2);
            }

            return false;
        }

        private static long Big32(Stream s)
        {
            int a = s.ReadByte(), b = s.ReadByte(), c = s.ReadByte(), d = s.ReadByte();
            if ((a | b | c | d) < 0)
                return -1;
            return ((long)a << 24) | ((long)b << 16) | ((long)c << 8) | (long)d;
        }

        private static string Type(Stream s)
        {
            var chars = new char[4];
            for (int i = 0; i < 4; i++)
            {
                int c = s.ReadByte();
                if (c < 0)
                    return null;
                chars[i] = (char)c;
            }

            return new string(chars);
        }

        private static void Skip(Stream s, long count)
        {
            if (count <= 0)
                return;
            if (s.CanSeek)
            {
                s.Seek(count, SeekOrigin.Current);
                return;
            }

            for (long i = 0; i < count && s.ReadByte() >= 0; i++)
            {
            }
        }
    }

    /// <summary>
    /// A see-through picture's hidden colours: every fully see-through pixel given the colour of
    /// the visible ones nearest it -- the "pull-push" of Gortler, Grzeszczuk, Szeliski and Cohen,
    /// "The Lumigraph" (SIGGRAPH 1996): each coarser level the mean of the visible pixels under it,
    /// then each hidden pixel its coarser level's. Without it the texture's filtering and its
    /// mipmaps mix in whatever colour an editor left under the see-through pixels (black, most
    /// often): a dark fringe round a shaped flag, and round a logo laid on the flag. The alpha is
    /// left as it is. PURE.
    /// </summary>
    public static class AlphaFill
    {
        /// <summary>Fills the hidden pixels (alpha 0) of a picture, rows in any order; false if none is hidden, or none visible.</summary>
        public static bool Fill(Color32[] pixels, int width, int height)
        {
            int n = width * height;
            if (pixels == null || width <= 0 || height <= 0 || pixels.Length < n)
                return false;

            bool hidden = false, shown = false;
            for (int i = 0; i < n; i++)
            {
                if (pixels[i].a == 0)
                    hidden = true;
                else
                    shown = true;
                if (hidden && shown)
                    break;
            }

            if (!hidden || !shown)
                return false;

            // Level 0: the picture. Colours packed 0xRRGGBB, and whether the pixel is known.
            var widths = new List<int> { width };
            var heights = new List<int> { height };
            var colours = new List<int[]>();
            var known = new List<bool[]>();
            var c0 = new int[n];
            var k0 = new bool[n];
            for (int i = 0; i < n; i++)
            {
                Color32 p = pixels[i];
                k0[i] = p.a != 0;
                c0[i] = (p.r << 16) | (p.g << 8) | p.b;
            }

            colours.Add(c0);
            known.Add(k0);

            // Pull: each coarser pixel the mean of the known ones of the (up to) four under it.
            while (widths[widths.Count - 1] > 1 || heights[heights.Count - 1] > 1)
            {
                int l = widths.Count - 1;
                int w = widths[l], h = heights[l];
                int nw = (w + 1) / 2, nh = (h + 1) / 2;
                int[] fine = colours[l];
                bool[] fineKnown = known[l];
                var coarse = new int[nw * nh];
                var coarseKnown = new bool[nw * nh];
                for (int y = 0; y < nh; y++)
                {
                    for (int x = 0; x < nw; x++)
                    {
                        int r = 0, g = 0, b = 0, count = 0;
                        for (int dy = 0; dy < 2; dy++)
                        {
                            int fy = 2 * y + dy;
                            if (fy >= h)
                                break;
                            for (int dx = 0; dx < 2; dx++)
                            {
                                int fx = 2 * x + dx;
                                if (fx >= w)
                                    break;
                                int k = fy * w + fx;
                                if (!fineKnown[k])
                                    continue;
                                int c = fine[k];
                                r += (c >> 16) & 0xFF;
                                g += (c >> 8) & 0xFF;
                                b += c & 0xFF;
                                count++;
                            }
                        }

                        if (count == 0)
                            continue;

                        int half = count / 2;
                        coarse[y * nw + x] = (((r + half) / count) << 16) | (((g + half) / count) << 8) | ((b + half) / count);
                        coarseKnown[y * nw + x] = true;
                    }
                }

                widths.Add(nw);
                heights.Add(nh);
                colours.Add(coarse);
                known.Add(coarseKnown);
            }

            // Push: from the coarsest down, each unknown pixel its parent's colour.
            for (int l = colours.Count - 2; l >= 0; l--)
            {
                int w = widths[l], h = heights[l], pw = widths[l + 1];
                int[] fine = colours[l], parent = colours[l + 1];
                bool[] fineKnown = known[l];
                for (int y = 0; y < h; y++)
                {
                    int row = y * w, parentRow = (y / 2) * pw;
                    for (int x = 0; x < w; x++)
                    {
                        if (fineKnown[row + x])
                            continue;
                        fine[row + x] = parent[parentRow + x / 2];
                        fineKnown[row + x] = true;
                    }
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (pixels[i].a != 0)
                    continue;
                int c = c0[i];
                pixels[i] = new Color32((byte)((c >> 16) & 0xFF), (byte)((c >> 8) & 0xFF), (byte)(c & 0xFF), 0);
            }

            return true;
        }
    }

    /// <summary>
    /// The Image window's crop view, drawn into its pixels: the flag's frame (its shape as seen)
    /// with the picture as the flag shows it, the rest of the picture dimmed round it -- what is cut
    /// off -- the flag's own colour where the frame has no picture, dark round everything, the
    /// frame's edge a line. PURE: a Texture2D's rows (from the bottom) in, the same out.
    /// </summary>
    public static class FlagCropView
    {
        /// <param name="picture">The picture's pixels, rows from the bottom (Texture2D.GetPixels32); null: none.</param>
        /// <param name="ww">The window on the turned picture (FlagCrop.Window), and its middle, <paramref name="cx"/>, <paramref name="cy"/>.</param>
        /// <param name="through">See-through pixels show the flag's colour (else they are holes: the dark shows).</param>
        /// <param name="view">The view's pixels, rows from the bottom, <paramref name="vw"/> x <paramref name="vh"/>.</param>
        /// <param name="fx">The frame's left edge, pixels from the view's left; <paramref name="fy"/> its top edge from the view's top; its size.</param>
        /// <param name="dim">What is left of the picture outside the frame, 0..1.</param>
        public static void Render(Color32[] picture, int pw, int ph, int turn, float ww, float wh, float cx, float cy, bool through,
                                  Color32[] view, int vw, int vh, float fx, float fy, float fw, float fh,
                                  Color32 field, Color32 back, Color32 edge, float dim)
        {
            fw = Math.Max(fw, 1f);
            fh = Math.Max(fh, 1f);
            int keep = (int)Math.Round(Math.Max(0f, Math.Min(dim, 1f)) * 256f);
            bool hasPicture = picture != null && pw > 0 && ph > 0 && picture.Length >= pw * ph;

            for (int row = 0; row < vh; row++)
            {
                // The view's rows run up from its bottom; the frame's from the top.
                float yTop = vh - 1 - row + 0.5f;
                float sy = (yTop - fy) / fh;
                bool rowInFrame = sy >= 0f && sy < 1f;
                float ty = cy + (sy - 0.5f) * wh;
                int o = row * vw;

                for (int x = 0; x < vw; x++)
                {
                    float sx = (x + 0.5f - fx) / fw;
                    bool inFrame = rowInFrame && sx >= 0f && sx < 1f;
                    float tx = cx + (sx - 0.5f) * ww;

                    Color32 c;
                    if (hasPicture && tx >= 0f && tx <= 1f && ty >= 0f && ty <= 1f)
                    {
                        float ux, uy;
                        FlagCrop.Unturn(turn, tx, ty, out ux, out uy);
                        Color32 p = Sample(picture, pw, ph, ux * pw - 0.5f, (1f - uy) * ph - 0.5f);
                        if (inFrame)
                        {
                            if (through)
                                c = Mix(field, p, p.a + (p.a >> 7));
                            else
                                c = p.a >= 128 ? Opaque(p) : back;
                        }
                        else
                        {
                            Color32 dimmed = Mix(back, p, keep);
                            c = through ? Mix(back, dimmed, p.a + (p.a >> 7)) : p.a >= 128 ? dimmed : back;
                        }
                    }
                    else
                    {
                        c = inFrame ? field : back;
                    }

                    view[o + x] = c;
                }
            }

            // The frame's edge: a line a pixel out from it all round.
            int x0 = (int)Math.Round(fx) - 1, x1 = (int)Math.Round(fx + fw);
            int y0 = (int)Math.Round(fy) - 1, y1 = (int)Math.Round(fy + fh);
            for (int x = x0; x <= x1; x++)
            {
                Put(view, vw, vh, x, y0, edge);
                Put(view, vw, vh, x, y1, edge);
            }

            for (int y = y0; y <= y1; y++)
            {
                Put(view, vw, vh, x0, y, edge);
                Put(view, vw, vh, x1, y, edge);
            }
        }

        /// <summary>A pixel set by its place from the view's top.</summary>
        private static void Put(Color32[] view, int vw, int vh, int x, int yTop, Color32 c)
        {
            if (x < 0 || x >= vw || yTop < 0 || yTop >= vh)
                return;
            view[(vh - 1 - yTop) * vw + x] = c;
        }

        private static Color32 Opaque(Color32 c)
        {
            c.a = 255;
            return c;
        }

        /// <summary>a to b by t of 256.</summary>
        private static Color32 Mix(Color32 a, Color32 b, int t)
        {
            int s = 256 - t;
            return new Color32((byte)((a.r * s + b.r * t) >> 8), (byte)((a.g * s + b.g * t) >> 8), (byte)((a.b * s + b.b * t) >> 8), 255);
        }

        /// <summary>Bilinear, at pixel coordinates (the pixel's middle at its index), clamped at the edges.</summary>
        private static Color32 Sample(Color32[] p, int w, int h, float x, float y)
        {
            if (x < 0f) x = 0f;
            if (y < 0f) y = 0f;
            if (x > w - 1) x = w - 1;
            if (y > h - 1) y = h - 1;
            int x0 = (int)x, y0 = (int)y;
            int x1 = x0 + 1 < w ? x0 + 1 : x0, y1 = y0 + 1 < h ? y0 + 1 : y0;
            int tx = (int)((x - x0) * 256f), ty = (int)((y - y0) * 256f);
            int sx = 256 - tx, sy = 256 - ty;
            Color32 a = p[y0 * w + x0], b = p[y0 * w + x1], c = p[y1 * w + x0], d = p[y1 * w + x1];
            int r = ((a.r * sx + b.r * tx) * sy + (c.r * sx + d.r * tx) * ty) >> 16;
            int g = ((a.g * sx + b.g * tx) * sy + (c.g * sx + d.g * tx) * ty) >> 16;
            int bl = ((a.b * sx + b.b * tx) * sy + (c.b * sx + d.b * tx) * ty) >> 16;
            int al = ((a.a * sx + b.a * tx) * sy + (c.a * sx + d.a * tx) * ty) >> 16;
            return new Color32((byte)r, (byte)g, (byte)bl, (byte)al);
        }
    }
}
