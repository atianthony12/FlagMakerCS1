using System.Collections.Generic;

namespace RealFlag.Flags
{
    /// <summary>
    /// Every flag's design in this city, by the index of the prop that carries it. The props
    /// themselves are the game's (Move It moves them, the bulldozer deletes them); this is what
    /// the mod adds to each. Kept with the city by FlagSaveData, copied along by Move It
    /// (FlagMoveIt), drawn by FlagRenderer.
    /// </summary>
    /// <remarks>
    /// Locked: the game saves on its simulation thread, Move It pastes on its own, the panel
    /// and the renderer work on the main one. Cleared when the city unloads (Loader), so nothing
    /// of one city reaches the next. An entry whose prop is gone -- or is no longer a flag --
    /// is dropped by the renderer's scan and never saved.
    /// </remarks>
    public static class FlagRegistry
    {
        private static readonly object Lock = new object();
        private static readonly Dictionary<uint, FlagSpec> Specs = new Dictionary<uint, FlagSpec>();

        /// <summary>Changes on every change: the renderer and the panel refresh what they show when it moves.</summary>
        public static int Version { get; private set; }

        public static int Count
        {
            get
            {
                lock (Lock)
                    return Specs.Count;
            }
        }

        public static bool TryGet(uint prop, out FlagSpec spec)
        {
            lock (Lock)
                return Specs.TryGetValue(prop, out spec);
        }

        public static void Set(uint prop, FlagSpec spec)
        {
            spec = spec.Sanitised();
            lock (Lock)
            {
                Specs[prop] = spec;
                Version++;
            }
        }

        public static bool Remove(uint prop)
        {
            lock (Lock)
            {
                if (!Specs.Remove(prop))
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

        /// <summary>Replaces everything with a city's saved flags (FlagSaveData).</summary>
        public static void Load(IList<KeyValuePair<uint, FlagSpec>> flags)
        {
            lock (Lock)
            {
                Specs.Clear();
                foreach (KeyValuePair<uint, FlagSpec> f in flags)
                    Specs[f.Key] = f.Value.Sanitised();
                Version++;
            }
        }

        /// <summary>A snapshot, in prop order.</summary>
        public static void CopyTo(List<KeyValuePair<uint, FlagSpec>> into)
        {
            into.Clear();
            lock (Lock)
            {
                foreach (KeyValuePair<uint, FlagSpec> f in Specs)
                    into.Add(f);
            }

            into.Sort((a, b) => a.Key.CompareTo(b.Key));
        }
    }
}
