using System;
using System.Collections.Generic;

namespace RealFlag.Flags
{
    /// <summary>
    /// A placed flag as undo and redo keep it (FlagHistory): where its prop stands (its anchor, the
    /// top hoist corner), which way it turns, and its whole design -- or not in the city at all
    /// (before it was placed, after it was deleted). PURE: no engine call (test-cloth tests it).
    /// </summary>
    public struct FlagState
    {
        /// <summary>False: the flag is not in the city (the state before its placing, after its deletion).</summary>
        public bool Present;

        /// <summary>The prop's position, world metres (y the flag's own: a flag prop is FixedHeight).</summary>
        public float X, Y, Z;

        /// <summary>The prop's angle, radians.</summary>
        public float Angle;

        public FlagSpec Spec;

        /// <summary>Closer than this, metres, two places are the same (a prop's x and z are kept to 0.26 m anyway, FlagProps).</summary>
        public const float PlaceTolerance = 1e-3f;

        /// <summary>Closer than this, radians, two turns are the same (a prop's angle is kept to 1/10430 of a radian).</summary>
        public const float AngleTolerance = 1e-4f;

        public static FlagState Absent
        {
            get { return new FlagState { Present = false, Spec = FlagSpec.Default }; }
        }

        public static FlagState At(float x, float y, float z, float angle, FlagSpec spec)
        {
            return new FlagState { Present = true, X = x, Y = y, Z = z, Angle = angle, Spec = spec };
        }

        /// <summary>
        /// The same flag state: both absent, or the same place and turn (within the tolerances) and
        /// the same design -- compared as the bytes the city saves it as (FlagCodec.EncodeOne), so
        /// every field a save keeps counts, today's and every one added to the codec later.
        /// </summary>
        public static bool Same(FlagState a, FlagState b)
        {
            if (a.Present != b.Present)
                return false;
            if (!a.Present)
                return true;

            if (Math.Abs(a.X - b.X) > PlaceTolerance || Math.Abs(a.Y - b.Y) > PlaceTolerance || Math.Abs(a.Z - b.Z) > PlaceTolerance)
                return false;

            if (AngleBetween(a.Angle, b.Angle) > AngleTolerance)
                return false;

            byte[] x = FlagCodec.EncodeOne(a.Spec), y = FlagCodec.EncodeOne(b.Spec);
            if (x.Length != y.Length)
                return false;
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] != y[i])
                    return false;
            }

            return true;
        }

        /// <summary>The turn between two angles, radians, the short way round (0..pi).</summary>
        public static float AngleBetween(float a, float b)
        {
            double twoPi = 2.0 * Math.PI;
            double d = Math.Abs(a - b) % twoPi;
            return (float)Math.Min(d, twoPi - d);
        }
    }

    /// <summary>
    /// Undo and redo of what the mod's own tool and panel do to placed flags: the steps, in the
    /// order they were made. PURE (no engine call): FlagUndo reads the flags' states from the game
    /// and applies them; this keeps the order. Main thread only.
    /// </summary>
    /// <remarks>
    /// A STEP is a flag's state before and after one gesture of the player's (a gizmo drag from
    /// press to release, a run of Page Up / Page Down, a slider's drag, a click), or its placing
    /// (absent before) or deletion (absent after). Changes to the same flag join the step still
    /// OPEN until it is closed: by an explicit end (<see cref="End"/>: the drag or the keys let go,
    /// the selection changed, an undo or redo asked), by a change to another flag, or by
    /// <see cref="JoinSeconds"/> with no change -- never while the gesture HOLDS it open (a drag, the
    /// keys: <c>held</c>; a mouse button down: <see cref="Keep"/>). A step whose after is its before
    /// (<see cref="FlagState.Same"/>) is dropped. A new step clears what could be redone; at most
    /// <see cref="MaxSteps"/> are kept, the oldest let go.
    ///
    /// Steps name a FLAG of this history's own numbering, never a prop index: the game reuses
    /// prop indices, and a flag deleted and put back comes back on a new prop (FlagProps.Create on
    /// the simulation thread). Each flag is BOUND to the prop that carries it now (0: none -- taken
    /// away by a step of ours); <see cref="Rebind"/> moves every step of it to its new prop at once.
    /// A flag gone any other way (the bulldozer, Move It's delete: <see cref="Forget"/>) takes its
    /// steps with it, so a later flag on its old index never receives them.
    /// </remarks>
    public sealed class FlagHistory
    {
        /// <summary>At most this many steps are kept to undo (the oldest let go).</summary>
        public const int MaxSteps = 100;

        /// <summary>Changes to one flag this close together are one step, seconds (FlagEditor's and FlagTool's holds are as long).</summary>
        public const float JoinSeconds = 0.6f;

        private sealed class Step
        {
            public int Flag;
            public FlagState Before, After;
        }

        /// <summary>The steps to undo, oldest first.</summary>
        private readonly List<Step> _undo = new List<Step>();

        /// <summary>The steps undone, to redo: the next one last.</summary>
        private readonly List<Step> _redo = new List<Step>();

        /// <summary>Each flag's prop now (0: not in the city, taken away by a step of ours).</summary>
        private readonly Dictionary<int, uint> _propOf = new Dictionary<int, uint>();

        private int _nextFlag = 1;

        // The step of the gesture under way, if any: when it last changed, and whether the gesture holds it open.
        private Step _open;
        private float _openAt;
        private bool _openHeld;

        public int UndoCount
        {
            get { return _undo.Count; }
        }

        public int RedoCount
        {
            get { return _redo.Count; }
        }

        public bool HasOpen
        {
            get { return _open != null; }
        }

        /// <summary>The prop that carries <paramref name="flag"/> now; 0 for none.</summary>
        public uint PropOf(int flag)
        {
            uint prop;
            return _propOf.TryGetValue(flag, out prop) ? prop : 0u;
        }

        /// <summary>The flag a prop carries, as this history numbers it; 0 for none.</summary>
        public int FlagOf(uint prop)
        {
            if (prop == 0)
                return 0;

            foreach (KeyValuePair<int, uint> p in _propOf)
            {
                if (p.Value == prop)
                    return p.Key;
            }

            return 0;
        }

        /// <summary>
        /// The flag on <paramref name="prop"/> went from <paramref name="before"/> to
        /// <paramref name="after"/> at <paramref name="now"/> (seconds): it joins the open step of
        /// that flag (its before kept, its after this one), or opens a new step. <paramref name="held"/>:
        /// the gesture keeps the step open until <see cref="End"/>, however long between changes.
        /// </summary>
        public void Change(uint prop, FlagState before, FlagState after, float now, bool held)
        {
            if (prop == 0)
                return;

            Tick(now);
            int flag = FlagOf(prop);
            if (_open != null && flag != 0 && _open.Flag == flag)
            {
                _open.After = after;
                _openAt = now;
                _openHeld |= held;
                return;
            }

            Close();
            if (flag == 0)
                flag = Bind(prop);
            _open = new Step { Flag = flag, Before = before, After = after };
            _openAt = now;
            _openHeld = held;
        }

        /// <summary>The gesture is still under way (a mouse button held down on a slider): the open step does not close by time.</summary>
        public void Keep(float now)
        {
            if (_open != null)
                _openAt = now;
        }

        /// <summary>The open step closes once no change has come for <see cref="JoinSeconds"/>, unless its gesture holds it.</summary>
        public void Tick(float now)
        {
            if (_open != null && !_openHeld && now - _openAt > JoinSeconds)
                Close();
        }

        /// <summary>The gesture ended: its step closes (kept, or dropped if nothing changed).</summary>
        public void End()
        {
            Close();
        }

        /// <summary>A flag placed by the mod's tool on <paramref name="prop"/>: a step of its own, absent before.</summary>
        public void Placed(uint prop, FlagState after)
        {
            if (prop == 0)
                return;

            Close();
            // A new prop: whatever this history still bound to that index was a flag gone unseen.
            Forget(prop);
            Push(new Step { Flag = Bind(prop), Before = FlagState.Absent, After = after });
        }

        /// <summary>
        /// The flag on <paramref name="prop"/> is being deleted by the player through the mod: a
        /// step, absent after, and the flag let go of its prop now -- the renderer's finding the prop
        /// gone (<see cref="Forget"/>) is then this step's own doing and drops nothing.
        /// </summary>
        public void Deleted(uint prop, FlagState before)
        {
            if (prop == 0)
                return;

            Close();
            int flag = FlagOf(prop);
            if (flag == 0)
                flag = Bind(prop);
            Push(new Step { Flag = flag, Before = before, After = FlagState.Absent });
            Unbind(flag);
        }

        /// <summary>
        /// The last step undone: the open step closed first, then the newest step moved to the redo
        /// side. <paramref name="from"/> is the state it left the flag in, <paramref name="to"/> the
        /// one to put back. False: nothing to undo.
        /// </summary>
        public bool TryUndo(out int flag, out FlagState from, out FlagState to)
        {
            Close();
            if (_undo.Count == 0)
            {
                flag = 0;
                from = to = FlagState.Absent;
                return false;
            }

            Step s = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            _redo.Add(s);
            flag = s.Flag;
            from = s.After;
            to = s.Before;
            return true;
        }

        /// <summary>The last step undone made again: as <see cref="TryUndo"/>, the other way. False: nothing to redo.</summary>
        public bool TryRedo(out int flag, out FlagState from, out FlagState to)
        {
            Close();
            if (_redo.Count == 0)
            {
                flag = 0;
                from = to = FlagState.Absent;
                return false;
            }

            Step s = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            _undo.Add(s);
            flag = s.Flag;
            from = s.Before;
            to = s.After;
            return true;
        }

        /// <summary>
        /// <paramref name="flag"/> was put back on a new prop (an undone deletion, a redone placing):
        /// every step of it names that prop from now on. A flag this history no longer knows (its
        /// steps let go meanwhile) is not bound again: false.
        /// </summary>
        public bool Rebind(int flag, uint prop)
        {
            if (prop == 0 || !_propOf.ContainsKey(flag))
                return false;

            // Whatever this history still bound to that index was a flag gone unseen.
            int other = FlagOf(prop);
            if (other != 0 && other != flag)
                DropFlag(other);
            _propOf[flag] = prop;
            return true;
        }

        /// <summary><paramref name="flag"/> taken away by a step of ours (undone placing, redone deletion): its steps stay, it has no prop.</summary>
        public void Unbind(int flag)
        {
            if (_propOf.ContainsKey(flag))
                _propOf[flag] = 0;
        }

        /// <summary>The flag on <paramref name="prop"/> is gone some other way: every step of it goes too.</summary>
        public void Forget(uint prop)
        {
            int flag = FlagOf(prop);
            if (flag != 0)
                DropFlag(flag);
        }

        /// <summary>Every step of <paramref name="flag"/> let go (its open one too), and the flag with them.</summary>
        public void DropFlag(int flag)
        {
            _undo.RemoveAll(s => s.Flag == flag);
            _redo.RemoveAll(s => s.Flag == flag);
            if (_open != null && _open.Flag == flag)
            {
                _open = null;
                _openHeld = false;
            }

            _propOf.Remove(flag);
        }

        /// <summary>Nothing to undo or redo (the city unloads).</summary>
        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            _propOf.Clear();
            _open = null;
            _openHeld = false;
        }

        private void Close()
        {
            Step s = _open;
            _open = null;
            _openHeld = false;
            if (s == null)
                return;

            if (FlagState.Same(s.Before, s.After))
                Prune();
            else
                Push(s);
        }

        private void Push(Step s)
        {
            _undo.Add(s);
            _redo.Clear();
            while (_undo.Count > MaxSteps)
                _undo.RemoveAt(0);
            Prune();
        }

        private int Bind(uint prop)
        {
            int flag = _nextFlag++;
            _propOf[flag] = prop;
            return flag;
        }

        /// <summary>Flags no step names any more are forgotten (the map stays as small as the steps).</summary>
        private void Prune()
        {
            if (_propOf.Count == 0)
                return;

            var named = new HashSet<int>();
            foreach (Step s in _undo)
                named.Add(s.Flag);
            foreach (Step s in _redo)
                named.Add(s.Flag);
            if (_open != null)
                named.Add(_open.Flag);

            var gone = new List<int>();
            foreach (int flag in _propOf.Keys)
            {
                if (!named.Contains(flag))
                    gone.Add(flag);
            }

            foreach (int flag in gone)
                _propOf.Remove(flag);
        }
    }
}
