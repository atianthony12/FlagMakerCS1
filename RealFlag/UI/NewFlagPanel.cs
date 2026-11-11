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
        /// The "+ New flag" button: opens the window (and the tool) on the last new flag's settings,
        /// or closes it if it is open. Copy opens it with the selected flag's design instead
        /// (<see cref="Open"/> alone). From 11-07 it started from the defaults; the same day the
        /// author took it back ("+ New flag should actually remember the last settings as it did
        /// before. I found that better UX-wise"): each row's reset icon puts one back.
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

            Open();
        }

        /// <summary>
        /// Opens the window in the panel's place -- the panel steps aside until it closes (the
        /// author, 2026-11-08: "+New Flag should close the main panel so not to overwhelm the user
        /// with many panels"; Copy too) -- and takes the tool out to place (a selected flag is let
        /// go: placing takes the tool).
        /// </summary>
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
            _instance.PlaceOverPanel();
            FlagsPanel.StepAside();
            _instance.Show();
            _instance.BringToFront();
            _instance.Focus(); // before its Start (the first time) it cannot: Update takes it then
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

        /// <summary>Esc with the tool out and the panel aside: this window takes the focus and the key, as the panel would (FlagsPanel.CatchEscape).</summary>
        public static bool CatchEscape()
        {
            NewFlagPanel panel = _instance;
            if (panel == null || !panel.isVisible || !panel.isEnabled || UIView.HasModalInput())
                return false;

            if (!panel.containsFocus)
                panel.Focus();
            return panel.containsFocus;
        }

        /// <summary>Re-reads its controls, if it is open (the panel's refresh calls it).</summary>
        public static void RefreshOpen()
        {
            if (_instance != null && _instance.isVisible && _instance._rows != null)
            {
                _instance.Relayout();
                _instance._rows.RefreshValues();
            }
        }

        /// <summary>
        /// Rows shown only while something holds (Row.ShownWhile: Recapture while the Motion is
        /// Loop) shown or hidden, the window as tall as that, its bottom kept on the screen.
        /// </summary>
        private void Relayout()
        {
            if (!_built || !_rows.Layout())
                return;

            float? content = _rows.HeightOf(_page);
            if (!content.HasValue)
                return;

            _pageHeight = content.Value;
            _page.height = _pageHeight;
            height = TitleBarHeight + 4f + _pageHeight + Margin;
            Vector2 screen = GetUIView().GetScreenResolution();
            if (relativePosition.y + height > screen.y)
                relativePosition = new Vector3(relativePosition.x, Mathf.Max(0f, screen.y - height));
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
                    Relayout();
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

                // The panel it stood in for comes back where it stands.
                FlagsPanel.ComeBack(relativePosition);
            };

            eventKeyDown += (component, p) =>
            {
                if (p.used || p.keycode != KeyCode.Escape)
                    return;

                // As the panel's Esc: a right-drag turn given up first, else the tool (and with
                // it this window) put away.
                p.Use();
                string step = LineFlags.Driving ? LineFlags.PutAway() : FlagTool.IsPlacing ? FlagTool.StepBack() : null;
                if (step == null)
                    Hide();
                Unfocus();
                Log.Msg("flags: Esc in the New flag window: " + (step ?? "closed") + " (the key kept from the game's pause menu)");
            };

            PlaceOverPanel();
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

            // The window holds the focus while nothing else does, so Esc comes to it, not to the
            // game's shortcuts (its pause menu: the author, 2026-11-08, "Esc ... going back
            // correctly to the main panel but the game's Pause Menu is opening"). The game hands a
            // key to its shortcuts in OnGUI unless a focused component uses it, and nothing was
            // focused: "+ New flag" is clicked on the panel, which takes the focus with it as it
            // steps aside (UIComponent.OnVisibilityChanged), and a click on the map lets it go.
            // Unused keys still reach the shortcuts (UIInput.ProcessKeyEvent), and a text field
            // or picker keeps the focus it has.
            if (UIView.activeComponent == null && !UIView.HasModalInput())
                Focus();

            // Esc with Line Tool out: kept from the game's pause menu, as FlagTool keeps it.
            if (LineFlags.Driving && Input.GetKeyDown(KeyCode.Escape) && !UIView.HasInputFocus())
                FlagsPanel.CatchEscape();

            if (Time.realtimeSinceStartup < _nextRefreshTime)
                return;

            _nextRefreshTime = Time.realtimeSinceStartup + RefreshInterval;
            _rows.RefreshStatus();
            Relayout();
            _rows.RefreshValues();
        }

        /// <summary>Where the panel stands (it steps aside for this window), its bottom kept on the screen.</summary>
        private void PlaceOverPanel()
        {
            if (!_built)
                return; // Start places it

            Vector3? place = FlagsPanel.Place;
            if (!place.HasValue)
                return;

            // The screen as the UI sees it (1920 x 1080 only at 16:9: 1728 wide at 16:10).
            Vector2 screen = GetUIView().GetScreenResolution();
            Vector3 p = place.Value;
            relativePosition = new Vector3(Mathf.Clamp(p.x, 0f, Mathf.Max(0f, screen.x - width)),
                                           Mathf.Clamp(p.y, 0f, Mathf.Max(0f, screen.y - height)));
        }

        public override void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            base.OnDestroy();
        }
    }
}
