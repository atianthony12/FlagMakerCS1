using System.Collections.Generic;
using ColossalFramework.UI;
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
    /// </remarks>
    public class FlagsPanel : UIPanel
    {
        /// <summary>Its name among the view's components (the popups open beside it).</summary>
        public const string ComponentName = "RealFlagPanel";

        private const float PanelWidth = 480f;
        private const float MinPanelHeight = 180f;
        private const float TitleBarHeight = 40f;
        private const float TabHeight = 28f;
        private const float Margin = 14f;
        public const float HeadingHeight = 24f;

        private static readonly PanelPage[] Tabs = { PanelPage.Existing, PanelPage.Settings };

        /// <summary>Where to open, when replacing a panel that was already on screen. Null = the default spot.</summary>
        public Vector3? InitialPosition;

        private readonly List<UIButton> _tabs = new List<UIButton>();
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
            _rows = new RowSet(this);

            atlas = UIBuilder.Atlas;
            backgroundSprite = "MenuPanel2";
            size = new Vector2(PanelWidth, MinPanelHeight);
            canFocus = true;
            isInteractive = true;
            name = ComponentName;

            BuildTitleBar();

            foreach (PanelPage page in Tabs)
                _contentHeights.Add(_rows.BuildRows(AddPage(SettingsCatalog.TabName(page)), page, 0f));

            LayoutTabs();
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
                NewFlagPanel.Close();
                ColourPicker.Close();
                CountryPicker.Close();
                ImagePanel.Close();
                PolePanel.Close();
            };

            eventKeyDown += (component, p) =>
            {
                if (p.used || p.keycode != KeyCode.Escape || !FlagTool.IsOut)
                    return;

                p.Use();
                string step = FlagTool.StepBack();
                Unfocus();
                Log.Msg("flags: Esc: " + step + " (the key kept from the game's pause menu)");
            };

            Log.Msg("panel built: " + _rows.Count + " controls over " + _pages.Count + " tabs");
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
                _rows.RefreshValues();
            NewFlagPanel.RefreshOpen();
            PolePanel.RefreshOpen();
            ImagePanel.RefreshOpen();
        }

        private void BuildTitleBar()
        {
            UILabel title = UIBuilder.AddLabel(this, Mod.DisplayName, new Vector3(Margin, Margin), 1.1f);
            title.autoSize = false;
            title.size = new Vector2(PanelWidth - TitleBarHeight, TitleBarHeight);

            UIButton close = AddUIComponent<UIButton>();
            close.atlas = atlas;
            close.size = new Vector2(32f, 32f);
            close.relativePosition = new Vector3(PanelWidth - 36f, 4f);
            close.normalBgSprite = "buttonclose";
            close.hoveredBgSprite = "buttonclosehover";
            close.pressedBgSprite = "buttonclosepressed";
            close.eventClick += (component, e) => Hide();

            UIDragHandle drag = AddUIComponent<UIDragHandle>();
            drag.target = this;
            drag.size = new Vector2(PanelWidth - 40f, TitleBarHeight);
            drag.relativePosition = Vector3.zero;
        }

        private UIPanel AddPage(string tabName)
        {
            int index = _pages.Count;

            UIButton tab = UIBuilder.AddTab(this, tabName, new Vector2(10f, TabHeight), Vector3.zero);
            tab.eventClick += (component, e) => SelectPage(index);
            _tabs.Add(tab);

            UIPanel page = AddUIComponent<UIPanel>();
            page.size = new Vector2(PanelWidth - 2f * Margin, 10f);
            page.relativePosition = new Vector3(Margin, TitleBarHeight + TabHeight + Margin * 0.5f);
            _pages.Add(page);

            return page;
        }

        private void LayoutTabs()
        {
            float width = (PanelWidth - 2f * Margin) / _tabs.Count;
            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].size = new Vector2(width, TabHeight);
                _tabs[i].relativePosition = new Vector3(Margin + i * width, TitleBarHeight + 2f);
            }
        }

        private void SelectPage(int index)
        {
            _page = index;
            for (int i = 0; i < _pages.Count; i++)
            {
                _pages[i].isVisible = i == index;
                if (i < _tabs.Count)
                    UIBuilder.SetTabSelected(_tabs[i], i == index);
            }

            // As tall as the selected page needs.
            float content = _contentHeights[index];
            _pages[index].height = content;
            float top = TitleBarHeight + TabHeight + Margin * 0.5f;
            height = Mathf.Max(MinPanelHeight, top + content + Margin);

            // The selection and its gizmo belong to the Flags tab (the New flag window stands on its own).
            if (index != 0)
            {
                if (FlagTool.IsPicking)
                    FlagTool.PutAway();
                FlagEditor.Deselect();
                PolePanel.CloseIf(PanelPage.PoleExisting);
                ImagePanel.CloseIf(PanelPage.Existing);
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
            UIView view = GetUIView();
            relativePosition = new Vector3(Mathf.Floor(view.fixedWidth * 0.04f), Mathf.Floor((view.fixedHeight - height) * 0.3f));
        }

        public override void OnDestroy()
        {
            ColourPicker.DestroyPicker();
            CountryPicker.DestroyPicker();
            ImagePanel.DestroyPanel();
            PolePanel.DestroyPanel();
            NewFlagPanel.DestroyPanel();

            if (_instance == this)
                _instance = null;

            base.OnDestroy();
        }
    }
}
