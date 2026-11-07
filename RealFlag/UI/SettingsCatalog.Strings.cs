using System;
using System.Globalization;
using RealFlag.Flags;

namespace RealFlag.UI
{
    /// <summary>
    /// STRING FLAGS' rows (2026-11-07): the panel's Strings tab, and the String window's three
    /// tabs -- Flags (their shape, size, spacing, motion), Designs (one design, designs in turn,
    /// or countries' flags in an order of the player's), String (the cord itself) -- once for the
    /// new string and once for the selected one (StringTarget.New / Selected). The rows that
    /// depend on the kind of sequence are left out of the window when they do not apply
    /// (Row.Shown), and the window builds itself again when that kind changes (StringTarget.Structure).
    /// </summary>
    public static partial class SettingsCatalog
    {
        private static Row _designRowNew, _designRowSelected;

        /// <summary>The Designs tab's row choosing which design the rows below edit (its choices are the target's designs: set before the window builds).</summary>
        public static Row StringDesignRow(StringTarget target)
        {
            return target.IsNew ? _designRowNew : _designRowSelected;
        }

        private static void BuildStrings()
        {
            StringTarget.Init();

            // The panel's Strings tab: the new string's window, the pick, the city's strings (the list is FlagsPanel's).
            Add(new Row
            {
                Kind = RowKind.Button,
                Panel = PanelPage.Strings,
                Id = "StrNewButton",
                OnClick = StringPanel.ToggleNew,
                Enabled = () => StringRenderer.Instance != null && StringPrefab.Info != null,
            });

            Add(new Row
            {
                Kind = RowKind.Button,
                Panel = PanelPage.Strings,
                Id = "StrPickButton",
                OnClick = () => StringTool.Open(StringTool.Mode.Pick),
                Enabled = () => StringRenderer.Instance != null && StringPrefab.Info != null,
            });

            Add(new Row
            {
                Kind = RowKind.Status,
                Panel = PanelPage.Strings,
                Id = "StrStatus",
                StatusText = StringsStatus,
            });

            AddString(StringTarget.New, PanelPage.StringNewFlags, PanelPage.StringNewDesigns, PanelPage.StringNewCord);
            AddString(StringTarget.Selected, PanelPage.StringSelFlags, PanelPage.StringSelDesigns, PanelPage.StringSelCord);
        }

        private static string StringsStatus()
        {
            StringRenderer r = StringRenderer.Instance;
            if (r == null)
                return Localization.Get("StrStatus.None");
            if (StringPrefab.Info == null)
                return Localization.Get("StringTool.NoNetwork");
            return Localization.Get("StrStatus.Text", r.SpanCount, r.SourceCount);
        }

        private static string Metres2(float value)
        {
            return Localization.Get("Unit.Metres", value.ToString("0.00", CultureInfo.InvariantCulture));
        }

        private static void AddString(StringTarget t, PanelPage flags, PanelPage designs, PanelPage cord)
        {
            StringSpec d = StringSpec.Default;
            Action apply = t.Apply;
            Action restructure = () =>
            {
                t.Restructure();
                t.Apply();
            };
            bool sel = !t.IsNew;

            // ---- the selected string's own buttons ----
            if (sel)
            {
                Add(new Row
                {
                    Kind = RowKind.Button,
                    Panel = flags,
                    Id = "StrSelGoTo",
                    OnClick = StringEditor.GoTo,
                    Id2 = "StrSelCopy",
                    OnClick2 = () =>
                    {
                        StringEditor.CopyToNew();
                        StringPanel.OpenNew();
                    },
                    Enabled = () => StringEditor.HasSelection,
                    Enabled2 = () => StringEditor.HasSelection,
                });

                Add(new Row
                {
                    Kind = RowKind.Button,
                    Panel = flags,
                    Id = "StrSelDelete",
                    OnClick = StringEditor.Delete,
                    Enabled = () => StringEditor.HasSelection,
                });
            }

            // ---- Flags ----
            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = flags,
                Group = "StrFlags",
                Int = t.Shape,
                DefaultInt = d.Shape,
                ChoiceValues = FlagCut.Values,
                ChoiceKeys = FlagCut.NameKeys,
                AfterChange = apply,
            });

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = flags,
                Float = t.Depth,
                DefaultFloat = d.Depth,
                Min = 10f, Max = 60f, Step = 5f,
                AfterChange = apply,
                Enabled = () => FlagCut.UsesDepth(t.Shape.value),
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = flags,
                Float = t.Width,
                DefaultFloat = d.Width,
                Min = StringSpec.MinFlag, Max = StringSpec.MaxFlag, Step = 0.05f,
                Format = Metres2,
                AfterChange = apply,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = flags,
                Float = t.Height,
                DefaultFloat = d.Height,
                Min = StringSpec.MinFlag, Max = StringSpec.MaxFlag, Step = 0.05f,
                Format = Metres2,
                AfterChange = apply,
            });

            Add(new Row
            {
                Kind = RowKind.Segmented,
                Panel = flags,
                Int = t.Mount,
                DefaultInt = d.Mount,
                ChoiceValues = StringMount.Values,
                ChoiceKeys = StringMount.NameKeys,
                AfterChange = apply,
            });

            Add(new Row
            {
                Kind = RowKind.Segmented,
                Panel = flags,
                Group = "StrSpacing",
                Int = t.Spacing,
                DefaultInt = d.Spacing,
                ChoiceValues = StringSpacing.Values,
                ChoiceKeys = StringSpacing.NameKeys,
                AfterChange = apply,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = flags,
                Float = t.Gap,
                DefaultFloat = d.Gap,
                Min = 0f, Max = 2f, Step = 0.01f,
                Format = Metres2,
                AfterChange = apply,
                Enabled = () => t.Spacing.value == StringSpacing.Gap,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = flags,
                Float = t.Count,
                DefaultFloat = d.Count,
                Min = 1f, Max = 120f, Step = 1f,
                Format = v => Localization.Get("Unit.Flags", Math.Round(v).ToString(CultureInfo.InvariantCulture)),
                AfterChange = apply,
                Enabled = () => t.Spacing.value == StringSpacing.Count,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = flags,
                Float = t.Margin,
                DefaultFloat = d.Margin,
                Min = 0f, Max = 3f, Step = 0.05f,
                Format = Metres2,
                AfterChange = apply,
            });

            Add(new Row
            {
                Kind = RowKind.Segmented,
                Panel = flags,
                Group = "StrMotion",
                Int = t.Motion,
                DefaultInt = d.Motion,
                ChoiceValues = FlagMotion.Values,
                ChoiceKeys = FlagMotion.NameKeys,
                AfterChange = apply,
            });

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = flags,
                Float = t.Waviness,
                DefaultFloat = d.Waviness,
                Min = 0f, Max = 200f, Step = 5f,
                AfterChange = apply,
                Enabled = () => t.Motion.value != FlagMotion.Flat,
            });

            Add(new Row
            {
                Kind = RowKind.Segmented,
                Panel = flags,
                Int = t.Motions,
                DefaultInt = d.Motions,
                ChoiceValues = new[] { 0, 1, 2, 4, 8 },
                ChoiceText = v => v == 0 ? Localization.Get("StrMotions.Auto") : v.ToString(CultureInfo.InvariantCulture),
                AfterChange = apply,
                Enabled = () => t.Motion.value == FlagMotion.Waves,
            });

            // ---- Designs ----
            Func<bool> notCountries = () => t.Sequence.value != StringSequence.Countries;
            Func<bool> alternate = () => t.Sequence.value == StringSequence.Alternate;
            Func<bool> countries = () => t.Sequence.value == StringSequence.Countries;
            Func<bool> patterned = () => t.Country.value == 0;

            Add(new Row
            {
                Kind = RowKind.Segmented,
                Panel = designs,
                Int = t.Sequence,
                DefaultInt = d.Sequence,
                ChoiceValues = StringSequence.Values,
                ChoiceKeys = StringSequence.NameKeys,
                AfterChange = restructure,
            });

            var designRow = new Row
            {
                Kind = RowKind.Segmented,
                Panel = designs,
                Int = t.Design,
                DefaultInt = 0,
                ChoiceValues = new[] { 0, 1, 2 },
                ChoiceText = v => (v + 1).ToString(CultureInfo.InvariantCulture),
                AfterChange = () => t.ChooseDesign(t.Design.value),
                Shown = alternate,
                // Which design is edited is not a setting to put back.
                AtDefault = () => true,
            };
            Add(designRow);
            if (t.IsNew)
                _designRowNew = designRow;
            else
                _designRowSelected = designRow;

            Add(new Row
            {
                Kind = RowKind.Button,
                Panel = designs,
                Id = t.Prefix + "AddDesign",
                OnClick = t.AddDesign,
                Id2 = t.Prefix + "RemoveDesign",
                OnClick2 = t.RemoveDesign,
                Enabled = () => t.Designs.Count < StringSpec.MaxDesigns,
                Enabled2 = () => t.Designs.Count > 1,
                Shown = alternate,
            });

            Add(new Row
            {
                Kind = RowKind.Country,
                Panel = designs,
                Int = t.Country,
                DefaultInt = 0,
                AfterChange = apply,
                Shown = notCountries,
            });

            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = designs,
                Int = t.Pattern,
                DefaultInt = d.Designs[0].Pattern,
                ChoiceValues = FlagPattern.Values,
                ChoiceKeys = FlagPattern.NameKeys,
                AfterChange = apply,
                Enabled = patterned,
                Shown = notCountries,
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = designs,
                Int = t.Colour1,
                DefaultInt = (int)d.Designs[0].Colour1,
                AfterChange = apply,
                Enabled = patterned,
                Shown = notCountries,
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = designs,
                Int = t.Colour2,
                DefaultInt = (int)d.Designs[0].Colour2,
                AfterChange = apply,
                Enabled = () => patterned() && Uses(t.Pattern, 2),
                Shown = notCountries,
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = designs,
                Int = t.Colour3,
                DefaultInt = (int)d.Designs[0].Colour3,
                AfterChange = apply,
                Enabled = () => patterned() && Uses(t.Pattern, 3),
                Shown = notCountries,
            });

            Add(new Row
            {
                Kind = RowKind.Segmented,
                Panel = designs,
                Int = t.Order,
                DefaultInt = d.DesignOrder,
                ChoiceValues = StringOrder.Values,
                ChoiceKeys = StringOrder.NameKeys,
                AfterChange = restructure,
                Shown = alternate,
            });

            Add(new Row
            {
                Kind = RowKind.Segmented,
                Panel = designs,
                Int = t.Which,
                DefaultInt = d.Which,
                ChoiceValues = CountryWhich.Values,
                ChoiceKeys = CountryWhich.NameKeys,
                AfterChange = restructure,
                Shown = countries,
            });

            Add(new Row
            {
                Kind = RowKind.Choice,
                Panel = designs,
                Int = t.Region,
                DefaultInt = d.Region,
                ChoiceValues = FlagRegions.Values,
                ChoiceKeys = FlagRegions.NameKeys,
                AfterChange = apply,
                Shown = () => countries() && t.Which.value == CountryWhich.Region,
            });

            Add(new Row
            {
                Kind = RowKind.Button,
                Panel = designs,
                Id = t.Prefix + "ChooseCountries",
                ButtonText = () => Localization.Get(t.Prefix + "ChooseCountries.Label") + " (" + t.Countries.Count + ")",
                OnClick = () => CountryListPicker.Toggle(t, t.IsNew ? StringPanel.ComponentName : StringPanel.ComponentName),
                Shown = () => countries() && t.Which.value == CountryWhich.List,
            });

            Add(new Row
            {
                Kind = RowKind.Segmented,
                Panel = designs,
                Int = t.CountryOrder,
                DefaultInt = d.CountryOrder,
                ChoiceValues = Flags.CountryOrder.Values,
                ChoiceKeys = Flags.CountryOrder.NameKeys,
                AfterChange = restructure,
                Shown = countries,
            });

            Add(new Row
            {
                Kind = RowKind.Country,
                Panel = designs,
                Int = t.Lead,
                DefaultInt = 0,
                AfterChange = apply,
                Shown = countries,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = designs,
                Float = t.LeadEvery,
                DefaultFloat = d.LeadEvery,
                Min = 0f, Max = 20f, Step = 1f,
                Format = v => v < 0.5f ? Localization.Get("StrLeadEvery.First") : Localization.Get("StrLeadEvery.Every", Math.Round(v).ToString(CultureInfo.InvariantCulture)),
                AfterChange = apply,
                Enabled = () => t.Lead.value != 0,
                Shown = countries,
            });

            Add(new Row
            {
                Kind = RowKind.Button,
                Panel = designs,
                Id = t.Prefix + "Shuffle",
                OnClick = t.Reshuffle,
                Shown = () => (alternate() && t.Order.value == StringOrder.Random) || (countries() && t.CountryOrder.value == Flags.CountryOrder.Random),
            });

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = designs,
                Float = t.Darkness,
                DefaultFloat = 0f,
                Min = 0f, Max = FlagSpec.MaxDarkness * 100f, Step = 5f,
                AfterChange = apply,
            });

            Add(new Row
            {
                Kind = RowKind.Segmented,
                Panel = designs,
                Int = t.CarryOn,
                DefaultInt = d.CarryOn ? 1 : 0,
                ChoiceValues = new[] { 1, 0 },
                ChoiceKeys = new[] { "StrCarryOn.Yes", "StrCarryOn.No" },
                AfterChange = apply,
            });

            // ---- String ----
            Add(new Row
            {
                Kind = RowKind.Segmented,
                Panel = cord,
                Group = "StrCord",
                Int = t.Kind,
                DefaultInt = d.Kind,
                ChoiceValues = StringKind.Values,
                ChoiceKeys = StringKind.NameKeys,
                AfterChange = apply,
            });

            Add(new Row
            {
                Kind = RowKind.Colour,
                Panel = cord,
                Int = t.Colour,
                DefaultInt = (int)d.Colour,
                AfterChange = apply,
            });

            Add(new Row
            {
                Kind = RowKind.Value,
                Panel = cord,
                Float = t.Thickness,
                DefaultFloat = d.Thickness * 1000f,
                Min = StringSpec.MinThickness * 1000f, Max = StringSpec.MaxThickness * 1000f, Step = 1f,
                Format = v => Localization.Get("Unit.Millimetres", Math.Round(v).ToString(CultureInfo.InvariantCulture)),
                AfterChange = apply,
            });

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = cord,
                Float = t.Sag,
                DefaultFloat = d.Sag,
                Min = StringSpec.MinSag * 100f, Max = StringSpec.MaxSag * 100f, Step = 0.5f,
                Format = v => Localization.Get("Unit.Percent", v.ToString("0.#", CultureInfo.InvariantCulture)),
                AfterChange = apply,
            });

            Add(new Row
            {
                Kind = RowKind.Percent,
                Panel = cord,
                Float = t.Swing,
                DefaultFloat = d.Swing,
                Min = 0f, Max = StringSpec.MaxSwing * 100f, Step = 5f,
                AfterChange = apply,
            });

            if (t.IsNew)
            {
                Add(new Row
                {
                    Kind = RowKind.Status,
                    Panel = cord,
                    Id = "StrPlaceHint",
                    StatusText = () => Localization.Get("StrPlaceHint.Text"),
                });
            }
        }
    }
}
