using System.Collections.Generic;

namespace RealFlag
{
    /// <summary>
    /// One saved setting: a value held in memory and written to FlagMaker.xml by
    /// <see cref="SettingsXml"/>. The lower-case <c>value</c> and <c>name</c> are those of the
    /// game's SavedFloat / SavedBool / SavedInt, which these replace.
    /// </summary>
    /// <remarks>
    /// Like a Saved*, a setting does not remember its default: the constructor's value is only
    /// what it holds until the file is read. <see cref="Settings.Defaults"/> and the catalog
    /// are what can put a default back.
    ///
    /// Written on the main thread only -- by the UIs, a reset, or a reload of the file.
    /// </remarks>
    public abstract class Setting
    {
        private static readonly List<Setting> Registry = new List<Setting>();

        protected Setting(string name, bool transient = false)
        {
            this.name = name;
            Transient = transient;
            Registry.Add(this);
        }

        /// <summary>The element name in FlagMaker.xml. Permanent once players have the file.</summary>
        public string name { get; private set; }

        /// <summary>
        /// A panel control's value that is not a setting at all -- the selected flag's length,
        /// say: never written to the file, never read from it, never reset.
        /// </summary>
        public bool Transient { get; private set; }

        /// <summary>Every setting ever constructed, so the file can say which one has no catalog row.</summary>
        public static IList<Setting> All
        {
            get { return Registry; }
        }

        protected void Changed()
        {
            if (!Transient)
                SettingsXml.MarkDirty();
        }
    }

    public sealed class FloatSetting : Setting
    {
        private float _value;

        public FloatSetting(string name, float value, bool transient = false)
            : base(name, transient)
        {
            _value = value;
        }

        public float value
        {
            get { return _value; }
            set
            {
                if (_value.Equals(value))
                    return;

                _value = value;
                Changed();
            }
        }
    }

    public sealed class BoolSetting : Setting
    {
        private bool _value;

        public BoolSetting(string name, bool value, bool transient = false)
            : base(name, transient)
        {
            _value = value;
        }

        public bool value
        {
            get { return _value; }
            set
            {
                if (_value == value)
                    return;

                _value = value;
                Changed();
            }
        }
    }

    public sealed class IntSetting : Setting
    {
        private int _value;

        public IntSetting(string name, int value, bool transient = false)
            : base(name, transient)
        {
            _value = value;
        }

        public int value
        {
            get { return _value; }
            set
            {
                if (_value == value)
                    return;

                _value = value;
                Changed();
            }
        }
    }
}
