using System;
using System.Collections.Generic;
using ColossalFramework.UI;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// The Pattern window a design's pattern row opens, beside its window (the author: "The
    /// Pattern options are taking too much space ... a panel for the pattern, and allow the user
    /// to save patterns and load them ... a preview pane for the pattern like we do for the flag
    /// image import"): a flat preview of the flag at its shape, the fifteen patterns as little
    /// flags in its colours, its three colours, and the saved patterns -- a click on one applies
    /// it, as a country does. Save keeps what the flag has under the name in the field; a saved
    /// one's name in the field is the one Delete deletes.
    /// </summary>
    /// <remarks>
    /// The picture is FlagPatternView's, plain pixels, not the shader's cloth: the author's "just a
    /// 2D sprite". The window watches what it shows every frame instead of being told, so a colour
    /// picker's drag or a flag newly selected shows at once: the preview at the UI's pixels while it
    /// changes and sharp again once still (as the Image window's crop view), the little flags a
    /// few a frame. The colours are the catalog's rows on two pages (PatternNew, PatternExisting),
    /// built and refreshed by a <see cref="RowSet"/> as the Flagpole popup's are.
    /// </remarks>
    public class PatternPanel : UIPanel
    {
        private const float PanelWidth = 340f;
        private const float Margin = 12f;
        private const float Inner = PanelWidth - 2f * Margin;
        private const float PreviewHeight = 124f;
        private const float PreviewMargin = 10f;

        private const int Columns = 5;
        private const float CellGap = 4f;
        private const float CellHeight = 40f;
        private const float CellThumbWidth = 45f, CellThumbHeight = 30f;

        /// <summary>The little flags' shape, whatever the flag's: 2:3, hoisted from the side.</summary>
        private const int CellProportion = 667;

        /// <summary>Little flags drawn in a frame: 15 in one would be some 90 000 pixels a colour change.</summary>
        private const int CellsPerFrame = 3;

        private const float ListRowHeight = 24f;
        private const int ListRows = 5;
        private const float ListThumbWidth = 30f, ListThumbHeight = 20f;

        private const float ScanInterval = 1f;

        /// <summary>The preview stays at the UI's pixels this long after it last changed, then is drawn sharp.</summary>
        private const float QuickTime = 0.3f;

        /// <summary>A greyed part's opacity, as RowSet's rows.</summary>
        private const float Greyed = 0.45f;

        private static readonly Color32 Back = new Color32(28, 31, 36, 255);

        private static PatternPanel _instance;

        private PatternTarget _target;
        private UIComponent _beside;
        private bool _built, _filled;

        private ModsLibCS1.UI.Builder _ui;
        private UILabel _title, _empty;
        private UITextureSprite _canvas;
        private Texture2D _canvasTexture;
        private Color32[] _canvasPixels;

        private readonly UIButton[] _cells = new UIButton[FlagPattern.Values.Length];
        private readonly PatternThumb[] _cellThumbs = new PatternThumb[FlagPattern.Values.Length];
        private readonly int[] _cellLit = new int[FlagPattern.Values.Length];

        private RowSet _rows;
        private UIPanel _coloursNew, _coloursExisting;

        private UIPanel _box;
        private UIScrollbar _bar;
        private readonly UIButton[] _buttons = new UIButton[ListRows];
        private readonly PatternThumb[] _thumbs = new PatternThumb[ListRows];
        private UITextField _name;
        private UIButton _save, _delete;

        private int _offset, _listedVersion = -1;
        private bool _refreshing;
        private bool _enabled = true;
        private float _nextScan, _quickUntil;

        // What the preview last showed, and at what size.
        private int _shownPattern = -1, _shownProportion, _shownHoist;
        private uint _shownC1, _shownC2, _shownC3;
        private bool _shownEnabled;
        private bool _shownSharp;

        /// <summary>Opens the window on a design's pattern, beside <paramref name="beside"/>; the same one again closes it.</summary>
        public static void Toggle(PatternTarget target, UIComponent beside)
        {
            if (_instance != null && _instance.isVisible && _instance._target == target)
            {
                Close();
                return;
            }

            if (_instance == null)
                _instance = UIView.GetAView().AddUIComponent(typeof(PatternPanel)) as PatternPanel;
            if (_instance == null)
                return;

            // They open in the same place.
            ImagePanel.Close();
            PolePanel.Close();

            _instance.Show();
            _instance.Edit(target, beside);
        }

        public static void Close()
        {
            if (_instance != null)
                _instance.Hide();
        }

        /// <summary>Closes it if it shows the pattern of <paramref name="page"/>'s design (that window went away).</summary>
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

            _ui = PanelLook.NewBuilder("the Pattern window");
            size = new Vector2(PanelWidth, 10f);
            canFocus = true;
            isInteractive = true;
            name = "RealFlagPatternPanel";

            // Background, title (the page's: Edit), close button and drag handle (ModsLibCS1).
            _title = _ui.AddFrame(this, string.Empty, Hide).Title;
            _title.textScale = 0.9f;

            float y = 46f;

            _canvas = AddUIComponent<UITextureSprite>();
            _canvas.size = new Vector2(Inner, PreviewHeight);
            _canvas.relativePosition = new Vector3(Margin, y);
            _canvas.isInteractive = true; // for its tooltip: the pattern's name
            y += PreviewHeight + 8f;

            // The patterns: a little flag each, the one the flag has lit.
            float cellWidth = Mathf.Floor((Inner - (Columns - 1) * CellGap) / Columns);
            for (int i = 0; i < _cells.Length; i++)
            {
                int column = i % Columns, row = i / Columns;
                UIButton cell = _ui.AddButton(this, string.Empty, new Vector2(cellWidth, CellHeight),
                                              new Vector3(Margin + column * (cellWidth + CellGap), y + row * (CellHeight + CellGap)));
                cell.tooltip = Localization.Get(FlagPattern.NameKeys[i]);
                int value = FlagPattern.Values[i];
                cell.eventClick += (component, e) => Choose(value);

                PatternThumb thumb = cell.AddUIComponent<PatternThumb>();
                thumb.size = new Vector2(CellThumbWidth, CellThumbHeight);
                thumb.relativePosition = new Vector3(Mathf.Round(0.5f * (cellWidth - CellThumbWidth)), Mathf.Round(0.5f * (CellHeight - CellThumbHeight)));
                thumb.isInteractive = false;

                _cells[i] = cell;
                _cellThumbs[i] = thumb;
                _cellLit[i] = -1;
            }

            int rows = (_cells.Length + Columns - 1) / Columns;
            y += rows * (CellHeight + CellGap) + 2f;

            // The colours, a page a design: only the library's names column is narrower here.
            _ui.LabelWidth = 76f;
            _rows = new RowSet(this, _ui);
            _coloursNew = AddColours(PanelPage.PatternNew, y);
            _coloursExisting = AddColours(PanelPage.PatternExisting, y);
            y += _ui.RowHeight + 4f;

            // The saved ones: a dark box of rows, the scrollbar at its right.
            UIPanel heading = AddUIComponent<UIPanel>();
            heading.size = new Vector2(Inner, _ui.Theme.HeadingHeight);
            heading.relativePosition = new Vector3(Margin, y);
            _ui.AddHeading(heading, Localization.Get("Pattern.Saved"), 2f);
            y += _ui.Theme.HeadingHeight + 2f;

            BuildList(y);
            y += _box.height + 8f;

            _ui.AddName(this, Localization.Get("Pattern.Name"), new Vector3(Margin, y + 5f), 76f);
            float field = Inner - 80f;
            _name = _ui.AddTextField(this, new Vector2(field, 26f), new Vector3(Margin + 80f, y), SavedPatternFile.MaxName);
            _name.padding = new RectOffset(6, 6, 5, 3);
            _name.horizontalAlignment = UIHorizontalAlignment.Left;
            _name.tooltip = Localization.Get("Pattern.NameTip");
            _name.eventTextChanged += (component, text) => RefreshButtons();
            y += 32f;

            float half = (Inner - 6f) * 0.5f;
            _save = _ui.AddButton(this, Localization.Get("Pattern.Save"), new Vector2(half, 28f), new Vector3(Margin, y));
            _save.tooltip = Localization.Get("Pattern.SaveTip");
            _save.eventClick += (component, e) => Save();
            _delete = _ui.AddButton(this, Localization.Get("Pattern.Delete"), new Vector2(half, 28f), new Vector3(Margin + half + 6f, y));
            _delete.tooltip = Localization.Get("Pattern.DeleteTip");
            _delete.eventClick += (component, e) => Delete();
            y += 28f + Margin;

            height = y;
            _built = true;

            eventVisibilityChanged += (component, visible) =>
            {
                if (!visible)
                    ColourPicker.Close();
            };

            eventKeyDown += (component, p) =>
            {
                if (p.used || p.keycode != KeyCode.Escape)
                    return;

                p.Use();
                Hide();
            };
        }

        private UIPanel AddColours(PanelPage page, float y)
        {
            UIPanel panel = AddUIComponent<UIPanel>();
            panel.size = new Vector2(Inner, _ui.RowHeight);
            panel.relativePosition = new Vector3(Margin, y);
            _rows.BuildRows(panel, page, 0f);
            return panel;
        }

        private void BuildList(float y)
        {
            _box = AddUIComponent<UIPanel>();
            PanelLook.DressListBox(_ui, _box);
            _box.size = new Vector2(Inner, ListRowHeight * ListRows + 6f);
            _box.relativePosition = new Vector3(Margin, y);
            _box.eventMouseWheel += (component, p) =>
            {
                Scroll(p.wheelDelta > 0 ? -2 : 2);
                p.Use();
            };

            float rowWidth = _box.width - 18f;
            for (int i = 0; i < ListRows; i++)
            {
                UIButton button = _box.AddUIComponent<UIButton>();
                PanelLook.DressListRow(_ui, button);
                button.size = new Vector2(rowWidth, ListRowHeight);
                button.relativePosition = new Vector3(3f, 3f + i * ListRowHeight);
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

                PatternThumb thumb = button.AddUIComponent<PatternThumb>();
                thumb.size = new Vector2(ListThumbWidth, ListThumbHeight);
                thumb.relativePosition = new Vector3(6f, Mathf.Round(0.5f * (ListRowHeight - ListThumbHeight)));
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

            _empty = _ui.AddLabel(_box, Localization.Get("Pattern.SavedEmpty"), new Vector3(10f, 10f), 0.75f);
            _empty.autoSize = false;
            _empty.wordWrap = true;
            _empty.size = new Vector2(_box.width - 20f, _box.height - 20f);
            _empty.isInteractive = false;
            _ui.Paint(_empty, _ui.Theme.Status);
        }

        private void Edit(PatternTarget target, UIComponent beside)
        {
            _target = target;
            _beside = beside;
            _filled = false;
            if (!_built)
                return; // Start has not run yet: Update fills it in on the next frame

            _filled = true;
            bool created = target.Page == PanelPage.New;
            _title.text = _ui.Caps(SettingsCatalog.TabName(target.ColoursPage));
            _coloursNew.isVisible = created;
            _coloursExisting.isVisible = !created;

            SavedPatterns.Refresh();
            _nextScan = Time.realtimeSinceStartup + ScanInterval;

            PanelLook.PlaceBeside(this, beside, 40f);
            ColourPicker.Close();
            RefreshAll();
            DrawPreview(false);
            BringToFront();
        }

        public override void Update()
        {
            base.Update();

            if (!_built || !isVisible || _target == null)
                return;

            // Opened before Start ran: filled in now.
            if (!_filled)
            {
                Edit(_target, _beside);
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now >= _nextScan)
            {
                _nextScan = now + ScanInterval;
                SavedPatterns.Refresh();
            }

            if (_listedVersion != SavedPatterns.Version)
            {
                FillRows();
                RefreshButtons();
            }

            if (Changed())
            {
                _quickUntil = now + QuickTime;
                DrawPreview(true);
                FillRows();
                RefreshButtons();
            }
            else if (_quickUntil > 0f && now >= _quickUntil)
            {
                _quickUntil = 0f;
                DrawPreview(false);
            }

            DrawCells();
        }

        // ---- the picture ---------------------------------------------------------------------------

        private bool Changed()
        {
            return _target.Pattern.value != _shownPattern || _target.Proportion.value != _shownProportion || _target.HoistedFrom.value != _shownHoist ||
                   _target.C1 != _shownC1 || _target.C2 != _shownC2 || _target.C3 != _shownC3 || _target.IsEnabled != _shownEnabled;
        }

        /// <summary>The flag as large as the view holds, its shape as seen; at the screen's sharpness unless <paramref name="quick"/>.</summary>
        private void DrawPreview(bool quick)
        {
            if (!_built || _target == null || (!Changed() && (quick || _shownSharp)))
                return;

            _shownPattern = _target.Pattern.value;
            _shownProportion = _target.Proportion.value;
            _shownHoist = _target.HoistedFrom.value;
            _shownC1 = _target.C1;
            _shownC2 = _target.C2;
            _shownC3 = _target.C3;
            _shownEnabled = _target.IsEnabled;
            _shownSharp = !quick;

            float scale = quick ? 1f : 2f;
            int vw = Mathf.RoundToInt(_canvas.width * scale), vh = Mathf.RoundToInt(_canvas.height * scale);
            if (_canvasTexture == null || _canvasTexture.width != vw || _canvasTexture.height != vh)
            {
                if (_canvasTexture != null)
                    UnityEngine.Object.Destroy(_canvasTexture);
                _canvasTexture = new Texture2D(vw, vh, TextureFormat.RGBA32, false, false)
                {
                    name = "RealFlag pattern preview",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _canvasPixels = new Color32[vw * vh];
            }

            float seen = Mathf.Max(_target.Seen, 0.01f);
            float boxWidth = vw - 2f * PreviewMargin * scale, boxHeight = vh - 2f * PreviewMargin * scale;
            float fw = Mathf.Min(boxWidth, boxHeight / seen);
            float fh = fw * seen;
            float fx = Mathf.Round(0.5f * (vw - fw)), fy = Mathf.Round(0.5f * (vh - fh));

            FlagPatternView.Render(_canvasPixels, vw, vh, fx, fy, fw, fh, _shownPattern, FlagProportion.Ratio(_shownProportion), _target.HoistedFromTop,
                                   _shownC1, _shownC2, _shownC3, Back);
            _canvasTexture.SetPixels32(_canvasPixels);
            _canvasTexture.Apply(false);
            _canvas.texture = _canvasTexture;
            _canvas.opacity = _shownEnabled ? 1f : Greyed;
            _canvas.tooltip = Localization.Get(FlagPattern.NameKeys[IndexOfPattern(_shownPattern)]);
        }

        private static int IndexOfPattern(int pattern)
        {
            return Math.Max(0, Array.IndexOf(FlagPattern.Values, pattern));
        }

        /// <summary>The little flags that do not show the flag's colours yet, a few a frame.</summary>
        private void DrawCells()
        {
            int drawn = 0;
            for (int i = 0; i < _cells.Length && drawn < CellsPerFrame; i++)
            {
                int value = FlagPattern.Values[i];
                if (!_cellThumbs[i].Stale(value, CellProportion, false, _target.C1, _target.C2, _target.C3))
                    continue;

                _cellThumbs[i].Show(value, CellProportion, false, _target.C1, _target.C2, _target.C3);
                drawn++;
            }
        }

        // ---- the controls --------------------------------------------------------------------------

        private void RefreshAll()
        {
            if (!_built || _target == null)
                return;

            _enabled = _target.IsEnabled;
            _rows.RefreshValues();

            for (int i = 0; i < _cells.Length; i++)
            {
                bool lit = _target.Pattern.value == FlagPattern.Values[i];
                int state = lit ? 1 : 0;
                if (_cellLit[i] != state)
                {
                    _cellLit[i] = state;
                    PanelLook.SetLit(_ui, _cells[i], lit);
                }

                if (_cells[i].isEnabled != _enabled)
                {
                    _cells[i].isEnabled = _enabled;
                    _cells[i].opacity = _enabled ? 1f : Greyed;
                }
            }

            FillRows();
            RefreshButtons();
        }

        /// <summary>Save with a name to keep it under, Delete with a saved one's name; a saved one can go with no flag to load it on.</summary>
        private void RefreshButtons()
        {
            if (!_built || _target == null)
                return;

            string name = SavedPatternFile.Sanitise(_name.text);
            Enable(_save, _enabled && name.Length > 0);
            Enable(_delete, name.Length > 0 && SavedPatterns.Find(name) != null);
        }

        private static void Enable(UIButton button, bool on)
        {
            if (button.isEnabled == on)
                return;

            button.isEnabled = on;
            button.opacity = on ? 1f : Greyed;
        }

        private void Choose(int pattern)
        {
            if (_target == null)
                return;

            try
            {
                _target.Choose(pattern);
            }
            catch (Exception e)
            {
                Log.Error("pattern window: applying the pattern threw.", e);
            }
        }

        // ---- the saved ones ------------------------------------------------------------------------

        private void Scroll(int rows)
        {
            _offset = Mathf.Clamp(_offset + rows, 0, Mathf.Max(0, SavedPatterns.All.Count - ListRows));
            FillRows();
        }

        /// <summary>The rows from the list at the offset, each with its little flag; the one the flag shows lit.</summary>
        private void FillRows()
        {
            if (!_built || _target == null)
                return;

            _listedVersion = SavedPatterns.Version;
            IList<SavedPattern> all = SavedPatterns.All;
            _offset = Mathf.Clamp(_offset, 0, Mathf.Max(0, all.Count - ListRows));

            _refreshing = true;
            try
            {
                _bar.maxValue = Mathf.Max(0, all.Count - ListRows);
                _bar.scrollSize = Mathf.Min(ListRows, all.Count);
                _bar.value = _offset;
            }
            finally
            {
                _refreshing = false;
            }

            _empty.isVisible = all.Count == 0;
            for (int i = 0; i < ListRows; i++)
            {
                int at = _offset + i;
                UIButton button = _buttons[i];
                if (at >= all.Count)
                {
                    button.isVisible = false;
                    continue;
                }

                SavedPattern saved = all[at];
                button.isVisible = true;
                if (button.text != saved.Name)
                    button.text = saved.Name;
                _thumbs[i].Show(saved.Pattern, CellProportion, false, saved.Colour1, saved.Colour2, saved.Colour3);
                PanelLook.SetListLit(_ui, button, _target.Shows(saved));
                button.isEnabled = _enabled;
            }
        }

        /// <summary>A saved one, clicked: on the flag, and its name in the field for Save and Delete.</summary>
        private void Pick(int index)
        {
            int at = _offset + index;
            IList<SavedPattern> all = SavedPatterns.All;
            if (_target == null || !_target.IsEnabled || at < 0 || at >= all.Count)
                return;

            SavedPattern saved = all[at];
            _name.text = saved.Name;
            try
            {
                _target.Load(saved);
            }
            catch (Exception e)
            {
                Log.Error("pattern window: applying the saved pattern threw.", e);
            }

            FillRows();
            RefreshButtons();
        }

        /// <summary>What the flag has now under the field's name; a name in use only if the player says so.</summary>
        private void Save()
        {
            if (_target == null || !_target.IsEnabled)
                return;

            string name = SavedPatternFile.Sanitise(_name.text);
            if (name.Length == 0)
                return;

            SavedPattern pattern = _target.Saved(name);
            if (SavedPatterns.Find(name) == null)
            {
                Store(pattern);
                return;
            }

            Ask(Localization.Get("Pattern.ReplaceConfirm", name), () => Store(pattern));
        }

        private void Store(SavedPattern pattern)
        {
            if (SavedPatterns.Store(pattern))
            {
                _name.text = pattern.Name;
                ShowSaved(pattern.Name);
            }
            else
            {
                Loader.Show(Localization.Get("Pattern.SaveFailed"));
            }
        }

        /// <summary>The list at a saved one, in view.</summary>
        private void ShowSaved(string name)
        {
            IList<SavedPattern> all = SavedPatterns.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!string.Equals(all[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (i < _offset)
                    _offset = i;
                else if (i >= _offset + ListRows)
                    _offset = i - ListRows + 1;
                break;
            }

            FillRows();
            RefreshButtons();
        }

        private void Delete()
        {
            string name = SavedPatternFile.Sanitise(_name.text);
            SavedPattern saved = SavedPatterns.Find(name);
            if (saved == null)
                return;

            Ask(Localization.Get("Pattern.DeleteConfirm", saved.Name), () =>
            {
                if (SavedPatterns.Delete(saved.Name))
                {
                    _name.text = string.Empty;
                    FillRows();
                    RefreshButtons();
                }
                else
                {
                    Loader.Show(Localization.Get("Pattern.SaveFailed"));
                }
            });
        }

        private static void Ask(string question, Action yes)
        {
            try
            {
                ConfirmPanel.ShowModal(Mod.DisplayName, question, (component, result) =>
                {
                    if (result == 1)
                        yes();
                });
            }
            catch (Exception e)
            {
                Log.Error("Could not ask about a saved pattern, so nothing was changed.", e);
            }
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
