using System;
using ColossalFramework;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag
{
    /// <summary>
    /// Persisted options, kept in FlagMaker.xml next to the log (see <see cref="SettingsXml"/>),
    /// where a player can read and edit them.
    /// </summary>
    /// <remarks>
    /// Every default is a named constant in <see cref="Defaults"/> and nowhere else: the
    /// constructors below read them, the fallbacks read them, and <see cref="UI.SettingsCatalog"/>
    /// carries the same constant on every row, which is what lets "Reset all settings to
    /// defaults" exist at all. A setting does NOT remember its default.
    ///
    /// The flags themselves are not settings: each is kept with its city (FlagSaveData). What
    /// is here is the wind when it is not the game's, how the NEXT flag is made (the New flag
    /// window), and the mod itself.
    /// The Flags tab's controls are TRANSIENT settings: panel controls over the selected flag,
    /// never written to the file (Volumetric Explosions' launch platform editor).
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

        /// <summary>
        /// Where the panel's button goes, stored by VALUE (append only; <see cref="IconPlaces"/>):
        /// Unified UI's bar (when that is there, else the HUD button), the HUD button, or none (the
        /// key alone opens the panel). Real Glass's and Volumetric Weather's option.
        /// </summary>
        public static IntSetting IconPlace { get; private set; }

        /// <summary>The values of <see cref="IconPlace"/>, as stored. Never renumber.</summary>
        public static class IconPlaces
        {
            public const int UnifiedUI = 0;
            public const int Toolbar = 1;
            public const int Never = 2;
        }

        public static SavedInputKey ToggleKey { get; private set; }

        /// <summary>Where the mod's own button sits when it is not in Unified UI, UI units from the top left.</summary>
        public static FloatSetting HudButtonX { get; private set; }
        public static FloatSetting HudButtonY { get; private set; }

        /// <summary>Writes every periodic line to the log. Off for players.</summary>
        public static BoolSetting DetailedLogging { get; private set; }

        /// <summary>0: the game's own language; otherwise one of <see cref="Localization.LanguageCodes"/>, from 1.</summary>
        public static IntSetting Language { get; private set; }

        /// <summary>
        /// THE PANELS' LOOK (ModsLibCS1.UI.Skin), stored by VALUE (append only): 0 See-through, 1
        /// Game blue, 2 Vanilla (the panels as they were), 3 Frosted, the default -- Volumetric
        /// Weather's and Real Glass's row. Every window of the panel and the HUD button wear it.
        /// </summary>
        public static IntSetting PanelSkin { get; private set; }

        /// <summary>The flags stop when the game is paused (a still for screenshots), and wave again when it runs.</summary>
        public static BoolSetting FreezeWhenPaused { get; private set; }

        /// <summary>
        /// How wet the flags get in the rain, 0..1 of the game's ground wetness (the author,
        /// 2026-11-11: "in rain, the flags look too wet ... by default it's around 10-20%"): the
        /// shader's RFWetness times this (_RFWetScale, set every frame by FlagRenderer.SetSun).
        /// </summary>
        public static FloatSetting RainWetness { get; private set; }

        /// <summary>
        /// The finest cloth a flag gets, cells along its longer side (one of FlagCloth.Details):
        /// the flags big on the screen get it, the others as fine as their size there needs.
        /// </summary>
        public static IntSetting ClothDetail { get; private set; }

        /// <summary>The cloth keeps out of itself (FlagCloth.CollisionThickness): its folds lie on one another instead of through.</summary>
        public static BoolSetting SelfCollision { get; private set; }

        /// <summary>The cloth keeps off the city (FlagSurroundings): the ground, water and roofs under it, walls, props and trees.</summary>
        public static BoolSetting CityCollision { get; private set; }

        /// <summary>Buildings keep the cloth off by their drawn shapes (BuildingShapes), not their simple LOD ones.</summary>
        public static BoolSetting DetailedBuildings { get; private set; }

        /// <summary>
        /// The threads that step the cloth (ClothWorker.ThreadChoices): 0 = Auto (one per six
        /// processors, two from eight, at most four), else the player's number -- more threads, more flags moving
        /// at full detail, more of the CPU (the author: "users can choose willingly to utilize
        /// their hardware").
        /// </summary>
        public static IntSetting ClothThreads { get; private set; }

        /// <summary>
        /// Shared motion (MotionRecord): with more flags in view than the cloth threads have time
        /// for, like flags play back one cloth's motion, each at its own delay, before any goes
        /// coarser. On by default (the author's choice, 2026-10-31); off, each flag its own cloth.
        /// </summary>
        public static BoolSetting SharedMotion { get; private set; }

        /// <summary>
        /// Loops stand in (ClothPlan.StandsIn, FlagRenderer.PlanStandIn): a waving flag the cloth
        /// threads have no room to move plays a loop instead of standing frozen at its last step,
        /// and waves again when it has room. On by default (the author, 2026-11-11: "it's a huge
        /// fix"); off, such flags stand still as before. Nothing of it is in a save.
        /// </summary>
        public static BoolSetting LoopStandIns { get; private set; }

        /// <summary>
        /// Crowds placed as Loop (FlagRenderer.Crowd.cs, ClothPlan.CrowdMost): a new waving flag with
        /// many waving flags near it, or a big Line Tool row, switches the new flag's Motion to
        /// Loop, and says so. On by default (the author, 2026-11-12: players "are lazy and won't
        /// bother changing the motion"); off, new flags keep the Motion chosen.
        /// </summary>
        public static BoolSetting CrowdLoops { get; private set; }

        /// <summary>The player has been told once, in a message, why their flags went to Loop. Kept in the file, no row.</summary>
        public static BoolSetting CrowdLoopsTold { get; private set; }

        /// <summary>
        /// Gusts and eddies of several sizes in a waving flag's wind (ClothJob.Varied, FlagWind's
        /// varied air, 2026-11-14): the same energy shared over more sizes, so a flag changes its
        /// posture every few seconds instead of holding one. On by default; kept in the file, no
        /// row -- there to be switched in the live-read file while watching the game.
        /// </summary>
        public static BoolSetting VariedWind { get; private set; }

        /// <summary>
        /// The panels' groups folded away (the author, 2026-11-11: "let's make this collapsible,
        /// like the menu in RealGlass"): a bit a group, by <see cref="FoldGroups"/>. Kept in the
        /// file, no row; Reset all opens them as at first.
        /// </summary>
        public static IntSetting PanelFolded { get; private set; }

        /// <summary>The groups that fold, by their bit in <see cref="PanelFolded"/>. Append only: the file keeps the bits.</summary>
        public static readonly string[] FoldGroups =
        {
            "Design", "Size", "Place", "Movement", "Wear", "Wind", "Language", "Panel", "Flags", "Cloth", "Diagnostics", "StartingOver",
        };

        /// <summary>
        /// The overall Quality (UI.QualityPreset: Low .. Ultra, or Custom), DERIVED from the cloth
        /// detail, self-collision, shared motion and buildings as drawn -- never in the file;
        /// choosing a level sets those four.
        /// </summary>
        public static IntSetting Quality { get; private set; }

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

        // ---- the New flag window: the next flag placed ----

        /// <summary>
        /// A country's flag instead of the pattern: FlagCountries' number for it (0: none, the
        /// pattern). The file keeps its CODE ("fr"): the number is this session's only.
        /// </summary>
        public static IntSetting NewCountry { get; private set; }

        /// <summary>
        /// A picture of the player's (a file in FlagMaker\Images): FlagImageNames' number for its
        /// name (0: none). The file keeps its NAME: the number is this session's only.
        /// </summary>
        public static IntSetting NewImage { get; private set; }

        /// <summary>Where the picture lies on the flag (FlagSpec.ImageX, ImageY, ImageZoom, ImageTurn), and what its see-through parts do (a FlagImageAlpha value). The Image window sets them.</summary>
        public static FloatSetting NewImageX { get; private set; }
        public static FloatSetting NewImageY { get; private set; }
        public static FloatSetting NewImageZoom { get; private set; }
        public static IntSetting NewImageTurn { get; private set; }
        public static IntSetting NewImageAlpha { get; private set; }

        public static IntSetting NewPattern { get; private set; }
        public static IntSetting NewColour1 { get; private set; }
        public static IntSetting NewColour2 { get; private set; }
        public static IntSetting NewColour3 { get; private set; }

        /// <summary>Hoist to fly, metres (the Width and Height rows set it: FlagSize).</summary>
        public static FloatSetting NewLength { get; private set; }

        /// <summary>Height / length x 1000, FlagSize.MinProportion to MaxProportion (the Width, Height and Shape rows set it).</summary>
        public static IntSetting NewProportion { get; private set; }

        /// <summary>The edge held on the pole: a FlagHoist value (the side, or the top: a banner).</summary>
        public static IntSetting NewHoistedFrom { get; private set; }

        /// <summary>
        /// A flag's width and height change together, its shape kept (the author: "by default the
        /// aspect is locked, so if they change Width the height changes too, it's linked"). One
        /// switch for the new flag and the selected one.
        /// </summary>
        public static BoolSetting LinkSize { get; private set; }

        /// <summary>Whether the flag moves: a FlagMotion value (it waves, or keeps one shape: still, or flat).</summary>
        public static IntSetting NewMotion { get; private set; }

        public static FloatSetting NewWaviness { get; private set; }

        /// <summary>The flag's cut (a FlagCut value: rectangle, swallowtail, pennant...) and its notch's or point's depth, a share of the length (FlagSpec.Cut, CutDepth).</summary>
        public static IntSetting NewCut { get; private set; }
        public static FloatSetting NewCutDepth { get; private set; }

        /// <summary>How much of the wind a waving flag feels, 0..1 (FlagSpec.WindShare; the panel's "Wind on it", a percentage).</summary>
        public static FloatSetting NewWindShare { get; private set; }

        /// <summary>How much darker the cloth is than its design, 0..0.8 (FlagSpec.Darkness; shown as a percentage).</summary>
        public static FloatSetting NewDarkness { get; private set; }

        /// <summary>The cloth's wear and tear, dirt and holes, 0..1 each (FlagSpec.Wear, Dirt, Holes; shown as percentages).</summary>
        public static FloatSetting NewWear { get; private set; }
        public static FloatSetting NewDirt { get; private set; }
        public static FloatSetting NewHoles { get; private set; }

        /// <summary>How torn, 0..1 (FlagSpec.Tear; shown as a percentage).</summary>
        public static FloatSetting NewTear { get; private set; }

        /// <summary>How torn the top and bottom edges are, 0..1 (FlagSpec.TearEdges; shown as a percentage).</summary>
        public static FloatSetting NewTearEdges { get; private set; }

        /// <summary>The way the flag points (its prop's rotation), degrees.</summary>
        public static FloatSetting NewRotation { get; private set; }

        /// <summary>One flag at a time, or a row by Line Tool (LineFlags' values). Starts at one flag each time the New flag window opens.</summary>
        public static IntSetting NewPlacing { get; private set; }

        /// <summary>A row's flags this far apart, metres.</summary>
        public static FloatSetting NewLineSpacing { get; private set; }

        /// <summary>A row's flags point along its line, the Rotation on top; off, the Rotation alone.</summary>
        public static BoolSetting NewLineAlong { get; private set; }

        /// <summary>A new banner hoisted from its top snaps to a roof's edge near the pointer (FlagEdgeSnap).</summary>
        public static BoolSetting NewSnapEdges { get; private set; }

        /// <summary>A banner hoisted from its top keeps in front of an unseen plate through its hoist, never swinging back into a wall (FlagSpec.BackPlate).</summary>
        public static BoolSetting NewBackPlate { get; private set; }

        /// <summary>
        /// The new flag's POLE HEIGHT: its length up its axis from its foot to the flag's top,
        /// metres (FlagSpec.PoleLength, for a pole that stands: FlagStand). Its name is the old
        /// Elevation's (the flag's top over what was under it), whose value the file keeps.
        /// </summary>
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

        /// <summary>The pole's FlagPoleProfile and FlagPoleBase; its shine, wear, rust and dirt 0..1; the ropes' thickness 0.5..3.</summary>
        public static IntSetting NewPoleProfile { get; private set; }
        public static IntSetting NewPoleBase { get; private set; }
        public static FloatSetting NewPoleShine { get; private set; }
        public static FloatSetting NewPoleWear { get; private set; }
        public static FloatSetting NewPoleRust { get; private set; }
        public static FloatSetting NewPoleDirt { get; private set; }
        public static FloatSetting NewRopeThickness { get; private set; }

        /// <summary>Where the new flag flies on its standing pole, 0..1 up it (FlagSpec.PolePosition: 1 the top, 0.5 half-mast).</summary>
        public static FloatSetting NewPolePosition { get; private set; }
        public static FloatSetting SelPolePosition { get; private set; }

        /// <summary>
        /// Which take of its motion a still or looping flag shows (FlagSpec.Take): a new one each
        /// time the flag is made still or a loop, or Recapture is pressed. No row of its own.
        /// </summary>
        public static IntSetting NewTake { get; private set; }
        public static IntSetting SelTake { get; private set; }

        /// <summary>What holds the cloth besides its hoist (FlagSpec.Held: FlagHold bits), the "Also held at" row's toggles.</summary>
        public static IntSetting NewHeld { get; private set; }
        public static IntSetting SelHeld { get; private set; }

        /// <summary>The hoist's lean, degrees: towards the fly, and to the side (FlagSpec.Tilt, Lean).</summary>
        public static FloatSetting NewTilt { get; private set; }
        public static FloatSetting NewLean { get; private set; }

        // ---- Existing flags: the selected flag (TRANSIENT) ----

        public static IntSetting SelCountry { get; private set; }
        public static IntSetting SelImage { get; private set; }
        public static FloatSetting SelImageX { get; private set; }
        public static FloatSetting SelImageY { get; private set; }
        public static FloatSetting SelImageZoom { get; private set; }
        public static IntSetting SelImageTurn { get; private set; }
        public static IntSetting SelImageAlpha { get; private set; }
        public static IntSetting SelPattern { get; private set; }
        public static IntSetting SelColour1 { get; private set; }
        public static IntSetting SelColour2 { get; private set; }
        public static IntSetting SelColour3 { get; private set; }
        public static FloatSetting SelLength { get; private set; }
        public static IntSetting SelProportion { get; private set; }
        public static IntSetting SelHoistedFrom { get; private set; }
        public static IntSetting SelMotion { get; private set; }
        public static FloatSetting SelWaviness { get; private set; }
        public static IntSetting SelCut { get; private set; }
        public static FloatSetting SelCutDepth { get; private set; }
        public static FloatSetting SelWindShare { get; private set; }
        public static FloatSetting SelDarkness { get; private set; }
        public static FloatSetting SelWear { get; private set; }
        public static FloatSetting SelDirt { get; private set; }
        public static FloatSetting SelHoles { get; private set; }
        public static FloatSetting SelTear { get; private set; }
        public static FloatSetting SelTearEdges { get; private set; }
        public static FloatSetting SelRotation { get; private set; }
        public static BoolSetting SelBackPlate { get; private set; }

        /// <summary>The selected flag's pole height (FlagSpec.PoleLength): changed, its foot stays and the flag goes up or down with the pole's top.</summary>
        public static FloatSetting SelHeight { get; private set; }
        public static IntSetting SelPole { get; private set; }
        public static IntSetting SelRopes { get; private set; }
        public static IntSetting SelPoleColour { get; private set; }
        public static IntSetting SelTopColour { get; private set; }
        public static IntSetting SelRopeColour { get; private set; }
        public static FloatSetting SelPoleThickness { get; private set; }
        public static IntSetting SelPoleProfile { get; private set; }
        public static IntSetting SelPoleBase { get; private set; }
        public static FloatSetting SelPoleShine { get; private set; }
        public static FloatSetting SelPoleWear { get; private set; }
        public static FloatSetting SelPoleRust { get; private set; }
        public static FloatSetting SelPoleDirt { get; private set; }
        public static FloatSetting SelRopeThickness { get; private set; }
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
            public const int IconPlace = IconPlaces.UnifiedUI;
            public const bool DetailedLogging = false;

            /// <summary>Under Volumetric Weather's (10, 60) and Volumetric Explosions' (10, 100) buttons.</summary>
            public const float HudButtonX = 10f;
            public const float HudButtonY = 140f;

            public const int Language = 0;

            /// <summary>Frosted glass (ModsLibCS1.UI.Skin.Frosted), as in Volumetric Weather and Real Glass.</summary>
            public const int PanelSkin = 3;
            public const bool FreezeWhenPaused = true;

            /// <summary>15% of the ground's wetness (the author: "by default it's around 10-20%"; all of it before 2026-11-11).</summary>
            public const float RainWetness = 0.15f;

            /// <summary>High: 32 cells along (2026-10-01, the author's choice; it was 18, now Low).</summary>
            public const int ClothDetail = 32;
            public const bool SelfCollision = true;
            public const bool CityCollision = true;

            /// <summary>On (2026-11-07): part of the Quality preset's High (the defaults) and up (UI.QualityPreset).</summary>
            public const bool DetailedBuildings = true;

            /// <summary>Auto (the author's choice, 2026-10-31: players raise it themselves).</summary>
            public const int ClothThreads = 0;
            public const bool SharedMotion = true;
            public const bool LoopStandIns = true;
            public const bool CrowdLoops = true;
            public const bool CrowdLoopsTold = false;
            public const bool VariedWind = true;

            /// <summary>
            /// Folded at first: Wear and tear (most flags are new) and the Settings tab's Language,
            /// In-game panel, Diagnostics and Starting over (set once). Bits by FoldGroups:
            /// Wear 16, Language 64, Panel 128, Diagnostics 1024, StartingOver 2048.
            /// </summary>
            public const int PanelFolded = 16 | 64 | 128 | 1024 | 2048;

            // ---- the wind ----
            /// <summary>The game's wind, until the player sets their own.</summary>
            public const bool WindFromGame = true;

            /// <summary>m/s: Beaufort 3, "gentle breeze" -- "wind extends light flag".</summary>
            public const float WindSpeed = 5f;
            public const float WindGusts = 1f;
            public const int WindMode = WindModes.EachFlag;
            public const float WindDirection = 0f;

            // ---- the new flag ----
            /// <summary>None: the pattern below.</summary>
            public const int Country = 0;

            /// <summary>No picture; laid as a picture just picked is: filling the flag, its middle in the middle, upright, see-through parts cut out.</summary>
            public const int Image = 0;
            public const float ImageX = 0.5f;
            public const float ImageY = 0.5f;
            public const float ImageZoom = 1f;
            public const int ImageTurn = 0;
            public const int ImageAlpha = FlagImageAlpha.Cut;
            public const int Pattern = FlagPattern.Default;
            public const int Colour1 = (int)FlagColours.DefaultColour1;
            public const int Colour2 = (int)FlagColours.DefaultColour2;
            public const int Colour3 = (int)FlagColours.DefaultColour3;

            /// <summary>m: a 2 x 3 m flag, the size flown on a town's 8-12 m poles.</summary>
            public const float Length = 3f;
            public const int Proportion = FlagProportion.Default;

            /// <summary>From its side, as flags fly.</summary>
            public const int HoistedFrom = FlagHoist.Side;

            /// <summary>Width and height together.</summary>
            public const bool LinkSize = true;

            /// <summary>
            /// Waving (2026-11-13, the author: "Let's make Waving the default again"). A loop for one
            /// day before (2026-11-12: a loop costs almost nothing, a waving cloth a thread's time);
            /// crowds of waving flags are still placed as Loop (Settings.CrowdLoops). FlagSpec.Default
            /// and the older formats are Waves too: a saved flag keeps its own.
            /// </summary>
            public const int Motion = FlagMotion.Waves;
            public const float Waviness = 1f;

            /// <summary>A rectangle, as every flag was; a cut's notch or point a third of the length.</summary>
            public const int Cut = FlagCut.Rectangle;
            public const float CutDepth = FlagCut.DefaultDepth;

            /// <summary>All the wind where it stands.</summary>
            public const float WindShare = 1f;

            /// <summary>
            /// 30% (the author, 2026-11-10: "Darkness: should be by default 30%"): the colours as
            /// designed are bright in the game's light. A save's flag keeps its own; a flag of an
            /// older format (before 4) reads 0, as designed.
            /// </summary>
            public const float Darkness = 0.3f;

            /// <summary>As new: no wear, dirt or holes.</summary>
            public const float Wear = 0f;
            public const float Dirt = 0f;
            public const float Holes = 0f;
            public const float Tear = 0f;
            public const float TearEdges = 0f;
            public const float Rotation = 0f;
            public const float LineSpacing = 10f;
            public const bool LineAlong = true;

            /// <summary>Off (the author, 2026-11-09: "snap to edges should be off by default").</summary>
            public const bool SnapEdges = false;

            /// <summary>On: a banner from its top never swings back behind its hoist (the author, 2026-11-09).</summary>
            public const bool BackPlate = true;

            /// <summary>m: an ordinary flagpole, its foot to the flag's top.</summary>
            public const float Height = 10f;

            /// <summary>m: the tallest pole the file's value (and a copied pole) may be.</summary>
            public const float MaxNewHeight = 200f;

            /// <summary>m: the pole height's slider reaches this high -- a tall city flagpole; 1..200 m was too coarse to set (2026-10-22, the author). Its "Allow taller poles" switch went 2026-11-11.</summary>
            public const float LockedHeight = 50f;

            /// <summary>No pole drawn: the flag is for the city's own poles, the mod's purpose.</summary>
            public const int Pole = FlagPoleStyle.None;
            public const int Ropes = FlagSpec.DefaultRopes;
            public const int PoleColour = (int)FlagSpec.DefaultPoleColour;
            public const int TopColour = (int)FlagSpec.DefaultTopColour;
            public const int RopeColour = (int)FlagSpec.DefaultRopeColour;
            public const float PoleThickness = 1f;

            /// <summary>A tapered satin pole on a flash collar, as new, its ropes as they were: every pole before 2026-11-09.</summary>
            public const int PoleProfile = FlagPoleProfile.Tapered;
            public const int PoleBase = FlagPoleBase.Collar;
            public const float PoleShine = FlagSpec.DefaultPoleShine;
            public const float PoleWear = 0f;
            public const float PoleRust = 0f;
            public const float PoleDirt = 0f;
            public const float RopeThickness = 1f;

            /// <summary>At the top of its pole (full mast).</summary>
            public const float PolePosition = 1f;

            /// <summary>The first take: every flag's still shape and loop before 2026-11-11.</summary>
            public const int Take = 0;

            /// <summary>Held at its hoist alone.</summary>
            public const int Held = 0;
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
        /// Constructs every setting with its default, then reads FlagMaker.xml over them. Called
        /// from Mod.OnEnabled, and defensively from everything that needs a setting before that
        /// could have run.
        /// </summary>
        public static void Init()
        {
            if (_initialised)
                return;

            // Here, whoever comes first (the options page can): before anything reads these files,
            // or a fresh FlagMaker.xml of defaults would stand in the way of the old one.
            OldNames.Move();

            try
            {
                // Looked up first because a reloaded mod finds it already there, and adding a
                // name twice throws.
                if (GameSettings.FindSettingsFileByName(InMemory) == null)
                    GameSettings.AddSettingsFile(new SettingsFile { fileName = InMemory, dontSave = true });

                IconPlace = new IntSetting("IconPlace", Defaults.IconPlace);
                ToggleKey = new SavedInputKey("ToggleKey", InMemory, DefaultToggleKey, false);
                DetailedLogging = new BoolSetting("DetailedLogging", Defaults.DetailedLogging);
                Language = new IntSetting("Language", Defaults.Language);
                PanelSkin = new IntSetting("PanelSkin", Defaults.PanelSkin);
                FreezeWhenPaused = new BoolSetting("FreezeWhenPaused", Defaults.FreezeWhenPaused);
                RainWetness = new FloatSetting("RainWetness", Defaults.RainWetness);
                ClothDetail = new IntSetting("ClothDetail", Defaults.ClothDetail);
                SelfCollision = new BoolSetting("SelfCollision", Defaults.SelfCollision);
                CityCollision = new BoolSetting("CityCollision", Defaults.CityCollision);
                DetailedBuildings = new BoolSetting("DetailedBuildings", Defaults.DetailedBuildings);
                ClothThreads = new IntSetting("ClothThreads", Defaults.ClothThreads);
                SharedMotion = new BoolSetting("SharedMotion", Defaults.SharedMotion);
                LoopStandIns = new BoolSetting("LoopStandIns", Defaults.LoopStandIns);
                CrowdLoops = new BoolSetting("CrowdLoops", Defaults.CrowdLoops);
                CrowdLoopsTold = new BoolSetting("CrowdLoopsTold", Defaults.CrowdLoopsTold);
                VariedWind = new BoolSetting("VariedWind", Defaults.VariedWind);
                PanelFolded = new IntSetting("PanelFolded", Defaults.PanelFolded);
                Quality = new IntSetting("Quality", UI.QualityPreset.High, true);

                WindFromGame = new BoolSetting("WindFromGame", Defaults.WindFromGame);
                WindSpeed = new FloatSetting("WindSpeed", Defaults.WindSpeed);
                WindGusts = new FloatSetting("WindGusts", Defaults.WindGusts);
                WindMode = new IntSetting("WindMode", Defaults.WindMode);
                WindDirection = new FloatSetting("WindDirection", Defaults.WindDirection);

                NewCountry = new IntSetting("NewCountry", Defaults.Country);
                NewImage = new IntSetting("NewImage", Defaults.Image);
                NewImageX = new FloatSetting("NewImageX", Defaults.ImageX);
                NewImageY = new FloatSetting("NewImageY", Defaults.ImageY);
                NewImageZoom = new FloatSetting("NewImageZoom", Defaults.ImageZoom);
                NewImageTurn = new IntSetting("NewImageTurn", Defaults.ImageTurn);
                NewImageAlpha = new IntSetting("NewImageAlpha", Defaults.ImageAlpha);
                NewPattern = new IntSetting("NewPattern", Defaults.Pattern);
                NewColour1 = new IntSetting("NewColour1", Defaults.Colour1);
                NewColour2 = new IntSetting("NewColour2", Defaults.Colour2);
                NewColour3 = new IntSetting("NewColour3", Defaults.Colour3);
                NewLength = new FloatSetting("NewLength", Defaults.Length);
                NewProportion = new IntSetting("NewProportion", Defaults.Proportion);
                NewHoistedFrom = new IntSetting("NewHoistedFrom", Defaults.HoistedFrom);
                LinkSize = new BoolSetting("LinkSize", Defaults.LinkSize);
                NewMotion = new IntSetting("NewMotion", Defaults.Motion);
                NewWaviness = new FloatSetting("NewWaviness", Defaults.Waviness);
                NewCut = new IntSetting("NewCut", Defaults.Cut);
                NewCutDepth = new FloatSetting("NewCutDepth", Defaults.CutDepth);
                NewWindShare = new FloatSetting("NewWindShare", Defaults.WindShare);
                NewDarkness = new FloatSetting("NewDarkness", Defaults.Darkness);
                NewWear = new FloatSetting("NewWear", Defaults.Wear);
                NewDirt = new FloatSetting("NewDirt", Defaults.Dirt);
                NewHoles = new FloatSetting("NewHoles", Defaults.Holes);
                NewTear = new FloatSetting("NewTear", Defaults.Tear);
                NewTearEdges = new FloatSetting("NewTearEdges", Defaults.TearEdges);
                NewRotation = new FloatSetting("NewRotation", Defaults.Rotation);
                NewPlacing = new IntSetting("NewPlacing", LineFlags.Single, true);
                NewLineSpacing = new FloatSetting("NewLineSpacing", Defaults.LineSpacing);
                NewLineAlong = new BoolSetting("NewLineAlong", Defaults.LineAlong);
                NewSnapEdges = new BoolSetting("NewSnapEdges", Defaults.SnapEdges);
                NewBackPlate = new BoolSetting("NewBackPlate", Defaults.BackPlate);
                NewHeight = new FloatSetting("NewHeight", Defaults.Height);
                NewPole = new IntSetting("NewPole", Defaults.Pole);
                NewRopes = new IntSetting("NewRopes", Defaults.Ropes);
                NewPoleColour = new IntSetting("NewPoleColour", Defaults.PoleColour);
                NewTopColour = new IntSetting("NewTopColour", Defaults.TopColour);
                NewRopeColour = new IntSetting("NewRopeColour", Defaults.RopeColour);
                NewPoleThickness = new FloatSetting("NewPoleThickness", Defaults.PoleThickness);
                NewPoleProfile = new IntSetting("NewPoleProfile", Defaults.PoleProfile);
                NewPoleBase = new IntSetting("NewPoleBase", Defaults.PoleBase);
                NewPoleShine = new FloatSetting("NewPoleShine", Defaults.PoleShine);
                NewPoleWear = new FloatSetting("NewPoleWear", Defaults.PoleWear);
                NewPoleRust = new FloatSetting("NewPoleRust", Defaults.PoleRust);
                NewPoleDirt = new FloatSetting("NewPoleDirt", Defaults.PoleDirt);
                NewRopeThickness = new FloatSetting("NewRopeThickness", Defaults.RopeThickness);
                NewPolePosition = new FloatSetting("NewPolePosition", Defaults.PolePosition);
                NewTake = new IntSetting("NewTake", Defaults.Take);
                NewHeld = new IntSetting("NewHeld", Defaults.Held);
                NewTilt = new FloatSetting("NewTilt", Defaults.Tilt);
                NewLean = new FloatSetting("NewLean", Defaults.Lean);

                SelCountry = new IntSetting("SelCountry", Defaults.Country, true);
                SelImage = new IntSetting("SelImage", Defaults.Image, true);
                SelImageX = new FloatSetting("SelImageX", Defaults.ImageX, true);
                SelImageY = new FloatSetting("SelImageY", Defaults.ImageY, true);
                SelImageZoom = new FloatSetting("SelImageZoom", Defaults.ImageZoom, true);
                SelImageTurn = new IntSetting("SelImageTurn", Defaults.ImageTurn, true);
                SelImageAlpha = new IntSetting("SelImageAlpha", Defaults.ImageAlpha, true);
                SelPattern = new IntSetting("SelPattern", Defaults.Pattern, true);
                SelColour1 = new IntSetting("SelColour1", Defaults.Colour1, true);
                SelColour2 = new IntSetting("SelColour2", Defaults.Colour2, true);
                SelColour3 = new IntSetting("SelColour3", Defaults.Colour3, true);
                SelLength = new FloatSetting("SelLength", Defaults.Length, true);
                SelProportion = new IntSetting("SelProportion", Defaults.Proportion, true);
                SelHoistedFrom = new IntSetting("SelHoistedFrom", Defaults.HoistedFrom, true);
                SelMotion = new IntSetting("SelMotion", Defaults.Motion, true);
                SelWaviness = new FloatSetting("SelWaviness", Defaults.Waviness, true);
                SelCut = new IntSetting("SelCut", Defaults.Cut, true);
                SelCutDepth = new FloatSetting("SelCutDepth", Defaults.CutDepth, true);
                SelWindShare = new FloatSetting("SelWindShare", Defaults.WindShare, true);
                SelDarkness = new FloatSetting("SelDarkness", Defaults.Darkness, true);
                SelWear = new FloatSetting("SelWear", Defaults.Wear, true);
                SelDirt = new FloatSetting("SelDirt", Defaults.Dirt, true);
                SelHoles = new FloatSetting("SelHoles", Defaults.Holes, true);
                SelTear = new FloatSetting("SelTear", Defaults.Tear, true);
                SelTearEdges = new FloatSetting("SelTearEdges", Defaults.TearEdges, true);
                SelRotation = new FloatSetting("SelRotation", Defaults.Rotation, true);
                SelBackPlate = new BoolSetting("SelBackPlate", Defaults.BackPlate, true);
                SelHeight = new FloatSetting("SelHeight", Defaults.Height, true);
                SelPole = new IntSetting("SelPole", Defaults.Pole, true);
                SelRopes = new IntSetting("SelRopes", Defaults.Ropes, true);
                SelPoleColour = new IntSetting("SelPoleColour", Defaults.PoleColour, true);
                SelTopColour = new IntSetting("SelTopColour", Defaults.TopColour, true);
                SelRopeColour = new IntSetting("SelRopeColour", Defaults.RopeColour, true);
                SelPoleThickness = new FloatSetting("SelPoleThickness", Defaults.PoleThickness, true);
                SelPoleProfile = new IntSetting("SelPoleProfile", Defaults.PoleProfile, true);
                SelPoleBase = new IntSetting("SelPoleBase", Defaults.PoleBase, true);
                SelPoleShine = new FloatSetting("SelPoleShine", Defaults.PoleShine, true);
                SelPoleWear = new FloatSetting("SelPoleWear", Defaults.PoleWear, true);
                SelPoleRust = new FloatSetting("SelPoleRust", Defaults.PoleRust, true);
                SelPoleDirt = new FloatSetting("SelPoleDirt", Defaults.PoleDirt, true);
                SelRopeThickness = new FloatSetting("SelRopeThickness", Defaults.RopeThickness, true);
                SelPolePosition = new FloatSetting("SelPolePosition", Defaults.PolePosition, true);
                SelTake = new IntSetting("SelTake", Defaults.Take, true);
                SelHeld = new IntSetting("SelHeld", Defaults.Held, true);
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
                Debug.LogError("[FlagMaker]Could not initialise settings.");
                Debug.LogException(e);
                return;
            }

            SettingsXml.Load();

            // A file from the day "ornate" was a profile (format 16): a tapered shaft on a cast base.
            if (NewPoleProfile.value == FlagPoleProfile.OldOrnate)
            {
                NewPoleProfile.value = FlagPoleProfile.Tapered;
                NewPoleBase.value = FlagPoleBase.Cast;
            }
        }

        /// <summary>
        /// The New flag window's flag, as a spec: what the next placed flag will be, and what a
        /// flag prop with no spec of its own (placed by Find It, say) is given.
        /// </summary>
        public static FlagSpec NewSpec()
        {
            FlagSpec spec = FlagSpec.Default;
            if (!_initialised)
                return spec;

            FlagCountry country = FlagCountries.At(NewCountry.value);
            spec.Country = country != null ? country.Code : string.Empty;
            spec.Image = FlagImageNames.At(NewImage.value);
            spec.ImageX = NewImageX.value;
            spec.ImageY = NewImageY.value;
            spec.ImageZoom = NewImageZoom.value;
            spec.ImageTurn = NewImageTurn.value;
            spec.ImageAlpha = NewImageAlpha.value;
            spec.Pattern = NewPattern.value;
            spec.Colour1 = (uint)NewColour1.value;
            spec.Colour2 = (uint)NewColour2.value;
            spec.Colour3 = (uint)NewColour3.value;
            spec.Length = NewLength.value;
            spec.Proportion = NewProportion.value;
            spec.HoistedFrom = NewHoistedFrom.value;
            spec.Motion = NewMotion.value;
            spec.Waviness = NewWaviness.value;
            spec.WindShare = NewWindShare.value;
            spec.Cut = NewCut.value;
            spec.CutDepth = NewCutDepth.value;
            spec.Darkness = NewDarkness.value;
            spec.Wear = NewWear.value;
            spec.Dirt = NewDirt.value;
            spec.Holes = NewHoles.value;
            spec.Tear = NewTear.value;
            spec.TearEdges = NewTearEdges.value;
            spec.BackPlate = NewBackPlate.value;
            spec.Pole = NewPole.value;
            spec.Ropes = NewRopes.value;
            spec.PoleColour = (uint)NewPoleColour.value;
            spec.TopColour = (uint)NewTopColour.value;
            spec.RopeColour = (uint)NewRopeColour.value;
            spec.PoleThickness = NewPoleThickness.value;
            spec.PoleProfile = NewPoleProfile.value;
            spec.PoleBase = NewPoleBase.value;
            spec.PoleShine = NewPoleShine.value;
            spec.PoleWear = NewPoleWear.value;
            spec.PoleRust = NewPoleRust.value;
            spec.PoleDirt = NewPoleDirt.value;
            spec.RopeThickness = NewRopeThickness.value;
            spec.PolePosition = NewPolePosition.value;
            spec.Take = NewTake.value;
            spec.Held = NewHeld.value;
            spec.Tilt = NewTilt.value;
            spec.Lean = NewLean.value;

            // A pole that stands is as tall as its row says, wherever the flag goes; a crossbar
            // reaching out level finds its wall (0: found along it).
            spec.PoleLength = FlagStand.PoleStands(spec) ? Math.Max(NewHeight.value, FlagPoleShape.MinLength) : 0f;
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
