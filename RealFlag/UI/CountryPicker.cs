using System;
using System.Collections.Generic;
using ColossalFramework.UI;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// The country picker a design's Country row opens, beside the panel: every flag of
    /// FlagCountries by name, a search field over them, each row its flag's thumbnail and
    /// name, and "None" -- a pattern -- at the top. A click puts that country on the row at once
    /// (the selected flag changes as you pick); Done or Esc closes it.
    /// </summary>
    /// <remarks>
    /// Twelve rows of buttons over a scrollbar, filled from the filtered list at its offset --
    /// not 257 buttons: the game's UI draws every component it has, and only the thumbnails in
    /// view are ever made (FlagImages, 80 px PNGs).
    /// </remarks>
    public class CountryPicker : UIPanel
    {
        private const float PanelWidth = 300f;
        private const float RowHeight = 24f;
        private const int Rows = 12;

        private static CountryPicker _instance;

        private Row _row;
        private UIComponent _beside;
        private bool _filled;
        private ModsLibCS1.UI.Builder _ui;
        private UILabel _title;
        private UITextField _search;
        private UIScrollbar _bar;
        private UIPanel _box;
        private readonly UIButton[] _buttons = new UIButton[Rows];
        private readonly UITextureSprite[] _thumbs = new UITextureSprite[Rows];

        /// <summary>The flags the search leaves, in order; null stands for "None".</summary>
        private readonly List<FlagCountry> _shown = new List<FlagCountry>();
        private int _offset;
        private bool _refreshing;

        /// <summary>Opens the picker on a country row, beside the panel; the same row again closes it.</summary>
        public static void Toggle(Row row, UIComponent beside)
        {
            if (_instance != null && _instance._row == row && _instance.isVisible)
            {
                Close();
                return;
            }

            if (_instance == null)
                _instance = UIView.GetAView().AddUIComponent(typeof(CountryPicker)) as CountryPicker;
            if (_instance == null)
                return;

            _instance.Show();
            _instance.Edit(row, beside);
        }

        public static void Close()
        {
            if (_instance != null)
                _instance.Hide();
        }

        /// <summary>With the panel it belongs to.</summary>
        public static void DestroyPicker()
        {
            if (_instance != null)
                UnityEngine.Object.Destroy(_instance.gameObject);
            _instance = null;
        }

        public override void Start()
        {
            base.Start();

            _ui = PanelLook.NewBuilder("the country picker");
            size = new Vector2(PanelWidth, 10f);
            canFocus = true;
            isInteractive = true;
            name = "RealFlagCountryPicker";

            // Background, title (the row's: Edit), close button and drag handle (ModsLibCS1).
            _title = _ui.AddFrame(this, string.Empty, Hide).Title;
            _title.textScale = 0.9f;

            float y = 46f;
            _ui.AddName(this, Localization.Get("Country.Search"), new Vector3(12f, y + 5f), 60f);

            // The library's field; the search follows every letter typed.
            _search = _ui.AddTextField(this, new Vector2(PanelWidth - 24f - 64f, 26f), new Vector3(12f + 64f, y), 40);
            _search.padding = new RectOffset(6, 6, 5, 3);
            _search.horizontalAlignment = UIHorizontalAlignment.Left;
            _search.eventTextChanged += (component, text) =>
            {
                // The game's field cuts a paste at its length wherever it falls, and a full field
                // takes no second half of a typed emoji: half of one left in it stops the game's
                // text drawing (TextCut). Put right (which comes back here) only when full: below
                // it, a typed emoji's halves come one key event apiece, the second still to come.
                if (text.Length >= _search.maxLength && TextCut.HasLoneSurrogate(text))
                    _search.text = TextCut.Whole(text);
                else
                    Filter();
            };
            y += 34f;

            // The list: a dark box of rows, the scrollbar at its right.
            _box = AddUIComponent<UIPanel>();
            PanelLook.DressListBox(_ui, _box);
            _box.size = new Vector2(PanelWidth - 24f, RowHeight * Rows + 6f);
            _box.relativePosition = new Vector3(12f, y);
            _box.eventMouseWheel += (component, p) =>
            {
                Scroll(p.wheelDelta > 0 ? -3 : 3);
                p.Use();
            };

            float rowWidth = _box.width - 18f;
            for (int i = 0; i < Rows; i++)
            {
                UIButton button = _box.AddUIComponent<UIButton>();
                PanelLook.DressListRow(_ui, button);
                button.size = new Vector2(rowWidth, RowHeight);
                button.relativePosition = new Vector3(3f, 3f + i * RowHeight);
                button.textScale = 0.75f;
                button.textHorizontalAlignment = UIHorizontalAlignment.Left;
                button.textPadding = new RectOffset(44, 6, 5, 0);
                int index = i;
                button.eventClick += (component, e) => Pick(index);
                button.eventMouseWheel += (component, p) =>
                {
                    Scroll(p.wheelDelta > 0 ? -3 : 3);
                    p.Use();
                };

                UITextureSprite thumb = button.AddUIComponent<UITextureSprite>();
                thumb.isInteractive = false;

                _buttons[i] = button;
                _thumbs[i] = thumb;
            }

            float barWidth = PanelLook.ListBarWidth(_ui);
            _bar = PanelLook.AddListBar(_ui, _box, new Vector2(barWidth, _box.height - 6f), new Vector3(_box.width - 3f - barWidth, 3f));
            _bar.incrementAmount = 3f;
            _bar.eventValueChanged += (component, value) =>
            {
                if (_refreshing)
                    return;
                _offset = Mathf.RoundToInt(value);
                FillRows();
            };
            y += _box.height + 8f;

            UIButton done = _ui.AddButton(this, Localization.Get("Picker.Done"), new Vector2(PanelWidth - 24f, 28f), new Vector3(12f, y));
            done.eventClick += (component, e) => Hide();
            y += 38f;

            height = y;

            eventKeyDown += (component, p) =>
            {
                if (p.used || p.keycode != KeyCode.Escape)
                    return;

                p.Use();
                Hide();
            };
        }

        private void Edit(Row row, UIComponent beside)
        {
            _row = row;
            _beside = beside;
            _filled = false;
            if (_title == null)
                return; // Start has not run yet: Update fills it in on the next frame

            _filled = true;
            _title.text = _ui.Caps(row.Label);
            _search.text = string.Empty;
            Filter();
            ScrollToChosen();

            PanelLook.PlaceBeside(this, beside, 40f);
            BringToFront();
        }

        public override void Update()
        {
            base.Update();

            // Opened before Start ran: fill it in now.
            if (!_filled && _row != null && isVisible && _title != null)
                Edit(_row, _beside);
        }

        /// <summary>The list again, from the search field: "None" first while nothing is typed.</summary>
        private void Filter()
        {
            if (_search == null)
                return;

            string query = _search.text ?? string.Empty;
            _shown.Clear();
            if (query.Trim().Length == 0)
                _shown.Add(null);

            foreach (FlagCountry country in CountryNames.Order)
            {
                if (CountryNames.Matches(country, query))
                    _shown.Add(country);
            }

            _offset = 0;
            FillRows();
        }

        private void ScrollToChosen()
        {
            FlagCountry chosen = _row != null && _row.Int != null ? FlagCountries.At(_row.Int.value) : null;
            int at = _shown.IndexOf(chosen);
            if (at >= Rows)
            {
                _offset = Mathf.Clamp(at - Rows / 2, 0, Mathf.Max(0, _shown.Count - Rows));
                FillRows();
            }
        }

        private void Scroll(int rows)
        {
            _offset = Mathf.Clamp(_offset + rows, 0, Mathf.Max(0, _shown.Count - Rows));
            FillRows();
        }

        /// <summary>The twelve rows from the list at the offset: names, thumbnails, the chosen one lit.</summary>
        private void FillRows()
        {
            int chosen = _row != null && _row.Int != null ? _row.Int.value : 0;
            _refreshing = true;
            try
            {
                _bar.maxValue = Mathf.Max(0, _shown.Count - Rows);
                _bar.scrollSize = Mathf.Min(Rows, _shown.Count);
                _bar.value = _offset;
            }
            finally
            {
                _refreshing = false;
            }

            for (int i = 0; i < Rows; i++)
            {
                int at = _offset + i;
                UIButton button = _buttons[i];
                UITextureSprite thumb = _thumbs[i];
                if (at >= _shown.Count)
                {
                    button.isVisible = false;
                    continue;
                }

                button.isVisible = true;
                FlagCountry country = _shown[at];
                button.text = country != null ? CountryNames.Of(country) : Localization.Get("Country.None");

                Texture2D texture = country != null ? FlagImages.Thumbnail(country.Image) : null;
                thumb.texture = texture;
                thumb.isVisible = texture != null;
                if (texture != null)
                {
                    float scale = Mathf.Min(34f / Mathf.Max(1, texture.width), 18f / Mathf.Max(1, texture.height));
                    thumb.size = new Vector2(Mathf.Round(texture.width * scale), Mathf.Round(texture.height * scale));
                    thumb.relativePosition = new Vector3(Mathf.Round(4f + 0.5f * (34f - thumb.width)), Mathf.Round(0.5f * (RowHeight - thumb.height)));
                }

                bool lit = country != null ? FlagCountries.NumberOf(country.Code) == chosen : chosen == 0;
                PanelLook.SetListLit(_ui, button, lit);
            }
        }

        private void Pick(int index)
        {
            int at = _offset + index;
            if (_row == null || _row.Int == null || !_row.IsEnabled || at < 0 || at >= _shown.Count)
                return;

            FlagCountry country = _shown[at];
            int number = country != null ? FlagCountries.NumberOf(country.Code) : 0;
            if (_row.Int.value != number)
            {
                _row.Int.value = number;
                try
                {
                    if (_row.AfterChange != null)
                        _row.AfterChange();
                }
                catch (Exception e)
                {
                    Log.Error("country picker: applying the country threw.", e);
                }

                SettingsCatalog.RefreshAllUIs();
            }

            FillRows();
        }

        public override void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            base.OnDestroy();
        }
    }
}
