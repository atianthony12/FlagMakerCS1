using System.Collections.Generic;
using ColossalFramework.UI;
using RealFlag.Flags;
using UnityEngine;

namespace RealFlag.UI
{
    /// <summary>
    /// The Strings tab's list of the city's strings -- a chain of spans each (StringNet.ChainOf) --
    /// with its spans, its flags and what they are; a click selects it (the String window opens on
    /// it), again lets it go; a double click takes the camera to it (as the flags' list does).
    /// </summary>
    public class StringListView : UIPanel
    {
        public const float RowHeight = 26f;
        public const int VisibleRows = 6;

        private ModsLibCS1.UI.Builder _ui;
        private UIPanel _box;
        private UIScrollbar _bar;
        private readonly UIButton[] _buttons = new UIButton[VisibleRows];
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<ushort> _spans = new List<ushort>();
        private readonly List<ushort> _chain = new List<ushort>();
        private readonly List<bool> _forward = new List<bool>();
        private int _offset, _version = -1, _count = -1;
        private bool _refreshing;

        private struct Entry
        {
            public ushort First;
            public int Spans, Flags;
            public string Text;
        }

        public void Build(float width, ModsLibCS1.UI.Builder ui)
        {
            _ui = ui;
            size = new Vector2(width, RowHeight * VisibleRows + 6f);

            _box = AddUIComponent<UIPanel>();
            PanelLook.DressListBox(_ui, _box);
            _box.size = new Vector2(width, RowHeight * VisibleRows + 6f);
            _box.relativePosition = Vector3.zero;
            _box.eventMouseWheel += (component, p) =>
            {
                Scroll(p.wheelDelta > 0 ? -2 : 2);
                p.Use();
            };

            float rowWidth = width - 18f;
            for (int i = 0; i < VisibleRows; i++)
            {
                UIButton button = _box.AddUIComponent<UIButton>();
                PanelLook.DressListRow(_ui, button);
                button.size = new Vector2(rowWidth, RowHeight);
                button.relativePosition = new Vector3(3f, 3f + i * RowHeight);
                button.textScale = 0.75f;
                button.textHorizontalAlignment = UIHorizontalAlignment.Left;
                button.textPadding = new RectOffset(8, 6, 6, 0);
                int index = i;
                button.eventClick += (component, e) => Click(index);
                // The game sends a pair's second click as this instead: the first has selected or let go.
                button.eventDoubleClick += (component, e) =>
                {
                    int at = _offset + index;
                    if (at < 0 || at >= _entries.Count)
                        return;
                    if (!StringEditor.SelectedChain.Contains(_entries[at].First))
                    {
                        StringEditor.Select(_entries[at].First);
                        StringPanel.OpenSelected();
                    }

                    StringEditor.GoTo();
                    Fill();
                };
                button.eventMouseWheel += (component, p) =>
                {
                    Scroll(p.wheelDelta > 0 ? -2 : 2);
                    p.Use();
                };
                _buttons[i] = button;
            }

            float barWidth = PanelLook.ListBarWidth(_ui);
            _bar = PanelLook.AddListBar(_ui, _box, new Vector2(barWidth, _box.height - 6f), new Vector3(_box.width - 3f - barWidth, 3f));
            _bar.incrementAmount = 2f;
            _bar.eventValueChanged += (component, value) =>
            {
                if (_refreshing)
                    return;
                _offset = Mathf.RoundToInt(value);
                Fill();
            };
        }

        /// <summary>Again when the city's strings changed (the registry, the renderer's spans).</summary>
        public void Refresh()
        {
            StringRenderer r = StringRenderer.Instance;
            int count = r != null ? r.SpanCount : 0;
            if (StringRegistry.Version == _version && count == _count)
            {
                Fill();
                return;
            }

            _version = StringRegistry.Version;
            _count = count;
            _entries.Clear();
            if (r != null)
            {
                r.SpansTo(_spans);
                _spans.Sort();
                var done = new HashSet<ushort>();
                foreach (ushort s in _spans)
                {
                    if (done.Contains(s))
                        continue;
                    StringNet.ChainOf(s, _chain, _forward);
                    int flags = 0;
                    ushort first = s;
                    foreach (ushort c in _chain)
                    {
                        done.Add(c);
                        StringSpan span;
                        if (r.TryGetSpan(c, out span))
                            flags += span.Layout.Count;
                        if (c < first)
                            first = c;
                    }

                    StringSpec spec;
                    StringRegistry.TryGet(s, out spec);
                    string name = !string.IsNullOrEmpty(spec.Name) ? spec.Name : Localization.Get("StrList.Name", first);
                    string what = spec.Sequence == StringSequence.Countries ? Localization.Get("StringSequence.Countries")
                                : spec.Sequence == StringSequence.Alternate ? Localization.Get("StrList.Designs", spec.Designs != null ? spec.Designs.Length : 0)
                                : Localization.Get("StringSequence.Same");
                    _entries.Add(new Entry
                    {
                        First = s,
                        Spans = _chain.Count,
                        Flags = flags,
                        Text = name + "  ·  " + Localization.Get("StrList.Counts", _chain.Count, flags) + "  ·  " + what,
                    });
                }
            }

            _offset = Mathf.Clamp(_offset, 0, Mathf.Max(0, _entries.Count - VisibleRows));
            Fill();
        }

        private void Scroll(int rows)
        {
            _offset = Mathf.Clamp(_offset + rows, 0, Mathf.Max(0, _entries.Count - VisibleRows));
            Fill();
        }

        private void Fill()
        {
            if (_bar == null)
                return;
            _refreshing = true;
            try
            {
                _bar.maxValue = Mathf.Max(0, _entries.Count - VisibleRows);
                _bar.scrollSize = Mathf.Min(VisibleRows, _entries.Count);
                _bar.value = _offset;
            }
            finally
            {
                _refreshing = false;
            }

            IList<ushort> selected = StringEditor.SelectedChain;
            for (int i = 0; i < VisibleRows; i++)
            {
                int at = _offset + i;
                UIButton b = _buttons[i];
                if (at >= _entries.Count)
                {
                    b.isVisible = _entries.Count == 0 && i == 0;
                    if (b.isVisible)
                    {
                        b.text = Localization.Get("StrList.Empty");
                        PanelLook.SetListLit(_ui, b, false);
                    }

                    continue;
                }

                b.isVisible = true;
                if (b.text != _entries[at].Text)
                    b.text = _entries[at].Text;
                PanelLook.SetListLit(_ui, b, selected.Contains(_entries[at].First));
            }
        }

        private void Click(int index)
        {
            int at = _offset + index;
            if (at < 0 || at >= _entries.Count)
                return;

            ushort s = _entries[at].First;
            if (StringEditor.SelectedChain.Contains(s))
            {
                StringEditor.Deselect();
                StringPanel.Close();
            }
            else
            {
                StringEditor.Select(s);
                StringPanel.OpenSelected();
            }

            Fill();
        }
    }
}
