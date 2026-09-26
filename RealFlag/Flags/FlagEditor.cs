using System;
using ColossalFramework.UI;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// The flag selected in the Existing flags tab (clicked in its list, or on the map with the
    /// pick tool): its design, pole, rotation and height in the tab's controls, each change the
    /// player makes there applied to it, and the gizmo's moves and turns (FlagTool).
    /// </summary>
    /// <remarks>
    /// The tab's controls are TRANSIENT settings (Settings.Sel*): copied from the flag when it is
    /// selected, and again whenever its prop changes under them (Move It turned or raised it),
    /// except while the player's own change is still on its way to the prop (the simulation
    /// thread applies it a frame or two later: re-reading the old value would pull the slider back).
    ///
    /// A selection lives as long as the mod's tool is out, which shows the gizmo on it: selecting
    /// takes the tool out; putting the tool away (Esc, right-click, another tool, the panel
    /// closed, the other tab) lets the selection go. The first version kept a flag selected --
    /// glowing, outlined -- until the city unloaded ("when I select an item, it doesn't
    /// de-select").
    /// </remarks>
    public static class FlagEditor
    {
        /// <summary>The selected flag's prop; 0 for none.</summary>
        public static uint Selected { get; private set; }

        /// <summary>True while the controls are being filled FROM the flag: those are not changes to apply.</summary>
        private static bool _loading;

        /// <summary>Real time until which the prop's rotation and height are not read back (a change of ours is on its way).</summary>
        private static float _quietUntil;

        public static bool HasSelection
        {
            get
            {
                FlagEntry e;
                return TryEntry(out e);
            }
        }

        private static bool TryEntry(out FlagEntry entry)
        {
            FlagRenderer r = FlagRenderer.Instance;
            if (Selected == 0 || r == null)
            {
                entry = default(FlagEntry);
                return false;
            }

            return r.TryGet(Selected, out entry);
        }

        /// <summary>Selects a flag, and takes the tool out on it (the gizmo).</summary>
        public static void Select(uint prop)
        {
            Selected = prop;
            _quietUntil = 0f;
            Load(true);

            FlagEntry e;
            if (TryEntry(out e))
                Log.Msg("flags: flag #" + prop + " selected (" + e.Spec.Describe() + ")");

            FlagTool.Open(FlagTool.Mode.Pick);
            UI.SettingsCatalog.RefreshAllUIs();
        }

        /// <summary>No flag selected (the tool, if out, stays out to pick another).</summary>
        public static void Deselect()
        {
            if (Selected == 0)
                return;

            Log.Msg("flags: flag #" + Selected + " deselected");
            Selected = 0;
            UI.SettingsCatalog.RefreshAllUIs();
        }

        /// <summary>The city is unloading: nothing selected, nothing said.</summary>
        public static void Clear()
        {
            Selected = 0;
        }

        /// <summary>The flag is gone (the renderer found its prop deleted).</summary>
        public static void Forget(uint prop)
        {
            if (Selected == prop)
                Deselect();
        }

        /// <summary>
        /// Copies the selected flag into the tab's controls: everything, or (<paramref name="all"/>
        /// false: the panel's periodic refresh) only its rotation and height, which Move It may have
        /// changed.
        /// </summary>
        public static void Load(bool all)
        {
            FlagEntry e;
            if (!TryEntry(out e))
                return;

            if (!all && Time.realtimeSinceStartup < _quietUntil)
                return;

            _loading = true;
            try
            {
                if (all)
                {
                    Settings.SelCountry.value = FlagCountries.NumberOf(e.Spec.Country);
                    Settings.SelPattern.value = e.Spec.Pattern;
                    Settings.SelColour1.value = (int)e.Spec.Colour1;
                    Settings.SelColour2.value = (int)e.Spec.Colour2;
                    Settings.SelColour3.value = (int)e.Spec.Colour3;
                    Settings.SelLength.value = e.Spec.Length;
                    Settings.SelProportion.value = e.Spec.Proportion;
                    Settings.SelWaviness.value = e.Spec.Waviness;
                    Settings.SelPole.value = e.Spec.Pole;
                    Settings.SelRopes.value = e.Spec.Ropes;
                    Settings.SelPoleColour.value = (int)e.Spec.PoleColour;
                    Settings.SelTopColour.value = (int)e.Spec.TopColour;
                    Settings.SelRopeColour.value = (int)e.Spec.RopeColour;
                    Settings.SelPoleThickness.value = e.Spec.PoleThickness;
                }

                float degrees = Mathf.Repeat(e.Angle * Mathf.Rad2Deg, 360f);
                if (Mathf.Abs(Mathf.DeltaAngle(degrees, Settings.SelRotation.value)) > 0.05f)
                    Settings.SelRotation.value = Mathf.Round(degrees * 10f) / 10f;

                float height = e.Anchor.y - e.Ground;
                if (Mathf.Abs(height - Settings.SelHeight.value) > 0.01f)
                    Settings.SelHeight.value = Mathf.Round(height * 100f) / 100f;

                if (Mathf.Abs(e.Spec.Tilt - Settings.SelTilt.value) > 0.05f)
                    Settings.SelTilt.value = Mathf.Round(e.Spec.Tilt * 10f) / 10f;
                if (Mathf.Abs(e.Spec.Lean - Settings.SelLean.value) > 0.05f)
                    Settings.SelLean.value = Mathf.Round(e.Spec.Lean * 10f) / 10f;
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>A design control changed (pattern, colours, size, proportions, waviness).</summary>
        public static void ApplyDesign()
        {
            FlagEntry e;
            if (_loading || !TryEntry(out e))
                return;

            FlagSpec spec = e.Spec;
            FlagCountry country = FlagCountries.At(Settings.SelCountry.value);
            spec.Country = country != null ? country.Code : string.Empty;
            spec.Pattern = Settings.SelPattern.value;
            spec.Colour1 = (uint)Settings.SelColour1.value;
            spec.Colour2 = (uint)Settings.SelColour2.value;
            spec.Colour3 = (uint)Settings.SelColour3.value;
            spec.Length = Settings.SelLength.value;
            spec.Proportion = Settings.SelProportion.value;
            spec.Waviness = Settings.SelWaviness.value;
            spec.Pole = Settings.SelPole.value;
            spec.Ropes = Settings.SelRopes.value;
            spec.PoleColour = (uint)Settings.SelPoleColour.value;
            spec.TopColour = (uint)Settings.SelTopColour.value;
            spec.RopeColour = (uint)Settings.SelRopeColour.value;
            spec.PoleThickness = Settings.SelPoleThickness.value;
            spec.Tilt = Settings.SelTilt.value;
            spec.Lean = Settings.SelLean.value;
            spec = spec.Sanitised();
            FlagRegistry.Set(Selected, spec);
            _quietUntil = Time.realtimeSinceStartup + 0.6f;

            if (Log.Detailed)
                Log.Detail("flags: flag #" + Selected + " redesigned: " + spec.Describe());
        }

        /// <summary>
        /// The gizmo moved or turned the selected flag: its prop moved and turned (on the
        /// simulation thread, as the panel's own sliders do), its tilt and lean into its spec, and
        /// the tab's controls showing the new values at once.
        /// </summary>
        public static void ApplyTransform(Vector3 anchor, float angle, float tilt, float lean)
        {
            FlagEntry e;
            if (!TryEntry(out e))
                return;

            _quietUntil = Time.realtimeSinceStartup + 0.6f;
            if ((anchor - e.Anchor).sqrMagnitude > 1e-8f)
                FlagProps.Move(Selected, anchor);
            if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, e.Angle * Mathf.Rad2Deg)) > 1e-3f)
                FlagProps.Rotate(Selected, angle);

            if (Mathf.Abs(tilt - e.Spec.Tilt) > 1e-3f || Mathf.Abs(lean - e.Spec.Lean) > 1e-3f)
            {
                FlagSpec spec = e.Spec;
                spec.Tilt = tilt;
                spec.Lean = lean;
                FlagRegistry.Set(Selected, spec);
            }

            _loading = true;
            try
            {
                Settings.SelRotation.value = Mathf.Round(Mathf.Repeat(angle * Mathf.Rad2Deg, 360f) * 10f) / 10f;
                Settings.SelHeight.value = Mathf.Round((anchor.y - FlagProps.GroundAt(anchor)) * 100f) / 100f;
                Settings.SelTilt.value = Mathf.Round(tilt * 10f) / 10f;
                Settings.SelLean.value = Mathf.Round(lean * 10f) / 10f;
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>The selected flag's place, heading and design, for the gizmo; false with none.</summary>
        public static bool TryGetSelected(out FlagEntry entry)
        {
            return TryEntry(out entry);
        }

        public static void ApplyRotation()
        {
            FlagEntry e;
            if (_loading || !TryEntry(out e))
                return;

            _quietUntil = Time.realtimeSinceStartup + 0.6f;
            FlagProps.Rotate(Selected, Settings.SelRotation.value * Mathf.Deg2Rad);
        }

        public static void ApplyHeight()
        {
            FlagEntry e;
            if (_loading || !TryEntry(out e))
                return;

            _quietUntil = Time.realtimeSinceStartup + 0.6f;
            FlagProps.Move(Selected, new Vector3(e.Anchor.x, e.Ground + Settings.SelHeight.value, e.Anchor.z));
        }

        /// <summary>The camera to the selected flag.</summary>
        public static void GoTo()
        {
            FlagEntry e;
            if (!TryEntry(out e))
                return;

            try
            {
                CameraController camera = ToolsModifierControl.cameraController;
                if (camera == null)
                    return;

                camera.m_targetPosition = new Vector3(e.Anchor.x, e.Anchor.y - 0.5f * e.Spec.Height, e.Anchor.z);
                camera.m_targetSize = Mathf.Clamp(e.Spec.Length * 8f, 30f, 600f);
                Log.Msg("flags: camera to flag #" + Selected);
            }
            catch (Exception ex)
            {
                Log.Warn("flags: could not move the camera: " + ex.Message);
            }
        }

        /// <summary>The Create new tab takes the selected flag's design, for another like it.</summary>
        public static void CopyToNew()
        {
            FlagEntry e;
            if (!TryEntry(out e))
                return;

            Settings.NewCountry.value = FlagCountries.NumberOf(e.Spec.Country);
            Settings.NewPattern.value = e.Spec.Pattern;
            Settings.NewColour1.value = (int)e.Spec.Colour1;
            Settings.NewColour2.value = (int)e.Spec.Colour2;
            Settings.NewColour3.value = (int)e.Spec.Colour3;
            Settings.NewLength.value = e.Spec.Length;

            // A country's own proportions stay its own: Create new's pattern keeps the list's.
            if (Array.IndexOf(FlagProportion.Values, e.Spec.Proportion) >= 0)
                Settings.NewProportion.value = e.Spec.Proportion;
            Settings.NewWaviness.value = e.Spec.Waviness;
            Settings.NewPole.value = e.Spec.Pole;
            Settings.NewRopes.value = e.Spec.Ropes;
            Settings.NewPoleColour.value = (int)e.Spec.PoleColour;
            Settings.NewTopColour.value = (int)e.Spec.TopColour;
            Settings.NewRopeColour.value = (int)e.Spec.RopeColour;
            Settings.NewPoleThickness.value = e.Spec.PoleThickness;
            Settings.NewTilt.value = e.Spec.Tilt;
            Settings.NewLean.value = e.Spec.Lean;
            Log.Msg("flags: flag #" + Selected + "'s design copied to Create new");
        }

        /// <summary>Deletes the selected flag, once the player says yes.</summary>
        public static void ConfirmDelete()
        {
            FlagEntry e;
            if (!TryEntry(out e))
                return;

            uint prop = Selected;
            try
            {
                ConfirmPanel.ShowModal(Mod.DisplayName, Localization.Get("DeleteFlag.Confirm"), (component, result) =>
                {
                    if (result != 1)
                        return;

                    FlagProps.Delete(prop);
                    Forget(prop);
                    Log.Msg("flags: flag #" + prop + " deleted from the panel");
                    UI.SettingsCatalog.RefreshAllUIs();
                });
            }
            catch (Exception ex)
            {
                Log.Error("flags: could not ask about deleting the flag, so it was not deleted.", ex);
            }
        }

        /// <summary>The line over the selected flag's controls.</summary>
        public static string Describe()
        {
            FlagEntry e;
            if (!TryEntry(out e))
                return Localization.Get("Existing.NoneSelected");

            return Localization.Get("Existing.Selected", Title(e), e.Anchor.x.ToString("F0"), e.Anchor.z.ToString("F0"),
                                    (e.Anchor.y - e.Ground).ToString("F1"));
        }

        /// <summary>"Flag 12", or its name.</summary>
        public static string Title(FlagEntry e)
        {
            return string.IsNullOrEmpty(e.Spec.Name) ? Localization.Get("Flag.Title", e.Prop) : e.Spec.Name;
        }
    }
}
