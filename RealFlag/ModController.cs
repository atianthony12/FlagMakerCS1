using ColossalFramework;
using ColossalFramework.Globalization;
using ColossalFramework.UI;
using UnityEngine;
using RealFlag.Flags;
using RealFlag.UI;

namespace RealFlag
{
    /// <summary>
    /// Watches for the toggle hotkey, owns the panel and its button, and creates the flag
    /// renderer. One per loaded city (Loader).
    /// </summary>
    public class ModController : MonoBehaviour
    {
        public static ModController Instance { get; private set; }

        private FlagsPanel _panel;
        private ModButton _hudButton;
        private bool _hotkeyWasDown;

        /// <summary>Rebuilds the button after the UUI preference changes.</summary>
        public static void RefreshButton()
        {
            if (Instance != null)
                Instance.BuildButton();
        }

        /// <summary>Rebuilds the in-game panel (its words are fixed when it is built). Safe with no city loaded.</summary>
        public static void RebuildPanel()
        {
            if (Instance != null)
                Instance.CreatePanel();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            Settings.Init();
            Log.ReportLevel();
            SystemReport.Write();
            SettingsCatalog.LogLayout();

            gameObject.AddComponent<FlagRenderer>();

            CreatePanel();

            // The panel's words are fixed when it is built; when the game's language changes and
            // ours follows it, build it again. (A static event: removed in OnDestroy.)
            LocaleManager.eventLocaleChanged += OnLocaleChanged;

            BuildButton();
        }

        /// <summary>
        /// Creates the panel, replacing the one that exists. The replacement opens where the old
        /// one stood and stays open if it was.
        /// </summary>
        private void CreatePanel()
        {
            bool wasVisible = false;
            Vector3? position = null;

            if (_panel != null)
            {
                wasVisible = _panel.isVisible;
                position = _panel.relativePosition;
                _panel.eventVisibilityChanged -= OnPanelVisibilityChanged;
                Destroy(_panel.gameObject);
            }

            _panel = UIView.GetAView().AddUIComponent(typeof(FlagsPanel)) as FlagsPanel;
            if (_panel == null)
                return;

            _panel.InitialPosition = position;

            // Stays open if it was open. From the game's options screen (a modal) that needs
            // one more step: a new component is born on top of everything, so it stays open
            // BEHIND the options instead (Volumetric Weather's "should not open the modal").
            if (wasVisible)
            {
                _panel.Show();
                KeepModalOnTop();
            }
            else
            {
                _panel.Hide();
            }

            _panel.eventVisibilityChanged += OnPanelVisibilityChanged;

            // The old panel was unhooked before it went, so Unified UI heard nothing; tell it.
            UUIIntegration.SetPressed(wasVisible);
        }

        /// <summary>
        /// Puts the open modal (the options screen) and its dimming layer back above
        /// everything, our new panel included (UIView.BringToFront's own IL does the same).
        /// </summary>
        private static void KeepModalOnTop()
        {
            try
            {
                UIComponent modal = UIView.GetModalComponent();
                if (modal == null)
                    return;

                UIComponent root = modal.GetRootContainer() ?? modal;
                UIComponent effect = UIView.GetAView().panelsLibraryModalEffect;

                if (effect != null && effect != root && effect.isVisible)
                    effect.BringToFront();

                root.BringToFront();

                Log.Msg("panel: rebuilt behind the open '" + root.name + "'" +
                        (effect != null && effect.isVisible ? " and its dimming" : ""));
            }
            catch (System.Exception e)
            {
                Log.Warn("panel: could not keep the options screen on top: " + e.Message);
            }
        }

        private void Update()
        {
            // When the button is registered, Unified UI owns the hotkey (we hand it over as
            // UUIHotKeys.ActivationKey). Polling it here as well would toggle the panel twice
            // per press, which looks like the key doing nothing.
            if (UUIIntegration.IsRegistered)
                return;

            // Don't steal the key while a text field has focus or a modal is up.
            if (UIView.HasModalInput() || UIView.HasInputFocus())
            {
                _hotkeyWasDown = false;
                return;
            }

            bool down = IsHotkeyDown();
            if (down && !_hotkeyWasDown)
                SetPanelVisible(_panel != null && !_panel.isVisible);

            _hotkeyWasDown = down;
        }

        private static bool IsHotkeyDown()
        {
            SavedInputKey key = Settings.ToggleKey;
            if (key == null || key.Key == KeyCode.None)
                return false;

            bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            return Input.GetKey(key.Key)
                && control == key.Control
                && shift == key.Shift
                && alt == key.Alt;
        }

        private void BuildButton()
        {
            UUIIntegration.Unregister();
            DestroyHudButton();

            bool registered = false;
            if (Settings.ShowInUnifiedUI != null && Settings.ShowInUnifiedUI.value)
                registered = UUIIntegration.Register(IconLoader.Load(), OnUUIToggled);

            if (registered && _panel != null)
                UUIIntegration.SetPressed(_panel.isVisible);

            // Falling back also covers "wanted UUI but it isn't installed", which would
            // otherwise leave the player with no button at all.
            if (!registered)
                CreateHudButton();

            Log.Msg("panel button: " + (registered ? "Unified UI" : "HUD button") +
                    " (Unified UI wanted=" + (Settings.ShowInUnifiedUI != null && Settings.ShowInUnifiedUI.value) + ")");
        }

        private void CreateHudButton()
        {
            _hudButton = UIView.GetAView().AddUIComponent(typeof(ModButton)) as ModButton;
            if (_hudButton == null)
                return;

            _hudButton.OnClicked = () => SetPanelVisible(_panel == null || !_panel.isVisible);
        }

        /// <summary>Puts the HUD button where the settings say, after a Reset or a hand edit of the file.</summary>
        public static void PlaceHudButton()
        {
            if (Instance != null && Instance._hudButton != null)
                Instance._hudButton.Place();
        }

        private void DestroyHudButton()
        {
            if (_hudButton == null)
                return;

            Destroy(_hudButton.gameObject);
            _hudButton = null;
        }

        /// <summary>
        /// The panel is the single source of truth for "open". Unified UI keeps a flag of its
        /// own and toggles on that, so it is told about every change, whoever made it.
        /// </summary>
        private void OnPanelVisibilityChanged(UIComponent component, bool visible)
        {
            UUIIntegration.SetPressed(visible);
        }

        private void OnUUIToggled(bool active)
        {
            Log.Msg("panel: Unified UI toggled it " + (active ? "on" : "off"));
            SetPanelVisible(active);
        }

        public void SetPanelVisible(bool visible)
        {
            if (_panel == null)
                return;

            if (visible)
            {
                _panel.Show();
                _panel.BringToFront();
            }
            else
            {
                _panel.Hide();
            }
        }

        private void OnLocaleChanged()
        {
            if (Settings.Language != null && Settings.Language.value != 0)
                return; // a language chosen on the options page does not follow the game's

            Log.Msg("localization: the game's language changed to '" + Localization.CurrentCode + "'; rebuilding the panel");
            CreatePanel();
        }

        private void OnDestroy()
        {
            LocaleManager.eventLocaleChanged -= OnLocaleChanged;
            UUIIntegration.Unregister();
            DestroyHudButton();

            if (_panel != null)
            {
                Destroy(_panel.gameObject);
                _panel = null;
            }

            if (Instance == this)
                Instance = null;
        }
    }
}
