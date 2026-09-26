using System;
using ColossalFramework.UI;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// The colour picker a flag's colour swatch opens, beside the panel: the colours the world's
    /// flags are made of (FlagColours.Palette), and hue, saturation and brightness for any other.
    /// Every change goes straight to the colour -- and so to the flag, for the selected one.
    /// </summary>
    /// <remarks>
    /// The game's own UIColorField lives in panels the game builds (the transport and team
    /// colour fields, read from the IL) with no template a mod can be sure of offline, so this
    /// is built from the components and sprites the rest of the panel already uses. The hue,
    /// saturation and brightness are the picker's own state while it is open: recomputing them
    /// from the colour would lose the hue of a grey the moment its saturation reached 0.
    /// </remarks>
    public class ColourPicker : UIPanel
    {
        private const float PanelWidth = 260f;
        private const float Swatch = 30f;
        private const int PerRow = 7;

        private static ColourPicker _instance;

        private Row _row;
        private UIComponent _beside;
        private bool _filled;
        private UILabel _title;
        private UIButton _preview;
        private UILabel _hex;
        private UISlider _hue, _saturation, _brightness;
        private bool _refreshing;
        private float _h, _s, _v;

        /// <summary>Opens the picker on a colour row, beside the panel; the same row again closes it.</summary>
        public static void Toggle(Row row, UIComponent beside)
        {
            if (_instance != null && _instance._row == row && _instance.isVisible)
            {
                Close();
                return;
            }

            if (_instance == null)
                _instance = UIView.GetAView().AddUIComponent(typeof(ColourPicker)) as ColourPicker;
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

        /// <summary>The row the picker is open on, for the panel's swatch highlight; null when closed.</summary>
        public static Row Editing
        {
            get { return _instance != null && _instance.isVisible ? _instance._row : null; }
        }

        public override void Start()
        {
            base.Start();

            atlas = UIBuilder.Atlas;
            backgroundSprite = "MenuPanel2";
            size = new Vector2(PanelWidth, 10f);
            canFocus = true;
            isInteractive = true;
            name = "RealFlagColourPicker";

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
            _preview = UIBuilder.AddSwatch(this, new Vector2(64f, 28f), new Vector3(12f, y), 0xFFFFFF);
            _preview.isInteractive = false;
            _hex = UIBuilder.AddLabel(this, "", new Vector3(86f, y + 6f), 0.85f);
            y += 38f;

            // The palette: the world's flag colours.
            for (int i = 0; i < FlagColours.Palette.Length; i++)
            {
                uint rgb = FlagColours.Palette[i];
                float x = 12f + (i % PerRow) * (Swatch + 3f);
                float sy = y + (i / PerRow) * (Swatch + 3f);
                UIButton swatch = UIBuilder.AddSwatch(this, new Vector2(Swatch, Swatch), new Vector3(x, sy), rgb);
                swatch.tooltip = FlagColours.Hex(rgb);
                swatch.eventClick += (component, e) => Pick(rgb);
            }

            y += ((FlagColours.Palette.Length + PerRow - 1) / PerRow) * (Swatch + 3f) + 6f;

            _hue = UIBuilder.AddCompactSlider(this, y, Localization.Get("Picker.Hue"), 70f, 44f, 0f, 359f, 1f, 0f,
                                              v => v.ToString("F0") + "°", v => Slide());
            y += 28f;
            _saturation = UIBuilder.AddCompactSlider(this, y, Localization.Get("Picker.Saturation"), 70f, 44f, 0f, 100f, 1f, 0f,
                                                     v => v.ToString("F0") + "%", v => Slide());
            y += 28f;
            _brightness = UIBuilder.AddCompactSlider(this, y, Localization.Get("Picker.Brightness"), 70f, 44f, 0f, 100f, 1f, 0f,
                                                     v => v.ToString("F0") + "%", v => Slide());
            y += 34f;

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
            uint rgb = row.Int != null ? (uint)row.Int.value : 0xFFFFFFu;
            FlagColours.ToHsv(rgb, out _h, out _s, out _v);
            ShowColour(rgb);

            if (beside != null)
            {
                UIView view = GetUIView();
                Vector3 p = beside.relativePosition;
                float x = p.x + beside.width + 6f;
                if (x + width > view.fixedWidth)
                    x = Mathf.Max(0f, p.x - width - 6f);
                relativePosition = new Vector3(x, Mathf.Clamp(p.y + 60f, 0f, Mathf.Max(0f, view.fixedHeight - height)));
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

        /// <summary>Shows a colour: the preview, its hex, and the sliders on the picker's own hue, saturation and brightness.</summary>
        private void ShowColour(uint rgb)
        {
            _refreshing = true;
            try
            {
                _title.text = _row != null ? _row.Label : "";
                UIBuilder.SetSwatch(_preview, rgb);
                _hex.text = FlagColours.Hex(rgb);
                _hue.value = _h;
                _saturation.value = _s * 100f;
                _brightness.value = _v * 100f;
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void Pick(uint rgb)
        {
            FlagColours.ToHsv(rgb, out _h, out _s, out _v);
            Store(rgb);
            ShowColour(rgb);
        }

        private void Slide()
        {
            if (_refreshing || _row == null)
                return;

            _h = _hue.value;
            _s = _saturation.value / 100f;
            _v = _brightness.value / 100f;
            uint rgb = FlagColours.FromHsv(_h, _s, _v);
            Store(rgb);

            UIBuilder.SetSwatch(_preview, rgb);
            _hex.text = FlagColours.Hex(rgb);
        }

        private void Store(uint rgb)
        {
            if (_row == null || _row.Int == null || !_row.IsEnabled)
                return;

            if ((uint)_row.Int.value == rgb)
                return;

            _row.Int.value = (int)rgb;
            try
            {
                if (_row.AfterChange != null)
                    _row.AfterChange();
            }
            catch (Exception e)
            {
                Log.Error("colour picker: applying the colour threw.", e);
            }

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
