using System;
using System.IO;
using ColossalFramework.IO;
using RealFlag.Flags;

namespace RealFlag
{
    /// <summary>
    /// The player's files under the mod's first name, moved to the new one once. Only copies from
    /// before the rename have them: RealFlag.xml becomes FlagMaker.xml, RealFlag\Images becomes
    /// FlagMaker\Images. Runs at the start of Settings.Init. Whatever is already under the new name
    /// wins and the old one is left where it is; nothing is ever overwritten or deleted but the old
    /// folder, when moving its Images has emptied it. The old logs stay: nothing reads them.
    /// </summary>
    internal static class OldNames
    {
        private const string OldSettings = "RealFlag.xml";
        private const string OldFolder = "RealFlag";

        public static void Move()
        {
            MoveSettings();
            MoveImages();
        }

        private static void MoveSettings()
        {
            try
            {
                string old = Path.Combine(DataLocation.localApplicationData, OldSettings);
                if (!File.Exists(old) || File.Exists(SettingsXml.Path))
                    return;

                File.Move(old, SettingsXml.Path);
                Log.Msg("settings: " + OldSettings + " moved to " + SettingsXml.FileName);
            }
            catch (Exception e)
            {
                Log.Warn("settings: could not move " + OldSettings + " to " + SettingsXml.FileName + ": " + e.Message);
            }
        }

        private static void MoveImages()
        {
            try
            {
                string folder = Path.Combine(DataLocation.localApplicationData, OldFolder);
                string old = Path.Combine(folder, "Images");
                string now = FlagImageFolder.FolderPath;
                if (!Directory.Exists(old) || Directory.Exists(now))
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(now));
                Directory.Move(old, now);
                if (Directory.GetFileSystemEntries(folder).Length == 0)
                    Directory.Delete(folder);

                Log.Msg("flags: your images folder moved to " + now);
            }
            catch (Exception e)
            {
                Log.Warn("flags: could not move your images folder to " + FlagImageFolder.FolderPath + ": " + e.Message);
            }
        }
    }
}
