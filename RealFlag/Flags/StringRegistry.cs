using System.Collections.Generic;

namespace RealFlag.Flags
{
    /// <summary>
    /// Every span's design, by its segment. Locked: Move It pastes, saves and the placing tool's
    /// spans (made on the simulation thread) reach it off the main thread. Version moves on every
    /// change, for the renderer and the list.
    /// </summary>
    public static class StringRegistry
    {
        private static readonly object Lock = new object();
        private static readonly Dictionary<ushort, StringSpec> Specs = new Dictionary<ushort, StringSpec>();

        /// <summary>The spans Set since the renderer last asked (TakeChanged), or every span (a Load or Clear).</summary>
        private static readonly List<ushort> Changed = new List<ushort>();
        private static bool _changedAll = true;

        public static int Version { get; private set; }

        public static int Count
        {
            get
            {
                lock (Lock)
                    return Specs.Count;
            }
        }

        public static bool TryGet(ushort segment, out StringSpec spec)
        {
            lock (Lock)
                return Specs.TryGetValue(segment, out spec);
        }

        public static bool Has(ushort segment)
        {
            lock (Lock)
                return Specs.ContainsKey(segment);
        }

        public static void Set(ushort segment, StringSpec spec)
        {
            spec = spec.Sanitised();
            lock (Lock)
            {
                Specs[segment] = spec;
                Changed.Add(segment);
                Version++;
            }
        }

        /// <summary>
        /// The spans Set since the last call, for the renderer; true when every span may have
        /// changed (a Load or Clear). Comparing every span's design on every change (two encodes
        /// each) cost a frame of a slider drag on a long chain with the spans' count squared.
        /// </summary>
        public static bool TakeChanged(List<ushort> into)
        {
            into.Clear();
            lock (Lock)
            {
                bool all = _changedAll;
                _changedAll = false;
                if (!all)
                    into.AddRange(Changed);
                Changed.Clear();
                return all;
            }
        }

        public static bool Remove(ushort segment)
        {
            lock (Lock)
            {
                if (!Specs.Remove(segment))
                    return false;

                Version++;
                return true;
            }
        }

        public static void Clear()
        {
            lock (Lock)
            {
                Specs.Clear();
                Changed.Clear();
                _changedAll = true;
                Version++;
            }
        }

        public static void Load(IList<KeyValuePair<ushort, StringSpec>> spans)
        {
            lock (Lock)
            {
                Specs.Clear();
                foreach (KeyValuePair<ushort, StringSpec> s in spans)
                    Specs[s.Key] = s.Value.Sanitised();
                Changed.Clear();
                _changedAll = true;
                Version++;
            }
        }

        public static void CopyTo(List<KeyValuePair<ushort, StringSpec>> into)
        {
            into.Clear();
            lock (Lock)
            {
                foreach (KeyValuePair<ushort, StringSpec> s in Specs)
                    into.Add(s);
            }
        }

        public static void KeysTo(List<ushort> into)
        {
            into.Clear();
            lock (Lock)
            {
                foreach (ushort k in Specs.Keys)
                    into.Add(k);
            }
        }
    }
}
