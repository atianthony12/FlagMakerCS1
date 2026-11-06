using System;
using RealFlag.Flags;

namespace RealFlag.UI
{
    /// <summary>
    /// The settings one window's pattern works through -- the New flag window's (the next flag's,
    /// kept in FlagMaker.xml) or the Flags tab's (the selected flag's, transient) -- and what the
    /// Pattern window does to them: choose a pattern, load a saved one.
    /// </summary>
    public sealed class PatternTarget
    {
        /// <summary>New or Existing: whose pattern it is (which window closes it).</summary>
        public PanelPage Page;

        public IntSetting Pattern, Colour1, Colour2, Colour3, Country, Proportion, HoistedFrom;

        /// <summary>What a change does besides the settings (the selected flag: FlagEditor.ApplyDesign); null for the new flag, which reads its settings.</summary>
        public Action Changed;

        public Func<bool> Enabled;

        /// <summary>A country's flag has its own pattern and colours, which are kept for a change back.</summary>
        public bool IsEnabled
        {
            get { return (Enabled == null || Enabled()) && (Country == null || Country.value == 0); }
        }

        /// <summary>The Pattern window's page of colour rows for this design.</summary>
        public PanelPage ColoursPage
        {
            get { return Page == PanelPage.New ? PanelPage.PatternNew : PanelPage.PatternExisting; }
        }

        public uint C1
        {
            get { return (uint)Colour1.value; }
        }

        public uint C2
        {
            get { return (uint)Colour2.value; }
        }

        public uint C3
        {
            get { return (uint)Colour3.value; }
        }

        /// <summary>The flag's shape as seen: its height over its width.</summary>
        public float Seen
        {
            get { return FlagCrop.Seen(Proportion.value, HoistedFrom.value); }
        }

        public bool HoistedFromTop
        {
            get { return HoistedFrom.value == FlagHoist.Top; }
        }

        public void Choose(int pattern)
        {
            if (Pattern.value == pattern || !IsEnabled)
                return;

            Pattern.value = pattern;
            Log.Msg("flags: " + Whose() + " pattern: " + FlagPattern.LogName(pattern));
            Apply();
        }

        /// <summary>A saved pattern's values, copied onto the flag: it keeps no tie to the saved one.</summary>
        public void Load(SavedPattern saved)
        {
            if (saved == null || !IsEnabled)
                return;

            Pattern.value = saved.Pattern;
            Colour1.value = (int)saved.Colour1;
            Colour2.value = (int)saved.Colour2;
            Colour3.value = (int)saved.Colour3;
            Log.Msg("flags: " + Whose() + " pattern: saved '" + saved.Name + "' (" + FlagPattern.LogName(saved.Pattern) + ")");
            Apply();
        }

        /// <summary>What the flag has now, under a name.</summary>
        public SavedPattern Saved(string name)
        {
            return new SavedPattern { Name = name, Pattern = Pattern.value, Colour1 = C1, Colour2 = C2, Colour3 = C3 };
        }

        public bool Shows(SavedPattern saved)
        {
            return saved.SameLook(Pattern.value, C1, C2, C3);
        }

        /// <summary>The change applied (the selected flag redesigned) and the panels following.</summary>
        private void Apply()
        {
            if (Changed != null)
                Changed();
            SettingsCatalog.RefreshAllUIs();
        }

        /// <summary>For the log: "the new flag's", "flag #12's".</summary>
        private string Whose()
        {
            return Page == PanelPage.New ? "the new flag's" : "flag #" + FlagEditor.Selected + "'s";
        }
    }
}
