using RealFlag.Flags;

namespace RealFlag.UI
{
    /// <summary>
    /// The overall Quality preset (the author, 2026-11-07: "an overall Quality Preset ... Reading
    /// the full model would be in at least High"; "Extreme is not needed as a preset and Custom
    /// covers it"): Low, Medium, High, Very high, Ultra -- each a set of the cloth detail,
    /// self-collision, shared motion and buildings as drawn -- or Custom, whatever else those four
    /// are. High is the defaults (Settings.Defaults), so a file from before reads as High. The
    /// preset holds nothing of its own: Settings.Quality is DERIVED from the four
    /// (<see cref="Sync"/>, every frame), and choosing a level writes them (<see cref="Apply"/>).
    /// The cloth's threads stay out of it (the machine's, not the quality), as does "Cloth avoids
    /// buildings, props and trees" (on at every level).
    /// </summary>
    public static class QualityPreset
    {
        public const int Low = 0;
        public const int Medium = 1;
        public const int High = 2;
        public const int VeryHigh = 3;
        public const int Ultra = 4;
        public const int Custom = 5;

        public static readonly int[] Values = { Low, Medium, High, VeryHigh, Ultra, Custom };

        public static readonly string[] NameKeys =
        {
            "Quality.Low", "Quality.Medium", "Quality.High", "Quality.VeryHigh", "Quality.Ultra", "Quality.Custom",
        };

        /// <summary>Each level's cloth detail (cells along), self-collision, shared motion and buildings as drawn.</summary>
        public static readonly int[] Detail = { 18, 24, 32, 40, 52 };
        public static readonly bool[] SelfCollision = { false, true, true, true, true };
        public static readonly bool[] SharedMotion = { true, true, true, true, false };
        public static readonly bool[] DrawnBuildings = { false, false, true, true, true };

        /// <summary>The level these four make, or <see cref="Custom"/>. PURE.</summary>
        public static int Of(int detail, bool selfCollision, bool sharedMotion, bool drawnBuildings)
        {
            for (int i = 0; i < Detail.Length; i++)
            {
                if (Detail[i] == detail && SelfCollision[i] == selfCollision && SharedMotion[i] == sharedMotion && DrawnBuildings[i] == drawnBuildings)
                    return i;
            }

            return Custom;
        }

        /// <summary>The level the settings make now.</summary>
        public static int Current()
        {
            return Of(Settings.ClothDetail != null ? Settings.ClothDetail.value : Settings.Defaults.ClothDetail,
                      Settings.SelfCollision == null || Settings.SelfCollision.value,
                      Settings.SharedMotion == null || Settings.SharedMotion.value,
                      Settings.DetailedBuildings == null || Settings.DetailedBuildings.value);
        }

        /// <summary>Settings.Quality follows the four (a file loaded or edited, a row changed): cheap, every frame.</summary>
        public static void Sync()
        {
            if (Settings.Quality != null)
                Settings.Quality.value = Current();
        }

        /// <summary>A level chosen: its four settings written, the cloth and the city told. Custom changes nothing.</summary>
        public static void Apply(int level)
        {
            if (level < Low || level > Ultra || Settings.ClothDetail == null)
                return;

            bool drawnBefore = Settings.DetailedBuildings == null || Settings.DetailedBuildings.value;
            Settings.ClothDetail.value = Detail[level];
            Settings.SelfCollision.value = SelfCollision[level];
            Settings.SharedMotion.value = SharedMotion[level];
            if (Settings.DetailedBuildings != null)
                Settings.DetailedBuildings.value = DrawnBuildings[level];

            Log.Msg("setting: quality = " + LogName(level) + " (cloth detail " + SettingsCatalog.DetailLogName(Detail[level]) + ", self-collision " +
                    (SelfCollision[level] ? "on" : "off") + ", shared motion " + (SharedMotion[level] ? "on" : "off") + ", buildings " +
                    (DrawnBuildings[level] ? "as drawn" : "by their simple shapes") + ")");
            FlagRenderer.ClothOptionsChanged();
            if (drawnBefore != DrawnBuildings[level])
            {
                FlagSurroundings.AllStale();
                FlagTool.ForgetSnap();
            }

            Sync();
        }

        public static string LogName(int level)
        {
            switch (level)
            {
                case Low: return "Low";
                case Medium: return "Medium";
                case High: return "High";
                case VeryHigh: return "Very high";
                case Ultra: return "Ultra";
                default: return "Custom";
            }
        }
    }
}
