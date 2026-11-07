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
                Version++;
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
