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
    /// deleted flag's design is not carried from save to save.
    /// </remarks>
    public sealed class FlagSaveData : SerializableDataExtensionBase
    {
        private const string Key = "RealFlag.Flags";

        public override void OnLoadData()
        {
            base.OnLoadData();

            try
            {
                byte[] data = serializableDataManager.LoadData(Key);
                string problem;
                List<KeyValuePair<uint, FlagSpec>> flags = FlagCodec.DecodeAll(data, out problem);

                if (flags == null)
                {
                    FlagRegistry.Clear();
                    Log.Warn("flags: the city's saved flag designs could not be read (" + problem + "); flags keep the new flag's design");
                    return;
                }

                FlagRegistry.Load(flags);
                if (data != null)
                    Log.Msg("flags: " + flags.Count + " flag designs loaded with the city");
            }
            catch (Exception e)
            {
                FlagRegistry.Clear();
                Log.Error("flags: loading the flag designs threw; flags keep the new flag's design.", e);
            }
        }

        public override void OnSaveData()
        {
            base.OnSaveData();

            try
            {
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
