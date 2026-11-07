using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RealFlag.Flags
{
    /// <summary>
    /// STRING FLAGS (2026-11-07; the author: "a system for adding String Flags. They should behave
    /// like networks, draggable by MoveIt! ... Space between each flag should be controllable on the
    /// string, and the string itself should be customizable like color and thickness ... Flags should
    /// be either alternating, the same, or all countries' flags (but let the user choose the
    /// priority)"; the design talk's decisions, all taken as recommended). A string is a chain of
    /// SPANS, each a segment of the mod's own network (StringPrefab) between two nodes the player
    /// clicked on walls, roofs, the ground or a pole's top; this is one span's design. PURE.
    /// </summary>
    public static class StringMount
    {
        /// <summary>The flag's hoist edge along the string, its fly hanging down: bunting's pennants point down. The design turned a quarter.</summary>
        public const int Hoist = 0;

        /// <summary>The flag's top edge along the string, the design upright: a string of country flags.</summary>
        public const int Top = 1;

        public static readonly int[] Values = { Hoist, Top };
        public static readonly string[] NameKeys = { "StringMount.Hoist", "StringMount.Top" };

        public static bool IsKnown(int value)
        {
            return value == Hoist || value == Top;
        }
    }

    /// <summary>
    /// The flags' outline (the flag shapes round's values, 2026-11-07: append only, PERMANENT),
    /// cut along the cloth's fly -- from the string down -- so a pennant hung by its hoist points
    /// down. <see cref="StringSpec.Depth"/> is a notch's or a point's depth, a share of the fly.
    /// </summary>
    public static class StringShape
    {
        public const int Rectangle = 0;
        public const int Swallowtail = 1;
        public const int SwallowtailTongue = 2;
        public const int Pennant = 3;
        public const int Burgee = 4;
        public const int Pointed = 5;
        public const int Rounded = 6;

        public static readonly int[] Values = { Rectangle, Swallowtail, SwallowtailTongue, Pennant, Burgee, Pointed, Rounded };
        public static readonly string[] NameKeys =
        {
            "StringShape.Rectangle", "StringShape.Swallowtail", "StringShape.SwallowtailTongue", "StringShape.Pennant",
            "StringShape.Burgee", "StringShape.Pointed", "StringShape.Rounded",
        };

        public static bool IsKnown(int value)
        {
            return value >= Rectangle && value <= Rounded;
        }

        /// <summary>The shape has a depth to set (a rectangle and a pennant have none).</summary>
        public static bool HasDepth(int value)
        {
            return value != Rectangle && value != Pennant;
        }

        public static string LogName(int value)
        {
            switch (value)
            {
                case Swallowtail: return "swallowtail";
                case SwallowtailTongue: return "swallowtail and tongue";
                case Pennant: return "pennant";
                case Burgee: return "burgee";
                case Pointed: return "pointed";
                case Rounded: return "rounded";
                default: return "rectangle";
            }
        }

        /// <summary>
        /// How far inside the outline a point is, metres along the fly (&gt; 0 inside): x from the
        /// string, t along it 0..1, the fly <paramref name="fly"/> long. FlagString.shader's
        /// RFShapeInside, the same arithmetic (the panels' little flags draw with it).
        /// </summary>
        public static float Inside(int shape, float x, float t, float fly, float depth)
        {
            float c = Math.Abs(2f * t - 1f), d = depth * fly, limit = fly;
            switch (shape)
            {
                case Swallowtail:
                    limit = fly - d * (1f - c);
                    break;
                case SwallowtailTongue:
                {
                    float m = (4f * t) % 2f;
                    limit = fly - d * (1f - Math.Abs(m - 1f));
                    break;
                }
                case Pennant:
                    limit = fly * (1f - c);
                    break;
                case Burgee:
                    limit = Math.Min(fly * Math.Min(1f, Math.Max(0f, (1f - c) / 0.3f)), fly - d * Math.Min(1f, Math.Max(0f, 1f - c / 0.7f)));
                    break;
                case Pointed:
                    limit = fly - d * c;
                    break;
                case Rounded:
                    limit = fly - d + d * (float)Math.Sqrt(Math.Max(0f, 1f - c * c));
                    break;
            }

            return limit - x;
        }
    }

    /// <summary>How the flags are spaced: a gap between them, or a count each span.</summary>
    public static class StringSpacing
    {
        public const int Gap = 0;
        public const int Count = 1;

        public static readonly int[] Values = { Gap, Count };
        public static readonly string[] NameKeys = { "StringSpacing.Gap", "StringSpacing.Count" };

        public static bool IsKnown(int value)
        {
            return value == Gap || value == Count;
        }
    }

    /// <summary>What the flags are: one design all along, designs in turn, or countries' flags.</summary>
    public static class StringSequence
    {
        public const int Same = 0;
        public const int Alternate = 1;
        public const int Countries = 2;

        public static readonly int[] Values = { Same, Alternate, Countries };
        public static readonly string[] NameKeys = { "StringSequence.Same", "StringSequence.Alternate", "StringSequence.Countries" };

        public static bool IsKnown(int value)
        {
            return value == Same || value == Alternate || value == Countries;
        }
    }

    /// <summary>The designs' order, alternating: in turn (A B C A B C), back and forth (A B C B A B), or shuffled each round.</summary>
    public static class StringOrder
    {
        public const int InTurn = 0;
        public const int BackAndForth = 1;
        public const int Random = 2;

        public static readonly int[] Values = { InTurn, BackAndForth, Random };
        public static readonly string[] NameKeys = { "StringOrder.InTurn", "StringOrder.BackAndForth", "StringOrder.Random" };

        public static bool IsKnown(int value)
        {
            return value == InTurn || value == BackAndForth || value == Random;
        }
    }

    /// <summary>Which countries: every flag, one part of the world's, or the player's own list.</summary>
    public static class CountryWhich
    {
        public const int All = 0;
        public const int Region = 1;
        public const int List = 2;

        public static readonly int[] Values = { All, Region, List };
        public static readonly string[] NameKeys = { "CountryWhich.All", "CountryWhich.Region", "CountryWhich.List" };

        public static bool IsKnown(int value)
        {
            return value == All || value == Region || value == List;
        }
    }

    /// <summary>The countries' order: A to Z (in the player's language), by part of the world, shuffled, or as the player picked them.</summary>
    public static class CountryOrder
    {
        public const int AToZ = 0;
        public const int ByRegion = 1;
        public const int Random = 2;
        public const int AsPicked = 3;

        public static readonly int[] Values = { AToZ, ByRegion, Random, AsPicked };
        public static readonly string[] NameKeys = { "CountryOrder.AToZ", "CountryOrder.ByRegion", "CountryOrder.Random", "CountryOrder.AsPicked" };

        public static bool IsKnown(int value)
        {
            return value >= AToZ && value <= AsPicked;
        }
    }

    /// <summary>What the string is: a cord, a flat tape (bunting's own), a rope, a wire.</summary>
    public static class StringKind
    {
        public const int Cord = 0;
        public const int Tape = 1;
        public const int Rope = 2;
        public const int Wire = 3;

        public static readonly int[] Values = { Cord, Tape, Rope, Wire };
        public static readonly string[] NameKeys = { "StringKind.Cord", "StringKind.Tape", "StringKind.Rope", "StringKind.Wire" };

        public static bool IsKnown(int value)
        {
            return value >= Cord && value <= Wire;
        }

        public static string LogName(int value)
        {
            switch (value)
            {
                case Tape: return "tape";
                case Rope: return "rope";
                case Wire: return "wire";
                default: return "cord";
            }
        }

        /// <summary>
        /// Its mass per metre, kg, for its thickness (a tape's: its width): a cord or rope of nylon
        /// and cotton (about 1100 kg/m3, a rope's lay 0.9 of its circle), a polyester tape 0.5 mm
        /// thick (1380 kg/m3), a steel wire (7850 kg/m3).
        /// </summary>
        public static float MassPerMetre(int kind, float thickness)
        {
            float t = Math.Max(thickness, 0.0005f);
            float circle = (float)Math.PI * 0.25f * t * t;
            switch (kind)
            {
                case Tape: return 0.0005f * t * 1380f;
                case Rope: return 0.9f * circle * 1100f;
                case Wire: return circle * 7850f;
                default: return circle * 1100f;
            }
        }
    }

    /// <summary>What a string's ends are drawn as: as clicked (nothing of their own), a pole under each, a hook only.</summary>
    public static class StringEnds
    {
        public const int AsClicked = 0;
        public const int Poles = 1;
        public const int Hooks = 2;

        public static readonly int[] Values = { AsClicked, Poles, Hooks };
        public static readonly string[] NameKeys = { "StringEnds.AsClicked", "StringEnds.Poles", "StringEnds.Hooks" };

        public static bool IsKnown(int value)
        {
            return value >= AsClicked && value <= Hooks;
        }
    }

    /// <summary>The parts of the world (UN M49's, from Unicode CLDR: FlagRegions.cs is generated). PERMANENT values.</summary>
    public static partial class FlagRegions
    {
        public const int Africa = 0;
        public const int Americas = 1;
        public const int Asia = 2;
        public const int Europe = 3;
        public const int Oceania = 4;
        public const int International = 5;

        public static readonly int[] Values = { Africa, Americas, Asia, Europe, Oceania, International };
        public static readonly string[] NameKeys =
        {
            "Region.Africa", "Region.Americas", "Region.Asia", "Region.Europe", "Region.Oceania", "Region.International",
        };

        private static Dictionary<string, int> _byCode;

        public static bool IsKnown(int value)
        {
            return value >= Africa && value <= International;
        }

        /// <summary>A flag's part of the world by its code; International for one CLDR puts in none (or a code it does not know).</summary>
        public static int Of(string code)
        {
            if (_byCode == null)
            {
                var map = new Dictionary<string, int>(Codes.Length);
                for (int i = 0; i < Codes.Length && i < RegionOfCode.Length; i++)
                    map[Codes[i]] = RegionOfCode[i];
                _byCode = map;
            }

            int region;
            return code != null && _byCode.TryGetValue(code, out region) ? region : International;
        }

        public static string LogName(int value)
        {
            switch (value)
            {
                case Africa: return "Africa";
                case Americas: return "the Americas";
                case Asia: return "Asia";
                case Europe: return "Europe";
                case Oceania: return "Oceania";
                default: return "international";
            }
        }
    }

    /// <summary>One span's design: its flags, their order, the string, how it moves. A struct: copied whole.</summary>
    public struct StringSpec
    {
        public const float MinFlag = 0.1f, MaxFlag = 3f;
        public const float MaxGap = 5f, MaxMargin = 10f;
        public const int MaxCount = 200;
        public const float MinThickness = 0.001f, MaxThickness = 0.05f;
        public const float MinSag = 0.002f, MaxSag = 0.2f;
        public const int MaxDesigns = 8;
        public const int MaxListed = 300;
        public const int MaxLeadEvery = 50;
        public const float MaxSwing = 2f;

        /// <summary>Each flag as seen, metres: along the string (Top) or down from it (Hoist) is the width.</summary>
        public float Width, Height;

        /// <summary>A <see cref="StringMount"/> value.</summary>
        public int Mount;

        /// <summary>A <see cref="StringShape"/> value, and its notch's or point's depth (a share of the fly, 0.1..0.6).</summary>
        public int Shape;
        public float Depth;

        /// <summary>A <see cref="StringSpacing"/> value, the gap between flags (m), the flags a span, and the bare string at each end (m).</summary>
        public int Spacing;
        public float Gap;
        public int Count;
        public float Margin;

        /// <summary>A <see cref="StringSequence"/> value, and the designs: [0] alone for Same, 1..8 in turn for Alternate, [0] the base (darkness, wear) for Countries.</summary>
        public int Sequence;
        public FlagSpec[] Designs;

        /// <summary>A <see cref="StringOrder"/> value (Alternate).</summary>
        public int DesignOrder;

        /// <summary>Countries: a <see cref="CountryWhich"/> value, the region (<see cref="FlagRegions"/>), the player's list (codes, in the order picked), a <see cref="CountryOrder"/> value.</summary>
        public int Which;
        public int Region;
        public string[] Countries;
        public int CountryOrder;

        /// <summary>A country flown first and again every <see cref="LeadEvery"/> flags (0: first only); empty for none.</summary>
        public string Lead;
        public int LeadEvery;

        /// <summary>The shuffles' seed (Random orders).</summary>
        public int Seed;

        /// <summary>The sequence carries on along a chain of spans (true), or starts again on each.</summary>
        public bool CarryOn;

        /// <summary>A <see cref="StringKind"/> value, its colour (sRGB 0xRRGGBB), its thickness (a tape's width), metres.</summary>
        public int Kind;
        public uint Colour;
        public float Thickness;

        /// <summary>How deep the span hangs at its middle, a share of its length (0.03: 3%).</summary>
        public float Sag;

        /// <summary>A <see cref="StringEnds"/> value.</summary>
        public int Ends;

        /// <summary>A <see cref="FlagMotion"/> value; how much the flags ripple (0..2); how far the string swings in the wind (0..2, 1 as the wind has it).</summary>
        public int Motion;
        public float Waviness;
        public float Swing;

        /// <summary>The cloths a span's flags share (0: Auto, by how many flags it has; else 1..8).</summary>
        public int Motions;

        /// <summary>The name the list shows; empty for "String n".</summary>
        public string Name;

        public static StringSpec Default
        {
            get
            {
                FlagSpec red = FlagSpec.Default, white = FlagSpec.Default, blue = FlagSpec.Default;
                red.Pattern = white.Pattern = blue.Pattern = FlagPattern.Plain;
                red.Colour1 = 0xC8341F;
                white.Colour1 = 0xF2F2F2;
                blue.Colour1 = 0x2A4F8F;
                return new StringSpec
                {
                    Width = 0.3f,
                    Height = 0.25f,
                    Mount = StringMount.Hoist,
                    Shape = StringShape.Pennant,
                    Depth = 0.3f,
                    Spacing = StringSpacing.Gap,
                    Gap = 0.08f,
                    Count = 30,
                    Margin = 0.3f,
                    Sequence = StringSequence.Alternate,
                    Designs = new[] { red, white, blue },
                    DesignOrder = StringOrder.InTurn,
                    Which = CountryWhich.All,
                    Region = FlagRegions.Europe,
                    Countries = new string[0],
                    CountryOrder = Flags.CountryOrder.AToZ,
                    Lead = string.Empty,
                    LeadEvery = 0,
                    Seed = 7,
                    CarryOn = true,
                    Kind = StringKind.Cord,
                    Colour = 0xF4EFE6,
                    Thickness = 0.004f,
                    Sag = 0.03f,
                    Ends = StringEnds.AsClicked,
                    Motion = FlagMotion.Waves,
                    Waviness = 1f,
                    Swing = 1f,
                    Motions = 0,
                    Name = string.Empty,
                };
            }
        }

        /// <summary>The flag's length along the string and down from it, metres.</summary>
        public float Along
        {
            get { return Mount == StringMount.Hoist ? Height : Width; }
        }

        public float Hang
        {
            get { return Mount == StringMount.Hoist ? Width : Height; }
        }

        /// <summary>Pulled into range, unknown values replaced: what a save or a paste holds is not trusted.</summary>
        public StringSpec Sanitised()
        {
            StringSpec s = this;
            s.Width = Clamp(s.Width, MinFlag, MaxFlag, 0.3f);
            s.Height = Clamp(s.Height, MinFlag, MaxFlag, 0.25f);
            if (!StringMount.IsKnown(s.Mount)) s.Mount = StringMount.Hoist;
            if (!StringShape.IsKnown(s.Shape)) s.Shape = StringShape.Rectangle;
            s.Depth = Clamp(s.Depth, 0.1f, 0.6f, 0.3f);
            if (!StringSpacing.IsKnown(s.Spacing)) s.Spacing = StringSpacing.Gap;
            s.Gap = Clamp(s.Gap, 0f, MaxGap, 0.08f);
            s.Count = Math.Max(1, Math.Min(s.Count, MaxCount));
            s.Margin = Clamp(s.Margin, 0f, MaxMargin, 0.3f);
            if (!StringSequence.IsKnown(s.Sequence)) s.Sequence = StringSequence.Same;

            FlagSpec[] designs = s.Designs ?? new FlagSpec[0];
            int n = Math.Min(designs.Length, MaxDesigns);
            if (n == 0)
            {
                s.Designs = new[] { FlagSpec.Default };
            }
            else
            {
                var copy = new FlagSpec[n];
                for (int i = 0; i < n; i++)
                    copy[i] = designs[i].Sanitised();
                s.Designs = copy;
            }

            if (!StringOrder.IsKnown(s.DesignOrder)) s.DesignOrder = StringOrder.InTurn;
            if (!CountryWhich.IsKnown(s.Which)) s.Which = CountryWhich.All;
            if (!FlagRegions.IsKnown(s.Region)) s.Region = FlagRegions.Europe;
            if (!Flags.CountryOrder.IsKnown(s.CountryOrder)) s.CountryOrder = Flags.CountryOrder.AToZ;

            var listed = new List<string>();
            if (s.Countries != null)
            {
                foreach (string code in s.Countries)
                {
                    string c = FlagCountries.Normalise(code);
                    if (c.Length > 0 && !listed.Contains(c) && listed.Count < MaxListed)
                        listed.Add(c);
                }
            }

            s.Countries = listed.ToArray();
            s.Lead = FlagCountries.Normalise(s.Lead);
            s.LeadEvery = Math.Max(0, Math.Min(s.LeadEvery, MaxLeadEvery));
            if (!StringKind.IsKnown(s.Kind)) s.Kind = StringKind.Cord;
            s.Colour &= 0xFFFFFF;
            s.Thickness = Clamp(s.Thickness, MinThickness, MaxThickness, 0.004f);
            s.Sag = Clamp(s.Sag, MinSag, MaxSag, 0.03f);
            if (!StringEnds.IsKnown(s.Ends)) s.Ends = StringEnds.AsClicked;
            if (!FlagMotion.IsKnown(s.Motion)) s.Motion = FlagMotion.Waves;
            s.Waviness = Clamp(s.Waviness, 0f, 2f, 1f);
            s.Swing = Clamp(s.Swing, 0f, MaxSwing, 1f);
            s.Motions = Math.Max(0, Math.Min(s.Motions, 8));
            s.Name = s.Name ?? string.Empty;
            if (s.Name.Length > 40)
                s.Name = s.Name.Substring(0, 40);
            return s;
        }

        private static float Clamp(float v, float min, float max, float fallback)
        {
            if (float.IsNaN(v) || float.IsInfinity(v))
                return fallback;
            return v < min ? min : v > max ? max : v;
        }

        /// <summary>The same look and the same cloth: nothing a redraw would change (designs compared by their codec bytes).</summary>
        public bool Same(StringSpec o)
        {
            return StringCodec.SameBytes(this, o);
        }

        /// <summary>One line for the log.</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append(Width.ToString("0.##")).Append(" x ").Append(Height.ToString("0.##")).Append(" m flags");
            sb.Append(Mount == StringMount.Top ? " upright" : " pointing down");
            if (Spacing == StringSpacing.Count)
                sb.Append(", ").Append(Count).Append(" a span");
            else
                sb.Append(", ").Append(Gap.ToString("0.##")).Append(" m apart");
            switch (Sequence)
            {
                case StringSequence.Same:
                    sb.Append(", all the same");
                    break;
                case StringSequence.Alternate:
                    sb.Append(", ").Append(Designs != null ? Designs.Length : 0).Append(" designs in turn");
                    break;
                default:
                    sb.Append(", countries (");
                    sb.Append(Which == CountryWhich.All ? "all" : Which == CountryWhich.Region ? FlagRegions.LogName(Region) : (Countries != null ? Countries.Length : 0) + " listed");
                    if (!string.IsNullOrEmpty(Lead))
                        sb.Append(", ").Append(Lead).Append(LeadEvery > 0 ? " every " + LeadEvery : " first");
                    sb.Append(")");
                    break;
            }

            sb.Append(", ").Append(StringKind.LogName(Kind)).Append(" ").Append((Thickness * 1000f).ToString("0")).Append(" mm ").Append(FlagColours.Hex(Colour));
            sb.Append(", sag ").Append((Sag * 100f).ToString("0.#")).Append("%");
            if (Motion != FlagMotion.Waves)
                sb.Append(", ").Append(FlagMotion.LogName(Motion));
            return sb.ToString();
        }
    }

    /// <summary>
    /// A span's design in bytes, and the city's spans by segment. Format 1 (2026-11-07). The
    /// designs inside are FlagCodec's, with ITS format written ahead of them, so a string reads
    /// its flags as the flags' own format reads them, whatever that format has grown to. Each
    /// format reads every older; a newer one, or garbage, is refused whole (Invariant 18: kept).
    /// </summary>
    public static class StringCodec
    {
        public const byte Version = 1;

        public static void Write(BinaryWriter w, StringSpec s)
        {
            s = s.Sanitised();
            w.Write(s.Width);
            w.Write(s.Height);
            w.Write((byte)s.Mount);
            w.Write((byte)s.Shape);
            w.Write(s.Depth);
            w.Write((byte)s.Spacing);
            w.Write(s.Gap);
            w.Write((ushort)s.Count);
            w.Write(s.Margin);
            w.Write((byte)s.Sequence);
            w.Write(FlagCodec.Version);
            w.Write((byte)s.Designs.Length);
            foreach (FlagSpec d in s.Designs)
                FlagCodec.Write(w, d);
            w.Write((byte)s.DesignOrder);
            w.Write((byte)s.Which);
            w.Write((byte)s.Region);
            w.Write((ushort)s.Countries.Length);
            foreach (string c in s.Countries)
                WriteAscii(w, c);
            w.Write((byte)s.CountryOrder);
            WriteAscii(w, s.Lead);
            w.Write((byte)s.LeadEvery);
            w.Write(s.Seed);
            w.Write(s.CarryOn);
            w.Write((byte)s.Kind);
            w.Write(s.Colour);
            w.Write(s.Thickness);
            w.Write(s.Sag);
            w.Write((byte)s.Ends);
            w.Write((byte)s.Motion);
            w.Write(s.Waviness);
            w.Write(s.Swing);
            w.Write((byte)s.Motions);
            byte[] name = Encoding.UTF8.GetBytes(s.Name ?? string.Empty);
            w.Write((ushort)name.Length);
            w.Write(name);
        }

        public static StringSpec Read(BinaryReader r, byte version)
        {
            var s = new StringSpec();
            s.Width = r.ReadSingle();
            s.Height = r.ReadSingle();
            s.Mount = r.ReadByte();
            s.Shape = r.ReadByte();
            s.Depth = r.ReadSingle();
            s.Spacing = r.ReadByte();
            s.Gap = r.ReadSingle();
            s.Count = r.ReadUInt16();
            s.Margin = r.ReadSingle();
            s.Sequence = r.ReadByte();
            byte flagVersion = r.ReadByte();
            if (flagVersion < FlagCodec.OldestVersion || flagVersion > FlagCodec.Version)
                throw new IOException("its flags are of a newer format (" + flagVersion + ") than this copy reads (" + FlagCodec.Version + ")");
            int designs = r.ReadByte();
            s.Designs = new FlagSpec[designs];
            for (int i = 0; i < designs; i++)
                s.Designs[i] = FlagCodec.Read(r, flagVersion);
            s.DesignOrder = r.ReadByte();
            s.Which = r.ReadByte();
            s.Region = r.ReadByte();
            int countries = r.ReadUInt16();
            s.Countries = new string[countries];
            for (int i = 0; i < countries; i++)
                s.Countries[i] = ReadAscii(r);
            s.CountryOrder = r.ReadByte();
            s.Lead = ReadAscii(r);
            s.LeadEvery = r.ReadByte();
            s.Seed = r.ReadInt32();
            s.CarryOn = r.ReadBoolean();
            s.Kind = r.ReadByte();
            s.Colour = r.ReadUInt32();
            s.Thickness = r.ReadSingle();
            s.Sag = r.ReadSingle();
            s.Ends = r.ReadByte();
            s.Motion = r.ReadByte();
            s.Waviness = r.ReadSingle();
            s.Swing = r.ReadSingle();
            s.Motions = r.ReadByte();
            int nameLength = r.ReadUInt16();
            s.Name = Encoding.UTF8.GetString(r.ReadBytes(nameLength));
            return s.Sanitised();
        }

        private static void WriteAscii(BinaryWriter w, string text)
        {
            byte[] b = Encoding.ASCII.GetBytes(text ?? string.Empty);
            w.Write((byte)Math.Min(b.Length, 255));
            w.Write(b, 0, Math.Min(b.Length, 255));
        }

        private static string ReadAscii(BinaryReader r)
        {
            int n = r.ReadByte();
            return Encoding.ASCII.GetString(r.ReadBytes(n));
        }

        public static byte[] EncodeOne(StringSpec spec)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Version);
                Write(w, spec);
                return stream.ToArray();
            }
        }

        public static bool TryDecodeOne(byte[] data, out StringSpec spec)
        {
            spec = StringSpec.Default;
            if (data == null || data.Length < 2)
                return false;

            try
            {
                using (var stream = new MemoryStream(data))
                using (var r = new BinaryReader(stream))
                {
                    byte version = r.ReadByte();
                    if (version < 1 || version > Version)
                        return false;
                    spec = Read(r, version);
                    return stream.Position == stream.Length;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Equal designs, byte for byte as they would be saved.</summary>
        public static bool SameBytes(StringSpec a, StringSpec b)
        {
            byte[] x = EncodeOne(a), y = EncodeOne(b);
            if (x.Length != y.Length)
                return false;
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i])
                    return false;
            }

            return true;
        }

        /// <summary>The city's spans, by segment.</summary>
        public static byte[] EncodeAll(IList<KeyValuePair<ushort, StringSpec>> spans)
        {
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                w.Write(Version);
                w.Write(spans.Count);
                foreach (KeyValuePair<ushort, StringSpec> s in spans)
                {
                    w.Write(s.Key);
                    Write(w, s.Value);
                }

                return stream.ToArray();
            }
        }

        /// <summary>The spans in <paramref name="data"/> (none for null or empty); null, with the reason, for what this copy cannot read -- never a part of it.</summary>
        public static List<KeyValuePair<ushort, StringSpec>> DecodeAll(byte[] data, out string problem)
        {
            problem = null;
            var spans = new List<KeyValuePair<ushort, StringSpec>>();
            if (data == null || data.Length == 0)
                return spans;

            try
            {
                using (var stream = new MemoryStream(data))
                using (var r = new BinaryReader(stream))
                {
                    byte version = r.ReadByte();
                    if (version < 1 || version > Version)
                    {
                        problem = "format " + version + " (this copy reads 1 to " + Version + ")";
                        return null;
                    }

                    int count = r.ReadInt32();
                    if (count < 0 || count > 65536)
                    {
                        problem = "a count of " + count;
                        return null;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        ushort segment = r.ReadUInt16();
                        spans.Add(new KeyValuePair<ushort, StringSpec>(segment, Read(r, version)));
                    }

                    if (stream.Position != stream.Length)
                    {
                        problem = (stream.Length - stream.Position) + " bytes past the end";
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                problem = e.GetType().Name + ": " + e.Message;
                return null;
            }

            return spans;
        }
    }

    /// <summary>
    /// The span between two points as it hangs: a CATENARY (the curve a uniform chain takes under
    /// its own weight, y = a cosh(x / a): Huygens, Leibniz and Johann Bernoulli, 1691), through
    /// both ends whatever their heights, its length set by the sag -- a level span of chord c
    /// sagging d at its middle is c (1 + 8/3 (d/c)^2) long (the parabola's length, which the
    /// shallow catenary has), and a sloping one is given the same slack over its chord. In the
    /// span's own vertical plane: x along the level from the first end, y up. PURE.
    /// </summary>
    public struct StringCurve
    {
        /// <summary>The ends' level distance and rise (the second end's height over the first's), metres.</summary>
        public float Level, Rise;

        /// <summary>The string's length, the catenary's parameter (metres), its lowest point's x, and its offset (y = A cosh((x - X0) / A) + C).</summary>
        public float Length, A, X0, C;

        /// <summary>Nearly upright, or too short to hang: drawn straight from end to end.</summary>
        public bool Straight;

        public static StringCurve Through(float level, float rise, float sag)
        {
            var c = new StringCurve { Level = Math.Max(level, 0f), Rise = rise };
            float chord = (float)Math.Sqrt(level * level + rise * rise);
            c.Length = chord * (1f + 8f / 3f * sag * sag);
            if (level < 0.05f || chord < 0.1f || sag <= 0f)
            {
                c.Straight = true;
                c.Length = chord;
                return c;
            }

            // 2 a sinh(h / 2a) = sqrt(L^2 - v^2): its left side falls as a grows; bisect on a's log.
            double h = level, v = rise, target = Math.Sqrt(Math.Max(c.Length * (double)c.Length - v * v, h * h * (1 + 1e-9)));
            double lo = h * 1e-4, hi = h * 1e5;
            for (int i = 0; i < 100; i++)
            {
                double mid = Math.Sqrt(lo * hi);
                double f = 2.0 * mid * Math.Sinh(h / (2.0 * mid));
                if (f > target) lo = mid; else hi = mid;
            }

            double a = Math.Sqrt(lo * hi);
            // The lowest point: the ends' heights fix where the curve's middle is (Wikipedia's "Catenary": determining parameters).
            double x0 = h / 2.0 - a * Atanh(Math.Max(-0.999999, Math.Min(0.999999, v / c.Length)));
            c.A = (float)a;
            c.X0 = (float)x0;
            c.C = (float)(-a * Math.Cosh(-x0 / a));
            return c;
        }

        private static double Atanh(double x)
        {
            return 0.5 * Math.Log((1.0 + x) / (1.0 - x));
        }

        /// <summary>The height at x (0 at the first end).</summary>
        public float HeightAt(float x)
        {
            if (Straight)
                return Level > 1e-4f ? Rise * x / Level : 0f;
            return (float)(A * Math.Cosh((x - X0) / A) + C);
        }

        /// <summary>How far below the straight line between the ends the string is at x (0 at both ends, positive down).</summary>
        public float DropAt(float x)
        {
            float line = Level > 1e-4f ? Rise * x / Level : 0f;
            return line - HeightAt(x);
        }

        /// <summary>The string's length from the first end to x.</summary>
        public float ArcTo(float x)
        {
            if (Straight)
                return Level > 1e-4f ? Length * x / Level : 0f;
            return (float)(A * (Math.Sinh((x - X0) / A) - Math.Sinh(-X0 / A)));
        }

        /// <summary>Where (x) the string has run <paramref name="arc"/> metres from the first end.</summary>
        public float XAt(float arc)
        {
            if (Straight)
                return Length > 1e-4f ? Level * arc / Length : 0f;
            double s0 = Math.Sinh(-X0 / A);
            double t = arc / A + s0;
            return (float)(X0 + A * Math.Log(t + Math.Sqrt(t * t + 1.0)));
        }

        /// <summary>The deepest drop below the line between the ends, metres (the sag).</summary>
        public float Deepest
        {
            get
            {
                if (Straight)
                    return 0f;
                // The drop is deepest where the curve's slope equals the chord's: sinh((x - X0)/A) = Rise/Level.
                double s = Rise / Math.Max(Level, 1e-4f);
                float x = (float)(X0 + A * Math.Log(s + Math.Sqrt(s * s + 1.0)));
                x = Math.Max(0f, Math.Min(Level, x));
                return DropAt(x);
            }
        }
    }

    /// <summary>Where each flag of a span sits: the arc length along the string where its edge starts, and the count.</summary>
    public struct StringLayout
    {
        public int Count;

        /// <summary>The first flag's edge from the first end, the arc from one flag's start to the next, and each flag's length along the string.</summary>
        public float First, Pitch, Along;

        public static StringLayout For(StringSpec spec, float length)
        {
            var l = new StringLayout { Along = spec.Along };
            float usable = length - 2f * spec.Margin;
            if (usable < spec.Along)
                return l;

            if (spec.Spacing == StringSpacing.Count)
            {
                int n = Math.Max(1, Math.Min(spec.Count, (int)Math.Floor(usable / spec.Along)));
                float gap = n > 1 ? (usable - n * spec.Along) / (n - 1) : 0f;
                l.Count = n;
                l.Pitch = spec.Along + gap;
                l.First = spec.Margin + (n > 1 ? 0f : (usable - spec.Along) * 0.5f);
                return l;
            }

            int count = (int)Math.Floor((usable + spec.Gap) / (spec.Along + spec.Gap) + 1e-4f);
            count = Math.Max(0, Math.Min(count, StringSpec.MaxCount));
            float used = count * spec.Along + Math.Max(0, count - 1) * spec.Gap;
            l.Count = count;
            l.Pitch = spec.Along + spec.Gap;
            l.First = spec.Margin + (usable - used) * 0.5f;
            return l;
        }
    }

    /// <summary>
    /// How far the span's sag plane swings about the line between its ends, radians (0 hanging
    /// straight down, positive towards where the wind blows). The wind blows it out to the angle
    /// overhead-line design calls the conductor's swing angle, tan phi = the wind's load over the
    /// weight, both per metre (EN 50341-1, IEC 60826); it swings there as a compound pendulum --
    /// the sagging curve turning about its chord, a parabola's: omega^2 = g ybar / r^2 = 5 g / (4 d)
    /// for a sag d -- damped (the flags' drag, CriticalShare of critical). PURE.
    /// </summary>
    public struct StringSwing
    {
        /// <summary>Air, kg/m3; the flags' drag on their area as they fly (Hoerner, "Fluid-Dynamic Drag", flags: about 0.1), a round cord's across it.</summary>
        public const float AirDensity = 1.2f, FlagDrag = 0.1f, CordDrag = 1.2f;

        /// <summary>The flag fabric's mass per square metre (FlagCloth's knitted polyester).</summary>
        public const float FabricMass = 0.115f;

        /// <summary>The swing's damping, a share of critical.</summary>
        public const float CriticalShare = 0.3f;

        /// <summary>The most it swings out, radians (just short of level).</summary>
        public const float MostAngle = 1.4f;

        public float Angle, Rate;

        /// <summary>The angle the wind holds it at: across-the-span speed <paramref name="speed"/> m/s.</summary>
        public static float Held(float speed, float flagAreaPerMetre, float thickness, int kind)
        {
            float q = 0.5f * AirDensity * speed * speed * (FlagDrag * flagAreaPerMetre + CordDrag * thickness);
            float w = (flagAreaPerMetre * FabricMass + StringKind.MassPerMetre(kind, thickness)) * 9.81f;
            return (float)Math.Atan2(q, Math.Max(w, 1e-5f));
        }

        /// <summary>The pendulum's angular frequency for a sag of <paramref name="sag"/> metres.</summary>
        public static float Omega(float sag)
        {
            return (float)Math.Sqrt(1.25 * 9.81 / Math.Max(sag, 0.02f));
        }

        /// <summary>One step of <paramref name="dt"/> s towards <paramref name="held"/> (signed: the wind's side), semi-implicit, sub-stepped to stay stable.</summary>
        public void Step(float dt, float held, float omega)
        {
            if (dt <= 0f)
                return;
            held = Math.Max(-MostAngle, Math.Min(MostAngle, held));
            int n = Math.Max(1, (int)Math.Ceiling(dt * omega / 0.3f));
            float h = dt / n, damping = 2f * CriticalShare * omega;
            for (int i = 0; i < n; i++)
            {
                float accel = omega * omega * (float)Math.Sin(held - Angle) - damping * Rate;
                Rate += accel * h;
                Angle += Rate * h;
            }

            if (float.IsNaN(Angle) || float.IsNaN(Rate))
            {
                Angle = held;
                Rate = 0f;
            }

            Angle = Math.Max(-MostAngle, Math.Min(MostAngle, Angle));
        }
    }

    /// <summary>
    /// Which design each flag of a string is, from its place along the chain (0 the first). The
    /// countries' list is made once for a spec and an order of names (the player's language's,
    /// for A to Z) and kept. PURE.
    /// </summary>
    public sealed class StringFlags
    {
        private readonly StringSpec _spec;
        private readonly string[] _countries;
        private readonly string _lead;

        /// <summary>The flags in the countries' order for <paramref name="spec"/>; <paramref name="byName"/> every flag in the language's A to Z.</summary>
        public StringFlags(StringSpec spec, IList<FlagCountry> byName)
        {
            _spec = spec.Sanitised();
            _lead = _spec.Sequence == StringSequence.Countries && FlagCountries.Find(_spec.Lead) != null ? _spec.Lead : null;
            _countries = _spec.Sequence == StringSequence.Countries ? CountriesFor(_spec, byName, _lead) : new string[0];
        }

        /// <summary>The countries flown, in order (the lead left out: it has its own places).</summary>
        public string[] Countries
        {
            get { return _countries; }
        }

        public static string[] CountriesFor(StringSpec spec, IList<FlagCountry> byName, string lead)
        {
            var pool = new List<string>();
            if (spec.Which == CountryWhich.List)
            {
                foreach (string code in spec.Countries ?? new string[0])
                {
                    if (FlagCountries.Find(code) != null && code != lead && !pool.Contains(code))
                        pool.Add(code);
                }

                if (spec.CountryOrder == Flags.CountryOrder.AsPicked)
                    return pool.ToArray();

                var rank = new Dictionary<string, int>();
                for (int i = 0; i < byName.Count; i++)
                    rank[byName[i].Code] = i;
                pool.Sort((a, b) => Rank(rank, a).CompareTo(Rank(rank, b)));
            }
            else
            {
                foreach (FlagCountry c in byName)
                {
                    if (c.Code == lead)
                        continue;
                    if (spec.Which == CountryWhich.Region && FlagRegions.Of(c.Code) != spec.Region)
                        continue;
                    pool.Add(c.Code);
                }
            }

            if (spec.CountryOrder == Flags.CountryOrder.ByRegion)
            {
                var place = new Dictionary<string, int>();
                for (int i = 0; i < pool.Count; i++)
                    place[pool[i]] = i;
                pool.Sort((a, b) =>
                {
                    int r = FlagRegions.Of(a).CompareTo(FlagRegions.Of(b));
                    return r != 0 ? r : place[a].CompareTo(place[b]);
                });
            }
            else if (spec.CountryOrder == Flags.CountryOrder.Random)
            {
                Shuffle(pool, spec.Seed);
            }

            return pool.ToArray();
        }

        private static int Rank(Dictionary<string, int> rank, string code)
        {
            int r;
            return rank.TryGetValue(code, out r) ? r : int.MaxValue;
        }

        /// <summary>Fisher and Yates' shuffle (Durstenfeld's), on xorshift32 (Marsaglia 2003) from the seed: the same order every time.</summary>
        public static void Shuffle<T>(IList<T> list, int seed)
        {
            uint s = (uint)seed * 2654435761u + 0x9E3779B9u;
            if (s == 0) s = 1;
            for (int i = list.Count - 1; i > 0; i--)
            {
                s ^= s << 13;
                s ^= s >> 17;
                s ^= s << 5;
                int j = (int)(s % (uint)(i + 1));
                T t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }

        /// <summary>The design of the flag at <paramref name="index"/> along the chain.</summary>
        public FlagSpec At(int index)
        {
            index = Math.Max(0, index);
            FlagSpec[] designs = _spec.Designs;
            switch (_spec.Sequence)
            {
                case StringSequence.Same:
                    return designs[0];

                case StringSequence.Alternate:
                    return designs[DesignIndex(index)];

                default:
                    FlagSpec d = designs[0];
                    d.Country = CountryAt(index) ?? string.Empty;
                    d.Image = string.Empty;
                    return d;
            }
        }

        /// <summary>Alternating: which of the designs the flag at <paramref name="index"/> is.</summary>
        public int DesignIndex(int index)
        {
            int n = _spec.Designs.Length;
            if (n <= 1)
                return 0;

            switch (_spec.DesignOrder)
            {
                case StringOrder.BackAndForth:
                {
                    int period = 2 * n - 2, k = index % period;
                    return k < n ? k : period - k;
                }

                case StringOrder.Random:
                {
                    // Two can only take turns (from a side the seed picks).
                    if (n == 2)
                        return (index + (_spec.Seed & 1)) % 2;

                    // Each round of n a new shuffle of the n, its first never the last round's last:
                    // swapped with its second, which leaves its own last as the shuffle had it.
                    int round = index / n;
                    int[] order = RoundOrder(round, n);
                    if (round > 0 && order[0] == RoundOrder(round - 1, n)[n - 1])
                    {
                        int t = order[0];
                        order[0] = order[1];
                        order[1] = t;
                    }

                    return order[index % n];
                }

                default:
                    return index % n;
            }
        }

        private int[] RoundOrder(int round, int n)
        {
            var order = new int[n];
            for (int i = 0; i < n; i++)
                order[i] = i;
            Shuffle(order, _spec.Seed * 7919 + round);
            return order;
        }

        /// <summary>Countries: the code at <paramref name="index"/>, the lead first and every LeadEvery after; null when there are none to fly.</summary>
        public string CountryAt(int index)
        {
            int n = _countries.Length;
            if (_lead != null)
            {
                if (index == 0)
                    return _lead;
                if (n == 0)
                    return _lead;
                int every = _spec.LeadEvery;
                if (every > 0)
                {
                    int cycle = every + 1;
                    if (index % cycle == 0)
                        return _lead;
                    int leads = 1 + (index - 1) / cycle;
                    return _countries[(index - leads) % n];
                }

                return _countries[(index - 1) % n];
            }

            return n > 0 ? _countries[index % n] : null;
        }
    }
}
