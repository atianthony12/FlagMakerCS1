using System.Collections.Generic;
using ColossalFramework.UI;
using ModsLibCS1.UI;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// A panel's rows, drawn from the catalog (SettingsCatalog), and kept up to date: the in-game
    /// panel's two tabs, the New flag window, and the Flagpole popup's two pages each hold one. A
    /// colour row is a swatch that opens the ColourPicker, a country row a button that opens the
    /// CountryPicker, an image row a button that opens the ImagePanel and a folder button, a
    /// pattern row a button that opens the PatternPanel, all beside the owner; up to three colour
    /// rows share a line; a shape row is a little flag of each shape, a segmented row a button a
    /// choice, the one that holds lit.
    /// </summary>
    /// <remarks>
    /// What FlagsPanel did itself before the popup needed the same (Volumetric Explosions'
    /// builders, grown for flags): build a page's rows in catalog order, refresh every control
    /// from its setting (a write only where something changed, never back into the setting),
    /// grey out the rows that do not apply. The sliders, switches, choices, headings and status
    /// lines are ModsLibCS1's rows in the panel's look (<see cref="PanelLook"/>); this mod's own
    /// kinds of row (colours, country, image, shape, segmented, the flag list) are built of the
    /// library's names and buttons, their parts lined up on its names' column.
    /// </remarks>
    public sealed class RowSet
    {
        private readonly UIComponent _owner;
        private readonly Builder _ui;
        private readonly List<Control> _controls = new List<Control>();
        private readonly List<ColourLine> _lines = new List<ColourLine>();

        /// <summary>
        /// The reset icon's column at the right end of each row with a setting, gap included (Real
        /// Glass's and Volumetric Weather's): every row's parts end before it.
        /// </summary>
        public const float ResetColumn = 28f;

        /// <summary>
        /// Guards the refresh against its own callbacks: a row set from its setting must never write
        /// the setting straight back. (The library's SetValue / SetIndex are quiet as well.)
        /// </summary>
        private bool _refreshing;

        /// <summary>The flag list, if one of the rows was it.</summary>
        public FlagListView List { get; private set; }

        public RowSet(UIComponent owner, Builder ui)
        {
            _owner = owner;
            _ui = ui;
        }

        public int Count
        {
            get { return _controls.Count; }
        }

        /// <summary>Where a row's control starts: right of the names' column (200 in Vanilla, as it was).</summary>
        private float Column
        {
            get { return _ui.LabelWidth + _ui.Theme.Gap; }
        }

        /// <summary>Where a row's parts end: before the reset icon's column.</summary>
        private static float Right(UIComponent page)
        {
            return page.width - ResetColumn;
        }

        /// <summary>A line of colour swatches (up to three rows) and its one reset icon, for the swatches off their default.</summary>
        private class ColourLine
        {
            public readonly List<Row> Rows = new List<Row>();
            public UIButton Reset;
        }

        /// <summary>One built row: whatever components it made, so they can be refreshed and greyed.</summary>
        private class Control
        {
            public Row Source;
            public SliderRow Slider;
            public ChoiceRow Choice;
            public ToggleRow Toggle;
            public UILabel Status;
            public UIButton Swatch;
            public UIButton Button;
            public UIButton Second;

            /// <summary>The row's reset icon (null: none, or the library's sheet missing).</summary>
            public UIButton Reset;

            /// <summary>A country row: its button (the country's name) and the flag's image beside it, and the number they show.</summary>
            public UIButton Country;
            public UITextureSprite CountryImage;
            public int CountryShown = -1;

            /// <summary>An image row: its button (the picture's name), its thumbnail, and what they show -- the picture's number, the textures' and the folder's versions then.</summary>
            public UIButton Image;
            public UITextureSprite ImageThumb;
            public int ImageShown = -1, ImageVersion = -1, ImageFolderVersion = -1;

            /// <summary>A pattern row: its button (the pattern's name) and its little picture.</summary>
            public UIButton Pattern;
            public PatternThumb PatternImage;

            /// <summary>A shape or segmented row: a button per preset or choice, the little flag drawn on each, and which is lit (1), not (0), or not yet shown (-1).</summary>
            public readonly List<UIButton> Options = new List<UIButton>();
            public readonly List<FlagIcon> Icons = new List<FlagIcon>();
            public int IconsHoist = -1;
            public readonly List<int> Lit = new List<int>();

            public readonly List<UIComponent> Parts = new List<UIComponent>();
            public readonly List<UIComponent> SecondParts = new List<UIComponent>();
            public bool Enabled = true;
            public bool Enabled2 = true;
        }

        /// <summary>Draws every catalog row that belongs on this page, in catalog order, from <paramref name="y"/>; returns the height used.</summary>
        public float BuildRows(UIPanel page, PanelPage which, float y)
        {
            // The library's sliders, switches and choices end before the reset icon too; the
            // builder may build more after these rows (the Pattern window's), as it was.
            float resetColumn = _ui.ResetColumn;
            _ui.ResetColumn = ResetColumn;
            try
            {
                return BuildPage(page, which, y);
            }
            finally
            {
                _ui.ResetColumn = resetColumn;
            }
        }

        private float BuildPage(UIPanel page, PanelPage which, float y)
        {
            ColourLine coloursLine = null;
            int coloursInLine = 0;
            float rowHeight = _ui.RowHeight;

            foreach (Row row in SettingsCatalog.Rows)
            {
                if (row.Panel != which || (row.Shown != null && !row.Shown()))
                    continue;

                if (row.Group != null)
                {
                    _ui.AddHeading(page, row.GroupTitle, y + 4f);
                    y += _ui.Theme.HeadingHeight;
                }

                // A design's colours share one line: "Colours [1] [2] [3]", one reset icon for them.
                if (row.Kind == RowKind.Colour)
                {
                    if (coloursLine == null || coloursInLine >= 3)
                    {
                        coloursLine = new ColourLine();
                        coloursInLine = 0;
                        _ui.AddName(page, Localization.Get("Colours.Label"), new Vector3(0f, y + 8f), _ui.LabelWidth);
                        coloursLine.Reset = _ui.AddReset(page, y, row.ResetHint);
                        if (coloursLine.Reset != null)
                        {
                            ColourLine captured = coloursLine;
                            coloursLine.Reset.eventClick += (component, e) => SettingsCatalog.ResetRows(captured.Rows);
                        }

                        _lines.Add(coloursLine);
                        y += rowHeight;
                    }

                    coloursLine.Rows.Add(row);
                    Control swatch = BuildSwatch(page, row, y - rowHeight, coloursInLine);
                    _controls.Add(swatch);
                    coloursInLine++;
                    continue;
                }

                coloursLine = null;

                Control control = Build(page, row, y);
                AddReset(page, row, y, control);
                _controls.Add(control);

                switch (row.Kind)
                {
                    case RowKind.Button:
                        y += rowHeight + 6f;
                        break;
                    case RowKind.FlagList:
                        y += FlagListView.RowHeight * FlagListView.VisibleRows + 12f;
                        break;
                    case RowKind.Shape:
                    case RowKind.Segmented:
                        y += rowHeight + 4f;
                        break;
                    default:
                        y += rowHeight;
                        break;
                }
            }

            return y;
        }

        /// <summary>The status lines' texts, again.</summary>
        public void RefreshStatus()
        {
            foreach (Control control in _controls)
            {
                if (control.Status != null && control.Source.StatusText != null)
                    control.Status.text = control.Source.StatusText();
            }
        }

        /// <summary>Re-reads every control from its setting; a write only where something changed.</summary>
        public void RefreshValues()
        {
            _refreshing = true;
            try
            {
                foreach (Control control in _controls)
                {
                    Row row = control.Source;

                    // Set quietly: the row's own callback never runs for a refresh. Again when only
                    // the readout differs: an override (a country's proportions) changes with another row.
                    if (control.Slider != null)
                    {
                        // A top that moved (the pole height's range unlocked or locked): a lower
                        // one pulls the slider in with it, quietly too -- the setting keeps its value.
                        float top = row.Top;
                        if (control.Slider.Slider.maxValue != top)
                            control.Slider.Slider.maxValue = top;

                        float display = Mathf.Clamp(row.Display, row.Min, top);
                        if (!Mathf.Approximately(control.Slider.Slider.value, display) ||
                            control.Slider.Value.text != ReadoutText(row, display))
                            control.Slider.SetValue(display);
                    }

                    if (control.Choice != null && control.Choice.Index != row.ChoiceIndex)
                        control.Choice.SetIndex(row.ChoiceIndex);

                    // Again when its thumbnail went with the last city (FlagImages.Clear).
                    if (control.Country != null && row.Int != null &&
                        (control.CountryShown != row.Int.value || (row.Int.value != 0 && control.CountryImage.texture == null)))
                        ShowCountry(control);

                    // A picture: again when it changed, its file did, or its folder.
                    if (control.Image != null && row.Int != null &&
                        (control.ImageShown != row.Int.value || control.ImageVersion != FlagImages.UserVersion ||
                         control.ImageFolderVersion != FlagImageFolder.Version || (row.Int.value != 0 && control.ImageThumb.texture == null)))
                        ShowImage(control);

                    if (control.Pattern != null)
                        ShowPattern(control);

                    // Not while its confirmation is open: the value is still the old one.
                    if (control.Toggle != null && row.Bool != null && !row.Pending && control.Toggle.Check.isChecked != row.Bool.value)
                        control.Toggle.SetValue(row.Bool.value);

                    if (control.Swatch != null && row.Int != null)
                    {
                        FlagWidgets.SetSwatch(control.Swatch, (uint)row.Int.value);

                        // Its code on it, to read off or type in elsewhere.
                        string tip = row.Label + " " + FlagColours.Hex((uint)row.Int.value) + ": " + row.Tooltip;
                        if (control.Swatch.tooltip != tip)
                            control.Swatch.tooltip = tip;
                    }

                    if (control.Button != null && row.ButtonText != null)
                    {
                        string text = _ui.Caps(row.ButtonText());
                        if (control.Button.text != text)
                            control.Button.text = text;
                    }

                    if (control.Options.Count > 0)
                        RefreshOptions(control);

                    if (control.Reset != null)
                        Builder.SetResetEnabled(control.Reset, ResetWanted(row));
                }

                foreach (ColourLine line in _lines)
                {
                    if (line.Reset == null)
                        continue;

                    bool wanted = false;
                    foreach (Row row in line.Rows)
                        wanted |= ResetWanted(row);
                    Builder.SetResetEnabled(line.Reset, wanted);
                }
            }
            finally
            {
                _refreshing = false;
            }

            RefreshEnabled();
        }

        /// <summary>A row's reset icon is lit while the row is off its default and not greyed out (Real Glass's).</summary>
        private static bool ResetWanted(Row row)
        {
            return row.CanReset && row.IsEnabled && !row.IsAtDefault;
        }

        /// <summary>
        /// The row's reset icon at its right end: every row with a setting or a way back, not a
        /// button, a status line or the list. A click puts that row back (SettingsCatalog.ResetRows).
        /// </summary>
        private void AddReset(UIPanel page, Row row, float y, Control control)
        {
            if (!row.CanReset || row.Kind == RowKind.Button || row.Kind == RowKind.Status || row.Kind == RowKind.FlagList)
                return;

            control.Reset = _ui.AddReset(page, y, row.ResetHint);
            if (control.Reset == null)
                return;

            Row captured = row;
            control.Reset.eventClick += (component, e) => SettingsCatalog.ResetRows(new[] { captured });
            Builder.SetResetEnabled(control.Reset, ResetWanted(row));
        }

        /// <summary>A shape or segmented row: the button of the shape the flag has (or the choice made) lit, the little flags hoisted as it is.</summary>
        private void RefreshOptions(Control control)
        {
            Row row = control.Source;

            if (row.Kind == RowKind.Shape && row.PresetHoist != null)
            {
                int hoist = row.PresetHoist();
                if (hoist != control.IconsHoist)
                {
                    control.IconsHoist = hoist;
                    for (int i = 0; i < control.Icons.Count && i < row.Presets.Length; i++)
                        control.Icons[i].Show(FlagSize.Ratio(row.Presets[i]), hoist);
                }
            }

            for (int i = 0; i < control.Options.Count; i++)
            {
                bool lit = row.Kind == RowKind.Shape
                    ? row.PresetActive != null && i < row.Presets.Length && row.PresetActive(row.Presets[i])
                    : row.Int != null && row.ChoiceValues != null && i < row.ChoiceValues.Length && row.Int.value == row.ChoiceValues[i];

                int state = lit ? 1 : 0;
                if (control.Lit[i] == state)
                    continue;

                control.Lit[i] = state;
                PanelLook.SetLit(_ui, control.Options[i], lit);
            }
        }

        private static string ReadoutText(Row row, float display)
        {
            string over = row.ReadoutOverride != null ? row.ReadoutOverride() : null;
            return over ?? row.FormatDisplay(display);
        }

        private void RefreshEnabled()
        {
            foreach (Control control in _controls)
            {
                bool wanted = control.Source.IsEnabled;
                if (wanted != control.Enabled)
                {
                    control.Enabled = wanted;
                    foreach (UIComponent part in control.Parts)
                    {
                        part.isEnabled = wanted;
                        part.opacity = wanted ? 1f : 0.45f;
                    }
                }

                if (control.Second == null)
                    continue;

                bool wanted2 = control.Source.IsEnabled2;
                if (wanted2 == control.Enabled2)
                    continue;

                control.Enabled2 = wanted2;
                foreach (UIComponent part in control.SecondParts)
                {
                    part.isEnabled = wanted2;
                    part.opacity = wanted2 ? 1f : 0.45f;
                }
            }
        }

        /// <summary>One of a design's colours: a swatch that opens the picker.</summary>
        private Control BuildSwatch(UIPanel page, Row row, float y, int index)
        {
            var control = new Control { Source = row };
            const float gap = 8f;

            // 70 px as before where it fits; narrower in the Pattern window, before the reset icon.
            float width = Mathf.Min(70f, Mathf.Floor((Right(page) - Column - 2f * gap) / 3f));
            UIButton swatch = FlagWidgets.AddSwatch(page, new Vector2(width, 22f), new Vector3(Column + index * (width + gap), y + 6f),
                                                    row.Int != null ? (uint)row.Int.value : 0xFFFFFFu);
            swatch.tooltip = row.Label + ": " + row.Tooltip;
            Row captured = row;
            swatch.eventClick += (component, e) =>
            {
                if (captured.IsEnabled)
                    ColourPicker.Toggle(captured, _owner);
            };

            control.Swatch = swatch;
            control.Parts.Add(swatch);
            return control;
        }

        private Control Build(UIPanel page, Row row, float y)
        {
            var control = new Control { Source = row };
            float column = Column;

            switch (row.Kind)
            {
                case RowKind.Status:
                    control.Status = _ui.AddStatus(page, y, row.StatusText != null ? row.StatusText() : string.Empty);
                    break;

                case RowKind.FlagList:
                    List = page.AddUIComponent<FlagListView>();
                    List.relativePosition = new Vector3(0f, y + 4f);
                    List.Build(page.width, _ui);
                    break;

                case RowKind.Button:
                {
                    bool pair = row.Id2 != null;
                    float width = pair ? (page.width - 8f) * 0.5f : page.width;

                    UIButton button = _ui.AddButton(page, row.ButtonText != null ? row.ButtonText() : row.Label, new Vector2(width, 32f),
                                                    new Vector3(0f, y + 4f));
                    button.textScale = 0.9f;
                    button.tooltip = row.Tooltip;
                    Row captured = row;
                    button.eventClick += (component, e) =>
                    {
                        if (captured.OnClick == null || !captured.IsEnabled)
                            return;

                        captured.OnClick();
                        SettingsCatalog.RefreshAllUIs();
                    };
                    control.Button = button;
                    control.Parts.Add(button);

                    if (pair)
                    {
                        UIButton second = _ui.AddButton(page, row.Label2, new Vector2(width, 32f), new Vector3(width + 8f, y + 4f));
                        second.textScale = 0.9f;
                        second.tooltip = row.Tooltip2;
                        second.eventClick += (component, e) =>
                        {
                            if (captured.OnClick2 == null || !captured.IsEnabled2)
                                return;

                            captured.OnClick2();
                            SettingsCatalog.RefreshAllUIs();
                        };
                        control.Second = second;
                        control.SecondParts.Add(second);
                    }

                    break;
                }

                case RowKind.Country:
                {
                    // "Country's flag   [ France          ] [flag]": a button that opens the picker.
                    AddRowLabel(page, row, y, control);

                    const float imageWidth = 42f;
                    float width = Right(page) - column - imageWidth - 8f;
                    UIButton button = NameButton(page, width, new Vector3(column, y + 3f));
                    button.tooltip = row.Tooltip;
                    Row captured = row;
                    button.eventClick += (component, e) =>
                    {
                        if (captured.IsEnabled)
                            CountryPicker.Toggle(captured, _owner);
                    };

                    UITextureSprite image = page.AddUIComponent<UITextureSprite>();
                    image.size = new Vector2(imageWidth, 26f);
                    image.relativePosition = new Vector3(column + width + 8f, y + 3f);
                    image.isInteractive = false;

                    control.Country = button;
                    control.CountryImage = image;
                    control.Parts.Add(button);
                    control.Parts.Add(image);
                    ShowCountry(control);
                    break;
                }

                case RowKind.Image:
                {
                    // "Your image   [ castle.png   ] [picture] [folder]": the button opens the Image
                    // window (the list of the folder's pictures, and where one lies on the flag), the
                    // folder button the folder itself -- whatever is selected.
                    AddRowLabel(page, row, y, control);

                    const float imageWidth = 42f, folderWidth = 30f, gap = 6f;
                    float width = Right(page) - column - imageWidth - folderWidth - 2f * gap;
                    UIButton button = NameButton(page, width, new Vector3(column, y + 3f));
                    button.tooltip = row.Tooltip;
                    Row captured = row;
                    button.eventClick += (component, e) =>
                    {
                        if (captured.IsEnabled && captured.Image != null)
                            ImagePanel.Toggle(captured.Image, _owner);
                    };

                    UITextureSprite image = page.AddUIComponent<UITextureSprite>();
                    image.size = new Vector2(imageWidth, 26f);
                    image.relativePosition = new Vector3(column + width + gap, y + 3f);
                    image.isInteractive = false;

                    UIButton folder = _ui.AddButton(page, string.Empty, new Vector2(folderWidth, 26f), new Vector3(Right(page) - folderWidth, y + 3f));
                    folder.tooltip = Localization.Get("Image.OpenTip");
                    new FolderIcon(folder, new Vector3(6f, 6f), new Vector2(18f, 14f));
                    folder.eventClick += (component, e) => FlagImageFolder.Open();

                    control.Image = button;
                    control.ImageThumb = image;
                    control.Parts.Add(button);
                    control.Parts.Add(image);
                    ShowImage(control);
                    break;
                }

                case RowKind.Pattern:
                {
                    // "Pattern   [ Nordic cross    ] [flag]": a button that opens the Pattern window.
                    AddRowLabel(page, row, y, control);

                    const float imageWidth = 42f;
                    float width = Right(page) - column - imageWidth - 8f;
                    UIButton button = NameButton(page, width, new Vector3(column, y + 3f));
                    button.tooltip = row.Tooltip;
                    Row captured = row;
                    button.eventClick += (component, e) =>
                    {
                        if (captured.IsEnabled && captured.Patterns != null)
                            PatternPanel.Toggle(captured.Patterns, _owner);
                    };

                    PatternThumb image = page.AddUIComponent<PatternThumb>();
                    image.relativePosition = new Vector3(column + width + 8f, y + 3f);
                    image.isInteractive = false;

                    control.Pattern = button;
                    control.PatternImage = image;
                    control.Parts.Add(button);
                    control.Parts.Add(image);
                    ShowPattern(control);
                    break;
                }

                case RowKind.Shape:
                {
                    // "Shape   [▯] [▯] [▯] [□] [▭] [▭] [▭]": a little flag of each shape, hoisted as the flag is.
                    AddRowLabel(page, row, y, control);
                    int count = row.Presets != null ? row.Presets.Length : 0;
                    const float gap = 4f, height = 28f;
                    float width = count > 0 ? Mathf.Floor((Right(page) - column - gap * (count - 1)) / count) : 0f;
                    Row captured = row;
                    for (int i = 0; i < count; i++)
                    {
                        int preset = row.Presets[i];
                        UIButton button = _ui.AddButton(page, string.Empty, new Vector2(width, height), new Vector3(column + i * (width + gap), y + 3f));
                        button.tooltip = FlagProportion.Name(preset);
                        button.eventClick += (component, e) =>
                        {
                            if (!captured.IsEnabled || captured.OnPreset == null)
                                return;

                            captured.OnPreset(preset);
                            Changed(captured);
                        };

                        var icon = new FlagIcon(button, new Vector3(0.5f * (width - 22f), 5f), new Vector2(22f, 18f));
                        icon.Show(FlagSize.Ratio(preset), row.PresetHoist != null ? row.PresetHoist() : FlagHoist.Side);
                        control.Options.Add(button);
                        control.Icons.Add(icon);
                        control.Lit.Add(-1);
                        control.Parts.Add(button);
                    }

                    break;
                }

                case RowKind.Segmented:
                {
                    // "Hoisted from   [|▭ Side] [▔▭ Top]": a button a choice, the chosen one lit.
                    AddRowLabel(page, row, y, control);
                    int count = row.ChoiceValues != null ? row.ChoiceValues.Length : 0;
                    const float gap = 6f, height = 28f;
                    float width = count > 0 ? Mathf.Floor((Right(page) - column - gap * (count - 1)) / count) : 0f;
                    Row captured = row;
                    for (int i = 0; i < count; i++)
                    {
                        int value = row.ChoiceValues[i];
                        bool drawn = row.ChoiceIcons != null && FlagWidgets.HasIcon(row.ChoiceIcons[i]);
                        UIButton button = _ui.AddButton(page, drawn ? string.Empty : row.ChoiceName(i), new Vector2(width, height), new Vector3(column + i * (width + gap), y + 3f));
                        button.tooltip = drawn ? row.ChoiceName(i) : row.Tooltip;
                        if (drawn)
                            FlagWidgets.AddIcon(button, row.ChoiceIcons[i], new Vector3(Mathf.Round(0.5f * (width - 20f)), 4f), 20f);

                        button.eventClick += (component, e) =>
                        {
                            if (!captured.IsEnabled || captured.Int == null || captured.Int.value == value)
                                return;

                            captured.Int.value = value;
                            Changed(captured);
                        };

                        if (row.HoistIcons)
                        {
                            // A little 2:3 flag hoisted the button's way, left of its word.
                            button.textHorizontalAlignment = UIHorizontalAlignment.Left;
                            button.textPadding = new RectOffset(38, 6, 6, 0);
                            var icon = new FlagIcon(button, new Vector3(10f, 5f), new Vector2(20f, 18f));
                            icon.Show(value == FlagHoist.Top ? 1.5f : 0.667f, value);
                            control.Icons.Add(icon);
                        }

                        control.Options.Add(button);
                        control.Lit.Add(-1);
                        control.Parts.Add(button);
                    }

                    break;
                }

                case RowKind.Toggle:
                {
                    Row captured = row;
                    ToggleRow toggle = _ui.AddToggle(page, y, row.Label, row.Bool != null && row.Bool.value, isChecked =>
                    {
                        if (_refreshing)
                            return;

                        SettingsCatalog.ApplyToggle(captured, isChecked);
                    });
                    toggle.Check.tooltip = row.Tooltip;
                    control.Toggle = toggle;
                    control.Parts.AddRange(toggle.Parts);
                    break;
                }

                case RowKind.Choice:
                {
                    // Vanilla: a slider over the choices, the choice's name as its value. The new
                    // looks: a row of buttons, or a dropdown when the names don't fit (measured, so
                    // it follows the language). Either way the row hands back an index, and what is
                    // stored is that choice's VALUE (choice rows are stored by value).
                    Row captured = row;
                    ChoiceRow choice = _ui.AddChoice(page, y, row.Label, row.Choices, row.ChoiceIndex, index =>
                    {
                        if (_refreshing || captured.Int == null || captured.ChoiceValues == null)
                            return;

                        index = Mathf.Clamp(index, 0, captured.ChoiceValues.Length - 1);
                        if (captured.Int.value == captured.ChoiceValues[index])
                            return;

                        captured.Int.value = captured.ChoiceValues[index];
                        Changed(captured);
                    });
                    Tip(choice, row.Tooltip);
                    control.Choice = choice;
                    control.Parts.AddRange(choice.Parts);
                    break;
                }

                default:
                {
                    Row captured = row;
                    SliderRow slider = _ui.AddSlider(page, y, row.Label, row.Min, row.Top, row.Step,
                        Mathf.Clamp(row.Display, row.Min, row.Top), v => ReadoutText(captured, v),
                        v =>
                        {
                            if (_refreshing)
                                return;

                            captured.Store(v);
                            Changed(captured);
                        });
                    Tip(slider, row.Tooltip);
                    control.Slider = slider;
                    control.Parts.AddRange(slider.Parts);
                    break;
                }
            }

            return control;
        }

        /// <summary>A button that shows a name (a country's, a picture's) from its left, in the row's text size.</summary>
        private UIButton NameButton(UIPanel page, float width, Vector3 position)
        {
            UIButton button = _ui.AddButton(page, string.Empty, new Vector2(width, 26f), position);
            button.textScale = 0.8f;
            button.textHorizontalAlignment = UIHorizontalAlignment.Left;
            button.textPadding = new RectOffset(8, 8, 5, 0);
            return button;
        }

        /// <summary>A row's tooltip on every part of it: whichever the pointer is over says it.</summary>
        private static void Tip(BuiltRow row, string tooltip)
        {
            foreach (UIComponent part in row.Parts)
                part.tooltip = tooltip;
        }

        /// <summary>A country row as its setting stands: the name on the button, the flag beside it (its thumbnail, at the flag's proportions), or "none".</summary>
        private static void ShowCountry(Control control)
        {
            Row row = control.Source;
            int number = row.Int != null ? row.Int.value : 0;
            control.CountryShown = number;

            FlagCountry country = FlagCountries.At(number);
            control.Country.text = country != null ? CountryNames.Of(country) : Localization.Get("Country.None");

            Texture2D thumb = country != null ? FlagImages.Thumbnail(country.Image) : null;
            control.CountryImage.texture = thumb;
            control.CountryImage.isVisible = thumb != null;
            if (thumb != null)
            {
                // Within the 42 x 26 kept for it, at the flag's own proportions.
                float scale = Mathf.Min(42f / Mathf.Max(1, thumb.width), 26f / Mathf.Max(1, thumb.height));
                control.CountryImage.size = new Vector2(Mathf.Round(thumb.width * scale), Mathf.Round(thumb.height * scale));
            }
        }

        /// <summary>An image row as its setting stands: the picture's name on the button (or none, or that its file is missing) and its thumbnail beside it, at its own proportions.</summary>
        private static void ShowImage(Control control)
        {
            Row row = control.Source;
            int number = row.Int != null ? row.Int.value : 0;
            control.ImageShown = number;
            control.ImageVersion = FlagImages.UserVersion;
            control.ImageFolderVersion = FlagImageFolder.Version;

            string name = FlagImageNames.At(number);
            Texture2D thumb = name.Length > 0 ? FlagImages.UserPreview(name) : null;
            control.Image.text = name.Length == 0 ? Localization.Get("Image.None") : thumb == null ? Localization.Get("Image.Missing", name) : name;

            control.ImageThumb.texture = thumb;
            control.ImageThumb.isVisible = thumb != null;
            if (thumb != null)
            {
                float scale = Mathf.Min(42f / Mathf.Max(1, thumb.width), 26f / Mathf.Max(1, thumb.height));
                control.ImageThumb.size = new Vector2(Mathf.Max(1f, Mathf.Round(thumb.width * scale)), Mathf.Max(1f, Mathf.Round(thumb.height * scale)));
            }
        }

        /// <summary>A pattern row as its settings stand: the pattern's name on the button, the flag beside it at its own shape, in its colours.</summary>
        private static void ShowPattern(Control control)
        {
            Row row = control.Source;
            PatternTarget target = row.Patterns;
            string name = row.ChoiceName(row.ChoiceIndex);
            if (control.Pattern.text != name)
                control.Pattern.text = name;

            // Within the 42 x 26 kept for it.
            float seen = Mathf.Max(target.Seen, 0.01f);
            float w = Mathf.Min(42f, 26f / seen);
            control.PatternImage.size = new Vector2(Mathf.Max(1f, Mathf.Round(w)), Mathf.Max(1f, Mathf.Round(w * seen)));
            control.PatternImage.Show(target.Pattern.value, target.Proportion.value, target.HoistedFromTop, target.C1, target.C2, target.C3);
        }

        /// <summary>A row's name on the left, as a slider row has it (greyed with the row).</summary>
        private void AddRowLabel(UIPanel page, Row row, float y, Control control)
        {
            UILabel label = _ui.AddName(page, row.Label, new Vector3(0f, y + 8f), _ui.LabelWidth);
            label.tooltip = row.Tooltip;
            control.Parts.Add(label);
        }

        /// <summary>After a control stored its value: rows with a live-apply hook run it.</summary>
        private static void Changed(Row row)
        {
            if (row.AfterChange == null)
                return;

            row.AfterChange();
            SettingsCatalog.RefreshAllUIs();
        }
    }
}
