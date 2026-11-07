using System.Collections.Generic;
using ColossalFramework.UI;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// The String window (2026-11-07), beside the panel: the NEW string -- opened by the Strings
    /// tab's "+ New string", while the tool strings spans with each click -- or the SELECTED one
    /// (picked on the map or in the list), its spans all redesigned as the rows move. Three tabs,
    /// so it fits a 1080-line screen: Flags (their shape, size, spacing, motion), Designs (what
    /// they are and in what order), String (the cord). Its rows are the catalog's
    /// (PanelPage.StringNew* / StringSel*), built again when the designs' kind changes (rows that
    /// do not apply are left out: Row.Shown).
    /// </summary>
    public class StringPanel : UIPanel
    {
        public const string ComponentName = "RealFlagStringPanel";

        private const float PanelWidth = 480f;
        private const float TitleBarHeight = 40f;
        private const float TabHeight = 28f;
        private const float Margin = 14f;
        private const float RefreshInterval = 0.25f;

        private static StringPanel _instance;
        private static bool _newMode = true;
        private static int _tab;

        private ModsLibCS1.UI.Builder _ui;
        private ModsLibCS1.UI.Frame _frame;
        private ModsLibCS1.UI.TabStrip _tabs;
        private readonly List<string> _tabNames = new List<string>();
        private readonly List<UIPanel> _pages = new List<UIPanel>();
        private readonly List<float> _heights = new List<float>();
        private RowSet _rows;
        private bool _built, _builtNew;
        private int _builtStructure = -1;
        private float _nextRefreshTime;

        public static bool IsOpen
        {
            get { return _instance != null && _instance.isVisible; }
        }

        public static bool IsOpenNew
        {
            get { return IsOpen && _newMode; }
        }

        private static StringTarget Target
        {
            get { return _newMode ? StringTarget.New : StringTarget.Selected; }
        }

        /// <summary>
        /// "+ New string": the window on the last new string's settings (as "+ New flag" keeps the
        /// last flag's: the author, 2026-11-07, "remember the last settings ... better UX-wise"; each
        /// row's reset icon puts one back), the tool placing; again, it closes.
        /// </summary>
        public static void ToggleNew()
        {
            if (IsOpenNew)
            {
                Close();
                return;
            }

            if (StringRenderer.Instance == null || StringPrefab.Info == null)
                return;

            OpenNew();
        }

        /// <summary>The window on the new string as it is (Copy has given it the selected one's design), the tool placing.</summary>
        public static void OpenNew()
        {
            if (StringRenderer.Instance == null || StringPrefab.Info == null)
                return;
            StringTarget.Init();
            StringEditor.Deselect();
            FlagTool.PutAway();
            NewFlagPanel.Close();
            _newMode = true;
            ShowWindow();
            StringTool.Open(StringTool.Mode.Place);
            Log.Msg("strings: the String window opened for a new string (" + StringTarget.New.Spec().Describe() + ")");
        }

        /// <summary>The window on the selected string (StringEditor.Select first).</summary>
        public static void OpenSelected()
        {
            if (!StringEditor.HasSelection)
                return;
            _newMode = false;
            ShowWindow();
            FlagsPanel.ShowStrings();
        }

        private static void ShowWindow()
        {
            const bool front = true;
            if (_instance == null)
                _instance = UIView.GetAView().AddUIComponent(typeof(StringPanel)) as StringPanel;
            if (_instance == null)
                return;

            _instance.Show();
            _instance.Rebuild();
            _instance.PlaceBesidePanel();
            if (front)
                _instance.BringToFront();
        }

        public static void Close()
        {
            if (_instance != null && _instance.isVisible)
                _instance.Hide();
        }

        public static void RefreshOpen()
        {
            if (_instance != null && _instance.isVisible && _instance._rows != null)
                _instance._rows.RefreshValues();
        }

        public static void DestroyPanel()
        {
            if (_instance != null)
                Object.Destroy(_instance.gameObject);
            _instance = null;
        }

        public override void Start()
        {
            base.Start();

            _ui = PanelLook.NewBuilder("the String window");
            size = new Vector2(PanelWidth, 200f);
            canFocus = true;
            isInteractive = true;
            name = ComponentName;

            _frame = _ui.AddFrame(this, Localization.Get("StrWindow.New"), Hide);
            _frame.Title.textScale = 1f;

            _tabNames.Add(Localization.Get("StrTab.Flags"));
            _tabNames.Add(Localization.Get("StrTab.Designs"));
            _tabNames.Add(Localization.Get("StrTab.String"));
            _tabs = _ui.AddTabStrip(this, new Vector3(Margin, TitleBarHeight + 2f), PanelWidth - 2f * Margin, TabHeight, _tabNames, SelectTab);
            _built = true;
            Rebuild();

            eventVisibilityChanged += (component, visible) =>
            {
                if (visible)
                    return;

                ColourPicker.Close();
                CountryPicker.Close();
                CountryListPicker.Close();
                if (_newMode)
                {
                    if (StringTool.IsPlacing)
                        StringTool.PutAway();
                    Log.Msg("strings: the String window closed");
                }
                else
                {
                    StringEditor.Deselect();
                }
            };

            eventKeyDown += (component, p) =>
            {
                if (p.used || p.keycode != KeyCode.Escape)
                    return;
                p.Use();
                Hide();
            };

            PlaceBesidePanel();
        }

        /// <summary>The rows for the mode and the target's structure, the tab kept.</summary>
        private void Rebuild()
        {
            if (!_built)
                return;

            StringTarget t = Target;
            if (t == null)
                return;

            // The design row's choices: one a design.
            Row designRow = SettingsCatalog.StringDesignRow(t);
            if (designRow != null)
            {
                var values = new int[System.Math.Max(1, t.Designs.Count)];
                for (int i = 0; i < values.Length; i++)
                    values[i] = i;
                designRow.ChoiceValues = values;
            }

            foreach (UIPanel page in _pages)
                Object.Destroy(page.gameObject);
            _pages.Clear();
            _heights.Clear();

            _rows = new RowSet(this, _ui);
            PanelPage[] pages = _newMode
                ? new[] { PanelPage.StringNewFlags, PanelPage.StringNewDesigns, PanelPage.StringNewCord }
                : new[] { PanelPage.StringSelFlags, PanelPage.StringSelDesigns, PanelPage.StringSelCord };
            foreach (PanelPage p in pages)
            {
                UIPanel page = AddUIComponent<UIPanel>();
                page.size = new Vector2(PanelWidth - 2f * Margin, 10f);
                page.relativePosition = new Vector3(Margin, TitleBarHeight + TabHeight + Margin * 0.5f);
                float h = _rows.BuildRows(page, p, 0f);
                page.height = h;
                _pages.Add(page);
                _heights.Add(h);
            }

            _builtNew = _newMode;
            _builtStructure = t.Structure;
            _frame.Title.text = _ui.Caps(Localization.Get(_newMode ? "StrWindow.New" : "StrWindow.Selected"));
            SelectTab(_tab);
            _rows.RefreshValues();
            _rows.RefreshStatus();
        }

        private void SelectTab(int index)
        {
            _tab = Mathf.Clamp(index, 0, _pages.Count - 1);
            for (int i = 0; i < _pages.Count; i++)
                _pages[i].isVisible = i == _tab;
            if (_tabs != null)
                _tabs.Select(_tab);

            float content = _heights.Count > _tab ? _heights[_tab] : 0f;
            height = TitleBarHeight + TabHeight + Margin * 0.5f + content + Margin;

            if (_tab != 1)
            {
                CountryPicker.Close();
                CountryListPicker.Close();
            }
        }

        public override void Update()
        {
            base.Update();

            if (!isVisible || !_built)
                return;

            // The new string's window lives as long as the tool places; the selected one's as long as there is a selection.
            if (_newMode && !StringTool.IsPlacing)
            {
                Hide();
                return;
            }

            if (!_newMode && !StringEditor.HasSelection)
            {
                Hide();
                return;
            }

            StringTarget t = Target;
            if (t != null && (t.Structure != _builtStructure || _builtNew != _newMode))
                Rebuild();

            if (Time.realtimeSinceStartup < _nextRefreshTime)
                return;
            _nextRefreshTime = Time.realtimeSinceStartup + RefreshInterval;
            StringEditor.Check();
            _rows.RefreshStatus();
            _rows.RefreshValues();
        }

        private void PlaceBesidePanel()
        {
            if (!_built)
                return;

            UIView view = GetUIView();
            UIComponent panel = view.FindUIComponent(FlagsPanel.ComponentName);
            if (panel == null)
                return;

            Vector2 screen = view.GetScreenResolution();
            Vector3 p = panel.relativePosition;
            float x = p.x + panel.width + 6f;
            if (x + width > screen.x)
                x = Mathf.Max(0f, p.x - width - 6f);
            relativePosition = new Vector3(x, Mathf.Clamp(p.y, 0f, Mathf.Max(0f, screen.y - height)));
        }

        public override void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
            base.OnDestroy();
        }
    }
}
