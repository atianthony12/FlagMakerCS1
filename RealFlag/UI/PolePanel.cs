using ColossalFramework.UI;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// The Flagpole popup, beside the window it was opened from: the pole the mod draws under the
    /// flag (none by default -- the author: "By default it's off, because the purpose of this mod
    /// is making the flag cloth adaptable to any pole"), its top, its ropes, colours and
    /// thickness, and the hoist's tilt and lean -- the pole's own angle, drawn or not. Opened from
    /// a Flagpole button: the New flag window's page edits the next flag, the Flags tab's the
    /// selected one.
    /// </summary>
    /// <remarks>
    /// A popup rather than rows in the windows: seven more rows would make the Flags tab taller
    /// than a 1080-line screen. Its rows are the catalog's (PanelPage.PoleNew and PoleExisting),
    /// built and refreshed by a <see cref="RowSet"/> as the panel's are; its colour swatches open
    /// the ColourPicker beside it.
    /// </remarks>
    public class PolePanel : UIPanel
    {
        private const float PanelWidth = 480f;
        private const float TitleBarHeight = 40f;
        private const float Margin = 14f;

        private static PolePanel _instance;

        private RowSet _rows;
        private UIPanel _create, _existing;
        private float _createHeight, _existingHeight;
        private UILabel _title;
        private PanelPage _page = PanelPage.PoleNew;
        private UIComponent _beside;
        private bool _built;

        /// <summary>Closes the popup if it shows <paramref name="page"/> (the window that page belongs to went away).</summary>
        public static void CloseIf(PanelPage page)
        {
            if (_instance != null && _instance.isVisible && _instance._page == page)
                Close();
        }

        /// <summary>Opens the popup on a window's page (PoleNew or PoleExisting) beside that window; the same page again closes it.</summary>
        public static void Toggle(PanelPage page, UIComponent beside)
        {
            if (_instance != null && _instance.isVisible && _instance._page == page)
            {
                Close();
                return;
            }

            if (_instance == null)
                _instance = UIView.GetAView().AddUIComponent(typeof(PolePanel)) as PolePanel;
            if (_instance == null)
                return;

            _instance._page = page;
            _instance._beside = beside;
            _instance.Show();
            _instance.ShowPage();
            _instance.BringToFront();
        }

        public static void Close()
        {
            if (_instance != null)
                _instance.Hide();
        }

        /// <summary>With the panel it belongs to.</summary>
        public static void DestroyPanel()
        {
            if (_instance != null)
                Object.Destroy(_instance.gameObject);
            _instance = null;
        }

        /// <summary>Re-reads its controls, if it is open (the panel's refresh calls it).</summary>
        public static void RefreshOpen()
        {
            if (_instance != null && _instance.isVisible && _instance._rows != null)
                _instance._rows.RefreshValues();
        }

        public override void Start()
        {
            base.Start();

            atlas = UIBuilder.Atlas;
            backgroundSprite = "MenuPanel2";
            size = new Vector2(PanelWidth, 200f);
            canFocus = true;
            isInteractive = true;
            name = "RealFlagPolePanel";

            _title = UIBuilder.AddLabel(this, "", new Vector3(Margin, Margin), 1f);
            _title.autoSize = false;
            _title.size = new Vector2(PanelWidth - 60f, 24f);

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

            _rows = new RowSet(this);
            _create = AddPage();
            _createHeight = _rows.BuildRows(_create, PanelPage.PoleNew, 0f);
            _existing = AddPage();
            _existingHeight = _rows.BuildRows(_existing, PanelPage.PoleExisting, 0f);
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

            ShowPage();
        }

        private UIPanel AddPage()
        {
            UIPanel page = AddUIComponent<UIPanel>();
            page.size = new Vector2(PanelWidth - 2f * Margin, 10f);
            page.relativePosition = new Vector3(Margin, TitleBarHeight + 4f);
            return page;
        }

        /// <summary>The page asked for, the popup as tall as it, beside the panel (to its right, or its left without room).</summary>
        private void ShowPage()
        {
            if (!_built)
                return; // Start has not run yet: it shows the page when it does

            bool create = _page == PanelPage.PoleNew;
            _create.isVisible = create;
            _existing.isVisible = !create;
            float content = create ? _createHeight : _existingHeight;
            (create ? _create : _existing).height = content;
            height = TitleBarHeight + 4f + content + Margin;
            _title.text = SettingsCatalog.TabName(_page);

            if (_beside != null)
            {
                UIView view = GetUIView();
                Vector3 p = _beside.relativePosition;
                float x = p.x + _beside.width + 6f;
                if (x + width > view.fixedWidth)
                    x = Mathf.Max(0f, p.x - width - 6f);
                relativePosition = new Vector3(x, Mathf.Clamp(p.y + 40f, 0f, Mathf.Max(0f, view.fixedHeight - height)));
            }

            ColourPicker.Close();
            _rows.RefreshValues();
        }

        public override void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            base.OnDestroy();
        }
    }
}
