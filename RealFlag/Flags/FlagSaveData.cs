using System;
using System.Collections.Generic;
using ColossalFramework;
using ICities;
using UnityEngine;

namespace RealFlag.Flags
{
    /// <summary>
    /// Keeps the flags with the city: the game's own save data for mods (ICities.ISerializableData,
    /// found by the game like the Loader), two keys -- the designs, and the flags' heights. The props
    /// that carry them are saved by the game with the rest of the city's props, but WITHOUT their
    /// height (FlagHeightCodec): the heights are ours to keep, or every flag loads under the map.
    /// </summary>
    /// <remarks>
    /// OnLoadData runs after the simulation data is read, before OnLevelLoaded: the props are
    /// there and bound (to the flag prefab, which FlagLoadingHook made in time), each at height 0;
    /// their heights are put back here, before anything reads where a flag is. OnSaveData runs
    /// on the simulation thread: only flags whose prop is still a flag are written, so a
    /// deleted flag's design is not carried from save to save -- unless the city's data is KEPT
    /// (<see cref="Keep"/>: unreadable, or its flags out of this mod's sight; the Loader then
    /// stands down): then both keys are written back exactly as they were loaded.
    /// </remarks>
    public sealed class FlagSaveData : SerializableDataExtensionBase
    {
        private const string Key = "RealFlag.Flags";

        /// <summary>The flags' heights (FlagHeightCodec), 2026-11-01 on. A city saved before has none (<see cref="StandUnplaced"/>).</summary>
        private const string HeightsKey = "RealFlag.Heights";

        /// <summary>The city's design data as it was loaded (null: it had none).</summary>
        private static volatile byte[] _loaded;

        /// <summary>The city's height data as it was loaded (null: it had none).</summary>
        private static volatile byte[] _loadedHeights;

        /// <summary>
        /// The city's design data, as loaded, when this copy of the mod cannot use it: every save of
        /// the city writes it back unchanged, and the mod stands down in the city (Loader.CannotKeep).
        /// Designs it cannot read -- a newer version's format -- or flags it cannot see are never its
        /// to throw away. Null in every other city.
        /// </summary>
        private static volatile byte[] _kept;

        /// <summary>The heights, as loaded, kept with <see cref="_kept"/> (null: the city had none).</summary>
        private static volatile byte[] _keptHeights;

        /// <summary>The flags whose height came with the city (the rest may need <see cref="StandUnplaced"/>).</summary>
        private static readonly HashSet<uint> Restored = new HashSet<uint>();

        /// <summary>Why the loaded data is kept as it was, for the log.</summary>
        public static string KeptBecause { get; private set; }

        public static bool Keeping
        {
            get { return _kept != null; }
        }

        /// <summary>From now on the city's saves carry its design and height data exactly as they were loaded (nothing, if it had none).</summary>
        public static void Keep(string because)
        {
            if (_loaded == null)
                return;

            _kept = _loaded;
            _keptHeights = _loadedHeights;
            KeptBecause = because;
            Log.Warn("flags: this city's saved flag designs are kept exactly as they were loaded, at every save (" + because + ")");
        }

        /// <summary>The city unloaded (Loader).</summary>
        public static void Forget()
        {
            _loaded = null;
            _loadedHeights = null;
            _kept = null;
            _keptHeights = null;
            KeptBecause = null;
            Restored.Clear();
        }

        public override void OnLoadData()
        {
            base.OnLoadData();
            Forget();

            try
            {
                byte[] heights = serializableDataManager.LoadData(HeightsKey);
                _loadedHeights = heights != null && heights.Length > 0 ? heights : null;

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

                RestoreHeights();
            }
            catch (Exception e)
            {
                FlagRegistry.Clear();
                Log.Error("flags: loading the flag designs threw.", e);
                Keep("loading them threw: " + e.Message);
            }
        }

        /// <summary>Each flag's saved height back on its prop (the game loaded it at 0).</summary>
        private static void RestoreHeights()
        {
            byte[] data = _loadedHeights;
            if (data == null)
                return;

            // Extended Managers Library keeps the props in a buffer of its own: the Loader stands
            // down and keeps both keys; nothing of the game's buffer is ours to change.
            if (Loader.EmlEnabled())
                return;

            string problem;
            Dictionary<uint, ushort> heights = FlagHeightCodec.Decode(data, out problem);
            if (heights == null)
            {
                Keep("their heights could not be read: " + problem);
                return;
            }

            int set = 0, notFlags = 0;
            foreach (KeyValuePair<uint, ushort> h in heights)
            {
                if (FlagProps.SetLoadedHeight(h.Key, h.Value))
                {
                    Restored.Add(h.Key);
                    set++;
                }
                else
                {
                    notFlags++;
                }
            }

            Log.Msg("flags: " + set + " flag height(s) loaded with the city" +
                    (notFlags > 0 ? " (" + notFlags + " for props that are no longer flags, left out)" : ""));
        }

        /// <summary>
        /// The flags that came without a height -- a city saved before the heights were kept, at 0
        /// under the map -- stood on the ground under them, as the mod's tool stands a new flag
        /// (FlagStand: a standing pole's foot on it). Loader.OnLevelLoaded, when the mod runs in the
        /// city (the terrain is there by then). A flag on a roof lands on the ground below it.
        /// </summary>
        public static void StandUnplaced()
        {
            var designs = new List<KeyValuePair<uint, FlagSpec>>();
            FlagRegistry.CopyTo(designs);

            int stood = 0;
            foreach (KeyValuePair<uint, FlagSpec> d in designs)
            {
                ushort posY;
                if (Restored.Contains(d.Key) || !FlagProps.TryGetHeight(d.Key, out posY) || posY != 0)
                    continue;

                Vector3 anchor;
                float angle;
                bool fixedHeight;
                if (!FlagProps.TryRead(d.Key, out anchor, out angle, out fixedHeight))
                    continue;

                FlagSpec spec = d.Value;
                float pole = spec.PoleLength > 0f ? spec.PoleLength : Settings.NewHeight != null ? Settings.NewHeight.value : Settings.Defaults.Height;
                float x, rise, z;
                FlagStand.Anchor(spec, angle, pole, 0f, 0f, 0f, out x, out rise, out z);
                if (FlagProps.SetLoadedHeight(d.Key, FlagProps.PosY(FlagProps.GroundAt(anchor) + rise)))
                    stood++;
            }

            if (stood > 0)
                Log.Warn("flags: " + stood + " flag(s) came without their height (the city was saved before Flag Maker kept the flags' heights;" +
                         "the game saves none): stood on the ground under them -- a flag that stood on a roof needs raising again (Page Up)");
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
                    byte[] keptHeights = _keptHeights;
                    if (keptHeights != null)
                        serializableDataManager.SaveData(HeightsKey, keptHeights);
                    else
                        Erase(HeightsKey);
                    Log.Msg("flags: the city's flag designs saved back exactly as they were loaded (" + KeptBecause + ")");
                    return;
                }

                var all = new List<KeyValuePair<uint, FlagSpec>>();
                FlagRegistry.CopyTo(all);

                var alive = new List<KeyValuePair<uint, FlagSpec>>(all.Count);
                var heights = new List<KeyValuePair<uint, ushort>>(all.Count);
                foreach (KeyValuePair<uint, FlagSpec> f in all)
                {
                    if (!FlagProps.IsFlagProp(f.Key))
                        continue;

                    alive.Add(f);
                    ushort posY;
                    if (FlagProps.TryGetHeight(f.Key, out posY))
                        heights.Add(new KeyValuePair<uint, ushort>(f.Key, posY));
                }

                // None: no key at all, so a city that never had a flag carries nothing of ours.
                if (alive.Count == 0)
                {
                    Erase(Key);
                    Erase(HeightsKey);
                    return;
                }

                serializableDataManager.SaveData(Key, FlagCodec.EncodeAll(alive));
                if (heights.Count > 0)
                    serializableDataManager.SaveData(HeightsKey, FlagHeightCodec.Encode(heights));
                else
                    Erase(HeightsKey);

                Log.Msg("flags: " + alive.Count + " flag designs and " + heights.Count + " height(s) saved with the city" +
                        (alive.Count < all.Count ? " (" + (all.Count - alive.Count) + " of deleted flags dropped)" : ""));
            }
            catch (Exception e)
            {
                Log.Error("flags: saving the flag designs threw; the save goes on without them.", e);
            }
        }

        private void Erase(string key)
        {
            foreach (string k in serializableDataManager.EnumerateData())
            {
                if (k == key)
                {
                    serializableDataManager.EraseData(key);
                    return;
                }
            }
        }
    }
}
