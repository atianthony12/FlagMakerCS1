using System;
using RealFlag.Flags;

namespace RealFlag.UI
{
    /// <summary>
    /// The settings one window's picture works through -- the New flag window's (the next flag's,
    /// kept in RealFlag.xml) or the Flags tab's (the selected flag's, transient) -- and what the
    /// Image window does to them: pick a picture, lay it on the flag, turn it.
    /// </summary>
    /// <remarks>
    /// The author: "I really like how Image Overlay and Procedural Objects do it. In-game they add
    /// a folder icon which opens where the user should drag images. Then in-game there is a
    /// dropdown with the filenames and it can be selected ... let's allow the user to 'crop' the
    /// image so its not stretched". A picture picked takes the flag's shape (the author's choice:
    /// the whole picture shows), a country's place, and is laid afresh: filling the flag, upright.
    /// </remarks>
    public sealed class ImageTarget
    {
        /// <summary>New or Existing: whose picture it is (the window's title, which window closes it).</summary>
        public PanelPage Page;

        public IntSetting Image, Turn, Alpha, Country, Proportion, HoistedFrom, Colour1;
        public FloatSetting X, Y, Zoom, Length;

        /// <summary>What a change does besides the settings (the selected flag: FlagEditor.ApplyDesign); null for the new flag, which reads its settings.</summary>
        public Action Changed;

        public Func<bool> Enabled;

        public bool IsEnabled
        {
            get { return Enabled == null || Enabled(); }
        }

        /// <summary>The picture's file name; empty for none.</summary>
        public string Name
        {
            get { return Image != null ? FlagImageNames.At(Image.value) : string.Empty; }
        }

        /// <summary>The flag's shape as seen: its height over its width.</summary>
        public float Seen
        {
            get { return FlagCrop.Seen(Proportion.value, HoistedFrom.value); }
        }

        /// <summary>
        /// A picture picked from the list (empty: none). It takes the country's place, is laid
        /// afresh -- filling the flag, its middle in the middle, upright -- and the flag takes its
        /// shape, so all of it shows.
        /// </summary>
        public void Pick(string name)
        {
            int number = FlagImageNames.NumberOf(name);
            if (number == Image.value)
                return;

            Image.value = number;
            if (number != 0)
            {
                if (Country != null)
                    Country.value = 0;
                X.value = 0.5f;
                Y.value = 0.5f;
                Zoom.value = 1f;
                Turn.value = 0;
                TakeShape();
            }

            Log.Msg("flags: " + Whose() + " image: " + (number != 0 ? "'" + FlagImageNames.At(number) + "'" + Size(FlagImageNames.At(number)) : "none"));
            Apply();
        }

        /// <summary>The flag takes the picture's shape as it stands (after its turn), its longer side kept, and the picture shows whole and centred. False without a picture of known size.</summary>
        public bool TakeShape()
        {
            int w, h;
            if (!PictureSize(out w, out h))
                return false;

            int shape = FlagCrop.ShapeOf(w, h, Turn.value);
            float newLength;
            int newProportion;
            FlagSize.Reshape(Length.value, Proportion.value, HoistedFrom.value, shape, out newLength, out newProportion);
            Length.value = newLength;
            Proportion.value = newProportion;
            X.value = 0.5f;
            Y.value = 0.5f;
            Zoom.value = 1f;
            return true;
        }

        /// <summary>The picture's shape as it stands, height over width x 1000; 0 if not known.</summary>
        public int PictureShape()
        {
            int w, h;
            return PictureSize(out w, out h) ? FlagCrop.ShapeOf(w, h, Turn.value) : 0;
        }

        /// <summary>
        /// The flag's shape is its picture's: a picture of known size on it (no country, whose own
        /// shape goes first), while its width and height go together (Settings.LinkSize) -- as a
        /// country's flag keeps its own proportions. Apart, any shape, the picture cropped to it.
        /// </summary>
        public bool ShapeTied
        {
            get
            {
                int w, h;
                return Settings.LinkSize != null && Settings.LinkSize.value && (Country == null || Country.value == 0) && PictureSize(out w, out h);
            }
        }

        /// <summary>The picture's shape as the cloth's proportion (hoist over length x 1000) for the edge hoisted; 0 without a picture of known size.</summary>
        public int PictureProportion()
        {
            int shape = PictureShape();
            return shape > 0 ? FlagSize.ClothProportion(shape, HoistedFrom.value) : 0;
        }

        /// <summary>
        /// The flag back to its picture's shape if it is tied to it and has another -- made square
        /// while its width and height went apart, say (the author: "when I toggled it back on ... It
        /// should have automatically rematched the height or width"): its longer side kept, the
        /// picture whole. Not applied (the caller does). True if it changed.
        /// </summary>
        public bool KeepShape()
        {
            if (!IsEnabled || !ShapeTied)
                return false;

            int shape = PictureShape();
            if (shape <= 0 || FlagSize.IsShape(Proportion.value, HoistedFrom.value, shape))
                return false;

            TakeShape();
            Log.Msg("flags: " + Whose() + " shape back to its image's: " + FlagProportion.Name(shape));
            return true;
        }

        /// <summary>The picture's size in pixels as its file has it: from its header, else from its texture made for the panel.</summary>
        public bool PictureSize(out int width, out int height)
        {
            string name = Name;
            width = height = 0;
            if (name.Length == 0)
                return false;

            if (FlagImageFolder.SizeOf(name, out width, out height))
                return true;

            UnityEngine.Texture2D preview = FlagImages.UserPreview(name);
            if (preview == null)
                return false;

            width = preview.width;
            height = preview.height;
            return true;
        }

        /// <summary>The window's middle and zoom (the crop view's drag and wheel, its buttons), held where FlagCrop keeps them.</summary>
        public void SetView(float x, float y, float zoom)
        {
            X.value = x;
            Y.value = y;
            Zoom.value = FlagCrop.ClampZoom(zoom);
        }

        /// <summary>
        /// The picture turned a quarter clockwise, about the flag's middle: the point of it there
        /// stays there. A flag whose shape is tied to its picture turns its shape with it (and
        /// shows the picture whole); one apart keeps its shape, the picture cropped to it.
        /// </summary>
        public void TurnQuarter()
        {
            int turn = Turn.value & 3;
            float fx, fy, tx, ty;
            FlagCrop.Unturn(turn, X.value, Y.value, out fx, out fy);
            FlagCrop.Turn((turn + 1) & 3, fx, fy, out tx, out ty);
            Turn.value = (turn + 1) & 3;
            X.value = tx;
            Y.value = ty;
            Log.Msg("flags: " + Whose() + " image turned to " + (90 * Turn.value) + " deg");
            KeepShape();
            Apply();
        }

        public void SetAlpha(int alpha)
        {
            if (Alpha.value == alpha || !FlagImageAlpha.IsKnown(alpha))
                return;

            Alpha.value = alpha;
            Log.Msg("flags: " + Whose() + " image's see-through parts " + (alpha == FlagImageAlpha.Through ? "show the flag" : "cut out"));
            Apply();
        }

        /// <summary>The change applied (the selected flag redesigned) and the panels following.</summary>
        public void Apply()
        {
            if (Changed != null)
                Changed();
            SettingsCatalog.RefreshAllUIs();
        }

        /// <summary>For the log: "the new flag's", "flag #12's".</summary>
        public string Whose()
        {
            return Page == PanelPage.New ? "the new flag's" : "flag #" + FlagEditor.Selected + "'s";
        }

        private static string Size(string name)
        {
            int w, h;
            return FlagImageFolder.SizeOf(name, out w, out h) ? " (" + w + " x " + h + ")" : "";
        }
    }
}
