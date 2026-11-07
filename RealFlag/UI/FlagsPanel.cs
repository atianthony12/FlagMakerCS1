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

        private static readonly PanelPage[] Tabs = { PanelPage.Existing, PanelPage.Strings, PanelPage.Settings };

        /// <summary>The Strings tab's place among the tabs (2026-11-07).</summary>
        private const int StringsTab = 1;

        private StringListView _stringList;

        /// <summary>
        /// A tab's icon in the new looks: a Lucide name drawn into this mod's own sheet
        /// (Resources\PanelIcons.lucide.txt; THIRD-PARTY-NOTICES.md). Vanilla keeps the names.
        /// </summary>
        private static string IconOf(PanelPage page)
        {
            switch (page)
            {
                case PanelPage.Existing: return "flag";
                case PanelPage.Strings: return "flag-triangle-right";
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

        /// <summary>A string was picked: show the Strings tab.</summary>
        public static void ShowStrings()
        {
            if (_instance == null)
                return;

            if (!_instance.isVisible)
                _instance.Show();
            if (_instance._page != StringsTab)
                _instance.SelectPage(StringsTab);
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
            {
                UIPanel built = AddPage();
                float content = _rows.BuildRows(built, page, 0f);

                // The Strings tab's list of the city's strings, under its rows.
                if (page == PanelPage.Strings)
                {
                    _stringList = built.AddUIComponent<StringListView>();
                    _stringList.relativePosition = new Vector3(0f, content + 4f);
                    _stringList.Build(built.width, _ui);
                    content += StringListView.RowHeight * StringListView.VisibleRows + 16f;
                }

                _contentHeights.Add(content);
            }

            SelectPage(0);

            if (InitialPosition.HasValue)
                relativePosition = InitialPosition.Value;
            else
                PlaceAtLeft();

            eventVisibilityChanged += (component, visible) =>
            {
                if (visible)
                {
                    RefreshValues();
                    return;
                }

                // Closed: the tool (and with it the selection), the New flag window and the pickers go with it.
                FlagTool.PutAway();
                StringTool.PutAway();
                StringPanel.Close();
                CountryListPicker.Close();
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
                if (p.used || p.keycode != KeyCode.Escape || (!FlagTool.IsOut && !rows && !StringTool.IsOut))
                    return;

                p.Use();
                string step = rows ? LineFlags.PutAway() : StringTool.IsOut ? StringTool.StepBack() : FlagTool.StepBack();
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
            if (_stringList != null && _page == StringsTab)
                _stringList.Refresh();

            RefreshValues();
        }

        /// <summary>Re-reads every control from its setting (the New flag window's and the Flagpole popup's too); a write only where something changed.</summary>
        public void RefreshValues()
        {
            if (_rows != null)
                _rows.RefreshValues();
            NewFlagPanel.RefreshOpen();
            StringPanel.RefreshOpen();
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

            // As tall as the selected page needs.
            float content = _contentHeights[index];
            _pages[index].height = content;
            float top = TitleBarHeight + _tabHeight + Margin * 0.5f;
            height = Mathf.Max(MinPanelHeight, top + content + Margin);

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

            // The selected string and its window belong to the Strings tab (the new string's window stands on its own).
            if (index != StringsTab)
            {
                if (StringTool.IsPicking)
                    StringTool.PutAway();
                if (StringEditor.HasSelection)
                {
                    StringEditor.Deselect();
                    StringPanel.Close();
                }
            }
            else if (_stringList != null)
            {
                _stringList.Refresh();
            }

            if (!NewFlagPanel.IsOpen)
            {
                ColourPicker.Close();
                CountryPicker.Close();
            }
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
            StringPanel.DestroyPanel();
            CountryListPicker.DestroyPicker();

            if (_instance == this)
                _instance = null;

            base.OnDestroy();
        }
    }
}
