using System;
using System.Collections.Generic;
using RealFlag.UI;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The selected STRING: the chain of spans the player picked (on the map or in the list),
    /// edited as one -- its spans all take the String window's design (StringTarget.Selected) --
    /// moved to by the camera, copied to the new string, deleted. A chain is what the player made
    /// in one go, or joined since: spans through nodes with two spans each (StringNet.ChainOf).
    /// </summary>
    public static class StringEditor
    {
        private static readonly List<ushort> Chain = new List<ushort>();
        private static readonly List<bool> Forward = new List<bool>();

        /// <summary>The span picked (0: none), and its chain.</summary>
        public static ushort Selected { get; private set; }

        public static IList<ushort> SelectedChain
        {
            get { return Chain; }
        }

        public static bool HasSelection
        {
            get { return Selected != 0; }
        }

        /// <summary>The next string's design, as the String window has it (the defaults with no window yet).</summary>
        public static StringSpec NewSpec()
        {
            StringTarget t = StringTarget.New;
            return t != null ? t.Spec() : StringSpec.Default;
        }

        public static void Select(ushort segment)
        {
            if (!StringNet.IsSpan(segment))
                return;

            StringSpec spec;
            if (!StringRegistry.TryGet(segment, out spec))
                spec = NewSpec();

            Selected = segment;
            StringNet.ChainOf(segment, Chain, Forward);
            StringTarget.Init();
            StringTarget.Selected.Load(spec);
            StringTarget.Selected.Changed = ApplySelected;
            Highlight();
            Log.Msg("strings: selected the string of span #" + segment + " (" + Chain.Count + " span(s): " + spec.Describe() + ")");
        }

        public static void Deselect()
        {
            if (Selected == 0)
                return;
            Selected = 0;
            Chain.Clear();
            Forward.Clear();
            Highlight();
        }

        /// <summary>The selection still there (a bulldozer or Move It may have taken it): let go if not.</summary>
        public static void Check()
        {
            if (Selected != 0 && !StringNet.IsSpan(Selected))
            {
                Log.Msg("strings: the selected string is gone");
                Deselect();
            }
        }

        private static void Highlight()
        {
            StringRenderer r = StringRenderer.Instance;
            if (r != null)
                r.SetHighlight(Chain, 0);
        }

        /// <summary>The String window's design onto every span of the selected string.</summary>
        public static void ApplySelected()
        {
            if (Selected == 0 || StringTarget.Selected == null)
                return;

            StringSpec spec = StringTarget.Selected.Spec();
            int n = 0;
            foreach (ushort s in Chain)
            {
                if (!StringNet.IsSpan(s))
                    continue;
                StringRegistry.Set(s, spec);
                n++;
            }

            if (Log.Detailed)
                Log.Detail("strings: the selected string (" + n + " span(s)) is now " + spec.Describe());
        }

        public static void GoTo()
        {
            StringRenderer r = StringRenderer.Instance;
            Vector3 middle;
            if (Selected == 0 || r == null || !r.TryMiddle(Selected, out middle))
                return;

            try
            {
                CameraController camera = ToolsModifierControl.cameraController;
                if (camera == null)
                    return;

                Vector3 a, b;
                ushort na, nb;
                float span = StringNet.TryRead(Selected, out a, out b, out na, out nb) ? Vector3.Distance(a, b) : 10f;
                camera.m_targetPosition = middle;
                camera.m_targetSize = Mathf.Clamp(span * 2f, 30f, 600f);
                Log.Msg("strings: camera to span #" + Selected);
            }
            catch (Exception e)
            {
                Log.Warn("strings: could not move the camera: " + e.Message);
            }
        }

        /// <summary>The new string takes the selected one's design (the window then opens to place another like it).</summary>
        public static void CopyToNew()
        {
            if (Selected == 0 || StringTarget.Selected == null)
                return;
            StringTarget.Init();
            StringTarget.New.Load(StringTarget.Selected.Spec());
            Log.Msg("strings: the new string takes the selected one's design");
        }

        /// <summary>Deletes the selected string, once the player says yes (the panel's button and the Delete key).</summary>
        public static void ConfirmDelete()
        {
            if (Selected == 0)
                return;

            ushort asked = Selected;
            try
            {
                ConfirmPanel.ShowModal(Mod.DisplayName, Localization.Get("StrSelDelete.Confirm"), (component, result) =>
                {
                    // The same string still selected: a click elsewhere meanwhile is not a yes to it.
                    if (result != 1 || Selected != asked)
                        return;
                    Delete();
                    SettingsCatalog.RefreshAllUIs();
                });
            }
            catch (Exception e)
            {
                Log.Error("strings: could not ask about deleting the string, so it was not deleted.", e);
            }
        }

        public static void Delete()
        {
            if (Selected == 0)
                return;
            var spans = new List<ushort>(Chain);
            Log.Msg("strings: deleting the selected string (" + spans.Count + " span(s))");
            Deselect();
            StringNet.Delete(spans);
        }

        /// <summary>The city unloaded.</summary>
        public static void Clear()
        {
            Selected = 0;
            Chain.Clear();
            Forward.Clear();
        }
    }
}
