using System.Collections.Generic;
using System.Globalization;
using ColossalFramework.UI;
using ModsLibCS1.UI;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// The Flags tab's list: every flag in the city, a row each -- its name, its pattern (its
    /// country, its picture) and its width by height as seen, its three colours (its image) -- in a
    /// scrolling box. A click selects the flag (FlagEditor) and the controls below take it over; a
    /// double click also takes the camera to it.
    /// </summary>
    public class FlagListView : UIPanel
    {
        public const float RowHeight = 24f;
        public const int VisibleRows = 5;

        /// <summary>Rows a notch of the wheel moves (the Image window's: 2 of its 7).</summary>
        private const int WheelRows = 2;

        /// <summary>
        /// A second click on a row this soon is a double click (Windows' default). The game's own is
        /// 0.25 s (UIInput.MouseHandler.m_DoubleClickTime), read once a frame: at 8-11 fps a double
        /// click often misses it, and the second click would let go of the flag just selected.
        /// </summary>
        private const float DoubleClickSeconds = 0.5f;

        private Builder _ui;
        private UIScrollablePanel _list;
        private UIScrollbar _bar;
        private UILabel _empty;

        private readonly List<FlagEntry> _entries = new List<FlagEntry>();
        private readonly List<UIButton> _rows = new List<UIButton>();
        private readonly List<uint> _shown = new List<uint>();
        // Per row: what it shows (its text and its colours or picture), and the sprites at its right.
        private readonly List<string> _looks = new List<string>();
        private readonly List<List<UIComponent>> _decorations = new List<List<UIComponent>>();
        private int _shownVersion = -1;
        private int _shownImages = -1;
        private uint _shownSelected = uint.MaxValue;
        // The flag of the last single click and when, for DoubleClickSeconds; 0: none waiting.
        private uint _clicked;
        private float _clickedAt;

        /// <summary>Builds the box inside a page, <paramref name="width"/> wide, in the panel's look.</summary>
        public void Build(float width, Builder ui)
        {
            _ui = ui;
            size = new Vector2(width, RowHeight * VisibleRows + 6f);
            PanelLook.DressListBox(ui, this);

            _list = AddUIComponent<UIScrollablePanel>();
            _list.size = new Vector2(width - 16f, height - 6f);
            _list.relativePosition = new Vector3(3f, 3f);
            _list.autoLayout = true;
            _list.autoLayoutDirection = LayoutDirection.Vertical;
            _list.autoLayoutPadding = new RectOffset(0, 0, 0, 0);
            _list.scrollWheelDirection = UIOrientation.Vertical;
            // UIScrollablePanel.OnMouseWheel does nothing without this (off by default); the rows
            // pass the wheel up to it. With a scrollbar a notch moves the bar's incrementAmount.
            _list.builtinKeyNavigation = true;
            _list.clipChildren = true;

            float barWidth = PanelLook.ListBarWidth(ui);
            _bar = PanelLook.AddListBar(ui, this, new Vector2(barWidth, height - 6f), new Vector3(width - 2f - barWidth, 3f));
            _bar.value = 0f;
            _bar.incrementAmount = RowHeight * WheelRows;
            _bar.autoHide = true;

            _list.verticalScrollbar = _bar;

            _empty = ui.AddLabel(this, Localization.Get("Existing.Empty"), new Vector3(10f, 10f), 0.75f);
            _empty.autoSize = false;
            _empty.wordWrap = true;
            _empty.size = new Vector2(width - 20f, height - 20f);
            ui.Paint(_empty, ui.Theme.Status);
        }

        /// <summary>Brings the rows up to date with the city's flags; cheap when nothing changed.</summary>
        public void Refresh()
        {
            FlagRenderer renderer = FlagRenderer.Instance;
            if (_list == null)
                return;

            if (renderer == null)
                _entries.Clear();
            else
                renderer.List(_entries);

            if (Changed())
                Sync();

            if (_shownSelected != FlagEditor.Selected)
                Highlight();

            _empty.isVisible = _entries.Count == 0;
        }

        /// <summary>A flag came or went, or one's design changed (the registry's version moved).</summary>
        private bool Changed()
        {
            // A picture's textures made again (its file changed): its thumbnail with them.
            if (_shownVersion != FlagRegistry.Version || _shownImages != FlagImages.UserVersion || _entries.Count != _shown.Count)
                return true;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Prop != _shown[i])
                    return true;
            }

            return false;
        }

        /// <summary>
        /// The rows brought to <see cref="_entries"/>: rows are added or taken away at the end only,
        /// and a row is drawn again only if what it shows changed. The list arranges ALL its rows
        /// each time one is added or removed (UIScrollablePanel.OnComponentAdded/Removed call
        /// AutoArrange, its IL), and a slider on the selected flag moves the registry's version up to
        /// four times a second: making every row again each time grew as the square of the flags.
        /// </summary>
        private void Sync()
        {
            while (_rows.Count < _entries.Count)
                AddRow();

            while (_rows.Count > _entries.Count)
            {
                int last = _rows.Count - 1;
                _list.RemoveUIComponent(_rows[last]);
                Object.Destroy(_rows[last].gameObject);
                _rows.RemoveAt(last);
                _shown.RemoveAt(last);
                _looks.RemoveAt(last);
                _decorations.RemoveAt(last);
            }

            for (int i = 0; i < _entries.Count; i++)
                Show(i, _entries[i]);

            _shownVersion = FlagRegistry.Version;
            _shownImages = FlagImages.UserVersion;
            _shownSelected = uint.MaxValue;
            Highlight();
        }

        /// <summary>A row at the end of the list, empty until <see cref="Show"/> fills it.</summary>
        private void AddRow()
        {
            int index = _rows.Count;
            UIButton row = _list.AddUIComponent<UIButton>();
            PanelLook.DressListRow(_ui, row);
            row.size = new Vector2(_list.width, RowHeight);
            row.textScale = 0.72f;
            row.textHorizontalAlignment = UIHorizontalAlignment.Left;
            row.textPadding = new RectOffset(8, 64, 4, 0);

            // A click selects the flag the row shows now; a click on the selected one lets it go.
            // The game sends the second click of a quick pair as a double click INSTEAD of a click
            // (UIInput), so a slower pair arrives here as two clicks: both go to the flag.
            row.eventClick += (component, p) =>
            {
                if (index >= _shown.Count)
                    return;

                uint prop = _shown[index];
                float now = Time.realtimeSinceStartup;
                if (prop == _clicked && now - _clickedAt < DoubleClickSeconds)
                {
                    GoTo(prop);
                    return;
                }

                _clicked = prop;
                _clickedAt = now;
                if (FlagEditor.Selected == prop)
                    FlagEditor.Deselect();
                else
                    FlagEditor.Select(prop);
                Highlight();
            };

            row.eventDoubleClick += (component, p) =>
            {
                if (index < _shown.Count)
                    GoTo(_shown[index]);
            };

            _rows.Add(row);
            _shown.Add(0u);
            _looks.Add(null);
            _decorations.Add(new List<UIComponent>());
        }

        /// <summary>
        /// A double click: the camera to the flag, left selected. The first click has already
        /// selected it, or let it go if it was the selected one: then it is selected again.
        /// </summary>
        private void GoTo(uint prop)
        {
            _clicked = 0u;
            if (FlagEditor.Selected != prop)
                FlagEditor.Select(prop);
            FlagEditor.GoTo();
            Highlight();
        }

        /// <summary>Row <paramref name="i"/> shows <paramref name="e"/>: drawn again only if its text, colours or picture changed.</summary>
        private void Show(int i, FlagEntry e)
        {
            FlagCountry country = FlagCountries.Find(e.Spec.Country);
            float shownWidth, shownHeight;
            FlagSize.Shown(e.Spec.Length, e.Spec.Proportion, e.Spec.HoistedFrom, out shownWidth, out shownHeight);
            string design = country != null ? CountryNames.Of(country) : e.Spec.HasImage ? e.Spec.Image : Localization.Get(PatternKey(e.Spec.Pattern));
            string text = FlagEditor.Title(e) + "  ·  " + design + ", " + Localization.Get("Unit.Size",
                shownWidth.ToString("0.#", CultureInfo.InvariantCulture), shownHeight.ToString("0.#", CultureInfo.InvariantCulture));

            // At the right: a country's flag, the player's picture, or the pattern's colours.
            Texture2D thumb = country != null ? FlagImages.Thumbnail(country.Image) : e.Spec.HasImage ? FlagImages.UserPreview(e.Spec.Image) : null;
            int used = thumb != null ? 0 : FlagPattern.ColoursUsed(e.Spec.Pattern);
            string look = text + "|" + (thumb != null
                ? "t" + thumb.GetInstanceID()
                : "c" + used + "," + e.Spec.Colour1 + "," + e.Spec.Colour2 + "," + e.Spec.Colour3);

            _shown[i] = e.Prop;
            if (look == _looks[i])
                return;

            _looks[i] = look;
            UIButton row = _rows[i];
            row.text = text;

            List<UIComponent> decoration = _decorations[i];
            foreach (UIComponent old in decoration)
            {
                row.RemoveUIComponent(old);
                Object.Destroy(old.gameObject);
            }

            decoration.Clear();

            float width = row.width;
            if (thumb != null)
            {
                float scale = Mathf.Min(40f / Mathf.Max(1, thumb.width), 16f / Mathf.Max(1, thumb.height));
                UITextureSprite image = row.AddUIComponent<UITextureSprite>();
                image.texture = thumb;
                image.size = new Vector2(Mathf.Round(thumb.width * scale), Mathf.Round(thumb.height * scale));
                image.relativePosition = new Vector3(width - 60f, Mathf.Round(0.5f * (RowHeight - image.height)));
                image.isInteractive = false;
                decoration.Add(image);
            }
            else
            {
                uint[] colours = { e.Spec.Colour1, e.Spec.Colour2, e.Spec.Colour3 };
                for (int c = 0; c < used; c++)
                {
                    UIButton swatch = FlagWidgets.AddSwatch(row, new Vector2(16f, 14f), new Vector3(width - 60f + c * 19f, 5f), colours[c]);
                    swatch.isInteractive = false;
                    decoration.Add(swatch);
                }
            }
        }

        private void Highlight()
        {
            for (int i = 0; i < _rows.Count && i < _shown.Count; i++)
            {
                PanelLook.SetListLit(_ui, _rows[i], _shown[i] == FlagEditor.Selected);
            }

            _shownSelected = FlagEditor.Selected;
        }

        private static string PatternKey(int pattern)
        {
            for (int i = 0; i < FlagPattern.Values.Length; i++)
            {
                if (FlagPattern.Values[i] == pattern)
                    return FlagPattern.NameKeys[i];
            }

            return FlagPattern.NameKeys[0];
        }
    }
}
