using System;
using ColossalFramework;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag
{
    /// <summary>
    /// Persisted options, kept in RealFlag.xml next to the log (see <see cref="SettingsXml"/>),
    /// where a player can read and edit them.
    /// </summary>
    /// <remarks>
    /// Every default is a named constant in <see cref="Defaults"/> and nowhere else: the
    /// constructors below read them, the fallbacks read them, and <see cref="UI.SettingsCatalog"/>
    /// carries the same constant on every row, which is what lets "Reset all settings to
    /// defaults" exist at all. A setting does NOT remember its default.
    ///
    /// The flags themselves are not settings: each is kept with its city (FlagSaveData). What
    /// is here is the wind when it is not the game's, how the NEXT flag is made (the Create new
    /// tab), and the mod itself.
    /// The Existing flags tab's controls are TRANSIENT settings: panel controls over the selected
    /// flag, never written to the file (Volumetric Explosions' launch platform editor).
    ///
    /// The first argument of each constructor is the setting's NAME in the file. When what a
    /// setting means changes, give it a new name.
    /// </remarks>
    public static class Settings
    {
        /// <summary>
        /// The hotkey's home in the game's settings system: registered with dontSave, so it is
        /// never written. NO DOT in this name: the game's PathUtils.AddExtension CHANGES an
        /// extension that is already there (Volumetric Weather lost a settings file to that).
        /// </summary>
        private const string InMemory = "RealFlagHotkey";

        public static BoolSetting ShowInUnifiedUI { get; private set; }
        public static SavedInputKey ToggleKey { get; private set; }

        /// <summary>Where the mod's own button sits when it is not in Unified UI, UI units from the top left.</summary>
        public static FloatSetting HudButtonX { get; private set; }
        public static FloatSetting HudButtonY { get; private set; }

        /// <summary>Writes every periodic line to the log. Off for players.</summary>
        public static BoolSetting DetailedLogging { get; private set; }

        /// <summary>0: the game's own language; otherwise one of <see cref="Localization.LanguageCodes"/>, from 1.</summary>
        public static IntSetting Language { get; private set; }

        /// <summary>The flags stop when the game is paused (a still for screenshots), and wave again when it runs.</summary>
        public static BoolSetting FreezeWhenPaused { get; private set; }

        /// <summary>
        /// The finest cloth a flag gets, cells along its longer side (one of FlagCloth.Details):
        /// the flags big on the screen get it, the others as fine as their size there needs.
        /// </summary>
        public static IntSetting ClothDetail { get; private set; }

        /// <summary>The cloth keeps out of itself (FlagCloth.CollisionThickness): its folds lie on one another instead of through.</summary>
        public static BoolSetting SelfCollision { get; private set; }

        // ---- the wind (the panel's Settings tab) ----

        /// <summary>
        /// The game's wind (GameWind: its direction, its weather, its wind map) -- the default --
        /// or the player's own: the four settings below.
        /// </summary>
        public static BoolSetting WindFromGame { get; private set; }

        /// <summary>The mean wind at 10 m over the ground, m/s: the weather stations' height, the Beaufort scale's.</summary>
        public static FloatSetting WindSpeed { get; private set; }

        /// <summary>How gusty, 0..2: 1 is the turbulence of a town (EN 1991-1-4, terrain category III).</summary>
        public static FloatSetting WindGusts { get; private set; }

        /// <summary>Where the wind blows, stored by VALUE: see <see cref="WindModes"/>.</summary>
        public static IntSetting WindMode { get; private set; }

        /// <summary>With <see cref="WindModes.Fixed"/>: the way the wind blows (and every flag points), degrees, the flags' rotation's.</summary>
        public static FloatSetting WindDirection { get; private set; }

        // ---- Create new: the next flag placed ----

        /// <summary>
        /// A country's flag instead of the pattern: FlagCountries' number for it (0: none, the
        /// pattern). The file keeps its CODE ("fr"): the number is this session's only.
        /// </summary>
        public static IntSetting NewCountry { get; private set; }

        public static IntSetting NewPattern { get; private set; }
        public static IntSetting NewColour1 { get; private set; }
        public static IntSetting NewColour2 { get; private set; }
        public static IntSetting NewColour3 { get; private set; }

        /// <summary>Hoist to fly, metres.</summary>
        public static FloatSetting NewLength { get; private set; }

        /// <summary>Height / length x 1000, one of <see cref="FlagProportion.Values"/>.</summary>
        public static IntSetting NewProportion { get; private set; }

        public static FloatSetting NewWaviness { get; private set; }

        /// <summary>The way the flag points (its prop's rotation), degrees.</summary>
        public static FloatSetting NewRotation { get; private set; }

        /// <summary>The top of the hoist over the ground (or the roof) where it is placed, metres.</summary>
        public static FloatSetting NewHeight { get; private set; }

        // ---- the pole and the hoist's lean (each tab's Flagpole popup) ----

        /// <summary>A FlagPoleStyle value: the pole the mod draws, by its top; 0 none.</summary>
        public static IntSetting NewPole { get; private set; }

        /// <summary>The halyard lines drawn: 0, 1, 2.</summary>
        public static IntSetting NewRopes { get; private set; }
        public static IntSetting NewPoleColour { get; private set; }
        public static IntSetting NewTopColour { get; private set; }
        public static IntSetting NewRopeColour { get; private set; }

        /// <summary>Against a real pole's for the flag and its height, 0.5..2 (shown as a percentage).</summary>
        public static FloatSetting NewPoleThickness { get; private set; }

        /// <summary>The hoist's lean, degrees: towards the fly, and to the side (FlagSpec.Tilt, Lean).</summary>
        public static FloatSetting NewTilt { get; private set; }
        public static FloatSetting NewLean { get; private set; }

        // ---- Existing flags: the selected flag (TRANSIENT) ----

        public static IntSetting SelCountry { get; private set; }
        public static IntSetting SelPattern { get; private set; }
        public static IntSetting SelColour1 { get; private set; }
        public static IntSetting SelColour2 { get; private set; }
        public static IntSetting SelColour3 { get; private set; }
        public static FloatSetting SelLength { get; private set; }
        public static IntSetting SelProportion { get; private set; }
        public static FloatSetting SelWaviness { get; private set; }
        public static FloatSetting SelRotation { get; private set; }
        public static FloatSetting SelHeight { get; private set; }
        public static IntSetting SelPole { get; private set; }
        public static IntSetting SelRopes { get; private set; }
        public static IntSetting SelPoleColour { get; private set; }
        public static IntSetting SelTopColour { get; private set; }
        public static IntSetting SelRopeColour { get; private set; }
        public static FloatSetting SelPoleThickness { get; private set; }
        public static FloatSetting SelTilt { get; private set; }
        public static FloatSetting SelLean { get; private set; }

        /// <summary>The wind's direction: along each flag's own rotation, one compass direction, or the game's.</summary>
        public static class WindModes
        {
            public const int EachFlag = 0;
            public const int Fixed = 1;
            public const int Game = 2;

            public static readonly int[] Values = { EachFlag, Fixed, Game };
            public static readonly string[] NameKeys = { "WindMode.EachFlag", "WindMode.Fixed", "WindMode.Game" };

            public static string LogName(int value)
            {
                return value == Fixed ? "fixed" : value == Game ? "the game's" : "each flag's own";
            }
        }

        public static class Defaults
        {
            // ---- housekeeping ----
            public const bool ShowInUnifiedUI = true;
            public const bool DetailedLogging = false;

            /// <summary>Under Volumetric Weather's (10, 60) and Volumetric Explosions' (10, 100) buttons.</summary>
            public const float HudButtonX = 10f;
            public const float HudButtonY = 140f;

            public const int Language = 0;
            public const bool FreezeWhenPaused = true;

            /// <summary>Normal: 18 cells along, the cloth as it was first made.</summary>
            public const int ClothDetail = FlagCloth.CellsAlong;
            public const bool SelfCollision = true;

            // ---- the wind ----
            /// <summary>The game's wind, until the player sets their own.</summary>
            public const bool WindFromGame = true;

            /// <summary>m/s: Beaufort 3, "gentle breeze" -- "wind extends light flag".</summary>
            public const float WindSpeed = 5f;
            public const float WindGusts = 1f;
            public const int WindMode = WindModes.EachFlag;
            public const float WindDirection = 0f;

            // ---- Create new ----
            /// <summary>None: the pattern below.</summary>
            public const int Country = 0;
            public const int Pattern = FlagPattern.Default;
            public const int Colour1 = (int)FlagColours.DefaultColour1;
            public const int Colour2 = (int)FlagColours.DefaultColour2;
            public const int Colour3 = (int)FlagColours.DefaultColour3;

            /// <summary>m: a 2 x 3 m flag, the size flown on a town's 8-12 m poles.</summary>
            public const float Length = 3f;
            public const int Proportion = FlagProportion.Default;
            public const float Waviness = 1f;
            public const float Rotation = 0f;

            /// <summary>m: the top of an ordinary flagpole.</summary>
            public const float Height = 10f;

            /// <summary>No pole drawn: the flag is for the city's own poles, the mod's purpose.</summary>
            public const int Pole = FlagPoleStyle.None;
            public const int Ropes = FlagSpec.DefaultRopes;
            public const int PoleColour = (int)FlagSpec.DefaultPoleColour;
            public const int TopColour = (int)FlagSpec.DefaultTopColour;
            public const int RopeColour = (int)FlagSpec.DefaultRopeColour;
            public const float PoleThickness = 1f;
            public const float Tilt = 0f;
            public const float Lean = 0f;
        }

        private static bool _initialised;

        /// <summary>True once every setting exists (the file is read straight after).</summary>
        public static bool IsInitialised
        {
            get { return _initialised; }
        }

        /// <summary>
        /// Constructs every setting with its default, then reads RealFlag.xml over them. Called
        /// from Mod.OnEnabled, and defensively from everything that needs a setting before that
        /// could have run.
        /// </summary>
        public static void Init()
        {
            if (_initialised)
                return;

            try
            {
                // Looked up first because a reloaded mod finds it already there, and adding a
                // name twice throws.
                if (GameSettings.FindSettingsFileByName(InMemory) == null)
                    GameSettings.AddSettingsFile(new SettingsFile { fileName = InMemory, dontSave = true });

                ShowInUnifiedUI = new BoolSetting("ShowInUnifiedUI", Defaults.ShowInUnifiedUI);
                ToggleKey = new SavedInputKey("ToggleKey", InMemory, DefaultToggleKey, false);
                DetailedLogging = new BoolSetting("DetailedLogging", Defaults.DetailedLogging);
                Language = new IntSetting("Language", Defaults.Language);
                FreezeWhenPaused = new BoolSetting("FreezeWhenPaused", Defaults.FreezeWhenPaused);
                ClothDetail = new IntSetting("ClothDetail", Defaults.ClothDetail);
                SelfCollision = new BoolSetting("SelfCollision", Defaults.SelfCollision);

                WindFromGame = new BoolSetting("WindFromGame", Defaults.WindFromGame);
                WindSpeed = new FloatSetting("WindSpeed", Defaults.WindSpeed);
                WindGusts = new FloatSetting("WindGusts", Defaults.WindGusts);
                WindMode = new IntSetting("WindMode", Defaults.WindMode);
                WindDirection = new FloatSetting("WindDirection", Defaults.WindDirection);

                NewCountry = new IntSetting("NewCountry", Defaults.Country);
                NewPattern = new IntSetting("NewPattern", Defaults.Pattern);
                NewColour1 = new IntSetting("NewColour1", Defaults.Colour1);
                NewColour2 = new IntSetting("NewColour2", Defaults.Colour2);
                NewColour3 = new IntSetting("NewColour3", Defaults.Colour3);
                NewLength = new FloatSetting("NewLength", Defaults.Length);
                NewProportion = new IntSetting("NewProportion", Defaults.Proportion);
                NewWaviness = new FloatSetting("NewWaviness", Defaults.Waviness);
                NewRotation = new FloatSetting("NewRotation", Defaults.Rotation);
                NewHeight = new FloatSetting("NewHeight", Defaults.Height);
                NewPole = new IntSetting("NewPole", Defaults.Pole);
                NewRopes = new IntSetting("NewRopes", Defaults.Ropes);
                NewPoleColour = new IntSetting("NewPoleColour", Defaults.PoleColour);
                NewTopColour = new IntSetting("NewTopColour", Defaults.TopColour);
                NewRopeColour = new IntSetting("NewRopeColour", Defaults.RopeColour);
                NewPoleThickness = new FloatSetting("NewPoleThickness", Defaults.PoleThickness);
                NewTilt = new FloatSetting("NewTilt", Defaults.Tilt);
                NewLean = new FloatSetting("NewLean", Defaults.Lean);

                SelCountry = new IntSetting("SelCountry", Defaults.Country, true);
                SelPattern = new IntSetting("SelPattern", Defaults.Pattern, true);
                SelColour1 = new IntSetting("SelColour1", Defaults.Colour1, true);
                SelColour2 = new IntSetting("SelColour2", Defaults.Colour2, true);
                SelColour3 = new IntSetting("SelColour3", Defaults.Colour3, true);
                SelLength = new FloatSetting("SelLength", Defaults.Length, true);
                SelProportion = new IntSetting("SelProportion", Defaults.Proportion, true);
                SelWaviness = new FloatSetting("SelWaviness", Defaults.Waviness, true);
                SelRotation = new FloatSetting("SelRotation", Defaults.Rotation, true);
                SelHeight = new FloatSetting("SelHeight", Defaults.Height, true);
                SelPole = new IntSetting("SelPole", Defaults.Pole, true);
                SelRopes = new IntSetting("SelRopes", Defaults.Ropes, true);
                SelPoleColour = new IntSetting("SelPoleColour", Defaults.PoleColour, true);
                SelTopColour = new IntSetting("SelTopColour", Defaults.TopColour, true);
                SelRopeColour = new IntSetting("SelRopeColour", Defaults.RopeColour, true);
                SelPoleThickness = new FloatSetting("SelPoleThickness", Defaults.PoleThickness, true);
                SelTilt = new FloatSetting("SelTilt", Defaults.Tilt, true);
                SelLean = new FloatSetting("SelLean", Defaults.Lean, true);

                HudButtonX = new FloatSetting("HudButtonX", Defaults.HudButtonX);
                HudButtonY = new FloatSetting("HudButtonY", Defaults.HudButtonY);

                // Before the file is read: reading it walks the catalog, and building the
                // catalog calls Init.
                _initialised = true;
            }
            catch (Exception e)
            {
                Debug.LogError("[RealFlag] Could not initialise settings.");
                Debug.LogException(e);
                return;
            }

            SettingsXml.Load();
        }

        /// <summary>
        /// The Create new tab's flag, as a spec: what the next placed flag will be, and what a
        /// flag prop with no spec of its own (placed by Find It, say) is given.
        /// </summary>
        public static FlagSpec NewSpec()
        {
            FlagSpec spec = FlagSpec.Default;
            if (!_initialised)
                return spec;

            FlagCountry country = FlagCountries.At(NewCountry.value);
            spec.Country = country != null ? country.Code : string.Empty;
            spec.Pattern = NewPattern.value;
            spec.Colour1 = (uint)NewColour1.value;
            spec.Colour2 = (uint)NewColour2.value;
            spec.Colour3 = (uint)NewColour3.value;
            spec.Length = NewLength.value;
            spec.Proportion = NewProportion.value;
            spec.Waviness = NewWaviness.value;
            spec.Pole = NewPole.value;
            spec.Ropes = NewRopes.value;
            spec.PoleColour = (uint)NewPoleColour.value;
            spec.TopColour = (uint)NewTopColour.value;
            spec.RopeColour = (uint)NewRopeColour.value;
            spec.PoleThickness = NewPoleThickness.value;
            spec.Tilt = NewTilt.value;
            spec.Lean = NewLean.value;
            return spec.Sanitised();
        }

        /// <summary>F6. A property because SavedInputKey.Encode is a method, so it cannot be a const.</summary>
        /// <remarks>
        /// Free in the game: its default keys (DefaultSettings' static constructor, read from the
        /// IL for Volumetric Explosions) use F1 for the quick save, F7 for the debug panel and F12
        /// for screenshots, and nothing else in F1..F12. Volumetric Weather has F4, Volumetric
        /// Explosions F3.
        /// </remarks>
        public static int DefaultToggleKey
        {
            get { return SavedInputKey.Encode(KeyCode.F6, false, false, false); }
        }
    }
}
