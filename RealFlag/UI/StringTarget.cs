using System;
using System.Collections.Generic;
using RealFlag.Flags;

namespace RealFlag.UI
{
    /// <summary>
    /// What one String window works on -- the next string's design (<see cref="New"/>) or the
    /// selected string's (<see cref="Selected"/>) -- as the panel's rows see it: a transient
    /// setting for each part of a StringSpec (never written to FlagMaker.xml: the new string keeps
    /// the last one's settings while the game runs), the designs and the countries' list kept beside
    /// them, and the one design the design rows edit (<see cref="Design"/>).
    /// </summary>
    public sealed class StringTarget
    {
        public static StringTarget New { get; private set; }
        public static StringTarget Selected { get; private set; }

        public readonly bool IsNew;
        public readonly string Prefix;

        public readonly FloatSetting Width, Height, Depth, Gap, Count, Margin, LeadEvery, Thickness, Sag, Waviness, Swing, Darkness;
        public readonly IntSetting Mount, Shape, Spacing, Sequence, Order, Which, Region, CountryOrder, Lead, CarryOn, Kind, Colour, Ends, Motion, Motions;

        /// <summary>The design the design rows edit: its index among <see cref="Designs"/>, and its parts.</summary>
        public readonly IntSetting Design, Country, Pattern, Colour1, Colour2, Colour3;

        public readonly List<FlagSpec> Designs = new List<FlagSpec>();
        public readonly List<string> Countries = new List<string>();
        public int Seed = StringSpec.Default.Seed;
        public string Name = string.Empty;

        /// <summary>Run after any change through the rows (the new string's ghost redrawn; the selected string redesigned).</summary>
        public Action Changed;

        /// <summary>The window's structure changed (designs added or removed, the sequence's kind): its rows are built again.</summary>
        public int Structure { get; private set; }

        public static void Init()
        {
            if (New != null)
                return;
            New = new StringTarget(true);
            Selected = new StringTarget(false);
        }

        private StringTarget(bool isNew)
        {
            IsNew = isNew;
            Prefix = isNew ? "NewStr" : "SelStr";
            StringSpec d = StringSpec.Default;
            FlagSpec fd = d.Designs[0];

            Width = F("Width", d.Width);
            Height = F("Height", d.Height);
            Depth = F("Depth", d.Depth);
            Gap = F("Gap", d.Gap);
            Count = F("Count", d.Count);
            Margin = F("Margin", d.Margin);
            LeadEvery = F("LeadEvery", d.LeadEvery);
            Thickness = F("Thickness", d.Thickness * 1000f);
            Sag = F("Sag", d.Sag);
            Waviness = F("Waviness", d.Waviness);
            Swing = F("Swing", d.Swing);
            Darkness = F("Darkness", fd.Darkness);

            Mount = I("Mount", d.Mount);
            Shape = I("Shape", d.Shape);
            Spacing = I("Spacing", d.Spacing);
            Sequence = I("Sequence", d.Sequence);
            Order = I("Order", d.DesignOrder);
            Which = I("Which", d.Which);
            Region = I("Region", d.Region);
            CountryOrder = I("CountryOrder", d.CountryOrder);
            Lead = I("Lead", 0);
            CarryOn = I("CarryOn", d.CarryOn ? 1 : 0);
            Kind = I("Kind", d.Kind);
            Colour = I("Colour", (int)d.Colour);
            Ends = I("Ends", d.Ends);
            Motion = I("Motion", d.Motion);
            Motions = I("Motions", d.Motions);

            Design = I("Design", 0);
            Country = I("Country", 0);
            Pattern = I("Pattern", fd.Pattern);
            Colour1 = I("Colour1", (int)fd.Colour1);
            Colour2 = I("Colour2", (int)fd.Colour2);
            Colour3 = I("Colour3", (int)fd.Colour3);

            Load(d);
        }

        private FloatSetting F(string name, float value)
        {
            return new FloatSetting(Prefix + name, value, true);
        }

        private IntSetting I(string name, int value)
        {
            return new IntSetting(Prefix + name, value, true);
        }

        /// <summary>The settings and lists from a design (the defaults, a selected string's, one copied).</summary>
        public void Load(StringSpec spec)
        {
            StringSpec s = spec.Sanitised();
            Width.value = s.Width;
            Height.value = s.Height;
            Depth.value = s.Depth;
            Gap.value = s.Gap;
            Count.value = s.Count;
            Margin.value = s.Margin;
            LeadEvery.value = s.LeadEvery;
            Thickness.value = s.Thickness * 1000f;
            Sag.value = s.Sag;
            Waviness.value = s.Waviness;
            Swing.value = s.Swing;
            Mount.value = s.Mount;
            Shape.value = s.Shape;
            Spacing.value = s.Spacing;
            Sequence.value = s.Sequence;
            Order.value = s.DesignOrder;
            Which.value = s.Which;
            Region.value = s.Region;
            CountryOrder.value = s.CountryOrder;
            Lead.value = FlagCountries.NumberOf(s.Lead);
            CarryOn.value = s.CarryOn ? 1 : 0;
            Kind.value = s.Kind;
            Colour.value = (int)s.Colour;
            Ends.value = s.Ends;
            Motion.value = s.Motion;
            Motions.value = s.Motions;
            Seed = s.Seed;
            Name = s.Name;

            Designs.Clear();
            Designs.AddRange(s.Designs);
            Countries.Clear();
            Countries.AddRange(s.Countries);
            Design.value = 0;
            DesignToSettings();
            Structure++;
        }

        /// <summary>The design being edited into its rows.</summary>
        public void DesignToSettings()
        {
            int i = Math.Max(0, Math.Min(Design.value, Designs.Count - 1));
            Design.value = i;
            FlagSpec d = Designs.Count > 0 ? Designs[i] : FlagSpec.Default;
            Country.value = FlagCountries.NumberOf(d.Country);
            Pattern.value = d.Pattern;
            Colour1.value = (int)d.Colour1;
            Colour2.value = (int)d.Colour2;
            Colour3.value = (int)d.Colour3;
            Darkness.value = d.Darkness;
        }

        /// <summary>The rows into the design being edited.</summary>
        public void SettingsToDesign()
        {
            if (Designs.Count == 0)
                Designs.Add(FlagSpec.Default);
            int i = Math.Max(0, Math.Min(Design.value, Designs.Count - 1));
            FlagSpec d = Designs[i];
            FlagCountry c = FlagCountries.At(Country.value);
            d.Country = c != null ? c.Code : string.Empty;
            if (c != null)
                d.Image = string.Empty;
            d.Pattern = Pattern.value;
            d.Colour1 = (uint)Colour1.value & 0xFFFFFF;
            d.Colour2 = (uint)Colour2.value & 0xFFFFFF;
            d.Colour3 = (uint)Colour3.value & 0xFFFFFF;
            d.Darkness = Darkness.value;
            Designs[i] = d.Sanitised();
        }

        /// <summary>The design as it stands.</summary>
        public StringSpec Spec()
        {
            SettingsToDesign();
            var s = new StringSpec
            {
                Width = Width.value,
                Height = Height.value,
                Mount = Mount.value,
                Shape = Shape.value,
                Depth = Depth.value,
                Spacing = Spacing.value,
                Gap = Gap.value,
                Count = (int)Math.Round(Count.value),
                Margin = Margin.value,
                Sequence = Sequence.value,
                Designs = Designs.ToArray(),
                DesignOrder = Order.value,
                Which = Which.value,
                Region = Region.value,
                Countries = Countries.ToArray(),
                CountryOrder = CountryOrder.value,
                Lead = FlagCountries.At(Lead.value) != null ? FlagCountries.At(Lead.value).Code : string.Empty,
                LeadEvery = (int)Math.Round(LeadEvery.value),
                Seed = Seed,
                CarryOn = CarryOn.value != 0,
                Kind = Kind.value,
                Colour = (uint)Colour.value & 0xFFFFFF,
                Thickness = Thickness.value / 1000f,
                Sag = Sag.value,
                Ends = Ends.value,
                Motion = Motion.value,
                Waviness = Waviness.value,
                Swing = Swing.value,
                Motions = Motions.value,
                Name = Name,
            };
            return s.Sanitised();
        }

        /// <summary>A row changed: the design written back, the owner told.</summary>
        public void Apply()
        {
            SettingsToDesign();
            if (Changed != null)
                Changed();
        }

        /// <summary>Another design (a copy of the one being edited), edited next.</summary>
        public void AddDesign()
        {
            SettingsToDesign();
            if (Designs.Count >= StringSpec.MaxDesigns)
                return;
            FlagSpec copy = Designs.Count > 0 ? Designs[Math.Max(0, Math.Min(Design.value, Designs.Count - 1))] : FlagSpec.Default;
            Designs.Add(copy);
            Design.value = Designs.Count - 1;
            DesignToSettings();
            Structure++;
            Log.Msg("strings: " + Whose() + " has " + Designs.Count + " designs");
            Apply();
        }

        /// <summary>The design being edited taken out (never the last one).</summary>
        public void RemoveDesign()
        {
            if (Designs.Count <= 1)
                return;
            int i = Math.Max(0, Math.Min(Design.value, Designs.Count - 1));
            Designs.RemoveAt(i);
            Design.value = Math.Min(i, Designs.Count - 1);
            DesignToSettings();
            Structure++;
            Log.Msg("strings: " + Whose() + " has " + Designs.Count + " design(s)");
            if (Changed != null)
                Changed();
        }

        /// <summary>Another design chosen to edit.</summary>
        public void ChooseDesign(int index)
        {
            Design.value = Math.Max(0, Math.Min(index, Designs.Count - 1));
            DesignToSettings();
        }

        public void Reshuffle()
        {
            Seed = (Seed * 1103515245 + 12345) & 0x7FFFFFFF;
            Log.Msg("strings: " + Whose() + " shuffled again (seed " + Seed + ")");
            if (Changed != null)
                Changed();
        }

        /// <summary>The countries' list changed (the list picker).</summary>
        public void CountriesChanged()
        {
            if (Changed != null)
                Changed();
        }

        /// <summary>The window's rows depend on this (built again when it moves).</summary>
        public void Restructure()
        {
            Structure++;
        }

        public string Whose()
        {
            return IsNew ? "the new string" : "the selected string";
        }
    }
}
