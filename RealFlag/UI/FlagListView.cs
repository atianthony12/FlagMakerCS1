using System.Collections.Generic;
using ColossalFramework.UI;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// The Flags tab's list: every flag in the city, a row each -- its name, its pattern (its
    /// country, its picture) and its width by height as seen, its three colours (its image) -- in a
    /// scrolling box. A click selects the flag (FlagEditor) and the controls below take it over.
    /// </summary>
    public class FlagListView : UIPanel
    {
        public const float RowHeight = 24f;
        public const int VisibleRows = 5;

        private UIScrollablePanel _list;
        private UIScrollbar _bar;
        private UILabel _empty;

        private readonly List<FlagEntry> _entries = new List<FlagEntry>();
        private readonly List<UIButton> _rows = new List<UIButton>();
        private readonly List<uint> _shown = new List<uint>();
        private int _shownVersion = -1;
        private int _shownImages = -1;
        private uint _shownSelected = uint.MaxValue;

        /// <summary>Builds the box inside a page, <paramref name="width"/> wide.</summary>
        public void Build(float width)
        {
            size = new Vector2(width, RowHeight * VisibleRows + 6f);
            atlas = UIBuilder.Atlas;
            backgroundSprite = "GenericPanelDark";

            _list = AddUIComponent<UIScrollablePanel>();
            _list.size = new Vector2(width - 16f, height - 6f);
            _list.relativePosition = new Vector3(3f, 3f);
            _list.autoLayout = true;
            _list.autoLayoutDirection = LayoutDirection.Vertical;
            _list.autoLayoutPadding = new RectOffset(0, 0, 0, 0);
            _list.scrollWheelDirection = UIOrientation.Vertical;
            _list.scrollWheelAmount = (int)RowHeight;
            _list.clipChildren = true;

            _bar = AddUIComponent<UIScrollbar>();
            _bar.size = new Vector2(10f, height - 6f);
            _bar.relativePosition = new Vector3(width - 12f, 3f);
            _bar.orientation = UIOrientation.Vertical;
            _bar.minValue = 0f;
            _bar.value = 0f;
            _bar.incrementAmount = RowHeight;
            _bar.autoHide = true;

            UISlicedSprite track = _bar.AddUIComponent<UISlicedSprite>();
            track.atlas = UIBuilder.Atlas;
            track.spriteName = "ScrollbarTrack";
            track.size = _bar.size;
            track.relativePosition = Vector3.zero;
            _bar.trackObject = track;

            UISlicedSprite thumb = track.AddUIComponent<UISlicedSprite>();
            thumb.atlas = UIBuilder.Atlas;
            thumb.spriteName = "ScrollbarThumb";
            thumb.size = new Vector2(10f, 20f);
            thumb.relativePosition = Vector3.zero;
            _bar.thumbObject = thumb;

            _list.verticalScrollbar = _bar;

            _empty = UIBuilder.AddLabel(this, Localization.Get("Existing.Empty"), new Vector3(10f, 10f), 0.75f);
            _empty.autoSize = false;
            _empty.wordWrap = true;
            _empty.size = new Vector2(width - 20f, height - 20f);
            _empty.textColor = UIBuilder.StatusColour;
        }

        /// <summary>Brings the rows up to date with the city's flags; cheap when nothing changed.</summary>
        public void Refresh()
        {
            FlagRenderer renderer = FlagRenderer.Instance;
            if (_list == null)
                return;

            if (renderer == null)
                _entries.Clear();
            else
                renderer.List(_entries);

            if (Changed())
                Rebuild();

            if (_shownSelected != FlagEditor.Selected)
                Highlight();

            _empty.isVisible = _entries.Count == 0;
        }

        /// <summary>A flag came or went, or one's design changed (the registry's version moved).</summary>
        private bool Changed()
        {
            // A picture's textures made again (its file changed): its thumbnail with them.
            if (_shownVersion != FlagRegistry.Version || _shownImages != FlagImages.UserVersion || _entries.Count != _shown.Count)
                return true;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Prop != _shown[i])
                    return true;
            }

            return false;
        }

        private void Rebuild()
        {
            foreach (UIButton row in _rows)
            {
                _list.RemoveUIComponent(row);
                Object.Destroy(row.gameObject);
            }

            _rows.Clear();
            _shown.Clear();

            float width = _list.width;
            foreach (FlagEntry e in _entries)
            {
                UIButton row = _list.AddUIComponent<UIButton>();
                row.atlas = UIBuilder.Atlas;
                row.size = new Vector2(width, RowHeight);
                FlagCountry country = FlagCountries.Find(e.Spec.Country);
                float shownWidth, shownHeight;
                FlagSize.Shown(e.Spec.Length, e.Spec.Proportion, e.Spec.HoistedFrom, out shownWidth, out shownHeight);
                string design = country != null ? country.Name : e.Spec.HasImage ? e.Spec.Image : Localization.Get(PatternKey(e.Spec.Pattern));
                row.text = FlagEditor.Title(e) + "  ·  " + design + ", " + Localization.Get("Unit.Size", shownWidth.ToString("0.#"), shownHeight.ToString("0.#"));
                row.textScale = 0.72f;
                row.textHorizontalAlignment = UIHorizontalAlignment.Left;
                row.textPadding = new RectOffset(8, 64, 4, 0);
                row.normalBgSprite = "ButtonMenu";
                row.hoveredBgSprite = "ButtonMenuHovered";
                row.pressedBgSprite = "ButtonMenuPressed";
                row.focusedBgSprite = "ButtonMenu";
                row.canFocus = false;

                // At the right: a country's flag, the player's picture, or the pattern's colours.
                Texture2D thumb = country != null ? FlagImages.Thumbnail(country.Image) : e.Spec.HasImage ? FlagImages.UserPreview(e.Spec.Image) : null;
                if (thumb != null)
                {
                    float scale = Mathf.Min(40f / Mathf.Max(1, thumb.width), 16f / Mathf.Max(1, thumb.height));
                    UITextureSprite image = row.AddUIComponent<UITextureSprite>();
                    image.texture = thumb;
                    image.size = new Vector2(Mathf.Round(thumb.width * scale), Mathf.Round(thumb.height * scale));
                    image.relativePosition = new Vector3(width - 60f, Mathf.Round(0.5f * (RowHeight - image.height)));
                    image.isInteractive = false;
                }
                else
                {
                    uint[] colours = { e.Spec.Colour1, e.Spec.Colour2, e.Spec.Colour3 };
                    int used = FlagPattern.ColoursUsed(e.Spec.Pattern);
                    for (int c = 0; c < used; c++)
                    {
                        UIButton swatch = UIBuilder.AddSwatch(row, new Vector2(16f, 14f), new Vector3(width - 60f + c * 19f, 5f), colours[c]);
                        swatch.isInteractive = false;
                    }
                }

                // A click selects; a click on the selected one lets it go.
                uint prop = e.Prop;
                row.eventClick += (component, p) =>
                {
                    if (FlagEditor.Selected == prop)
                        FlagEditor.Deselect();
                    else
                        FlagEditor.Select(prop);
                    Highlight();
                };

                _rows.Add(row);
                _shown.Add(prop);
            }

            _shownVersion = FlagRegistry.Version;
            _shownImages = FlagImages.UserVersion;
            _shownSelected = uint.MaxValue;
            Highlight();
        }

        private void Highlight()
        {
            for (int i = 0; i < _rows.Count && i < _shown.Count; i++)
            {
                bool selected = _shown[i] == FlagEditor.Selected;
                _rows[i].normalBgSprite = selected ? "ButtonMenuFocused" : "ButtonMenu";
                _rows[i].focusedBgSprite = _rows[i].normalBgSprite;
                _rows[i].textColor = selected ? new Color32(255, 255, 255, 255) : new Color32(210, 225, 240, 255);
            }

            _shownSelected = FlagEditor.Selected;
        }

        private static string PatternKey(int pattern)
        {
            for (int i = 0; i < FlagPattern.Values.Length; i++)
            {
                if (FlagPattern.Values[i] == pattern)
                    return FlagPattern.NameKeys[i];
            }

            return FlagPattern.NameKeys[0];
        }
    }
}
