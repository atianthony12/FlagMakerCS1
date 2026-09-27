using System;
using System.Collections.Generic;
using System.IO;
using ColossalFramework.IO;

namespace RealFlag.Flags
{
    /// <summary>
    /// The player's images folder, RealFlag\Images next to RealFlag.xml (in the game's own
    /// folder of local data: Procedural Objects keeps its textures the same way, in
    /// ProceduralObjects\Textures, and Image Overlay its pictures in a Files folder -- the author
    /// asked for their way of it): what is in it, each picture's size read from its header, its
    /// bytes for the cloth (FlagImages.UserSource), and the game's own file browser opened on it
    /// (ColossalFramework.Utils.OpenInFileBrowser, what the Content Manager's folder buttons use:
    /// explorer.exe on Windows, from its IL). Main thread.
    /// </summary>
    /// <remarks>
    /// PNG and JPEG, the two Texture2D.LoadImage reads. Looked at again every second while the
    /// Image window is open (<see cref="Scan"/>): a file dropped in shows in its list, and a file
    /// changed or gone is let go (FlagImages.ForgetUser) -- a picture edited and saved again shows
    /// on its flags as soon as the window is next open.
    /// </remarks>
    public static class FlagImageFolder
    {
        /// <summary>One picture in the folder.</summary>
        public sealed class Entry
        {
            public string Name;
            public long Length;
            public DateTime Written;

            /// <summary>From its header (ImageHeader): its size in pixels, and whether it can have see-through parts. Readable false: not a PNG or JPEG header.</summary>
            public int Width, Height;
            public bool Alpha;
            public bool Readable;
        }

        private static readonly List<Entry> List = new List<Entry>();
        private static readonly Dictionary<string, Entry> ByName = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        private static string _path;
        private static bool _scanned;
        private static string _lastProblem;

        /// <summary>Moves on whenever what is in the folder changes.</summary>
        public static int Version { get; private set; }

        /// <summary>%LOCALAPPDATA%\Colossal Order\Cities_Skylines\RealFlag\Images on Windows.</summary>
        public static string FolderPath
        {
            get
            {
                if (_path == null)
                    _path = Path.Combine(Path.Combine(DataLocation.localApplicationData, "RealFlag"), "Images");
                return _path;
            }
        }

        /// <summary>The pictures in it, by name (looked at once if it never was).</summary>
        public static IList<Entry> Entries
        {
            get
            {
                if (!_scanned)
                    Scan();
                return List;
            }
        }

        /// <summary>The picture of this name in the folder; null if it is not there.</summary>
        public static Entry Find(string name)
        {
            if (!_scanned)
                Scan();

            Entry e;
            return !string.IsNullOrEmpty(name) && ByName.TryGetValue(name, out e) ? e : null;
        }

        /// <summary>A picture's size in pixels, as its file has it (from its header); false if it is not known.</summary>
        public static bool SizeOf(string name, out int width, out int height)
        {
            Entry e = Find(name);
            if (e != null && e.Readable)
            {
                width = e.Width;
                height = e.Height;
                return true;
            }

            width = height = 0;
            return false;
        }

        /// <summary>Makes the folder if it is not there yet. False (and why, in the log) if it cannot be.</summary>
        public static bool Ensure()
        {
            try
            {
                if (!Directory.Exists(FolderPath))
                {
                    Directory.CreateDirectory(FolderPath);
                    Log.Msg("flags: your images folder made: " + FolderPath);
                }

                return true;
            }
            catch (Exception e)
            {
                Log.Warn("flags: could not make your images folder " + FolderPath + ": " + e.Message);
                return false;
            }
        }

        /// <summary>The folder in the game's file browser (Explorer on Windows), made first if it is not there.</summary>
        public static void Open()
        {
            if (!Ensure())
                return;

            try
            {
                ColossalFramework.Utils.OpenInFileBrowser(FolderPath);
                Log.Msg("flags: your images folder opened: " + FolderPath);
            }
            catch (Exception e)
            {
                Log.Warn("flags: could not open your images folder " + FolderPath + ": " + e.Message);
            }
        }

        /// <summary>A picture's bytes (FlagImages.UserSource); null if it is not there.</summary>
        public static byte[] Bytes(string name)
        {
            string clean = FlagImageNames.Sanitise(name);
            if (clean.Length == 0)
                return null;

            try
            {
                string file = Path.Combine(FolderPath, clean);
                return File.Exists(file) ? File.ReadAllBytes(file) : null;
            }
            catch (Exception e)
            {
                Log.Warn("flags: could not read your image '" + clean + "': " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Looks at the folder again: new pictures listed, changed ones read again and let go on
        /// the flags (made again when next drawn), gone ones let go. True if anything changed.
        /// </summary>
        public static bool Scan()
        {
            bool first = !_scanned;
            _scanned = true;

            var added = new List<string>();
            var changed = new List<string>();
            var removed = new List<string>();
            try
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (Directory.Exists(FolderPath))
                {
                    foreach (string file in Directory.GetFiles(FolderPath))
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                            continue;

                        string name = Path.GetFileName(file);
                        if (FlagImageNames.Sanitise(name) != name)
                            continue;

                        var info = new FileInfo(file);
                        seen.Add(name);
                        Entry e;
                        if (ByName.TryGetValue(name, out e))
                        {
                            if (e.Length == info.Length && e.Written == info.LastWriteTimeUtc)
                                continue;
                            changed.Add(name);
                        }
                        else
                        {
                            e = new Entry { Name = name };
                            List.Add(e);
                            ByName[name] = e;
                            added.Add(name);
                        }

                        e.Length = info.Length;
                        e.Written = info.LastWriteTimeUtc;
                        ReadHeader(e, file);

                        // Made again from the new file (or looked for again, if a flag wanted it before it came).
                        FlagImages.ForgetUser(name);
                    }
                }

                for (int i = List.Count - 1; i >= 0; i--)
                {
                    if (seen.Contains(List[i].Name))
                        continue;

                    removed.Add(List[i].Name);
                    FlagImages.ForgetUser(List[i].Name);
                    ByName.Remove(List[i].Name);
                    List.RemoveAt(i);
                }

                _lastProblem = null;
            }
            catch (Exception e)
            {
                if (e.Message != _lastProblem)
                {
                    _lastProblem = e.Message;
                    Log.Warn("flags: could not look in your images folder " + FolderPath + ": " + e.Message);
                }

                return false;
            }

            if (added.Count + changed.Count + removed.Count == 0)
            {
                if (first)
                    Log.Msg("flags: your images folder " + FolderPath + (Directory.Exists(FolderPath) ? ": no pictures yet" : ": not made yet"));
                return false;
            }

            List.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            Version++;

            if (first)
            {
                Log.Msg("flags: your images folder " + FolderPath + ": " + List.Count + " picture(s)" + Describe(List));
            }
            else
            {
                Log.Msg("flags: your images folder changed --" + (added.Count > 0 ? " new: " + Join(added) : "") +
                        (changed.Count > 0 ? " changed: " + Join(changed) : "") + (removed.Count > 0 ? " gone: " + Join(removed) : ""));
            }

            return true;
        }

        private static void ReadHeader(Entry e, string file)
        {
            try
            {
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    int w, h;
                    bool alpha;
                    e.Readable = ImageHeader.TryRead(stream, out w, out h, out alpha);
                    e.Width = w;
                    e.Height = h;
                    e.Alpha = alpha;
                }
            }
            catch (Exception)
            {
                // Still being written, or held: its size unknown until the next look.
                e.Readable = false;
                e.Length = -1;
            }
        }

        private static string Describe(List<Entry> entries)
        {
            var parts = new List<string>();
            foreach (Entry e in entries)
            {
                parts.Add(e.Name + (e.Readable ? " " + e.Width + "x" + e.Height + (e.Alpha ? " (alpha)" : "") : " (not a PNG or JPEG header)"));
                if (parts.Count == 12)
                {
                    parts.Add("...");
                    break;
                }
            }

            return parts.Count > 0 ? ": " + string.Join(", ", parts.ToArray()) : "";
        }

        private static string Join(List<string> names)
        {
            return names.Count <= 8 ? string.Join(", ", names.ToArray()) : string.Join(", ", names.GetRange(0, 8).ToArray()) + " and " + (names.Count - 8) + " more";
        }
    }
}
