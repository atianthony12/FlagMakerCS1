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

        /// <summary>True if the panel's atlas has this icon (ModsLibCS1 merges Resources\PanelIcons into it).</summary>
        public static bool HasIcon(string sprite)
        {
            UITextureAtlas atlas = ModsLibCS1.UI.Atlas.Ours;
            return atlas != null && atlas[sprite] != null;
        }

        /// <summary>A white icon of the panel's atlas on a component, not in the pointer's way.</summary>
        public static UISprite AddIcon(UIComponent parent, string sprite, Vector3 at, float size)
        {
            UISprite icon = parent.AddUIComponent<UISprite>();
            icon.atlas = ModsLibCS1.UI.Atlas.Ours;
            icon.spriteName = sprite;
            icon.size = new Vector2(size, size);
            icon.relativePosition = at;
            icon.isInteractive = false;
            return icon;
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

    /// <summary>
    /// A little 2:3 flag cut to a FlagCut on a button -- the Shape row's (FlagCut's outlines,
    /// drawn from FlagCut.Edge): the cloth light as <see cref="FlagIcon"/>'s with its gilt bar,
    /// hoisted from the flag's own edge, at twice the UI's units (sharp at 4K), the edge
    /// anti-aliased (4 x 4 samples a pixel). Drawn again only when what it shows changes. It owns
    /// its texture.
    /// </summary>
    public sealed class CutIcon
    {
        private const int PixelsPerUnit = 2;
        private static readonly Color32 ClothColour = new Color32(214, 226, 238, 255);
        private static readonly Color32 BarColour = new Color32(212, 175, 55, 255);

        private readonly OwnedTextureSprite _sprite;
        private readonly Vector2 _box;
        /// <summary>The hoist's bar on a hold button: a pole's grey, so the gold there is the held edge alone.</summary>
        private static readonly Color32 PoleColour = new Color32(140, 146, 156, 255);

        private int _cut = -1, _hoist = -1, _held = -1;
        private float _depth = -1f;

        /// <summary>
        /// A held edge's gold, texture pixels from the edge in (the hold row's, FlagHold): 3 UI
        /// pixels at 2 a unit (the author, 2026-11-11: 2.5, 1.25 UI pixels, was "very small and not
        /// easily noticeable").
        /// </summary>
        private const float HeldPixels = 6f;

        /// <summary>A held corner's gold runs this share of the flag along each edge from it.</summary>
        private const float HeldCorner = 0.38f;

        /// <param name="parent">The button it is drawn on.</param>
        /// <param name="at">The box's top left corner on it.</param>
        /// <param name="box">Its size.</param>
        public CutIcon(UIComponent parent, Vector3 at, Vector2 box)
        {
            _box = box;
            _sprite = parent.AddUIComponent<OwnedTextureSprite>();
            _sprite.isInteractive = false;
            _sprite.size = box;
            _sprite.relativePosition = at;
        }

        /// <param name="held">FlagHold bits drawn in gold along their edges (the hold row's buttons); 0 none.</param>
        public void Show(int cut, float depth, int hoist, int held = 0)
        {
            Texture2D texture = _sprite.Own;
            if (cut == _cut && hoist == _hoist && held == _held && Mathf.Abs(depth - _depth) < 1e-3f && texture != null)
                return;

            _cut = cut;
            _depth = depth;
            _hoist = hoist;
            _held = held;
            int w = Mathf.Max(4, Mathf.RoundToInt(_box.x * PixelsPerUnit)), h = Mathf.Max(4, Mathf.RoundToInt(_box.y * PixelsPerUnit));
            if (texture == null || texture.width != w || texture.height != h)
            {
                _sprite.Release();
                texture = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
                {
                    name = "RealFlag cut icon",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _sprite.Own = texture;
            }

            bool top = hoist == Flags.FlagHoist.Top;

            // The flag 2:3 as seen (a banner from its top 3:2 tall), as large as the box takes,
            // the bar's two pixels kept clear on the hoist's side.
            float seen = top ? 1.5f : 2f / 3f;
            float bar = 2f * PixelsPerUnit;
            float fw = w - bar - 2f, fh = fw * seen;
            if (fh > h - 2f)
            {
                fh = h - 2f;
                fw = fh / seen;
            }

            float fx = top ? Mathf.Round(0.5f * (w - fw)) : Mathf.Round(0.5f * (w - fw - bar)) + bar;
            float fy = top ? Mathf.Round(0.5f * (h - fh - bar)) + bar : Mathf.Round(0.5f * (h - fh));
            var pixels = new Color32[w * h];
            const int S = 4;
            for (int row = 0; row < h; row++)
            {
                for (int x = 0; x < w; x++)
                {
                    // The bar the flag hangs from, a pixel proud of the cloth.
                    float py = h - 1 - row;
                    bool onBar = top ? py >= fy - bar && py < fy && x >= fx - 2f && x < fx + fw + 2f
                                     : x >= fx - bar && x < fx && py >= fy - 2f && py < fy + fh + 2f;
                    if (onBar)
                    {
                        // On a hold button the pole's side is grey: only the edge it holds is gold.
                        pixels[row * w + x] = held != 0 ? PoleColour : BarColour;
                        continue;
                    }

                    int inside = 0;
                    for (int sy = 0; sy < S; sy++)
                    {
                        for (int sx = 0; sx < S; sx++)
                        {
                            float ax = (x + (sx + 0.5f) / S - fx) / fw, ay = (py + (sy + 0.5f) / S - fy) / fh;
                            if (ax < 0f || ax >= 1f || ay < 0f || ay >= 1f)
                                continue;

                            // From the top, the cloth's u runs down and its v across.
                            float u = top ? ay : ax, v = top ? ax : ay;
                            if (Flags.FlagCut.Inside(cut, depth, u, v))
                                inside++;
                        }
                    }

                    Color32 c = ClothColour;
                    c.a = (byte)(255 * inside / (S * S));
                    if (inside > 0 && held != 0 && Held(held, cut, depth, top, x + 0.5f - fx, py + 0.5f - fy, fw, fh))
                        c = new Color32(BarColour.r, BarColour.g, BarColour.b, c.a);
                    pixels[row * w + x] = c;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
            _sprite.texture = texture;
        }

        /// <summary>
        /// The pixel at (x, y) from the flag's corner (fw x fh as seen) lies on a held edge: within
        /// <see cref="HeldPixels"/> of the cloth's first or last row, of its fly's end (the cut's
        /// outline, FlagCut.Edge), or of either near the far corners.
        /// </summary>
        private static bool Held(int held, int cut, float depth, bool top, float x, float y, float fw, float fh)
        {
            float ax = x / fw, ay = y / fh;
            float u = top ? ay : ax, v = top ? ax : ay;
            float perU = top ? fh : fw, perV = top ? fw : fh;
            float end = Flags.FlagCut.Edge(cut, depth, Mathf.Clamp01(v));
            bool first = v * perV < HeldPixels, last = (1f - v) * perV < HeldPixels, fly = (end - u) * perU < HeldPixels;
            if ((held & Flags.FlagHold.TopEdge) != 0 && first) return true;
            if ((held & Flags.FlagHold.BottomEdge) != 0 && last) return true;
            if ((held & Flags.FlagHold.FlyEdge) != 0 && fly) return true;
            if ((held & Flags.FlagHold.FlyCorners) != 0 &&
                ((fly && (v < HeldCorner || v > 1f - HeldCorner)) || ((first || last) && u > end - HeldCorner)))
                return true;
            return false;
        }
    }

    /// <summary>A texture sprite that owns its texture: destroyed with it (a <see cref="CutIcon"/>'s).</summary>
    public class OwnedTextureSprite : UITextureSprite
    {
        public Texture2D Own;

        public void Release()
        {
            if (Own != null)
                Object.Destroy(Own);
            Own = null;
        }

        public override void OnDestroy()
        {
            Release();
            base.OnDestroy();
        }
    }
}
