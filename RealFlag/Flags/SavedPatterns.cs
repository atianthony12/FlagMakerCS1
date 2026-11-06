using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace RealFlag.Flags
{
    /// <summary>A pattern and its three colours under a name of the player's. Nothing else of a flag: the author's "just the pattern and colors themselves".</summary>
    public sealed class SavedPattern
    {
        public string Name;
        public int Pattern;
        public uint Colour1, Colour2, Colour3;

        /// <summary>It looks as these do; colours a pattern does not use do not count.</summary>
        public bool SameLook(int pattern, uint colour1, uint colour2, uint colour3)
        {
            if (Pattern != pattern || Colour1 != colour1)
                return false;

            int used = FlagPattern.ColoursUsed(pattern);
            return (used < 2 || Colour2 == colour2) && (used < 3 || Colour3 == colour3);
        }
    }

    /// <summary>The text of Patterns.xml. PURE: written and read here, the file itself in <see cref="SavedPatterns"/>.</summary>
    public static class SavedPatternFile
    {
        public const int MaxName = 40;

        private const string Root = "Patterns";
        private const string Entry = "Pattern";

        /// <summary>A name as it is kept: no control characters, no spaces round it, <see cref="MaxName"/> characters at most.</summary>
        public static string Sanitise(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            var kept = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (!char.IsControl(c))
                    kept.Append(c);
            }

            string text = kept.ToString().Trim();
            if (text.Length > MaxName)
                text = text.Substring(0, MaxName).TrimEnd();

            return text;
        }

        private static string Escape(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
        }

        public static string Write(IEnumerable<SavedPattern> patterns)
        {
            var text = new StringBuilder();
            text.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
            text.Append("<!-- Flag Maker's saved patterns: a name, a pattern's number and its three colors. -->\n");
            text.Append('<').Append(Root).Append(">\n");
            foreach (SavedPattern p in patterns)
            {
                text.Append("  <").Append(Entry)
                    .Append(" name=\"").Append(Escape(p.Name)).Append('"')
                    .Append(" pattern=\"").Append(p.Pattern.ToString(CultureInfo.InvariantCulture)).Append('"')
                    .Append(" colour1=\"").Append(FlagColours.Hex(p.Colour1)).Append('"')
                    .Append(" colour2=\"").Append(FlagColours.Hex(p.Colour2)).Append('"')
                    .Append(" colour3=\"").Append(FlagColours.Hex(p.Colour3)).Append("\" />\n");
            }

            text.Append("</").Append(Root).Append(">\n");
            return text.ToString();
        }

        /// <summary>
        /// The patterns in a file's text; null if it is not such a file at all. An entry that cannot
        /// be used -- no name, a name already read, a pattern or a colour this copy does not know
        /// -- is left out and counted: the caller keeps the file's text rather than lose it.
        /// </summary>
        public static List<SavedPattern> Read(string text, out int skipped)
        {
            skipped = 0;
            var document = new XmlDocument { XmlResolver = null };
            try
            {
                document.LoadXml(text);
            }
            catch (XmlException)
            {
                return null;
            }

            XmlElement root = document.DocumentElement;
            if (root == null || root.Name != Root)
                return null;

            var patterns = new List<SavedPattern>();
            var names = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (XmlNode node in root.ChildNodes)
            {
                var element = node as XmlElement;
                if (element == null)
                    continue;

                SavedPattern pattern = element.Name == Entry ? ReadEntry(element) : null;
                if (pattern == null || names.ContainsKey(pattern.Name))
                {
                    skipped++;
                    continue;
                }

                names[pattern.Name] = true;
                patterns.Add(pattern);
            }

            return patterns;
        }

        private static SavedPattern ReadEntry(XmlElement element)
        {
            string name = Sanitise(element.GetAttribute("name"));
            int number;
            uint c1, c2, c3;
            if (name.Length == 0 ||
                !int.TryParse(element.GetAttribute("pattern"), NumberStyles.Integer, CultureInfo.InvariantCulture, out number) ||
                !FlagPattern.IsKnown(number) ||
                !FlagColours.TryParseHex(element.GetAttribute("colour1"), out c1) ||
                !FlagColours.TryParseHex(element.GetAttribute("colour2"), out c2) ||
                !FlagColours.TryParseHex(element.GetAttribute("colour3"), out c3))
                return null;

            return new SavedPattern { Name = name, Pattern = number, Colour1 = c1, Colour2 = c2, Colour3 = c3 };
        }
    }

    /// <summary>
    /// The player's saved patterns, FlagMaker\Patterns.xml beside the images folder: for every
    /// city, kept when the mod is not. A flag COPIES a saved pattern (FlagSpec holds the pattern and
    /// the colours), so saving, changing or deleting one never touches a flag, and nothing of this
    /// is in a save. Main thread.
    /// </summary>
    /// <remarks>
    /// Read again when the file changes, so a hand edit shows while the window is open. What
    /// this copy cannot read is not its to throw away: a file it could not read at all, or
    /// entries it left out, are copied to Patterns.bad.xml before the next save writes over them
    /// (and nothing is written if that fails).
    /// </remarks>
    public static class SavedPatterns
    {
        public const string FileName = "Patterns.xml";
        private const string BadName = "Patterns.bad";

        private static readonly List<SavedPattern> List = new List<SavedPattern>();

        private static DateTime _seenTime;
        private static long _seenLength = -1;
        private static bool _read, _unreadParts, _warnedRead;

        /// <summary>Goes up whenever the list changes.</summary>
        public static int Version { get; private set; }

        /// <summary>The saved patterns, in the file's order, the newest last. Not to be changed.</summary>
        public static IList<SavedPattern> All
        {
            get { return List; }
        }

        /// <summary>FlagMaker\Patterns.xml.</summary>
        public static string FilePath
        {
            get { return Path.Combine(Path.GetDirectoryName(FlagImageFolder.FolderPath), FileName); }
        }

        public static SavedPattern Find(string name)
        {
            foreach (SavedPattern p in List)
            {
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                    return p;
            }

            return null;
        }

        /// <summary>The file again if it changed since it was last read (or written); cheap otherwise.</summary>
        public static void Refresh()
        {
            try
            {
                var file = new FileInfo(FilePath);
                if (!file.Exists)
                {
                    bool had = List.Count > 0;
                    List.Clear();
                    _read = true;
                    _unreadParts = false;
                    _seenLength = -1;
                    if (had)
                        Version++;
                    return;
                }

                if (_read && file.Length == _seenLength && file.LastWriteTimeUtc == _seenTime)
                    return;

                string text = File.ReadAllText(file.FullName);
                _read = true;
                _seenLength = file.Length;
                _seenTime = file.LastWriteTimeUtc;

                int skipped = 0;
                List<SavedPattern> patterns = text.Trim().Length == 0 ? new List<SavedPattern>() : SavedPatternFile.Read(text, out skipped);
                List.Clear();
                if (patterns == null)
                {
                    _unreadParts = true;
                    Log.Warn("patterns: " + FileName + " cannot be read; none shown. The next save keeps it as " + BadName + ".xml");
                }
                else
                {
                    List.AddRange(patterns);
                    _unreadParts = skipped > 0;
                    Log.Msg("patterns: " + List.Count + " saved pattern(s) read from " + FileName +
                            (skipped > 0 ? "; " + skipped + " left out (the next save keeps the file as " + BadName + ".xml)" : ""));
                }

                Version++;
                _warnedRead = false;
            }
            catch (Exception e)
            {
                // Not read (open in an editor, say): as it was, and tried again at the next look.
                if (!_warnedRead)
                    Log.Warn("patterns: could not read " + FileName + ": " + e.Message);
                _warnedRead = true;
            }
        }

        /// <summary>Keeps a pattern under its name, in the place of one by that name. False if the file could not be written: nothing changed.</summary>
        public static bool Store(SavedPattern pattern)
        {
            var list = new List<SavedPattern>(List);
            int at = list.FindIndex(p => string.Equals(p.Name, pattern.Name, StringComparison.OrdinalIgnoreCase));
            if (at >= 0)
                list[at] = pattern;
            else
                list.Add(pattern);

            if (!Commit(list))
                return false;

            Log.Msg("patterns: '" + pattern.Name + "' " + (at >= 0 ? "replaced" : "saved") + " (" + FlagPattern.LogName(pattern.Pattern) + ", " +
                    FlagColours.Hex(pattern.Colour1) + " " + FlagColours.Hex(pattern.Colour2) + " " + FlagColours.Hex(pattern.Colour3) + ")");
            return true;
        }

        public static bool Delete(string name)
        {
            var list = new List<SavedPattern>(List);
            int removed = list.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
            if (removed == 0 || !Commit(list))
                return false;

            Log.Msg("patterns: '" + name + "' deleted");
            return true;
        }

        private static bool Commit(List<SavedPattern> list)
        {
            string path = FilePath, temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (_unreadParts && File.Exists(path))
                {
                    string kept = KeepPath(path);
                    File.Copy(path, kept, false);
                    Log.Warn("patterns: what could not be read of " + FileName + " is kept in " + Path.GetFileName(kept));
                }

                File.WriteAllText(temp, SavedPatternFile.Write(list), new UTF8Encoding(false));
                if (!File.Exists(path))
                {
                    File.Move(temp, path);
                }
                else
                {
                    // As SettingsXml: replaced whole; where File.Replace does not work, copied over.
                    try
                    {
                        File.Replace(temp, path, null);
                    }
                    catch (Exception e) when (e is IOException || e is PlatformNotSupportedException || e is UnauthorizedAccessException)
                    {
                        File.Copy(temp, path, true);
                        File.Delete(temp);
                    }
                }

                var file = new FileInfo(path);
                _seenLength = file.Length;
                _seenTime = file.LastWriteTimeUtc;
                _read = true;
                _unreadParts = false;
                List.Clear();
                List.AddRange(list);
                Version++;
                return true;
            }
            catch (Exception e)
            {
                Log.Error("patterns: could not write " + FileName, e);
                return false;
            }
        }

        /// <summary>Patterns.bad.xml, or the first number free after it: an older one is never written over.</summary>
        private static string KeepPath(string path)
        {
            string folder = Path.GetDirectoryName(path);
            string kept = Path.Combine(folder, BadName + ".xml");
            for (int i = 2; File.Exists(kept); i++)
                kept = Path.Combine(folder, BadName + i.ToString(CultureInfo.InvariantCulture) + ".xml");

            return kept;
        }
    }
}
