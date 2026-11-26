using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using ColossalFramework.IO;

namespace RealFlag.Flags
{
    /// <summary>
    /// The loops' folder (2026-11-12): FlagMaker\Loops beside FlagMaker\Images. A loop made once
    /// (FlagMotion.Loop, LoopBake: 4-12 s of a cloth thread at a city's load) is kept there as a
    /// file (LoopFile) and read back in a later session instead of being made again. A cache, never
    /// part of a save: delete the folder and every loop is made again as it always was.
    /// </summary>
    /// <remarks>
    /// ANY DOUBT MEANS THE LOOP IS MADE AS TODAY. A file is taken only when LoopFile.Read finds it
    /// whole and the very loop asked for, made by this very build (the DLL's ModuleVersionId: new at
    /// every build); anything else is a miss, and that file is deleted. An error of the folder itself
    /// (it cannot be made, access denied, the disk full) is that loop's miss; the third of a session
    /// turns the cache OFF for the rest of it with one warning: every ask is a miss at once, nothing
    /// is written.
    ///
    /// All its file work runs on ONE thread of its own (below normal priority, started at the first
    /// ask): never on the main thread, and never in a cloth job (the main thread waits on those, and
    /// ClothWorker.Wait gives up after 5 s). Reads go before writes, writes before the tidying. That
    /// thread calls no Unity API and no Log method: it leaves its answers on each Request and its
    /// messages in a list the main thread says in the log (<see cref="Tell"/>, once a frame).
    ///
    /// The tidying, on that thread: once a session, part-written files left over (*.tmp) and files
    /// of another build (their first bytes read only) are deleted; after a write, while the folder
    /// is over <see cref="MaxFolderBytes"/>, the file read or written longest ago goes (a read
    /// touches its file's time). A file it cannot open or delete (read-only, held by another
    /// program) is skipped and left, never one of the folder's errors.
    /// </remarks>
    public sealed class LoopCache
    {
        /// <summary>The folder is kept under this many bytes (a High loop's file is 1-2.5 MB).</summary>
        public const long MaxFolderBytes = 200L * 1024 * 1024;

        /// <summary>Where a read stands: not answered yet, the loop read, or none to be had (a bake makes it).</summary>
        public enum Answer
        {
            Pending,
            Hit,
            Miss,
        }

        /// <summary>
        /// One read the main thread asked for, answered once by the folder's thread: its
        /// <see cref="Data"/> (and the milliseconds it took) are written before its state, which is
        /// volatile, so the main thread that sees Hit sees them too.
        /// </summary>
        public sealed class Request
        {
            public readonly string Identity;

            public Request(string identity)
            {
                Identity = identity;
            }

            internal volatile int State; // an Answer
            internal volatile bool Forgotten;

            /// <summary>
            /// The folder's thread has begun this read (set there, read on the main thread): reads
            /// are done one at a time, so one queued behind others waits its turn before any of
            /// the main thread's patience is its own (FlagRenderer.FromDisk).
            /// </summary>
            internal volatile bool Begun;

            /// <summary>The loop read, once Hit. Shared with whoever takes it, never changed (LoopData).</summary>
            public LoopData Data;

            /// <summary>The milliseconds the folder's thread took to read and check it.</summary>
            public double Ms;
        }

        // ---- the session's: kept from one city to the next -------------------------------------

        /// <summary>Off for the rest of the session (an error of the folder, or no build id): every ask a miss.</summary>
        private static volatile bool _off;

        /// <summary>The folder's errors this session, and how many turn it off (see Run).</summary>
        private static int _errors;
        private const int MaxErrors = 3;

        /// <summary>The once-a-session tidying done; where the folder is said in the log once.</summary>
        private static volatile bool _tidied, _folderSaid;

        // ---- this renderer's -------------------------------------------------------------------

        private readonly string _folder;
        private readonly Guid _stamp;
        private readonly object _lock = new object();
        private readonly Queue<Request> _reads = new Queue<Request>();
        private readonly Queue<KeyValuePair<string, LoopData>> _writes = new Queue<KeyValuePair<string, LoopData>>();
        private readonly List<KeyValuePair<int, string>> _messages = new List<KeyValuePair<int, string>>();
        private Thread _thread;
        private bool _stop, _pruneDue;

        private const int SayMsg = 0, SayDetail = 1, SayWarn = 2;

        private LoopCache(string folder, Guid stamp)
        {
            _folder = folder;
            _stamp = stamp;
        }

        /// <summary>The folder, as found once on the main thread.</summary>
        public string Folder
        {
            get { return _folder; }
        }

        /// <summary>
        /// The cache for a renderer, on the main thread: its folder and this build's stamp found
        /// here, once. Null when it is off for the session, or the stamp cannot be read (said once).
        /// </summary>
        public static LoopCache Open()
        {
            if (_off)
                return null;

            try
            {
                Guid stamp = typeof(Mod).Module.ModuleVersionId;
                if (stamp == Guid.Empty)
                    throw new InvalidOperationException("the mod's build id is empty");
                string folder = Path.Combine(Path.Combine(DataLocation.localApplicationData, "FlagMaker"), "Loops");
                return new LoopCache(folder, stamp);
            }
            catch (Exception e)
            {
                _off = true;
                Log.Warn("flags: loops are not kept on disk this session (" + e.GetType().Name + ": " + e.Message + "); each is made afresh");
                return null;
            }
        }

        /// <summary>A loop asked for by its identity (LoopFile.Identity), on the main thread: answered on the folder's thread (<see cref="Poll"/>).</summary>
        public Request Ask(string identity)
        {
            var r = new Request(identity);
            if (_off)
            {
                r.State = (int)Answer.Miss;
                return r;
            }

            // Queued before the thread is started: its first work is this read, not the tidying.
            lock (_lock)
            {
                _reads.Enqueue(r);
                Monitor.PulseAll(_lock);
            }

            if (!Started())
                r.State = (int)Answer.Miss;
            return r;
        }

        /// <summary>Where a read stands; a miss at once once the cache is off (nothing may be left waiting on a thread that no longer reads).</summary>
        public Answer Poll(Request r)
        {
            var state = (Answer)r.State;
            if (state == Answer.Pending && _off)
                return Answer.Miss;
            return state;
        }

        /// <summary>A read no one waits for any more (given up on, its entry let go): not done if not begun, its answer dropped.</summary>
        public void Forget(Request r)
        {
            if (r == null)
                return;
            r.Forgotten = true;
            r.Data = null;
        }

        /// <summary>
        /// A loop just made, kept for later sessions: written on the folder's thread (a part-written
        /// file first, then put in its place). Its Data is only read there -- shared with the
        /// renderer's loop, which is safe only because nobody ever writes to it (LoopData).
        /// </summary>
        public void Store(string identity, LoopData loop)
        {
            if (_off || loop == null)
                return;

            lock (_lock)
            {
                _writes.Enqueue(new KeyValuePair<string, LoopData>(identity, loop));
                Monitor.PulseAll(_lock);
            }

            Started();
        }

        /// <summary>What the folder's thread had to say, into the log: on the main thread, once a frame.</summary>
        public void Tell()
        {
            KeyValuePair<int, string>[] said;
            lock (_lock)
            {
                if (_messages.Count == 0)
                    return;
                said = _messages.ToArray();
                _messages.Clear();
            }

            foreach (KeyValuePair<int, string> m in said)
            {
                if (m.Key == SayWarn)
                    Log.Warn(m.Value);
                else if (m.Key == SayMsg)
                    Log.Msg(m.Value);
                else if (Log.Detailed)
                    Log.Detail(m.Value);
            }
        }

        /// <summary>
        /// With the renderer, on the main thread: nothing more is read or written, and the thread
        /// is let go (a write under way finishes within the wait: its part-written file is never
        /// under the real name). What it had to say is said.
        /// </summary>
        public void Stop()
        {
            Thread thread;
            lock (_lock)
            {
                _stop = true;
                _reads.Clear();
                _writes.Clear();
                Monitor.PulseAll(_lock);
                thread = _thread;
            }

            if (thread != null && !thread.Join(1000))
                Log.Warn("flags: the loops' folder's thread did not stop within 1 s; it ends with the game (a background thread)");
            Tell();
        }

        /// <summary>The thread, started at the first ask; false (and off, said once) if it cannot be.</summary>
        private bool Started()
        {
            lock (_lock)
            {
                if (_thread != null)
                    return !_stop;
                if (_stop)
                    return false;

                try
                {
                    _thread = new Thread(Run) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "RealFlag loops" };
                    _thread.Start();
                    return true;
                }
                catch (Exception e)
                {
                    _thread = null;
                    _off = true;
                    Log.Warn("flags: the loops' folder's thread did not start (" + e.GetType().Name + ": " + e.Message + "); loops are made afresh this session");
                    return false;
                }
            }
        }

        // ---- the folder's thread: no Unity API, no Log ------------------------------------------

        private void Run()
        {
            try
            {
                while (true)
                {
                    Request read = null;
                    KeyValuePair<string, LoopData> write = default(KeyValuePair<string, LoopData>);
                    bool writing = false, tidy = false, prune = false;
                    lock (_lock)
                    {
                        while (!_stop && _reads.Count == 0 && _writes.Count == 0 && !(!_off && (!_tidied || _pruneDue)))
                            Monitor.Wait(_lock);
                        if (_stop)
                            return;

                        if (_reads.Count > 0)
                            read = _reads.Dequeue();
                        else if (_writes.Count > 0)
                        {
                            write = _writes.Dequeue();
                            writing = true;
                        }
                        else if (!_tidied)
                            tidy = true;
                        else
                        {
                            prune = true;
                            _pruneDue = false;
                        }
                    }

                    // Off: every read a miss, nothing written.
                    if (_off)
                    {
                        if (read != null)
                            Answered(read, null, 0.0);
                        continue;
                    }

                    try
                    {
                        if (read != null)
                            Read(read);
                        else if (writing)
                            Write(write.Key, write.Value);
                        else if (tidy)
                            Tidy();
                        else if (prune)
                            Prune();
                    }
                    catch (Exception e)
                    {
                        // One file another program holds for a moment (a virus scanner on a file
                        // just written) is that loop's miss, not the folder's failure: the folder
                        // is given up on at the session's MaxErrors-th error. Each is a miss or a
                        // write dropped either way, so the loop is made as ever.
                        int errors = Interlocked.Increment(ref _errors);
                        if (errors >= MaxErrors)
                            TurnOff(e);
                        else
                            Say(SayDetail, "flags: the loops' folder: " + e.GetType().Name + ": " + e.Message + " (error " + errors + " of the " + MaxErrors +
                                           " after which it is left alone this session); that loop is made afresh");
                        if (read != null)
                            Answered(read, null, 0.0);
                    }
                }
            }
            catch (Exception e)
            {
                // Never a thread gone with the cache still on: every ask is a miss from here.
                TurnOff(e);
            }
        }

        private void Answered(Request r, LoopData loop, double ms)
        {
            r.Data = r.Forgotten ? null : loop;
            r.Ms = ms;
            r.State = (int)(loop != null ? Answer.Hit : Answer.Miss);
        }

        private string PathOf(string identity)
        {
            return Path.Combine(_folder, LoopFile.FileName(identity));
        }

        /// <summary>One read: no file is a miss; a file refused is deleted (a miss, its reason in the detailed log).</summary>
        private void Read(Request r)
        {
            if (r.Forgotten)
            {
                Answered(r, null, 0.0);
                return;
            }

            r.Begun = true;
            Stopwatch clock = Stopwatch.StartNew();
            string path = PathOf(r.Identity);
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                Answered(r, null, 0.0);
                return;
            }

            if (info.Length > LoopFile.MaxFileBytes)
            {
                Refused(path, "longer (" + info.Length + " bytes) than any loop's file");
                Answered(r, null, 0.0);
                return;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (FileNotFoundException)
            {
                Answered(r, null, 0.0);
                return;
            }
            catch (DirectoryNotFoundException)
            {
                Answered(r, null, 0.0);
                return;
            }

            string why;
            LoopData loop = LoopFile.Read(bytes, r.Identity, _stamp, out why);
            if (loop == null)
            {
                Refused(path, why);
                Answered(r, null, 0.0);
                return;
            }

            // Its time is when it was last used (Prune goes by it). A file whose time cannot be set
            // (read-only) is still read as it is: only its place in the pruning's order is older.
            try
            {
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
            }
            catch (Exception)
            {
            }

            FolderWorks();
            Answered(r, loop, clock.Elapsed.TotalMilliseconds);
        }

        private void Refused(string path, string why)
        {
            bool deleted = TryDelete(new FileInfo(path));
            Say(SayDetail, "flags: a kept loop's file was refused and " + (deleted ? "deleted" : "left (it could not be deleted)") + " (" + why + "): " +
                           Path.GetFileName(path) + "; its loop is made afresh");
        }

        /// <summary>
        /// One file deleted; false, said once a session (detailed log), when it cannot be -- read-only,
        /// or held by another program. Never thrown: one such file threw out of the tidying every
        /// time it was begun again, and the session's errors (MaxErrors) were used up in a moment,
        /// the folder off -- every session, the file still there. It is skipped and left.
        /// </summary>
        private bool TryDelete(FileInfo f)
        {
            try
            {
                f.Delete();
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Stuck(f, e);
                return false;
            }
        }

        /// <summary>A file that could not be opened or deleted, skipped: said once a session, in the detailed log.</summary>
        private void Stuck(FileInfo f, Exception e)
        {
            if (_stuckSaid)
                return;
            _stuckSaid = true;
            Say(SayDetail, "flags: a file in the loops' folder could not be opened or deleted (" + e.GetType().Name + ": " + e.Message + "), left as it is: " +
                           f.Name + " (said once; any others are skipped alike)");
        }

        private static volatile bool _stuckSaid;

        /// <summary>One write: the whole file under a name of its own, then put in the loop's place (as SavedPatterns and SettingsXml replace theirs).</summary>
        private void Write(string identity, LoopData loop)
        {
            Directory.CreateDirectory(_folder);
            byte[] bytes = LoopFile.Write(loop, identity, _stamp);
            string path = PathOf(identity);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temp, bytes);
                if (!File.Exists(path))
                {
                    File.Move(temp, path);
                }
                else
                {
                    try
                    {
                        File.Replace(temp, path, null);
                    }
                    catch (Exception e) when (e is IOException || e is PlatformNotSupportedException || e is UnauthorizedAccessException)
                    {
                        // Copied over where File.Replace does not work: should it stop half way,
                        // the file's length and checksum refuse it at its next read.
                        File.Copy(temp, path, true);
                        File.Delete(temp);
                    }
                }
            }
            catch (Exception)
            {
                // The part-written copy goes; the error turns the cache off (Run).
                try
                {
                    if (File.Exists(temp))
                        File.Delete(temp);
                }
                catch (Exception)
                {
                }

                throw;
            }

            FolderWorks();
            lock (_lock)
                _pruneDue = true;
        }

        /// <summary>The loop files in the folder (by their extension, exactly), none if there is no folder.</summary>
        private FileInfo[] LoopFiles()
        {
            if (!Directory.Exists(_folder))
                return new FileInfo[0];

            var found = new List<FileInfo>();
            foreach (FileInfo f in new DirectoryInfo(_folder).GetFiles("*" + LoopFile.Extension))
            {
                if (f.Name.EndsWith(LoopFile.Extension, StringComparison.OrdinalIgnoreCase))
                    found.Add(f);
            }

            return found.ToArray();
        }

        /// <summary>A read is waiting: the tidying gives way to it (and is begun again later).</summary>
        private bool ReadsWaiting()
        {
            lock (_lock)
                return _reads.Count > 0 || _stop;
        }

        /// <summary>
        /// Once a session: part-written files left over, and files of another build (their first
        /// bytes read only), deleted. Gives way between files to a read that comes meanwhile (a
        /// city's load asks for its loops at once): begun again, from the start, once none waits.
        /// </summary>
        private void Tidy()
        {
            // A file that cannot be opened or deleted is skipped (TryDelete), never thrown.
            int parts = 0, older = 0, skipped = 0;
            if (Directory.Exists(_folder))
            {
                foreach (FileInfo f in new DirectoryInfo(_folder).GetFiles("*.tmp"))
                {
                    if (ReadsWaiting())
                        return;
                    if (TryDelete(f))
                        parts++;
                    else
                        skipped++;
                }

                var head = new byte[LoopFile.HeadBytes];
                foreach (FileInfo f in LoopFiles())
                {
                    if (ReadsWaiting())
                        return;
                    int got = 0;
                    try
                    {
                        using (var s = new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            while (got < head.Length)
                            {
                                int n = s.Read(head, got, head.Length - got);
                                if (n <= 0)
                                    break;
                                got += n;
                            }
                        }
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        Stuck(f, e);
                        skipped++;
                        continue;
                    }

                    if (!LoopFile.IsOurs(head, got, _stamp))
                    {
                        if (TryDelete(f))
                            older++;
                        else
                            skipped++;
                    }
                }
            }

            _tidied = true;
            lock (_lock)
                _pruneDue = true;
            if (parts + older + skipped > 0)
                Say(SayDetail, "flags: the loops' folder tidied -- " + older + " file(s) of another build of the mod and " + parts + " part-written file(s) deleted" +
                               (skipped > 0 ? ", " + skipped + " that could not be opened or deleted left" : ""));
        }

        /// <summary>While the folder is over its size, the file used longest ago goes.</summary>
        private void Prune()
        {
            // A file gone since the listing counts nothing (SizeOf); one that cannot be deleted is
            // skipped and still counted as there.
            FileInfo[] files = LoopFiles();
            long total = 0;
            foreach (FileInfo f in files)
                total += SizeOf(f);
            if (total <= MaxFolderBytes)
                return;

            Array.Sort(files, (a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
            int deleted = 0, skipped = 0;
            foreach (FileInfo f in files)
            {
                if (total <= MaxFolderBytes)
                    break;
                long bytes = SizeOf(f);
                if (TryDelete(f))
                {
                    total -= bytes;
                    deleted++;
                }
                else
                {
                    skipped++;
                }
            }

            Say(SayDetail, "flags: " + deleted + " kept loop(s) used longest ago deleted, to keep the loops' folder under " + MaxFolderBytes / (1024 * 1024) + " MB" +
                           (skipped > 0 ? " (" + skipped + " that could not be deleted left)" : ""));
        }

        /// <summary>A listed file's length; 0 for one gone (or unreadable) since the listing.</summary>
        private static long SizeOf(FileInfo f)
        {
            try
            {
                return f.Length;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return 0;
            }
        }

        /// <summary>The first read or write that worked this session: where the loops are kept, said once.</summary>
        private void FolderWorks()
        {
            if (_folderSaid)
                return;
            _folderSaid = true;
            Say(SayMsg, "flags: loops are kept in " + _folder + " -- each made once, read again in later sessions (up to " + MaxFolderBytes / (1024 * 1024) +
                        " MB; the folder may be deleted at any time)");
        }

        /// <summary>The folder cannot be used: off for the rest of the session, said once; reads waiting are misses, writes dropped.</summary>
        private void TurnOff(Exception e)
        {
            if (_off)
                return;
            _off = true;
            Say(SayWarn, "flags: the loops' folder cannot be used (" + e.GetType().Name + ": " + e.Message + "); loops are made afresh for the rest of this session -- " + _folder);

            lock (_lock)
            {
                while (_reads.Count > 0)
                    Answered(_reads.Dequeue(), null, 0.0);
                _writes.Clear();
            }
        }

        private void Say(int level, string text)
        {
            lock (_lock)
                _messages.Add(new KeyValuePair<int, string>(level, text));
        }
    }
}
