using System.Collections.Generic;
using ColossalFramework.UI;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// A string's own list of countries (Countries, My list): the country picker's search and
    /// rows, a click adding a country to the end of the list or taking it out, each listed one
    /// lit with its place in the list -- the order "As picked" flies them in. Beside the String
    /// window; Clear empties the list.
    /// </summary>
    public class CountryListPicker : UIPanel
    {
        private const float PanelWidth = 300f;
        private const float RowHeight = 24f;
        private const int Rows = 12;

        private static CountryListPicker _instance;

        private StringTarget _target;
        private string _beside;
        private bool _filled;
        private ModsLibCS1.UI.Builder _ui;
        private UILabel _title;
        private UITextField _search;
        private UIScrollbar _bar;
        private UIPanel _box;
        private readonly UIButton[] _buttons = new UIButton[Rows];
        private readonly UITextureSprite[] _thumbs = new UITextureSprite[Rows];
        private readonly List<FlagCountry> _shown = new List<FlagCountry>();
        private int _offset;
        private bool _refreshing;

        public static void Toggle(StringTarget target, string beside)
        {
            if (_instance != null && _instance._target == target && _instance.isVisible)
            {
                Close();
                return;
            }

            if (_instance == null)
                _instance = UIView.GetAView().AddUIComponent(typeof(CountryListPicker)) as CountryListPicker;
            if (_instance == null)
                return;

            _instance.Show();
            _instance.Edit(target, beside);
        }

        public static void Close()
        {
            if (_instance != null)
                _instance.Hide();
        }

        public static void DestroyPicker()
        {
            if (_instance != null)
                Object.Destroy(_instance.gameObject);
            _instance = null;
        }

        public override void Start()
        {
            base.Start();

            _ui = PanelLook.NewBuilder("the countries' list");
            size = new Vector2(PanelWidth, 10f);
            canFocus = true;
            isInteractive = true;
            name = "RealFlagCountryListPicker";

            _title = _ui.AddFrame(this, string.Empty, Hide).Title;
            _title.textScale = 0.9f;

            float y = 46f;
            _ui.AddName(this, Localization.Get("Country.Search"), new Vector3(12f, y + 5f), 60f);
            _search = _ui.AddTextField(this, new Vector2(PanelWidth - 24f - 64f, 26f), new Vector3(12f + 64f, y), 40);
            _search.padding = new RectOffset(6, 6, 5, 3);
            _search.horizontalAlignment = UIHorizontalAlignment.Left;
            _search.eventTextChanged += (component, text) => Filter();
            y += 34f;

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

            float half = (PanelWidth - 24f - 8f) * 0.5f;
            UIButton clear = _ui.AddButton(this, Localization.Get("CountryList.Clear"), new Vector2(half, 28f), new Vector3(12f, y));
            clear.eventClick += (component, e) =>
            {
                if (_target == null || _target.Countries.Count == 0)
                    return;
                _target.Countries.Clear();
                Log.Msg("strings: " + _target.Whose() + "'s list of countries emptied");
                _target.CountriesChanged();
                FillRows();
                SettingsCatalog.RefreshAllUIs();
            };
            UIButton done = _ui.AddButton(this, Localization.Get("Picker.Done"), new Vector2(half, 28f), new Vector3(12f + half + 8f, y));
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

        private void Edit(StringTarget target, string beside)
        {
            _target = target;
            _beside = beside;
            _filled = false;
            if (_title == null)
                return;

            _filled = true;
            _title.text = _ui.Caps(Localization.Get("CountryList.Title"));
            _search.text = string.Empty;
            Filter();

            UIComponent owner = GetUIView().FindUIComponent(beside);
            if (owner != null)
                PanelLook.PlaceBeside(this, owner, 40f);
            BringToFront();
        }

        public override void Update()
        {
            base.Update();
            if (!_filled && _target != null && isVisible && _title != null)
                Edit(_target, _beside);
        }

        private void Filter()
        {
            if (_search == null)
                return;
            string query = _search.text ?? string.Empty;
            _shown.Clear();
            foreach (FlagCountry country in CountryNames.Order)
            {
                if (CountryNames.Matches(country, query))
                    _shown.Add(country);
            }

            _offset = 0;
            FillRows();
        }

        private void Scroll(int rows)
        {
            _offset = Mathf.Clamp(_offset + rows, 0, Mathf.Max(0, _shown.Count - Rows));
            FillRows();
        }

        private void FillRows()
        {
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
                int place = _target != null ? _target.Countries.IndexOf(country.Code) : -1;
                button.text = (place >= 0 ? (place + 1) + ". " : string.Empty) + CountryNames.Of(country);

                Texture2D texture = FlagImages.Thumbnail(country.Image);
                thumb.texture = texture;
                thumb.isVisible = texture != null;
                if (texture != null)
                {
                    float scale = Mathf.Min(34f / Mathf.Max(1, texture.width), 18f / Mathf.Max(1, texture.height));
                    thumb.size = new Vector2(Mathf.Round(texture.width * scale), Mathf.Round(texture.height * scale));
                    thumb.relativePosition = new Vector3(Mathf.Round(4f + 0.5f * (34f - thumb.width)), Mathf.Round(0.5f * (RowHeight - thumb.height)));
                }

                PanelLook.SetListLit(_ui, button, place >= 0);
            }
        }

        private void Pick(int index)
        {
            int at = _offset + index;
            if (_target == null || at < 0 || at >= _shown.Count)
                return;

            string code = _shown[at].Code;
            int place = _target.Countries.IndexOf(code);
            if (place >= 0)
                _target.Countries.RemoveAt(place);
            else if (_target.Countries.Count < StringSpec.MaxListed)
                _target.Countries.Add(code);
            _target.CountriesChanged();
            FillRows();
            SettingsCatalog.RefreshAllUIs();
        }

        public override void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            base.OnDestroy();
        }
    }
}
