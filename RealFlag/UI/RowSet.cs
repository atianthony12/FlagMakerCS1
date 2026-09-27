using System.Collections.Generic;
using ColossalFramework.UI;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// A panel's rows, drawn from the catalog (SettingsCatalog), and kept up to date: the in-game
    /// panel's two tabs, the New flag window, and the Flagpole popup's two pages each hold one. A
    /// colour row is a swatch that opens the ColourPicker, a country row a button that opens the
    /// CountryPicker, an image row a button that opens the ImagePanel and a folder button, all
    /// beside the owner; up to three colour rows share a line; a shape row is a little flag of
    /// each shape, a segmented row a button a choice, the one that holds lit.
    /// </summary>
    /// <remarks>
    /// What FlagsPanel did itself before the popup needed the same (Volumetric Explosions'
    /// builders, grown for flags): build a page's rows in catalog order, refresh every control
    /// from its setting (a write only where something changed, never back into the setting --
    /// writing a slider's value fires its change event), grey out the rows that do not apply.
    /// </remarks>
    public sealed class RowSet
    {
        private readonly UIComponent _owner;
        private readonly List<Control> _controls = new List<Control>();

        /// <summary>Guards the refresh against its own callbacks (writing a slider's value fires eventValueChanged).</summary>
        private bool _refreshing;

        /// <summary>The flag list, if one of the rows was it.</summary>
        public FlagListView List { get; private set; }

        public RowSet(UIComponent owner)
        {
            _owner = owner;
        }

        public int Count
        {
            get { return _controls.Count; }
        }

        /// <summary>One built row: whatever components it made, so they can be refreshed and greyed.</summary>
        private class Control
        {
            public Row Source;
            public UISlider Slider;
            public UICheckBox Check;
            public UILabel Readout;
            public UILabel Status;
            public UIButton Swatch;
            public UIButton Button;
            public UIButton Second;

            /// <summary>A country row: its button (the country's name) and the flag's image beside it, and the number they show.</summary>
            public UIButton Country;
            public UITextureSprite CountryImage;
            public int CountryShown = -1;

            /// <summary>An image row: its button (the picture's name), its thumbnail, and what they show -- the picture's number, the textures' and the folder's versions then.</summary>
            public UIButton Image;
            public UITextureSprite ImageThumb;
            public int ImageShown = -1, ImageVersion = -1, ImageFolderVersion = -1;

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
            UIPanel coloursLine = null;
            int coloursInLine = 0;

            foreach (Row row in SettingsCatalog.Rows)
            {
                if (row.Panel != which)
                    continue;

                if (row.Group != null)
                {
                    UIBuilder.AddHeading(page, row.GroupTitle, y + 4f);
                    y += FlagsPanel.HeadingHeight;
                }

                // A design's colours share one line: "Colours [1] [2] [3]".
                if (row.Kind == RowKind.Colour)
                {
                    if (coloursLine == null || coloursInLine >= 3)
                    {
                        coloursLine = page;
                        coloursInLine = 0;
                        UILabel label = UIBuilder.AddLabel(page, Localization.Get("Colours.Label"), new Vector3(0f, y + 8f), 0.8f);
                        label.autoSize = false;
                        label.size = new Vector2(196f, 20f);
                        y += UIBuilder.RowHeight;
                    }

                    Control swatch = BuildSwatch(page, row, y - UIBuilder.RowHeight, coloursInLine);
                    _controls.Add(swatch);
                    coloursInLine++;
                    continue;
                }

                coloursLine = null;

                Control control = Build(page, row, y);
                _controls.Add(control);

                switch (row.Kind)
                {
                    case RowKind.Button:
                        y += UIBuilder.RowHeight + 6f;
                        break;
                    case RowKind.FlagList:
                        y += FlagListView.RowHeight * FlagListView.VisibleRows + 12f;
                        break;
                    case RowKind.Shape:
                    case RowKind.Segmented:
                        y += UIBuilder.RowHeight + 4f;
                        break;
                    default:
                        y += UIBuilder.RowHeight;
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

                    if (control.Slider != null)
                    {
                        float display = row.Kind == RowKind.Choice ? row.ChoiceIndex : Mathf.Clamp(row.Display, row.Min, row.Max);
                        if (!Mathf.Approximately(control.Slider.value, display))
                            control.Slider.value = display;

                        // Every time: an override (a country's proportions) changes with another row.
                        if (control.Readout != null)
                        {
                            string text = ReadoutText(row, display);
                            if (control.Readout.text != text)
                                control.Readout.text = text;
                        }
                    }

                    // Again when its thumbnail went with the last city (FlagImages.Clear).
                    if (control.Country != null && row.Int != null &&
                        (control.CountryShown != row.Int.value || (row.Int.value != 0 && control.CountryImage.texture == null)))
                        ShowCountry(control);

                    // A picture: again when it changed, its file did, or its folder.
                    if (control.Image != null && row.Int != null &&
                        (control.ImageShown != row.Int.value || control.ImageVersion != FlagImages.UserVersion ||
                         control.ImageFolderVersion != FlagImageFolder.Version || (row.Int.value != 0 && control.ImageThumb.texture == null)))
                        ShowImage(control);

                    if (control.Check != null && row.Bool != null && !row.Pending && control.Check.isChecked != row.Bool.value)
                        control.Check.isChecked = row.Bool.value;

                    if (control.Swatch != null && row.Int != null)
                    {
                        UIBuilder.SetSwatch(control.Swatch, (uint)row.Int.value);

                        // Its code on it, to read off or type in elsewhere.
                        string tip = row.Label + " " + FlagColours.Hex((uint)row.Int.value) + ": " + row.Tooltip;
                        if (control.Swatch.tooltip != tip)
                            control.Swatch.tooltip = tip;
                    }

                    if (control.Button != null && row.ButtonText != null)
                    {
                        string text = row.ButtonText();
                        if (control.Button.text != text)
                            control.Button.text = text;
                    }

                    if (control.Options.Count > 0)
                        RefreshOptions(control);
                }
            }
            finally
            {
                _refreshing = false;
            }

            RefreshEnabled();
        }

        /// <summary>A shape or segmented row: the button of the shape the flag has (or the choice made) lit, the little flags hoisted as it is.</summary>
        private static void RefreshOptions(Control control)
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
                UIBuilder.SetLit(control.Options[i], lit);
            }
        }

        private static string ReadoutText(Row row, float display)
        {
            string over = row.ReadoutOverride != null ? row.ReadoutOverride() : null;
            if (over != null)
                return over;

            if (row.Kind != RowKind.Choice)
                return row.FormatDisplay(display);

            int index = Mathf.RoundToInt(display);
            return row.Choices != null && index >= 0 && index < row.Choices.Length ? row.Choices[index] : string.Empty;
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
            const float width = 70f, gap = 8f;
            UIButton swatch = UIBuilder.AddSwatch(page, new Vector2(width, 22f), new Vector3(200f + index * (width + gap), y + 6f),
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

            switch (row.Kind)
            {
                case RowKind.Status:
                    control.Status = UIBuilder.AddLabel(page, row.StatusText != null ? row.StatusText() : string.Empty, new Vector3(0f, y + 8f), 0.8f);
                    control.Status.autoSize = false;
                    control.Status.size = new Vector2(page.width, 20f);
                    control.Status.textColor = UIBuilder.StatusColour;
                    break;

                case RowKind.FlagList:
                    List = page.AddUIComponent<FlagListView>();
                    List.relativePosition = new Vector3(0f, y + 4f);
                    List.Build(page.width);
                    break;

                case RowKind.Button:
                {
                    bool pair = row.Id2 != null;
                    float width = pair ? (page.width - 8f) * 0.5f : page.width;

                    UIButton button = UIBuilder.AddButton(page, row.ButtonText != null ? row.ButtonText() : row.Label, new Vector2(width, 32f),
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
                        UIButton second = UIBuilder.AddButton(page, row.Label2, new Vector2(width, 32f), new Vector3(width + 8f, y + 4f));
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
                    UILabel label = UIBuilder.AddLabel(page, row.Label, new Vector3(0f, y + 8f), 0.8f);
                    label.autoSize = false;
                    label.size = new Vector2(196f, 20f);
                    label.tooltip = row.Tooltip;

                    const float imageWidth = 42f;
                    float width = page.width - 200f - imageWidth - 8f;
                    UIButton button = UIBuilder.AddButton(page, string.Empty, new Vector2(width, 26f), new Vector3(200f, y + 3f));
                    button.textScale = 0.8f;
                    button.textHorizontalAlignment = UIHorizontalAlignment.Left;
                    button.textPadding = new RectOffset(8, 8, 5, 0);
                    button.tooltip = row.Tooltip;
                    Row captured = row;
                    button.eventClick += (component, e) =>
                    {
                        if (captured.IsEnabled)
                            CountryPicker.Toggle(captured, _owner);
                    };

                    UITextureSprite image = page.AddUIComponent<UITextureSprite>();
                    image.size = new Vector2(imageWidth, 26f);
                    image.relativePosition = new Vector3(200f + width + 8f, y + 3f);
                    image.isInteractive = false;

                    control.Country = button;
                    control.CountryImage = image;
                    control.Parts.Add(label);
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
                    float width = page.width - 200f - imageWidth - folderWidth - 2f * gap;
                    UIButton button = UIBuilder.AddButton(page, string.Empty, new Vector2(width, 26f), new Vector3(200f, y + 3f));
                    button.textScale = 0.8f;
                    button.textHorizontalAlignment = UIHorizontalAlignment.Left;
                    button.textPadding = new RectOffset(8, 8, 5, 0);
                    button.tooltip = row.Tooltip;
                    Row captured = row;
                    button.eventClick += (component, e) =>
                    {
                        if (captured.IsEnabled && captured.Image != null)
                            ImagePanel.Toggle(captured.Image, _owner);
                    };

                    UITextureSprite image = page.AddUIComponent<UITextureSprite>();
                    image.size = new Vector2(imageWidth, 26f);
                    image.relativePosition = new Vector3(200f + width + gap, y + 3f);
                    image.isInteractive = false;

                    UIButton folder = UIBuilder.AddButton(page, string.Empty, new Vector2(folderWidth, 26f), new Vector3(page.width - folderWidth, y + 3f));
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

                case RowKind.Shape:
                {
                    // "Shape   [▯] [▯] [▯] [□] [▭] [▭] [▭]": a little flag of each shape, hoisted as the flag is.
                    AddRowLabel(page, row, y, control);
                    int count = row.Presets != null ? row.Presets.Length : 0;
                    const float gap = 4f, height = 28f;
                    float width = count > 0 ? Mathf.Floor((page.width - 200f - gap * (count - 1)) / count) : 0f;
                    Row captured = row;
                    for (int i = 0; i < count; i++)
                    {
                        int preset = row.Presets[i];
                        UIButton button = UIBuilder.AddButton(page, string.Empty, new Vector2(width, height), new Vector3(200f + i * (width + gap), y + 3f));
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
                    float width = count > 0 ? Mathf.Floor((page.width - 200f - gap * (count - 1)) / count) : 0f;
                    Row captured = row;
                    for (int i = 0; i < count; i++)
                    {
                        int value = row.ChoiceValues[i];
                        UIButton button = UIBuilder.AddButton(page, row.ChoiceName(i), new Vector2(width, height), new Vector3(200f + i * (width + gap), y + 3f));
                        button.tooltip = row.Tooltip;
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
                    UICheckBox box = UIBuilder.AddCheckbox(page, y, row.Label, row.Bool != null && row.Bool.value, isChecked =>
                    {
                        if (_refreshing)
                            return;

                        SettingsCatalog.ApplyToggle(captured, isChecked);
                    });
                    box.tooltip = row.Tooltip;
                    control.Check = box;
                    control.Parts.Add(box);
                    break;
                }

                case RowKind.Choice:
                {
                    // A slider over the choices, with the choice's name as the readout (a dropdown
                    // would need sprites nobody has read out of the atlas: Volumetric Explosions).
                    Row captured = row;
                    int count = row.Choices != null ? row.Choices.Length : 1;
                    UISlider slider = UIBuilder.AddSlider(page, y, row.Label, 0f, Mathf.Max(1, count - 1), 1f, row.ChoiceIndex,
                        v => ReadoutText(captured, v),
                        v =>
                        {
                            if (_refreshing || captured.Int == null || captured.ChoiceValues == null)
                                return;

                            int index = Mathf.Clamp(Mathf.RoundToInt(v), 0, captured.ChoiceValues.Length - 1);
                            captured.Int.value = captured.ChoiceValues[index];
                            Changed(captured);
                        });
                    slider.tooltip = row.Tooltip;
                    control.Slider = slider;
                    control.Parts.Add(slider);
                    AddSiblings(page, control, slider);
                    break;
                }

                default:
                {
                    Row captured = row;
                    UISlider slider = UIBuilder.AddSlider(page, y, row.Label, row.Min, row.Max, row.Step,
                        Mathf.Clamp(row.Display, row.Min, row.Max), row.FormatDisplay,
                        v =>
                        {
                            if (_refreshing)
                                return;

                            captured.Store(v);
                            Changed(captured);
                        });
                    slider.tooltip = row.Tooltip;
                    control.Slider = slider;
                    control.Parts.Add(slider);
                    AddSiblings(page, control, slider);
                    break;
                }
            }

            return control;
        }

        /// <summary>A country row as its setting stands: the name on the button, the flag beside it (its thumbnail, at the flag's proportions), or "none".</summary>
        private static void ShowCountry(Control control)
        {
            Row row = control.Source;
            int number = row.Int != null ? row.Int.value : 0;
            control.CountryShown = number;

            FlagCountry country = FlagCountries.At(number);
            control.Country.text = country != null ? country.Name : Localization.Get("Country.None");

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

        /// <summary>A row's name on the left, as a slider row has it (greyed with the row).</summary>
        private static void AddRowLabel(UIPanel page, Row row, float y, Control control)
        {
            UILabel label = UIBuilder.AddLabel(page, row.Label, new Vector3(0f, y + 8f), 0.8f);
            label.autoSize = false;
            label.size = new Vector2(196f, 20f);
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

        /// <summary>The name label and the value readout of a slider row, so the whole row greys out together (AddSlider makes exactly three components, in that order).</summary>
        private static void AddSiblings(UIPanel page, Control control, UISlider slider)
        {
            int index = -1;
            for (int i = 0; i < page.components.Count; i++)
            {
                if (page.components[i] == slider)
                {
                    index = i;
                    break;
                }
            }

            if (index <= 0)
                return;

            UILabel name = page.components[index - 1] as UILabel;
            if (name != null)
                control.Parts.Add(name);

            if (index + 1 < page.components.Count)
            {
                UILabel readout = page.components[index + 1] as UILabel;
                if (readout != null)
                {
                    control.Readout = readout;
                    control.Parts.Add(readout);
                }
            }
        }
    }
}
