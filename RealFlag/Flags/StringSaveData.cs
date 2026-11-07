using System;
using System.Collections.Generic;
using ICities;

namespace RealFlag.Flags
{
    /// <summary>
    /// The city's strings' designs, kept with the save under "RealFlag.Strings" (PERMANENT): each
    /// span's design by its segment (StringCodec). The spans themselves -- their nodes, where they
    /// are, how high -- are the game's network, saved by the game. Only spans that are still the
    /// string network are written; none, and the key goes. Invariant 18: what this copy cannot
    /// read is KEPT and written back byte for byte at every save, and no span of that city is
    /// given a new design over it.
    /// </summary>
    public sealed class StringSaveData : SerializableDataExtensionBase
    {
        public const string Key = "RealFlag.Strings";

        private static volatile byte[] _loaded;
        private static volatile byte[] _kept;

        public static string KeptBecause { get; private set; }

        public static bool Keeping
        {
            get { return _kept != null; }
        }

        public static void Keep(string because)
        {
            if (_loaded == null)
                return;

            _kept = _loaded;
            KeptBecause = because;
            Log.Warn("strings: this city's saved string designs are kept exactly as they were loaded, at every save (" + because + ")");
        }

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
                List<KeyValuePair<ushort, StringSpec>> spans = StringCodec.DecodeAll(data, out problem);
                if (spans == null)
                {
                    StringRegistry.Clear();
                    Keep("they could not be read: " + problem);
                    return;
                }

                StringRegistry.Load(spans);
                if (data != null)
                    Log.Msg("strings: " + spans.Count + " span design(s) loaded with the city");
            }
            catch (Exception e)
            {
                StringRegistry.Clear();
                Log.Error("strings: loading the string designs threw.", e);
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
                    Log.Msg("strings: the city's string designs saved back exactly as they were loaded (" + KeptBecause + ")");
                    return;
                }

                var all = new List<KeyValuePair<ushort, StringSpec>>();
                StringRegistry.CopyTo(all);
                var alive = new List<KeyValuePair<ushort, StringSpec>>(all.Count);
                foreach (KeyValuePair<ushort, StringSpec> s in all)
                {
                    if (StringNet.IsSpan(s.Key))
                        alive.Add(s);
                }

                if (alive.Count == 0)
                {
                    Erase();
                    return;
                }

                serializableDataManager.SaveData(Key, StringCodec.EncodeAll(alive));
                Log.Msg("strings: " + alive.Count + " span design(s) saved with the city" +
                        (alive.Count < all.Count ? " (" + (all.Count - alive.Count) + " of removed spans dropped)" : ""));
            }
            catch (Exception e)
            {
                Log.Error("strings: saving the string designs threw; the save goes on without them.", e);
            }
        }

        private void Erase()
        {
            foreach (string k in serializableDataManager.EnumerateData())
            {
                if (k == Key)
                {
                    serializableDataManager.EraseData(Key);
                    return;
                }
            }
        }
    }
}
