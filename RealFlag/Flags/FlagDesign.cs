using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RealFlag.Flags
{
    /// <summary>
    /// The patterns a flag can have, drawn by FlagCloth.shader from its three colours. Stored by
    /// VALUE (in the save and the settings file): APPEND ONLY, never renumber.
    /// </summary>
    /// <remarks>
    /// The layouts of the world's flags, as vexillology names them (W. Smith 1975, Flags Through
    /// the Ages and Across the World; the proportions of each part from the flags that made the
    /// layout famous). Colour 1 is always the field (the background), so a plain flag is colour 1.
    /// </remarks>
    public static class FlagPattern
    {
        public const int Plain = 0;
        public const int HorizontalBicolour = 1;
        public const int HorizontalTricolour = 2;
        public const int VerticalTricolour = 3;
        public const int VerticalBicolour = 4;
        public const int NordicCross = 5;
        public const int Cross = 6;
        public const int Saltire = 7;
        public const int Canton = 8;
        public const int StripesAndCanton = 9;
        public const int Disc = 10;
        public const int HoistTriangle = 11;
        public const int CentreBand = 12;
        public const int DiagonalBand = 13;
        public const int Border = 14;

        /// <summary>The panel's order (by value, but grouped as the eye reads them).</summary>
        public static readonly int[] Values =
        {
            Plain, HorizontalBicolour, HorizontalTricolour, CentreBand, VerticalBicolour, VerticalTricolour,
            NordicCross, Cross, Saltire, Canton, StripesAndCanton, Disc, HoistTriangle, DiagonalBand, Border,
        };

        public static readonly string[] NameKeys =
        {
            "Pattern.Plain", "Pattern.HorizontalBicolour", "Pattern.HorizontalTricolour", "Pattern.CentreBand",
            "Pattern.VerticalBicolour", "Pattern.VerticalTricolour", "Pattern.NordicCross", "Pattern.Cross",
            "Pattern.Saltire", "Pattern.Canton", "Pattern.StripesAndCanton", "Pattern.Disc", "Pattern.HoistTriangle",
            "Pattern.DiagonalBand", "Pattern.Border",
        };

        public const int Default = NordicCross;

        /// <summary>How many of the three colours the pattern shows (1..3): the panel greys out the rest.</summary>
        public static int ColoursUsed(int pattern)
        {
            switch (pattern)
            {
                case Plain: return 1;
                case HorizontalBicolour:
                case VerticalBicolour:
                    return 2;
                default:
                    return 3;
            }
        }

        public static bool IsKnown(int pattern)
        {
            return pattern >= Plain && pattern <= Border;
        }

        /// <summary>For the log: the English name of a stored value.</summary>
        public static string LogName(int pattern)
        {
            for (int i = 0; i < Values.Length; i++)
            {
                if (Values[i] == pattern)
                    return NameKeys[i].Substring("Pattern.".Length);
            }

            return "#" + pattern;
        }
    }

    /// <summary>
    /// A flag's proportions, height : length (the hoist : the fly), stored by VALUE as height /
    /// length x 1000, any value from <see cref="FlagSize.MinProportion"/> to
    /// <see cref="FlagSize.MaxProportion"/> (the Width and Height sliders set any). The names of
    /// the most flown: 2:3 (most of Europe, much of the world), 1:2 (the United Kingdom, Canada),
    /// 3:5, 10:19 (the United States), 1:1.
    /// </summary>
    public static class FlagProportion
    {
        public static readonly int[] Values = { 1000, 750, 667, 625, 600, 571, 526, 500 };
        public static readonly string[] Names = { "1:1", "3:4", "2:3", "5:8", "3:5", "4:7", "10:19", "1:2" };
        public const int Default = 667;

        /// <summary>
        /// The panel's shapes, as the flag is SEEN hanging (its height over its width x 1000), from
        /// the tallest to the widest: a banner's 3:1, 2:1 and 3:2, the square, then the flags' 2:3,
        /// 3:5 and 1:2.
        /// </summary>
        public static readonly int[] Shapes = { 3000, 2000, 1500, 1000, 667, 600, 500 };

        public static float Ratio(int value)
        {
            return FlagSize.Ratio(value);
        }

        public static string Name(int value)
        {
            for (int i = 0; i < Values.Length; i++)
            {
                if (Values[i] == value)
                    return Names[i];
            }

            var inv = System.Globalization.CultureInfo.InvariantCulture;

            // Taller than long (a flag hung upright): its small numbers where it has them -- 3:2,
            // 2:1, 3:1 -- or "1.22:1" (Nepal's own).
            if (value > 1000)
            {
                for (int b = 1; b <= 4; b++)
                {
                    int a = (int)Math.Round(value * b / 1000.0);
                    if (Math.Abs(a * 1000.0 / b - value) <= 0.002 * value)
                        return a + ":" + b;
                }

                return Ratio(value).ToString("0.##", inv) + ":1";
            }

            // Off the list (a country's own, as its image has it): "1:2.55" for Qatar's 11:28 --
            // the image's rounded pixels do not give the official fraction back.
            return "1:" + (1f / Ratio(value)).ToString("0.##", inv);
        }
    }

    /// <summary>
    /// Which edge of a flag is hoisted -- held on the pole or the halyard: its SIDE, as flags fly,
    /// or its TOP, as a banner hangs from a crossbar or an arm. Stored by VALUE in the save and
    /// the settings file: APPEND ONLY.
    /// </summary>
    /// <remarks>
    /// Hoisted from the top, the hoist runs across along the flag's heading and the fly hangs
    /// down; the design keeps its hoist edge on the hoist, so it shows turned the way a flag hung
    /// upright is shown -- its canton at the top on the left, to the flag's own right (the United
    /// States Flag Code, 4 U.S.C. 7(i): "When displayed either horizontally or vertically against
    /// a wall, the union should be uppermost and to the flag's own right, that is, to the
    /// observer's left").
    /// </remarks>
    public static class FlagHoist
    {
        public const int Side = 0;
        public const int Top = 1;

        public static readonly int[] Values = { Side, Top };
        public static readonly string[] NameKeys = { "Hoist.Side", "Hoist.Top" };

        public static bool IsKnown(int value)
        {
            return value == Side || value == Top;
        }

        public static string LogName(int value)
        {
            return value == Top ? "top" : "side";
        }
    }

    /// <summary>
    /// Whether a flag moves: stored by VALUE in the save and the settings file, APPEND ONLY.
    /// Waves: the cloth flies in the wind (the default). Still: it keeps one shape, caught once as
    /// it flew in a breeze along its heading (a banner hoisted from its top hangs), and never moves
    /// again. Flat: held flat, as a flag on a wall or a banner in its frame. A still or flat flag
    /// is never stepped: it costs the cloth's threads nothing once made (StillPose).
    /// </summary>
    /// <remarks>
    /// The author: "Some flags should be also static" -- asked whether How wavy at 0% should be
    /// that or an option of its own, and left to Claude. Its own: How wavy at 0% already means a
    /// stiff calm cloth that still flies (saves hold it), a still flag needs a shape to hold that
    /// a slider cannot choose, and the slider stays meaningful for a still flag (how rippled the
    /// shape it keeps).
    /// </remarks>
    public static class FlagMotion
    {
        public const int Waves = 0;
        public const int Still = 1;
        public const int Flat = 2;

        public static readonly int[] Values = { Waves, Still, Flat };
        public static readonly string[] NameKeys = { "Motion.Waves", "Motion.Still", "Motion.Flat" };

        public static bool IsKnown(int value)
        {
            return value >= Waves && value <= Flat;
        }

        public static string LogName(int value)
        {
            return value == Still ? "still" : value == Flat ? "flat" : "waving";
        }
    }

    /// <summary>
    /// A flag's size as it is SEEN hanging -- its width across and its height down -- against the
    /// cloth's own: its length from the hoist to the fly and its proportion (hoist / length x
    /// 1000). Hoisted from its side, the width is the length; from its top, the hoist runs across
    /// and the length hangs down. PURE: the panel's Width, Height and Shape rows, and the tests.
    /// </summary>
    /// <remarks>
    /// The author: "the user should now be able to control both width and height, but by default
    /// the aspect is locked, so if they change Width the height changes too". Linked, a new width
    /// or height scales the whole flag (its proportion exactly kept); unlinked, it changes that side
    /// alone, and the other follows only as far as the proportions must stay within 1:5 to 5:1. A
    /// shape keeps the flag's longer side.
    /// </remarks>
    public static class FlagSize
    {
        /// <summary>Each side's least and most, metres (a flag's length's: FlagSpec.MinLength, MaxLength).</summary>
        public const float Min = 0.5f;
        public const float Max = 30f;

        /// <summary>The proportions a flag may have, height / length x 1000: 1:5 to 5:1.</summary>
        public const int MinProportion = 200;
        public const int MaxProportion = 5000;

        /// <summary>The widest a flag may be for its height, and the tallest for its width.</summary>
        private const float MostStretched = MaxProportion / 1000f;

        /// <summary>Height / length, the proportion pulled into range.</summary>
        public static float Ratio(int proportion)
        {
            return ClampProportion(proportion) / 1000f;
        }

        public static int ClampProportion(int proportion)
        {
            return Math.Max(MinProportion, Math.Min(proportion, MaxProportion));
        }

        /// <summary>How wide and how tall the flag is seen, metres.</summary>
        public static void Shown(float length, int proportion, int hoisted, out float width, out float height)
        {
            float hoist = length * Ratio(proportion);
            if (hoisted == FlagHoist.Top)
            {
                width = hoist;
                height = length;
            }
            else
            {
                width = length;
                height = hoist;
            }
        }

        /// <summary>Its shape as seen: height over width x 1000 (what the panel's shapes are).</summary>
        public static int ShownProportion(int proportion, int hoisted)
        {
            proportion = ClampProportion(proportion);
            return hoisted == FlagHoist.Top ? (int)Math.Round(1000000.0 / proportion) : proportion;
        }

        /// <summary>The cloth's proportion for a shape as seen (height over width x 1000).</summary>
        public static int ClothProportion(int shown, int hoisted)
        {
            shown = ClampProportion(shown);
            return ClampProportion(hoisted == FlagHoist.Top ? (int)Math.Round(1000000.0 / shown) : shown);
        }

        /// <summary>The cloth for a flag seen this wide and this tall: its length and proportion, each side within <see cref="Min"/> to <see cref="Max"/>.</summary>
        public static void FromShown(float width, float height, int hoisted, out float length, out int proportion)
        {
            bool top = hoisted == FlagHoist.Top;
            float fly = Clamp(top ? height : width, Min, Max);
            float along = Clamp(top ? width : height, Min, Max);
            length = fly;
            proportion = ClampProportion((int)Math.Round(1000.0 * along / fly));
        }

        /// <summary>
        /// A new width (or, <paramref name="height"/> true, a new height) for the flag: linked, the
        /// whole flag scaled to it, as far as both sides stay within <see cref="Min"/> to
        /// <see cref="Max"/>; otherwise that side alone, the other pushed only as far as the
        /// proportions must stay within 1:5 to 5:1. The cloth's length and proportion after it.
        /// </summary>
        public static void Resize(float length, int proportion, int hoisted, bool height, float value, bool linked,
                                  out float newLength, out int newProportion)
        {
            float w, h;
            Shown(length, proportion, hoisted, out w, out h);
            value = Clamp(value, Min, Max);

            if (linked)
            {
                float current = Math.Max(height ? h : w, 1e-4f);
                float smaller = Math.Max(Math.Min(w, h), 1e-4f), larger = Math.Max(Math.Max(w, h), 1e-4f);
                float scale = Clamp(value / current, Min / smaller, Max / larger);
                newLength = length * scale;
                newProportion = ClampProportion(proportion);
                return;
            }

            float nw = height ? w : value, nh = height ? value : h;
            if (height)
                nw = Clamp(nw, Math.Max(Min, nh / MostStretched), Math.Min(Max, nh * MostStretched));
            else
                nh = Clamp(nh, Math.Max(Min, nw / MostStretched), Math.Min(Max, nw * MostStretched));

            FromShown(nw, nh, hoisted, out newLength, out newProportion);
        }

        /// <summary>
        /// A shape as seen (height over width x 1000) for the flag, its longer side kept (made
        /// longer only if the shorter would be under <see cref="Min"/>). The cloth's length and
        /// proportion after it.
        /// </summary>
        public static void Reshape(float length, int proportion, int hoisted, int shown, out float newLength, out int newProportion)
        {
            float w, h;
            Shown(length, proportion, hoisted, out w, out h);
            float r = Ratio(shown);
            float longer = Clamp(Math.Max(w, h), Min, Max);
            float nw, nh;
            if (r >= 1f)
            {
                nh = longer;
                nw = longer / r;
            }
            else
            {
                nw = longer;
                nh = longer * r;
            }

            float shorter = Math.Min(nw, nh);
            if (shorter < Min)
            {
                float s = Min / shorter;
                nw *= s;
                nh *= s;
            }

            newProportion = ClothProportion(shown, hoisted);
            newLength = Clamp(hoisted == FlagHoist.Top ? nh : nw, Min, Max);
        }

        /// <summary>The shape as seen is this one (within half a percent: a proportion is kept in thousandths, and turned for a flag hoisted from its top).</summary>
        public static bool IsShape(int proportion, int hoisted, int shape)
        {
            int shown = ShownProportion(proportion, hoisted);
            return Math.Abs(shown - shape) <= 0.005 * shape;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value))
                return min;
            return value < min ? min : value > max ? max : value;
        }
    }

    /// <summary>
    /// The colours the picker offers first: the ones the world's flags are made of (sRGB, as the
    /// usual published specifications give them). Any colour can be set besides.
    /// </summary>
    public static class FlagColours
    {
        public static readonly uint[] Palette =
        {
            0xFFFFFF, // white
            0xC8102E, // flag red (Pantone 186)
            0x8D1B3D, // maroon (Qatar)
            0xFF8200, // orange (Ireland's orange, Pantone 151)
            0xFFCD00, // yellow (Pantone 116)
            0x009A44, // green (Pantone 355)
            0x00553A, // dark green
            0x75AADB, // sky blue (Argentina)
            0x0072CE, // blue (Pantone 285)
            0x002664, // navy (Pantone 281)
            0x5E2A84, // purple
            0x7B4A12, // brown
            0x8A8D8F, // grey
            0x000000, // black
        };

        public const uint DefaultColour1 = 0x002664;   // navy field
        public const uint DefaultColour2 = 0xFFFFFF;   // white cross
        public const uint DefaultColour3 = 0xFFFFFF;   // white centre (no fimbriation)

        public static uint Clamp(long rgb)
        {
            return (uint)(rgb & 0xFFFFFF);
        }

        public static string Hex(uint rgb)
        {
            return "#" + (rgb & 0xFFFFFF).ToString("X6", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>"#1a2B3c", "1A2B3C", "0x1a2b3c": six hex digits, any case. False otherwise.</summary>
        public static bool TryParseHex(string text, out uint rgb)
        {
            rgb = 0;
            if (text == null)
                return false;

            string t = text.Trim();
            if (t.StartsWith("#"))
                t = t.Substring(1);
            else if (t.StartsWith("0x") || t.StartsWith("0X"))
                t = t.Substring(2);

            if (t.Length != 6)
                return false;

            uint value;
            if (!uint.TryParse(t, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out value))
                return false;

            rgb = value & 0xFFFFFF;
            return true;
        }

        /// <summary>
        /// The first colour code in any text a player might paste: "#RRGGBB" or "RRGGBB" alone,
        /// "0xRRGGBB", CSS's short "#RGB" (each digit doubled), or a "#RRGGBB" / "#RGB" inside
        /// other text ("color: #1a2b3c;"). Case does not matter.
        /// </summary>
        public static bool FindHex(string text, out uint rgb)
        {
            rgb = 0;
            if (text == null)
                return false;

            if (TryParseHex(text, out rgb))
                return true;

            string t = text.Trim();
            if (t.Length == 3 && HexDigits(t, 0) == 3)
                return Short(t, 0, out rgb);

            for (int i = t.IndexOf('#'); i >= 0 && i < t.Length; i = t.IndexOf('#', i + 1))
            {
                int digits = HexDigits(t, i + 1);
                if (digits == 6)
                    return TryParseHex(t.Substring(i + 1, 6), out rgb);
                if (digits == 3)
                    return Short(t, i + 1, out rgb);
            }

            return false;
        }

        /// <summary>How many hex digits run from <paramref name="start"/>.</summary>
        private static int HexDigits(string t, int start)
        {
            int n = 0;
            while (start + n < t.Length)
            {
                char c = t[start + n];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    break;
                n++;
            }

            return n;
        }

        /// <summary>"#RGB": each digit doubled.</summary>
        private static bool Short(string t, int start, out uint rgb)
        {
            string full = new string(new[] { t[start], t[start], t[start + 1], t[start + 1], t[start + 2], t[start + 2] });
            return TryParseHex(full, out rgb);
        }

        /// <summary>sRGB 0..255 channels of a packed colour.</summary>
        public static void Channels(uint rgb, out int r, out int g, out int b)
        {
            r = (int)((rgb >> 16) & 0xFF);
            g = (int)((rgb >> 8) & 0xFF);
            b = (int)(rgb & 0xFF);
        }

        public static uint Pack(int r, int g, int b)
        {
            r = Math.Max(0, Math.Min(r, 255));
            g = Math.Max(0, Math.Min(g, 255));
            b = Math.Max(0, Math.Min(b, 255));
            return (uint)((r << 16) | (g << 8) | b);
        }

        /// <summary>Hue 0..360, saturation and value 0..1 (the picker's sliders) of a packed colour.</summary>
        public static void ToHsv(uint rgb, out float h, out float s, out float v)
        {
            int ri, gi, bi;
            Channels(rgb, out ri, out gi, out bi);
            float r = ri / 255f, g = gi / 255f, b = bi / 255f;
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            float d = max - min;
            v = max;
            s = max <= 0f ? 0f : d / max;

            if (d <= 0f)
                h = 0f;
            else if (max == r)
                h = 60f * (((g - b) / d) % 6f);
            else if (max == g)
                h = 60f * ((b - r) / d + 2f);
            else
                h = 60f * ((r - g) / d + 4f);

            if (h < 0f)
                h += 360f;
        }

        public static uint FromHsv(float h, float s, float v)
        {
            h = ((h % 360f) + 360f) % 360f;
            s = Math.Max(0f, Math.Min(s, 1f));
            v = Math.Max(0f, Math.Min(v, 1f));

            float c = v * s;
            float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
            float m = v - c;
            float r, g, b;

            if (h < 60f) { r = c; g = x; b = 0f; }
            else if (h < 120f) { r = x; g = c; b = 0f; }
            else if (h < 180f) { r = 0f; g = c; b = x; }
            else if (h < 240f) { r = 0f; g = x; b = c; }
            else if (h < 300f) { r = x; g = 0f; b = c; }
            else { r = c; g = 0f; b = x; }

            return Pack((int)Math.Round((r + m) * 255f), (int)Math.Round((g + m) * 255f), (int)Math.Round((b + m) * 255f));
        }

        /// <summary>sRGB 0..1 to linear light (IEC 61966-2-1), for the shader in the game's linear colour space.</summary>
        public static float ToLinear(float c)
        {
            return c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055f) / 1.055f, 2.4);
        }
    }

    /// <summary>
    /// Everything that makes one flag what it is, besides where it stands and which way it
    /// faces (those are its prop's: MoveIt and the panel move the prop).
    /// </summary>
    public struct FlagSpec
    {
        /// <summary>A <see cref="FlagPattern"/> value.</summary>
        public int Pattern;

        /// <summary>sRGB 0xRRGGBB: the field, and the pattern's second and third colours.</summary>
        public uint Colour1, Colour2, Colour3;

        /// <summary>Hoist to fly, metres.</summary>
        public float Length;

        /// <summary>Height (the hoist) / length x 1000, <see cref="FlagSize.MinProportion"/> to <see cref="FlagSize.MaxProportion"/> (<see cref="FlagProportion"/>).</summary>
        public int Proportion;

        /// <summary>
        /// A <see cref="FlagHoist"/> value: the edge held on the pole -- the side (the default, as
        /// flags fly) or the top (a banner hanging from a crossbar). The hoist's line and the fly
        /// at rest trade places (FlagOrientation); the cloth is the same.
        /// </summary>
        public int HoistedFrom;

        /// <summary>How much the cloth ripples, 0..2 (100%: ordinary flag cloth).</summary>
        public float Waviness;

        /// <summary>A <see cref="FlagMotion"/> value: the cloth waves in the wind, or keeps one shape (still, or flat).</summary>
        public int Motion;

        /// <summary>The name the Existing flags list shows; empty for "Flag n".</summary>
        public string Name;

        /// <summary>
        /// A country's code (FlagCountries: "fr", "gb-sct", "eu"): the flag is that country's
        /// image, at its proportions, instead of the pattern and colours (which are kept, for a
        /// change back). Empty for a pattern flag. A code this version does not know is kept
        /// and drawn as the pattern.
        /// </summary>
        public string Country;

        /// <summary>
        /// A <see cref="FlagPoleStyle"/> value: the pole the mod draws under the flag, by its top
        /// (a ball, a flat cap, a spear), or none -- the flag is then for any pole of the city's
        /// own (the mod's purpose, so none is the default).
        /// </summary>
        public int Pole;

        /// <summary>The halyard's lines drawn beside the pole: 0, 1 (the one the flag is clipped to) or 2 (both sides of the loop).</summary>
        public int Ropes;

        /// <summary>sRGB 0xRRGGBB: the pole (and its truck and foot), its top, its ropes.</summary>
        public uint PoleColour, TopColour, RopeColour;

        /// <summary>The pole's thickness against a real one's for this flag and height (FlagPoleShape), 0.5..2.</summary>
        public float PoleThickness;

        /// <summary>
        /// The drawn pole's length along its axis, from the flag's top down to its foot, metres --
        /// part of the flag, so Move It raising or lowering it raises or lowers the pole with it
        /// (the author: "Elevating or lowering the flag pole with MoveIt! should NOT change its
        /// height ... It should only elevate the whole thing up or down"). 0: not known yet (a
        /// flag just placed, an older save, a new tilt): the renderer finds what stands under it
        /// down the axis once, and keeps that. The panel's Elevation keeps the foot where it is
        /// (the pole longer or shorter); "Pole down to the ground" finds the ground again.
        /// </summary>
        public float PoleLength;

        /// <summary>
        /// The hoist's lean, degrees, -90..90 (FlagOrientation): <see cref="Tilt"/> turns the top of
        /// the pole towards the fly (+) or away from it, <see cref="Lean"/> to the side. Both 0: the
        /// hoist hangs straight down from the anchor, as it always did. A wall-mounted pole is a
        /// tilt of 30 to 45 degrees, a horizontal one 90.
        /// </summary>
        public float Tilt, Lean;

        /// <summary>
        /// How much darker the cloth is than its design, 0..<see cref="MaxDarkness"/> (0: the
        /// colours as designed): for a flag whose white or yellow glares in a bright sun (the
        /// author: "some country flags are too bright"). See <see cref="Brightness"/>.
        /// </summary>
        public float Darkness;

        /// <summary>
        /// A picture of the player's own: a file in the images folder, FlagMaker\Images
        /// (FlagImageFolder), by its NAME ("castle.png"). Empty for none. Drawn over the pattern,
        /// never stretched (FlagCrop); a country's flag goes before it. A name whose file is not
        /// there is kept and drawn as the pattern, until the file is.
        /// </summary>
        public string Image;

        /// <summary>
        /// Where the flag's middle is on the picture as it stands after its turn: across from its
        /// left edge and down from its top, 0..1 (FlagCrop.ClampCentre keeps it where the flag
        /// shows picture only, or the picture whole).
        /// </summary>
        public float ImageX, ImageY;

        /// <summary>1: the picture just fills the flag; more, a part of it; less, all of it with the flag's pattern round it (FlagCrop.MinZoom..MaxZoom).</summary>
        public float ImageZoom;

        /// <summary>The picture turned this many quarter turns clockwise, 0..3.</summary>
        public int ImageTurn;

        /// <summary>
        /// A <see cref="FlagImageAlpha"/> value: the picture's see-through parts cut out of the
        /// flag (a shaped flag, as Nepal's) or showing the flag's own pattern (a logo on it).
        /// </summary>
        public int ImageAlpha;

        /// <summary>
        /// How worn the cloth is, 0..1 (0: new): its dyes faded by the sun and its fly end frayed,
        /// torn into strips at the most (FlagWear.cginc). Its look only: the cloth moves as a new one.
        /// </summary>
        public float Wear;

        /// <summary>How dirty, 0..1: grime over it, darker at the hoist and the lower edge, rain's streaks down it.</summary>
        public float Dirt;

        /// <summary>
        /// How holed, 0..1: bullet holes, burns and tears scattered over it, more of them the higher
        /// (each flag its own scatter: FlagWear.SeedFor its prop). Cut out as a picture's
        /// see-through parts are, in its shadow too.
        /// </summary>
        public float Holes;

        /// <summary>
        /// How torn, 0..1: the fly end ripped away in ragged tongues with V-shaped gaps between them
        /// (a pirate's flag), the fly corners bitten off, and past half, pieces missing from the cloth
        /// itself. The hoist's end always stays. Its look only, as <see cref="Wear"/>.
        /// </summary>
        public float Tear;

        /// <summary>
        /// How much of the wind where it stands the flag feels, 0..1 (1: all of it, the default) --
        /// the panel's "Wind on it" (the author, 2026-11-07: a slider for how much the flag moves,
        /// beside How wavy, which shapes its ripples). A banner hanging against a facade stands in
        /// the building's shelter, where the air is slower than in the open (EN 1991-1-4 7.4.2's
        /// shelter factor for walls and fences, the idea): at a quarter of a breeze it mostly hangs
        /// and sways. A waving flag's only: a still flag's shape is made in its own breeze
        /// (StillPose), a flat one has none.
        /// </summary>
        public float WindShare;

        /// <summary>
        /// How torn its top and bottom edges are, 0..1 (the panel's "Torn edges", the author,
        /// 2026-11-07: "right now the torn only goes on one side ... a secondary tear slider"):
        /// ripped in stretches, deeper towards the fly, with rips running in across the cloth, one
        /// edge worse than the other (each flag's own). Its look only, as <see cref="Tear"/>.
        /// </summary>
        public float TearEdges;

        /// <summary>
        /// The flag's cut, a FlagCut value (the author, 2026-11-07: "the only shapes we have are
        /// square/rectangle, but let's add more types like fanion, swallowtail, and more"): the
        /// outline of its fly. The CLOTH takes it -- what is cut away is not stepped and catches no
        /// air (FlagCloth's cells), so a swallowtail's tails fly apart and a pennant's point whips --
        /// and the shader cuts its exact edge.
        /// </summary>
        public int Cut;

        /// <summary>How deep the cut's notch or point is, a share of the length (<see cref="FlagCut.MinDepth"/> .. <see cref="FlagCut.MaxDepth"/>; the rectangle and the pennant have none).</summary>
        public float CutDepth;

        public const float MinLength = 0.5f;
        public const float MaxLength = 30f;
        public const float MinPoleThickness = 0.5f;
        public const float MaxPoleThickness = 2f;
        public const float MaxTilt = 90f;

        /// <summary>The longest pole kept, metres (a flag 300 m up on its own pole; the Elevation slider's most).</summary>
        public const float MaxPoleLength = 1000f;

        /// <summary>The darkest the panel offers: 80%, the design's white at a fifth of its sRGB value, a dark grey.</summary>
        public const float MaxDarkness = 0.8f;

        public const uint DefaultPoleColour = 0xE4E5E2;   // white, as most town poles are painted
        public const uint DefaultTopColour = 0xD4AF37;    // gilt ball
        public const uint DefaultRopeColour = 0xF2EFE6;   // white braided polyester
        public const int DefaultRopes = 2;

        /// <summary>A country's flag, not a pattern.</summary>
        public bool IsCountry
        {
            get { return !string.IsNullOrEmpty(Country); }
        }

        /// <summary>A picture of the player's on the cloth (a country's flag goes before it).</summary>
        public bool HasImage
        {
            get { return !IsCountry && !string.IsNullOrEmpty(Image); }
        }

        public float Height
        {
            get { return Length * FlagProportion.Ratio(Proportion); }
        }

        /// <summary>
        /// What the cloth reflects against its design's (linear) colours, 0..1: the colours as if
        /// their sRGB values were (1 - <see cref="Darkness"/>) of theirs -- (1 - d)^2.2, the sRGB
        /// curve's overall power (IEC 61966-2-1) -- so the slider darkens evenly to the eye: 20% takes
        /// white to sRGB 0.8 (0.61 linear), 50% to mid grey (0.22).
        /// </summary>
        public float Brightness
        {
            get { return (float)Math.Pow(1.0 - Math.Max(0f, Math.Min(Darkness, MaxDarkness)), 2.2); }
        }

        public static FlagSpec Default
        {
            get
            {
                return new FlagSpec
                {
                    Pattern = FlagPattern.Default,
                    Colour1 = FlagColours.DefaultColour1,
                    Colour2 = FlagColours.DefaultColour2,
                    Colour3 = FlagColours.DefaultColour3,
                    Length = 3f,
                    Proportion = FlagProportion.Default,
                    HoistedFrom = FlagHoist.Side,
                    Waviness = 1f,
                    Motion = FlagMotion.Waves,
                    Name = string.Empty,
                    Country = string.Empty,
                    Pole = FlagPoleStyle.None,
                    Ropes = DefaultRopes,
                    PoleColour = DefaultPoleColour,
                    TopColour = DefaultTopColour,
                    RopeColour = DefaultRopeColour,
                    PoleThickness = 1f,
                    PoleLength = 0f,
                    Tilt = 0f,
                    Lean = 0f,
                    Darkness = 0f,
                    Image = string.Empty,
                    ImageX = 0.5f,
                    ImageY = 0.5f,
                    ImageZoom = 1f,
                    ImageTurn = 0,
                    ImageAlpha = FlagImageAlpha.Cut,
                    Wear = 0f,
                    Dirt = 0f,
                    Holes = 0f,
                    Tear = 0f,
                    WindShare = 1f,
                    TearEdges = 0f,
                    Cut = FlagCut.Rectangle,
                    CutDepth = FlagCut.DefaultDepth,
                };
            }
        }

        /// <summary>
        /// A pole is drawn under this flag: one chosen (<see cref="PoleChosen"/>), and the flag
        /// hoisted from its side. A banner hoisted from its top hangs with none -- its crossbar was
        /// too small (the author, 2026-10-22: "we just should not have a pole if it's hoisted from
        /// the top"; a side-hoisted flag turned level is the way to one).
        /// </summary>
        public bool HasPole
        {
            get { return Pole != FlagPoleStyle.None && HoistedFrom != FlagHoist.Top; }
        }

        /// <summary>The player chose the mod's pole (a style), drawn or not: kept through a top hoist, so the pole comes back with the side's.</summary>
        public bool PoleChosen
        {
            get { return Pole != FlagPoleStyle.None; }
        }

        /// <summary>The hoist leans (a tilted pole), rather than hanging straight down.</summary>
        public bool IsTilted
        {
            get { return Tilt != 0f || Lean != 0f; }
        }

        /// <summary>Pulled into range, unknown values replaced: what a save or a MoveIt paste may hold is not trusted.</summary>
        public FlagSpec Sanitised()
        {
            FlagSpec s = this;
            if (!FlagPattern.IsKnown(s.Pattern))
                s.Pattern = FlagPattern.Default;
            s.Colour1 &= 0xFFFFFF;
            s.Colour2 &= 0xFFFFFF;
            s.Colour3 &= 0xFFFFFF;
            if (float.IsNaN(s.Length) || float.IsInfinity(s.Length))
                s.Length = 3f;
            s.Length = Math.Max(MinLength, Math.Min(s.Length, MaxLength));
            if (s.Proportion < FlagSize.MinProportion || s.Proportion > FlagSize.MaxProportion)
                s.Proportion = FlagProportion.Default;
            if (!FlagHoist.IsKnown(s.HoistedFrom))
                s.HoistedFrom = FlagHoist.Side;
            if (float.IsNaN(s.Waviness) || float.IsInfinity(s.Waviness))
                s.Waviness = 1f;
            s.Waviness = Math.Max(0f, Math.Min(s.Waviness, 2f));
            if (!FlagMotion.IsKnown(s.Motion))
                s.Motion = FlagMotion.Waves;
            s.Name = s.Name ?? string.Empty;
            if (s.Name.Length > 64)
                s.Name = s.Name.Substring(0, 64);

            // A country's flag flies at its own proportions, whatever a paste or the panel says.
            s.Country = FlagCountries.Normalise(s.Country);
            FlagCountry country = FlagCountries.Find(s.Country);
            if (country != null)
                s.Proportion = country.Proportion;

            // The hoist no longer than a flag may be long (a tall flag of the longest length).
            float ratio = FlagSize.Ratio(s.Proportion);
            if (s.Length * ratio > MaxLength)
                s.Length = MaxLength / ratio;

            if (!FlagPoleStyle.IsKnown(s.Pole))
                s.Pole = FlagPoleStyle.None;
            s.Ropes = Math.Max(0, Math.Min(s.Ropes, 2));
            s.PoleColour &= 0xFFFFFF;
            s.TopColour &= 0xFFFFFF;
            s.RopeColour &= 0xFFFFFF;
            if (float.IsNaN(s.PoleThickness) || float.IsInfinity(s.PoleThickness))
                s.PoleThickness = 1f;
            s.PoleThickness = Math.Max(MinPoleThickness, Math.Min(s.PoleThickness, MaxPoleThickness));
            if (float.IsNaN(s.PoleLength) || float.IsInfinity(s.PoleLength) || s.PoleLength < 0f)
                s.PoleLength = 0f;
            s.PoleLength = Math.Min(s.PoleLength, MaxPoleLength);
            s.Tilt = Angle(s.Tilt);
            s.Lean = Angle(s.Lean);
            if (float.IsNaN(s.Darkness) || float.IsInfinity(s.Darkness))
                s.Darkness = 0f;
            s.Darkness = Math.Max(0f, Math.Min(s.Darkness, MaxDarkness));

            // A picture: a file name only; none on a country's flag, which goes before it.
            s.Image = s.IsCountry ? string.Empty : FlagImageNames.Sanitise(s.Image);
            s.ImageX = Unit(s.ImageX);
            s.ImageY = Unit(s.ImageY);
            s.ImageZoom = FlagCrop.ClampZoom(s.ImageZoom);
            s.ImageTurn &= 3;
            if (!FlagImageAlpha.IsKnown(s.ImageAlpha))
                s.ImageAlpha = FlagImageAlpha.Cut;
            s.Wear = Share(s.Wear);
            s.Dirt = Share(s.Dirt);
            s.Holes = Share(s.Holes);
            s.Tear = Share(s.Tear);
            s.TearEdges = Share(s.TearEdges);
            s.WindShare = float.IsNaN(s.WindShare) || float.IsInfinity(s.WindShare) ? 1f : Share(s.WindShare);
            s.Cut = FlagCut.Sanitise(s.Cut);
            s.CutDepth = FlagCut.SanitiseDepth(s.CutDepth);
            return s;
        }

        /// <summary>A share 0..1 (NaN and the infinities none).</summary>
        private static float Share(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;
            return Math.Max(0f, Math.Min(value, 1f));
        }

        /// <summary>Worn, dirty or holed at all (the shader's weathering runs).</summary>
        public bool IsWeathered
        {
            get { return Wear > 0f || Dirt > 0f || Holes > 0f || Tear > 0f || TearEdges > 0f; }
        }

        /// <summary>The same wear, tear, dirt and holes (only the property block changes when not).</summary>
        public bool SameWeather(FlagSpec other)
        {
            return Math.Abs(Wear - other.Wear) < 1e-5f && Math.Abs(Dirt - other.Dirt) < 1e-5f && Math.Abs(Holes - other.Holes) < 1e-5f &&
                   Math.Abs(Tear - other.Tear) < 1e-5f && Math.Abs(TearEdges - other.TearEdges) < 1e-5f;
        }

        /// <summary>A place on a picture: finite, and near it (FlagCrop.ClampCentre holds it to the window's own range when it is used).</summary>
        private static float Unit(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0.5f;
            return Math.Max(-8f, Math.Min(value, 9f));
        }

        private static float Angle(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
                return 0f;
            return Math.Max(-MaxTilt, Math.Min(degrees, MaxTilt));
        }

        /// <summary>The shape changed (a new cloth is needed), not just the look: its size, or its cut (the cloth's cells).</summary>
        public bool SameShape(FlagSpec other)
        {
            return Math.Abs(Length - other.Length) < 1e-4f && Proportion == other.Proportion && SameCut(other);
        }

        /// <summary>The same cut: the same outline, the same depth where the cut has one.</summary>
        public bool SameCut(FlagSpec other)
        {
            return Cut == other.Cut && (!FlagCut.UsesDepth(Cut) || Math.Abs(CutDepth - other.CutDepth) < 1e-4f);
        }

        /// <summary>The hoist's line is the same -- the same edge hoisted, the same lean (the cloth need not turn).</summary>
        public bool SameHoist(FlagSpec other)
        {
            return HoistedFrom == other.HoistedFrom && Math.Abs(Tilt - other.Tilt) < 1e-3f && Math.Abs(Lean - other.Lean) < 1e-3f;
        }

        /// <summary>
        /// A still flag keeps the same shape (StillPose): the same size, hoist line, waviness, motion,
        /// and hold on its hoist (sewn on, or clipped to a drawn pole's halyard). Nothing else
        /// shapes it: not the pole's length or thickness (its collider is left out of the pose).
        /// </summary>
        public bool SamePose(FlagSpec other)
        {
            return SameShape(other) && SameHoist(other) && Math.Abs(Waviness - other.Waviness) < 1e-5f && Motion == other.Motion &&
                   OnHalyard == other.OnHalyard;
        }

        /// <summary>Clipped to a drawn pole's halyard by its two grommets (FlagPoleShape.ApplyTo), not held along its whole hoist.</summary>
        public bool OnHalyard
        {
            get { return HasPole && Ropes > 0; }
        }

        /// <summary>The same pole (the same mesh and the same collider for the cloth).</summary>
        public bool SamePole(FlagSpec other)
        {
            return Pole == other.Pole && Ropes == other.Ropes && Math.Abs(PoleThickness - other.PoleThickness) < 1e-4f &&
                   Math.Abs(PoleLength - other.PoleLength) < 1e-3f;
        }

        /// <summary>The same pole colours (only the property block changes).</summary>
        public bool SamePoleColours(FlagSpec other)
        {
            return PoleColour == other.PoleColour && TopColour == other.TopColour && RopeColour == other.RopeColour;
        }

        /// <summary>The same picture, laid the same way (only the property block changes when it does not).</summary>
        public bool SameImage(FlagSpec other)
        {
            return string.Equals(Image ?? string.Empty, other.Image ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                   Math.Abs(ImageX - other.ImageX) < 1e-5f && Math.Abs(ImageY - other.ImageY) < 1e-5f &&
                   Math.Abs(ImageZoom - other.ImageZoom) < 1e-5f && ImageTurn == other.ImageTurn && ImageAlpha == other.ImageAlpha;
        }

        public string Describe()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            FlagCountry country = FlagCountries.Find(Country);
            string design = country != null
                ? country.Name + " (" + country.Code + ")"
                : (IsCountry ? "country '" + Country + "' unknown here, as " : "") + FlagPattern.LogName(Pattern) + " " +
                  FlagColours.Hex(Colour1) + "/" + FlagColours.Hex(Colour2) + "/" + FlagColours.Hex(Colour3);
            if (HasImage)
                design = "image '" + Image + "' (" + (ImageZoom * 100f).ToString("0", inv) + "%" +
                         (ImageTurn != 0 ? ", turned " + (90 * ImageTurn) + " deg" : "") +
                         (Math.Abs(ImageX - 0.5f) > 1e-3f || Math.Abs(ImageY - 0.5f) > 1e-3f
                             ? ", middle at " + ImageX.ToString("0.###", inv) + "/" + ImageY.ToString("0.###", inv)
                             : "") +
                         (ImageAlpha == FlagImageAlpha.Through ? ", the flag through its see-through parts" : "") + ") over " + design;
            string text = design + ", " + Length.ToString("0.##", inv) + " m at " + FlagProportion.Name(Proportion) + ", waviness " +
                          (Waviness * 100f).ToString("F0", inv) + "%";
            if (Cut != FlagCut.Rectangle)
                text += ", cut " + FlagCut.LogName(Cut) + (FlagCut.UsesDepth(Cut) ? " " + (CutDepth * 100f).ToString("F0", inv) + "% deep" : "");
            if (HoistedFrom != FlagHoist.Side)
                text += ", hoisted from its " + FlagHoist.LogName(HoistedFrom);
            if (Motion != FlagMotion.Waves)
                text += ", " + FlagMotion.LogName(Motion);
            if (WindShare < 1f)
                text += ", " + (WindShare * 100f).ToString("F0", inv) + "% of the wind on it";
            if (Darkness > 0f)
                text += ", darkness " + (Darkness * 100f).ToString("F0", inv) + "%";
            if (IsWeathered)
                text += ", wear " + (Wear * 100f).ToString("F0", inv) + "% dirt " + (Dirt * 100f).ToString("F0", inv) + "% holes " +
                        (Holes * 100f).ToString("F0", inv) + "% torn " + (Tear * 100f).ToString("F0", inv) + "% torn edges " +
                        (TearEdges * 100f).ToString("F0", inv) + "%";
            if (HasPole)
                text += ", pole with a " + FlagPoleStyle.LogName(Pole) + " top and " + Ropes + " rope(s)" +
                        (Math.Abs(PoleThickness - 1f) > 1e-3f ? ", " + (PoleThickness * 100f).ToString("F0", inv) + "% thick" : "") +
                        (PoleLength > 0f ? ", " + PoleLength.ToString("0.#", inv) + " m long" : "");
            if (IsTilted)
                text += ", tilted " + Tilt.ToString("0.#", inv) + " deg, leaning " + Lean.ToString("0.#", inv) + " deg";
            return text;
        }
    }

    /// <summary>
    /// The pole the mod can draw under a flag, by its top (the finial): stored by VALUE in the
    /// save, APPEND ONLY. None: no pole (the default; a flag for the city's own poles).
    /// </summary>
    public static class FlagPoleStyle
    {
        public const int None = 0;
        public const int Ball = 1;
        public const int Flat = 2;
        public const int Spear = 3;

        public static readonly int[] Values = { None, Ball, Flat, Spear };
        public static readonly string[] NameKeys = { "Pole.None", "Pole.Ball", "Pole.Flat", "Pole.Spear" };

        public static bool IsKnown(int style)
        {
            return style >= None && style <= Spear;
        }

        public static string LogName(int style)
        {
            return style == Ball ? "ball" : style == Flat ? "flat" : style == Spear ? "spear" : "no";
        }
    }

    /// <summary>
    /// What a player's picture's see-through parts do (FlagSpec.ImageAlpha): stored by VALUE in the
    /// save and the settings file, APPEND ONLY. Cut: they are holes in the flag -- a pennant's or a
    /// swallowtail's PNG makes a shaped flag, as Nepal's country image does (the default, the
    /// author's choice). Through: the flag's own pattern and colours show there -- a logo on a
    /// coloured flag.
    /// </summary>
    public static class FlagImageAlpha
    {
        public const int Cut = 0;
        public const int Through = 1;

        public static readonly int[] Values = { Cut, Through };
        public static readonly string[] NameKeys = { "Image.Cut", "Image.Through" };

        public static bool IsKnown(int value)
        {
            return value == Cut || value == Through;
        }
    }

    /// <summary>
    /// Which scatter of holes, tatters and grime a flag gets (FlagWear.cginc's _RFWearSeed): from its
    /// prop's number, so like flags side by side are not worn alike, and a flag keeps its own through
    /// moves, saves and loads. A whole number 0..255 (the shader's hashes stay exact on small
    /// lattice coordinates). PURE.
    /// </summary>
    public static class FlagWear
    {
        public static float SeedFor(uint prop)
        {
            // Knuth's multiplicative hash (TAOCP 6.4): the top byte of prop x 2654435761.
            return (float)((prop * 2654435761u) >> 24);
        }
    }

    /// <summary>The halyard lines a drawn pole shows: the panel's choices.</summary>
    public static class FlagRopes
    {
        public static readonly int[] Values = { 0, 1, 2 };
        public static readonly string[] NameKeys = { "Ropes.None", "Ropes.One", "Ropes.Two" };
    }

    /// <summary>
    /// The byte form of a flag's spec: in the city's save (keyed by prop), in MoveIt's clipboard
    /// and exports. Versioned; anything that cannot be read is refused whole. PURE.
    /// </summary>
    public static class FlagCodec
    {
        /// <summary>
        /// The format written. 1: the pattern flag (2026-09-25). 2 (2026-09-26): the same, then
        /// the country's code (a byte of length, ASCII). 3 (2026-09-26): the same, then the pole
        /// (its style and ropes as bytes, its three colours, its thickness) and the hoist's tilt
        /// and lean (floats, degrees). 4 (2026-09-26): the same, then the cloth's darkness (a
        /// float, 0..0.8). 5 (2026-09-27): the same, then the edge hoisted (a byte, FlagHoist) --
        /// and the proportion, the same short, may now be any from 1:5 to 5:1. 6 (2026-09-27): the
        /// same, then the drawn pole's length (a float, metres; 0 not known yet). 7 (2026-09-27):
        /// the same, then the player's picture -- its file name (a ushort of length, UTF-8), where
        /// the flag's middle is on it and its zoom (three floats), its turn and what its see-through
        /// parts do (two bytes). 8 (2026-09-27): the same, then whether it moves (a byte,
        /// FlagMotion). 9 (2026-11-03): the same, then its wear, dirt and holes (three floats,
        /// 0..1). 10 (2026-11-04): the same, then how torn (a float, 0..1). 11 (2026-11-07): the
        /// same, then how torn its top and bottom edges are (a float, 0..1; older ones whole). 12:
        /// never written by a commit (WindShare's while it was unpushed; read as 11). 13
        /// (2026-11-07): the same as 11, then its cut (a byte, FlagCut) and the cut's depth (a
        /// float, a share of the length; older ones rectangles). 14 (2026-11-07): the same, then
        /// how much of the wind is on it (a float, 0..1; older ones all of it). Every older one is
        /// still read.
        /// </summary>
        public const byte Version = 14;

        /// <summary>The oldest format still read.</summary>
        public const byte OldestVersion = 1;

        public static void Write(BinaryWriter w, FlagSpec s)
        {
            w.Write((byte)s.Pattern);
            w.Write(s.Colour1);
            w.Write(s.Colour2);
            w.Write(s.Colour3);
            w.Write(s.Length);
            w.Write((short)s.Proportion);
            w.Write(s.Waviness);
            WriteText(w, s.Name);
            WriteText(w, s.Country);
            w.Write((byte)s.Pole);
            w.Write((byte)s.Ropes);
            w.Write(s.PoleColour);
            w.Write(s.TopColour);
            w.Write(s.RopeColour);
            w.Write(s.PoleThickness);
            w.Write(s.Tilt);
            w.Write(s.Lean);
            w.Write(s.Darkness);
            w.Write((byte)s.HoistedFrom);
            w.Write(s.PoleLength);
            WriteLongText(w, s.Image);
            w.Write(s.ImageX);
            w.Write(s.ImageY);
            w.Write(s.ImageZoom);
            w.Write((byte)s.ImageTurn);
            w.Write((byte)s.ImageAlpha);
            w.Write((byte)s.Motion);
            w.Write(s.Wear);
            w.Write(s.Dirt);
            w.Write(s.Holes);
            w.Write(s.Tear);
            w.Write(s.TearEdges);
            w.Write((byte)s.Cut);
            w.Write(s.CutDepth);
            w.Write(s.WindShare);
        }

        private static void WriteText(BinaryWriter w, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            w.Write((byte)Math.Min(bytes.Length, 255));
            w.Write(bytes, 0, Math.Min(bytes.Length, 255));
        }

        private static string ReadText(BinaryReader r)
        {
            int length = r.ReadByte();
            byte[] bytes = r.ReadBytes(length);
            if (bytes.Length != length)
                throw new EndOfStreamException("a text runs past the data");
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>A text whose UTF-8 may pass 255 bytes (a file name in any script): a ushort of length.</summary>
        private static void WriteLongText(BinaryWriter w, string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            int length = Math.Min(bytes.Length, 4096);
            w.Write((ushort)length);
            w.Write(bytes, 0, length);
        }

        private static string ReadLongText(BinaryReader r)
        {
            int length = r.ReadUInt16();
            // IOException, not InvalidDataException: Unity's 2.0 subset has no such class.
            if (length > 4096)
                throw new IOException("a text of " + length + " bytes");
            byte[] bytes = r.ReadBytes(length);
            if (bytes.Length != length)
                throw new EndOfStreamException("a text runs past the data");
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>One spec in the given format (a version this code knows: <see cref="OldestVersion"/> to <see cref="Version"/>).</summary>
        public static FlagSpec Read(BinaryReader r, byte version)
        {
            var s = new FlagSpec
            {
                Pattern = r.ReadByte(),
                Colour1 = r.ReadUInt32(),
                Colour2 = r.ReadUInt32(),
                Colour3 = r.ReadUInt32(),
                Length = r.ReadSingle(),
                Proportion = r.ReadInt16(),
                Waviness = r.ReadSingle(),
            };
            s.Name = ReadText(r);
            s.Country = version >= 2 ? ReadText(r) : string.Empty;

            FlagSpec d = FlagSpec.Default;
            if (version >= 3)
            {
                s.Pole = r.ReadByte();
                s.Ropes = r.ReadByte();
                s.PoleColour = r.ReadUInt32();
                s.TopColour = r.ReadUInt32();
                s.RopeColour = r.ReadUInt32();
                s.PoleThickness = r.ReadSingle();
                s.Tilt = r.ReadSingle();
                s.Lean = r.ReadSingle();
            }
            else
            {
                // Before poles and tilts: none, straight down.
                s.Pole = d.Pole;
                s.Ropes = d.Ropes;
                s.PoleColour = d.PoleColour;
                s.TopColour = d.TopColour;
                s.RopeColour = d.RopeColour;
                s.PoleThickness = d.PoleThickness;
            }

            // Before the darkness: the colours as designed.
            s.Darkness = version >= 4 ? r.ReadSingle() : 0f;

            // Before the top was hoisted: every flag from its side.
            s.HoistedFrom = version >= 5 ? r.ReadByte() : FlagHoist.Side;

            // Before the pole kept its length: not known -- found down its axis once, then kept.
            s.PoleLength = version >= 6 ? r.ReadSingle() : 0f;

            // Before the player's pictures: none, laid as a new one is.
            if (version >= 7)
            {
                s.Image = ReadLongText(r);
                s.ImageX = r.ReadSingle();
                s.ImageY = r.ReadSingle();
                s.ImageZoom = r.ReadSingle();
                s.ImageTurn = r.ReadByte();
                s.ImageAlpha = r.ReadByte();
            }
            else
            {
                s.Image = d.Image;
                s.ImageX = d.ImageX;
                s.ImageY = d.ImageY;
                s.ImageZoom = d.ImageZoom;
                s.ImageTurn = d.ImageTurn;
                s.ImageAlpha = d.ImageAlpha;
            }

            // Before still flags: every flag waves.
            s.Motion = version >= 8 ? r.ReadByte() : FlagMotion.Waves;

            // Before wear and tear: as new.
            if (version >= 9)
            {
                s.Wear = r.ReadSingle();
                s.Dirt = r.ReadSingle();
                s.Holes = r.ReadSingle();
            }

            // Before torn flags: whole.
            s.Tear = version >= 10 ? r.ReadSingle() : 0f;

            // Before torn edges: whole.
            s.TearEdges = version >= 11 ? r.ReadSingle() : 0f;

            // Before cuts: a rectangle.
            if (version >= 13)
            {
                s.Cut = r.ReadByte();
                s.CutDepth = r.ReadSingle();
            }
            else
            {
                s.Cut = FlagCut.Rectangle;
                s.CutDepth = FlagCut.DefaultDepth;
            }

            // Before the wind's share: all of it.
            s.WindShare = version >= 14 ? r.ReadSingle() : 1f;

            return s.Sanitised();
        }

        private static bool Known(byte version)
        {
            return version >= OldestVersion && version <= Version;
        }

        /// <summary>One spec, with the version in front (MoveIt's clipboard).</summary>
        public static byte[] EncodeOne(FlagSpec spec)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Version);
                Write(w, spec);
                return stream.ToArray();
            }
        }

        public static bool TryDecodeOne(byte[] data, out FlagSpec spec)
        {
            spec = FlagSpec.Default;
            if (data == null || data.Length < 2)
                return false;

            try
            {
                using (var stream = new MemoryStream(data))
                using (var r = new BinaryReader(stream))
                {
                    byte version = r.ReadByte();
                    if (!Known(version))
                        return false;

                    spec = Read(r, version);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The city's flags, by prop index.</summary>
        public static byte[] EncodeAll(IList<KeyValuePair<uint, FlagSpec>> flags)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Version);
                w.Write(flags.Count);
                foreach (KeyValuePair<uint, FlagSpec> f in flags)
                {
                    w.Write(f.Key);
                    Write(w, f.Value);
                }

                return stream.ToArray();
            }
        }

        /// <summary>Null (and why) when the bytes are not a list of flags this version can read.</summary>
        public static List<KeyValuePair<uint, FlagSpec>> DecodeAll(byte[] data, out string problem)
        {
            problem = null;
            var list = new List<KeyValuePair<uint, FlagSpec>>();
            if (data == null || data.Length == 0)
                return list;

            try
            {
                using (var stream = new MemoryStream(data))
                using (var r = new BinaryReader(stream))
                {
                    byte version = r.ReadByte();
                    if (!Known(version))
                    {
                        problem = version > Version
                            ? "saved by a newer version of the mod (format " + version + ")"
                            : "not a format this mod ever wrote (" + version + ")";
                        return null;
                    }

                    int count = r.ReadInt32();
                    if (count < 0 || count > 1 << 20)
                    {
                        problem = "a count of " + count;
                        return null;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        uint prop = r.ReadUInt32();
                        list.Add(new KeyValuePair<uint, FlagSpec>(prop, Read(r, version)));
                    }
                }
            }
            catch (Exception e)
            {
                problem = e.GetType().Name + ": " + e.Message;
                return null;
            }

            return list;
        }
    }

    /// <summary>
    /// The flags' heights in the city: each flag prop's PropInstance.m_posY (1/64 m) by prop index,
    /// saved by FlagSaveData beside the designs. The game does not save a prop's height
    /// (PropManager.Data.Serialize writes flags, prefab, x, z and angle; Deserialize sets m_posY to
    /// 0, and only a prop WITHOUT FixedHeight is put back on the terrain -- CalculateProp, its IL):
    /// every flag is FixedHeight, so without this a city loads its flags at height 0, under the map
    /// (VolTest3, 2026-11-01). Kept apart from FlagCodec so Move It's clipboard and the design
    /// format stay as they are. Versioned like it; anything unreadable is refused whole. PURE.
    /// </summary>
    public static class FlagHeightCodec
    {
        /// <summary>1 (2026-11-01): a count, then each flag's prop index (uint) and m_posY (ushort).</summary>
        public const byte Version = 1;

        public static byte[] Encode(IList<KeyValuePair<uint, ushort>> heights)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Version);
                w.Write(heights.Count);
                foreach (KeyValuePair<uint, ushort> h in heights)
                {
                    w.Write(h.Key);
                    w.Write(h.Value);
                }

                return stream.ToArray();
            }
        }

        /// <summary>Null (and why) when the bytes are not heights this version can read; empty for none.</summary>
        public static Dictionary<uint, ushort> Decode(byte[] data, out string problem)
        {
            problem = null;
            var heights = new Dictionary<uint, ushort>();
            if (data == null || data.Length == 0)
                return heights;

            try
            {
                using (var stream = new MemoryStream(data))
                using (var r = new BinaryReader(stream))
                {
                    byte version = r.ReadByte();
                    if (version != Version)
                    {
                        problem = version > Version
                            ? "saved by a newer version of the mod (heights format " + version + ")"
                            : "not a heights format this mod ever wrote (" + version + ")";
                        return null;
                    }

                    int count = r.ReadInt32();
                    if (count < 0 || count > 1 << 20)
                    {
                        problem = "a count of " + count;
                        return null;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        uint prop = r.ReadUInt32();
                        heights[prop] = r.ReadUInt16();
                    }
                }
            }
            catch (Exception e)
            {
                problem = e.GetType().Name + ": " + e.Message;
                return null;
            }

            return heights;
        }
    }
}
