using ColossalFramework.UI;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// The New flag window, beside the panel: the next flag -- its design, its pole, which way it
    /// points, how high its top stands -- opened by the Flags tab's "+ New flag" button. While it
    /// is open the flag follows the cursor, the real cloth blowing, and each click places one
    /// (FlagTool's PLACE); closed, or Esc, and it goes, the tool with it. The author: "Let's remove
    /// the Create New tab. Rather, by default we'll show the existing flags, and a plus button
    /// somewhere to add new flag. The options for the new flag will be in a new window, and if the
    /// window is closed or the user presses Esc it goes away."
    /// </summary>
    /// <remarks>
    /// Its rows are the catalog's (PanelPage.New), built and refreshed by a <see cref="RowSet"/>
    /// as the panel's are; its swatches and its country open their pickers beside it, its
    /// Flagpole button the Flagpole popup. It lives as long as the tool places: the tool put away
    /// by any means -- Esc (the panel steps it back, FlagTool.StepBack), another tool, the Flags
    /// tab's Pick or a flag picked in its list -- closes it; closing it puts the tool away.
    /// </remarks>
    public class NewFlagPanel : UIPanel
    {
        /// <summary>Its name among the view's components (the Flagpole popup opens beside it).</summary>
        public const string ComponentName = "RealFlagNewPanel";

        private const float PanelWidth = 480f;
        private const float TitleBarHeight = 40f;
        private const float Margin = 14f;
        private const float RefreshInterval = 0.25f;

        private static NewFlagPanel _instance;

        private RowSet _rows;
        private UIPanel _page;
        private float _pageHeight;
        private bool _built;
        private float _nextRefreshTime;

        public static bool IsOpen
        {
            get { return _instance != null && _instance.isVisible; }
        }

        /// <summary>
        /// The "+ New flag" button: opens the window (and the tool) on a flag of the defaults
        /// (SettingsCatalog.ResetNewFlag), or closes it if it is open. Copy opens it with the
        /// selected flag's design instead (<see cref="Open"/> alone).
        /// </summary>
        public static void Toggle()
        {
            if (IsOpen)
            {
                Close();
                return;
            }

            if (FlagRenderer.Instance == null || FlagPrefab.Info == null)
                return;

            SettingsCatalog.ResetNewFlag();
            Open();
        }

        /// <summary>Opens the window beside the panel and takes the tool out to place (a selected flag is let go: placing takes the tool).</summary>
        public static void Open()
        {
            if (FlagRenderer.Instance == null || FlagPrefab.Info == null)
                return;

            if (_instance == null)
                _instance = UIView.GetAView().AddUIComponent(typeof(NewFlagPanel)) as NewFlagPanel;
            if (_instance == null)
                return;

            FlagEditor.Deselect();
            PolePanel.CloseIf(PanelPage.PoleExisting);
            PatternPanel.CloseIf(PanelPage.Existing);

            bool was = _instance.isVisible;
            _instance.Show();
            _instance.PlaceBesidePanel();
            _instance.BringToFront();
            Settings.NewPlacing.value = LineFlags.Single;
            FlagTool.Open(FlagTool.Mode.Place);

            if (!was)
                Log.Msg("flags: the New flag window opened (" + Settings.NewSpec().Describe() + ")");
        }

        public static void Close()
        {
            if (_instance != null && _instance.isVisible)
                _instance.Hide();
        }

        /// <summary>Re-reads its controls, if it is open (the panel's refresh calls it).</summary>
        public static void RefreshOpen()
        {
            if (_instance != null && _instance.isVisible && _instance._rows != null)
                _instance._rows.RefreshValues();
        }

        /// <summary>With the panel it belongs to.</summary>
        public static void DestroyPanel()
        {
            if (_instance != null)
                Object.Destroy(_instance.gameObject);
            _instance = null;
        }

        public override void Start()
        {
            base.Start();

            ModsLibCS1.UI.Builder ui = PanelLook.NewBuilder("the New flag window");
            size = new Vector2(PanelWidth, 200f);
            canFocus = true;
            isInteractive = true;
            name = ComponentName;

            // Background, title, close button and the drag handle over the title (ModsLibCS1).
            ModsLibCS1.UI.Frame frame = ui.AddFrame(this, SettingsCatalog.TabName(PanelPage.New), Hide);
            frame.Title.textScale = 1f;

            _rows = new RowSet(this, ui);
            _page = AddUIComponent<UIPanel>();
            _page.size = new Vector2(PanelWidth - 2f * Margin, 10f);
            _page.relativePosition = new Vector3(Margin, TitleBarHeight + 4f);
            _pageHeight = _rows.BuildRows(_page, PanelPage.New, 0f);
            _page.height = _pageHeight;
            height = TitleBarHeight + 4f + _pageHeight + Margin;
            _built = true;

            eventVisibilityChanged += (component, visible) =>
            {
                if (visible)
                {
                    _rows.RefreshValues();
                    _rows.RefreshStatus();
                    return;
                }

                // Closed: placing ends, and what was opened from here closes too.
                if (FlagTool.IsPlacing)
                    FlagTool.PutAway();
                LineFlags.PutAway();
                Settings.NewPlacing.value = LineFlags.Single;
                ColourPicker.Close();
                CountryPicker.Close();
                ImagePanel.CloseIf(PanelPage.New);
                PatternPanel.CloseIf(PanelPage.New);
                PolePanel.CloseIf(PanelPage.PoleNew);
                Log.Msg("flags: the New flag window closed");
            };

            eventKeyDown += (component, p) =>
            {
                if (p.used || p.keycode != KeyCode.Escape)
                    return;

                p.Use();
                Hide();
            };

            PlaceBesidePanel();
            _rows.RefreshValues();
            _rows.RefreshStatus();
        }

        public override void Update()
        {
            base.Update();

            if (!isVisible || !_built)
                return;

            // The tool put away by any means: the window goes with it. Line Tool placing a row
            // from here counts: it is this window's tool for as long as it has the flag.
            if (!FlagTool.IsPlacing && !LineFlags.Driving)
            {
                Hide();
                return;
            }

            // Esc with Line Tool out: kept from the game's pause menu, as FlagTool keeps it.
            if (LineFlags.Driving && Input.GetKeyDown(KeyCode.Escape) && !UIView.HasInputFocus())
                FlagsPanel.CatchEscape();

            if (Time.realtimeSinceStartup < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.realtimeSinceStartup + RefreshInterval;
            _rows.RefreshStatus();
            _rows.RefreshValues();
        }

        /// <summary>To the right of the panel (its left without room), level with its tabs.</summary>
        private void PlaceBesidePanel()
        {
            if (!_built)
                return; // Start places it

            UIView view = GetUIView();
            UIComponent panel = view.FindUIComponent(FlagsPanel.ComponentName);
            if (panel == null)
                return;

            // The screen as the UI sees it (1920 x 1080 only at 16:9: 1728 wide at 16:10).
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
