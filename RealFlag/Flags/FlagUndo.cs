using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// Undo (Ctrl+Z) and redo (Ctrl+Y, Ctrl+Shift+Z) of what the player does to placed flags with
    /// the mod's own tool and panel (the author, 2026-11-12: "there's no undo/redo for moving the
    /// flag. Let's add this", "make sure all states have undo/redo with ctrl+z and ctrl y or shift
    /// z"): the gizmo's moves and turns, Page Up / Page Down, every row of the selected flag's
    /// design and pole, placing a flag and deleting one. FlagHistory keeps the steps (PURE); this
    /// reads the flags' states from the game and puts them back through the paths an edit takes
    /// (FlagRegistry, FlagProps on the simulation thread), never touching a cloth (Invariant 2: the
    /// renderer follows the prop and the registry as for any edit). Main thread, but for the
    /// simulation thread's answers to a placing (<see cref="Enqueue"/>).
    /// </summary>
    /// <remarks>
    /// Recorded where the mod changes a placed flag for the player: FlagEditor's ApplyDesign,
    /// ApplyTransform, ApplyRotation, ApplyPoleHeight and ConfirmDelete, FlagTool's placing. NOT
    /// the renderer's own writes (a pole's cast length kept, a height fixed, flags stood on the
    /// ground at load), nor Move It's, Line Tool's or any other tool's: Move It keeps its own undo.
    /// A step's states are whole (place, turn, the whole FlagSpec), so every row present and to
    /// come is covered without code of its own. Undo puts back the state before even if another
    /// tool moved the flag since. Nothing of it is saved; it is cleared when the city unloads.
    ///
    /// The prop's place and turn reach the game a frame or two after they are sent (FlagProps on
    /// the simulation thread): what was last sent is the flag's state for <see cref="KnownTime"/>
    /// (FlagEditor's own quiet time), so a gesture following another at once starts from where
    /// the last one sent it, not from the prop's old place.
    /// </remarks>
    public static class FlagUndo
    {
        private static readonly FlagHistory History = new FlagHistory();

        // The place and turn last sent to a prop by an edit or an undo, and until when they are
        // trusted over the prop's own (real time).
        private static uint _knownProp;
        private static float _knownX, _knownY, _knownZ, _knownAngle;
        private static float _knownUntil;

        /// <summary>A place sent is the flag's for this long, seconds (FlagEditor's quiet time; FlagHistory.JoinSeconds).</summary>
        private const float KnownTime = 0.6f;

        /// <summary>The simulation thread's answer to a flag made (placed, or put back by an undo or a redo).</summary>
        private struct Made
        {
            public int Generation;
            public bool PutBack;
            public int Flag;
            public uint Prop;
            public FlagState State;
            public string Word;
        }

        // Answers from the simulation thread, taken on the main thread (Drain).
        private static readonly object MadeLock = new object();
        private static readonly List<Made> _made = new List<Made>();

        // Flags asked of the simulation thread and not answered yet (main thread): undo and redo
        // wait for them -- a step must never act on a prop that is not there yet. Generation: a
        // late answer from a city unloaded since is ignored.
        private static int _waiting;
        private static float _waitingSince;
        private static int _generation;

        /// <summary>An answer not come within this long, seconds, is no longer waited for (said once; a late one is still taken).</summary>
        private const float WaitLimit = 10f;

        // A flag put back, to be shown selected once the renderer has found its new prop (its scan
        // goes through the buffer in 16 frames), until then at most -- as it is THEN (Now): another
        // undo may have changed it meanwhile.
        private static uint _showProp;
        private static float _showUntil;

        /// <summary>A prop answered for a flag made is taken for it only this close to where it was made, metres (a prop's x and z are kept to 0.26 m).</summary>
        private const float MadeTolerance = 0.5f;

        /// <summary>A flag put back is waited for this long, seconds, to show it selected.</summary>
        private const float ShowWait = 3f;

        // ---- recording ------------------------------------------------------------------------

        /// <summary>
        /// The flag on <paramref name="prop"/> as it is now: its place and turn as last sent (within
        /// <see cref="KnownTime"/>) or its prop's, its design the registry's (set at once by every
        /// edit). Absent if the prop is not a flag.
        /// </summary>
        public static FlagState Now(uint prop)
        {
            Vector3 anchor;
            float angle;
            bool fixedHeight;
            if (!FlagProps.TryRead(prop, out anchor, out angle, out fixedHeight))
                return FlagState.Absent;

            FlagSpec spec;
            if (!FlagRegistry.TryGet(prop, out spec))
                return FlagState.Absent;

            if (prop == _knownProp && Time.realtimeSinceStartup <= _knownUntil)
                return FlagState.At(_knownX, _knownY, _knownZ, _knownAngle, spec);
            return FlagState.At(anchor.x, anchor.y, anchor.z, angle, spec);
        }

        /// <summary>An edit sent <paramref name="prop"/> to this place and turn (FlagProps.Move / Rotate land a frame or two later).</summary>
        public static void Sent(uint prop, Vector3 anchor, float angle)
        {
            _knownProp = prop;
            _knownX = anchor.x;
            _knownY = anchor.y;
            _knownZ = anchor.z;
            _knownAngle = angle;
            _knownUntil = Time.realtimeSinceStartup + KnownTime;
        }

        private static void Sent(uint prop, FlagState state)
        {
            Sent(prop, new Vector3(state.X, state.Y, state.Z), state.Angle);
        }

        /// <summary>
        /// The player changed the flag on <paramref name="prop"/>, from <paramref name="before"/>
        /// (<see cref="Now"/> before the change) to what it is now: the gesture's step, joined or
        /// opened (FlagHistory.Change). <paramref name="held"/>: a drag or the raise keys, ended by
        /// <see cref="End"/>.
        /// </summary>
        public static void Changed(uint prop, FlagState before, bool held)
        {
            if (prop == 0 || !before.Present)
                return;

            FlagState after = Now(prop);
            if (after.Present)
                History.Change(prop, before, after, Time.realtimeSinceStartup, held);
        }

        /// <summary>The gesture ended (a drag or the keys let go, the selection changed): its step closes.</summary>
        public static void End()
        {
            History.End();
        }

        /// <summary>
        /// The mod's tool places a flag (FlagProps.Create, <paramref name="done"/> told its prop on
        /// the simulation thread): a step once the prop exists, the flag as it was placed -- what a
        /// redo puts back, the crowds' rule (FlagRenderer.Crowd.cs) already in its design.
        /// </summary>
        public static void Place(Vector3 anchor, float angle, FlagSpec spec, Action<uint> done)
        {
            // No prefab: Create says why and makes nothing, nor answers.
            if (FlagPrefab.Info == null)
            {
                FlagProps.Create(anchor, angle, spec, done);
                return;
            }

            FlagState state = FlagState.At(anchor.x, anchor.y, anchor.z, angle, spec.Sanitised());
            int generation = _generation;
            Wait();
            FlagProps.Create(anchor, angle, spec, prop =>
            {
                try
                {
                    if (done != null)
                        done(prop);
                }
                finally
                {
                    Enqueue(new Made { Generation = generation, PutBack = false, Prop = prop, State = state });
                }
            });
        }

        /// <summary>
        /// The player deletes the flag on <paramref name="prop"/> through the mod (the panel's
        /// Delete, the Del key), just before FlagProps.Delete: a step, its state before kept.
        /// </summary>
        public static void Deleting(uint prop)
        {
            FlagState before = Now(prop);
            if (before.Present)
                History.Deleted(prop, before);
        }

        /// <summary>
        /// The flag on <paramref name="prop"/> is gone, and not by a step of ours (the bulldozer, Move
        /// It's delete; FlagEditor.Forget, from the renderer): its steps go with it -- the game reuses
        /// the index.
        /// </summary>
        public static void Forget(uint prop)
        {
            History.Forget(prop);
            if (_knownProp == prop)
                _knownUntil = 0f;
        }

        /// <summary>The city unloads: nothing to undo or redo, nothing waited for.</summary>
        public static void Clear()
        {
            History.Clear();
            lock (MadeLock)
                _made.Clear();
            _generation++;
            _waiting = 0;
            _knownProp = 0;
            _knownUntil = 0f;
            _showProp = 0;
        }

        // ---- every frame the tool is out ------------------------------------------------------

        /// <summary>
        /// Every frame while the mod's tool is out (FlagTool.OnToolUpdate): the simulation thread's
        /// answers taken; a step whose gesture is over closed; a mouse button held (a slider, the
        /// colour square) keeps its step open; a flag put back shown once the renderer has it.
        /// </summary>
        public static void Tick()
        {
            Drain();
            float now = Time.realtimeSinceStartup;
            if (Input.GetMouseButton(0))
                History.Keep(now);
            History.Tick(now);

            if (_waiting > 0 && now - _waitingSince > WaitLimit)
            {
                Log.Warn("undo: " + _waiting + " flag(s) asked of the game " + WaitLimit.ToString("F0", CultureInfo.InvariantCulture) +
                         " s ago have not come; undo and redo work again without them");
                _waiting = 0;
            }

            if (_showProp != 0)
            {
                FlagEntry entry;
                FlagRenderer r = FlagRenderer.Instance;
                if (r != null && r.TryGet(_showProp, out entry))
                {
                    uint prop = _showProp;
                    _showProp = 0;
                    FlagState state = Now(prop);
                    if (state.Present)
                        FlagEditor.Restored(prop, state);
                }
                else if (now > _showUntil)
                {
                    _showProp = 0;
                }
            }
        }

        private static void Wait()
        {
            if (_waiting == 0)
                _waitingSince = Time.realtimeSinceStartup;
            _waiting++;
        }

        /// <summary>The simulation thread's answer (FlagProps.Create's done), taken on the main thread by <see cref="Drain"/>.</summary>
        private static void Enqueue(Made made)
        {
            lock (MadeLock)
                _made.Add(made);
        }

        private static void Drain()
        {
            Made[] made;
            lock (MadeLock)
            {
                if (_made.Count == 0)
                    return;
                made = _made.ToArray();
                _made.Clear();
            }

            foreach (Made m in made)
            {
                if (m.Generation != _generation)
                    continue;

                if (_waiting > 0)
                    _waiting--;

                // Taken only while that prop is still the flag made: an answer taken late (this
                // runs while the tool is out) may name a prop deleted meanwhile, its index another
                // flag's by now -- whom Ctrl+Z must never take away.
                bool there = m.Prop != 0 && Stands(m.Prop, m.State);

                if (!m.PutBack)
                {
                    // Placed (FlagTool already said where): undo takes it away again.
                    if (there)
                        History.Placed(m.Prop, m.State);
                    continue;
                }

                if (m.Prop == 0)
                {
                    History.DropFlag(m.Flag);
                    Log.Warn(m.Word + ": the flag could not be put back (the city has as many props as it can hold); its steps are let go");
                    continue;
                }

                if (!there)
                {
                    History.DropFlag(m.Flag);
                    Log.Msg(m.Word + ": flag #" + m.Prop + ", put back, was gone again before it was seen; its steps are let go");
                    continue;
                }

                if (!History.Rebind(m.Flag, m.Prop))
                {
                    // Its steps were let go meanwhile (a new step cleared the redo side): the flag
                    // stays, as any placed flag.
                    Log.Msg(m.Word + ": flag #" + m.Prop + " put back (" + m.State.Spec.Describe() + ")");
                    continue;
                }

                Sent(m.Prop, m.State);
                Log.Msg(m.Word + ": flag #" + m.Prop + " put back where it stood (" + Where(m.State) + "): " + m.State.Spec.Describe());

                // Shown selected once the renderer has found it (picking only: FlagEditor.Restored).
                _showProp = m.Prop;
                _showUntil = Time.realtimeSinceStartup + ShowWait;
            }
        }

        /// <summary>The prop is a flag standing where <paramref name="state"/> was made (within <see cref="MadeTolerance"/>).</summary>
        private static bool Stands(uint prop, FlagState state)
        {
            Vector3 anchor;
            float angle;
            bool fixedHeight;
            return FlagProps.TryRead(prop, out anchor, out angle, out fixedHeight) &&
                   Mathf.Abs(anchor.x - state.X) <= MadeTolerance && Mathf.Abs(anchor.y - state.Y) <= MadeTolerance &&
                   Mathf.Abs(anchor.z - state.Z) <= MadeTolerance;
        }

        // ---- undo and redo --------------------------------------------------------------------

        /// <summary>Ctrl+Z: the last step undone.</summary>
        public static void Undo()
        {
            Apply(true);
        }

        /// <summary>Ctrl+Y, Ctrl+Shift+Z: the last step undone made again.</summary>
        public static void Redo()
        {
            Apply(false);
        }

        private static void Apply(bool undo)
        {
            string word = undo ? "undo" : "redo";
            Drain();
            if (_waiting > 0)
            {
                Log.Msg(word + ": not yet -- a flag is still being placed or put back (a frame or two); press again");
                return;
            }

            int flag;
            FlagState from, to;
            if (!(undo ? History.TryUndo(out flag, out from, out to) : History.TryRedo(out flag, out from, out to)))
            {
                Log.Msg(word + ": nothing to " + word);
                return;
            }

            uint prop = History.PropOf(flag);
            if (from.Present && (prop == 0 || !FlagProps.IsFlagProp(prop)))
            {
                // Gone some way the renderer has not seen yet: nothing of it to put back.
                History.DropFlag(flag);
                Log.Msg(word + ": that flag is no longer in the city; its steps are let go");
                return;
            }

            if (from.Present && to.Present)
            {
                // As an edit does: the design into the registry, the prop moved and turned.
                FlagRegistry.Set(prop, to.Spec);
                FlagProps.Move(prop, new Vector3(to.X, to.Y, to.Z));
                FlagProps.Rotate(prop, to.Angle);
                Sent(prop, to);
                Log.Msg(word + ": flag #" + prop + " " + Changes(from, to));
                FlagEditor.Restored(prop, to);
                return;
            }

            if (from.Present)
            {
                // Taken away: its placing undone, or its deletion made again. The renderer's
                // finding the prop gone is then this step's doing (the flag let go of it first).
                History.Unbind(flag);
                if (_knownProp == prop)
                    _knownUntil = 0f;
                if (_showProp == prop)
                    _showProp = 0;
                if (FlagEditor.Selected == prop)
                    FlagEditor.Deselect();
                FlagProps.Delete(prop);
                Log.Msg(word + ": flag #" + prop + " taken away again (" + (undo ? "its placing undone" : "deleted again") + ")");
                return;
            }

            if (!to.Present)
                return;

            // Put back: on a new prop, made on the simulation thread; every step of it follows it
            // there once it exists (Drain: FlagHistory.Rebind), and nothing is undone meanwhile.
            if (FlagPrefab.Info == null)
            {
                History.DropFlag(flag);
                Log.Warn(word + ": the flag cannot be put back: the flag prefab was not made in this city (see the start of the log)");
                return;
            }

            int generation = _generation;
            Wait();
            FlagProps.Create(new Vector3(to.X, to.Y, to.Z), to.Angle, to.Spec, made =>
                Enqueue(new Made { Generation = generation, PutBack = true, Flag = flag, Prop = made, State = to, Word = word }));
        }

        /// <summary>What a step changes, for the log: "moved back 3.2 m, turned 45 deg, its design: ...".</summary>
        private static string Changes(FlagState from, FlagState to)
        {
            var inv = CultureInfo.InvariantCulture;
            var parts = new List<string>();
            float moved = new Vector3(to.X - from.X, to.Y - from.Y, to.Z - from.Z).magnitude;
            if (moved > FlagState.PlaceTolerance)
                parts.Add("moved " + moved.ToString("0.##", inv) + " m to (" + Where(to) + ")");
            float turned = FlagState.AngleBetween(from.Angle, to.Angle) * Mathf.Rad2Deg;
            if (turned > FlagState.AngleTolerance * Mathf.Rad2Deg)
                parts.Add("turned " + turned.ToString("0.#", inv) + " deg to " + Mathf.Repeat(to.Angle * Mathf.Rad2Deg, 360f).ToString("0.#", inv) + " deg");
            if (!FlagState.Same(FlagState.At(0f, 0f, 0f, 0f, from.Spec), FlagState.At(0f, 0f, 0f, 0f, to.Spec)))
                parts.Add("its design as it was: " + to.Spec.Describe());
            return parts.Count > 0 ? string.Join(", ", parts.ToArray()) : "as it was (no change to show: another tool had put it so)";
        }

        private static string Where(FlagState s)
        {
            var inv = CultureInfo.InvariantCulture;
            return s.X.ToString("F0", inv) + ", " + s.Z.ToString("F0", inv) + "; top of the hoist " + s.Y.ToString("F1", inv) + " m";
        }
    }
}
