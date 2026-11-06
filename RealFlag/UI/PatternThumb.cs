using ColossalFramework.UI;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// A pattern as a little picture on the panels: the whole sprite is the flag, drawn at twice
    /// the UI's units (sharp at 4K) by FlagPatternView, again only when something it shows changed.
    /// Not in the pointer's way. It owns its texture, which goes with it.
    /// </summary>
    public class PatternThumb : UITextureSprite
    {
        private const int PixelsPerUnit = 2;

        private Texture2D _own;
        private Color32[] _pixels;
        private int _pattern = -1, _proportion;
        private bool _top;
        private uint _c1, _c2, _c3;

        /// <summary>It does not show this, at its size.</summary>
        public bool Stale(int pattern, int proportion, bool top, uint c1, uint c2, uint c3)
        {
            return _own == null || pattern != _pattern || proportion != _proportion || top != _top || c1 != _c1 || c2 != _c2 || c3 != _c3 ||
                   _own.width != PixelsWide || _own.height != PixelsTall;
        }

        private int PixelsWide
        {
            get { return Mathf.Max(1, Mathf.RoundToInt(width * PixelsPerUnit)); }
        }

        private int PixelsTall
        {
            get { return Mathf.Max(1, Mathf.RoundToInt(height * PixelsPerUnit)); }
        }

        public void Show(int pattern, int proportion, bool top, uint c1, uint c2, uint c3)
        {
            if (!Stale(pattern, proportion, top, c1, c2, c3))
                return;

            _pattern = pattern;
            _proportion = proportion;
            _top = top;
            _c1 = c1;
            _c2 = c2;
            _c3 = c3;

            int w = PixelsWide, h = PixelsTall;
            if (_own == null || _own.width != w || _own.height != h)
            {
                ReleaseTexture();
                _own = new Texture2D(w, h, TextureFormat.RGBA32, false, false)
                {
                    name = "RealFlag pattern thumbnail",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _pixels = new Color32[w * h];
            }

            FlagPatternView.Render(_pixels, w, h, 0f, 0f, w, h, pattern, FlagProportion.Ratio(proportion), top, c1, c2, c3, default(Color32));
            _own.SetPixels32(_pixels);
            _own.Apply(false);
            texture = _own;
        }

        private void ReleaseTexture()
        {
            if (_own != null)
                Object.Destroy(_own);
            _own = null;
            _pixels = null;
        }

        public override void OnDestroy()
        {
            ReleaseTexture();
            base.OnDestroy();
        }
    }
}
