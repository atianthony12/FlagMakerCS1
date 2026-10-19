using ColossalFramework.UI;
using ModsLibCS1.UI;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// The panel's look: the "Panel look" row (Settings.PanelSkin) read as a ModsLibCS1 skin, a
    /// Builder for it, and the few parts this mod's windows draw that the library has no row for
    /// -- a button lit as a choice made, the lists' rows, boxes and bars. Every window (the panel,
    /// the New flag window, the Flagpole popup, the pickers, the Image window) and the HUD button
    /// draw through ModsLibCS1; a change of look rebuilds them. (Volumetric Weather's and Real
    /// Glass's PanelLook.) Vanilla draws each part as the panel did before.
    /// </summary>
    internal static class PanelLook
    {
        private static bool _sinkSet;

        /// <summary>The picked skin; a number this version doesn't know (a newer file) is the default.</summary>
        public static Skin Skin
        {
            get
            {
                int value = Settings.PanelSkin != null ? Settings.PanelSkin.value : Settings.Defaults.PanelSkin;
                switch (value)
                {
                    case (int)Skin.SeeThrough:
                    case (int)Skin.GameBlue:
                    case (int)Skin.Vanilla:
                    case (int)Skin.Frosted:
                        return (Skin)value;
                    default:
                        return (Skin)Settings.Defaults.PanelSkin;
                }
            }
        }

        /// <summary>A Builder for the picked skin: Vanilla's, logged, if the library's sprites can't be had.</summary>
        public static Builder NewBuilder(string who)
        {
            if (!_sinkSet)
            {
                _sinkSet = true;
                LibLog.Sink = Log.Msg;
            }

            Builder builder = Builder.For(Skin);
            Log.Msg("panel look: " + who + " " + builder.Skin + (builder.Skin != Skin ? " (asked for " + Skin + ")" : ""));
            return builder;
        }

        private static bool Vanilla(Builder ui)
        {
            return ui.Skin == Skin.Vanilla;
        }

        /// <summary>
        /// A button of a row of choices lit (the choice made, the shape the flag has) or not: the
        /// game's focused button in Vanilla, the look's accent in the others.
        /// </summary>
        public static void SetLit(Builder ui, UIButton button, bool lit)
        {
            if (!Vanilla(ui))
            {
                ui.SetAccent(button, lit);
                return;
            }

            button.normalBgSprite = lit ? "ButtonMenuFocused" : "ButtonMenu";
            button.focusedBgSprite = button.normalBgSprite;
            button.textColor = lit ? new Color32(255, 255, 255, 255) : new Color32(210, 225, 240, 255);
        }

        /// <summary>
        /// The box a list's rows stand in (the flags, the countries, the pictures): the game's dark
        /// panel in Vanilla, the look's list in the others.
        /// </summary>
        public static void DressListBox(Builder ui, UIPanel box)
        {
            SkinSprites s = ui.Theme.Sprites;
            box.atlas = ui.Atlas;
            box.backgroundSprite = Vanilla(ui) || s.List == null ? "GenericPanelDark" : s.List;
        }

        /// <summary>A row of a list, as a button: the game's menu button in Vanilla, the look's list item in the others.</summary>
        public static void DressListRow(Builder ui, UIButton row)
        {
            row.atlas = ui.Atlas;
            row.canFocus = false;
            if (Vanilla(ui))
            {
                row.normalBgSprite = "ButtonMenu";
                row.hoveredBgSprite = "ButtonMenuHovered";
                row.pressedBgSprite = "ButtonMenuPressed";
                row.focusedBgSprite = "ButtonMenu";
            }
            else
            {
                SkinSprites s = ui.Theme.Sprites;
                row.hoveredBgSprite = s.ListHover;
                row.pressedBgSprite = s.ListHighlight;
            }

            SetListLit(ui, row, false);
        }

        /// <summary>A list's row lit (the flag selected, the country or picture chosen) or not.</summary>
        public static void SetListLit(Builder ui, UIButton row, bool lit)
        {
            if (Vanilla(ui))
            {
                SetLit(ui, row, lit);
                return;
            }

            SkinSprites s = ui.Theme.Sprites;
            row.normalBgSprite = lit ? s.ListHighlight : string.Empty;
            row.focusedBgSprite = row.normalBgSprite;
            Color32 text = lit ? ui.Theme.TabSelectedText : ui.Theme.Text;
            ui.Paint(row, text);
            row.hoveredTextColor = ui.Theme.TabSelectedText;
            row.pressedTextColor = ui.Theme.TabSelectedText;
            row.focusedTextColor = text;
        }

        /// <summary>The thin bar a list scrolls with, beside it in the look's colours: 10 wide in Vanilla, 6 in the others.</summary>
        public static float ListBarWidth(Builder ui)
        {
            return Vanilla(ui) || ModsLibCS1.UI.Atlas.Ours == null ? 10f : Builder.ScrollBarWidth;
        }

        /// <summary>
        /// A list's vertical bar, <paramref name="size"/> big at <paramref name="position"/> in
        /// <paramref name="parent"/>: the game's scroll bar in Vanilla; the library's pill, tinted,
        /// in the others (the game's sprites when the library's sheet is missing).
        /// </summary>
        public static UIScrollbar AddListBar(Builder ui, UIComponent parent, Vector2 size, Vector3 position)
        {
            UIScrollbar bar = parent.AddUIComponent<UIScrollbar>();
            bar.size = size;
            bar.relativePosition = position;
            bar.orientation = UIOrientation.Vertical;
            bar.minValue = 0f;
            bar.stepSize = 1f;

            UITextureAtlas ours = ModsLibCS1.UI.Atlas.Ours;
            bool own = !Vanilla(ui) && ours != null && ours[Builder.ScrollSprite] != null;

            UISlicedSprite track = bar.AddUIComponent<UISlicedSprite>();
            track.atlas = own ? ours : ModsLibCS1.UI.Atlas.Game;
            track.spriteName = own ? Builder.ScrollSprite : "ScrollbarTrack";
            if (own)
                track.color = ui.Theme.ScrollTrack;
            track.size = size;
            track.relativePosition = Vector3.zero;
            bar.trackObject = track;

            UISlicedSprite thumb = track.AddUIComponent<UISlicedSprite>();
            thumb.atlas = track.atlas;
            thumb.spriteName = own ? Builder.ScrollSprite : "ScrollbarThumb";
            if (own)
            {
                Color32 normal = ui.Theme.ScrollThumb, lit = ui.Theme.ScrollThumbHover;
                thumb.color = normal;
                thumb.minimumSize = new Vector2(size.x, 24f);
                bar.eventMouseEnter += (component, e) => thumb.color = lit;
                bar.eventMouseLeave += (component, e) => thumb.color = normal;
            }
            thumb.size = new Vector2(size.x, 30f);
            thumb.relativePosition = Vector3.zero;
            bar.thumbObject = thumb;
            return bar;
        }

        /// <summary>
        /// A popup's place beside the window it was opened from: to its right, or its left without
        /// room, <paramref name="down"/> below its top, on the screen as the UI sees it (1920 x 1080
        /// only at 16:9: 1728 wide at 16:10).
        /// </summary>
        public static void PlaceBeside(UIComponent popup, UIComponent beside, float down)
        {
            if (beside == null)
                return;

            Vector2 screen = popup.GetUIView().GetScreenResolution();
            Vector3 p = beside.relativePosition;
            float x = p.x + beside.width + 6f;
            if (x + popup.width > screen.x)
                x = Mathf.Max(0f, p.x - popup.width - 6f);
            popup.relativePosition = new Vector3(x, Mathf.Clamp(p.y + down, 0f, Mathf.Max(0f, screen.y - popup.height)));
        }
    }
}
