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
        private bool _refreshing;
        private float _h, _s, _v;

        /// <summary>The square's size (saturation across, brightness up) and its texture's, drawn smooth between.</summary>
        private const float FieldHeight = 132f;
        private const int FieldTexWidth = 128, FieldTexHeight = 64;
        private const float HueHeight = 14f;

        private OwnedTextureSprite _field, _hueBar, _fieldMark, _hueMark;
        private const int Dragging = 0, DragField = 1, DragHue = 2;
        private int _dragging = Dragging;

        /// <summary>The hue the square was last drawn for (it is drawn again only when the hue moves).</summary>
        private float _fieldHue = -1f;

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

            // Any other colour: a square of saturation (across) and brightness (up) for the hue, and
            // the hue strip under it, both dragged on (the author, 2026-11-11: "i don't like hue /
            // saturation / brightness. let's do a color picker instead" -- the three sliders went).
            float inner = PanelWidth - 24f;
            _field = AddUIComponent<OwnedTextureSprite>();
            _field.size = new Vector2(inner, FieldHeight);
            _field.relativePosition = new Vector3(12f, y);
            _field.isInteractive = true;
            HookDrag(_field, DragField);
            _fieldMark = AddMark(_field, MarkTexture(12, 12, true), new Vector2(12f, 12f));
            y += FieldHeight + 8f;

            _hueBar = AddUIComponent<OwnedTextureSprite>();
            _hueBar.size = new Vector2(inner, HueHeight);
            _hueBar.relativePosition = new Vector3(12f, y);
            _hueBar.isInteractive = true;
            _hueBar.Own = HueTexture();
            _hueBar.texture = _hueBar.Own;
            HookDrag(_hueBar, DragHue);
            _hueMark = AddMark(_hueBar, MarkTexture(4, (int)HueHeight + 6, false), new Vector2(4f, HueHeight + 6f));
            y += HueHeight + 12f;

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
                ShowMarks();
            }
            finally
            {
                _refreshing = false;
            }
        }

        /// <summary>The square drawn for the hue (again only when it moved), and both marks where the colour is.</summary>
        private void ShowMarks()
        {
            if (_field == null)
                return;

            if (Mathf.Abs(_h - _fieldHue) > 0.01f || _field.Own == null)
                DrawField();

            _fieldMark.relativePosition = new Vector3(Mathf.Round(_s * _field.width - 0.5f * _fieldMark.width),
                                                      Mathf.Round((1f - _v) * _field.height - 0.5f * _fieldMark.height));
            _hueMark.relativePosition = new Vector3(Mathf.Round(_h / 360f * _hueBar.width - 0.5f * _hueMark.width), -3f);
        }

        /// <summary>Saturation 0..1 across, brightness 0..1 up, for the picker's hue.</summary>
        private void DrawField()
        {
            Texture2D texture = _field.Own;
            if (texture == null)
            {
                texture = NewTexture(FieldTexWidth, FieldTexHeight, "RealFlag colour field");
                _field.Own = texture;
                _field.texture = texture;
            }

            var pixels = new Color32[FieldTexWidth * FieldTexHeight];
            for (int row = 0; row < FieldTexHeight; row++)
            {
                // A texture's first row is its bottom: brightness up.
                float v = row / (float)(FieldTexHeight - 1);
                for (int x = 0; x < FieldTexWidth; x++)
                    pixels[row * FieldTexWidth + x] = ToColour32(FlagColours.FromHsv(_h, x / (float)(FieldTexWidth - 1), v));
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
            _fieldHue = _h;
        }

        /// <summary>The hue strip: 0..360 degrees across at full saturation and brightness.</summary>
        private static Texture2D HueTexture()
        {
            const int width = 256;
            Texture2D texture = NewTexture(width, 2, "RealFlag hue strip");
            var pixels = new Color32[width * 2];
            for (int x = 0; x < width; x++)
                pixels[x] = pixels[width + x] = ToColour32(FlagColours.FromHsv(360f * x / (width - 1), 1f, 1f));
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        /// <summary>
        /// A mark on the square (a ring, white in a dark edge) or the strip (a bar), a child that
        /// lets the mouse through to its parent.
        /// </summary>
        private static Texture2D MarkTexture(int w, int h, bool ring)
        {
            Texture2D texture = NewTexture(w, h, "RealFlag colour mark");
            var pixels = new Color32[w * h];
            var white = new Color32(255, 255, 255, 255);
            var dark = new Color32(20, 24, 30, 230);
            var clear = new Color32(0, 0, 0, 0);
            float cx = 0.5f * (w - 1), cy = 0.5f * (h - 1), r = 0.5f * Mathf.Min(w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color32 c;
                    if (ring)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        c = d > r - 0.5f ? clear : d > r - 2f ? dark : d > r - 4f ? white : d > r - 5f ? dark : clear;
                    }
                    else
                    {
                        c = x == 0 || x == w - 1 || y == 0 || y == h - 1 ? dark : white;
                    }

                    pixels[y * w + x] = c;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
            return texture;
        }

        private static OwnedTextureSprite AddMark(UIComponent parent, Texture2D texture, Vector2 size)
        {
            OwnedTextureSprite mark = parent.AddUIComponent<OwnedTextureSprite>();
            mark.Own = texture;
            mark.texture = texture;
            mark.size = size;
            mark.isInteractive = false;
            return mark;
        }

        private static Texture2D NewTexture(int w, int h, string name)
        {
            return new Texture2D(w, h, TextureFormat.RGBA32, false, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        private static Color32 ToColour32(uint rgb)
        {
            return new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 255);
        }

        /// <summary>Press, drag and let go on the square or the strip (the Image window's crop view does the same).</summary>
        private void HookDrag(UIComponent target, int what)
        {
            target.eventMouseDown += (component, p) =>
            {
                if (!p.buttons.IsFlagSet(UIMouseButton.Left))
                    return;

                _dragging = what;
                DragAt(target, p);
                p.Use();
            };
            target.eventMouseMove += (component, p) =>
            {
                if (_dragging != what || !p.buttons.IsFlagSet(UIMouseButton.Left))
                    return;

                DragAt(target, p);
                p.Use();
            };
            target.eventMouseUp += (component, p) =>
            {
                if (_dragging != what)
                    return;

                _dragging = Dragging;
                if (_row != null && _row.Int != null)
                    Log.Msg("colour picker: " + _row.Label + " now " + FlagColours.Hex((uint)_row.Int.value));
            };
        }

        /// <summary>The pointer's place on the square (saturation, brightness) or the strip (hue), the colour taken from it.</summary>
        private void DragAt(UIComponent target, UIMouseEventParameter p)
        {
            Vector2 at;
            if (_row == null || !_row.IsEnabled || !Local(target, p.ray, out at))
                return;

            float across = Mathf.Clamp01(at.x / Mathf.Max(1f, target.width));
            if (_dragging == DragField)
            {
                _s = across;
                _v = 1f - Mathf.Clamp01(at.y / Mathf.Max(1f, target.height));
            }
            else
            {
                _h = Mathf.Min(across * 360f, 359.9f);
            }

            uint rgb = FlagColours.FromHsv(_h, _s, _v);
            Store(rgb);

            _refreshing = true;
            try
            {
                FlagWidgets.SetSwatch(_preview, rgb);
                _hex.text = FlagColours.Hex(rgb);
                ShowMarks();
            }
            finally
            {
                _refreshing = false;
            }
        }

        /// <summary>Where a ray meets a component, in its own units from its top left (ImagePanel's Local, for any component).</summary>
        private static bool Local(UIComponent target, Ray ray, out Vector2 at)
        {
            at = Vector2.zero;
            Vector3[] corners = target.GetCorners();
            if (corners == null || corners.Length < 4)
                return false;

            // Corners: top left, top right, bottom right, bottom left.
            Vector3 origin = corners[0], across = corners[1] - corners[0], down = corners[3] - corners[0];
            Vector3 normal = Vector3.Cross(across, down);
            float facing = Vector3.Dot(ray.direction, normal);
            if (Mathf.Abs(facing) < 1e-9f)
                return false;

            float t = Vector3.Dot(origin - ray.origin, normal) / facing;
            Vector3 hit = ray.origin + ray.direction * t - origin;
            at = new Vector2(Vector3.Dot(hit, across) / Mathf.Max(across.sqrMagnitude, 1e-12f) * target.width,
                             Vector3.Dot(hit, down) / Mathf.Max(down.sqrMagnitude, 1e-12f) * target.height);
            return true;
        }

        /// <summary>A colour chosen whole (the palette, the hex code): the sliders follow it.</summary>
        private void Pick(uint rgb, bool field = true)
        {
            FlagColours.ToHsv(rgb, out _h, out _s, out _v);
            Store(rgb);
            ShowColour(rgb, field);
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
