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

            atlas = UIBuilder.Atlas;
            backgroundSprite = "MenuPanel2";
            size = new Vector2(PanelWidth, 10f);
            canFocus = true;
            isInteractive = true;
            name = "RealFlagCountryPicker";

            _title = UIBuilder.AddLabel(this, "", new Vector3(12f, 12f), 0.9f);
            _title.autoSize = false;
            _title.size = new Vector2(PanelWidth - 50f, 20f);

            UIButton close = AddUIComponent<UIButton>();
            close.atlas = atlas;
            close.size = new Vector2(28f, 28f);
            close.relativePosition = new Vector3(PanelWidth - 32f, 4f);
            close.normalBgSprite = "buttonclose";
            close.hoveredBgSprite = "buttonclosehover";
            close.pressedBgSprite = "buttonclosepressed";
            close.eventClick += (component, e) => Hide();

            UIDragHandle drag = AddUIComponent<UIDragHandle>();
            drag.target = this;
            drag.size = new Vector2(PanelWidth - 36f, 36f);
            drag.relativePosition = Vector3.zero;

            float y = 40f;
            UILabel find = UIBuilder.AddLabel(this, Localization.Get("Country.Search"), new Vector3(12f, y + 5f), 0.8f);
            find.autoSize = false;
            find.size = new Vector2(60f, 20f);

            _search = AddUIComponent<UITextField>();
            _search.atlas = atlas;
            _search.size = new Vector2(PanelWidth - 24f - 64f, 26f);
            _search.relativePosition = new Vector3(12f + 64f, y);
            _search.normalBgSprite = "TextFieldPanel";
            _search.hoveredBgSprite = "TextFieldPanelHovered";
            _search.focusedBgSprite = "TextFieldPanelFocused";
            _search.selectionSprite = "EmptySprite";
            _search.selectionBackgroundColor = new Color32(0, 105, 210, 255);
            _search.textColor = new Color32(20, 20, 20, 255);
            _search.color = new Color32(255, 255, 255, 255);
            _search.textScale = 0.85f;
            _search.padding = new RectOffset(6, 6, 5, 3);
            _search.horizontalAlignment = UIHorizontalAlignment.Left;
            _search.builtinKeyNavigation = true;
            _search.isInteractive = true;
            _search.readOnly = false;
            _search.canFocus = true;
            _search.maxLength = 40;
            _search.eventTextChanged += (component, text) => Filter();
            y += 34f;

            // The list: a dark box of rows, the scrollbar at its right.
            _box = AddUIComponent<UIPanel>();
            _box.atlas = atlas;
            _box.backgroundSprite = "GenericPanelDark";
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
                button.atlas = atlas;
                button.size = new Vector2(rowWidth, RowHeight);
                button.relativePosition = new Vector3(3f, 3f + i * RowHeight);
                button.textScale = 0.75f;
                button.textHorizontalAlignment = UIHorizontalAlignment.Left;
                button.textPadding = new RectOffset(44, 6, 5, 0);
                button.normalBgSprite = "ButtonMenu";
                button.hoveredBgSprite = "ButtonMenuHovered";
                button.pressedBgSprite = "ButtonMenuPressed";
                button.focusedBgSprite = "ButtonMenu";
                button.canFocus = false;
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

            _bar = _box.AddUIComponent<UIScrollbar>();
            _bar.size = new Vector2(10f, _box.height - 6f);
            _bar.relativePosition = new Vector3(_box.width - 13f, 3f);
            _bar.orientation = UIOrientation.Vertical;
            _bar.minValue = 0f;
            _bar.stepSize = 1f;
            _bar.incrementAmount = 3f;
            UISlicedSprite track = _bar.AddUIComponent<UISlicedSprite>();
            track.atlas = atlas;
            track.spriteName = "ScrollbarTrack";
            track.size = _bar.size;
            track.relativePosition = Vector3.zero;
            _bar.trackObject = track;
            UISlicedSprite handle = track.AddUIComponent<UISlicedSprite>();
            handle.atlas = atlas;
            handle.spriteName = "ScrollbarThumb";
            handle.size = new Vector2(10f, 30f);
            handle.relativePosition = Vector3.zero;
            _bar.thumbObject = handle;
            _bar.eventValueChanged += (component, value) =>
            {
                if (_refreshing)
                    return;
                _offset = Mathf.RoundToInt(value);
                FillRows();
            };
            y += _box.height + 8f;

            UIButton done = UIBuilder.AddButton(this, Localization.Get("Picker.Done"), new Vector2(PanelWidth - 24f, 28f), new Vector3(12f, y));
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
            _title.text = row.Label;
            _search.text = string.Empty;
            Filter();
            ScrollToChosen();

            if (beside != null)
            {
                // The screen as the UI sees it (1920 x 1080 only at 16:9: 1728 wide at 16:10).
                Vector2 screen = GetUIView().GetScreenResolution();
                Vector3 p = beside.relativePosition;
                float x = p.x + beside.width + 6f;
                if (x + width > screen.x)
                    x = Mathf.Max(0f, p.x - width - 6f);
                relativePosition = new Vector3(x, Mathf.Clamp(p.y + 40f, 0f, Mathf.Max(0f, screen.y - height)));
            }

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

            foreach (FlagCountry country in FlagCountries.ByName)
            {
                if (FlagCountries.Matches(country, query))
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
                button.text = country != null ? country.Name : Localization.Get("Country.None");

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
                button.normalBgSprite = lit ? "ButtonMenuFocused" : "ButtonMenu";
                button.focusedBgSprite = button.normalBgSprite;
                button.textColor = lit ? new Color32(255, 255, 255, 255) : new Color32(210, 225, 240, 255);
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
