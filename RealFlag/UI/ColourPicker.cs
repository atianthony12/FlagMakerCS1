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
    ///
    /// The hex code is a text field (the author: "allow the user to copy/paste hex codes"):
    /// typed or pasted into (the game's UITextField has Ctrl+A/C/V/X, through
    /// ColossalFramework.Clipboard -- read from its IL), a whole #RRGGBB applies as it is typed,
    /// anything else when Enter is pressed or the field is left (FlagColours.FindHex: "#RGB", a
    /// code inside other text); Copy and Paste buttons do the same through the clipboard. While
    /// the field has the keyboard the game's camera and shortcuts leave the keys alone
    /// (CameraController asks ToolController.HasInputFocus, the shortcuts UIView.HasInputFocus).
    /// </remarks>
    public class ColourPicker : UIPanel
    {
        private const float PanelWidth = 300f;
        private const float Swatch = 30f;
        private const int PerRow = 7;

        private static ColourPicker _instance;

        private Row _row;
        private UIComponent _beside;
        private bool _filled;
        private UILabel _title;
        private UIButton _preview;
        private UITextField _hex;
        private ModsLibCS1.UI.Builder _ui;
        private ModsLibCS1.UI.SliderRow _hue, _saturation, _brightness;
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

            _ui = PanelLook.NewBuilder("the colour picker");
            size = new Vector2(PanelWidth, 10f);
            canFocus = true;
            isInteractive = true;
            name = "RealFlagColourPicker";

            // Background, title (the row's: ShowColour), close button and drag handle (ModsLibCS1).
            _title = _ui.AddFrame(this, string.Empty, Hide).Title;
            _title.textScale = 0.9f;

            float y = 46f;
            _preview = FlagWidgets.AddSwatch(this, new Vector2(44f, 28f), new Vector3(12f, y), 0xFFFFFF);
            _preview.isInteractive = false;
            _hex = AddHexField(new Vector3(62f, y), new Vector2(92f, 28f));

            UIButton copy = _ui.AddButton(this, Localization.Get("Picker.Copy"), new Vector2(60f, 28f), new Vector3(160f, y));
            copy.tooltip = Localization.Get("Picker.CopyTip");
            copy.eventClick += (component, e) => CopyHex();

            UIButton paste = _ui.AddButton(this, Localization.Get("Picker.Paste"), new Vector2(62f, 28f), new Vector3(226f, y));
            paste.tooltip = Localization.Get("Picker.PasteTip");
            paste.eventClick += (component, e) => PasteHex();
            y += 38f;

            // The palette: the world's flag colours, in the middle.
            float left = Mathf.Floor(0.5f * (PanelWidth - (PerRow * (Swatch + 3f) - 3f)));
            for (int i = 0; i < FlagColours.Palette.Length; i++)
            {
                uint rgb = FlagColours.Palette[i];
                float x = left + (i % PerRow) * (Swatch + 3f);
                float sy = y + (i / PerRow) * (Swatch + 3f);
                UIButton swatch = FlagWidgets.AddSwatch(this, new Vector2(Swatch, Swatch), new Vector3(x, sy), rgb);
                swatch.tooltip = FlagColours.Hex(rgb);
                swatch.eventClick += (component, e) => Pick(rgb);
            }

            y += ((FlagColours.Palette.Length + PerRow - 1) / PerRow) * (Swatch + 3f) + 6f;

            // Hue, saturation, brightness: the library's slider rows in a strip inside the margins,
            // a short name and a short readout.
            UIPanel sliders = AddUIComponent<UIPanel>();
            sliders.size = new Vector2(PanelWidth - 24f, 3f * 28f);
            sliders.relativePosition = new Vector3(12f, y - 2f);
            _ui.LabelWidth = 70f;
            _ui.ValueWidth = 48f;
            _hue = _ui.AddSlider(sliders, 0f, Localization.Get("Picker.Hue"), 0f, 359f, 1f, 0f,
                                 v => v.ToString("F0") + "°", v => Slide());
            _saturation = _ui.AddSlider(sliders, 28f, Localization.Get("Picker.Saturation"), 0f, 100f, 1f, 0f,
                                        v => v.ToString("F0") + "%", v => Slide());
            _brightness = _ui.AddSlider(sliders, 56f, Localization.Get("Picker.Brightness"), 0f, 100f, 1f, 0f,
                                        v => v.ToString("F0") + "%", v => Slide());
            y += 3f * 28f + 6f;

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

        /// <summary>The hex code's text field (the country picker's search field, dressed alike).</summary>
        private UITextField AddHexField(Vector3 position, Vector2 size)
        {
            // The library's field: Ctrl+A/C/V/X, Enter submits, Esc gives the edit up, a click elsewhere submits.
            UITextField field = _ui.AddTextField(this, size, position, 32);
            field.textScale = 0.9f;
            field.padding = new RectOffset(8, 6, 7, 3);
            field.horizontalAlignment = UIHorizontalAlignment.Left;
            field.tooltip = Localization.Get("Picker.HexTip");

            // A whole code applies as it is typed or pasted; anything else waits for Enter.
            field.eventTextChanged += (component, text) =>
            {
                uint rgb;
                if (!_refreshing && FlagColours.TryParseHex(text, out rgb))
                    Pick(rgb, false);
            };
            field.eventTextSubmitted += (component, text) => TakeHex(text);
            field.eventTextCancelled += (component, text) => ShowHex();
            return field;
        }

        /// <summary>The field's text, taken: the colour in it (FindHex), or the colour as it was.</summary>
        private void TakeHex(string text)
        {
            uint rgb;
            if (_row != null && FlagColours.FindHex(text, out rgb))
                Pick(rgb);
            else
                ShowHex();
        }

        /// <summary>The field back to the colour's own code.</summary>
        private void ShowHex()
        {
            if (_row == null || _hex == null)
                return;

            _refreshing = true;
            try
            {
                _hex.text = FlagColours.Hex(_row.Int != null ? (uint)_row.Int.value : 0xFFFFFFu);
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void CopyHex()
        {
            if (_row == null || _row.Int == null)
                return;

            string hex = FlagColours.Hex((uint)_row.Int.value);
            try
            {
                ColossalFramework.Clipboard.text = hex;
            }
            catch (Exception)
            {
                GUIUtility.systemCopyBuffer = hex;
            }

            Log.Msg("colour picker: " + hex + " copied");
        }

        private void PasteHex()
        {
            string text;
            try
            {
                text = ColossalFramework.Clipboard.text;
            }
            catch (Exception)
            {
                text = GUIUtility.systemCopyBuffer;
            }

            uint rgb;
            if (_row != null && _row.IsEnabled && FlagColours.FindHex(text, out rgb))
            {
                Pick(rgb);
                Log.Msg("colour picker: " + FlagColours.Hex(rgb) + " pasted");
            }
            else
            {
                Log.Msg("colour picker: nothing like a colour on the clipboard");
            }
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

            PanelLook.PlaceBeside(this, beside, 60f);
            BringToFront();
        }

        public override void Update()
        {
            base.Update();

            // Opened before Start ran: fill it in now.
            if (!_filled && _row != null && isVisible && _title != null)
                Edit(_row, _beside);
        }

        /// <summary>
        /// Shows a colour: the preview, its hex (unless it is being typed in), and the sliders on
        /// the picker's own hue, saturation and brightness.
        /// </summary>
        private void ShowColour(uint rgb, bool field = true)
        {
            _refreshing = true;
            try
            {
                _title.text = _ui.Caps(_row != null ? _row.Label : "");
                FlagWidgets.SetSwatch(_preview, rgb);
                if (field)
                    _hex.text = FlagColours.Hex(rgb);
                _hue.SetValue(_h);
                _saturation.SetValue(_s * 100f);
                _brightness.SetValue(_v * 100f);
            }
            finally
            {
                _refreshing = false;
            }
        }

        /// <summary>A colour chosen whole (the palette, the hex code): the sliders follow it.</summary>
        private void Pick(uint rgb, bool field = true)
        {
            FlagColours.ToHsv(rgb, out _h, out _s, out _v);
            Store(rgb);
            ShowColour(rgb, field);
        }

        private void Slide()
        {
            if (_refreshing || _row == null)
                return;

            _h = _hue.Slider.value;
            _s = _saturation.Slider.value / 100f;
            _v = _brightness.Slider.value / 100f;
            uint rgb = FlagColours.FromHsv(_h, _s, _v);
            Store(rgb);

            _refreshing = true;
            try
            {
                FlagWidgets.SetSwatch(_preview, rgb);
                _hex.text = FlagColours.Hex(rgb);
            }
            finally
            {
                _refreshing = false;
            }
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
