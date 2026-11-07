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
    /// <remarks>
    /// Virtual, as CountryPicker and ImagePanel: <see cref="Slots"/> rows exist whatever the city's
    /// count, each relabelled with the flag at the list's offset as the list scrolls, and the
    /// scrollbar sized to all of them. A thousand flags cost what five do. The rows sit in a view
    /// that clips them to the five in the box; the spares lie below its edge and are never seen.
    /// </remarks>
    public class FlagListView : UIPanel
    {
        public const float RowHeight = 24f;
        public const int VisibleRows = 5;

        /// <summary>Row components: the visible ones plus two spares below the view's edge.</summary>
        private const int Slots = VisibleRows + 2;

        /// <summary>Rows a notch of the wheel moves (the Image window's: 2 of its 7).</summary>
        private const int WheelRows = 2;

        /// <summary>
        /// A second click on a row this soon is a double click (Windows' default). The game's own is
        /// 0.25 s (UIInput.MouseHandler.m_DoubleClickTime), read once a frame: at 8-11 fps a double
        /// click often misses it, and the second click would let go of the flag just selected.
        /// </summary>
        private const float DoubleClickSeconds = 0.5f;

        private Builder _ui;
        private UIPanel _view;
        private UIScrollbar _bar;
        private UILabel _empty;

        // The city's flags in prop order, from the last Refresh; the rows show a window of them.
        private readonly List<FlagEntry> _entries = new List<FlagEntry>();
        private readonly UIButton[] _rows = new UIButton[Slots];
        // Per slot: the prop it shows, what it shows (its text and its colours or picture; null: the
        // slot is empty and hidden), and the sprites at its right.
        private readonly uint[] _slotProp = new uint[Slots];
        private readonly string[] _looks = new string[Slots];
        private readonly List<UIComponent>[] _decorations = new List<UIComponent>[Slots];
        private int _offset;
        private int _shownCount = -1;
        private int _shownVersion = -1;
        private int _shownImages = -1;
        private uint _shownSelected = uint.MaxValue;
        // Set while the bar is moved by code, so its value event does not scroll again.
        private bool _refreshing;
        // The flag of the last single click and when, for DoubleClickSeconds; 0: none waiting.
        private uint _clicked;
        private float _clickedAt;

        /// <summary>Builds the box inside a page, <paramref name="width"/> wide, in the panel's look.</summary>
        public void Build(float width, Builder ui)
        {
            _ui = ui;
            size = new Vector2(width, RowHeight * VisibleRows + 6f);
            PanelLook.DressListBox(ui, this);

            _view = AddUIComponent<UIPanel>();
            _view.size = new Vector2(width - 16f, RowHeight * VisibleRows);
            _view.relativePosition = new Vector3(3f, 3f);
            _view.clipChildren = true;
            _view.eventMouseWheel += (component, p) => OnWheel(p);

            for (int i = 0; i < Slots; i++)
            {
                UIButton row = _view.AddUIComponent<UIButton>();
                PanelLook.DressListRow(ui, row);
                row.size = new Vector2(_view.width, RowHeight);
                row.relativePosition = new Vector3(0f, i * RowHeight);
                row.textScale = 0.72f;
                row.textHorizontalAlignment = UIHorizontalAlignment.Left;
                row.textPadding = new RectOffset(8, 64, 4, 0);
                row.isVisible = false;

                // A click selects the flag the row shows now; a click on the selected one lets it go.
                // The game sends the second click of a quick pair as a double click INSTEAD of a click
                // (UIInput), so a slower pair arrives here as two clicks: both go to the flag.
                int slot = i;
                row.eventClick += (component, p) => Click(slot);
                row.eventDoubleClick += (component, p) =>
                {
                    if (_looks[slot] != null)
                        GoTo(_slotProp[slot]);
                };
                row.eventMouseWheel += (component, p) => OnWheel(p);

                _rows[i] = row;
                _decorations[i] = new List<UIComponent>();
            }

            float barWidth = PanelLook.ListBarWidth(ui);
            _bar = PanelLook.AddListBar(ui, this, new Vector2(barWidth, height - 6f), new Vector3(width - 2f - barWidth, 3f));
            _bar.value = 0f;
            // The arrows move a whole row's worth as the wheel does: the bar counts rows now.
            _bar.incrementAmount = WheelRows;
            _bar.autoHide = true;
            _bar.eventValueChanged += (component, value) =>
            {
                if (!_refreshing)
                    SetOffset(Mathf.RoundToInt(value));
            };

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
            if (_view == null)
                return;

            // Read before the list is taken: a change made meanwhile moves the version again and
            // shows at the next refresh.
            int version = FlagRegistry.Version;
            int images = FlagImages.UserVersion;

            if (renderer == null)
                _entries.Clear();
            else
                renderer.List(_entries);

            if (_entries.Count != _shownCount)
            {
                _shownCount = _entries.Count;
                _offset = Mathf.Clamp(_offset, 0, MaxOffset());
                SyncBar();
            }

            // A design or a registry change may move any shown row's text or colours (a slider on
            // the selected flag moves the version up to four times a second): those rows are made
            // again, the rest only where the flag in a slot changed.
            bool moved = version != _shownVersion || images != _shownImages;
            _shownVersion = version;
            _shownImages = images;
            FillRows(moved);

            if (_shownSelected != FlagEditor.Selected)
                Highlight();

            _empty.isVisible = _entries.Count == 0;
        }

        /// <summary>The most rows the list can scroll: the count less the view's five.</summary>
        private int MaxOffset()
        {
            return Mathf.Max(0, _entries.Count - VisibleRows);
        }

        /// <summary>Moves the list to a row offset (the wheel, the bar's arrows and track).</summary>
        private void SetOffset(int offset)
        {
            int to = Mathf.Clamp(offset, 0, MaxOffset());
            if (to == _offset)
                return;

            _offset = to;
            SyncBar();
            FillRows(false);
        }

        private void Scroll(int rows)
        {
            SetOffset(_offset + rows);
        }

        private void OnWheel(UIMouseEventParameter p)
        {
            Scroll(p.wheelDelta > 0 ? -WheelRows : WheelRows);
            p.Use();
        }

        /// <summary>The bar's range and place: all the rows as the count, the view's five as the page.</summary>
        private void SyncBar()
        {
            _refreshing = true;
            try
            {
                _bar.maxValue = MaxOffset();
                _bar.scrollSize = VisibleRows;
                _bar.value = _offset;
            }
            finally
            {
                _refreshing = false;
            }
        }

        /// <summary>
        /// Each slot shows the flag at the offset plus its index. A slot is drawn again only if its
        /// flag changed (its prop), or <paramref name="force"/> says the registry or the pictures did.
        /// </summary>
        private void FillRows(bool force)
        {
            int count = _entries.Count;
            for (int i = 0; i < Slots; i++)
            {
                int at = _offset + i;
                if (at >= count)
                {
                    if (_looks[i] != null)
                        Empty(i);
                    continue;
                }

                FlagEntry e = _entries[at];
                if (!force && _looks[i] != null && _slotProp[i] == e.Prop)
                    continue;

                Show(i, e);
            }
        }

        /// <summary>A slot with no flag: hidden, its sprites gone.</summary>
        private void Empty(int i)
        {
            _rows[i].isVisible = false;
            _looks[i] = null;
            ClearDecorations(i);
        }

        private void ClearDecorations(int i)
        {
            List<UIComponent> decoration = _decorations[i];
            foreach (UIComponent old in decoration)
            {
                _rows[i].RemoveUIComponent(old);
                Object.Destroy(old.gameObject);
            }

            decoration.Clear();
        }

        /// <summary>
        /// A click on a slot: selects its flag, or lets it go if it is the selected one. A second
        /// click within DoubleClickSeconds is the double click (GoTo).
        /// </summary>
        private void Click(int slot)
        {
            if (_looks[slot] == null)
                return;

            uint prop = _slotProp[slot];
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

        /// <summary>Slot <paramref name="i"/> shows <paramref name="e"/>: drawn again only if its text, colours or picture changed.</summary>
        private void Show(int i, FlagEntry e)
        {
            UIButton row = _rows[i];
            _slotProp[i] = e.Prop;
            row.isVisible = true;
            PanelLook.SetListLit(_ui, row, e.Prop == FlagEditor.Selected);

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

            if (look == _looks[i])
                return;

            _looks[i] = look;
            row.text = text;
            ClearDecorations(i);

            List<UIComponent> decoration = _decorations[i];
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

        /// <summary>The shown rows lit where their flag is the selected one.</summary>
        private void Highlight()
        {
            for (int i = 0; i < Slots; i++)
            {
                if (_looks[i] != null)
                    PanelLook.SetListLit(_ui, _rows[i], _slotProp[i] == FlagEditor.Selected);
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
