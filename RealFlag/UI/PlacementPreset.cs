using System;
using System.Globalization;
using RealFlag.Flags;

namespace RealFlag.UI
{
    /// <summary>
    /// What a placement preset sets of the new flag: how it is hoisted, what holds it, its size,
    /// and its whole pole as the New flag window's Flagpole popup holds it -- style, height, ropes,
    /// base, position, finish, profile, colours, shine, thickness, wear, the block (2026-11-26).
    /// Never the flag's own design (country, pattern, colours, picture, cut, wear and tears,
    /// motion, waviness, darkness). PURE.
    /// </summary>
    public struct PlacementValues
    {
        public int HoistedFrom;
        public float Tilt, Lean;
        public int Pole;
        public float Height;
        public int Ropes, PoleBase;
        public float PolePosition;
        public int Held;

        /// <summary>The flag's size as the Width and Height rows keep it (FlagSize): its length and its proportion (hoist / length x 1000).</summary>
        public float Length;
        public int Proportion;

        /// <summary>The pole's look, as the Flagpole popup's rows (Settings.NewPoleColour ... NewBlockWear): colours sRGB, the rest in the settings' own units.</summary>
        public int PoleColour, TopColour, RopeColour;
        public float PoleThickness;
        public int PoleProfile;
        public float PoleShine, PoleWear, PoleRust, PoleDirt, RopeThickness;
        public int PoleFinish;

        /// <summary>The concrete block, drawn only with a Block base: its shape, width and height (m), colour and wear.</summary>
        public int BlockShape;
        public float BlockWidth, BlockHeight;
        public int BlockColour;
        public float BlockWear;
    }

    /// <summary>
    /// The New flag window's placement presets (the author, 2026-11-19: "Presets for flag
    /// orientation, like flags on buildings ... Default ... Wall Flag ... easier for users to make
    /// flags that go on building walls. So flagpole should be very small, rotation maybe 45deg. A
    /// third option would be those banners" -- a photo of tall banners, each on its own pole, hung
    /// from a short arm at its top, their bottoms free): Default, Wall flag, Banner -- or Custom,
    /// whatever else the settings they write are. As the Quality preset (QualityPreset): it holds
    /// nothing of its own, Settings.NewPreset is DERIVED from the settings (<see cref="Sync"/>,
    /// every frame) and choosing one writes them (<see cref="Apply"/>). Nothing of it is in a
    /// flag's spec or a save; the New flag window's settings are not undone (FlagUndo).
    /// 2026-11-26 (the author: "When a preset is selected, the preset should also apply flagpole
    /// styles, etc. the purpose of presets is to make it easy for the user to just plop a flag
    /// without customizing. Also, 'With a pole' should be the default selected value"): a preset
    /// writes the whole pole too, its look the defaults' in all three; a fresh install, every
    /// setting at its default, shows With pole (test-settings.ps1 proves it).
    /// </summary>
    public static class PlacementPreset
    {
        public const int Default = 0;
        public const int Wall = 1;
        public const int Banner = 2;
        public const int Custom = 3;

        /// <summary>
        /// The dropdown's entries (2026-11-25; the author: "it's best if we do a dropdown instead" of
        /// the icon buttons of 11-24): the three, and Custom, which it shows when the settings make
        /// none (choosing it changes nothing: <see cref="Apply"/>).
        /// </summary>
        public static readonly int[] Values = { Default, Wall, Banner, Custom };

        /// <summary>Their names: "With pole", "Hanging on wall", "Banner", "Custom".</summary>
        public static readonly string[] NameKeys = { "Preset.Default", "Preset.Wall", "Preset.Banner", "Preset.Custom" };

        /// <summary>
        /// Each preset's values. Default ("With pole"): the New flag window's defaults
        /// (Settings.Defaults: a 10 m ball-top pole since 2026-11-24, "Default preset should be with
        /// pole"). Wall
        /// flag: a short ball-top pole tilted 45 degrees out from the wall (the heading out of it:
        /// FlagEdgeSnap.OnWall), no ropes, no base on the wall, a 1.5 x 1 m flag. Banner: a 6 m
        /// flat-topped pole on its collar, no ropes, the cloth held along its top edge (its arm,
        /// FlagPoleShape.ArmTop), 1.2 m wide and 3 m tall, its bottom free. The pole's look
        /// (finish, profile, colours, shine, thickness, wear, block) is the defaults' in all three
        /// (<see cref="DefaultLook"/>): no preset of its own was asked for.
        /// </summary>
        public static readonly PlacementValues[] Presets =
        {
            DefaultLook(new PlacementValues
            {
                HoistedFrom = Settings.Defaults.HoistedFrom, Tilt = Settings.Defaults.Tilt, Lean = Settings.Defaults.Lean,
                Pole = Settings.Defaults.Pole, Height = Settings.Defaults.Height, Ropes = Settings.Defaults.Ropes,
                PoleBase = Settings.Defaults.PoleBase, PolePosition = Settings.Defaults.PolePosition, Held = Settings.Defaults.Held,
                Length = Settings.Defaults.Length, Proportion = Settings.Defaults.Proportion,
            }),
            DefaultLook(new PlacementValues
            {
                HoistedFrom = FlagHoist.Side, Tilt = 45f, Lean = 0f,
                Pole = FlagPoleStyle.Ball, Height = 2.5f, Ropes = 0,
                PoleBase = FlagPoleBase.None, PolePosition = 1f, Held = FlagHold.None,
                Length = 1.5f, Proportion = 667,
            }),
            DefaultLook(new PlacementValues
            {
                HoistedFrom = FlagHoist.Side, Tilt = 0f, Lean = 0f,
                Pole = FlagPoleStyle.Flat, Height = 6f, Ropes = 0,
                PoleBase = FlagPoleBase.Collar, PolePosition = 1f, Held = FlagHold.TopEdge,
                Length = 1.2f, Proportion = 2500,
            }),
        };

        /// <summary>The values with the pole's look the New flag window's defaults (Settings.Defaults). PURE.</summary>
        private static PlacementValues DefaultLook(PlacementValues v)
        {
            v.PoleColour = Settings.Defaults.PoleColour;
            v.TopColour = Settings.Defaults.TopColour;
            v.RopeColour = Settings.Defaults.RopeColour;
            v.PoleThickness = Settings.Defaults.PoleThickness;
            v.PoleProfile = Settings.Defaults.PoleProfile;
            v.PoleShine = Settings.Defaults.PoleShine;
            v.PoleWear = Settings.Defaults.PoleWear;
            v.PoleRust = Settings.Defaults.PoleRust;
            v.PoleDirt = Settings.Defaults.PoleDirt;
            v.RopeThickness = Settings.Defaults.RopeThickness;
            v.PoleFinish = Settings.Defaults.PoleFinish;
            v.BlockShape = Settings.Defaults.BlockShape;
            v.BlockWidth = Settings.Defaults.BlockWidth;
            v.BlockHeight = Settings.Defaults.BlockHeight;
            v.BlockColour = Settings.Defaults.BlockColour;
            v.BlockWear = Settings.Defaults.BlockWear;
            return v;
        }

        /// <summary>The preset these values make, or <see cref="Custom"/>: every one alike (the floats to well under a slider's step). PURE.</summary>
        public static int Of(PlacementValues v)
        {
            for (int i = 0; i < Presets.Length; i++)
            {
                if (Same(Presets[i], v))
                    return i;
            }

            return Custom;
        }

        /// <summary>
        /// Every field <see cref="Write"/> writes, compared: one left out would show a preset over
        /// settings it does not make, and choosing it again would change nothing (a setting ignores
        /// its own value). Ints and colours exactly; the floats to well under a slider's step and
        /// well over FlagMaker.xml's rounding (four decimals of what the row shows: 1e-6 of a
        /// percentage's value, 5e-5 m) -- percentages (5% steps) to 0.001, metres (0.05 m steps) to 0.005.
        /// </summary>
        private static bool Same(PlacementValues a, PlacementValues b)
        {
            return a.HoistedFrom == b.HoistedFrom && Math.Abs(a.Tilt - b.Tilt) < 0.05f && Math.Abs(a.Lean - b.Lean) < 0.05f &&
                   a.Pole == b.Pole && Math.Abs(a.Height - b.Height) < 0.01f && a.Ropes == b.Ropes && a.PoleBase == b.PoleBase &&
                   Math.Abs(a.PolePosition - b.PolePosition) < 0.001f && (a.Held & FlagHold.All) == (b.Held & FlagHold.All) &&
                   Math.Abs(a.Length - b.Length) < 0.005f && a.Proportion == b.Proportion &&
                   a.PoleColour == b.PoleColour && a.TopColour == b.TopColour && a.RopeColour == b.RopeColour &&
                   Math.Abs(a.PoleThickness - b.PoleThickness) < 0.001f && a.PoleProfile == b.PoleProfile &&
                   Math.Abs(a.PoleShine - b.PoleShine) < 0.001f && Math.Abs(a.PoleWear - b.PoleWear) < 0.001f &&
                   Math.Abs(a.PoleRust - b.PoleRust) < 0.001f && Math.Abs(a.PoleDirt - b.PoleDirt) < 0.001f &&
                   Math.Abs(a.RopeThickness - b.RopeThickness) < 0.001f && a.PoleFinish == b.PoleFinish &&
                   a.BlockShape == b.BlockShape && Math.Abs(a.BlockWidth - b.BlockWidth) < 0.005f &&
                   Math.Abs(a.BlockHeight - b.BlockHeight) < 0.005f && a.BlockColour == b.BlockColour &&
                   Math.Abs(a.BlockWear - b.BlockWear) < 0.001f;
        }

        /// <summary>The new flag's values now.</summary>
        public static PlacementValues Now()
        {
            return new PlacementValues
            {
                HoistedFrom = Settings.NewHoistedFrom.value,
                Tilt = Settings.NewTilt.value,
                Lean = Settings.NewLean.value,
                Pole = Settings.NewPole.value,
                Height = Settings.NewHeight.value,
                Ropes = Settings.NewRopes.value,
                PoleBase = Settings.NewPoleBase.value,
                PolePosition = Settings.NewPolePosition.value,
                Held = Settings.NewHeld.value,
                Length = Settings.NewLength.value,
                Proportion = Settings.NewProportion.value,
                PoleColour = Settings.NewPoleColour.value,
                TopColour = Settings.NewTopColour.value,
                RopeColour = Settings.NewRopeColour.value,
                PoleThickness = Settings.NewPoleThickness.value,
                PoleProfile = Settings.NewPoleProfile.value,
                PoleShine = Settings.NewPoleShine.value,
                PoleWear = Settings.NewPoleWear.value,
                PoleRust = Settings.NewPoleRust.value,
                PoleDirt = Settings.NewPoleDirt.value,
                RopeThickness = Settings.NewRopeThickness.value,
                PoleFinish = Settings.NewPoleFinish.value,
                BlockShape = Settings.NewBlockShape.value,
                BlockWidth = Settings.NewBlockWidth.value,
                BlockHeight = Settings.NewBlockHeight.value,
                BlockColour = Settings.NewBlockColour.value,
                BlockWear = Settings.NewBlockWear.value,
            };
        }

        /// <summary>Settings.NewPreset follows the settings (a file loaded or edited, a row changed): cheap, every frame.</summary>
        public static void Sync()
        {
            if (Settings.NewPreset != null && Settings.NewHoistedFrom != null)
                Settings.NewPreset.value = Of(Now());
        }

        /// <summary>
        /// A preset chosen: its settings written (<see cref="Write"/>), the log told. The size as
        /// the Width and Height rows write it -- the length and proportion themselves (FlagSize),
        /// whatever the window's picture or country: a picture is cropped to the new shape, never
        /// stretched (FlagCrop), a country's flag stretched to it (CropMap.ForCountry), as when the
        /// player sizes the flag by hand. Custom changes nothing.
        /// </summary>
        public static void Apply(int preset)
        {
            if (preset < Default || preset >= Presets.Length || Settings.NewHoistedFrom == null)
                return;

            PlacementValues p = Presets[preset];
            Write(p);

            Log.Msg("setting: preset = " + LogName(preset) + " (" + Describe(p) + ")" +
                    (Settings.NewImage.value != 0 ? "; the picture cropped to the new shape" : ""));
            Sync();
        }

        /// <summary>
        /// The values written over the New flag window's settings, every field of them (each one
        /// <see cref="Same"/> and <see cref="Now"/> know). No log line: Apply's, and the offline
        /// test's way in (test-settings.ps1).
        /// </summary>
        public static void Write(PlacementValues p)
        {
            if (Settings.NewHoistedFrom == null)
                return;

            Settings.NewHoistedFrom.value = p.HoistedFrom;
            Settings.NewTilt.value = p.Tilt;
            Settings.NewLean.value = p.Lean;
            Settings.NewPole.value = p.Pole;
            Settings.NewHeight.value = p.Height;
            Settings.NewRopes.value = p.Ropes;
            Settings.NewPoleBase.value = p.PoleBase;
            Settings.NewPolePosition.value = p.PolePosition;
            Settings.NewHeld.value = p.Held;
            Settings.NewLength.value = p.Length;
            Settings.NewProportion.value = p.Proportion;
            Settings.NewPoleColour.value = p.PoleColour;
            Settings.NewTopColour.value = p.TopColour;
            Settings.NewRopeColour.value = p.RopeColour;
            Settings.NewPoleThickness.value = p.PoleThickness;
            Settings.NewPoleProfile.value = p.PoleProfile;
            Settings.NewPoleShine.value = p.PoleShine;
            Settings.NewPoleWear.value = p.PoleWear;
            Settings.NewPoleRust.value = p.PoleRust;
            Settings.NewPoleDirt.value = p.PoleDirt;
            Settings.NewRopeThickness.value = p.RopeThickness;
            Settings.NewPoleFinish.value = p.PoleFinish;
            Settings.NewBlockShape.value = p.BlockShape;
            Settings.NewBlockWidth.value = p.BlockWidth;
            Settings.NewBlockHeight.value = p.BlockHeight;
            Settings.NewBlockColour.value = p.BlockColour;
            Settings.NewBlockWear.value = p.BlockWear;
        }

        public static string LogName(int preset)
        {
            switch (preset)
            {
                case Default: return "With pole";
                case Wall: return "Hanging on wall";
                case Banner: return "Banner";
                default: return "Custom";
            }
        }

        /// <summary>For the log: "hoisted from its side, tilted 45 deg, lean 0 deg, a ball-top pole 2.5 m, ropes 0, base 2, at 100% of it, held at its hoist only, 1.5 m x 667, painted tapered #E4E5E2".</summary>
        private static string Describe(PlacementValues p)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            return "hoisted from its " + (p.HoistedFrom == FlagHoist.Top ? "top" : "side") + ", tilted " + p.Tilt.ToString("0.#", inv) +
                   " deg, lean " + p.Lean.ToString("0.#", inv) + " deg, pole " + FlagPoleStyle.LogName(p.Pole) + " " + p.Height.ToString("0.#", inv) +
                   " m, ropes " + p.Ropes + ", base " + p.PoleBase + ", at " + (p.PolePosition * 100f).ToString("F0", inv) + "% of it, held at " +
                   FlagHold.LogName(p.Held) + ", " + p.Length.ToString("0.##", inv) + " m long at " + FlagProportion.Name(p.Proportion) + ", " +
                   FlagPoleFinish.LogName(p.PoleFinish) + " " + FlagPoleProfile.LogName(p.PoleProfile) + " " + FlagColours.Hex((uint)p.PoleColour);
        }
    }
}
