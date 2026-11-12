using System;
using System.IO;
using System.Text;

namespace RealFlag.Flags
{
    /// <summary>
    /// A made loop as a file of the loops' folder (LoopCache, 2026-11-12): written once a bake has
    /// made it, read back in a later session instead of making it again. PURE: bytes in, bytes out;
    /// the folder and the thread are LoopCache's.
    /// </summary>
    /// <remarks>
    /// The folder is a cache, never part of a save: ANY DOUBT ABOUT A FILE MEANS THE LOOP IS MADE
    /// AGAIN, as it always was. So <see cref="Read"/> takes a file only when every number in it is
    /// one this build could have made, for exactly the loop asked for (its whole identity, compared
    /// in full), by exactly this build (<paramref name="stamp"/>: the DLL's own id, new at every
    /// build -- a change to the cloth can never be played from an older file), whole (its length
    /// and a checksum over everything in it), and all numbers (no NaN or infinity). Anything else is
    /// refused with the reason, and the caller deletes it. Nothing is allocated from a number in
    /// the file before that number has been checked against the bytes really there and against a
    /// ceiling.
    ///
    /// The layout, little-endian (BinaryWriter's; the frames are copied as they lie in memory, so a
    /// machine that is not little-endian keeps no files):
    ///   "RFLP", the format (int), the stamp (16 bytes),
    ///   the identity (int count, UTF-8),
    ///   Columns, Rows, Frames, TilesX, TilesY, Width, Height (ints), FrameTime (float),
    ///   the box Min X Y Z, Max X Y Z, the slab's MinU MaxU MinV MaxV MinN MaxN, Out X Y Z (floats),
    ///   the text (int count, UTF-8),
    ///   the frames' length in bytes (long), the checksum (ulong),
    ///   the frames: LoopData.Data whole, all four channels, exactly LoopBake.Raw's bytes.
    /// The checksum is FNV-1a, 64 bits (Fowler, Noll and Vo), over every byte of the file but its
    /// own eight.
    /// </remarks>
    public static class LoopFile
    {
        /// <summary>The files' extension in the loops' folder.</summary>
        public const string Extension = ".loop";

        /// <summary>This layout's number: a file of another is refused (and made again).</summary>
        public const int Format = 1;

        private static readonly byte[] Magic = { (byte)'R', (byte)'F', (byte)'L', (byte)'P' };

        /// <summary>The bytes before the identity: magic, format, stamp.</summary>
        public const int HeadBytes = 4 + 4 + 16;

        /// <summary>The longest identity and text a file may hold, bytes (LoopMotion.Key is about 100, Describe about 300).</summary>
        public const int MaxIdentityBytes = 2048, MaxTextBytes = 4096;

        /// <summary>The most particles a loop's cloth has each way: FlagCloth.GridFor at LoopMotion.MaxDetail (detail + 1).</summary>
        public static readonly int MaxSide = LoopMotion.MaxDetail + 1;

        /// <summary>The most frames a loop has: the longest loop (MaxSeconds x Longest) at the shorter frame time (a frame every 2 steps), and one over.</summary>
        public static readonly int MaxFrames = (int)Math.Ceiling(LoopMotion.MaxSeconds * LoopMotion.Longest / LoopMotion.FrameTime(0f)) + 1;

        /// <summary>The widest or tallest texture a file may claim, texels.</summary>
        public const int MaxTextureSide = 8192;

        /// <summary>A hard ceiling on the frames' bytes, whatever the numbers above allow (a High loop is 1-2.5 MB).</summary>
        public const long MaxPayloadBytes = 32L * 1024 * 1024;

        /// <summary>No file is longer than this (the frames' ceiling and the longest header).</summary>
        public const long MaxFileBytes = MaxPayloadBytes + 64 * 1024;

        // ---- names -------------------------------------------------------------------------

        /// <summary>
        /// Everything that makes a loop: its key (LoopMotion.Key: the cloth, its breeze, the
        /// detail, self-collision, the take) and every argument LoopBake takes besides it (the
        /// frames' edges smoothed or not).
        /// </summary>
        public static string Identity(string key, bool smoothEdges)
        {
            return key + "|edges " + (smoothEdges ? 1 : 0);
        }

        /// <summary>The file of a loop in the folder: its identity's FNV-1a hash in hex. Two identities may share a name; the identity inside the file is what is compared.</summary>
        public static string FileName(string identity)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(identity ?? "");
            return Fnv1a(bytes, 0, bytes.Length, FnvOffset).ToString("x16") + Extension;
        }

        // ---- the checksum ------------------------------------------------------------------

        /// <summary>FNV-1a's 64-bit offset basis and prime.</summary>
        public const ulong FnvOffset = 14695981039346656037UL, FnvPrime = 1099511628211UL;

        /// <summary>FNV-1a, 64 bits, over <paramref name="count"/> bytes from <paramref name="start"/>, carried on from <paramref name="hash"/>.</summary>
        public static ulong Fnv1a(byte[] bytes, int start, int count, ulong hash)
        {
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                hash ^= bytes[i];
                hash *= FnvPrime;
            }

            return hash;
        }

        /// <summary>The file's checksum: every byte but the checksum's own eight at <paramref name="at"/>.</summary>
        private static ulong Checksum(byte[] file, int at)
        {
            ulong hash = Fnv1a(file, 0, at, FnvOffset);
            return Fnv1a(file, at + 8, file.Length - at - 8, hash);
        }

        // ---- writing -----------------------------------------------------------------------

        /// <summary>
        /// The file of a made loop. Checks nothing of the loop (a test writes one with a NaN in
        /// it to see it refused); throws on an identity or text too long to be read back, and on a
        /// machine that is not little-endian.
        /// </summary>
        public static byte[] Write(LoopData loop, string identity, Guid stamp)
        {
            if (!BitConverter.IsLittleEndian)
                throw new NotSupportedException("the loops' files are little-endian, this machine is not");

            byte[] id = Encoding.UTF8.GetBytes(identity ?? "");
            byte[] text = Encoding.UTF8.GetBytes(loop.Text ?? "");
            if (id.Length > MaxIdentityBytes || text.Length > MaxTextBytes)
                throw new ArgumentException("a loop's identity or text is too long for its file");

            long payload = (long)loop.Data.Length * 4;
            var head = new MemoryStream();
            var w = new BinaryWriter(head);
            w.Write(Magic);
            w.Write(Format);
            w.Write(stamp.ToByteArray());
            w.Write(id.Length);
            w.Write(id);
            w.Write(loop.Columns);
            w.Write(loop.Rows);
            w.Write(loop.Frames);
            w.Write(loop.TilesX);
            w.Write(loop.TilesY);
            w.Write(loop.Width);
            w.Write(loop.Height);
            w.Write(loop.FrameTime);
            w.Write(loop.MinX); w.Write(loop.MinY); w.Write(loop.MinZ);
            w.Write(loop.MaxX); w.Write(loop.MaxY); w.Write(loop.MaxZ);
            w.Write(loop.MinU); w.Write(loop.MaxU);
            w.Write(loop.MinV); w.Write(loop.MaxV);
            w.Write(loop.MinN); w.Write(loop.MaxN);
            w.Write(loop.OutX); w.Write(loop.OutY); w.Write(loop.OutZ);
            w.Write(text.Length);
            w.Write(text);
            w.Write(payload);
            int checksumAt = (int)head.Length;
            w.Write(0UL);
            w.Flush();

            byte[] header = head.ToArray();
            var file = new byte[header.Length + payload];
            Buffer.BlockCopy(header, 0, file, 0, header.Length);
            Buffer.BlockCopy(loop.Data, 0, file, header.Length, (int)payload);

            byte[] sum = BitConverter.GetBytes(Checksum(file, checksumAt));
            Buffer.BlockCopy(sum, 0, file, checksumAt, 8);
            return file;
        }

        // ---- reading -----------------------------------------------------------------------

        /// <summary>
        /// The first <see cref="HeadBytes"/> of a file are a loop file's of this format made by
        /// the build <paramref name="stamp"/>: the loops' folder's housekeeping reads only these
        /// of each file, and deletes those that are not.
        /// </summary>
        public static bool IsOurs(byte[] head, int count, Guid stamp)
        {
            if (head == null || count < HeadBytes || head.Length < HeadBytes)
                return false;
            for (int i = 0; i < Magic.Length; i++)
            {
                if (head[i] != Magic[i])
                    return false;
            }

            return BitConverter.ToInt32(head, 4) == Format && StampAt(head, 8) == stamp;
        }

        private static Guid StampAt(byte[] bytes, int at)
        {
            var g = new byte[16];
            Buffer.BlockCopy(bytes, at, g, 0, 16);
            return new Guid(g);
        }

        /// <summary>
        /// The loop in <paramref name="file"/> if it is the one asked for (<paramref name="identity"/>),
        /// by this build (<paramref name="stamp"/>), whole and sound; else null, and
        /// <paramref name="why"/> says why.
        /// </summary>
        public static LoopData Read(byte[] file, string identity, Guid stamp, out string why)
        {
            why = null;
            if (!BitConverter.IsLittleEndian)
                return Refuse("this machine is not little-endian", out why);
            if (file == null || file.Length == 0)
                return Refuse("empty", out why);
            if (file.Length > MaxFileBytes)
                return Refuse("longer (" + file.Length + " bytes) than any loop's file", out why);
            if (file.Length < HeadBytes)
                return Refuse("cut short in its header (" + file.Length + " bytes)", out why);

            for (int i = 0; i < Magic.Length; i++)
            {
                if (file[i] != Magic[i])
                    return Refuse("not a loop's file (its first bytes)", out why);
            }

            int format = BitConverter.ToInt32(file, 4);
            if (format != Format)
                return Refuse("of another format (" + format + ", this build reads " + Format + ")", out why);
            if (StampAt(file, 8) != stamp)
                return Refuse("made by another build of the mod", out why);

            int at = HeadBytes;
            int count;
            if (!Int(file, ref at, out count))
                return Truncated(file, out why);
            if (count < 0 || count > MaxIdentityBytes)
                return Refuse("its identity's length is out of range (" + count + ")", out why);
            if (file.Length - at < count)
                return Truncated(file, out why);
            string inFile = Encoding.UTF8.GetString(file, at, count);
            at += count;
            if (!string.Equals(inFile, identity, StringComparison.Ordinal))
                return Refuse("of another loop (its identity differs)", out why);

            // The sizes, the frame time, the box, the slab, the axis: 7 ints and 16 floats.
            const int numbers = 7 * 4 + 16 * 4;
            if (file.Length - at < numbers)
                return Truncated(file, out why);
            int columns = BitConverter.ToInt32(file, at); at += 4;
            int rows = BitConverter.ToInt32(file, at); at += 4;
            int frames = BitConverter.ToInt32(file, at); at += 4;
            int tilesX = BitConverter.ToInt32(file, at); at += 4;
            int tilesY = BitConverter.ToInt32(file, at); at += 4;
            int width = BitConverter.ToInt32(file, at); at += 4;
            int height = BitConverter.ToInt32(file, at); at += 4;
            var f = new float[16];
            for (int i = 0; i < f.Length; i++, at += 4)
                f[i] = BitConverter.ToSingle(file, at);

            if (!Int(file, ref at, out count))
                return Truncated(file, out why);
            if (count < 0 || count > MaxTextBytes)
                return Refuse("its text's length is out of range (" + count + ")", out why);
            if (file.Length - at < count)
                return Truncated(file, out why);
            string text = Encoding.UTF8.GetString(file, at, count);
            at += count;

            if (file.Length - at < 16)
                return Truncated(file, out why);
            long payload = BitConverter.ToInt64(file, at); at += 8;
            int checksumAt = at;
            ulong checksum = BitConverter.ToUInt64(file, at); at += 8;

            // The sizes: each in range, and agreeing with each other as LoopBake.Cut makes them.
            float frameTime = f[0];
            string bad = SizesProblem(columns, rows, frames, tilesX, tilesY, width, height, frameTime, payload);
            if (bad != null)
                return Refuse(bad, out why);

            long left = file.Length - at;
            if (left < payload)
                return Refuse("cut short in its frames (" + left + " of " + payload + " bytes)", out why);
            if (left > payload)
                return Refuse("longer than its frames (" + (left - payload) + " bytes past them)", out why);

            for (int i = 0; i < f.Length; i++)
            {
                if (float.IsNaN(f[i]) || float.IsInfinity(f[i]))
                    return Refuse("a number of its header is not a number", out why);
            }

            // Min before max: the box (1..6) and the slab (7..12).
            if (f[1] > f[4] || f[2] > f[5] || f[3] > f[6] || f[7] > f[8] || f[9] > f[10] || f[11] > f[12])
                return Refuse("its box or its extents are inside out", out why);

            if (Checksum(file, checksumAt) != checksum)
                return Refuse("its checksum does not match (the file is damaged)", out why);

            // Now known to be as many bytes as the sizes say, and under the ceiling.
            var data = new float[payload / 4];
            Buffer.BlockCopy(file, at, data, 0, (int)payload);
            for (int i = 0; i < data.Length; i++)
            {
                if (float.IsNaN(data[i]) || float.IsInfinity(data[i]))
                    return Refuse("a number of its frames is not a number", out why);
            }

            return new LoopData(columns, rows, frames, tilesX, tilesY, width, height, frameTime,
                                f[1], f[2], f[3], f[4], f[5], f[6],
                                f[7], f[8], f[9], f[10], f[11], f[12],
                                f[13], f[14], f[15], text, data);
        }

        /// <summary>What is wrong with a file's sizes, or null: as LoopBake.Cut makes them, in range, and the frames' bytes they give.</summary>
        private static string SizesProblem(int columns, int rows, int frames, int tilesX, int tilesY, int width, int height, float frameTime, long payload)
        {
            if (columns < 2 || columns > MaxSide || rows < 2 || rows > MaxSide)
                return "its grid is out of range (" + columns + "x" + rows + ")";
            if (frames < 2 || frames > MaxFrames)
                return "its frames are out of range (" + frames + ")";
            if (frameTime != LoopMotion.FrameTime(0f) && frameTime != LoopMotion.FrameTime(LoopMotion.BigLength))
                return "its frame time is none a loop has";
            if (tilesX < 1 || tilesX > frames || tilesY != (frames + tilesX - 1) / tilesX)
                return "its tiles do not hold its frames (" + tilesX + " x " + tilesY + " for " + frames + ")";
            if (width != (long)tilesX * columns || height != (long)tilesY * rows || width > MaxTextureSide || height > MaxTextureSide)
                return "its texture's size disagrees with its tiles (" + width + "x" + height + ")";
            long bytes = (long)width * height * 16;
            if (bytes > MaxPayloadBytes)
                return "its frames are larger than any loop's (" + bytes + " bytes)";
            if (payload != bytes)
                return "its frames' length (" + payload + " bytes) disagrees with its texture's (" + bytes + ")";
            return null;
        }

        private static bool Int(byte[] file, ref int at, out int value)
        {
            if (file.Length - at < 4)
            {
                value = 0;
                return false;
            }

            value = BitConverter.ToInt32(file, at);
            at += 4;
            return true;
        }

        private static LoopData Truncated(byte[] file, out string why)
        {
            return Refuse("cut short in its header (" + file.Length + " bytes)", out why);
        }

        private static LoopData Refuse(string reason, out string why)
        {
            why = reason;
            return null;
        }
    }
}
