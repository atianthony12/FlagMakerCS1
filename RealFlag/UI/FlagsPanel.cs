using System.Collections.Generic;
using ColossalFramework.UI;
using ModsLibCS1.UI;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// The in-game panel (F6), two tabs: Flags (every flag in the city -- a click selects one to
    /// edit -- and the "+ New flag" button that opens the <see cref="NewFlagPanel"/> beside it) and
    /// Settings (the wind -- the game's unless the player sets their own -- and the cloth). The
    /// author first asked for "two tabs: Create new and Existing Flags", then "Wind should be by
    /// default based on the game's wind, and can be overridden in a 3rd tab, Settings", then
    /// "Let's remove the Create New tab. Rather, by default we'll show the existing flags, and a
    /// plus button somewhere to add new flag".
    /// </summary>
    /// <remarks>
    /// Volumetric Explosions' panel, cut down and grown where flags need it: the rows are built
    /// and refreshed by a <see cref="RowSet"/> from <see cref="SettingsCatalog"/>; a colour row is
    /// a swatch that opens the <see cref="ColourPicker"/> (a design's three colours share one
    /// line); the list is <see cref="FlagListView"/>; two buttons can share a line; the Flagpole
    /// button opens the <see cref="PolePanel"/> beside it. It opens at the LEFT of the screen,
    /// clear of the middle where flags are placed.
    ///
    /// Every control comes from ModsLibCS1, in the look the "Panel look" row picks
    /// (<see cref="PanelLook"/>); Vanilla is the panel as it was before. In the new looks the two
    /// tabs are this mod's own icons (a flag, a gear: Resources\PanelIcons), the open tab's name
    /// after the title. The panel grows with the tab shown, in every look.
    /// </remarks>
    public class FlagsPanel : UIPanel
    {
        /// <summary>Its name among the view's components (the popups open beside it).</summary>
        public const string ComponentName = "RealFlagPanel";

        private const float PanelWidth = 480f;
        private const float MinPanelHeight = 180f;
        private const float TitleBarHeight = 40f;
        private const float TabHeight = 28f;

        /// <summary>The new looks' icon tabs: taller, for the icons (Volumetric Weather's and Real Glass's).</summary>
        private const float IconTabHeight = 36f;
        private const float Margin = 14f;

        private static readonly PanelPage[] Tabs = { PanelPage.Existing, PanelPage.Settings };

        /// <summary>
        /// A tab's icon in the new looks: a Lucide name drawn into this mod's own sheet
        /// (Resources\PanelIcons.lucide.txt; THIRD-PARTY-NOTICES.md). Vanilla keeps the names.
        /// </summary>
        private static string IconOf(PanelPage page)
        {
            switch (page)
            {
                case PanelPage.Existing: return "flag";
                case PanelPage.Settings: return "settings";
                default: return null;
            }
        }

        /// <summary>Where to open, when replacing a panel that was already on screen. Null = the default spot.</summary>
        public Vector3? InitialPosition;

        private Builder _ui;
        private Frame _frame;
        private TabStrip _tabs;
        private float _tabHeight = TabHeight;
        private readonly List<string> _tabNames = new List<string>();
        private readonly List<UIPanel> _pages = new List<UIPanel>();
        private readonly List<float> _contentHeights = new List<float>();
        private RowSet _rows;
        private int _page;

        private const float RefreshInterval = 0.25f;
        private float _nextRefreshTime;

        private static FlagsPanel _instance;

        /// <summary>Re-reads every control, if the panel exists. Safe to call from anywhere.</summary>
        public static void RefreshOpenPanel()
        {
            if (_instance != null)
                _instance.RefreshValues();
        }

        public static bool IsOpen
        {
            get { return _instance != null && _instance.isVisible; }
        }

        /// <summary>
        /// The panel stepped aside for the New flag window, which stands in its place (the author,
        /// 2026-11-08: "+New Flag should close the main panel so not to overwhelm the user with
        /// many panels"): hidden, but the mod still open -- it comes back when that window closes.
        /// </summary>
        private static bool _aside;

        public static bool IsAside
        {
            // Cleared whenever the panel shows (its visibility handler).
            get { return _aside && _instance != null; }
        }

        /// <summary>The mod's windows are up: the panel, or the New flag window in its place (the key and the button toggle this).</summary>
        public static bool IsShown
        {
            get { return IsOpen || (IsAside && NewFlagPanel.IsOpen); }
        }

        /// <summary>Where the panel stands (the New flag window opens there), or null without one.</summary>
        public static Vector3? Place
        {
            get { return _instance != null ? _instance.relativePosition : (Vector3?)null; }
        }

        /// <summary>The New flag window is opening: the panel hides without closing what it closes (the tool, that window).</summary>
        public static void StepAside()
        {
            if (_instance == null || !_instance.isVisible)
                return;

            _aside = true;
            _instance.Hide();
            Log.Msg("panel: stepped aside for the New flag window");
        }

        /// <summary>The New flag window closed: the panel comes back where that window stood, if it stepped aside for it.</summary>
        public static void ComeBack(Vector3 where)
        {
            if (!IsAside)
            {
                _aside = false;
                return;
            }

            _aside = false;
            _instance.relativePosition = where;
            _instance.Show();
            _instance.KeepOnScreen();
            Log.Msg("panel: back after the New flag window");
        }

        /// <summary>
        /// The key or the button closes the mod: the panel, or the New flag window standing in for
        /// it (the panel then stays hidden: no visibility change says so, the caller tells Unified UI).
        /// </summary>
        public static void CloseAll()
        {
            if (_instance == null)
                return;

            bool aside = IsAside;
            _aside = false;
            if (aside)
                NewFlagPanel.Close();
            else
                _instance.Hide();
        }

        /// <summary>The pick tool chose a flag: show it in the Flags tab.</summary>
        public static void ShowExisting()
        {
            if (_instance == null)
                return;

            if (!_instance.isVisible)
                _instance.Show();
            if (_instance._page != 0)
                _instance.SelectPage(0);
            _instance.RefreshValues();
        }

        /// <summary>
        /// Esc has gone down with the flag tool out: the panel takes the focus, so the key comes to
        /// it and it steps the tool back, instead of the game opening its pause menu (the game's Esc
        /// only knows its own toolbar's tools; Volumetric Explosions, read from the IL).
        /// </summary>
        public static bool CatchEscape()
        {
            FlagsPanel panel = _instance;
            if (IsAside)
                return NewFlagPanel.CatchEscape();
            if (panel == null || !panel.isVisible || !panel.isEnabled || UIView.HasModalInput())
                return false;

            if (!panel.containsFocus)
                panel.Focus();
            return panel.containsFocus;
        }

        public override void Start()
        {
            base.Start();

            _instance = this;
            _aside = false;
            _ui = PanelLook.NewBuilder("the panel");
            _tabHeight = _ui.Skin == Skin.Vanilla ? TabHeight : IconTabHeight;
            _rows = new RowSet(this, _ui);

            size = new Vector2(PanelWidth, MinPanelHeight);
            canFocus = true;
            isInteractive = true;
            name = ComponentName;

            // Background, title, close button; dragging the title bar moves the whole panel.
            _frame = _ui.AddFrame(this, Mod.DisplayName, Hide);

            List<string> icons = new List<string>();
            foreach (PanelPage page in Tabs)
            {
                _tabNames.Add(SettingsCatalog.TabName(page));
                icons.Add(IconOf(page));
            }

            _tabs = _ui.AddTabStrip(this, new Vector3(Margin, TitleBarHeight + 2f), PanelWidth - 2f * Margin, _tabHeight,
                                    _tabNames, SelectPage, icons);

            foreach (PanelPage page in Tabs)
                _contentHeights.Add(_rows.BuildRows(AddPage(), page, 0f));

            SelectPage(0);

            if (InitialPosition.HasValue)
                relativePosition = InitialPosition.Value;
            else
                PlaceAtLeft();

            eventVisibilityChanged += (component, visible) =>
            {
                if (visible)
                {
                    _aside = false;
                    RefreshValues();
                    return;
                }

                // Aside for the New flag window: that window and its tool stay.
                if (_aside)
                    return;

                // Closed: the tool (and with it the selection), the New flag window and the pickers go with it.
                FlagTool.PutAway();
                LineFlags.PutAway();
                NewFlagPanel.Close();
                ColourPicker.Close();
                CountryPicker.Close();
                ImagePanel.Close();
                PatternPanel.Close();
                PolePanel.Close();
            };

            eventKeyDown += (component, p) =>
            {
                bool rows = LineFlags.Driving;
                if (p.used || p.keycode != KeyCode.Escape || (!FlagTool.IsOut && !rows))
                    return;

                p.Use();
                string step = rows ? LineFlags.PutAway() : FlagTool.StepBack();
                Unfocus();
                Log.Msg("flags: Esc: " + step + " (the key kept from the game's pause menu)");
            };

            Log.Msg("panel built: " + _rows.Count + " controls over " + _pages.Count + " tabs (" + _ui.Skin + ")");
        }

        public override void Update()
        {
            base.Update();

            if (!isVisible || Time.realtimeSinceStartup < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.realtimeSinceStartup + RefreshInterval;

            // Move It may have turned or raised the selected flag.
            FlagEditor.Load(false);

            _rows.RefreshStatus();
            if (_rows.List != null)
                _rows.List.Refresh();

            RefreshValues();
        }

        /// <summary>Re-reads every control from its setting (the New flag window's and the Flagpole popup's too); a write only where something changed.</summary>
        public void RefreshValues()
        {
            if (_rows != null)
            {
                Relayout();
                _rows.RefreshValues();
            }
            NewFlagPanel.RefreshOpen();
            PolePanel.RefreshOpen();
            ImagePanel.RefreshOpen();
            PatternPanel.RefreshOpen();
        }

        private UIPanel AddPage()
        {
            UIPanel page = AddUIComponent<UIPanel>();
            page.size = new Vector2(PanelWidth - 2f * Margin, 10f);
            page.relativePosition = new Vector3(Margin, TitleBarHeight + _tabHeight + Margin * 0.5f);
            _pages.Add(page);

            return page;
        }

        private void SelectPage(int index)
        {
            _page = index;
            for (int i = 0; i < _pages.Count; i++)
                _pages[i].isVisible = i == index;

            _tabs.Select(index);
            _ui.SetSubtitle(_frame, _tabNames[index]);

            FitPage();

            // The selection and its gizmo belong to the Flags tab (the New flag window stands on its own).
            if (index != 0)
            {
                if (FlagTool.IsPicking)
                    FlagTool.PutAway();
                FlagEditor.Deselect();
                PolePanel.CloseIf(PanelPage.PoleExisting);
                ImagePanel.CloseIf(PanelPage.Existing);
                PatternPanel.CloseIf(PanelPage.Existing);
            }

            if (!NewFlagPanel.IsOpen)
            {
                ColourPicker.Close();
                CountryPicker.Close();
            }
        }

        /// <summary>As tall as the selected page needs.</summary>
        private void FitPage()
        {
            float content = _contentHeights[_page];
            _pages[_page].height = content;
            float top = TitleBarHeight + _tabHeight + Margin * 0.5f;
            height = Mathf.Max(MinPanelHeight, top + content + Margin);
        }

        /// <summary>
        /// A selection made or let go: the Flags tab's selected-flag rows shown or hidden (RowSet's
        /// blocks), the panel as tall as that, its bottom kept on the screen.
        /// </summary>
        private void Relayout()
        {
            if (!_rows.Layout())
                return;

            for (int i = 0; i < _pages.Count; i++)
            {
                float? content = _rows.HeightOf(_pages[i]);
                if (content.HasValue)
                    _contentHeights[i] = content.Value;
            }

            FitPage();
            KeepOnScreen();

            // The selected flag's pickers go with its rows.
            if (!FlagEditor.HasSelection)
            {
                if (!NewFlagPanel.IsOpen)
                {
                    ColourPicker.Close();
                    CountryPicker.Close();
                }

                PolePanel.CloseIf(PanelPage.PoleExisting);
                ImagePanel.CloseIf(PanelPage.Existing);
                PatternPanel.CloseIf(PanelPage.Existing);
            }
        }

        /// <summary>Moves the panel up if it grew past the screen's bottom (never past its top).</summary>
        private void KeepOnScreen()
        {
            Vector2 screen = GetUIView().GetScreenResolution();
            Vector3 p = relativePosition;
            float x = Mathf.Clamp(p.x, 0f, Mathf.Max(0f, screen.x - width));
            float y = Mathf.Clamp(p.y, 0f, Mathf.Max(0f, screen.y - height));
            if (x != p.x || y != p.y)
                relativePosition = new Vector3(x, y);
        }

        /// <summary>A little in from the left edge, a third of the way down: clear of the middle of the screen.</summary>
        private void PlaceAtLeft()
        {
            // The screen as the UI sees it (1920 x 1080 only at 16:9); never above its top, where
            // the title bar and its close button would be out of reach.
            Vector2 screen = GetUIView().GetScreenResolution();
            relativePosition = new Vector3(Mathf.Floor(screen.x * 0.04f), Mathf.Max(0f, Mathf.Floor((screen.y - height) * 0.3f)));
        }

        public override void OnDestroy()
        {
            ColourPicker.DestroyPicker();
            CountryPicker.DestroyPicker();
            ImagePanel.DestroyPanel();
            PatternPanel.DestroyPanel();
            PolePanel.DestroyPanel();
            NewFlagPanel.DestroyPanel();

            if (_instance == this)
            {
                _instance = null;
                _aside = false;
            }

            base.OnDestroy();
        }
    }
}
