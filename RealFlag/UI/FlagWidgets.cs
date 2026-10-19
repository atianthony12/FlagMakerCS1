using System.Collections.Generic;
using ColossalFramework.UI;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// The panel's own small drawings, which no look of ModsLibCS1 has: colour swatches, blocks of
    /// one colour (<see cref="FlagIcon"/>, the folder's fallback). The rows, buttons, fields and
    /// frames are ModsLibCS1's (<see cref="PanelLook"/>); this was the UIBuilder before them.
    /// </summary>
    public static class FlagWidgets
    {
        /// <summary>
        /// A swatch: the game atlas's white panel sprite ("GenericPanelWhite", read out of the live
        /// atlas for Volumetric Weather) tinted with a colour, as a button, in every look: it shows
        /// a colour, not the look. The colour is sRGB.
        /// </summary>
        public static UIButton AddSwatch(UIComponent parent, Vector2 size, Vector3 position, uint rgb)
        {
            UIButton swatch = parent.AddUIComponent<UIButton>();
            swatch.atlas = ModsLibCS1.UI.Atlas.Game;
            swatch.size = size;
            swatch.relativePosition = position;
            swatch.normalBgSprite = SwatchSprite;
            swatch.hoveredBgSprite = SwatchSprite;
            swatch.pressedBgSprite = SwatchSprite;
            swatch.focusedBgSprite = SwatchSprite;
            swatch.disabledBgSprite = SwatchSprite;
            swatch.canFocus = false;
            SetSwatch(swatch, rgb);
            return swatch;
        }

        public const string SwatchSprite = "GenericPanelWhite";

        /// <summary>Tints a swatch; a hovered swatch is a shade lighter, so it reads as clickable.</summary>
        public static void SetSwatch(UIButton swatch, uint rgb)
        {
            Color32 c = Swatch(rgb);
            swatch.color = c;
            swatch.focusedColor = c;
            swatch.pressedColor = c;
            swatch.hoveredColor = Color32.Lerp(c, new Color32(255, 255, 255, 255), 0.25f);
        }

        public static Color32 Swatch(uint rgb)
        {
            return new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 255);
        }

        private static readonly Dictionary<int, Texture2D> Solids = new Dictionary<int, Texture2D>();

        /// <summary>
        /// A texture of one colour, a pixel stretched to any size: the panel's small drawings
        /// (FlagIcon) at sizes the atlas's sliced sprites do not keep square at. Made once a
        /// colour, kept through the cities (Resources.UnloadUnusedAssets passes HideAndDontSave).
        /// </summary>
        public static Texture2D Solid(Color32 colour)
        {
            int key = (colour.r << 24) | (colour.g << 16) | (colour.b << 8) | colour.a;
            Texture2D texture;
            if (Solids.TryGetValue(key, out texture) && texture != null)
                return texture;

            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "RealFlag solid",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixel(0, 0, colour);
            texture.Apply(false, false);
            Solids[key] = texture;
            return texture;
        }

        /// <summary>A block of one colour on a component, not in the pointer's way.</summary>
        public static UITextureSprite AddBlock(UIComponent parent, Color32 colour)
        {
            UITextureSprite block = parent.AddUIComponent<UITextureSprite>();
            block.texture = Solid(colour);
            block.isInteractive = false;
            return block;
        }
    }

    /// <summary>
    /// A folder drawn on a button -- the images folder's button (the author: "a folder icon which
    /// opens where the user should drag images", as Procedural Objects and Image Overlay have):
    /// this mod's own Lucide "folder-open" (Resources\PanelIcons, merged into ModsLibCS1's atlas)
    /// in gold, in every look; without the library's atlas a folder of blocks as
    /// <see cref="FlagIcon"/> is, its back with its tab and its front a shade lighter.
    /// </summary>
    public sealed class FolderIcon
    {
        public const string Sprite = "icon-folder-open";

        private static readonly Color32 BackColour = new Color32(196, 146, 44, 255);
        private static readonly Color32 FrontColour = new Color32(242, 193, 78, 255);

        /// <param name="parent">The button it is drawn on.</param>
        /// <param name="at">Its top left corner on it; <paramref name="size"/> its size (18 x 14 reads well).</param>
        public FolderIcon(UIComponent parent, Vector3 at, Vector2 size)
        {
            float w = Mathf.Round(size.x), h = Mathf.Round(size.y);

            UITextureAtlas ours = ModsLibCS1.UI.Atlas.Ours;
            if (ours != null && ours[Sprite] != null)
            {
                // The icon is square (24 units, a 2-unit margin): as wide as the box, centred on it.
                UISprite icon = parent.AddUIComponent<UISprite>();
                icon.atlas = ours;
                icon.spriteName = Sprite;
                icon.color = FrontColour;
                icon.size = new Vector2(w, w);
                icon.relativePosition = at + new Vector3(0f, Mathf.Round(0.5f * (h - w)));
                icon.isInteractive = false;
                return;
            }

            float tab = Mathf.Max(2f, Mathf.Round(h * 0.2f));

            UITextureSprite back = FlagWidgets.AddBlock(parent, BackColour);
            back.size = new Vector2(w, h - tab);
            back.relativePosition = at + new Vector3(0f, tab);

            UITextureSprite ear = FlagWidgets.AddBlock(parent, BackColour);
            ear.size = new Vector2(Mathf.Round(w * 0.42f), tab + 1f);
            ear.relativePosition = at;

            UITextureSprite front = FlagWidgets.AddBlock(parent, FrontColour);
            front.size = new Vector2(w, Mathf.Round((h - tab) * 0.72f));
            front.relativePosition = at + new Vector3(0f, h - front.height);
        }
    }

    /// <summary>
    /// A little flag drawn in a box on a button: its cloth at a shape as seen (height over width)
    /// and the edge it is hoisted from, a gilt bar along its side or its top -- the panel's shapes
    /// and its Hoisted from choice (the author: "maybe add a small square/rectangle/etc icons
    /// based on the ratio"). Drawn of blocks, not an icon: its shape is the flag's, any shape.
    /// </summary>
    public sealed class FlagIcon
    {
        /// <summary>The cloth, light as a flag on the dark button; the bar, the pole's gilt (FlagSpec.DefaultTopColour).</summary>
        private static readonly Color32 ClothColour = new Color32(214, 226, 238, 255);
        private static readonly Color32 BarColour = new Color32(212, 175, 55, 255);

        private readonly UITextureSprite _cloth, _bar;
        private readonly Vector3 _at;
        private readonly Vector2 _box;

        /// <param name="parent">The button it is drawn on.</param>
        /// <param name="at">The box's top left corner on it.</param>
        /// <param name="box">The most it may take.</param>
        public FlagIcon(UIComponent parent, Vector3 at, Vector2 box)
        {
            _at = at;
            _box = box;
            _cloth = FlagWidgets.AddBlock(parent, ClothColour);
            _bar = FlagWidgets.AddBlock(parent, BarColour);
        }

        /// <summary>The flag at <paramref name="ratio"/> (height over width, as seen), as large as the box takes, hoisted from <paramref name="hoist"/> (FlagHoist).</summary>
        public void Show(float ratio, int hoist)
        {
            ratio = Mathf.Max(ratio, 0.01f);
            float w = _box.x, h = _box.x * ratio;
            if (h > _box.y)
            {
                h = _box.y;
                w = _box.y / ratio;
            }

            w = Mathf.Max(3f, Mathf.Round(w));
            h = Mathf.Max(3f, Mathf.Round(h));
            Vector3 corner = _at + new Vector3(Mathf.Round(0.5f * (_box.x - w)), Mathf.Round(0.5f * (_box.y - h)));
            _cloth.size = new Vector2(w, h);
            _cloth.relativePosition = corner;

            // The bar a pixel proud of the cloth, as the pole or the crossbar it hangs from.
            if (hoist == Flags.FlagHoist.Top)
            {
                _bar.size = new Vector2(w + 4f, 2f);
                _bar.relativePosition = corner + new Vector3(-2f, -1f);
            }
            else
            {
                _bar.size = new Vector2(2f, h + 4f);
                _bar.relativePosition = corner + new Vector3(-1f, -2f);
            }
        }
    }
}
