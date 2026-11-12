using System;
using System.Globalization;
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
    ///
    /// A flag with a pole that stands keeps the Pole height the player gave it: turned onto a new
    /// hoist line (the edge hoisted, a tilt or lean) it stands again where it stood, as the tool
    /// stands a new one (Restand); the Pole height's slider moves its top, its foot staying.
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
            // The last flag's gesture is over (FlagUndo: one step per gesture).
            FlagUndo.End();
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
            FlagUndo.End();
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

        /// <summary>
        /// The flag is gone (the renderer found its prop deleted): deselected, and its undo steps let
        /// go -- unless a step of ours took it away (FlagUndo let go of the prop first).
        /// </summary>
        public static void Forget(uint prop)
        {
            if (Selected == prop)
                Deselect();
            FlagUndo.Forget(prop);
        }

        /// <summary>
        /// Copies the selected flag into the tab's controls: everything, or (<paramref name="all"/>
        /// false: the panel's periodic refresh) only its rotation, tilt and pole height, which Move
        /// It, the gizmo or the pole's first cast may have changed.
        /// </summary>
        public static void Load(bool all)
        {
            FlagEntry e;
            if (!TryEntry(out e))
                return;

            if (!all && Time.realtimeSinceStartup < _quietUntil)
                return;

            Fill(e.Spec, e.Angle, all);
        }

        /// <summary>
        /// The tab's controls from a design and a heading: <see cref="Load"/>'s, the flag as the
        /// renderer has it; an undo's (<see cref="Restored"/>), the state it put back -- the
        /// renderer's copy is a frame behind the registry, and ApplyDesign builds the whole spec
        /// from these controls: one left stale would undo the undo at the next row changed.
        /// </summary>
        private static void Fill(FlagSpec spec, float angle, bool all)
        {
            _loading = true;
            try
            {
                if (all)
                {
                    Settings.SelCountry.value = FlagCountries.NumberOf(spec.Country);
                    Settings.SelImage.value = FlagImageNames.NumberOf(spec.Image);
                    Settings.SelImageX.value = spec.ImageX;
                    Settings.SelImageY.value = spec.ImageY;
                    Settings.SelImageZoom.value = spec.ImageZoom;
                    Settings.SelImageTurn.value = spec.ImageTurn;
                    Settings.SelImageAlpha.value = spec.ImageAlpha;
                    Settings.SelPattern.value = spec.Pattern;
                    Settings.SelColour1.value = (int)spec.Colour1;
                    Settings.SelColour2.value = (int)spec.Colour2;
                    Settings.SelColour3.value = (int)spec.Colour3;
                    Settings.SelLength.value = spec.Length;
                    Settings.SelProportion.value = spec.Proportion;
                    Settings.SelHoistedFrom.value = spec.HoistedFrom;
                    Settings.SelMotion.value = spec.Motion;
                    Settings.SelWaviness.value = spec.Waviness;
                    Settings.SelWindShare.value = spec.WindShare;
                    Settings.SelCut.value = spec.Cut;
                    Settings.SelCutDepth.value = spec.CutDepth;
                    Settings.SelDarkness.value = spec.Darkness;
                    Settings.SelWear.value = spec.Wear;
                    Settings.SelDirt.value = spec.Dirt;
                    Settings.SelHoles.value = spec.Holes;
                    Settings.SelTear.value = spec.Tear;
                    Settings.SelTearEdges.value = spec.TearEdges;
                    Settings.SelBackPlate.value = spec.BackPlate;
                    Settings.SelPole.value = spec.Pole;
                    Settings.SelRopes.value = spec.Ropes;
                    Settings.SelPoleColour.value = (int)spec.PoleColour;
                    Settings.SelTopColour.value = (int)spec.TopColour;
                    Settings.SelRopeColour.value = (int)spec.RopeColour;
                    Settings.SelPoleThickness.value = spec.PoleThickness;
                    Settings.SelPoleProfile.value = spec.PoleProfile;
                    Settings.SelPoleBase.value = spec.PoleBase;
                    Settings.SelPoleShine.value = spec.PoleShine;
                    Settings.SelPoleWear.value = spec.PoleWear;
                    Settings.SelPoleRust.value = spec.PoleRust;
                    Settings.SelPoleDirt.value = spec.PoleDirt;
                    Settings.SelRopeThickness.value = spec.RopeThickness;
                    Settings.SelPolePosition.value = spec.PolePosition;
                    Settings.SelTake.value = spec.Take;
                    Settings.SelHeld.value = spec.Held;

                    // With no pole that stands (none, a crossbar, one still being found), the Pole
                    // height is a new flag's: what it stands with if its hoist turns down.
                    if (!FlagStand.PoleStands(spec) || spec.PoleLength <= 0f)
                        Settings.SelHeight.value = Settings.NewHeight.value;
                }

                float degrees = Mathf.Repeat(angle * Mathf.Rad2Deg, 360f);
                if (Mathf.Abs(Mathf.DeltaAngle(degrees, Settings.SelRotation.value)) > 0.05f)
                    Settings.SelRotation.value = Mathf.Round(degrees * 10f) / 10f;

                // Its pole's height, if it stands (0 while an older save's is still being found). A
                // crossbar's length is its own, found along it: the row keeps the height the pole
                // stands with when its hoist turns down again.
                if (FlagStand.PoleStands(spec) && spec.PoleLength > 0f && Mathf.Abs(spec.PoleLength - Settings.SelHeight.value) > 0.01f)
                    Settings.SelHeight.value = Mathf.Round(spec.PoleLength * 100f) / 100f;

                if (Mathf.Abs(spec.Tilt - Settings.SelTilt.value) > 0.05f)
                    Settings.SelTilt.value = Mathf.Round(spec.Tilt * 10f) / 10f;
                if (Mathf.Abs(spec.Lean - Settings.SelLean.value) > 0.05f)
                    Settings.SelLean.value = Mathf.Round(spec.Lean * 10f) / 10f;
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>A design control changed (pattern, colours, the picture and where it lies, size and shape, the edge hoisted, waviness, darkness, the pole).</summary>
        public static void ApplyDesign()
        {
            FlagEntry e;
            if (_loading || !TryEntry(out e))
                return;

            // For undo (FlagUndo): the flag as it was, then as this left it.
            FlagState before = FlagUndo.Now(Selected);
            FlagSpec old = Latest(e);
            FlagSpec spec = old;
            FlagCountry country = FlagCountries.At(Settings.SelCountry.value);
            spec.Country = country != null ? country.Code : string.Empty;
            spec.Image = FlagImageNames.At(Settings.SelImage.value);
            spec.ImageX = Settings.SelImageX.value;
            spec.ImageY = Settings.SelImageY.value;
            spec.ImageZoom = Settings.SelImageZoom.value;
            spec.ImageTurn = Settings.SelImageTurn.value;
            spec.ImageAlpha = Settings.SelImageAlpha.value;
            spec.Pattern = Settings.SelPattern.value;
            spec.Colour1 = (uint)Settings.SelColour1.value;
            spec.Colour2 = (uint)Settings.SelColour2.value;
            spec.Colour3 = (uint)Settings.SelColour3.value;
            spec.Length = Settings.SelLength.value;
            spec.Proportion = Settings.SelProportion.value;
            spec.HoistedFrom = Settings.SelHoistedFrom.value;
            spec.Motion = Settings.SelMotion.value;
            spec.Waviness = Settings.SelWaviness.value;
            spec.WindShare = Settings.SelWindShare.value;
            spec.Cut = Settings.SelCut.value;
            spec.CutDepth = Settings.SelCutDepth.value;
            spec.Darkness = Settings.SelDarkness.value;
            spec.Wear = Settings.SelWear.value;
            spec.Dirt = Settings.SelDirt.value;
            spec.Holes = Settings.SelHoles.value;
            spec.Tear = Settings.SelTear.value;
            spec.TearEdges = Settings.SelTearEdges.value;
            spec.BackPlate = Settings.SelBackPlate.value;
            spec.Pole = Settings.SelPole.value;
            spec.Ropes = Settings.SelRopes.value;
            spec.PoleColour = (uint)Settings.SelPoleColour.value;
            spec.TopColour = (uint)Settings.SelTopColour.value;
            spec.RopeColour = (uint)Settings.SelRopeColour.value;
            spec.PoleThickness = Settings.SelPoleThickness.value;
            spec.PoleProfile = Settings.SelPoleProfile.value;
            spec.PoleBase = Settings.SelPoleBase.value;
            spec.PoleShine = Settings.SelPoleShine.value;
            spec.PoleWear = Settings.SelPoleWear.value;
            spec.PoleRust = Settings.SelPoleRust.value;
            spec.PoleDirt = Settings.SelPoleDirt.value;
            spec.RopeThickness = Settings.SelRopeThickness.value;
            spec.PolePosition = Settings.SelPolePosition.value;
            spec.Take = Settings.SelTake.value;
            spec.Held = Settings.SelHeld.value;
            spec.Tilt = Settings.SelTilt.value;
            spec.Lean = Settings.SelLean.value;
            spec = spec.Sanitised();

            // A new line for the pole (its tilt, its lean, the edge hoisted): the flag stands again
            // where it stood, a pole that stands as tall as it was (Restand). A pole put back (the
            // flag may have moved since): its length found again down it, the flag staying.
            Vector3 anchor = e.Anchor;
            bool stood = false;
            if (!spec.SameHoist(old))
                stood = Restand(e, old, ref spec, e.Angle, out anchor);
            else if (spec.HasPole && !old.HasPole)
                spec.PoleLength = 0f;
            else if (Mathf.Abs(spec.PolePosition - old.PolePosition) > 1e-4f && FlagStand.PoleStands(old) && old.PoleLength > 0f &&
                     HoldBase(e, old))
            {
                // Raised or lowered on its pole (half-mast): the foot and the pole stay, the flag
                // goes up or down it.
                anchor = StandAt(spec, e.Angle);
                stood = true;
            }
            FlagRegistry.Set(Selected, spec);
            if (stood)
            {
                FlagProps.Move(Selected, anchor);
                FlagUndo.Sent(Selected, anchor, before.Present ? before.Angle : e.Angle);
            }
            _quietUntil = Time.realtimeSinceStartup + 0.6f;
            FlagUndo.Changed(Selected, before, false);

            if (Log.Detailed)
                Log.Detail("flags: flag #" + Selected + " redesigned: " + spec.Describe() +
                           (stood ? " -- standing where it stood, its top " + (anchor.y - _base.y).ToString("F1") + " m over it" : ""));
        }

        /// <summary>
        /// The gizmo moved or turned the selected flag: its prop moved and turned (on the
        /// simulation thread, as the panel's own sliders do), its tilt and lean into its spec, and
        /// the tab's controls showing the new values at once. Tilted or leaning, it stands again
        /// where it stood (Restand: a pole that stands pivots on its foot, as tall as it was).
        /// </summary>
        /// <param name="held">A gizmo drag or a run of Page Up / Page Down: one undo step until FlagUndo.End (the drag or the keys let go).</param>
        /// <returns>Where the flag's anchor goes: <paramref name="anchor"/>, or where it stands again.</returns>
        public static Vector3 ApplyTransform(Vector3 anchor, float angle, float tilt, float lean, bool held = false)
        {
            FlagEntry e;
            if (!TryEntry(out e))
                return anchor;

            FlagState before = FlagUndo.Now(Selected);
            _quietUntil = Time.realtimeSinceStartup + 0.6f;
            FlagSpec old = Latest(e);
            if (Mathf.Abs(tilt - old.Tilt) > 1e-4f || Mathf.Abs(lean - old.Lean) > 1e-4f)
            {
                // Against the spec as last set (the renderer's is a frame behind a drag): every
                // step of a ring's drag is a new line (the gizmo calls when it changed by 1e-4).
                FlagSpec spec = old;
                spec.Tilt = tilt;
                spec.Lean = lean;
                Vector3 stood;
                if (Restand(e, old, ref spec, angle, out stood))
                    anchor = stood;
                FlagRegistry.Set(Selected, spec);
            }
            else
            {
                // Moved or turned: where it stands goes with it.
                LetBaseGo();
            }

            if ((anchor - e.Anchor).sqrMagnitude > 1e-8f)
                FlagProps.Move(Selected, anchor);
            if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, e.Angle * Mathf.Rad2Deg)) > 1e-3f)
                FlagProps.Rotate(Selected, angle);
            FlagUndo.Sent(Selected, anchor, angle);
            FlagUndo.Changed(Selected, before, held);

            _loading = true;
            try
            {
                Settings.SelRotation.value = Mathf.Round(Mathf.Repeat(angle * Mathf.Rad2Deg, 360f) * 10f) / 10f;
                Settings.SelTilt.value = Mathf.Round(tilt * 10f) / 10f;
                Settings.SelLean.value = Mathf.Round(lean * 10f) / 10f;
            }
            finally
            {
                _loading = false;
            }

            return anchor;
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

            FlagState before = FlagUndo.Now(Selected);
            _quietUntil = Time.realtimeSinceStartup + 0.6f;
            LetBaseGo();
            float angle = Settings.SelRotation.value * Mathf.Deg2Rad;
            FlagProps.Rotate(Selected, angle);
            FlagUndo.Sent(Selected, before.Present ? new Vector3(before.X, before.Y, before.Z) : e.Anchor, angle);
            FlagUndo.Changed(Selected, before, false);
        }

        /// <summary>
        /// The Flagpole popup's Pole height: the selected flag's pole this long, its foot where it
        /// was -- the flag goes up or down its axis with the pole's top. (Move It, the gizmo's arrows
        /// and Page Up and Page Down move the whole flag, its pole with it.) Before its first cast
        /// has found the pole's length (an older save's), the flag stays and the foot goes.
        /// </summary>
        public static void ApplyPoleHeight()
        {
            FlagEntry e;
            if (_loading || !TryEntry(out e))
                return;

            FlagSpec spec = Latest(e);
            if (!FlagStand.PoleStands(spec))
                return;

            float length = PoleHeight();
            float old = spec.PoleLength;
            if (Mathf.Abs(length - old) < 1e-3f)
                return;

            // The foot, from where the flag stood when the slider began to move (HoldBase).
            FlagState before = FlagUndo.Now(Selected);
            bool known = HoldBase(e, spec);
            _quietUntil = Time.realtimeSinceStartup + 0.6f;
            spec.PoleLength = length;
            FlagRegistry.Set(Selected, spec);
            if (known)
            {
                _baseHeight = length;
                Vector3 anchor = StandAt(spec, e.Angle);
                FlagProps.Move(Selected, anchor);
                FlagUndo.Sent(Selected, anchor, before.Present ? before.Angle : e.Angle);
            }
            FlagUndo.Changed(Selected, before, false);

            if (Log.Detailed)
                Log.Detail("flags: flag #" + Selected + "'s pole " + length.ToString("F1") + " m tall now (was " + old.ToString("F1") + ")");
        }

        /// <summary>
        /// An undo or redo (FlagUndo) put flag <paramref name="prop"/> back to <paramref name="state"/>
        /// -- its design already the registry's, its prop's move and turn on their way. Picking, the
        /// flag is selected, so the gizmo and the tab show what changed (placing, the New flag
        /// window stays: selecting would put it away). Selected, the tab's controls, the pole's held
        /// foot, the gizmo and the raise keys' own place all take the state now: none of what a
        /// gesture just before held may pull the flag back.
        /// </summary>
        public static void Restored(uint prop, FlagState state)
        {
            FlagEntry e;
            FlagRenderer r = FlagRenderer.Instance;
            if (FlagTool.IsPicking && Selected != prop && r != null && r.TryGet(prop, out e))
            {
                Select(prop);
                UI.FlagsPanel.ShowExisting();
            }

            if (Selected != prop)
            {
                if (_baseOf == prop)
                    LetBaseGo();
                return;
            }

            float now = Time.realtimeSinceStartup;
            _quietUntil = now + 0.6f;
            Vector3 anchor = new Vector3(state.X, state.Y, state.Z);

            // A pole that stands, of a known height: drawn from the foot it is put back on while
            // the prop follows (TryHeldBase), and any change right after stands it from there.
            if (FlagStand.PoleStands(state.Spec) && state.Spec.PoleLength > 0f)
            {
                float x, y, z;
                FlagStand.Base(state.Spec, state.Angle, state.Spec.PoleLength, anchor.x, anchor.y, anchor.z, out x, out y, out z);
                _base = new Vector3(x, y, z);
                _baseHeight = state.Spec.PoleLength;
                _baseKnown = true;
                _baseOf = prop;
                _baseUntil = now + HoldTime;
            }
            else
            {
                LetBaseGo();
            }

            Fill(state.Spec, state.Angle, true);
            FlagTool.ShowRestored(anchor, state.Angle, state.Spec.Tilt, state.Spec.Lean);
            UI.SettingsCatalog.RefreshAllUIs();
        }

        // ---- where the selected flag stands ---------------------------------------------------

        /// <summary>
        /// Where the selected flag stands (FlagStand.Base: its standing pole's foot, else the lowest
        /// of it at rest), the height its pole stands with, and whether that is known -- held while a
        /// change that stands it again is under way (the Pole height's slider, the edge hoisted, a
        /// tilt or lean from the Flagpole popup or the gizmo's rings): the prop follows a frame or two
        /// behind, and the spec's new values are already the registry's, so working it out afresh
        /// from the two mid-change would walk the flag's foot away.
        /// </summary>
        private static Vector3 _base;
        private static float _baseHeight;
        private static bool _baseKnown;
        private static uint _baseOf;
        private static float _baseUntil;

        /// <summary>A change's stand point is held this long after its last step, seconds (the time the prop may take to follow, as <see cref="_quietUntil"/>'s).</summary>
        private const float HoldTime = 0.6f;

        private static bool Holding
        {
            get { return _baseOf == Selected && Selected != 0 && Time.realtimeSinceStartup <= _baseUntil; }
        }

        /// <summary>
        /// Holds where the selected flag stands, worked out from its prop and <paramref name="spec"/>
        /// (as it stands, at rest) when no change is under way; false if that cannot be known -- a
        /// pole that stands, its length still being found (an older save's): then the flag's top stays.
        /// </summary>
        private static bool HoldBase(FlagEntry e, FlagSpec spec)
        {
            if (!Holding)
            {
                bool stands = FlagStand.PoleStands(spec);
                _baseOf = Selected;
                _baseKnown = !stands || spec.PoleLength > 0f;
                _baseHeight = stands && spec.PoleLength > 0f ? spec.PoleLength : PoleHeight();
                float x, y, z;
                FlagStand.Base(spec, e.Angle, _baseHeight, e.Anchor.x, e.Anchor.y, e.Anchor.z, out x, out y, out z);
                _base = new Vector3(x, y, z);
            }

            _baseUntil = Time.realtimeSinceStartup + HoldTime;
            return _baseKnown;
        }

        /// <summary>
        /// The stand point held for flag <paramref name="prop"/> while a change that stands it again
        /// is under way (a standing pole's foot): the renderer draws the pole from it meanwhile.
        /// The new spec is the registry's at once, the prop's move comes a frame or more later (the
        /// simulation thread's), and a pole drawn from the old anchor with the new spec jumped up or
        /// down by the difference and back at every step of "Position on pole" (2026-11-10, the
        /// author: "it's causing the pole itself to stretch up and down"). Main thread.
        /// </summary>
        public static bool TryHeldBase(uint prop, out Vector3 point)
        {
            point = _base;
            return prop != 0 && prop == _baseOf && _baseKnown && Holding;
        }

        /// <summary>The flag moved or turned some other way (the gizmo's arrows, Page Up and Page Down, its rotation): where it stands goes with it.</summary>
        private static void LetBaseGo()
        {
            _baseUntil = 0f;
        }

        /// <summary>Where the anchor of the selected flag, of <paramref name="spec"/> turned to <paramref name="heading"/>, goes to stand on the held stand point.</summary>
        private static Vector3 StandAt(FlagSpec spec, float heading)
        {
            float x, y, z;
            FlagStand.Anchor(spec, heading, _baseHeight, _base.x, _base.y, _base.z, out x, out y, out z);
            return new Vector3(x, y, z);
        }

        /// <summary>
        /// The selected flag turns onto a new hoist line, from <paramref name="old"/> to
        /// <paramref name="spec"/> (<paramref name="heading"/> its angle then). With a pole that
        /// stands before or after (FlagStand.Restands), it stands again where it stood, as the tool
        /// stands a new one: a pole that stands keeps its height -- the Pole height's, whatever the
        /// line -- and its foot, a crossbar finds its length along to its wall (0); true, and where
        /// its anchor goes. Otherwise (no pole, a crossbar turned, a pole still being found) its top
        /// stays and the length is found along the new line: false.
        /// </summary>
        private static bool Restand(FlagEntry e, FlagSpec old, ref FlagSpec spec, float heading, out Vector3 anchor)
        {
            anchor = e.Anchor;
            if (!old.PoleChosen || !spec.PoleChosen || (!Holding && !FlagStand.Restands(old, spec)) || !HoldBase(e, old))
            {
                spec.PoleLength = 0f;
                return false;
            }

            spec.PoleLength = FlagStand.PoleStands(spec) ? _baseHeight : 0f;
            anchor = StandAt(spec, heading);
            return true;
        }

        /// <summary>The Pole height row's value: how tall the selected flag's pole stands, or would if its hoist turned down.</summary>
        private static float PoleHeight()
        {
            return Mathf.Clamp(Settings.SelHeight.value, FlagPoleShape.MinLength, FlagSpec.MaxPoleLength);
        }

        /// <summary>The selected flag's spec as last set: the registry's -- the renderer's copy is a frame behind a change of ours.</summary>
        private static FlagSpec Latest(FlagEntry e)
        {
            FlagSpec spec;
            return FlagRegistry.TryGet(e.Prop, out spec) ? spec : e.Spec;
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

                // The middle of the flag at rest: half way down its hoist and out along its fly.
                float hx, hy, hz, fx, fy, fz;
                FlagOrientation.Frame(e.Angle, e.Spec.Tilt, e.Spec.Lean, e.Spec.HoistedFrom, out hx, out hy, out hz, out fx, out fy, out fz);
                camera.m_targetPosition = e.Anchor + 0.5f * e.Spec.Height * new Vector3(hx, hy, hz) + 0.5f * e.Spec.Length * new Vector3(fx, fy, fz);
                camera.m_targetSize = Mathf.Clamp(Mathf.Max(e.Spec.Length, e.Spec.Height) * 8f, 30f, 600f);
                Log.Msg("flags: camera to flag #" + Selected);
            }
            catch (Exception ex)
            {
                Log.Warn("flags: could not move the camera: " + ex.Message);
            }
        }

        /// <summary>
        /// The New flag window takes everything of the selected flag's, for another like it (the
        /// panel then opens the window to place it): its design, its pole -- the Pole height too, the
        /// range unlocked if it is taller than the locked one (the author, 2026-11-09: "Copy a flag is
        /// not copying all properties correctly, especially the flagpole height") -- and its rotation.
        /// Its name and its place are its own.
        /// </summary>
        public static void CopyToNew()
        {
            FlagEntry e;
            if (!TryEntry(out e))
                return;

            // Read the latest: the renderer's copy is a frame behind a change just made in the panel.
            FlagSpec spec = Latest(e);
            CopySpecToNew(spec);

            // A pole that stands: its height. Otherwise (none, a crossbar, one still being found) the
            // Flags tab's Pole height row, what it would stand with -- as that row shows.
            float height = FlagStand.PoleStands(spec) && spec.PoleLength > 0f ? spec.PoleLength : Settings.SelHeight.value;
            // Past the slider's 50 m it is kept as it is (the row's readout says it).
            height = Mathf.Clamp(Mathf.Round(height * 100f) / 100f, FlagPoleShape.MinLength, Settings.Defaults.MaxNewHeight);
            Settings.NewHeight.value = height;
            Settings.NewRotation.value = FlagOrientation.WholeDegrees(Mathf.Repeat(e.Angle * Mathf.Rad2Deg, 360f));
            Log.Msg("flags: flag #" + Selected + " copied to the new flag (" + spec.Describe() + "; pole height " +
                    height.ToString("0.##", CultureInfo.InvariantCulture) + " m, rotation " + Settings.NewRotation.value.ToString("0.#", CultureInfo.InvariantCulture) + " deg)");
        }

        /// <summary>
        /// Every design and pole setting of the New flag window from <paramref name="s"/> -- each of
        /// FlagSpec's fields Settings.NewSpec reads, but its name and the pole's length (the Pole
        /// height row, set by CopyToNew).
        /// </summary>
        private static void CopySpecToNew(FlagSpec s)
        {
            Settings.NewCountry.value = FlagCountries.NumberOf(s.Country);
            Settings.NewImage.value = FlagImageNames.NumberOf(s.Image);
            Settings.NewImageX.value = s.ImageX;
            Settings.NewImageY.value = s.ImageY;
            Settings.NewImageZoom.value = s.ImageZoom;
            Settings.NewImageTurn.value = s.ImageTurn;
            Settings.NewImageAlpha.value = s.ImageAlpha;
            Settings.NewPattern.value = s.Pattern;
            Settings.NewColour1.value = (int)s.Colour1;
            Settings.NewColour2.value = (int)s.Colour2;
            Settings.NewColour3.value = (int)s.Colour3;
            Settings.NewLength.value = s.Length;
            Settings.NewProportion.value = s.Proportion;
            Settings.NewHoistedFrom.value = s.HoistedFrom;
            Settings.NewMotion.value = s.Motion;
            Settings.NewWaviness.value = s.Waviness;
            Settings.NewWindShare.value = s.WindShare;
            Settings.NewCut.value = s.Cut;
            Settings.NewCutDepth.value = s.CutDepth;
            Settings.NewDarkness.value = s.Darkness;
            Settings.NewWear.value = s.Wear;
            Settings.NewDirt.value = s.Dirt;
            Settings.NewHoles.value = s.Holes;
            Settings.NewTear.value = s.Tear;
            Settings.NewTearEdges.value = s.TearEdges;
            Settings.NewBackPlate.value = s.BackPlate;
            Settings.NewPole.value = s.Pole;
            Settings.NewRopes.value = s.Ropes;
            Settings.NewPoleColour.value = (int)s.PoleColour;
            Settings.NewTopColour.value = (int)s.TopColour;
            Settings.NewRopeColour.value = (int)s.RopeColour;
            Settings.NewPoleThickness.value = s.PoleThickness;
            Settings.NewPoleProfile.value = s.PoleProfile;
            Settings.NewPoleBase.value = s.PoleBase;
            Settings.NewPoleShine.value = s.PoleShine;
            Settings.NewPoleWear.value = s.PoleWear;
            Settings.NewPoleRust.value = s.PoleRust;
            Settings.NewPoleDirt.value = s.PoleDirt;
            Settings.NewRopeThickness.value = s.RopeThickness;
            Settings.NewPolePosition.value = s.PolePosition;
            Settings.NewTake.value = s.Take;
            Settings.NewHeld.value = s.Held;
            Settings.NewTilt.value = s.Tilt;
            Settings.NewLean.value = s.Lean;
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

                    // A step to undo (FlagUndo): the flag as it was, put back on Ctrl+Z.
                    FlagUndo.Deleting(prop);
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

        /// <summary>"Flag 12", or its name.</summary>
        public static string Title(FlagEntry e)
        {
            return string.IsNullOrEmpty(e.Spec.Name) ? Localization.Get("Flag.Title", e.Prop) : e.Spec.Name;
        }
    }
}
