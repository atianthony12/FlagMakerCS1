using System;
using System.Collections.Generic;
using System.Globalization;
using ColossalFramework;
using RealFlag.Flags;

namespace RealFlag.UI
{
    public enum RowKind
    {
        /// <summary>A 0..1 (or multiplier) setting shown as a percentage.</summary>
        Percent,

        /// <summary>A slider in the setting's own units; <see cref="Row.Format"/> writes the readout.</summary>
        Value,

        Toggle,
        Key,

        /// <summary>A button; with <see cref="Row.Id2"/> set, two side by side.</summary>
        Button,

        /// <summary>A line of text the UI keeps up to date. The in-game panel only.</summary>
        Status,

        /// <summary>A dropdown (options page) or a slider over the choices (panel).</summary>
        Choice,

        /// <summary>
        /// A colour (an IntSetting holding 0xRRGGBB): a swatch that opens the colour picker. Up to
        /// three in a row share one line in the panel; the file writes "#RRGGBB".
        /// </summary>
        Colour,

        /// <summary>The Existing flags tab's list of the city's flags. The in-game panel only.</summary>
        FlagList,

        /// <summary>
        /// A country's flag (an IntSetting holding FlagCountries' number, 0 for none): a button
        /// with its name and image that opens the country picker. The file writes its code.
        /// </summary>
        Country,
    }

    /// <summary>
    /// Which part of the in-game panel a row is in: one of its three tabs, or a page of the
    /// Flagpole popup (each design tab's own). None = not in it.
    /// </summary>
    public enum PanelPage { None, Create, Existing, Settings, PoleCreate, PoleExisting }

    /// <summary>General = the game's mod options page. None = not on it.</summary>
    public enum OptionsPage { None, General }

    /// <summary>
    /// One setting, everything either UI needs to draw it, and its default. (Volumetric
    /// Explosions' row, with colours, the flag list and paired buttons.)
    /// </summary>
    /// <remarks>
    /// A row holds no words. Its texts are in Localization/en.xml under its <see cref="Name"/>:
    /// "NewLength.Label", "NewLength.Tooltip", and so on, read in the game's language every time a
    /// UI asks. <see cref="SettingsCatalog.LogLayout"/> checks at startup that English has every
    /// key a row needs.
    /// </remarks>
    public class Row
    {
        public RowKind Kind;
        public PanelPage Panel;
        public OptionsPage Options;

        /// <summary>The name of a row with no setting (a button, a status line). A row with a setting is named after it.</summary>
        public string Id;

        /// <summary>A second button beside the first (half the width each): its name, what it does, when it is on.</summary>
        public string Id2;
        public Action OnClick2;
        public Func<bool> Enabled2;

        /// <summary>Non-null starts a new group here. Its title is "Group.&lt;Group&gt;" in the language files.</summary>
        public string Group;

        /// <summary>A line of text under the row ("&lt;Name&gt;.Note"), for the warnings a tooltip would hide.</summary>
        public bool HasNote;

        /// <summary>Toggles: ask "&lt;Name&gt;.Confirm" before switching ON.</summary>
        public bool Confirm;

        public FloatSetting Float;
        public BoolSetting Bool;
        public IntSetting Int;
        public SavedInputKey Key;

        // The SAME constants Settings.Init() constructs from. A setting does not remember its
        // own default, so this is the only thing that can put one back.
        public float DefaultFloat;
        public bool DefaultBool;
        public int DefaultInt;
        public int DefaultKey;

        public float Min, Max, Step;

        /// <summary>Formats the slider readout. Null means "N%".</summary>
        public Func<float, string> Format;

        /// <summary>The panel's readout while this says something (non-null): the proportions of a country's flag, say.</summary>
        public Func<string> ReadoutOverride;

        /// <summary>Choice rows: the language keys of the choices' names, one per value.</summary>
        public string[] ChoiceKeys;

        /// <summary>Choice rows whose names are not in the language files: value -> name.</summary>
        public Func<int, string> ChoiceText;

        public int[] ChoiceValues;

        /// <summary>Live-apply hook. Must be safe with no city loaded: the options page is.</summary>
        public Action AfterChange;

        public Func<string> StatusText;
        public Action OnClick;

        /// <summary>A button whose words change (the Flagpole button: what the pole is): read at every refresh instead of its label.</summary>
        public Func<string> ButtonText;

        /// <summary>Greys the row out.</summary>
        public Func<bool> Enabled;

        /// <summary>True while this row's confirmation dialog is open (the stored value is still the old one).</summary>
        public bool Pending;

        /// <summary>The name this row is stored under in RealFlag.xml and the log, and its key in the language files.</summary>
        public string Name
        {
            get
            {
                if (Float != null) return Float.name;
                if (Bool != null) return Bool.name;
                if (Int != null) return Int.name;
                if (Key != null) return Key.name;
                return Id;
            }
        }

        // ---- the texts, in the game's language ------------------------------------------------

        public string Label
        {
            get { return Localization.Get(Name + ".Label"); }
        }

        public string Tooltip
        {
            get { return Localization.Get(Name + ".Tooltip"); }
        }

        public string Label2
        {
            get { return Id2 != null ? Localization.Get(Id2 + ".Label") : null; }
        }

        public string Tooltip2
        {
            get { return Id2 != null ? Localization.Get(Id2 + ".Tooltip") : null; }
        }

        public string Note
        {
            get { return HasNote ? Localization.Get(Name + ".Note") : null; }
        }

        public string ConfirmOn
        {
            get { return Confirm ? Localization.Get(Name + ".Confirm") : null; }
        }

        public string GroupTitle
        {
            get { return Group != null ? Localization.Get("Group." + Group) : null; }
        }

        public string[] Choices
        {
            get
            {
                if (ChoiceValues == null)
                    return null;

                var names = new string[ChoiceValues.Length];
                for (int i = 0; i < names.Length; i++)
                    names[i] = ChoiceName(i);

                return names;
            }
        }

        public string ChoiceName(int index)
        {
            if (ChoiceText != null)
                return ChoiceText(ChoiceValues[index]);

            if (ChoiceKeys != null && index < ChoiceKeys.Length)
                return Localization.Get(ChoiceKeys[index]);

            return ChoiceValues[index].ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>Every language key this row needs, for the startup check.</summary>
        public IEnumerable<string> TextKeys()
        {
            if (Kind == RowKind.Status || Kind == RowKind.FlagList)
                yield break;

            yield return Name + ".Label";

            if (Panel != PanelPage.None || Options != OptionsPage.None)
                yield return Name + ".Tooltip";

            if (Id2 != null)
            {
                yield return Id2 + ".Label";
                yield return Id2 + ".Tooltip";
            }

            if (HasNote)
                yield return Name + ".Note";

            if (Confirm)
                yield return Name + ".Confirm";

            if (Group != null)
                yield return "Group." + Group;

            if (ChoiceKeys != null)
            {
                foreach (string key in ChoiceKeys)
                    yield return key;
            }
        }

        // ---- values ---------------------------------------------------------------------------

        public bool IsEnabled
        {
            get { return Enabled == null || Enabled(); }
        }

        public bool IsEnabled2
        {
            get { return Enabled2 == null || Enabled2(); }
        }

        /// <summary>The slider position: percent rows are stored as a fraction and shown x100.</summary>
        public float Display
        {
            get
            {
                if (Float == null)
                    return 0f;

                return Kind == RowKind.Percent ? Float.value * 100f : Float.value;
            }
        }

        public void Store(float display)
        {
            if (Float == null)
                return;

            Float.value = Kind == RowKind.Percent ? display / 100f : display;
        }

        public string FormatDisplay(float display)
        {
            if (Format != null)
                return Format(display);

            return Localization.Get("Unit.Percent", display.ToString("F0"));
        }

        /// <summary>The index into <see cref="Choices"/> that the stored value means.</summary>
        public int ChoiceIndex
        {
            get
            {
                if (Int == null || ChoiceValues == null)
                    return 0;

                for (int i = 0; i < ChoiceValues.Length; i++)
                {
                    if (ChoiceValues[i] == Int.value)
                        return i;
                }

                return 0;
            }
        }

        /// <summary>A panel control over the selected flag, not a setting: never saved, never reset.</summary>
        public bool IsTransient
        {
            get { return (Float != null && Float.Transient) || (Int != null && Int.Transient); }
        }

        /// <summary>Writes the shipping default back. Does not fire <see cref="AfterChange"/>.</summary>
        public void ResetToDefault()
        {
            if (Float != null) Float.value = DefaultFloat;
            if (Bool != null) Bool.value = DefaultBool;
            if (Int != null) Int.value = DefaultInt;
            if (Key != null) Key.value = DefaultKey;
        }
    }

    /// <summary>
    /// Every setting in the mod, once, with where it is shown and what its default is. Both UIs
    /// are drawn from this list, RealFlag.xml is written from it and Reset walks it (Volumetric
    /// Weather's design).
    /// </summary>
    /// <remarks>
    /// The split: the flags, the wind and the cloth are in the in-game panel (Row.Panel) -- three
    /// tabs, Create new, Existing flags and Settings; the options page has the mod itself
    /// (Row.Options == General), the cloth and the pause among it too. The words are in
    /// Localization/en.xml, in the same order.
    /// </remarks>
    public static class SettingsCatalog
    {
        private static List<Row> _rows;
        private static bool _loggedLayout;

        public static List<Row> Rows
        {
            get
            {
                if (_rows == null)
                    Build();

                return _rows;
            }
        }

        public static int CountForPanel(PanelPage page)
        {
            int n = 0;
            foreach (Row row in Rows)
            {
                if (row.Panel == page)
                    n++;
            }

            return n;
        }

        public static int CountForOptions(OptionsPage page)
        {
            int n = 0;
            foreach (Row row in Rows)
            {
                if (row.Options == page)
                    n++;
            }

            return n;
        }

        /// <summary>A tab's name (or the wind's heading), in the game's language.</summary>
        public static string TabName(PanelPage page)
        {
            return Localization.Get("Tab." + page);
        }

        /// <summary>One line per page, at build time, and one warning if English lacks a text any row needs.</summary>
        public static void LogLayout()
        {
            if (_loggedLayout)
                return;

            _loggedLayout = true;

            int nowhere = 0;
            foreach (Row row in Rows)
            {
                if (row.Panel == PanelPage.None && row.Options == OptionsPage.None)
                    nowhere++;
            }

            Log.Msg("settings catalog: " + Rows.Count + " rows | Create new=" + CountForPanel(PanelPage.Create) +
                    " | Existing flags=" + CountForPanel(PanelPage.Existing) + " | Settings=" + CountForPanel(PanelPage.Settings) +
                    " | options page=" + CountForOptions(OptionsPage.General) + " | rows with no UI=" + nowhere);

            var missing = new List<string>();
            foreach (Row row in Rows)
            {
                foreach (string key in row.TextKeys())
                {
                    if (!Localization.HasEnglish(key))
                        missing.Add(key);
                }
            }

            foreach (PanelPage page in Enum.GetValues(typeof(PanelPage)))
            {
                if (page != PanelPage.None && !Localization.HasEnglish("Tab." + page))
                    missing.Add("Tab." + page);
            }

            if (missing.Count > 0)
                Log.Warn("localization: en.xml has no text for " + string.Join(", ", missing.ToArray()));
        }

        /// <summary>Puts every setting back to its shipping default, the rows with no UI included. Always logged.</summary>
        public static void ResetAll()
        {
            foreach (Row row in Rows)
            {
                if (!row.IsTransient)
                    row.ResetToDefault();
            }

            foreach (Row row in Rows)
            {
                if (row.AfterChange != null && !row.IsTransient)
                    row.AfterChange();
            }

            SettingsXml.SaveNow();
            Log.Msg("settings: RESET to defaults (" + Rows.Count + " rows)");
        }

        /// <summary>Stores a checkbox, asking first where the row says to ask (only ever when switching ON).</summary>
        public static void ApplyToggle(Row row, bool value)
        {
            if (row.Bool == null)
                return;

            if (!value || !row.Confirm)
            {
                Settle(row, value);
                return;
            }

            try
            {
                row.Pending = true;

                ConfirmPanel.ShowModal(Mod.DisplayName, row.ConfirmOn,
                    (component, result) =>
                    {
                        row.Pending = false;
                        Settle(row, result == 1);
                    });
            }
            catch (Exception e)
            {
                row.Pending = false;
                Log.Warn("Could not show the confirmation for '" + row.Name + "': " + e.Message);
                Settle(row, value);
            }
        }

        private static void Settle(Row row, bool value)
        {
            row.Bool.value = value;

            // Written to disk NOW rather than with the slider changes a second later: a switch
            // is a decision.
            SettingsXml.SaveNow();

            if (row.AfterChange != null)
                row.AfterChange();

            RefreshAllUIs();
        }

        /// <summary>Re-reads every control in both UIs; either can be open while the other changes.</summary>
        public static void RefreshAllUIs()
        {
            if (OptionsUI.Instance != null)
                OptionsUI.Instance.RefreshValues();

            FlagsPanel.RefreshOpenPanel();
        }

        private static Action State(string what, Func<string> value)
        {
            return () => Log.Msg("setting: " + what + " = " + value());
        }

        private static Func<string> OnOff(BoolSetting setting)
        {
            return () => setting != null && setting.value ? "ON" : "off";
        }

        private static string Steps(float value)
        {
            return value.ToString("F0");
        }

        private static string Metres(float value)
        {
            return Localization.Get("Unit.Metres", value.ToString("0.#", CultureInfo.InvariantCulture));
        }

        private static string Degrees(float value)
        {
            return Localization.Get("Unit.Degrees", value.ToString("F0"));
        }

        /// <summary>The Flagpole button's words: "Flagpole: none" or "Flagpole: ball, 2 ropes", and the tilt if any.</summary>
        private static Func<string> PoleSummary(IntSetting pole, IntSetting ropes, FloatSetting tilt, FloatSetting lean)
        {
            return () =>
            {
                int style = pole != null ? pole.value : Settings.Defaults.Pole;
                string text = style == FlagPoleStyle.None
                    ? Localization.Get("PoleButton.None")
                    : Localization.Get("PoleButton.Pole", Localization.Get(PoleKey(style)), ropes != null ? ropes.value : 0);

                float t = tilt != null ? tilt.value : 0f, l = lean != null ? lean.value : 0f;
                if (Math.Abs(t) >= 0.5f || Math.Abs(l) >= 0.5f)
                    text += Localization.Get("PoleButton.Tilted", t.ToString("F0"), l.ToString("F0"));
                return text;
            };
        }

        private static string PoleKey(int style)
        {
            int i = Array.IndexOf(FlagPoleStyle.Values, style);
            return FlagPoleStyle.NameKeys[i < 0 ? 0 : i];
        }

        /// <summary>"5 m/s, Bf 3": the wind and its Beaufort force, whose descriptions speak of flags.</summary>
        private static string WindText(float value)
        {
            return Localization.Get("Unit.Wind", value.ToString("0.#", CultureInfo.InvariantCulture), Beaufort(value));
        }

        /// <summary>The Beaufort force of a wind at 10 m (WMO upper limits, m/s).</summary>
        public static int Beaufort(float speed)
        {
            float[] limits = { 0.5f, 1.5f, 3.3f, 5.5f, 7.9f, 10.7f, 13.8f, 17.1f, 20.7f, 24.4f, 28.4f, 32.6f };
            for (int i = 0; i < limits.Length; i++)
            {
                if (speed < limits[i])
                    return i;
            }

            return 12;
        }

        private static bool Uses(IntSetting pattern, int colours)
        {
            return pattern == null || FlagPattern.ColoursUsed(pattern.value) >= colours;
        }

        /// <summary>The list's proportion nearest to a value off it (a country's).</summary>
        private static int NearestProportion(int value)
        {
            int best = FlagProportion.Default;
            foreach (int v in FlagProportion.Values)
            {
                if (Math.Abs(v - value) < Math.Abs(best - value))
                    best = v;
            }

            return best;
        }

        /// <summary>The names of FlagCloth.Details, in its order.</summary>
        private static readonly string[] DetailNameKeys =
        {
            "ClothDetail.Normal", "ClothDetail.High", "ClothDetail.VeryHigh", "ClothDetail.Ultra",
        };

        /// <summary>For the log: "High (24 cells)".</summary>
        public static string DetailLogName(int detail)
        {
            int[] values = FlagCloth.Details;
            for (int i = 0; i < values.Length && i < DetailNameKeys.Length; i++)
            {
                if (values[i] == detail)
                    return DetailNameKeys[i].Substring("ClothDetail.".Length) + " (" + detail + " cells)";
            }

            return detail + " cells";
        }

        // ---- the catalog ----------------------------------------------------------------------

        private static void Build()
        {
            Settings.Init();

            // A first Init reads RealFlag.xml, which is written from this list, so it may have
            // built it already.
            if (_rows != null)
                return;

            _rows = new List<Row>();

            BuildCreate();
            BuildExisting();
            BuildWind();
            BuildGeneralOptions();
            BuildHidden();
        }

        private static void Add(Row row)
        {
            _rows.Add(row);
        }

        /// <summary>
        /// A flag's design, the same rows on both tabs: the next flag's (settings) or the selected
        /// one's (transient). First the country: with one chosen, the pattern, its colours and the
        /// proportions are the country's flag's and grey out (they are kept, for a change back);
        /// the size, the waviness and the darkness stay the player's -- the darkness is for a
        /// country's colours above all, which cannot be picked (the author: "some country flags
        /// are too bright").
        /// </summary>
        private static void AddDesign(PanelPage page, IntSetting country, IntSetting pattern, IntSetting c1, IntSetting c2, IntSetting c3,
                                      FloatSetting length, IntSetting proportion, FloatSetting waviness, FloatSetting darkness,
                                      Action changed, Func<bool> enabled)
        {
            Func<bool> patterned = () => (enabled == null || enabled()) && (country == null || country.value == 0);

            Add(new Row
            {
                Kind = RowKind.Country,
                Panel = page,
                Int = country,
                DefaultInt = Settings.Defaults.Country,
                AfterChange = () =>
                {
                    // Back to a pattern from a country flown at its own proportions: the nearest of the list's.
                    if (country.value == 0 && proportion != null && Array.IndexOf(FlagProportion.Values, proportion.value) < 0)
                        proportion.value = NearestProportion(proportion.value);
                    if (changed != null)
                        changed();
                },
                Enabled = enabled,
            });

            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = page,
                Int = pattern,
                DefaultInt = Settings.Defaults.Pattern,
                ChoiceValues = FlagPattern.Values,
                ChoiceKeys = FlagPattern.NameKeys,
                AfterChange = changed,
                Enabled = patterned,
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = page,
                Int = c1,
                DefaultInt = Settings.Defaults.Colour1,
                AfterChange = changed,
                Enabled = patterned,
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = page,
                Int = c2,
                DefaultInt = Settings.Defaults.Colour2,
                AfterChange = changed,
                Enabled = () => patterned() && Uses(pattern, 2),
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = page,
                Int = c3,
                DefaultInt = Settings.Defaults.Colour3,
                AfterChange = changed,
                Enabled = () => patterned() && Uses(pattern, 3),
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = page,
                Float = length,
                DefaultFloat = Settings.Defaults.Length,
                Min = FlagSpec.MinLength, Max = FlagSpec.MaxLength, Step = 0.1f,
                Format = Metres,
                AfterChange = changed,
                Enabled = enabled,
            });

            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = page,
                Int = proportion,
                DefaultInt = Settings.Defaults.Proportion,
                ChoiceValues = FlagProportion.Values,
                ChoiceText = FlagProportion.Name,
                AfterChange = changed,
                Enabled = patterned,
                ReadoutOverride = () =>
                {
                    FlagCountry flag = country != null ? FlagCountries.At(country.value) : null;
                    return flag != null ? FlagProportion.Name(flag.Proportion) : null;
                },
            });

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = waviness,
                DefaultFloat = Settings.Defaults.Waviness,
                Min = 0f, Max = 200f, Step = 5f,
                AfterChange = changed,
                Enabled = enabled,
            });

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = darkness,
                DefaultFloat = Settings.Defaults.Darkness,
                Min = 0f, Max = FlagSpec.MaxDarkness * 100f, Step = 5f,
                AfterChange = changed,
                Enabled = enabled,
            });
        }

        /// <summary>
        /// The Flagpole popup's page for a tab: the pole the mod draws (none by default: the
        /// author's "the purpose of this mod is making the flag cloth adaptable to any pole"), its
        /// ropes, colours and thickness, and the hoist's tilt and lean -- the pole's angle, which a
        /// flag on a pole of the city's own (a wall-mounted one) has too.
        /// </summary>
        private static void AddPole(PanelPage page, IntSetting pole, IntSetting ropes, IntSetting poleColour, IntSetting topColour, IntSetting ropeColour,
                                    FloatSetting thickness, FloatSetting tilt, FloatSetting lean, Action changed, Func<bool> enabled)
        {
            Func<bool> drawn = () => (enabled == null || enabled()) && pole.value != FlagPoleStyle.None;

            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = page,
                Int = pole,
                DefaultInt = Settings.Defaults.Pole,
                ChoiceValues = FlagPoleStyle.Values,
                ChoiceKeys = FlagPoleStyle.NameKeys,
                AfterChange = changed,
                Enabled = enabled,
            });

            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = page,
                Int = ropes,
                DefaultInt = Settings.Defaults.Ropes,
                ChoiceValues = FlagRopes.Values,
                ChoiceKeys = FlagRopes.NameKeys,
                AfterChange = changed,
                Enabled = drawn,
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = page,
                Int = poleColour,
                DefaultInt = Settings.Defaults.PoleColour,
                AfterChange = changed,
                Enabled = drawn,
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = page,
                Int = topColour,
                DefaultInt = Settings.Defaults.TopColour,
                AfterChange = changed,
                Enabled = () => drawn() && (pole.value == FlagPoleStyle.Ball || pole.value == FlagPoleStyle.Spear),
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = page,
                Int = ropeColour,
                DefaultInt = Settings.Defaults.RopeColour,
                AfterChange = changed,
                Enabled = () => drawn() && ropes.value > 0,
            });

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = thickness,
                DefaultFloat = Settings.Defaults.PoleThickness,
                Min = FlagSpec.MinPoleThickness * 100f, Max = FlagSpec.MaxPoleThickness * 100f, Step = 5f,
                AfterChange = changed,
                Enabled = drawn,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = page,
                Float = tilt,
                DefaultFloat = Settings.Defaults.Tilt,
                Min = -FlagSpec.MaxTilt, Max = FlagSpec.MaxTilt, Step = 1f,
                Format = Degrees,
                AfterChange = changed,
                Enabled = enabled,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = page,
                Float = lean,
                DefaultFloat = Settings.Defaults.Lean,
                Min = -FlagSpec.MaxTilt, Max = FlagSpec.MaxTilt, Step = 1f,
                Format = Degrees,
                AfterChange = changed,
                Enabled = enabled,
            });
        }

        /// <summary>A tab's Flagpole button: its words say what the pole is; a click opens the popup's page for that tab.</summary>
        private static void AddPoleButton(PanelPage tab, PanelPage popup, string id, IntSetting pole, IntSetting ropes, FloatSetting tilt,
                                          FloatSetting lean, Func<bool> enabled)
        {
            Add(new Row
            {
                Kind = RowKind.Button,
                Panel = tab,
                Id = id,
                ButtonText = PoleSummary(pole, ropes, tilt, lean),
                OnClick = () => PolePanel.Toggle(popup, FlagsPanelInstance()),
                Enabled = enabled,
            });
        }

        /// <summary>The in-game panel, for the popup to open beside (null: the popup keeps its place).</summary>
        private static ColossalFramework.UI.UIComponent FlagsPanelInstance()
        {
            ColossalFramework.UI.UIView view = ColossalFramework.UI.UIView.GetAView();
            return view != null ? view.FindUIComponent("RealFlagPanel") : null;
        }

        /// <summary>
        /// Create new: the next flag -- its design, which way it points, how high its hoist's top
        /// stands -- and the button that places it on the map (the author: "multiple options for
        /// the flag: rotation, size, how wavy, etc.").
        /// </summary>
        private static void BuildCreate()
        {
            AddDesign(PanelPage.Create, Settings.NewCountry, Settings.NewPattern, Settings.NewColour1, Settings.NewColour2, Settings.NewColour3,
                      Settings.NewLength, Settings.NewProportion, Settings.NewWaviness, Settings.NewDarkness, null, null);

            AddPoleButton(PanelPage.Create, PanelPage.PoleCreate, "NewPoleButton", Settings.NewPole, Settings.NewRopes, Settings.NewTilt,
                          Settings.NewLean, null);
            AddPole(PanelPage.PoleCreate, Settings.NewPole, Settings.NewRopes, Settings.NewPoleColour, Settings.NewTopColour, Settings.NewRopeColour,
                    Settings.NewPoleThickness, Settings.NewTilt, Settings.NewLean, null, null);

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = PanelPage.Create,
                Float = Settings.NewRotation,
                DefaultFloat = Settings.Defaults.Rotation,
                Min = 0f, Max = 359f, Step = 1f,
                Format = Degrees,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = PanelPage.Create,
                Float = Settings.NewHeight,
                DefaultFloat = Settings.Defaults.Height,
                Min = 0f, Max = 200f, Step = 0.5f,
                Format = Metres,
            });

            Add(new Row
            {
                Kind = RowKind.Button,
                Panel = PanelPage.Create,
                Id = "PlaceFlags",
                OnClick = () => FlagTool.Toggle(FlagTool.Mode.Place),
                Enabled = () => FlagRenderer.Instance != null && FlagPrefab.Info != null,
            });

            Add(new Row
            {
                Kind = RowKind.Status,
                Panel = PanelPage.Create,
                Id = "CreateStatus",
                StatusText = FlagRenderer.Describe,
            });
        }

        /// <summary>
        /// Existing flags: every flag in the city, a click selects one ("clicking each will allow
        /// editing"), and the same controls as Create new over the selected flag, applied at once.
        /// </summary>
        private static void BuildExisting()
        {
            Add(new Row
            {
                Kind = RowKind.FlagList,
                Panel = PanelPage.Existing,
                Id = "FlagList",
            });

            Add(new Row
            {
                Kind = RowKind.Status,
                Panel = PanelPage.Existing,
                Id = "SelectedFlag",
                StatusText = FlagEditor.Describe,
            });

            Func<bool> selected = () => FlagEditor.HasSelection;
            AddDesign(PanelPage.Existing, Settings.SelCountry, Settings.SelPattern, Settings.SelColour1, Settings.SelColour2, Settings.SelColour3,
                      Settings.SelLength, Settings.SelProportion, Settings.SelWaviness, Settings.SelDarkness, FlagEditor.ApplyDesign, selected);

            AddPoleButton(PanelPage.Existing, PanelPage.PoleExisting, "SelPoleButton", Settings.SelPole, Settings.SelRopes, Settings.SelTilt,
                          Settings.SelLean, selected);
            AddPole(PanelPage.PoleExisting, Settings.SelPole, Settings.SelRopes, Settings.SelPoleColour, Settings.SelTopColour, Settings.SelRopeColour,
                    Settings.SelPoleThickness, Settings.SelTilt, Settings.SelLean, FlagEditor.ApplyDesign, selected);

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = PanelPage.Existing,
                Float = Settings.SelRotation,
                Min = 0f, Max = 359f, Step = 1f,
                Format = Degrees,
                AfterChange = FlagEditor.ApplyRotation,
                Enabled = selected,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = PanelPage.Existing,
                Float = Settings.SelHeight,
                Min = 0f, Max = 300f, Step = 0.5f,
                Format = Metres,
                AfterChange = FlagEditor.ApplyHeight,
                Enabled = selected,
            });

            Add(new Row
            {
                Kind = RowKind.Button,
                Panel = PanelPage.Existing,
                Id = "GoToFlag",
                OnClick = FlagEditor.GoTo,
                Enabled = selected,
                Id2 = "PickFlag",
                OnClick2 = () =>
                {
                    // With a flag selected: let it go and pick another (the tool stays out).
                    if (FlagEditor.Selected != 0)
                    {
                        FlagEditor.Deselect();
                        FlagTool.Open(FlagTool.Mode.Pick);
                    }
                    else
                    {
                        FlagTool.Toggle(FlagTool.Mode.Pick);
                    }
                },
                Enabled2 = () => FlagRenderer.Instance != null,
            });

            Add(new Row
            {
                Kind = RowKind.Button,
                Panel = PanelPage.Existing,
                Id = "CopyToNew",
                OnClick = FlagEditor.CopyToNew,
                Enabled = selected,
                Id2 = "DeleteFlag",
                OnClick2 = FlagEditor.ConfirmDelete,
                Enabled2 = selected,
            });
        }

        /// <summary>
        /// The Settings tab's wind: the game's by default -- its direction, its weather, its wind
        /// map (GameWind) -- or the player's own (the author: "Wind should be by default based on
        /// the game's wind, and can be overridden in a 3rd tab, Settings").
        /// </summary>
        private static void BuildWind()
        {
            Func<bool> own = () => Settings.WindFromGame != null && !Settings.WindFromGame.value;

            Add(new Row
            {
                Kind = RowKind.Toggle,
                Panel = PanelPage.Settings,
                Group = "Wind",
                Bool = Settings.WindFromGame,
                DefaultBool = Settings.Defaults.WindFromGame,
                AfterChange = State("the game's wind", OnOff(Settings.WindFromGame)),
            });

            Add(new Row
            {
                Kind = RowKind.Status,
                Panel = PanelPage.Settings,
                Id = "GameWindNow",
                StatusText = GameWindText,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = PanelPage.Settings,
                Float = Settings.WindSpeed,
                DefaultFloat = Settings.Defaults.WindSpeed,
                Min = 0f, Max = 30f, Step = 0.5f,
                Format = WindText,
                Enabled = own,
            });

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = PanelPage.Settings,
                Float = Settings.WindGusts,
                DefaultFloat = Settings.Defaults.WindGusts,
                Min = 0f, Max = 200f, Step = 5f,
                Enabled = own,
            });

            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = PanelPage.Settings,
                Int = Settings.WindMode,
                DefaultInt = Settings.Defaults.WindMode,
                ChoiceValues = Settings.WindModes.Values,
                ChoiceKeys = Settings.WindModes.NameKeys,
                AfterChange = () => Log.Msg("setting: wind direction = " + Settings.WindModes.LogName(Settings.WindMode.value)),
                Enabled = own,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = PanelPage.Settings,
                Float = Settings.WindDirection,
                DefaultFloat = Settings.Defaults.WindDirection,
                Min = 0f, Max = 359f, Step = 1f,
                Format = Degrees,
                Enabled = () => own() && (Settings.WindMode == null || Settings.WindMode.value == Settings.WindModes.Fixed),
            });
        }

        /// <summary>
        /// The Settings tab's line under the switch: the game's wind as it is now (over open level
        /// ground: a flag on a hill gets more, in a valley or by a forest less), or that the
        /// player's own is in use.
        /// </summary>
        private static string GameWindText()
        {
            if (Settings.WindFromGame != null && !Settings.WindFromGame.value)
                return Localization.Get("GameWindNow.Own");

            UnityEngine.Vector3 direction;
            float weather, rain, fog;
            if (!GameWind.Read(out direction, out weather, out rain, out fog))
                return Localization.Get("GameWindNow.NoCity");

            float speed = GameWind.BaseSpeed * weather;
            // As a flag's rotation, the way the "Angle" slider reads it (FlagProps.Direction).
            float degrees = UnityEngine.Mathf.Repeat(UnityEngine.Mathf.Atan2(direction.z, direction.x) * UnityEngine.Mathf.Rad2Deg, 360f);
            string text = Localization.Get("GameWindNow.Status", WindText(speed), degrees.ToString("F0"));
            if (rain >= 0.01f)
                text += Localization.Get("GameWindNow.Rain", (50f * rain).ToString("F0"));
            if (fog >= 0.01f)
                text += Localization.Get("GameWindNow.Fog", (50f * fog).ToString("F0"));
            return text;
        }

        /// <summary>The game's options page: the mod itself, and the reset button at the very bottom.</summary>
        private static void BuildGeneralOptions()
        {
            Add(new Row
            {
                Kind = RowKind.Choice,
                Options = OptionsPage.General,
                Group = "Language",
                Int = Settings.Language,
                DefaultInt = Settings.Defaults.Language,
                ChoiceValues = Localization.LanguageChoices(),
                ChoiceText = Localization.LanguageName,
                AfterChange = () =>
                {
                    Log.Msg("setting: language = " + Localization.CurrentCode +
                            (Settings.Language != null && Settings.Language.value == 0 ? " (the game's)" : " (chosen)"));
                    ModController.RebuildPanel();
                },
            });

            Add(new Row
            {
                Kind = RowKind.Key,
                Options = OptionsPage.General,
                Group = "Panel",
                Key = Settings.ToggleKey,
                DefaultKey = Settings.DefaultToggleKey,
            });

            Add(new Row
            {
                Kind = RowKind.Toggle,
                Options = OptionsPage.General,
                Bool = Settings.ShowInUnifiedUI,
                DefaultBool = Settings.Defaults.ShowInUnifiedUI,
                AfterChange = ModController.RefreshButton,
            });

            // The pause and the cloth: on this page and in the panel's Settings tab (the author:
            // "give it as an option for users"), where a change can be watched as it happens.
            Add(new Row
            {
                Kind = RowKind.Toggle,
                Panel = PanelPage.Settings,
                Options = OptionsPage.General,
                Group = "Flags",
                Bool = Settings.FreezeWhenPaused,
                DefaultBool = Settings.Defaults.FreezeWhenPaused,
                AfterChange = State("flags stop while the game is paused", OnOff(Settings.FreezeWhenPaused)),
            });

            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = PanelPage.Settings,
                Options = OptionsPage.General,
                Group = "Cloth",
                Int = Settings.ClothDetail,
                DefaultInt = Settings.Defaults.ClothDetail,
                ChoiceValues = FlagCloth.Details,
                ChoiceKeys = DetailNameKeys,
                AfterChange = () =>
                {
                    Log.Msg("setting: cloth detail = " + DetailLogName(Settings.ClothDetail.value));
                    FlagRenderer.ClothOptionsChanged();
                },
            });

            Add(new Row
            {
                Kind = RowKind.Toggle,
                Panel = PanelPage.Settings,
                Options = OptionsPage.General,
                Bool = Settings.SelfCollision,
                DefaultBool = Settings.Defaults.SelfCollision,
                AfterChange = () =>
                {
                    Log.Msg("setting: the cloth collides with itself = " + OnOff(Settings.SelfCollision)());
                    FlagRenderer.ClothOptionsChanged();
                },
            });

            Add(new Row
            {
                Kind = RowKind.Toggle,
                Options = OptionsPage.General,
                Group = "Diagnostics",
                Bool = Settings.DetailedLogging,
                DefaultBool = Settings.Defaults.DetailedLogging,
                AfterChange = Log.ReportLevel,
            });

            Add(new Row
            {
                Kind = RowKind.Button,
                Options = OptionsPage.General,
                Group = "StartingOver",
                Id = "ResetAll",
                OnClick = ConfirmReset,
            });
        }

        /// <summary>Settings that are live but have no row anywhere: in the catalog because a reset walks it.</summary>
        private static void BuildHidden()
        {
            Add(new Row
            {
                Kind = RowKind.Value,
                Float = Settings.HudButtonX,
                DefaultFloat = Settings.Defaults.HudButtonX,
                Min = 0f, Max = 10000f, Step = 1f,
                Format = Steps,
                AfterChange = ModController.PlaceHudButton,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Float = Settings.HudButtonY,
                DefaultFloat = Settings.Defaults.HudButtonY,
                Min = 0f, Max = 10000f, Step = 1f,
                Format = Steps,
                AfterChange = ModController.PlaceHudButton,
            });
        }

        private static void ConfirmReset()
        {
            try
            {
                ConfirmPanel.ShowModal(Mod.DisplayName, Localization.Get("ResetAll.Confirm"),
                    (component, result) =>
                    {
                        if (result != 1)
                        {
                            Log.Msg("settings: reset cancelled");
                            return;
                        }

                        ResetAll();
                        RefreshAllUIs();
                    });
            }
            catch (Exception e)
            {
                Log.Error("Could not ask about resetting the settings, so nothing was changed.", e);
            }
        }
    }
}
