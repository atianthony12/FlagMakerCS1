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

        /// <summary>
        /// A flag's shape: a small picture of each of <see cref="Row.Presets"/> (FlagProportion.Shapes,
        /// as the flag is seen), the one it has lit; a click gives it that shape. No setting of
        /// its own. The in-game panel only.
        /// </summary>
        Shape,

        /// <summary>A choice (an IntSetting over <see cref="Row.ChoiceValues"/>) as a row of buttons, the one chosen lit. The file writes it as a choice.</summary>
        Segmented,

        /// <summary>
        /// A picture of the player's (an IntSetting holding FlagImageNames' number, 0 for none): a
        /// button with its name and a thumbnail that opens the Image window (<see cref="Row.Image"/>),
        /// and a folder button that opens the images folder. The file writes its name.
        /// </summary>
        Image,

        /// <summary>
        /// A flag's pattern (an IntSetting over <see cref="Row.ChoiceValues"/>): a button with its
        /// name and a little picture of it that opens the Pattern window (<see cref="Row.Patterns"/>).
        /// The file writes it as a choice.
        /// </summary>
        Pattern,
    }

    /// <summary>
    /// Which part of the in-game panel a row is in: one of its two tabs (Flags, Settings), the
    /// New flag window, a page of the Flagpole popup or of the Pattern window (the new flag's, the
    /// selected one's). None = not in it.
    /// </summary>
    public enum PanelPage { None, New, Existing, Settings, PoleNew, PoleExisting, PatternNew, PatternExisting }

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

        /// <summary>
        /// A panel slider whose top moves (the pole height's, locked or not): its top now, held
        /// within Min..Max. Null: Max. Max stays the setting's range -- the file's values are held
        /// to it, not to this.
        /// </summary>
        public Func<float> SliderMax;

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

        /// <summary>
        /// A slider over no setting of its own (a flag's width and height, which are its length
        /// and proportions, FlagSize): what it shows, and what a move of it does. The row is then
        /// never saved or reset -- the settings it works through have rows of their own.
        /// </summary>
        public Func<float> Get;
        public Action<float> Set;

        /// <summary>
        /// A row's reset icon over no setting of its own, or one that needs more than its value
        /// put back (the width and height: the default size; the shape; a picture let go):
        /// whether it stands at its default, and what puts it there. Null: the setting's default.
        /// </summary>
        public Func<bool> AtDefault;
        public Action ResetTo;

        /// <summary>Shape rows: the shapes offered (height over width x 1000, as seen), which is the flag's now, the edge it is hoisted from (drawn on each), and what a click on one does.</summary>
        public int[] Presets;
        public Func<int, bool> PresetActive;
        public Func<int> PresetHoist;
        public Action<int> OnPreset;

        /// <summary>Segmented rows over the hoisted edge (FlagHoist): each button shows a small flag hoisted that way.</summary>
        public bool HoistIcons;

        /// <summary>Segmented rows over the cut (FlagCut): each button shows the cut as a little flag, its name the tooltip; hoisted as <see cref="PresetHoist"/> says, its depth <see cref="CutDepth"/>'s.</summary>
        public bool CutIcons;
        public FloatSetting CutDepth;

        /// <summary>
        /// Segmented rows over BITS (FlagHold): each button toggles its value's bit in
        /// <see cref="Int"/>, lit while the bit is set; any number lit at once.
        /// </summary>
        public bool MaskBits;

        /// <summary>Segmented rows over the holds: each button a little flag (cut by <see cref="IconCut"/>, hoisted as <see cref="PresetHoist"/>) with its held edge in gold.</summary>
        public bool HoldIcons;
        public IntSetting IconCut;

        /// <summary>Segmented rows whose buttons' tooltips change with the flag (the holds: a banner from its top has sides where a flag has a top and a bottom): the tooltip of choice i now.</summary>
        public Func<int, string> ChoiceTooltips;

        /// <summary>
        /// A Segmented row across the whole row: no name column, no reset icon (its first button is
        /// its way back) -- the New flag window's presets atop it (the author, 2026-11-19: "the
        /// buttons' texts are overflowing", "the presets can go on the top"): three buttons in the
        /// 230 px right of the name column were ~77 px each, "WALL FLAG" ~95 px of capitals.
        /// </summary>
        public bool FullWidth;

        /// <summary>A Choice row drawn as a dropdown whether or not its buttons would fit (Vanilla keeps its slider): the presets (2026-11-25).</summary>
        public bool Dropdown;

        /// <summary>
        /// A slider whose name column is only as wide as the page's wide rows' names need, not the
        /// page's 196: the track gets the rest (the flag's Width and Height, 0.5-30 m -- the author,
        /// 2026-11-10: "too aggressive ... enlarge the slider itself").
        /// </summary>
        public bool WideSlider;

        /// <summary>
        /// A colour row on a line of its own, named by its own label ("Pole color [ ]"), not one of
        /// three swatches under "Colors" (the author, 2026-11-11, of the Flagpole popup's two
        /// "Colors" lines: "it's not clear which is which").
        /// </summary>
        public bool ColourAlone;

        /// <summary>Image rows: the window's settings the Image window works on.</summary>
        public ImageTarget Image;

        /// <summary>Pattern rows: the settings the Pattern window works on.</summary>
        public PatternTarget Patterns;

        /// <summary>Segmented rows of icons (the atlas's sprites, one per value): no words on the buttons, each choice's name its tooltip.</summary>
        public string[] ChoiceIcons;

        /// <summary>Greys the row out.</summary>
        public Func<bool> Enabled;

        /// <summary>Leaves the row out of its panel, when the panel is built (what needs another mod). Null: always there.</summary>
        public Func<bool> Shown;

        /// <summary>
        /// Shows the row only while this says true, the page closing up without it (RowSet's
        /// blocks; checked at each refresh). Rows in a run share the same delegate. Null: always.
        /// </summary>
        public Func<bool> ShownWhile;

        /// <summary>True while this row's confirmation dialog is open (the stored value is still the old one).</summary>
        public bool Pending;

        /// <summary>The name this row is stored under in FlagMaker.xml and the log, and its key in the language files.</summary>
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

        /// <summary>
        /// The row's tooltip, or null: a row has one only if en.xml has its key (the author,
        /// 2026-11-11, of the Flags tab: "Remove all of the tooltips here, except on:
        /// Proportions/Shape/Anchor Points buttons, Motion" -- a tooltip is removed by deleting its
        /// text, no dead key left for translators).
        /// </summary>
        public string Tooltip
        {
            get { return Localization.HasEnglish(Name + ".Tooltip") ? Localization.Get(Name + ".Tooltip") : null; }
        }

        public string Label2
        {
            get { return Id2 != null ? Localization.Get(Id2 + ".Label") : null; }
        }

        public string Tooltip2
        {
            get { return Id2 != null && Localization.HasEnglish(Id2 + ".Tooltip") ? Localization.Get(Id2 + ".Tooltip") : null; }
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

            // Tooltips are optional (Tooltip): a row without one is not missing a key.
            if (Id2 != null)
                yield return Id2 + ".Label";

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

        /// <summary>The panel slider's top now: <see cref="SliderMax"/> within Min..Max, else Max.</summary>
        public float Top
        {
            get { return SliderMax != null ? Math.Max(Min, Math.Min(Max, SliderMax())) : Max; }
        }

        /// <summary>The slider position: percent rows are stored as a fraction and shown x100.</summary>
        public float Display
        {
            get
            {
                if (Get != null)
                    return Get();

                if (Float == null)
                    return 0f;

                return Kind == RowKind.Percent ? Float.value * 100f : Float.value;
            }
        }

        public void Store(float display)
        {
            if (Set != null)
            {
                Set(display);
                return;
            }

            if (Float == null)
                return;

            Float.value = Kind == RowKind.Percent ? display / 100f : display;
        }

        /// <summary>The row holds a setting of its own (saved, reset); a button, a status line, a width or a shape does not.</summary>
        public bool HasSetting
        {
            get { return Float != null || Bool != null || Int != null || Key != null; }
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

        /// <summary>The row can be put back by its reset icon: a setting of its own, or a way back (<see cref="ResetTo"/>).</summary>
        public bool CanReset
        {
            get { return ResetTo != null || HasSetting; }
        }

        /// <summary>At its default: what fades the row's reset icon.</summary>
        public bool IsAtDefault
        {
            get
            {
                if (AtDefault != null) return AtDefault();
                if (Float != null && Math.Abs(Float.value - DefaultFloat) > 1e-4f * Math.Max(1f, Math.Abs(DefaultFloat))) return false;
                if (Bool != null && Bool.value != DefaultBool) return false;
                if (Int != null && Int.value != DefaultInt) return false;
                if (Key != null && Key.value != DefaultKey) return false;
                return true;
            }
        }

        /// <summary>The row's reset icon's words.</summary>
        public string ResetHint
        {
            get { return Localization.Get("Reset.Default"); }
        }
    }

    /// <summary>
    /// Every setting in the mod, once, with where it is shown and what its default is. Both UIs
    /// are drawn from this list, FlagMaker.xml is written from it and Reset walks it (Volumetric
    /// Weather's design).
    /// </summary>
    /// <remarks>
    /// The split: the flags, the wind and the cloth are in the in-game panel (Row.Panel) -- two
    /// tabs, Flags (the city's, the selected one's controls) and Settings, and the New flag
    /// window its "+ New flag" button opens; the options page has the mod itself
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

            Log.Msg("settings catalog: " + Rows.Count + " rows | Flags=" + CountForPanel(PanelPage.Existing) +
                    " | New flag=" + CountForPanel(PanelPage.New) + " | Settings=" + CountForPanel(PanelPage.Settings) +
                    " | Flagpole=" + CountForPanel(PanelPage.PoleNew) + "+" + CountForPanel(PanelPage.PoleExisting) +
                    " | Pattern=" + CountForPanel(PanelPage.PatternNew) + "+" + CountForPanel(PanelPage.PatternExisting) +
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
                if (row.AfterChange != null && row.HasSetting && !row.IsTransient)
                    row.AfterChange();
            }

            SettingsXml.SaveNow();
            Log.Msg("settings: RESET to defaults (" + Rows.Count + " rows)");
        }

        /// <summary>
        /// A row's reset icon (Real Glass's and Volumetric Weather's): that row back to its default
        /// as if the player had moved it there -- its live hook run (the selected flag redesigned),
        /// saved now, every UI refreshed. A switch goes through <see cref="ApplyToggle"/> like a
        /// click. A line of colours has one icon for its swatches: the ones off their default go back.
        /// </summary>
        public static void ResetRows(IList<Row> rows)
        {
            var reset = new List<Row>();
            foreach (Row row in rows)
            {
                if (row != null && row.CanReset && row.IsEnabled && !row.IsAtDefault)
                    reset.Add(row);
            }

            if (reset.Count == 0)
                return;

            var hooks = new List<Action>();
            foreach (Row row in reset)
            {
                Log.Msg("setting: " + row.Name + " back to its default (the row's reset icon)");

                if (row.Kind == RowKind.Toggle && row.Bool != null)
                {
                    ApplyToggle(row, row.DefaultBool);
                    continue;
                }

                if (row.ResetTo != null)
                    row.ResetTo();
                else
                    row.ResetToDefault();

                // Once each: a line of colours shares the window's.
                if (row.AfterChange != null && !hooks.Contains(row.AfterChange))
                    hooks.Add(row.AfterChange);
            }

            SettingsXml.SaveNow();
            foreach (Action hook in hooks)
                hook();

            RefreshAllUIs();
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

        /// <summary>A number as the settings file writes it (a row with no slider of its own).</summary>
        private static string Plain(float value)
        {
            return SettingsXmlFormat.FormatNumber(value);
        }

        private static string Metres(float value)
        {
            return Localization.Get("Unit.Metres", value.ToString("0.#", CultureInfo.InvariantCulture));
        }

        /// <summary>"0.85 m": a block's size, in its 5 cm steps.</summary>
        private static string MetresFine(float value)
        {
            return Localization.Get("Unit.Metres", value.ToString("0.0#", CultureInfo.InvariantCulture));
        }

        private static string Degrees(float value)
        {
            return Localization.Get("Unit.Degrees", value.ToString("F0"));
        }

        /// <summary>The Flagpole button's words: "Flagpole: none" or "Flagpole: ball, 10 m, 2 ropes" (a crossbar's length is its wall's), and the tilt if any.</summary>
        private static Func<string> PoleSummary(IntSetting pole, FloatSetting height, IntSetting ropes, FloatSetting tilt, FloatSetting lean, IntSetting hoistedFrom)
        {
            return () =>
            {
                int style = pole != null ? pole.value : Settings.Defaults.Pole;
                string text = hoistedFrom != null && hoistedFrom.value == FlagHoist.Top
                    ? Localization.Get("PoleButton.Top")
                    : style == FlagPoleStyle.None
                    ? Localization.Get("PoleButton.None")
                    : Stands(tilt, lean, hoistedFrom)
                    ? Localization.Get("PoleButton.Tall", Localization.Get(PoleKey(style)), (height != null ? height.value : Settings.Defaults.Height).ToString("0.#", CultureInfo.InvariantCulture),
                                       ropes != null ? ropes.value : 0)
                    : Localization.Get("PoleButton.Pole", Localization.Get(PoleKey(style)), ropes != null ? ropes.value : 0);

                float t = tilt != null ? tilt.value : 0f, l = lean != null ? lean.value : 0f;
                if (Math.Abs(t) >= 0.5f || Math.Abs(l) >= 0.5f)
                    text += Localization.Get("PoleButton.Tilted", t.ToString("F0"), l.ToString("F0"));
                return text;
            };
        }

        /// <summary>A window's pole stands on its foot, by its tilt, lean and edge hoisted (FlagStand); else it is a crossbar reaching out level.</summary>
        private static bool Stands(FloatSetting tilt, FloatSetting lean, IntSetting hoistedFrom)
        {
            float hx, hy, hz, fx, fy, fz;
            FlagOrientation.Frame(0f, tilt != null ? tilt.value : 0f, lean != null ? lean.value : 0f, hoistedFrom != null ? hoistedFrom.value : FlagHoist.Side,
                                  out hx, out hy, out hz, out fx, out fy, out fz);
            return FlagStand.Upright(hy);
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

        /// <summary>For a row that changes nothing else when it changes (the new flag's size): the UIs still follow at once.</summary>
        private static readonly Action Nothing = () => { };

        /// <summary>
        /// The proportion the flag flies at: the one set, a country's flag's too since 2026-11-10
        /// (choosing a country sets its own; the Shape row and the sides change it).
        /// </summary>
        private static int FlownProportion(IntSetting country, IntSetting proportion)
        {
            return proportion != null ? proportion.value : Settings.Defaults.Proportion;
        }

        /// <summary>
        /// The default shape as seen with the edge it is hoisted from (a banner's is the default
        /// turned); a country's flag's is its own (the Shape row's reset gives it back).
        /// </summary>
        private static int DefaultShape(IntSetting country, IntSetting hoistedFrom)
        {
            FlagCountry flag = country != null ? FlagCountries.At(country.value) : null;
            return FlagSize.ShownProportion(flag != null ? flag.Proportion : Settings.Defaults.Proportion, hoistedFrom.value);
        }

        /// <summary>The flag's width (or height) as it is seen, metres (FlagSize.Shown).</summary>
        private static float ShownSide(bool height, IntSetting country, FloatSetting length, IntSetting proportion, IntSetting hoistedFrom)
        {
            float w, h;
            FlagSize.Shown(length.value, FlownProportion(country, proportion), hoistedFrom.value, out w, out h);
            return height ? h : w;
        }

        /// <summary>
        /// A new width or height (FlagSize.Resize): with its sides linked the whole flag scales;
        /// otherwise that side alone -- a country's flag's too since 2026-11-10 (stretched to its
        /// shape, CropMap.ForCountry). Linked, it scales at the shape the flag HAS, a picture's flag
        /// too (until 2026-11-10 at the picture's: the lock also locked Proportions, which the
        /// author did not want).
        /// </summary>
        private static void ResizeTo(bool height, float value, IntSetting country, FloatSetting length, IntSetting proportion, IntSetting hoistedFrom,
                                     ImageTarget pictures)
        {
            bool linked = Settings.LinkSize == null || Settings.LinkSize.value;
            float newLength;
            int newProportion;
            FlagSize.Resize(length.value, FlownProportion(country, proportion), hoistedFrom.value, height, value, linked,
                            out newLength, out newProportion);
            length.value = newLength;
            proportion.value = newProportion;
        }

        /// <summary>"2 m · 2:3": a height, and the shape it makes as seen (height to width).</summary>
        private static Func<float, string> HeightText(IntSetting country, IntSetting proportion, IntSetting hoistedFrom)
        {
            return v => Localization.Get("Unit.MetresShape", v.ToString("0.#", CultureInfo.InvariantCulture),
                                         FlagProportion.Name(FlagSize.ShownProportion(FlownProportion(country, proportion), hoistedFrom.value)));
        }

        /// <summary>The names of FlagCloth.Details, in its order.</summary>
        private static readonly string[] DetailNameKeys =
        {
            "ClothDetail.Low", "ClothDetail.Medium", "ClothDetail.High", "ClothDetail.VeryHigh", "ClothDetail.Ultra", "ClothDetail.Extreme",
        };

        /// <summary>For the log: "High (32 cells)".</summary>
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

        /// <summary>The "Cloth threads" choices: "Auto (4)", then the numbers.</summary>
        private static string ThreadsText(int value)
        {
            if (value <= 0)
                return Localization.Get("ClothThreads.Auto", ClothWorker.ThreadsFor(Environment.ProcessorCount).ToString(CultureInfo.InvariantCulture));
            return value.ToString(CultureInfo.InvariantCulture);
        }

        // ---- the catalog ----------------------------------------------------------------------

        private static void Build()
        {
            Settings.Init();

            // A first Init reads FlagMaker.xml, which is written from this list, so it may have
            // built it already.
            if (_rows != null)
                return;

            _rows = new List<Row>();

            BuildNew();
            BuildExisting();
            BuildWind();
            BuildGeneralOptions();
            BuildHidden();
        }

        private static void Add(Row row)
        {
            _rows.Add(row);
        }

        /// <summary>The loop of a page's flag (Recapture's words): the New flag window's design, else the selected flag's.</summary>
        private static LoopState LoopStateFor(PanelPage page, out float progress)
        {
            progress = 0f;
            if (FlagRenderer.Instance == null)
                return LoopState.None;
            return page == PanelPage.New ? FlagRenderer.LoopStateOf(Settings.NewSpec(), out progress)
                                         : FlagRenderer.LoopStateOf(FlagEditor.Selected, out progress);
        }

        /// <summary>
        /// A flag's design, the same rows in the New flag window and the Flags tab: the next
        /// flag's (settings) or the selected one's (transient). First the country: with one
        /// chosen, the pattern and its colours are the country's flag's and grey out (they are
        /// kept, for a change back), its shape its own until the player picks another (since
        /// 2026-11-10); its size, the edge it is hoisted from, the waviness
        /// and the darkness stay the player's -- the darkness is for a country's colours above
        /// all, which cannot be picked (the author: "some country flags are too bright"). Then a
        /// picture of the player's own (the author: "a folder icon which opens where the user
        /// should drag images. Then in-game there is a dropdown with the filenames"): a country
        /// and a picture take each other's place; the pattern stays the player's under a picture
        /// (it shows round one smaller than the flag, and through its see-through parts).
        /// </summary>
        /// <remarks>
        /// The size as the flag is seen (the author: "select the ratio for the flag in a more
        /// visual way ... control both width and height, but by default the aspect is locked"):
        /// the shapes as small pictures, then the width and the height -- sliders over the
        /// flag's length and proportion, which are what it keeps (FlagSize) -- linked unless the
        /// switch under them is off, and the edge it is hoisted from (the side, or the top: a
        /// banner), which turns what the width and height are of the cloth.
        /// </remarks>
        private static void AddDesign(PanelPage page, string shapeId, string widthId, string heightId, string recaptureId, IntSetting country, IntSetting image,
                                      ImageTarget pictures, IntSetting pattern, IntSetting c1, IntSetting c2, IntSetting c3, FloatSetting length,
                                      IntSetting proportion, IntSetting hoistedFrom, IntSetting motion, FloatSetting waviness, FloatSetting windShare,
                                      FloatSetting darkness,
                                      FloatSetting wear, FloatSetting tear, FloatSetting tearEdges, FloatSetting dirt, FloatSetting holes,
                                      IntSetting cut, FloatSetting cutDepth, IntSetting held, Action changed, Func<bool> enabled,
                                      Action placeRows)
        {
            // In folding groups, in the author's order (2026-11-11: "reorder the sections, in New
            // flag too: 1. design 2. size and shape 3. pole and placement etc."): Design (Darkness
            // with it), Size and shape, Pole and placement (the window's own rows, placeRows),
            // Movement, Wear and tear.
            Func<bool> patterned = () => (enabled == null || enabled()) && (country == null || country.value == 0);
            Action resized = changed ?? Nothing;

            // A shape of the player's choosing, a country's flag's too since 2026-11-10 (the author:
            // "allow vertical flags and aspect ratios, including enabling the preset shapes also on
            // country flags"; stretched to it, turned for one standing the other way:
            // CropMap.ForCountry) -- not a picture's while the width and height go together (the
            // flag keeps the picture's then).
            // Lock width and height no longer greys it with a picture on (2026-11-10).
            Func<bool> shapeFree = () => enabled == null || enabled();

            Add(new Row
            {
                Kind = RowKind.Country,
                Panel = page,
                Group = "Design",
                Int = country,
                DefaultInt = Settings.Defaults.Country,
                AfterChange = () =>
                {
                    // A country's flag takes the picture's place. Its width and height stay as they
                    // were (the author, 2026-11-10: "Changing Country is resetting the width and
                    // height of the flag. Fix it"): the design stretches to them; the Shape row's
                    // reset icon gives the country's own proportions.
                    if (country.value != 0 && image != null && image.value != 0)
                        image.value = 0;
                    resized();
                },
                Enabled = enabled,
            });

            Add(new Row
            {
                Kind = RowKind.Image,
                Panel = page,
                Int = image,
                DefaultInt = Settings.Defaults.Image,
                Image = pictures,
                AfterChange = resized,
                Enabled = enabled,
            });

            // The pattern: one row here, its choice, colours and saved ones in the Pattern window (the
            // author: "The Pattern options are taking too much space"). The colours are rows of
            // that window's page, kept and reset as they were.
            PanelPage patternPage = page == PanelPage.New ? PanelPage.PatternNew : PanelPage.PatternExisting;
            Add(new Row
            {
                Kind = RowKind.Pattern,
                Panel = page,
                Int = pattern,
                DefaultInt = Settings.Defaults.Pattern,
                ChoiceValues = FlagPattern.Values,
                ChoiceKeys = FlagPattern.NameKeys,
                Patterns = new PatternTarget
                {
                    Page = page,
                    Pattern = pattern,
                    Colour1 = c1,
                    Colour2 = c2,
                    Colour3 = c3,
                    Country = country,
                    Proportion = proportion,
                    HoistedFrom = hoistedFrom,
                    Cut = cut,
                    CutDepth = cutDepth,
                    Changed = changed,
                    Enabled = enabled,
                },
                AfterChange = changed,
                Enabled = patterned,
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

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = patternPage,
                Int = c1,
                DefaultInt = Settings.Defaults.Colour1,
                AfterChange = changed,
                Enabled = patterned,
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = patternPage,
                Int = c2,
                DefaultInt = Settings.Defaults.Colour2,
                AfterChange = changed,
                Enabled = () => patterned() && Uses(pattern, 2),
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = patternPage,
                Int = c3,
                DefaultInt = Settings.Defaults.Colour3,
                AfterChange = changed,
                Enabled = () => patterned() && Uses(pattern, 3),
            });

            Add(new Row
            {
                Kind = RowKind.Shape,
                Panel = page,
                Group = "Size",
                Id = shapeId,
                Presets = FlagProportion.Shapes,
                PresetActive = shape => FlagSize.IsShape(FlownProportion(country, proportion), hoistedFrom.value, shape),
                PresetHoist = () => hoistedFrom.value,
                OnPreset = shape =>
                {
                    float newLength;
                    int newProportion;
                    FlagSize.Reshape(length.value, proportion.value, hoistedFrom.value, shape, out newLength, out newProportion);
                    length.value = newLength;
                    proportion.value = newProportion;
                },
                // Its reset: the default shape as seen, as a click on that little flag gives it (a
                // country's flag's own).
                AtDefault = () => FlagSize.IsShape(FlownProportion(country, proportion), hoistedFrom.value, DefaultShape(country, hoistedFrom)),
                ResetTo = () =>
                {
                    float newLength;
                    int newProportion;
                    FlagSize.Reshape(length.value, proportion.value, hoistedFrom.value, DefaultShape(country, hoistedFrom), out newLength, out newProportion);
                    length.value = newLength;
                    proportion.value = newProportion;
                },
                AfterChange = resized,
                Enabled = shapeFree,
            });

            // The width's and the height's reset: the default size (its length) at the shape it has.
            Func<bool> sizeAtDefault = () => Math.Abs(length.value - Settings.Defaults.Length) < 0.005f;
            Action sizeToDefault = () => length.value = Settings.Defaults.Length;

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = page,
                Id = widthId,
                Get = () => ShownSide(false, country, length, proportion, hoistedFrom),
                Set = v => ResizeTo(false, v, country, length, proportion, hoistedFrom, pictures),
                AtDefault = sizeAtDefault,
                ResetTo = sizeToDefault,
                Min = FlagSize.Min, Max = FlagSize.Max, Step = 0.1f,
                WideSlider = true,
                Format = Metres,
                AfterChange = resized,
                Enabled = enabled,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = page,
                Id = heightId,
                Get = () => ShownSide(true, country, length, proportion, hoistedFrom),
                Set = v => ResizeTo(true, v, country, length, proportion, hoistedFrom, pictures),
                AtDefault = sizeAtDefault,
                ResetTo = sizeToDefault,
                Min = FlagSize.Min, Max = FlagSize.Max, Step = 0.1f,
                WideSlider = true,
                Format = HeightText(country, proportion, hoistedFrom),
                AfterChange = resized,
                Enabled = enabled,
            });

            // One switch for both windows, a country's flag's sides too (since 2026-11-10).
            // Switched back on, a flag with a picture takes the picture's shape again.
            Add(new Row
            {
                Kind = RowKind.Toggle,
                Panel = page,
                Bool = Settings.LinkSize,
                DefaultBool = Settings.Defaults.LinkSize,
                // It locks the sides to each other only, never the Proportions row (the author,
                // 2026-11-10: "it's also locking Proportions which we don't want"): ticked again it no
                // longer puts a picture's shape back over the one chosen.
                AfterChange = State("width and height together", OnOff(Settings.LinkSize)),
                Enabled = enabled,
            });

            // Hoisted the other way, a picture stays upright as seen: a flag tied to its picture's
            // shape turns its cloth to keep it.
            Add(new Row
            {
                Kind = RowKind.Segmented,
                Panel = page,
                Int = hoistedFrom,
                DefaultInt = Settings.Defaults.HoistedFrom,
                ChoiceValues = FlagHoist.Values,
                ChoiceKeys = FlagHoist.NameKeys,
                HoistIcons = true,
                AfterChange = () =>
                {
                    if (pictures != null)
                        pictures.KeepShape();
                    resized();
                },
                Enabled = enabled,
            });

            // The cut (the author, 2026-11-07: "let's add more types like fanion, swallowtail, and
            // more"): the fly's outline, a little flag a button; any flag, a country's too (a
            // Danish split flag is a swallowtail). Its depth where it has one.
            string whose = page == PanelPage.New ? "the new flag" : "the selected flag";
            Add(new Row
            {
                Kind = RowKind.Segmented,
                Panel = page,
                Int = cut,
                DefaultInt = Settings.Defaults.Cut,
                ChoiceValues = FlagCut.Values,
                ChoiceKeys = FlagCut.NameKeys,
                CutIcons = true,
                CutDepth = cutDepth,
                PresetHoist = () => hoistedFrom.value,
                AfterChange = () =>
                {
                    Log.Msg("flags: " + whose + " cut: " + FlagCut.LogName(cut.value) +
                            (FlagCut.UsesDepth(cut.value) ? ", " + (cutDepth.value * 100f).ToString("F0", CultureInfo.InvariantCulture) + "% deep" : ""));
                    resized();
                },
                Enabled = enabled,
            });

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = cutDepth,
                DefaultFloat = Settings.Defaults.CutDepth,
                Min = FlagCut.MinDepth * 100f, Max = FlagCut.MaxDepth * 100f, Step = 1f,
                AfterChange = resized,
                Enabled = () => (enabled == null || enabled()) && FlagCut.UsesDepth(cut.value),
            });

            // Pole and placement: the window's own (its Flagpole button first, which starts the group).
            if (placeRows != null)
            {
                int first = Rows.Count;
                placeRows();
                if (Rows.Count > first)
                    Rows[first].Group = "Place";
            }

            // Also held at (the author, 2026-11-11: banners "hoisted from the side, but are steady
            // at the top and bottom ... the flag itself is cloth and ripples"; "Yes, toggles"): a
            // toggle a bit (FlagHold), each a little flag with the edge it holds in gold, hoisted
            // and cut as the flag. Nothing to hold on a flat flag.
            Add(new Row
            {
                Kind = RowKind.Segmented,
                Panel = page,
                Group = "Movement",
                Int = held,
                DefaultInt = Settings.Defaults.Held,
                ChoiceValues = FlagHold.Values,
                ChoiceKeys = FlagHold.NameKeys,
                MaskBits = true,
                HoldIcons = true,
                IconCut = cut,
                ChoiceTooltips = i => Localization.Get(hoistedFrom.value == FlagHoist.Top ? FlagHold.BannerTooltipKeys[i] : FlagHold.TooltipKeys[i]),
                CutDepth = cutDepth,
                PresetHoist = () => hoistedFrom.value,
                AfterChange = () =>
                {
                    Log.Msg("flags: " + whose + " held at " + FlagHold.LogName(held.value));
                    (changed ?? Nothing)();
                },
                Enabled = () => (enabled == null || enabled()) && motion.value != FlagMotion.Flat,
            });

            // Whether it moves (the author: "Some flags should be also static"): an option of its
            // own, not How wavy at 0% (which is a stiff cloth that still flies); How wavy shapes a
            // still flag's ripples too, and has nothing to do on a flat one.
            string who = page == PanelPage.New ? "the new flag" : "the selected flag";

            // Its take (FlagSpec.Take; the author, 2026-11-11: "if i make a flag Still then back to
            // waving, then Still again, the flag state will be the same as the first time, so the
            // motion should recapture on its own"): a new one each time it is made still or a loop,
            // and on Recapture -- a new still shape, a new loop.
            IntSetting take = page == PanelPage.New ? Settings.NewTake : Settings.SelTake;
            Action newTake = () =>
            {
                take.value = FlagSpec.NextTake(take.value);
                Log.Msg("flags: " + who + "'s " + (motion.value == FlagMotion.Loop ? "loop" : "still shape") + " is made afresh (take " + take.value + ")");
            };
            // A choice row (the strip Ropes and Base use), not separate buttons: four buttons at
            // the column's quarter cut "Waving" (the author, 2026-11-11: "Waving is overflowing.
            // change these from pills to tabs"); the strip reaches into the names' column, or is a
            // dropdown where a language's names don't fit (ChoiceFit). Each segment keeps its own
            // tooltip (RowSet's Choice case, Motion.*.Tooltip).
            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = page,
                Int = motion,
                DefaultInt = Settings.Defaults.Motion,
                ChoiceValues = FlagMotion.Values,
                ChoiceKeys = FlagMotion.NameKeys,
                AfterChange = () =>
                {
                    Log.Msg("flags: " + who + " " + (motion.value == FlagMotion.Waves ? "waves in the wind" :
                                                     motion.value == FlagMotion.Loop ? "plays a loop" :
                                                     motion.value == FlagMotion.Still ? "keeps still" : "is held flat"));
                    // Into Still only: a loop's take stays (like loop flags placed at other times
                    // share one bake, 1-2.5 MB and ~2 s each); Recapture gives it another.
                    if (motion.value == FlagMotion.Still)
                        newTake();
                    // The player's own Waving after crowds went to Loop: kept while placing.
                    if (page == PanelPage.New)
                        FlagRenderer.CrowdMotionChosen(motion.value);
                    (changed ?? Nothing)();
                },
                Enabled = enabled,
            });

            // Recapture: another loop of the same flag (the author, 2026-11-11: "only visible if
            // Motion is set to Loop" -- Still makes a new pose by itself each time it is chosen).
            // While the loop is being recorded it says how far, greyed, and Recapture again once
            // made (the author, 2026-11-12: "it's a bit confusing when I switch to Loop and
            // nothing changes"); a loop that cannot be made says so, and stays clickable -- a new
            // take is another loop, tried afresh. The selected flag's loop, or the new flag's
            // design's (FlagRenderer.LoopStateOf).
            Add(new Row
            {
                Kind = RowKind.Button,
                Panel = page,
                Id = recaptureId,
                OnClick = () =>
                {
                    newTake();
                    (changed ?? Nothing)();
                },
                ButtonText = () =>
                {
                    float progress;
                    LoopState state = LoopStateFor(page, out progress);
                    if (state == LoopState.Recording)
                        return Localization.Get("Motion.Recording", (int)Math.Floor(progress * 100f));
                    if (state == LoopState.Failed)
                        return Localization.Get("Motion.CantRecord");
                    return Localization.Get(recaptureId + ".Label");
                },
                Enabled = () =>
                {
                    float progress;
                    return (enabled == null || enabled()) && motion.value == FlagMotion.Loop && LoopStateFor(page, out progress) != LoopState.Recording;
                },
                ShownWhile = () => motion.value == FlagMotion.Loop,
            });

            // Why the new flag went to Loop by itself (crowds placed as Loop, 2026-11-12): shown
            // while that switch holds.
            if (page == PanelPage.New)
            {
                Add(new Row
                {
                    Kind = RowKind.Status,
                    Panel = page,
                    Id = "NewCrowdNote",
                    StatusText = () => Localization.Get("Motion.CrowdNote"),
                    ShownWhile = FlagRenderer.CrowdNoteShown,
                });
            }

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = waviness,
                DefaultFloat = Settings.Defaults.Waviness,
                Min = 0f, Max = 200f, Step = 5f,
                AfterChange = changed,
                Enabled = () => (enabled == null || enabled()) && motion.value != FlagMotion.Flat,
            });

            // Wind on it (the author, 2026-11-07: how much the flag moves, beside How wavy's
            // ripples): the share of the wind where it stands (FlagSpec.WindShare) -- of its own
            // breeze for a flag that plays a loop (LoopMotion.Wind). Not a still one's (its shape
            // is made in its own breeze, whole).
            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = windShare,
                DefaultFloat = Settings.Defaults.WindShare,
                Min = 0f, Max = 100f, Step = 5f,
                AfterChange = changed,
                Enabled = () => (enabled == null || enabled()) && FlagMotion.Moves(motion.value),
            });

            // Wear and tear (the author, 2026-11-03: "Wear and Tear, Dirt, Holes (like bullet holes
            // but not just) properties on flags as an additional option"): the cloth's look only
            // (FlagWear.cginc), every flag its own scatter. As new by default.
            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Group = "Wear",
                Float = wear,
                DefaultFloat = Settings.Defaults.Wear,
                Min = 0f, Max = 100f, Step = 5f,
                AfterChange = changed,
                Enabled = enabled,
            });

            // Torn (the author, 2026-11-04: "tear, like the cloth being torn off like pirate flags").
            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = tear,
                DefaultFloat = Settings.Defaults.Tear,
                Min = 0f, Max = 100f, Step = 5f,
                AfterChange = changed,
                Enabled = enabled,
            });

            // Torn edges (the author, 2026-11-07: "right now the torn only goes on one side ... a
            // secondary tear slider"): the top and bottom edges ripped, one worse than the other.
            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = tearEdges,
                DefaultFloat = Settings.Defaults.TearEdges,
                Min = 0f, Max = 100f, Step = 5f,
                AfterChange = changed,
                Enabled = enabled,
            });

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = dirt,
                DefaultFloat = Settings.Defaults.Dirt,
                Min = 0f, Max = 100f, Step = 5f,
                AfterChange = changed,
                Enabled = enabled,
            });

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = holes,
                DefaultFloat = Settings.Defaults.Holes,
                Min = 0f, Max = 100f, Step = 5f,
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
        private static void AddPole(PanelPage page, IntSetting pole, FloatSetting height, IntSetting ropes, IntSetting poleColour, IntSetting topColour,
                                    IntSetting ropeColour, FloatSetting thickness, FloatSetting tilt, FloatSetting lean, IntSetting hoistedFrom,
                                    IntSetting profile, IntSetting poleBase, FloatSetting shine, FloatSetting ropeThickness,
                                    FloatSetting position, IntSetting finish, IntSetting blockShape, FloatSetting blockWidth,
                                    FloatSetting blockHeight, IntSetting blockColour, FloatSetting blockWear,
                                    Action changed, Action heightChanged, Func<bool> enabled)
        {
            // Hoisted from its top, a flag has no pole (FlagSpec.HasPole): the pole's rows grey out,
            // the choice kept for its side again; the tilt and lean still turn the banner.
            Func<bool> side = () => (enabled == null || enabled()) && hoistedFrom.value != FlagHoist.Top;
            Func<bool> drawn = () => side() && pole.value != FlagPoleStyle.None;
            Func<bool> standing = () => drawn() && Stands(tilt, lean, hoistedFrom);

            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = page,
                Int = pole,
                DefaultInt = Settings.Defaults.Pole,
                ChoiceValues = FlagPoleStyle.Values,
                ChoiceKeys = FlagPoleStyle.NameKeys,
                AfterChange = changed,
                Enabled = side,
            });

            // Its shape and, on its own, what it stands on (the author, 2026-11-09: "More pole shapes
            // ... Some flagpoles are like 'layered'"; then "separate the bottom from the shape itself").
            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = page,
                Int = profile,
                DefaultInt = Settings.Defaults.PoleProfile,
                ChoiceValues = FlagPoleProfile.Values,
                ChoiceKeys = FlagPoleProfile.NameKeys,
                AfterChange = changed,
                Enabled = drawn,
            });

            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = page,
                Int = poleBase,
                DefaultInt = Settings.Defaults.PoleBase,
                ChoiceValues = FlagPoleBase.Values,
                ChoiceKeys = FlagPoleBase.NameKeys,
                AfterChange = changed,
                Enabled = drawn,
            });

            // The concrete block's own rows, under Base and only with a Block base (the author,
            // 2026-11-19: "basic options like size, color, rounded or not, wear and tear"), as the
            // rope's rows only with a rope: one condition, shared (RowSet makes a block of the run
            // of rows with the same one). Grey where it is not drawn: a pole that does not stand.
            Func<bool> hasBlock = () => poleBase.value == FlagPoleBase.Block;
            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = page,
                Int = blockShape,
                DefaultInt = Settings.Defaults.BlockShape,
                ChoiceValues = FlagBlockShape.Values,
                ChoiceKeys = FlagBlockShape.NameKeys,
                AfterChange = changed,
                Enabled = standing,
                ShownWhile = hasBlock,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = page,
                Float = blockWidth,
                DefaultFloat = Settings.Defaults.BlockWidth,
                Min = FlagSpec.MinBlockWidth, Max = FlagSpec.MaxBlockWidth, Step = 0.05f,
                Format = MetresFine,
                AfterChange = changed,
                Enabled = standing,
                ShownWhile = hasBlock,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = page,
                Float = blockHeight,
                DefaultFloat = Settings.Defaults.BlockHeight,
                Min = FlagSpec.MinBlockHeight, Max = FlagSpec.MaxBlockHeight, Step = 0.05f,
                Format = MetresFine,
                AfterChange = changed,
                Enabled = standing,
                ShownWhile = hasBlock,
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = page,
                Int = blockColour,
                ColourAlone = true,
                DefaultInt = Settings.Defaults.BlockColour,
                AfterChange = changed,
                Enabled = standing,
                ShownWhile = hasBlock,
            });

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = blockWear,
                DefaultFloat = Settings.Defaults.BlockWear,
                Min = 0f, Max = 100f, Step = 5f,
                AfterChange = changed,
                Enabled = standing,
                ShownWhile = hasBlock,
            });

            // How tall a pole that stands is (its foot to the flag's top); a crossbar reaching out
            // level finds its own length, along to its wall. The slider goes to LockedHeight (the
            // author: "The range is too far"); its "Allow taller poles" switch is gone (2026-11-11:
            // "let's just remove this. no need for it"). A pole taller than the slider reaches (an
            // older one, a copy, the file's value) keeps its height, and its readout says it.
            Func<float> top = () => Settings.Defaults.LockedHeight;
            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = page,
                Float = height,
                DefaultFloat = Settings.Defaults.Height,
                Min = 1f, Max = Settings.Defaults.MaxNewHeight, Step = 0.5f,
                SliderMax = top,
                Format = Metres,
                ReadoutOverride = () => height.value > top() + 0.01f ? Metres(height.value) : null,
                AfterChange = heightChanged,
                Enabled = standing,
            });

            // Where the flag flies on its pole (the author, 2026-11-10: "like half masting"): 100%
            // the top, 50% half-mast; the pole keeps its height and foot (FlagPoleShape.Drop).
            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = position,
                DefaultFloat = Settings.Defaults.PolePosition,
                Min = 0f, Max = 100f, Step = 5f,
                AfterChange = changed,
                Enabled = standing,
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

            // The rope's rows are shown only with a rope (the author, 2026-11-11: "if there's no rope,
            // hide this row"); PolePanel.Relayout closes the popup up. One condition, shared.
            Func<bool> hasRopes = () => ropes.value > 0;

            // How thick the ropes are against the pole's own (the author, 2026-11-09: "we can add a
            // thickness slider" for the ropes).
            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = ropeThickness,
                DefaultFloat = Settings.Defaults.RopeThickness,
                Min = FlagSpec.MinRopeThickness * 100f, Max = FlagSpec.MaxRopeThickness * 100f, Step = 5f,
                AfterChange = changed,
                Enabled = () => drawn() && ropes.value > 0,
                ShownWhile = hasRopes,
            });

            // Painted, or bare galvanized zinc (the author, 2026-11-19): zinc has its own grey, so its
            // pole colour row goes while it is galvanized; the Shine stays (new bright zinc, dull aged).
            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = page,
                Int = finish,
                DefaultInt = Settings.Defaults.PoleFinish,
                ChoiceValues = FlagPoleFinish.Values,
                ChoiceKeys = FlagPoleFinish.NameKeys,
                AfterChange = changed,
                Enabled = drawn,
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = page,
                Int = poleColour,
                ColourAlone = true,
                DefaultInt = Settings.Defaults.PoleColour,
                AfterChange = changed,
                Enabled = drawn,
                ShownWhile = () => finish.value != FlagPoleFinish.Galvanized,
            });

            // How glossy its paint is, in place of materials (the author, 2026-11-09: "We have the
            // color picker. If anything, let's just add a slider for specular/shininess").
            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = shine,
                DefaultFloat = Settings.Defaults.PoleShine,
                Min = 0f, Max = 100f, Step = 5f,
                AfterChange = changed,
                Enabled = drawn,
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = page,
                Int = topColour,
                ColourAlone = true,
                DefaultInt = Settings.Defaults.TopColour,
                AfterChange = changed,
                Enabled = () => drawn() && FlagPoleStyle.HasGilt(pole.value),
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = page,
                Int = ropeColour,
                ColourAlone = true,
                DefaultInt = Settings.Defaults.RopeColour,
                AfterChange = changed,
                Enabled = () => drawn() && ropes.value > 0,
                ShownWhile = hasRopes,
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

        /// <summary>
        /// The Flagpole popup's wear and tear, last on its page: worn, rust and dirt -- the pole's
        /// look only (FlagPole.shader), every pole its own scatter.
        /// </summary>
        private static void AddPoleWear(PanelPage page, IntSetting pole, IntSetting hoistedFrom, FloatSetting wear, FloatSetting rust,
                                        FloatSetting dirt, Action changed, Func<bool> enabled)
        {
            Func<bool> drawn = () => (enabled == null || enabled()) && hoistedFrom.value != FlagHoist.Top && pole.value != FlagPoleStyle.None;
            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Group = "Wear",
                Float = wear,
                DefaultFloat = Settings.Defaults.PoleWear,
                Min = 0f, Max = 100f, Step = 5f,
                AfterChange = changed,
                Enabled = drawn,
            });

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = rust,
                DefaultFloat = Settings.Defaults.PoleRust,
                Min = 0f, Max = 100f, Step = 5f,
                AfterChange = changed,
                Enabled = drawn,
            });

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = page,
                Float = dirt,
                DefaultFloat = Settings.Defaults.PoleDirt,
                Min = 0f, Max = 100f, Step = 5f,
                AfterChange = changed,
                Enabled = drawn,
            });
        }

        /// <summary>A window's Flagpole button: its words say what the pole is; a click opens the popup's page for it, beside the window (<paramref name="beside"/>, a UI component's name).</summary>
        private static void AddPoleButton(PanelPage tab, PanelPage popup, string id, string beside, IntSetting pole, FloatSetting height, IntSetting ropes,
                                          FloatSetting tilt, FloatSetting lean, IntSetting hoistedFrom, Func<bool> enabled)
        {
            Add(new Row
            {
                Kind = RowKind.Button,
                Panel = tab,
                Id = id,
                ButtonText = PoleSummary(pole, height, ropes, tilt, lean, hoistedFrom),
                OnClick = () => PolePanel.Toggle(popup, Component(beside)),
                Enabled = enabled,
            });
        }

        /// <summary>A window of the mod's by its name, for a popup to open beside (null: the popup keeps its place).</summary>
        private static ColossalFramework.UI.UIComponent Component(string name)
        {
            ColossalFramework.UI.UIView view = ColossalFramework.UI.UIView.GetAView();
            return view != null ? view.FindUIComponent(name) : null;
        }

        /// <summary>
        /// The New flag window: the next flag -- its design, which way it points, how high its
        /// hoist's top stands. While it is open the flag follows the cursor and a click places it
        /// (the author: "a plus button somewhere to add new flag. The options for the new flag
        /// will be in a new window, and if the window is closed or the user presses Esc it goes
        /// away").
        /// </summary>
        private static void BuildNew()
        {
            var pictures = new ImageTarget
            {
                Page = PanelPage.New,
                Image = Settings.NewImage,
                X = Settings.NewImageX,
                Y = Settings.NewImageY,
                Zoom = Settings.NewImageZoom,
                Turn = Settings.NewImageTurn,
                Alpha = Settings.NewImageAlpha,
                Country = Settings.NewCountry,
                Length = Settings.NewLength,
                Proportion = Settings.NewProportion,
                HoistedFrom = Settings.NewHoistedFrom,
                Colour1 = Settings.NewColour1,
            };

            AddNewPreset();
            AddDesign(PanelPage.New, "NewShape", "NewWidth", "NewTall", "NewRecapture", Settings.NewCountry, Settings.NewImage, pictures, Settings.NewPattern,
                      Settings.NewColour1, Settings.NewColour2, Settings.NewColour3, Settings.NewLength, Settings.NewProportion,
                      Settings.NewHoistedFrom, Settings.NewMotion, Settings.NewWaviness, Settings.NewWindShare, Settings.NewDarkness,
                      Settings.NewWear, Settings.NewTear, Settings.NewTearEdges, Settings.NewDirt, Settings.NewHoles,
                      Settings.NewCut, Settings.NewCutDepth, Settings.NewHeld, null, null, AddNewPlacement);
            AddNewHidden();
        }

        /// <summary>
        /// The placement presets atop the New flag window (2026-11-19; the author: "Presets for
        /// flag orientation, like flags on buildings ... Default ... Wall Flag ... A third option
        /// would be those banners", then "the presets can go on the top, not under the
        /// flagpole"). A choice writes the hoist, the pole, the holds and the size
        /// (PlacementPreset); Default is its way back. 2026-11-24 (the author: "its own
        /// accordion/category would be better"): a folding group of its own, Presets. 2026-11-25
        /// (the author, of the icon buttons tried the day before: "I don't like the icons tbh. I
        /// think it's best if we do a dropdown instead"): a dropdown -- With pole, Hanging on wall,
        /// Banner, and Custom, which it shows when the settings make none (choosing it changes
        /// nothing).
        /// </summary>
        private static void AddNewPreset()
        {
            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = PanelPage.New,
                Group = "Preset",
                Int = Settings.NewPreset,
                DefaultInt = PlacementPreset.Default,
                ChoiceValues = PlacementPreset.Values,
                ChoiceKeys = PlacementPreset.NameKeys,
                Dropdown = true,
                AfterChange = () => PlacementPreset.Apply(Settings.NewPreset.value),
            });
        }

        /// <summary>The New flag window's Pole and placement group: its Flagpole button first (it starts the group), the status line last.</summary>
        private static void AddNewPlacement()
        {
            // The new flag stands where it is put (FlagStand), Page Up and Page Down raising it: no
            // Elevation of its own. Its pole's height is in the Flagpole popup.
            AddPoleButton(PanelPage.New, PanelPage.PoleNew, "NewPoleButton", NewFlagPanel.ComponentName, Settings.NewPole, Settings.NewHeight, Settings.NewRopes,
                          Settings.NewTilt, Settings.NewLean, Settings.NewHoistedFrom, null);
            AddPole(PanelPage.PoleNew, Settings.NewPole, Settings.NewHeight, Settings.NewRopes, Settings.NewPoleColour, Settings.NewTopColour, Settings.NewRopeColour,
                    Settings.NewPoleThickness, Settings.NewTilt, Settings.NewLean, Settings.NewHoistedFrom,
                    Settings.NewPoleProfile, Settings.NewPoleBase, Settings.NewPoleShine, Settings.NewRopeThickness, Settings.NewPolePosition,
                    Settings.NewPoleFinish, Settings.NewBlockShape, Settings.NewBlockWidth, Settings.NewBlockHeight, Settings.NewBlockColour,
                    Settings.NewBlockWear, null, null, null);
            AddPoleWear(PanelPage.PoleNew, Settings.NewPole, Settings.NewHoistedFrom, Settings.NewPoleWear, Settings.NewPoleRust,
                        Settings.NewPoleDirt, null, null);

            // Rows of flags, by the Line Tool mod: these three only with it (LineFlags).
            Func<bool> inRows = () => Settings.NewPlacing.value != LineFlags.Single;
            Add(new Row
            {
                Kind = RowKind.Segmented,
                Panel = PanelPage.New,
                Int = Settings.NewPlacing,
                DefaultInt = LineFlags.Single,
                ChoiceValues = LineFlags.Values,
                ChoiceKeys = LineFlags.NameKeys,
                ChoiceIcons = LineFlags.Icons,
                AfterChange = () => LineFlags.Choose(Settings.NewPlacing.value),
                Shown = LineFlags.IsAvailable,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = PanelPage.New,
                Float = Settings.NewLineSpacing,
                DefaultFloat = Settings.Defaults.LineSpacing,
                Min = 1f, Max = 100f, Step = 0.5f,
                Format = Metres,
                Enabled = inRows,
                Shown = LineFlags.IsAvailable,
            });

            Add(new Row
            {
                Kind = RowKind.Toggle,
                Panel = PanelPage.New,
                Bool = Settings.NewLineAlong,
                DefaultBool = Settings.Defaults.LineAlong,
                Enabled = inRows,
                Shown = LineFlags.IsAvailable,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = PanelPage.New,
                Float = Settings.NewRotation,
                DefaultFloat = Settings.Defaults.Rotation,
                Min = 0f, Max = 359f, Step = 1f,
                Format = Degrees,
            });

            // A wall flag goes on the wall the pointer is on (2026-11-19, with the presets): shown
            // only for a pole tilted out by its side hoist (FlagEdgeSnap.WallFlag), one flag at a
            // time as Snap to edges is, not Line Tool's rows.
            Add(new Row
            {
                Kind = RowKind.Toggle,
                Panel = PanelPage.New,
                Bool = Settings.NewSnapWalls,
                DefaultBool = Settings.Defaults.SnapWalls,
                AfterChange = State("snap to walls", OnOff(Settings.NewSnapWalls)),
                Enabled = () => Settings.NewPlacing.value == LineFlags.Single,
                ShownWhile = () => FlagEdgeSnap.WallFlag(Settings.NewPole.value, Settings.NewHoistedFrom.value, Settings.NewTilt.value, Settings.NewLean.value),
            });

            // Snap to edges and Back plate are a top banner's only: shown only while the new flag
            // hangs from its top (the author, 2026-11-11: "show these only when flags hang from the
            // top, no need to show them otherwise"). One condition for both: one block (RowSet).
            Func<bool> newTop = () => Settings.NewHoistedFrom.value == FlagHoist.Top;

            // A banner hoisted from its top snaps to a roof's edge (the author, 2026-11-07: "snap to
            // edge too"): one flag at a time, not Line Tool's rows.
            Add(new Row
            {
                Kind = RowKind.Toggle,
                Panel = PanelPage.New,
                Bool = Settings.NewSnapEdges,
                DefaultBool = Settings.Defaults.SnapEdges,
                AfterChange = State("snap to roof edges", OnOff(Settings.NewSnapEdges)),
                Enabled = () => Settings.NewHoistedFrom.value == FlagHoist.Top && Settings.NewPlacing.value == LineFlags.Single,
                ShownWhile = newTop,
            });

            // Under it: a banner from its top keeps in front of a plate through its hoist, never
            // back into the wall behind it (the author, 2026-11-09; FlagSpec.BackPlate).
            Add(new Row
            {
                Kind = RowKind.Toggle,
                Panel = PanelPage.New,
                Bool = Settings.NewBackPlate,
                DefaultBool = Settings.Defaults.BackPlate,
                AfterChange = State("back plate", OnOff(Settings.NewBackPlate)),
                Enabled = () => Settings.NewHoistedFrom.value == FlagHoist.Top && FlagMotion.Moves(Settings.NewMotion.value),
                ShownWhile = newTop,
            });

            Add(new Row
            {
                Kind = RowKind.Status,
                Panel = PanelPage.New,
                Id = "NewStatus",
                StatusText = FlagRenderer.Describe,
            });
        }

        /// <summary>The New flag window's settings with no row of their own: kept in the file, put back by a reset.</summary>
        private static void AddNewHidden()
        {
            // What the width and height sliders work through: kept in the file, put back by a
            // reset, but not shown as they are.
            Add(new Row
            {
                Kind = RowKind.Value,
                Float = Settings.NewLength,
                DefaultFloat = Settings.Defaults.Length,
                Min = FlagSize.Min, Max = FlagSize.Max, Step = 0.1f,
                Format = Metres,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Int = Settings.NewProportion,
                DefaultInt = Settings.Defaults.Proportion,
                Min = FlagSize.MinProportion, Max = FlagSize.MaxProportion, Step = 1f,
            });

            // Where the new flag's picture lies on it: the Image window sets them; kept in the file
            // and put back by a reset, not shown as rows.
            Add(new Row
            {
                Kind = RowKind.Value,
                Float = Settings.NewImageX,
                DefaultFloat = Settings.Defaults.ImageX,
                Min = -8f, Max = 9f, Step = 0.001f,
                Format = Plain,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Float = Settings.NewImageY,
                DefaultFloat = Settings.Defaults.ImageY,
                Min = -8f, Max = 9f, Step = 0.001f,
                Format = Plain,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Float = Settings.NewImageZoom,
                DefaultFloat = Settings.Defaults.ImageZoom,
                Min = FlagCrop.MinZoom, Max = FlagCrop.MaxZoom, Step = 0.01f,
                Format = Plain,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Int = Settings.NewImageTurn,
                DefaultInt = Settings.Defaults.ImageTurn,
                Min = 0f, Max = 3f, Step = 1f,
            });

            // The new flag's take of its motion (FlagSpec.Take): the Motion row and Recapture set it.
            Add(new Row
            {
                Kind = RowKind.Value,
                Int = Settings.NewTake,
                DefaultInt = Settings.Defaults.Take,
                Min = 0f, Max = FlagSpec.MaxTake, Step = 1f,
            });

            Add(new Row
            {
                Kind = RowKind.Choice,
                Int = Settings.NewImageAlpha,
                DefaultInt = Settings.Defaults.ImageAlpha,
                ChoiceValues = FlagImageAlpha.Values,
                ChoiceKeys = FlagImageAlpha.NameKeys,
            });
        }

        /// <summary>
        /// The Flags tab: the button that opens the New flag window, every flag in the city -- a
        /// click selects one ("clicking each will allow editing") -- and the same controls as a
        /// new flag's over the selected one, applied at once.
        /// </summary>
        private static void BuildExisting()
        {
            Add(new Row
            {
                Kind = RowKind.Button,
                Panel = PanelPage.Existing,
                Id = "NewFlag",
                OnClick = NewFlagPanel.Toggle,
                Enabled = () => FlagRenderer.Instance != null && FlagPrefab.Info != null,
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
                Kind = RowKind.FlagList,
                Panel = PanelPage.Existing,
                Id = "FlagList",
            });

            // No status line under the list (the author, 2026-11-11, of "Flag 1043 at x 312, z -1180,
            // 12.0 m up." / "Nothing selected ...": "remove this entirely"): the list's lit row says
            // which flag is selected.

            // Everything below is the selected flag's, shown only while one is selected: with
            // none the tab is the button and the list (the author, 2026-11-08:
            // "if a flag is not selected, let's not show all the options in the panel, it's
            // overwhelming. Only when a flag is selected should we show all the options").
            Func<bool> selected = () => FlagEditor.HasSelection;
            int firstSelected = Rows.Count;

            // The selected flag's own buttons just under the list, where no folded group can hide
            // them (the groups below fold: the author, 2026-11-11).
            Add(new Row
            {
                Kind = RowKind.Button,
                Panel = PanelPage.Existing,
                Id = "GoToFlag",
                OnClick = FlagEditor.GoTo,
                Enabled = selected,
                Id2 = "DeleteFlag",
                OnClick2 = FlagEditor.ConfirmDelete,
                Enabled2 = selected,
            });

            // Another like it: its design into the New flag window, which opens to place it.
            Add(new Row
            {
                Kind = RowKind.Button,
                Panel = PanelPage.Existing,
                Id = "CopyToNew",
                OnClick = () =>
                {
                    FlagEditor.CopyToNew();
                    NewFlagPanel.Open();
                },
                Enabled = () => selected() && FlagPrefab.Info != null,
            });

            var pictures = new ImageTarget
            {
                Page = PanelPage.Existing,
                Image = Settings.SelImage,
                X = Settings.SelImageX,
                Y = Settings.SelImageY,
                Zoom = Settings.SelImageZoom,
                Turn = Settings.SelImageTurn,
                Alpha = Settings.SelImageAlpha,
                Country = Settings.SelCountry,
                Length = Settings.SelLength,
                Proportion = Settings.SelProportion,
                HoistedFrom = Settings.SelHoistedFrom,
                Colour1 = Settings.SelColour1,
                Changed = FlagEditor.ApplyDesign,
                Enabled = selected,
            };

            AddDesign(PanelPage.Existing, "SelShape", "SelWidth", "SelTall", "SelRecapture", Settings.SelCountry, Settings.SelImage, pictures, Settings.SelPattern,
                      Settings.SelColour1, Settings.SelColour2, Settings.SelColour3, Settings.SelLength, Settings.SelProportion,
                      Settings.SelHoistedFrom, Settings.SelMotion, Settings.SelWaviness, Settings.SelWindShare, Settings.SelDarkness,
                      Settings.SelWear, Settings.SelTear, Settings.SelTearEdges, Settings.SelDirt, Settings.SelHoles,
                      Settings.SelCut, Settings.SelCutDepth, Settings.SelHeld, FlagEditor.ApplyDesign, selected,
                      () => AddSelPlacement(selected));

            for (int i = firstSelected; i < Rows.Count; i++)
            {
                if (Rows[i].Panel != PanelPage.Existing)
                    continue;

                // A row with a condition of its own (Recapture: while the Motion is Loop) keeps it,
                // and needs a selection too.
                Func<bool> own = Rows[i].ShownWhile;
                Rows[i].ShownWhile = own == null ? selected : () => selected() && own();
            }
        }

        /// <summary>The Flags tab's Pole and placement group, the selected flag's: its Flagpole button first (it starts the group).</summary>
        private static void AddSelPlacement(Func<bool> selected)
        {
            // The selected flag's pole height (its foot stays, the flag goes with the pole's top) is
            // in the Flagpole popup; Page Up, Page Down and the gizmo move the whole flag.
            AddPoleButton(PanelPage.Existing, PanelPage.PoleExisting, "SelPoleButton", FlagsPanel.ComponentName, Settings.SelPole, Settings.SelHeight, Settings.SelRopes,
                          Settings.SelTilt, Settings.SelLean, Settings.SelHoistedFrom, selected);
            AddPole(PanelPage.PoleExisting, Settings.SelPole, Settings.SelHeight, Settings.SelRopes, Settings.SelPoleColour, Settings.SelTopColour, Settings.SelRopeColour,
                    Settings.SelPoleThickness, Settings.SelTilt, Settings.SelLean, Settings.SelHoistedFrom,
                    Settings.SelPoleProfile, Settings.SelPoleBase, Settings.SelPoleShine, Settings.SelRopeThickness, Settings.SelPolePosition,
                    Settings.SelPoleFinish, Settings.SelBlockShape, Settings.SelBlockWidth, Settings.SelBlockHeight, Settings.SelBlockColour,
                    Settings.SelBlockWear, FlagEditor.ApplyDesign, FlagEditor.ApplyPoleHeight, selected);

            AddPoleWear(PanelPage.PoleExisting, Settings.SelPole, Settings.SelHoistedFrom, Settings.SelPoleWear, Settings.SelPoleRust,
                        Settings.SelPoleDirt, FlagEditor.ApplyDesign, selected);

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = PanelPage.Existing,
                Float = Settings.SelRotation,
                DefaultFloat = Settings.Defaults.Rotation,
                Min = 0f, Max = 359f, Step = 1f,
                Format = Degrees,
                AfterChange = FlagEditor.ApplyRotation,
                Enabled = selected,
            });

            Add(new Row
            {
                Kind = RowKind.Toggle,
                Panel = PanelPage.Existing,
                Bool = Settings.SelBackPlate,
                DefaultBool = Settings.Defaults.BackPlate,
                AfterChange = FlagEditor.ApplyDesign,
                Enabled = () => selected() && Settings.SelHoistedFrom.value == FlagHoist.Top && FlagMotion.Moves(Settings.SelMotion.value),
                // A top banner's only (the author, 2026-11-11); the selection's own condition is added below.
                ShownWhile = () => Settings.SelHoistedFrom.value == FlagHoist.Top,
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
                // With the game's wind too (2026-11-10): it gives the strength, this the way it blows.
                AfterChange = () => Log.Msg("setting: wind direction = " + Settings.WindModes.LogName(Settings.WindMode.value)),
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = PanelPage.Settings,
                Float = Settings.WindDirection,
                DefaultFloat = Settings.Defaults.WindDirection,
                Min = 0f, Max = 359f, Step = 1f,
                Format = Degrees,
                Enabled = () => Settings.WindMode == null || Settings.WindMode.value == Settings.WindModes.Fixed,
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

            // Volumetric Weather's wind when it is on (2026-10-30): its weather is in it already.
            float vwSpeed;
            bool overridden;
            UnityEngine.Vector3 vwDirection;
            if (WeatherModWind.TryRead(out vwDirection, out vwSpeed, out overridden))
            {
                float vwDegrees = UnityEngine.Mathf.Repeat(UnityEngine.Mathf.Atan2(vwDirection.z, vwDirection.x) * UnityEngine.Mathf.Rad2Deg, 360f);
                // One line whether its weather or its Now tab sets it (the author, 2026-11-10: remove "set on its Now tab").
                return Localization.Get("GameWindNow.Weather", WindText(vwSpeed), vwDegrees.ToString("F0"));
            }

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

            // Where the panel's button goes: Unified UI, the HUD button ("Toolbar") or none (Real
            // Glass's and Volumetric Weather's row). Stored by value, append only.
            Add(new Row
            {
                Kind = RowKind.Choice,
                Options = OptionsPage.General,
                Int = Settings.IconPlace,
                DefaultInt = Settings.Defaults.IconPlace,
                ChoiceKeys = new[] { "IconPlace.UnifiedUI", "IconPlace.Toolbar", "IconPlace.Never" },
                ChoiceValues = new[] { Settings.IconPlaces.UnifiedUI, Settings.IconPlaces.Toolbar, Settings.IconPlaces.Never },
                AfterChange = ModController.RefreshButton,
            });

            // The panels' look (ModsLibCS1, Volumetric Weather's and Real Glass's row). Stored by
            // value, append only; listed with the default (Frosted glass) first and the old look
            // last. A change rebuilds the panel where it stands (its windows with it). The mod's
            // button keeps the game's round sprites in every look.
            Add(new Row
            {
                Kind = RowKind.Choice,
                Options = OptionsPage.General,
                Int = Settings.PanelSkin,
                DefaultInt = Settings.Defaults.PanelSkin,
                ChoiceKeys = new[] { "PanelSkin.Frosted", "PanelSkin.SeeThrough", "PanelSkin.GameBlue", "PanelSkin.Vanilla" },
                ChoiceValues = new[]
                {
                    (int)ModsLibCS1.UI.Skin.Frosted, (int)ModsLibCS1.UI.Skin.SeeThrough,
                    (int)ModsLibCS1.UI.Skin.GameBlue, (int)ModsLibCS1.UI.Skin.Vanilla,
                },
                AfterChange = () =>
                {
                    Log.Msg("setting: panel look = " + PanelLook.Skin);
                    ModController.RebuildPanel();
                    ModController.RefreshButton();
                },
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

            // Wet in the rain (the author, 2026-11-11: "in rain, the flags look too wet. Maybe we can
            // have a slider in the Settings tab, and by default it's around 10-20%"): the share of
            // the game's ground wetness the cloth takes (the shader's _RFWetScale).
            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = PanelPage.Settings,
                Options = OptionsPage.General,
                Float = Settings.RainWetness,
                DefaultFloat = Settings.Defaults.RainWetness,
                Min = 0f, Max = 100f, Step = 5f,
                AfterChange = () => Log.Msg("setting: flags wet in the rain = " + (Settings.RainWetness.value * 100f).ToString("F0", CultureInfo.InvariantCulture) + "%"),
            });

            // The overall Quality (2026-11-07; the author: "an overall Quality Preset"): it sets the
            // cloth detail, self-collision, shared motion and buildings as drawn below, and shows
            // Custom when they are set otherwise (QualityPreset).
            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = PanelPage.Settings,
                Options = OptionsPage.General,
                Group = "Cloth",
                Int = Settings.Quality,
                DefaultInt = QualityPreset.High,
                ChoiceValues = QualityPreset.Values,
                ChoiceKeys = QualityPreset.NameKeys,
                AfterChange = () => QualityPreset.Apply(Settings.Quality.value),
            });

            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = PanelPage.Settings,
                Options = OptionsPage.General,
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
                Panel = PanelPage.Settings,
                Options = OptionsPage.General,
                Bool = Settings.CityCollision,
                DefaultBool = Settings.Defaults.CityCollision,
                AfterChange = () =>
                {
                    Log.Msg("setting: the cloth keeps off the city = " + OnOff(Settings.CityCollision)());
                    FlagRenderer.ClothOptionsChanged();
                },
            });

            // Buildings as drawn (the author, 2026-11-07: "a lot of assets don't have proper
            // collision"): their meshes, not their LOD shapes. Every surface gathered again.
            Add(new Row
            {
                Kind = RowKind.Toggle,
                Panel = PanelPage.Settings,
                Options = OptionsPage.General,
                Bool = Settings.DetailedBuildings,
                DefaultBool = Settings.Defaults.DetailedBuildings,
                AfterChange = () =>
                {
                    Log.Msg("setting: the cloth keeps off buildings as drawn = " + OnOff(Settings.DetailedBuildings)());
                    Flags.FlagSurroundings.AllStale();
                    FlagTool.ForgetSnap();
                },
                Enabled = () => Settings.CityCollision == null || Settings.CityCollision.value,
            });

            // The cloth's threads (2026-10-31; the author: "give an option for More threads ... users
            // can choose willingly to utilize their hardware"): Auto as before, or a number up to the
            // machine's processors. Applied between batches (FlagRenderer.ApplyOptions).
            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = PanelPage.Settings,
                Options = OptionsPage.General,
                Int = Settings.ClothThreads,
                DefaultInt = Settings.Defaults.ClothThreads,
                ChoiceValues = ClothWorker.ThreadChoicesFor(Environment.ProcessorCount),
                ChoiceText = ThreadsText,
                AfterChange = () =>
                {
                    Log.Msg("setting: cloth threads = " + (Settings.ClothThreads.value <= 0 ? "Auto" : Settings.ClothThreads.value.ToString(CultureInfo.InvariantCulture)) +
                            " (" + ClothWorker.ThreadsFor(Environment.ProcessorCount, Settings.ClothThreads.value) + " of " + Environment.ProcessorCount + " processors)");
                    FlagRenderer.ClothOptionsChanged();
                },
            });

            // Shared motion (2026-10-31; the author: "add Shared motion as an option, it should be on
            // by default. Users with more powerful hardware can choose to change that").
            Add(new Row
            {
                Kind = RowKind.Toggle,
                Panel = PanelPage.Settings,
                Options = OptionsPage.General,
                Bool = Settings.SharedMotion,
                DefaultBool = Settings.Defaults.SharedMotion,
                AfterChange = () =>
                {
                    Log.Msg("setting: shared motion = " + OnOff(Settings.SharedMotion)());
                    FlagRenderer.ClothOptionsChanged();
                },
            });

            // Loops stand in (2026-11-11; the author: flags past the ones that move "just stop
            // animating until i pan my camera near them ... if we can substitute that with Loop
            // that would be so much better"): outside the Quality levels, as the threads are.
            Add(new Row
            {
                Kind = RowKind.Toggle,
                Panel = PanelPage.Settings,
                Options = OptionsPage.General,
                Bool = Settings.LoopStandIns,
                DefaultBool = Settings.Defaults.LoopStandIns,
                AfterChange = () =>
                {
                    Log.Msg("setting: loops stand in for flags with no room to wave = " + OnOff(Settings.LoopStandIns)());
                    FlagRenderer.ClothOptionsChanged();
                },
            });

            // Crowds placed as Loop (2026-11-12; the author: players "are lazy and won't bother
            // changing the motion"): past ClothPlan.CrowdMost waving flags near a new one, or a
            // Line Tool row that long, the new flag's Motion goes to Loop (FlagRenderer.Crowd.cs).
            Add(new Row
            {
                Kind = RowKind.Toggle,
                Panel = PanelPage.Settings,
                Options = OptionsPage.General,
                Bool = Settings.CrowdLoops,
                DefaultBool = Settings.Defaults.CrowdLoops,
                AfterChange = State("crowds of new waving flags placed as Loop", OnOff(Settings.CrowdLoops)),
            });

            // Whether the one-off message about it has been shown: kept in the file, no row.
            Add(new Row
            {
                Kind = RowKind.Toggle,
                Bool = Settings.CrowdLoopsTold,
                DefaultBool = Settings.Defaults.CrowdLoopsTold,
            });

            // Gusts and eddies of several sizes in a waving flag's wind (2026-11-14): kept in the
            // file, no row; the renderer reads it again when the file changes, and says so.
            Add(new Row
            {
                Kind = RowKind.Toggle,
                Bool = Settings.VariedWind,
                DefaultBool = Settings.Defaults.VariedWind,
                AfterChange = FlagRenderer.ClothOptionsChanged,
            });

            // The panels' folded groups (RowSet's folding headings): kept in the file, no row.
            Add(new Row
            {
                Kind = RowKind.Value,
                Int = Settings.PanelFolded,
                DefaultInt = Settings.Defaults.PanelFolded,
                Min = 0f, Max = (1 << Settings.FoldGroups.Length) - 1, Step = 1f,
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
