using System;
using System.Collections.Generic;
using ColossalFramework;
using ICities;

namespace RealFlag.Flags
{
    /// <summary>
    /// Keeps the flags' designs with the city: the game's own save data for mods
    /// (ICities.ISerializableData, found by the game like the Loader), one key. The props that
    /// carry them are saved by the game with the rest of the city's props.
    /// </summary>
    /// <remarks>
    /// OnLoadData runs after the simulation data is read, before OnLevelLoaded: the props are
    /// there and bound (to the flag prefab, which FlagLoadingHook made in time). OnSaveData runs
    /// on the simulation thread: only designs whose prop is still a flag are written, so a
    /// deleted flag's design is not carried from save to save -- unless the city's data is KEPT
    /// (<see cref="Keep"/>: unreadable, or its flags out of this mod's sight; the Loader then
    /// stands down): then it is written back exactly as it was loaded.
    /// </remarks>
    public sealed class FlagSaveData : SerializableDataExtensionBase
    {
        private const string Key = "RealFlag.Flags";

        /// <summary>The city's design data as it was loaded (null: it had none).</summary>
        private static volatile byte[] _loaded;

        /// <summary>
        /// The city's design data, as loaded, when this copy of the mod cannot use it: every save of
        /// the city writes it back unchanged, and the mod stands down in the city (Loader.CannotKeep).
        /// Designs it cannot read -- a newer version's format -- or flags it cannot see are never its
        /// to throw away. Null in every other city.
        /// </summary>
        private static volatile byte[] _kept;

        /// <summary>Why the loaded data is kept as it was, for the log.</summary>
        public static string KeptBecause { get; private set; }

        public static bool Keeping
        {
            get { return _kept != null; }
        }

        /// <summary>From now on the city's saves carry its design data exactly as it was loaded (nothing, if it had none).</summary>
        public static void Keep(string because)
        {
            if (_loaded == null)
                return;

            _kept = _loaded;
            KeptBecause = because;
            Log.Warn("flags: this city's saved flag designs are kept exactly as they were loaded, at every save (" + because + ")");
        }

        /// <summary>The city unloaded (Loader).</summary>
        public static void Forget()
        {
            _loaded = null;
            _kept = null;
            KeptBecause = null;
        }

        public override void OnLoadData()
        {
            base.OnLoadData();
            Forget();

            try
            {
                byte[] data = serializableDataManager.LoadData(Key);
                _loaded = data != null && data.Length > 0 ? data : null;
                string problem;
                List<KeyValuePair<uint, FlagSpec>> flags = FlagCodec.DecodeAll(data, out problem);

                if (flags == null)
                {
                    FlagRegistry.Clear();
                    Keep("they could not be read: " + problem);
                    return;
                }

                FlagRegistry.Load(flags);
                if (data != null)
                    Log.Msg("flags: " + flags.Count + " flag designs loaded with the city");
            }
            catch (Exception e)
            {
                FlagRegistry.Clear();
                Log.Error("flags: loading the flag designs threw.", e);
                Keep("loading them threw: " + e.Message);
            }
        }

        public override void OnSaveData()
        {
            base.OnSaveData();

            try
            {
                byte[] kept = _kept;
                if (kept != null)
                {
                    serializableDataManager.SaveData(Key, kept);
                    Log.Msg("flags: the city's flag designs saved back exactly as they were loaded (" + KeptBecause + ")");
                    return;
                }

                var all = new List<KeyValuePair<uint, FlagSpec>>();
                FlagRegistry.CopyTo(all);

                var alive = new List<KeyValuePair<uint, FlagSpec>>(all.Count);
                foreach (KeyValuePair<uint, FlagSpec> f in all)
                {
                    if (FlagProps.IsFlagProp(f.Key))
                        alive.Add(f);
                }

                // None: no key at all, so a city that never had a flag carries nothing of ours.
                if (alive.Count == 0)
                {
                    foreach (string key in serializableDataManager.EnumerateData())
                    {
                        if (key == Key)
                        {
                            serializableDataManager.EraseData(Key);
                            break;
                        }
                    }

                    return;
                }

                serializableDataManager.SaveData(Key, FlagCodec.EncodeAll(alive));
                Log.Msg("flags: " + alive.Count + " flag designs saved with the city" +
                        (alive.Count < all.Count ? " (" + (all.Count - alive.Count) + " of deleted flags dropped)" : ""));
            }
            catch (Exception e)
            {
                Log.Error("flags: saving the flag designs threw; the save goes on without them.", e);
            }
        }
    }
}
