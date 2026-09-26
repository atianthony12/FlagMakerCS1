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
    /// length x 1000. APPEND ONLY. The most flown: 2:3 (most of Europe, much of the world), 1:2
    /// (the United Kingdom, Canada, Russia's 2:3 aside), 3:5, 10:19 (the United States), 1:1.
    /// </summary>
    public static class FlagProportion
    {
        public static readonly int[] Values = { 1000, 750, 667, 625, 600, 571, 526, 500 };
        public static readonly string[] Names = { "1:1", "3:4", "2:3", "5:8", "3:5", "4:7", "10:19", "1:2" };
        public const int Default = 667;

        public static float Ratio(int value)
        {
            return Math.Max(200, Math.Min(value, 2000)) / 1000f;
        }

        public static string Name(int value)
        {
            for (int i = 0; i < Values.Length; i++)
            {
                if (Values[i] == value)
                    return Names[i];
            }

            // Off the list (a country's own, as its image has it): "1:2.55" for Qatar's 11:28 --
            // the image's rounded pixels do not give the official fraction back.
            return "1:" + (1f / Ratio(value)).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
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

        /// <summary>A <see cref="FlagProportion"/> value: height / length x 1000.</summary>
        public int Proportion;

        /// <summary>How much the cloth ripples, 0..2 (100%: ordinary flag cloth).</summary>
        public float Waviness;

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
        /// The hoist's lean, degrees, -90..90 (FlagOrientation): <see cref="Tilt"/> turns the top of
        /// the pole towards the fly (+) or away from it, <see cref="Lean"/> to the side. Both 0: the
        /// hoist hangs straight down from the anchor, as it always did. A wall-mounted pole is a
        /// tilt of 30 to 45 degrees, a horizontal one 90.
        /// </summary>
        public float Tilt, Lean;

        public const float MinLength = 0.5f;
        public const float MaxLength = 30f;
        public const float MinPoleThickness = 0.5f;
        public const float MaxPoleThickness = 2f;
        public const float MaxTilt = 90f;

        public const uint DefaultPoleColour = 0xE4E5E2;   // white, as most town poles are painted
        public const uint DefaultTopColour = 0xD4AF37;    // gilt ball
        public const uint DefaultRopeColour = 0xF2EFE6;   // white braided polyester
        public const int DefaultRopes = 2;

        /// <summary>A country's flag, not a pattern.</summary>
        public bool IsCountry
        {
            get { return !string.IsNullOrEmpty(Country); }
        }

        public float Height
        {
            get { return Length * FlagProportion.Ratio(Proportion); }
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
                    Waviness = 1f,
                    Name = string.Empty,
                    Country = string.Empty,
                    Pole = FlagPoleStyle.None,
                    Ropes = DefaultRopes,
                    PoleColour = DefaultPoleColour,
                    TopColour = DefaultTopColour,
                    RopeColour = DefaultRopeColour,
                    PoleThickness = 1f,
                    Tilt = 0f,
                    Lean = 0f,
                };
            }
        }

        /// <summary>A pole is drawn under this flag.</summary>
        public bool HasPole
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
            if (s.Proportion < 200 || s.Proportion > 2000)
                s.Proportion = FlagProportion.Default;
            if (float.IsNaN(s.Waviness) || float.IsInfinity(s.Waviness))
                s.Waviness = 1f;
            s.Waviness = Math.Max(0f, Math.Min(s.Waviness, 2f));
            s.Name = s.Name ?? string.Empty;
            if (s.Name.Length > 64)
                s.Name = s.Name.Substring(0, 64);

            // A country's flag flies at its own proportions, whatever a paste or the panel says.
            s.Country = FlagCountries.Normalise(s.Country);
            FlagCountry country = FlagCountries.Find(s.Country);
            if (country != null)
                s.Proportion = country.Proportion;

            if (!FlagPoleStyle.IsKnown(s.Pole))
                s.Pole = FlagPoleStyle.None;
            s.Ropes = Math.Max(0, Math.Min(s.Ropes, 2));
            s.PoleColour &= 0xFFFFFF;
            s.TopColour &= 0xFFFFFF;
            s.RopeColour &= 0xFFFFFF;
            if (float.IsNaN(s.PoleThickness) || float.IsInfinity(s.PoleThickness))
                s.PoleThickness = 1f;
            s.PoleThickness = Math.Max(MinPoleThickness, Math.Min(s.PoleThickness, MaxPoleThickness));
            s.Tilt = Angle(s.Tilt);
            s.Lean = Angle(s.Lean);
            return s;
        }

        private static float Angle(float degrees)
        {
            if (float.IsNaN(degrees) || float.IsInfinity(degrees))
                return 0f;
            return Math.Max(-MaxTilt, Math.Min(degrees, MaxTilt));
        }

        /// <summary>The shape changed (a new cloth is needed), not just the look.</summary>
        public bool SameShape(FlagSpec other)
        {
            return Math.Abs(Length - other.Length) < 1e-4f && Proportion == other.Proportion;
        }

        /// <summary>The hoist's lean is the same (the cloth need not turn).</summary>
        public bool SameHoist(FlagSpec other)
        {
            return Math.Abs(Tilt - other.Tilt) < 1e-3f && Math.Abs(Lean - other.Lean) < 1e-3f;
        }

        /// <summary>The same pole (the same mesh and the same collider for the cloth).</summary>
        public bool SamePole(FlagSpec other)
        {
            return Pole == other.Pole && Ropes == other.Ropes && Math.Abs(PoleThickness - other.PoleThickness) < 1e-4f;
        }

        /// <summary>The same pole colours (only the property block changes).</summary>
        public bool SamePoleColours(FlagSpec other)
        {
            return PoleColour == other.PoleColour && TopColour == other.TopColour && RopeColour == other.RopeColour;
        }

        public string Describe()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            FlagCountry country = FlagCountries.Find(Country);
            string design = country != null
                ? country.Name + " (" + country.Code + ")"
                : (IsCountry ? "country '" + Country + "' unknown here, as " : "") + FlagPattern.LogName(Pattern) + " " +
                  FlagColours.Hex(Colour1) + "/" + FlagColours.Hex(Colour2) + "/" + FlagColours.Hex(Colour3);
            string text = design + ", " + Length.ToString("0.##", inv) + " m at " + FlagProportion.Name(Proportion) + ", waviness " +
                          (Waviness * 100f).ToString("F0", inv) + "%";
            if (HasPole)
                text += ", pole with a " + FlagPoleStyle.LogName(Pole) + " top and " + Ropes + " rope(s)" +
                        (Math.Abs(PoleThickness - 1f) > 1e-3f ? ", " + (PoleThickness * 100f).ToString("F0", inv) + "% thick" : "");
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
        /// and lean (floats, degrees). Every older one is still read.
        /// </summary>
        public const byte Version = 3;

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
}
