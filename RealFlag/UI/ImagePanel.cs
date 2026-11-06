using System;
using System.Collections.Generic;
using ColossalFramework.UI;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// The Image window a design's image row opens, beside its window: the pictures in the
    /// images folder (its button opens the folder: the author's "a folder icon which opens where
    /// the user should drag images"), a list of them by name with a thumbnail each ("a dropdown
    /// with the filenames and it can be selected"), and under it where the picked one lies on the
    /// flag -- "crop the image so its not stretched": the flag's frame as it is seen with the
    /// picture in it, what is cut off dimmed round it; a drag moves the picture, the wheel and the
    /// slider zoom it, and Fill, Whole picture, Turn and the flag taking the picture's shape. A
    /// picked picture shows on the flag at once; Done, Esc or the X closes it.
    /// </summary>
    /// <remarks>
    /// The folder is looked at again every second while the window is open (FlagImageFolder.Scan):
    /// pictures dropped in come into the list. The list is the country picker's -- rows of buttons
    /// over a scrollbar, filled from the list at its offset -- and its thumbnails (the panel's
    /// copies, FlagImages.UserPreview) are made one a frame, the rows in view only: a folder of
    /// big photographs decodes a picture a frame, not all at once. The crop view is drawn into a
    /// texture of its own from the panel's copy's pixels (FlagCropView), at the screen's pixels
    /// when still, at the UI's while dragged.
    /// </remarks>
    public class ImagePanel : UIPanel
    {
        private const float PanelWidth = 320f;
        private const float Margin = 12f;
        private const float Inner = PanelWidth - 2f * Margin;
        private const float RowHeight = 24f;
        private const int Rows = 7;
        private const float CanvasHeight = 176f;
        private const float FrameMargin = 18f;
        private const float ScanInterval = 1f;

        /// <summary>What is left of the picture outside the flag's frame.</summary>
        private const float Dim = 0.35f;

        /// <summary>The zoom slider's steps: logarithmic, FlagCrop.MinZoom to MaxZoom.</summary>
        private const float ZoomSteps = 1000f;

        private static readonly Color32 Back = new Color32(28, 31, 36, 255);
        private static readonly Color32 Edge = new Color32(235, 235, 235, 255);

        private static ImagePanel _instance;

        private ImageTarget _target;
        private UIComponent _beside;
        private bool _built, _filled;

        private ModsLibCS1.UI.Builder _ui;
        private UILabel _title, _hint, _dragHint, _seeThrough;
        private UIPanel _box;
        private UIScrollbar _bar;
        private readonly UIButton[] _buttons = new UIButton[Rows];
        private readonly UITextureSprite[] _thumbs = new UITextureSprite[Rows];
        private UITextureSprite _canvas;
        private Texture2D _canvasTexture;
        private Color32[] _canvasPixels;
        private ModsLibCS1.UI.SliderRow _zoom;
        private UIButton _fill, _whole, _turn, _shape, _cut, _through;
        private readonly List<UIComponent> _cropParts = new List<UIComponent>();
        private readonly List<UIComponent> _alphaParts = new List<UIComponent>();

        /// <summary>The names the list shows, in order; "" stands for none.</summary>
        private readonly List<string> _shown = new List<string>();
        private int _offset;
        private int _listedVersion = -1;
        private string _listedChosen;
        private bool _refreshing;
        private bool _thumbsWanted;
        private float _nextScan;
        private bool _enabled = true, _cropEnabled = true, _alphaEnabled = true;

        /// <summary>Until then the view is drawn at the UI's pixels (a drag, the wheel, the slider moving it), then sharp again.</summary>
        private float _quickUntil;

        // The crop view as last drawn, and the pixels of the picture it was drawn from.
        private string _viewKey;
        private Texture2D _pixelsOf;
        private Color32[] _pixels;

        // A drag on the crop view: where it began on the view, and the window's middle then.
        private bool _dragging;
        private Vector2 _dragFrom;
        private float _dragX, _dragY;

        /// <summary>Opens the window on a design's picture, beside <paramref name="beside"/>; the same one again closes it.</summary>
        public static void Toggle(ImageTarget target, UIComponent beside)
        {
            if (_instance != null && _instance.isVisible && _instance._target == target)
            {
                Close();
                return;
            }

            if (_instance == null)
                _instance = UIView.GetAView().AddUIComponent(typeof(ImagePanel)) as ImagePanel;
            if (_instance == null)
                return;

            PatternPanel.Close();
            _instance.Show();
            _instance.Edit(target, beside);
        }

        public static void Close()
        {
            if (_instance != null)
                _instance.Hide();
        }

        /// <summary>Closes it if it shows the picture of <paramref name="page"/>'s design (that window went away).</summary>
        public static void CloseIf(PanelPage page)
        {
            if (_instance != null && _instance.isVisible && _instance._target != null && _instance._target.Page == page)
                Close();
        }

        /// <summary>With the panel it belongs to.</summary>
        public static void DestroyPanel()
        {
            if (_instance != null)
                UnityEngine.Object.Destroy(_instance.gameObject);
            _instance = null;
        }

        /// <summary>Re-reads its controls, if it is open (the panel's refresh calls it).</summary>
        public static void RefreshOpen()
        {
            if (_instance != null && _instance.isVisible && _instance._filled)
                _instance.RefreshAll();
        }

        public override void Start()
        {
            base.Start();

            _ui = PanelLook.NewBuilder("the Image window");
            size = new Vector2(PanelWidth, 10f);
            canFocus = true;
            isInteractive = true;
            name = "RealFlagImagePanel";

            // Background, title (the window's: Edit), close button and drag handle (ModsLibCS1).
            _title = _ui.AddFrame(this, string.Empty, Hide).Title;
            _title.textScale = 0.9f;

            float y = 46f;

            // The folder: the button, and where it is.
            UIButton open = _ui.AddButton(this, Localization.Get("Image.Open"), new Vector2(Inner, 28f), new Vector3(Margin, y));
            open.textHorizontalAlignment = UIHorizontalAlignment.Left;
            open.textPadding = new RectOffset(36, 8, 7, 0);
            open.tooltip = Localization.Get("Image.OpenTip");
            new FolderIcon(open, new Vector3(10f, 7f), new Vector2(18f, 14f));
            open.eventClick += (component, e) =>
            {
                FlagImageFolder.Open();
                FlagImageFolder.Scan();
                Filter();
            };
            y += 32f;

            _hint = _ui.AddLabel(this, Localization.Get("Image.Hint"), new Vector3(Margin, y), 0.7f);
            _hint.autoSize = false;
            _hint.wordWrap = true;
            _hint.size = new Vector2(Inner, 30f);
            _ui.Paint(_hint, _ui.Theme.Status);
            _hint.tooltip = FlagImageFolder.FolderPath;
            y += 34f;

            // The list: a dark box of rows, the scrollbar at its right.
            _box = AddUIComponent<UIPanel>();
            PanelLook.DressListBox(_ui, _box);
            _box.size = new Vector2(Inner, RowHeight * Rows + 6f);
            _box.relativePosition = new Vector3(Margin, y);
            _box.eventMouseWheel += (component, p) =>
            {
                Scroll(p.wheelDelta > 0 ? -2 : 2);
                p.Use();
            };

            float rowWidth = _box.width - 18f;
            for (int i = 0; i < Rows; i++)
            {
                UIButton button = _box.AddUIComponent<UIButton>();
                PanelLook.DressListRow(_ui, button);
                button.size = new Vector2(rowWidth, RowHeight);
                button.relativePosition = new Vector3(3f, 3f + i * RowHeight);
                button.textScale = 0.72f;
                button.textHorizontalAlignment = UIHorizontalAlignment.Left;
                button.textPadding = new RectOffset(44, 6, 5, 0);
                int index = i;
                button.eventClick += (component, e) => Pick(index);
                button.eventMouseWheel += (component, p) =>
                {
                    Scroll(p.wheelDelta > 0 ? -2 : 2);
                    p.Use();
                };

                UITextureSprite thumb = button.AddUIComponent<UITextureSprite>();
                thumb.isInteractive = false;

                _buttons[i] = button;
                _thumbs[i] = thumb;
            }

            float barWidth = PanelLook.ListBarWidth(_ui);
            _bar = PanelLook.AddListBar(_ui, _box, new Vector2(barWidth, _box.height - 6f), new Vector3(_box.width - 3f - barWidth, 3f));
            _bar.incrementAmount = 2f;
            _bar.eventValueChanged += (component, value) =>
            {
                if (_refreshing)
                    return;
                _offset = Mathf.RoundToInt(value);
                FillRows();
            };
            y += _box.height + 8f;

            // Where it lies on the flag.
            _dragHint = _ui.AddLabel(this, Localization.Get("Image.Drag"), new Vector3(Margin, y), 0.7f);
            _dragHint.autoSize = false;
            _dragHint.size = new Vector2(Inner, 16f);
            _ui.Paint(_dragHint, _ui.Theme.Status);
            _cropParts.Add(_dragHint);
            y += 18f;

            _canvas = AddUIComponent<UITextureSprite>();
            _canvas.size = new Vector2(Inner, CanvasHeight);
            _canvas.relativePosition = new Vector3(Margin, y);
            _canvas.isInteractive = true;
            _canvas.tooltip = Localization.Get("Image.DragTip");
            _canvas.eventMouseDown += (component, p) => BeginDrag(p);
            _canvas.eventMouseMove += (component, p) => DragTo(p);
            _canvas.eventMouseUp += (component, p) => EndDrag();
            _canvas.eventMouseWheel += (component, p) =>
            {
                WheelZoom(p);
                p.Use();
            };
            y += CanvasHeight + 6f;

            // The zoom: the library's slider row in a strip inside the margins, its name and readout short.
            UIPanel zoomRow = AddUIComponent<UIPanel>();
            zoomRow.size = new Vector2(Inner, 28f);
            zoomRow.relativePosition = new Vector3(Margin, y - 2f);
            _ui.LabelWidth = 56f;
            _ui.ValueWidth = 58f;
            _zoom = _ui.AddSlider(zoomRow, 0f, Localization.Get("Image.Zoom"), 0f, ZoomSteps, 1f, SliderOf(1f),
                v => ZoomText(ZoomOf(v)), v =>
                {
                    if (_refreshing || _target == null || !_cropEnabled)
                        return;
                    SetZoom(ZoomOf(v), 0.5f, 0.5f);
                });
            foreach (UIComponent part in _zoom.Parts)
            {
                part.tooltip = Localization.Get("Image.ZoomTip");
                _cropParts.Add(part);
            }
            y += 30f;

            float third = (Inner - 12f) / 3f;
            _fill = _ui.AddButton(this, Localization.Get("Image.Fill"), new Vector2(third, 28f), new Vector3(Margin, y));
            _fill.tooltip = Localization.Get("Image.FillTip");
            _fill.eventClick += (component, e) => Fill();
            _whole = _ui.AddButton(this, Localization.Get("Image.Whole"), new Vector2(third, 28f), new Vector3(Margin + third + 6f, y));
            _whole.tooltip = Localization.Get("Image.WholeTip");
            _whole.eventClick += (component, e) => Whole();
            _turn = _ui.AddButton(this, Localization.Get("Image.Turn"), new Vector2(third, 28f), new Vector3(Margin + 2f * (third + 6f), y));
            _turn.tooltip = Localization.Get("Image.TurnTip");
            _turn.eventClick += (component, e) =>
            {
                if (_target != null && _cropEnabled)
                    _target.TurnQuarter();
            };
            _cropParts.Add(_fill);
            _cropParts.Add(_whole);
            _cropParts.Add(_turn);
            y += 32f;

            _shape = _ui.AddButton(this, "", new Vector2(Inner, 28f), new Vector3(Margin, y));
            _shape.tooltip = Localization.Get("Image.ShapeTip");
            _shape.eventClick += (component, e) => TakeShape();
            _cropParts.Add(_shape);
            y += 34f;

            // What its see-through parts do.
            _seeThrough = _ui.AddName(this, Localization.Get("Image.SeeThrough"), new Vector3(Margin, y + 4f), 108f);
            float half = (Inner - 112f - 6f) * 0.5f;
            _cut = _ui.AddButton(this, Localization.Get("Image.Cut"), new Vector2(half, 26f), new Vector3(Margin + 112f, y));
            _cut.tooltip = Localization.Get("Image.CutTip");
            _cut.eventClick += (component, e) =>
            {
                if (_target != null && _alphaEnabled)
                    _target.SetAlpha(FlagImageAlpha.Cut);
            };
            _through = _ui.AddButton(this, Localization.Get("Image.Through"), new Vector2(half, 26f), new Vector3(Margin + 112f + half + 6f, y));
            _through.tooltip = Localization.Get("Image.ThroughTip");
            _through.eventClick += (component, e) =>
            {
                if (_target != null && _alphaEnabled)
                    _target.SetAlpha(FlagImageAlpha.Through);
            };
            _alphaParts.Add(_seeThrough);
            _alphaParts.Add(_cut);
            _alphaParts.Add(_through);
            y += 34f;

            UIButton done = _ui.AddButton(this, Localization.Get("Picker.Done"), new Vector2(Inner, 28f), new Vector3(Margin, y));
            done.eventClick += (component, e) => Hide();
            y += 38f;

            height = y;
            _built = true;

            eventVisibilityChanged += (component, visible) =>
            {
                if (!visible)
                    EndDrag();
            };

            eventKeyDown += (component, p) =>
            {
                if (p.used || p.keycode != KeyCode.Escape)
                    return;

                p.Use();
                Hide();
            };
        }

        private void Edit(ImageTarget target, UIComponent beside)
        {
            if (_target != target)
                _viewKey = null;

            _target = target;
            _beside = beside;
            _filled = false;
            if (!_built)
                return; // Start has not run yet: Update fills it in on the next frame

            _filled = true;
            _title.text = _ui.Caps(Localization.Get(target.Page == PanelPage.New ? "Image.TitleNew" : "Image.TitleExisting"));
            FlagImageFolder.Scan();
            _nextScan = Time.realtimeSinceStartup + ScanInterval;
            Filter();
            ScrollToChosen();

            PanelLook.PlaceBeside(this, beside, 40f);
            RefreshAll();
            BringToFront();
        }

        public override void Update()
        {
            base.Update();

            if (!isVisible || _target == null)
                return;

            // Opened before Start ran: filled in now.
            if (!_filled && _built)
            {
                Edit(_target, _beside);
                return;
            }

            // A drag ends when the button is let go, wherever that was.
            if (_dragging && !Input.GetMouseButton(0))
                EndDrag();

            float now = Time.realtimeSinceStartup;

            // Moved a moment ago: sharp again now that it is still.
            if (!_dragging && _quickUntil > 0f && now >= _quickUntil)
            {
                _quickUntil = 0f;
                Draw(false);
            }

            if (now >= _nextScan)
            {
                _nextScan = now + ScanInterval;
                if (FlagImageFolder.Scan())
                    Filter();
            }

            if (_listedVersion != FlagImageFolder.Version)
                Filter();

            // One thumbnail a frame, the rows in view only.
            if (_thumbsWanted)
                MakeOneThumbnail();
        }

        // ---- the list ---------------------------------------------------------------------------

        /// <summary>The list again: none first, the folder's pictures, and the design's own if its file is not there.</summary>
        private void Filter()
        {
            if (!_built)
                return;

            _listedVersion = FlagImageFolder.Version;
            _shown.Clear();
            _shown.Add(string.Empty);
            string chosen = _target != null ? _target.Name : string.Empty;
            _listedChosen = chosen;
            if (chosen.Length > 0 && FlagImageFolder.Find(chosen) == null)
                _shown.Add(chosen);
            foreach (FlagImageFolder.Entry e in FlagImageFolder.Entries)
                _shown.Add(e.Name);

            _offset = Mathf.Clamp(_offset, 0, Mathf.Max(0, _shown.Count - Rows));
            _hint.text = Localization.Get(_shown.Count > 1 || chosen.Length > 0 ? "Image.Hint" : "Image.Empty");
            FillRows();
        }

        private void ScrollToChosen()
        {
            string chosen = _target != null ? _target.Name : string.Empty;
            int at = IndexOf(chosen);
            if (at >= Rows)
            {
                _offset = Mathf.Clamp(at - Rows / 2, 0, Mathf.Max(0, _shown.Count - Rows));
                FillRows();
            }
        }

        private int IndexOf(string name)
        {
            for (int i = 0; i < _shown.Count; i++)
            {
                if (string.Equals(_shown[i], name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        private void Scroll(int rows)
        {
            _offset = Mathf.Clamp(_offset + rows, 0, Mathf.Max(0, _shown.Count - Rows));
            FillRows();
        }

        /// <summary>The rows from the list at the offset: names and sizes, the thumbnails made so far, the chosen one lit.</summary>
        private void FillRows()
        {
            if (!_built)
                return;

            string chosen = _target != null ? _target.Name : string.Empty;
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

            _thumbsWanted = false;
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
                string name = _shown[at];
                FlagImageFolder.Entry entry = name.Length > 0 ? FlagImageFolder.Find(name) : null;
                if (name.Length == 0)
                    button.text = Localization.Get("Image.NoneRow");
                else if (entry == null)
                    button.text = Localization.Get("Image.Missing", name);
                else if (entry.Readable)
                    button.text = Localization.Get("Image.Row", name, entry.Width, entry.Height);
                else
                    button.text = name;

                // A thumbnail only once it is made (MakeOneThumbnail makes them a frame apart).
                Texture2D texture = null;
                if (entry != null)
                {
                    if (FlagImages.HasUserPreview(name))
                        texture = FlagImages.UserPreview(name);
                    else
                        _thumbsWanted = true;
                }

                thumb.texture = texture;
                thumb.isVisible = texture != null;
                if (texture != null)
                {
                    float scale = Mathf.Min(34f / Mathf.Max(1, texture.width), 18f / Mathf.Max(1, texture.height));
                    thumb.size = new Vector2(Mathf.Max(1f, Mathf.Round(texture.width * scale)), Mathf.Max(1f, Mathf.Round(texture.height * scale)));
                    thumb.relativePosition = new Vector3(Mathf.Round(4f + 0.5f * (34f - thumb.width)), Mathf.Round(0.5f * (RowHeight - thumb.height)));
                }

                bool lit = string.Equals(name, chosen, StringComparison.OrdinalIgnoreCase);
                PanelLook.SetListLit(_ui, button, lit);
                button.isEnabled = _enabled;
            }
        }

        /// <summary>The first thumbnail in view not made yet, made; the rows shown again once it is.</summary>
        private void MakeOneThumbnail()
        {
            _thumbsWanted = false;
            for (int i = 0; i < Rows; i++)
            {
                int at = _offset + i;
                if (at >= _shown.Count)
                    break;

                string name = _shown[at];
                if (name.Length == 0 || FlagImages.HasUserPreview(name) || FlagImageFolder.Find(name) == null)
                    continue;

                FlagImages.UserPreview(name);
                FillRows();
                return;
            }
        }

        private void Pick(int index)
        {
            int at = _offset + index;
            if (_target == null || !_target.IsEnabled || at < 0 || at >= _shown.Count)
                return;

            try
            {
                _target.Pick(_shown[at]);
            }
            catch (Exception e)
            {
                Log.Error("image window: applying the picture threw.", e);
            }

            FillRows();
        }

        // ---- the crop view ----------------------------------------------------------------------

        /// <summary>Everything read again from the design: the list's lit row, the view, the slider, the buttons.</summary>
        private void RefreshAll()
        {
            if (!_built || _target == null)
                return;

            string name = _target.Name;
            bool enabled = _target.IsEnabled;
            Texture2D preview = name.Length > 0 ? FlagImages.UserPreview(name) : null;
            bool crop = enabled && preview != null;
            FlagImageFolder.Entry entry = name.Length > 0 ? FlagImageFolder.Find(name) : null;
            bool alpha = crop && (entry == null || !entry.Readable || entry.Alpha);

            // The lit row follows the design (a pick, a country chosen instead, another flag selected).
            if (enabled != _enabled || !string.Equals(name, _listedChosen, StringComparison.OrdinalIgnoreCase))
            {
                _enabled = enabled;
                _listedChosen = name;
                Filter();
            }

            SetEnabled(_cropParts, crop, ref _cropEnabled);
            SetEnabled(_alphaParts, alpha, ref _alphaEnabled);

            _refreshing = true;
            try
            {
                float zoom = FlagCrop.ClampZoom(_target.Zoom.value);
                float slider = SliderOf(zoom);
                if (!Mathf.Approximately(_zoom.Slider.value, slider))
                    _zoom.SetValue(slider);

                // The zoom itself, not the slider's step nearest it.
                _zoom.Value.text = ZoomText(zoom);
            }
            finally
            {
                _refreshing = false;
            }

            int shape = _target.PictureShape();
            _shape.text = shape > 0 ? Localization.Get("Image.Shape", FlagProportion.Name(shape)) : Localization.Get("Image.ShapeNone");
            PanelLook.SetLit(_ui, _cut, _target.Alpha.value == FlagImageAlpha.Cut);
            PanelLook.SetLit(_ui, _through, _target.Alpha.value == FlagImageAlpha.Through);

            Draw(_dragging || _quickUntil > 0f);
        }

        private static void SetEnabled(List<UIComponent> parts, bool enabled, ref bool state)
        {
            if (enabled == state)
                return;

            state = enabled;
            foreach (UIComponent part in parts)
            {
                if (part == null)
                    continue;
                part.isEnabled = enabled;
                part.opacity = enabled ? 1f : 0.45f;
            }
        }

        /// <summary>
        /// The view drawn again if what it shows changed: at the screen's own pixels, or the UI's
        /// (<paramref name="quick"/>: while a drag or the wheel moves it).
        /// </summary>
        private void Draw(bool quick)
        {
            if (!_built || _target == null)
                return;

            float scale = quick ? 1f : Mathf.Clamp(PixelScale(), 1f, 2f);
            int vw = Mathf.Max(8, Mathf.RoundToInt(_canvas.width * scale));
            int vh = Mathf.Max(8, Mathf.RoundToInt(_canvas.height * scale));

            string name = _target.Name;
            Texture2D preview = name.Length > 0 ? FlagImages.UserPreview(name) : null;
            float seen = _target.Seen;
            int turn = _target.Turn.value & 3;
            float zoom = FlagCrop.ClampZoom(_target.Zoom.value);
            bool through = _target.Alpha.value == FlagImageAlpha.Through;
            uint field = (uint)_target.Colour1.value;
            string key = name + "|" + (preview != null ? preview.GetInstanceID() : 0) + "|" + turn + "|" + zoom + "|" + _target.X.value + "|" +
                         _target.Y.value + "|" + seen + "|" + through + "|" + field + "|" + vw + "x" + vh + "|" + _target.IsEnabled;
            if (key == _viewKey)
                return;
            _viewKey = key;

            if (_canvasTexture == null || _canvasTexture.width != vw || _canvasTexture.height != vh)
            {
                if (_canvasTexture != null)
                    UnityEngine.Object.Destroy(_canvasTexture);
                _canvasTexture = new Texture2D(vw, vh, TextureFormat.RGBA32, false, false)
                {
                    name = "RealFlag crop view",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _canvasPixels = new Color32[vw * vh];
            }

            float fx, fy, fw, fh;
            Frame(seen, out fx, out fy, out fw, out fh);

            Color32[] pixels = PixelsOf(preview);
            int pw = preview != null ? preview.width : 0, ph = preview != null ? preview.height : 0;
            int tw, th;
            FlagCrop.TurnedSize(Mathf.Max(1, pw), Mathf.Max(1, ph), turn, out tw, out th);
            float ww, wh;
            FlagCrop.Window(seen, tw, th, zoom, out ww, out wh);
            float cx = FlagCrop.ClampCentre(_target.X.value, ww), cy = FlagCrop.ClampCentre(_target.Y.value, wh);

            FlagCropView.Render(pixels, pw, ph, turn, ww, wh, cx, cy, through, _canvasPixels, vw, vh, fx * scale, fy * scale, fw * scale, fh * scale,
                                FlagWidgets.Swatch(field), Back, Edge, Dim);
            _canvasTexture.SetPixels32(_canvasPixels);
            _canvasTexture.Apply(false);
            _canvas.texture = _canvasTexture;
        }

        /// <summary>The picture's pixels, read once a texture (the panel's copy stays readable).</summary>
        private Color32[] PixelsOf(Texture2D preview)
        {
            if (preview == null)
                return null;

            if (_pixelsOf != preview || _pixels == null)
            {
                try
                {
                    _pixels = preview.GetPixels32();
                    _pixelsOf = preview;
                }
                catch (Exception e)
                {
                    Log.Warn("image window: could not read the picture's copy: " + e.Message);
                    _pixels = null;
                    _pixelsOf = null;
                }
            }

            return _pixels;
        }

        /// <summary>The flag's frame in the view (UI units from its top left): its shape as seen, as large as the view takes, in the middle.</summary>
        private void Frame(float seen, out float fx, out float fy, out float fw, out float fh)
        {
            float maxW = _canvas.width - 2f * FrameMargin, maxH = _canvas.height - 2f * FrameMargin;
            fw = maxW;
            fh = maxW * seen;
            if (fh > maxH)
            {
                fh = maxH;
                fw = maxH / Mathf.Max(seen, 1e-3f);
            }

            fw = Mathf.Max(4f, fw);
            fh = Mathf.Max(4f, fh);
            fx = 0.5f * (_canvas.width - fw);
            fy = 0.5f * (_canvas.height - fh);
        }

        /// <summary>Screen pixels a UI unit of the view (the game's UI is drawn larger on a larger screen).</summary>
        private float PixelScale()
        {
            try
            {
                Vector3[] corners = _canvas.GetCorners();
                Camera camera = GetUIView().uiCamera;
                if (camera == null || corners == null || corners.Length < 2)
                    return 1f;
                float pixels = Vector3.Distance(camera.WorldToScreenPoint(corners[0]), camera.WorldToScreenPoint(corners[1]));
                return pixels / Mathf.Max(1f, _canvas.width);
            }
            catch (Exception)
            {
                return 1f;
            }
        }

        /// <summary>Where the pointer's ray meets the view, UI units from its top left (outside it too, while a drag goes on).</summary>
        private bool Local(UIMouseEventParameter p, out Vector2 at)
        {
            at = Vector2.zero;
            Vector3[] corners = _canvas.GetCorners();
            if (corners == null || corners.Length < 4)
                return false;

            // Corners: top left, top right, bottom right, bottom left.
            Vector3 origin = corners[0], across = corners[1] - corners[0], down = corners[3] - corners[0];
            Vector3 normal = Vector3.Cross(across, down);
            Ray ray = p.ray;
            float facing = Vector3.Dot(ray.direction, normal);
            if (Mathf.Abs(facing) < 1e-9f)
                return false;

            float t = Vector3.Dot(origin - ray.origin, normal) / facing;
            Vector3 hit = ray.origin + ray.direction * t - origin;
            at = new Vector2(Vector3.Dot(hit, across) / Mathf.Max(across.sqrMagnitude, 1e-12f) * _canvas.width,
                             Vector3.Dot(hit, down) / Mathf.Max(down.sqrMagnitude, 1e-12f) * _canvas.height);
            return true;
        }

        private bool PictureTurned(out int tw, out int th)
        {
            tw = th = 0;
            Texture2D preview = _target != null && _target.Name.Length > 0 ? FlagImages.UserPreview(_target.Name) : null;
            if (preview == null)
                return false;
            FlagCrop.TurnedSize(preview.width, preview.height, _target.Turn.value, out tw, out th);
            return true;
        }

        private void BeginDrag(UIMouseEventParameter p)
        {
            if (_target == null || !_cropEnabled || !p.buttons.IsFlagSet(UIMouseButton.Left))
                return;

            Vector2 at;
            if (!Local(p, out at))
                return;

            _dragging = true;
            _dragFrom = at;
            _dragX = _target.X.value;
            _dragY = _target.Y.value;
            p.Use();
        }

        private void DragTo(UIMouseEventParameter p)
        {
            if (!_dragging || _target == null)
                return;

            int tw, th;
            Vector2 at;
            if (!PictureTurned(out tw, out th) || !Local(p, out at))
                return;

            float seen = _target.Seen, fx, fy, fw, fh;
            Frame(seen, out fx, out fy, out fw, out fh);
            float nx, ny;
            FlagCrop.Pan(seen, tw, th, _target.Zoom.value, _dragX, _dragY, (at.x - _dragFrom.x) / fw, (at.y - _dragFrom.y) / fh, out nx, out ny);
            if (Mathf.Approximately(nx, _target.X.value) && Mathf.Approximately(ny, _target.Y.value))
                return;

            _target.SetView(nx, ny, _target.Zoom.value);
            _quickUntil = Time.realtimeSinceStartup + 0.3f;
            _target.Apply();
            Draw(true);
        }

        private void EndDrag()
        {
            if (!_dragging)
                return;

            _dragging = false;
            if (_target != null)
                Log.Msg("flags: " + _target.Whose() + " image moved: its middle at " + _target.X.value.ToString("0.###") + " / " + _target.Y.value.ToString("0.###") +
                        " of it, " + ZoomText(_target.Zoom.value));
            Draw(false);
        }

        /// <summary>The wheel: a seventh in or out a notch, about the picture's point under the pointer.</summary>
        private void WheelZoom(UIMouseEventParameter p)
        {
            if (_target == null || !_cropEnabled || Mathf.Approximately(p.wheelDelta, 0f))
                return;

            float px = 0.5f, py = 0.5f;
            Vector2 at;
            if (Local(p, out at))
            {
                float fx, fy, fw, fh;
                Frame(_target.Seen, out fx, out fy, out fw, out fh);
                px = (at.x - fx) / fw;
                py = (at.y - fy) / fh;
            }

            SetZoom(FlagCrop.ClampZoom(_target.Zoom.value * Mathf.Pow(1.15f, Mathf.Sign(p.wheelDelta))), px, py);
        }

        /// <summary>A new zoom, about the point of the flag at (<paramref name="px"/>, <paramref name="py"/>) of its width and height.</summary>
        private void SetZoom(float zoom, float px, float py)
        {
            int tw, th;
            if (!PictureTurned(out tw, out th))
                return;

            float nx, ny;
            FlagCrop.ZoomAt(_target.Seen, tw, th, _target.Zoom.value, _target.X.value, _target.Y.value, zoom, px, py, out nx, out ny);
            _target.SetView(nx, ny, zoom);
            _quickUntil = Time.realtimeSinceStartup + 0.3f;
            _target.Apply();
            Draw(true);
        }

        private void Fill()
        {
            if (_target == null || !_cropEnabled)
                return;

            _target.SetView(0.5f, 0.5f, 1f);
            Log.Msg("flags: " + _target.Whose() + " image fills the flag");
            _target.Apply();
        }

        private void Whole()
        {
            int tw, th;
            if (_target == null || !_cropEnabled || !PictureTurned(out tw, out th))
                return;

            _target.SetView(0.5f, 0.5f, FlagCrop.WholeZoom(_target.Seen, tw, th));
            Log.Msg("flags: " + _target.Whose() + " image whole on the flag (" + ZoomText(_target.Zoom.value) + ")");
            _target.Apply();
        }

        private void TakeShape()
        {
            if (_target == null || !_cropEnabled)
                return;

            if (_target.TakeShape())
            {
                Log.Msg("flags: " + _target.Whose() + " shape taken from its image: " + FlagProportion.Name(_target.PictureShape()));
                _target.Apply();
            }
        }

        private static float ZoomOf(float slider)
        {
            // Its steps land either side of 100% (99.8, 100.25): near it, exactly it -- the picture
            // just filling the flag, as Fill leaves it (the log's "middle at 0.501" was that sliver).
            float zoom = FlagCrop.MinZoom * Mathf.Pow(FlagCrop.MaxZoom / FlagCrop.MinZoom, Mathf.Clamp01(slider / ZoomSteps));
            return Mathf.Abs(zoom - 1f) < 0.01f ? 1f : zoom;
        }

        private static float SliderOf(float zoom)
        {
            return ZoomSteps * Mathf.Log(FlagCrop.ClampZoom(zoom) / FlagCrop.MinZoom) / Mathf.Log(FlagCrop.MaxZoom / FlagCrop.MinZoom);
        }

        private static string ZoomText(float zoom)
        {
            return Localization.Get("Unit.Percent", (zoom * 100f).ToString("F0"));
        }

        public override void OnDestroy()
        {
            if (_canvasTexture != null)
                UnityEngine.Object.Destroy(_canvasTexture);
            _canvasTexture = null;

            if (_instance == this)
                _instance = null;

            base.OnDestroy();
        }
    }
}
