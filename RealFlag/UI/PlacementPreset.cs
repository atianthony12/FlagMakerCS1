using System;
using System.Globalization;
using RealFlag.Flags;

namespace RealFlag.UI
{
    /// <summary>
    /// What a placement preset sets of the new flag: how it is hoisted, its pole, what holds it,
    /// its size. Never its design (country, pattern, colours, picture, wear, motion, darkness, the
    /// pole's colours, profile and shine). PURE.
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
    /// </summary>
    public static class PlacementPreset
    {
        public const int Default = 0;
        public const int Wall = 1;
        public const int Banner = 2;
        public const int Custom = 3;

        /// <summary>The row's buttons: Custom has none (none lit).</summary>
        public static readonly int[] Values = { Default, Wall, Banner };

        /// <summary>Their names, which are the buttons' tooltips ("With pole", "Hanging on wall", "Banner").</summary>
        public static readonly string[] NameKeys = { "Preset.Default", "Preset.Wall", "Preset.Banner" };

        /// <summary>
        /// Their buttons' icons (2026-11-24; the author: "The presets could be icon buttons instead,
        /// with tooltips"): Lucide's flag, brick-wall and bookmark (a banner with its notched tail)
        /// in the panel's own sheet (Resources\PanelIcons.lucide.txt).
        /// </summary>
        public static readonly string[] Icons = { "icon-flag", "icon-brick-wall", "icon-bookmark" };

        /// <summary>
        /// Each preset's values. Default ("With pole"): the New flag window's defaults
        /// (Settings.Defaults: a 10 m ball-top pole since 2026-11-24, "Default preset should be with
        /// pole"). Wall
        /// flag: a short ball-top pole tilted 45 degrees out from the wall (the heading out of it:
        /// FlagEdgeSnap.OnWall), no ropes, no base on the wall, a 1.5 x 1 m flag. Banner: a 6 m
        /// flat-topped pole on its collar, no ropes, the cloth held along its top edge (its arm,
        /// FlagPoleShape.ArmTop), 1.2 m wide and 3 m tall, its bottom free.
        /// </summary>
        public static readonly PlacementValues[] Presets =
        {
            new PlacementValues
            {
                HoistedFrom = Settings.Defaults.HoistedFrom, Tilt = Settings.Defaults.Tilt, Lean = Settings.Defaults.Lean,
                Pole = Settings.Defaults.Pole, Height = Settings.Defaults.Height, Ropes = Settings.Defaults.Ropes,
                PoleBase = Settings.Defaults.PoleBase, PolePosition = Settings.Defaults.PolePosition, Held = Settings.Defaults.Held,
                Length = Settings.Defaults.Length, Proportion = Settings.Defaults.Proportion,
            },
            new PlacementValues
            {
                HoistedFrom = FlagHoist.Side, Tilt = 45f, Lean = 0f,
                Pole = FlagPoleStyle.Ball, Height = 2.5f, Ropes = 0,
                PoleBase = FlagPoleBase.None, PolePosition = 1f, Held = FlagHold.None,
                Length = 1.5f, Proportion = 667,
            },
            new PlacementValues
            {
                HoistedFrom = FlagHoist.Side, Tilt = 0f, Lean = 0f,
                Pole = FlagPoleStyle.Flat, Height = 6f, Ropes = 0,
                PoleBase = FlagPoleBase.Collar, PolePosition = 1f, Held = FlagHold.TopEdge,
                Length = 1.2f, Proportion = 2500,
            },
        };

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

        private static bool Same(PlacementValues a, PlacementValues b)
        {
            return a.HoistedFrom == b.HoistedFrom && Math.Abs(a.Tilt - b.Tilt) < 0.05f && Math.Abs(a.Lean - b.Lean) < 0.05f &&
                   a.Pole == b.Pole && Math.Abs(a.Height - b.Height) < 0.01f && a.Ropes == b.Ropes && a.PoleBase == b.PoleBase &&
                   Math.Abs(a.PolePosition - b.PolePosition) < 0.001f && (a.Held & FlagHold.All) == (b.Held & FlagHold.All) &&
                   Math.Abs(a.Length - b.Length) < 0.005f && a.Proportion == b.Proportion;
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
            };
        }

        /// <summary>Settings.NewPreset follows the settings (a file loaded or edited, a row changed): cheap, every frame.</summary>
        public static void Sync()
        {
            if (Settings.NewPreset != null && Settings.NewHoistedFrom != null)
                Settings.NewPreset.value = Of(Now());
        }

        /// <summary>
        /// A preset chosen: its settings written. The size as the Width and Height rows write it --
        /// the length and proportion themselves (FlagSize), whatever the window's picture or
        /// country: a picture is cropped to the new shape, never stretched (FlagCrop), a country's
        /// flag stretched to it (CropMap.ForCountry), as when the player sizes the flag by hand.
        /// Custom changes nothing.
        /// </summary>
        public static void Apply(int preset)
        {
            if (preset < Default || preset >= Presets.Length || Settings.NewHoistedFrom == null)
                return;

            PlacementValues p = Presets[preset];
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

            Log.Msg("setting: preset = " + LogName(preset) + " (" + Describe(p) + ")" +
                    (Settings.NewImage.value != 0 ? "; the picture cropped to the new shape" : ""));
            Sync();
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

        /// <summary>For the log: "hoisted from its side, tilted 45 deg, lean 0 deg, a ball-top pole 2.5 m, ropes 0, base 2, at 100% of it, held at its hoist only, 1.5 m x 667".</summary>
        private static string Describe(PlacementValues p)
        {
            CultureInfo inv = CultureInfo.InvariantCulture;
            return "hoisted from its " + (p.HoistedFrom == FlagHoist.Top ? "top" : "side") + ", tilted " + p.Tilt.ToString("0.#", inv) +
                   " deg, lean " + p.Lean.ToString("0.#", inv) + " deg, pole " + FlagPoleStyle.LogName(p.Pole) + " " + p.Height.ToString("0.#", inv) +
                   " m, ropes " + p.Ropes + ", base " + p.PoleBase + ", at " + (p.PolePosition * 100f).ToString("F0", inv) + "% of it, held at " +
                   FlagHold.LogName(p.Held) + ", " + p.Length.ToString("0.##", inv) + " m long at " + FlagProportion.Name(p.Proportion);
        }
    }
}
