using System;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// A pattern drawn flat in plain pixels, for the panels: the Pattern window's preview, its
    /// little flags, the rows' thumbnails (the author: "a preview pane for the pattern like we do
    /// for the flag image import" ... "just a 2D sprite"). PURE: Color32 is only a struct here.
    /// </summary>
    /// <remarks>
    /// The C# copy of FlagPattern.cginc's RFPatternColour, as ClothSurface is of the vertex
    /// program's surface: <b>a new pattern, or a pattern's numbers changed, goes in both</b>. Every
    /// pattern there is a field and at most two layers laid over it, which is what
    /// <see cref="Layers"/> gives back; the same distances in hoist units, the same edge blended
    /// over 1.2 pixels of footprint. Blended in the panel's sRGB where the shader blends in
    /// linear: seen only in the one pixel of an edge.
    /// </remarks>
    public static class FlagPatternView
    {
        private static float Inside(float d, float w)
        {
            float t = 0.5f - d / Math.Max(w, 1e-5f);
            return t < 0f ? 0f : t > 1f ? 1f : t;
        }

        private static float Frac(float x)
        {
            return x - (float)Math.Floor(x);
        }

        private static float Cross(float px, float py, float cx, float cy, float width)
        {
            return Math.Min(Math.Abs(py - cy) - 0.5f * width, Math.Abs(px - cx) - 0.5f * width);
        }

        private static float Saltire(float px, float py, float len, float width)
        {
            float n = (float)Math.Sqrt(len * len + 1f);
            float dx = len / n, dy = 1f / n;
            float a = Math.Abs(px * dy - py * dx);
            float b = Math.Abs(px * dy + (py - 1f) * dx);
            return Math.Min(a, b) - 0.5f * width;
        }

        private static float Box(float px, float py, float cx, float cy, float hx, float hy)
        {
            return Math.Max(Math.Abs(px - cx) - hx, Math.Abs(py - cy) - hy);
        }

        /// <summary>
        /// What shows at a point of the cloth: colour <paramref name="field"/>, colour
        /// <paramref name="first"/> over it by <paramref name="t1"/>, colour <paramref name="second"/>
        /// over that by <paramref name="t2"/> (colours 1..3).
        /// </summary>
        /// <param name="px">Along the fly from the hoist, in hoist units (0 .. <paramref name="len"/>).</param>
        /// <param name="py">Down from the top, in hoist units (0..1).</param>
        /// <param name="len">The flag's length over its hoist.</param>
        /// <param name="w">An edge's width, in hoist units.</param>
        public static void Layers(int pattern, float px, float py, float len, float w, out int field, out int first, out float t1, out int second, out float t2)
        {
            field = 1;
            first = 1;
            second = 1;
            t1 = 0f;
            t2 = 0f;

            switch (pattern)
            {
                case FlagPattern.HorizontalBicolour:
                    field = 2;
                    t1 = Inside(py - 0.5f, w);
                    break;

                case FlagPattern.HorizontalTricolour:
                    field = 3;
                    first = 2;
                    t1 = Inside(py - 2f / 3f, w);
                    t2 = Inside(py - 1f / 3f, w);
                    break;

                case FlagPattern.VerticalTricolour:
                    field = 3;
                    first = 2;
                    t1 = Inside(px - 2f * len / 3f, w);
                    t2 = Inside(px - len / 3f, w);
                    break;

                case FlagPattern.VerticalBicolour:
                    field = 2;
                    t1 = Inside(px - 0.5f * len, w);
                    break;

                case FlagPattern.NordicCross:
                    first = 2;
                    second = 3;
                    t1 = Inside(Cross(px, py, 0.375f * len, 0.5f, 0.2f), w);
                    t2 = Inside(Cross(px, py, 0.375f * len, 0.5f, 0.1f), w);
                    break;

                case FlagPattern.Cross:
                    first = 2;
                    second = 3;
                    t1 = Inside(Cross(px, py, 0.5f * len, 0.5f, 0.2f), w);
                    t2 = Inside(Cross(px, py, 0.5f * len, 0.5f, 0.1f), w);
                    break;

                case FlagPattern.Saltire:
                    first = 2;
                    second = 3;
                    t1 = Inside(Saltire(px, py, len, 0.2f), w);
                    t2 = Inside(Saltire(px, py, len, 0.08f), w);
                    break;

                case FlagPattern.Canton:
                {
                    float box = Box(px, py, 0.25f * len, 0.25f, 0.25f * len, 0.25f);
                    first = 3;
                    second = 2;
                    t1 = Inside(box - 0.03f, w);
                    t2 = Inside(box, w);
                    break;
                }

                case FlagPattern.StripesAndCanton:
                {
                    float q = Frac(py * 6.5f);
                    float dq = Math.Abs(Frac(q + 0.25f) - 0.5f);
                    field = 2;
                    second = 3;
                    t1 = Inside((dq * 2f - 0.5f) / 13f, w);
                    t2 = Inside(Box(px, py, 0.2f * len, 3.5f / 13f, 0.2f * len, 3.5f / 13f), w);
                    break;
                }

                case FlagPattern.Disc:
                {
                    float dx = px - 0.5f * len, dy = py - 0.5f;
                    float r = (float)Math.Sqrt(dx * dx + dy * dy);
                    first = 3;
                    second = 2;
                    t1 = Inside(r - 0.33f, w);
                    t2 = Inside(r - 0.3f, w);
                    break;
                }

                case FlagPattern.HoistTriangle:
                {
                    // Its two edges meet at the apex; their outward normals along (1, -len) and (1, len).
                    float n = (float)Math.Sqrt(1f + len * len);
                    float nx = 1f / n, ny = len / n;
                    float ax = px - 0.5f * len, ay = py - 0.5f;
                    float tri = Math.Max(ax * nx - ay * ny, ax * nx + ay * ny);
                    field = 2;
                    second = 3;
                    t1 = Inside(py - 0.5f, w);
                    t2 = Inside(tri, w);
                    break;
                }

                case FlagPattern.CentreBand:
                    field = 3;
                    first = 2;
                    t1 = Inside(py - 0.75f, w);
                    t2 = Inside(py - 0.25f, w);
                    break;

                case FlagPattern.DiagonalBand:
                {
                    float n = (float)Math.Sqrt(1f + len * len);
                    float diag = (px + (py - 1f) * len) / n;
                    field = 2;
                    second = 3;
                    t1 = Inside(diag, w);
                    t2 = Inside(Math.Abs(diag) - 0.12f, w);
                    break;
                }

                case FlagPattern.Border:
                {
                    float frame = Box(px, py, 0.5f * len, 0.5f, 0.5f * len, 0.5f);
                    field = 2;
                    first = 3;
                    t1 = Inside(frame + 0.1f, w);
                    t2 = Inside(frame + 0.13f, w);
                    break;
                }
            }
        }

        private static float Part(uint rgb, int shift)
        {
            return (rgb >> shift) & 0xFF;
        }

        private static uint Colour(int number, uint c1, uint c2, uint c3)
        {
            return number == 3 ? c3 : number == 2 ? c2 : c1;
        }

        /// <summary>
        /// The flag laid in a rectangle of a view, as it is seen -- hoisted from its top, the cloth's
        /// u runs down and its v across (FlagCrop.Map) -- and <paramref name="back"/> round it.
        /// </summary>
        /// <param name="view">The view's pixels, rows from the bottom, <paramref name="vw"/> x <paramref name="vh"/>.</param>
        /// <param name="fx">The flag's left edge, pixels from the view's left; <paramref name="fy"/> its top edge from the view's top; its size.</param>
        /// <param name="ratio">The cloth's hoist over its length (FlagProportion.Ratio), whichever edge is hoisted.</param>
        /// <param name="top">Hoisted from its top (FlagHoist.Top).</param>
        /// <param name="c1">The three colours, 0xRRGGBB in sRGB.</param>
        public static void Render(Color32[] view, int vw, int vh, float fx, float fy, float fw, float fh, int pattern, float ratio, bool top,
                                  uint c1, uint c2, uint c3, Color32 back)
        {
            Render(view, vw, vh, fx, fy, fw, fh, pattern, ratio, top, c1, c2, c3, back, FlagCut.Rectangle, FlagCut.DefaultDepth);
        }

        /// <summary>
        /// The same, cut to <paramref name="cut"/> (FlagCut) at <paramref name="depth"/>: past the
        /// fly's outline <paramref name="back"/> shows, the edge anti-aliased over a pixel.
        /// </summary>
        public static void Render(Color32[] view, int vw, int vh, float fx, float fy, float fw, float fh, int pattern, float ratio, bool top,
                                  uint c1, uint c2, uint c3, Color32 back, int cut, float depth)
        {
            float len = 1f / Math.Max(ratio, 0.05f);
            cut = FlagCut.Sanitise(cut);
            depth = FlagCut.SanitiseDepth(depth);
            bool cutting = cut != FlagCut.Rectangle;
            fw = Math.Max(fw, 1f);
            fh = Math.Max(fh, 1f);

            // The footprint of a pixel, in hoist units, as fwidth(p) gives it to the shader.
            float along = (top ? 1f / fh : 1f / fw) * len;
            float across = top ? 1f / fw : 1f / fh;
            float w = Math.Max(Math.Max(along, across), 1e-4f) * 1.2f;

            // A pixel along the fly, as a share of the length (the cut's edge is drawn over one).
            float du = top ? 1f / fh : 1f / fw;

            for (int row = 0; row < vh; row++)
            {
                float sy = (vh - 1 - row + 0.5f - fy) / fh;
                bool rowIn = sy >= 0f && sy < 1f;
                int o = row * vw;

                for (int x = 0; x < vw; x++)
                {
                    float sx = (x + 0.5f - fx) / fw;
                    if (!rowIn || sx < 0f || sx >= 1f)
                    {
                        view[o + x] = back;
                        continue;
                    }

                    float u = top ? sy : sx, v = top ? sx : sy;
                    float cover = 1f;
                    if (cutting)
                    {
                        cover = 0.5f + (FlagCut.Edge(cut, depth, v) - u) / du;
                        cover = cover < 0f ? 0f : cover > 1f ? 1f : cover;
                        if (cover <= 0f)
                        {
                            view[o + x] = back;
                            continue;
                        }
                    }

                    int field, first, second;
                    float t1, t2;
                    Layers(pattern, u * len, v, len, w, out field, out first, out t1, out second, out t2);

                    uint a = Colour(field, c1, c2, c3), b = Colour(first, c1, c2, c3), c = Colour(second, c1, c2, c3);
                    var cloth = new Color32(Blend(a, b, c, 16, t1, t2), Blend(a, b, c, 8, t1, t2), Blend(a, b, c, 0, t1, t2), 255);
                    view[o + x] = cover >= 1f ? cloth : Mix(back, cloth, cover);
                }
            }
        }

        /// <summary>
        /// <paramref name="t"/> of the way from <paramref name="under"/> to <paramref name="over"/>:
        /// over a clear background (alpha 0) the cloth's colour, its alpha the coverage.
        /// </summary>
        private static Color32 Mix(Color32 under, Color32 over, float t)
        {
            if (under.a == 0)
                return new Color32(over.r, over.g, over.b, (byte)(255f * t + 0.5f));

            return new Color32((byte)(under.r + (over.r - under.r) * t + 0.5f), (byte)(under.g + (over.g - under.g) * t + 0.5f),
                               (byte)(under.b + (over.b - under.b) * t + 0.5f), (byte)(under.a + (over.a - under.a) * t + 0.5f));
        }

        private static byte Blend(uint a, uint b, uint c, int shift, float t1, float t2)
        {
            float under = Part(a, shift) + (Part(b, shift) - Part(a, shift)) * t1;
            return (byte)(under + (Part(c, shift) - under) * t2 + 0.5f);
        }
    }
}
